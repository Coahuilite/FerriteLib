using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

internal static class KernelReactiveInventoryTests
{
    private static readonly Rect View = new(20, 30, 420, 800);
    private const string Page = "<UiPage Schema=\"2\" Source=\"inventory\"><Templates><Column Id=\"row\" Padding=\"0\" Gap=\"0\">"
        + "<Widget Id=\"label\" Kind=\"text/wrapped\" Bind=\"caption\"/>"
        + "<Widget Id=\"field\" Kind=\"input/number-field\" Bind=\"count\" Max=\"100\"/>"
        + "<Widget Id=\"add\" Kind=\"input/button\" ActionBind=\"increment\" Text=\"Add\"/>"
        + "</Column></Templates><Column Padding=\"0\" Gap=\"0\">"
        + "<Widget Id=\"total\" Kind=\"text/wrapped\" Bind=\"caption\"/>"
        + "<Widget Id=\"pick\" Kind=\"input/dropdown\" Bind=\"selected\"/>"
        + "<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"selected\"/>"
        + "<Repeat Id=\"rows\" Items=\"items\" Template=\"row\" Padding=\"0\" Gap=\"0\"/>"
        + "</Column></UiPage>";
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine("  ok: RU1-HOST " + name);
    }
    private static IList Labels => (IList)typeof(Verse.Widgets).GetField("LabelTexts", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
    private static bool Painted(int start, string text) { for (int i = start; i < Labels.Count; i++) if ((string)Labels[i]! == text) return true; return false; }
    private static UiHost Host(UiBindings bindings, bool service = false) => new("inventory", UiLayoutManifest.Parse(service
        ? Page.Replace("<Widget Id=\"total\"", "<Widget Id=\"complete\" Kind=\"input/button\" ActionBind=\"complete\" Text=\"Complete\"/><Widget Id=\"total\"") : Page), bindings, UiTheme.Vanilla, new Metrics(), new Translation());
    private static UiNode Node(UiHost host, string id) => host.Session.GetNodeByElementId(id) ?? throw new Exception("Missing node " + id);
    private static Vector2 Centre(UiHost host, string id) { Rect rect = Node(host, id).Rect!.Value; return new Vector2(View.x + rect.x + rect.width / 2, View.y + rect.y + rect.height / 2); }
    private static void Pump(UiHost host, EventType phase, Vector2? point = null)
    {
        Event.current = new Event { type = phase, button = 0, mousePosition = point ?? new Vector2(-10, -10) };
        host.DrawFrame(View);
    }
    internal static int RunAll()
    {
        try
        {
            UiWidgetRegistry.Clear(); UiWidgetRegistry.InitializeCore();
            ResponseOutput(); SnapshotAndSelection(); RepeatIdentity(); External(); Failure();
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("  FAIL: RU1-HOST " + ex); return 1; }
        finally { Event.current = null; GUIUtility.hotControl = 0; UiNative.DebugHotControl = 0; UiNative.DebugMousePositionEnabled = false; }
    }
    private static void ResponseOutput()
    {
        using var page = new ResponseListPageExample();
        using var host = new UiHost("response-list", UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"response-list\"><Column Padding=\"0\" Gap=\"0\"><Widget Id=\"increment\" Kind=\"input/button\" ActionBind=\"increment\" Text=\"Add\"/><Widget Id=\"output\" Kind=\"text/wrapped\" Bind=\"text\"/></Column></UiPage>"),
            page.Bindings, UiTheme.Vanilla, new Metrics(), new Translation());
        int labels = Labels.Count;
        Pump(host, EventType.Repaint);
        float leading = UiTheme.Vanilla.Geometry.Padding * 2;
        Check(page.Runs == 1 && Painted(labels, "1") && Node(host, "output").Rect!.Value.height == 2 + leading,
            "response-produced list reaches real wrapped Host presentation without self-subscription");
        Vector2 button = Centre(host, "increment");
        Pump(host, EventType.MouseDown, button);
        Check(page.Runs == 1, "default Button press does not prematurely run list producer");
        Pump(host, EventType.MouseUp, button);
        labels = Labels.Count; Pump(host, EventType.Repaint);
        Check(page.Input.Value == 2 && page.Runs == 2 && Painted(labels, "1,2") && Node(host, "output").Rect!.Value.height == 6 + leading,
            "default Button release drives explicit input response output list and content-dependent Host geometry once");
        page.Output.Add(9);
        labels = Labels.Count; Pump(host, EventType.Repaint);
        Check(page.Runs == 2 && Painted(labels, "1,2,9") && Node(host, "output").Rect!.Value.height == 10 + leading,
            "external output change reaches actual Host consumer without recalling producer");
    }
    private static void SnapshotAndSelection()
    {
        using var page = new InventoryPageExample(); using var host = Host(page.Bindings);
        Pump(host, EventType.Repaint);
        UiLayoutSnapshot arranged = host.MeasureAndArrange(new Vector2(View.width, View.height));
        float height = Node(host, "label#a").Rect!.Value.height;
        page.Items.Value[0].Count = 10; page.Add("c", 3, 3);
        int labels = Labels.Count;
        Event.current = new Event { type = EventType.Repaint, mousePosition = new Vector2(-10, -10) };
        host.Draw(View, arranged);
        Check(Painted(labels, "a: 1") && !Painted(labels, "a: 10") && !Painted(labels, "c: 3") && host.Session.GetNodeByElementId("row#c") == null && Node(host, "label#a").Rect!.Value.height == height,
            "actual Repeat wrapped text and typed default options draw the measured collection/item version after post-sync writes");
        labels = Labels.Count; Pump(host, EventType.Repaint);
        Check(Painted(labels, "a: 10") && Painted(labels, "c: 3") && Node(host, "label#a").Rect!.Value.height == height + 2 && host.Session.GetNodeByElementId("row#c") != null,
            "next Host synchronization adopts Add and item Count in real labels and content-dependent row geometry");
        var sample = host.Observe(new UiObservationBudget(items: 1));
        var facts = sample.Nodes.Single(node => node.Path.EndsWith("/pick", StringComparison.Ordinal)).Facts;
        Check(facts.Any(f => f.Name == "option.0.text" && f.Value == "a: 10") && facts.Any(f => f.Name == "option-count" && f.Value == "3") && facts.Any(f => f.Name == "options" && f.Truncated),
            "real bounded Host observation reads current tracked typed options with explicit truncation");
        Vector2 mode = Centre(host, "mode");
        // Three options at this width have two columns. The first option remains selected.
        Rect row = Node(host, "mode").Rect!.Value;
        mode = new Vector2(View.x + row.x + row.width / 4, View.y + row.y + UiTheme.Vanilla.Geometry.Spacing + UiTheme.Vanilla.Geometry.RowHeight / 2);
        int requests = page.Requests;
        Pump(host, EventType.MouseDown, mode); Pump(host, EventType.MouseUp, mode);
        Check(page.Requests == requests + 1 && page.Selected.Value == 1, "default typed mode-row sends a valid same-value selection request");
        Vector2 trigger = Centre(host, "pick");
        Pump(host, EventType.MouseDown, trigger); Pump(host, EventType.MouseUp, trigger);
        Check(host.Session.OpenPopupId != null, "default typed dropdown opens from tracked projected choices");
        Rect anchor = host.Session.OpenPopupAnchor!.Value;
        Vector2 option = new(anchor.x + 12, anchor.yMax + UiPopup.OptionHeight / 2);
        requests = page.Requests;
        Pump(host, EventType.MouseDown, option); Pump(host, EventType.MouseUp, option);
        Check(page.Requests == requests + 1 && page.Selected.Value == 1 && host.Session.OpenPopupId == null, "default typed popup keeps same-value request and closes once");
    }
    private static void RepeatIdentity()
    {
        using var page = new InventoryPageExample(); using var host = Host(page.Bindings); Pump(host, EventType.Repaint);
        UiNode a = Node(host, "row#a"), field = Node(host, "field#a");
        var numeric = new UiNumericEdit(0, 100);
        UiEditSession<float> edit = UiNative.Editor<float>(host.CreateContext(View.width).WithNode(field), "items.a.count", numeric.Format, numeric.Parse);
        edit.Observe(1); edit.Focus(); edit.Input("20", true, false, false, _ => throw new Exception("uncommitted draft wrote"), () => 1);
        page.Items.Value.Move(0, 1); host.MeasureAndArrange(new Vector2(View.width, View.height));
        Check(ReferenceEquals(a, Node(host, "row#a")) && ReferenceEquals(field, Node(host, "field#a")) && edit.Draft == "20" && ReferenceEquals(field.GetOrCreateState("items.a.count").Editor, edit),
            "reactive Move reuses actual Repeat node and default numeric editor draft by stable key");
        edit.Cancel(1);
        Pump(host, EventType.Repaint);
        Pump(host, EventType.MouseDown, Centre(host, "add#a"));
        UiInteractionTarget? captured = host.Session.Interactions.CaptureOwner;
        page.Items.Value.Move(1, 0); host.MeasureAndArrange(new Vector2(View.width, View.height));
        Check(captured.HasValue && captured.Equals(host.Session.Interactions.CaptureOwner) && ReferenceEquals(a, Node(host, "row#a")), "reactive reorder retains actual keyed row capture identity");
        int requests = page.Requests;
        Pump(host, EventType.MouseUp, Centre(host, "add#a"));
        Check(page.Find("a")!.Count == 2 && page.Find("b")!.Count == 2 && page.Requests == requests + 1, "reordered captured default Button releases once into the original item");
        page.Add("c", 3, 3); Pump(host, EventType.Repaint);
        Check(ReferenceEquals(a, Node(host, "row#a")) && host.Session.GetNodeByElementId("row#c") != null, "Add creates only new stable row without replacing existing identity");
        Pump(host, EventType.MouseDown, Centre(host, "add#a"));
        InventoryItem removed = page.Find("a")!; page.Items.Value.Remove(removed); Pump(host, EventType.Repaint);
        Check(host.Session.GetNodeByElementId("row#a") == null && host.Session.GetValueStates(field.Id).Count == 0 && host.Session.Interactions.CaptureOwner == null && !edit.Focused,
            "Remove prunes actual row editor and capture without committing or replaying a request");
        requests = page.Requests; removed.Count = 99; Pump(host, EventType.MouseUp, new Vector2(-10, -10));
        Check(page.Requests == requests && page.Total.Value == 5, "removed target neither follows old properties nor receives deferred release");
        var old = page.Items.Value;
        page.RegisterKey("d"); var d = new InventoryItem(page.Runtime, "d", 4, 4);
        page.Items.Value = page.Runtime.List(new[] { d }, (x, y) => ReferenceEquals(x, y)); Pump(host, EventType.Repaint);
        Check(host.Session.GetNodeByElementId("row#b") == null && host.Session.GetNodeByElementId("row#c") == null && host.Session.GetNodeByElementId("row#d") != null, "replacing collection source reconciles through the one existing Repeat tree");
        int total = page.TotalRuns; old.Add(removed); Pump(host, EventType.Repaint);
        Check(page.TotalRuns == total && page.Total.Value == 4, "old collection changes do not invalidate new Host projections");
        host.Close(); host.Close(); d.Count = 5;
        Check(page.ObservedTotal == 5, "closing actual list Host twice leaves independent business owner active");
    }
    private static void External()
    {
        var service = new OrderBoardService(); using var projection = new OrderBoardProjectionExample(service); var page = projection.Page;
        using var host = Host(page.Bindings, service: true); Pump(host, EventType.Repaint);
        Vector2 button = Centre(host, "complete");
        Pump(host, EventType.MouseDown, button);
        Check(service.Requests == 0, "independent multi-item business command is not dispatched on default Button press");
        Pump(host, EventType.MouseUp, button); int afterCommand = Labels.Count; Pump(host, EventType.Repaint);
        Check(service.Requests == 1 && page.ObservedTotal == 4 && Painted(afterCommand, "Total 4"), "actual default Button command returns safe multi-item result through batch graph response and Host presentation");
        UiNode kept = Node(host, "row#b");
        service.Replace(new OrderBoardService.Row("b", 2, 12), new OrderBoardService.Row("a", 1, 1), new OrderBoardService.Row("c", 3, 3));
        projection.Apply(service.ReadSnapshot()); int labels = Labels.Count; Pump(host, EventType.Repaint);
        Check(ReferenceEquals(kept, Node(host, "row#b")) && Painted(labels, "b: 12") && Painted(labels, "Total 16"), "independent safe multi-item service projection drives actual default Repeat options and wrapped Host geometry");
        host.Close(); host.Close(); service.Increment(); projection.Apply(service.ReadSnapshot());
        Check(page.ObservedTotal == 17 && service.Requests == 2, "closed external Host releases view only while independent service projection keeps working");
    }
    private static void Failure()
    {
        using var page = new InventoryPageExample(); var fail = page.Runtime.Boolean(true);
        page.Bindings.BindTrackedOptions<int>("fault", () => fail.Value ? throw new Exception("inventory-sync-fault") : new[] { 1 }, (a, b) => a == b);
        using var host = Host(page.Bindings); bool refused = false;
        try { Pump(host, EventType.Repaint); } catch (InvalidOperationException ex) { refused = ex.ToString().Contains("inventory-sync-fault"); }
        Check(refused && host.Session.GetNodeByElementId("row#a") == null && host.Session.Interactions.CaptureOwner == null, "failed collection synchronization publishes no partial Repeat geometry or hit owner");
        fail.Value = false; Pump(host, EventType.Repaint);
        Check(host.Session.GetNodeByElementId("row#a") != null && page.Requests == 0, "explicit successful next Host attempt restores tracking without replaying input");
    }
    private sealed class Metrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => text.Length * 2;
        public float MeasureWidth(string text, UiFont font) => text.Length * 8;
    }
    private sealed class Translation : IUiTranslation { public string Translate(string key) => key; public int TranslationRevision => 0; }
}
