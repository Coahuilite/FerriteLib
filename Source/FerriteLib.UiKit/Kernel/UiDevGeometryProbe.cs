#if FER_DEV
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One development-only geometry sample: what one arranged element's rectangles were in the pass that drew
/// it, which identity it carried, the appearance it was drawn with and where that appearance came from, and
/// the boundaries that were in force around it. The instrument observes; it never decides. Nothing here is
/// read back by the engine, the session or a widget, which is what keeps "measuring it" from changing "it".
/// <para>
/// The rects are the point of the record. <see cref="Arranged"/> is the node's own geometry in page space,
/// <see cref="Window"/> is what the widget's Draw was handed after the pass's origin was applied, and
/// <see cref="Origin"/> is that offset - so a reader can tell which coordinate space a number came from
/// instead of comparing two spaces and calling the difference a defect. <see cref="Clip"/> is the boundary
/// actually in force (window space), which is neither of those two.
/// </para>
/// <para>
/// <b>Mutable inside the assembly, copied on the way out.</b> A sample is a work item the capture fills in
/// once and reads back; what a consumer sees is <see cref="UiDevNodeSnapshot"/>, a fresh object built from it
/// by <see cref="UiDevGeometryCapture.Snapshot"/>. Samples never leave this assembly, so there is no promise
/// here to keep - and the copy, not the sample, is what carries the guarantee.
/// </para>
/// </summary>
internal struct UiDevGeometrySample
{
    /// <summary>The session frame this sample was taken in.</summary>
    internal int Pass { get; set; }

    /// <summary>The element's display path, <c>UiNodeId.Path</c>.</summary>
    internal string Path { get; set; }

    /// <summary>The element's canonical identity, <c>UiNodeId.Key</c> - not a re-derived display string.</summary>
    internal string NodeKey { get; set; }

    /// <summary>The element's declared ordinal among its siblings; -1 for a root or an orphan.</summary>
    internal int Ordinal { get; set; }

    /// <summary>The element's declared <c>Id</c>, or "".</summary>
    internal string ElementId { get; set; }

    /// <summary>The widget kind, or the container's element name.</summary>
    internal string Kind { get; set; }

    /// <summary>True for an arranged container rather than a drawn widget.</summary>
    internal bool IsContainer { get; set; }

    /// <summary>The container's element name (Scroll, Column, ...), or empty for a widget.</summary>
    internal string ContainerKind { get; set; }

    /// <summary>True for the scoped containers (Scroll/Clip), whose rect is a viewport.</summary>
    internal bool IsScopedContainer { get; set; }

    /// <summary>True when the element pushed a hit layer, i.e. a pointer can land on it.</summary>
    internal bool IsHitSurface { get; set; }

    /// <summary>
    /// The engine's arranged rect for this entry, exactly as the node publishes it. Page space at the top
    /// level; inside a scrolled container it is that container's content-local space, which is why the draw
    /// rect is recorded beside it instead of being derived from it.
    /// </summary>
    internal Rect Arranged { get; set; }

    /// <summary>The rect the widget's Draw was handed - the element's own drawing space.</summary>
    internal Rect Draw { get; set; }

    /// <summary>The draw rect converted into Host window space, which is where the pointer and hit stack live.</summary>
    internal Rect Window { get; set; }

    /// <summary>The draw-local to window-space offset this sample was converted with.</summary>
    internal Vector2 Origin { get; set; }

    /// <summary>Scroll content rect (scroll-local) for a scoped container, else null.</summary>
    internal Rect? Content { get; set; }

    /// <summary>Fixed, Auto or MatchContent, read from the declaration.</summary>
    internal string HeightMode { get; set; }

    /// <summary>The height the element resolved to, which is what the height mode decided.</summary>
    internal float ResolvedHeight { get; set; }

    /// <summary>The SCOPE's font: the resolved theme's typography selection for this element's scope.</summary>
    internal UiFont ScopeFont { get; set; }

    /// <summary>
    /// The font text is finally painted with. Always null at this site - the choice is made inside the
    /// element's own draw, which runs after this sample - and it is a FIELD rather than a literal at the
    /// rendering site, so both renderings derive the documented unknown from the same fact instead of one of
    /// them asserting it.
    /// </summary>
    internal UiFont? PaintedFont { get; set; }

    /// <summary>
    /// The element's effective appearance, resolved through the registry's own seam at the observation site:
    /// the canonical look the kind selected, whether the element declared one, the kind's default and accepted
    /// set, where the resolver came from, and whether the exact (scope, kind) pair is registered.
    /// </summary>
    internal string Appearance { get; set; }

    internal bool AppearanceDeclared { get; set; }
    internal string AppearanceDefault { get; set; }
    internal string AppearanceSupported { get; set; }
    internal string AppearanceSource { get; set; }
    internal bool AppearancePairRegistered { get; set; }

    /// <summary>The nearest declared scheme on the element's chain, or "" - read through the resolver.</summary>
    internal string Scheme { get; set; }

