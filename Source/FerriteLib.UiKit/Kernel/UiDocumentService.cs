using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The bounded, disposable owner of the library's document half: which file each consumer page came from,
/// which hosts depend on which file, the last valid version of every file, and the candidate/commit
/// discipline that turns "somebody saved an XML file" into "the open windows that read it are showing the
/// new tree".
/// <para>
/// <b>Ownership.</b> The service owns the state, not any window session: a host attaches, and a host that
/// closes detaches, but the last valid version of a document outlives every window that read it. Nothing
/// here is static or process-wide, and everything that grows is capped
/// (<see cref="MaxDocuments"/>, <see cref="MaxDependencies"/>, <see cref="MaxReports"/>).
/// </para>
/// <para>
/// <b>The watcher thread never touches the game.</b> <see cref="Watch"/> arms a <see cref="FileSystemWatcher"/>
/// whose only action is <see cref="Signal"/> - a set insertion under a lock - and the engine, the theme,
/// the business model and every Verse/Unity type are reached only from <see cref="Pump"/>, which the host
/// calls at its own frame boundary. Repeated notifications coalesce into one pending entry per document,
/// and a dropped notification (a watcher buffer overflow, a stopped watcher, a file replaced while the
/// process was not looking) is recovered by <see cref="Reload"/> or <see cref="ReloadAll"/>.
/// </para>
/// <para>
/// <b>Atomicity is per document, across every host that depends on it.</b> One change batch is the set of
/// changes to one document since the last pump: every affected host validates the candidate first, and if
/// any of them refuses it, none of them commits - there is no half-updated batch. A failure in a different
/// document is a different batch and does not block this one.
/// </para>
/// <para>
/// <b>Last-known-good.</b> The external file is preferred; when it does not exist the embedded fallback
/// text travels with the source and is what the host gets. From then on a failed candidate keeps the last
/// accepted version, and the failure is reported once per refusing version.
/// </para>
/// </summary>
public sealed class UiDocumentService : IDisposable
{
    /// <summary>Registered sources one service accepts. A bounded table, not a cache that grows.</summary>
    public const int MaxDocuments = 64;

    /// <summary>Host-to-document dependencies one service accepts.</summary>
    public const int MaxDependencies = 256;

    /// <summary>Reports kept for diagnostics; the list is a ring, not a log.</summary>
    public const int MaxReports = 32;

    /// <summary>Largest document read; a bigger file is refused instead of loaded.</summary>
    public const int MaxDocumentBytes = 1048576;

#if FER_DEV
    private const bool DevBuildAutoWatchDefault = true;
#else
    private const bool DevBuildAutoWatchDefault = false;
#endif

    private readonly object gate = new object();
    private readonly Dictionary<string, DocumentState> documents = new(StringComparer.Ordinal);
    private readonly List<Dependency> dependencies = new();
    private readonly HashSet<string> pending = new(StringComparer.Ordinal);
    private readonly List<UiReloadReport> reports = new();
    private bool autoWatch;
    private bool overflowed;
    private bool disposed;

    // Re-arm attempts are made on the frame path, so they are throttled: a document whose directory did
    // not exist yet must get another chance, without stat-ing every registered path once per frame.
    private int reArmCountdown;

    /// <summary>
    /// Test seam, instance-level (never process-wide): the harness forces one host's document prepare or
    /// commit to fail. A prepare-phase fault exercises the style half of the all-or-nothing rule, which a
    /// valid style document cannot fail on its own; a commit-phase fault exercises the batch rollback,
    /// which a production commit cannot reach once every host validated. Null in production.
    /// </summary>
    internal Func<UiHost, string, string>? DocumentFaultOverride;

    internal const string PreparePhase = "prepare";

    internal const string CommitPhase = "commit";

    /// <summary>Pumps between re-arm attempts on the frame path.</summary>
    private const int ReArmPumpInterval = 30;

