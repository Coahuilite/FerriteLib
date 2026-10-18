using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;
internal static class KernelObservationTests
{
    public static int RunAll()
    {
        try
        {
            ReadFactsWithoutDraw();
            BoundedCollections();
            StandardAndEmbeddedEditors();
            NestedScrollHost();
            DiscoveryLifetimeAndCapacity();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: R9 " + ex);
            return 1;
        }
    }

    private static void ReadFactsWithoutDraw()
    {
        UiWidgetRegistry.InitializeCore();
        string model = "actual model";
        int writes = 0;
        bool failRead = false;
        bool visible = true;
        var bindings = new UiBindings();
        bindings.BindValue("text", () => failRead ? throw new InvalidOperationException("read failed") : model, value =>
        {
            model = value;
            writes++;
        });
        bindings.BindCommand("run", () => writes++);
        bindings.BindReadOnly("show", () => visible);
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"r9\"><Stack Id=\"root\">" + "<Widget Id=\"text\" Kind=\"input/text-field\" Bind=\"text\" Label=\"Name\" VisibleKey=\"show\"/>" + "<Widget Id=\"button\" Kind=\"input/button\" ActionBind=\"run\" Text=\"Run\"/>" + "<Widget Id=\"note\" Kind=\"text/wrapped\" Text=\"readable\"/></Stack></UiPage>");
        using var host = new UiHost("r9", manifest, bindings, UiTheme.Vanilla, new Metrics(), new Translation());
        Check(host.Observe().Status == UiObservationStatus.NotSampled, "unarranged host reports unsampled without materialization");
        Check(UiHost.ObserveLiveHosts(128).Hosts.Any(item => item.Host == host.Session.Identity), "live host discovered by stable session identity");
        host.MeasureAndArrange(new Vector2(320, 240));
        UiNode node = host.Session.GetNodeByElementId("text")!;
        var ctx = host.CreateContext(320).WithNode(node);
        bool Parse(string value, out string parsed)
        {
            parsed = value;
            return true;
        }

        var editor = UiNative.Editor<string>(ctx, "text", value => value, Parse);
        editor.Observe(model);
        editor.Input("local draft", true, false, false, _ => throw new Exception("unexpected publication"), () => model);
        int frame = host.Session.Frame;
        UiNative.TextFieldOverride = (_, _) => throw new Exception("observation must not paint");
        try
        {
            UiHostObservation snapshot = host.Observe(nodeKey: node.Id.Key);
            Check(snapshot.Nodes.Count == 1 && snapshot.Nodes[0].Key == node.Id.Key, "exact subtree excludes following sibling");
            var facts = snapshot.Nodes[0].Facts;
            Check(facts.Any(f => f.Name == "value" && f.Value == model) && facts.Any(f => f.Name == "draft" && f.Value == "local draft"), "model and local draft are independent actual facts");
            Check(snapshot.Frame == frame && host.Session.Frame == frame && writes == 0 && editor.Draft == "local draft", "read neither draws, opens a frame, requests nor changes draft");
            Check(facts.Any(f => f.Name == "native-focus" && f.Status == UiObservationStatus.NotSampled) && facts.Any(f => f.Name == "geometry" && f.Source.Contains("page-local")), "native unknown and last page-local geometry are explicit");
            model = "changed current model";
            UiHostObservation current = UiHost.ObserveLiveHost(host.Session.Identity, nodeKey: node.Id.Key);
            Check(current.Nodes[0].Facts.Any(f => f.Name == "value" && f.Value == model) && snapshot.Nodes[0].Facts.Any(f => f.Name == "value" && f.Value == "actual model"), "detached prior sample stays historical while fresh read sees actual model");
            UiHostObservation small = host.Observe(new UiObservationBudget(nodes: 1, text: 3));
            Check(small.Nodes.Count == 1 && small.Truncated, "node budget reports truncation");
            UiHostObservation shortText = host.Observe(new UiObservationBudget(text: 3), node.Id.Key);
            Check(shortText.Nodes[0].Facts.Any(f => f.Name == "value" && f.Truncated && f.Value == "cha"), "text budget truncates explicitly");
            Check(host.Observe(nodeKey: "missing").Status == UiObservationStatus.NotFound, "unknown stable key differs from unsupported projection");
            node.MarkDirty();
            Check(host.Observe().Status == UiObservationStatus.Stale, "pending layout invalidation identifies old geometry");
            failRead = true;
            Check(host.Observe(nodeKey: node.Id.Key).Nodes[0].Facts.Any(f => f.Status == UiObservationStatus.Failed && f.Reason == "InvalidOperationException"), "failed getter remains unknown with cause class, never default value");
            failRead = false;
            visible = false;
            bindings.NotifyChanged("show");
            host.MeasureAndArrange(new Vector2(320, 240));
            UiHostObservation hidden = host.Observe(nodeKey: node.Id.Key);
            Check(hidden.Nodes.Count == 1 && hidden.Reason == "retained-node-without-current-arranged-entry"
                && hidden.Nodes[0].Facts.Any(f => f.Name == "geometry" && f.Status == UiObservationStatus.NotSampled)
                && hidden.Nodes[0].Facts.Any(f => f.Name == "draft" && f.Value == "local draft"),
                "known hidden editor remains locatable without inventing geometry or reading an old model declaration");
        }
        finally
        {
            UiNative.TextFieldOverride = null;
        }