    /// <summary>The nearest declared density on the element's chain, or "".</summary>
    internal string Density { get; set; }

    /// <summary>"page" when the element used the injected theme instance, "region" when a resolver clone.</summary>
    internal string ThemeOrigin { get; set; }

    /// <summary>The resolved theme's layout revision.</summary>
    internal int ThemeLayoutRevision { get; set; }

    /// <summary>The resolved theme's colour revision.</summary>
    internal int ThemeColourRevision { get; set; }

    /// <summary>The authored Tone text as written, or "".</summary>
    internal string Tone { get; set; }

    /// <summary>The authored Emphasis text as written, or "".</summary>
    internal string Emphasis { get; set; }

    /// <summary>The published disabled state of the element.</summary>
    internal bool Disabled { get; set; }

    /// <summary>The effective clip in force (Host window space), or null when none could be read.</summary>
    internal Rect? Clip { get; set; }

    /// <summary>
    /// The hover boundary. Always null at this site - the hover claim is published during the element's own
    /// draw, which runs after the sample - and it is a FIELD rather than a literal at the rendering site so
    /// that the text and the data both render this same fact. A boundary hard-coded as "unknown" in one
    /// rendering and derived from the sample in the other is a divergence nothing would catch.
    /// </summary>
    internal Rect? Hover { get; set; }

    /// <summary>Always null at this site: this library publishes no focus owner to report.</summary>
    internal Rect? Focus { get; set; }

    /// <summary>The session's open popup owner, or "".</summary>
    internal string OpenPopupId { get; set; }

    /// <summary>The open popup's anchor (Host window space), or null when no popup was open.</summary>
    internal Rect? PopupAnchor { get; set; }

    /// <summary>True when this element owns the session's open popup.</summary>
    internal bool OwnsOpenPopup { get; set; }
}

/// <summary>
/// One development-only input sample: an element's answer to a hit query, so "which element did this press
/// land on, and if none, why not" has an answer that is not a guess. The verdict vocabulary is the funnel's
/// own two refusals plus the native control's answer: <c>disabled</c>, <c>covered</c>, <c>hit</c>,
/// <c>miss</c>.
/// <para>
/// Consumption is recorded as the phase TRANSITION beside it rather than as a separate decision: a reader
/// can see that <c>MouseDown</c> became <c>Used</c> across the query, which is what consuming an event means
/// in this backend. A pass that entered already <c>Used</c> marks its input origin unknown instead of
/// guessing which phase started it.
/// </para>
/// </summary>
internal struct UiDevInputSample
{
    internal int Pass { get; set; }

    /// <summary>The queried element's canonical identity, or "" when no node applied.</summary>
    internal string NodeKey { get; set; }

    internal string Path { get; set; }
    internal string Kind { get; set; }

    /// <summary>The queried element's declared <c>Id</c>, or "".</summary>
    internal string ElementId { get; set; }

    /// <summary>The queried rect in Host window space.</summary>
    internal Rect Rect { get; set; }

    /// <summary>The pointer in Host window space - the same space as <see cref="Rect"/>.</summary>
    internal Vector2 Point { get; set; }

    /// <summary>What the funnel decided.</summary>
    internal string Verdict { get; set; }

    /// <summary>The phase before the query ran.</summary>
    internal string EventBefore { get; set; }

    /// <summary>The phase after the query ran.</summary>
    internal string EventAfter { get; set; }

    /// <summary>True when the phase moved from a usable one to Used across this query.</summary>
    internal bool Consumed { get; set; }

    /// <summary>True when the pass entered already Used, so the originating input is not observable.</summary>
    internal bool OriginUnknown { get; set; }

    /// <summary>The phase the pass was entered with.</summary>
    internal string EntryEvent { get; set; }
}

/// <summary>
/// The per-subscription store behind the geometry instrument. It hangs off an existing
/// <see cref="UiDiagnosticSubscription"/>, so its lifetime, isolation and release path are the diagnostic
/// surface's own and the instrument adds no process-wide state of any kind.
/// <para>
/// <b>One pass at a time.</b> The buffer describes the most recent frame and is replaced when the frame
/// number moves, because the question this answers is "what did the layout do", and a frame is the unit a
/// layout answer exists in. Both halves are bounded and count what they dropped, so a large page reports a
/// head plus a dropped count rather than an unbounded log.
/// </para>
/// <para>
/// <b>One pass, two switches.</b> <see cref="Dump"/> renders the stored samples as diffable text and
/// <see cref="Snapshot"/> renders the SAME samples as a freshly-copied object graph - those two are
/// renderings of this capture and cannot describe different frames. The outline
/// (<see cref="UiDevGeometryProbe.Outline"/>) is a THIRD consumer of the same engine walk, but since
/// SA1.5 its switch lives on the subscription, not here: with this capture live it paints exactly the
/// RETAINED entries (this class's bound is what the picture must be faithful to), and with no capture
/// it still paints the draw-step entries it is called for while storing nothing.
/// </para>
/// </summary>
internal sealed class UiDevGeometryCapture
{
    /// <summary>Geometry samples one pass keeps; the rest of the pass is counted, never silently lost.</summary>
    internal const int MaxGeometrySamples = 512;

