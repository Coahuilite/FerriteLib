using System;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Which of the two document vocabularies a <see cref="UiDocumentSource"/> names. The kind is part of the
/// source identity rather than something inferred from the file extension: the two vocabularies have two
/// parsers and two different failure policies, and an author who renames a layout to <c>.styles</c> must
/// not change what the library does with it.
/// </summary>
public enum UiDocumentKind
{
    /// <summary>
    /// A <c>Schema="2"</c> layout manifest. A structural failure is page-class: the candidate is refused
    /// and the affected hosts keep the last valid tree.
    /// </summary>
    Layout = 0,

    /// <summary>
    /// A standalone <c>Schema="1"</c> style document. A structural failure is refused like a layout one
    /// (the last valid appearance survives); declaration-level drops inside a readable document stay soft
    /// and are recorded on the document, as they are for a document handed to a host directly.
    /// </summary>
    Style = 1
}

/// <summary>
/// One file a consumer owns, handed to <see cref="UiDocumentService"/> by path. Nothing here scans a
/// directory: the consumer names the file, and it is the service that later re-reads it. The library owns
/// no directory convention and no embedded resource layout of its own, which is why the fallback text
/// travels beside the path instead of being discovered.
/// </summary>
public readonly struct UiDocumentSource
{
    public UiDocumentSource(string id, UiDocumentKind kind, string path)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentException("A document source needs a non-empty Id.", nameof(id));
        }

        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("A document source needs a non-empty Path.", nameof(path));
        }

        Id = id;
        Kind = kind;
        Path = path;
    }

    /// <summary>Stable identity of this source inside one service; the key dependency tracking uses.</summary>
    public string Id { get; }

    /// <summary>Which parser reads <see cref="Path"/>.</summary>
    public UiDocumentKind Kind { get; }

    /// <summary>The file the consumer named. Never scanned for, never guessed.</summary>
    public string Path { get; }

    public override string ToString()
    {
        return Kind.ToString() + " '" + Id + "' at '" + Path + "'";
    }
}

/// <summary>
/// What one reload attempt did, in the shape a failure has to be reportable in: which file, which element
/// and why. One report per attempted document change, returned by <see cref="UiDocumentService.Reload"/>
/// and kept (bounded) in <see cref="UiDocumentService.Reports"/>.
/// <para>
/// The outcome axes are deliberately separate rather than one status enum, because they answer different
/// questions: <see cref="Accepted"/> says the new version was committed to every affected host,
/// <see cref="Skipped"/> says nothing had to happen because the bytes were already current,
/// <see cref="Duplicate"/> says this exact failure was already reported, and <see cref="Rejected"/> is the
/// failure case. A caller that only checks "did it work" reads <see cref="Accepted"/>.
/// </para>
/// </summary>
public sealed class UiReloadReport
{
    internal UiReloadReport(
        string documentId,
        UiDocumentKind kind,
        string path,
        string version,
        bool accepted,
        bool skipped,
        bool duplicate,
        string element,
        string reason,
        int hostsAffected,
        int hostsCommitted)
    {
        DocumentId = documentId ?? "";
        Kind = kind;
        Path = path ?? "";
        Version = version ?? "";
        Accepted = accepted;
        Skipped = skipped;
        Duplicate = duplicate;
        Element = element ?? "";
        Reason = reason ?? "";
        HostsAffected = hostsAffected;
        HostsCommitted = hostsCommitted;
    }

    /// <summary>The <see cref="UiDocumentSource.Id"/> this attempt belongs to.</summary>
    public string DocumentId { get; }

    /// <summary>The document vocabulary that was read.</summary>
    public UiDocumentKind Kind { get; }

    /// <summary>The file the consumer named - the "file" half of a failure report.</summary>
    public string Path { get; }

    /// <summary>Content identity of the candidate: a hash of the bytes, or the embedded fallback's hash.</summary>
    public string Version { get; }

    /// <summary>True when this version was committed to every host depending on the document.</summary>
    public bool Accepted { get; }

    /// <summary>True when the candidate's identity equals the current valid version, so nothing was committed.</summary>
    public bool Skipped { get; }

    /// <summary>True when an identical failure had already been reported; the report is not recorded again.</summary>
    public bool Duplicate { get; }

    /// <summary>True for a failure: neither committed nor skipped.</summary>
    public bool Rejected => !Accepted && !Skipped;

    /// <summary>The element (layout path, or declared Id when the path is unknown) a failure names, else "".</summary>
    public string Element { get; }

    /// <summary>Why the candidate was refused, or what the accepted commit did.</summary>
    public string Reason { get; }

    /// <summary>How many live hosts depended on the document when the attempt ran.</summary>
    public int HostsAffected { get; }

    /// <summary>How many of those hosts committed the new version. Never partial for an accepted batch.</summary>
    public int HostsCommitted { get; }

    public override string ToString()
    {
        if (Accepted)
        {
            return "accepted " + Kind + " '" + DocumentId + "' (" + Version + ") to "
                + HostsCommitted.ToString(CultureInfo.InvariantCulture) + " host(s): " + Reason;
        }

        if (Skipped)
        {
            return "skipped " + Kind + " '" + DocumentId + "' (" + Version + "): " + Reason;
        }

        return (Duplicate ? "duplicate failure " : "rejected ") + Kind + " '" + DocumentId + "' ("
            + Version + ") at '" + Element + "': " + Reason;
    }
}
