using System;
using System.Collections.Generic;
using System.Threading;
using FerriteLib.UiKit.Kernel;

internal static class ReactiveFoundationTests
{
    internal static void Run(Action<bool, string> check)
    {
        void Check(bool value, string name) => check(value, "REACTIVE " + name);
        bool Throws(Action action, string contains = "")
        {
            try { action(); return false; } catch (Exception ex) { return ex.ToString().Contains(contains); }
        }
        using var counter = new CounterPageExample();
        var runtime = counter.Runtime;
        var b = counter.Bindings;
        using var view = b.CreatePresentation();
        view.Synchronize();
        b.Invoke("increment");
        Check(counter.Count.Value == 1 && counter.Observed == 1 && counter.Operations == 1, "command to immediate assignment dependency response without a window");
        view.Synchronize();
        using (view.Enter())
        {
            var plan = UiLayout.Stack(new UiLayoutFrame(120, 200, 0, 0, 0, 0, 0, 0, default),
                new[] { new UiLayoutChild(120) }, (i,w,h,c) => new UiLayoutSize(w, b.Get<string>("text").Length * 2));
            Check(plan.Slots[0].Height == 14 && b.Get<int>("count") == 1, "same production binding presentation drives neutral layout geometry");
            b.Invoke("increment");
            Check(counter.Count.Value == 2 && b.Get<int>("count") == 1, "business operation is immediate while current presentation remains fixed");
        }
        view.Synchronize();
        using (view.Enter()) Check(b.Get<int>("count") == 2, "next legal synchronization adopts pending input");
        int reactions = 0;
        using var observer = runtime.CreateOwner("test-business");
        observer.Respond("count", () => { _ = counter.Count.Value; reactions++; });
        using (b.BeginOperation())
        {
            b.Set("count", 4);
            Check(counter.Count.Value == 4 && b.Get<int>("count") == 4 && reactions == 1, "Set invalidates immediately and explicit Get is fresh inside outer batch");
            b.Invoke<int>("add", 3);
            b.TryInvokeCommand("increment");
            Check(counter.Count.Value == 8 && reactions == 1, "typed and try command nest into the same outer logical batch");
        }
        Check(reactions == 2 && counter.Observed == 8, "outer batch drains automatic response once");
        int before = reactions;
        b.Set("count", 8);
        Check(reactions == before, "same scalar storage skips invalidation");
        int operations = counter.Operations;
        b.Invoke<int>("add", 0); b.Invoke<int>("add", 0);
        Check(counter.Operations == operations + 2 && reactions == before, "two same-final-value business operations are never deduplicated");
        var selected = runtime.Boolean(); var left = runtime.Int(1); var right = runtime.Int(1);
        int runs = 0;
        using var child = observer.CreateChild("branch");
        var derived = child.Compute("selected", () => { runs++; return selected.Value ? right.Value : left.Value; });
        Check(derived.Value == 1 && runs == 1, "derived caches actual reads");
        selected.Value = true;
        Check(derived.Value == 1 && runs == 2, "same-result source replacement is reevaluated");
        left.Value = 5;
        Check(derived.Value == 1 && runs == 2, "old branch edge removed");
        right.Value = 6;
        Check(derived.Value == 6 && runs == 3, "new branch edge followed");
        using (runtime.Batch()) { right.Value = 7; Check(derived.Value == 7, "explicit derived read is never knowingly stale in batch"); }
        child.Dispose(); child.Dispose();
        Check(Throws(() => _ = derived.Value, "ObjectDisposedException"), "child disposal is idempotent and ends only its computations");
        var a = runtime.Int(); var z = runtime.Int(); int depth = 0, max = 0;
        observer.Respond("follow-write", () => { depth++; max = Math.Max(max, depth); int v=a.Value; if(v>0) z.Value=v; depth--; });
        int zSeen = 0; observer.Respond("follow-read", () => zSeen=z.Value);
        a.Value = 3;
        Check(zSeen == 3 && max == 1, "response writes schedule subsequent work without recursive subscriptions");
        var failSwitch = runtime.Boolean(); int failRuns=0;
        var fail = observer.Compute("failing", () => { failRuns++; int v=right.Value; if(failSwitch.Value) { _=left.Value; throw new Exception("intentional"); } return v; });
        _=fail.Value; failSwitch.Value=true;
        Check(Throws(() => _=fail.Value, "test-business/failing"), "failed computation preserves located cause");
        failSwitch.Value=false; Check(fail.Value==7, "failure restores temporary tracking context for explicit new attempt");
        var bad = observer.Compute("illegal-write", () => { a.Value++; return 1; });
        Check(Throws(() => _=bad.Value, "forbidden"), "tracked read cannot mutate business state");
        b.BindCommand("throw-after-write", () => { counter.Count.Value=30; throw new Exception("after-write"); });
        Check(Throws(() => b.Invoke("throw-after-write"), "after-write") && counter.Count.Value==30 && counter.Observed==30, "exception neither rolls back nor replays already written business");
        view.Synchronize(); int stable = b.Get<int>("count");
        bool failRead = true;
        b.BindTracked<int>("broken", () => failRead ? throw new Exception("sync-failure") : 12);
        Check(Throws(view.Synchronize, "presentation/broken"), "presentation synchronization failure is located");
        using(view.Enter()) Check(b.Get<int>("count")==stable, "failed synchronization does not publish a partial read view");
        failRead=false; view.Synchronize();
        using(view.Enter()) Check(b.Get<int>("broken")==12, "failed new binding can be explicitly synchronized later");
        Exception? accessFailure=null;
        var thread=new Thread(() => { try { _=a.Value; } catch(Exception ex) { accessFailure=ex; } }); thread.Start(); thread.Join();
        Check(accessFailure is InvalidOperationException, "wrong access context is refused rather than silently marshalled");
        var loop=runtime.Int(); using var loopOwner=runtime.CreateOwner("cycle-owner");
        loopOwner.Respond("loop", () => { int value=loop.Value; if(value>0) loop.Value=value+1; });
        Check(Throws(() => loop.Value=1, "cycle-owner/loop"), "response cycle is bounded and located");
        loopOwner.Dispose(); a.Value=4;
        Check(zSeen==4, "loop failure leaves runtime usable for independent owned work");
        int viewRuns=0; b.BindTracked("view-lifetime", () => { viewRuns++; return counter.Count.Value; });
        view.Synchronize(); view.Dispose(); view.Dispose();
        counter.Count.Value=31;
        Check(counter.Observed==31 && viewRuns==1, "view close twice removes graph edges without stopping business owner");

        var service=new WorkshopService(); using var projection=new WorkshopProjectionExample(service);
        using var workshopView=projection.Bindings.CreatePresentation(); workshopView.Synchronize();
        projection.Bindings.Invoke("complete"); workshopView.Synchronize();
        using(workshopView.Enter())
        {
            var plan=UiLayout.Stack(new UiLayoutFrame(160,200,0,0,0,0,0,0,default),new[]{new UiLayoutChild(160)},
                (i,w,h,c)=>new UiLayoutSize(w,projection.Bindings.Get<string>("text").Length));
            Check(plan.Slots[0].Height==11 && projection.Bindings.Get<int>("completed")==1, "independent service safe projection uses same binding graph presentation and geometry chain");
            service.SetAccepting(false); projection.Apply(service.ReadSnapshot());
            Check(!projection.Bindings.TryInvokeCommand("complete") && service.Requests==1, "latest projected CanRun veto survives stale presentation");
        }
        service.SetAccepting(true); service.Complete(); var safeSnapshot=service.ReadSnapshot();
        service.Complete(); projection.Apply(safeSnapshot);
        Check(projection.Completed.Value==2 && service.ReadSnapshot().Completed==3, "projection never reads mutable external state through a UI getter");
        workshopView.Dispose(); workshopView.Dispose(); service.Complete(); projection.Apply(service.ReadSnapshot());
        Check(projection.LastBusinessObservation==4, "independent authoritative service and response continue after view disposal");
        projection.Dispose(); service.Complete();
        Check(service.ReadSnapshot().Completed==5, "disposing projection does not dispose or roll back external service");
    }
}