    /// <summary>Input samples one pass keeps.</summary>
    internal const int MaxInputSamples = 128;

    private readonly List<UiDevGeometrySample> geometry = new List<UiDevGeometrySample>();
    private readonly List<UiDevInputSample> input = new List<UiDevInputSample>();
    private int pass = -1;

    internal int Pass => pass;
    internal string EntryEvent { get; private set; } = "none";

    internal int GeometryCount => geometry.Count;

    internal int InputCount => input.Count;

    internal long DroppedGeometry { get; private set; }

    internal long DroppedInput { get; private set; }

    /// <summary>Starts a new pass, dropping the previous one's samples. A repeat of the current pass no-ops.</summary>
    internal void BeginPass(int current)
    {
        if (current == pass) return;
        pass = current;
        EntryEvent = UiNative.DiagnosticEventName();
        geometry.Clear();
        input.Clear();
        DroppedGeometry = 0;
        DroppedInput = 0;
    }

    /// <summary>
    /// Keeps one geometry sample, or counts it as dropped when the bound is reached. The answer is what lets
    /// the overlay paint exactly the retained entries: a capture that dropped a node must not outline it
    /// either, or the picture would show something the data does not.
    /// </summary>
    internal bool Add(UiDevGeometrySample sample)
    {
        if (geometry.Count >= MaxGeometrySamples)
        {
            DroppedGeometry++;
            return false;
        }

        geometry.Add(sample);
        return true;
    }
    internal void Add(UiDevInputSample sample)
    {
        if (input.Count >= MaxInputSamples)
        {
            DroppedInput++;
            return;
        }

        input.Add(sample);
    }

    /// <summary>Empties both halves without touching the pass number (the outline switch is not here since SA1.5).</summary>
    internal void Clear()
    {
        geometry.Clear();
        input.Clear();
        DroppedGeometry = 0;
        DroppedInput = 0;
    }

