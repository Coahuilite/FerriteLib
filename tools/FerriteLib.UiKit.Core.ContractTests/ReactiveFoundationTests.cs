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
        ImmediateQualification(Check, Throws);
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
        var interactions = new UiInteractionSession();
        var target = new UiInteractionTarget("neutral-checkbox", "counter");
        var operation = UiValueBehavior.Boolean(interactions.Activate(target, new UiInputEligibility(true)), true);
        int bothSeen = 0; bool selectedSeen = false;
        using (var both = runtime.CreateOwner("neutral-checkbox-business"))
        {
            both.Respond("both", () => { selectedSeen = counter.Selected.Value; _ = counter.Count.Value; bothSeen++; });
            UiValueBehavior.Request(target, operation, b, value => { b.Set("selected", value); b.Invoke("changed"); });
            Check(bothSeen == 2 && selectedSeen && counter.Count.Value == 3, "neutral shared behavior Set-to-Invoke uses one logical operation before presentation geometry");
            view.Synchronize();
            using(view.Enter())
            {
                var plan=UiLayout.Stack(new UiLayoutFrame(100,100,0,0,0,0,0,0,default),new[]{new UiLayoutChild(100)},
                    (i,w,h,c)=>new UiLayoutSize(w,b.Get<int>("count")*10));
                Check(plan.Slots[0].Height==30,"established neutral behavior request reaches synchronized geometry in one production chain");
            }
        }
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
        int selectionRequests=0;
        var sameSelection=UiValueBehavior.Selection(interactions.Activate(target,new UiInputEligibility(true)),8);
        UiValueBehavior.Request(target,sameSelection,b,v=>{selectionRequests++;b.Set("count",v);});
        UiValueBehavior.Request(target,sameSelection,b,v=>{selectionRequests++;b.Set("count",v);});
        Check(selectionRequests==2 && reactions==2,"same-value selection still makes each request while scalar storage skips notification");
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
        var manySource = runtime.Int(); int manyReads=0;
        using(var many=runtime.CreateOwner("many-independent"))
        {
            for(int i=0;i<150;i++) many.Respond("response-"+i,()=>{ _=manySource.Value; manyReads++; });
            manySource.Value=1;
            Check(manyReads==300,"more than 100 independent responses are not misdiagnosed as a cycle");
        }
        using(runtime.ReadPhase()) Check(Throws(()=>a.Value=9,"forbidden"),"explicit new pure measurement or draw scope refuses writes");
        int provisionalRuns=0;
        var initialFailure=observer.RespondManaged("initial-failure",()=>{ provisionalRuns++; _=a.Value; throw new Exception("initial"); });
        Check(initialFailure.IsPaused && initialFailure.Fault!.Identity.Contains("initial-failure"),"initial response failure is retained for owner recovery");
        a.Value=4;
        Check(provisionalRuns==1,"failed response registration stays paused after source changes");
        var registrationValue=runtime.Int(); var order=new List<string>();
        observer.Respond("existing-registration-target",()=>{ _=registrationValue.Value; order.Add("B"); });
        order.Clear();
        observer.Respond("new-registration-writer",()=>{ order.Add("A-start");registrationValue.Value=1;order.Add("A-end"); });
        Check(string.Join(",",order)=="A-start,A-end,B","initial response writes use scheduled follow-up rather than recursively invoking an existing observer");
        int initialInBatch=0;
        using(runtime.Batch())
        {
            observer.Respond("deferred-registration",()=>{ _=registrationValue.Value;initialInBatch++; });
            registrationValue.Value=2;
            Check(initialInBatch==0,"response registration inside a batch cannot insert initial observation halfway through it");
        }
        Check(initialInBatch==1,"first response evaluation runs at the outer legal boundary");
        var legacy=new UiBindings();int legacyValue=0,legacyCalls=0;
        legacy.BindValue("value",()=>legacyValue,v=>legacyValue=v);
        legacy.BindCommand("call",()=>legacyCalls++);
        legacy.BindAction<int>("add",v=>legacyValue+=v);
        Exception? legacyFailure=null;
        var legacyThread=new Thread(()=>{try{legacy.Set("value",1);legacy.Invoke("call");legacy.Invoke<int>("add",2);legacy.TryInvokeCommand("call");}catch(Exception ex){legacyFailure=ex;}});
        legacyThread.Start();legacyThread.Join();
        Check(legacyFailure==null && legacyValue==3 && legacyCalls==2,"untracked legacy registry preserves serial cross-thread operation conditions rather than inheriting reactive ownership");
        var otherRuntime=new UiReactiveRuntime();var otherValue=otherRuntime.Int(10);
        var cross=observer.Compute("cross-owner",()=>otherValue.Value);
        Check(Throws(()=>_=cross.Value,"Cross-runtime"),"cross-runtime tracked reads diagnose the missing-edge boundary instead of silently caching");
        var crossWrite=observer.Compute("cross-write",()=>{otherValue.Value=11;return 0;});
        Check(Throws(()=>_=crossWrite.Value,"Cross-runtime") && otherValue.Value==10,"cross-runtime write cannot bypass the active pure read contract");
        using(runtime.ReadPhase())
        {
            using(runtime.ReadPhase()) Check(a.Value==4,"nested same-runtime pure read is legal");
            Check(Throws(()=>otherValue.Value=12,"Cross-runtime"),"nested scope exit restores the enclosing active runtime");
            var same=observer.Compute("same-owner",()=>a.Value);
            Check(same.Value==4,"same-runtime nested tracked computation remains legal");
        }
        otherValue.Value=12;
        Check(otherValue.Value==12,"outermost pure-read exit leaves no thread marker behind");
        var throwsRead=observer.Compute<int>("nested-failure",()=>{using(runtime.ReadPhase()){throw new Exception("scope-failure");}});
        Check(Throws(()=>_=throwsRead.Value,"scope-failure"),"nested tracked-read failure retains the original cause");
        otherValue.Value=13;legacy.Set("value",5);
        Check(otherValue.Value==13 && legacyValue==5,"failed nested read restores context for another runtime and untracked legacy calls");
        var legacyMutation=observer.Compute("legacy-in-pure",()=>{legacy.Set("value",6);return 0;});
        Check(Throws(()=>_=legacyMutation.Value,"forbidden") && legacyValue==5,"new tracked purity cannot be bypassed through an otherwise legacy binding setter");
        var failSwitch = runtime.Boolean(); int failRuns=0;
        var fail = observer.Compute("failing", () => { failRuns++; int v=right.Value; if(failSwitch.Value) { _=left.Value; throw new Exception("intentional"); } return v; });
        _=fail.Value; failSwitch.Value=true;
        Check(Throws(() => _=fail.Value, "test-business/failing"), "failed computation preserves located cause");
        failSwitch.Value=false; Check(fail.Value==7, "failure restores temporary tracking context for explicit new attempt");
        var bad = observer.Compute("illegal-write", () => { a.Value++; return 1; });
        Check(Throws(() => _=bad.Value, "forbidden"), "tracked read cannot mutate business state");
        b.BindCommand("throw-after-write", () => { counter.Count.Value=30; throw new Exception("after-write"); });
        Check(Throws(() => b.Invoke("throw-after-write"), "after-write") && counter.Count.Value==30 && counter.Observed==30, "exception neither rolls back nor replays already written business");
        var twinSource=runtime.Int();using var twinOwner=runtime.CreateOwner("twin-failure");
        var twinResponse=twinOwner.RespondManaged("response",()=>{if(twinSource.Value>0)throw new Exception("response-cause");});
        b.BindCommand("twin-fail",()=>{twinSource.Value=1;throw new Exception("business-cause");});
        Exception? twin=null;try{b.Invoke("twin-fail");}catch(Exception ex){twin=ex;}
        Check(twin != null && twin.ToString().Contains("business-cause") && twinResponse.Fault!.ToString().Contains("response-cause"),"business error and independently retained response fault preserve both causes");
        view.Synchronize(); int stable = b.Get<int>("count");
        bool failRead = true;
        b.BindTracked<int>("broken", () => failRead ? throw new Exception("sync-failure") : 12);
        view.Synchronize(); Check(view.GetStatus("broken").Fault!.Identity=="presentation/broken", "presentation synchronization failure is located without stopping independent keys");
        using(view.Enter()) Check(b.Get<int>("count")==stable, "failed synchronization does not publish a partial read view");
        failRead=false; view.Retry("broken"); view.Synchronize();
        using(view.Enter()) Check(b.Get<int>("broken")==12, "failed new binding can be explicitly synchronized later");
        Exception? accessFailure=null;
        var thread=new Thread(() => { try { _=a.Value; } catch(Exception ex) { accessFailure=ex; } }); thread.Start(); thread.Join();
        Check(accessFailure is InvalidOperationException, "wrong access context is refused rather than silently marshalled");
        var loop=runtime.Int(); using var loopOwner=runtime.CreateOwner("cycle-owner");
        var loopingResponse=loopOwner.RespondManaged("loop", () => { int value=loop.Value; if(value>0) loop.Value=value+1; });
        loop.Value=1; Check(loopingResponse.Fault?.Kind=="propagation-budget" && loopingResponse.Fault.Identity=="cycle-owner/loop", "response repeated propagation is bounded and located");
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
        service.SetAccepting(false); int serviceBefore=service.ReadSnapshot().Completed;
        bool commandRan=projection.Bindings.TryInvokeCommand("complete");
        Check(commandRan && service.ReadSnapshot().Completed==serviceBefore && !projection.Accepting.Value,"business may reject a command even when the last safe UI qualification permitted dispatch");
        service.SetAccepting(true);
        workshopView.Dispose(); workshopView.Dispose(); service.Complete(); projection.Apply(service.ReadSnapshot());
        Check(projection.LastBusinessObservation==4, "independent authoritative service and response continue after view disposal");
        projection.Dispose(); service.Complete();
        Check(service.ReadSnapshot().Completed==5, "disposing projection does not dispose or roll back external service");
    }
    private static void ImmediateQualification(Action<bool,string> check, Func<Action,string,bool> throws)
    {
        var runtime=new UiReactiveRuntime();var count=runtime.Int(1);var bindings=new UiBindings(runtime);
        bindings.BindTracked("count",()=>count.Value,v=>count.Value=v);
        int dispatched=0,predicateValue=0,responses=0;
        bindings.BindCommand("qualified",()=>dispatched++,()=>bindings.Get<int>("count")<100);
        bindings.BindCommand("failing-qualification",()=>dispatched++,()=>{predicateValue=bindings.Get<int>("count");throw new Exception("qualification-cause");});
        using var owner=runtime.CreateOwner("qualification-business");
        owner.Respond("count",()=>{_=count.Value;responses++;});
        using var view=bindings.CreatePresentation();view.Synchronize();
        using(view.Enter())
        {
            check(bindings.CanExecute("qualified") && bindings.Get<int>("count")==1,"successful qualification restores the current presentation read scope");
            using(runtime.Batch())
            {
                bindings.Set("count",159);
                check(!bindings.CanExecute("qualified") && bindings.Get<int>("count")==1 && responses==1,"qualification reads latest binding data without flushing a pending business batch");
                check(!bindings.TryInvokeCommand("qualified") && dispatched==0 && bindings.Get<int>("count")==1,"latest binding-based qualification refuses dispatch and preserves presentation input");
                check(throws(()=>bindings.CanExecute("failing-qualification"),"qualification-cause") && predicateValue==159 && bindings.Get<int>("count")==1 && responses==1,"throwing qualification reads latest data then restores presentation without draining responses");
            }
            check(responses==2 && !bindings.CanExecute("qualified") && bindings.Get<int>("count")==1,"qualification never publishes a new presentation after outer response drain");
        }
    }

}
