using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Verse;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Output policy only. Does not enable fit, timings, geometry or observation.</summary>
public sealed class UiDiagnosticLogOptions
{
    public bool DetailEnabled { get; set; }
}

/// <summary>
/// One native domain-output owner per adopted Host/session (or an explicit standalone lifetime).
/// Original facts remain with their producers. No polling, replay, business references or native-input owner.
/// </summary>
public sealed class VerseUiDiagnosticLog : IDisposable, IUiDiagnosticOutput
{
    private const int WarningBudget = 24, ErrorBudget = 24, NormalBudget = 12;
    private const int KeyCapacity = 64;
    private const double WindowSeconds = 60;
    private readonly HashSet<string> keys = new(StringComparer.Ordinal);
    private readonly int[] emitted = new int[3];
    private readonly long[] repeated = new long[3], limited = new long[3];
    private readonly string[] latestLimited = new string[3];
    private readonly bool[] limitAnnounced = new bool[3];
    private readonly string source;
    private int sessionId;
    private long windowStarted = Stopwatch.GetTimestamp();
    private bool detailEnabled, active = true, sinkFailed, publishing;

    /// <summary>Standalone output. Wire callbacks explicitly; no global fit attachment is changed.</summary>
    public VerseUiDiagnosticLog(string consumer, UiDiagnosticLogOptions? options = null)
        : this(consumer, "unknown", options) { }

    private VerseUiDiagnosticLog(string consumer, string source, UiDiagnosticLogOptions? options)
    {
        if (string.IsNullOrWhiteSpace(consumer)) throw new ArgumentException("A consumer token is required.", nameof(consumer));
        Consumer = Token(consumer);
        this.source = Token(source);
        Configure(options);
    }

    public string Consumer { get; }
    public bool IsActive => active;
    public bool DetailEnabled => active && detailEnabled;
    /// <summary>Native formatting/output failures. No recursive logging or business retry is performed.</summary>
    public long OutputFailures { get; private set; }