    /// <summary>
    /// Creates the service. <paramref name="autoWatch"/> overrides the build's default: automatic watching
    /// is on by default in <c>FER_DEV</c> builds and off in release builds, and a consumer may switch it
    /// either way at any time through <see cref="AutoWatch"/>. Construction itself starts no thread and
    /// reads no file; <see cref="Add"/> does the first read.
    /// </summary>
    public UiDocumentService(bool? autoWatch = null)
    {
        this.autoWatch = autoWatch ?? DefaultAutoWatch;
    }

    /// <summary>
    /// Whether automatic watching is on unless a caller says otherwise: true for an <c>FER_DEV</c> build,
    /// false for a release build. The manual path (<see cref="Reload"/>, <see cref="ReloadAll"/>) is
    /// available in both.
    /// </summary>
    public static bool DefaultAutoWatch => DevBuildAutoWatchDefault;

    /// <summary>
    /// Automatic file watching. Turning it on arms a watcher for every registered document whose directory
    /// exists; turning it off disarms them. The worker thread only posts change signals either way.
    /// </summary>
    public bool AutoWatch
    {
        get => autoWatch;
        set
        {
            EnsureAlive();
            // Deliberately not an early return on an unchanged value: re-setting AutoWatch to true is the
            // documented re-arm after a directory that did not exist yet, so the same value must still do
            // the work. RefreshWatchers arms only the documents that hold no watcher.
            autoWatch = value;
            RefreshWatchers();
        }
    }

    /// <summary>True once <see cref="Dispose"/> ran.</summary>
    public bool IsDisposed => disposed;

    /// <summary>Registered document sources.</summary>
    public int DocumentCount => documents.Count;

    /// <summary>Attached hosts holding a dependency register.</summary>
    public int DependencyCount => dependencies.Count;

    /// <summary>
    /// Registers one consumer-owned file, reading its first valid version (external file first, embedded
    /// fallback text when the file is absent or unreadable at this point) and arming its watcher when
    /// <see cref="AutoWatch"/> is on.
    /// <para>
    /// Returns false when the service already holds <see cref="MaxDocuments"/> sources - a bound refused
    /// rather than silently ignored. A duplicate <paramref name="source"/> id or an invalid source is a
    /// caller error and throws.
    /// </para>
    /// </summary>
    public bool Add(UiDocumentSource source, string embeddedFallbackXml = "")
    {
        EnsureAlive();
        if (documents.ContainsKey(source.Id))
        {
            throw new ArgumentException("A document source with id '" + source.Id + "' is already registered.", nameof(source));
        }

        if (documents.Count >= MaxDocuments) return false;

        var state = new DocumentState(source, embeddedFallbackXml ?? "");
        ReadFirstVersion(state);

        documents.Add(source.Id, state);
        if (autoWatch) Watch(state);
        return true;
    }

    /// <summary>
    /// Unregisters one source, disarming its watcher. Dependencies that named it are released (their hosts
    /// keep whatever tree they already have). Returns false when the id was never registered.
    /// </summary>
    public bool Remove(string documentId)
    {
        EnsureAlive();
        if (documentId == null || !documents.TryGetValue(documentId, out DocumentState? state)) return false;

        Unwatch(state);
        documents.Remove(documentId);
        lock (gate)
        {
            pending.Remove(documentId);
        }

        for (int i = dependencies.Count - 1; i >= 0; i--)
        {
            Dependency dependency = dependencies[i];
            if (string.Equals(dependency.LayoutId, documentId, StringComparison.Ordinal)) dependency.LayoutId = null;
            if (string.Equals(dependency.StyleId, documentId, StringComparison.Ordinal)) dependency.StyleId = null;
            if (dependency.LayoutId == null && dependency.StyleId == null) dependencies.RemoveAt(i);
        }

        return true;
    }

    /// <summary>True when this document currently has an armed watcher.</summary>
    public bool IsWatching(string documentId)
    {
        return !disposed
            && documentId != null
            && documents.TryGetValue(documentId, out DocumentState? state)
            && state.Watcher != null;
    }

