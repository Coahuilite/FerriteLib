using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;

namespace FerriteLib.UiKit.Tests;

internal static class KernelLayoutReuseTests
{
    private static readonly UiLayoutRect View = new(20, 30, 240, 700);
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); Console.WriteLine("  ok: RU2-HOST " + name); }
    private static UiNode Node(UiHost host, string id) => host.Session.GetNodeByElementId(id) ?? throw new Exception("Missing " + id);
    private static UiTheme Theme() => new() { Geometry = new UiGeometry(0, 0, 0, 20, 1) };
    private static UiHost Host(string body, UiBindings bindings, ITextMetrics? metrics = null, UiTheme? theme = null, Translation? translation = null)
        => new("ru2", UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"ru2\">" + body + "</UiPage>"), bindings, theme ?? Theme(), metrics ?? new Metrics(), translation ?? new Translation());
    private static UiLayoutSnapshot Arrange(UiHost host, float width = 240) => host.MeasureAndArrange(new UiLayoutPoint(width, View.Height));
    private static void Pump(UiHost host, EventType phase, UiLayoutPoint? point = null)
    { Event.current = new Event { type = phase, button = 0, mousePosition = VerseUiExecution.Point(point ?? new UiLayoutPoint(-20, -20)) }; NativeFixture.DrawFrame(host,View); }
    private static UiLayoutPoint Centre(UiHost host, string id)
    { UiLayoutRect r = Node(host, id).Rect!.Value; return new UiLayoutPoint(View.X + r.X + r.Width / 2, View.Y + r.Y + r.Height / 2); }
    private static string Lines(int count) => string.Join("\n", Enumerable.Repeat("x", count));
    private static void Trace(string name, params UiNode[] nodes)
    { foreach (UiNode node in nodes) Console.WriteLine("RU2-TRACE " + name + " node=" + node.Path + " scope=" + node.UpdateScopeRoot.Path + " measure=" + node.LayoutMeasureCount + " arrange=" + node.LayoutArrangeCount + " rect=" + R(node.Rect) + " content=" + R(node.ContentRect) + " shared=" + node.SharedLabelWidth); }
    private static string R(UiLayoutRect? r) => r.HasValue ? $"({r.Value.X},{r.Value.Y},{r.Value.Width},{r.Value.Height})" : "none";
    internal static int RunAll()
    {
        try
        {
            UiWidgetRegistry.Clear(); UiWidgetRegistry.InitializeCore();
            CounterAndMovement(); ScrollResults(); SharedResults(); EnvironmentAndLegacy(); WorkshopAndIdentity(); SourceSwitch(); CustomAndFailure(); PopupAndDraft(); WrapperInputs(); NativeMetricFacts(); LegacyLayoutInputs(); AllUses();
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("  FAIL: RU2-HOST " + ex); return 1; }
        finally { Event.current = null; GUIUtility.hotControl = 0; NativeFixture.DebugHotControl = 0; NativeFixture.DebugMousePositionEnabled = false; NativeFixture.TextFieldOverride = null; }
    }
    private static void CounterAndMovement()
    {
        using var counter = new CounterPageExample(); int requests = 0;
((UiBindings)counter.Bindings).BindCommand("other", () => requests++);
        counter.Bindings.BindTracked("detail", () => counter.Count.Value < 10 ? "one" : "one\ntwo");
        using var host = Host("<Column Id=\"page\" UpdateScope=\"true\" Padding=\"0\" Gap=\"0\"><Column Id=\"counter\" UpdateScope=\"true\" Padding=\"0\" Gap=\"0\"><Widget Id=\"count\" Kind=\"text/wrapped\" Bind=\"text\"/><Widget Id=\"detail\" Kind=\"text/wrapped\" Bind=\"detail\"/></Column><Column Id=\"other\" UpdateScope=\"true\" Padding=\"0\" Gap=\"0\"><Widget Id=\"stable\" Kind=\"text/wrapped\" Text=\"stable\"/><Widget Id=\"go\" Kind=\"input/button\" ActionBind=\"other\" Text=\"Go\"/></Column></Column>", counter.Bindings);
        Arrange(host); UiNode page = Node(host, "page"), region = Node(host, "counter"), other = Node(host, "other"), text = Node(host, "count"), stable = Node(host, "stable");
        int parent = page.LayoutArrangeCount, local = region.LayoutArrangeCount, independent = other.LayoutArrangeCount, stableMeasure = stable.LayoutMeasureCount, countMeasure = text.LayoutMeasureCount;
        Trace("counter-before", page, region, text, other, stable);
        counter.Count.Value = 1; Arrange(host);
        Check(text.LayoutMeasureCount == countMeasure + 1 && region.LayoutArrangeCount == local && page.LayoutArrangeCount == parent,
            "counter content changes with the same complete size: measured leaf changes, actual local and parent plans are reused");
        Check(stable.LayoutMeasureCount == stableMeasure && other.LayoutArrangeCount == independent && text.UpdateScopeRoot.Equals(region.Id) && stable.UpdateScopeRoot.Equals(other.Id),
            "explicit nested scope ownership retains real unrelated leaf measurement and region arrangement");
        float previousY = other.Rect!.Value.Y; counter.Count.Value = 10; Arrange(host);
        Check(other.Rect!.Value.Y == previousY + 20 && stable.LayoutMeasureCount == stableMeasure && other.LayoutArrangeCount == independent,
            "a real extra text line pushes the later region while its measurement and local arrangement stay reused");
        Trace("counter-grown", page, region, text, other, stable);
        Pump(host, EventType.MouseDown, Centre(host, "go"));
        Check(host.Session.Interactions.CaptureOwner?.Node == Node(host, "go").Id.Key, "new effective moved geometry, not the old cached position, receives the actual press");
        Pump(host, EventType.MouseUp, Centre(host, "go"));
        Check(requests == 1, "moved retained Button releases exactly one request through the real Host path");
    }
    private static void ScrollResults()
    {
        string value = Lines(4); var bindings = new UiBindings(); bindings.BindReadOnly("text", () => value, UiInvalidation.Measure);
        using var host = Host("<Column Id=\"page\" Padding=\"0\" Gap=\"0\"><Scroll Id=\"scroll\" Height=\"100\" Padding=\"0\" Gap=\"0\"><Clip Id=\"clip\" Padding=\"0\" Gap=\"0\"><Widget Id=\"content\" Kind=\"text/wrapped\" Bind=\"text\"/></Clip></Scroll><Widget Id=\"after\" Kind=\"text/wrapped\" Text=\"after\"/></Column>", bindings);
        Arrange(host); UiNode scroll = Node(host, "scroll"), after = Node(host, "after"), parent = Node(host, "page"); int a = after.LayoutMeasureCount, p = parent.LayoutArrangeCount;
        host.Session.SetScrollPosition(scroll, new UiLayoutPoint(0, 999)); Arrange(host);
        Check(scroll.Rect!.Value.Height == 100 && scroll.ContentRect!.Value.Height == 80 && host.Session.GetScrollPosition(scroll).Y == 0, "actual fixed viewport100 and measured content80 clamp to scroll maximum0");
        value = Lines(7); bindings.NotifyChanged("text"); Arrange(host); host.Session.SetScrollPosition(scroll, new UiLayoutPoint(0, 999)); Arrange(host);
        Check(scroll.ContentRect!.Value.Height == 140 && scroll.ContentRect.Value.Width == 224 && host.Session.GetScrollPosition(scroll).Y == 40,
            "content80 to140 propagates its real Scroll output despite unchanged viewport100, reserves scrollbar width and clamps to40");
        Check(parent.LayoutArrangeCount == p && after.LayoutMeasureCount == a && after.Rect!.Value.Y == 100, "fixed Scroll outer size does not falsely invalidate parent arrangement or following measurement");
        Pump(host, EventType.Repaint); Check(!host.Session.IsTripped(Node(host, "content")), "cached nested Clip/Scroll geometry draws through the balanced actual native-scope adapter");
        Trace("scroll140", scroll, Node(host, "clip"), Node(host, "content"), parent, after);
    }
    private static void SharedResults()
    {
        var translation = new Translation(); translation.Values["A"] = "aaaaaa"; translation.Values["B"] = "bbbbbbbbb";
        float a = 1, b = 2; var bindings = new UiBindings(); bindings.BindValue("a", () => a, v => a = v); bindings.BindValue("b", () => b, v => b = v);
        using var host = Host("<Column Id=\"group\" SharedLabels=\"true\" UpdateScope=\"true\" Padding=\"0\" Gap=\"0\"><Widget Id=\"a\" Kind=\"input/number-field\" Bind=\"a\" LabelKey=\"A\"/><Widget Id=\"b\" Kind=\"input/slider\" Bind=\"b\" LabelKey=\"B\" UpdateScope=\"true\"/></Column>", bindings, translation: translation);
        Arrange(host, 400); UiNode first = Node(host, "a"), peer = Node(host, "b"), group = Node(host, "group"); int peerMeasure = peer.LayoutMeasureCount, arrange = group.LayoutArrangeCount;
        Check(first.SharedLabelWidth == 90 && peer.SharedLabelWidth == 90, "real default labelled wrappers report contributions60 and90 into the maximum90");
        translation.Values["A"] = "aaaaaaa"; translation.Revision++; Arrange(host, 400);
        Check(first.SharedLabelWidth == 90 && peer.LayoutMeasureCount == peerMeasure && group.LayoutArrangeCount == arrange,
            "contribution60 to70 validates changed source but unchanged group90 does not remeasure the peer or rearrange the group");
        translation.Values["A"] = "aaaaaaaaaaaa"; translation.Revision++; Arrange(host, 400);
        Check(first.SharedLabelWidth == 120 && peer.SharedLabelWidth == 120 && peer.LayoutMeasureCount == peerMeasure + 1,
            "contribution crosses maximum to120 and the actual shared-column consumer remeasures with120");
        UiSliderLayout actual = UiSliderLayout.Measure("bbbbbbbbb", 400, UiFont.Small, new UiDensity(0, 0, 0, 20, 1), new Metrics(), peer.SharedLabelWidth);
        Check(actual.LabelWidth == 120, "consumer field geometry uses the propagated shared result rather than the peer's own90");
        Trace("shared120", group, first, peer);
    }
    private static void EnvironmentAndLegacy()
    {
        var metrics = new Metrics(); var theme = Theme(); var bindings = new UiBindings(); bindings.BindReadOnly("p", () => .5f);
        using var host = Host("<Column Id=\"root\" Padding=\"0\" Gap=\"0\"><Widget Id=\"text\" Kind=\"text/wrapped\" Text=\"abcdefgh\"/><Widget Id=\"progress\" Kind=\"display/progress\" Bind=\"p\"/></Column>", bindings, metrics, theme);
        Arrange(host, 100); UiNode text = Node(host, "text"), fixedBand = Node(host, "progress"); int t = text.LayoutMeasureCount, f = fixedBand.LayoutMeasureCount;
        theme[UiPaletteRole.Foreground] = new UiColour(1,0,0); Arrange(host, 100);
        Check(text.LayoutMeasureCount == t && fixedBand.LayoutMeasureCount == f, "pure colour updates do not remeasure either text or a fixed band");
        Arrange(host, 40); Check(text.Rect!.Value.Height == 40 && text.LayoutMeasureCount == t + 1 && fixedBand.LayoutMeasureCount == f,
            "changed actual width remeasures wrapped text but not the unrelated fixed-band calculation");
        metrics.Revisions[(int)UiFont.Small]++; Arrange(host, 40);
        Check(text.Rect!.Value.Height == 42 && text.LayoutMeasureCount == t + 2 && fixedBand.LayoutMeasureCount == f, "the real per-font metric stamp invalidates its consumer without a global font clock");
        t = text.LayoutMeasureCount; metrics.Revisions[(int)UiFont.Medium]++; Arrange(host, 40);
        Check(text.LayoutMeasureCount == t, "an unused font environment change leaves the actual Small-font consumer valid");
        metrics.Available = false; Arrange(host, 40); Check(text.LayoutMeasureCount == t + 1, "revoking the optional environment removes its previously granted text-measure reuse");
        var oldMetrics = new LegacyMetrics(); using var old = Host("<Widget Id=\"old\" Kind=\"text/wrapped\" Text=\"legacy\"/>", new UiBindings(), oldMetrics);
        Arrange(old); int calls = oldMetrics.Calls; Arrange(old); Check(oldMetrics.Calls == calls, "old two-member ITextMetrics still compiles and retains its original unchanged-snapshot condition");
        old.Session.BumpContentRevision(); Arrange(old); Check(oldMetrics.Calls > calls, "unknown metrics are revalidated at the old legal synchronization point, not silently granted environment purity");
        Trace("environment", text, fixedBand);
    }
    private static void WorkshopAndIdentity()
    {
        var service = new OrderBoardService(); using var projection = new OrderBoardProjectionExample(service); var page = projection.Page;
        string body = "<Templates><Column Id=\"row\" UpdateScope=\"true\" Padding=\"0\" Gap=\"0\"><Widget Id=\"label\" Kind=\"text/wrapped\" Bind=\"caption\"/><Widget Id=\"field\" Kind=\"input/number-field\" Bind=\"count\" Max=\"100\"/><Widget Id=\"add\" Kind=\"input/button\" ActionBind=\"increment\" Text=\"Add\"/></Column></Templates><Repeat Id=\"rows\" Items=\"items\" Template=\"row\" Padding=\"0\" Gap=\"0\"/>";
        using var host = Host(body, page.Bindings); Pump(host, EventType.Repaint);
        UiNode rowB = Node(host, "row#b"), labelB = Node(host, "label#b"), fieldB = Node(host, "field#b"); int m = labelB.LayoutMeasureCount, a = rowB.LayoutArrangeCount;
        service.Replace(new OrderBoardService.Row("a", 1, 9), new OrderBoardService.Row("b", 2, 2)); projection.Apply(service.ReadSnapshot()); Pump(host, EventType.Repaint);
        Check(labelB.LayoutMeasureCount == m && rowB.LayoutArrangeCount == a, "one safely projected workshop item changes while another real Repeat row reuses measurement and arrangement");
        Pump(host, EventType.MouseDown, Centre(host, "add#b")); UiInteractionTarget? capture = host.Session.Interactions.CaptureOwner;
        service.Replace(new OrderBoardService.Row("b", 2, 2), new OrderBoardService.Row("a", 1, 9), new OrderBoardService.Row("c", 3, 3)); projection.Apply(service.ReadSnapshot()); Arrange(host);
        Check(ReferenceEquals(rowB, Node(host, "row#b")) && ReferenceEquals(fieldB, Node(host, "field#b")) && capture.Equals(host.Session.Interactions.CaptureOwner) && Node(host, "row#c") != null,
            "safe projection Add and Move preserve keyed nodes and captured item while creating only the new row");
        int requests = page.Requests; Pump(host, EventType.MouseUp, Centre(host, "add#b")); Check(page.Requests == requests + 1 && page.Find("b")!.Count == 3, "reordered cached real Button releases to its original business item exactly once");
        Pump(host, EventType.MouseDown, Centre(host, "add#b"));
        service.Replace(new OrderBoardService.Row("a", 1, 9), new OrderBoardService.Row("c", 3, 3)); projection.Apply(service.ReadSnapshot()); Pump(host, EventType.Repaint);
        Check(host.Session.GetNodeByElementId("row#b") == null && host.Session.Interactions.CaptureOwner == null, "safe Remove releases cached row identity and its retained capture");
        host.Close(); int total = page.ObservedTotal; service.Increment(); projection.Apply(service.ReadSnapshot()); Check(page.ObservedTotal == total + 1, "closing cached Host releases its UI ownership without acquiring authority over the independent business service");
        Trace("workshop-before-removal", rowB, labelB, fieldB);
    }
    private static void SourceSwitch()
    {
        var runtime = new UiReactiveRuntime(); var old = runtime.Int(1); var next = runtime.Int(1); var source = runtime.Value(old, (x, y) => ReferenceEquals(x, y));
        var bindings = new UiBindings(runtime); int reads = 0; bindings.BindTracked("caption", () => { reads++; return source.Value.Value.ToString(); });
        using var host = Host("<Widget Id=\"caption\" Kind=\"text/wrapped\" Bind=\"caption\"/>", bindings); Arrange(host); UiNode node = Node(host, "caption"); int measures = node.LayoutMeasureCount;
        source.Value = next; Arrange(host); int capturedReads = reads;
        Check(node.LayoutMeasureCount == measures && old.SubscriberCount == 0 && next.SubscriberCount > 0, "same-result source replacement updates actual subscriptions before reusing measurement");
        old.Value = 2; Arrange(host); Check(reads == capturedReads && node.LayoutMeasureCount == measures, "old source no longer invalidates calculation or retained measurement");
        next.Value = 2; Arrange(host); Check(reads == capturedReads + 1 && node.LayoutMeasureCount == measures + 1, "new source still reaches real measurement with the next changed result");
        Console.WriteLine($"RU2-EVALUATION source-switch reads={reads} measures={node.LayoutMeasureCount} old-subs={old.SubscriberCount} new-subs={next.SubscriberCount}");
        host.Close(); Check(next.SubscriberCount == 0, "closing cached Host releases the switched view dependency");
    }
    private static void CustomAndFailure()
    {
        float value = 20; var bindings = new UiBindings(); bindings.BindReadOnly("h", () => value, UiInvalidation.Measure);
        var custom = new Custom(); UiWidgetRegistry.Define(new UiWidgetDefinition("ru2", "custom", () => custom, new[] { "Bind" }));
        using var host = Host("<Column Id=\"root\" Padding=\"0\" Gap=\"0\"><Widget Id=\"custom\" Kind=\"custom\" Bind=\"h\"/><Widget Id=\"stable\" Kind=\"text/wrapped\" Text=\"stable\"/></Column>", bindings);
        Arrange(host); UiNode node = Node(host, "custom"), stable = Node(host, "stable"); int count = custom.Calculations; bindings.NotifyChanged("h"); Arrange(host);
        Check(custom.Calculations == count, "new custom complete-input entry can announce unchanged input without calculating again");
        value = 40; bindings.NotifyChanged("h"); Arrange(host); Check(custom.Calculations == count + 1 && stable.Rect!.Value.Y == 40, "new custom captured scalar input reaches the real parent result and geometry");
        bool refused = false; try { using var bad = Host("<Column Id=\"bad\" UpdateScope=\"guess\"/>", new UiBindings()); } catch (UiContractException ex) { refused = ex.ElementPath == "bad" && ex.Message.Contains("UpdateScope"); }
        Check(refused, "invalid scope declarations receive a located creation-time contract diagnostic");
UiWidgetRegistry.Define(new UiWidgetDefinition("ru2", "invalid", () => new Invalid(), Array.Empty<string>()));
        using var invalid = Host("<Widget Id=\"broken\" Kind=\"invalid\"/>", new UiBindings()); Arrange(invalid); Check(invalid.Session.IsTripped(Node(invalid, "broken")), "invalid custom measurement capture is diagnosed at its actual node through the retained recovery guard");
        // Existing transactional anchor refusal now also exercises staged cache rollback.
        var session = new UiSession(); var engine = new UiLayoutEngine("ru2"); var ctx = NativeFixture.Context("ru2", session, new Metrics(), Theme(), new Translation(), new UiBindings(), 240, "root");
        IReadOnlyList<UiElementSpec> Roots(string anchor) => UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"ru2\"><Overlay Id=\"overlay\" Padding=\"0\"><Widget Id=\"a\" Kind=\"text/wrapped\" Text=\"a\" Width=\"40\" AlignX=\"Left\" AnchorX=\"" + anchor + "\"/></Overlay></UiPage>").Roots;
        engine.ArrangeRoots(ctx, new UiLayoutPoint(240, 200), Roots("parent.Left")); UiNode kept = session.GetNodeByElementId("a")!; UiLayoutRect before = kept.Rect!.Value;
        bool failed = false; try { engine.ArrangeRoots(ctx, new UiLayoutPoint(240, 200), Roots("a.Right")); } catch (FormatException) { failed = true; }
        Check(failed && kept.Rect!.Value.X == before.X, "changed definitions cannot reuse stale anchor plans; failed candidate restores previous complete geometry");
        engine.ArrangeRoots(ctx, new UiLayoutPoint(240, 200), Roots("parent.Right")); Check(kept.Rect!.Value.X == 240, "explicit valid next attempt publishes new placement after failure without reusing a half-candidate cache");
        session.Dispose(); Trace("custom", node, stable);
    }
    private static void PopupAndDraft()
    {
        string text = "one"; string selected = "a"; var bindings = new UiBindings();
        bindings.BindReadOnly("text", () => text, UiInvalidation.Measure);
        bindings.BindValue("selected", () => selected, v => selected = v); bindings.BindChoices("selected", ()=> (new[] { "a", "b" }).Select(value=>new UiChoice<string>(value,value,value)).ToArray());
        using var host = Host("<Column Id=\"root\" Padding=\"0\" Gap=\"0\"><Widget Id=\"text\" Kind=\"text/wrapped\" Bind=\"text\"/><Widget Id=\"popup\" Kind=\"input/dropdown\" Bind=\"selected\" OptionsBind=\"selected\"/></Column>", bindings);
        Pump(host, EventType.Repaint); Pump(host, EventType.MouseDown, Centre(host, "popup")); Pump(host, EventType.MouseUp, Centre(host, "popup"));
        Check(host.Session.OpenPopupId != null, "actual default dropdown opens before a reused-subtree movement");
        float before = host.Session.OpenPopupAnchor!.Value.Y; int measure = Node(host, "popup").LayoutMeasureCount;
        text = "one\ntwo"; bindings.NotifyChanged("text"); Pump(host, EventType.Repaint);
        Check(host.Session.OpenPopupAnchor!.Value.Y == before + 20 && Node(host, "popup").LayoutMeasureCount == measure,
            "retained popup measurement is reused but its actual window-space anchor follows the moved trigger");
        using var page = new InventoryPageExample();
        using var rows = Host("<Templates><Column Id=\"row\" Padding=\"0\"><Widget Id=\"field\" Kind=\"input/number-field\" Bind=\"count\" Max=\"100\"/></Column></Templates><Repeat Id=\"rows\" Items=\"items\" Template=\"row\" Padding=\"0\"/>", page.Bindings);
        Arrange(rows); UiNode field = Node(rows, "field#a"); var numeric = new UiNumericEdit(0, 100);
        UiNode privatePart = rows.CreateContext(240).WithNode(field).Child("retained").Node!;
        UiEditSession<float> edit = NativeFixture.Editor<float>(rows.CreateContext(240).WithNode(field), new UiBindingAddress("items","a","count").Key, numeric.Format, numeric.Parse);
        edit.Observe(1); edit.Focus(); edit.Input("20", true, false, false, _ => throw new Exception("Draft committed"), () => 1);
        int measured = field.LayoutMeasureCount; page.Items.Value.Move(0, 1); Arrange(rows);
        Check(ReferenceEquals(field, Node(rows, "field#a")) && edit.Draft == "20" && field.LayoutMeasureCount == measured
            && privatePart.Parent == field && privatePart.UpdateScopeRoot.Equals(field.UpdateScopeRoot),
            "a moved cached numeric wrapper retains its real editor and uncommitted draft without remeasurement");
        page.Items.Value.Remove(page.Find("a")!); Pump(rows, EventType.Repaint);
        Check(rows.Session.GetNodeByElementId("field#a") == null && !edit.Focused, "removing the cached editor releases its actual draft ownership without committing");
    }
    private static void WrapperInputs()
    {
        IReadOnlyList<UiTreeRow> tree = new[] { new UiTreeRow("a", 0, "a") };
        UiImage image = new(1,1,40,30);
        IReadOnlyList<UiChoice<int>> options = new[] { new UiChoice<int>("one", 1), new UiChoice<int>("two", 2) };
        int selected = 1; float progress = .5f; var bindings = new UiBindings();
        bindings.BindReadOnly("tree", () => tree); bindings.BindReadOnly("image", () => image);
        bindings.BindValue("mode", () => selected, v => selected = v); bindings.BindChoices("mode",()=>options);
        bindings.BindReadOnly("progress", () => progress);
        using var host = Host("<Column Id=\"root\" Padding=\"0\" Gap=\"0\"><Widget Id=\"tree\" Kind=\"container/tree\" Bind=\"tree\"/><Widget Id=\"image\" Kind=\"display/image\" Bind=\"image\"/><Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\"/><Widget Id=\"progress\" Kind=\"display/progress\" Bind=\"progress\"/></Column>", bindings);
        Arrange(host); UiNode t = Node(host, "tree"), i = Node(host, "image"), m = Node(host, "mode"), p = Node(host, "progress");
        int tc = t.LayoutMeasureCount, ic = i.LayoutMeasureCount, mc = m.LayoutMeasureCount, pc = p.LayoutMeasureCount;
        tree = new[] { new UiTreeRow("a", 0, "renamed", selected: true) }; progress = .8f; bindings.NotifyChanged("tree", "progress"); Arrange(host);
        Check(t.LayoutMeasureCount == tc && p.LayoutMeasureCount == pc && i.LayoutMeasureCount == ic && m.LayoutMeasureCount == mc,
            "default tree presentation-only row facts and progress values do not recalculate unchanged bands or unrelated wrappers");
        tree = new[] { new UiTreeRow("a", 0, "a"), new UiTreeRow("b", 0, "b") }; image = new UiImage(1,2,40,50);
        options = new[] { new UiChoice<int>("one", 1), new UiChoice<int>("two", 2), new UiChoice<int>("three", 3) };
        bindings.NotifyChanged("tree", "image", "mode"); Arrange(host);
        Check(t.Rect!.Value.Height == 40 && i.Rect!.Value.Height == 50 && m.Rect!.Value.Height == 60 && p.LayoutMeasureCount == pc,
            "actual default tree row count, supplier image size and typed option count reach measurement while fixed progress is reused");
        string text = "one"; int requests = 0; var edits = new UiBindings(); edits.BindValue("text", () => text, value => { text = value; requests++; });
        using var editorHost = Host("<Column Id=\"root\" Padding=\"0\" Gap=\"0\"><Widget Id=\"edit\" Kind=\"input/text-field\" Bind=\"text\" Multiline=\"true\"/><Widget Id=\"after\" Kind=\"text/wrapped\" Text=\"after\"/></Column>", edits);
        Pump(editorHost, EventType.Repaint); UiNode edit = Node(editorHost, "edit"), after = Node(editorHost, "after"); int previous = edit.LayoutMeasureCount, unaffected = after.LayoutMeasureCount;
        NativeFixture.TextFieldOverride = (_, _) => "one\ntwo"; Pump(editorHost, EventType.MouseDown, Centre(editorHost, "edit")); NativeFixture.TextFieldOverride = null;
        Arrange(editorHost);
        Check(requests == 0 && text == "one" && edit.Rect!.Value.Height == 40 && edit.LayoutMeasureCount == previous + 1 && after.LayoutMeasureCount == unaffected && after.Rect!.Value.Y == 40,
            "native multiline draft change invalidates its default measurement without author dirty calls, keeps business uncommitted and moves the reused following text");
        string caption = "one"; var paint = new UiBindings(); paint.BindReadOnly("caption", () => caption, UiInvalidation.Paint);
        using var measured = Host("<Widget Id=\"text\" Kind=\"text/wrapped\" Bind=\"caption\"/>", paint);
        Arrange(measured); caption = "one\ntwo"; paint.NotifyChanged("caption"); Arrange(measured);
        Check(Node(measured, "text").Rect!.Value.Height == 40, "default content-sized wrapper validates real changed input even when its source classifies the announcement as Paint");
        Trace("wrapper-inputs", t, i, m, p, edit, after);
    }
    private static void NativeMetricFacts()
    {
        Verse.GameFont previous = Verse.Text.Font; bool wrap = Verse.Text.WordWrap; UnityEngine.TextAnchor anchor = Verse.Text.Anchor;
        Verse.Text.Font = Verse.GameFont.Small; GUIStyle style = Verse.Text.CurFontStyle; int size = style.fontSize;
        try
        {
            UiMeasureInputs first = ((IUiTextMetricsEnvironment)VerseFerriteTextMetrics.Instance).CaptureEnvironment(UiFont.Small)!;
            style.fontSize = size + 1;
            Check(!first.Equals(((IUiTextMetricsEnvironment)VerseFerriteTextMetrics.Instance).CaptureEnvironment(UiFont.Small)), "default Verse adapter reports actual changed font-style metric facts through the optional environment seam");
            Verse.Text.WordWrap = false; Verse.Text.Anchor = UnityEngine.TextAnchor.MiddleRight;
            VerseFerriteTextMetrics.Instance.MeasureText("native adapter fixture", UiFont.Medium, 100);
            Check(!Verse.Text.WordWrap && Verse.Text.Anchor == UnityEngine.TextAnchor.MiddleRight && Verse.Text.Font == Verse.GameFont.Small,
                "default metric calculation restores caller font/wrap/anchor after using its known measurement environment");
        }
        finally { style.fontSize = size; Verse.Text.Font = previous; Verse.Text.WordWrap = wrap; Verse.Text.Anchor = anchor; }
    }
    private static void LegacyLayoutInputs()
    {
        bool visible = true; float width = 80; int visibleReads = 0, widthReads = 0, naturalReads = 0; float natural = 70;
        var bindings = new UiBindings(); bindings.BindReadOnly("visible", () => { visibleReads++; return visible; });
        bindings.BindReadOnly("width", () => { widthReads++; return width; });
UiWidgetRegistry.Define(new UiWidgetDefinition("ru2", "legacy", () => new LegacyWidget(), Array.Empty<string>(), Array.Empty<string>(), (_, _) => { naturalReads++; return natural; }));
        using var host = Host("<Column Id=\"root\" Padding=\"0\" Gap=\"0\"><Widget Id=\"conditional\" Kind=\"text/wrapped\" Text=\"visible\" VisibleKey=\"visible\" WidthKey=\"width\" AlignX=\"Left\"/><Widget Id=\"auto\" Kind=\"legacy\" Width=\"Auto\"/></Column>", bindings);
        Arrange(host); int vr = visibleReads, wr = widthReads, nr = naturalReads;
        visible = false; width = 100; natural = 90; Arrange(host);
        Check(Node(host, "conditional").Rect!.Value.Width == 80 && Node(host, "auto").Rect!.Value.Width == 70 && visibleReads == vr && widthReads == wr && naturalReads == nr,
            "idle stage validation does not poll ordinary visibility/width getters or the old custom natural-width callback without their original synchronization");
        bindings.NotifyChanged("visible", "width"); Arrange(host); Check(Node(host, "conditional").Rect == null && Node(host, "auto").Rect!.Value.Width == 90,
            "ordinary notified structure and the original legal full-sync custom width update still reach current geometry");
        visible = true; bindings.NotifyChanged("visible"); Arrange(host);
        Check(Node(host, "conditional").Rect!.Value.Width == 100 && Node(host, "conditional").LayoutMeasureCount > 0, "notified return adopts the actual current ordinary width and retained identity");
        Console.WriteLine($"RU2-EVALUATION ordinary-layout visible={visibleReads} width={widthReads} natural={naturalReads}");
    }
    private sealed class LegacyWidget : IUiWidget
    {
        public string Kind => "legacy"; public void Configure(UiElementSpec s) { } public void Validate(IUiBindingReader b, string p) { }
        public float Measure(UiWidgetContext ctx) => 20; public void Draw(UiLayoutRect rect, UiWidgetContext ctx) { }
    }
    private static void AllUses()
    {
        // All retained wrappers already have purpose-specific lanes; this census verifies actual optional
        // participation rather than silently granting arbitrary legacy IUiWidget purity.
        string[] kinds = UiWidgetRegistry.KnownKinds(UiWidgetRegistry.CoreScope).ToArray();
        foreach (string kind in kinds) Check(UiWidgetRegistry.Resolve(UiWidgetRegistry.CoreScope, kind) is IUiMeasureParticipant, "inventory enrolled default " + kind);
        Check(kinds.Length == 18, "complete eighteen-kind registration census remains intact");
        foreach (string kind in new[] { "Stack", "Row", "Column", "Wrap", "Overlay", "Section", "Surface", "Scroll", "Clip" })
        {
            using var host = Host("<" + kind + " Id=\"group\" UpdateScope=\"true\" Padding=\"0\" Gap=\"0\"><Widget Id=\"leaf\" Kind=\"text/wrapped\" Text=\"same\"/></" + kind + ">", new UiBindings());
            Arrange(host); UiNode leaf = Node(host, "leaf"), group = Node(host, "group"); int m = leaf.LayoutMeasureCount, a = group.LayoutArrangeCount;
            Arrange(host); Check(leaf.LayoutMeasureCount == m && group.LayoutArrangeCount == a && leaf.UpdateScopeRoot.Equals(group.Id), "inventory actual scoped stage reuse " + kind);
        }
    }
    private sealed class Custom : IUiWidget, IUiMeasureParticipant
    {
        public string Kind => "custom"; internal int Calculations;
        public void Configure(UiElementSpec spec) { }
        public void Validate(IUiBindingReader bindings, string path) { bindings.ValidateValue<float>("h", path); }
        public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();
        public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
        { float input = ctx.Bindings.Get<float>("h"); return new UiMeasurement(new UiMeasureInputs(numbers: new[] { input }), () => { Calculations++; return input; }); }
        public void Draw(UiLayoutRect rect, UiWidgetContext ctx) { }
    }
    private sealed class Invalid : IUiWidget, IUiMeasureParticipant
    {
        public string Kind => "invalid"; public void Configure(UiElementSpec spec) { } public void Validate(IUiBindingReader b, string p) { }
        public float Measure(UiWidgetContext ctx) => 20; public UiMeasurement CaptureMeasurement(UiWidgetContext ctx) => null!; public void Draw(UiLayoutRect rect, UiWidgetContext ctx) { }
    }
    internal sealed class Metrics : ITextMetrics, IUiTextMetricsEnvironment
    {
        internal readonly int[] Revisions = new int[4]; internal bool Available = true; internal int HeightCalls, WidthCalls;
        public UiMeasureInputs? CaptureEnvironment(UiFont font) => Available ? new UiMeasureInputs(new[] { "ru2-fixture" }, new[] { (float)font, Revisions[(int)font] }) : null;
        public float MeasureText(string text, UiFont font, float width)
        { HeightCalls++; return text.Split(new[] { '\n' }).Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length * 10 / Math.Max(1, width)))) * (20 + Revisions[(int)font]); }
        public float MeasureWidth(string text, UiFont font) { WidthCalls++; return text.Length * 10; }
    }
    private sealed class LegacyMetrics : ITextMetrics
    { internal int Calls; public float MeasureText(string t, UiFont f, float w) { Calls++; return 20; } public float MeasureWidth(string t, UiFont f) => t.Length * 10; }
    private sealed class Translation : IUiTranslation
    { internal int Revision; internal readonly Dictionary<string, string> Values = new(); public int TranslationRevision => Revision; public string Translate(string key) => Values.TryGetValue(key, out string? value) ? value : key; }
}
