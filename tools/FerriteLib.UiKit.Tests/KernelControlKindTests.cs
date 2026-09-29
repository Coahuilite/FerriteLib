using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The three 0.5 common controls, each against its own contract.
/// <list type="bullet">
/// <item><b>input/checkbox</b> - two states over a bool binding, its own hit rule, and the two disabled
/// shapes: a value the model publishes read-only (the writability funnel) and a command the owner has taken
/// away (the command funnel P2 built). Neither executes, neither captures the pointer, and both paint the one
/// disabled treatment. Its <c>Appearance</c> axis is resolved through the SHARED seam
/// (<see cref="UiAppearanceResolver"/>), which is what makes the measured body and the drawn body one number
/// and what makes an unknown look a creation-time refusal instead of a silent default.</item>
/// <item><b>display/progress</b> - the value contract (a float read as a fraction of a declared Max, clamped),
/// the measure contract (one fixed band, so a value announcement is Paint-class and reuses the arrangement),
/// and the negative rule that it takes no pointer at all.</item>
/// <item><b>container/tree</b> - per-row band geometry from the declared or density row height, one indent per
/// level, a per-band hit rule whose payload is the row's own stable key, and the refusal of rows that cannot
/// carry identity.</item>
/// </list>
/// </summary>
internal static class KernelControlKindTests
{
    private const string Scope = "control-lane";

    private static int failures;
    private static bool flagValue;
    private static float pctValue;
    private static int commandFired;
    private static bool commandAllowed = true;
    private static string pickedKey = "";
    private static List<UiTreeRow> treeRows = new();

    public static int RunAll()
    {
        failures = 0;
        Run("A checkbox is two states over a bool binding and its click writes the inverse", VerifyCheckboxToggles);
        Run("The bool kind's selected appearance enters natural size and shares one input path", VerifyBooleanAppearanceSeam);
        Run("The switch thumb: OFF neutral ink, ON the accent", VerifySwitchThumbTokens);
        Run("A checkbox's hit rule is its own band: a click outside writes nothing", VerifyCheckboxHitRule);
        Run("A read-only checkbox paints the disabled treatment and takes no pointer", VerifyReadOnlyCheckbox);
        Run("A command-gated checkbox neither executes nor captures", VerifyGatedCheckbox);
        Run("A checkbox refuses a missing, mistyped or unbound contract at creation", VerifyCheckboxCreationRefusals);
        Run("A progress value is a clamped fraction of its declared Max", VerifyProgressValueContract);
        Run("A progress announcement reuses the arrangement instead of re-measuring", VerifyProgressIsPaintClass);
        Run("A progress control takes no pointer at all", VerifyProgressTakesNoPointer);
        Run("A progress refuses a missing binding and a malformed Max at creation", VerifyProgressCreationRefusals);
        Run("A tree band is one row tall and indents one step per level", VerifyTreeGeometry);
        Run("A tree's hit payload is the clicked row's own key, across a reorder", VerifyTreeHitFollowsKey);
        Run("A tree's expansion marker follows the model by stable key, not by band index", VerifyTreeExpansionFollowsKey);
        Run("A tree refuses a row it cannot identify and still hits the rows it can", VerifyTreeRefusals);
        Run("A tree refuses its own creation contract violations", VerifyTreeCreationRefusals);
        return failures;
    }

    // --- boolean appearance seam (R1) -----------------------------------------------------------

    /// <summary>The default square switch's body with nothing drawn beside it: the track alone.</summary>
    private const float SwitchBody = 34f;

    /// <summary>The gap the switch holds before a label it actually draws.</summary>
    private const float SwitchGap = 6f;

    /// <summary>The checkbox look's own floor, mirroring the kind's documented minimum box.</summary>
    private const float CheckboxMinSide = 8f;

    /// <summary>
    /// Where the switch's thumb lands in a cleared recording: one painted surface is one fill plus four
    /// edges (<c>VerifyDensityTokens</c> pins that count), and the knob is the next solid after the track.
    /// </summary>
    private const int SwitchKnobSolidIndex = 5;

    /// <summary>
    /// The checkbox look's variant of the same quantity the switch's body states: the box side inside a
    /// default uniform band. Spelled as the arithmetic rather than as a literal, so the lane says WHICH
    /// relationship it is pinning - "the box is the band less its own padding, floored" - instead of pinning a
    /// number a run once produced.
    /// </summary>
    private static readonly float CheckboxBoxSide = UiGeometry.Default.RowHeight - UiGeometry.Default.Padding * 2f;