    /// <summary>
    /// Posts one change signal for a document. This is the whole watcher thread's job and it is safe from
    /// any thread: it touches one locked set, never Verse, never Unity, never a business object. It returns
    /// false for an unknown id or after <see cref="Dispose"/>, so a late watcher callback cannot resurrect
    /// a disposed service. Repeated signals for one document coalesce into a single pending entry.
    /// </summary>
    public bool Signal(string documentId)
    {
        if (disposed || documentId == null) return false;
        lock (gate)
        {
            if (disposed || !documents.ContainsKey(documentId)) return false;
            pending.Add(documentId);
            return true;
        }
    }

    /// <summary>True when a signal is waiting for a pump (a watcher callback landed since the last one).</summary>
    public bool HasPending
    {
        get
        {
            lock (gate)
            {
                return pending.Count > 0 || overflowed;
            }
        }
    }

    /// <summary>
    /// The main-thread commit boundary: reads every document a signal named, validates each batch in full,
    /// and commits the batches that pass. Called by an attached host at its own frame start, so the GUI
    /// pass that is about to run already sees the new tree and the pass that just finished was never
    /// mutated under itself. Safe to call from a consumer as well; it is idempotent when nothing is pending.
    /// Returns true when at least one batch committed. Never throws once disposed - a frame path must not
    /// introduce an exception into a GUI pass.
    /// </summary>
    public bool Pump()
    {
        if (disposed) return false;
        if (autoWatch) AttemptReArm();

        List<string> batch;
        lock (gate)
        {
            if (overflowed)
            {
                // A watcher buffer overflow means notifications were lost. Re-check every watched document
                // rather than pretending nothing moved; the harmless cost is a re-read of unchanged files.
                overflowed = false;
                foreach (DocumentState watched in documents.Values)
                {
                    if (watched.Watcher != null) pending.Add(watched.Source.Id);
                }
            }

            batch = new List<string>(pending);
            pending.Clear();
        }

        bool committed = false;
        for (int i = 0; i < batch.Count; i++)
        {
            UiReloadReport? report = ReloadCore(batch[i]);
            if (report != null && report.Accepted) committed = true;
        }

        return committed;
    }

    /// <summary>
    /// Manual reload of one document: the entry point that works with watching off and that recovers a
    /// dropped notification. Same candidate/validate/commit path as <see cref="Pump"/>, and a signal that
    /// was already pending for this document is consumed by it. Returns null for an unknown id.
    /// </summary>
    public UiReloadReport? Reload(string documentId)
    {
        EnsureAlive();
        if (documentId == null) return null;
        RefreshWatchers();
        lock (gate)
        {
            pending.Remove(documentId);
        }

        return ReloadCore(documentId);
    }

    /// <summary>Manual reload of every registered document, in registration order.</summary>
    public IReadOnlyList<UiReloadReport> ReloadAll()
    {
        EnsureAlive();
        RefreshWatchers();
        var results = new List<UiReloadReport>();
        var ids = new List<string>(documents.Keys);
        for (int i = 0; i < ids.Count; i++)
        {
            lock (gate)
            {
                pending.Remove(ids[i]);
            }

            UiReloadReport? report = ReloadCore(ids[i]);
            if (report != null) results.Add(report);
        }

        return results;
    }

