using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

internal static class LayoutReuseTests
{
    private sealed class Metrics : ITextMetrics, IUiTextMetricsEnvironment
    {
        internal int Revision;
        public UiMeasureInputs CaptureEnvironment(UiFont font) => new(new[] { "neutral-fixture" }, new[] { (float)font, Revision });
        public float MeasureWidth(string text, UiFont font) => text.Length * 10;
        public float MeasureText(string text, UiFont font, float width) => Math.Max(1, (float)Math.Ceiling(text.Length * 10 / width)) * (20 + Revision);
    }
    private sealed class Band
    {
        private UiMeasureInputs? inputs, environment;
        internal int Calculations;
        private float height;
        internal UiLayoutSize Read(string text, float width, Metrics metrics)
        {
            var request = new UiMeasurement(new UiMeasureInputs(new[] { text }, new[] { width }, true), () => metrics.MeasureText(text, UiFont.Small, width));
            UiMeasureInputs currentEnvironment = metrics.CaptureEnvironment(UiFont.Small);
            if (inputs == null || !UiMeasureValidity.CanReuse(inputs, request.Inputs, environment, currentEnvironment))
            { height = request.Calculate(); Calculations++; inputs = request.Inputs; environment = currentEnvironment; }
            return new UiLayoutSize(width, height);
        }
    }
    internal static void Run(Action<bool, string> check)
    {
        string[] strings = { "a" }; float[] numbers = { 1 };
        var captured = new UiMeasureInputs(strings, numbers); strings[0] = "mutated"; numbers[0] = 2;
        check(captured.Equals(new UiMeasureInputs(new[] { "a" }, new[] { 1f })), "RU2 captured inputs are detached immutable values");
        var environment = new UiMeasureInputs(new[] { "font-A" }, new[] { 1f });
        var textInput = new UiMeasureInputs(new[] { "text" }, usesTextMetrics: true);
        check(UiMeasureValidity.CanReuse(textInput, textInput, environment, environment), "RU2 real equal inputs and known matching environment permit measure reuse");
        check(!UiMeasureValidity.CanReuse(textInput, textInput, environment, null), "RU2 environment revocation denies measure reuse");
        check(!UiMeasureValidity.CanReuse(textInput, textInput, environment, new UiMeasureInputs(new[] { "font-B" })), "RU2 environment identity change denies stale measure reuse");
        check(UiMeasureValidity.CanReuse(captured, captured, environment, null), "RU2 non-text calculations do not acquire an irrelevant font prerequisite");
        Counter(check); Workshop(check);
    }
    private static void Counter(Action<bool, string> check)
    {
        using var page = new CounterPageExample(); using var presentation = new UiBindingPresentation(page.Bindings);
        var metrics = new Metrics(); var counter = new Band(); var stable = new Band();
        var frame = new UiLayoutFrame(70, 300, 0, 0, 0, 0, 0, 0, new UiLayoutHeight(null, false));
        var children = new[] { new UiLayoutChild(70), new UiLayoutChild(70) };
        UiLayoutReuse? plan = null; int arrangements = 0; bool reused = false;
        void Synchronize()
        {
            presentation.Synchronize(); using var inputs = presentation.Enter();
            plan = UiLayoutReuse.Resolve(plan, new UiMeasureInputs(new[] { "counter", "stable" }, new[] { 70f }),
                (i, w, a, r) => i == 0 ? counter.Read(page.Bindings.Get<string>("text"), w, metrics) : stable.Read("z", w, metrics),
                measure => { arrangements++; return UiLayout.Stack(frame, children, measure); }, out reused);
        }
        Synchronize(); check(counter.Calculations == 1 && stable.Calculations == 1 && arrangements == 1, "RU2 neutral counter binding presentation consumes production measurement validity and production Stack stage");
        page.Count.Value = 1; Synchronize();
        check(reused && counter.Calculations == 2 && stable.Calculations == 1 && arrangements == 1 && plan!.Plan.Slots[1].Y == 20,
            "RU2 neutral changed counter text same output verifies children before retaining parent plan and unrelated calculation");
        page.Count.Value = 10; Synchronize();
        check(!reused && arrangements == 2 && plan!.Plan.Slots[1].Y == 40 && stable.Calculations == 1,
            "RU2 neutral actual wrapped extra line updates the later placement without remeasuring its input");
        int before = counter.Calculations; Synchronize(); check(reused && counter.Calculations == before && arrangements == 2, "RU2 neutral no-input synchronization retains actual stage outputs");
        metrics.Revision++; Synchronize();
        check(!reused && plan!.Plan.Slots[1].Y == 42 && counter.Calculations == before + 1, "RU2 neutral real environment facts invalidate affected measurements and propagate their changed sizes");
        Console.WriteLine($"RU2-NEUTRAL counter calculations={counter.Calculations} stable={stable.Calculations} arrangements={arrangements} later-y={plan!.Plan.Slots[1].Y}");
    }
    private static void Workshop(Action<bool, string> check)
    {
        var service = new OrderBoardService(); using var projection = new OrderBoardProjectionExample(service);
        var page = projection.Page; using var presentation = new UiBindingPresentation(page.Bindings);
        var bands = new Dictionary<string, Band>(StringComparer.Ordinal); var metrics = new Metrics();
        UiLayoutReuse? prior = null; int arrangements = 0; bool reused = false;
        void Synchronize()
        {
            presentation.Synchronize(); using var inputs = presentation.Enter();
            IReadOnlyList<string> keys = page.Bindings.Get<IReadOnlyList<string>>("items");
            var children = keys.Select(_ => new UiLayoutChild(40)).ToArray();
            foreach (string key in keys) if (!bands.ContainsKey(key)) bands.Add(key, new Band());
            var frame = new UiLayoutFrame(40, 300, 0, 0, 0, 0, 0, 0, new UiLayoutHeight(null, false));
            prior = UiLayoutReuse.Resolve(prior, new UiMeasureInputs(keys.ToArray()),
                (i, w, a, r) => bands[keys[i]].Read(page.Bindings.Get<string>("items." + keys[i] + ".caption"), w, metrics),
                measure => { arrangements++; return UiLayout.VerticalContent(frame, children, measure); }, out reused);
        }
        Synchronize(); service.Increment(); projection.Apply(service.ReadSnapshot()); Synchronize();
        check(reused && bands["a"].Calculations == 2 && bands["b"].Calculations == 1 && arrangements == 1,
            "RU2 neutral independent safe multi-item projection updates only the affected row calculation with same outward result");
        service.Replace(new OrderBoardService.Row("b", 2, 2), new OrderBoardService.Row("a", 1, 2), new OrderBoardService.Row("c", 3, 3));
        projection.Apply(service.ReadSnapshot()); Synchronize();
        check(!reused && bands["b"].Calculations == 1 && bands["a"].Calculations == 2 && bands["c"].Calculations == 1,
            "RU2 neutral safe Add and Move update structure while retaining actual keyed row measurements");
        service.Replace(new OrderBoardService.Row("a", 1, 2)); projection.Apply(service.ReadSnapshot()); Synchronize();
        check(!reused && prior!.Plan.Slots.Count == 1 && bands["a"].Calculations == 2, "RU2 neutral safe Remove publishes only current membership without recalculating retained input");
        Console.WriteLine($"RU2-NEUTRAL workshop a={bands["a"].Calculations} b={bands["b"].Calculations} c={bands["c"].Calculations} arrangements={arrangements} slots={prior!.Plan.Slots.Count}");
    }
}
