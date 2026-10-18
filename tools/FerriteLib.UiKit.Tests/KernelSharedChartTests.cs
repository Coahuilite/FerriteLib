using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;
internal static class KernelSharedChartTests
{
    private static readonly Rect Bounds = new(0, 0, 200, 120);
    private static readonly Rect Plot = new(8, 8, 184, 104);
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        Console.WriteLine("  ok: POINT " + message);
    }

    public static int RunAll()
    {
        try
        {
            var standard = Trajectory(0);
            Check(standard.SequenceEqual(Trajectory(1)) && standard.SequenceEqual(Trajectory(2)), "standard chart, independent native and nonrendering point trajectories agree");
            IdentityAndRecovery();
            PaintFailureAndClose();
            StaticEntry();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: POINT " + ex);
            return 1;
        }
        finally
        {
            Event.current = null;
            GUIUtility.hotControl = 0;
        }
    }

    private static void Input(EventType phase, float x, float y, bool used = false)
    {
        UiNative.DebugMousePositionEnabled = false;
        Event.current = new Event
        {
            type = phase,
            button = 0,
            mousePosition = new Vector2(x, y)
        };
        if (used)
            Event.current.Use();
    }

    private static UiWidgetContext Context(UiSession session, UiBindings bindings) => new("point", session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, Bounds.width, "chart");
    private static LineChartWidget Widget(bool keyed = false)
    {
        var widget = new LineChartWidget();
        var attributes = new Dictionary<string, string>
        {
            ["Bind"] = "points",
            ["ActionBind"] = "change",
            ["Editable"] = "true"
        };
        if (keyed)
            attributes["PointKeysBind"] = "keys";
        widget.Configure(new UiElementSpec("chart", LineChartWidget.Kind, attributes));
        return widget;
    }

    private static List<string> Trajectory(int route)
    {
        GUIUtility.hotControl = 0;
        using var session = new UiSession();
        var points = new List<Vector2>
        {
            new(0, 1),
            new(.5f, .5f),
            new(1, 0)
        };
        var bindings = new UiBindings();
        var requests = new List<string>();
        bindings.BindReadOnly<IReadOnlyList<Vector2>>("points", () => points);
        bindings.BindAction<UiChartPointChange>("change", change => requests.Add(change.X + "," + change.Y));
        var ctx = Context(session, bindings);
        var widget = Widget();
        using var pure = new UiInteractionSession();
        var target = new UiInteractionTarget("", "points\u001fchange\u001f\u001f", "chart-point", 1);
        var trace = new List<string>();
        void Drive(EventType phase, float x, float y, bool used = false)
        {
            Input(phase, x, y, used);
            if (route == 0)
                widget.Draw(Bounds, ctx);
            else
            {
                UiInteractionResult operation;
                if (route == 1)
                    operation = UiNative.Point(new Rect(94, 54, 12, 12), Plot, ctx, "points\u001fchange\u001f", 1, .5f, .5f);
                else
                {
                    UiPointerPhase pointer = phase == EventType.MouseDown ? UiPointerPhase.Press : phase == EventType.MouseDrag ? UiPointerPhase.Drag : phase == EventType.MouseUp ? UiPointerPhase.Release : UiPointerPhase.None;
                    operation = pure.PointerPoint(target, new UiPointerSample(pointer, x, y, used), new UiHitGeometry(94, 54, 12, 12, Plot.x, Plot.width, Plot.y, Plot.height), new UiInputEligibility(true), .5f, .5f);
                }

                UiInteractionRequests.Activation(target, operation, () => requests.Add(operation.Value + "," + operation.Y));
            }

            bool held = route == 2 ? pure.CaptureOwner.HasValue : session.Interactions.CaptureOwner.HasValue;
            trace.Add(string.Join(";", requests) + "|" + held);
        }

        Drive(EventType.MouseDown, 100, 60);
        Check(requests.Count == 0, "point press alone does not publish: route=" + route);
        Drive(EventType.Repaint, 100, 60);
        Drive(EventType.MouseDrag, 146, 34);
        Drive(EventType.MouseDrag, 146, 34);
        Check(requests.Count == 1, "repeated point sample does not replay a rejected business change: route=" + route);
        Drive(EventType.MouseDrag, 400, 300);
        Drive(EventType.MouseUp, 400, 300, true);
        Check(requests.Count == 2, "consumed native release ends without another point request: route=" + route);
        return trace;
    }

    private static void IdentityAndRecovery()
    {
        GUIUtility.hotControl = 0;
        using var session = new UiSession();
        var points = new List<Vector2>
        {
            new(0, 1),
            new(.5f, .5f),
            new(1, 0)
        };
        var keys = new List<string>
        {
            "A",
            "B",
            "C"
        };
        var changes = new List<UiChartPointChange>();
        var bindings = new UiBindings();
        bool fail = false;
        var cause = new InvalidOperationException("point callback failed after write");
        bindings.BindReadOnly<IReadOnlyList<Vector2>>("points", () => points);
        bindings.BindReadOnly<IReadOnlyList<string>>("keys", () => keys);
        bindings.BindAction<UiChartPointChange>("change", change =>
        {
            changes.Add(change);
            points[change.Index] = new Vector2(change.X, change.Y);
            if (fail)
                throw cause;
        });
        var ctx = Context(session, bindings);
        var widget = Widget(true);
        Input(EventType.MouseDown, 100, 60);
        widget.Draw(Bounds, ctx);
        (points[0], points[1]) = (points[1], points[0]);
        (keys[0], keys[1]) = (keys[1], keys[0]);
        Input(EventType.MouseDrag, 146, 34);
        widget.Draw(Bounds, ctx);
        Check(changes.Count == 1 && changes[0].Index == 0 && changes[0].Key == "B" && points[1].x == 0, "stable keyed point reorders to current Index without writing another point");
        Input(EventType.Repaint, 146, 34);
        widget.Draw(Bounds, ctx);
        Check(session.Interactions.CaptureOwner?.ItemKey?.Equals("B") == true, "normal keyed drag echo does not look like target replacement");
        keys[0] = "replacement";
        Input(EventType.MouseDrag, 160, 20);
        widget.Draw(Bounds, ctx);
        Check(changes.Count == 1 && !session.Interactions.CaptureOwner.HasValue && GUIUtility.hotControl == 0, "removed logical point ends both capture records without writing its replacement");
        widget = Widget();
        Input(EventType.MouseDown, 146, 34);
        widget.Draw(Bounds, ctx);
        fail = true;
        Input(EventType.MouseDrag, 160, 20);
        Exception? observed = null;
        try
        {
            widget.Draw(Bounds, ctx);
        }
        catch (Exception ex)
        {
            observed = ex;
        }

        Check(ReferenceEquals(observed?.InnerException, cause) && changes.Count == 2, "point callback partial effects retain original contextual cause");
        fail = false;
        Input(EventType.Repaint, 160, 20);
        widget.Draw(Bounds, ctx);
        Check(session.Interactions.CaptureOwner.HasValue && changes.Count == 2, "legacy normal self-drag echo is retained without callback replay");
        points[1] = new Vector2(.2f, .2f);
        Input(EventType.MouseDrag, 170, 10);
        widget.Draw(Bounds, ctx);
        Check(!session.Interactions.CaptureOwner.HasValue && changes.Count == 2, "observed ambiguous legacy series replacement ends capture rather than guessing identity");
        widget = Widget(true);
        keys[1] = keys[0];
        Input(EventType.Repaint, 100, 60);
        observed = null;
        try
        {
            widget.Draw(Bounds, ctx);
        }
        catch (Exception ex)
        {
            observed = ex;
        }

        Check(observed?.Message.Contains("unique") == true, "explicit keys are validated without imposing a prerequisite on old calls");
    }

    private static void PaintFailureAndClose()
    {
        var paintFailure = typeof(Verse.Widgets).GetField("DrawBoxSolidFailure")!;
        foreach (bool close in new[]
        {
            false,
            true
        }

        )
        {
            GUIUtility.hotControl = 0;
            using var session = new UiSession();
            var bindings = new UiBindings();
            var points = new[]
            {
                new Vector2(0, 1),
                new Vector2(.5f, .5f),
                new Vector2(1, 0)
            };
            int requests = 0;
            bindings.BindReadOnly<IReadOnlyList<Vector2>>("points", () => points);
            bindings.BindAction<UiChartPointChange>("change", _ =>
            {
                requests++;
                if (close)
                    session.Dispose();
            });
            var ctx = Context(session, bindings);
            var widget = Widget();
            Input(EventType.MouseDown, 100, 60);
            widget.Draw(Bounds, ctx);
            var cause = new InvalidOperationException("pure paint failure");
            paintFailure.SetValue(null, cause);
            Exception? observed = null;
            try
            {
                Input(EventType.MouseDrag, 146, 34);
                widget.Draw(Bounds, ctx);
            }
            catch (Exception ex)
            {
                observed = ex;
            }
            finally
            {
                paintFailure.SetValue(null, null);
            }

            Check(requests == 1 && (close ? observed == null : ReferenceEquals(observed, cause)), "established point request precedes pure paint; callback Close skips paint entirely: close=" + close);
            if (!close)
            {
                Input(EventType.Repaint, 146, 34);
                widget.Draw(Bounds, ctx);
                Check(requests == 1, "pure point paint failure does not replay the request");
            }
            else
                Check(!session.Interactions.CaptureOwner.HasValue && GUIUtility.hotControl == 0, "callback Close ends point capture without normal traversal");
        }
    }

    private static void StaticEntry()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using var host = new UiHost("point", UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"point\"><Widget Id=\"static\" Kind=\"chart/line\" Points=\"0,1;1,0\" /></UiPage>"), new UiBindings(), UiTheme.Vanilla, new Metrics(), new Translation());
        Input(EventType.Repaint, 100, 60);
        host.DrawFrame(Bounds);
        Check(host.Session.TrippedNodes.Count == 0 && !host.Session.Interactions.CaptureOwner.HasValue, "static Points with an Id requires neither a value binding nor a keys registration");
    }

    private sealed class Metrics : ITextMetrics
    {
        public float MeasureWidth(string text, UiFont font) => text.Length * 8;
        public float MeasureText(string text, UiFont font, float width) => 16;
    }

    private sealed class Translation : IUiTranslation
    {
        public int TranslationRevision => 0;

        public string Translate(string key) => key;
    }
}
