using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        checks++;
        Console.WriteLine("PASS " + name);
    }

    private static UiPointerSample Sample(UiPointerPhase phase, float x = 50, float y = 10, bool used = false) => new(phase, x, y, used);
    private static readonly UiInputEligibility Eligible = new(true);
    private static readonly UiHitGeometry Geometry = new(0, 0, 100, 20);
    private static readonly UiRange Range = new(0, 10);
    private static readonly UiInteractionTarget Target = new("row-A", "business-A");
    private static int Main()
    {
        try
        {
            Properties();
            SharedBehavior();
            IdentityAndEnd();
            BooleanSelectionExpansion();
            Isolation();
            Editing();
            Console.WriteLine($"ALL PASS {checks} neutral checks");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Editing()
    {
        float model = 10;
        int requests = 0;
        bool Parse(string text, out float value) => float.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
        UiEditSession<float> Editor() => new(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture), Parse);
        void Set(float value) { requests++; model = value; }
        var edit = Editor();
        edit.Observe(model);
        edit.Focus();
        edit.Input("20", true, false, false, Set, () => model);
        Check(model == 10 && edit.Modified && edit.Valid, "E1 deferred qualified draft is not a business value");
        Check(edit.TryStepBase(model, out float basis) && basis == 20, "N1 step takes valid local draft");
        edit.TakeOver(basis + 1, Set, () => model);
        Check(model == 21 && requests == 1 && !edit.HasDraft, "N1 one request without intermediate draft publication");
        edit.Input("-", true, false, false, Set, () => model);
        Check(!edit.TryStepBase(model, out _) && !edit.Confirm(false, Set, () => model) && edit.Draft == "-", "E3 N3 invalid blur retains draft and cannot step");
        edit.TakeOver(30, Set, () => model);
        Check(model == 30 && requests == 2 && !edit.HasDraft, "N2 slider takes over invalid draft with one request");
        edit.Input("40", true, false, false, Set, () => model);
        model = 35;
        edit.Observe(model);
        Check(edit.Conflict && edit.Draft == "40" && !edit.Confirm(false, Set, () => model), "E4 external model conflict preserves draft and prevents blur publish");
        edit.Confirm(true, Set, () => model);
        Check(model == 40 && requests == 3 && !edit.HasDraft, "E2 explicit confirmation can replace known changed basis");
        edit.Input("50", true, true, true, Set, () => model);
        Check(requests == 3, "IME intermediate never publishes");
        edit.Input("50", true, false, true, Set, () => model);
        edit.Confirm(true, Set, () => model);
        Check(requests == 4 && model == 50, "E2 composition completed Live candidate does not publish twice on confirm");
        edit.Input("60", false, false, true, Set, () => model);
        Check(edit.Draft == "50" && requests == 4, "readonly prevents draft mutation");
        edit.Input("70", true, false, false, Set, () => model);
        model = 65;
        edit.Cancel(model);
        Check(edit.Draft == "65" && model == 65 && requests == 4, "E5 cancel uses latest model without rollback");
        edit.Input("80", true, false, false, Set, () => model);
        edit.Confirm(true, _ => requests++, () => model);
        Check(edit.Draft == "65" && !edit.HasDraft && model == 65, "E2 successful request is not assumed business acceptance");
        var numeric = new UiNumericEdit(0, 100);
        Check(!numeric.Parse("NaN", out _) && !numeric.Parse("Infinity", out _) && !numeric.Parse("-", out _) &&
            !numeric.Parse("", out _), "NUMERIC nonfinite and temporary invalid strings are not candidates");
        Check(numeric.Parse("120", out float clamped) && clamped == 100, "NUMERIC range constrains a request candidate");
        var local = new UiEditSession<float>(numeric.Format, numeric.Parse);
        model = 10;
        local.Observe(model);
        local.Focus();
        local.Input("20", true, false, false, Set, () => model);
        float selected = -1;
        numeric.Step(local, model, 1, value => selected = value, () => model);
        Check(selected == 21 && model == 10 && !local.HasDraft, "N1 shared numeric step requests 21 even when business keeps 10");
        numeric.Step(local, model, 1, value => selected = value, () => model);
        Check(selected == 11, "N1 next no-draft step begins at actual model, not last requested 21");
        model = 10;
        var roundtrip = new UiEditSession<float>(numeric.Format, numeric.Parse);
        roundtrip.Observe(model);
        roundtrip.Focus();
        int liveWrites = 0;
        void LiveSet(float value) { liveWrites++; model = value; }
        roundtrip.Input("20", true, false, true, LiveSet, () => model);
        roundtrip.Input("10", true, false, true, LiveSet, () => model);
        roundtrip.Observe(model);
        Check(liveWrites == 2 && model == 10 && roundtrip.Draft == "10", "E1 Live return to initial text is a new candidate, not an unmodified edit");
        var recovery = new UiEditSession<float>(numeric.Format, numeric.Parse);
        recovery.Observe(model);
        recovery.Focus();
        recovery.Input("20", true, false, false, LiveSet, () => model);
        try { numeric.Step(recovery, model, 1, _ => throw new InvalidOperationException("before write"), () => model); }
        catch (InvalidOperationException) { }
        recovery.Observe(model);
        recovery.Input("20", true, false, true, LiveSet, () => model);
        Check(liveWrites == 2 && model == 10 && recovery.Draft == "20", "N1 failed step is not automatically retried or mistaken for publishing old draft");
        Check(!recovery.Confirm(false, LiveSet, () => model) && liveWrites == 2 && recovery.Draft == "20",
            "E3 ordinary blur after failed takeover retains the draft and does not publish automatically");
        recovery.Confirm(true, LiveSet, () => model);
        Check(liveWrites == 3 && model == 20, "E2 explicit draft confirmation after failed 21 still requests 20");
        foreach (bool live in new[] { false, true })
        {
            model = 10;
            var failed = new UiEditSession<float>(numeric.Format, numeric.Parse);
            failed.Observe(model);
            failed.Focus();
            int attempts = 0;
            void Fail(float value) { attempts++; throw new InvalidOperationException("before write"); }
            try
            {
                failed.Input("20", true, false, live, Fail, () => model);
                if (!live) failed.Confirm(true, Fail, () => model);
            }
            catch (InvalidOperationException) { }
            failed.Input("20", true, false, true, Fail, () => model);
            Check(!failed.Confirm(false, Fail, () => model) && attempts == 1,
                "failed publication has no automatic Live/blur retry: live=" + live);
            failed.Confirm(true, value => { attempts++; model = value; }, () => model);
            Check(attempts == 2 && model == 20 && !failed.HasDraft,
                "explicit confirmation is a new attempt after a failed same-draft publication: live=" + live);
        }
    }

    private static void Properties()
    {
        var own = new UiPropertyDeclaration<UiFont>();
        var local = new UiPropertyDeclaration<UiFont>();
        var parent = new UiPropertyDeclaration<UiFont>();
        var host = new UiPropertyDeclaration<UiFont>();
        var ancestors = new[]
        {
            new KeyValuePair<string, UiPropertyDeclaration<UiFont>>("parent", parent)
        };
        UiResolvedProperty<UiFont> Resolve() => UiPropertyResolver.Resolve(own, local, ancestors, host, UiFont.Small);
        parent.Set(UiFont.Small);
        own.Set(UiFont.Tiny);
        parent.Set(UiFont.Medium);
        Check(Resolve().Value == UiFont.Tiny && Resolve().Origin == UiPropertyOrigin.Instance, "P1 explicit Tiny survives parent Medium");
        own.Remove();
        Check(Resolve().Value == UiFont.Medium && Resolve().Source == "parent", "P2 remove follows current parent");
        parent.Set(UiFont.Small);
        Check(Resolve().Value == UiFont.Small && !own.IsDeclared, "P3 continues following without copying");
        parent.Set(UiFont.Medium);
        local.Set(UiFont.Small);
        own.Set(UiFont.Tiny);
        own.Remove();
        Check(Resolve().Value == UiFont.Small && Resolve().Origin == UiPropertyOrigin.LocalStyle, "P4 local declared font succeeds override");
        local.Remove();
        own.Set(UiFont.Medium);
        parent.Set(UiFont.Small);
        Check(Resolve().Value == UiFont.Medium, "P5 equal explicit value retains intent");
        own.Remove();
        Check(Resolve().Value == UiFont.Small, "P5 removal restores following");
        parent.Remove();
        host.Set(UiFont.Medium);
        Check(Resolve().Origin == UiPropertyOrigin.Host, "P6 host source");
        host.Remove();
        Check(Resolve().Origin == UiPropertyOrigin.Fallback, "P6 fallback source");
        parent.Set(UiFont.Tiny);
        Check(Resolve().Origin == UiPropertyOrigin.Ancestor, "P6 new ancestor supersedes fallback");
        var density = new UiPropertyDeclaration<UiDensity>();
        density.Set(new(4, 3, 2, 20));
        var resolvedDensity = UiPropertyResolver.Resolve<UiDensity>(null, null, new[] { new KeyValuePair<string, UiPropertyDeclaration<UiDensity>>("parent", density) }, null, default);
        own.Set(UiFont.Tiny);
        own.Remove();
        Check(resolvedDensity.Value.RowHeight == 20, "P1-P4 density independent from font removal");
        var metrics = new Recorder();
        var wide = UiSliderLayout.Measure("Long label", 400, UiFont.Tiny, resolvedDensity.Value, metrics);
        var narrow = UiSliderLayout.Measure("Long label", 40, UiFont.Medium, resolvedDensity.Value, metrics);
        Check(!wide.Stacked && narrow.Stacked && narrow.Height > wide.Height, "P3 actual-width label reflow");
        Check(metrics.Font == UiFont.Medium && metrics.Width == 40, "P3 shared ITextMetrics records effective font/assigned width");
    }

    // Rendering changes only the observation count; all three clients call the same production transitions and request boundary.
    private static List<string> Trace(bool render)
    {
        using var session = new UiInteractionSession();
        var trace = new List<string>();
        int requests = 0;
        int paints = 0;
        void Apply(UiInteractionResult result, bool range)
        {
            if (range)
                UiInteractionRequests.Range(Target, result, v => requests++);
            else
                UiInteractionRequests.Activation(Target, result, () => requests++);
            if (render)
                paints++;
            trace.Add($"{result.Request}:{result.Value}:{result.Consume}:{result.Captured}:{result.Ended}:{requests}");
        }

        Apply(session.PointerActivation(Target, Sample(UiPointerPhase.Press), Geometry, Eligible), false);
        Apply(session.PointerActivation(Target, Sample(UiPointerPhase.Release), Geometry, Eligible), false);
        Apply(session.PointerActivation(Target, Sample(UiPointerPhase.None), Geometry, Eligible), false);
        Check(requests == 1, "H1 release exactly once, repaint none");
        Apply(session.PointerRange(Target, Sample(UiPointerPhase.Press), Geometry, Eligible, Range, 0), true);
        Apply(session.PointerRange(Target, Sample(UiPointerPhase.Drag, 150), Geometry, new(true, true, false), Range, 5), true);
        Apply(session.PointerRange(Target, Sample(UiPointerPhase.None), Geometry, Eligible, Range, 10), true);
        Apply(session.PointerRange(Target, Sample(UiPointerPhase.Release, 150), Geometry, new(true, true, false), Range, 10), true);
        Check(requests == 3 && session.CaptureOwner == null, "H2 midpoint, captured drag outside, release without repeat");
        Check(render || paints == 0, "headless has zero presentation calls");
        return trace;
    }

    private static void SharedBehavior()
    {
        var standard = Trace(true);
        var independent = Trace(true);
        var headless = Trace(false);
        Check(standard.SequenceEqual(independent) && standard.SequenceEqual(headless), "H1/H2 three isolated request-state-capture trajectories identical");
        Console.WriteLine("TRACE " + string.Join(" | ", headless));
        using var session = new UiInteractionSession();
        bool diagnosed = false;
        try
        {
            session.PointerActivation(Target, Sample(UiPointerPhase.Press), default, Eligible);
        }
        catch (InvalidOperationException ex)
        {
            diagnosed = ex.Message.Contains("row-A");
        }

        Check(diagnosed, "H3 missing geometry diagnosed with target");
        Check(session.Activate(Target, Eligible).Request && session.Select(Target, Range, 99, Eligible).Value == 10, "H3 semantic operations need no pointer geometry");
        foreach (var eligibility in new[]
        {
            new UiInputEligibility(false),
            new UiInputEligibility(true, false),
            new UiInputEligibility(true, true, false)
        }

        )
            Check(!session.PointerActivation(Target, Sample(UiPointerPhase.Press), Geometry, eligibility).Captured, "H4 denied press cannot capture");
        Check(!session.PointerRange(Target, Sample(UiPointerPhase.Press, used: true), Geometry, Eligible, Range, 0).Request, "H4 consumed press cannot select");
        Check(!session.Activate(Target, new(false)).Request && !session.Select(Target, Range, 5, new(true, false)).Request, "H4 semantic eligibility enforced");
        session.PointerActivation(Target, Sample(UiPointerPhase.Press), Geometry, Eligible);
        Check(!session.PointerActivation(Target, Sample(UiPointerPhase.Release, 150), Geometry, Eligible).Request, "F1 outside release cancels activation");
        session.PointerActivation(Target, Sample(UiPointerPhase.Press), Geometry, Eligible);
        Check(!session.PointerActivation(Target, Sample(UiPointerPhase.Release, used: true), Geometry, Eligible).Captured, "consumed release still ends owned capture without activation");
    }

    private static void IdentityAndEnd()
    {
        using var session = new UiInteractionSession();
        session.PointerActivation(Target, Sample(UiPointerPhase.Press), Geometry, Eligible);
        var other = session.PointerActivation(new("row-B", "business-A"), Sample(UiPointerPhase.None), Geometry, Eligible);
        Check(!other.Captured && !other.Request, "I1 result reports this target capture, never another node capture");
        Check(session.CaptureOwner?.Equals(Target) == true, "I1 same binding different node does not merge capture");
        session.PointerActivation(Target, Sample(UiPointerPhase.None), new(0, 0, 200, 20), Eligible);
        Check(session.CaptureOwner?.Equals(Target) == true, "I1 compatible reflow/presentation preserves capture");
        Check(!session.PointerActivation(new("row-A", "business-B"), Sample(UiPointerPhase.Release), Geometry, Eligible).Request && session.CaptureOwner == null, "I2 target replacement never transfers old press");
        foreach (UiInteractionEnd end in new[]
        {
            UiInteractionEnd.Hidden,
            UiInteractionEnd.Removed,
            UiInteractionEnd.Aborted,
            UiInteractionEnd.CaptureLost
        }

        )
        {
            session.PointerActivation(Target, Sample(UiPointerPhase.Press), Geometry, Eligible);
            session.EndNode(Target.Node, end);
            Check(session.LastEnd == end && !session.PointerActivation(Target, Sample(UiPointerPhase.Release), Geometry, Eligible).Request, "END " + end + " never replays input");
        }

        session.PointerActivation(Target, Sample(UiPointerPhase.Press), Geometry, Eligible);
        Check(!session.PointerActivation(Target, Sample(UiPointerPhase.Release), Geometry, new(false)).Request && session.CaptureOwner == null, "F1 lost qualification ends capture");
        int actual = 0;
        int attempts = 0;
        bool original = false;
        var error = new Exception("side effect");
        try
        {
            UiInteractionRequests.Range(Target, session.Select(Target, Range, 5, Eligible), value =>
            {
                attempts++;
                actual = (int)value;
                throw error;
            });
        }
        catch (InvalidOperationException ex)
        {
            original = ReferenceEquals(ex.InnerException, error) && ex.Message.Contains("business-A");
        }

        Check(actual == 5 && attempts == 1 && original, "E5 partial setter side effect preserved, original cause/context, no retry");
        using var sibling = new UiInteractionSession();
        sibling.PointerActivation(Target, Sample(UiPointerPhase.Press), Geometry, Eligible);
        session.Dispose();
        session.Dispose();
        Check(sibling.CaptureOwner.HasValue, "E3 idempotent close isolates sibling");
    }

    private static void BooleanSelectionExpansion()
    {
        using var session = new UiInteractionSession();
        var first = new UiInteractionTarget("composite", "binding", "option", "a", "business");
        var second = new UiInteractionTarget("composite", "binding", "option", "b", "business");
        session.PointerActivation(second, Sample(UiPointerPhase.Press), Geometry, Eligible);
        session.PointerActivation(first, Sample(UiPointerPhase.None), Geometry, Eligible);
        Check(session.CaptureOwner?.Equals(second) == true, "BSE earlier sibling does not cancel captured selection");
        int requests = 0;
        string current = "b";
        var release = session.PointerActivation(second, Sample(UiPointerPhase.Release), Geometry, Eligible);
        UiValueBehavior.Request(second, UiValueBehavior.Selection(release, "b"), value =>
        {
            current = value;
            requests++;
        });
        UiValueBehavior.Request(second, UiValueBehavior.Selection(session.Activate(second, Eligible), "b"), value => requests++);
        Check(current == "b" && requests == 2, "BSE each established same-value selection requests once");
        UiValueBehavior.Request(second, UiValueBehavior.Selection(session.PointerActivation(second, Sample(UiPointerPhase.None), Geometry, Eligible), "b"), value => requests++);
        Check(requests == 2, "BSE repaint never repeats selection");
        Check(UiValueBehavior.Observe(new[] { "a", "b" }, "missing").Missing, "BSE missing current is fact, not first-item repair");
        Check(!UiValueBehavior.Observe(new[] { "a", "b" }, "b", value => false).Selectable, "BSE selected disabled option remains observable");
        Check(!UiValueBehavior.Boolean(session.Activate(second, new(false)), true).Request, "BSE disabled boolean refuses operation");
        Check(!UiValueBehavior.Selection(session.Activate(second, new(true, false)), "a").Request, "BSE read-only selection refuses operation");
        Check(!UiValueBehavior.Expansion(session.Activate(second, Eligible), true, false).Request, "BSE leaf cannot expand");
        bool expanded = false;
        UiValueBehavior.Request(second, UiValueBehavior.Expansion(session.Activate(second, Eligible), true, true), value => expanded = value);
        Check(expanded, "BSE expansion requests target state without owning model");
        session.PointerActivation(second, Sample(UiPointerPhase.Press), Geometry, Eligible);
        var replacement = new UiInteractionTarget("composite", "binding", "option", "b", "replacement");
        Check(!session.PointerActivation(replacement, Sample(UiPointerPhase.Release), Geometry, Eligible).Request && session.CaptureOwner == null, "BSE true business replacement ends capture despite same part/key");
        session.PointerActivation(second, Sample(UiPointerPhase.Press), Geometry, Eligible);
        var rebound = new UiInteractionTarget("composite", "other-binding", "option", "b", "business");
        Check(!session.PointerActivation(rebound, Sample(UiPointerPhase.Release), Geometry, Eligible).Request && session.LastEnd == UiInteractionEnd.TargetChanged && session.CaptureOwner == null, "BSE same part/item binding replacement ends capture without sibling cancellation");
        session.PointerActivation(second, Sample(UiPointerPhase.Press), Geometry, Eligible);
        Check(!UiValueBehavior.Boolean(session.PointerActivation(second, Sample(UiPointerPhase.Release, used: true), Geometry, Eligible), true).Request && session.CaptureOwner == null, "BSE consumed release ends capture without boolean request");
    }

    private static void Isolation()
    {
        var references = typeof(UiInteractionSession).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Check(!references.Any(n => new[] { "Unity", "Verse", "RimWorld", "Harmony", "Assembly-CSharp" }.Any(n.StartsWith)), "C1/C2 compiled production source has no platform assembly dependencies");
        Console.WriteLine("REFERENCES " + string.Join(", ", references));
    }

    private sealed class Recorder : ITextMetrics
    {
        public UiFont Font;
        public float Width;
        public float MeasureWidth(string text, UiFont font) => text.Length * (font == UiFont.Medium ? 10 : 5);
        public float MeasureText(string text, UiFont font, float width)
        {
            Font = font;
            Width = width;
            return (float)Math.Ceiling(MeasureWidth(text, font) / width) * 20;
        }
    }
}