    /// <summary>
    /// R1's new seam, asserted on the REAL registered kind through a host - not on a helper.
    /// <list type="number">
    /// <item>the default appearance is the square switch, and its BODY enters <c>Width="Auto"</c>: with no
    /// label the natural width is the body alone, which text-only measurement could never produce;</item>
    /// <item>the DRAWN track is that same number, so measure and draw are one value rather than two
    /// derivations that happen to agree;</item>
    /// <item>each appearance is measured against its OWN expectation, so the lane says what a look's body is
    /// rather than asserting a relationship between two quantities the metrics never defined;</item>
    /// <item>a label is added to the body rather than replacing it, and a BLANK declaration reserves
    /// nothing;</item>
    /// <item>both appearances share ONE input path: each writes exactly once per click;</item>
    /// <item>the declaration is resolved through the SHARED seam: the registry publishes the kind's look set
    /// and the set answers the same way it does, case-insensitively, while an unknown value is refused at
    /// creation instead of being silently drawn as the default;</item>
    /// <item>the switch's OFF outline is the theme's control edge, so it survives a scope that flattens the
    /// raised surface onto its own edge.</item>
    /// </list>
    /// </summary>
    private static void VerifyBooleanAppearanceSeam()
    {
        // Body only, default appearance (no Appearance attribute at all).
        flagValue = false;
        float switchWidth;
        using (UiHost host = Host(BoolAutoPage(""), CheckboxBindings()))
        {
            UiLayoutSnapshot snapshot = Arrange(host);
            switchWidth = snapshot.RectById["flag"].width;
            Check(Near(switchWidth, SwitchBody),
                "an unlabelled bool control with the default appearance reserves its own body, not zero: got "
                + switchWidth.ToString("0.###") + ", expected " + SwitchBody.ToString("0.###"));
            ClickAt(host, Centre(snapshot.RectById["flag"]));
            Check(flagValue, "the default switch is the same one bool behaviour: a click writes the inverse");
        }

        // The other appearance, with the SAME fixture shape as the case-folding check below: an unlabelled
        // bool control. Its natural body is derived from the pinned metrics rather than read off the run, so
        // the expectation and the mechanism are stated together.
        flagValue = false;
        using (UiHost host = Host(BoolAutoPage(" Appearance=\"checkbox\""), CheckboxBindings()))
        {
            UiLayoutSnapshot snapshot = Arrange(host);
            float boxWidth = snapshot.RectById["flag"].width;
            Check(Near(boxWidth, CheckboxBoxSide),
                "the checkbox appearance reserves its box side, derived from the pinned metrics - body "
                + CheckboxBoxSide.ToString("0.###") + " = max(" + CheckboxMinSide.ToString("0.###")
                + ", " + (UiGeometry.Default.RowHeight - UiGeometry.Default.Padding * 2f).ToString("0.###")
                + "), gap included: got " + boxWidth.ToString("0.###"));
            ClickAt(host, Centre(snapshot.RectById["flag"]));
            Check(flagValue, "the checkbox appearance takes the SAME input path and writes once");
        }

        // The two appearances are DIFFERENT SHAPES, and each is asserted against its own expectation rather
        // than against the other's: comparing them to each other would be asserting a relationship the
        // metrics do not define, which is exactly how a lane ends up pinning an accident.
        Check(!Near(SwitchBody, CheckboxBoxSide),
            "switch body and checkbox box side are two quantities, not one constant: switch="
            + SwitchBody.ToString("0.###") + " checkbox=" + CheckboxBoxSide.ToString("0.###"));
        using (UiHost host = Host(BoolAutoPage(" Appearance=\"switch\""), CheckboxBindings()))
        {
            Check(Near(Arrange(host).RectById["flag"].width, SwitchBody),
                "the switch look measures its own track body, spelled out explicitly");
        }

        // A BLANK label reserves nothing: the attribute is declared, but no text is drawn beside the body.
        using (UiHost host = Host(BoolAutoPage(" Label=\"\""), CheckboxBindings()))
        {
            UiLayoutSnapshot snapshot = Arrange(host);
            float blank = snapshot.RectById["flag"].width;
            Check(Near(blank, SwitchBody),
                "a blank label reserves no gap: got " + blank.ToString("0.###") + ", expected "
                + SwitchBody.ToString("0.###"));
        }

        // A drawn label is ADDED to the body, with the switch's own gap.
        using (UiHost host = Host(BoolAutoPage(" Label=\"Flag\""), CheckboxBindings()))
        {
            UiLayoutSnapshot snapshot = Arrange(host);
            float labelled = snapshot.RectById["flag"].width;
            Check(labelled > SwitchBody + SwitchGap,
                "the label is added to the body and its gap, not measured instead of it: got "
                + labelled.ToString("0.###") + ", body+gap is "
                + (SwitchBody + SwitchGap).ToString("0.###"));
        }

        // A declared Height still decides the band, ahead of the appearance's natural height.
        using (UiHost host = Host(BoolAutoPage(" Height=\"40\""), CheckboxBindings()))
        {
            UiLayoutSnapshot snapshot = Arrange(host);
            Check(Near(snapshot.RectById["flag"].height, 40f),
                "an explicit Height still decides the band, got "
                + snapshot.RectById["flag"].height.ToString("0.###"));
        }

        // The declared look set is the SHARED seam, not a private reading of the attribute: the registry
        // publishes it for the kind, and the resolver it hands back is the one the widget resolves through.
        UiAppearanceResolver? resolver = UiWidgetRegistry.GetAppearanceResolver(Scope, CheckboxWidget.Kind);
        Check(resolver != null, "the registry publishes the bool kind's appearance seam");
        Check(resolver != null && string.Equals(resolver.Default, CheckboxWidget.SwitchLook, StringComparison.Ordinal),
            "whose default look is the square switch, so an element that declares nothing is not look-less");
        var supported = new List<string>(resolver?.Supported ?? (IReadOnlyCollection<string>)Array.Empty<string>());
        Check(supported.Contains(CheckboxWidget.SwitchLook) && supported.Contains(CheckboxWidget.CheckboxLook)
            && supported.Count == 2,
            "and it carries exactly the two looks the kind draws: [" + string.Join(", ", supported) + "]");

        // The seam folds case, so a declaration is not a spelling test - and the VALUE it resolves to is the
        // canonical name, which is what every later comparison reads.
        var upper = new UiElementSpec("flag", CheckboxWidget.Kind, new Dictionary<string, string>
        {
            ["Appearance"] = "CHECKBOX"
        });
        ResolvedAppearance upperLook = resolver!.Resolve(upper);
        Check(upperLook.Declared && string.Equals(upperLook.Value, CheckboxWidget.CheckboxLook, StringComparison.Ordinal),
            "a differently-cased declaration resolves to the canonical look name, not to the authored spelling");
        using (UiHost host = Host(BoolAutoPage(" Appearance=\"CHECKBOX\""), CheckboxBindings()))
        {
            float upperWidth = Arrange(host).RectById["flag"].width;
            Check(Near(upperWidth, CheckboxBoxSide),
                "and the host measures the same checkbox body the lower-case spelling does - the comparison is "
                + "checkbox against checkbox, the equivalent fixture: CHECKBOX=" + upperWidth.ToString("0.###")
                + ", checkbox=" + CheckboxBoxSide.ToString("0.###"));
            Check(!Near(upperWidth, switchWidth),
                "while the switch keeps its own, unrelated body: the look's distinction is intact");
        }

        // The negative control for the resolver's own inputs: the SAME element with no declaration answers
        // the default and reports it as undeclared, so "declared" is measuring the declaration and not the
        // resolver's willingness to say yes.
        var silent = new UiElementSpec("flag", CheckboxWidget.Kind, new Dictionary<string, string>());
        ResolvedAppearance silentLook = resolver!.Resolve(silent);
        Check(!silentLook.Declared && string.Equals(silentLook.Value, CheckboxWidget.SwitchLook, StringComparison.Ordinal),
            "an element that declares nothing answers the default AND reports that nothing was declared");

        // An unknown value is refused AT CREATION through the same seam, rather than degrading per frame.
        Check(CreationFailure(BoolPage(" Appearance=\"fancy\"")) != null,
            "an unknown appearance is refused at creation through the shared look set");
        Check(CreationFailure(BoolPage(" Appearance=\"switch\"")) == null,
            "while the declared default, spelled out, is accepted");
        Check(CreationFailure(BoolPage(" Appearance=\"Checkbox\"")) == null,
            "and so is the alternative under either case");

        // The OFF outline is the theme's control edge. A palette that flattens the raised plane onto its own
        // edge (fill == border, this vocabulary's way of saying "no box") must still leave the track visible,
        // which is only possible if the fill and the edge come from two independent tokens.
        var flat = new Color(0.10f, 0.09f, 0.07f, 1f);
        var controlEdge = new Color(0.55f, 0.55f, 0.55f, 1f);
        UiTheme flattened = UiTheme.Vanilla;
        flattened.Raised = flat;
        flattened.RaisedBorder = flat;
        flattened.BorderStrong = controlEdge;

        flagValue = false;
        using (UiHost host = Host(BoolAutoPage(""), CheckboxBindings(), flattened))
        {
            ClearDraws();
            host.DrawFrame(new Rect(0f, 0f, 200f, 60f));
            IList colors = RecordedColors();
            Check(colors.Count >= 5, "the OFF switch painted its track: " + colors.Count + " solid(s)");
            bool fill = colors.Count > 0 && SameColor((Color)colors[0], flat);
            bool edge = colors.Count >= 5;
            for (int i = 1; i < 5 && i < colors.Count; i++)
            {
                edge &= SameColor((Color)colors[i], controlEdge);
            }

            Check(fill, "the OFF track fills with the raised plane it sits on");
            Check(edge, "and outlines with the theme's control edge, so a flattened raised surface cannot hide it");
        }
    }

