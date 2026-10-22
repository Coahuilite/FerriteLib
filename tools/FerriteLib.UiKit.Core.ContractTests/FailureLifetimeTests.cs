using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

internal static class FailureLifetimeTests
{
    internal static void Run(Action<bool,string> check)
    {
        void C(bool ok,string name)=>check(ok,"RU3 "+name);
        UiReactiveFault? Fault(Action read){try{read();return null;}catch(UiReactiveFault ex){return ex;}}
        var runtime=new UiReactiveRuntime();using var owner=runtime.CreateOwner("failure-test");
        var branch=runtime.Boolean();var left=runtime.Int(1);var right=runtime.Int(2);var unrelated=runtime.Int();
        int runs=0;
        var value=owner.Compute("branch",()=>{runs++; if(branch.Value){int v=right.Value;if(v<5)throw new Exception("right unavailable");return v;}return left.Value;});
        C(value.Value==1 && runs==1,"initial successful value");branch.Value=true;
        UiReactiveFault? first=Fault(()=>_=value.Value);C(first?.Identity=="failure-test/branch" && runs==2,"new branch failure is located");
        C(value.TryGetLastValue(out int old) && old==1 && value.HasValue,"last valid value remains separate");
        for(int i=0;i<12;i++)C(ReferenceEquals(first,Fault(()=>_=value.Value)),"cached failure read "+i);
        unrelated.Value=1;C(Fault(()=>_=value.Value)!=null && runs==2,"unrelated input cannot retry");
        left.Value=8;C(ReferenceEquals(first,Fault(()=>_=value.Value)) && runs==2,"failed branch drops obsolete successful left edge before right recovers");
        right.Value=5;C(value.Value==5 && runs==3 && value.Fault==null,"failed new branch keeps its actual recovery read");
        left.Value=9;C(value.Value==5 && runs==3,"successful recovery replaces old branch dependencies");
        int initialRuns=0;var trigger=runtime.Int();var initial=owner.Compute("first-failure",()=>{initialRuns++;int n=trigger.Value;if(n==0)throw new Exception("first");return n;});
        C(Fault(()=>_=initial.Value)!=null && !initial.TryGetLastValue(out _),"first failure has no fabricated zero");
        for(int i=0;i<10;i++)_=Fault(()=>_=initial.Value);
        C(initialRuns==1,"first failure attempted once across repeated reads");trigger.Value=1;C(initial.Value==1 && initialRuns==2,"first failed read is recoverable by actual input");
        int retryRuns=0;bool repaired=false;var retry=owner.Compute("explicit",()=>{retryRuns++;if(!repaired)throw new Exception("repair");return 7;});
        var outer=owner.Compute("outer",()=>retry.Value);_=Fault(()=>_=outer.Value);repaired=true;_=Fault(()=>_=outer.Value);
        C(retryRuns==1,"unannounced external mutation is not guessed as recovery");outer.Retry();C(outer.Value==7 && retryRuns==2,"explicit pure retry follows actual failed dependency without business replay");
        UiReactiveComputed<int>? a=null,b=null;int cycleRuns=0;
        a=owner.Compute("cycle-A",()=>{cycleRuns++;return b!.Value;});b=owner.Compute("cycle-B",()=>a.Value);
        var cycle=Fault(()=>_=a.Value);for(int i=0;i<5;i++)_=Fault(()=>_=a.Value);
        C(cycle?.Kind=="pure-cycle" && cycle.Identity.Contains("cycle-A") && cycleRuns==1,"pure recursion has distinct cached diagnostic");
        var cyclic=runtime.Boolean(true);UiReactiveComputed<int>? recoverA=null,recoverB=null;
        recoverA=owner.Compute("recover-cycle-A",()=>cyclic.Value?recoverB!.Value:42);
        recoverB=owner.Compute("recover-cycle-B",()=>recoverA.Value);
        C(Fault(()=>_=recoverA.Value)?.Kind=="pure-cycle","conditional cycle records actual branch input");
        cyclic.Value=false;C(recoverA.Value==42,"related input breaks cached pure cycle through preserved failed-child edge");

        int callbacks=0,applied=0,independent=0;bool callbackBroken=false;var input=runtime.Int();
        var callback=owner.RespondManaged("side-effect",()=>{int n=input.Value;callbacks++;applied=n;if(callbackBroken)throw new Exception("after write");});
        owner.Respond("independent",()=>independent=input.Value);
        callbackBroken=true;input.Value=1;
        C(callback.IsPaused && callback.Fault!.Identity=="failure-test/side-effect" && applied==1 && independent==1,"failed callback preserves writes and independently queued work");
        input.Value=2;input.Value=3;C(callbacks==2 && applied==1 && independent==3,"source changes do not resume side effects");
        callbackBroken=false;callback.Resume();C(callbacks==3 && applied==3 && !callback.IsPaused,"owner resume runs current state once without replay");
        using var finiteOwner=runtime.CreateOwner("finite");var finite=runtime.Int();int finiteRuns=0;
        var finiteResponse=finiteOwner.RespondManaged("0-to-3",()=>{int n=finite.Value;finiteRuns++;if(n<3)finite.Value=n+1;});
        C(finite.Value==3 && finiteRuns==4 && !finiteResponse.IsPaused,"finite 0 to 1 to 2 to 3 feedback completes");
        using var feedback=runtime.CreateOwner("feedback");var turn=runtime.Int(-1);int writes=0;
        var zero=feedback.RespondManaged("zero",()=>{if(turn.Value==0){writes++;turn.Value=1;}});
        var one=feedback.RespondManaged("one",()=>{if(turn.Value==1){writes++;turn.Value=0;}});
        using(runtime.Batch()){turn.Value=0;input.Value=4;}
        C((zero.Fault?.Kind=="propagation-budget" || one.Fault?.Kind=="propagation-budget") && writes>=256 && independent==4,"repeated-response budget stops actual production read write feedback without clearing independent queue");
        int stoppedWrites=writes;for(int i=0;i<10;i++){_=turn.Value;unrelated.Value++;}
        C(writes==stoppedWrites,"reading and unrelated changes cannot restart exhausted response");
        int many=0;var manySource=runtime.Int();using(var manyOwner=runtime.CreateOwner("many"))
        {
            for(int i=0;i<400;i++)manyOwner.Respond("once-"+i,()=>{_=manySource.Value;many++;});
            manySource.Value=1;C(many==800,"400 independent one-shot responses are not repeated-response loops");
        }
        using var counter=new FailureCounterExample();using var view=counter.Bindings.CreatePresentation();view.Synchronize();
        counter.Broken.Value=true;view.Synchronize();var status=view.GetStatus("caption");
        C(status.HasLastValue && status.Fault!=null && !status.IsCurrent,"presentation reports stale display distinctly from current validity");
        using(view.Enter())C(Fault(()=>_=counter.Bindings.Get<string>("caption"))!=null,"public derived binding read cannot masquerade as a last successful display");
        int formats=counter.Formats;for(int i=0;i<8;i++)view.Synchronize();C(counter.Formats==formats,"repeated presentation synchronization cannot retry same fault");
        counter.Bindings.Invoke("reset");view.Synchronize();C(counter.Count.Value==0 && counter.Resets==1,"independent reset remains a real current business request");
        counter.Broken.Value=false;view.Synchronize();using(view.Enter())C(counter.Bindings.Get<string>("caption")=="Count 0" && view.GetStatus("caption").IsCurrent,"related format input restores current presentation");
        using var workshop=new FailureWorkshopExample();workshop.Apply(1);
        C(workshop.Notification.IsPaused && workshop.Business.Completed==1,"independent workshop callback fault does not undo completed business");
        workshop.Apply(8);workshop.RecoverNotification();C(workshop.CallbackRuns==3 && workshop.Business.Completed==1,"workshop owner repair executes latest observation without reissuing previous commands");
        using var confirmation=workshop.OpenConfirmation();workshop.Selected="B23";workshop.Business.A17Cancelable=false;confirmation.Confirm();
        C(confirmation.Target=="A17" && confirmation.Result==false && workshop.Business.LastTarget=="A17" && workshop.Business.Completed==1,"confirmation target remains A17 and service rejects latest ineligible target after selecting B23");
        confirmation.Dispose();confirmation.Confirm();C(workshop.Business.Requests==1,"closed or repeated confirmation cannot issue another operation");
        Console.WriteLine("RU3-FACT branch-runs="+runs+" first-runs="+initialRuns+" explicit-runs="+retryRuns+" cycle-runs="+cycleRuns+" callback-runs="+callbacks+" finite-runs="+finiteRuns+" feedback-writes="+writes+" many="+many+" stopped="+(zero.Fault??one.Fault)!.StoppedScope+" recovery="+(zero.Fault??one.Fault)!.Recovery);
    }
}
