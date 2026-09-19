using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// CP-1/CP-2 placement vocabulary lane (0.7.x Batch 1). The vocabulary is one rule over both axes:
/// <c>origin = parentInner.origin + fraction * (parentInner.span - selfSpan) + offset</c>, where
/// <c>AlignX</c>/<c>AlignY</c> names both the parent's reference point and the element's own pivot, and
/// <c>OffsetX</c>/<c>OffsetY</c> is a pixel nudge or a percentage of the parent's inner span.
/// <para>
/// What this lane pins, in the plan's own words: the placement matrix (origin x ratio x pivot, against an
/// <c>Overlay</c>, including ratio 0 and 100%); the refusal matrix (each refused combination throws at
/// creation with the element path); the flow boundary (an absent attribute arranges exactly as before, and
/// CP-2's cross-axis subset works); and the envelope guard (the aligned box is the arranged rect, never the
/// painted shape, and content that exceeds it moves no sibling).
/// </para>
/// <para>
/// Every arrangement case declares <c>Padding="0" Gap="0"</c> on its container on purpose: CP-0 makes an
/// absent attribute mean the theme's geometry (6), and these numbers are about placement, not spacing. The
/// one case that must not declare them is the stretch-default case, which pins the slot flow already
/// produced.
/// </para>
/// </summary>
internal static class KernelPlacementTests
{
    private const string Scope = "placement-test";
    private const string ProbeKind = "probe/glyph";

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Placement matrix: horizontal origin x ratio x pivot against an Overlay", VerifyHorizontalMatrix);
        Run("Placement matrix: vertical origin, ratio and pixel nudge", VerifyVerticalMatrix);
        Run("Stretch is the default and reproduces today's Overlay slot exactly", VerifyStretchDefault);
        Run("Refusal matrix: every refused combination throws at creation with the path", VerifyRefusalMatrix);
        Run("Refusal matrix: accepted forms are not over-refused", VerifyAcceptedForms);
        Run("Flow boundary: absent placement arranges exactly as before", VerifyFlowBoundaryUnchanged);
        Run("Flow boundary: CP-2's cross-axis subset works in a Column and a Row", VerifyFlowCrossAxis);
        Run("Template boundary: a template root owns no container, its children keep the vocabulary", VerifyTemplateBoundary);
        Run("Envelope guard: placement reads the arranged rect, never the painted shape", VerifyEnvelopeIsTheRect);
        Run("Envelope guard: an overflowing paint moves no sibling and grows no envelope", VerifyOverflowMovesNothing);
        return failures;
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    // --- the placement matrix -------------------------------------------------------------------

    /// <summary>
    /// The parent's inner box is 400 x 100 here (Overlay with Padding=0, Gap=0, Height=100), the element's
    /// own envelope is 120 x 20. Every expected number below is that frame's arithmetic plus the pivot:
    /// Left = 0, Center = (400-120)/2 = 140, Right = 400-120 = 280.
    /// </summary>
    private static void VerifyHorizontalMatrix()
    {
        RegisterProbe();

        CheckRect(Child("AlignX=\"Left\""), 0f, "AlignX=Left puts the element's own left edge on the parent's inner left");
        CheckRect(Child("AlignX=\"Center\""), 140f, "AlignX=Center puts the element's own centre on the parent's centre");
        CheckRect(Child("AlignX=\"Right\""), 280f, "AlignX=Right puts the element's own right edge on the parent's inner right");
        CheckRect(Child("AlignX=\"Center\" OffsetX=\"-20\""), 120f, "a bare OffsetX is a pixel nudge applied after the reference point");

        // The ratio unit is the parent's INNER span (400), never the element's own 120.
        CheckRect(Child("AlignX=\"Left\" OffsetX=\"25%\""), 100f, "OffsetX=25% is a quarter of the parent's inner width (400), not of the element (120)");
        CheckRect(Child("AlignX=\"Left\" OffsetX=\"0%\""), 0f, "ratio 0 is the inner origin");
        CheckRect(Child("AlignX=\"Left\" OffsetX=\"100%\""), 400f, "ratio 100% is the far edge of the inner span");
        CheckRect(Child("AlignX=\"Right\" OffsetX=\"10%\""), 320f, "a ratio rides on top of the pivot: Right (280) + 10% of 400 (40)");

        // An Auto width is the element's own envelope too, so the pivot tracks the measured label.
        UiLayoutSnapshot auto = Snapshot(Overlay(
            "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"Auto\" Label=\"ab\" AlignX=\"Center\" />"));
        Check(Near(auto.RectById["a"].x, 192f) && Near(auto.RectById["a"].width, 16f),
            "an Auto envelope centres like any other: a 16px label sits at (400-16)/2 = 192");

        // Both axes at once, which is what a placement container is for.
        UiLayoutSnapshot corner = Snapshot(Overlay(
            "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" AlignX=\"Right\" AlignY=\"Bottom\" />"));
        Check(Near(corner.RectById["a"].x, 280f) && Near(corner.RectById["a"].y, 80f),
            "both axes resolve in one pass: Right/Bottom corners the element in the parent's inner box");
    }

