using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// B7 lane: the <c>input/text-field</c> atom, and the four things that make it a kind rather than a
/// convenience — per-element focus, a draft the session owns, the commit rule, and the one disabled funnel
/// it shares with every other interactive kind.
/// <para>
/// A lane that only proved the control draws would not be evidence for this kind, so every claim here drives
/// the interaction: a pointer that focuses and a pointer that does not, a keystroke that reaches the model,
/// a deferred draft that must <b>not</b> reach it until the commit frame, a read-only binding that refuses
/// the write, and a command-disabled element whose funnel call never reaches the native control at all. The
/// red-first evidence for this file is a mutated control (the deferred-write condition removed, and the
/// read-only guard removed) — recorded in the commit message and in <c>MEMORY.md</c>, because a new kind has
/// no pre-fix revision to be red against.
/// </para>
/// <para>
/// Note the <see cref="Run"/> convention: <c>ok:</c> is printed only when the action both returned and left
/// no failed check behind. Printing it unconditionally is the pre-existing convention recorded as F2, and a
/// lane whose whole subject is interaction state is exactly where a green lane name over red assertions
/// would be most misleading.
/// </para>
/// </summary>
internal static class KernelTextFieldTests
{
    private const string Scope = "text-field-test";
    private const string ProbeKind = "test/command-probe";
    private const float Width = 240f;
    private const float Height = 24f;

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("input/text-field registers with its schema and its label set", VerifyRegistration);
        Run("A field that cannot name a string binding is refused at creation", VerifyValidation);
        Run("A live edit reaches the bound model on the keystroke", VerifyLiveEditWrites);
        Run("Focus and the draft belong to the session, and blur restores the model", VerifyFocusAndDraft);
        Run("Live=false defers the write, and Enter commits the draft exactly once", VerifyDeferredCommit);
        Run("A read-only binding is never written through the field", VerifyReadOnlyFieldTakesNoEdit);
        Run("A command-disabled element's field reaches neither the control nor focus", VerifyDisabledFieldTakesNoEdit);
        Run("The placeholder is painted only on an empty, writable, unfocused field", VerifyPlaceholder);
        Run("Height follows the theme unless the element declares one", VerifyHeight);
        ResetSeams();
        return failures;
    }

    // --- registration and creation contract -----------------------------------------------------

    private static void VerifyRegistration()
    {
        PrepareCore();

        IReadOnlyCollection<string>? schema = UiWidgetRegistry.GetAttributeSchema(UiWidgetRegistry.CoreScope, TextFieldWidget.Kind);
        if (schema == null)
        {
            Check(false, "input/text-field declares an attribute schema");
            return;
        }

        foreach (string name in new[]
        {
            "Id", "Kind", "Tab", "Hidden", "Visible", "VisibleKey", "Tone", "Emphasis",
            "Bind", "Live", "Placeholder", "PlaceholderKey", "Label", "LabelKey", "Height"
        })
        {
            Check(Contains(schema, name), "the schema allows '" + name + "'");
        }

        IReadOnlyCollection<string>? labels = UiWidgetRegistry.GetLabelAttributes(UiWidgetRegistry.CoreScope, TextFieldWidget.Kind);
        Check(
            labels != null && labels.Count == 2 && Contains(labels, "Label") && Contains(labels, "LabelKey"),
            "the label set is exactly Label/LabelKey - the text this kind paints as its own label");
        Check(
            labels != null && !Contains(labels, "Placeholder") && !Contains(labels, "PlaceholderKey"),
            "the placeholder is not in the label set: a hint painted inside an empty field is not the element's declared width");
    }

    private static void VerifyValidation()
    {
        PrepareCore();
        var bindings = new UiBindings();

        var unnamed = new TextFieldWidget();
        unnamed.Configure(new UiElementSpec("", TextFieldWidget.Kind, new Dictionary<string, string>()));
        Check(Throws(() => unnamed.Validate(bindings, "page/field")),
            "a field with neither Id nor Bind is refused at creation");

        var named = new TextFieldWidget();
        named.Configure(new UiElementSpec("field", TextFieldWidget.Kind, new Dictionary<string, string>()));

        float number = 1f;
        bindings.BindValue("field", () => number, value => number = value);
        Check(Throws(() => named.Validate(bindings, "page/field")),
            "a key bound to a non-string value is refused by the typed validation");

        var named2 = new TextFieldWidget();
        named2.Configure(new UiElementSpec("other", TextFieldWidget.Kind, new Dictionary<string, string>()));
        string text = "";
        bindings.BindValue("other", () => text, value => text = value);
        Check(!Throws(() => named2.Validate(bindings, "page/other")),
            "and a string-bound key passes it");
    }

    // --- focus, edit and commit ------------------------------------------------------------------

    private static void VerifyLiveEditWrites()
    {
        PrepareCore();
        var bindings = new UiBindings();
        string model = "start";
        bindings.BindValue("search", () => model, value => model = value);

        using UiSession session = new();
        TextFieldWidget widget = MakeWidget(Attrs("search"));
        UiWidgetContext ctx = MakeContext(session, bindings);
        var rect = new Rect(0f, 0f, Width, Height);

        EnablePointer(Center(rect));
        UiNative.TextFieldOverride = (r, text) => "typed";
        widget.Draw(rect, ctx);

        Check(model == "typed", "the keystroke reaches the model through the typed binding");
        Check(session.GetOrCreateValueState("search").Focused, "and the pointer inside the field focused it");
        DisablePointer();
    }

    private static void VerifyFocusAndDraft()
    {
        PrepareCore();
        var bindings = new UiBindings();
        string model = "start";
        bindings.BindValue("search", () => model, value => model = value);

        using UiSession session = new();
        TextFieldWidget widget = MakeWidget(Attrs("search", live: false));
        UiWidgetContext ctx = MakeContext(session, bindings);
        var rect = new Rect(0f, 0f, Width, Height);
        UiValueState state = session.GetOrCreateValueState("search");

        // A pointer outside the field must not take focus, and must not write anything: the backend hands
        // back the text it was given, so this frame carries no edit at all.
        EnablePointer(new Vector2(rect.xMax + 40f, rect.yMax + 40f));
        UiNative.TextFieldOverride = (r, text) => text;
        widget.Draw(rect, ctx);
        Check(!state.Focused, "a pointer outside the field does not focus it");
        Check(model == "start", "and nothing is written");

        // The complementary rule, pinned rather than left to the fixture: an edit that arrives while the
        // field is unfocused commits immediately even under Live=false. There is no draft to defer - the
        // deferral exists so a focused editor can be typed into, and this frame is not that.
        UiNative.TextFieldOverride = (r, text) => "typed";
        widget.Draw(rect, ctx);
        Check(model == "typed", "an edit the field never held as a draft commits immediately, even under Live=false");

        // Now the deferred case: restore the model, focus the field, and type again.
        model = "start";
        UiNative.DebugMousePosition = Center(rect);
        UiNative.TextFieldOverride = (r, text) => "typed";
        widget.Draw(rect, ctx);
        Check(state.Focused, "a pointer inside the field focuses it");
        Check(model == "start", "Live=false keeps the focused draft out of the model");
        Check(state.EditText == "typed", "the draft is the session's, and it is what the field shows");

        // Losing focus commits the draft and the field displays the model again.
        UiNative.DebugMouseDown = false;
        UiNative.DebugFocusLost = true;
        UiNative.TextFieldOverride = (r, text) => text;
        widget.Draw(rect, ctx);
        Check(!state.Focused, "losing focus releases the field");
        Check(model == "typed", "and the deferred draft reaches the model on blur");

        UiNative.DebugFocusLost = false;
        ClearLabels();
        widget.Draw(rect, ctx);
        Check(state.EditText == "typed", "the next frame shows the committed model, not a stale draft");
        DisablePointer();
    }

    private static void VerifyDeferredCommit()
    {
        PrepareCore();
        var bindings = new UiBindings();
        string model = "start";
        int writes = 0;
        bindings.BindValue("search", () => model, value =>
        {
            model = value;
            writes++;
        });

        using UiSession session = new();
        TextFieldWidget widget = MakeWidget(Attrs("search", live: false));
        UiWidgetContext ctx = MakeContext(session, bindings);
        var rect = new Rect(0f, 0f, Width, Height);

        EnablePointer(Center(rect));
        UiNative.TextFieldOverride = (r, text) => "draft";
        widget.Draw(rect, ctx);
        Check(model == "start" && writes == 0, "a keystroke while Live=false writes nothing, however long the draft");

        UiNative.DebugMouseDown = false;
        UiNative.TextFieldOverride = (r, text) => text;
        UiNative.DebugEnter = true;
        widget.Draw(rect, ctx);
        Check(model == "draft", "Enter commits the draft through the binding");
        Check(writes == 1, "exactly once, not once per frame");
        Check(!session.GetOrCreateValueState("search").Focused, "and Enter releases focus");

        UiNative.DebugEnter = false;
        widget.Draw(rect, ctx);
        Check(writes == 1, "a following frame with no edit writes nothing again");

        // The deferred write must be a commit, not a live write that happened to be late: with Live=true
        // the very same keystroke reaches the model on the frame it is typed.
        var bindings2 = new UiBindings();
        string live = "start";
        bindings2.BindValue("search", () => live, value => live = value);
        using UiSession session2 = new();
        TextFieldWidget liveWidget = MakeWidget(Attrs("search"));
        UiWidgetContext liveCtx = MakeContext(session2, bindings2);
        EnablePointer(Center(rect));
        UiNative.TextFieldOverride = (r, text) => "draft";
        liveWidget.Draw(rect, liveCtx);
        Check(live == "draft", "the same keystroke reaches the model immediately when Live is left at its default");
        DisablePointer();
    }

    // --- the two refusals ------------------------------------------------------------------------

    private static void VerifyReadOnlyFieldTakesNoEdit()
    {
        PrepareCore();
        string published = "sealed";
        var bindings = new UiBindings();
        bindings.BindReadOnly("published", () => published);

        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
                + "<Widget Id=\"search\" Kind=\"input/text-field\" Bind=\"published\" Height=\"24\" Placeholder=\"find a pack\" />"
                + "</UiPage>"),
            bindings,
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());

        int native = 0;
        UiNative.TextFieldOverride = (r, text) =>
        {
            native++;
            return "hacked";
        };

        EnablePointer(new Vector2(10f, 10f));
        ClearLabels();
        host.DrawFrame(new Rect(0f, 0f, Width, 120f));

        Check(native > 0, "a read-only field still draws through the native control: read-only is not the command-disabled state");
        Check(published == "sealed", "but the edit is never written through the read-only binding");
        // The widget's own guard is not decoration on top of the binding's refusal: without it the write
        // reaches a read-only key, UiBindings.Set throws, and the tree replaces the field with a recovery
        // band. This check is what makes the guard's purpose visible - it pins that a read-only field trips
        // nothing rather than merely that the model survived.
        Check(host.Session.TrippedNodes.Count == 0, "and refusing the write trips no recovery: the field is not replaced by a band");
        Check(LabelTexts().Count == 0, "and no placeholder invites typing into a field that refuses writes");
        DisablePointer();
    }

    private static void VerifyDisabledFieldTakesNoEdit()
    {
        PrepareCore();
        UiWidgetRegistry.Register(Scope, ProbeKind, () => new ProbeWidget(), new[] { "Id", "Kind", "ActionBind", "Height" });

        bool enabled = false;
        var bindings = new UiBindings();
        bindings.BindCommand("gate", () => { }, () => enabled);

        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
                + "<Widget Id=\"gate\" Kind=\"" + ProbeKind + "\" ActionBind=\"gate\" Height=\"24\" />"
                + "</UiPage>"),
            bindings,
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());

        host.DrawFrame(new Rect(0f, 0f, Width, 120f));
        UiNode node = host.Session.GetNodeByElementId("gate")
            ?? throw new Exception("the command-bound element was not arranged");
        Check(node.IsDisabled, "the probe element is published disabled while its predicate answers false");

        var rect = new Rect(0f, 0f, Width, Height);
        int native = 0;
        UiNative.TextFieldOverride = (r, text) =>
        {
            native++;
            return "hacked";
        };

        string shown = UiNative.TextField(rect, "gate", host.Session, "model", out bool committed);
        Check(native == 0, "a disabled field never reaches the native control, so no click is consumed and no text is edited");
        Check(!committed, "and it commits nothing");
        Check(!host.Session.GetOrCreateValueState("gate").Focused, "and it never takes focus");
        Check(shown == "model", "while still handing the caller the model's value to draw");

        // Contrast, so the checks above are the guard's answer rather than a lane that never reached it.
        enabled = true;
        host.DrawFrame(new Rect(0f, 0f, Width, 120f));
        Check(!node.IsDisabled, "the same element stops being disabled once its predicate allows the command");
        native = 0;
        UiNative.TextField(rect, "gate", host.Session, "model", out committed);
        Check(native == 1 && committed, "and an enabled field reaches the control and reports the edit");

        UiNative.TextFieldOverride = null;
    }

    // --- placeholder and measure ------------------------------------------------------------------

    private static void VerifyPlaceholder()
    {
        PrepareCore();
        var bindings = new UiBindings();
        string model = "";
        bindings.BindValue("search", () => model, value => model = value);

        using UiSession session = new();
        TextFieldWidget widget = MakeWidget(Attrs("search", placeholder: "find a pack"));
        UiWidgetContext ctx = MakeContext(session, bindings);
        var rect = new Rect(0f, 0f, Width, Height);
        UiNative.TextFieldOverride = (r, text) => text;

        DisablePointer();
        ClearLabels();
        widget.Draw(rect, ctx);
        Check(LabelTexts().Contains("find a pack"), "an empty, unfocused field paints its placeholder");

        EnablePointer(Center(rect));
        ClearLabels();
        widget.Draw(rect, ctx);
        Check(session.GetOrCreateValueState("search").Focused, "the pointer inside focused the empty field");
        Check(!LabelTexts().Contains("find a pack"), "a focused field paints none: the caret is the hint");

        UiNative.DebugMouseDown = false;
        UiNative.DebugFocusLost = true;
        widget.Draw(rect, ctx);
        UiNative.DebugFocusLost = false;
        model = "typed";
        ClearLabels();
        widget.Draw(rect, ctx);
        Check(!LabelTexts().Contains("find a pack"), "and a non-empty field paints none either");

        DisablePointer();
    }

    private static void VerifyHeight()
    {
        PrepareCore();
        var bindings = new UiBindings();
        string model = "";
        bindings.BindValue("search", () => model, value => model = value);

        using UiSession session = new();
        UiWidgetContext ctx = MakeContext(session, bindings);

        Check(Near(MakeWidget(Attrs("search")).Measure(ctx), ctx.Theme.Geometry.RowHeight),
            "an undeclared Height measures the theme's row height");

        Dictionary<string, string> tall = Attrs("search");
        tall["Height"] = "40";
        Check(Near(MakeWidget(tall).Measure(ctx), 40f), "a declared Height wins, as on every other atom");
    }

    // --- helpers ----------------------------------------------------------------------------------

    private static void PrepareCore()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
    }

    private static TextFieldWidget MakeWidget(IReadOnlyDictionary<string, string> attrs)
    {
        var widget = new TextFieldWidget();
        widget.Configure(new UiElementSpec("search", TextFieldWidget.Kind, attrs));
        return widget;
    }

    private static Dictionary<string, string> Attrs(string bind, bool live = true, string? placeholder = null)
    {
        var attrs = new Dictionary<string, string> { ["Bind"] = bind };
        if (!live) attrs["Live"] = "false";
        if (placeholder != null) attrs["Placeholder"] = placeholder;
        return attrs;
    }

    private static UiWidgetContext MakeContext(UiSession session, IUiBindings bindings)
    {
        return new UiWidgetContext(
            "text-field-test", session, new StubMetrics(), UiTheme.DarkGold,
            new StubTranslation(), bindings, Width, "root");
    }

    private static Vector2 Center(Rect rect) => new(rect.x + rect.width / 2f, rect.y + rect.height / 2f);

    private static void EnablePointer(Vector2 point)
    {
        UiNative.DebugMousePositionEnabled = true;
        UiNative.DebugMousePosition = point;
        UiNative.DebugMouseDown = true;
    }

    private static void DisablePointer()
    {
        ResetSeams();
    }

    private static void ResetSeams()
    {
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMousePosition = default;
        UiNative.DebugMouseDown = false;
        UiNative.DebugMouseDrag = false;
        UiNative.DebugMouseUp = false;
        UiNative.DebugEnter = false;
        UiNative.DebugFocusLost = false;
        UiNative.TextFieldOverride = null;
    }

    private static void ClearLabels()
    {
        foreach (string name in new[] { "LabelRects", "LabelTexts", "LabelColors" })
        {
            FieldInfo? field = typeof(Verse.Widgets).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                throw new Exception("Verse stub is missing " + name);
            }

            ((IList)field.GetValue(null)!).Clear();
        }
    }

    private static IList LabelTexts()
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField("LabelTexts", BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new Exception("Verse stub is missing LabelTexts");
        return (IList)field.GetValue(null)!;
    }

    private static bool Contains(IReadOnlyCollection<string> set, string name)
    {
        foreach (string candidate in set)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static bool Near(float left, float right)
    {
        return Math.Abs(left - right) <= 0.0001f;
    }

    private static int Run(string name, Action action)
    {
        ResetSeams();
        int before = failures;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }

        if (failures == before)
        {
            Console.WriteLine("  ok: " + name);
        }

        return failures - before;
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

    /// <summary>
    /// A kind that declares <c>ActionBind</c> and nothing else: the shape the engine publishes disabled,
    /// used here to reach the funnel's disabled branch with a real arranged element.
    /// </summary>
    private sealed class ProbeWidget : IUiWidget
    {
        string IUiWidget.Kind => ProbeKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            return 24f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }
}
