using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The option-level help contract, generalized off the one kind it started on. <c>HoveredOptionHelpOutputBind</c> shipped on
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
        Run("Actual output withdraws on empty choices, reliable hide and close", VerifyOutputLifetime);
        Run("Shared output takes publisher identity even for the same topic", VerifySharedOutput);
        Run("Consumer writes are not owned by an old publisher", VerifyConsumerOutput);
        Run("Element hide/removal and owner switch withdraw only their output", VerifyElementAndBackingLifetime);
        Run("Tracked output is observable and failures do not replay", VerifyObservableOutput);
        Run("Choice completion precedes a failing help withdrawal", VerifyChoiceCompletion);
        foreach (string kind in new[] { DropdownWidget.Kind, InputModeRowWidget.Kind })
        {
            foreach (string source in new[] { "string", "typed", "XML" })
                Run(kind + " " + source + " omitted/empty Help clears actual publication", () => VerifyAbsentHelp(kind, source));
        }
        ResetSeams();
        return failures;
    }

    private static void VerifyOutputLifetime()
    {
        PrepareCore();
        string help = "";
        IReadOnlyList<UiChoice<string>> choices = new[] { new UiChoice<string>("Alpha", "a", "topic/a") };
        var bindings = new UiBindings();
        bindings.BindValue("choice", () => "a", _ => { });
        bindings.BindChoices("choice", () => choices);
        bindings.BindValue("help", () => help, value => help = value);
        using var host = new UiHost(Scope, UiLayoutManifest.Parse(
            "<UiPage Schema='2' Source='" + Scope + "'><Widget Id='choice' Kind='input/dropdown' Bind='choice' Height='24' HoveredOptionHelpOutputBind='help'/></UiPage>"),
            bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        void HoverRow()
        {
            NativeEvents.Begin(EventType.Repaint, Row(0).Center);
            NativeFixture.DrawFrame(host, Viewport());
        }
        NativeEvents.Click(Trigger().Center, () => NativeFixture.DrawFrame(host, Viewport()));
        HoverRow();
        Check(help == "topic/a", "real popup publishes before its choices disappear");
        choices = Array.Empty<UiChoice<string>>();
        bindings.NotifyChanged("choice");
        HoverRow();
        Check(help == "", "empty choices withdraw the previous output");
        choices = new[] { new UiChoice<string>("Alpha", "a", "topic/a") };
        bindings.NotifyChanged("choice");
        NativeEvents.Click(Trigger().Center, () => NativeFixture.DrawFrame(host, Viewport()));
        HoverRow();
        Check(help == "topic/a", "restored choices publish again");
        host.SetDisplayDemand(false);
        Check(help == "", "reliably hiding the Host withdraws its output without destroying the Host");
        host.SetDisplayDemand(true);
        NativeEvents.Begin(EventType.Repaint, Trigger().Center);
        NativeFixture.DrawFrame(host, Viewport());
        NativeEvents.Click(Trigger().Center, () => NativeFixture.DrawFrame(host, Viewport()));
        HoverRow();
        Check(help == "topic/a", "the same Host republishes after becoming visible again");
        host.Close();
        Check(help == "", "closing withdraws the last owned output");
    }

    private static UiLayoutManifest ModePage(string extra = "") => UiLayoutManifest.Parse(
        "<UiPage Schema='2' Source='" + Scope + "'><Widget Id='mode' Kind='input/mode-row' Bind='choice'"
        + " HoveredOptionHelpOutputBind='help' HelpRef='general' " + extra + "/></UiPage>");

    private static UiBindings ModeBindings(Func<string> readHelp, Action<string> writeHelp)
    {
        var bindings = new UiBindings();
        bindings.BindReadOnly("choice", () => "a");
        bindings.BindChoices("choice", () => new[] { new UiChoice<string>("Alpha", "a", "same-topic") });
        bindings.BindValue("help", readHelp, writeHelp);
        return bindings;
    }

    private static UiHost ModeHost(IUiBindingAccess bindings, string extra = "") =>
        new(Scope, ModePage(extra), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

    private static void HoverMode(UiHost host)
    {
        NativeEvents.Begin(EventType.Repaint, new UiLayoutPoint(30, UiTheme.Vanilla.Geometry.Spacing + 12));
        NativeFixture.DrawFrame(host, Viewport());
    }

    private static void VerifySharedOutput()
    {
        PrepareCore();
        string help = ""; int writes = 0;
        var bindings = ModeBindings(() => help, value => { help = value; writes++; });
        using var a = ModeHost(bindings);
        using var b = ModeHost(bindings);
        HoverMode(a); HoverMode(b);
        Check(help == "same-topic" && writes == 1, "B acquires same-value output without another setter call");
        a.Close();
        Check(help == "same-topic" && writes == 1, "closing A cannot clear same-address/same-token B output");
        b.Close();
        Check(help == "" && writes == 2, "closing the actual publisher withdraws exactly once");
        using var reopened = ModeHost(bindings);
        HoverMode(reopened);
        Check(help == "same-topic", "a new Host republishes rather than inheriting dead ownership");
    }

    private static void VerifyConsumerOutput()
    {
        foreach (string consumer in new[] { "consumer-topic", "same-topic" })
        {
            PrepareCore();
            string help = ""; int writes = 0;
            var bindings = ModeBindings(() => help, value => { help = value; writes++; });
            using var host = ModeHost(bindings);
            HoverMode(host);
            bindings.Set("help", consumer);
            int before = writes;
            host.Close();
            Check(help == consumer && writes == before, "old publisher cannot clear a consumer Set, including the same value");
        }
        PrepareCore();
        string external = "";
        var announced = ModeBindings(() => external, value => external = value);
        using var owner = ModeHost(announced);
        HoverMode(owner); announced.NotifyChanged("help"); owner.Close();
        Check(external == "same-topic", "a consumer announcement also relinquishes the old output ownership");
    }

    private static void VerifyElementAndBackingLifetime()
    {
        PrepareCore();
        string old = "", next = ""; bool visible = true;
        var first = ModeBindings(() => old, value => old = value);
        first.BindReadOnly("visible", () => visible);
        var second = ModeBindings(() => next, value => next = value);
        second.BindReadOnly("visible", () => true);
        var proxy = new FerriteLib.UiKit.AuthorExamples.ForwardingBindings(first);
        using var host = ModeHost(proxy, "VisibleBind='visible'");
        HoverMode(host); visible = false; first.NotifyChanged("visible"); HoverMode(host);
        Check(old == "" && host.Session.IsActive, "hidden element withdraws output without destroying its Session");
        visible = true; first.NotifyChanged("visible"); HoverMode(host);
        Check(old == "same-topic", "same hidden node can publish after restoration");
        proxy.Current = new UiBindings();
        bool refused = false;
        try { host.MeasureAndArrange(new UiLayoutPoint(240, 600)); } catch (UiContractException) { refused = true; }
        proxy.Current = first;
        Check(refused && old == "same-topic", "failed backing validation does not withdraw the valid old output");
        HoverMode(host);
        proxy.Current = second; HoverMode(host);
        Check(old == "" && next == "same-topic", "successful same-Host backing switch releases only the old registry and publishes to the new one");
        host.UpdateDefinition(UiLayoutManifest.Parse("<UiPage Schema='2' Source='" + Scope + "'><Widget Id='other' Kind='chrome/rule'/></UiPage>"));
        Check(next == "", "definition removal withdraws output at the actual commit");
    }

    private static void VerifyObservableOutput()
    {
        PrepareCore();
        var runtime = new UiReactiveRuntime();
        var cell = runtime.Text("");
        using var observer = runtime.CreateOwner("help-observer");
        var seen = new List<string>(); observer.Respond("help", () => seen.Add(cell.Value));
        var bindings = new UiBindings(runtime);
        bindings.BindReadOnly("choice", () => "a");
        bindings.BindChoices("choice", () => new[] { new UiChoice<string>("Alpha", "a", "same-topic") });
        bindings.BindTracked("help", () => cell.Value, value => cell.Value = value);
        using var host = ModeHost(bindings);
        HoverMode(host); host.SetDisplayDemand(false);
        Check(seen.Count == 3 && seen[0] == "" && seen[1] == "same-topic" && seen[2] == "", "actual tracked output subscriber observes publish then lifecycle withdrawal");

        string help = ""; int writes = 0;
        var faulty = ModeBindings(() => help, value => { writes++; help = value; if (value.Length == 0) throw new InvalidOperationException("output-cleanup-fault"); });
        using var failed = ModeHost(faulty); HoverMode(failed);
        bool threw = false;
        try { failed.Close(); } catch (AggregateException) { threw = true; }
        failed.Close();
        Check(threw && !failed.Session.IsActive && writes == 2, "cleanup failure is reported after close and never replays either business write");
    }

    private static void VerifyChoiceCompletion()
    {
        PrepareCore();
        string value = "a", help = ""; int requests = 0, clears = 0;
        var bindings = new UiBindings();
        bindings.BindValue("choice", () => value, next => { value = next; requests++; });
        bindings.BindValue("help", () => help, next => { help = next; if (next.Length == 0) { clears++; throw new InvalidOperationException("help-clear-fault"); } });
        using var host = Host(bindings, "HoveredOptionHelpOutputBind='help'");
        NativeEvents.Click(Trigger().Center, () => NativeFixture.DrawFrame(host, Viewport()));
        NativeEvents.Begin(EventType.Repaint, Row(1).Center); NativeFixture.DrawFrame(host, Viewport());
        bool threw = false;
        try { NativeEvents.Click(Row(1).Center, () => NativeFixture.DrawFrame(host, Viewport())); }
        catch (InvalidOperationException error) { threw = error.Message == "help-clear-fault"; }
        Check(threw && value == "b" && requests == 1 && clears == 1 && !host.Session.IsPopupOpen("choice"),
            "real selection completes once and closes before its output-cleanup failure is reported");
        NativeEvents.Begin(EventType.Repaint, new UiLayoutPoint(-20, -20)); NativeFixture.DrawFrame(host, Viewport());
        Check(requests == 1 && clears == 1, "later paint does not replay the completed selection or failed cleanup");
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
                new UiChoice<int>("Empty label", 3, helpRef: "") });
        }
        else
        {
            bindings.BindValue("choice", () => stringValue, value => { stringValue = value; valueWrites++; });
            if (source == "string")
                bindings.BindChoices("choice", () => new[] {
                    new UiChoice<string>("Explicit label", "a", "option-topic"),
                    new UiChoice<string>("Omitted label", "b"),
                    new UiChoice<string>("Empty label", "c", helpRef: "") });
        }
        bindings.BindValue("help", () => help, value => { help = value; writes++; });
        string choices = source == "XML"
            ? "<Choice Value='a' Text='Explicit label' HelpRef='option-topic'/>"
              + "<Choice Value='b' Text='Omitted label'/><Choice Value='c' Text='Empty label' HelpRef=''/>"
            : "";
        using var host = new UiHost(Scope, UiLayoutManifest.Parse(
            "<UiPage Schema='2' Source='" + Scope + "'><Widget Id='choice' Kind='" + kind
            + "' Bind='choice'" + (kind == DropdownWidget.Kind ? " Height='24'" : "")
            + " HoveredOptionHelpOutputBind='help' HelpRef='element-topic'>"
            + choices + "</Widget></UiPage>"), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host.Session.HelpGracePasses = 0;
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
            Check(host.Session.CurrentHelpRef == "element-topic",
                "no option claim is invented; both kinds fall back to their element HelpRef");
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
        using var host=new UiHost(Scope,UiLayoutManifest.Parse("<UiPage Schema='2' Source='"+Scope+"'><Widget Id='typed' Kind='input/dropdown' Bind='typed' Height='24' HoveredOptionHelpOutputBind='help'/></UiPage>"),bindings,UiTheme.Vanilla,new StubMetrics(),new StubTranslation());
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
        Check(Reject(choiceOnly, "HoveredOptionHelpOutputBind=\"nobody-bound-this\"", "nobody-bound-this"),
            "an unbound sink on the dropdown is refused at creation");

        var readOnly = new UiBindings();
        readOnly.BindValue<string>("choice", () => "a", _ => { });
        readOnly.BindReadOnly("published", () => "help");
        Check(Reject(readOnly, "HoveredOptionHelpOutputBind=\"published\"", "not writable"),
            "a read-only sink is refused, because the publication is a write");

        var good = new UiBindings();
        good.BindValue<string>("choice", () => "a", _ => { });
        string help = "";
        good.BindValue<string>("help", () => help, value => help = value);
        using UiHost host = Host(good, "HoveredOptionHelpOutputBind=\"help\"");
        Check(true, "a writable string sink creates");
    }

    private static void VerifyPopupRowPublishes()
    {
        Fixture fixture = Open();
        Check(fixture.Host.Session.IsPopupOpen("choice"), "the popup is open, so its rows are the ones being drawn");

        Hover(fixture, Row(1));
        Check(fixture.HelpRef == "b", "hovering the second row publishes that option's value");

        Hover(fixture, Row(0));
        Check(fixture.HelpRef == "a", "moving to the first row publishes its value instead");
    }

    private static void VerifyClearing()
    {
        Fixture fixture = Open();
        Hover(fixture, Row(1));
        Check(fixture.HelpRef == "b", "the row's identity starts published");

        // Still open, pointer off the rows: the popup's own pass reports "nothing hovered".
        NativeFixture.DebugMousePosition = new UiLayoutPoint(Width / 2f, 500f);
        NativeFixture.BeginDebugEvent();
        NativeFixture.DrawFrame(fixture.Host,Viewport());
        Check(fixture.HelpRef == "", "leaving the rows clears the sink while the popup stays open");

        // Close it by clicking the trigger again.
        NativeEvents.Click(Trigger().Center,()=>NativeFixture.DrawFrame(fixture.Host,Viewport()));
        Check(!fixture.Host.Session.IsPopupOpen("choice"), "clicking the trigger closes the popup");
        NativeFixture.ButtonOverride = null;
        NativeFixture.BeginDebugEvent();
        NativeFixture.DrawFrame(fixture.Host,Viewport());
        Check(fixture.HelpRef == "", "and a closed dropdown holds no option identity");
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
        public string HelpRef = "";
        public int Writes;
    }

    private static Fixture Open()
    {
        var fixture = new Fixture();
        var bindings = new UiBindings();
        bindings.BindValue<string>("choice", () => "a", _ => { });
        bindings.BindValue<string>("help", () => fixture.HelpRef, value =>
        {
            fixture.HelpRef = value;
            fixture.Writes++;
        });

        fixture.Host = Host(bindings, "HoveredOptionHelpOutputBind=\"help\"");

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
                "<UiPage Schema=\"2\" Source=\""+(Scope)+"\"><Widget Id=\"dd\" Kind=\"input/dropdown\" Bind=\"choice\" Height=\"24\" "+(helpAttribute)+"     ><Choice Value=\"a\" Text=\"Alpha\" HelpRef=\"a\"/><Choice Value=\"b\" Text=\"Beta\" HelpRef=\"b\"/></Widget></UiPage>"),
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
