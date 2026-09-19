using System;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Independent Batch 1 verification lane (task-3), owned by the verifier and not by either implementer.
/// <para>
/// Its fixtures, its arithmetic and its attack angles are its own: the placement matrix runs on a
/// 500 x 160 Overlay with a 10/6 pad, the density half runs on a per-element <c>Density</c> scope resolved
/// out of a style document rather than on an injected theme, the deprecation half counts notes across
/// four declarations at once rather than replaying one widget, and the envelope half uses the built-in
/// <c>chrome/rule</c> so the element under test is not a fixture this lane registered itself.
/// </para>
/// <para>
/// The claim it exists to falsify is the one the contract states in
/// <c>docs/development/0.7/05-api-contract.md</c> under "Batch 1", including the two stated boundaries
/// (declared-vs-effective kind, template root) and the L1 orphan-name question the architecture doc leaves
/// unowned. A probe here that cannot decide a claim says so instead of passing it.
/// </para>
/// </summary>
internal static class KernelBatch1VerificationTests
{
    private const string Scope = "batch1-verify";

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        ResetHarness();

        Run("CP-6: the four authored tone meanings are accepted and record nothing", VerifyFourAuthoredMeanings);
        Run("CP-6: Active/Disabled keep their treatment and record one note per declaration, not per frame", VerifyDeprecatedRedirects);
        Run("CP-6: a fifth tone value still falls back and is recorded", VerifyFifthToneFallsBack);
        Run("CP-6: the Disabled redirect is the data-derived disabled state", VerifyDisabledRedirectIsTheDataState);
        Run("CP-6: HoverPoint is gone from the C# surface and the document token list", VerifyHoverPointGone);
        Run("CP-6: AccentHover is genuinely derived, read-only and follows AccentGold", VerifyAccentHoverDerived);
        Run("CP-0: an undeclared container takes the theme's geometry", VerifyDensityReach);
        Run("CP-0: Padding=0/Gap=0 reproduces the pre-Batch-1 zero result exactly", VerifyEscapeHatch);
        Run("CP-0: measure and draw agree on density-scoped titled containers", VerifyMeasureDrawAgree);
        Run("CP-1: the placement matrix at 0%, 100% and pixel nudges", VerifyPlacementMatrix);
        Run("CP-1: absent and explicit Stretch arrange identically to the floor", VerifyStretchEquivalence);
        Run("CP-1/CP-2: every refused combination throws at creation with a located error", VerifyRefusals);
        Run("CP-1: the envelope is the arranged rect, not the painted shape", VerifyEnvelopeByRect);
        Run("CP-2: every accepted cross-axis name is consumed (L1 orphan-name check)", VerifyAcceptedNamesAreConsumed);
        Run("CP-2 boundary: a Row with Narrow=Column keeps a placement its narrow state does not own", VerifyNarrowSwapBoundary);
        Run("CP-1 boundary: a template root refuses all four names, an element inside keeps the subset", VerifyTemplateBoundary);
        Run("Cross-cutting: the placement types the new file added are not public", VerifyPlacementTypesAreInternal);
        Run("Cross-cutting: an overflowing element reaches the fit audit on the overflow axis", VerifyOverflowReachesTheFitAudit);

