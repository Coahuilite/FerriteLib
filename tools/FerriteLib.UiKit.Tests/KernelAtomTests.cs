using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Acceptance lane for the core leaf atoms (0.4.x leaf vocabulary): wrapped text, button, rule,
/// slider and number field. Every atom is driven through its own kind — registration, per-kind
/// attribute schema, declared label set, the one Schema=2 manifest all five are created from — plus
/// one behavioural assertion per atom: a measure contract, a hit test, or a geometry rule.
/// <para>
/// The measurement stub is a wrapping model over the shared half-width glyph table, not a per-font
/// constant. A measure that returned a fixed band would still pass a single-width assertion; it
/// cannot pass two widths of the same string at once, which is what every height assertion below
/// pairs up.
/// </para>
/// </summary>
internal static class KernelAtomTests
{
    private const string Scope = "atom-test";
    private const string Notes = "这是一段用于验证换行高度的文本内容"; // 17 ideographs x 16px = 272px at Small
    private const string Latin = "abcdefghijklmnopq";              // 17 characters x 8px = 136px at Small

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Five atoms register, carry a schema and a label set", VerifyAtomRegistration);
        Run("All five atoms are created from one Schema=2 manifest", VerifyOneManifestCreatesEveryAtom);
        Run("Wrapped text measures its own wrapped height", VerifyWrappedTextMeasureContract);
        Run("Atom Auto widths hug their declared labels", VerifyAtomAutoWidths);
        Run("Button hit test fires its command and paints its own states", VerifyButtonHitAndStates);
        Run("Rule owns hairline geometry and takes no text measure", VerifyRuleGeometry);
        Run("Slider writes its binding and measures its label band", VerifySliderValueAndLabelBand);
        Run("Number field owns focus, edit buffer and commit rule", VerifyNumberFieldCommitRule);
        Run("Atom schemas refuse an attribute the kind never reads", VerifyAtomSchemasRefuseUnknownAttributes);
        ResetSeams();
        return failures;
    }

    // --- registration --------------------------------------------------------------------------

    private static void VerifyAtomRegistration()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string[] kinds = { WrappedTextWidget.Kind, ButtonWidget.Kind, RuleWidget.Kind, SliderWidget.Kind, NumberFieldWidget.Kind };
        foreach (string kind in kinds)
        {
            Check(UiWidgetRegistry.GetAttributeSchema(UiWidgetRegistry.CoreScope, kind) != null,
                kind + " registers a creation-time attribute schema");
        }

        // The four text-bearing atoms feed Width="Auto" through the same declared label set the engine
        // reads; without one, Auto silently falls back to the unsized distribution (N1's failure shape).
        CheckLabelSet(WrappedTextWidget.Kind, "Text", "TextKey");
        CheckLabelSet(ButtonWidget.Kind, "Text", "TextKey");
        CheckLabelSet(SliderWidget.Kind, "Label", "LabelKey");
        CheckLabelSet(NumberFieldWidget.Kind, "Label", "LabelKey");

        // The rule draws no text, so it declares no label set: Auto on it must fall back rather than
        // report a width the kind never paints.
        Check(UiWidgetRegistry.GetLabelAttributes(UiWidgetRegistry.CoreScope, RuleWidget.Kind) == null,
            "chrome/rule declares no label set, so Width=Auto cannot invent a width for it");
    }

    private static void CheckLabelSet(string kind, string literal, string key)
    {
        IReadOnlyCollection<string>? labels = UiWidgetRegistry.GetLabelAttributes(UiWidgetRegistry.CoreScope, kind);
        if (labels == null)
        {
            Check(false, kind + " declares a label set");
            return;
        }

        Check(Contains(labels, literal) && Contains(labels, key), kind + " declares " + literal + "/" + key);
    }

    private static bool Contains(IReadOnlyCollection<string> set, string name)
    {
        foreach (string candidate in set)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    // --- one manifest, five kinds --------------------------------------------------------------

    private static void VerifyOneManifestCreatesEveryAtom()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Stack Id=\"root\" Gap=\"6\" Padding=\"8\">"
            + "<Row Id=\"textRow\">"
            // A Row (not a Stack) because a fixed Width is a column width; a stack child is full width.
            + "<Widget Id=\"note\" Kind=\"text/wrapped\" Bind=\"Note\" Width=\"120\" />"
            + "<Widget Id=\"filler\" Kind=\"chrome/rule\" Height=\"4\" />"
            + "</Row>"
            + "<Widget Id=\"apply\" Kind=\"input/button\" ActionBind=\"Apply\" Text=\"Apply\" />"
            + "<Widget Id=\"sep\" Kind=\"chrome/rule\" Height=\"9\" />"
            + "<Widget Id=\"level\" Kind=\"input/slider\" Bind=\"Level\" Label=\"Volume\" Min=\"0\" Max=\"1\" />"
            + "<Widget Id=\"count\" Kind=\"input/number-field\" Bind=\"Count\" Min=\"0\" Max=\"99\" />"
            + "</Stack>"
            + "</UiPage>";

        var bindings = new UiBindings();
        int fired = 0;
        string note = Notes;
        float level = 0.5f;
        float count = 1f;
        bindings.BindReadOnly("Note", () => note);
        bindings.BindCommand("Apply", () => fired++);
        bindings.BindValue("Level", () => level, v => level = v);
        bindings.BindValue("Count", () => count, v => count = v);

        using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.DarkGold, new WrappedMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(400f, 620f));

        foreach (string id in new[] { "note", "apply", "sep", "level", "count" })
        {
            Check(snapshot.RectById.ContainsKey(id), "the manifest created a rect for '" + id + "'");
        }

        Rect noteRect = snapshot.RectById["note"];
        Check(Near(120f, noteRect.width), "the declared Width survives into the arranged slot");
        Check(Near(60f, noteRect.height),
            "the wrapped band is the measured height: 17 ideographs at 120px wrap into 3 lines of 16 plus 12 padding");
        Check(Near(9f, snapshot.RectById["sep"].height), "a declared Height still wins over the rule's natural hairline");

        // The one pass that proves all five Draw paths execute inside a real tree without a trip.
        EnablePointer(new Vector2(-100000f, -100000f));
        host.DrawFrame(new Rect(0f, 0f, 400f, 620f));
        Check(host.Session.TrippedComponentIds.Count == 0,
            "every atom drew in one frame without tripping the recovery guard");
        Check(fired == 0, "no seam fired a command during a pass with the pointer far away");
        ResetSeams();
    }

    // --- text/wrapped ---------------------------------------------------------------------------

    private static void VerifyWrappedTextMeasureContract()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();
        UiTheme theme = UiTheme.DarkGold;

        WrappedTextWidget widget = new();
        widget.Configure(new UiElementSpec("note", WrappedTextWidget.Kind, Attrs(("Text", Notes))));
        UiWidgetContext ctx = MakeContext(session, new UiBindings(), theme);

        CheckClose(28f, widget.Measure(ctx.WithViewWidth(400f)), "one 16px line plus 12 padding at 400px");
        CheckClose(60f, widget.Measure(ctx.WithViewWidth(120f)), "three 16px lines plus 12 padding at 120px");

        WrappedTextWidget latin = new();
        latin.Configure(new UiElementSpec("note", WrappedTextWidget.Kind, Attrs(("Text", Latin))));
        CheckClose(44f, latin.Measure(ctx.WithViewWidth(120f)),
            "same character count, half the advance: 2 lines, not 3 - the band tracks glyphs, not a constant");

        WrappedTextWidget empty = new();
        empty.Configure(new UiElementSpec("note", WrappedTextWidget.Kind, new Dictionary<string, string>()));
        CheckClose(20f, empty.Measure(ctx.WithViewWidth(10f)),
            "an empty leaf keeps a band instead of collapsing its siblings");

        // The translation seam, exercised at a width where the bracketed stub form and the bare key
        // fall on different sides of a line break: "[a.b.c.d.e.f.g.h.i.j]" is 21 characters (168px) and
        // the key alone is 19 (152px), measured into a 160px band.
        const string Key = "a.b.c.d.e.f.g.h.i.j";
        WrappedTextWidget keyed = new();
        keyed.Configure(new UiElementSpec("note", WrappedTextWidget.Kind, Attrs(("TextKey", Key))));
        CheckClose(44f, keyed.Measure(ctx.WithViewWidth(160f)), "a TextKey band wraps the translated string into 2 lines");

        WrappedTextWidget literal = new();
        literal.Configure(new UiElementSpec("note", WrappedTextWidget.Kind, Attrs(("Text", Key))));
        CheckClose(28f, literal.Measure(ctx.WithViewWidth(160f)),
            "the same characters as a literal fit one line - so the key path really did translate");
    }

    // --- Width="Auto" through the declared label sets -------------------------------------------

    private static void VerifyAtomAutoWidths()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\">"
            + "<Widget Id=\"note\" Kind=\"text/wrapped\" Width=\"Auto\" Text=\"音量\" />"
            + "<Widget Id=\"apply\" Kind=\"input/button\" Width=\"Auto\" ActionBind=\"Apply\" Text=\"ok\" />"
            + "<Widget Id=\"level\" Kind=\"input/slider\" Width=\"Auto\" Bind=\"Level\" Label=\"音量\" />"
            + "<Widget Id=\"count\" Kind=\"input/number-field\" Width=\"Auto\" Bind=\"Count\" Label=\"ab\" />"
            + "<Widget Id=\"rest\" Kind=\"chrome/rule\" Height=\"2\" />"
            + "</Row>"
            + "</UiPage>";

        var bindings = new UiBindings();
        float level = 0f;
        float count = 0f;
        bindings.BindCommand("Apply", () => { });
        bindings.BindValue("Level", () => level, v => level = v);
        bindings.BindValue("Count", () => count, v => count = v);

        using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.DarkGold, new WrappedMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(400f, 200f));

        Check(Near(32f, snapshot.RectById["note"].width), "text/wrapped Auto = 2 ideographs x 16px");
        Check(Near(16f, snapshot.RectById["apply"].width), "input/button Auto = 2 Latin characters x 8px");
        Check(Near(32f, snapshot.RectById["level"].width), "input/slider Auto measures its Label");
        Check(Near(16f, snapshot.RectById["count"].width), "input/number-field Auto measures its Label");
        Check(Near(304f, snapshot.RectById["rest"].width), "the unlabeled rule takes the remainder");
    }

    // --- input/button ---------------------------------------------------------------------------

    private static void VerifyButtonHitAndStates()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();
        UiTheme theme = UiTheme.DarkGold;

        int fired = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("apply", () => fired++);

        ButtonWidget widget = new();
        widget.Configure(new UiElementSpec("apply", ButtonWidget.Kind, Attrs(("ActionBind", "apply"), ("Text", "Apply"))));
        UiWidgetContext ctx = MakeContext(session, bindings, theme);
        var rect = new Rect(0f, 0f, 120f, 28f);

        // The appearance ladder first, with the activation seam held closed: each colour reading is
        // then the state machine's own and cannot be a click's side effect.
        EnablePointer(new Vector2(60f, 14f));
        UiNative.ButtonOverride = _ => false;
        UiNative.DebugMouseDown = true;
        ClearRecordedBoxes();
        widget.Draw(rect, ctx);
        Check(HasColor(RecordedBoxColors(), theme.Selected) && HasColor(RecordedBoxColors(), theme.AccentGold),
            "a held pointer inside the rect paints the active treatment");
        Check(fired == 0, "a state pass with the activation seam closed fires nothing");

        UiNative.DebugMouseDown = false;
        ClearRecordedBoxes();
        widget.Draw(rect, ctx);
        Check(HasColor(RecordedBoxColors(), theme.Hover) && HasColor(RecordedBoxColors(), theme.BorderStrong),
            "hover paints the theme's hover token, not the active treatment");

        UiNative.DebugMousePosition = new Vector2(-5000f, -5000f);
        ClearRecordedBoxes();
        widget.Draw(rect, ctx);
        Check(HasColor(RecordedBoxColors(), theme.Raised) && HasColor(RecordedBoxColors(), theme.Border),
            "the idle treatment is the neutral row of the tone table");
        Check(!HasColor(RecordedBoxColors(), theme.Hover), "an idle button emits no hover token");

        // The hit rule: activation arrives with exactly the arranged rect, and only while the pointer
        // is inside it. The override stands in for the native control, so the rect it is handed is the
        // one this atom computed.
        Vector2 pointer = new(60f, 14f);
        UiNative.DebugMousePosition = pointer;
        Rect? activated = null;
        UiNative.ButtonOverride = candidate =>
        {
            activated = candidate;
            return pointer.x >= candidate.x && pointer.x <= candidate.xMax
                && pointer.y >= candidate.y && pointer.y <= candidate.yMax;
        };
        widget.Draw(rect, ctx);
        Check(fired == 1, "a click on the arranged rect fires the command once");
        Check(activated.HasValue && SameRect(rect, activated.Value),
            "the activation seam received exactly the arranged rect");

        // The override reads the captured pointer, so moving it is what moves the click target here.
        pointer = new Vector2(-5000f, -5000f);
        UiNative.DebugMousePosition = pointer;
        widget.Draw(rect, ctx);
        Check(fired == 1, "a click outside the rect does not fire");
        ResetSeams();
    }

    // --- chrome/rule ----------------------------------------------------------------------------

    private static void VerifyRuleGeometry()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();
        UiTheme theme = UiTheme.DarkGold;

        RuleWidget widget = new();
        widget.Configure(new UiElementSpec("sep", RuleWidget.Kind, Attrs(("Thickness", "2"), ("Inset", "4"))));
        UiWidgetContext ctx = MakeContext(session, new UiBindings(), theme);

        CheckClose(2f, widget.Measure(ctx), "the natural size is the hairline, not a text band or a declaration");

        ClearRecordedBoxes();
        widget.Draw(new Rect(10f, 5f, 200f, 9f), ctx);
        IList rects = RecordedBoxRects();
        Check(rects.Count == 1, "a rule paints exactly one solid");
        if (rects.Count == 1)
        {
            var drawn = (Rect)rects[0]!;
            CheckClose(14f, drawn.x, "Inset applies on the leading side");
            CheckClose(192f, drawn.width, "and on the trailing side");
            CheckClose(8.5f, drawn.y, "the line sits on the vertical centre of its band");
            CheckClose(2f, drawn.height, "the declared thickness is the painted height");
        }

        IList colors = RecordedBoxColors();
        Check(colors.Count == 1 && HasColor(colors, theme.Divider), "the rule paints the divider token and nothing else");

        // One pixel is a legal band, so the degenerate guard is the zero-size one.
        ClearRecordedBoxes();
        widget.Configure(new UiElementSpec("sep", RuleWidget.Kind, new Dictionary<string, string>()));
        widget.Draw(new Rect(0f, 0f, 100f, 1f), ctx);
        Check(RecordedBoxRects().Count == 1, "an undeclared rule still paints its one-pixel hairline");
    }

    // --- input/slider ---------------------------------------------------------------------------

    private static void VerifySliderValueAndLabelBand()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();
        UiTheme theme = UiTheme.DarkGold;

        float value = 0.2f;
        var bindings = new UiBindings();
        bindings.BindValue("level", () => value, v => value = v);

        SliderWidget widget = new();
        widget.Configure(new UiElementSpec("level", SliderWidget.Kind, Attrs(("Bind", "level"), ("Min", "0"), ("Max", "1"), ("Label", "音量"))));
        UiWidgetContext ctx = MakeContext(session, bindings, theme);

        Rect captured = default;
        EnablePointer(new Vector2(-100000f, -100000f));
        UiNative.SliderOverride = (rect, current, min, max) =>
        {
            captured = rect;
            return 0.8f;
        };
        widget.Draw(new Rect(0f, 0f, 300f, 28f), ctx);
        CheckClose(0.8f, value, "the slider writes its value through the typed binding");
        CheckClose(42f, captured.x, "the label band is 2 ideographs x 16px plus 4 padding, then the 6px gap");
        CheckClose(258f, captured.width, "the track is exactly what the measured label leaves");

        // Same element, Latin label: the band follows the glyphs, so a hard-coded 80f column cannot pass.
        widget.Configure(new UiElementSpec("level", SliderWidget.Kind, Attrs(("Bind", "level"), ("Min", "0"), ("Max", "1"), ("Label", "ab"))));
        widget.Draw(new Rect(0f, 0f, 300f, 28f), ctx);
        CheckClose(26f, captured.x, "2 Latin characters (16px) plus 4 padding, then the gap");
        CheckClose(274f, captured.width, "the track grows by exactly what the shorter label frees");

        // An out-of-range host value is clamped into the declared window before it is written back.
        UiNative.SliderOverride = (rect, current, min, max) => 5f;
        widget.Draw(new Rect(0f, 0f, 300f, 28f), ctx);
        CheckClose(1f, value, "a host value above Max is clamped, not written as-is");
        ResetSeams();
    }

    // --- input/number-field ---------------------------------------------------------------------

    private static void VerifyNumberFieldCommitRule()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();
        UiTheme theme = UiTheme.DarkGold;

        float value = 0.5f;
        var bindings = new UiBindings();
        bindings.BindValue("count", () => value, v => value = v);

        NumberFieldWidget widget = new();
        widget.Configure(new UiElementSpec("count", NumberFieldWidget.Kind, Attrs(("Bind", "count"), ("Min", "0"), ("Max", "10"))));
        UiWidgetContext ctx = MakeContext(session, bindings, theme);
        var rect = new Rect(0f, 0f, 120f, 28f);

        EnablePointer(new Vector2(-100000f, -100000f));
        UiNative.TextFieldOverride = (r, text) => "0.75";
        widget.Draw(rect, ctx);
        CheckClose(0.75f, value, "an unfocused free edit commits through the binding");

        UiNative.TextFieldOverride = (r, text) => "abc";
        widget.Draw(rect, ctx);
        CheckClose(0.75f, value, "unparseable text is never written");
        Check(session.GetOrCreateValueState("count").EditText == "abc", "and it stays in the session edit buffer");

        // Focus, then a deferred edit: Live=false must not write while the field holds focus.
        float lazy = 0.5f;
        var lazyBindings = new UiBindings();
        lazyBindings.BindValue("lazy", () => lazy, v => lazy = v);
        NumberFieldWidget deferred = new();
        deferred.Configure(new UiElementSpec("lazy", NumberFieldWidget.Kind, Attrs(("Bind", "lazy"), ("Min", "0"), ("Max", "10"), ("Live", "false"))));
        UiWidgetContext lazyCtx = MakeContext(session, lazyBindings, theme);

        UiNative.TextFieldOverride = (r, text) => text;
        UiNative.DebugMouseDown = true;
        UiNative.DebugMousePosition = new Vector2(60f, 14f);
        deferred.Draw(rect, lazyCtx);
        UiNative.DebugMouseDown = false;
        Check(session.GetOrCreateValueState("lazy").Focused, "a mouse-down inside the field focuses it");

        UiNative.TextFieldOverride = (r, text) => "0.7";
        deferred.Draw(rect, lazyCtx);
        CheckClose(0.5f, lazy, "a focused Live=false field defers the write");
        CheckClose(0.7f, session.GetOrCreateValueState("lazy").FloatValue, "while the parsed value still lands in the session");
        ResetSeams();
    }

    // --- creation-time schemas ------------------------------------------------------------------

    private static void VerifyAtomSchemasRefuseUnknownAttributes()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        foreach (string kind in new[] { WrappedTextWidget.Kind, ButtonWidget.Kind, RuleWidget.Kind, SliderWidget.Kind, NumberFieldWidget.Kind })
        {
            Reject(
                "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
                + "<Widget Id=\"x\" Kind=\"" + kind + "\" Bogus=\"1\" />"
                + "</UiPage>",
                kind + " refuses an attribute its schema never listed");
        }

        Reject(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"b\" Kind=\"input/button\" Text=\"x\" />"
            + "</UiPage>",
            "a button with no ActionBind fails at creation instead of drawing a dead control");
    }

    // --- harness --------------------------------------------------------------------------------

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
        if (Math.Abs(expected - actual) <= 0.0001f)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " (expected '" + expected + "', got '" + actual + "')");
        }
    }

    private static bool Near(float expected, float actual) => Math.Abs(expected - actual) <= 0.0001f;

    private static void Reject(string xml, string what)
    {
        try
        {
            using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), new UiBindings(), UiTheme.DarkGold, new WrappedMetrics(), new StubTranslation());
            Check(false, what + " - but the host accepted it");
        }
        catch (UiContractException)
        {
            Check(true, what + " - rejected at creation");
        }
    }

    private static Dictionary<string, string> Attrs(params (string Name, string Value)[] pairs)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string value) in pairs)
        {
            attributes[name] = value;
        }

        return attributes;
    }

    private static UiWidgetContext MakeContext(UiSession session, IUiBindings bindings, UiTheme theme)
    {
        return new UiWidgetContext(Scope, session, new WrappedMetrics(), theme, new StubTranslation(), bindings, 400f, "root");
    }

    private static void EnablePointer(Vector2 position)
    {
        UiNative.DebugMousePositionEnabled = true;
        UiNative.DebugMousePosition = position;
        UiNative.ButtonOverride = rect =>
            position.x >= rect.x && position.x <= rect.xMax && position.y >= rect.y && position.y <= rect.yMax;
        UiNative.SliderOverride = (rect, current, min, max) => current;
        UiNative.TextFieldOverride = (rect, text) => text;
    }

    private static void ResetSeams()
    {
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMousePosition = Vector2.zero;
        UiNative.DebugMouseDown = false;
        UiNative.DebugMouseDrag = false;
        UiNative.DebugMouseUp = false;
        UiNative.DebugEnter = false;
        UiNative.DebugFocusLost = false;
        UiNative.ButtonOverride = null;
        UiNative.SliderOverride = null;
        UiNative.TextFieldOverride = null;
    }

    private static void ClearRecordedBoxes()
    {
        Invoke("ClearDrawBoxSolidCalls");
    }

    private static IList RecordedBoxRects()
    {
        return (IList)Field("DrawBoxSolidRects");
    }

    private static IList RecordedBoxColors()
    {
        return (IList)Field("DrawBoxSolidColors");
    }

    private static bool HasColor(IList colors, Color expected)
    {
        foreach (object value in colors)
        {
            if (value is Color color && SameColor(color, expected)) return true;
        }

        return false;
    }

    private static bool SameColor(Color one, Color other)
    {
        return one.r == other.r && one.g == other.g && one.b == other.b && one.a == other.a;
    }

    private static bool SameRect(Rect one, Rect other)
    {
        return Near(one.x, other.x) && Near(one.y, other.y)
            && Near(one.width, other.width) && Near(one.height, other.height);
    }

    private static object Field(string name)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(name, BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new Exception("Verse stub is missing " + name);
        return field.GetValue(null)!;
    }

    private static void Invoke(string name)
    {
        MethodInfo? method = typeof(Verse.Widgets).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        if (method == null) throw new Exception("Verse stub is missing " + name);
        method.Invoke(null, null);
    }

    /// <summary>
    /// The lane's text ruler: width from the shared half-width glyph model, height from the wrapped
    /// line count times that model's em. Constant-per-font stubs cannot express "the same string at
    /// two widths", which is the positive control every measure assertion here relies on.
    /// </summary>
    private sealed class WrappedMetrics : ITextMetrics
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
        public string Translate(string key)
        {
            return "[" + key + "]";
        }

        public int TranslationRevision => 0;
    }
}
