using System;
using FerriteLib.UiKit.Kernel;

// Compilable author examples. WorkshopService deliberately has no FL dependency.
internal sealed class CounterPageExample : IDisposable
{
    public readonly UiReactiveRuntime Runtime = new();
    public readonly UiBindings Bindings;
    public readonly UiReactiveValue<int> Count;
    public readonly UiReactiveValue<bool> Selected;
    public readonly UiReactiveOwner BusinessOwner;
    public int Operations;
    public int Observed;
    public CounterPageExample()
    {
        Bindings = new UiBindings(Runtime);
        Count = Runtime.Int(); Selected = Runtime.Boolean();
        BusinessOwner = Runtime.CreateOwner("counter-business");
        BusinessOwner.Respond("observe-count", () => Observed = Count.Value);
        Bindings.BindTracked("count", () => Count.Value, value => Count.Value = value);
        Bindings.BindTracked("text", () => "Count " + Count.Value);
        Bindings.BindTracked("selected", () => Selected.Value, value => Selected.Value = value);
        Bindings.BindCommand("increment", () => { Operations++; Count.Value++; }, () => Count.Value < 100);
        Bindings.BindCommand("changed", () => { Operations++; Count.Value++; });
        Bindings.BindAction<int>("add", amount => { Operations++; Count.Value += amount; });
    }
    public void Dispose() => BusinessOwner.Dispose();
}

// A response produces a list; presentation consumes it without making it the producer's input.
internal sealed class ResponseListPageExample : IDisposable
{
    internal readonly UiReactiveRuntime Runtime = new();
    internal readonly UiBindings Bindings;
    internal readonly UiReactiveValue<int> Input;
    internal readonly UiReactiveList<int> Output;
    private readonly UiReactiveOwner owner;
    internal int Runs;
    internal ResponseListPageExample()
    {
        Input = Runtime.Int(1);
        Output = Runtime.List(Array.Empty<int>(), (a, b) => a == b);
        owner = Runtime.CreateOwner("response-list-business");
        owner.Respond("append-input", () => { Runs++; Output.Add(Input.Value); });
        Bindings = new UiBindings(Runtime);
        Bindings.BindCommand("increment", () => Input.Value++);
        Bindings.BindTracked("text", () => string.Join(",", Output));
    }
    public void Dispose() => owner.Dispose();
}
