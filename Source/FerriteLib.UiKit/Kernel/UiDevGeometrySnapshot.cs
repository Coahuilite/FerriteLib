using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One boundary the instrument tried to observe: either the rect that was actually in force, or an explicit
/// statement that the value could not be observed <b>and why</b>.
/// <para>
/// It exists because "we did not measure it" and "we measured zero" are different facts, and a diagnostic
/// that renders the first as the second is worse than one that renders nothing: a plausible default is read
/// as a measurement. Every field an observation site cannot answer carries <see cref="Unknown"/> with a
/// reason rather than a zero rect, and the reason string is part of the contract (a consumer may print it).
/// </para>
/// <para>
/// <b>Space.</b> A boundary rect is in Host window space unless the member that carries it says otherwise -
/// the same space the hit stack and the pointer live in, which is the only space in which a clip or a popup
/// anchor is meaningful.
/// </para>
/// <para>
/// <b>Use the two factories.</b> <see cref="Known"/> and <see cref="Unknown"/> are the only values the
/// payload produces, and an <see cref="Unknown"/> always carries a reason. A <c>default</c> instance - which
/// nothing here produces, but which a caller can write - has no reason and renders as <c>unknown()</c>; it is
/// distinguishable from a real observation by <see cref="IsKnown"/> and by the empty reason.
/// </para>
/// </summary>
public readonly struct UiDevBoundary
{
    private UiDevBoundary(bool isKnown, Rect rect, string reason)
    {
        IsKnown = isKnown;
        Rect = rect;
        UnknownReason = reason;
    }

    /// <summary>A boundary that was observed, in Host window space.</summary>
    public static UiDevBoundary Known(Rect rect)
    {
        return new UiDevBoundary(true, rect, "");
    }

    /// <summary>
    /// A boundary that could not be observed. <paramref name="reason"/> is required: an unknown without a
    /// reason is the silence this type exists to replace.
    /// </summary>
    public static UiDevBoundary Unknown(string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            throw new ArgumentException("An unknown boundary must carry a reason.", nameof(reason));
        }

        return new UiDevBoundary(false, default, reason);
    }

    /// <summary>True when <see cref="Rect"/> is an observation; false when it is meaningless.</summary>
    public bool IsKnown { get; }

    /// <summary>The observed rect in Host window space. Meaningful only when <see cref="IsKnown"/>.</summary>
    public Rect Rect { get; }

    /// <summary>Why the value is unknown. Empty when <see cref="IsKnown"/>.</summary>
    public string UnknownReason { get; }

    public override string ToString()
    {
        if (!IsKnown) return "unknown(" + UnknownReason + ")";
        // this.Rect, not Rect: the property shares its name with its type, and qualifying the receiver keeps
        // every reference here unambiguous.
        return "(" + this.Rect.x.ToString("0.###", CultureInfo.InvariantCulture) + ","
            + this.Rect.y.ToString("0.###", CultureInfo.InvariantCulture) + ","
            + this.Rect.width.ToString("0.###", CultureInfo.InvariantCulture) + ","
            + this.Rect.height.ToString("0.###", CultureInfo.InvariantCulture) + ")";
    }
}

/// <summary>
/// One arranged element as the development instrument observed it in the captured pass: its stable identity,
/// the four coordinate spaces it has a rect in, the appearance it was drawn with and where that appearance
/// came from, and the boundaries in force around it.
/// <para>
/// <b>This is a copy, not a window onto live state.</b> The capture builds a new instance per snapshot call
/// from values it already holds, and every setter is <c>internal</c>, so a snapshot handed to a consumer
/// cannot be changed afterwards - by a later draw, by another host, or by the consumer itself.
/// </para>
/// <para>
/// <b>Every field says which space or which source it came from.</b> The same discipline the text dump has:
/// a number a reader cannot place is a number a reader will misread.
/// </para>
/// </summary>
public sealed class UiDevNodeSnapshot
{
    /// <summary>
    /// Why <see cref="Hover"/> is unknown at a geometry sample. The hover claim is written during an
    /// element's own Draw (<c>UiSession.ClaimHover</c>), and a geometry sample is taken before that Draw runs,
    /// so a value read here would belong to whichever element claimed last - usually another one.
    /// </summary>
    public const string HoverUnknownReason =
        "the hover claim is published during the element's own draw, which runs after this sample";

