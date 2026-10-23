using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FerriteLib.UiKit.Kernel;

internal static class ReactiveListWriteTests
{
    internal static void Run(Action<bool, string> check)
    {
        void Check(bool value, string name) => check(value, "RU1-WRITE " + name);
        Writes(Check);
        Reads(Check);
        Guards(Check);
        Presentation(Check);
    }

    private static bool Throws(Action action, string text)
    {
        try { action(); return false; }
        catch (Exception ex) { return ex.ToString().Contains(text); }
    }

    private static void Writes(Action<bool, string> check)
    {
        var runtime = new UiReactiveRuntime();
        var output = runtime.List(Array.Empty<int>(), (a, b) => a == b);
        using var owner = runtime.CreateOwner("list-write");
        int runs = 0;
        owner.Respond("append", () => { runs++; output.Add(1); });
        check(runs == 1 && output.Snapshot.SequenceEqual(new[] { 1 }) && output.SubscriberCount == 0,
            "no-input Add response runs once with exactly one item and zero output subscribers");
        output.Add(2);
        check(runs == 1 && output.SubscriberCount == 0, "external output change does not recall write-only response");
        Console.WriteLine("RU1-WRITE-FACT no-input runs=" + runs + " output-count=" + output.Count + " output-subscribers=" + output.SubscriberCount);

        void Mutation(string name, int[] initial, Action<UiReactiveList<int>> write, int[] expected, bool changed)
        {
            var list = runtime.List(initial, (a, b) => a == b);
            using var scope = owner.CreateChild(name);
            int consumerRuns = 0, writerRuns = 0;
            scope.Respond("consumer", () => { _ = list.Snapshot; consumerRuns++; });
            scope.Respond("writer", () => { writerRuns++; write(list); });
            check(writerRuns == 1 && list.Snapshot.SequenceEqual(expected) && list.SubscriberCount == 1 && consumerRuns == (changed ? 2 : 1),
                name + " preserves result and structural invalidation without subscribing its writer");
            list.Add(99);
            check(writerRuns == 1 && consumerRuns == (changed ? 3 : 2), name + " keeps true consumers active without recalling writer");
        }

        int[] three = { 1, 2, 3 };
        Mutation("Add", three, list => list.Add(4), new[] { 1, 2, 3, 4 }, true);
        Mutation("RemoveAt", three, list => list.RemoveAt(1), new[] { 1, 3 }, true);
        Mutation("Remove", three, list => check(list.Remove(2), "present Remove returns true"), new[] { 1, 3 }, true);
        Mutation("absent Remove", three, list => check(!list.Remove(9), "absent Remove returns false"), three, false);
        Mutation("Move", three, list => list.Move(0, 2), new[] { 2, 3, 1 }, true);
        Mutation("Move-to-self", three, list => list.Move(1, 1), three, false);
        Mutation("Clear", three, list => list.Clear(), Array.Empty<int>(), true);
        Mutation("empty Clear", Array.Empty<int>(), list => list.Clear(), Array.Empty<int>(), false);
        Mutation("ReplaceAll", three, list => list.ReplaceAll(new[] { 4, 5 }), new[] { 4, 5 }, true);
        Mutation("equal ReplaceAll", three, list => list.ReplaceAll(new[] { 1, 2, 3 }), three, false);
    }

    private static void Reads(Action<bool, string> check)
    {
        var runtime = new UiReactiveRuntime();
        var list = runtime.List(new[] { 1, 2 }, (a, b) => a == b);
        using var owner = runtime.CreateOwner("list-reads");
        var readers = new Func<int>[] { () => list.Snapshot.Count, () => list.Count, () => list[0], () => list.Sum() };
        var values = new List<UiReactiveComputed<int>>();
        int reads = 0;
        foreach (var read in readers) values.Add(owner.Compute("public-" + values.Count, () => { reads++; return read(); }));
        foreach (var value in values) _ = value.Value;
        list.Add(3);
        foreach (var value in values) _ = value.Value;
        check(reads == 8 && list.SubscriberCount == 4 && values[0].Value == 3 && values[3].Value == 6,
            "public Snapshot Count index and enumeration still subscribe and invalidate");

        int explicitRuns = 0;
        owner.Respond("explicit-read-before-write", () => { explicitRuns++; _ = list.Count; list.Remove(999); });
        list.Add(4);
        check(explicitRuns == 2 && list.SubscriberCount == 5, "write leaves an explicit earlier output read subscribed");

        var source = runtime.List(new[] { 1 }, (a, b) => a == b);
        var destination = runtime.List(Array.Empty<int>(), (a, b) => a == b);
        int copies = 0;
        owner.Respond("copy-observable-source", () => { copies++; destination.ReplaceAll(source); });
        source.Add(2);
        destination.Add(9);
        check(copies == 2 && source.SubscriberCount == 1 && destination.SubscriberCount == 0 && destination.Snapshot.SequenceEqual(new[] { 1, 2, 9 }),
            "ReplaceAll really enumerates and follows observable input without observing destination");

        void Comparison(string name, Action<UiReactiveList<int>> write)
        {
            var policy = runtime.Int();
            var compared = runtime.List(new[] { 1, 2 }, (a, b) => { _ = policy.Value; return a == b; });
            using var scope = owner.CreateChild(name);
            int comparisons = 0;
            scope.Respond("writer", () => { comparisons++; write(compared); });
            policy.Value++;
            check(comparisons == 2 && policy.SubscriberCount == 1 && compared.SubscriberCount == 0,
                name + " retains real reactive reads inside caller equality callback");
        }
        Comparison("Remove comparison", compared => compared.Remove(999));
        Comparison("ReplaceAll comparison", compared => compared.ReplaceAll(new[] { 1, 2 }));
        Comparison("Move comparison", compared => compared.Move(0, 1));
    }

