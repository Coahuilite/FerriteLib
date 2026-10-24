using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The option-level help contract, generalized off the one kind it started on. <c>HoverHelpKey</c> shipped on
/// <c>input/mode-row</c> alone; the consumer's shared dropdown-popup helper claims a help identity for the
/// hovered row too, so the same attribute now drives the popup's options and both kinds read it through one
/// implementation (<see cref="OptionHelp"/>) instead of two copies.
/// <para>
/// Each choice carries an explicit help identity. Migrated string/static declarations explicitly
/// preserve their old Value identity; typed choices can explicitly preserve their old Text identity.
/// Publication clears on leave/close and writes only when the hovered answer changes.
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
        Run("Typed choices preserve explicit legacy label help without converting their business values", VerifyTypedLabelHelp);
        foreach (string kind in new[] { DropdownWidget.Kind, InputModeRowWidget.Kind })
        {
            foreach (string source in new[] { "string", "typed", "XML" })
                Run(kind + " " + source + " omitted/empty Help clears actual publication", () => VerifyAbsentHelp(kind, source));
        }
        ResetSeams();
        return failures;
    }

    private static void VerifyAbsentHelp(string kind, string source)
    {
        PrepareCore();
        string help = "", stringValue = "a";
        int typedValue = 1, writes = 0, valueWrites = 0;
        var bindings = new UiBindings();
        if (source == "typed")
        {
            bindings.BindValue("choice", () => typedValue, value => { typedValue = value; valueWrites++; });
            bindings.BindChoices("choice", () => new[] {
                new UiChoice<int>("Explicit label", 1, "option-topic"),
                new UiChoice<int>("Omitted label", 2),
                new UiChoice<int>("Empty label", 3, help: "") });
        }
        else
        {
            bindings.BindValue("choice", () => stringValue, value => { stringValue = value; valueWrites++; });
            if (source == "string")
                bindings.BindChoices("choice", () => new[] {
                    new UiChoice<string>("Explicit label", "a", "option-topic"),
                    new UiChoice<string>("Omitted label", "b"),
                    new UiChoice<string>("Empty label", "c", help: "") });
        }
        bindings.BindValue("help", () => help, value => { help = value; writes++; });
        string choices = source == "XML"
            ? "<Choice Value='a' Text='Explicit label' Help='option-topic'/>"
              + "<Choice Value='b' Text='Omitted label'/><Choice Value='c' Text='Empty label' Help=''/>"
            : "";
        using var host = new UiHost(Scope, UiLayoutManifest.Parse(
            "<UiPage Schema='2' Source='" + Scope + "'><Widget Id='choice' Kind='" + kind
            + "' Bind='choice'" + (kind == DropdownWidget.Kind ? " Height='24'" : "")
            + " HoverHelpKey='help' HelpKey='element-topic'>"
            + choices + "</Widget></UiPage>"), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host.Session.HoverGraceFrames = 0;
        if (kind == DropdownWidget.Kind)
            NativeEvents.Click(Trigger().Center, () => NativeFixture.DrawFrame(host, Viewport()));

        void HoverOption(int index)
        {
            UiLayoutPoint point;
            if (kind == DropdownWidget.Kind)
            {
                UiLayoutRect popup = UiPopup.RectFor(Trigger(), 3, Viewport());
                point = new UiLayoutPoint(popup.X + popup.Width / 2, popup.Y + (index + 0.5f) * OptionHeight);
            }
            else
            {
                UiGeometry geometry = UiTheme.Vanilla.Geometry;
                point = new UiLayoutPoint(Width / 2, geometry.Spacing + index * (OptionHeight + geometry.Gap) + OptionHeight / 2);
            }
            NativeEvents.Begin(EventType.Repaint, point);
            NativeFixture.DrawFrame(host, Viewport());
        }

        foreach (int absent in new[] { 1, 2 })
        {
            HoverOption(0);
            Check(help == "option-topic", "explicit Help is really published before the no-help transition");
            int before = writes;
            HoverOption(absent);
            Check(help == "" && writes == before + 1,
                (absent == 1 ? "omitted" : "empty") + " Help clears the published topic, without Value/Text inference");
            Check(host.Session.HoverClaim == (kind == InputModeRowWidget.Kind ? "element-topic" : null),
                "no option claim is invented; a hovered mode row retains its element HelpKey");
            HoverOption(absent);
            Check(writes == before + 1, "remaining over a no-help option does not republish");
        }
        Check(valueWrites == 0 && typedValue == 1 && stringValue == "a", "help-only hover never writes a business selection");
    }

    private static void VerifyTypedLabelHelp()
    {
        PrepareCore();
        int value=1,writes=0;string help="";
        var bindings=new UiBindings();
        bindings.BindValue("typed",()=>value,v=>{value=v;writes++;});
        bindings.BindChoices("typed",()=>new[]{new UiChoice<int>("First label",1,"First label"),new UiChoice<int>("Second label",2,"Second label")});
        bindings.BindValue("help",()=>help,v=>help=v);
        using var host=new UiHost(Scope,UiLayoutManifest.Parse("<UiPage Schema='2' Source='"+Scope+"'><Widget Id='typed' Kind='input/dropdown' Bind='typed' Height='24' HoverHelpKey='help'/></UiPage>"),bindings,UiTheme.Vanilla,new StubMetrics(),new StubTranslation());
        NativeEvents.Click(Trigger().Center,()=>NativeFixture.DrawFrame(host,Viewport()));
        NativeEvents.Begin(EventType.Repaint,Row(1).Center);NativeFixture.DrawFrame(host,Viewport());
        Check(help=="Second label"&&value==1&&writes==0,"typed row publishes its explicitly preserved old Text identity, not its int value or a request");
        NativeEvents.Click(Row(1).Center,()=>NativeFixture.DrawFrame(host,Viewport()));
        Check(value==2&&writes==1,"the same typed row still dispatches the real business value exactly once");
        NativeEvents.Begin(EventType.Repaint,new UiLayoutPoint(-100,-100));NativeFixture.DrawFrame(host,Viewport());
        Check(help=="","typed explicit help clears after selection/leave like string/static help");
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
        NativeFixture.DebugMousePosition = new UiLayoutPoint(Width / 2f, 500f);
        NativeFixture.BeginDebugEvent();
        NativeFixture.DrawFrame(fixture.Host,Viewport());
        Check(fixture.Help == "", "leaving the rows clears the sink while the popup stays open");

        // Close it by clicking the trigger again.
        NativeEvents.Click(Trigger().Center,()=>NativeFixture.DrawFrame(fixture.Host,Viewport()));
        Check(!fixture.Host.Session.IsPopupOpen("choice"), "clicking the trigger closes the popup");
        NativeFixture.ButtonOverride = null;
        NativeFixture.BeginDebugEvent();
        NativeFixture.DrawFrame(fixture.Host,Viewport());
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
        NativeEvents.Click(Trigger().Center,()=>NativeFixture.DrawFrame(fixture.Host,Viewport()));
        NativeFixture.ButtonOverride = null;
        NativeFixture.DebugMousePositionEnabled = true;
        return fixture;
    }

    private static void Hover(Fixture fixture, UiLayoutRect row)
    {
        NativeFixture.DebugMousePosition = new UiLayoutPoint(row.X + row.Width / 2f, row.Y + row.Height / 2f);
        NativeFixture.BeginDebugEvent();
        NativeFixture.DrawFrame(fixture.Host,Viewport());
    }

    /// <summary>The trigger's window-space rect: the widget is the page root at the viewport origin.</summary>
    private static UiLayoutRect Trigger() => new(0f, 0f, Width, OptionHeight);

    /// <summary>One popup row's window-space rect, straight from <see cref="UiPopup.RectFor"/> and the anchored
    /// popup geometry: the anchor is the trigger, the rows start at its bottom edge.</summary>
    private static UiLayoutRect Row(int index)
    {
        UiLayoutRect popup = UiPopup.RectFor(Trigger(), 2, Viewport());
        return new UiLayoutRect(popup.X, popup.Y + index * OptionHeight, popup.Width, OptionHeight);
    }

    private static UiLayoutRect Viewport() => new(0f, 0f, Width, 600f);

    private static UiHost Host(UiBindings bindings, string helpAttribute)
    {
        PrepareCore();

        return new UiHost(
            Scope,
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\""+(Scope)+"\"><Widget Id=\"dd\" Kind=\"input/dropdown\" Bind=\"choice\" Height=\"24\" "+(helpAttribute)+"     ><Choice Value=\"a\" Text=\"Alpha\" Help=\"a\"/><Choice Value=\"b\" Text=\"Beta\" Help=\"b\"/></Widget></UiPage>"),
            bindings,
            UiTheme.Vanilla,
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

    private static bool SameRect(UiLayoutRect left, UiLayoutRect right)
    {
        return Math.Abs(left.X - right.X) < 0.01f && Math.Abs(left.Y - right.Y) < 0.01f
            && Math.Abs(left.Width - right.Width) < 0.01f && Math.Abs(left.Height - right.Height) < 0.01f;
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
        NativeFixture.DebugMousePositionEnabled = false;
        NativeFixture.DebugMousePosition = default;
        NativeFixture.DebugMouseDown = false;
        NativeFixture.ButtonOverride = null;
        NativeFixture.TextFieldOverride = null;
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
