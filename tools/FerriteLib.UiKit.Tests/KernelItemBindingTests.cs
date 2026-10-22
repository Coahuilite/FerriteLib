using System;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;
internal static class KernelItemBindingTests
{
    private static readonly Rect View = new(0, 0, 300, 400);
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); Console.WriteLine("  ok: ITEMS-HOST " + name); }
    private static void Pump(UiHost host, EventType phase = EventType.Repaint, Vector2? point = null)
    { Event.current = new Event { type = phase, button = 0, mousePosition = point ?? new Vector2(-10, -10) }; host.DrawFrame(View); }
    internal static int RunAll()
    {
        try
        {
            UiWidgetRegistry.Clear(); UiWidgetRegistry.InitializeCore(); using var page = new ItemBindingExample();
            string xml = "<UiPage Schema=\"2\" Source=\"item-author\"><Templates><Column Id=\"row\" Padding=\"0\" Gap=\"0\"><Widget Id=\"label\" Kind=\"text/wrapped\" Bind=\"caption\"/><Widget Id=\"field\" Kind=\"input/number-field\" Bind=\"count\" Max=\"100\" Metric.RowHeight=\"30\"/><Widget Id=\"add\" Kind=\"input/button\" ActionBind=\"increment\" Text=\"Add\"/></Column></Templates><Repeat Id=\"rows\" Items=\"items\" Template=\"row\" Padding=\"0\" Gap=\"0\"/></UiPage>";
            using var host = new UiHost("item-author", UiLayoutManifest.Parse(xml), page.Bindings, UiTheme.Vanilla, new Metrics(), new Translation());
            Pump(host); UiNode Node(string id) => host.Session.GetNodeByElementId(id) ?? throw new Exception("Missing " + id);
            UiNode a = Node("row#a"), field = Node("field#a"); var codec = new UiNumericEdit(0, 100);
            var editor = UiNative.Editor<float>(host.CreateContext(300).WithNode(field), "items.a.count", codec.Format, codec.Parse);
            editor.Observe(10); editor.Input("20", true, false, false, _ => throw new Exception("unexpected request"), () => 10);
            ItemSnapshot old = page.Snapshot[0], replacement = new("a", 50);
            page.Snapshot = new[] { page.Snapshot[1], replacement }; page.Items.Refresh(); Pump(host);
            Check(ReferenceEquals(a, Node("row#a")) && ReferenceEquals(field, Node("field#a")) && ReferenceEquals(editor, field.GetOrCreateState("items.a.count").Editor)
                && editor.Draft == "20" && editor.Conflict, "real keyed Repeat retains correct editor on reorder/same-key new snapshot and observes conflict");
            Rect add = Node("add#a").Rect!.Value; Vector2 point = new(add.x + 10, add.y + 10);
            Pump(host, EventType.MouseDown, point); Pump(host, EventType.MouseUp, point); Pump(host);
            Check(replacement.Count == 51 && replacement.Requests == 1 && old.Count == 10,
                "actual default Button requests current same-key object, not old snapshot or reordered index");
            page.Snapshot = new[] { page.Snapshot[0] }; page.Items.Refresh(); Pump(host);
            Check(host.Session.GetNodeByElementId("row#a") == null && host.Session.GetValueStates(field.Id).Count == 0 && !editor.Focused,
                "actual source deletion releases row/editor/connection without committing draft or rebuilding Host");
            Check(!page.Bindings.ContainsBinding("items.a.count") && !host.Presentation!.GetStatus("items.a.count").HasLastValue
                && replacement.Count == 51, "deleted item has no binding or retained UI read; external object remains owned by business");
            string session = host.Session.Identity.ToString(); host.Close(); host.Close(); page.Dispose();
            Check(!host.Session.IsActive && !page.Bindings.ContainsBinding("items") && old.Count == 10,
                "idempotent host/helper shutdown releases UI without destroying former business targets");
            Console.WriteLine("ITEMS-HOST-TRACE host=" + session + " snapshot=a50 requests=1 actual51 reordered=b,a then-delete=a draft20-never-published");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("  FAIL: ITEMS-HOST " + ex); return 1; }
        finally { Event.current = null; GUIUtility.hotControl = 0; UiNative.DebugHotControl = 0; }
    }
    private sealed class Metrics : ITextMetrics
    { public float MeasureWidth(string text, UiFont font) => text.Length * 8; public float MeasureText(string text, UiFont font, float width) => 16; }
    private sealed class Translation : IUiTranslation
    { public int TranslationRevision => 0; public string Translate(string key) => key; }
}