    /// <summary>
    /// Why <see cref="Focus"/> is unknown. This library has no focus product subsystem: the session's active
    /// node is the draw-time element stack, not a focus owner, so there is nothing here to report and this
    /// instrument will not invent one.
    /// </summary>
    public const string FocusUnknownReason =
        "no ELEMENT-level focus owner is published by this library: the session's active node is the "
        + "draw-time element stack. A window-level focus policy exists elsewhere, which is not this";

    /// <summary>
    /// Why <see cref="Clip"/> can be unknown: the pass published no host viewport, so the session held no
    /// clip at all. The wording says "no clip to report" rather than "could not be read" on purpose - see the
    /// note on <see cref="Clip"/> about the two states this one unknown carries.
    /// </summary>
    public const string ClipUnknownReason =
        "no host viewport was published for this pass, so there is no clip to report";

    /// <summary>Why <see cref="Content"/> is unknown for most elements: only a scoped container publishes one.</summary>
    public const string ContentUnknownReason =
        "no content rect was published for this element (only a scoped container publishes one)";

    /// <summary>Why <see cref="PopupAnchor"/> is unknown: there was no open popup to anchor.</summary>
    public const string NoOpenPopupUnknownReason =
        "no popup was open at sample time";

    /// <summary>
    /// Why <see cref="PaintedFont"/> is null. The font a label finally paints with and is audited against is
    /// chosen inside the widget's own draw - <c>UiThemeDraw.Label</c> takes it as a parameter and several
    /// chrome kinds pass their own constant (a band font, a small header font) - and that draw runs after this
    /// sample. The instrument does not infer it, so the one font it does report is the scope's selection.
    /// </summary>
    public const string PaintedFontUnknownReason =
        "the font a label finally paints with is chosen inside the widget's own draw, which runs after this sample; the scope selection beside it is what the engine measures with and what the shared text vocabulary reads";

    /// <summary>
    /// The value <see cref="AppearanceSource"/> takes when the kind declares no appearance seam at all. It is
    /// a fact about the kind, not a missing measurement: a kind with no looks paints one fixed shape.
    /// </summary>
    public const string NoAppearanceSource = "none";

    /// <summary>
    /// The value <see cref="AppearanceSource"/> takes when the resolver came from the element's own
    /// <c>(scope, kind)</c> registration's look set.
    /// </summary>
    public const string ScopeAppearanceSource = "scope";

    /// <summary>
    /// The value <see cref="AppearanceSource"/> takes when the resolver came from the core scope: either the
    /// element's scope IS core, or the scope declares the kind without declaring looks for it.
    /// </summary>
    public const string CoreAppearanceSource = "core";

    internal UiDevNodeSnapshot()
    {
    }

    /// <summary>The session frame this sample was taken in.</summary>
    public int Pass { get; internal set; }

    /// <summary>
    /// The node's canonical identity, <c>UiNodeId.Key</c>: the segment join no XML text can spell, so two
    /// different elements cannot produce the same value. Not for humans - log <see cref="Path"/>.
    /// </summary>
    public string NodeKey { get; internal set; } = "";

    /// <summary>The node's display path, <c>UiNodeId.Path</c>: the string this library has always printed.</summary>
    public string Path { get; internal set; } = "";

    /// <summary>The element's declared ordinal among its siblings; -1 for a root or an orphan.</summary>
    public int Ordinal { get; internal set; } = -1;

    /// <summary>The element's declared <c>Id</c>, or "" when it has none.</summary>
    public string ElementId { get; internal set; } = "";

    /// <summary>The widget kind, or the container's element name.</summary>
    public string Kind { get; internal set; } = "";

    /// <summary>True for an arranged container rather than a drawn widget.</summary>
    public bool IsContainer { get; internal set; }

    /// <summary>The container's element name (Scroll, Column, ...), or "" for a widget.</summary>
    public string ContainerKind { get; internal set; } = "";

    /// <summary>True for the scoped containers (Scroll/Clip), whose rect is a viewport.</summary>
    public bool IsScopedContainer { get; internal set; }

    /// <summary>
    /// True when this element pushed a hit layer, i.e. a pointer can land on it. Containers do not: a
    /// container's layer would cover its own children.
    /// </summary>
    public bool IsHitSurface { get; internal set; }

