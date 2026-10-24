using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

static partial class Program
{
    // No friend assembly or fixture state. These are real public author calls on the product Host.
    static void HelpContracts()
    {
        var bindings = new UiBindings();
        bindings.BindReadOnly("business/choice", () => "a");
        bindings.BindReadOnly("business/visible", () => true);
        bindings.BindReadOnly("business/width", () => 200f);
        bindings.BindReadOnly("business/selected", () => true);
        bindings.BindReadOnly("catalog/ref", () => "this binding is not the help topic");
        bindings.BindChoices("business/choice", () => new[] {
            new UiChoice<string>("Alpha", "a", helpRef: "catalog/option"),
            new UiChoice<string>("Beta", "b") });
        var page = UiLayoutManifest.Parse("<UiPage Schema='2' Source='help-author'><Column Id='root' Padding='0' Gap='0'><Widget Id='actual-element' Kind='input/mode-row' Bind='business/choice' VisibleBind='business/visible' WidthBind='business/width' AlignX='Left' SelectedBind='business/selected' HelpRef='catalog/general'/></Column></UiPage>");
        var execution = new Execution();
        using var host = new UiHost("help-author", page, bindings, new UiTheme(), new Metrics(), new Translation());
        var viewport = new UiLayoutRect(0, 0, 240, 600);
        void Pump(UiLayoutPoint pointer) => host.DrawFrame(viewport, new UiFrame(UiFramePhase.Repaint, pointer), execution);
        Pump(new UiLayoutPoint(20, 18));
        Check(host.Session.CurrentHelpRef == "catalog/option" && host.Session.CurrentHelpSource == host.Session.GetNodeByElementId("actual-element")!.Id,
            "public no-sink Session publication uses the actual element source and option-ref priority");
        var layout = host.MeasureAndArrange(new UiLayoutPoint(240, 600));
        Check(layout.RectById["actual-element"].Width == 200 && bindings.Get<float>(binding: "business/width") == 200,
            "binding addresses differ from real element identity; renamed named arguments read the right target");
        float secondY = new UiTheme().Geometry.Spacing + new UiTheme().Geometry.RowHeight + new UiTheme().Geometry.Gap + 12;
        Pump(new UiLayoutPoint(20, secondY));
        Check(host.Session.CurrentHelpRef == "catalog/general", "empty option HelpRef preserves the general element topic without a sink");
        host.Session.HelpGracePasses = 15;
        Pump(new UiLayoutPoint(-30, -30));
        Check(host.Session.CurrentHelpRef == null, "the inherited first-gap reset remains unchanged");
        for (int pass = 0; pass < 15; pass++)
        {
            Pump(new UiLayoutPoint(-30, -30));
            Check(host.Session.CurrentHelpRef == "catalog/general" && host.Session.CurrentHelpSource == host.Session.GetNodeByElementId("actual-element")!.Id,
                "effective current HelpRef/source retains the real US grace pass " + (pass + 1));
        }
        Pump(new UiLayoutPoint(-30, -30));
        Check(host.Session.CurrentHelpRef == null && host.Session.CurrentHelpSource == null, "the effective topic expires after all15 retained passes");
        Pump(new UiLayoutPoint(20, 18));
        Check(host.Session.CurrentHelpRef == "catalog/option", "a live option replaces held/general help on its actual pass");

        using var opaque = new UiHost("help-opaque", UiLayoutManifest.Parse("<UiPage Schema='2' Source='help-opaque'><Widget Id='identity' Kind='chrome/rule' Height='24' HelpRef='catalog/ref'/></UiPage>"), bindings, new UiTheme(), new Metrics(), new Translation());
        opaque.DrawFrame(viewport, new UiFrame(UiFramePhase.Repaint, new UiLayoutPoint(20, 1)), execution);
        Check(opaque.Session.CurrentHelpRef == "catalog/ref", "a HelpRef that resembles a binding address is not dereferenced as a binding or translated body");
        string payload = "";
        bindings.BindReadOnly("business/payload", () => "actual-item");
        bindings.BindAction<string>(actionBind: "business/action", action: value => payload = value);
        using var button = new UiHost("help-action", UiLayoutManifest.Parse("<UiPage Schema='2' Source='help-action'><Widget Id='not-the-action-address' Kind='input/button' ActionBind='business/action' PayloadBind='business/payload' Text='Run'/></UiPage>"), bindings, new UiTheme(), new Metrics(), new Translation());
        button.DrawFrame(viewport, new UiFrame(UiFramePhase.Repaint), execution);
        button.DrawFrame(viewport, new UiFrame(UiFramePhase.MouseDown, new UiLayoutPoint(20, 12)), execution);
        button.DrawFrame(viewport, new UiFrame(UiFramePhase.MouseUp, new UiLayoutPoint(20, 12)), execution);
        Check(payload == "actual-item", "PayloadBind/ActionBind resolve addresses rather than the element id");
        foreach (string old in new[] { "HelpKey='catalog/ref'", "VisibleKey='business/visible'", "HoverHelpKey='catalog/ref'" })
        {
            bool refused = false;
            try { using var invalid = new UiHost("help-invalid", UiLayoutManifest.Parse("<UiPage Schema='2' Source='help-invalid'><Widget Id='bad' Kind='chrome/rule' " + old + "/></UiPage>"), bindings, new UiTheme(), new Metrics(), new Translation()); }
            catch (UiContractException error) { refused = error.Message.Contains("bad"); }
            Check(refused, "retired spelling is located/diagnosed, not an alias: " + old);
        }
        HelpProxyOutputContracts();
    }

