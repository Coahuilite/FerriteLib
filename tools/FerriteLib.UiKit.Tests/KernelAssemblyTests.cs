using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

internal static class KernelAssemblyTests
{
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("  ok: ASSEMBLY " + name);
    }
    public static int RunAll()
    {
        try
        {
            UiWidgetRegistry.Clear();
            UiWidgetRegistry.InitializeCore();
            ProgramAndUpdate();
            KeyedBindingRoles();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: ASSEMBLY " + ex);
            return 1;
        }
        finally
        {
            Event.current = null;
            GUIUtility.hotControl = 0;
            UiNative.DebugMousePositionEnabled = false;
        }
    }
    private static UiElementSpec Spec(string id, string kind, params (string Key, string Value)[] attributes)
        => new(id, kind, attributes.ToDictionary(pair => pair.Key, pair => pair.Value));
    private static UiLayoutManifest Definition(string label)
        => UiLayoutManifest.Create("assembly", new[] { new UiElementSpec("root", "Stack",
            new Dictionary<string, string> { ["Padding"] = "0" }, new[] {
                Spec("field", "input/number-field", ("Bind", "value"), ("Max", "100"), ("Label", label)) }) });
    private static void ProgramAndUpdate()
    {
        float model = 5;
        int writes = 0;
        var bindings = new UiBindings();
        bindings.BindValue("value", () => model, value => { writes++; model = value; });
        bindings.BindCommand("action", () => { });
        var theme = UiTheme.Vanilla;
        var program = Definition("Value");
        using var host = new UiHost("assembly", program, bindings, theme, new Metrics(), new Translation());
        UiLayoutSnapshot before = host.MeasureAndArrange(new Vector2(300, 300));
        using var xmlHost = new UiHost("assembly", UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"assembly\"><Stack Id=\"root\" Padding=\"0\"><Widget Id=\"field\" Kind=\"input/number-field\" Bind=\"value\" Max=\"100\" Label=\"Value\"/></Stack></UiPage>"), bindings, UiTheme.Vanilla, new Metrics(), new Translation());
        UiLayoutSnapshot xml = xmlHost.MeasureAndArrange(new Vector2(300, 300));
        Check(before.RectById["field"].Equals((object)xml.RectById["field"]), "ordinary C# definitions and XML use the same actual component/host geometry");
        UiNode node = host.Session.GetNodeByElementId("field")!;
        UiNode previous = host.Session.EnterNode(node);
        var numeric = new UiNumericEdit(0, 100);
        UiEditSession<float> edit;
        UiValueState state;
        try
        {
            edit = UiNative.Editor<float>(host.CreateContext(300).WithNode(node), "value", numeric.Format, numeric.Parse);
            edit.Observe(model);
            edit.Focus();
            edit.Input("20", true, false, false, value => { writes++; model = value; }, () => model);
            state = node.GetOrCreateState("value");
            host.Session.NoteEditOpened(state);
        }
        finally { host.Session.EnterNode(previous); }
        bool refused = false;
        try { host.UpdateDefinition(UiLayoutManifest.Create("assembly", new[] { Spec("field", "input/text-field", ("Bind", "value")) })); }
        catch (UiContractException) { refused = true; }
        Check(refused && ReferenceEquals(host.Manifest, program) && ReferenceEquals(state.Editor, edit) && edit.Draft == "20" && writes == 0,
            "program update reuses typed preflight and preserves the real old draft/tree on refusal");
        var cycle = new UiElementSpec("positioned", "Overlay", children: new[] {
            Spec("a", "chrome/rule", ("AnchorX", "b.Right")), Spec("b", "chrome/rule", ("AnchorX", "a.Right")) });
        refused = false;
        try { host.UpdateDefinition(UiLayoutManifest.Create("assembly", new[] { cycle })); }
        catch (UiContractException ex) { refused = ex.Message.Contains("Cyclic") && ex.ElementPath.Contains("positioned"); }
        Check(refused && ReferenceEquals(host.Manifest, program) && ReferenceEquals(state.Editor, edit),
            "new finite relation cycles are preflighted by the shared production graph before replacing a live definition");
        theme.DefaultFont = UiFont.Medium;
        theme[UiPaletteRole.Foreground] = new Color(0, 1, 1, 1);
        var badTemplate = Spec("bad-template", "unregistered/atom");
        var candidate = UiLayoutManifest.Create("assembly", program.Roots,
            UiStyleDocument.Parse("<Styles Schema=\"1\" Scheme=\"blue\"><Scheme Name=\"blue\"><Palette Role=\"PanelSurface\" Value=\"#0000FF\"/></Scheme></Styles>"),
            new Dictionary<string, UiElementSpec> { ["bad-template"] = badTemplate });
        refused = false;
        try { host.UpdateDefinition(candidate); }
        catch (UiContractException) { refused = true; }
        Check(refused && ReferenceEquals(host.Manifest, program) && host.DefinitionRevision == 0
            && theme.DefaultFont == UiFont.Medium && theme.HasFontOverride && !theme.HasDensityOverride
            && theme[UiPaletteRole.Foreground] == new Color(0, 1, 1, 1) && ReferenceEquals(state.Editor, edit),
            "failed real staging restores exact current theme claims and old tree, without promoting density or destroying draft");
        host.UpdateDefinition(Definition("Changed"));
        UiLayoutSnapshot updated = host.MeasureAndArrange(new Vector2(300, 300));
        Check(ReferenceEquals(node, host.Session.GetNodeByElementId("field")) && ReferenceEquals(state.Editor, edit)
            && edit.Draft == "20" && host.DefinitionRevision == 1 && updated.DefinitionRevision == 1 && writes == 0,
            "compatible definition update retains actual instance/editor and advances only accepted definition identity");
        host.UpdateDefinition(UiLayoutManifest.Create("assembly", new[] { Spec("field", "input/button", ("ActionBind", "action"), ("Text", "Apply")) }));
        Check(state.Editor == null && host.Session.ActiveEditState == null && !edit.Focused && writes == 0,
            "successful kind replacement disconnects and ends old editor ownership without a commit");
        refused = false;
        try { UiLayoutManifest.Create("assembly", new[] { Spec("same", "chrome/rule"), Spec("same", "chrome/rule") }); }
        catch (FormatException) { refused = true; }
        Check(refused, "program assembly refuses duplicate identities using the declaration contract");
        refused = false;
        try { UiLayoutManifest.Create("assembly", new[] { new UiElementSpec("unused", "input/button", children: new[] { Spec("nested", "chrome/rule") }) }); }
        catch (FormatException) { refused = true; }
        Check(refused, "program assembly rejects widget children the actual control cannot consume");
    }

    private static void KeyedBindingRoles()
    {
        var keys = new List<string> { "a", "b" };
        var requests = new List<string>();
        var help = new Dictionary<string, string> { ["a"] = "stale", ["b"] = "stale" };
        var bindings = new UiBindings();
        bindings.BindReadOnly<IReadOnlyList<string>>("items", () => keys, UiInvalidation.Structure);
        bindings.BindReadOnly("width", () => 120f);
        bindings.BindReadOnly<IReadOnlyList<string>>("pointkeys", () => new[] { "GLOBAL", "GLOBAL-END" });
        string globalHelp = "global";
        bindings.BindValue("help", () => globalHelp, value => globalHelp = value);
        bindings.BindAction<string>("check", _ => requests.Add("GLOBAL-check"));
        bindings.BindAction<string>("disclose", _ => requests.Add("GLOBAL-disclose"));
        foreach (string item in keys)
        {
            string key = item;
            string prefix = "items." + key + ".";
            bindings.BindReadOnly<IReadOnlyList<UiTreeRow>>(prefix + "tree", () => new[] { new UiTreeRow("node", 0, key, expandable: true, checkable: true) });
            bindings.BindAction<string>(prefix + "check", row => requests.Add("check:" + key + ":" + row));
            bindings.BindAction<string>(prefix + "disclose", row => requests.Add("disclose:" + key + ":" + row));
            bindings.BindReadOnly<IReadOnlyList<Vector2>>(prefix + "points", () => new[] { new Vector2(.5f, .5f), new Vector2(1, 0) });
            bindings.BindReadOnly<IReadOnlyList<string>>(prefix + "pointkeys", () => new[] { key + "point", key + "end" });
            bindings.BindAction<UiChartPointChange>(prefix + "change", change => requests.Add("point:" + key + ":" + change.Key));
            bindings.BindValue(prefix + "choice", () => "A", _ => { });
            bindings.BindValue(prefix + "help", () => help[key], value => help[key] = value);
            bindings.BindReadOnly(prefix + "width", () => key == "a" ? 45f : 65f);
        }
        const string page = "<UiPage Schema=\"2\" Source=\"assembly\"><Templates><Column Id=\"item\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"tree\" Kind=\"container/tree\" Bind=\"tree\" CheckBind=\"check\" DisclosureBind=\"disclose\"/>"
            + "<Widget Id=\"chart\" Kind=\"chart/line\" Bind=\"points\" PointKeysBind=\"pointkeys\" ActionBind=\"change\" Height=\"60\"/>"
            + "<Widget Id=\"choice\" Kind=\"input/dropdown\" Bind=\"choice\" HoverHelpKey=\"help\" Option1=\"A\"/>"
            + "<Row Id=\"width-row\" Padding=\"0\"><Widget Id=\"caption\" Kind=\"chrome/banner\" TextKey=\"catalog\" WidthKey=\"width\" Height=\"10\"/></Row>"
            + "<Overlay Id=\"anchored-group\" Padding=\"0\" Height=\"10\"><Widget Id=\"anchor-base\" Kind=\"chrome/rule\" Width=\"20\" AlignX=\"Left\"/><Widget Id=\"anchored\" Kind=\"chrome/rule\" Width=\"10\" AlignX=\"Left\" AnchorX=\"anchor-base.Right\" OffsetX=\"5\"/></Overlay>"
            + "</Column></Templates><Repeat Id=\"rows\" Items=\"items\" Template=\"item\" Padding=\"0\" Gap=\"4\"/></UiPage>";
        var translation = new Translation();
        using var host = new UiHost("assembly", UiLayoutManifest.Parse(page), bindings, UiTheme.Vanilla, new Metrics(), translation);
        Rect viewport = new(0, 0, 300, 600);
        void Drive(EventType phase, float x = 290, float y = 590)
        {
            Event.current = new Event { type = phase, button = 0, mousePosition = new Vector2(x, y) };
            host.DrawFrame(viewport);
        }
        Drive(EventType.Repaint);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300, 600));
        Check(snapshot.RectById["caption#a"].width == 45 && snapshot.RectById["caption#b"].width == 65
            && help.Values.All(value => value == "") && globalHelp == "global" && translation.Keys.All(key => key == "catalog"),
            "actual Repeat consumes per-item WidthKey/HoverHelpKey while translation catalog keys stay global");
        Check(snapshot.RectById["anchored#a"].x == snapshot.RectById["anchor-base#a"].xMax + 5
            && snapshot.RectById["anchored#b"].x == snapshot.RectById["anchor-base#b"].xMax + 5,
            "finite sibling relations in actual Repeat instances stay within each exact item");
        Rect tree = snapshot.RectById["tree#a"];
        Drive(EventType.MouseDown, tree.x + 36, tree.y + 14);
        keys.Reverse();
        bindings.NotifyChanged("items");
        snapshot = host.MeasureAndArrange(new Vector2(300, 600));
        tree = snapshot.RectById["tree#a"];
        Drive(EventType.MouseUp, tree.x + 36, tree.y + 14);
        Drive(EventType.MouseDown, tree.x + 14, tree.y + 14);
        Drive(EventType.MouseUp, tree.x + 14, tree.y + 14);
        Rect chart = snapshot.RectById["chart#a"];
        Drive(EventType.MouseDown, chart.x + chart.width / 2, chart.y + chart.height / 2);
        Drive(EventType.MouseDrag, chart.x + chart.width * .8f, chart.y + chart.height * .2f);
        Drive(EventType.MouseUp, chart.x + chart.width * .8f, chart.y + chart.height * .2f);
        Check(requests.SequenceEqual(new[] { "check:a:node", "disclose:a:node", "point:a:apoint" }),
            "two actual keyed rows reorder without retargeting Tree check/disclosure or chart PointKeysBind to another item/global key: " + string.Join(",", requests));
    }
    private sealed class Metrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 20;
        public float MeasureWidth(string text, UiFont font) => text.Length * 8;
    }
    private sealed class Translation : IUiTranslation
    {
        internal readonly List<string> Keys = new();
        public string Translate(string key) { Keys.Add(key); return key; }
        public int TranslationRevision => 0;
    }
}
