using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Command-state lane: dynamic executability, and the one disabled decision it feeds.
/// <para>
/// The claim is not "a disabled button is painted differently" - that producer already existed
/// (<c>KernelWritabilityTests</c> holds the read-only case). The claim is that the disabled answer is
/// enforced, once, where every kind meets it:
/// <list type="bullet">
/// <item><c>CanExecute</c> is the veto read, and <c>Invoke</c> refuses to run a command whose predicate
/// answers false - so a control that forgot to ask still cannot fire it.</item>
/// <item>The engine publishes that answer on the element's node, and the funnel's context-carrying
/// interactive entry points refuse the pointer for a disabled element: no hot-control capture, no consumed
/// event, no click that disappears into a control the owner has taken away.</item>
/// <item>The disabled look keeps coming from the one writability funnel
/// (<c>UiThemeDraw.StatusTreatment(..., writable: false)</c> → <c>UiStatusTone.Disabled</c>), so there is
/// still exactly one disabled treatment in the library.</item>
/// <item>An element that declares no command, and an element whose <c>ActionBind</c> names an action
/// rather than a command, are never disabled - the guard cannot change a page that binds no commands.</item>
/// </list>
/// Planted-failure evidence: removing the <c>Invoke</c> guard reddens "a disabled element executes
/// nothing"; removing the funnel guard reddens "a disabled element does not capture the hot control".
/// </para>
/// </summary>
internal static class KernelCommandStateTests
{
    private const string Scope = "command-state-lane";
    private const string ProbeKind = "test/action-probe";

    private static int failures;
    private static float actionPayload;

    public static int RunAll()
    {
        failures = 0;
        Run("A command's executability read follows its predicate", VerifyCanExecuteSemantics);
        Run("A disabled command does not run", VerifyInvokeRefusesDisabled);
        Run("The disabled treatment is the one writability funnel", VerifyDisabledPlane);
        Run("A disabled element neither executes nor captures the pointer", VerifyDisabledElementTakesNoInput);
        Run("Elements with no command, and action-bound elements, are never disabled", VerifyNoCommandIsNeverDisabled);
        return failures;
    }

    private static void VerifyCanExecuteSemantics()
    {
        var bindings = new UiBindings();
        bool allowed = true;
        bindings.BindCommand("gated", () => { }, () => allowed);
        bindings.BindCommand("open", () => { });
        bindings.BindCommand("refused", () => { }, () => false);

        Check(bindings.CanExecute("gated"), "a registered command whose predicate answers true may run");
        allowed = false;
        Check(!bindings.CanExecute("gated"), "and the same read follows the predicate when it changes");
        Check(bindings.CanExecute("open"), "a command bound with no predicate is always executable");
        Check(!bindings.CanExecute("refused"), "a command bound with a false predicate never is");
        Check(
            bindings.CanExecute("nobody-bound-this"),
            "an unregistered key answers true, because this is the veto a disabled decision consults rather than an existence check");

        bool threw = false;
        try
        {
            bindings.CanExecute(null!);
        }
        catch (ArgumentNullException)
        {
            threw = true;
        }

        Check(threw, "a null key is a caller error, as the rest of the surface is");
    }

    private static void VerifyInvokeRefusesDisabled()
    {
        var bindings = new UiBindings();
        int fired = 0;
        bool allowed = false;
        bindings.BindCommand("gated", () => fired++, () => allowed);
        bindings.BindCommand("open", () => fired++);

        bindings.Invoke("gated");
        Check(fired == 0, "invoking a disabled command runs nothing");

        allowed = true;
        bindings.Invoke("gated");
        Check(fired == 1, "and enabling it runs it once");

        bindings.Invoke("open");
        Check(fired == 2, "a command with no predicate is unaffected by any of this");

        bool missingThrew = false;
        try
        {
            bindings.Invoke("nobody-bound-this");
        }
        catch (KeyNotFoundException)
        {
            missingThrew = true;
        }

        Check(missingThrew, "a key nobody bound still throws rather than silently doing nothing");
    }

    private static void VerifyDisabledPlane()
    {
        UiTheme theme = UiTheme.DarkGold;
        var bindings = new UiBindings();
        bindings.BindCommand("gated", () => { }, () => false);
        bindings.BindCommand("open", () => { });
        var rect = new Rect(0f, 0f, 60f, 20f);

        ClearRecordedBoxes();
        UiThemeDraw.StatusTreatment(rect, theme, UiStatusTone.Active, writable: bindings.CanExecute("gated"));
        CheckPaint(0, theme.Base, theme.Divider, "a disabled command paints the disabled plane whatever tone was authored");

        ClearRecordedBoxes();
        UiThemeDraw.StatusTreatment(rect, theme, UiStatusTone.Active, writable: bindings.CanExecute("open"));
        CheckPaint(0, theme.Selected, theme.AccentGold, "an executable command keeps the authored tone");

        Check(
            !SameColor(theme.Base, theme.Selected) && !SameColor(theme.Divider, theme.AccentGold),
            "and the two treatments differ in every colour, so neither check above can pass by accident");

        ClearRecordedBoxes();
        UiThemeDraw.StatusBadge(rect, "value", theme, UiStatusTone.Danger, null, writable: bindings.CanExecute("gated"));
        CheckPaint(0, theme.Base, theme.Divider, "the badge plane follows the same single rule");
        CheckLastLabel(theme.TextDisabled, "and the badge writes the disabled text colour");
    }