    /// <summary>
    /// Binds before the real Host constructor starts, including creation-time style drops and failure.
    /// The original constructor remains legal. Metrics are passed through without replacement.
    /// </summary>
    public static UiHost CreateHost(string consumer, string source, UiLayoutManifest manifest,
        IUiBindingAccess bindings, UiTheme theme, ITextMetrics metrics, IUiTranslation translation,
        UiStyleDocument? document = null, UiDiagnosticLogOptions? options = null)
    {
        var log = new VerseUiDiagnosticLog(consumer, source, options);
        try { return new UiHost(source, manifest, bindings, theme, metrics, translation, document, log); }
        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error))
        {
            UiDomainDiagnostics.Publish(log, null, new UiDomainDiagnostic("host.creation-failed", UiDiagnosticLevel.Error,
                "unknown", error.GetType().Name, "page-unavailable", "correct-creation-contract-before-reopening"));
            log.Dispose();
            throw;
        }
    }

    /// <summary>Future facts only. Idempotently returns the live binding already owned by this consumer.</summary>
    public static VerseUiDiagnosticLog Attach(UiHost host, string consumer, UiDiagnosticLogOptions? options = null)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));
        if (!host.Session.IsActive) throw new ObjectDisposedException(nameof(host));
        if (string.IsNullOrWhiteSpace(consumer)) throw new ArgumentException("A consumer token is required.", nameof(consumer));
        if (host.Session.DiagnosticOutput is VerseUiDiagnosticLog existing && existing.IsActive)
        {
            if (!string.Equals(existing.Consumer, Token(consumer), StringComparison.Ordinal))
                throw new InvalidOperationException("The Host already has a different diagnostic output owner.");
            if (options != null) existing.Configure(options);
            return existing;
        }
        var log = new VerseUiDiagnosticLog(consumer, host.Source, options);
        ((IUiDiagnosticOutput)log).BindSession(host.Session.Identity);
        host.Session.DiagnosticOutput = log;
        return log;
    }

    /// <summary>Copies policy. Does not clear facts, deduplication, budgets or any sampler request.</summary>
    public void Configure(UiDiagnosticLogOptions? options) => detailEnabled = options?.DetailEnabled ?? false;

    public void OnOverflow(UiOverflowReport report) => UiDomainDiagnostics.Overflow(this, null, report);
    public void OnStyleFallback(UiStyleFallbackReport report) => UiDomainDiagnostics.Style(this, null, report);

    /// <summary>Optional Trace callback. Keep Controls.Trace null when detail is off to avoid caller formatting.</summary>
    public void Trace(string message)
    {
        if (!DetailEnabled) return;
        UiSession? session = UiDiagnosticHub.ActiveSession;
        if (session?.Identity != sessionId) session = null;
        UiDomainDiagnostics.Publish(this, session, new UiDomainDiagnostic("interaction.trace", UiDiagnosticLevel.Normal,
            session?.ActiveNode.Id.Key ?? "unknown", "interaction", "observation-only", "none", detail: message));
    }

    void IUiDiagnosticOutput.BindSession(int identity) => sessionId = identity;

    void IUiDiagnosticOutput.Publish(UiDomainDiagnostic fact, UiDiagnosticMoment moment)
    {
        if (!active || sinkFailed || publishing || fact.Level == UiDiagnosticLevel.Normal && !detailEnabled) return;
        publishing = true;
        try
        {
            long now = Stopwatch.GetTimestamp();
            if ((now - windowStarted) / (double)Stopwatch.Frequency >= WindowSeconds)
            { Summary("window"); Reset(now); }
            int level = (int)fact.Level;
            string key = Token(fact.Code) + "|" + Token(fact.Node) + "|" + Token(fact.Category);
            if (keys.Contains(key)) { repeated[level]++; return; }
            int budget = fact.Level == UiDiagnosticLevel.Error ? ErrorBudget : fact.Level == UiDiagnosticLevel.Warning ? WarningBudget : NormalBudget;
            if (emitted[level] >= budget || keys.Count >= KeyCapacity)
            {
                limited[level]++;
                latestLimited[level] = key;
                if (!limitAnnounced[level])
                {
                    limitAnnounced[level] = true;
                    Write(fact.Level, Header(fact.Level, "diagnostics.budget") + " first-unprinted=" + key
                        + " impact=output-throttled next=inspect-original-facts-or-wait-for-next-window");
                }
                return;
            }
            keys.Add(key);
            emitted[level]++;
            string line = Header(fact.Level, fact.Code) + " node=" + Token(fact.Node) + " category=" + Token(fact.Category)
                + " impact=" + Token(fact.Impact) + " next=" + Token(fact.Next);
            if (detailEnabled) line += Details(fact, moment);
            Write(fact.Level, line);
        }
        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error)) { OutputFailures++; sinkFailed = true; }
        finally { publishing = false; }
    }

    private string Header(UiDiagnosticLevel level, string code) => "[FerriteLib.UIKIT] consumer=" + Consumer
        + " level=" + level.ToString().ToLowerInvariant() + " event=" + Token(code) + " host=" + source
        + " session=" + (sessionId == 0 ? "unknown" : sessionId.ToString(CultureInfo.InvariantCulture));

    private static string Details(UiDomainDiagnostic fact, UiDiagnosticMoment moment)
    {
        string value = moment.Sampled
            ? " pass=" + moment.Pass + " phase=" + moment.Phase + " raw-phase=" + moment.RawPhase + " consumed=" + moment.Consumed
                + " content=" + moment.ContentRevision + " arranged-content=" + moment.ArrangedContentRevision
                + " definition=" + moment.DefinitionRevision + " arranged-definition=" + moment.ArrangedDefinitionRevision
                + " dirty=" + moment.Dirty + " layout-state=" + (moment.ArrangedContentRevision < 0 ? "NotSampled"
                    : moment.Dirty || moment.ArrangedContentRevision != moment.ContentRevision
                        || moment.ArrangedDefinitionRevision != moment.DefinitionRevision ? "Stale" : "NotSampled")
            : " context=NotSampled";
        if (fact.Overflow is UiOverflowReport overflow)
            value += " axis=" + overflow.Axis + " needed=" + Number(overflow.Needed) + " available=" + Number(overflow.Available)
                + " wrap=" + Number(overflow.RectWidth) + " font=" + Token(UiTextFonts.FontOf(overflow).ToString());
        if (fact.Fallback is UiStyleFallbackReport fallback)
        {
            value += " attribute=" + Token(fallback.Attribute);
            if (fallback.Attribute == "FontSize")
                value += " requested=" + Token(fallback.Authored) + " resolution=" + SafeDetail(fallback.Resolved);
        }
        if (fact.Timing is UiDiagnosticTiming timing) value += " " + timing;
        if (!string.IsNullOrEmpty(fact.Detail)) value += " detail=" + SafeDetail(fact.Detail);
        return value;
    }

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Token(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "unknown";
        var result = new StringBuilder(Math.Min(value!.Length, 160));
        foreach (char c in value)
        {
            if (result.Length >= 160) break;
            result.Append(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9'
                || c == '.' || c == '_' || c == '-' || c == '/' || c == '#' ? c : '_');
        }
        return result.ToString();
    }
    private static string SafeDetail(string value)
    {
        int length = Math.Min(value.Length, 768);
        var result = new StringBuilder(length);
        for (int i = 0; i < length; i++) result.Append(char.IsControl(value[i]) ? ' ' : value[i]);
        return result.ToString();
    }

    private void Summary(string reason)
    {
        UiDiagnosticLevel highest = UiDiagnosticLevel.Normal;
        long total = 0;
        for (int i = 0; i < 3; i++)
            if (repeated[i] != 0 || limited[i] != 0) { highest = (UiDiagnosticLevel)i; total += repeated[i] + limited[i]; }
        if (total == 0 || sinkFailed) return;
        string line = Header(highest, "diagnostics.summary") + " boundary=" + reason;
        for (int i = 0; i < 3; i++)
        {
            string level = ((UiDiagnosticLevel)i).ToString().ToLowerInvariant();
            line += " " + level + "-repeated=" + repeated[i] + " " + level + "-limited=" + limited[i]
                + " " + level + "-latest-unprinted=" + (latestLimited[i] ?? "none");
        }
        Write(highest, line);
    }
    private void Reset(long now)
    {
        keys.Clear(); Array.Clear(emitted, 0, 3); Array.Clear(repeated, 0, 3); Array.Clear(limited, 0, 3);
        Array.Clear(latestLimited, 0, 3); Array.Clear(limitAnnounced, 0, 3); windowStarted = now;
    }
    private static void Write(UiDiagnosticLevel level, string line)
    {
        if (level == UiDiagnosticLevel.Error) Log.Error(line);
        else if (level == UiDiagnosticLevel.Warning) Log.Warning(line);
        else Log.Message(line);
    }

    /// <summary>Emits any suppression summary once, then releases only this output ledger.</summary>
    public void Dispose()
    {
        if (!active) return;
        active = false;
        try { if (!publishing) Summary("close"); }
        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error)) { OutputFailures++; }
        finally { Reset(0); }
    }
}