    /// <summary>
    /// Binds one host to the documents it reads and hands it the current valid version of each - the
    /// dependency tracking clause, and what makes "a shared style updates every window that actually
    /// depends on it" checkable rather than aspirational. <paramref name="layoutId"/> and
    /// <paramref name="styleId"/> may both be given; at least one must be, otherwise there is nothing to
    /// track.
    /// <para>
    /// Re-attaching a host replaces its previous dependency. Returns false when the dependency bound is
    /// reached or a named document is not registered.
    /// </para>
    /// </summary>
    public bool Attach(UiHost host, string? layoutId, string? styleId = null)
    {
        EnsureAlive();
        if (host == null) throw new ArgumentNullException(nameof(host));
        if (layoutId == null && styleId == null)
        {
            throw new ArgumentException("A host dependency needs at least one document id.", nameof(layoutId));
        }

        DocumentState? layout = layoutId == null ? null : RequireDocument(layoutId);
        DocumentState? style = styleId == null ? null : RequireDocument(styleId);
        if ((layoutId != null && layout == null) || (styleId != null && style == null)) return false;

        Detach(host);
        if (dependencies.Count >= MaxDependencies) return false;

        var dependency = new Dependency(host, layoutId, styleId);
        dependencies.Add(dependency);
        host.AttachDocumentService(this);

        if (layout != null && layout.GoodVersion.Length > 0 && layout.GoodLayout != null)
        {
            if (host.TryPrepareLayoutCandidate(layout.GoodLayout, out string element, out string reason))
            {
                host.CommitLayoutCandidate(layout.GoodLayout);
            }
            else
            {
                AddReport(new UiReloadReport(
                    layout.Source.Id, layout.Source.Kind, layout.Source.Path, layout.GoodVersion,
                    accepted: false, skipped: false, duplicate: false,
                    element, "the initial version could not be applied to host '" + host.Source + "': " + reason,
                    1, 0));
            }
        }

        if (style != null && style.GoodVersion.Length > 0 && style.GoodStyle != null)
        {
            host.CommitStyleCandidate(style.GoodStyle);
        }

        return true;
    }

