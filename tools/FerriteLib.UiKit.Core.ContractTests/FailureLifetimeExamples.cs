using System;
using FerriteLib.UiKit.Kernel;

// Compilable author entry: the business value, pure display, view and automatic callback have different owners.
internal sealed class FailureCounterExample : IDisposable
{
    internal readonly UiReactiveRuntime Runtime = new();
    internal readonly UiReactiveOwner Owner;
    internal readonly UiReactiveValue<int> Count;
    internal readonly UiReactiveValue<bool> Broken;
    internal readonly UiReactiveComputed<string> Caption;
    internal readonly UiBindings Bindings;
    internal int Formats, Resets;
    internal FailureCounterExample()
    {
        Count=Runtime.Int(4); Broken=Runtime.Boolean(); Owner=Runtime.CreateOwner("counter");
        Caption=Owner.Compute("format",()=> { Formats++; int value=Count.Value; if(Broken.Value)throw new FormatException("counter format"); return "Count "+value; });
        Bindings=new UiBindings(Runtime);
        Bindings.BindTracked("caption",()=>Caption.Value);
        Bindings.BindCommand("reset",()=>{ Resets++; Count.Value=0; });
        Bindings.BindCommand("retry-display",Caption.Retry);
    }
    public void Dispose()=>Owner.Dispose();
}

// Independent authoritative service. Confirmation captures a target; the service rechecks it at execution.
internal sealed class FailureWorkshopExample : IDisposable
{
    internal sealed class Service
    {
        internal bool A17Cancelable=true;
        internal int Requests, Completed;
        internal string LastTarget="";
        internal bool Cancel(string target)
        {
            Requests++; LastTarget=target;
            if(target=="A17" && !A17Cancelable)return false;
            Completed++; return true;
        }
    }
    internal sealed class Confirmation : IDisposable
    {
        private readonly Service service;
        internal readonly string Target;
        internal bool Closed;
        internal bool? Result;
        internal Confirmation(Service service,string target){this.service=service;Target=target;}
        internal void Confirm(){if(!Closed){Result=service.Cancel(Target);Closed=true;}}
        public void Dispose(){Closed=true;}
    }
    internal readonly Service Business=new();
    internal readonly UiReactiveRuntime Runtime=new();
    internal readonly UiReactiveOwner Owner;
    internal readonly UiReactiveValue<int> Snapshot;
    internal readonly UiBindings Bindings;
    internal UiReactiveResponse Notification;
    internal bool BadConnection=true;
    internal int CallbackRuns;
    internal string Selected="A17";
    internal FailureWorkshopExample()
    {
        Snapshot=Runtime.Int();Owner=Runtime.CreateOwner("workshop");Bindings=new UiBindings(Runtime);
        Bindings.BindTracked("caption",()=>"Completed "+Snapshot.Value);
        // An intentionally faulty consumer integration; side effects are neither undone nor replayed.
        Notification=Owner.RespondManaged("completion-notification",()=>
        {
            int current=Snapshot.Value;CallbackRuns++;
            if(current>0 && BadConnection){Business.Completed++;throw new Exception("notification reissued a command");}
        });
    }
    internal Confirmation OpenConfirmation()=>new(Business,Selected);
    internal void Apply(int completed)=>Snapshot.Value=completed;
    internal void RecoverNotification(){BadConnection=false;Notification.Resume();}
    public void Dispose()=>Owner.Dispose();
}
