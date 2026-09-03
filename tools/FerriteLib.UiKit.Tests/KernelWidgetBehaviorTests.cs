using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Widget behavior lanes migrated from the deleted legacy per-widget suites onto the greenfield
/// kernel widgets: stepper +/- stepping and clamping through typed bindings, slider-to-field
/// sync, number-field commit semantics (immediate when live, deferred while focused, never for
/// unparseable text), and the mode-row's responsive measure steps, option selection, and
/// selected-state theme tokens. Driven entirely through UiNative test seams plus the Verse
/// stub's recorded DrawBoxSolid calls; no in-game claim.
/// </summary>
internal static class KernelWidgetBehaviorTests
{
    private const float FieldWidth = 64f;
    private const float ButtonWidth = 24f;
    private const float Gap = 6f;
    private const float RowHeight = 28f;
    private const float TotalWidth = 400f;

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        VerifyStepperButtonsStepAndClamp();
        VerifyStepperSliderWritesAndSyncsField();
        VerifyStepperFieldCommitSemantics();
        VerifyModeRowResponsiveMeasure();
        VerifyModeRowSelectsAndPaintsTokens();
        ResetSeams();
        return failures;
    }

    // Rect math mirrors StepperSliderWidget.Draw for the default (no-label) layout:
    // minus [0,24], slider [30,300], field [306,370], plus [376,400] at y=0..28.
    private static Rect MinusRect() => new(0f, 0f, ButtonWidth, RowHeight);

    private static Rect FieldRect()
    {
        float sliderX = ButtonWidth + Gap;
        float remaining = Math.Max(1f, TotalWidth - sliderX - FieldWidth - Gap * 2f - ButtonWidth);
        float fieldX = sliderX + remaining + Gap;
        return new Rect(fieldX, 0f, FieldWidth, RowHeight);
    }

    private static Rect PlusRect()
    {
        Rect field = FieldRect();
        return new Rect(field.xMax + Gap, 0f, ButtonWidth, RowHeight);
    }

    private static void VerifyStepperButtonsStepAndClamp()
    {
        float value = 0.5f;
        var bindings = new UiBindings();
        bindings.BindValue("vol", () => value, v => value = v);

        using UiSession session = new();
        StepperSliderWidget widget = MakeStepper();
        widget.Configure(new UiElementSpec("vol", StepperSliderWidget.Kind, Attrs("0", "1", "0.05", null)));

        UiWidgetContext ctx = MakeContext(session, bindings);
        EnablePointer();

        UiNative.DebugMousePosition = Center(MinusRect());
        widget.Draw(new Rect(0f, 0f, TotalWidth, RowHeight), ctx);
        CheckClose(0.45f, value, "minus button steps down by Step and writes the binding");

        UiNative.DebugMousePosition = Center(PlusRect());
        widget.Draw(new Rect(0f, 0f, TotalWidth, RowHeight), ctx);
        CheckClose(0.5f, value, "plus button steps back up");

        value = 0.98f;
        UiNative.DebugMousePosition = Center(PlusRect());
        widget.Draw(new Rect(0f, 0f, TotalWidth, RowHeight), ctx);
        CheckClose(1f, value, "plus clamps at Max");

        value = 0.02f;
        UiNative.DebugMousePosition = Center(MinusRect());
        widget.Draw(new Rect(0f, 0f, TotalWidth, RowHeight), ctx);
        CheckClose(0f, value, "minus clamps at Min");

        DisablePointer();
    }

    private static void VerifyStepperSliderWritesAndSyncsField()
    {
        float value = 0.5f;
        var bindings = new UiBindings();
        bindings.BindValue("vol", () => value, v => value = v);

        using UiSession session = new();
        StepperSliderWidget widget = MakeStepper();
        widget.Configure(new UiElementSpec("vol", StepperSliderWidget.Kind, Attrs("0", "1", "0.05", null)));
        UiWidgetContext ctx = MakeContext(session, bindings);

        EnablePointer();
        UiNative.SliderOverride = (rect, current, min, max) => 0.8f;
        widget.Draw(new Rect(0f, 0f, TotalWidth, RowHeight), ctx);

        CheckClose(0.8f, value, "slider change is written through the typed binding");
        UiValueState state = session.GetOrCreateValueState("vol");
        Check(state.EditText == "0.8", "number field text syncs to the new slider value");
        DisablePointer();
    }

    private static void VerifyStepperFieldCommitSemantics()
    {
        var bindings = new UiBindings();
        float value = 0.5f;
        bindings.BindValue("vol", () => value, v => value = v);

        using UiSession session = new();
        StepperSliderWidget widget = MakeStepper();
        widget.Configure(new UiElementSpec("vol", StepperSliderWidget.Kind, Attrs("0", "1", "0.05", null)));
        UiWidgetContext ctx = MakeContext(session, bindings);

        EnablePointer();
        Rect field = FieldRect();

        // Live (default): editing while unfocused commits immediately.
        UiNative.TextFieldOverride = (rect, text) => "0.75";
        widget.Draw(new Rect(0f, 0f, TotalWidth, RowHeight), ctx);
        CheckClose(0.75f, value, "unfocused field edit commits through the binding");

        // Unparseable text never writes.
        UiNative.TextFieldOverride = (rect, text) => "abc";
        widget.Draw(new Rect(0f, 0f, TotalWidth, RowHeight), ctx);
        CheckClose(0.75f, value, "invalid text is kept in the edit buffer, not written");
        Check(session.GetOrCreateValueState("vol").EditText == "abc",
            "invalid text stays in the session edit buffer");

        // Live=false: edits are deferred while the control holds focus.
        bindings.BindValue("lazy", () => value, v => value = v);
        StepperSliderWidget lazy = MakeStepper();
        lazy.Configure(new UiElementSpec("lazy", StepperSliderWidget.Kind, Attrs("0", "1", "0.05", "false")));

        UiNative.TextFieldOverride = (rect, text) => text;
        UiNative.DebugMouseDown = true;
        UiNative.DebugMousePosition = Center(field);
        lazy.Draw(new Rect(0f, 0f, TotalWidth, RowHeight), ctx);
        UiNative.DebugMouseDown = false;
        Check(session.GetOrCreateValueState("lazy").Focused, "mouse-down inside the field focuses it");

        value = 0.5f;
        UiNative.TextFieldOverride = (rect, text) => "0.7";
        lazy.Draw(new Rect(0f, 0f, TotalWidth, RowHeight), ctx);
        CheckClose(0.5f, value, "focused Live=false field defers the write");
        CheckClose(0.7f, session.GetOrCreateValueState("lazy").FloatValue,
            "focused edit still lands in the session value state");

        DisablePointer();
    }

    private static void VerifyModeRowResponsiveMeasure()
    {
        var bindings = new UiBindings();
        string mode = "A";
        bindings.BindValue("mode", () => mode, v => mode = v);

        using UiSession session = new();
        InputModeRowWidget widget = new();
        widget.Configure(MakeModeRowSpec());
        UiWidgetContext ctx = MakeContext(session, bindings);

        float wide = widget.Measure(ctx.WithViewWidth(600f));
        float compact = widget.Measure(ctx.WithViewWidth(400f));
        float narrow = widget.Measure(ctx.WithViewWidth(150f));

        Check(compact > wide, "compact mode uses more rows than wide");
        Check(narrow > compact, "narrow mode uses more rows than compact");
        CheckEqual(36f, wide, "wide (4 columns) is one row tall");
        CheckEqual(70f, compact, "compact (2 columns) is two rows tall");
        CheckEqual(138f, narrow, "narrow (1 column) stacks all four options");
    }

    private static void VerifyModeRowSelectsAndPaintsTokens()
    {
        var bindings = new UiBindings();
        string mode = "A";
        bindings.BindValue("mode", () => mode, v => mode = v);

        using UiSession session = new();
        InputModeRowWidget widget = new();
        widget.Configure(MakeModeRowSpec());
        UiWidgetContext ctx = MakeContext(session, bindings);

        var page = new Rect(0f, 0f, 150f, 138f);
        EnablePointer();
        // Narrow layout: option 2 occupies the second stacked row.
        float columnWidth = page.width - 8f;
        UiNative.DebugMousePosition = new Vector2(
            page.x + 4f + columnWidth / 2f,
            page.y + 4f + 1 * (RowHeight + 6f) + RowHeight / 2f);
        widget.Draw(page, ctx);
        Check(mode == "B", "clicking an option writes its value through the typed binding");
        DisablePointer();

        // Selected state must paint with the theme's selected fill + gold accent border;
        // an unselected row must not emit either token.
        ClearRecordedBoxes();
        widget.Draw(page, ctx);
        IList colors = RecordedBoxColors();
        UiTheme theme = UiTheme.DarkGold;
        Check(colors.Contains(theme.Selected) && colors.Contains(theme.AccentGold),
            "selected option uses the Selected fill and AccentGold border tokens");

        mode = "unset";
        ClearRecordedBoxes();
        widget.Draw(page, ctx);
        colors = RecordedBoxColors();
        Check(!colors.Contains(theme.Selected) && !colors.Contains(theme.AccentGold),
            "unselected options emit neither selected token");
    }

    private static StepperSliderWidget MakeStepper()
    {
        return new StepperSliderWidget();
    }

    private static Dictionary<string, string> Attrs(string min, string max, string step, string? live)
    {
        var attrs = new Dictionary<string, string>
        {
            ["Min"] = min,
            ["Max"] = max,
            ["Step"] = step
        };

        if (live != null) attrs["Live"] = live;
        return attrs;
    }

    private static UiElementSpec MakeModeRowSpec()
    {
        var attrs = new Dictionary<string, string>
        {
            ["Title1"] = "A", ["Value1"] = "A",
            ["Title2"] = "B", ["Value2"] = "B",
            ["Title3"] = "C", ["Value3"] = "C",
            ["Title4"] = "D", ["Value4"] = "D"
        };

        return new UiElementSpec("mode", InputModeRowWidget.Kind, attrs);
    }

    private static UiWidgetContext MakeContext(UiSession session, IUiBindings bindings)
    {
        return new UiWidgetContext(
            "test", session, new FixedMetrics(), UiTheme.DarkGold,
            new FixedTranslation(), bindings, TotalWidth, "root");
    }

    private static Vector2 Center(Rect rect) => new(rect.x + rect.width / 2f, rect.y + rect.height / 2f);

    private static void EnablePointer()
    {
        UiNative.DebugMousePositionEnabled = true;
        UiNative.DebugMousePosition = new Vector2(-100000f, -100000f);
        UiNative.ButtonOverride = rect =>
        {
            Vector2 p = UiNative.DebugMousePosition;
            return p.x >= rect.x && p.x <= rect.xMax && p.y >= rect.y && p.y <= rect.yMax;
        };
        UiNative.SliderOverride = (rect, current, min, max) => current;
        UiNative.TextFieldOverride = (rect, text) => text;
    }

    private static void DisablePointer()
    {
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMouseDown = false;
        UiNative.DebugEnter = false;
        ResetSeams();
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

    private static void ClearRecordedBoxes()
    {
        MethodInfo? clear = typeof(Verse.Widgets).GetMethod(
            "ClearDrawBoxSolidCalls", BindingFlags.Public | BindingFlags.Static);
        if (clear == null) throw new Exception("Verse stub is missing ClearDrawBoxSolidCalls");
        clear.Invoke(null, null);
    }

    private static IList RecordedBoxColors()
    {
        FieldInfo? colors = typeof(Verse.Widgets).GetField(
            "DrawBoxSolidColors", BindingFlags.Public | BindingFlags.Static);
        if (colors == null) throw new Exception("Verse stub is missing DrawBoxSolidColors");
        return (IList)colors.GetValue(null)!;
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

    private static void CheckEqual<T>(T expected, T actual, string name)
    {
        if (Equals(expected, actual))
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " (expected '" + expected + "', got '" + actual + "')");
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

    private sealed class FixedMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => text.Length;

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class FixedTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
    }
}
