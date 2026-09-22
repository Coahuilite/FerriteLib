#if FER_DEV
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One development-only geometry sample: what one arranged element's rectangles were in the pass that drew
/// it. The instrument observes; it never decides. Nothing here is read back by the engine, the session or a
/// widget, which is what keeps "measuring it" from changing "it".
/// <para>
/// The two rects are the point of the record. <see cref="Arranged"/> is the node's own geometry in page
/// space, <see cref="Window"/> is what the widget's Draw was handed after the pass's origin was applied,
/// and <see cref="Origin"/> is that offset - so a reader can tell which coordinate space a number came from
/// instead of comparing two spaces and calling the difference a defect.
/// </para>
/// </summary>
internal readonly struct UiDevGeometrySample
{
    internal UiDevGeometrySample(
        int pass,
        string path,
        string kind,
        bool isContainer,
        string containerKind,
        bool isScopedContainer,
        Rect arranged,
        Rect draw,
        Rect window,
        Vector2 origin,
        Rect? content,
        string heightMode,
        float resolvedHeight)
    {
        Pass = pass;
        Path = path ?? "";
        Kind = kind ?? "";
        IsContainer = isContainer;
        ContainerKind = containerKind ?? "";
        IsScopedContainer = isScopedContainer;
        Arranged = arranged;
        Draw = draw;
        Window = window;
        Origin = origin;
        Content = content;
        HeightMode = heightMode ?? "";
        ResolvedHeight = resolvedHeight;
    }

    /// <summary>The session frame this sample was taken in.</summary>
    internal int Pass { get; }

    /// <summary>The element's display path.</summary>
    internal string Path { get; }

    /// <summary>The widget kind, or the container's element name.</summary>
    internal string Kind { get; }

    /// <summary>True for an arranged container rather than a drawn widget.</summary>
    internal bool IsContainer { get; }

    /// <summary>The container's element name (Scroll, Column, ...), or empty for a widget.</summary>
    internal string ContainerKind { get; }

    /// <summary>True for the scoped containers (Scroll/Clip), whose rect is a viewport.</summary>
    internal bool IsScopedContainer { get; }

    /// <summary>
    /// The engine's arranged rect for this entry, exactly as the node publishes it. Page space at the top
    /// level; inside a scrolled container it is that container's content-local space, which is why the draw
    /// rect is recorded beside it instead of being derived from it.
    /// </summary>
    internal Rect Arranged { get; }

    /// <summary>The rect the widget's Draw was handed - the element's own drawing space.</summary>
    internal Rect Draw { get; }

    /// <summary>The draw rect converted into Host window space, which is where the pointer and hit stack live.</summary>
    internal Rect Window { get; }

    /// <summary>The draw-local to window-space offset this sample was converted with.</summary>
    internal Vector2 Origin { get; }

    /// <summary>Scroll content rect (scroll-local) for a scoped container, else null.</summary>
    internal Rect? Content { get; }

    /// <summary>Fixed, Auto or MatchContent, read from the declaration.</summary>
    internal string HeightMode { get; }

    /// <summary>The height the element resolved to, which is what the height mode decided.</summary>
    internal float ResolvedHeight { get; }
}

