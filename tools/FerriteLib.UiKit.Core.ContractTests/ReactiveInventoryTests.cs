using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FerriteLib.UiKit.Kernel;

internal static class ReactiveInventoryTests
{
    internal static void Run(Action<bool, string> check)
    {
        void Check(bool value, string name) => check(value, "RU1 " + name);
        bool Throws(Action action, string text) { try { action(); return false; } catch (Exception ex) { return ex.ToString().Contains(text); } }
        using var page = new InventoryPageExample();
        UiReactiveRuntime runtime = page.Runtime; UiBindings b = page.Bindings;
        InventoryItem a = page.Items.Value[0], oldB = page.Items.Value[1];
        using var owner = runtime.CreateOwner("inventory-checks");
        int structures = 0;
        var size = owner.Compute("structure", () => { structures++; return page.Items.Value.Count; });
        Check(size.Value == 2 && page.Total.Value == 3, "ordinary object properties and controlled list feed the same production derived graph");
        int totalRuns = page.TotalRuns, observations = page.Observations;
        a.Count = 1;
        Check(page.TotalRuns == totalRuns && page.Observations == observations, "equal property writes skip data invalidation");
        using (b.BeginOperation())
        {
            a.Count = 4;
            Check(b.Get<int>("total") == 6 && page.Total.Value == 6 && page.Observations == observations, "business and explicit derived reads are current before batch drain");
            oldB.Count = 5;
        }
        Check(page.ObservedTotal == 9 && page.Observations == observations + 1 && size.Value == 2 && structures == 1, "item properties coalesce responses without invalidating structural readers");
        using var view = b.CreatePresentation(); view.Synchronize();
        view.Synchronize();
        int keys = page.KeyRuns, choices = page.ChoiceRuns;
        Console.WriteLine("RU1-FACT initial item subscriptions a=" + a.Subscribers + " b=" + oldB.Subscribers + " structure=" + page.Items.Value.SubscriberCount);
        a.Count = 6; view.Synchronize();
        Check(page.KeyRuns == keys && page.ChoiceRuns == choices + 1, "property change updates actual options but not structural key calculation");
        var originalStructure = page.Items.Value.Snapshot;
        bool readOnly = originalStructure is IList<InventoryItem> list && Throws(() => list.Add(a), "NotSupportedException");
        Check(readOnly, "structural snapshot cannot mutate backing storage around notification");
        using (view.Enter())
        {
            IReadOnlyList<UiChoice<int>> shown = b.GetOptions<UiChoice<int>>("selected");
            int responseTotal = 0; string responseLabel = "";
            using var responseOwner = owner.CreateChild("binding-response");
            responseOwner.Respond("current", () => { responseTotal = b.Get<int>("total"); responseLabel = b.GetOptions<UiChoice<int>>("selected")[0].Text; });
            page.Add("c", 3, 7); a.Count = 10;
            Check(responseTotal == 22 && responseLabel == "a: 10", "automatic response reads latest binding values and options while ordinary Draw input stays frozen");
            Check(shown.Count == 2 && shown[0].Text == "a: 6" && b.Get<int>("total") == 11 && b.GetOptions<UiChoice<int>>("selected")[0].Text == "a: 6", "same-round collection and item projection remain frozen after structural and property writes");
            b.TryGetChoices("selected", out var erased);
            b.TryReadObservationOptions("selected", 1, out var prefix, out int count);
            Check(erased.Count == 2 && (int)erased[0].Value! == 1 && prefix.Count == 1 && ((UiChoice<int>)prefix[0]!).Text == "a: 6" && count == 2 && b.Get<int>("selected") == 1, "typed choices and bounded read share synchronized options while same-name value channel stays distinct");
            b.BindCommand("latest-options", () => Check(b.GetOptions<UiChoice<int>>("selected").Count == 3 && b.Get<int>("total") == 22, "operation options and scalar reads bypass current presentation"));
            b.Invoke("latest-options");
        }
        Check(originalStructure.Count == 2, "held structural version does not change on Add");
        view.Synchronize();
        using (view.Enter())
        {
            IReadOnlyList<string> itemKeys = b.Get<IReadOnlyList<string>>("items");
            var geometry = UiLayout.Stack(new UiLayoutFrame(200, 400, 0, 0, 0, 0, 0, 0, default), itemKeys.Select(_ => new UiLayoutChild(200)).ToArray(),
                (i, w, h, c) => new UiLayoutSize(w, b.Get<string>("items." + itemKeys[i] + ".caption").Length));
            Check(geometry.Slots.Count == 3 && geometry.Slots[0].Height == 5 && b.GetOptions<UiChoice<int>>("selected")[0].Text == "a: 10", "real object-list projection and bindings drive neutral per-item geometry at next synchronization");
        }
        int structuralRevision = b.GetRevision("items"); totalRuns = page.TotalRuns; choices = page.ChoiceRuns;
        page.Items.Value.Move(0, 0); page.Items.Value.ReplaceAll(page.Items.Value.Snapshot); page.Items.Value.Remove(new InventoryItem(runtime, "absent", 9, 0));
        view.Synchronize();
        Check(b.GetRevision("items") == structuralRevision && page.TotalRuns == totalRuns && page.ChoiceRuns == choices, "Move-to-self equal replacement and absent removal are structural no-ops");
        page.Items.Value.Move(0, 2); view.Synchronize();
        using (view.Enter()) Check(b.Get<IReadOnlyList<string>>("items")[2] == "a" && b.GetOptions<UiChoice<int>>("selected")[2].Value == 1, "Move changes order in both key and options consumers");
        page.Items.Value.Remove(oldB); view.Synchronize();
        totalRuns = page.TotalRuns; choices = page.ChoiceRuns; int bReads = oldB.Reads;
        oldB.Count = 100; view.Synchronize();
        Check(page.TotalRuns == totalRuns && page.ChoiceRuns == choices && oldB.Reads == bReads && oldB.Subscribers == 0, "removed item loses total options and row property subscriptions");
        Console.WriteLine("RU1-FACT removed b subscriptions=" + oldB.Subscribers + " total-runs=" + page.TotalRuns + " options-runs=" + page.ChoiceRuns);
        var replacement = runtime.List(new[] { page.Items.Value[0], a }, (x, y) => ReferenceEquals(x, y));
        var previous = page.Items.Value; int priorRevision = view.Revision;
        page.Items.Value = replacement; view.Synchronize();
        Check(view.Revision == priorRevision, "same-result list-source replacement refreshes dependencies without false presentation change");
        _ = size.Value;
        Check(previous.SubscriberCount == 0, "old collection has no subscribers after every affected reader reevaluates");
        previous.Add(new InventoryItem(runtime, "old", 7, 7)); totalRuns = page.TotalRuns; view.Synchronize();
        Check(page.TotalRuns == totalRuns && page.Items.Value.Count == 2, "replaced old collection is disconnected");
        var focus = new InventoryItem(runtime, "focus", 8, a.Count);
        int unchangedFocus = b.GetRevision("focus");
        page.Focus.Value = focus; view.Synchronize(); int focusRevision = b.GetRevision("focus");
        Check(focusRevision == unchangedFocus, "equal-result object-source replacement updates following without false value announcement");
        a.Count++; view.Synchronize();
        Check(b.GetRevision("focus") == focusRevision && b.Get<int>("focus") == focus.Count, "same-value ordinary object replacement detaches old property edge");
        focus.Count++; view.Synchronize();
        Check(b.Get<int>("focus") == focus.Count && b.GetRevision("focus") == focusRevision + 1, "new ordinary object property is followed");
        using (var branchOwner = owner.CreateChild("conditional"))
        {
            var useA = runtime.Boolean(true); int branchRuns = 0;
            var branch = branchOwner.Compute("item", () => { branchRuns++; return useA.Value ? a.Count : focus.Count; });
            _ = branch.Value; useA.Value = false; _ = branch.Value; int before = branchRuns;
            a.Count++; _ = branch.Value;
            Check(branchRuns == before, "conditional ordinary item read removes the nonselected property dependency");
            focus.Count++; _ = branch.Value;
            Check(branchRuns == before + 1, "conditional ordinary item read follows its new property dependency");
        }
        var plain = new Mutable { Number = 1 }; var connected = runtime.Value(plain, (x, y) => ReferenceEquals(x, y)); int plainRuns = 0;
        var plainRead = owner.Compute("not-deep", () => { plainRuns++; return connected.Value.Number; });
        Check(plainRead.Value == 1, "reference policy is explicitly selected"); plain.Number = 2; connected.Value = plain;
        Check(plainRead.Value == 1 && plainRuns == 1, "ordinary in-place mutation is not silently deep observed");
        connected.Value = new Mutable { Number = 2 };
        Check(plainRead.Value == 2 && plainRuns == 2, "reference identity change invalidates explicitly connected value");
        var money = runtime.Value(1m, (x, y) => x == y); int moneyRuns = 0;
        var amount = owner.Compute("decimal", () => { moneyRuns++; return money.Value; }); _ = amount.Value; money.Value = 1m;
        Check(amount.Value == 1m && moneyRuns == 1, "other value types use caller-chosen equality and equal writes skip");
        Check(Throws(() => { using (runtime.ReadPhase()) page.Items.Value.Clear(); }, "forbidden"), "controlled structure cannot mutate in new pure phase");
        var guarded = new PrefixOnlyList();
        var boundedBindings = new UiBindings(runtime);
        boundedBindings.BindTrackedOptions<int>("bounded", () => guarded, (x, y) => x == y);
        boundedBindings.TryReadObservationOptions("bounded", 1, out var bounded, out int boundedCount);
        Check(boundedCount == 1000000 && bounded.Count == 1 && (int)bounded[0]! == 7 && guarded.IndexReads == 1, "current tracked options prefix reads only requested indices without enumerating or copying the whole list");
        var compareRuntime = new UiReactiveRuntime(); var comparisonValue = compareRuntime.Int(1); var comparisonBindings = new UiBindings(compareRuntime);
        comparisonBindings.BindTracked("value", () => comparisonValue.Value, equal: (x, y) => { comparisonValue.Value++; return x == y; });
        using (var compareView = comparisonBindings.CreatePresentation())
        {
            compareView.Synchronize(); comparisonValue.Value = 2;
            compareView.Synchronize(); Check(compareView.GetStatus("value").Fault!.ToString().Contains("forbidden") && comparisonValue.Value == 2, "new presentation equality cannot perform reactive writes");
        }
        var other = new UiReactiveRuntime(); var otherList = other.List(new[] { 1 }, (x, y) => x == y);
        var foreign = owner.Compute("foreign-list", () => otherList.Count);
        Check(Throws(() => _ = foreign.Value, "Cross-runtime"), "collection reads enforce same-graph access");
        int requests = page.Requests; b.TrySetTypedValue("selected", 1); b.TrySetTypedValue("selected", 1);
        Check(page.Requests == requests + 2, "valid same-value typed selections dispatch separately from equal storage");
        int oldTotal = b.Get<int>("total"); int oldRevision = view.Revision;
        var fail = runtime.Boolean(); b.BindTrackedOptions<int>("fault-options", () => fail.Value ? throw new Exception("options-cause") : new[] { 1 }, (x, y) => x == y);
        view.Synchronize(); oldRevision = view.Revision; int captionRevision = b.GetRevision("caption");
        fail.Value = true; a.Count++;
        view.Synchronize(); Check(view.GetStatus("fault-options",true).Fault!.ToString().Contains("options-cause") && view.Revision > oldRevision && b.GetRevision("caption") > captionRevision, "RU3 failed options are invalid while independent complete values publish");
        using (view.Enter()) Check(b.Get<int>("total") == page.Total.Value && Throws(()=>_=b.GetOptions<int>("fault-options"),"options-cause") && view.GetStatus("fault-options",true).HasLastValue, "RU3 old collection is retained as display, never returned as a current successful read");
        fail.Value = false; view.Synchronize();
        Check(b.Get<int>("total") == page.Total.Value, "explicit next synchronization restores tracking without operation replay");
        view.Dispose(); view.Dispose(); int seen = page.Observations; a.Count++;
        Check(page.Observations == seen + 1 && a.Subscribers == 1 && focus.Subscribers == 0, "view closes twice without releasing business subscriptions");
        Console.WriteLine("RU1-FACT view closed subscriptions a=" + a.Subscribers + " focus=" + focus.Subscribers + " business-total=" + page.ObservedTotal);
        External(Check, Throws);
    }
    private static void External(Action<bool, string> check, Func<Action, string, bool> throws)
    {
        var service = new OrderBoardService(); using var projection = new OrderBoardProjectionExample(service);
        var page = projection.Page; using var view = page.Bindings.CreatePresentation(); view.Synchronize();
        page.Bindings.Invoke("complete"); view.Synchronize();
        using (view.Enter())
        {
            var commandGeometry = UiLayout.Stack(new UiLayoutFrame(100, 100, 0, 0, 0, 0, 0, 0, default), new[] { new UiLayoutChild(100) }, (i, w, h, c) => new UiLayoutSize(w, page.Bindings.Get<int>("total")));
            check(service.Requests == 1 && page.ObservedTotal == 4 && commandGeometry.Slots[0].Height == 4, "accepted independent multi-item command returns through safe result batch dependencies response bindings and neutral geometry");
        }
        using var owner = page.Runtime.CreateOwner("external-independent-read"); int aRuns = 0, bRuns = 0;
        var aOnly = owner.Compute("a", () => { aRuns++; return page.Find("a")!.Count; });
        var bOnly = owner.Compute("b", () => { bRuns++; return page.Find("b")!.Count; });
        _ = aOnly.Value; _ = bOnly.Value;
        int totals = page.TotalRuns, options = page.ChoiceRuns, keys = page.KeyRuns, observed = page.Observations;
        projection.Apply(service.ReadSnapshot()); view.Synchronize();
        check(page.TotalRuns == totals && page.ChoiceRuns == options && page.KeyRuns == keys && page.Observations == observed, "full unchanged safe business result does not dirty every source or computation");
        OrderBoardService.Snapshot? delivered = null;
        var producer = new Thread(() => { service.Replace(new OrderBoardService.Row("a", 1, 9), new OrderBoardService.Row("b", 2, 2)); delivered = service.ReadSnapshot(); service.Increment(); });
        producer.Start(); producer.Join();
        Exception? access = null; var wrong = new Thread(() => { try { projection.Apply(delivered!); } catch (Exception ex) { access = ex; } }); wrong.Start(); wrong.Join();
        check(access is InvalidOperationException && page.Total.Value == 4, "external result must first be delivered onto projection serial context, not merely wrapped in Batch");
        InventoryItem b = page.Find("b")!; int bReads = b.Reads;
        projection.Apply(delivered!); view.Synchronize();
        _ = aOnly.Value; _ = bOnly.Value;
        check(page.Total.Value == 11 && service.ReadSnapshot().Rows[0].Count == 10 && delivered!.Rows[0].Count == 9 && b.Reads > bReads && page.KeyRuns == keys && aRuns == 2 && bRuns == 1, "safe multi-item result stays detached from later background writes and changes only actual property sources");
        Console.WriteLine("RU1-FACT safe result a=9 current business a=10 independent a-runs=" + aRuns + " b-runs=" + bRuns + " key-runs=" + page.KeyRuns);
        using (view.Enter())
        {
            var plan = UiLayout.Stack(new UiLayoutFrame(100, 100, 0, 0, 0, 0, 0, 0, default), new[] { new UiLayoutChild(100) }, (i, w, h, c) => new UiLayoutSize(w, page.Bindings.Get<int>("total")));
            check(plan.Slots[0].Height == 11 && page.Bindings.GetOptions<UiChoice<int>>("selected")[0].Text == "a: 9", "independent safe multi-item projection uses same production binding presentation and geometry");
            service.SetAccepting(false); projection.Apply(service.ReadSnapshot()); int requests = service.Requests;
            check(!page.Bindings.TryInvokeCommand("complete") && service.Requests == requests, "latest projected qualification wins over stale current presentation");
        }
        service.SetAccepting(true); projection.Apply(service.ReadSnapshot()); service.SetAccepting(false); int before = service.ReadSnapshot().Rows[0].Count;
        check(page.Bindings.TryInvokeCommand("complete") && service.ReadSnapshot().Rows[0].Count == before && !projection.Accepting.Value, "independent business rejects an otherwise qualified request");
        view.Dispose(); view.Dispose(); service.SetAccepting(true); service.Increment(); projection.Apply(service.ReadSnapshot());
        check(page.ObservedTotal == service.ReadSnapshot().Rows.Sum(row => row.Count), "external business and independent response remain active after view closure");
        projection.Dispose(); check(service.Increment(), "closing projection does not dispose external business service");
    }
    private sealed class Mutable { internal int Number; }
    private sealed class PrefixOnlyList : IReadOnlyList<int>
    {
        internal int IndexReads;
        public int Count => 1000000;
        public int this[int index] { get { if (index != 0) throw new Exception("read past prefix"); IndexReads++; return 7; } }
        public IEnumerator<int> GetEnumerator() => throw new Exception("must not enumerate prefix-only list");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