        host.Close();
        Check(host.Observe().Status == UiObservationStatus.Stale && UiHost.ObserveLiveHost(host.Session.Identity).Status == UiObservationStatus.NotFound, "closed host sample is stale and is removed from live discovery");
    }

    private static void StandardAndEmbeddedEditors()
    {
        string model = "initial";
        int requests = 0;
        var bindings = new UiBindings();
        bindings.BindValue("text", () => model, value =>
        {
            model = value;
            requests++;
        });
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"r9-host\">" + "<Widget Id=\"text\" Kind=\"input/text-field\" Bind=\"text\"/></UiPage>");
        var window = new UiPageWindow(new UiWindowKey("r9", "page", "standard"), manifest, bindings, UiTheme.Vanilla, new Translation(), "Page", "Close", _ => "Unavailable", new Metrics());
        using var embedded = new UiHost("r9-host", manifest, bindings, UiTheme.Vanilla, new Metrics(), new Translation());
        window.windowRect = new Rect(0, 0, 500, 400);
        Verse.Find.WindowStack.Add(window);
        UiNative.DebugMousePositionEnabled = true;
        UiNative.DebugMouseDown = false;
        UiNative.DebugEnter = false;
        UiNative.DebugFocusLost = false;
        UiNative.TextFieldOverride = (_, text) => text;
        try
        {
            window.WindowOnGUI();
            embedded.DrawFrame(new Rect(0, 0, 320, 240), window);
            UiHost standard = window.PageHost!;
            Check(standard != null && standard.Session.Identity != embedded.Session.Identity, "ordinary concrete page and embedded host own distinct real sessions");
            bool Parse(string value, out string parsed)
            {
                parsed = value;
                return true;
            }

            foreach (UiHost host in new[]
            {
                standard!,
                embedded
            }

            )
            {
                UiNode node = host.Session.GetNodeByElementId("text")!;
                var editor = UiNative.Editor<string>(host.CreateContext(320).WithNode(node), "text", value => value, Parse);
                editor.Observe(model);
                editor.Focus();
            }

            UiNative.TextFieldOverride = (_, _) => "candidate";
            window.WindowOnGUI();
            embedded.DrawFrame(new Rect(0, 0, 320, 240), window);
            Check(requests == 0 && standard!.Session.ActiveEditState != null && embedded.Session.ActiveEditState != null, "both host routes hold the same deferred common edit without publishing on paint");
            var modal = new BlockingWindow();
            Verse.Find.WindowStack.Add(modal);
            try
            {
                UiNative.TextFieldOverride = (_, text) => text;
                window.WindowOnGUI();
                embedded.DrawFrame(new Rect(0, 0, 320, 240), window);
                Check(!embedded.Session.PointerInputAllowed && !standard!.Session.PointerInputAllowed && requests == 0
                    && standard.Observe().Nodes[0].Facts.Any(f => f.Name == "draft" && f.Value == "candidate"),
                    "real shell stack qualification suspends input under a modal without publishing or discarding the draft");
            }
            finally { Verse.Find.WindowStack.TryRemove(modal); }
            UiNode standardNode = standard!.Session.GetNodeByElementId("text")!;
            UiNative.Editor<string>(standard.CreateContext(320).WithNode(standardNode), "text", value => value, Parse).Focus();
            UiNode embeddedNode = embedded.Session.GetNodeByElementId("text")!;
            UiNative.Editor<string>(embedded.CreateContext(320).WithNode(embeddedNode), "text", value => value, Parse).Focus();
            embedded.DrawFrame(new Rect(0, 0, 320, 240), window);
            window.WindowOnGUI();
            Check(standard.Session.PointerInputAllowed && embedded.Session.PointerInputAllowed && standard.TryHandleAccept(),
                "standard host regains stack-qualified input and confirms its retained editor");
            UiNative.TextFieldOverride = (_, text) => text;
            window.WindowOnGUI();
            Check(requests == 1 && model == "candidate" && embedded.Session.ActiveEditState != null, "standard confirmation requests once and does not clear the other host editor");
            Check(embedded.TryHandleAccept(), "embedded host owns its independent confirmation boundary");
            embedded.DrawFrame(new Rect(0, 0, 320, 240), window);
            Check(requests == 2 && embedded.Session.ActiveEditState == null, "explicit other-host confirmation follows the common request contract");
            window.PreClose();
            Check(!standard.Session.IsActive && embedded.Session.IsActive, "standard close disposes only its own session and leaves embedded use alive");
        }
        finally
        {
            Verse.Find.WindowStack.TryRemove(window);
            window.PreClose();
            UiNative.TextFieldOverride = null;
            UiNative.DebugMousePositionEnabled = false;
            UiNative.DebugEnter = false;
            UiNative.DebugFocusLost = false;
        }
    }

    private static void NestedScrollHost()
    {
        int requests = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("run", () => requests++);
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"r9-nested\">"
            + "<Scroll Id=\"outer\" Height=\"120\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Kind=\"chrome/rule\" Height=\"40\"/><Scroll Id=\"inner\" Height=\"60\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Kind=\"chrome/rule\" Height=\"30\"/><Widget Id=\"button\" Kind=\"input/button\" ActionBind=\"run\" Height=\"28\"/>"
            + "<Widget Kind=\"chrome/rule\" Height=\"100\"/></Scroll><Widget Kind=\"chrome/rule\" Height=\"100\"/></Scroll></UiPage>");
        using var host = new UiHost("r9-nested", manifest, bindings, UiTheme.Vanilla, new Metrics(), new Translation());
        Rect viewport = new(35, 45, 300, 180);
        host.MeasureAndArrange(new Vector2(viewport.width, viewport.height));
        host.Session.SetScrollPosition(host.Session.GetNodeByElementId("outer")!, new Vector2(0, 20));
        host.Session.SetScrollPosition(host.Session.GetNodeByElementId("inner")!, new Vector2(0, 15));
        Rect button = host.Session.GetNodeByElementId("button")!.Rect!.Value;
        Event previousEvent = Event.current;
        UiNative.DebugMousePositionEnabled = false;
        void Pump(EventType type, Vector2 point)
        {
            Event.current = new Event { type = type, button = 0, mousePosition = point };
            host.DrawFrame(viewport);
        }
        try
        {
            Pump(EventType.Repaint, new Vector2(-100, -100));
            Vector2 raw = new(viewport.x + button.x + 5, viewport.y + button.y + 5);
            Pump(EventType.MouseDown, raw);
            Pump(EventType.MouseUp, raw);
            Check(requests == 0, "unscrolled page coordinates do not activate a nested scrolled target");
            Vector2 visible = new(raw.x, raw.y - 20 - 15);
            Pump(EventType.MouseDown, visible);
            Check(requests == 0 && host.Session.Interactions.CaptureOwner.HasValue,
                "nested scroll and offset viewport map a real primary press to the shared owner");
            Pump(EventType.MouseUp, visible);
            KernelTripGuard.ExpectNoTrips(host.Session, "nested scroll host");
            Check(requests == 1 && !host.Session.Interactions.CaptureOwner.HasValue,
                "nested native scroll scopes release into one qualified shared activation");
            UiNode node = host.Session.GetNodeByElementId("button")!;
            Check(host.Observe(nodeKey: node.Id.Key).Nodes[0].Facts.Count(f => f.Name == "viewport-ancestor") == 2,
                "read projection retains both actual viewport ancestors without inventing a window hit result");
        }
        finally { Event.current = previousEvent; }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<UiHost> AbandonedHost()
    {
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"r9-weak\"><Widget Id=\"rule\" Kind=\"chrome/rule\"/></UiPage>");
        var host = new UiHost("r9-weak", manifest, new UiBindings(), UiTheme.Vanilla, new Metrics(), new Translation());
        return new WeakReference<UiHost>(host);
    }

    private static void DiscoveryLifetimeAndCapacity()
    {
        WeakReference<UiHost> abandoned = AbandonedHost();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Check(!abandoned.TryGetTarget(out _), "weak discovery never keeps an abandoned host alive");
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"r9-capacity\"><Widget Id=\"rule\" Kind=\"chrome/rule\"/></UiPage>");
        var hosts = new List<UiHost>();
        try
        {
            for (int i = 0; i < 513; i++)
                hosts.Add(new UiHost("r9-capacity", manifest, new UiBindings(), UiTheme.Vanilla, new Metrics(), new Translation()));
            UiHostDiscovery discovery = UiHost.ObserveLiveHosts(2);
            Check(discovery.Hosts.Count <= 2 && discovery.Truncated && discovery.HistoricalCapacityRefusals > 0, "discovery reports both caller budget and actual weak-index capacity refusals");
            Check(UiHost.ObserveLiveHost(hosts[512].Session.Identity).Status == UiObservationStatus.NotSampled, "capacity-omitted instance is unknown, not falsely reported nonexistent");
        }
        finally
        {
            foreach (UiHost host in hosts)
                host.Dispose();
        }

        Check(!UiHost.ObserveLiveHosts(128).Hosts.Any(item => item.Source == "r9-capacity"), "Close/Dispose removes indexed live entries without changing historical refusal evidence");
    }

    private static void BoundedCollections()
    {
        var options = new GuardedList<UiOption>(new[] { new UiOption("one", "1"), new UiOption("two", "2"), new UiOption("three", "3") });
        var bindings = new UiBindings();
        bindings.BindValue("choice", () => "2", _ => throw new Exception("observation cannot select"));
        bindings.BindOptions("choices", () => options);
        bindings.BindValue("static", () => "kept", _ => throw new Exception("observation cannot select"));
        bindings.BindOptions("static", () => (IReadOnlyList<string>)new[] { "ignored decoy" });
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"r9-options\">" + "<Widget Id=\"choice\" Kind=\"input/dropdown\" Bind=\"choice\" OptionsBind=\"choices\"/><Widget Id=\"static\" Kind=\"input/dropdown\" Bind=\"static\" Option1=\"static label\"/></UiPage>");
        using var host = new UiHost("r9-options", manifest, bindings, UiTheme.Vanilla, new Metrics(), new Translation());
        host.MeasureAndArrange(new Vector2(320, 240));
        options.MaximumRead = 2;
        UiHostObservation snapshot = host.Observe(new UiObservationBudget(items: 2));
        var facts = snapshot.Nodes.Single(node => node.Path == "choice").Facts;
        Check(facts.Any(f => f.Name == "option-count" && f.Value == "3") && facts.Any(f => f.Name == "option.1" && f.Value.Contains("two")) && facts.Any(f => f.Name == "options" && f.Truncated) && !facts.Any(f => f.Status == UiObservationStatus.Failed), "dynamic option projection reads only its budgeted prefix, with exact count and explicit truncation");
        Check(facts.Any(f => f.Name == "value" && f.Value == "2"), "option reads do not replace current selection with the first item");
        var staticFacts = snapshot.Nodes.Single(node => node.Path == "static").Facts;
        Check(staticFacts.Any(f => f.Name == "option.0" && f.Value == "static label|value=static label")
            && !staticFacts.Any(f => f.Value.Contains("ignored decoy")),
            "string dropdown observation preserves static option conditions instead of adopting an unrelated same-key option binding");
        bool refused = false;
        try
        {
            _ = new UiObservationBudget(nodes: 513);
        }
        catch (ArgumentOutOfRangeException)
        {
            refused = true;
        }

        Check(refused, "invalid budgets are refused rather than allocating unbounded output");
    }

    private sealed class GuardedList<T> : IReadOnlyList<T>
    {
        private readonly T[] items;
        public GuardedList(T[] items)
        {
            this.items = items;
        }

        public int MaximumRead = int.MaxValue;
        public int Count => items.Length;

        public T this[int index] => index < MaximumRead ? items[index] : throw new Exception("read beyond observation prefix");
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        Console.WriteLine("  ok: R9 " + message);
    }

    private sealed class BlockingWindow : Verse.Window
    {
        public BlockingWindow() { absorbInputAroundWindow = true; }
        public override void DoWindowContents(Rect rect) { }
    }
    private sealed class Metrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 20;
        public float MeasureWidth(string text, UiFont font) => text.Length * 8;
    }

    private sealed class Translation : IUiTranslation
    {
        public string Translate(string key) => key;
        public int TranslationRevision => 0;
    }
}
