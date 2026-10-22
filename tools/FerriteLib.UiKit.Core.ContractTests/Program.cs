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
            FailureLifetimeTests.Run(Check);
            LayoutReuseTests.Run(Check);
            ReactiveFoundationTests.Run(Check);
            ReactiveInventoryTests.Run(Check);
            ReactiveListWriteTests.Run(Check);
            ItemBindingTests.Run(Check);
            NeutralDependencyTests.Run(Check);
            PointerLayers();
            ImageGeometry();
            Palette();
            Properties();
            Observations();
            StepperGeometry();
            SharedBehavior();
            IdentityAndEnd();
            BooleanSelectionExpansion();
            Isolation();
            Editing();
            PointInteraction();
            Layout();
            Console.WriteLine($"ALL PASS {checks} neutral checks");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void ImageGeometry()
    {
        var slot = new UiLayoutRect(10, 20, 100, 100);
        UiLayoutRect contain = UiImageLayout.Arrange(slot, 200, 100);
        Check(contain.X == 10 && contain.Y == 45 && contain.Width == 100 && contain.Height == 50, "image default contain center");
        UiLayoutRect cover = UiImageLayout.Arrange(slot, 200, 100, new UiImageOptions(UiImageFit.Cover));
        Check(cover.X == -40 && cover.Y == 20 && cover.Width == 200 && cover.Height == 100, "image cover preserves aspect beyond clip slot");
        UiLayoutRect aligned = UiImageLayout.Arrange(slot, 200, 100, new UiImageOptions(alignY: UiImageAlignY.Bottom));
        Check(aligned.Y == 70, "image alignment stays inside allocated slot");
        UiLayoutRect stretch = UiImageLayout.Arrange(slot, 200, 100, new UiImageOptions(UiImageFit.Stretch));
        Check(stretch.Width == 100 && stretch.Height == 100, "image explicit stretch");
        Check(UiImageLayout.Arrange(slot, 0, 100).Width == 0 && UiImageLayout.Arrange(slot, float.NaN, 100).Width == 0,
            "invalid source geometry is empty");
        Check(UiImageLayout.AspectHeight(80, 200, 100) == 40 && UiImageLayout.AspectHeight(float.PositiveInfinity, 200, 100) == 0,
            "width based aspect measure is finite");
        bool rejected = false;
        try { UiImageOptions.Parse("1", null, null); } catch (FormatException) { rejected = true; }
        Check(rejected, "image vocabulary rejects numeric enum aliases");
    }
    private static void PointerLayers()
    {
        var layers = new List<UiPointerLayer> {
            new("large", new UiLayoutRect(0,0,160,80)),
            new("lock", new UiLayoutRect(100,10,30,30))
        };
        Check(UiPointerLayers.IsCovered(layers,"large",115,25) && !UiPointerLayers.IsCovered(layers,"lock",115,25), "same production core resolves topmost content");
        Check(!UiPointerLayers.IsCovered(layers,"large",20,60), "nonoverlapping part stays available");
        layers[1] = new UiPointerLayer("lock",new UiLayoutRect(100,10,30,30),hasOperation:false);
        Check(!UiPointerLayers.IsCovered(layers,"large",115,25), "unconfigured upper region passes through");
        layers[1] = new UiPointerLayer("lock",new UiLayoutRect(100,10,30,30),new UiLayoutRect(0,0,105,100));
        Check(!UiPointerLayers.IsCovered(layers,"large",115,25) && UiPointerLayers.IsCovered(layers,"large",103,25), "effective clip participates in the same decision");
        layers.RemoveAt(1);
        Check(!UiPointerLayers.IsCovered(layers,"large",115,25), "removed layer has no stale coverage");
    }
    private static void Palette()
    {
        var palette = new UiPalette();
        var peer = new UiPalette();
        foreach (UiPaletteRole role in Enum.GetValues(typeof(UiPaletteRole)))
            Check(palette[role] == UiPalette.Default(role) && !palette.IsDeclared(role), "palette complete readonly default: " + role);
        palette[UiPaletteRole.WarningSurface] = new UiColour(.1f,.2f,.3f);
        Check(palette[UiPaletteRole.DangerSurface] == peer[UiPaletteRole.DangerSurface], "warning and danger independent");
        palette[UiPaletteRole.SuccessForeground] = new UiColour(.2f,.3f,.4f);
        Check(palette[UiPaletteRole.SelectedForeground] == peer[UiPaletteRole.SelectedForeground], "success and selection foreground independent");
        palette[UiPaletteRole.Accent] = peer[UiPaletteRole.Accent];
        Check(palette.IsDeclared(UiPaletteRole.Accent), "explicit default-equal declaration retains intent");
        var clone = palette.Clone();
        clone[UiPaletteRole.WarningSurface] = new UiColour(.8f,.7f,.6f);
        Check(palette[UiPaletteRole.WarningSurface] != clone[UiPaletteRole.WarningSurface], "clone declarations are isolated");
        palette.Remove(UiPaletteRole.WarningSurface);
        Check(!palette.IsDeclared(UiPaletteRole.WarningSurface) && palette[UiPaletteRole.WarningSurface] == peer[UiPaletteRole.WarningSurface], "remove reveals readonly default");
        foreach (string text in new[] { "#123", "#1234", "#112233", "#11223344", ".1,.2,.3", ".1,.2,.3,.4" })
            Check(UiColour.TryParse(text, out _), "neutral colour lexeme: " + text);
        foreach (string text in new[] { "#12", "#GGG", "NaN,0,0", "1.1,0,0", "1,2", "0,0,0,1,1" })
            Check(!UiColour.TryParse(text, out _), "neutral malformed colour refused: " + text);
        Check(!UiPalette.TryRole("1", out _) && !UiPalette.TryRole("TextOnGold", out _), "no numeric or legacy role aliases");
    }
    private static void Layout()
    {
        var frame = new UiLayoutFrame(200, 100, 5, 5, 5, 5, 0, 2, new UiLayoutHeight(100, false));
        var children = new[]
        {
            new UiLayoutChild(20),
            new UiLayoutChild(30)
        };
        UiLayoutSize Measure(int i, float width, float height, float? reference) => new(width, i == 0 ? 10 : 20);
        UiLayoutPlan stack = UiLayout.Stack(frame, children, Measure);
        Check(stack.Slots[0].Y == 5 && stack.Slots[1].Y == 17 && stack.NaturalHeight == 42, "LAYOUT Stack Column Section Surface and Clip consume the same padded vertical flow");
        UiLayoutPlan row = UiLayout.Row(frame, children, Measure);
        Check(row.Slots[1].X == 27 && row.NaturalHeight == 30, "LAYOUT Row consumes actual child widths and maximum measured height");
        UiLayoutPlan wrap = UiLayout.Wrap(frame, children, 1, Measure);
        Check(wrap.Slots[1].X == 5 && wrap.Slots[1].Y == 17, "LAYOUT Wrap column break uses measured line height");
        UiLayoutPlan content = UiLayout.VerticalContent(frame, children, Measure);
        Check(content.NaturalHeight == stack.NaturalHeight && content.Slots[1].Y == stack.Slots[1].Y, "LAYOUT Scroll and Repeat content consume the same production vertical geometry");
        Check(UiLayout.ScrollContentWidth(100, 101, 100, 16) == 84 && UiLayout.ScrollContentWidth(100, 100, 100, 16) == 100, "LAYOUT real full natural height alone decides scrollbar width reservation");
        UiLayoutPoint scroll = UiLayout.ClampScroll(new UiLayoutPoint(-10, 500), new UiLayoutSize(100, 300), new UiLayoutSize(100, 100));
        Check(scroll.X == 0 && scroll.Y == 200, "LAYOUT only actual content/viewport geometry bounds the single scroll authority");
        Check(UiLayout.SharedLabelColumn(new float[] { 10, 70, 20 }) == 70, "LAYOUT shared columns are an effective demand maximum, not copied author declarations");
        UiLayoutPlan overlay = UiLayout.Overlay(frame, children, Measure);
        UiLayoutPlan anchored = UiLayout.Anchored(overlay, frame, new UiLayoutAnchor? [] { new(1, 1, 0, new UiLayoutOffset(5)), new(-1, .5f, .5f, default) }, new UiLayoutAnchor? [2]);
        Check(anchored.Slots[1].X == 85 && anchored.Slots[0].X == 120, "LAYOUT actual neutral parent and sibling position geometry resolves forward references");
        bool cyclic = false;
        try
        {
            UiLayout.Anchored(overlay, frame, new UiLayoutAnchor? [] { new(1, 0, 0, default), new(0, 0, 0, default) }, new UiLayoutAnchor? [2]);
        }
        catch (ArgumentException)
        {
            cyclic = true;
        }

        Check(cyclic, "LAYOUT neutral cycle refusal cannot publish partial slots");
        UiWidthPlan widths = UiLayout.Columns(new[] { new UiWidthDemand(UiWidthMode.Auto, 100, 40), new UiWidthDemand(UiWidthMode.Flexible, 0, 30) }, 50, 0);
        Check(widths.Widths[0] == 40 && widths.Widths[1] == 30 && widths.Overflow, "LAYOUT insufficient parent budget retains declared minima and exposes overflow");
    }

    private static void PointInteraction()
    {
        using var session = new UiInteractionSession();
        var target = new UiInteractionTarget("chart", "series", "chart-point", "B");
        var sibling = new UiInteractionTarget("chart", "series", "chart-point", "A");
        var geometry = new UiHitGeometry(44, 44, 12, 12, 0, 100, 0, 100, true);
        UiInteractionResult Drive(UiPointerPhase phase, float x, float y, bool used = false) =>
            session.PointerPoint(target, Sample(phase, x, y, used), geometry, Eligible, .5f, .5f);
        Check(!Drive(UiPointerPhase.Press, 55, 55).Captured, "POINT square corner outside the existing circular radius is not a hit");
        Check(!Drive(UiPointerPhase.Press, 50, 50).Request && session.CaptureOwner?.Equals(target) == true,
            "POINT press captures without publishing a value");
        session.PointerPoint(sibling, Sample(UiPointerPhase.None), geometry, Eligible, 0, 1);
        Check(session.CaptureOwner?.Equals(target) == true, "POINT nonowner sibling visit cannot end capture");
        var moved = Drive(UiPointerPhase.Drag, 50, 20);
        Check(moved.Request && moved.Value == .5f && moved.Y == .8f, "POINT Y-only change is a shared normalized request");
        Check(!Drive(UiPointerPhase.Drag, 50, 20).Request, "POINT unchanged sample never retries business rejection");
        moved = Drive(UiPointerPhase.Drag, 120, -10);
        Check(moved.Request && moved.Value == 1 && moved.Y == 1, "POINT both coordinates are clamped by common mapping");
        Check(!Drive(UiPointerPhase.Release, 120, -10, true).Request && !session.CaptureOwner.HasValue,
            "POINT consumed release ends only the hold without another request");
        var range = new UiRangeObservation(15, 0, 10);
        Check(range.Value == 15 && range.Finite && range.Fraction == 1, "DESCRIPTION actual out-of-range model remains distinct from clamped fill");
        range = new UiRangeObservation(float.NaN, 0, 10);
        Check(!range.Finite && range.Fraction == 0, "DESCRIPTION invalid model is an explicit unknown, not a repair");
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
        foreach (string route in new[] { "confirm", "live", "takeover" })
        {
            model = 10; int completed = 0;
            var completedEdit = new UiEditSession<float>(numeric.Format, numeric.Parse);
            completedEdit.Observe(model); completedEdit.Focus();
            completedEdit.Input("20", true, false, false, Set, () => model);
            void Complete(float value) { completed++; model = value; }
            float BrokenRead() => throw new InvalidOperationException("read after normal return");
            try
            {
                if (route == "live") completedEdit.Input("20", true, false, true, Complete, BrokenRead);
                else if (route == "takeover") completedEdit.TakeOver(30, Complete, BrokenRead);
                else completedEdit.Confirm(true, Complete, BrokenRead);
            }
            catch (InvalidOperationException) { }
            Check(completed == 1 && (route == "live" || !completedEdit.HasDraft),
                "normal dispatch is complete before display read failure: " + route);
            completedEdit.Observe(model); completedEdit.Confirm(true, Complete, () => model);
            Check(completed == 1 && !completedEdit.HasDraft,
                "recovery and explicit no-candidate confirm never replay completed request: " + route);
        }
    }

    private static void StepperGeometry()
    {
        var density = new UiDensity(0, 0, 6, 28);
        var metrics = new Recorder();
        UiSliderLayout label = UiSliderLayout.Measure("", 80, UiFont.Small, density, metrics);
        UiStepperLayout narrow = UiStepperLayout.Arrange(80, 28, label, 24, 64, 6, 6);
        Check(narrow.RequiredWidth == 131 && narrow.Shortage == 51 && narrow.AvailableWidth == 80,
            "STEPPER internal required width is distinct from the authoritative outer allocation");
        Check(narrow.Minus.X == 0 && narrow.Range.X == 30 && narrow.Range.Width == 1
            && narrow.Field.X == 37 && narrow.Field.Width == 64 && narrow.Plus.X == 107,
            "STEPPER same neutral geometry defines actual private parts and diagnosed shortage");
        UiStepperLayout exact = UiStepperLayout.Arrange(131, 28, label, 24, 64, 6, 6);
        Check(exact.Shortage == 0 && exact.Plus.X + exact.Plus.Width == 131,
            "STEPPER exact fit does not report a false shortage");
        UiStepperLayout configured = UiStepperLayout.Arrange(80, 28, label, 10, 20, 6, 6);
        Check(configured.Shortage == 0 && configured.Range.Width == 22,
            "STEPPER explicit existing part width configuration can fit without a new MinWidth prerequisite");
        UiSliderLayout stacked = UiSliderLayout.Measure("label", 80, UiFont.Small, density, metrics, 0, 186);
        UiStepperLayout labelled = UiStepperLayout.Arrange(80, stacked.Height, stacked, 24, 64, 6, 6);
        Check(stacked.Stacked && labelled.Minus.Y == stacked.LabelHeight + 6 && labelled.Shortage == 51,
            "STEPPER stacking a label does not silently satisfy an independent internal width shortage");
    }
    private static void Observations()
    {
        var budget = new UiObservationBudget(nodes: 2, depth: 1, text: 3, items: 2);
        Check(budget.Limit("abcdef") == "abc" && budget.Limit("a") == "a", "neutral observation text budget");
        bool refused = false;
        try { _ = new UiObservationBudget(nodes: 513); }
        catch (ArgumentOutOfRangeException) { refused = true; }
        Check(refused, "neutral observation output budget is finite");
        var fact = new UiObservedFact("value", "abc", UiObservationStatus.Stale, "last-layout", "new-definition", true);
        Check(fact.Truncated && fact.Status == UiObservationStatus.Stale && fact.Reason == "new-definition", "sampling state and truncation remain separate facts");
        var observation = new UiHostObservation(1, "page", 7, 3, 2, DateTime.UtcNow, UiObservationStatus.Stale,
            "definition-newer-than-layout", false, Array.Empty<UiObservedNode>());
        Check(observation.Definition == 3 && observation.LayoutDefinition == 2 && observation.Frame == 7,
            "neutral observation preserves distinct current and layout revisions");
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