    private static void VerifyVerticalMatrix()
    {
        RegisterProbe();

        CheckY(Child("AlignY=\"Top\""), 0f, "AlignY=Top is the parent's inner top");
        CheckY(Child("AlignY=\"Middle\""), 40f, "AlignY=Middle centres the element's own box: (100-20)/2");
        CheckY(Child("AlignY=\"Bottom\""), 80f, "AlignY=Bottom puts the element's own bottom on the parent's inner bottom");
        CheckY(Child("AlignY=\"Bottom\" OffsetY=\"10%\""), 90f, "OffsetY=10% is a tenth of the parent's inner height (100)");
        CheckY(Child("AlignY=\"Top\" OffsetY=\"12\""), 12f, "a bare OffsetY is a pixel nudge");
        CheckY(Child(""), 0f, "an absent AlignY is Stretch, which is where flow put the element");
    }

    private static void VerifyStretchDefault()
    {
        RegisterProbe();

        // R2 in the plan's words: absent vocabulary means today's behaviour. An Overlay child has always
        // been measured at the parent's whole inner width, and a numeric Width next to no AlignX keeps that
        // slot - which is why CP-1 asks for no migration on an existing page.
        UiLayoutSnapshot bare = Snapshot(Overlay("<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" />"));
        Check(Near(bare.RectById["a"].x, 0f) && Near(bare.RectById["a"].width, 400f),
            "an Overlay child with no placement vocabulary keeps today's full-inner-width slot");

        UiLayoutSnapshot declared = Snapshot(Overlay(
            "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" />"));
        Check(Near(declared.RectById["a"].x, bare.RectById["a"].x)
            && Near(declared.RectById["a"].width, bare.RectById["a"].width),
            "and a numeric Width beside no AlignX still arranges exactly as before (no migration)");

        UiLayoutSnapshot stretch = Snapshot(Overlay(
            "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Stretch\" />"));
        Check(Near(stretch.RectById["a"].x, 0f) && Near(stretch.RectById["a"].width, 400f),
            "an explicit Stretch is the same slot as the default");
    }

    // --- the refusal matrix ---------------------------------------------------------------------

    private static void VerifyRefusalMatrix()
    {
        RegisterProbe();

        Reject(Page(OverlayBody("<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" AlignX=\"Diagonal\" />")),
            "an unknown alignment value");
        Reject(Page(OverlayBody("<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" AlignY=\"Left\" />")),
            "an edge name from the other axis");
        Reject(Page(OverlayBody("<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Left\" OffsetX=\"120%\" />")),
            "a ratio above 100%");
        Reject(Page(OverlayBody("<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Left\" OffsetX=\"-5%\" />")),
            "a ratio below 0%");
        Reject(Page(OverlayBody("<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Left\" OffsetX=\"wide\" />")),
            "a malformed offset");
        Reject(Page(OverlayBody("<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Left\" OffsetX=\"NaN\" />")),
            "a non-finite offset");
        Reject(Page(OverlayBody("<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Stretch\" Width=\"120\" />")),
            "an explicit Stretch together with a numeric Width");
        Reject(Page(OverlayBody("<Column Id=\"c\" Fill=\"true\" AlignX=\"Center\" />")),
            "placement together with Fill on the same element");

        // R1: the vocabulary is valid only inside a placement container, and a flow container's child
        // accepts the cross-axis subset only.
        Reject(Page("<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Center\" />"),
            "placement on a root element, which no container owns");
        Reject(Page("<Row Id=\"r\" Padding=\"0\"><Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Right\" /></Row>"),
            "AlignX on a Row child, where x belongs to the flow");
        Reject(Page("<Column Id=\"c\" Padding=\"0\"><Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignY=\"Middle\" /></Column>"),
            "AlignY on a Column child, where y belongs to the flow");
        Reject(Page("<Column Id=\"c\" Padding=\"0\"><Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Right\" OffsetY=\"4\" /></Column>"),
            "an offset on the main axis of a flow container");

        // CP-2's boundary: a percentage offset is a claim on the parent's span.
        Reject(Page("<Column Id=\"c\" Padding=\"0\"><Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Center\" OffsetX=\"50%\" /></Column>"),
            "percentage placement on a child of a flow container");
        Reject(Page("<Row Id=\"r\" Padding=\"0\"><Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignY=\"Middle\" OffsetY=\"50%\" /></Row>"),
            "percentage placement on the cross axis of a flow container");

        // The refusal is located: the attribute and the element path are both in the message.
        try
        {
            Host(Page("<Column Id=\"c\" Padding=\"0\"><Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Right\" OffsetX=\"wide\" /></Column>"));
            Check(false, "a nested malformed offset - but the host accepted it");
        }
        catch (UiContractException ex)
        {
            Check(ex.Message.Contains("OffsetX") && ex.Message.Contains("'c/a'"),
                "the refusal names the attribute, the value and the element path (got: " + ex.Message + ")");
        }
    }

    private static void VerifyAcceptedForms()
    {
        RegisterProbe();

        Host(Page(OverlayBody("<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" AlignX=\"Center\" OffsetX=\"50%\" AlignY=\"Bottom\" OffsetY=\"12\" />")));
        Check(true, "a placement container accepts both axes with a ratio and a pixel nudge");

        Host(Page("<Row Id=\"r\" Padding=\"0\"><Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignY=\"Middle\" /></Row>"));
        Host(Page("<Column Id=\"c\" Padding=\"0\"><Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"100\" AlignX=\"Right\" /></Column>"));
        Host(Page("<Column Id=\"c\" Padding=\"0\"><Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" AlignX=\"Center\" OffsetX=\"12\" /></Column>"));
        Check(true, "a flow container's child accepts the cross-axis align and a pixel nudge");

        Host(Page("<Overlay Id=\"box\" Padding=\"0\"><Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" /></Overlay>"));
        Check(true, "a numeric Width with no AlignX stays accepted, so an existing page does not migrate");
    }

    // --- the flow boundary ----------------------------------------------------------------------

    private static void VerifyFlowBoundaryUnchanged()
    {
        RegisterProbe();

        UiLayoutSnapshot column = Snapshot(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Column Id=\"c\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"10\" />"
            + "<Widget Id=\"b\" Kind=\"" + ProbeKind + "\" Height=\"10\" />"
            + "</Column></UiPage>", 400f, 200f);
        Check(Near(column.RectById["a"].x, 0f) && Near(column.RectById["a"].y, 0f)
            && Near(column.RectById["b"].y, 10f) && Near(column.RectById["a"].width, 400f),
            "a Column child with no placement vocabulary arranges exactly as before");

        UiLayoutSnapshot sized = Snapshot(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Column Id=\"c\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"10\" Width=\"100\" />"
            + "</Column></UiPage>", 400f, 200f);
        Check(Near(sized.RectById["a"].width, 400f) && Near(sized.RectById["a"].x, 0f),
            "a numeric Width beside no AlignX keeps today's flow slot (the width is still the flow's to decide)");

        UiLayoutSnapshot row = Snapshot(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Row Id=\"r\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"10\" Width=\"100\" />"
            + "<Widget Id=\"b\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"100\" />"
            + "</Row></UiPage>", 400f, 200f);
        Check(Near(row.RectById["a"].x, 0f) && Near(row.RectById["a"].y, 0f)
            && Near(row.RectById["b"].x, 100f) && Near(row.RectById["b"].y, 0f),
            "a Row child with no placement vocabulary arranges exactly as before");
    }

    private static void VerifyFlowCrossAxis()
    {
        RegisterProbe();

        UiLayoutSnapshot column = Snapshot(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Column Id=\"c\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"right\" Kind=\"" + ProbeKind + "\" Height=\"10\" Width=\"100\" AlignX=\"Right\" />"
            + "<Widget Id=\"centre\" Kind=\"" + ProbeKind + "\" Height=\"10\" Width=\"100\" AlignX=\"Center\" />"
            + "<Widget Id=\"nudged\" Kind=\"" + ProbeKind + "\" Height=\"10\" Width=\"100\" AlignX=\"Right\" OffsetX=\"-10\" />"
            + "</Column></UiPage>", 400f, 200f);
        Check(Near(column.RectById["right"].x, 300f), "a Column child aligns right against the parent's inner width (400-100)");
        Check(Near(column.RectById["centre"].x, 150f), "and centres: (400-100)/2");
        Check(Near(column.RectById["nudged"].x, 290f), "a pixel nudge rides on the cross-axis reference point");

        UiLayoutSnapshot row = Snapshot(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Row Id=\"r\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"tall\" Kind=\"" + ProbeKind + "\" Height=\"50\" Width=\"100\" />"
            + "<Widget Id=\"bottom\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"100\" AlignY=\"Bottom\" />"
            + "<Widget Id=\"middle\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"100\" AlignY=\"Middle\" />"
            + "<Widget Id=\"nudged\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"100\" AlignY=\"Bottom\" OffsetY=\"5\" />"
            + "</Row></UiPage>", 400f, 200f);
        Check(Near(row.RectById["tall"].y, 0f), "the tallest Row child defines the line, and keeps the line top");
        Check(Near(row.RectById["bottom"].y, 30f), "AlignY=Bottom sits on the Row's own inner bottom (50-20)");
        Check(Near(row.RectById["middle"].y, 15f), "AlignY=Middle centres on the Row's inner height: (50-20)/2");
        Check(Near(row.RectById["nudged"].y, 35f), "a pixel nudge rides on the cross-axis reference point");

        // A Wrap line is a horizontal line of flow, and a Scroll flows vertically: both have a cross axis,
        // and both must read the same rule.
        UiLayoutSnapshot wrap = Snapshot(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Wrap Id=\"w\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"tall\" Kind=\"" + ProbeKind + "\" Height=\"40\" Width=\"200\" />"
            + "<Widget Id=\"short\" Kind=\"" + ProbeKind + "\" Height=\"10\" Width=\"100\" AlignY=\"Bottom\" />"
            + "</Wrap></UiPage>", 400f, 200f);
        Check(Near(wrap.RectById["tall"].y, 0f) && Near(wrap.RectById["short"].y, 30f),
            "a Wrap line is horizontal, so its cross axis is vertical: the short child sits on the line's bottom");

        UiLayoutSnapshot scroll = Snapshot(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Scroll Id=\"s\" Padding=\"0\" Gap=\"0\" Height=\"100\">"
            + "<Widget Id=\"right\" Kind=\"" + ProbeKind + "\" Height=\"10\" Width=\"100\" AlignX=\"Right\" />"
            + "</Scroll></UiPage>", 400f, 100f);
        Check(Near(scroll.RectById["right"].x, 300f),
            "a Scroll flows vertically, so its cross axis is horizontal too");
    }

    // --- the template boundary ----------------------------------------------------------------

    /// <summary>
    /// A template subtree is outside the Host's creation walk, so the engine runs the same refusal matrix
    /// over it. A template ROOT is not a child of a container - the collection element owns the generated
    /// row's slot - so placement is refused there rather than accepted and then ignored by the row
    /// arrangement, while an element INSIDE the template is a real child of its own container and keeps the
    /// cross-axis subset.
    /// </summary>
    private static void VerifyTemplateBoundary()
    {
        RegisterProbe();
        var items = new List<string> { "a" };

        try
        {
            HostWithItems(
                "<Templates><Row Id=\"row\" Padding=\"0\" Gap=\"0\" AlignX=\"Center\">"
                + "<Widget Id=\"glyph\" Kind=\"" + ProbeKind + "\" Height=\"10\" /></Row></Templates>"
                + "<Repeat Id=\"rows\" Items=\"items\" Template=\"row\" />",
                items);
            Check(false, "a template root carrying AlignX - but the host accepted it");
        }
        catch (UiContractException ex)
        {
            Check(ex.Message.Contains("AlignX") && ex.Message.Contains("<Templates>/row"),
                "a template root owns no container, so its placement vocabulary is refused with the template path: " + ex.Message);
        }

        HostWithItems(
            "<Templates><Column Id=\"stack\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"glyph\" Kind=\"" + ProbeKind + "\" Height=\"10\" Width=\"100\" AlignX=\"Center\" /></Column></Templates>"
            + "<Repeat Id=\"rows\" Items=\"items\" Template=\"stack\" />",
            items);
        Check(true, "an element inside a template is a child of its own container and keeps the cross-axis subset");
    }

    private static void HostWithItems(string body, IReadOnlyList<string> items)
    {
        var bindings = new UiBindings();
        bindings.BindReadOnly<IReadOnlyList<string>>("items", () => items, UiInvalidation.Structure);
        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse(Page(body)),
            bindings,
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());
    }

    // --- the envelope guard ---------------------------------------------------------------------

    private static void VerifyEnvelopeIsTheRect()
    {
        // The probe paints a 3-px rail inside its rect. Placement must read the ARRANGED rect (120), never
        // the painted shape (3), or the envelope would follow the paint - which is exactly what E3 forbids.
        var painted = new List<Rect>();
        RegisterProbe(painted);

        UiLayoutEngine engine = new(Scope);
        UiWidgetContext ctx = CreateContext();
        UiLayoutSnapshot snapshot = engine.ArrangeRoots(
            ctx, new Vector2(400f, 100f), Parse(Overlay(
                "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" AlignX=\"Center\" />")).Roots);

        Check(Near(snapshot.RectById["a"].x, 140f) && Near(snapshot.RectById["a"].width, 120f),
            "the placed envelope is the arranged rect (140, width 120), not the 3-px painted rail");

        engine.Draw(ctx, snapshot, new Rect(0f, 0f, 400f, 100f));
        Check(painted.Count == 1 && Near(painted[0].x, 140f) && Near(painted[0].width, 120f),
            "and the draw half hands the widget that same rect, so measure and draw agree");
        Check(Near(painted[0].width, 120f) && !Near(painted[0].width, 3f),
            "the rail is painted inside the envelope; it never becomes the envelope");
    }

    private static void VerifyOverflowMovesNothing()
    {
        RegisterProbe();
        var painted = new List<Rect>();
        RegisterOverflowProbe(painted);

        UiLayoutEngine engine = new(Scope);
        UiWidgetContext ctx = CreateContext();
        UiLayoutSnapshot before = engine.ArrangeRoots(
            ctx, new Vector2(400f, 100f), Parse(Overlay(
                "<Widget Id=\"left\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" AlignX=\"Left\" />"
                + "<Widget Id=\"spill\" Kind=\"probe/spill\" Height=\"20\" Width=\"120\" AlignX=\"Left\" OffsetX=\"40\" />"
                + "<Widget Id=\"right\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" AlignX=\"Right\" />")).Roots);

        Rect left = before.RectById["left"];
        Rect right = before.RectById["right"];
        Rect spill = before.RectById["spill"];
        Check(Near(spill.x, 40f) && Near(spill.width, 120f), "the spilling element is placed by its rect, not by what it paints");

        engine.Draw(ctx, before, new Rect(0f, 0f, 400f, 100f));
        Check(painted.Count == 2 && painted[0].width < 400f && painted[1].width > 400f,
            "the probe really painted past the parent's inner width while its envelope stayed 120");

        UiLayoutSnapshot after = engine.ArrangeRoots(
            ctx, new Vector2(400f, 100f), Parse(Overlay(
                "<Widget Id=\"left\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" AlignX=\"Left\" />"
                + "<Widget Id=\"spill\" Kind=\"probe/spill\" Height=\"20\" Width=\"120\" AlignX=\"Left\" OffsetX=\"40\" />"
                + "<Widget Id=\"right\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" AlignX=\"Right\" />")).Roots);
        Check(Near(after.RectById["left"].x, left.x) && Near(after.RectById["left"].width, left.width)
            && Near(after.RectById["right"].x, right.x) && Near(after.RectById["right"].width, right.width)
            && Near(after.RectById["spill"].width, 120f),
            "an overflowing paint moves no sibling and does not grow its own envelope");
    }

    // --- harness --------------------------------------------------------------------------------

    private static void CheckRect(string childAttributes, float expectedX, string what)
    {
        UiLayoutSnapshot snapshot = Snapshot(Overlay(
            "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" " + childAttributes + " />"));
        Rect rect = snapshot.RectById["a"];
        Check(Near(rect.x, expectedX) && Near(rect.width, 120f),
            what + " (x=" + rect.x + ", width=" + rect.width + ")");
    }

    private static void CheckY(string childAttributes, float expectedY, string what)
    {
        UiLayoutSnapshot snapshot = Snapshot(Overlay(
            "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"20\" Width=\"120\" " + childAttributes + " />"));
        Check(Near(snapshot.RectById["a"].y, expectedY),
            what + " (y=" + snapshot.RectById["a"].y + ")");
    }

    private static string Child(string childAttributes)
    {
        return childAttributes;
    }

    /// <summary>An Overlay pinned to a 400 x 100 inner box: Padding=0/Gap=0 keep CP-0's density out of the arithmetic.</summary>
    private static string Overlay(string child)
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Overlay Id=\"box\" Padding=\"0\" Gap=\"0\" Height=\"100\">"
            + child + "</Overlay></UiPage>";
    }

    private static string OverlayBody(string child)
    {
        return "<Overlay Id=\"box\" Padding=\"0\" Gap=\"0\" Height=\"100\">" + child + "</Overlay>";
    }

    private static string Page(string body)
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>";
    }

    private static void Host(string xml)
    {
        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse(xml),
            new UiBindings(),
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());
    }

    private static void Reject(string xml, string what)
    {
        try
        {
            Host(xml);
            Check(false, what + " - but the host accepted it");
        }
        catch (UiContractException)
        {
            Check(true, what + " - rejected at creation");
        }
    }

    private static UiLayoutSnapshot Snapshot(string xml)
    {
        return Snapshot(xml, 400f, 100f);
    }

    private static UiLayoutSnapshot Snapshot(string xml, float width, float height)
    {
        UiLayoutEngine engine = new(Scope);
        UiWidgetContext ctx = CreateContext();
        return engine.ArrangeRoots(ctx, new Vector2(width, height), Parse(xml).Roots);
    }

    private static UiLayoutManifest Parse(string xml)
    {
        return UiLayoutManifest.Parse(xml);
    }

    private static UiWidgetContext CreateContext()
    {
        return new UiWidgetContext(
            Scope,
            new UiSession(),
            new StubMetrics(),
            UiTheme.DarkGold,
            new StubTranslation(),
            new UiBindings(),
            400f,
            "root");
    }

    private static void RegisterProbe()
    {
        RegisterProbe(null);
    }

    private static void RegisterProbe(List<Rect>? painted)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(
            Scope,
            ProbeKind,
            () => new ProbeWidget(ProbeKind, painted, 0f),
            new[] { "Height", "Width", "Label" },
            new[] { "Label" });
    }

    private static void RegisterOverflowProbe(List<Rect> painted)
    {
        UiWidgetRegistry.Register(
            Scope,
            "probe/spill",
            () => new ProbeWidget("probe/spill", painted, 400f),
            new[] { "Height", "Width" });
    }

    private static bool Near(float a, float b)
    {
        return Math.Abs(a - b) < 0.01f;
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
        }
    }

    private sealed class ProbeWidget : IUiWidget
    {
        private readonly string kind;
        private readonly List<Rect>? painted;
        private readonly float spill;

        public ProbeWidget(string kind, List<Rect>? painted, float spill)
        {
            this.kind = kind;
            this.painted = painted;
            this.spill = spill;
        }

        public string Kind => kind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            return 20f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            if (painted == null) return;

            // The envelope is the rect it was handed. A 3-px rail is painted inside it, and the spill
            // probe paints past it - the paint is what E3 must never let become the envelope.
            painted.Add(rect);
            if (spill > 0f) painted.Add(new Rect(rect.x, rect.y, rect.width + spill, rect.height));
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return font switch
            {
                UiFont.Tiny => 16f,
                UiFont.Small => 24f,
                _ => 32f
            };
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key)
        {
            return "[" + key + "]";
        }

        public int TranslationRevision => 0;
    }
}