    sealed class HelpAccess : FerriteLib.UiKit.AuthorExamples.ForwardingBindings
    {
        internal int Gets, Sets, Endpoints;
        internal HelpAccess(UiBindings owner) : base(owner) { }
        public override T Get<T>(string key)
        {
            if (key == "help") Gets++;
            return base.Get<T>(key);
        }
        public override void Set<T>(string key, T value)
        {
            if (key == "help") Sets++;
            base.Set(key, value);
        }
        public override UiValueEndpoint Value(string key) { if (key == "help") Endpoints++; return base.Value(key); }
    }

    static void HelpProxyOutputContracts()
    {
        string help = ""; int writes = 0, clears = 0;
        Action? onRead = null;
        var owner = new UiBindings();
        owner.BindReadOnly("choice", () => "a");
        owner.BindChoices("choice", () => new[] { new UiChoice<string>("Alpha", "a", "same-ref") });
        owner.BindValue("help", () => { Action? callback = onRead; onRead = null; callback?.Invoke(); return help; },
            value => { help = value; writes++; if (value.Length == 0) clears++; });
        var access = new HelpAccess(owner);
        var manifest = UiLayoutManifest.Parse("<UiPage Schema='2' Source='help-proxy'><Widget Id='actual-element' Kind='input/mode-row' Bind='choice' HoveredOptionHelpOutputBind='help'/></UiPage>");
        var execution = new Execution();
        var viewport = new UiLayoutRect(0, 0, 240, 600);
        void Hover(UiHost host) => host.DrawFrame(viewport, new UiFrame(UiFramePhase.Repaint, new UiLayoutPoint(20, 18)), execution);
        using var a = new UiHost("help-proxy", manifest, access, new UiTheme(), new Metrics(), new Translation());
        Hover(a); a.SetDisplayDemand(false);
        Check(help == "" && writes == 2 && clears == 1 && access.Endpoints > 0 && access.Gets == 0 && access.Sets == 0,
            "public proxy supplies the owned endpoint for both publish/hide, matching the existing selection capability rather than a universal Get/Set interceptor");
        a.SetDisplayDemand(true); Hover(a); a.Close();
        Check(help == "" && writes == 4 && clears == 2, "actual close withdraws through the same endpoint and observable registered setter");

        using var b = new UiHost("help-proxy", manifest, access, new UiTheme(), new Metrics(), new Translation());
        using var c = new UiHost("help-proxy", manifest, access, new UiTheme(), new Metrics(), new Translation());
        Hover(b);
        onRead = () => Hover(c);
        Hover(b); // Get returns the same value, but the nested current publisher is now c.
        int beforeClears = clears;
        b.Close();
        Check(help == "same-ref" && clears == beforeClears, "same-value endpoint getter cannot overwrite a newer nested publisher; closing B leaves C output");
        c.Close();
        Check(help == "" && clears == beforeClears + 1, "the nested actual publisher still withdraws through its access-supplied endpoint");

        string nextHelp = "";
        var nextOwner = new UiBindings();
        nextOwner.BindReadOnly("choice", () => "a");
        nextOwner.BindChoices("choice", () => new[] { new UiChoice<string>("Alpha", "a", "next-ref") });
        nextOwner.BindValue("help", () => nextHelp, value => nextHelp = value);
        using var switched = new UiHost("help-proxy", manifest, access, new UiTheme(), new Metrics(), new Translation());
        Hover(switched); access.Current = nextOwner; Hover(switched);
        Check(help == "" && nextHelp == "next-ref", "active output permits same-Host backing switch: saved old endpoint withdraws old registry, newly supplied endpoint publishes new registry");
        switched.Close();
        Check(nextHelp == "" && access.Gets == 0 && access.Sets == 0, "both registries use the declared sealed endpoint capability consistently, without a new proxy-capture API or switch prerequisite");
        access.Current = owner;

        help = "consumer-initial";
        using var idle = new UiHost("help-proxy", manifest, access, new UiTheme(), new Metrics(), new Translation());
        idle.DrawFrame(viewport, new UiFrame(UiFramePhase.Repaint, new UiLayoutPoint(-30, -30)), execution);
        idle.Close();
        Check(help == "consumer-initial", "an unowned initial/consumer value is not claimed as current hover or erased by idle cleanup");
    }
}
