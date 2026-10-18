using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;
internal static class KernelBooleanSelectionExpansionTests
{
    private const string Scope = "bse";
    private static readonly Rect View = new(20, 30, 420, 400);
    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception(description);
        Console.WriteLine("  ok: BSE " + description);
    }

    private static void Pump(UiHost host, EventType phase, Vector2 point, bool consumed = false)
    {
        var input = new Event
        {
            type = phase,
            button = 0,
            mousePosition = point
        };
        if (consumed)
            input.Use();
        Event.current = input;
        host.DrawFrame(View);
    }

    private static Vector2 Centre(Rect rect) => new(rect.x + rect.width / 2, rect.y + rect.height / 2);
    private static Rect RectOf(UiHost host, string id)
    {
        UiNode node = host.Session.GetNodeByElementId(id)!;
        Rect rect = node.Rect!.Value;
        return new Rect(rect.x + View.x, rect.y + View.y, rect.width, rect.height);
    }

    private static UiHost Host(string content, UiBindings bindings)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Column Padding=\"0\" Gap=\"2\">" + content + "</Column></UiPage>");
        var host = new UiHost(Scope, manifest, bindings, UiTheme.Vanilla, new Metrics(), new Translation());
        Pump(host, EventType.Repaint, new Vector2(-100, -100));
        return host;
    }

    public static int RunAll()
    {
        try
        {
            UiNative.ButtonOverride = null;
            UiNative.DebugMousePositionEnabled = false;
            UiNative.Trace = null;
            SharedBindingPopupOwners();
            PressedPresentationFacts();
            CheckboxRoutes();
            SelectionExpansionRoutes();
            ModeRowIdentity();
            DropdownProtocol();
            TreeIdentity();
            TreePartsAndRemoval();
            CallbackCloseAndReadonly();
            BindingRetargets();
            FailureBoundary();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: BSE " + ex);
            return 1;
        }
        finally
        {
            Event.current = null;
            GUIUtility.hotControl = 0;
            UiNative.ButtonOverride = null;
            UiNative.DebugMousePositionEnabled = false;
            UiNative.Trace = null;
        }
    }

    private static void CheckboxRoutes()
    {
        var traces = new List<string>();
        for (int route = 0; route < 4; route++)
        {
            bool model = false;
            int writes = 0;
            int commands = 0;
            using var pure = new UiInteractionSession();
            var bindings = new UiBindings();
            bindings.BindValue("flag", () => model, value =>
            {
                model = value;
                writes++;
            });
            bindings.BindCommand("after", () =>
            {
                Check(model, "Checkbox command follows Set");
                commands++;
            });
            string look = route == 1 ? "checkbox" : "switch";
            using var host = Host("<Widget Id=\"flag\" Kind=\"input/checkbox\" Bind=\"flag\" ActionBind=\"after\" Appearance=\"" + look + "\" />", bindings);
            Rect hit = RectOf(host, "flag");
            Vector2 point = Centre(hit);
            var trace = new List<string>();
            foreach (EventType phase in new[]
            {
                EventType.MouseDown,
                EventType.Repaint,
                EventType.MouseUp,
                EventType.Repaint
            }

            )
            {
                if (route < 2)
                    Pump(host, phase, point);
                else if (route == 2)
                {
                    Event.current = new Event
                    {
                        type = phase,
                        button = 0,
                        mousePosition = point
                    };
                    var ctx = new UiWidgetContext(Scope, host.Session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, View.width, "custom-check");
                    var target = UiNative.InteractionTarget(ctx, "flag");
                    UiValueBehavior.Request(target, UiValueBehavior.Boolean(UiNative.Activation(hit, ctx, "flag"), !model), value =>
                    {
                        model = value;
                        writes++;
                        commands++;
                    });
                    UiThemeDraw.Solid(new Rect(hit.x, hit.y, 3, hit.height), ctx.Theme.AccentGold);
                }
                else
                {
                    var target = new UiInteractionTarget("flag", "flag");
                    UiPointerPhase input = phase == EventType.MouseDown ? UiPointerPhase.Press : phase == EventType.MouseUp ? UiPointerPhase.Release : UiPointerPhase.None;
                    UiInteractionResult interaction = pure.PointerActivation(target, new UiPointerSample(input, point.x, point.y), new UiHitGeometry(hit.x, hit.y, hit.width, hit.height), new UiInputEligibility(true));
                    UiValueBehavior.Request(target, UiValueBehavior.Boolean(interaction, !model), value =>
                    {
                        model = value;
                        writes++;
                        commands++;
                    });
                }

                bool captured = route < 3 ? host.Session.Interactions.CaptureOwner.HasValue : pure.CaptureOwner.HasValue;
                trace.Add(writes + ":" + model + ":" + captured);
            }

            Check(writes == 1 && commands == 1, "boolean route " + route + " requests exactly on valid release");
            traces.Add(string.Join("|", trace));
            Console.WriteLine("  BSE TRACE Boolean " + route + " " + traces.Last());
        }

        Check(traces.All(trace => trace == traces[0]), "both Checkbox looks / independent presentation / headless share trajectories");
    }

    private static void SharedBindingPopupOwners()
    {
        string current = "a";
        int writes = 0;
        var bindings = new UiBindings();
        bindings.BindValue("shared", () => current, value =>
        {
            current = value;
            writes++;
        });
        bindings.BindOptions("items", () => new[] { "a", "b" });
        using var host = Host("<Widget Id=\"first\" Kind=\"input/dropdown\" Bind=\"shared\" OptionsBind=\"items\" /><Widget Id=\"second\" Kind=\"input/dropdown\" Bind=\"shared\" OptionsBind=\"items\" /><Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"shared\" Value1=\"a\" Value2=\"b\" />", bindings);
        Vector2 first = Centre(RectOf(host, "first"));
        Pump(host, EventType.MouseDown, first);
        Pump(host, EventType.MouseUp, first);
        Check(host.Session.IsPopupOpen("shared") && host.Session.OpenPopupAnchor?.y == RectOf(host, "first").y, "two real Dropdown instances sharing Bind do not close or reanchor the first menu during sibling traversal");
        Pump(host, EventType.Repaint, first);
        Check(host.Session.IsPopupOpen("shared"), "same-Bind sibling repaint preserves actual popup instance");
        Rect anchor = host.Session.OpenPopupAnchor!.Value;
        Vector2 option = new(anchor.x + 10, anchor.yMax + UiPopup.OptionHeight * 1.5f);
        Pump(host, EventType.MouseDown, option);
        Pump(host, EventType.MouseUp, option);
        Check(current == "b" && writes == 1 && host.Session.OpenPopupId == null, "same-Bind first popup selects once through its own option target");
        Vector2 second = Centre(RectOf(host, "second"));
        Pump(host, EventType.MouseDown, second);
        Pump(host, EventType.MouseUp, second);
        Check(host.Session.IsPopupOpen("shared") && host.Session.OpenPopupAnchor?.y == RectOf(host, "second").y,
            "same-Bind second popup keeps its own anchor while the earlier sibling draws");
        Pump(host, EventType.Repaint, second);
        host.Session.ClosePopup();
        Rect modeRect = RectOf(host, "mode");
        Vector2 mode = new(modeRect.x + 15, modeRect.y + modeRect.height / 2);
        Pump(host, EventType.MouseDown, mode);
        UiInteractionTarget? held = host.Session.Interactions.CaptureOwner;
        Check(held?.Node == host.Session.GetNodeByElementId("mode")!.Id.Key, "real ModeRow starts a same-Bind non-popup capture");
        host.Session.OpenPopup("shared", RectOf(host, "first"));
        var modeNode = host.Session.GetNodeByElementId("mode")!;
        var composite = new UiWidgetContext(Scope, host.Session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, View.width, "same-node-composite", node: modeNode);
        Event.current = new Event
        {
            type = EventType.Repaint,
            mousePosition = mode
        };
        UiNative.PopupOption(modeRect, "shared", composite, "a");
        host.Session.ClosePopup();
        Check(host.Session.Interactions.CaptureOwner?.Equals(held!.Value) == true && host.Session.OwnedHotControl.HasValue, "closing a raw same-Bind popup does not release ModeRow capture by prefix inference");
        Pump(host, EventType.MouseUp, mode);
    }

    private static IList Recorded(string field) => (IList)typeof(Verse.Widgets).GetField(field)!.GetValue(null)!;
    private static bool SameColor(Color left, Color right) => Math.Abs(left.r - right.r) < 0.001f && Math.Abs(left.g - right.g) < 0.001f && Math.Abs(left.b - right.b) < 0.001f && Math.Abs(left.a - right.a) < 0.001f;
    private static void PressedPresentationFacts()
    {
        foreach (string kind in new[]
        {
            "switch",
            "checkbox",
            "tree"
        }

        )
            foreach (string eligibility in new[]
            {
                "eligible",
                "disabled",
                "readonly",
                "covered",
                "window",
                "consumed"
            }

            )
            {
                // Tree row operations are commands, not writable value bindings.
                if (kind == "tree" && eligibility == "readonly")
                    continue;
                using var session = new UiSession();
                var theme = UiTheme.Vanilla.Clone();
                theme.TextOnGold = new Color(0.93f, 0.14f, 0.27f, 1);
                var bindings = new UiBindings();
                int requests = 0;
                if (eligibility == "readonly")
                    bindings.BindReadOnly("flag", () => false);
                else
                    bindings.BindValue("flag", () => false, _ => requests++);
                bindings.BindReadOnly<IReadOnlyList<UiTreeRow>>("rows", () => new[] { new UiTreeRow("row", 0, "Row", expandable: true, checkable: true) });
                bindings.BindAction<string>("row", _ => requests++);
                string widgetKind = kind == "tree" ? "container/tree" : "input/checkbox";
                var spec = new UiElementSpec("visual", widgetKind, new Dictionary<string, string> { ["Bind"] = kind == "tree" ? "rows" : "flag", ["Label"] = "Flag", ["Appearance"] = kind, ["ActionBind"] = kind == "tree" ? "row" : "", ["RowHeight"] = "28" });
                var node = session.GetOrCreateNode(UiNodeId.Root(spec, 0), widgetKind, 0);
                node.IsDisabled = eligibility == "disabled";
                session.PointerInputAllowed = eligibility != "window";
                var ctx = new UiWidgetContext(Scope, session, new Metrics(), theme, new Translation(), bindings, 200, "visual", node: node);
                IUiWidget widget = kind == "tree" ? new TreeWidget() : new CheckboxWidget();
                widget.Configure(spec);
                var rect = new Rect(0, 0, 200, 28);
                if (eligibility == "covered")
                {
                    session.BeginHitPass();
                    session.PushHitLayer(node, rect, isPopup: true);
                    session.BeginHitPass();
                }

                bool PaintedPressed()
                {
                    if (kind != "tree")
                    {
                        IList colors = Recorded("LabelColors");
                        return colors.Count > 0 && SameColor((Color)colors[colors.Count - 1]!, theme.Styles.Resolve(UiStatusTone.Active).Text);
                    }

                    IList rects = Recorded("DrawBoxSolidRects");
                    IList fills = Recorded("DrawBoxSolidColors");
                    for (int i = 0; i < rects.Count; i++)
                    {
                        Rect painted = (Rect)rects[i]!;
                        if (painted.x == rect.x && painted.y == rect.y && painted.width == rect.width && painted.height == rect.height && SameColor((Color)fills[i]!, theme.SelectedSurface.Fill))
                            return true;
                    }

                    return false;
                }

                void Draw(EventType phase, bool used = false)
                {
                    Recorded("LabelColors").Clear();
                    Recorded("DrawBoxSolidRects").Clear();
                    Recorded("DrawBoxSolidColors").Clear();
                    Event.current = new Event
                    {
                        type = phase,
                        button = 0,
                        mousePosition = new Vector2(180, 14)
                    };
                    if (used)
                        Event.current.Use();
                    widget.Draw(rect, ctx);
                }

                bool eligible = eligibility == "eligible";
                Draw(EventType.MouseDown, eligibility == "consumed");
                Check(PaintedPressed() == eligible && session.Interactions.CaptureOwner.HasValue == eligible, kind + " " + eligibility + " pressed paint matches shared capture, not raw down");
                Draw(EventType.Repaint);
                Check(PaintedPressed() == eligible, kind + " " + eligibility + " repaint uses held owner facts");
                if (eligible)
                {
                    node.IsDisabled = true;
                    Draw(EventType.Repaint);
                    Check(!PaintedPressed() && session.Interactions.CaptureOwner == null && requests == 0, kind + " invalidated held owner stops pressed paint without request");
                }
            }
    }

    private static void ModeRowIdentity()
    {
        string selected = "b";
        int requests = 0;
        var bindings = new UiBindings();
        bindings.BindValue("mode", () => selected, value =>
        {
            selected = value;
            requests++;
        });
        using var host = Host("<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Value1=\"a\" Value2=\"b\" />", bindings);
        Rect rect = RectOf(host, "mode");
        Vector2 point = new(rect.x + rect.width * 0.75f, rect.y + rect.height / 2);
        Pump(host, EventType.MouseDown, point);
        Check(host.Session.Interactions.CaptureOwner?.ItemKey as string == "b", "mode-row second item captures its value identity");
        Pump(host, EventType.Repaint, point);
        Check(requests == 0 && host.Session.Interactions.CaptureOwner.HasValue, "earlier mode option does not cancel held owner");
        Pump(host, EventType.MouseUp, point);
        Check(requests == 1 && selected == "b", "explicit same-value selection still requests once");
        Pump(host, EventType.Repaint, point);
        Check(requests == 1, "selection repaint does not request");
        selected = "missing";
        Pump(host, EventType.Repaint, point);
        Check(host.Session.GetNodeByElementId("mode")!.Selection?.Missing == true && requests == 1, "missing selection is observed without model repair");
    }

    private static void SelectionExpansionRoutes()
    {
        var trajectories = new List<string>();
        for (int route = 0; route < 3; route++)
        {
            string selected = "a";
            int writes = 0;
            bool expanded = false;
            var bindings = new UiBindings();
            bindings.BindValue("pick", () => selected, value =>
            {
                selected = value;
                writes++;
            });
            bindings.BindOptions("items", () => new[] { "a", "b" });
            using var host = Host("<Widget Id=\"pick\" Kind=\"input/dropdown\" OptionsBind=\"items\" />", bindings);
            using var neutral = new UiInteractionSession();
            var ctx = new UiWidgetContext(Scope, host.Session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, View.width, "independent");
            Rect trigger = RectOf(host, "pick");
            Rect menu = new(trigger.x, trigger.yMax, trigger.width, UiPopup.OptionHeight * 2);
            var trace = new List<string>();
            void Drive(EventType phase, bool option)
            {
                Vector2 point = option ? new Vector2(menu.x + 10, menu.y + UiPopup.OptionHeight * 1.5f) : Centre(trigger);
                if (route == 0)
                    Pump(host, phase, point);
                else if (route == 1)
                {
                    Event.current = new Event
                    {
                        type = phase,
                        button = 0,
                        mousePosition = point
                    };
                    UiNative.DropdownButton(trigger, "pick", ctx);
                    if (host.Session.IsPopupOpen("pick"))
                    {
                        UiPopup.DrawOptionList(menu, "pick", ctx, new[] { new KeyValuePair<string, string>("A", "a"), new KeyValuePair<string, string>("B", "b") }, selected, value =>
                        {
                            selected = value;
                            writes++;
                        });
                    }
                }
                else
                {
                    UiPointerPhase sample = phase == EventType.MouseDown ? UiPointerPhase.Press : phase == EventType.MouseUp ? UiPointerPhase.Release : UiPointerPhase.None;
                    var target = new UiInteractionTarget("headless", "pick", "trigger", null, "model");
                    var input = new UiPointerSample(sample, point.x, point.y);
                    var interaction = neutral.PointerActivation(target, input, new UiHitGeometry(trigger.x, trigger.y, trigger.width, trigger.height), new(true));
                    UiValueBehavior.Request(target, UiValueBehavior.Expansion(interaction, !expanded, true), value => expanded = value);
                    if (expanded)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            string value = i == 0 ? "a" : "b";
                            target = new UiInteractionTarget("headless", "pick", "popup-option", value, "model");
                            interaction = neutral.PointerActivation(target, input, new UiHitGeometry(menu.x, menu.y + i * UiPopup.OptionHeight, menu.width, UiPopup.OptionHeight), new(true));
                            UiValueBehavior.Request(target, UiValueBehavior.Selection(interaction, value), candidate =>
                            {
                                expanded = false;
                                selected = candidate;
                                writes++;
                            });
                        }
                    }
                }

                if (route < 2)
                    expanded = host.Session.IsPopupOpen("pick");
                UiInteractionTarget? owner = route < 2 ? host.Session.Interactions.CaptureOwner : neutral.CaptureOwner;
                trace.Add(writes + ":" + selected + ":" + expanded + ":" + owner?.Part + ":" + (owner?.Part == "popup-option" ? owner?.ItemKey : owner.HasValue ? "trigger" : null));
            }

            foreach (bool option in new[]
            {
                false,
                true,
                false,
                true
            }

            )
            {
                Drive(EventType.MouseDown, option);
                Drive(EventType.Repaint, option);
                Drive(EventType.MouseUp, option);
                Drive(EventType.Repaint, option);
            }

            Check(writes == 2 && selected == "b" && !expanded, "selection/expansion route " + route + " handles repeated explicit selection");
            trajectories.Add(string.Join("|", trace));
            Console.WriteLine("  BSE TRACE SelectionExpansion " + route + " " + trajectories.Last());
        }

        Check(trajectories.All(trace => trace == trajectories[0]), "default Dropdown / independent popup presentation / headless share selection and expansion trajectories");
    }

    private static void DropdownProtocol()
    {
        string selected = "a";
        int writes = 0;
        int underlying = 0;
        var bindings = new UiBindings();
        bindings.BindValue("choice", () => selected, value =>
        {
            selected = value;
            writes++;
        });
        bindings.BindOptions("choices", () => new[] { "a", "b", "c" });
        bindings.BindCommand("under", () => underlying++);
        using var host = Host("<Widget Id=\"choice\" Kind=\"input/dropdown\" Bind=\"choice\" OptionsBind=\"choices\" /><Widget Id=\"under\" Kind=\"input/button\" ActionBind=\"under\" Height=\"28\" /><Widget Id=\"tail\" Kind=\"input/button\" ActionBind=\"under\" Height=\"140\" />", bindings);
        Vector2 trigger = Centre(RectOf(host, "choice"));
        Pump(host, EventType.MouseDown, trigger);
        Check(host.Session.OpenPopupId == null, "dropdown press only arms trigger");
        Pump(host, EventType.MouseUp, trigger);
        Check(host.Session.IsPopupOpen("choice"), "dropdown release opens expansion");
        Rect anchor = host.Session.OpenPopupAnchor!.Value;
        Vector2 option = new(anchor.x + 10, anchor.yMax + UiPopup.OptionHeight * 1.5f);
        Pump(host, EventType.MouseDown, option);
        Pump(host, EventType.Repaint, option);
        Check(host.Session.Interactions.CaptureOwner?.ItemKey as string == "b", "later popup option survives earlier option visits");
        Pump(host, EventType.MouseUp, option, true);
        Check(writes == 0 && host.Session.OwnedHotControl == null && host.Session.OpenPopupId != null, "Used raw MouseUp releases only owned option without selecting");
        Pump(host, EventType.MouseDown, option);
        Pump(host, EventType.MouseUp, option);
        Check(selected == "b" && writes == 1 && underlying == 0 && host.Session.OpenPopupId == null, "popup release selects once without covered button command");
        Pump(host, EventType.MouseDown, trigger);
        Pump(host, EventType.MouseUp, trigger);
        Vector2 outside = Centre(RectOf(host, "tail"));
        // The menu is three rows high; move the tail command below that menu.
        outside = new Vector2(outside.x, anchor.yMax + 110);
        Check(RectOf(host, "tail").Contains(outside), "F3 outside point is over a real underlying business button");
        Pump(host, EventType.MouseDown, outside);
        Check(Event.current.type == EventType.Used && host.Session.OpenPopupId == null, "F3 real Host route consumes outside primary press and closes menu");
        Pump(host, EventType.MouseUp, outside);
        Check(underlying == 0 && writes == 1 && host.Session.OwnedHotControl == null, "F3 later release does not replay underlying or selection requests");
    }

    private static void TreeIdentity()
    {
        var calls = new List<string>();
        IReadOnlyList<UiTreeRow> rows = new[]
        {
            new UiTreeRow("a", 0, "A"),
            new UiTreeRow("b", 0, "B")
        };
        var bindings = new UiBindings();
        bindings.BindValue("rows", () => rows, value => rows = value);
        bindings.BindAction<string>("row", key => calls.Add(key));
        using var host = Host("<Widget Id=\"tree\" Kind=\"container/tree\" Bind=\"rows\" ActionBind=\"row\" RowHeight=\"28\" />", bindings);
        Rect tree = RectOf(host, "tree");
        Vector2 second = new(tree.x + tree.width - 20, tree.y + 42);
        Pump(host, EventType.MouseDown, second);
        Check(host.Session.Interactions.CaptureOwner?.ItemKey as string == "b", "Tree body uses stable row key");
        rows = new[]
        {
            rows[1],
            rows[0]
        };
        host.Session.BumpContentRevision();
        Pump(host, EventType.Repaint, second);
        Vector2 first = new(second.x, tree.y + 14);
        Pump(host, EventType.MouseUp, first);
        Check(calls.SequenceEqual(new[] { "b" }), "Tree reorder releases to same stable row, not old index");
        Pump(host, EventType.MouseDown, first);
        host.Session.GetNodeByElementId("tree")!.InteractionScope = "replacement";
        Pump(host, EventType.MouseUp, first);
        Check(calls.Count == 1 && host.Session.OwnedHotControl == null, "I2 actual business retarget ends old Tree gesture");
    }

    private static void FailureBoundary()
    {
        bool model = false;
        int requests = 0;
        using var session = new UiInteractionSession();
        var target = new UiInteractionTarget("check", "model-A");
        var operation = UiValueBehavior.Boolean(session.Activate(target, new UiInputEligibility(true)), true);
        try
        {
            UiValueBehavior.Request(target, operation, value =>
            {
                model = value;
                requests++;
                throw new ApplicationException("after side effect");
            });
            throw new Exception("missing failure");
        }
        catch (InvalidOperationException ex)
        {
            Check(model && requests == 1 && ex.InnerException is ApplicationException && ex.Message.Contains("model-A"), "partial boolean side effect retained with original error and target, no retry");
        }

        Check(!UiValueBehavior.Selection(session.Activate(target, new UiInputEligibility(false)), "b").Request, "disabled selection cannot request");
        Check(!UiValueBehavior.Expansion(session.Activate(target, new UiInputEligibility(true)), true, false).Request, "no-content expansion is ineligible");
        using var native = new UiSession();
        var bindings = new UiBindings();
        int commands = 0;
        bindings.BindValue("flag", () => model, value =>
        {
            model = value;
            requests++;
            throw new ApplicationException("native partial write");
        });
        bindings.BindCommand("after", () => commands++);
        var ctx = new UiWidgetContext(Scope, native, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, 100, "checkbox");
        var checkbox = new CheckboxWidget();
        checkbox.Configure(new UiElementSpec("flag", "input/checkbox", new Dictionary<string, string> { ["Bind"] = "flag", ["ActionBind"] = "after" }));
        var rect = new Rect(0, 0, 100, 28);
        Event.current = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(50, 14)
        };
        checkbox.Draw(rect, ctx);
        Event.current = new Event
        {
            type = EventType.MouseUp,
            button = 0,
            mousePosition = new Vector2(50, 14)
        };
        try
        {
            checkbox.Draw(rect, ctx);
            throw new Exception("native checkbox failed to expose setter failure");
        }
        catch (InvalidOperationException ex)
        {
            Check(!model && requests == 2 && commands == 0 && ex.InnerException is ApplicationException && native.OwnedHotControl == null, "real Checkbox partial Set failure releases capture, retains effect and does not Invoke or retry");
        }
    }

    private static void CallbackCloseAndReadonly()
    {
        var bindings = new UiBindings();
        bindings.BindReadOnly("flag", () => false);
        using (var host = Host("<Widget Id=\"flag\" Kind=\"input/checkbox\" />", bindings))
        {
            Vector2 point = Centre(RectOf(host, "flag"));
            Pump(host, EventType.MouseDown, point);
            Pump(host, EventType.MouseUp, point);
            Check(host.Session.OwnedHotControl == null && host.Session.Interactions.CaptureOwner == null, "read-only real Checkbox never captures or requests");
        }

        UiHost? closing = null;
        int selected = 0;
        var choices = new UiBindings();
        choices.BindValue("pick", () => "a", value =>
        {
            selected++;
            closing!.Close();
        });
        choices.BindOptions("items", () => new[] { "a", "b", "c" });
        closing = Host("<Widget Id=\"pick\" Kind=\"input/dropdown\" OptionsBind=\"items\" />", choices);
        using (closing)
        {
            Vector2 trigger = Centre(RectOf(closing, "pick"));
            Pump(closing, EventType.MouseDown, trigger);
            Pump(closing, EventType.MouseUp, trigger);
            Rect anchor = closing.Session.OpenPopupAnchor!.Value;
            Vector2 first = new(anchor.x + 10, anchor.yMax + 10);
            Pump(closing, EventType.MouseDown, first);
            Pump(closing, EventType.MouseUp, first);
            Check(selected == 1 && !closing.Session.IsActive && GUIUtility.hotControl == 0, "selection callback close stops popup traversal and releases session");
        }
    }

    private static void BindingRetargets()
    {
        foreach (string kind in new[]
        {
            "input/button",
            "input/slider",
            "input/checkbox",
            "input/mode-row",
            "input/dropdown"
        }

        )
        {
            using var session = new UiSession();
            int a = 0;
            int b = 0;
            var bindings = new UiBindings();
            bindings.BindCommand("action-a", () => a++);
            bindings.BindCommand("action-b", () => b++);
            bool boolean = kind == "input/checkbox";
            bool number = kind == "input/slider";
            if (boolean)
            {
                bindings.BindValue("a", () => false, _ => a++);
                bindings.BindValue("b", () => false, _ => b++);
            }
            else if (number)
            {
                bindings.BindValue("a", () => 0f, _ => a++);
                bindings.BindValue("b", () => 0f, _ => b++);
            }
            else
            {
                bindings.BindValue("a", () => "v", _ => a++);
                bindings.BindValue("b", () => "v", _ => b++);
            }

            IUiWidget widget = kind switch
            {
                "input/button" => new ButtonWidget(),
                "input/slider" => new SliderWidget(),
                "input/checkbox" => new CheckboxWidget(),
                "input/mode-row" => new InputModeRowWidget(),
                _ => new DropdownWidget()};
            UiElementSpec Spec(string binding) => new("same-node", kind, new Dictionary<string, string> { ["Bind"] = binding, ["ActionBind"] = "action-" + binding, ["Value1"] = "v", ["Option1"] = "V", ["Max"] = "10" });
            var ctx = new UiWidgetContext(Scope, session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, 200, "same-node");
            var rect = new Rect(0, 0, 200, 28);
            widget.Configure(Spec("a"));
            Event.current = new Event
            {
                type = EventType.MouseDown,
                button = 0,
                mousePosition = new Vector2(100, 14)
            };
            widget.Draw(rect, ctx);
            Check(session.Interactions.CaptureOwner.HasValue, kind + " established its original binding capture");
            int before = a;
            widget.Configure(Spec("b"));
            Event.current = new Event
            {
                type = EventType.MouseUp,
                button = 0,
                mousePosition = new Vector2(100, 14)
            };
            widget.Draw(rect, ctx);
            Check(a == before && b == 0 && session.OwnedHotControl == null && session.Interactions.LastEnd == UiInteractionEnd.TargetChanged && session.OpenPopupId == null, kind + " same logical part binding replacement ends immediately without old/new target request");
        }

        foreach (string attribute in new[]
        {
            "ActionBind",
            "DisclosureBind",
            "CheckBind"
        }

        )
        {
            var ends = new List<string>();
            UiNative.Trace = ends.Add;
            using var session = new UiSession();
            int calls = 0;
            var bindings = new UiBindings();
            bindings.BindReadOnly<IReadOnlyList<UiTreeRow>>("rows", () => new[] { new UiTreeRow("row", 0, "Row", expandable: true, checkable: true) });
            bindings.BindAction<string>("a", _ => calls++);
            bindings.BindAction<string>("b", _ => calls++);
            var widget = new TreeWidget();
            UiElementSpec Spec(string target) => new("tree", "container/tree", new Dictionary<string, string> { ["Bind"] = "rows", [attribute] = target, ["RowHeight"] = "28" });
            var ctx = new UiWidgetContext(Scope, session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, 200, "tree");
            float pad = UiTheme.Vanilla.Geometry.Padding;
            float side = Math.Max(6, 28 - pad * 2);
            float x = attribute == "ActionBind" ? 180 : pad + side / 2 + (attribute == "CheckBind" ? side + pad : 0);
            widget.Configure(Spec("a"));
            Event.current = new Event
            {
                type = EventType.MouseDown,
                button = 0,
                mousePosition = new Vector2(x, 14)
            };
            widget.Draw(new Rect(0, 0, 200, 28), ctx);
            Check(session.Interactions.CaptureOwner.HasValue, "Tree " + attribute + " starts its own gesture");
            widget.Configure(Spec("b"));
            Event.current = new Event
            {
                type = EventType.MouseUp,
                button = 0,
                mousePosition = new Vector2(x, 14)
            };
            widget.Draw(new Rect(0, 0, 200, 28), ctx);
            Check(calls == 0 && session.OwnedHotControl == null && ends.Any(line => line.Contains("reason=TargetChanged")), "Tree " + attribute + " replacement ends the same part without transferring the gesture");
        }

        UiNative.Trace = null;
    }

    private static void TreePartsAndRemoval()
    {
        var calls = new List<string>();
        IReadOnlyList<UiTreeRow> rows = new[]
        {
            new UiTreeRow("stable", 0, "Stable", expandable: true, checkable: true)
        };
        var bindings = new UiBindings();
        bindings.BindReadOnly("rows", () => rows);
        bindings.BindAction<string>("row", key => calls.Add("body:" + key));
        bindings.BindAction<string>("expand", key => calls.Add("expand:" + key));
        bindings.BindAction<string>("check", key => calls.Add("check:" + key));
        using var host = Host("<Widget Id=\"tree\" Kind=\"container/tree\" Bind=\"rows\" ActionBind=\"row\" DisclosureBind=\"expand\" CheckBind=\"check\" RowHeight=\"28\" />", bindings);
        Rect band = RectOf(host, "tree");
        float pad = UiTheme.Vanilla.Geometry.Padding;
        float side = Math.Max(6, 28 - pad * 2);
        Vector2 marker = new(band.x + pad + side / 2, band.y + 14);
        Vector2 check = new(marker.x + side + pad, marker.y);
        foreach (Vector2 point in new[]
        {
            marker,
            check
        }

        )
        {
            int before = calls.Count;
            Pump(host, EventType.MouseDown, point);
            Check(calls.Count == before, "Tree part press does not invoke");
            Pump(host, EventType.Repaint, point);
            Pump(host, EventType.MouseUp, point);
        }

        Check(calls.SequenceEqual(new[] { "expand:stable", "check:stable" }), "Tree disclosure/check are distinct release operations without row command");
        Pump(host, EventType.MouseDown, marker);
        rows = Array.Empty<UiTreeRow>();
        host.Session.BumpContentRevision();
        Pump(host, EventType.Repaint, marker);
        Pump(host, EventType.MouseUp, marker);
        Check(calls.Count == 2 && host.Session.OwnedHotControl == null && host.Session.Interactions.CaptureOwner == null, "removed Tree content releases capture and never replays to replacement row");
    }

    private sealed class Metrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 24;
        public float MeasureWidth(string text, UiFont font) => text.Length * 8;
    }

    private sealed class Translation : IUiTranslation
    {
        public string Translate(string key) => key;
        public int TranslationRevision => 0;
    }
}
