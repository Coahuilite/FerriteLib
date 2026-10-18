using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;
internal static class KernelSharedEditingTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        Console.WriteLine("  ok: EDIT " + message);
    }

    public static int RunAll()
    {
        try
        {
            var standard = Trace(0);
            Check(standard.SequenceEqual(Trace(1)) && standard.SequenceEqual(Trace(2)), "standard, independent native and nonrendering editors share the full draft trajectory");
            MultilineAndReadOnly();
            Combination();
            FailureAndIsolation();
            CompositionAndHost();
            PublicationRecovery();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: EDIT " + ex);
            return 1;
        }
        finally
        {
            Reset();
        }
    }

    private static List<string> Trace(int route)
    {
        Reset();
        using var session = new UiSession();
        string model = "start";
        int requests = 0;
        var bindings = new UiBindings();
        void Set(string value)
        {
            model = value;
            requests++;
        }

        bindings.BindValue("text", () => model, Set);
        var ctx = new UiWidgetContext("edit", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), bindings, 200, "text");
        var widget = new TextFieldWidget();
        widget.Configure(new UiElementSpec("text", TextFieldWidget.Kind, new Dictionary<string, string> { ["Bind"] = "text" }));
        bool Parse(string text, out string value)
        {
            value = text;
            return true;
        }

        var headless = new UiEditSession<string>(value => value, Parse);
        UiEditSession<string> Editor() => route == 2 ? headless : UiNative.Editor<string>(ctx, "text", value => value, Parse);
        var trace = new List<string>();
        void Drive(string? text = null, bool focus = false, bool confirm = false, bool blur = false, bool cancel = false)
        {
            if (route == 2)
            {
                headless.Observe(model);
                if (focus)
                    headless.Focus();
                if (cancel)
                    headless.Cancel(model);
                else if (confirm || blur)
                    headless.Confirm(confirm, Set, () => model);
                if (text != null)
                    headless.Input(text, true, false, false, Set, () => model);
            }
            else
            {
                UiNative.DebugMousePositionEnabled = true;
                UiNative.DebugMousePosition = new Vector2(20, 14);
                UiNative.DebugMouseDown = focus;
                UiNative.DebugEnter = confirm;
                UiNative.DebugFocusLost = blur;
                UiNative.TextFieldOverride = (_, shown) => text ?? shown;
                if (cancel)
                    session.GetOrCreateValueState("text").DiscardRequested = true;
                if (route == 0)
                    widget.Draw(new Rect(0, 0, 200, 28), ctx);
                else
                    UiNative.Edit(new Rect(0, 0, 200, 28), ctx, "text", model, value => value, Parse, false, false, Set, () => model);
            }

            var editor = Editor();
            trace.Add($"{model}|{requests}|{editor.Draft}|{editor.Focused}|{editor.Modified}|{editor.Conflict}");
        }

        Drive("draft", focus: true);
        Check(requests == 0 && Editor().Modified, "candidate default keeps a focused draft");
        Drive();
        model = "outside";
        Drive();
        Check(Editor().Conflict && Editor().Draft == "draft", "observed external change retains the actual draft");
        Drive(blur: true);
        Check(requests == 0, "conflicting blur does not publish");
        Drive(focus: true);
        Drive(confirm: true);
        Check(model == "draft" && requests == 1, "explicit confirm publishes once and ends normally");
        Drive("later", focus: true);
        model = "latest";
        Drive(cancel: true);
        Check(model == "latest" && requests == 1 && Editor().Draft == "latest", "cancel follows latest model without rollback");
        return trace;
    }

    private static void MultilineAndReadOnly()
    {
        Reset();
        using var session = new UiSession();
        string model = "one";
        int requests = 0;
        var bindings = new UiBindings();
        bindings.BindValue("text", () => model, value =>
        {
            model = value;
            requests++;
        });
        var ctx = new UiWidgetContext("edit", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), bindings, 200, "text");
        var widget = new TextFieldWidget();
        widget.Configure(new UiElementSpec("text", TextFieldWidget.Kind, new Dictionary<string, string> { ["Bind"] = "text", ["Multiline"] = "true" }));
        UiNative.DebugMousePositionEnabled = true;
        UiNative.DebugMousePosition = new Vector2(20, 14);
        UiNative.DebugMouseDown = true;
        UiNative.TextFieldOverride = (_, _) => "one\ntwo";
        widget.Draw(new Rect(0, 0, 200, 80), ctx);
        UiNative.DebugMouseDown = false;
        UiNative.TextFieldOverride = (_, text) => text;
        Event.current = new Event
        {
            type = EventType.KeyDown,
            keyCode = KeyCode.Return
        };
        widget.Draw(new Rect(0, 0, 200, 80), ctx);
        Check(requests == 0 && session.GetOrCreateValueState("text").Focused, "multiline Return remains native newline, not confirmation");
        Event.current = new Event
        {
            type = EventType.KeyDown,
            keyCode = KeyCode.Return,
            control = true
        };
        widget.Draw(new Rect(0, 0, 200, 80), ctx);
        Check(requests == 1 && model == "one\ntwo", "multiline Ctrl+Return confirms one shared draft");
        var readOnly = new UiBindings();
        readOnly.BindReadOnly("text", () => model);
        ctx = new UiWidgetContext("edit", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), readOnly, 200, "text");
        UiNative.TextFieldOverride = (_, _) => "forbidden";
        UiNative.DebugMouseDown = true;
        Event.current = new Event
        {
            type = EventType.KeyDown,
            character = 'x'
        };
        widget.Draw(new Rect(0, 0, 200, 80), ctx);
        Check(Event.current.type == EventType.Used && session.GetOrCreateValueState("text").EditText == model, "readonly protects native mutation before the control and never adopts the changed string");
        Event.current = new Event
        {
            type = EventType.KeyDown,
            keyCode = (KeyCode)99,
            control = true
        };
        widget.Draw(new Rect(0, 0, 200, 80), ctx);
        Check(Event.current.type == EventType.KeyDown && requests == 1, "readonly native Copy is allowed without a request");
    }

    private static void Combination()
    {
        Reset();
        using var session = new UiSession();
        float model = 10;
        var requests = new List<float>();
        var bindings = new UiBindings();
        bindings.BindValue("value", () => model, value =>
        {
            requests.Add(value);
            model = value;
        });
        var ctx = new UiWidgetContext("edit", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), bindings, 400, "value");
        var widget = new StepperSliderWidget();
        widget.Configure(new UiElementSpec("value", StepperSliderWidget.Kind, new Dictionary<string, string> { ["Bind"] = "value", ["Min"] = "0", ["Max"] = "100", ["Step"] = "1" }));
        var numeric = new UiNumericEdit(0, 100);
        var editor = UiNative.Editor<float>(ctx, "value", numeric.Format, numeric.Parse);
        editor.Observe(model);
        editor.Focus();
        editor.Input("20", true, false, false, _ => throw new Exception("premature request"), () => model);
        UiNative.ButtonOverride = rect => rect.x > 370;
        widget.Draw(new Rect(0, 0, 400, 28), ctx);
        Check(requests.SequenceEqual(new[] { 21f }) && !editor.HasDraft, "real combination steps from draft once, not model or intermediate commit");
        editor.Focus();
        editor.Input("-", true, false, false, _ => throw new Exception("invalid request"), () => model);
        widget.Draw(new Rect(0, 0, 400, 28), ctx);
        Check(requests.Count == 1 && editor.Draft == "-", "real combination refuses invalid step without losing text");
        UiNative.ButtonOverride = null;
        UiNative.SliderOverride = (_, _, _, _) => 30;
        widget.Draw(new Rect(0, 0, 400, 28), ctx);
        Check(requests.SequenceEqual(new[] { 21f, 30f }) && !editor.HasDraft, "real range selection takes over invalid local draft");
    }

    private static void Reset()
    {
        UiNative.TextFieldOverride = null;
        UiNative.ButtonOverride = null;
        UiNative.SliderOverride = null;
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMouseDown = false;
        UiNative.DebugEnter = false;
        UiNative.DebugFocusLost = false;
        Event.current = null;
        typeof(Input).GetProperty("compositionString")!.SetValue(null, "");
    }

    private static void PublicationRecovery()
    {
        foreach (bool combination in new[]
        {
            false,
            true
        }

        )
        {
            Reset();
            using var session = new UiSession();
            float model = 10;
            var requests = new List<float>();
            bool fail = false;
            var bindings = new UiBindings();
            bindings.BindValue("value", () => model, value =>
            {
                requests.Add(value);
                if (fail)
                    throw new InvalidOperationException("before write");
                model = value;
            });
            var ctx = new UiWidgetContext("edit", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), bindings, 400, "value");
            IUiWidget widget = combination ? new StepperSliderWidget() : new NumberFieldWidget();
            var attributes = new Dictionary<string, string>
            {
                ["Bind"] = "value",
                ["Max"] = "100",
                ["Live"] = "true"
            };
            widget.Configure(new UiElementSpec("value", combination ? StepperSliderWidget.Kind : NumberFieldWidget.Kind, attributes));
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMousePosition = new Vector2(combination ? 330 : 20, 14);
            UiNative.DebugMouseDown = true;
            UiNative.TextFieldOverride = (_, _) => "20";
            widget.Draw(new Rect(0, 0, 400, 28), ctx);
            UiNative.DebugMouseDown = false;
            UiNative.TextFieldOverride = (_, _) => "10";
            widget.Draw(new Rect(0, 0, 400, 28), ctx);
            UiNative.TextFieldOverride = (_, text) => text;
            widget.Draw(new Rect(0, 0, 400, 28), ctx);
            Check(requests.SequenceEqual(new[] { 20f, 10f }) && model == 10 && session.GetOrCreateValueState("value").EditText == "10", "real Live wrapper publishes 10→20→10 without losing the return input: combination=" + combination);
            if (!combination)
                continue;
            attributes["Live"] = "false";
            widget.Configure(new UiElementSpec("value", StepperSliderWidget.Kind, attributes));
            UiNative.DebugMouseDown = true;
            UiNative.TextFieldOverride = (_, _) => "20";
            widget.Draw(new Rect(0, 0, 400, 28), ctx);
            UiNative.DebugMouseDown = false;
            UiNative.TextFieldOverride = (_, text) => text;
            UiNative.ButtonOverride = rect => rect.x > 370;
            fail = true;
            Exception? observed = null;
            try
            {
                widget.Draw(new Rect(0, 0, 400, 28), ctx);
            }
            catch (Exception ex)
            {
                observed = ex;
            }

            Check(observed?.InnerException != null && requests.Last() == 21 && model == 10, "real combination failed step attempted 21, not its held draft20");
            fail = false;
            UiNative.ButtonOverride = null;
            widget.Draw(new Rect(0, 0, 400, 28), ctx);
            Check(requests.Count == 3 && session.GetOrCreateValueState("value").EditText == "20", "failed combination does not automatically retry or discard the old draft");
            UiNative.DebugEnter = true;
            widget.Draw(new Rect(0, 0, 400, 28), ctx);
            Check(requests.SequenceEqual(new[] { 20f, 10f, 21f, 20f }) && model == 20, "a distinct explicit confirmation still publishes20 after the failed21");
        }
    }

    private static void CompositionAndHost()
    {
        Reset();
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        string model = "start";
        int requests = 0;
        var bindings = new UiBindings();
        bindings.BindValue("text", () => model, value =>
        {
            model = value;
            requests++;
        });
        using var host = new UiHost("edit", UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"edit\"><Widget Id=\"text\" Kind=\"input/text-field\" Live=\"true\" Height=\"28\" /></UiPage>"), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        var bounds = new Rect(0, 0, 200, 100);
        UiNative.DebugMousePositionEnabled = true;
        UiNative.DebugMousePosition = new Vector2(20, 14);
        UiNative.DebugMouseDown = true;
        UiNative.TextFieldOverride = (_, _) => "composing";
        typeof(Input).GetProperty("compositionString")!.SetValue(null, "composing");
        host.DrawFrame(bounds);
        Check(requests == 0, "native composition sample does not enter business publication even under Live=true");
        UiNative.DebugMouseDown = false;
        UiNative.TextFieldOverride = (_, text) => text;
        Event.current = new Event
        {
            type = EventType.KeyDown,
            keyCode = KeyCode.Return
        };
        Check(host.TryHandleAccept() && !host.Session.ActiveEditState!.CommitRequested, "real host reserves composition Return without requesting whole-edit confirmation");
        host.DrawFrame(bounds);
        Check(requests == 0 && host.Session.ActiveEditState != null, "composition Return does not also end the shared edit");
        Event.current = new Event
        {
            type = EventType.KeyDown,
            keyCode = KeyCode.Escape
        };
        Check(host.TryHandleCancel() && !host.Session.ActiveEditState!.DiscardRequested, "real host reserves composition Escape without discarding the whole draft");
        host.DrawFrame(bounds);
        Check(Event.current.type == EventType.Used && requests == 0, "native composition cancel cannot fall through into the late window-close check");
        typeof(Input).GetProperty("compositionString")!.SetValue(null, "");
        Event.current = new Event
        {
            type = EventType.Repaint
        };
        host.DrawFrame(bounds);
        Check(model == "composing" && requests == 1, "composition completion publishes its qualified Live candidate once");
        Event.current = new Event
        {
            type = EventType.KeyDown,
            keyCode = KeyCode.Return
        };
        host.TryHandleAccept();
        host.DrawFrame(bounds);
        Check(requests == 1 && host.Session.ActiveEditState == null, "host confirmation after Live publication does not send the same candidate again");
    }

    private static void FailureAndIsolation()
    {
        Reset();
        using var session = new UiSession();
        float model = 10;
        int requests = 0;
        bool failSet = false;
        var cause = new InvalidOperationException("partial write");
        var bindings = new UiBindings();
        bindings.BindValue("value", () => model, value =>
        {
            requests++;
            model = value;
            if (failSet)
                throw cause;
        });
        var numeric = new UiNumericEdit(0, 100);
        UiNode Node(string id) => session.GetOrCreateNode(UiNodeId.Root(new UiElementSpec(id, NumberFieldWidget.Kind, new Dictionary<string, string>()), 0), NumberFieldWidget.Kind, 0);
        UiNode first = Node("first");
        UiNode second = Node("second");
        UiWidgetContext Context(UiNode node) => new("edit", session, new StubMetrics(), UiTheme.Vanilla, new StubTranslation(), bindings, 200, node.Id.Key, node: node);
        void Scoped(UiNode node, Action<UiWidgetContext> action)
        {
            UiNode previous = session.EnterNode(node);
            try
            {
                action(Context(node));
            }
            finally
            {
                session.EnterNode(previous);
            }
        }

        Scoped(second, ctx =>
        {
            var editor = UiNative.Editor<float>(ctx, "value", numeric.Format, numeric.Parse);
            editor.Observe(model);
            editor.Input("45", true, false, false, _ => throw new Exception("unexpected request"), () => model);
        });
        Scoped(first, ctx =>
        {
            var widget = new NumberFieldWidget();
            widget.Configure(new UiElementSpec("first", NumberFieldWidget.Kind, new Dictionary<string, string> { ["Bind"] = "value", ["Max"] = "100" }));
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMouseDown = true;
            UiNative.DebugMousePosition = new Vector2(20, 14);
            UiNative.TextFieldOverride = (_, _) => "20";
            widget.Draw(new Rect(0, 0, 200, 28), ctx);
            UiNative.DebugMouseDown = false;
            UiNative.DebugEnter = true;
            var paintCause = new InvalidOperationException("native paint failed");
            UiNative.TextFieldOverride = (_, _) => throw paintCause;
            Exception? observed = null;
            try
            {
                widget.Draw(new Rect(0, 0, 200, 28), ctx);
            }
            catch (Exception ex)
            {
                observed = ex;
            }

            Check(ReferenceEquals(observed, paintCause) && requests == 1 && model == 20, "established confirmation is dispatched before a pure native rendering failure");
            UiNative.DebugEnter = false;
            UiNative.TextFieldOverride = (_, text) => text;
            widget.Draw(new Rect(0, 0, 200, 28), ctx);
            Check(requests == 1, "paint failure does not replay the established request");
            UiNative.DebugMouseDown = true;
            UiNative.TextFieldOverride = (_, _) => "30";
            widget.Draw(new Rect(0, 0, 200, 28), ctx);
            UiNative.DebugMouseDown = false;
            UiNative.DebugEnter = true;
            UiNative.TextFieldOverride = (_, text) => text;
            failSet = true;
            observed = null;
            try
            {
                widget.Draw(new Rect(0, 0, 200, 28), ctx);
            }
            catch (Exception ex)
            {
                observed = ex;
            }

            Check(ReferenceEquals(observed?.InnerException, cause) && requests == 2 && model == 30, "partial setter side effects retain original cause and target without rollback");
            UiNative.DebugEnter = false;
            failSet = false;
            widget.Draw(new Rect(0, 0, 200, 28), ctx);
            Check(requests == 2, "setter failure never creates an automatic retry");
            first.InteractionScope = "replacement";
            widget.Draw(new Rect(0, 0, 200, 28), ctx);
            Check(!UiNative.Editor<float>(ctx, "value", numeric.Format, numeric.Parse).HasDraft, "real business retarget drops the old editor without publication");
        });
        Scoped(second, ctx =>
        {
            var editor = UiNative.Editor<float>(ctx, "value", numeric.Format, numeric.Parse);
            editor.Observe(model);
            Check(editor.Draft == "45" && editor.Conflict, "same Bind does not grant another instance takeover rights");
        });
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureWidth(string text, UiFont font) => text.Length * 8;
        public float MeasureText(string text, UiFont font, float width) => 16;
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public int TranslationRevision => 0;

        public string Translate(string key) => key;
    }
}
