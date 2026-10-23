using System;
using FerriteLib.UiKit.Kernel;

// The caller delivers an immutable snapshot onto Runtime's serial context. No live background object is read by a getter.
internal sealed class WorkshopProjectionExample : IDisposable
{
    internal readonly UiReactiveRuntime Runtime = new();
    internal readonly UiBindings Bindings;
    internal readonly UiReactiveValue<int> Completed;
    internal readonly UiReactiveValue<bool> Accepting;
    internal readonly UiReactiveOwner BusinessOwner;
    internal int LastBusinessObservation;
    internal WorkshopProjectionExample(WorkshopService service)
    {
        Bindings = new UiBindings(Runtime); Completed = Runtime.Int(); Accepting = Runtime.Boolean();
        BusinessOwner = Runtime.CreateOwner("workshop-projection-service");
        BusinessOwner.Respond("audit", () => LastBusinessObservation = Completed.Value);
        Bindings.BindTracked("completed", () => Completed.Value);
        Bindings.BindTracked("text", () => "Completed " + Completed.Value);
        Bindings.BindTracked("accepting", () => Accepting.Value);
        Bindings.BindCommand("complete", () => { service.Complete(); Apply(service.ReadSnapshot()); }, () => Accepting.Value);
        Apply(service.ReadSnapshot());
    }
    internal void Apply(WorkshopService.Snapshot safeResult)
    {
        using (Runtime.Batch()) { Completed.Value = safeResult.Completed; Accepting.Value = safeResult.Accepting; }
    }
    public void Dispose() => BusinessOwner.Dispose();
}

