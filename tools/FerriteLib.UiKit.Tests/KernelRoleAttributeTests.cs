using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Role-attribute lane (0.4.x style work): <c>Tone</c> and <c>Emphasis</c> as manifest attributes on the
/// leaf atoms, resolved through the theme's one table. It holds both ends of the failure ladder — a value
/// outside the vocabulary falls back to the default row and is recorded on the fitting audit's
/// appearance half, while a misspelled attribute <i>name</i> is still refused at Host creation — plus
/// that roles move appearance and never layout, that the data side's writability beats the author, and
/// that the atoms follow the theme's density font instead of pinning one of their own.
/// </summary>
internal static class KernelRoleAttributeTests
{
    private const string Scope = "role-attr-test";
    private const string LongCjk = "这是一段用于验证换行高度的文本内容"; // 17 ideographs

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Role attributes are declared on the atoms", VerifyRoleVocabulary);
        Run("Unknown role values fall back and are recorded", VerifyUnknownRoleFallsBack);
        Run("Roles change appearance without moving layout", VerifyAppearanceNotLayout);
        Run("Writability beats the authored role", VerifyWritabilityBeatsAuthor);
        Run("Read-only value atoms neither write nor throw", VerifyReadOnlyAtomsDoNotWrite);
        Run("Density font and geometry reach the atoms", VerifyDensityReachesAtoms);
        ResetSeams();
        UiFitAudit.Detach();
        UiFitAudit.Reset();
        return failures;
    }

    // --- the vocabulary -------------------------------------------------------------------------

    private static void VerifyRoleVocabulary()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        foreach (string kind in new[] { WrappedTextWidget.Kind, ButtonWidget.Kind, RuleWidget.Kind, SliderWidget.Kind, NumberFieldWidget.Kind })
        {
            Check(HasAttribute(kind, "Tone"), kind + " accepts Tone");
        }

        foreach (string kind in new[] { WrappedTextWidget.Kind, ButtonWidget.Kind, SliderWidget.Kind, NumberFieldWidget.Kind })
        {
            Check(HasAttribute(kind, "Emphasis"), kind + " accepts Emphasis");
        }

        Check(!HasAttribute(RuleWidget.Kind, "Emphasis"),
            "chrome/rule refuses Emphasis: it paints no text, so the attribute could not move a pixel");

        // The ladder: an unknown VALUE renders with the default row, an unknown NAME is still fatal.
        Reject(Page("<Widget Id=\"b\" Kind=\"input/button\" ActionBind=\"go\" Text=\"x\" Ton=\"Danger\" />"),
            "a misspelled role attribute name is refused at creation");
        Reject(Page("<Widget Id=\"r\" Kind=\"chrome/rule\" Emphasis=\"Muted\" />"),
            "Emphasis on a kind whose schema never declared it is refused");
        Reject(Page("<Stack Id=\"s\" Tone=\"Danger\"><Widget Id=\"n\" Kind=\"text/wrapped\" Text=\"x\" /></Stack>"),
            "Tone on a container is refused: roles do not inherit and no carrier exists yet");

        // A role is not a label: Width=Auto still measures the declared label set.
        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\">"
            + "<Widget Id=\"cap\" Kind=\"text/wrapped\" Width=\"Auto\" Tone=\"Danger\" Emphasis=\"Muted\" Text=\"音量\" Height=\"20\" />"
            + "<Widget Id=\"rest\" Kind=\"chrome/rule\" Height=\"4\" />"
            + "</Row>"
            + "</UiPage>";
        using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), new UiBindings(), UiTheme.DarkGold, new WrappingMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(400f, 60f));
        CheckClose(32f, snapshot.RectById["cap"].width,
            "a role does not join the label set: Width=Auto still measures Text/TextKey (2 ideographs x 16)");
    }

    private static bool HasAttribute(string kind, string name)
    {
        IReadOnlyCollection<string>? schema = UiWidgetRegistry.GetAttributeSchema(UiWidgetRegistry.CoreScope, kind);
        if (schema == null) return false;
        foreach (string candidate in schema)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    // --- fallback, both ends --------------------------------------------------------------------

    private static void VerifyUnknownRoleFallsBack()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        var records = new List<UiStyleFallbackReport>();
        UiFitAudit.AttachStyleFallback(records.Add);
        try
        {
            UiFitAudit.Reset();
            using UiSession session = new();
            UiTheme theme = UiTheme.DarkGold;

            int fired = 0;
            var bindings = new UiBindings();
            bindings.BindCommand("go", () => fired++);

            // An unknown tone: creation succeeds (appearance fails soft) and the default row is painted.
            ButtonWidget button = new();
            button.Configure(new UiElementSpec("apply", ButtonWidget.Kind,
                Attributes(("ActionBind", "go"), ("Text", "Apply"), ("Tone", "purplish"))));
            UiWidgetContext ctx = Context(session, theme, bindings, 120f);
            ClearBoxes();
            button.Draw(new Rect(0f, 0f, 120f, 28f), ctx);
            CheckPainted(theme.Raised, theme.Border, "a tone outside the vocabulary paints the default row");
            Check(fired == 0, "painting is not activating: the seam was never armed");

            Check(records.Count == 1, "the unknown tone was reported through the fitting audit's appearance half");
            if (records.Count == 1)
            {
                Check(records[0].Kind == ButtonWidget.Kind && records[0].Attribute == "Tone"
                    && records[0].Authored == "purplish" && records[0].Resolved == "Neutral",
                    "the record names the kind, the attribute, the authored text and the fallback");
                Check(records[0].Diagnostic.IndexOf("purplish", StringComparison.Ordinal) >= 0,
                    "the diagnostic is readable: " + records[0].Diagnostic);
            }

            Check(UiFitAudit.StyleFallbackCount == 1, "the appearance count is live");
            Check(!UiFitAudit.Enabled, "the text half stays off: the appearance record does not depend on it");
            Check(UiFitAudit.LastStyleFallbackDiagnostic != null
                && UiFitAudit.LastStyleFallbackDiagnostic.IndexOf("purplish", StringComparison.Ordinal) >= 0,
                "the last diagnostic names the authored value");

            ClearBoxes();
            button.Draw(new Rect(0f, 0f, 120f, 28f), ctx);
            Check(records.Count == 1 && UiFitAudit.StyleFallbackCount == 1,
                "a second pass reports the same finding once, not per frame");

            // A declared tone is a rule, not a fallback.
            ButtonWidget toned = new();
            toned.Configure(new UiElementSpec("apply", ButtonWidget.Kind,
                Attributes(("ActionBind", "go"), ("Text", "Apply"), ("Tone", "Danger"))));
            ClearBoxes();
            toned.Draw(new Rect(0f, 0f, 120f, 28f), ctx);
            CheckPainted(theme.Danger, theme.Danger, "a declared tone paints that row");
            Check(records.Count == 1, "a declared tone records nothing: it is a rule, not a fallback");

            // An unknown emphasis on a text leaf: fallback to Normal, and the drawn colour proves it.
            WrappedTextWidget leaf = new();
            leaf.Configure(new UiElementSpec("note", WrappedTextWidget.Kind,
                Attributes(("Text", "hello"), ("Emphasis", "shouty"))));
            int before = LabelCount();
            leaf.Draw(new Rect(0f, 0f, 200f, 40f), Context(session, theme, new UiBindings(), 200f));
            Check(SameColor(LastLabelColor(before), theme.TextPrimary),
                "an emphasis outside the vocabulary paints the Normal text colour");
            Check(records.Count == 2 && records[1].Attribute == "Emphasis" && records[1].Resolved == "Normal",
                "the unknown emphasis is recorded too, with the value it fell back to");

            // A declared emphasis is the second axis working: secondary text, no record.
            WrappedTextWidget muted = new();
            muted.Configure(new UiElementSpec("note", WrappedTextWidget.Kind,
                Attributes(("Text", "hello"), ("Emphasis", "Muted"))));
            before = LabelCount();
            muted.Draw(new Rect(0f, 0f, 200f, 40f), Context(session, theme, new UiBindings(), 200f));
            Check(SameColor(LastLabelColor(before), theme.TextSecondary), "Emphasis=Muted paints the secondary text");
            Check(records.Count == 2, "a declared emphasis records nothing");

            // The engine path: the record carries the element path the tree arranged.
            UiFitAudit.Reset();
            string xml =
                "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
                + "<Widget Id=\"probe\" Kind=\"input/button\" ActionBind=\"go\" Text=\"x\" Tone=\"chartreuse\" Height=\"28\" />"
                + "</UiPage>";
            using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), bindings, theme, new WrappingMetrics(), new StubTranslation());
            host.DrawFrame(new Rect(0f, 0f, 200f, 60f));
            Check(records.Count == 3, "the tree's own pass recorded the fallback as well");
            if (records.Count == 3)
            {
                Check(records[2].ElementPath.IndexOf("probe", StringComparison.Ordinal) >= 0,
                    "the record names the element path an author can search for (got '" + records[2].ElementPath + "')");
            }

            // Lifecycle: the appearance half is host/session state with the same life as the text half —
            // bounded, forgottable, detachable — never a monotonic global.
            UiFitAudit.Reset();
            Check(UiFitAudit.StyleFallbackCount == 0 && UiFitAudit.LastStyleFallbackDiagnostic == null,
                "Reset forgets the appearance findings and the last diagnostic with them");
            int sinkCalls = records.Count;
            ClearBoxes();
            button.Draw(new Rect(0f, 0f, 120f, 28f), ctx);
            Check(UiFitAudit.StyleFallbackCount == 1, "a later pass records again, so the half stays live");
            Check(records.Count == sinkCalls + 1, "and the sink fires again after the reset");

            UiFitAudit.Detach();
            Check(UiFitAudit.LastStyleFallbackDiagnostic == null,
                "Detach drops the sink and the diagnostic a disposed host would otherwise leave armed");
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }
    }

    // --- appearance, never layout ---------------------------------------------------------------

    private static void VerifyAppearanceNotLayout()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();
        UiTheme theme = UiTheme.DarkGold;
        var bindings = new UiBindings();
        float value = 0.5f;
        bindings.BindValue("v", () => value, written => value = written);
        bindings.BindCommand("go", () => { });
        UiWidgetContext ctx = Context(session, theme, bindings, 240f);

        CheckClose(Measured(new WrappedTextWidget(), WrappedTextWidget.Kind, ctx, ("Text", LongCjk)),
            Measured(new WrappedTextWidget(), WrappedTextWidget.Kind, ctx, ("Text", LongCjk), ("Tone", "Danger"), ("Emphasis", "Muted")),
            "text/wrapped measures the same band whatever role it carries");
        CheckClose(Measured(new ButtonWidget(), ButtonWidget.Kind, ctx, ("ActionBind", "go"), ("Text", "ok")),
            Measured(new ButtonWidget(), ButtonWidget.Kind, ctx, ("ActionBind", "go"), ("Text", "ok"), ("Tone", "Success")),
            "input/button keeps its height whatever role it carries");
        CheckClose(Measured(new RuleWidget(), RuleWidget.Kind, ctx, ("Thickness", "2")),
            Measured(new RuleWidget(), RuleWidget.Kind, ctx, ("Thickness", "2"), ("Tone", "Warning")),
            "chrome/rule keeps its hairline whatever role it carries");
        CheckClose(Measured(new SliderWidget(), SliderWidget.Kind, ctx, ("Bind", "v")),
            Measured(new SliderWidget(), SliderWidget.Kind, ctx, ("Bind", "v"), ("Tone", "Danger")),
            "input/slider keeps its height whatever role it carries");
        CheckClose(Measured(new NumberFieldWidget(), NumberFieldWidget.Kind, ctx, ("Bind", "v")),
            Measured(new NumberFieldWidget(), NumberFieldWidget.Kind, ctx, ("Bind", "v"), ("Emphasis", "Muted")),
            "input/number-field keeps its height whatever role it carries");

        // And through a real tree: the same page with different roles arranges to the same rects.
        string Plain(string tone) =>
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Stack Id=\"root\" Gap=\"6\">"
            + "<Widget Id=\"note\" Kind=\"text/wrapped\" Text=\"音量\" />"
            + "<Widget Id=\"apply\" Kind=\"input/button\" ActionBind=\"go\" Text=\"ok\" Tone=\"" + tone + "\" />"
            + "<Widget Id=\"sep\" Kind=\"chrome/rule\" Thickness=\"2\" />"
            + "</Stack></UiPage>";

        UiLayoutSnapshot neutral = Arrange(Plain("Neutral"), bindings, theme);
        UiLayoutSnapshot danger = Arrange(Plain("Danger"), bindings, theme);
        Check(SameRect(neutral.RectById["note"], danger.RectById["note"])
            && SameRect(neutral.RectById["apply"], danger.RectById["apply"])
            && SameRect(neutral.RectById["sep"], danger.RectById["sep"]),
            "a role never moves a rect: tone is not layout-bearing");

        // Appearance, observed: the fill/edge via the box recorder and the text via the label recorder.
        ButtonWidget button = new();
        button.Configure(new UiElementSpec("apply", ButtonWidget.Kind, Attributes(("ActionBind", "go"), ("Text", "ok"), ("Tone", "Danger"))));
        ClearBoxes();
        int before = LabelCount();
        button.Draw(new Rect(0f, 0f, 120f, 28f), ctx);
        CheckPainted(theme.Danger, theme.Danger, "a Danger button fills and edges with the alarm row");
        Check(SameColor(LastLabelColor(before), theme.TextOnDanger), "and writes that row's text colour");

        ButtonWidget success = new();
        success.Configure(new UiElementSpec("apply", ButtonWidget.Kind, Attributes(("ActionBind", "go"), ("Text", "ok"), ("Tone", "Success"), ("Emphasis", "Muted"))));
        ClearBoxes();
        before = LabelCount();
        success.Draw(new Rect(0f, 0f, 120f, 28f), ctx);
        CheckPainted(theme.Success, theme.BorderStrong, "a Success button fills with the success row");
        Check(SameColor(LastLabelColor(before), theme.TextOnGold), "a saturated tone's text ignores emphasis, as the table says");

        WrappedTextWidget leaf = new();
        leaf.Configure(new UiElementSpec("note", WrappedTextWidget.Kind, Attributes(("Text", "hello"), ("Tone", "Danger"))));
        before = LabelCount();
        leaf.Draw(new Rect(0f, 0f, 200f, 40f), ctx);
        Check(SameColor(LastLabelColor(before), theme.TextOnDanger), "text/wrapped writes the role's text colour");

        // The rule's three coordinates must be one pixel: fail-soft renders what "no declaration" renders,
        // never a promotion to the neutral treatment the author did not ask for.
        RuleWidget rule = new();
        rule.Configure(new UiElementSpec("sep", RuleWidget.Kind, Attributes(("Thickness", "2"))));
        Color untold = Painted(rule, ctx);

        rule.Configure(new UiElementSpec("sep", RuleWidget.Kind, Attributes(("Thickness", "2"), ("Tone", "Neutral"))));
        Color explicitNeutral = Painted(rule, ctx);

        rule.Configure(new UiElementSpec("sep", RuleWidget.Kind, Attributes(("Thickness", "2"), ("Tone", "puce"))));
        Color fellBack = Painted(rule, ctx);

        Check(SameColor(untold, explicitNeutral) && SameColor(explicitNeutral, fellBack),
            "an unwritten tone, an explicit Neutral and an unknown value's fallback paint one pixel");
        Check(SameColor(untold, theme.Styles.Resolve(UiStatusTone.Neutral).Border),
            "and that pixel is the neutral treatment's edge");

        rule.Configure(new UiElementSpec("sep", RuleWidget.Kind, Attributes(("Thickness", "2"), ("Tone", "Danger"))));
        Color dangerRule = Painted(rule, ctx);
        Check(SameColor(dangerRule, theme.Danger), "a toned rule opts into that treatment's edge colour");
        Check(!SameColor(dangerRule, untold), "and a real tone is still visibly a declaration");

        SliderWidget slider = new();
        slider.Configure(new UiElementSpec("v", SliderWidget.Kind, Attributes(("Bind", "v"), ("Label", "Vol"), ("Tone", "Danger"))));
        before = LabelCount();
        slider.Draw(new Rect(0f, 0f, 200f, 28f), ctx);
        Check(SameColor(LastLabelColor(before), theme.TextOnDanger), "input/slider paints its label in the role's text colour");

        NumberFieldWidget field = new();
        field.Configure(new UiElementSpec("v", NumberFieldWidget.Kind, Attributes(("Bind", "v"), ("Label", "Vol"), ("Tone", "Danger"))));
        before = LabelCount();
        field.Draw(new Rect(0f, 0f, 200f, 28f), ctx);
        Check(SameColor(LastLabelColor(before), theme.TextOnDanger), "input/number-field paints its label in the role's text colour");
    }

    // --- state beats author ---------------------------------------------------------------------

    private static void VerifyWritabilityBeatsAuthor()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiFitAudit.Reset();
        using UiSession session = new();
        UiTheme theme = UiTheme.DarkGold;
        float value = 0.5f;
        var bindings = new UiBindings();
        bindings.BindValue("editable", () => value, written => value = written);
        bindings.BindReadOnly("published", () => value);
        bindings.BindReadOnly<string>("publishedText", () => "hello");
        bindings.BindCommand("go", () => { });

        SliderWidget readOnly = new();
        readOnly.Configure(new UiElementSpec("published", SliderWidget.Kind,
            Attributes(("Bind", "published"), ("Min", "0"), ("Max", "1"), ("Label", "Vol"), ("Tone", "Danger"))));
        int before = LabelCount();
        readOnly.Draw(new Rect(0f, 0f, 200f, 28f), Context(session, theme, bindings, 200f));
        Check(SameColor(LastLabelColor(before), theme.TextDisabled),
            "a read-only slider paints disabled whatever tone the author wrote (state beats author)");

        SliderWidget writable = new();
        writable.Configure(new UiElementSpec("editable", SliderWidget.Kind,
            Attributes(("Bind", "editable"), ("Min", "0"), ("Max", "1"), ("Label", "Vol"), ("Tone", "Danger"))));
        before = LabelCount();
        writable.Draw(new Rect(0f, 0f, 200f, 28f), Context(session, theme, bindings, 200f));
        Check(SameColor(LastLabelColor(before), theme.TextOnDanger), "a writable slider keeps the authored tone");

        WrappedTextWidget bound = new();
        bound.Configure(new UiElementSpec("note", WrappedTextWidget.Kind,
            Attributes(("Bind", "publishedText"), ("Text", "x"), ("Tone", "Danger"))));
        before = LabelCount();
        bound.Draw(new Rect(0f, 0f, 200f, 40f), Context(session, theme, bindings, 200f));
        Check(SameColor(LastLabelColor(before), theme.TextDisabled),
            "a text leaf reading a read-only binding is disabled too");

        WrappedTextWidget literal = new();
        literal.Configure(new UiElementSpec("note", WrappedTextWidget.Kind, Attributes(("Text", "x"), ("Tone", "Danger"))));
        before = LabelCount();
        literal.Draw(new Rect(0f, 0f, 200f, 40f), Context(session, theme, bindings, 200f));
        Check(SameColor(LastLabelColor(before), theme.TextOnDanger),
            "an unbound leaf has no data side to ask, so the role decides");

        // A command binding is not a value binding: asking would answer "not writable" and paint every
        // bound button disabled, so this atom asks nothing.
        ButtonWidget button = new();
        button.Configure(new UiElementSpec("apply", ButtonWidget.Kind,
            Attributes(("ActionBind", "go"), ("Text", "ok"), ("Tone", "Danger"))));
        ClearBoxes();
        button.Draw(new Rect(0f, 0f, 120f, 28f), Context(session, theme, bindings, 120f));
        CheckPainted(theme.Danger, theme.Danger, "a command-bound button keeps its authored tone (no writability question)");

        Check(UiFitAudit.StyleFallbackCount == 0, "state beating author is a rule, not a fallback");
    }

    // --- density --------------------------------------------------------------------------------

    private static void VerifyDensityReachesAtoms()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();

        // 1) The density font: the band the atom reserves and the font the outlet receives move together.
        UiTheme theme = UiTheme.DarkGold;
        WrappedTextWidget leaf = new();
        leaf.Configure(new UiElementSpec("note", WrappedTextWidget.Kind, Attributes(("Text", LongCjk))));
        CheckClose(60f, leaf.Measure(Context(session, theme, new UiBindings(), 120f)),
            "at the shipped density: 17 ideographs wrap into 3 lines of 16 plus 2 x 6 padding");

        theme.DefaultFont = UiFont.Medium;
        CheckClose(66f, leaf.Measure(Context(session, theme, new UiBindings(), 120f)),
            "a density font change moves the atom's measured band (3 lines of 18 plus 12)");

        var reports = new List<UiOverflowReport>();
        // The text half is a bounded, cumulative collector; a lane that filled its budget leaves it
        // saturated, so this one starts it clean rather than inheriting another lane's findings.
        UiFitAudit.Reset();
        UiFitAudit.Attach(new WrappingMetrics(), reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            // A band shorter than the wrapped need makes the audit speak; the atom insets its own
            // padding first, so the probe rect has to leave a band taller than one pixel for the audit
            // to look at it at all.
            leaf.Draw(new Rect(0f, 0f, 120f, 20f), Context(session, theme, new UiBindings(), 120f));
            Check(reports.Count == 1 && reports[0].Font == UiFont.Medium,
                "the font the outlet was handed is the density font the band was measured with (reports: "
                + reports.Count + ")");

            // The pinned boundary, written down rather than discovered: the two composites keep the type
            // size they are named for, so a density font change deliberately does not move their bands.
            reports.Clear();
            UiFitAudit.Reset();
            ChromeBannerWidget banner = new();
            banner.Configure(new UiElementSpec("b", ChromeBannerWidget.Kind, Attributes(("Text", LongCjk))));
            banner.Draw(new Rect(0f, 0f, 20f, 20f), Context(session, theme, new UiBindings(), 20f));
            Check(reports.Count == 1 && reports[0].Font == UiFont.Tiny,
                "chrome/banner keeps its own type size under a density font change (pinned band, documented)");
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }

        // 2) The density geometry: every band the atoms used to spell by hand now reads the bundle.
        UiTheme dense = UiTheme.DarkGold;
        dense.Geometry = new UiGeometry(10f, 8f, 12f, 40f, 3f);
        var bindings = new UiBindings();
        float value = 0.5f;
        bindings.BindValue("v", () => value, written => value = written);
        bindings.BindCommand("go", () => { });
        UiWidgetContext ctx = Context(session, dense, bindings, 300f);

        CheckClose(40f, Measured(new ButtonWidget(), ButtonWidget.Kind, ctx, ("ActionBind", "go"), ("Text", "ok")),
            "the button's default height is the density row height");
        CheckClose(3f, Measured(new RuleWidget(), RuleWidget.Kind, ctx),
            "the rule's natural thickness is the density hairline");
        CheckClose(68f, Measured(new WrappedTextWidget(), WrappedTextWidget.Kind,
                Context(session, dense, bindings, 120f), ("Text", LongCjk)),
            "the text atom's band carries the density padding (3 lines of 16 plus 2 x 10 at a 120px column)");

        Rect track = default;
        UiNative.SliderOverride = (rect, current, min, max) =>
        {
            track = rect;
            return current;
        };
        SliderWidget slider = new();
        slider.Configure(new UiElementSpec("v", SliderWidget.Kind, Attributes(("Bind", "v"), ("Label", "ab"))));
        slider.Draw(new Rect(0f, 0f, 300f, 28f), ctx);
        CheckClose(36f, track.x, "the slider's label band and gap both follow the density tokens (16 + 8 spacing + 12 gap)");
        UiNative.SliderOverride = null;

        CheckClose(36f, Measured(new WrappedTextWidget(), WrappedTextWidget.Kind, ctx, ("Text", "ab"), ("Tone", "Danger")),
            "and a role adds nothing to that band (1 line of 16 plus 2 x 10)");
    }

    // --- read-only value atoms: painted disabled and unable to act ---------------------------------

    /// <summary>
    /// "Looks disabled" and "cannot act" are one statement. A read-only binding's control still draws
    /// (a disabled look must not be a hole), but a drag or a committed edit must neither reach the
    /// binding nor throw into the tree's recovery band.
    /// <para>
    /// The value change is produced through the funnel's override seam because that is the only way the
    /// harness can produce one: the stub's slider echoes its input, so without the seam a lane would
    /// pass vacuously — it would never reach the write it exists to catch. The input state around it is
    /// the real one (pointer down and dragging over the control).
    /// </para>
    /// </summary>
    private static void VerifyReadOnlyAtomsDoNotWrite()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();
        UiTheme theme = UiTheme.DarkGold;

        float published = 0.5f;
        float editable = 0.5f;
        var bindings = new UiBindings();
        bindings.BindReadOnly("published", () => published);
        bindings.BindValue("editable", () => editable, written => editable = written);
        UiWidgetContext ctx = Context(session, theme, bindings, 200f);

        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMousePosition = new Vector2(80f, 14f);
            UiNative.DebugMouseDown = true;
            UiNative.DebugMouseDrag = true;
            UiNative.SliderOverride = (rect, current, min, max) => 0.9f;

            SliderWidget readOnly = new();
            readOnly.Configure(new UiElementSpec("published", SliderWidget.Kind,
                Attributes(("Bind", "published"), ("Min", "0"), ("Max", "1"), ("Tone", "Danger"))));
            Exception? thrown = null;
            try
            {
                readOnly.Draw(new Rect(0f, 0f, 200f, 28f), ctx);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            Check(thrown == null, "a read-only slider under a drag does not throw"
                + (thrown == null ? "" : " (threw " + thrown.GetType().Name + ")"));
            CheckClose(0.5f, published, "and the value the data side refuses is never written");

            // The positive control: the same drive on a writable binding commits, so the guard is not a
            // blanket skip that would pass by doing nothing.
            SliderWidget writable = new();
            writable.Configure(new UiElementSpec("editable", SliderWidget.Kind,
                Attributes(("Bind", "editable"), ("Min", "0"), ("Max", "1"))));
            writable.Draw(new Rect(0f, 0f, 200f, 28f), ctx);
            CheckClose(0.9f, editable, "the same drag on a writable binding commits");

            UiNative.TextFieldOverride = (rect, text) => "0.7";

            NumberFieldWidget readOnlyField = new();
            readOnlyField.Configure(new UiElementSpec("published", NumberFieldWidget.Kind,
                Attributes(("Bind", "published"), ("Min", "0"), ("Max", "10"))));
            thrown = null;
            try
            {
                readOnlyField.Draw(new Rect(0f, 0f, 120f, 28f), Context(session, theme, bindings, 120f));
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            Check(thrown == null, "a read-only number field does not throw"
                + (thrown == null ? "" : " (threw " + thrown.GetType().Name + ")"));
            CheckClose(0.5f, published, "and its committed edit never reaches the binding");

            NumberFieldWidget writableField = new();
            writableField.Configure(new UiElementSpec("editable", NumberFieldWidget.Kind,
                Attributes(("Bind", "editable"), ("Min", "0"), ("Max", "10"))));
            writableField.Draw(new Rect(0f, 0f, 120f, 28f), Context(session, theme, bindings, 120f));
            CheckClose(0.7f, editable, "the same edit on a writable binding commits");
        }
        finally
        {
            ResetSeams();
        }

        // The end-to-end half: the tree's own pass must not need the recovery band for this.
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"published\" Kind=\"input/slider\" Bind=\"published\" Min=\"0\" Max=\"1\" Tone=\"Danger\" />"
            + "</UiPage>";
        using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), bindings, theme, new WrappingMetrics(), new StubTranslation());
        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMousePosition = new Vector2(80f, 14f);
            UiNative.DebugMouseDown = true;
            UiNative.DebugMouseDrag = true;
            UiNative.SliderOverride = (rect, current, min, max) => 0.9f;
            host.DrawFrame(new Rect(0f, 0f, 200f, 60f));
        }
        finally
        {
            ResetSeams();
        }

        Check(host.Session.TrippedComponentIds.Count == 0,
            "the recovery band is never the fallback: the page's pass tripped nothing");
        CheckClose(0.5f, published, "and the page's pass wrote nothing either");
    }

    // --- harness --------------------------------------------------------------------------------

    private static UiLayoutSnapshot Arrange(string xml, IUiBindings bindings, UiTheme theme)
    {
        using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), bindings, theme, new WrappingMetrics(), new StubTranslation());
        return host.MeasureAndArrange(new Vector2(400f, 400f));
    }

    private static float Measured(IUiWidget widget, string kind, UiWidgetContext ctx, params (string Name, string Value)[] attributes)
    {
        widget.Configure(new UiElementSpec("probe", kind, Attributes(attributes)));
        return widget.Measure(ctx);
    }

    private static string Page(string body)
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>";
    }

    private static void Reject(string xml, string what)
    {
        try
        {
            using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), new UiBindings(), UiTheme.DarkGold, new WrappingMetrics(), new StubTranslation());
            Check(false, what + " - but the host accepted it");
        }
        catch (UiContractException)
        {
            Check(true, what);
        }
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

    private static UiWidgetContext Context(UiSession session, UiTheme theme, IUiBindings bindings, float viewWidth)
    {
        return new UiWidgetContext(Scope, session, new WrappingMetrics(), theme, new StubTranslation(), bindings, viewWidth, "root");
    }

    /// <summary>Draws one rule and returns the single colour it painted.</summary>
    private static Color Painted(RuleWidget rule, UiWidgetContext ctx)
    {
        ClearBoxes();
        rule.Draw(new Rect(0f, 0f, 100f, 9f), ctx);
        IList colors = RecordedBoxColors();
        if (colors.Count != 1)
        {
            Check(false, "expected one painted solid, recorded " + colors.Count);
            return default;
        }

        return (Color)colors[0]!;
    }

    private static void CheckPainted(Color fill, Color border, string name)
    {
        IList colors = RecordedBoxColors();
        if (colors.Count < 5)
        {
            Check(false, name + " (recorded " + colors.Count + " solid(s); expected 5)");
            return;
        }

        bool ok = SameColor((Color)colors[0]!, fill);
        for (int i = 1; i < 5; i++)
        {
            ok &= SameColor((Color)colors[i]!, border);
        }

        Check(ok, name);
    }

    private static Color LastLabelColor(int start)
    {
        IList colors = RecordedLabelColors();
        if (colors.Count <= start)
        {
            Check(false, "expected a label call after index " + start + ", recorded none");
            return default;
        }

        return (Color)colors[colors.Count - 1]!;
    }

    private static int LabelCount()
    {
        return RecordedLabelColors().Count;
    }

    private static bool SameColor(Color left, Color right)
    {
        return Near(left.r, right.r) && Near(left.g, right.g) && Near(left.b, right.b) && Near(left.a, right.a);
    }

    private static bool SameRect(Rect left, Rect right)
    {
        return Near(left.x, right.x) && Near(left.y, right.y) && Near(left.width, right.width) && Near(left.height, right.height);
    }

    private static bool Near(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= 0.0001f;
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

    private static void CheckClose(float expected, float actual, string name)
    {
        Check(Near(expected, actual), name + " (expected " + expected + ", got " + actual + ")");
    }

    private static void ResetSeams()
    {
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMouseDown = false;
        UiNative.DebugMouseDrag = false;
        UiNative.DebugMouseUp = false;
        UiNative.DebugEnter = false;
        UiNative.DebugFocusLost = false;
        UiNative.ButtonOverride = null;
        UiNative.SliderOverride = null;
        UiNative.TextFieldOverride = null;
    }

    private static void ClearBoxes()
    {
        MethodInfo? clear = typeof(Verse.Widgets).GetMethod("ClearDrawBoxSolidCalls", BindingFlags.Public | BindingFlags.Static);
        if (clear == null) throw new Exception("Verse stub is missing ClearDrawBoxSolidCalls");
        clear.Invoke(null, null);
    }

    private static IList RecordedBoxColors()
    {
        return (IList)Field("DrawBoxSolidColors");
    }

    private static IList RecordedLabelColors()
    {
        return (IList)Field("LabelColors");
    }

    private static object Field(string name)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(name, BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new Exception("Verse stub is missing " + name);
        return field.GetValue(null)!;
    }

    /// <summary>
    /// The lane's ruler: width from the shared half-width glyph table, height from the wrapped line count
    /// times that model's em. A constant-per-font stub could not express "the band follows the density
    /// font", which is one of the things this lane has to observe.
    /// </summary>
    private sealed class WrappingMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float advance = StubTextWidth.Of(text, font);
            int lines = Math.Max(1, (int)Math.Ceiling(advance / Math.Max(1f, width)));
            return lines * Em(font);
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);

        private static float Em(UiFont font) => font switch
        {
            UiFont.Tiny => 12f,
            UiFont.Medium => 18f,
            _ => 16f
        };
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key) => "[" + key + "]";

        public int TranslationRevision => 0;
    }
}