/// <summary>
/// One development-only input sample: an element's answer to a hit query, so "which element did this press
/// land on, and if none, why not" has an answer that is not a guess. The verdict vocabulary is the funnel's
/// own two refusals plus the native control's answer: <c>disabled</c>, <c>covered</c>, <c>hit</c>,
/// <c>miss</c>.
/// </summary>
internal readonly struct UiDevInputSample
{
    internal UiDevInputSample(int pass, string path, string kind, Rect rect, Vector2 point, string verdict)
    {
        Pass = pass;
        Path = path ?? "";
        Kind = kind ?? "";
        Rect = rect;
        Point = point;
        Verdict = verdict ?? "";
    }

    internal int Pass { get; }
    internal string Path { get; }
    internal string Kind { get; }

    /// <summary>The queried rect in Host window space.</summary>
    internal Rect Rect { get; }

    /// <summary>The pointer in Host window space - the same space as <see cref="Rect"/>.</summary>
    internal Vector2 Point { get; }

    /// <summary>What the funnel decided.</summary>
    internal string Verdict { get; }
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

    /// <summary>Whether the outline overlay is painted as well as recorded.</summary>
    internal bool Overlay { get; set; }

    internal int Pass => pass;

    internal int GeometryCount => geometry.Count;

    internal int InputCount => input.Count;

    internal long DroppedGeometry { get; private set; }

    internal long DroppedInput { get; private set; }

    /// <summary>Starts a new pass, dropping the previous one's samples. A repeat of the current pass no-ops.</summary>
    internal void BeginPass(int current)
    {
        if (current == pass) return;
        pass = current;
        geometry.Clear();
        input.Clear();
        DroppedGeometry = 0;
        DroppedInput = 0;
    }

    internal void Add(UiDevGeometrySample sample)
    {
        if (geometry.Count >= MaxGeometrySamples)
        {
            DroppedGeometry++;
            return;
        }

        geometry.Add(sample);
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

    /// <summary>Empties both halves without touching the pass number or the overlay switch.</summary>
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
    /// </summary>
    internal string Dump()
    {
        var text = new StringBuilder();
        text.Append("[ferritelib.geometry] pass=").Append(pass.ToString(CultureInfo.InvariantCulture))
            .Append(" nodes=").Append(geometry.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" nodes-dropped=").Append(DroppedGeometry.ToString(CultureInfo.InvariantCulture))
            .Append(" inputs=").Append(input.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" inputs-dropped=").Append(DroppedInput.ToString(CultureInfo.InvariantCulture))
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
                .Append(" h=").Append(Number(sample.ResolvedHeight));

            if (sample.Content.HasValue)
            {
                text.Append(" content=").Append(Rect(sample.Content.Value));
            }

            text.Append('\n');
        }

        for (int i = 0; i < input.Count; i++)
        {
            UiDevInputSample sample = input[i];
            text.Append("input path=").Append(sample.Path)
                .Append(" kind=").Append(sample.Kind)
                .Append(" point=").Append(Point(sample.Point))
                .Append(" rect=").Append(Rect(sample.Rect))
                .Append(" verdict=").Append(sample.Verdict)
                .Append('\n');
        }

        return text.ToString();
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
}

/// <summary>
/// The development-only numeric instrument: where every arranged element actually is, what height mode
/// resolved it, what a press landed on, and - on request - an outline of the sampled rects.
/// <para>
/// <b>Compiled out of a release build.</b> The whole file is inside <c>#if FER_DEV</c>, and every call site
/// is guarded the same way, so a release payload carries no capture, no buffer and no call: the enable path
/// on <see cref="UiDiagnosticSubscription.GeometryEnabled"/> refuses rather than degrading into a tool that
/// reports nothing and looks fine. The gate is the build configuration, and a lane holds both halves.
/// </para>
/// <para>
/// <b>One place, three chokepoints.</b> The instrument is this file; it is fed from the three points that
/// already own the facts - the engine's per-entry draw walk (rects, height mode, container viewport), the
/// funnel's click decision (the verdict), and the engine's entry again for the optional outline. Nothing is
/// recorded from a widget, so a kind cannot report a number the engine disagrees with.
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

    /// <summary>
    /// Records one arranged entry. Called from the engine's draw walk, where the arranged rect, the draw
    /// rect, the content rect and the resolved height mode are all already in hand.
    /// </summary>
    internal static void Note(
        UiNode node,
        UiElementSpec spec,
        string containerKind,
        bool isContainer,
        bool isScopedContainer,
        Rect arranged,
        Rect? contentRect,
        Rect drawRect,
        UiWidgetContext ctx)
    {
        UiDevGeometryCapture? capture = UiDiagnosticHub.ActiveSubscription?.Geometry;
        if (capture == null) return;

        capture.BeginPass(ctx.Session.Frame);
        string kind = isContainer && containerKind.Length > 0 ? containerKind : spec.Kind;
        capture.Add(new UiDevGeometrySample(
            ctx.Session.Frame,
            node.Path,
            kind,
            isContainer,
            containerKind,
            isScopedContainer,
            arranged,
            drawRect,
            ctx.ToWindowRect(drawRect),
            ctx.WindowOrigin,
            contentRect,
            HeightMode(spec),
            arranged.height));
    }

    /// <summary>
    /// Paints the outline over one entry's draw rect, after that entry has drawn. Drawn in draw-local space
    /// so it lines up with the element by construction, and through the theme's single solid outlet so the
    /// backend-containment allowlist does not grow a second one.
    /// </summary>
    internal static void Outline(Rect drawRect)
    {
        UiDevGeometryCapture? capture = UiDiagnosticHub.ActiveSubscription?.Geometry;
        if (capture == null || !capture.Overlay) return;
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
    /// taken only when the pointer is down or up, or when the native control actually fired.
    /// </summary>
    internal static void NoteInput(UiWidgetContext ctx, UiNode? element, Rect rect, string verdict)
    {
        UiDevGeometryCapture? capture = UiDiagnosticHub.ActiveSubscription?.Geometry;
        if (capture == null) return;

        bool pressed = string.Equals(verdict, "hit", StringComparison.Ordinal)
            || UiNative.IsPointerDown()
            || UiNative.IsPointerUp();
        if (!pressed) return;

        capture.BeginPass(ctx.Session.Frame);
        Vector2 pointer = UiNative.PointerPosition();
        capture.Add(new UiDevInputSample(
            ctx.Session.Frame,
            element is null ? ctx.ElementPath : element.Path,
            element is null ? "" : element.Kind,
            ctx.ToWindowRect(rect),
            ctx.ToWindowRect(new Rect(pointer.x, pointer.y, 0f, 0f)).position,
            verdict));
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
}
#endif