    /// <summary>
    /// The engine's arranged rect for this entry, exactly as the node publishes it. Page space at the top
    /// level; inside a scrolled container it is that container's content-local space, which is why
    /// <see cref="Draw"/> is recorded beside it instead of being derived from it.
    /// </summary>
    public Rect Arranged { get; internal set; }

    /// <summary>The rect the widget's Draw was handed - the element's own drawing space.</summary>
    public Rect Draw { get; internal set; }

    /// <summary><see cref="Draw"/> converted into Host window space, where the pointer and hit stack live.</summary>
    public Rect Window { get; internal set; }

    /// <summary>The draw-local to window-space offset this sample was converted with.</summary>
    public Vector2 Origin { get; internal set; }

    /// <summary>Scroll content rect (scroll-local) for a scoped container; unknown for everything else.</summary>
    public UiDevBoundary Content { get; internal set; }

    /// <summary>Fixed, Auto or MatchContent, read from the declaration the way the engine reads it.</summary>
    public string HeightMode { get; internal set; } = "";

    /// <summary>The height the element resolved to, which is what the height mode decided.</summary>
    public float ResolvedHeight { get; internal set; }

    /// <summary>
    /// The <b>scope's</b> font: <c>UiTheme.DefaultFont</c> as the resolved theme answers it for this element's
    /// scope. This is an observation, and it is the font the engine's own text measurement uses.
    /// <para>
    /// <b>Deliberately not called the painted font.</b> The label outlet resolves
    /// <c>font ?? theme.Styles.Font</c>, and <c>Styles.Font</c> is this same value - so a label that names no
    /// font paints with it. Several kinds do name one (a band or header constant), and that choice is made
    /// inside their own draw, so <see cref="PaintedFont"/> is the member that answers "what is painted" and it
    /// is null here. Reporting this value as the painted font would be the misreport this split exists to
    /// prevent. There is no per-element <c>Font</c> attribute in this library: the scope selection is the only
    /// font input a page can express.
    /// </para>
    /// </summary>
    public UiFont ScopeFont { get; internal set; }

    /// <summary>
    /// The font text is finally painted (and fit-audited) with, when the sample site could observe it. Null at
    /// every sample this instrument produces today, and never filled with a plausible stand-in: see
    /// <see cref="PaintedFontUnknownReason"/>, which the dump prints beside it.
    /// </summary>
    public UiFont? PaintedFont { get; internal set; }

    /// <summary>
    /// The element's <b>effective appearance</b>: the canonical look name its kind's resolver selected for it,
    /// read through the same registry seam the natural-body entry point uses
    /// (<c>UiWidgetRegistry.GetAppearanceResolver(scope, kind)</c> → <c>UiAppearanceResolver.Resolve(spec)</c>).
    /// This is the RESOLVED value, not the authored text beside it - <see cref="Tone"/> records what the page
    /// wrote, this records what the kind will draw.
    /// <para>
    /// Empty together with <see cref="NoAppearanceSource"/> when the kind declares no appearance seam at all:
    /// the kind paints one fixed shape, which is a fact about the kind rather than an unmeasured value.
    /// </para>
    /// </summary>
    public string Appearance { get; internal set; } = "";

    /// <summary>
    /// True when the element DECLARED a look its kind supports, false when it took the kind's default (absent,
    /// blank or unrecognised - the seam answers the default for all three, which is what the creation-time
    /// contract reads to refuse a genuinely unknown spelling once).
    /// </summary>
    public bool AppearanceDeclared { get; internal set; }

    /// <summary>The look the kind falls back to when the element declares nothing it implements, or "" with no seam.</summary>
    public string AppearanceDefault { get; internal set; } = "";

    /// <summary>
    /// The kind's accepted look names, ordinal-joined with <c>,</c>, or "" with no seam. It is the vocabulary
    /// the declaration was read against, published by the same resolver object the widget resolves through, so
    /// a reader can see what the element COULD have declared rather than only what it did.
    /// </summary>
    public string AppearanceSupported { get; internal set; } = "";

    /// <summary>
    /// Where that resolver came from: <see cref="ScopeAppearanceSource"/> when it is the element's own scope's
    /// registered look set, <see cref="CoreAppearanceSource"/> when it is the core scope's (either because the
    /// element's scope IS core, or because that scope declares the kind without declaring looks for it), or
    /// <see cref="NoAppearanceSource"/> when the kind has no seam.
    /// <para>
    /// A registered scoped alias and a core fallback are different facts and this member separates them: the
    /// instrument proves which one answered by asking the registry for the core resolver too and comparing
    /// instances, so the answer is identity rather than a heuristic.
    /// </para>
    /// </summary>
    public string AppearanceSource { get; internal set; } = "";