    /// <summary>
    /// The switch's thumb, observed on ACTUAL DRAW calls rather than on the source: one painted surface is one
    /// fill plus four edges, so the sixth recorded solid on an otherwise empty page is the knob.
    /// <list type="bullet">
    /// <item><b>MUTATION-TARGET</b> - an ON switch paints its thumb in the ACCENT. The accent is what says
    /// "on"; the selected plane's TEXT colour is ink FOR a gold plane, and a thumb wearing it reads as a label
    /// on the control rather than as the control's own state.</item>
    /// <item><b>GUARD</b> - an OFF switch paints its thumb in the neutral ink and no accent at all. It holds
    /// on both sides of the ON mutation, so it is a regression guard, not the failure-sensitive half.</item>
    /// </list>
    /// </summary>
    private static void VerifySwitchThumbTokens()
    {
        flagValue = true;
        using (UiHost on = Host(BoolAutoPage(""), CheckboxBindings()))
        {
            ClearDraws();
            on.DrawFrame(new Rect(0f, 0f, 200f, 60f));
            IList onColors = RecordedColors();
            Check(onColors.Count > SwitchKnobSolidIndex,
                "an ON switch recorded its track and its thumb: " + onColors.Count + " solid(s)");
            Check(onColors.Count > SwitchKnobSolidIndex
                    && SameColor((Color)onColors[SwitchKnobSolidIndex], UiTheme.Vanilla.AccentGold),
                "an ON switch paints its THUMB in the accent token, not in the selected plane's text colour: got "
                + (onColors.Count > SwitchKnobSolidIndex ? Describe((Color)onColors[SwitchKnobSolidIndex]) : "(none)")
                + ", accent is " + Describe(UiTheme.Vanilla.AccentGold));
        }

        flagValue = false;
        using (UiHost off = Host(BoolAutoPage(""), CheckboxBindings()))
        {
            ClearDraws();
            off.DrawFrame(new Rect(0f, 0f, 200f, 60f));
            IList offColors = RecordedColors();
            Check(offColors.Count > SwitchKnobSolidIndex
                    && SameColor((Color)offColors[SwitchKnobSolidIndex], UiTheme.Vanilla.TextPrimary),
                "while an OFF switch paints its thumb in the neutral ink");
            bool accent = false;
            foreach (object color in offColors)
            {
                accent |= SameColor((Color)color, UiTheme.Vanilla.AccentGold);
            }

            Check(!accent, "and an OFF switch paints no accent at all");
        }
    }