    /// <summary>
    /// The diffable text: one line per node in paint order, then one line per input answer, with every
    /// number in the invariant culture at three decimals. Two dumps of the same page differ exactly where
    /// the geometry differs, which is the whole reason the numbers are not rounded to pixels here.
    /// <para>
    /// The fields are appended in a fixed order and never reordered, because this text is what a human
    /// diffs: a reordered line would read as a change where nothing moved.
    /// </para>
    /// </summary>
    internal string Dump(string host, int sessionId)
    {
        var text = new StringBuilder();
        text.Append("[ferritelib.geometry] pass=").Append(pass.ToString(CultureInfo.InvariantCulture))
            .Append(" host=").Append(host ?? "")
            .Append(" session=").Append(sessionId.ToString(CultureInfo.InvariantCulture))
            .Append(" nodes=").Append(geometry.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" nodes-dropped=").Append(DroppedGeometry.ToString(CultureInfo.InvariantCulture))
            .Append(" inputs=").Append(input.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" inputs-dropped=").Append(DroppedInput.ToString(CultureInfo.InvariantCulture))
            .Append(" event=").Append(EntryEvent)
            .Append(EntryEvent == "Used" ? " input-origin=unknown" : "")
            .Append('\n');

        for (int i = 0; i < geometry.Count; i++)
        {
            UiDevGeometrySample sample = geometry[i];
            text.Append(sample.IsScopedContainer ? "viewport " : "rect ")
                .Append("path=").Append(sample.Path)
                .Append(" kind=").Append(sample.Kind)
                .Append(sample.IsContainer ? " container=yes" : " container=no")
                .Append(" arranged=").Append(Rect(sample.Arranged))
                .Append(" draw=").Append(Rect(sample.Draw))
                .Append(" window=").Append(Rect(sample.Window))
                .Append(" origin=").Append(Point(sample.Origin))
                .Append(" height=").Append(sample.HeightMode)
                .Append(" h=").Append(Number(sample.ResolvedHeight))
                // Every boundary is rendered from the SAME field the snapshot renders, through the same
                // known/unknown decision, so the two renderings cannot disagree about a boundary neither of
                // them can observe. Content is always printed for that reason: the data carries an explicit
                // unknown, and a text rendering that simply omitted it would be a second, quieter answer.
                .Append(" content=").Append(Boundary(sample.Content, UiDevNodeSnapshot.ContentUnknownReason));

            // Identity, appearance and provenance, then the boundaries. Appended after the geometry so the
            // existing prefix of every line is unchanged: a dump a human has been reading keeps reading.
            text.Append(" key=").Append(sample.NodeKey)
                .Append(" ordinal=").Append(sample.Ordinal.ToString(CultureInfo.InvariantCulture))
                .Append(" id=").Append(sample.ElementId)
                .Append(" hit=").Append(sample.IsHitSurface ? "yes" : "no")
                .Append(" scope-font=").Append(sample.ScopeFont.ToString())
                .Append(" painted-font=").Append(Font(sample.PaintedFont, UiDevNodeSnapshot.PaintedFontUnknownReason))
                .Append(" appearance=").Append(Sample(sample.Appearance))
                .Append(" appearance-declared=").Append(sample.AppearanceDeclared ? "yes" : "no")
                .Append(" appearance-default=").Append(Sample(sample.AppearanceDefault))
                .Append(" appearance-supports=").Append(Sample(sample.AppearanceSupported))
                .Append(" appearance-source=").Append(sample.AppearanceSource)
                .Append(" appearance-pair=").Append(sample.AppearancePairRegistered ? "yes" : "no")
                .Append(" scheme=").Append(Sample(sample.Scheme))
                .Append(" density=").Append(Sample(sample.Density))
                .Append(" theme=").Append(sample.ThemeOrigin)
                .Append(" theme-rev=").Append(sample.ThemeLayoutRevision.ToString(CultureInfo.InvariantCulture))
                .Append('/').Append(sample.ThemeColourRevision.ToString(CultureInfo.InvariantCulture))
                .Append(" tone=").Append(Sample(sample.Tone))
                .Append(" emphasis=").Append(Sample(sample.Emphasis))
                .Append(" disabled=").Append(sample.Disabled ? "yes" : "no")
                .Append(" clip=").Append(Boundary(sample.Clip, UiDevNodeSnapshot.ClipUnknownReason))
                .Append(" hover=").Append(Boundary(sample.Hover, UiDevNodeSnapshot.HoverUnknownReason))
                .Append(" focus=").Append(Boundary(sample.Focus, UiDevNodeSnapshot.FocusUnknownReason))
                .Append(" popup=").Append(Sample(sample.OpenPopupId))
                .Append(" popup-anchor=").Append(Boundary(sample.PopupAnchor, UiDevNodeSnapshot.NoOpenPopupUnknownReason))
                .Append(" owns-popup=").Append(sample.OwnsOpenPopup ? "yes" : "no")
                .Append('\n');
        }

        for (int i = 0; i < input.Count; i++)
        {
            UiDevInputSample sample = input[i];
            text.Append("input path=").Append(sample.Path)
                .Append(" kind=").Append(sample.Kind)
                .Append(" point=").Append(Point(sample.Point))
                .Append(" rect=").Append(Rect(sample.Rect))
                .Append(" verdict=").Append(sample.Verdict)
                .Append(" event-before=").Append(sample.EventBefore)
                .Append(" event-after=").Append(sample.EventAfter)
                .Append(" consumed=").Append(sample.Consumed ? "yes" : "no")
                .Append(" origin=").Append(sample.OriginUnknown ? "unknown" : "known")
                .Append(" entry=").Append(sample.EntryEvent)
                .Append(" key=").Append(sample.NodeKey)
                .Append(" id=").Append(sample.ElementId)
                .Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// The same capture as data. What is guaranteed: every node and input is a fresh object per call, the two
    /// arrays are allocated by the call, and the snapshot itself is a new instance per call - so nothing a
    /// consumer holds is a view over this capture's buffers, and a later pass cannot change it.
    /// <para>
    /// What is NOT promised: the lists are those arrays, so a caller that down-casts its OWN copy to an array
    /// and mutates it changes that copy. It cannot reach the collector, the live pass or any other snapshot,
    /// which is the property that matters here; "immutable collection" would be the stronger and false claim.
    /// </para>
    /// </summary>
    internal UiDevGeometrySnapshot Snapshot(string host, int sessionId)
    {
        var nodes = new UiDevNodeSnapshot[geometry.Count];
        for (int i = 0; i < geometry.Count; i++)
        {
            UiDevGeometrySample sample = geometry[i];
            nodes[i] = new UiDevNodeSnapshot
            {
                Pass = sample.Pass,
                NodeKey = sample.NodeKey,
                Path = sample.Path,
                Ordinal = sample.Ordinal,
                ElementId = sample.ElementId,
                Kind = sample.Kind,
                IsContainer = sample.IsContainer,
                ContainerKind = sample.ContainerKind,
                IsScopedContainer = sample.IsScopedContainer,
                IsHitSurface = sample.IsHitSurface,
                Arranged = sample.Arranged,
                Draw = sample.Draw,
                Window = sample.Window,
                Origin = sample.Origin,
                Content = sample.Content.HasValue
                    ? UiDevBoundary.Known(sample.Content.Value)
                    : UiDevBoundary.Unknown(UiDevNodeSnapshot.ContentUnknownReason),
                HeightMode = sample.HeightMode,
                ResolvedHeight = sample.ResolvedHeight,
                ScopeFont = sample.ScopeFont,
                PaintedFont = sample.PaintedFont,
                Appearance = sample.Appearance,
                AppearanceDeclared = sample.AppearanceDeclared,
                AppearanceDefault = sample.AppearanceDefault,
                AppearanceSupported = sample.AppearanceSupported,
                AppearanceSource = sample.AppearanceSource,
                AppearancePairRegistered = sample.AppearancePairRegistered,
                Scheme = sample.Scheme,
                Density = sample.Density,
                ThemeOrigin = sample.ThemeOrigin,
                ThemeLayoutRevision = sample.ThemeLayoutRevision,
                ThemeColourRevision = sample.ThemeColourRevision,
                Tone = sample.Tone,
                Emphasis = sample.Emphasis,
                Disabled = sample.Disabled,
                Clip = sample.Clip.HasValue
                    ? UiDevBoundary.Known(sample.Clip.Value)
                    : UiDevBoundary.Unknown(UiDevNodeSnapshot.ClipUnknownReason),
                // The two boundaries this site genuinely cannot answer, built from the sample's own field and
                // recorded as unknown WITH the reason rather than as an empty rect a reader would take for a
                // measurement. The text rendering reads the same field, so neither can drift alone.
                Hover = sample.Hover.HasValue
                    ? UiDevBoundary.Known(sample.Hover.Value)
                    : UiDevBoundary.Unknown(UiDevNodeSnapshot.HoverUnknownReason),
                Focus = sample.Focus.HasValue
                    ? UiDevBoundary.Known(sample.Focus.Value)
                    : UiDevBoundary.Unknown(UiDevNodeSnapshot.FocusUnknownReason),
                OpenPopupId = sample.OpenPopupId,
                PopupAnchor = sample.PopupAnchor.HasValue
                    ? UiDevBoundary.Known(sample.PopupAnchor.Value)
                    : UiDevBoundary.Unknown(UiDevNodeSnapshot.NoOpenPopupUnknownReason),
                OwnsOpenPopup = sample.OwnsOpenPopup
            };
        }

        var inputs = new UiDevInputSnapshot[input.Count];
        for (int i = 0; i < input.Count; i++)
        {
            UiDevInputSample sample = input[i];
            inputs[i] = new UiDevInputSnapshot
            {
                Pass = sample.Pass,
                NodeKey = sample.NodeKey,
                Path = sample.Path,
                Kind = sample.Kind,
                ElementId = sample.ElementId,
                Rect = sample.Rect,
                Point = sample.Point,
                Verdict = sample.Verdict,
                EventBefore = sample.EventBefore,
                EventAfter = sample.EventAfter,
                Consumed = sample.Consumed,
                OriginUnknown = sample.OriginUnknown,
                EntryEvent = sample.EntryEvent
            };
        }

        // A fresh instance per call, written in ONE place. Fill assigns every field on every call, which is
        // what makes "a fresh instance" a one-line property rather than a property of each assignment - and
        // what the mutation batch's c7 subverts: reusing an instance there refreshes it in place instead of
        // handing out a copy, so the old snapshot follows later draws.
        UiDevGeometrySnapshot snapshot = new UiDevGeometrySnapshot();
        Fill(snapshot, host, sessionId, nodes, inputs);
        return snapshot;
    }

    /// <summary>
    /// Writes one pass's values onto <paramref name="snapshot"/>: every field, every call, no early return.
    /// The completeness is the contract - a field this method forgot would be the one field a reused instance
    /// could keep stale - so it stays one flat list of assignments.
    /// </summary>
    private void Fill(
        UiDevGeometrySnapshot snapshot,
        string host,
        int sessionId,
        UiDevNodeSnapshot[] nodes,
        UiDevInputSnapshot[] inputs)
    {
        snapshot.Host = host ?? "";
        snapshot.SessionId = sessionId;
        snapshot.Pass = pass;
        snapshot.EntryEvent = EntryEvent;
        snapshot.Nodes = nodes;
        snapshot.Inputs = inputs;
        snapshot.NodesDropped = DroppedGeometry;
        snapshot.InputsDropped = DroppedInput;
    }

    private static string Number(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Point(Vector2 point)
    {
        return "(" + Number(point.x) + "," + Number(point.y) + ")";
    }

    private static string Rect(Rect rect)
    {
        return "(" + Number(rect.x) + "," + Number(rect.y) + ","
            + Number(rect.width) + "," + Number(rect.height) + ")";
    }

    /// <summary>A text field, with an empty value spelled so a reader can tell "none" from "missing column".</summary>
    private static string Sample(string value)
    {
        return string.IsNullOrEmpty(value) ? "-" : value;
    }

    /// <summary>A boundary as text: the rect, or <c>unknown(reason)</c>.</summary>
    private static string Boundary(Rect? rect, string unknownReason)
    {
        return rect.HasValue ? Rect(rect.Value) : "unknown(" + unknownReason + ")";
    }

    /// <summary>
    /// A font as text: the name, or <c>unknown(reason)</c>. The known half is what a site that can observe the
    /// painted font would print; today the only font this capture observes is the scope's, and the painted one
    /// is recorded as unknown rather than replaced by it.
    /// </summary>
    private static string Font(UiFont? font, string unknownReason)
    {
        return font.HasValue ? font.Value.ToString() : "unknown(" + unknownReason + ")";
    }
}

/// <summary>
/// The development-only numeric instrument: where every arranged element actually is, which identity and
/// appearance it drew with, what a press landed on, and - on request - an outline of the sampled rects.
/// <para>
/// <b>Compiled out of a release build.</b> The whole capture side of this file is inside <c>#if FER_DEV</c>,
/// and every call site is guarded the same way, so a release payload carries no capture, no buffer and no
/// call: the enable path on <see cref="UiDiagnosticSubscription.GeometryEnabled"/> refuses rather than
/// degrading into a tool that reports nothing and looks fine. The gate is the build configuration, and a
/// lane holds both halves. The snapshot TYPES a consumer reads are deliberately <b>not</b> inside the gate
/// (see <c>UiDevGeometrySnapshot.cs</c>): a public type that vanishes between two configurations of the same
/// contract is a compile-against-Dev/ship-against-Release trap, and the reader answers <c>false</c> in the
/// configuration that has no capture instead.
/// </para>
/// <para>
/// <b>One place, three chokepoints, one capture.</b> The instrument is this file; it is fed from the three
/// points that already own the facts - the engine's per-entry draw walk (rects, height mode, identity,
/// appearance, clip and container viewport), the funnel's click decision (the verdict and the phase
/// transition), and the engine's entry again for the optional outline. Nothing is recorded from a widget, so
/// a kind cannot report a number the engine disagrees with. The human dump, the structured snapshot and the
/// outline are three renderings of that one capture, not three collectors.
/// </para>
/// <para>
/// <b>Observation only.</b> No sample reaches the layout, the session, the hit stack or the fit audit: the
/// outline is painted after the element it outlines has drawn, in that element's own draw-local space.
/// </para>
/// </summary>
internal static class UiDevGeometryProbe
{
    /// <summary>Colour of the outline painted around each sampled element's draw rect.</summary>
    private static readonly Color OutlineInk = new Color(0.16f, 0.86f, 1f, 1f);

    /// <summary>The authored role attribute, read as text: the instrument never interprets a role.</summary>
    private const string ToneAttribute = "Tone";

    /// <summary>The authored emphasis attribute, read as text.</summary>
    private const string EmphasisAttribute = "Emphasis";

    /// <summary>
    /// Records one arranged entry. Called from the engine's draw walk, where the arranged rect, the draw
    /// rect, the content rect, the resolved height mode and the resolved theme are all already in hand.
    /// <para>
    /// <paramref name="resolver"/> and <paramref name="chain"/> are the same resolver and the same chain the
    /// theme beside them was resolved from, so the scheme and density names reported here are the
    /// resolution's own input rather than a second reading of the manifest. Both are read only when a
    /// capture is actually live, which is what keeps the instrument free for a host that never opted in.
    /// </para>
    /// <para>
    /// The answer says whether the capture RETAINED this entry, which is what the optional overlay keys its
    /// painting off: while a capture is live, the picture and the data are one bounded capture rendered twice,
    /// not two collectors. With no capture the same call paints every entry reaching the draw step and
    /// stores nothing (SA1.5; the scoped viewport's own rect has no draw step - see Outline).
    /// </para>
    /// </summary>
    internal static bool Note(
        UiNode node,
        UiElementSpec spec,
        string containerKind,
        bool isContainer,
        bool isScopedContainer,
        bool isHitSurface,
        Rect arranged,
        Rect? contentRect,
        Rect drawRect,
        UiWidgetContext ctx,
        bool themeIsRegionClone,
        UiStyleResolver? resolver,
        IReadOnlyList<UiStyleDeclaration>? chain)
    {
        UiDevGeometryCapture? capture = UiDiagnosticHub.ActiveSubscription?.Geometry;
        if (capture == null) return false;

        capture.BeginPass(ctx.Session.Frame);
        string kind = isContainer && containerKind.Length > 0 ? containerKind : spec.Kind;
        string scheme = "";
        string density = "";
        if (resolver != null && chain != null)
        {
            scheme = resolver.ResolveScheme(chain) ?? "";
            density = resolver.ResolveDensity(chain) ?? "";
        }

        UiTheme theme = ctx.Theme;
        AppearanceFacts appearance = AppearanceOf(ctx.Source, kind, spec);
        return capture.Add(new UiDevGeometrySample
        {
            Pass = ctx.Session.Frame,
            Path = node.Path,
            NodeKey = node.Id.Key,
            Ordinal = node.Ordinal,
            ElementId = node.ElementId,
            Kind = kind,
            IsContainer = isContainer,
            ContainerKind = containerKind,
            IsScopedContainer = isScopedContainer,
            IsHitSurface = isHitSurface,
            Arranged = arranged,
            Draw = drawRect,
            Window = ctx.ToWindowRect(drawRect),
            Origin = ctx.WindowOrigin,
            Content = contentRect,
            HeightMode = HeightMode(spec),
            ResolvedHeight = arranged.height,
            ScopeFont = theme.DefaultFont,
            // The transparent case, recorded as the fact it is: the font a label finally paints with is chosen
            // inside the element's own draw, so nothing here may stand in for it.
            PaintedFont = null,
            Appearance = appearance.Value,
            AppearanceDeclared = appearance.Declared,
            AppearanceDefault = appearance.Default,
            AppearanceSupported = appearance.Supported,
            AppearanceSource = appearance.Source,
            AppearancePairRegistered = appearance.PairRegistered,
            Scheme = scheme,
            Density = density,
            ThemeOrigin = themeIsRegionClone ? "region" : "page",
            ThemeLayoutRevision = theme.LayoutRevision,
            ThemeColourRevision = theme.ColourRevision,
            Tone = Authored(spec, ToneAttribute),
            Emphasis = Authored(spec, EmphasisAttribute),
            Disabled = node.IsDisabled,
            Clip = ctx.Session.CurrentClip,
            // The two boundaries this site genuinely cannot observe. They are left null HERE, at the one
            // place that knows why, and both renderings derive the documented unknown from that field - so
            // the reason lives with the observation, not with each renderer.
            Hover = null,
            Focus = null,
            OpenPopupId = ctx.Session.OpenPopupId ?? "",
            PopupAnchor = ctx.Session.OpenPopupAnchor,
            OwnsOpenPopup = node.ElementId.Length > 0 && ctx.Session.IsPopupOpen(node.ElementId)
        });
    }

    /// <summary>
    /// The element's effective appearance and where it came from, read through the SAME registry seam the
    /// engine's natural-body entry point uses. Nothing is registered, resolved twice or re-derived here: the
    /// instrument asks the one registry for the resolver the kind declared, asks that resolver what the
    /// element's own declaration means against the kind's own set, and asks the registry two provenance
    /// questions that only the registry can answer - whether the exact (scope, kind) pair is declared, and
    /// which scope's look set actually answered (proved by comparing the returned instance against the core
    /// one, so it is identity rather than a heuristic).
    /// </summary>
    private static AppearanceFacts AppearanceOf(string scope, string kind, UiElementSpec spec)
    {
        UiAppearanceResolver? effective = UiWidgetRegistry.GetAppearanceResolver(scope, kind);
        if (effective == null)
        {
            // A kind that declares no looks paints one fixed shape. That is a fact about the kind, so this is
            // an empty answer rather than an unknown one.
            return new AppearanceFacts("", false, "", "", UiDevNodeSnapshot.NoAppearanceSource, false);
        }

        ResolvedAppearance resolved = effective.Resolve(spec);
        UiAppearanceResolver? core = UiWidgetRegistry.GetAppearanceResolver(UiWidgetRegistry.CoreScope, kind);
        string source = ReferenceEquals(effective, core)
            ? UiDevNodeSnapshot.CoreAppearanceSource
            : UiDevNodeSnapshot.ScopeAppearanceSource;
        return new AppearanceFacts(
            resolved.Value,
            resolved.Declared,
            effective.Default,
            Supported(effective),
            source,
            UiWidgetRegistry.TryGetDeclaration(scope, kind, out _));
    }

    /// <summary>The kind's accepted look names in ordinal order, or "" when the set is empty.</summary>
    private static string Supported(UiAppearanceResolver resolver)
    {
        var names = new List<string>();
        foreach (string name in resolver.Supported) names.Add(name);
        if (names.Count == 0) return "";
        names.Sort(StringComparer.Ordinal);
        return string.Join(",", names);
    }

    /// <summary>One element's resolved appearance and provenance, as the sample carries it.</summary>
    private readonly struct AppearanceFacts
    {
        internal AppearanceFacts(
            string value, bool declared, string fallback, string supported, string source, bool pairRegistered)
        {
            Value = value;
            Declared = declared;
            Default = fallback;
            Supported = supported;
            Source = source;
            PairRegistered = pairRegistered;
        }

        internal string Value { get; }
        internal bool Declared { get; }
        internal string Default { get; }
        internal string Supported { get; }
        internal string Source { get; }
        internal bool PairRegistered { get; }
    }

    /// <summary>
    /// Paints the outline over one entry's draw rect, after that entry has drawn. Drawn in draw-local space
    /// so it lines up with the element by construction, and through the theme's single solid outlet so the
    /// backend-containment allowlist does not grow a second one.
    /// <para>
    /// <b>SA1.5: the outline switch is independent of the capture switch.</b> Both modes render the SAME
    /// engine walk - this very call site, in this very draw-local rect - so there is no second layout
    /// system to disagree with. The call site is the engine's per-entry DRAW step, which is also the
    /// stated coverage limit: a scoped container's own viewport rect has no draw step at its own entry
    /// (its content walks nested below and is outlined entry by entry), so the viewport band itself is
    /// deliberately not outlined - <c>KernelDevGeometryTests</c> pins the outlined set as retained minus
    /// scoped, not a silent approximation.
    /// <list type="bullet">
    /// <item>capture on: <paramref name="retained"/> is the answer <see cref="Note"/> gave for this same
    /// entry, and the picture stays the third rendering of the one bounded capture - a node the capture
    /// dropped is not outlined as if the data described it;</item>
    /// <item>capture off: there is no bounded list to be faithful to, so every entry reaching this step
    /// is outlined, nothing is sampled, no buffer exists, and the dump/snapshot readers keep answering
    /// truthfully that nothing was captured.</item>
    /// </list>
    /// </para>
    /// </summary>
    internal static void Outline(Rect drawRect, bool retained)
    {
        UiDiagnosticSubscription? subscription = UiDiagnosticHub.ActiveSubscription;
        if (subscription == null || !subscription.GeometryOverlay) return;
        if (subscription.GeometryEnabled && !retained) return;
        if (drawRect.width <= 0f || drawRect.height <= 0f) return;

        const float hairline = 1f;
        UiThemeDraw.Solid(new Rect(drawRect.x, drawRect.y, drawRect.width, hairline), OutlineInk);
        UiThemeDraw.Solid(new Rect(drawRect.x, drawRect.yMax - hairline, drawRect.width, hairline), OutlineInk);
        UiThemeDraw.Solid(new Rect(drawRect.x, drawRect.y, hairline, drawRect.height), OutlineInk);
        UiThemeDraw.Solid(new Rect(drawRect.xMax - hairline, drawRect.y, hairline, drawRect.height), OutlineInk);
    }

    /// <summary>
    /// Records one hit query's verdict. Sampling is press-shaped on purpose: a button is queried every frame
    /// it draws, and a per-frame line per control would be a log rather than a numeric answer, so a sample is
    /// taken when the pass started with a pointer press/release, or the native control fired. A pass that
    /// entered already Used is also observable, but explicitly marks its original input kind unknown.
    /// </summary>
    internal static void NoteInput(UiWidgetContext ctx, UiNode? element, Rect rect, string verdict, string eventBefore)
    {
        UiDevGeometryCapture? capture = UiDiagnosticHub.ActiveSubscription?.Geometry;
        if (capture == null) return;

        capture.BeginPass(ctx.Session.Frame);
        bool sampleInput = string.Equals(verdict, "hit", StringComparison.Ordinal)
            || capture.EntryEvent == "MouseDown" || capture.EntryEvent == "MouseUp"
            || capture.EntryEvent == "Used"
            || UiNative.IsPointerDown()
            || UiNative.IsPointerUp();
        if (!sampleInput) return;

        string eventAfter = UiNative.DiagnosticEventName();
        Vector2 pointer = UiNative.PointerPosition();
        capture.Add(new UiDevInputSample
        {
            Pass = ctx.Session.Frame,
            NodeKey = element is null ? "" : element.Id.Key,
            Path = element is null ? ctx.ElementPath : element.Path,
            Kind = element is null ? "" : element.Kind,
            ElementId = element is null ? "" : element.ElementId,
            Rect = ctx.ToWindowRect(rect),
            Point = ctx.ToWindowRect(new Rect(pointer.x, pointer.y, 0f, 0f)).position,
            Verdict = verdict,
            EventBefore = eventBefore,
            EventAfter = eventAfter,
            // Consumption is the phase transition and nothing else: a query that was handed an already-Used
            // event did not consume it, and a query that left a usable phase usable did not either.
            Consumed = string.Equals(eventAfter, "Used", StringComparison.Ordinal)
                && !string.Equals(eventBefore, "Used", StringComparison.Ordinal),
            OriginUnknown = string.Equals(capture.EntryEvent, "Used", StringComparison.Ordinal),
            EntryEvent = capture.EntryEvent
        });
    }

    /// <summary>The declared height mode, read the way the engine reads it rather than re-derived.</summary>
    private static string HeightMode(UiElementSpec spec)
    {
        if (UiPlacement.IsMatchContentHeight(spec)) return "MatchContent";
        if (!spec.TryGetAttribute("Height", out string raw)) return "Auto";

        string value = raw.Trim();
        if (value.Length == 0) return "Auto";
        return string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase) ? "Auto" : "Fixed";
    }

    /// <summary>
    /// An authored role attribute as written, trimmed. Deliberately NOT the role parser: that parser reports
    /// a retired or unknown spelling on the appearance channel, and an instrument that records a note by
    /// looking would be changing the audit output it exists to observe.
    /// </summary>
    private static string Authored(UiElementSpec spec, string attribute)
    {
        return spec.TryGetAttribute(attribute, out string raw) ? (raw ?? "").Trim() : "";
    }
}
#endif