    /// <summary>
    /// True when this exact <c>(scope, kind)</c> pair is registered in the element's own scope (no core-scope
    /// fallback, the registry's own exact lookup). It disambiguates the one mixed case in
    /// <see cref="AppearanceSource"/>: a scope that declares a kind but no looks for it has its own
    /// declaration and resolves its looks from core, and both facts are reported.
    /// </summary>
    public bool AppearancePairRegistered { get; internal set; }

    /// <summary>
    /// The nearest declared <c>Scheme</c> on the element's style chain, or "" when it declared none. Read
    /// through the resolver's own accessor over the same chain the theme was resolved from, so it names the
    /// resolution's own input rather than a second parse of the manifest.
    /// </summary>
    public string Scheme { get; internal set; } = "";

    /// <summary>The nearest declared <c>Density</c> on the chain, or "" - same source as <see cref="Scheme"/>.</summary>
    public string Density { get; internal set; } = "";

    /// <summary>
    /// Where the element's appearance came from, as an identity fact rather than a guess: <c>page</c> when it
    /// drew with the injected page theme instance itself, <c>region</c> when it drew with a clone the style
    /// resolver built for its effective (scheme, density) pair.
    /// <para>
    /// The rule is exact and comes from the engine, not from a heuristic here: a declaration that names
    /// neither a scheme nor a density appends nothing to the chain, so an element that styles nothing keeps
    /// the injected theme and reports <c>page</c>; an element that names one - or that sits inside an element
    /// which does - resolves through the resolver and reports <c>region</c>. Read it together with
    /// <see cref="Scheme"/> and <see cref="Density"/>, which name the pair the region was built for.
    /// </para>
    /// </summary>
    public string ThemeOrigin { get; internal set; } = "";

    /// <summary>The resolved theme's layout revision: which revision of its density/font produced these numbers.</summary>
    public int ThemeLayoutRevision { get; internal set; }

    /// <summary>The resolved theme's colour revision: which revision of its palette produced its colours.</summary>
    public int ThemeColourRevision { get; internal set; }

    /// <summary>
    /// The element's authored <c>Tone</c> text as written, or "" when it declared none. It is the AUTHORED
    /// input, not the resolved treatment: the resolved treatment is computed inside the widget (which may
    /// also apply hover/armed state), so an instrument that re-derived it here would report a colour that
    /// was perhaps never painted - and calling the role parser would record a deprecation note the
    /// instrument caused.
    /// </summary>
    public string Tone { get; internal set; } = "";

    /// <summary>The element's authored <c>Emphasis</c> text as written, or "" - same source and caveat as <see cref="Tone"/>.</summary>
    public string Emphasis { get; internal set; } = "";

    /// <summary>
    /// True while this element is disabled for input: the command it declares answers <c>CanExecute</c>
    /// false. Published by the engine on the element's node, so it is a fact about the element rather than
    /// about the widget that drew it.
    /// </summary>
    public bool Disabled { get; internal set; }

    /// <summary>
    /// The effective clip in force around this element, in Host window space: the intersection of every
    /// enclosing scoped container and the host viewport, not merely the innermost rectangle. Taken from the
    /// session's own clip at sample time, which is the value the drawing actually obeyed.
    /// <para>
    /// <b>Nesting, stated because it is easy to misread.</b> A sample is taken as the engine reaches an
    /// entry, before that entry opens its own scope. A scoped container's own sample therefore carries the
    /// clip of its PARENT - the boundary it was drawn inside - while every element inside it carries the
    /// intersection that container contributed. So "the clip in force" answers "what was this element drawn
    /// within", and a container's own viewport appears on its children rather than on itself.
    /// </para>
    /// <para>
    /// <b>One unknown state, two meanings, and why that is accepted here.</b> An unclipped pass - a
    /// degenerate host viewport, where the session itself holds no clip - reports the same
    /// <c>IsKnown == false</c> as a value the instrument failed to read. The reason string separates them
    /// (<see cref="UiDevNodeSnapshot.ClipUnknownReason"/> says there is no clip to report, rather than that one
    /// could not be read), and a consumer that must tell the two apart compares
    /// <see cref="UiDevBoundary.UnknownReason"/> rather than <c>IsKnown</c>. A third state would be a larger
    /// public surface than this ambiguity is worth; the alternative - reporting a fabricated rect - is the
    /// failure the type exists to prevent.
    /// </para>
    /// </summary>
    public UiDevBoundary Clip { get; internal set; }