    private static string BoolAutoPage(string extra)
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"col\">"
            + "<Widget Id=\"flag\" Kind=\"input/checkbox\" Bind=\"flag\" Width=\"Auto\"" + extra + " />"
            + "</Column>"
            + "</UiPage>";
    }

    /// <summary>The same widget outside a column, for the creation-time refusals.</summary>
    private static string BoolPage(string extra)
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"flag\" Kind=\"input/checkbox\" Bind=\"flag\"" + extra + " />"
            + "</UiPage>";
    }

    // --- checkbox -------------------------------------------------------------------------------

    private static void VerifyCheckboxToggles()
    {
        flagValue = true;
        using UiHost host = Host(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"flag\" Kind=\"input/checkbox\" Bind=\"flag\" Label=\"Flag\" Appearance=\"checkbox\" />"
            + "</UiPage>",
            CheckboxBindings());

        UiLayoutSnapshot snapshot = Arrange(host);
        Check(flagValue, "the binding starts true");
        ClickAt(host, Centre(snapshot.RectById["flag"]));
        Check(!flagValue, "one click writes the inverse of the value it read");
        ClickAt(host, Centre(snapshot.RectById["flag"]));
        Check(flagValue, "and the next click writes it back");
    }

    private static void VerifyCheckboxHitRule()
    {
        flagValue = false;
        using UiHost host = Host(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"flag\" Kind=\"input/checkbox\" Bind=\"flag\" Label=\"Flag\" Width=\"60\" Appearance=\"checkbox\" />"
            + "</UiPage>",
            CheckboxBindings());

        UiLayoutSnapshot snapshot = Arrange(host);
        Rect band = snapshot.RectById["flag"];
        ClickAt(host, new Vector2(band.xMax + 20f, band.y + band.height * 0.5f));
        Check(!flagValue, "a click outside the element's arranged band writes nothing");
        ClickAt(host, new Vector2(band.x + 2f, band.y + 2f));
        Check(flagValue, "and a click inside it does");
    }

    private static void VerifyReadOnlyCheckbox()
    {
        var bindings = new UiBindings();
        bindings.BindReadOnly<bool>("flag", () => false);
        using UiHost host = Host(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"flag\" Kind=\"input/checkbox\" Bind=\"flag\" Label=\"Flag\" Appearance=\"checkbox\" />"
            + "</UiPage>",
            bindings);

        UiLayoutSnapshot snapshot = Arrange(host);
        UiNode node = host.Session.GetNodeByElementId("flag") ?? throw new Exception("the checkbox was not arranged");
        Check(!node.IsDisabled, "a read-only value binding is not a dead command element");

        ClearDraws();
        host.DrawFrame(new Rect(0f, 0f, 200f, 60f));
        Check(RecordedColors().Count >= 5, "the checkbox painted its box");
        UiTheme theme = UiTheme.Vanilla;
        Check(
            SameColor((Color)RecordedColors()[0], theme.Base) && SameColor((Color)RecordedColors()[1], theme.Divider),
            "and the plane is the one disabled treatment the resolved-value table owns");

        // Capture is observed on the MouseDown itself: after a full click the hot control is released again
        // whatever happened in between, so a post-click read cannot tell a refusal from a completed capture.
        GUIUtility.hotControl = 0;
        Pump(host, EventType.MouseDown, Centre(snapshot.RectById["flag"]));
        Check(GUIUtility.hotControl == 0, "a value the model publishes read-only captures no pointer at all");
        GUIUtility.hotControl = 0;
        Pump(host, EventType.MouseUp, Centre(snapshot.RectById["flag"]));
        Check(!host.Session.IsTripped(node), "and the click never reaches a write on a read-only binding");
        GUIUtility.hotControl = 0;
    }

    private static void VerifyGatedCheckbox()
    {
        flagValue = false;
        commandFired = 0;
        commandAllowed = false;

        var bindings = new UiBindings();
        bindings.BindValue<bool>("flag", () => flagValue, value => flagValue = value);
        bindings.BindCommand("gated", () => commandFired++, () => commandAllowed);

        using UiHost host = Host(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"flag\" Kind=\"input/checkbox\" Bind=\"flag\" ActionBind=\"gated\" Label=\"Flag\" Appearance=\"checkbox\" />"
            + "</UiPage>",
            bindings);

        UiLayoutSnapshot snapshot = Arrange(host);
        UiNode node = host.Session.GetNodeByElementId("flag") ?? throw new Exception("the checkbox was not arranged");
        Check(node.IsDisabled, "a false CanExecute publishes the element disabled");

        GUIUtility.hotControl = 0;
        Pump(host, EventType.MouseDown, Centre(snapshot.RectById["flag"]));
        Check(GUIUtility.hotControl == 0, "a gated checkbox does not capture the hot control");
        GUIUtility.hotControl = 0;
        Pump(host, EventType.MouseUp, Centre(snapshot.RectById["flag"]));
        Check(!flagValue, "and its click writes no value");
        Check(commandFired == 0, "and its command never runs");
        GUIUtility.hotControl = 0;

        commandAllowed = true;
        ClickAt(host, Centre(snapshot.RectById["flag"]));
        Check(flagValue, "re-enabling it lets the click write again");
        Check(commandFired == 1, "and the toggle fires the command it declares exactly once");
    }

    private static void VerifyCheckboxCreationRefusals()
    {
        const string page =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Widget Id=\"flag\" Kind=\"input/checkbox\" {0}/></UiPage>";

        var unbound = new UiBindings();
        Check(CreationFailure(string.Format(page, "Bind=\"nobody\""), unbound) != null,
            "a checkbox whose Bind names nothing is refused at creation");

        var mistyped = new UiBindings();
        mistyped.BindReadOnly<string>("flag", () => "text");
        Check(CreationFailure(string.Format(page, "Bind=\"flag\""), mistyped) != null,
            "and one whose Bind is not a bool is refused too");

        var noKey = new UiBindings();
        Check(CreationFailure(string.Format(page, ""), noKey) != null,
            "and one with neither Bind nor Id to fall back to is refused");

        var actionInstead = new UiBindings();
        actionInstead.BindValue<bool>("flag", () => false, value => { });
        actionInstead.BindAction<string>("gated", value => { });
        Check(CreationFailure(string.Format(page, "Bind=\"flag\" ActionBind=\"gated\""), actionInstead) != null,
            "and an ActionBind naming an action rather than a command is refused");
    }

    // --- progress --------------------------------------------------------------------------------

    private static void VerifyProgressValueContract()
    {
        using UiHost host = Host(ProgressPage(), ProgressBindings(out _));

        UiTheme theme = UiTheme.Vanilla;
        float edge = Math.Max(1f, theme.Geometry.Hairline);
        Rect band = Arrange(host).RectById["bar"];
        float inner = band.width - edge * 2f;

        pctValue = 1f;
        Check(Near(FillWidth(host), inner * 0.5f), "a value of 1 against Max=2 fills half the track: " + FillWidth(host));

        pctValue = 5f;
        Check(Near(FillWidth(host), inner), "a value above Max clamps to a full track");

        pctValue = -3f;
        Check(Near(FillWidth(host), 0f), "and a value below zero paints an empty one");
    }

    private static void VerifyProgressIsPaintClass()
    {
        using UiHost host = Host(ProgressPage(), ProgressBindings(out IUiBindings bindings));

        pctValue = 0.5f;
        UiLayoutSnapshot before = host.MeasureAndArrange(new Vector2(200f, 120f));
        float beforeWidth = FillWidth(host);

        pctValue = 1.5f;
        bindings.NotifyChanged("pct");
        UiLayoutSnapshot after = host.MeasureAndArrange(new Vector2(200f, 120f));

        Check(ReferenceEquals(before, after),
            "a Paint-class announcement reuses the arranged snapshot instead of re-measuring the page");
        Check(FillWidth(host) > beforeWidth, "while the next paint reads the new value");
    }

    private static void VerifyProgressTakesNoPointer()
    {
        using UiHost host = Host(ProgressPage(), ProgressBindings(out _));
        UiLayoutSnapshot snapshot = Arrange(host);

        GUIUtility.hotControl = 0;
        ClickAt(host, Centre(snapshot.RectById["bar"]));
        Check(GUIUtility.hotControl == 0, "a progress readout never takes the pointer, so nothing beneath it loses a click");
    }

    private static void VerifyProgressCreationRefusals()
    {
        var bindings = new UiBindings();
        bindings.BindReadOnly<float>("pct", () => 0f);
        const string page =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Widget Id=\"bar\" Kind=\"display/progress\" {0}/></UiPage>";

        Check(CreationFailure(string.Format(page, "Bind=\"nobody\""), bindings) != null,
            "a progress bar with no read is refused at creation");
        Check(CreationFailure(string.Format(page, "Bind=\"pct\" Max=\"zero\""), bindings) != null,
            "and a malformed Max is refused rather than degraded per frame");
        Check(CreationFailure(string.Format(page, "Bind=\"pct\" Max=\"0\""), bindings) != null,
            "and a non-positive Max with it");
        Check(CreationFailure(string.Format(page, "Bind=\"pct\" Max=\"4\""), bindings) == null,
            "while a positive Max is accepted");
    }

    // --- tree ------------------------------------------------------------------------------------

    private static void VerifyTreeGeometry()
    {
        treeRows = new List<UiTreeRow>
        {
            new("root", 0, "root"),
            new("child", 1, "child"),
            new("grandchild", 2, "grandchild"),
        };

        using UiHost host = Host(TreePage(), TreeBindings());
        ClearDraws();
        host.DrawFrame(new Rect(0f, 0f, 200f, 120f));
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(200f, 120f));

        IList rects = (IList)Field(typeof(Verse.Widgets), "LabelRects", null);
        IList texts = (IList)Field(typeof(Verse.Widgets), "LabelTexts", null);
        Check(rects.Count == 3, "one label per row: " + rects.Count);

        var first = (Rect)rects[0]!;
        var second = (Rect)rects[1]!;
        var third = (Rect)rects[2]!;
        Check(Near(first.height, 20f) && Near(second.height, 20f) && Near(third.height, 20f),
            "every band is the declared RowHeight tall");
        Check(second.x > first.x && third.x > second.x,
            "and each level indents one step further than its parent");
        Check(string.Equals((string)texts[0]!, "root", StringComparison.Ordinal)
            && string.Equals((string)texts[2]!, "grandchild", StringComparison.Ordinal),
            "rows draw in the order the model supplied");

        Rect tree = snapshot.RectById["tree"];
        pickedKey = "";
        ClickAt(host, new Vector2(tree.x + 4f, tree.y + 20f + 10f));
        Check(string.Equals(pickedKey, "child", StringComparison.Ordinal),
            "a click inside the second band reports the second row's key, so the bands are where the rows are: " + pickedKey);
    }

    private static void VerifyTreeHitFollowsKey()
    {
        treeRows = new List<UiTreeRow>
        {
            new("alpha", 0, "alpha"),
            new("beta", 0, "beta"),
        };

        using UiHost host = Host(TreePage(), TreeBindings());
        UiLayoutSnapshot snapshot = ArrangeTree(host);
        Rect tree = snapshot.RectById["tree"];

        pickedKey = "";
        ClickAt(host, new Vector2(tree.x + 4f, tree.y + 10f));
        Check(string.Equals(pickedKey, "alpha", StringComparison.Ordinal), "the first band reports the first key");

        treeRows.Reverse();
        host.Bindings.NotifyChanged("tree");
        snapshot = ArrangeTree(host);
        tree = snapshot.RectById["tree"];
        pickedKey = "";
        ClickAt(host, new Vector2(tree.x + 4f, tree.y + 10f));
        Check(string.Equals(pickedKey, "beta", StringComparison.Ordinal),
            "after a reorder the same band reports the row that now owns it, never a stashed index");
    }

    private static void VerifyTreeExpansionFollowsKey()
    {
        treeRows = new List<UiTreeRow>
        {
            new("open", 0, "open", expandable: true, expanded: true),
            new("shut", 0, "shut", expandable: true, expanded: false),
        };

        using UiHost host = Host(TreePage(), TreeBindings());
        Check(BandSolids(host, 0) > BandSolids(host, 1),
            "the expanded row paints a filled marker and the collapsed one an outline");

        treeRows.Reverse();
        host.Bindings.NotifyChanged("tree");
        Check(BandSolids(host, 1) > BandSolids(host, 0),
            "after the reorder the filled marker followed its row's key, not the band index");
    }

    private static void VerifyTreeRefusals()
    {
        UiFitAudit.Reset();
        treeRows = new List<UiTreeRow>
        {
            new("keep", 0, "keep"),
            new("dup", 1, "dup"),
            new("dup", 1, "dup"),
            new("", 1, ""),
            new("negative", -1, "negative"),
            new("tail", 2, "tail"),
        };

        using UiHost host = Host(TreePage(), TreeBindings());
        ClearDraws();
        host.DrawFrame(new Rect(0f, 0f, 200f, 120f));
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(200f, 120f));

        IList rects = (IList)Field(typeof(Verse.Widgets), "LabelRects", null);
        Check(rects.Count == 3, "only the rows that can carry identity are drawn: " + rects.Count);
        Check(UiFitAudit.StyleFallbackCount >= 3,
            "and each refusal is reported once through the bounded channel: " + UiFitAudit.StyleFallbackCount);

        Rect tree = snapshot.RectById["tree"];
        pickedKey = "";
        ClickAt(host, new Vector2(tree.x + 4f, tree.y + 20f + 10f));
        Check(string.Equals(pickedKey, "dup", StringComparison.Ordinal),
            "the accepted row of the duplicated key is hit by its own key: " + pickedKey);
        pickedKey = "";
        ClickAt(host, new Vector2(tree.x + 4f, tree.y + 40f + 10f));
        Check(string.Equals(pickedKey, "tail", StringComparison.Ordinal),
            "and the row after the refused ones kept its band, not their slots: " + pickedKey);
    }

    private static void VerifyTreeCreationRefusals()
    {
        var bindings = new UiBindings();
        bindings.BindReadOnly<IReadOnlyList<UiTreeRow>>("tree", () => treeRows);
        const string page =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Widget Id=\"tree\" Kind=\"container/tree\" {0}/></UiPage>";

        Check(CreationFailure(string.Format(page, "Bind=\"nobody\""), bindings) != null,
            "a tree with no row source is refused at creation");
        Check(CreationFailure(string.Format(page, "Bind=\"tree\" ActionBind=\"nobody\""), bindings) != null,
            "and an ActionBind nothing bound is refused too");
        Check(CreationFailure(string.Format(page, "Bind=\"tree\" RowHeight=\"0\""), bindings) != null,
            "and a non-positive RowHeight is refused rather than degraded");
        Check(CreationFailure(string.Format(page, "Bind=\"tree\" RowHeight=\"18\""), bindings) == null,
            "while a declared RowHeight is accepted");
    }

    // --- pages and bindings ----------------------------------------------------------------------

    private static string ProgressPage()
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"bar\" Kind=\"display/progress\" Bind=\"pct\" Max=\"2\" Height=\"10\" />"
            + "</UiPage>";
    }

    private static string TreePage()
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"tree\" Kind=\"container/tree\" Bind=\"tree\" ActionBind=\"pick\" RowHeight=\"20\" Width=\"100\" />"
            + "</UiPage>";
    }

    private static UiBindings CheckboxBindings()
    {
        var bindings = new UiBindings();
        bindings.BindValue<bool>("flag", () => flagValue, value => flagValue = value);
        return bindings;
    }

    private static UiBindings ProgressBindings(out IUiBindings asInterface)
    {
        var bindings = new UiBindings();
        bindings.BindReadOnly<float>("pct", () => pctValue, UiInvalidation.Paint);
        asInterface = bindings;
        return bindings;
    }

    private static UiBindings TreeBindings()
    {
        var bindings = new UiBindings();
        bindings.BindReadOnly<IReadOnlyList<UiTreeRow>>("tree", () => treeRows, UiInvalidation.Structure);
        bindings.BindAction<string>("pick", key => pickedKey = key);
        return bindings;
    }

    private static UiHost Host(string xml, UiBindings bindings)
    {
        return Host(xml, bindings, UiTheme.Vanilla);
    }

    private static UiHost Host(string xml, UiBindings bindings, UiTheme theme)
    {
        return new UiHost(
            Scope, UiLayoutManifest.Parse(xml), bindings, theme, new StubMetrics(), new StubTranslation());
    }

    /// <summary>Builds a host only to see whether creation refuses it; a null answer means it was accepted.</summary>
    private static string? CreationFailure(string xml)
    {
        try
        {
            using var host = new UiHost(
                Scope, UiLayoutManifest.Parse(xml), CheckboxBindings(), UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
            return null;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    private static string? CreationFailure(string xml, UiBindings bindings)
    {
        try
        {
            using var host = new UiHost(
                Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
            return null;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    // --- helpers --------------------------------------------------------------------------------

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        host.DrawFrame(new Rect(0f, 0f, 200f, 120f));
        return host.MeasureAndArrange(new Vector2(200f, 120f));
    }

    private static UiLayoutSnapshot ArrangeTree(UiHost host)
    {
        return Arrange(host);
    }

    private static void ClickAt(UiHost host, Vector2 point)
    {
        Pump(host, EventType.MouseDown, point);
        Pump(host, EventType.MouseUp, point);
    }

    private static void Pump(UiHost host, EventType type, Vector2 point)
    {
        Event e = Event.KeyboardEvent("space");
        e.type = type;
        e.button = 0;
        e.mousePosition = point;
        Event.current = e;
        host.DrawFrame(new Rect(0f, 0f, 200f, 120f));
        Event.current = null;
    }

    private static Vector2 Centre(Rect rect)
    {
        return new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
    }

    private static float FillWidth(UiHost host)
    {
        ClearDraws();
        host.DrawFrame(new Rect(0f, 0f, 200f, 120f));
        IList rects = (IList)Field(typeof(Verse.Widgets), "DrawBoxSolidRects", null);
        return rects.Count >= 6 ? ((Rect)rects[5]!).width : 0f;
    }

    /// <summary>Draws once into a cleared recording and counts the solid calls inside one 20px band.</summary>
    private static int BandSolids(UiHost host, int bandIndex)
    {
        ClearDraws();
        host.DrawFrame(new Rect(0f, 0f, 200f, 120f));
        IList rects = (IList)Field(typeof(Verse.Widgets), "DrawBoxSolidRects", null);
        float top = bandIndex * 20f;
        int count = 0;
        foreach (object entry in rects)
        {
            var rect = (Rect)entry;
            if (rect.y >= top - 0.5f && rect.y <= top + 20f + 0.5f) count++;
        }

        return count;
    }

    /// <summary>
    /// Clears every recording the paint outlets keep, not only the solid boxes: the label lists accumulate
    /// across frames too, and a per-frame count is the only way "one label per row" is an assertion about
    /// this pass instead of about the lane's history.
    /// </summary>
    private static void ClearDraws()
    {
        Invoke(typeof(Verse.Widgets), "ClearDrawBoxSolidCalls");
        ((IList)Field(typeof(Verse.Widgets), "LabelRects", null)).Clear();
        ((IList)Field(typeof(Verse.Widgets), "LabelTexts", null)).Clear();
        ((IList)Field(typeof(Verse.Widgets), "LabelColors", null)).Clear();
    }

    private static IList RecordedColors()
    {
        return (IList)Field(typeof(Verse.Widgets), "DrawBoxSolidColors", null);
    }

    private static object Field(Type type, string name, object? instance)
    {
        FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
        if (field == null) throw new Exception(type.Name + " has no field '" + name + "'");
        return field.GetValue(instance)!;
    }

    private static void Invoke(Type type, string name)
    {
        MethodInfo? method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public);
        if (method == null) throw new Exception(type.Name + " has no method '" + name + "'");
        method.Invoke(null, null);
    }

    private static bool SameColor(Color left, Color right)
    {
        return Near(left.r, right.r) && Near(left.g, right.g) && Near(left.b, right.b) && Near(left.a, right.a);
    }

    private static string Describe(Color color)
    {
        return "(" + color.r + ", " + color.g + ", " + color.b + ", " + color.a + ")";
    }

    private static bool Near(float left, float right)
    {
        return Math.Abs(left - right) <= 0.01f;
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

    private static void Run(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
        }

        public float MeasureWidth(string text, UiFont font)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
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