    private static void Guards(Action<bool, string> check)
    {
        var runtime = new UiReactiveRuntime();
        var list = runtime.List(new[] { 1, 2 }, (a, b) => a == b);
        using var owner = runtime.CreateOwner("list-guards");
        var writes = new Action[] { () => list.Add(3), () => list.RemoveAt(0), () => list.Remove(99), () => list.Move(0, 0), () => list.Clear(), () => list.ReplaceAll(new[] { 1, 2 }) };
        using (runtime.ReadPhase())
            check(writes.All(write => Throws(write, "forbidden")) && list.Snapshot.SequenceEqual(new[] { 1, 2 }),
                "all six write entrances including no-ops retain pure-read prohibition");
        var bad = owner.Compute("pure-writer", () => { list.Add(3); return 0; });
        check(Throws(() => _ = bad.Value, "forbidden") && list.Count == 2, "tracked calculation cannot write a list");

        var foreignRuntime = new UiReactiveRuntime();
        var foreign = foreignRuntime.List(new[] { 1, 2 }, (a, b) => a == b);
        var foreignValue = foreignRuntime.Int();
        var crossing = owner.Compute("cross-runtime", () => foreignValue.ReadCurrent());
        check(Throws(() => _ = crossing.Value, "Cross-runtime"), "new internal current-value seam itself verifies runtime ownership");
        var foreignResponse=owner.RespondManaged("foreign-write", () => foreign.Add(3));
        check(foreignResponse.Fault!.ToString().Contains("Cross-runtime") && foreign.Count == 2,
            "response list write cannot cross runtime ownership");
        Exception? threadFailure = null, seamFailure = null;
        var thread = new Thread(() =>
        {
            try { list.Add(3); } catch (Exception ex) { threadFailure = ex; }
            try { _ = foreignValue.ReadCurrent(); } catch (Exception ex) { seamFailure = ex; }
        });
        thread.Start(); thread.Join();
        check(threadFailure is InvalidOperationException && seamFailure is InvalidOperationException && list.Count == 2,
            "list write and internal current-value seam both reject foreign threads");

        var badIndex=owner.RespondManaged("bad-index", () => list.RemoveAt(99));
        check(badIndex.Fault!.ToString().Contains("ArgumentOutOfRangeException") && list.SubscriberCount == 0,
            "failed mutation keeps original cause and no provisional subscriptions");
        owner.Respond("after-failure", () => list.Add(3));
        foreign.Add(3);
        check(list.Count == 3 && list.SubscriberCount == 0 && foreign.Count == 3, "access purity and mutation failures restore usable tracking context");

        var loop = runtime.List(Array.Empty<int>(), (a, b) => a == b);
        var looping=owner.RespondManaged("explicit-list-loop", () => loop.Add(loop.Count));
        check(looping.Fault?.Kind=="propagation-budget" && loop.Count == 256 && loop.SubscriberCount == 1,
            "genuine explicit list self-read then write still reaches located finite cycle exit");
        owner.Respond("after-cycle", () => loop.Add(257));
        check(loop.Count == 257 && loop.SubscriberCount == 1, "cycle failure leaves runtime usable without swallowing applied writes");
    }

    private static void Presentation(Action<bool, string> check)
    {
        using var page = new ResponseListPageExample();
        var bindings = page.Bindings;
        using var view = bindings.CreatePresentation();
        view.Synchronize();
        using (view.Enter())
        {
            using (bindings.BeginOperation())
            {
                bindings.Invoke("increment");
                check(page.Input.Value == 2 && page.Runs == 1 && page.Output.Count == 1,
                    "neutral operation changes explicit input while outer batch defers list-producing response");
            }
            check(page.Runs == 2 && page.Output.Snapshot.SequenceEqual(new[] { 1, 2 }) && bindings.Get<string>("text") == "1",
                "input response appends once while current binding presentation stays frozen");
        }
        view.Synchronize();
        using (view.Enter())
        {
            var geometry = UiLayout.Stack(new UiLayoutFrame(120, 100, 0, 0, 0, 0, 0, 0, default),
                new[] { new UiLayoutChild(120) }, (i, w, h, c) => new UiLayoutSize(w, bindings.Get<string>("text").Length));
            check(geometry.Slots[0].Height == 3 && bindings.Get<string>("text") == "1,2" && page.Input.SubscriberCount == 1 && page.Output.SubscriberCount == 1,
                "same production input response output Bindings presentation and neutral geometry connect with only real subscriptions");
        }
        page.Output.Add(9); view.Synchronize();
        using (view.Enter()) check(page.Runs == 2 && bindings.Get<string>("text") == "1,2,9",
            "external output write updates true presentation consumer without recalling its producer");
        Console.WriteLine("RU1-WRITE-FACT E2E producer-runs=" + page.Runs + " input-subscribers=" + page.Input.SubscriberCount + " output-subscribers=" + page.Output.SubscriberCount);
    }
}