    private static void VerifyDisabledElementTakesNoInput()
    {
        bool allowed = true;
        int fired = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("apply", () => fired++, () => allowed);

        using UiHost host = Host(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"apply\" Kind=\"input/button\" ActionBind=\"apply\" Text=\"Apply\" Height=\"28\" />"
            + "</UiPage>",
            bindings);

        var viewport = new Rect(0f, 0f, 240f, 120f);
        Vector2 point = new(20f, 14f);

        try
        {
            // Settle the arrangement with no event so the pumps below only exercise input.
            Event.current = null;
            host.DrawFrame(viewport);

            Pump(host, EventType.MouseDown, point);
            Check(
                GUIUtility.hotControl != 0,
                "an enabled element captures the hot control on MouseDown (the control the disabled case needs)");
            Pump(host, EventType.MouseUp, point);
            Check(fired == 1, "and the click fires its command exactly once");

            GUIUtility.hotControl = 0;
            allowed = false;
            Pump(host, EventType.MouseDown, point);
            Check(
                GUIUtility.hotControl == 0,
                "a disabled element does not capture the hot control at all: the pointer is never taken from what is beneath it");
            Pump(host, EventType.MouseUp, point);
            Check(fired == 1, "and its click executes nothing");

            allowed = true;
            Pump(host, EventType.MouseDown, point);
            Pump(host, EventType.MouseUp, point);
            Check(fired == 2, "re-enabling it puts the element back in charge of its own pointer");
        }
        finally
        {
            Event.current = null;
            GUIUtility.hotControl = 0;
            UiNative.ButtonOverride = null;
        }
    }

    private static void VerifyNoCommandIsNeverDisabled()
    {
        var bindings = new UiBindings();
        bindings.BindAction<float>("point-changed", value => actionPayload = value);
        bindings.BindCommand("gated", () => { }, () => false);

        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(
            Scope, ProbeKind, () => new ActionProbeWidget(), new[] { "Id", "Kind", "ActionBind", "Height" });

        using UiHost host = Host(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"column\" Gap=\"4\">"
            + "<Widget Id=\"probe\" Kind=\"" + ProbeKind + "\" ActionBind=\"point-changed\" Height=\"20\" />"
            + "<Widget Id=\"separator\" Kind=\"chrome/rule\" Thickness=\"1\" />"
            + "</Column>"
            + "</UiPage>",
            bindings);

        host.DrawFrame(new Rect(0f, 0f, 240f, 120f));

        UiNode probe = host.Session.GetNodeByElementId("probe")
            ?? throw new Exception("the action-bound probe was not arranged");
        Check(
            !probe.IsDisabled,
            "an ActionBind that names an action rather than a command is not a disabled decision");

        UiNode separator = host.Session.GetNodeByElementId("separator")
            ?? throw new Exception("the separator was not arranged");
        Check(!separator.IsDisabled, "and an element that declares no command is never disabled");

        UiNode? nobody = host.Session.GetNodeByElementId("not-in-this-page");
        Check(nobody == null, "an identity the definition does not declare has no node at all");
    }

    // --- helpers -------------------------------------------------------------------------------

    private static UiHost Host(string xml, UiBindings bindings)
    {
        return new UiHost(
            Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
    }

    /// <summary>
    /// One real event pass: the stub button model captures the hot control on MouseDown and activates on
    /// MouseUp with a matching control, so "the disabled element never took the pointer" is observable
    /// rather than an opinion about the code path.
    /// </summary>
    private static void Pump(UiHost host, EventType type, Vector2 point)
    {
        Event e = Event.KeyboardEvent("space");
        e.type = type;
        e.button = 0;
        e.mousePosition = point;
        Event.current = e;
        host.DrawFrame(new Rect(0f, 0f, 240f, 120f));
        Event.current = null;
    }

    private static void CheckPaint(int offset, Color fill, Color border, string name)
    {
        IList colors = RecordedBoxColors();
        if (colors.Count < offset + 5)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " recorded " + colors.Count
                + " solid(s); expected at least " + (offset + 5));
            return;
        }

        Check(SameColor((Color)colors[offset], fill), name + " (fill)");

        bool edgesMatch = true;
        for (int i = 1; i < 5; i++)
        {
            edgesMatch &= SameColor((Color)colors[offset + i], border);
        }

        Check(edgesMatch, name + " (border)");
    }

    private static void CheckLastLabel(Color expected, string name)
    {
        IList colors = RecordedLabelColors();
        if (colors.Count == 0)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " (no label was recorded)");
            return;
        }

        Check(SameColor((Color)colors[colors.Count - 1], expected), name);
    }

    private static void ClearRecordedBoxes()
    {
        Invoke("ClearDrawBoxSolidCalls");
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

    private static void Invoke(string name)
    {
        MethodInfo? method = typeof(Verse.Widgets).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        if (method == null) throw new Exception("Verse stub is missing " + name);
        method.Invoke(null, null);
    }

    private static bool SameColor(Color left, Color right)
    {
        return Near(left.r, right.r) && Near(left.g, right.g) && Near(left.b, right.b) && Near(left.a, right.a);
    }

    private static bool Near(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= 0.0001f;
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

    /// <summary>
    /// A kind whose binding is an action, not a command: its <c>ActionBind</c> names a key registered
    /// through <c>BindAction</c>, which is the shape that must not read as disabled.
    /// </summary>
    private sealed class ActionProbeWidget : IUiWidget
    {
        private UiElementSpec spec = UiElementSpec.Empty;

        string IUiWidget.Kind => ProbeKind;

        public void Configure(UiElementSpec spec)
        {
            this.spec = spec;
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
            bindings.ValidateAction<float>(spec.TryGetAttribute("ActionBind", out string key) ? key : "", elementPath);
        }

        public float Measure(UiWidgetContext ctx)
        {
            return 20f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            if (UiNative.Button(rect, ctx) && spec.TryGetAttribute("ActionBind", out string key))
            {
                ctx.Bindings.Invoke(key, 1f);
            }
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