    /// <summary>Always unknown at a geometry sample; see <see cref="HoverUnknownReason"/>.</summary>
    public UiDevBoundary Hover { get; internal set; }

    /// <summary>Always unknown; see <see cref="FocusUnknownReason"/>.</summary>
    public UiDevBoundary Focus { get; internal set; }

    /// <summary>
    /// The session's open popup owner at sample time, or "" when no popup was open. A popup is session state,
    /// so this is the same answer for every element in the pass.
    /// <para>
    /// <b>Timing, stated because it is the one thing this cannot tell you.</b> Every sample is taken as the
    /// engine reaches its element, BEFORE that element's own draw. So a popup a trigger is about to open in
    /// this same pass is not here yet, and a popup the pass is about to close (an owner that stops drawing is
    /// closed at the END of the hit pass) is still here. What it answers is "what the session held when this
    /// element was reached".
    /// </para>
    /// </summary>
    public string OpenPopupId { get; internal set; } = "";

    /// <summary>
    /// The open popup's anchor in Host window space - the same space the pointer and hit stack live in, which
    /// is the only space in which an anchor is meaningful - or unknown, with the reason recorded, when no popup
    /// was open at sample time.
    /// </summary>
    public UiDevBoundary PopupAnchor { get; internal set; }

    /// <summary>
    /// True when THIS element is the owner of the session's open popup (<c>UiSession.IsPopupOpen</c>). False
    /// for every element of a page with no open popup, and for an element that declares no <c>Id</c>, which
    /// cannot own one. It is read at the same moment as <see cref="OpenPopupId"/>, so the timing note there
    /// applies to this too.
    /// </summary>
    public bool OwnsOpenPopup { get; internal set; }

    public override string ToString()
    {
        return Path + " [" + Kind + "]";
    }
}

/// <summary>
/// One hit query as the development instrument observed it: what the funnel was asked, what it answered, and
/// how the input event stood before and after the query.
/// </summary>
public sealed class UiDevInputSnapshot
{
    internal UiDevInputSnapshot()
    {
    }

    /// <summary>The session frame this sample was taken in.</summary>
    public int Pass { get; internal set; }

    /// <summary>The queried element's canonical identity, <c>UiNodeId.Key</c>; "" when no element applied.</summary>
    public string NodeKey { get; internal set; } = "";

    /// <summary>The queried element's display path, or the context's element path when no node applied.</summary>
    public string Path { get; internal set; } = "";

    /// <summary>The queried element's kind, or "" when no node applied.</summary>
    public string Kind { get; internal set; } = "";

    /// <summary>The queried element's declared <c>Id</c>, or "".</summary>
    public string ElementId { get; internal set; } = "";

    /// <summary>The queried rect in Host window space.</summary>
    public Rect Rect { get; internal set; }

    /// <summary>The pointer in Host window space - the same space as <see cref="Rect"/>.</summary>
    public Vector2 Point { get; internal set; }

    /// <summary>What the funnel decided: <c>disabled</c>, <c>covered</c>, <c>hit</c> or <c>miss</c>.</summary>
    public string Verdict { get; internal set; } = "";

    /// <summary>The event phase before the query ran, e.g. <c>MouseDown</c> or <c>Used</c>.</summary>
    public string EventBefore { get; internal set; } = "";

    /// <summary>The event phase after the query ran. <c>Used</c> here is what a consumed event looks like.</summary>
    public string EventAfter { get; internal set; } = "";

    /// <summary>
    /// True when the query consumed the event: the phase moved from a usable one to <c>Used</c>. Derived
    /// from the two phases beside it rather than asked of the funnel, so a reader can always recheck it.
    /// </summary>
    public bool Consumed { get; internal set; }

    /// <summary>
    /// True when the pass entered with the event already <c>Used</c>, so the input that started it is not
    /// observable here. The sample is still recorded, and says so rather than guessing a phase.
    /// </summary>
    public bool OriginUnknown { get; internal set; }