        ResetHarness();
        return failures;
    }

    // --- CP-6: the authored tone vocabulary ------------------------------------------------

    /// <summary>The four declared meanings are rules: they resolve and record nothing.</summary>
    private static void VerifyFourAuthoredMeanings()
    {
        var records = new List<UiStyleFallbackReport>();
        try
        {
            UiFitAudit.AttachStyleFallback(records.Add);
            UiFitAudit.Reset();

            string xml = Page("<Stack Id=\"s\" Padding=\"0\" Gap=\"0\">"
                + Rule("n", "Neutral") + Rule("su", "Success") + Rule("wa", "Warning") + Rule("da", "Danger")
                + "</Stack>");
            using UiHost host = NewHost(xml);
            host.DrawFrame(new Rect(0f, 0f, 300f, 200f));

            Check(records.Count == 0 && UiFitAudit.StyleFallbackCount == 0,
                "Neutral/Success/Warning/Danger are declared values, not fallbacks (records " + records.Count + ")");
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }
    }

    /// <summary>
    /// A page with TWO declarations of <c>Active</c>, one of <c>Disabled</c> and one unknown value. The
    /// contract's dedup key is (element path | kind | attribute | authored), so the page must record four
    /// notes on the first frame and still four on the second - one per declaration, not one per frame and
    /// not one per authored name.
    /// </summary>
    private static void VerifyDeprecatedRedirects()
    {
        var records = new List<UiStyleFallbackReport>();
        try
        {
            UiFitAudit.AttachStyleFallback(records.Add);
            UiFitAudit.Reset();

            string xml = Page("<Stack Id=\"s\" Padding=\"0\" Gap=\"0\">"
                + Rule("a1", "Active") + Rule("a2", "Active") + Rule("d1", "Disabled") + Rule("u1", "chartreuse")
                + "</Stack>");
            using UiHost host = NewHost(xml);
            host.DrawFrame(new Rect(0f, 0f, 300f, 200f));

            Check(records.Count == 4, "four declarations record four notes after one frame (got " + records.Count + ")");

            int active = 0;
            int disabled = 0;
            int fallback = 0;
            var activePaths = new List<string>();
            foreach (UiStyleFallbackReport report in records)
            {
                if (report.Authored == "Active")
                {
                    active++;
                    activePaths.Add(report.ElementPath);
                    Check(report.Attribute == "Tone"
                        && report.Resolved.IndexOf("Active", StringComparison.Ordinal) >= 0
                        && report.Diagnostic.IndexOf("deprecat", StringComparison.OrdinalIgnoreCase) >= 0,
                        "Active records a Tone deprecation naming the selected state (got '" + report.Resolved + "')");
                }
                else if (report.Authored == "Disabled")
                {
                    disabled++;
                    Check(report.Attribute == "Tone"
                        && report.Resolved.IndexOf("Disabled", StringComparison.Ordinal) >= 0
                        && report.Diagnostic.IndexOf("deprecat", StringComparison.OrdinalIgnoreCase) >= 0,
                        "Disabled records a Tone deprecation naming the derived state (got '" + report.Resolved + "')");
                }
                else if (report.Authored == "chartreuse")
                {
                    fallback++;
                    Check(report.Resolved == "Neutral", "a fifth value still falls back to Neutral");
                }
            }

            Check(active == 2 && disabled == 1 && fallback == 1,
                "the notes partition per declaration (Active " + active + ", Disabled " + disabled + ", other " + fallback + ")");
            Check(activePaths.Count == 2 && activePaths[0] != activePaths[1],
                "two declarations of the same authored name record two notes, one per element path");

            host.DrawFrame(new Rect(0f, 0f, 300f, 200f));
            Check(records.Count == 4 && UiFitAudit.StyleFallbackCount == 4,
                "a second frame adds nothing: dedup is per declaration, not per frame (got " + records.Count + ")");
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }
    }

    /// <summary>
    /// A fifth value is recorded, and the painted colour proves it resolved to the neutral treatment
    /// rather than silently applying something else.
    /// </summary>
    private static void VerifyFifthToneFallsBack()
    {
        var records = new List<UiStyleFallbackReport>();
        try
        {
            UiFitAudit.AttachStyleFallback(records.Add);
            UiFitAudit.Reset();

            string xml = Page("<Stack Id=\"s\" Padding=\"0\" Gap=\"0\">"
                + Rule("n", "Neutral") + Rule("t", "Teal") + "</Stack>");
            using UiHost host = NewHost(xml);
            List<Color> colors = StaticList<Color>("DrawBoxSolidColors");
            int before = colors.Count;
            host.DrawFrame(new Rect(0f, 0f, 300f, 200f));

            Check(records.Count == 1 && records[0].Attribute == "Tone"
                && records[0].Authored == "Teal" && records[0].Resolved == "Neutral",
                "a fifth value is recorded once as a Tone fallback to Neutral (got " + records.Count + ")");
            Check(colors.Count == before + 2, "both rules painted one solid each");
            if (colors.Count == before + 2)
            {
                Check(SameColor(colors[before], colors[before + 1]),
                    "and the fifth value paints what an explicit Neutral paints");
            }
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }
    }

    /// <summary>
    /// The contract calls <c>Disabled</c> a data-derived state. This probe makes that operational: an
    /// authored <c>Tone="Disabled"</c> and a leaf whose read-only binding derives the same state must be
    /// indistinguishable, and both must differ from the neutral treatment an unknown value falls back to.
    /// </summary>
    private static void VerifyDisabledRedirectIsTheDataState()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        using UiSession session = new();
        UiTheme theme = UiTheme.DarkGold;
        var bindings = new UiBindings();
        bindings.BindReadOnly<string>("ro", () => "x");
        UiWidgetContext ctx = new(Scope, session, Metrics(), theme, Translation(), bindings, 200f, "root");

        Color authored = DrawLeaf(ctx, ("Text", "x"), ("Tone", "Disabled"));
        Color fromBinding = DrawLeaf(ctx, ("Bind", "ro"), ("Tone", "Neutral"));
        Color neutral = DrawLeaf(ctx, ("Text", "x"), ("Tone", "Neutral"));

        Check(SameColor(authored, fromBinding),
            "Tone=\"Disabled\" paints exactly what a read-only binding derives");
        Check(!SameColor(authored, neutral),
            "and it is not the neutral treatment an unknown value falls back to");
    }

    /// <summary>
    /// <c>HoverPoint</c> must be gone from the compiled surface and from the document token vocabulary,
    /// and a scheme still declaring it must be reported rather than silently applied.
    /// </summary>
    private static void VerifyHoverPointGone()
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        Check(typeof(UiTheme).GetMember("HoverPoint", All).Length == 0,
            "UiTheme.HoverPoint has no member left: no property, field or alias");

        int survivors = 0;
        foreach (Type type in typeof(UiTheme).Assembly.GetTypes())
        {
            survivors += type.GetMember("HoverPoint", All).Length;
        }

        Check(survivors == 0, "no other type in the payload carries the name either (found " + survivors + ")");

        UiStyleDocument ghost = UiStyleDocument.Parse(
            "<Styles Schema=\"1\"><Scheme Name=\"ghost\">"
            + "<Color Token=\"HoverPoint\" Value=\"#FF0000FF\" />"
            + "<Color Token=\"AccentGold\" Value=\"#00FF00FF\" />"
            + "</Scheme></Styles>");
        Check(ghost.Issues.Count == 1,
            "a scheme declaring HoverPoint records exactly one issue (got " + ghost.Issues.Count + ")");
        Check(ghost.Issues.Count == 1 && ghost.Issues[0].Message.IndexOf("HoverPoint", StringComparison.Ordinal) >= 0,
            "and the issue names the dropped token");

        bool hasScheme = ghost.TryGetScheme("ghost", out UiStyleDocument.SchemeDefinition scheme);
        Check(hasScheme && !scheme.Colours.ContainsKey("HoverPoint") && scheme.Colours.ContainsKey("AccentGold"),
            "the dropped declaration never reaches the scheme while the rest still does");

        // Independent: it is treated like any other unknown token, not specially handled.
        UiStyleDocument twoUnknown = UiStyleDocument.Parse(
            "<Styles Schema=\"1\"><Scheme Name=\"s\">"
            + "<Color Token=\"HoverPoint\" Value=\"#FF0000FF\" />"
            + "<Color Token=\"NotAToken\" Value=\"#FF0000FF\" />"
            + "</Scheme></Styles>");
        Check(twoUnknown.Issues.Count == 2,
            "HoverPoint is refused exactly like an arbitrary unknown token (got " + twoUnknown.Issues.Count + " issues)");

        // Independent: the scheme still applies everything else, and the derived step follows the applied
        // accent - so a red HoverPoint declaration cannot leak into the theme.
        var resolver = new UiStyleResolver(UiTheme.DarkGold, ghost);
        UiTheme scoped = resolver.ThemeFor(new[] { new UiStyleDeclaration(scheme: "ghost") });
        Check(SameColor(scoped.AccentGold, new Color(0f, 1f, 0f, 1f)),
            "the rest of the scheme still applies (AccentGold is the scheme's green)");
        Check(SameColor(scoped.AccentHover, Lift(new Color(0f, 1f, 0f, 1f))),
            "and the derived hover step follows the applied accent");
        Check(!SameColor(scoped.AccentHover, new Color(1f, 0f, 0f, 1f)),
            "the dropped HoverPoint value never reaches the theme (the hover step is not red)");
    }

    /// <summary>
    /// The derivation is a value, not a promise: it must move when the accent moves, be read-only, carry
    /// the alpha through, and travel a fixed 30% of the remaining distance to white. The channels are
    /// asserted with this lane's own inputs.
    /// </summary>
    private static void VerifyAccentHoverDerived()
    {
        PropertyInfo? derived = typeof(UiTheme).GetProperty("AccentHover", BindingFlags.Public | BindingFlags.Instance);
        Check(derived != null, "UiTheme.AccentHover exists");
        Check(derived != null && derived.CanRead && !derived.CanWrite,
            "and it is read-only: a caller cannot assign the derived step");

        UiTheme probe = UiTheme.Vanilla;
        probe.AccentGold = new Color(0.10f, 0.50f, 0.90f, 0.25f);
        Color first = probe.AccentHover;
        CheckNear(0.37f, first.r, "the red channel travels 30% of its remaining distance to white");
        CheckNear(0.65f, first.g, "the green channel does too");
        CheckNear(0.93f, first.b, "so does the blue channel");
        CheckNear(0.25f, first.a, "and alpha is carried unchanged");

        probe.AccentGold = new Color(0.90f, 0.10f, 0.30f, 1f);
        Color second = probe.AccentHover;
        CheckNear(0.93f, second.r, "re-tinting the accent recomputes red");
        CheckNear(0.37f, second.g, "recomputes green");
        CheckNear(0.51f, second.b, "recomputes blue");
        Check(!SameColor(first, second), "reading AccentHover after moving AccentGold returns a moved value");
        Check(SameColor(probe.AccentHover, second), "and reading it twice in a row is deterministic");

        probe.AccentGold = new Color(0.20f, 0.40f, 0.60f, 0f);
        CheckNear(0f, probe.AccentHover.a, "a zero-alpha accent gets a zero-alpha hover step, never an opaque one");

        probe.AccentGold = new Color(1f, 1f, 1f, 1f);
        Check(SameColor(probe.AccentHover, new Color(1f, 1f, 1f, 1f)), "white is a fixed point of the derivation");
    }

    // --- CP-0: density reaches container spacing -------------------------------------------

    /// <summary>
    /// A container declaring neither <c>Padding</c> nor <c>Gap</c> takes the theme's geometry, and a
    /// theme-only geometry change moves the arranged rects of a page whose manifest did not change.
    /// </summary>
    private static void VerifyDensityReach()
    {
        string xml = Page("<Stack Id=\"s\">"
            + W("a", RuleWidget.Kind, "Height=\"4\"") + W("b", RuleWidget.Kind, "Height=\"4\"")
            + "</Stack>");

        UiTheme six = UiTheme.Vanilla;
        six.Geometry = new UiGeometry(6f, 4f, 6f, 28f, 1f);
        UiTheme wide = UiTheme.Vanilla;
        wide.Geometry = new UiGeometry(13f, 4f, 7f, 28f, 1f);

        UiLayoutSnapshot atSix = Arrange(xml, six, 300f, 400f);
        UiLayoutSnapshot atWide = Arrange(xml, wide, 300f, 400f);

        Rect aSix = atSix.RectById["a"];
        Rect bSix = atSix.RectById["b"];
        Rect aWide = atWide.RectById["a"];
        Rect bWide = atWide.RectById["b"];

        CheckNear(6f, aSix.x, "an undeclared container's first child starts at the theme's Padding (left)");
        CheckNear(6f, aSix.y, "and at the theme's Padding (top)");
        CheckNear(16f, bSix.y, "two children are separated by the theme's Gap (6 + 4 + 6)");

        CheckNear(13f, aWide.x, "a theme-only geometry change moves the child's x");
        CheckNear(13f, aWide.y, "and its y");
        CheckNear(24f, bWide.y, "and the gap between them (13 + 4 + 7)");
        Check(!SameRect(aSix, aWide), "the rects moved although no manifest attribute changed");
    }

    /// <summary>
    /// The documented escape hatch, measured against the pre-Batch-1 result reconstructed from an explicit
    /// zero geometry: a page with <c>Padding="0" Gap="0"</c> under a nonzero theme must arrange exactly
    /// like a page with no attributes under a zero theme, to the float.
    /// </summary>
    private static void VerifyEscapeHatch()
    {
        string children = W("a", RuleWidget.Kind, "Height=\"4\"") + W("b", RuleWidget.Kind, "Height=\"4\"");
        string bare = Page("<Stack Id=\"s\">" + children + "</Stack>");
        string escaped = Page("<Stack Id=\"s\" Padding=\"0\" Gap=\"0\">" + children + "</Stack>");

        UiTheme zero = UiTheme.Vanilla;
        zero.Geometry = new UiGeometry(0f, 4f, 0f, 28f, 1f);
        UiTheme wide = UiTheme.Vanilla;
        wide.Geometry = new UiGeometry(13f, 4f, 7f, 28f, 1f);

        UiLayoutSnapshot pre = Arrange(bare, zero, 300f, 400f);
        UiLayoutSnapshot esc = Arrange(escaped, wide, 300f, 400f);

        Check(SameRect(pre.RectById["a"], esc.RectById["a"])
            && SameRect(pre.RectById["b"], esc.RectById["b"]),
            "Padding=0/Gap=0 reproduces the pre-Batch-1 zero result exactly");
        CheckNear(0f, esc.RectById["a"].x, "the escape hatch's first child is at x=0");
        CheckNear(0f, esc.RectById["a"].y, "and y=0");
        CheckNear(4f, esc.RectById["b"].y, "and the second child follows with a zero gap");

        // Positive control: without the escape hatch the same manifest under the same theme does move.
        UiLayoutSnapshot wouldMove = Arrange(bare, wide, 300f, 400f);
        Check(!SameRect(wouldMove.RectById["a"], pre.RectById["a"]),
            "without the escape hatch the theme geometry would move the rect (positive control)");
    }

    /// <summary>
    /// The measure half and the draw half must resolve the same Padding. The attack is a pair of titled
    /// containers whose geometry comes from a per-element <c>Density</c> scope in a style document, so the
    /// scoped theme (not the injected one) is what both halves have to agree on.
    /// </summary>
    private static void VerifyMeasureDrawAgree()
    {
        UiStyleDocument document = UiStyleDocument.Parse(
            "<Styles Schema=\"1\">"
            + "<Density Name=\"roomy\"><Metric Token=\"Padding\" Value=\"11\" /><Metric Token=\"Gap\" Value=\"4\" /></Density>"
            + "<Density Name=\"tight\"><Metric Token=\"Padding\" Value=\"3\" /><Metric Token=\"Gap\" Value=\"1\" /></Density>"
            + "</Styles>");

        string xml = Page("<Column Id=\"root\" Padding=\"0\" Gap=\"0\">"
            + "<Column Id=\"box1\" Title=\"One\" Density=\"roomy\">"
            + W("a1", RuleWidget.Kind, "Height=\"4\"") + "</Column>"
            + "<Column Id=\"box2\" Title=\"Two\" Density=\"tight\">"
            + W("a2", RuleWidget.Kind, "Height=\"4\"") + "</Column>"
            + "</Column>");

        using UiHost host = NewHost(xml, document);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 300f));

        Rect box1 = snapshot.RectById["box1"];
        Rect a1 = snapshot.RectById["a1"];
        Rect box2 = snapshot.RectById["box2"];
        Rect a2 = snapshot.RectById["a2"];

        List<Rect> labels = StaticList<Rect>("LabelRects");
        int before = labels.Count;
        host.Draw(new Rect(0f, 0f, 300f, 300f), snapshot);

        CheckNear(11f, a1.x - box1.x, "the roomy scope's measure half places the child on its Padding (11)");
        CheckNear(3f, a2.x - box2.x, "the tight scope's measure half places the child on its Padding (3)");

        Check(labels.Count >= before + 2, "both titled containers drew their title (got " + (labels.Count - before) + " label(s))");
        if (labels.Count < before + 2) return;

        Rect title1 = labels[before];
        Rect title2 = labels[before + 1];
        CheckNear(11f, title1.x - box1.x, "the draw half resolves the roomy scope's Padding (11), so measure and draw agree");
        CheckNear(box1.width - 22f, title1.width, "and insets the title rect by that same Padding");
        CheckNear(3f, title2.x - box2.x, "the draw half resolves the tight scope's Padding (3) too");
        CheckNear(box2.width - 6f, title2.width, "and insets this title rect by that same Padding");
    }

    // --- CP-1 / CP-2: placement -----------------------------------------------------------

    /// <summary>
    /// The matrix on this lane's own frame: Overlay inner box 480 x 140 (Padding 10, Height 160), child
    /// 200 x 40. Every expected number is that arithmetic, not a value read from the author lane.
    /// </summary>
    private static void VerifyPlacementMatrix()
    {
        CheckX("AlignX=\"Left\"", 10f, "Left parks the element's left edge on the inner left");
        CheckX("AlignX=\"Center\"", 150f, "Center shares the fraction: 10 + 0.5 * (480 - 200)");
        CheckX("AlignX=\"Right\"", 290f, "Right parks the right edge on the inner right");
        CheckX("AlignX=\"Left\" OffsetX=\"0%\"", 10f, "0% is the inner origin");
        CheckX("AlignX=\"Left\" OffsetX=\"100%\"", 490f, "100% is the far edge of the inner span");
        CheckX("AlignX=\"Left\" OffsetX=\"25%\"", 130f, "25% is a quarter of the parent's 480, not of the child's 200");
        CheckX("AlignX=\"Center\" OffsetX=\"-40\"", 110f, "a bare negative number is a pixel nudge applied after the pivot");
        CheckX("AlignX=\"Left\" OffsetX=\"-30\"", -20f, "an unclamped pixel nudge may leave the parent; the engine applies the arithmetic raw");

        CheckY("AlignY=\"Top\"", 10f, "Top is the inner top");
        CheckY("AlignY=\"Middle\"", 60f, "Middle shares the fraction: 10 + 0.5 * (140 - 40)");
        CheckY("AlignY=\"Bottom\"", 110f, "Bottom parks the bottom edge on the inner bottom");
        CheckY("AlignY=\"Bottom\" OffsetY=\"10%\"", 124f, "10% is a tenth of the parent's 140");

        // The placed envelope is still the declared size; placement moves a box, it does not resize one.
        Rect sized = OverlayChild("AlignX=\"Right\" AlignY=\"Bottom\"");
        CheckNear(200f, sized.width, "placement never resizes the element");
        CheckNear(40f, sized.height, "on either axis");
    }

    /// <summary>
    /// R2 read as an arrangement: an absent attribute, an explicit <c>Stretch</c>, and an element with no
    /// declared width at all must land on one identical slot, on both a placement container and a flow
    /// container, to the float. (The pre-Batch-1 code always produced this slot; a clone cannot run that
    /// build, so the slot is reconstructed from the engine's documented flow origin instead.)
    /// </summary>
    private static void VerifyStretchEquivalence()
    {
        string overlayBare = Page("<Overlay Id=\"o\" Padding=\"10\" Gap=\"6\" Height=\"160\">"
            + W("c", RuleWidget.Kind, "Height=\"20\" Width=\"200\"") + "</Overlay>");
        string overlayStretch = Page("<Overlay Id=\"o\" Padding=\"10\" Gap=\"6\" Height=\"160\">"
            + W("c", RuleWidget.Kind, "Height=\"20\" AlignX=\"Stretch\"") + "</Overlay>");
        string overlayUnsized = Page("<Overlay Id=\"o\" Padding=\"10\" Gap=\"6\" Height=\"160\">"
            + W("c", RuleWidget.Kind, "Height=\"20\"") + "</Overlay>");

        Rect bare = Arrange(overlayBare, 500f, 200f).RectById["c"];
        Rect stretch = Arrange(overlayStretch, 500f, 200f).RectById["c"];
        Rect unsized = Arrange(overlayUnsized, 500f, 200f).RectById["c"];
        Check(SameRect(bare, stretch) && SameRect(bare, unsized),
            "absent, explicit Stretch and no declared width land on one identical slot");
        CheckNear(10f, bare.x, "which is the container's inner origin");
        CheckNear(480f, bare.width, "at the whole inner width");

        string columnBare = Page("<Column Id=\"c\" Padding=\"0\" Gap=\"0\">"
            + W("w", RuleWidget.Kind, "Height=\"20\" Width=\"100\"") + "</Column>");
        string columnStretch = Page("<Column Id=\"c\" Padding=\"0\" Gap=\"0\">"
            + W("w", RuleWidget.Kind, "Height=\"20\" AlignX=\"Stretch\"") + "</Column>");
        Rect flowBare = Arrange(columnBare, 400f, 200f).RectById["w"];
        Rect flowStretch = Arrange(columnStretch, 400f, 200f).RectById["w"];
        Check(SameRect(flowBare, flowStretch), "a flow child's absent and explicit Stretch are identical too");
        CheckNear(0f, flowBare.x, "the flow child keeps the flow's slot at x=0");
        CheckNear(400f, flowBare.width, "and the flow's full width, so a numeric Width beside no alignment is still the flow's to decide");
    }

    /// <summary>Every refusal in the contract's matrix, each one a located creation-time error.</summary>
    private static void VerifyRefusals()
    {
        ExpectRejected(Page(OverlayBody(W("c", RuleWidget.Kind, "Height=\"20\" Width=\"120\" AlignX=\"Diagonal\""))),
            "AlignX", "Diagonal", "box/c");
        ExpectRejected(Page(OverlayBody(W("c", RuleWidget.Kind, "Height=\"20\" Width=\"120\" AlignY=\"Left\""))),
            "AlignY", "Left", "box/c");
        ExpectRejected(Page(OverlayBody(W("c", RuleWidget.Kind, "Height=\"20\" AlignX=\"Left\" OffsetX=\"120%\""))),
            "OffsetX", "120%", "box/c");
        ExpectRejected(Page(OverlayBody(W("c", RuleWidget.Kind, "Height=\"20\" AlignX=\"Left\" OffsetX=\"-5%\""))),
            "OffsetX", "-5%", "box/c");
        ExpectRejected(Page(OverlayBody(W("c", RuleWidget.Kind, "Height=\"20\" AlignX=\"Left\" OffsetX=\"wide\""))),
            "OffsetX", "wide", "box/c");
        ExpectRejected(Page(OverlayBody(W("c", RuleWidget.Kind, "Height=\"20\" AlignX=\"Left\" OffsetX=\"NaN\""))),
            "OffsetX", "NaN", "box/c");
        ExpectRejected(Page(OverlayBody(W("c", RuleWidget.Kind, "Height=\"20\" Width=\"120\" AlignX=\"Stretch\""))),
            "AlignX", "Stretch", "box/c");
        ExpectRejected(Page("<Overlay Id=\"box\" Padding=\"0\">"
                + "<Column Id=\"c\" Fill=\"true\" AlignX=\"Center\" /></Overlay>"),
            "Fill", "true", "box/c");

        // R1: no container owns a root, and a flow container's child accepts only the cross axis.
        ExpectRejected(Page(W("c", RuleWidget.Kind, "Height=\"20\" AlignX=\"Center\"")),
            "AlignX", "Center", "c", null, false);
        ExpectRejected(Page("<Row Id=\"r\" Padding=\"0\">" + W("c", RuleWidget.Kind, "Height=\"20\" AlignX=\"Right\"") + "</Row>"),
            "AlignX", "Right", "r/c");
        ExpectRejected(Page("<Column Id=\"c\" Padding=\"0\">" + W("w", RuleWidget.Kind, "Height=\"20\" AlignY=\"Middle\"") + "</Column>"),
            "AlignY", "Middle", "c/w");
        ExpectRejected(Page("<Column Id=\"c\" Padding=\"0\">" + W("w", RuleWidget.Kind, "Height=\"20\" AlignX=\"Right\" OffsetY=\"4\"") + "</Column>"),
            "OffsetY", "4", "c/w");
        ExpectRejected(Page("<Column Id=\"c\" Padding=\"0\">" + W("w", RuleWidget.Kind, "Height=\"20\" AlignX=\"Center\" OffsetX=\"50%\"") + "</Column>"),
            "OffsetX", "50%", "c/w");
        ExpectRejected(Page("<Row Id=\"r\" Padding=\"0\">" + W("w", RuleWidget.Kind, "Height=\"20\" AlignY=\"Middle\" OffsetY=\"50%\"") + "</Row>"),
            "OffsetY", "50%", "r/w");

        // Accepted forms must not be over-refused (the refusal matrix is not a blanket ban).
        ExpectAccepted(Page(OverlayBody(W("c", RuleWidget.Kind,
            "Height=\"20\" Width=\"120\" AlignX=\"Center\" OffsetX=\"50%\" AlignY=\"Bottom\" OffsetY=\"12\""))),
            null, "a placement container accepts both axes with a ratio and a pixel nudge");
        ExpectAccepted(Page("<Row Id=\"r\" Padding=\"0\">" + W("w", RuleWidget.Kind, "Height=\"20\" AlignY=\"Middle\" OffsetY=\"5\"") + "</Row>"),
            null, "a flow child keeps its cross-axis alignment with a pixel nudge");
        ExpectAccepted(Page("<Column Id=\"c\" Padding=\"0\">" + W("w", RuleWidget.Kind, "Height=\"20\" Width=\"60\" AlignX=\"Right\" OffsetX=\"-8\"") + "</Column>"),
            null, "a column child keeps its cross-axis alignment with a pixel nudge");
        ExpectAccepted(Page(OverlayBody(W("c", RuleWidget.Kind, "Height=\"20\" Width=\"120\""))),
            null, "a numeric Width with no alignment still arranges, so an existing page does not migrate");
    }

    /// <summary>
    /// E1/E3: the envelope is the arranged rect. The built-in <c>chrome/rule</c> paints a 1px hairline
    /// inset to 20px wide inside a 200 x 40 rect, so placement must follow the rect and never the paint.
    /// </summary>
    private static void VerifyEnvelopeByRect()
    {
        string xml = Page("<Overlay Id=\"box\" Padding=\"10\" Gap=\"6\" Height=\"160\">"
            + W("rail", RuleWidget.Kind, "Width=\"200\" Height=\"40\" Inset=\"90\" AlignX=\"Right\" AlignY=\"Bottom\"")
            + "</Overlay>");

        using UiHost host = NewHost(xml);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(500f, 200f));
        Rect rail = snapshot.RectById["rail"];
        CheckNear(290f, rail.x, "the envelope's x is the 200-wide arranged rect (10 + 480 - 200), not the 20px paint");
        CheckNear(110f, rail.y, "the envelope's y is the 40-tall arranged rect (10 + 140 - 40), not the 1px paint");
        CheckNear(200f, rail.width, "the envelope keeps the arranged width");
        CheckNear(40f, rail.height, "the envelope keeps the arranged height");

        List<Rect> painted = StaticList<Rect>("DrawBoxSolidRects");
        int before = painted.Count;
        host.Draw(new Rect(0f, 0f, 500f, 200f), snapshot);

        Check(painted.Count == before + 1, "the rule painted exactly one solid (got " + (painted.Count - before) + ")");
        if (painted.Count != before + 1) return;

        Rect railPaint = painted[before];
        CheckNear(20f, railPaint.width, "the paint really is far narrower than its 200px envelope (Inset=90)");
        CheckNear(1f, railPaint.height, "and far shorter than its 40px envelope");
        CheckNear(129.5f, railPaint.y, "the paint sits on the rect's vertical centre (110 + (40 - 1)/2)");
        Check(!Near(470f, rail.x), "placement did not use the 20px painted width");
        Check(!Near(149f, rail.y), "placement did not use the 1px painted height");
    }

    /// <summary>
    /// The L1 question the architecture doc leaves unowned, made executable for this batch's vocabulary:
    /// every name that creation-time validation ACCEPTS must move the arrangement in the context it was
    /// accepted in. A name accepted but inert is an orphan name, and this lane reports it as a failure.
    /// </summary>
    private static void VerifyAcceptedNamesAreConsumed()
    {
        // Placement container: all four names.
        string overlayBase = "<Overlay Id=\"o\" Padding=\"0\" Gap=\"0\" Height=\"120\">{0}</Overlay>";
        ExpectConsumed("Overlay child AlignX", "w",
            Page(Fmt(overlayBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignX=\"Left\""))),
            Page(Fmt(overlayBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignX=\"Right\""))));
        ExpectConsumed("Overlay child OffsetX", "w",
            Page(Fmt(overlayBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignX=\"Left\" OffsetX=\"0\""))),
            Page(Fmt(overlayBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignX=\"Left\" OffsetX=\"40\""))));
        ExpectConsumed("Overlay child AlignY", "w",
            Page(Fmt(overlayBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignY=\"Top\""))),
            Page(Fmt(overlayBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignY=\"Bottom\""))));
        ExpectConsumed("Overlay child OffsetY", "w",
            Page(Fmt(overlayBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignY=\"Top\" OffsetY=\"0\""))),
            Page(Fmt(overlayBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignY=\"Top\" OffsetY=\"25\""))));

        // Row / Wrap: the vertical cross axis.
        string rowBase = "<Row Id=\"r\" Padding=\"0\" Gap=\"0\" Height=\"120\">{0}{1}</Row>";
        string rowFiller = W("f", RuleWidget.Kind, "Width=\"60\" Height=\"90\"");
        ExpectConsumed("Row child AlignY", "w",
            Page(Fmt2(rowBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignY=\"Top\""), rowFiller)),
            Page(Fmt2(rowBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignY=\"Bottom\""), rowFiller)));
        ExpectConsumed("Row child OffsetY", "w",
            Page(Fmt2(rowBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignY=\"Top\" OffsetY=\"0\""), rowFiller)),
            Page(Fmt2(rowBase, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignY=\"Top\" OffsetY=\"15\""), rowFiller)));

        string wrapBase = "<Wrap Id=\"p\" Padding=\"0\" Gap=\"0\" Height=\"200\">{0}{1}</Wrap>";
        string wrapFiller = W("f", RuleWidget.Kind, "Width=\"200\" Height=\"40\"");
        ExpectConsumed("Wrap child AlignY", "w",
            Page(Fmt2(wrapBase, W("w", RuleWidget.Kind, "Width=\"100\" Height=\"10\" AlignY=\"Top\""), wrapFiller)),
            Page(Fmt2(wrapBase, W("w", RuleWidget.Kind, "Width=\"100\" Height=\"10\" AlignY=\"Bottom\""), wrapFiller)));
        ExpectConsumed("Wrap child OffsetY", "w",
            Page(Fmt2(wrapBase, W("w", RuleWidget.Kind, "Width=\"100\" Height=\"10\" AlignY=\"Top\" OffsetY=\"0\""), wrapFiller)),
            Page(Fmt2(wrapBase, W("w", RuleWidget.Kind, "Width=\"100\" Height=\"10\" AlignY=\"Top\" OffsetY=\"5\""), wrapFiller)));

        // Vertical flow containers: the horizontal cross axis.
        foreach (string kind in new[] { "Column", "Stack", "Section", "Surface", "Scroll", "Clip" })
        {
            string baseXml = "<" + kind + " Id=\"c\" Padding=\"0\" Gap=\"0\" Height=\"120\">{0}</" + kind + ">";
            ExpectConsumed(kind + " child AlignX", "w",
                Page(Fmt(baseXml, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignX=\"Left\""))),
                Page(Fmt(baseXml, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignX=\"Right\""))));
            ExpectConsumed(kind + " child OffsetX", "w",
                Page(Fmt(baseXml, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignX=\"Left\" OffsetX=\"0\""))),
                Page(Fmt(baseXml, W("w", RuleWidget.Kind, "Width=\"60\" Height=\"20\" AlignX=\"Left\" OffsetX=\"40\""))));
        }
    }

    /// <summary>
    /// The documented boundary: validation reads the DECLARED kind, the engine reads the EFFECTIVE one.
    /// A declared Row carrying <c>Breakpoint</c> + <c>Narrow="Column"</c> accepts a child's <c>AlignY</c>;
    /// it applies wide and is inert narrow rather than refused.
    /// </summary>
    private static void VerifyNarrowSwapBoundary()
    {
        string xml = Page("<Row Id=\"r\" Breakpoint=\"400\" Narrow=\"Column\" Padding=\"0\" Gap=\"0\" Height=\"120\">"
            + W("w", RuleWidget.Kind, "Width=\"120\" Height=\"20\" AlignY=\"Bottom\"") + "</Row>");

        CheckAccepted(xml, null, "a Row with Breakpoint + Narrow=Column accepts a cross-axis placement on its child");

        UiLayoutSnapshot wide = Arrange(xml, 600f, 300f);
        UiLayoutSnapshot narrow = Arrange(xml, 300f, 300f);

        CheckNear(100f, wide.RectById["w"].y,
            "wide (effective Row) the AlignY=Bottom placement applies: 0 + 1 * (120 - 20)");
        CheckNear(0f, narrow.RectById["w"].y,
            "narrow (effective Column) the same declaration is inert, exactly as the contract states");
    }

    /// <summary>
    /// The second boundary: a template root has no container parent, so all four names are refused there;
    /// an element inside a template is a real child and keeps the cross-axis subset.
    /// </summary>
    private static void VerifyTemplateBoundary()
    {
        var items = new List<string> { "a" };
        var bindings = new UiBindings();
        bindings.BindReadOnly<IReadOnlyList<string>>("items", () => items, UiInvalidation.Structure);

        ExpectRejected(TemplatePage("AlignX=\"Center\""), "AlignX", "Center", "<Templates>/t", bindings, false);
        ExpectRejected(TemplatePage("OffsetX=\"4\""), "OffsetX", "4", "<Templates>/t", bindings, false);
        ExpectRejected(TemplatePage("AlignY=\"Bottom\""), "AlignY", "Bottom", "<Templates>/t", bindings, false);
        ExpectRejected(TemplatePage("OffsetY=\"4\""), "OffsetY", "4", "<Templates>/t", bindings, false);

        ExpectAccepted(
            Page("<Templates><Column Id=\"t\" Padding=\"0\" Gap=\"0\">"
            + W("g", RuleWidget.Kind, "Height=\"10\" Width=\"60\" AlignX=\"Center\"")
            + "</Column></Templates><Repeat Id=\"rep\" Items=\"items\" Template=\"t\" />"),
            bindings, "an element inside a template is a real child and keeps the cross-axis subset");
    }

    // --- cross-cutting ---------------------------------------------------------------------

    /// <summary>
    /// The plan promised "no new public type": the new file's types must stay internal, so the API-tier
    /// type list is untouched. (The authoritative classification is FerriteLibApiTierTests, run with the
    /// whole harness.)
    /// </summary>
    private static void VerifyPlacementTypesAreInternal()
    {
        Assembly assembly = typeof(UiTheme).Assembly;
        Type? placement = assembly.GetType("FerriteLib.UiKit.Kernel.UiPlacement");
        Check(placement != null, "the new placement helper exists in the payload");
        Check(placement != null && !placement.IsPublic && !placement.IsNestedPublic,
            "and it is internal, not a public type");

        int publicHelpers = 0;
        foreach (Type type in assembly.GetTypes())
        {
            if (!type.IsPublic && !type.IsNestedPublic) continue;
            if (type.Name.IndexOf("Placement", StringComparison.Ordinal) >= 0
                || type.Name == "Alignment" || type.Name == "Axis" || type.Name == "WrapLineItem")
            {
                publicHelpers++;
            }
        }

        Check(publicHelpers == 0, "no placement-related public type was added (found " + publicHelpers + ")");
    }

    // --- harness ---------------------------------------------------------------------------

    /// <summary>
    /// E3's second half (U2): content that exceeds an element's arranged rect must arrive as a finding on
    /// the existing fit-audit channel, on the named axis of the two-axis <see cref="UiOverflowAxis"/>
    /// vocabulary. The author lane proves the overflow moves nothing; this proves it is reported.
    /// </summary>
    private static void VerifyOverflowReachesTheFitAudit()
    {
        Check(Array.IndexOf(Enum.GetNames(typeof(UiOverflowAxis)), "Height") >= 0
            && Array.IndexOf(Enum.GetNames(typeof(UiOverflowAxis)), "Width") >= 0,
            "the fit audit's overflow vocabulary is two-axis (Width and Height)");

        var reports = new List<UiOverflowReport>();
        UiFitAudit.Reset();
        UiFitAudit.Attach(new OverflowMetrics(), reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            // One ruler for the host and the audit both, so the finding is not an artefact of two
            // different measures: the element's declared Height is what makes its rect too small.
            string xml = Page("<Stack Id=\"s\" Padding=\"0\" Gap=\"0\">"
                + W("t", WrappedTextWidget.Kind, "Text=\"abcdefghij\" Height=\"20\"")
                + "</Stack>");
            using UiHost host = new(
                Scope, UiLayoutManifest.Parse(xml), new UiBindings(), UiTheme.DarkGold, new OverflowMetrics(), Translation());
            host.DrawFrame(new Rect(0f, 0f, 120f, 200f));

            Check(reports.Count >= 1,
                "content exceeding the element's arranged rect raises a fit-audit finding (got " + reports.Count + ")");

            UiOverflowReport? onHeight = null;
            foreach (UiOverflowReport report in reports)
            {
                if (report.Axis == UiOverflowAxis.Height)
                {
                    onHeight = report;
                    break;
                }
            }

            Check(onHeight.HasValue, "and the finding arrives on the named Height axis of UiOverflowAxis");
            if (onHeight.HasValue)
            {
                UiOverflowReport finding = onHeight.Value;
                Check(finding.Needed > finding.Available,
                    "with need greater than the band it was given (needed " + finding.Needed
                    + " > available " + finding.Available + ")");
                Check(finding.ElementPath.IndexOf("t", StringComparison.Ordinal) >= 0,
                    "and attributed to the overflowing element path (got '" + finding.ElementPath + "')");
                Console.WriteLine("  note: U2 overflow finding: path='" + finding.ElementPath
                    + "', axis=" + finding.Axis + ", needed=" + finding.Needed + ", available=" + finding.Available);
            }
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
            UiFitAudit.Enabled = false;
        }
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

    private static void CheckNear(float expected, float actual, string name)
    {
        Check(Near(expected, actual), name + " (expected " + expected + ", got " + actual + ")");
    }

    private static bool Near(float left, float right)
    {
        return Math.Abs(left - right) <= 0.001f;
    }

    private static bool SameRect(Rect left, Rect right)
    {
        return Near(left.x, right.x) && Near(left.y, right.y)
            && Near(left.width, right.width) && Near(left.height, right.height);
    }

    private static bool SameColor(Color left, Color right)
    {
        return Near(left.r, right.r) && Near(left.g, right.g)
            && Near(left.b, right.b) && Near(left.a, right.a);
    }

    private static Color Lift(Color accent)
    {
        return new Color(
            accent.r + (1f - accent.r) * 0.3f,
            accent.g + (1f - accent.g) * 0.3f,
            accent.b + (1f - accent.b) * 0.3f,
            accent.a);
    }

    private static void ResetHarness()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiFitAudit.Detach();
        UiFitAudit.Reset();
        UiFitAudit.Enabled = false;
    }

    private static string Page(string body)
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>";
    }

    private static string TemplatePage(string placementAttributes)
    {
        return Page("<Templates><Column Id=\"t\" Padding=\"0\" Gap=\"0\" " + placementAttributes + ">"
            + W("g", RuleWidget.Kind, "Height=\"4\"")
            + "</Column></Templates><Repeat Id=\"rep\" Items=\"items\" Template=\"t\" />");
    }

    private static string OverlayBody(string child)
    {
        return "<Overlay Id=\"box\" Padding=\"0\" Gap=\"0\" Height=\"100\">" + child + "</Overlay>";
    }

    private static string W(string id, string kind, string attributes)
    {
        return "<Widget Id=\"" + id + "\" Kind=\"" + kind + "\" " + attributes + " />";
    }

    private static string Rule(string id, string tone)
    {
        return W(id, RuleWidget.Kind, "Tone=\"" + tone + "\"");
    }

    private static string Fmt(string template, string child)
    {
        return template.Replace("{0}", child);
    }

    private static string Fmt2(string template, string first, string second)
    {
        return template.Replace("{0}", first).Replace("{1}", second);
    }

    private static UiHost NewHost(string xml, UiStyleDocument? document = null, IUiBindings? bindings = null, UiTheme? theme = null)
    {
        return new UiHost(
            Scope,
            UiLayoutManifest.Parse(xml),
            bindings ?? new UiBindings(),
            theme ?? UiTheme.DarkGold,
            Metrics(),
            Translation(),
            document);
    }

    private static UiLayoutSnapshot Arrange(string xml, float width, float height)
    {
        return Arrange(xml, UiTheme.DarkGold, width, height);
    }

    private static UiLayoutSnapshot Arrange(string xml, UiTheme theme, float width, float height)
    {
        using UiHost host = NewHost(xml, null, null, theme);
        return host.MeasureAndArrange(new Vector2(width, height));
    }

    private static Rect OverlayChild(string childAttributes)
    {
        string xml = Page("<Overlay Id=\"o\" Padding=\"10\" Gap=\"6\" Height=\"160\">"
            + W("c", RuleWidget.Kind, "Width=\"200\" Height=\"40\" " + childAttributes)
            + "</Overlay>");
        return Arrange(xml, 500f, 200f).RectById["c"];
    }

    private static void CheckX(string childAttributes, float expectedX, string what)
    {
        Rect rect = OverlayChild(childAttributes);
        Check(Near(rect.x, expectedX), what + " (x=" + rect.x + ", expected " + expectedX + ")");
    }

    private static void CheckY(string childAttributes, float expectedY, string what)
    {
        Rect rect = OverlayChild(childAttributes);
        Check(Near(rect.y, expectedY), what + " (y=" + rect.y + ", expected " + expectedY + ")");
    }

    private static void ExpectRejected(string xml, string attribute, string value, string pathFragment, IUiBindings? bindings = null, bool requireValue = true)
    {
        try
        {
            using UiHost host = NewHost(xml, null, bindings);
            Check(false, "refusing " + attribute + "='" + value + "' - but creation accepted it");
        }
        catch (UiContractException ex)
        {
            bool located = ex.ElementPath.IndexOf(pathFragment, StringComparison.Ordinal) >= 0;
            bool named = ex.Message.IndexOf(attribute, StringComparison.Ordinal) >= 0
                && (!requireValue || ex.Message.IndexOf(value, StringComparison.Ordinal) >= 0);
            Check(located && named,
                "refusing " + attribute + "='" + value + "' at '" + pathFragment + "' throws UiContractException naming it"
                + " (path='" + ex.ElementPath + "', message='" + ex.Message + "')");
        }
    }

    private static void ExpectAccepted(string xml, IUiBindings? bindings, string what)
    {
        try
        {
            using UiHost host = NewHost(xml, null, bindings);
            Check(true, what);
        }
        catch (UiContractException ex)
        {
            Check(false, what + " - rejected at creation: " + ex.Message);
        }
    }

    private static void CheckAccepted(string xml, IUiBindings? bindings, string what)
    {
        ExpectAccepted(xml, bindings, what);
    }

    private static void ExpectConsumed(string what, string id, string baselineXml, string variantXml)
    {
        Rect baseline = default;
        Rect variant = default;
        try
        {
            baseline = Arrange(baselineXml, 400f, 300f).RectById[id];
            variant = Arrange(variantXml, 400f, 300f).RectById[id];
        }
        catch (UiContractException ex)
        {
            Check(false, what + ": the fixture was refused at creation, so consumption is unverified: " + ex.Message);
            return;
        }

        Check(!SameRect(baseline, variant),
            what + " is accepted AND consumed (baseline " + Describe(baseline) + " vs variant " + Describe(variant) + ")");
        if (SameRect(baseline, variant))
        {
            Console.Error.WriteLine("  NOTE: L1 orphan-name candidate: " + what
                + " was accepted but arranged identically at " + Describe(baseline));
        }
    }

    private static string Describe(Rect rect)
    {
        return "(" + rect.x + "," + rect.y + "," + rect.width + "," + rect.height + ")";
    }

    private static Color DrawLeaf(UiWidgetContext ctx, params (string Name, string Value)[] attributes)
    {
        WrappedTextWidget leaf = new();
        leaf.Configure(new UiElementSpec("leaf", WrappedTextWidget.Kind, Attributes(attributes)));
        List<Color> labels = StaticList<Color>("LabelColors");
        int before = labels.Count;
        leaf.Draw(new Rect(0f, 0f, 200f, 40f), ctx);
        if (labels.Count <= before)
        {
            Check(false, "the leaf painted no label, so its treatment cannot be read");
            return default;
        }

        return labels[labels.Count - 1];
    }

    private static Dictionary<string, string> Attributes(params (string Name, string Value)[] pairs)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string value) in pairs)
        {
            attributes[name] = value;
        }

        return attributes;
    }

    private static List<T> StaticList<T>(string field)
    {
        FieldInfo? info = typeof(Verse.Widgets).GetField(field, BindingFlags.Public | BindingFlags.Static);
        if (info == null) throw new Exception("Verse stub is missing the recorder field " + field);
        object? value = info.GetValue(null);
        if (value is not List<T> list) throw new Exception("Verse stub field " + field + " is not List<" + typeof(T).Name + ">");
        return list;
    }

    private static ITextMetrics Metrics()
    {
        return new StubMetrics();
    }

    private static IUiTranslation Translation()
    {
        return new StubTranslation();
    }

    /// <summary>
    /// The U2 ruler: it reserves far more height than the 20px band the element declares, which is what
    /// makes the element's content exceed its arranged rect.
    /// </summary>
    private sealed class OverflowMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return string.IsNullOrEmpty(text) ? 0f : 40f;
        }

        public float MeasureWidth(string text, UiFont font)
        {
            return StubTextWidth.Of(text, font);
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return font switch
            {
                UiFont.Tiny => 12f,
                UiFont.Medium => 18f,
                _ => 16f
            };
        }

        public float MeasureWidth(string text, UiFont font)
        {
            return StubTextWidth.Of(text, font);
        }
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
