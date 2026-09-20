using System;
using System.Collections.Generic;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The option-level help contract, generalized off the one kind it started on. <c>HoverHelpKey</c> shipped on
/// <c>input/mode-row</c> alone; the consumer's shared dropdown-popup helper claims a help identity for the
/// hovered row too, so the same attribute now drives the popup's options and both kinds read it through one
/// implementation (<see cref="OptionHelp"/>) instead of two copies.
/// <para>
/// The identity a popup row publishes is the option's <b>value</b>: a dropdown's options come from the
/// consumer's data (an options binding, or the static <c>OptionN</c>/<c>ValueN</c> pairs), so the value is the
/// machine token a help catalog is keyed by — the same choice the mode row falls back to when a cell declares
/// no help text. The publication follows the same rule on both kinds: the hovered row's identity, cleared when
/// nothing is hovered, written only when the answer changes.
/// </para>
/// </summary>
internal static class KernelOptionHelpTests
{
    private const string Scope = "option-help-test";
    private const float Width = 240f;
    private const float OptionHeight = 24f;

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Both option-bearing kinds declare the same attribute", VerifyBothKindsDeclareIt);
        Run("The dropdown's sink is validated at creation", VerifySinkValidation);
        Run("Hovering a popup row publishes its value", VerifyPopupRowPublishes);
        Run("Leaving the rows clears it, and so does closing", VerifyClearing);
        Run("A re-hovered row writes nothing", VerifyNoChurn);
        ResetSeams();
        return failures;
    }

    private static void VerifyBothKindsDeclareIt()
    {
        PrepareCore();

        IReadOnlyCollection<string>? modeRow = UiWidgetRegistry.GetAttributeSchema(UiWidgetRegistry.CoreScope, InputModeRowWidget.Kind);
        IReadOnlyCollection<string>? dropdown = UiWidgetRegistry.GetAttributeSchema(UiWidgetRegistry.CoreScope, DropdownWidget.Kind);

        Check(modeRow != null && Contains(modeRow, OptionHelp.Attribute), "input/mode-row declares it");
        Check(dropdown != null && Contains(dropdown, OptionHelp.Attribute), "input/dropdown declares it, so it is a popup-level capability rather than one kind's special case");
    }

    private static void VerifySinkValidation()
    {
        PrepareCore();

        var choiceOnly = new UiBindings();
        choiceOnly.BindValue<string>("choice", () => "a", _ => { });
        Check(Reject(choiceOnly, "HoverHelpKey=\"nobody-bound-this\"", "nobody-bound-this"),
            "an unbound sink on the dropdown is refused at creation");

        var readOnly = new UiBindings();
        readOnly.BindValue<string>("choice", () => "a", _ => { });
        readOnly.BindReadOnly("published", () => "help");
        Check(Reject(readOnly, "HoverHelpKey=\"published\"", "not writable"),
            "a read-only sink is refused, because the publication is a write");

        var good = new UiBindings();
        good.BindValue<string>("choice", () => "a", _ => { });
        string help = "";
        good.BindValue<string>("help", () => help, value => help = value);
        using UiHost host = Host(good, "HoverHelpKey=\"help\"");
        Check(true, "a writable string sink creates");
    }

    private static void VerifyPopupRowPublishes()
    {
        Fixture fixture = Open();
        Check(fixture.Host.Session.IsPopupOpen("choice"), "the popup is open, so its rows are the ones being drawn");

        Hover(fixture, Row(1));
        Check(fixture.Help == "b", "hovering the second row publishes that option's value");

        Hover(fixture, Row(0));
        Check(fixture.Help == "a", "moving to the first row publishes its value instead");
    }

    private static void VerifyClearing()
    {
        Fixture fixture = Open();
        Hover(fixture, Row(1));
        Check(fixture.Help == "b", "the row's identity starts published");

        // Still open, pointer off the rows: the popup's own pass reports "nothing hovered".
        UiNative.DebugMousePosition = new Vector2(Width / 2f, 500f);
        fixture.Host.DrawFrame(Viewport());
        Check(fixture.Help == "", "leaving the rows clears the sink while the popup stays open");

        // Close it by clicking the trigger again.
        UiNative.DebugMousePositionEnabled = false;
        UiNative.ButtonOverride = rect => SameRect(rect, Trigger());
        fixture.Host.DrawFrame(Viewport());
        Check(!fixture.Host.Session.IsPopupOpen("choice"), "clicking the trigger closes the popup");
        UiNative.ButtonOverride = null;
        fixture.Host.DrawFrame(Viewport());
        Check(fixture.Help == "", "and a closed dropdown holds no option identity");
    }

    private static void VerifyNoChurn()
    {
        Fixture fixture = Open();
        Hover(fixture, Row(1));
        int afterFirst = fixture.Writes;
        Hover(fixture, Row(1));
        Check(fixture.Writes == afterFirst, "re-hovering the same row writes nothing: one write per transition, not per frame");
    }

    // --- fixture ----------------------------------------------------------------------------------

    private sealed class Fixture
    {
        public UiHost Host = null!;
        public string Help = "";
        public int Writes;
    }

    private static Fixture Open()
    {
        var fixture = new Fixture();
        var bindings = new UiBindings();
        bindings.BindValue<string>("choice", () => "a", _ => { });
        bindings.BindValue<string>("help", () => fixture.Help, value =>
        {
            fixture.Help = value;
            fixture.Writes++;
        });

        fixture.Host = Host(bindings, "HoverHelpKey=\"help\"");

        // Frame 1: click the trigger. The popup's rows live in Host window space below it.
        UiNative.ButtonOverride = rect => SameRect(rect, Trigger());
        fixture.Host.DrawFrame(Viewport());
        UiNative.ButtonOverride = null;
        UiNative.DebugMousePositionEnabled = true;
        return fixture;
    }

    private static void Hover(Fixture fixture, Rect row)
    {
        UiNative.DebugMousePosition = new Vector2(row.x + row.width / 2f, row.y + row.height / 2f);
        fixture.Host.DrawFrame(Viewport());
    }

    /// <summary>The trigger's window-space rect: the widget is the page root at the viewport origin.</summary>
    private static Rect Trigger() => new(0f, 0f, Width, OptionHeight);

    /// <summary>One popup row's window-space rect, straight from <see cref="UiPopup.RectFor"/> and the anchored
    /// popup geometry: the anchor is the trigger, the rows start at its bottom edge.</summary>
    private static Rect Row(int index)
    {
        Rect popup = UiPopup.RectFor(Trigger(), 2, Viewport());
        return new Rect(popup.x, popup.y + index * OptionHeight, popup.width, OptionHeight);
    }

    private static Rect Viewport() => new(0f, 0f, Width, 600f);

    private static UiHost Host(UiBindings bindings, string helpAttribute)
    {
        PrepareCore();

        return new UiHost(
            Scope,
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
                + "<Widget Id=\"dd\" Kind=\"input/dropdown\" Bind=\"choice\" Height=\"24\" " + helpAttribute
                + " Option1=\"Alpha\" Value1=\"a\" Option2=\"Beta\" Value2=\"b\" />"
                + "</UiPage>"),
            bindings,
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());
    }

    private static void PrepareCore()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
    }

    private static bool Reject(UiBindings bindings, string helpAttribute, string expectedInMessage)
    {
        try
        {
            using UiHost host = Host(bindings, helpAttribute);
            return false;
        }
        catch (UiContractException ex)
        {
            return ex.Message.IndexOf(expectedInMessage, StringComparison.Ordinal) >= 0;
        }
    }

    private static bool SameRect(Rect left, Rect right)
    {
        return Math.Abs(left.x - right.x) < 0.01f && Math.Abs(left.y - right.y) < 0.01f
            && Math.Abs(left.width - right.width) < 0.01f && Math.Abs(left.height - right.height) < 0.01f;
    }

    private static bool Contains(IReadOnlyCollection<string> set, string name)
    {
        foreach (string candidate in set)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static void ResetSeams()
    {
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMousePosition = default;
        UiNative.DebugMouseDown = false;
        UiNative.ButtonOverride = null;
        UiNative.TextFieldOverride = null;
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
}
