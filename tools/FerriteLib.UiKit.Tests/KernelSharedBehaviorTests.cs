using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;
internal static class KernelSharedBehaviorTests
{
    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        Console.WriteLine("  ok: S01 " + name);
    }

    private static readonly Rect Bounds = new(0, 0, 100, 28);
    private static Dictionary<string, string> Attrs(params (string, string)[] values) => values.ToDictionary(p => p.Item1, p => p.Item2);
    private static void Pointer(UiPointerPhase phase, float x = 50, float y = 14)
    {
        UiNative.DebugMousePositionEnabled = true;
        UiNative.DebugMousePosition = new Vector2(x, y);
        UiNative.DebugMouseDown = phase == UiPointerPhase.Press;
        UiNative.DebugMouseDrag = phase == UiPointerPhase.Drag;
        UiNative.DebugMouseUp = phase == UiPointerPhase.Release;
    }

    private static void Reset()
    {
        UiNative.ButtonOverride = null;
        UiNative.SliderOverride = null;
        Event.current = null;
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMouseDown = false;
        UiNative.DebugMouseDrag = false;
        UiNative.DebugMouseUp = false;
        UiNative.DebugHotControl = 0;
    }

    public static int RunAll()
    {
        try
        {
            Reset();
            ThreeRoutes();
            PointerFailures();
            PropertiesInRealSlider();
            Lifecycle();
            CallbackClose();
            SetterFailure();
            AbortedHitPass();
            ConsumedNativeRelease();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: S01 " + ex);
            return 1;
        }
        finally
        {
            Reset();
        }
    }

    private static List<string> Route(int mode)
    {
        using var session = new UiSession();
        int activations = 0;
        int writes = 0;
        float value = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("go", () => activations++);
        bindings.BindValue("level", () => value, v =>
        {
            value = v;
            writes++;
        });
        var ctx = new UiWidgetContext("s01", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), bindings, 100, "row");
        var button = new ButtonWidget();
        button.Configure(new UiElementSpec("go", ButtonWidget.Kind, Attrs(("ActionBind", "go"))));
        var slider = new SliderWidget();
        slider.Configure(new UiElementSpec("level", SliderWidget.Kind, Attrs(("Bind", "level"), ("Max", "10"))));
        var trace = new List<string>();
        void Drive(bool range, UiPointerPhase phase, float x, bool allowed = true, bool consumed = false, bool covered = false)
        {
            Pointer(phase, x);
            session.PointerInputAllowed = allowed;
            if (consumed)
                Event.current = new Event
                {
                    type = EventType.Used,
                    mousePosition = new Vector2(x, 14),
                    button = 0
                };
            if (covered)
            {
                session.BeginHitPass();
                session.PushHitLayer(session.ActiveNode, Bounds, isPopup: true);
                session.EndHitPass();
                session.BeginHitPass();
            }

            if (mode == 0)
            {
                if (range)
                    slider.Draw(Bounds, ctx);
                else
                    button.Draw(Bounds, ctx);
            }
            else if (mode == 1)
            {
                // Independent composition: different visuals, not derived from either default wrapper.
                UiInteractionResult result = range ? UiNative.Range(Bounds, ctx, "level", value, 0, 10) : UiNative.Activation(Bounds, ctx, "go");
                if (range)
                    UiInteractionRequests.Range(UiNative.InteractionTarget(ctx, "level"), result, v =>
                    {
                        value = v;
                        writes++;
                    });
                else
                    UiInteractionRequests.Activation(UiNative.InteractionTarget(ctx, "go"), result, () => activations++);
                UiThemeDraw.Solid(new Rect(0, 26, 100, 2), ctx.Theme[UiPaletteRole.Accent]);
            }
            else
            {
                // No default Draw or platform event loop: same production behavior/session and request boundary.
                var target = UiNative.InteractionTarget(ctx, range ? "level" : "go");
                var sample = new UiPointerSample(phase, x, 14, consumed);
                var geometry = range ? new UiHitGeometry(0, 0, 100, 28, 6, 88) : new UiHitGeometry(0, 0, 100, 28);
                var result = range ? session.Interactions.PointerRange(target, sample, geometry, new(allowed, true, !covered), new(0, 10), value) : session.Interactions.PointerActivation(target, sample, geometry, new(allowed, true, !covered));
                if (range)
                    UiInteractionRequests.Range(target, result, v =>
                    {
                        value = v;
                        writes++;
                    });
                else
                    UiInteractionRequests.Activation(target, result, () => activations++);
            }

            Event.current = null;
            if (covered)
                session.EndHitPass();
            trace.Add($"{activations}:{writes}:{value}:{session.Interactions.CaptureOwner?.TargetScope}:{session.Interactions.LastEnd}");
        }

        Drive(false, UiPointerPhase.Press, 50);
        Drive(false, UiPointerPhase.Release, 50);
        Drive(false, UiPointerPhase.None, 50);
        Drive(true, UiPointerPhase.Press, 50);
        Drive(true, UiPointerPhase.Drag, 28);
        Drive(true, UiPointerPhase.Drag, 150);
        Drive(true, UiPointerPhase.None, 150);
        Drive(true, UiPointerPhase.Release, 150);
        Drive(false, UiPointerPhase.Press, 50, allowed: false);
        Drive(false, UiPointerPhase.Release, 50, allowed: false);
        Drive(true, UiPointerPhase.Press, 50, consumed: true);
        Drive(false, UiPointerPhase.Press, 50, covered: true);
        Drive(true, UiPointerPhase.Press, 50, covered: true);
        Check(activations == 1 && writes == 3 && value == 10 && !session.Interactions.CaptureOwner.HasValue, "H1/H2/H4 route " + mode + " request/repaint/release");
        if (mode != 2)
            Check(!session.OwnedHotControl.HasValue && UiNative.DebugHotControl == 0, "native release route " + mode);
        Console.WriteLine("  S01 TRACE " + mode + " " + string.Join(" | ", trace));
        return trace;
    }

    private static void ThreeRoutes()
    {
        var standard = Route(0);
        var custom = Route(1);
        var headless = Route(2);
        Check(standard.SequenceEqual(custom) && standard.SequenceEqual(headless), "real default/custom/headless trajectories match");
    }

    private static void PointerFailures()
    {
        using var session = new UiSession();
        var node = session.GetOrCreateNode(UiNodeId.Root(new UiElementSpec("b", ButtonWidget.Kind, new Dictionary<string, string>()), 0), ButtonWidget.Kind, 0);
        var ctx = new UiWidgetContext("s01", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), new UiBindings(), 100, "b", node: node);
        UiNode previous = session.EnterNode(node);
        try
        {
            session.SetHostViewport(new Rect(200, 100, 100, 28));
            session.BeginHitPass();
            UiWidgetContext offset = ctx.WithWindowOrigin(new Vector2(200, 100));
            Pointer(UiPointerPhase.Press);
            Check(UiNative.WindowPointer(offset).x == 250 && UiNative.WindowPointer(offset).y == 114 && UiNative.CanReceivePointerPress(Bounds, offset), "nonzero content origin maps pointer and effective clip once");
            session.EndHitPass();
            session.SetHostViewport(Bounds);
            session.BeginHitPass();
            var used = new Event
            {
                type = EventType.Used,
                mousePosition = new Vector2(50, 14),
                button = 0
            };
            Event.current = used;
            Check(!UiNative.Activation(Bounds, ctx).Captured, "H4 consumed native event cannot arm");
            Event.current = null;
            Pointer(UiPointerPhase.Press);
            UiNative.Activation(Bounds, ctx);
            Check(session.Interactions.CaptureOwner.HasValue, "F1 native press captures shared session");
            Pointer(UiPointerPhase.Release, 150);
            Check(!UiNative.Activation(Bounds, ctx).Request && !session.OwnedHotControl.HasValue, "F1 outside release abandons and releases native capture");
            Pointer(UiPointerPhase.Press);
            UiNative.Activation(Bounds, ctx);
            node.IsDisabled = true;
            Pointer(UiPointerPhase.Release);
            Check(!UiNative.Activation(Bounds, ctx).Request && !session.Interactions.CaptureOwner.HasValue, "disabled after press ends without request");
            node.IsDisabled = false;
            Pointer(UiPointerPhase.Press);
            UiNative.Activation(Bounds, ctx);
            UiNative.DebugHotControl = 12345;
            Pointer(UiPointerPhase.Release);
            Check(!UiNative.Activation(Bounds, ctx).Request && UiNative.DebugHotControl == 12345, "native capture loss does not release another owner");
            UiNative.DebugHotControl = 0;
            Pointer(UiPointerPhase.Press);
            UiNative.Activation(Bounds, ctx);
            UiNode replacementOwner = session.GetOrCreateNode(UiNodeId.Root(new UiElementSpec("replacement", ButtonWidget.Kind, new Dictionary<string, string>()), 1), ButtonWidget.Kind, 1);
            UiNode old = session.EnterNode(replacementOwner);
            session.CaptureHotControl(12345);
            session.ExitNode(old);
            Pointer(UiPointerPhase.Release);
            Check(!UiNative.Activation(Bounds, ctx).Request && UiNative.DebugHotControl == 12345 && ReferenceEquals(session.OwnedHotControlOwner, replacementOwner), "same-session capture takeover ends the old operation without releasing its new owner");
            session.ReleaseHotControl(12345);
            session.PointerInputAllowed = false;
            Pointer(UiPointerPhase.Press);
            Check(!UiNative.Activation(Bounds, ctx).Captured, "native window qualification rejects press");
            session.PointerInputAllowed = true;
            Pointer(UiPointerPhase.Press);
            UiNative.Range(Bounds, ctx, "level", 0, 0, 10);
            node.InteractionScope = "new-target";
            Pointer(UiPointerPhase.Release);
            Check(!UiNative.Range(Bounds, ctx, "level", 0, 0, 10).Request && !session.OwnedHotControl.HasValue, "I2 target replacement clears logical/native capture");
            Pointer(UiPointerPhase.Press);
            UiNative.Activation(Bounds, ctx);
            UiSessionGuard.DrawOrFallback(session, "b", "custom", "b", Bounds, () => throw new InvalidOperationException("presentation only"), null);
            Check(session.Interactions.CaptureOwner.HasValue, "E4 presentation-only failure keeps reliable behavior capture");
            Pointer(UiPointerPhase.Release);
            Check(UiNative.Activation(Bounds, ctx).Request, "E4 preserved capture still releases through shared behavior");
            Pointer(UiPointerPhase.Press);
            UiNative.Activation(Bounds, ctx);
            bool geometryFailed = false;
            try
            {
                UiNative.Activation(new Rect(0, 0, 0, 28), ctx);
            }
            catch (InvalidOperationException ex)
            {
                geometryFailed = ex.Message.Contains("geometry");
            }

            Check(geometryFailed && !session.Interactions.CaptureOwner.HasValue && !session.OwnedHotControl.HasValue, "E4 unreliable geometry diagnoses and releases both capture layers");
        }
        finally
        {
            session.ExitNode(previous);
        }
    }

    private static void PropertiesInRealSlider()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        int writes = 0;
        var bindings = new UiBindings();
        bindings.BindValue("v", () => 0f, v => writes++);
        using var host = new UiHost("s01", UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"s01\"><Column Id=\"parent\"><Widget Id=\"slider\" Kind=\"input/slider\" Bind=\"v\" Label=\"Long label wraps across the track\" /></Column></UiPage>"), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        Pointer(UiPointerPhase.None, -1000);
        host.DrawFrame(new Rect(0, 0, 120, 400));
        var parent = host.Session.GetNodeByElementId("parent")!;
        var slider = host.Session.GetNodeByElementId("slider")!;
        parent.Properties.Font.Set(UiFont.Small);
        parent.Properties.Density.Set(new(2, 2, 3, 20));
        slider.Properties.Font.Set(UiFont.Tiny);
        host.DrawFrame(new Rect(0, 0, 120, 400));
        parent.Properties.Font.Set(UiFont.Medium);
        parent.Properties.Density.Set(new(1, 1, 2, 16));
        host.DrawFrame(new Rect(0, 0, 120, 400));
        Check(slider.Properties.ResolvedFont.Value == UiFont.Tiny && slider.Properties.ResolvedDensity.Value.RowHeight == 16, "P1 real Slider Tiny/current compact density");
        slider.Properties.Font.Remove();
        host.DrawFrame(new Rect(0, 0, 120, 400));
        Check(slider.Properties.ResolvedFont.Value == UiFont.Medium && slider.Properties.ResolvedFont.Origin == UiPropertyOrigin.Ancestor, "P2 real Slider follows parent Medium on removal");
        float medium = slider.Rect!.Value.height;
        parent.Properties.Font.Set(UiFont.Small);
        host.DrawFrame(new Rect(0, 0, 120, 400));
        Check(slider.Properties.ResolvedFont.Value == UiFont.Small && slider.Rect!.Value.height < medium, "P3 real wrapper remeasures after parent font change");
        slider.Properties.LocalFont.Set(UiFont.Small);
        slider.Properties.Font.Set(UiFont.Tiny);
        slider.Properties.Font.Remove();
        host.DrawFrame(new Rect(0, 0, 120, 400));
        Check(slider.Properties.ResolvedFont.Origin == UiPropertyOrigin.LocalStyle && writes == 0, "P4 real local style succeeds, no business writes");
        slider.Properties.LocalFont.Remove();
        slider.Properties.Font.Set(UiFont.Small);
        parent.Properties.Font.Set(UiFont.Tiny);
        host.DrawFrame(new Rect(0, 0, 120, 400));
        Check(slider.Properties.ResolvedFont.Value == UiFont.Small && slider.Properties.ResolvedFont.Origin == UiPropertyOrigin.Instance, "P5 real equal explicit font retains declaration intent");
        slider.Properties.Font.Remove();
        parent.Properties.Font.Remove();
        host.StyleResolver.Baseline.DefaultFont = UiFont.Medium;
        host.DrawFrame(new Rect(0, 0, 120, 400));
        Check(slider.Properties.ResolvedFont.Origin == UiPropertyOrigin.Host && slider.Properties.ResolvedFont.Value == UiFont.Medium, "P6 real host default source");
        host.StyleResolver.Baseline.RemoveFontOverride();
        host.DrawFrame(new Rect(0, 0, 120, 400));
        Check(slider.Properties.ResolvedFont.Origin == UiPropertyOrigin.Fallback && slider.Properties.ResolvedFont.Value == UiFont.Small && writes == 0, "P6 real fallback after host declaration removal, zero writes");
        slider.Properties.Font.Set(UiFont.Tiny);
        UiTheme palette = UiTheme.Vanilla;
        _ = slider.Properties.Apply(slider, palette);
        int revision = palette.LayoutRevision;
        palette[UiPaletteRole.PanelSurface] = new Color(1, 0, 0, 1);
        UiTheme recolored = slider.Properties.Apply(slider, palette);
        Check(recolored[UiPaletteRole.PanelSurface].r == 1 && recolored[UiPaletteRole.PanelSurface].g == 0 && palette.LayoutRevision == revision, "property overlay follows host color-only changes without forcing layout");
    }

    private static void Lifecycle()
    {
        using var session = new UiSession();
        var node = session.GetOrCreateNode(UiNodeId.Root(new UiElementSpec("b", ButtonWidget.Kind, new Dictionary<string, string>()), 0), ButtonWidget.Kind, 0);
        var ctx = new UiWidgetContext("s01", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), new UiBindings(), 100, "b", node: node);
        session.SetHostViewport(Bounds);
        session.BeginHitPass();
        var previous = session.EnterNode(node);
        Pointer(UiPointerPhase.Press);
        UiNative.Activation(Bounds, ctx);
        session.ExitNode(previous);
        session.EndHitPass();
        session.BeginHitPass();
        session.EndHitPass();
        Check(!session.OwnedHotControl.HasValue && !session.Interactions.CaptureOwner.HasValue, "hidden owner releases both capture layers");
        previous = session.EnterNode(node);
        Pointer(UiPointerPhase.Press);
        UiNative.Activation(Bounds, ctx);
        session.ExitNode(previous);
        session.PruneNodesExcept(new HashSet<UiNodeId>());
        Check(!session.Interactions.CaptureOwner.HasValue && !session.OwnedHotControl.HasValue, "removed owner releases capture immediately");
        session.Dispose();
        session.Dispose();
        Check(UiNative.DebugHotControl == 0, "actual close is idempotent");
    }

    private static void CallbackClose()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiHost? host = null;
        int requests = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("close", () =>
        {
            requests++;
            host!.Close();
        });
        host = new UiHost("s01", UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"s01\"><Column><Scroll Id=\"scroll\" Height=\"80\"><Clip><Column><Widget Id=\"close\" Kind=\"input/button\" ActionBind=\"close\" /></Column></Clip></Scroll><Widget Kind=\"text/wrapped\" Text=\"after\" /></Column></UiPage>"), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        using (host)
        {
            Pointer(UiPointerPhase.None, -1000);
            host.DrawFrame(new Rect(0, 0, 200, 100));
            Rect button = host.Session.GetNodeByElementId("close")!.Rect!.Value;
            Pointer(UiPointerPhase.Press, button.x + 5, button.y + 5);
            host.DrawFrame(new Rect(0, 0, 200, 100));
            Pointer(UiPointerPhase.Release, button.x + 5, button.y + 5);
            host.DrawFrame(new Rect(0, 0, 200, 100));
            Check(requests == 1 && !host.Session.IsActive && UiNative.DebugHotControl == 0 && host.Session.ScrollPositions.Count == 0, "E3 callback closes without later traversal or normal frame cleanup");
        }
    }

    private static void SetterFailure()
    {
        using var session = new UiSession();
        int requests = 0;
        float model = 0;
        var cause = new InvalidOperationException("setter changed the model then failed");
        var bindings = new UiBindings();
        bindings.BindValue("v", () => model, v =>
        {
            requests++;
            model = v;
            throw cause;
        });
        var ctx = new UiWidgetContext("s01", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), bindings, 100, "slider");
        var slider = new SliderWidget();
        slider.Configure(new UiElementSpec("slider", SliderWidget.Kind, Attrs(("Bind", "v"), ("Max", "10"))));
        Pointer(UiPointerPhase.Press);
        bool contextual = false;
        try
        {
            slider.Draw(Bounds, ctx);
        }
        catch (InvalidOperationException ex)
        {
            contextual = ReferenceEquals(ex.InnerException, cause) && ex.Message.Contains("v");
        }

        Pointer(UiPointerPhase.None);
        slider.Draw(Bounds, ctx);
        Check(contextual && requests == 1 && model == 5, "E5 real Slider keeps partial effect and original cause; repaint never retries");
        Pointer(UiPointerPhase.Release);
        slider.Draw(Bounds, ctx);
    }

    private static void AbortedHitPass()
    {
        using var session = new UiSession();
        var node = session.GetOrCreateNode(UiNodeId.Root(new UiElementSpec("a", ButtonWidget.Kind, new Dictionary<string, string>()), 0), ButtonWidget.Kind, 0);
        var other = session.GetOrCreateNode(UiNodeId.Root(new UiElementSpec("b", ButtonWidget.Kind, new Dictionary<string, string>()), 1), ButtonWidget.Kind, 1);
        session.SetHostViewport(Bounds);
        session.BeginHitPass();
        session.PushHitLayer(node, Bounds, false);
        session.PushHitLayer(other, Bounds, false);
        session.EndHitPass();
        session.BeginHitPass();
        session.PushHitLayer(node, new Rect(0, 0, 10, 10), false);
        session.AbortHitPass();
        Check(session.HitLayers.Count == 2 && session.GetNode(other.Id) != null && !session.IsInputGeometryReliable, "E4 aborted traversal restores complete hit list and keeps unvisited identities");
        var ctx = new UiWidgetContext("s01", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), new UiBindings(), 100, "a", node: node);
        session.BeginHitPass();
        Pointer(UiPointerPhase.Press);
        Check(!UiNative.Activation(Bounds, ctx).Captured, "E4 recovery pass suspends pointer input until geometry completes");
        session.PushHitLayer(node, Bounds, false);
        session.EndHitPass();
        Pointer(UiPointerPhase.Press);
        Check(UiNative.Activation(Bounds, ctx).Captured, "E4 a new press works after a complete recovery pass");
    }

    private static void ConsumedNativeRelease()
    {
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMouseDown = false;
        UiNative.DebugMouseDrag = false;
        UiNative.DebugMouseUp = false;
        int previousHotControl = GUIUtility.hotControl;
        try
        {
            foreach (bool range in new[]
            {
                false,
                true
            }

            )
            {
                using var session = new UiSession();
                using var otherSession = new UiSession();
                var node = session.GetOrCreateNode(UiNodeId.Root(new UiElementSpec("native", range ? SliderWidget.Kind : ButtonWidget.Kind, new Dictionary<string, string>()), 0), range ? SliderWidget.Kind : ButtonWidget.Kind, 0);
                int requests = 0;
                float model = 0;
                var bindings = new UiBindings();
                bindings.BindCommand("go", () => requests++);
                bindings.BindValue("level", () => model, value =>
                {
                    model = value;
                    requests++;
                });
                var context = new UiWidgetContext("s01", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), bindings, 100, "native", node: node);
                var button = new ButtonWidget();
                button.Configure(new UiElementSpec("native", ButtonWidget.Kind, Attrs(("ActionBind", "go"))));
                var slider = new SliderWidget();
                slider.Configure(new UiElementSpec("native", SliderWidget.Kind, Attrs(("Bind", "level"), ("Max", "10"))));
                void Draw()
                {
                    if (range)
                        slider.Draw(Bounds, context);
                    else
                        button.Draw(Bounds, context);
                }

                void NativeEvent(EventType type, int mouseButton, bool consume, float x = 50)
                {
                    Event.current = new Event
                    {
                        type = type,
                        button = mouseButton,
                        mousePosition = new Vector2(x, 14)
                    };
                    if (consume)
                        Event.current.Use();
                }

                GUIUtility.hotControl = 0;
                NativeEvent(EventType.MouseDown, 0, false);
                Draw();
                int afterPress = requests;
                Check(session.Interactions.CaptureOwner.HasValue && session.OwnedHotControl.HasValue && GUIUtility.hotControl != 0, "native Event primary press captures without debug mouse flags: range=" + range);
                NativeEvent(EventType.MouseUp, 1, true);
                Draw();
                Check(session.Interactions.CaptureOwner.HasValue && requests == afterPress, "consumed secondary release does not end a primary capture: range=" + range);
                NativeEvent(EventType.MouseUp, 0, true);
                Check(Event.current.type == EventType.Used && Event.current.rawType == EventType.MouseUp, "Event.Use preserves the raw primary release protocol");
                Draw();
                Check(!session.Interactions.CaptureOwner.HasValue && !session.OwnedHotControl.HasValue && GUIUtility.hotControl == 0 && session.Interactions.LastEnd == UiInteractionEnd.Released && requests == afterPress, "Used/raw MouseUp releases own capture without activation or another range request: range=" + range);
                NativeEvent(EventType.MouseDown, 0, true);
                Draw();
                Check(!session.Interactions.CaptureOwner.HasValue && requests == afterPress, "raw consumed press never starts an operation: range=" + range);
                NativeEvent(EventType.MouseDown, 0, false);
                Draw();
                afterPress = requests;
                otherSession.CaptureHotControl(12345);
                NativeEvent(EventType.MouseUp, 0, true, 150);
                Draw();
                Check(!session.Interactions.CaptureOwner.HasValue && !session.OwnedHotControl.HasValue && GUIUtility.hotControl == 12345 && otherSession.OwnedHotControl == 12345 && requests == afterPress, "consumed native release never clears another session's capture: range=" + range);
                otherSession.ReleaseHotControl(12345);
            }
        }
        finally
        {
            Event.current = null;
            GUIUtility.hotControl = previousHotControl;
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureWidth(string text, UiFont font) => text.Length * (font == UiFont.Medium ? 12 : font == UiFont.Tiny ? 5 : 8);
        public float MeasureText(string text, UiFont font, float width) => (float)Math.Ceiling(MeasureWidth(text, font) / Math.Max(1, width)) * (font == UiFont.Medium ? 24 : 16);
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public int TranslationRevision => 0;

        public string Translate(string key) => key;
    }
}
