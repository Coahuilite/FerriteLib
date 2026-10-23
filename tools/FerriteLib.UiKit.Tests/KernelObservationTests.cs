using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

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
            RepairedProjectionFacts();
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
        host.MeasureAndArrange(new UiLayoutPoint(320, 240));
        UiNode node = host.Session.GetNodeByElementId("text")!;
        var ctx = host.CreateContext(320).WithNode(node);
        bool Parse(string value, out string parsed)
        {
            parsed = value;
            return true;
        }

        var editor = NativeFixture.Editor<string>(ctx, "text", value => value, Parse);
        editor.Observe(model);
        editor.Input("local draft", true, false, false, _ => throw new Exception("unexpected publication"), () => model);
        int frame = host.Session.Frame;
        NativeFixture.TextFieldOverride = (_, _) => throw new Exception("observation must not paint");
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
            host.MeasureAndArrange(new UiLayoutPoint(320, 240));
            UiHostObservation hidden = host.Observe(nodeKey: node.Id.Key);
            Check(hidden.Nodes.Count == 1 && hidden.Reason == "retained-node-without-current-arranged-entry"
                && hidden.Nodes[0].Facts.Any(f => f.Name == "geometry" && f.Status == UiObservationStatus.NotSampled)
                && hidden.Nodes[0].Facts.Any(f => f.Name == "draft" && f.Value == "local draft"),
                "known hidden editor remains locatable without inventing geometry or reading an old model declaration");
        }
        finally
        {
            NativeFixture.TextFieldOverride = null;
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
        window.windowRect = VerseUiExecution.Rect(new UiLayoutRect(0, 0, 500, 400));
        Verse.Find.WindowStack.Add(window);
        NativeFixture.DebugMousePositionEnabled = true;
        NativeFixture.DebugMouseDown = false;
        NativeFixture.DebugEnter = false;
        NativeFixture.DebugFocusLost = false;
        NativeFixture.TextFieldOverride = (_, text) => text;
        try
        {
            NativeEvents.Begin(EventType.Repaint,new UiLayoutPoint(-10,-10));window.WindowOnGUI();NativeFixture.DrawFrame(embedded,VerseUiExecution.Rect(new UiLayoutRect(0,0,320,240)),window);
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
                var editor = NativeFixture.Editor<string>(host.CreateContext(320).WithNode(node), "text", value => value, Parse);
                editor.Observe(model);
                editor.Focus();
            }

            NativeFixture.TextFieldOverride = (_, _) => "candidate";
            NativeEvents.Begin(EventType.Repaint,new UiLayoutPoint(-10,-10));window.WindowOnGUI();NativeFixture.DrawFrame(embedded,VerseUiExecution.Rect(new UiLayoutRect(0,0,320,240)),window);
            Check(requests == 0 && standard!.Session.ActiveEditState != null && embedded.Session.ActiveEditState != null, "both host routes hold the same deferred common edit without publishing on paint");
            var modal = new BlockingWindow();
            Verse.Find.WindowStack.Add(modal);
            try
            {
                NativeFixture.TextFieldOverride = (_, text) => text;
                NativeEvents.Begin(EventType.Repaint,new UiLayoutPoint(-10,-10));window.WindowOnGUI();NativeFixture.DrawFrame(embedded,VerseUiExecution.Rect(new UiLayoutRect(0,0,320,240)),window);
                Check(!embedded.Session.PointerInputAllowed && !standard!.Session.PointerInputAllowed && requests == 0
                    && standard.Observe().Nodes[0].Facts.Any(f => f.Name == "draft" && f.Value == "candidate"),
                    "real shell stack qualification suspends input under a modal without publishing or discarding the draft");
            }
            finally { Verse.Find.WindowStack.TryRemove(modal); }
            UiNode standardNode = standard!.Session.GetNodeByElementId("text")!;
            NativeFixture.Editor<string>(standard.CreateContext(320).WithNode(standardNode), "text", value => value, Parse).Focus();
            UiNode embeddedNode = embedded.Session.GetNodeByElementId("text")!;
            NativeFixture.Editor<string>(embedded.CreateContext(320).WithNode(embeddedNode), "text", value => value, Parse).Focus();
            NativeEvents.Begin(EventType.Repaint,new UiLayoutPoint(-10,-10));NativeFixture.DrawFrame(embedded,VerseUiExecution.Rect(new UiLayoutRect(0,0,320,240)),window);window.WindowOnGUI();
            Check(standard.Session.PointerInputAllowed && embedded.Session.PointerInputAllowed && standard.TryHandleAccept(),
                "standard host regains stack-qualified input and confirms its retained editor");
            NativeFixture.TextFieldOverride = (_, text) => text;
            window.WindowOnGUI();
            Check(requests == 1 && model == "candidate" && embedded.Session.ActiveEditState != null, "standard confirmation requests once and does not clear the other host editor");
            Check(embedded.TryHandleAccept(), "embedded host owns its independent confirmation boundary");
            NativeFixture.DrawFrame(            embedded,VerseUiExecution.Rect(new UiLayoutRect(0, 0, 320, 240)), window);
            Check(requests == 2 && embedded.Session.ActiveEditState == null, "explicit other-host confirmation follows the common request contract");
            window.PreClose();
            Check(!standard.Session.IsActive && embedded.Session.IsActive, "standard close disposes only its own session and leaves embedded use alive");
        }
        finally
        {
            Verse.Find.WindowStack.TryRemove(window);
            window.PreClose();
            NativeFixture.TextFieldOverride = null;
            NativeFixture.DebugMousePositionEnabled = false;
            NativeFixture.DebugEnter = false;
            NativeFixture.DebugFocusLost = false;
        }
    }

    private enum SelectionValue { First, Second, Missing }

    private static void RepairedProjectionFacts()
    {
        SelectionValue current = SelectionValue.Missing;
        float progress = 3;
        bool progressUnavailable = false;
        using var native=new VerseUiExecution();UiImage texture=default;
        int writes = 0;
        var bindings = new UiBindings();
        var choices = new[] { new UiChoice<SelectionValue>("same label", SelectionValue.First), new UiChoice<SelectionValue>("same label", SelectionValue.Second) };
        bindings.BindValue("typed", () => current, value => { current = value; writes++; });
        bindings.BindChoices("choices", () => (IReadOnlyList<UiChoice<SelectionValue>>)choices);
        bindings.BindReadOnly("mode", () => SelectionValue.Second);
        bindings.BindChoices("mode", () => (IReadOnlyList<UiChoice<SelectionValue>>)choices);
        bindings.BindValue("sparse", () => "later", _ => writes++);
        bindings.BindValue("sparse-mode", () => "later", _ => writes++);
        bindings.BindReadOnly<UiImage>("picture", () => texture);
        bindings.BindReadOnly("progress", () => progressUnavailable ? throw new InvalidOperationException("unavailable") : progress);
        bindings.BindAction<UiChartPointChange>("point", _ => writes++);
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"r9-facts\"><Stack Id=\"root\" Padding=\"0\" Gap=\"0\"><Widget Id=\"first\" Kind=\"input/dropdown\" Bind=\"typed\" OptionsBind=\"choices\"/><Widget Id=\"second\" Kind=\"input/dropdown\" Bind=\"typed\" OptionsBind=\"choices\"/><Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\"/><Widget Id=\"sparse\" Kind=\"input/dropdown\" Bind=\"sparse\"   ><Choice Value=\"later\" Text=\"later label\" Help=\"later\"/><Choice Value=\"fourth\" Text=\"fourth\" Help=\"fourth\"/></Widget><Widget Id=\"sparse-mode\" Kind=\"input/mode-row\" Bind=\"sparse-mode\"   ><Choice Value=\"later\" Text=\"later label\" Help=\"later\"/><Choice Value=\"fourth\" Text=\"fourth\" Help=\"fourth\"/></Widget><Widget Id=\"image\" Kind=\"display/image\" Bind=\"picture\" Height=\"28\"/><Widget Id=\"progress\" Kind=\"display/progress\" Bind=\"progress\" Max=\"2\"/><Widget Id=\"chart\" Kind=\"chart/line\" Points=\"0,0;1,1\" Editable=\"0\" ActionBind=\"point\"/></Stack></UiPage>");
        using var host = new UiHost("r9-facts", manifest, bindings, UiTheme.Vanilla, new Metrics(), new Translation());
        host.Session.SetFrame(native.CaptureFrame(),native);
        Event previousEvent = Event.current;
        NativeFixture.DebugMousePositionEnabled = false;
        try
        {
            Event.current = new Event { type = EventType.Repaint };
            NativeFixture.DrawFrame(            host,new UiLayoutRect(0, 0, 400, 800));
            UiHostObservation sample = host.Observe(new UiObservationBudget(items: 1));
            IReadOnlyList<UiObservedFact> Facts(string id) => sample.Nodes.Single(node => node.Path == "root/" + id).Facts;
            bool Has(string id, string name, string value) => Facts(id).Any(f => f.Name == name && f.Value == value);
            Check(Has("image", "available", "False") && Has("progress", "min", "0") && Has("progress", "max", "2")
                && Has("progress", "finite", "True") && Has("progress", "fraction", "1") && Has("progress", "value", "3"),
                "description projection preserves known-null Image and full actual Progress range versus clamped fraction");
            Check(Has("first", "option.0.text", "same label") && Has("first", "option.0.value", "First")
                && Has("first", "option.0.selectable", "True") && Has("first", "selection.missing", "True")
                && Has("mode", "selection.missing", "False") && Has("mode", "option.0.selectable", "False"),
                "typed option values and selectability remain distinct from duplicate labels and actual wrapper Missing");
            foreach (string id in new[] { "sparse", "sparse-mode" })
                Check(Has(id, "option-count", "2") && Has(id,"option.0.text","later label") && !Facts(id).Any(f=>f.Name=="option.1.value") && Has(id, "option.0.value", "later")
                    && Facts(id).Any(f => f.Name == "options" && f.Truncated),
                    "sparse static " + id + " counts actual Choice entries, reads only the first under Items1 and reports the omitted second");
            Check(Has("chart", "editable", "False"), "Editable0 observation uses the exact production chart capability");
            sample = host.Observe(new UiObservationBudget(items: 2));
            Check(Has("first", "option.1.text", "same label") && Has("first", "option.1.value", "Second"),
                "duplicate typed labels do not merge distinct stable values");
            texture = native.Images.Add(new Texture2D(2,2));
            progress = float.NaN;
            sample = host.Observe();
            Check(Has("image", "available", "True") && Has("progress", "finite", "False") && Has("progress", "value", "NaN"),
                "fresh description facts keep available image and nonfinite actual model distinct from a fill answer");
            progressUnavailable = true;
            sample = host.Observe();
            Check(Facts("progress").Any(f => f.Name == "description" && f.Status == UiObservationStatus.NotSampled)
                && !Facts("progress").Any(f => f.Name == "value"), "unavailable progress remains unknown, not zero");
            UiNode second = host.Session.GetNodeByElementId("second")!;
            UiLayoutRect trigger = second.Rect!.Value;
            UiLayoutPoint point = new(trigger.X + 5, trigger.Y + 5);
            Event.current = new Event { type = EventType.MouseDown, button = 0, mousePosition = VerseUiExecution.Point(point) };
            NativeFixture.DrawFrame(            host,new UiLayoutRect(0, 0, 400, 800));
            Event.current = new Event { type = EventType.MouseUp, button = 0, mousePosition = VerseUiExecution.Point(point) };
            NativeFixture.DrawFrame(            host,new UiLayoutRect(0, 0, 400, 800));
            UiHostObservation popup = host.Observe();
            Check(popup.Facts.Any(f => f.Name == "popup-owner-node" && f.Value == second.Id.Key)
                && popup.Facts.Any(f => f.Name == "popup-owner-part" && f.Value == "trigger")
                && !popup.Facts.Any(f => f.Name == "popup-owner-node" && f.Value == host.Session.GetNodeByElementId("first")!.Id.Key),
                "same-Bind popup observation identifies its real stable target owner, not the shared binding");
            Check(writes == 0, "all projection and popup-owner checks perform no business write");
        }
        finally { Event.current = previousEvent; }
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
        UiLayoutRect viewport = new(35, 45, 300, 180);
        host.MeasureAndArrange(new UiLayoutPoint(viewport.Width, viewport.Height));
        host.Session.SetScrollPosition(host.Session.GetNodeByElementId("outer")!, new UiLayoutPoint(0, 20));
        host.Session.SetScrollPosition(host.Session.GetNodeByElementId("inner")!, new UiLayoutPoint(0, 15));
        UiLayoutRect button = host.Session.GetNodeByElementId("button")!.Rect!.Value;
        Event previousEvent = Event.current;
        NativeFixture.DebugMousePositionEnabled = false;
        void Pump(EventType type, UiLayoutPoint point)
        {
            Event.current = new Event { type = type, button = 0, mousePosition = VerseUiExecution.Point(point) };
            NativeFixture.DrawFrame(            host,viewport);
        }
        try
        {
            Pump(EventType.Repaint, new UiLayoutPoint(-100, -100));
            UiLayoutPoint raw = new(viewport.X + button.X + 5, viewport.Y + button.Y + 5);
            Pump(EventType.MouseDown, raw);
            Pump(EventType.MouseUp, raw);
            Check(requests == 0, "unscrolled page coordinates do not activate a nested scrolled target");
            UiLayoutPoint visible = new(raw.X, raw.Y - 20 - 15);
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
    private static System.WeakReference<UiHost> AbandonedHost()
    {
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"r9-weak\"><Widget Id=\"rule\" Kind=\"chrome/rule\"/></UiPage>");
        var host = new UiHost("r9-weak", manifest, new UiBindings(), UiTheme.Vanilla, new Metrics(), new Translation());
        return new System.WeakReference<UiHost>(host);
    }

    private static void DiscoveryLifetimeAndCapacity()
    {
        System.WeakReference<UiHost> abandoned = AbandonedHost();
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
        var options = new GuardedList<UiChoice<string>>(new[] { new UiChoice<string>("one", "1"), new UiChoice<string>("two", "2"), new UiChoice<string>("three", "3") });
        var bindings = new UiBindings();
        bindings.BindValue("choice", () => "2", _ => throw new Exception("observation cannot select"));
        bindings.BindChoices("choices", () => options);
        bindings.BindValue("static", () => "kept", _ => throw new Exception("observation cannot select"));
        bindings.BindOptions("static", () => (IReadOnlyList<string>)new[] { "ignored decoy" });
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"r9-options\"><Widget Id=\"choice\" Kind=\"input/dropdown\" Bind=\"choice\" OptionsBind=\"choices\"/><Widget Id=\"static\" Kind=\"input/dropdown\" Bind=\"static\" ><Choice Value=\"static label\" Text=\"static label\" Help=\"static label\"/></Widget></UiPage>");
        using var host = new UiHost("r9-options", manifest, bindings, UiTheme.Vanilla, new Metrics(), new Translation());
        host.MeasureAndArrange(new UiLayoutPoint(320, 240));
        options.MaximumRead = 2;
        UiHostObservation snapshot = host.Observe(new UiObservationBudget(items: 2));
        var facts = snapshot.Nodes.Single(node => node.Path == "choice").Facts;
        Check(facts.Any(f => f.Name == "option-count" && f.Value == "3") && facts.Any(f => f.Name == "option.1.text" && f.Value == "two") && facts.Any(f => f.Name == "options" && f.Truncated) && !facts.Any(f => f.Status == UiObservationStatus.Failed), "dynamic option projection reads only its budgeted prefix, with exact count and explicit truncation");
        Check(facts.Any(f => f.Name == "value" && f.Value == "2"), "option reads do not replace current selection with the first item");
        var staticFacts = snapshot.Nodes.Single(node => node.Path == "static").Facts;
        Check(staticFacts.Any(f => f.Name == "option.0.value" && f.Value == "static label")
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
        public override void DoWindowContents(UnityEngine.Rect rect) { }
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