    /// <summary>The phase this pass was entered with, as the host captured it before any control consumed it.</summary>
    public string EntryEvent { get; internal set; } = "";

    public override string ToString()
    {
        return Verdict + " " + EventBefore + "->" + EventAfter + " " + Path;
    }
}

/// <summary>
/// One captured pass, as data: the same capture <see cref="UiDiagnosticSubscription.DumpGeometry"/> renders
/// as text, handed out as objects instead.
/// <para>
/// <b>One capture, two renderings, one pass.</b> These objects are built from the capture's own stored
/// samples in the same call that would produce the text dump, so the two describe the same frame by
/// construction rather than by agreement between two collectors; a lane asserts the agreement explicitly
/// (same pass number, same per-node facts) so a future divergence cannot pass unnoticed.
/// </para>
/// <para>
/// There is no visible overlay in this relationship because there is no second source: the outline painter
/// reads the same capture's overlay switch and outlines the same nodes, and it paints after the element it
/// outlines has drawn, so it can never feed back into what was sampled.
/// </para>
/// <para>
/// <b>Bounded, current-pass, and defensively copied.</b> The node and input lists are bounded by the
/// capture's own limits and carry the count each one dropped; a snapshot describes the most recent pass and
/// is unaffected by later draws, because it is a copy.
/// </para>
/// </summary>
public sealed class UiDevGeometrySnapshot
{
    private static readonly UiDevNodeSnapshot[] NoNodes = Array.Empty<UiDevNodeSnapshot>();
    private static readonly UiDevInputSnapshot[] NoInputs = Array.Empty<UiDevInputSnapshot>();

    internal UiDevGeometrySnapshot()
    {
    }

    /// <summary>The manifest source of the host this pass belongs to.</summary>
    public string Host { get; internal set; } = "";

    /// <summary>The session that produced the pass, unique for the life of the process.</summary>
    public int SessionId { get; internal set; }

    /// <summary>
    /// The session frame this capture describes. A snapshot a consumer can obtain always describes a real
    /// pass: the reader answers <c>false</c> instead of handing out a placeholder, which is why there is no
    /// public way to construct one of these and no "empty snapshot" state to misread.
    /// </summary>
    public int Pass { get; internal set; } = -1;

    /// <summary>The phase the pass was entered with, captured before any control consumed it.</summary>
    public string EntryEvent { get; internal set; } = "none";

    /// <summary>Nodes the capture kept.</summary>
    public IReadOnlyList<UiDevNodeSnapshot> Nodes { get; internal set; } = NoNodes;

    /// <summary>Input queries the capture kept.</summary>
    public IReadOnlyList<UiDevInputSnapshot> Inputs { get; internal set; } = NoInputs;

    /// <summary>Arranged nodes this pass produced that the bounded capture did not keep.</summary>
    public long NodesDropped { get; internal set; }

    /// <summary>Input samples this pass produced that the bounded capture did not keep.</summary>
    public long InputsDropped { get; internal set; }

    /// <summary>The node with this canonical identity, or null. Keyed by <see cref="UiDevNodeSnapshot.NodeKey"/>.</summary>
    public UiDevNodeSnapshot? NodeByKey(string nodeKey)
    {
        if (string.IsNullOrEmpty(nodeKey)) return null;
        for (int i = 0; i < Nodes.Count; i++)
        {
            if (string.Equals(Nodes[i].NodeKey, nodeKey, StringComparison.Ordinal)) return Nodes[i];
        }

        return null;
    }

    /// <summary>The node with this display path, or null. Display paths can be ambiguous when a kind contains
    /// the separator; prefer <see cref="NodeByKey"/> when the caller has the canonical identity.</summary>
    public UiDevNodeSnapshot? NodeByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        for (int i = 0; i < Nodes.Count; i++)
        {
            if (string.Equals(Nodes[i].Path, path, StringComparison.Ordinal)) return Nodes[i];
        }

        return null;
    }

    public override string ToString()
    {
        return "pass=" + Pass.ToString(CultureInfo.InvariantCulture)
            + " nodes=" + Nodes.Count.ToString(CultureInfo.InvariantCulture)
            + " inputs=" + Inputs.Count.ToString(CultureInfo.InvariantCulture);
    }
}
