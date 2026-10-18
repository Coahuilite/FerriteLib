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
            Isolation();
            Console.WriteLine($"ALL PASS {checks} neutral checks");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
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
