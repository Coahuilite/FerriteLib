using System;
using System.Collections;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Writability lane: the read side that turns "this element is read-only" into an appearance. The
/// kernel has always carried <c>BindReadOnly</c> — the wired consumer publishes six values that way —
/// but a widget could not ask whether a value may be written, so the disabled treatment had no producer:
/// it existed only as a tone a caller could pick by hand. This lane holds the read's semantics
/// (read-only false, writable true, unbound false, null a caller error), that the read agrees with what
/// <c>Set</c> actually enforces, and that a read-only binding drives the disabled plane and text through
/// the outlets while a writable one keeps the authored tone. Colours come from the Verse stub's recorded
/// draw calls, so a broken chain shows up as a wrong pixel rather than as a code-reading opinion.
/// </summary>
internal static class KernelWritabilityTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("The interface publishes the writability read", VerifyInterfacePublishesTheRead);
        Run("Read-only, writable and unbound keys answer as documented", VerifyReadSemantics);
        Run("The read is what Set enforces", VerifyReadPredictsSet);
        Run("A read-only binding paints the disabled treatment", VerifyReadOnlyPaintsDisabled);
        Run("A caller that does not ask keeps the authored tone", VerifyUnknownWritabilityKeepsTone);
        return failures;
    }

    private static void VerifyInterfacePublishesTheRead()
    {
        MethodInfo? member = typeof(IUiBindings).GetMethod("IsWritable");
        Check(
            member != null && member.ReturnType == typeof(bool),
            "IUiBindings exposes a bool IsWritable(string) read (the breaking interface addition)");
        Check(typeof(UiBindings).GetMethod("IsWritable") != null, "the reference implementation carries it");
    }

    private static void VerifyReadSemantics()
    {
        var bindings = new UiBindings();
        float value = 1f;
        bindings.BindValue("editable", () => value, updated => value = updated);
        bindings.BindReadOnly("published", () => value);

        Check(bindings.IsWritable("editable"), "a BindValue key is writable");
        Check(!bindings.IsWritable("published"), "a BindReadOnly key is not");
        Check(!bindings.IsWritable("nobody-bound-this"), "an unbound key answers false (the conservative direction)");
        Check(!bindings.IsWritable(""), "an empty key answers false");

        bool threw = false;
        try
        {
            bindings.IsWritable(null!);
        }
        catch (ArgumentNullException)
        {
            threw = true;
        }

        Check(threw, "a null key is a caller error and throws, as the rest of the surface does");
    }

    private static void VerifyReadPredictsSet()
    {
        var bindings = new UiBindings();
        float value = 1f;
        bindings.BindReadOnly("published", () => value);
        bindings.BindValue("editable", () => value, updated => value = updated);

        bool blocked = false;
        try
        {
            bindings.Set("published", 2f);
        }
        catch (InvalidOperationException)
        {
            blocked = true;
        }

        Check(
            !bindings.IsWritable("published") && blocked,
            "the read is the answer Set enforces: a read-only key refuses the write it says it will refuse");

        bindings.Set("editable", 3f);
        Check(
            bindings.IsWritable("editable") && Near(value, 3f),
            "the writable key writes, and the read said so");
    }

    private static void VerifyReadOnlyPaintsDisabled()
    {
        UiTheme theme = UiTheme.DarkGold;
        var bindings = new UiBindings();
        bindings.BindReadOnly("published", () => 1f);
        bindings.BindValue("editable", () => 1f, _ => { });

        // The producer this task owes: an authored tone plus a read-only binding resolves to Disabled.
        ClearRecordedBoxes();
        UiThemeDraw.StatusTreatment(
            new Rect(0f, 0f, 60f, 20f), theme, UiStatusTone.Active, writable: bindings.IsWritable("published"));
        CheckPaint(0, theme.Base, theme.Divider, "a read-only binding paints the disabled plane whatever tone was authored");

        ClearRecordedBoxes();
        UiThemeDraw.StatusTreatment(
            new Rect(0f, 0f, 60f, 20f), theme, UiStatusTone.Active, writable: bindings.IsWritable("editable"));
        CheckPaint(0, theme.Selected, theme.AccentGold, "a writable binding keeps the authored tone");

        Check(
            !SameColor(theme.Base, theme.Selected) && !SameColor(theme.Divider, theme.AccentGold),
            "the two treatments differ in every colour, so the two checks above cannot both pass by accident");

        ClearRecordedBoxes();
        UiThemeDraw.StatusBadge(
            new Rect(0f, 0f, 60f, 20f), "value", theme, UiStatusTone.Danger, null, writable: bindings.IsWritable("published"));
        CheckPaint(0, theme.Base, theme.Divider, "the badge plane follows the same rule");
        CheckLastLabel(theme.TextDisabled, "and the badge writes the disabled text colour");
    }

    private static void VerifyUnknownWritabilityKeepsTone()
    {
        UiTheme theme = UiTheme.DarkGold;

        ClearRecordedBoxes();
        UiThemeDraw.StatusTreatment(new Rect(0f, 0f, 60f, 20f), theme, UiStatusTone.Danger);
        CheckPaint(0, theme.Danger, theme.Danger, "no writability argument means the tone decides alone");

        ClearRecordedBoxes();
        UiThemeDraw.StatusBadge(new Rect(0f, 0f, 60f, 20f), "value", theme, UiStatusTone.Danger);
        CheckPaint(0, theme.Danger, theme.Danger, "and the same holds for the badge");
    }

    /// <summary>
    /// Asserts the five solids one painted surface emits — the fill plus four edges — start at
    /// <paramref name="offset"/> in the recorder.
    /// </summary>
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

        Color actual = (Color)colors[colors.Count - 1];
        Check(SameColor(actual, expected), name + " (expected " + Describe(expected) + ", got " + Describe(actual) + ")");
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

    private static IList RecordedLabelColors()
    {
        FieldInfo? colors = typeof(Verse.Widgets).GetField(
            "LabelColors", BindingFlags.Public | BindingFlags.Static);
        if (colors == null) throw new Exception("Verse stub is missing LabelColors");
        return (IList)colors.GetValue(null)!;
    }

    private static bool SameColor(Color left, Color right)
    {
        return Near(left.r, right.r) && Near(left.g, right.g) && Near(left.b, right.b) && Near(left.a, right.a);
    }

    private static bool Near(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= 0.0001f;
    }

    private static string Describe(Color color)
    {
        return "(" + color.r + ", " + color.g + ", " + color.b + ", " + color.a + ")";
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
}