    /// <summary>
    /// Releases every dependency this host held. A closed host must not keep the service referencing it,
    /// and the documents themselves stay exactly where they are - the service owns them, not the session.
    /// </summary>
    public void Detach(UiHost host)
    {
        if (host == null) return;
        for (int i = dependencies.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(dependencies[i].Host, host)) dependencies.RemoveAt(i);
        }
    }

    /// <summary>
    /// Reports produced so far, oldest first, capped at <see cref="MaxReports"/>. A snapshot: the caller
    /// cannot mutate the service's own list.
    /// </summary>
    public IReadOnlyList<UiReloadReport> Reports
    {
        get
        {
            lock (gate)
            {
                return new List<UiReloadReport>(reports);
            }
        }
    }

    /// <summary>The most recent report, or null when nothing has been attempted yet.</summary>
    public UiReloadReport? LastReport
    {
        get
        {
            lock (gate)
            {
                return reports.Count > 0 ? reports[reports.Count - 1] : null;
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        lock (gate)
        {
            disposed = true;
            pending.Clear();
        }

        foreach (DocumentState state in documents.Values)
        {
            Unwatch(state);
        }

        documents.Clear();
        dependencies.Clear();
        lock (gate)
        {
            reports.Clear();
        }
    }

    // --- candidate / commit ---------------------------------------------------------------------

    private UiReloadReport? ReloadCore(string documentId)
    {
        if (!documents.TryGetValue(documentId, out DocumentState? state)) return null;

        var affected = new List<Dependency>();
        for (int i = 0; i < dependencies.Count; i++)
        {
            Dependency dependency = dependencies[i];
            if (dependency.Host.SessionDisposed) continue;
            if (string.Equals(dependency.LayoutId, documentId, StringComparison.Ordinal)
                || string.Equals(dependency.StyleId, documentId, StringComparison.Ordinal))
            {
                affected.Add(dependency);
            }
        }

        Candidate candidate = ReadCandidate(state);
        if (candidate.Failure.Length > 0)
        {
            bool duplicate = string.Equals(candidate.Version, state.LastFailedVersion, StringComparison.Ordinal);
            state.LastFailedVersion = candidate.Version;
            var failure = new UiReloadReport(
                state.Source.Id, state.Source.Kind, state.Source.Path, candidate.Version,
                accepted: false, skipped: false, duplicate, "", candidate.Failure,
                affected.Count, 0);
            if (!duplicate) AddReport(failure);
            return failure;
        }

        if (state.GoodVersion.Length > 0 && string.Equals(candidate.Version, state.GoodVersion, StringComparison.Ordinal))
        {
            return new UiReloadReport(
                state.Source.Id, state.Source.Kind, state.Source.Path, candidate.Version,
                accepted: false, skipped: true, duplicate: false, "",
                "the bytes are unchanged since the last accepted version",
                affected.Count, 0);
        }

        // Validate the whole batch before committing any of it. A layout candidate is checked against the
        // host's own creation-time contract, because "it parsed" is not "every affected window can draw
        // it"; a style candidate is pre-flighted by applying it over a throwaway theme clone, because a
        // valid style document has no per-host element contract to fail on and the batch rule still has to
        // hold for it. Every affected dependency goes through this loop, whatever kind it names.
        string failureElement = "";
        string failureReason = "";
        for (int i = 0; i < affected.Count && failureReason.Length == 0; i++)
        {
            Dependency dependency = affected[i];
            if (string.Equals(dependency.LayoutId, documentId, StringComparison.Ordinal))
            {
                // The planted fault sits inside the dependency's own kind branch on purpose: a host can only
                // refuse through the pre-check its kind uses, so a lane that removes a branch removes the
                // refusal with it and cannot pass by accident.
                string planted = PlantedPrepareFailure(dependency.Host);
                UiLayoutManifest? layout = candidate.Layout;
                if (planted.Length > 0)
                {
                    failureReason = "host '" + dependency.Host.Source + "': " + planted;
                }
                else if (layout == null)
                {
                    failureReason = "the candidate carried no layout document";
                }
                else if (!dependency.Host.TryPrepareLayoutCandidate(layout, out failureElement, out failureReason))
                {
                    failureReason = "host '" + dependency.Host.Source + "': " + failureReason;
                }
            }
            else
            {
                string planted = PlantedPrepareFailure(dependency.Host);
                UiStyleDocument? style = candidate.Style;
                if (planted.Length > 0)
                {
                    failureReason = "host '" + dependency.Host.Source + "': " + planted;
                }
                else if (style == null)
                {
                    failureReason = "the candidate carried no style document";
                }
                else if (!dependency.Host.TryPrepareStyleCandidate(style, out failureElement, out failureReason))
                {
                    failureReason = "host '" + dependency.Host.Source + "': " + failureReason;
                }
            }
        }

        if (failureReason.Length > 0)
        {
            return RefuseBatch(state, candidate, failureElement, failureReason, affected.Count);
        }

        // Every affected host accepted the candidate, so commit them. A commit cannot fail after validation
        // today, but it is not trusted either: each host is captured before it is committed, and a failure
        // anywhere rolls the already-committed hosts back. That is what keeps "no half-updated batch" true
        // for style documents and for any host that can fail at commit time later.
        var applied = new List<AppliedCommit>();
        int committed = 0;
        string commitFailure = "";
        for (int i = 0; i < affected.Count && commitFailure.Length == 0; i++)
        {
            Dependency dependency = affected[i];
            applied.Add(new AppliedCommit(dependency.Host, dependency.Host.CaptureDocumentRollback()));
            try
            {
                string planted = DocumentFaultOverride?.Invoke(dependency.Host, CommitPhase) ?? "";
                if (planted.Length > 0)
                {
                    commitFailure = "host '" + dependency.Host.Source + "': " + planted;
                    break;
                }

                if (string.Equals(dependency.LayoutId, documentId, StringComparison.Ordinal))
                {
                    dependency.Host.CommitLayoutCandidate(candidate.Layout!);
                }
                else if (candidate.Style != null)
                {
                    dependency.Host.CommitStyleCandidate(candidate.Style);
                }

                committed++;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                commitFailure = "host '" + dependency.Host.Source + "': the commit threw "
                    + ex.GetType().Name + ": " + ex.Message;
            }
        }

        if (commitFailure.Length > 0)
        {
            for (int i = applied.Count - 1; i >= 0; i--)
            {
                applied[i].Host.RestoreDocumentRollback(applied[i].Rollback);
            }

            return RefuseBatch(
                state, candidate, "", commitFailure + "; the whole batch was rolled back", affected.Count);
        }

        state.Accept(candidate);
        var accepted = new UiReloadReport(
            state.Source.Id, state.Source.Kind, state.Source.Path, candidate.Version,
            accepted: true, skipped: false, duplicate: false, "",
            committed == 0
                ? "accepted; no live host depends on this document yet"
                : "committed to every affected host",
            affected.Count, committed);
        AddReport(accepted);
        return accepted;
    }

    /// <summary>The planted prepare-phase refusal for one host, or "" when the seam is off or silent.</summary>
    private string PlantedPrepareFailure(UiHost host)
    {
        return DocumentFaultOverride?.Invoke(host, PreparePhase) ?? "";
    }

    /// <summary>
    /// Records one refused batch and returns its report - the shared tail of the validation refusal and the
    /// commit-and-rollback refusal. Failures stay deduplicated per refusing version on both paths.
    /// </summary>
    private UiReloadReport RefuseBatch(
        DocumentState state, Candidate candidate, string element, string reason, int affectedCount)
    {
        bool duplicate = string.Equals(candidate.Version, state.LastFailedVersion, StringComparison.Ordinal);
        state.LastFailedVersion = candidate.Version;
        var refused = new UiReloadReport(
            state.Source.Id, state.Source.Kind, state.Source.Path, candidate.Version,
            accepted: false, skipped: false, duplicate, element, reason,
            affectedCount, 0);
        if (!duplicate) AddReport(refused);
        return refused;
    }

    private void ReadFirstVersion(DocumentState state)
    {
        Candidate candidate = ReadCandidate(state);
        if (candidate.Failure.Length == 0)
        {
            state.Accept(candidate);
            return;
        }

        state.LastFailedVersion = candidate.Version;
        AddReport(new UiReloadReport(
            state.Source.Id, state.Source.Kind, state.Source.Path, candidate.Version,
            accepted: false, skipped: false, duplicate: false, "", candidate.Failure, 0, 0));
    }

    private static Candidate ReadCandidate(DocumentState state)
    {
        Candidate external = ReadExternal(state);
        // A parsed candidate carries an empty (non-null) Failure, a refused one carries a message, and only
        // the "no external file" case is the default struct - which is what sends us to the fallback.
        if (external.Failure != null) return external;
        return ReadEmbedded(state, "");
    }

    private static Candidate ReadExternal(DocumentState state)
    {
        UiDocumentSource source = state.Source;
        string path = source.Path;

        bool exists;
        try
        {
            exists = File.Exists(path);
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return Candidate.Refused(HashText("path:" + ex.Message), "the path could not be queried: " + ex.Message);
        }

        if (!exists) return default;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return Candidate.Refused(HashText("read:" + ex.Message), "the file could not be read: " + ex.Message);
        }

        if (bytes.Length > MaxDocumentBytes)
        {
            return Candidate.Refused(
                HashText("oversized:" + bytes.Length.ToString(CultureInfo.InvariantCulture)),
                "the file is " + bytes.Length.ToString(CultureInfo.InvariantCulture) + " bytes, above the "
                + MaxDocumentBytes.ToString(CultureInfo.InvariantCulture) + "-byte bound");
        }

        string version = Hash(bytes);
        if (source.Kind == UiDocumentKind.Layout)
        {
            try
            {
                return Candidate.LayoutVersion(version, UiLayoutManifest.ParseFile(path));
            }
            catch (Exception ex) when (IsParseFailure(ex))
            {
                return Candidate.Refused(version, "file '" + path + "' did not parse: " + ex.Message);
            }
        }

        UiStyleDocument style = UiStyleDocument.ParseFile(path);
        if (style.StructurallyInvalid)
        {
            return Candidate.Refused(version, "file '" + path + "' did not parse: " + style.Issues[0]);
        }

        return Candidate.StyleVersion(version, style);
    }

    private static Candidate ReadEmbedded(DocumentState state, string externalFailure)
    {
        string xml = state.EmbeddedFallbackXml;
        string version = "embedded:" + HashText(xml);
        string prefix = externalFailure.Length > 0 ? externalFailure + "; " : "";
        if (xml.Trim().Length == 0)
        {
            return Candidate.Refused(version, prefix + "no external file exists and no embedded fallback was declared");
        }

        if (state.Source.Kind == UiDocumentKind.Layout)
        {
            try
            {
                return Candidate.LayoutVersion(version, UiLayoutManifest.Parse(xml));
            }
            catch (Exception ex) when (IsParseFailure(ex))
            {
                return Candidate.Refused(version, prefix + "the embedded fallback did not parse: " + ex.Message);
            }
        }

        UiStyleDocument style = UiStyleDocument.Parse(xml);
        if (style.StructurallyInvalid)
        {
            return Candidate.Refused(version, prefix + "the embedded fallback did not parse: " + style.Issues[0]);
        }

        return Candidate.StyleVersion(version, style);
    }

    private DocumentState? RequireDocument(string id)
    {
        return documents.TryGetValue(id, out DocumentState? state) ? state : null;
    }

    private void AddReport(UiReloadReport report)
    {
        lock (gate)
        {
            if (reports.Count >= MaxReports) reports.RemoveAt(0);
            reports.Add(report);
        }
    }

    // --- watching -------------------------------------------------------------------------------

    private void Watch(DocumentState state)
    {
        Unwatch(state);

        string directory;
        try
        {
            directory = Path.GetDirectoryName(Path.GetFullPath(state.Source.Path)) ?? "";
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return;
        }

        if (directory.Length == 0 || !Directory.Exists(directory)) return;

        string fileName = "";
        try
        {
            fileName = Path.GetFileName(state.Source.Path);
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return;
        }

        if (fileName.Length == 0) return;

        FileSystemWatcher watcher;
        try
        {
            watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime
            };
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return;
        }

        // The whole worker-thread surface: one closure over the id, one locked set insertion. A watcher
        // whose file keeps being rewritten (editors do that) can fire many times for one save; the set is
        // what makes that one pending entry.
        string documentId = state.Source.Id;
        watcher.Changed += (sender, args) => Signal(documentId);
        watcher.Created += (sender, args) => Signal(documentId);
        watcher.Deleted += (sender, args) => Signal(documentId);
        watcher.Renamed += (sender, args) => Signal(documentId);
        watcher.Error += (sender, args) =>
        {
            lock (gate)
            {
                if (!disposed) overflowed = true;
            }
        };

        try
        {
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            watcher.Dispose();
            return;
        }

        state.Watcher = watcher;
    }

    /// <summary>
    /// Arms a watcher for every registered document that holds none, or disarms every one when watching is
    /// off. Already-armed documents cost one null check, which is what makes this safe on the explicit
    /// re-arm path, the manual reload path and the throttled pump path.
    /// </summary>
    private void RefreshWatchers()
    {
        foreach (DocumentState state in documents.Values)
        {
            if (autoWatch)
            {
                if (state.Watcher == null) Watch(state);
            }
            else
            {
                Unwatch(state);
            }
        }
    }

    /// <summary>
    /// The throttled pump-path re-arm. A document whose directory did not exist when it was registered gets
    /// another chance automatically - the dev hot-reload path must not silently do nothing on a first run -
    /// while the frame path does not stat every registered path once per frame. Bounded by the document
    /// table; the manual reload and the AutoWatch setter re-arm without waiting for the throttle.
    /// </summary>
    private void AttemptReArm()
    {
        bool missing = false;
        foreach (DocumentState state in documents.Values)
        {
            if (state.Watcher == null)
            {
                missing = true;
                break;
            }
        }

        if (!missing) return;

        if (reArmCountdown > 0)
        {
            reArmCountdown--;
            return;
        }

        reArmCountdown = ReArmPumpInterval;
        RefreshWatchers();
    }

    private static void Unwatch(DocumentState state)
    {
        FileSystemWatcher? watcher = state.Watcher;
        state.Watcher = null;
        if (watcher == null) return;
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
    }

    private void EnsureAlive()
    {
        if (disposed)
        {
            throw new InvalidOperationException("UiDocumentService is disposed; create a new service.");
        }
    }

    // --- small helpers --------------------------------------------------------------------------

    private static bool IsIoFailure(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
            or System.Security.SecurityException;
    }

    private static bool IsParseFailure(Exception ex)
    {
        return ex is FormatException or IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or System.Security.SecurityException;
    }

    /// <summary>
    /// Content identity of one candidate. It is a hash of the bytes rather than a timestamp, because a
    /// timestamp is not a version: two saves inside one filesystem tick would look identical, and an editor
    /// that rewrites the same bytes would look different.
    /// </summary>
    private static string Hash(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(bytes);
        var builder = new StringBuilder(digest.Length * 2);
        for (int i = 0; i < digest.Length; i++)
        {
            builder.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string HashText(string text)
    {
        return Hash(Encoding.UTF8.GetBytes(text ?? ""));
    }

    private sealed class DocumentState
    {
        internal DocumentState(UiDocumentSource source, string embeddedFallbackXml)
        {
            Source = source;
            EmbeddedFallbackXml = embeddedFallbackXml;
        }

        internal UiDocumentSource Source { get; }

        internal string EmbeddedFallbackXml { get; }

        /// <summary>Version of the last accepted candidate; "" until something valid was read.</summary>
        internal string GoodVersion { get; private set; } = "";

        internal UiLayoutManifest? GoodLayout { get; private set; }

        internal UiStyleDocument? GoodStyle { get; private set; }

        /// <summary>Version already reported as a failure, so the same breakage is reported once.</summary>
        internal string LastFailedVersion { get; set; } = "";

        internal FileSystemWatcher? Watcher { get; set; }

        internal void Accept(Candidate candidate)
        {
            GoodVersion = candidate.Version;
            GoodLayout = candidate.Layout;
            GoodStyle = candidate.Style;
        }
    }

    private sealed class Dependency
    {
        internal Dependency(UiHost host, string? layoutId, string? styleId)
        {
            Host = host;
            LayoutId = layoutId;
            StyleId = styleId;
        }

        internal UiHost Host { get; }

        internal string? LayoutId { get; set; }

        internal string? StyleId { get; set; }
    }

    /// <summary>One host's pre-commit state, kept only for the duration of a batch commit.</summary>
    private readonly struct AppliedCommit
    {
        internal AppliedCommit(UiHost host, UiHost.DocumentRollback rollback)
        {
            Host = host;
            Rollback = rollback;
        }

        internal UiHost Host { get; }

        internal UiHost.DocumentRollback Rollback { get; }
    }

    private readonly struct Candidate
    {
        private Candidate(string version, UiLayoutManifest? layout, UiStyleDocument? style, string failure)
        {
            Version = version;
            Layout = layout;
            Style = style;
            Failure = failure;
        }

        internal string Version { get; }

        internal UiLayoutManifest? Layout { get; }

        internal UiStyleDocument? Style { get; }

        internal string Failure { get; }

        internal static Candidate LayoutVersion(string version, UiLayoutManifest layout)
        {
            return new Candidate(version, layout, null, "");
        }

        internal static Candidate StyleVersion(string version, UiStyleDocument style)
        {
            return new Candidate(version, null, style, "");
        }

        internal static Candidate Refused(string version, string failure)
        {
            return new Candidate(version, null, null, failure);
        }
    }
}
