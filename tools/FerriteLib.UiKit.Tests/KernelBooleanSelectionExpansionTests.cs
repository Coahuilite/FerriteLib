using UnityEngine;
using Verse;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;

namespace FerriteLib.UiKit.Tests;
internal static class KernelBooleanSelectionExpansionTests
{
    private const string Scope = "bse";
    private static readonly UiLayoutRect View = new(20, 30, 420, 400);
    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception(description);
        Console.WriteLine("  ok: BSE " + description);
    }

    private static void Pump(UiHost host, EventType phase, UiLayoutPoint point, bool consumed = false)
    {
        var input = new Event
        {
            type = phase,
            button = 0,
            mousePosition = VerseUiExecution.Point(point)
        };
        if (consumed)
            input.Use();
        Event.current = input;
        NativeFixture.DrawFrame(        host,View);
    }

    private static UiLayoutPoint Centre(UiLayoutRect rect) => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    private static UiLayoutRect RectOf(UiHost host, string id)
    {
        UiNode node = host.Session.GetNodeByElementId(id)!;
        UiLayoutRect rect = node.Rect!.Value;
        return new UiLayoutRect(rect.X + View.X, rect.Y + View.Y, rect.Width, rect.Height);
    }

    private static UiHost Host(string content, UiBindings bindings)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Column Padding=\"0\" Gap=\"2\">" + content + "</Column></UiPage>");
        var host = new UiHost(Scope, manifest, bindings, UiTheme.Vanilla, new Metrics(), new Translation());
        Pump(host, EventType.Repaint, new UiLayoutPoint(-100, -100));
        return host;
    }

    public static int RunAll()
    {
        try
        {
            NativeFixture.ButtonOverride = null;
            NativeFixture.DebugMousePositionEnabled = false;
            NativeFixture.Trace = null;
            EstablishedRequestsSurvivePaintFailure();
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
            NativeFixture.ButtonOverride = null;
            NativeFixture.DebugMousePositionEnabled = false;
            NativeFixture.Trace = null;
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
            UiLayoutRect hit = RectOf(host, "flag");
            UiLayoutPoint point = Centre(hit);
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
                        mousePosition = VerseUiExecution.Point(point)
                    };
                    var ctx = NativeFixture.Context(Scope, host.Session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, View.Width, "custom-check");
                    var target = NativeFixture.InteractionTarget(ctx, "flag");
                    UiValueBehavior.Request(target, UiValueBehavior.Boolean(NativeFixture.Activation(hit, ctx, "flag"), !model), value =>
                    {
                        model = value;
                        writes++;
                        commands++;
                    });
                    UiThemeDraw.Solid(NativeFixture.Paint,new UiLayoutRect(hit.X, hit.Y, 3, hit.Height), ctx.Theme[UiPaletteRole.Accent]);
                }
                else
                {
                    var target = new UiInteractionTarget("flag", "flag");
                    UiPointerPhase input = phase == EventType.MouseDown ? UiPointerPhase.Press : phase == EventType.MouseUp ? UiPointerPhase.Release : UiPointerPhase.None;
                    UiInteractionResult interaction = pure.PointerActivation(target, new UiPointerSample(input, point.X, point.Y), new UiHitGeometry(hit.X, hit.Y, hit.Width, hit.Height), new UiInputEligibility(true));
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
        bindings.BindChoices("items", ()=> (new[] { "a", "b" }).Select(value=>new UiChoice<string>(value,value,value)).ToArray());
        using var host = Host("<Widget Id=\"first\" Kind=\"input/dropdown\" Bind=\"shared\" OptionsBind=\"items\" /><Widget Id=\"second\" Kind=\"input/dropdown\" Bind=\"shared\" OptionsBind=\"items\" /><Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"shared\"   ><Choice Value=\"a\" Text=\"a\" HelpRef=\"a\"/><Choice Value=\"b\" Text=\"b\" HelpRef=\"b\"/></Widget>", bindings);
        UiLayoutPoint first = Centre(RectOf(host, "first"));
        Pump(host, EventType.MouseDown, first);
        Pump(host, EventType.MouseUp, first);
        Check(host.Session.IsPopupOpen("shared") && host.Session.OpenPopupAnchor?.Y == RectOf(host, "first").Y, "two real Dropdown instances sharing Bind do not close or reanchor the first menu during sibling traversal");
        Pump(host, EventType.Repaint, first);
        Check(host.Session.IsPopupOpen("shared"), "same-Bind sibling repaint preserves actual popup instance");
        UiLayoutRect anchor = host.Session.OpenPopupAnchor!.Value;
        UiLayoutPoint option = new(anchor.X + 10, anchor.YMax + UiPopup.OptionHeight * 1.5f);
        Pump(host, EventType.MouseDown, option);
        Pump(host, EventType.MouseUp, option);
        Check(current == "b" && writes == 1 && host.Session.OpenPopupId == null, "same-Bind first popup selects once through its own option target");
        UiLayoutPoint second = Centre(RectOf(host, "second"));
        Pump(host, EventType.MouseDown, second);
        Pump(host, EventType.MouseUp, second);
        Check(host.Session.IsPopupOpen("shared") && host.Session.OpenPopupAnchor?.Y == RectOf(host, "second").Y, "same-Bind second popup keeps its own anchor while the earlier sibling draws");
        Pump(host, EventType.Repaint, second);
        host.Session.ClosePopup();
        UiLayoutRect modeRect = RectOf(host, "mode");
        UiLayoutPoint mode = new(modeRect.X + 15, modeRect.Y + modeRect.Height / 2);
        Pump(host, EventType.MouseDown, mode);
        UiInteractionTarget? held = host.Session.Interactions.CaptureOwner;
        Check(held?.Node == host.Session.GetNodeByElementId("mode")!.Id.Key, "real ModeRow starts a same-Bind non-popup capture");
        host.Session.OpenPopup("shared", RectOf(host, "first"));
        var modeNode = host.Session.GetNodeByElementId("mode")!;
        var composite = NativeFixture.Context(Scope, host.Session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, View.Width, "same-node-composite", node: modeNode);
        Event.current = new Event
        {
            type = EventType.Repaint,
            mousePosition = VerseUiExecution.Point(mode)
        };
        NativeFixture.PopupOption(modeRect, "shared", composite, "a");
        host.Session.ClosePopup();
        Check(host.Session.Interactions.CaptureOwner?.Equals(held!.Value) == true && host.Session.OwnedHotControl.HasValue, "closing a raw same-Bind popup does not release ModeRow capture by prefix inference");
        Pump(host, EventType.MouseUp, mode);
    }

    private static IList Recorded(string field) => (IList)typeof(Verse.Widgets).GetField(field)!.GetValue(null)!;
    private static void EstablishedRequestsSurvivePaintFailure()
    {
        foreach (string operation in new[]
        {
            "switch",
            "checkbox",
            "disclosure",
            "check",
            "body"
        }

        )
            foreach (bool close in new[]
            {
                false,
                true
            }

            )
            {
                using var session = new UiSession();
                var translation = new PaintFailureTranslation();
                var bindings = new UiBindings();
                var calls = new List<string>();
                bool model = false;
                bindings.BindValue("flag", () => model, value =>
                {
                    calls.Add("Set");
                    model = value;
                    if (close)
                        session.Dispose();
                });
                bindings.BindCommand("after", () => calls.Add("Invoke"));
                bindings.BindReadOnly<IReadOnlyList<UiTreeRow>>("rows", () => new[] { new UiTreeRow("row", 0, "Row", expandable: true, checkable: true, textKey: "paint-only"), new UiTreeRow("row-2", 0, "Second", expandable: true, checkable: true, textKey: "paint-only") });
                bindings.BindAction<string>("row", key =>
                {
                    calls.Add(operation + ":" + key);
                    if (close)
                        session.Dispose();
                });
                bool boolean = operation == "switch" || operation == "checkbox";
                string kind = boolean ? "input/checkbox" : "container/tree";
                var attributes = new Dictionary<string, string>
                {
                    ["Bind"] = boolean ? "flag" : "rows",
                    ["Appearance"] = operation,
                    ["LabelKey"] = "paint-only",
                    ["RowHeight"] = "28"
                };
                attributes[boolean ? "ActionBind" : operation == "disclosure" ? "DisclosureBind" : operation == "check" ? "CheckBind" : "ActionBind"] = boolean ? "after" : "row";
                IUiWidget widget = boolean ? new CheckboxWidget() : new TreeWidget();
                widget.Configure(new UiElementSpec("actual-wrapper", kind, attributes));
                var ctx = NativeFixture.Context(Scope, session, new Metrics(), UiTheme.Vanilla, translation, bindings, 200, "actual-wrapper");
                var rect = new UiLayoutRect(0, 0, 200, boolean ? 28 : 56);
                float pad = UiTheme.Vanilla.Geometry.Padding;
                float side = Math.Max(6, 28 - pad * 2);
                float x = boolean || operation == "body" ? 180 : pad + side / 2 + (operation == "check" ? side + pad : 0);
                Event.current = new Event
                {
                    type = EventType.MouseDown,
                    button = 0,
                    mousePosition = VerseUiExecution.Point(new UiLayoutPoint(x, 14))
                };
                NativeFixture.Draw(                widget,rect, ctx);
                Check(calls.Count == 0 && session.Interactions.CaptureOwner.HasValue, operation + " paint-failure fixture holds an actual native press");
                translation.Throw = true;
                translation.Attempts = 0;
                Event.current = new Event
                {
                    type = EventType.MouseUp,
                    button = 0,
                    mousePosition = VerseUiExecution.Point(new UiLayoutPoint(x, 14))
                };
                bool paintFailed = false;
                try
                {
                    NativeFixture.Draw(                    widget,rect, ctx);
                }
                catch (ApplicationException ex)when (ex.Message == "paint-only")
                {
                    paintFailed = true;
                }

                Check(boolean ? model && calls.SequenceEqual(new[] { "Set", "Invoke" }) : calls.SequenceEqual(new[] { operation + ":row" }), operation + " established release request is independent of pure paint success, close=" + close);
                Check(session.Interactions.CaptureOwner == null && session.OwnedHotControl == null, operation + " request/paint boundary retains native release");
                if (close)
                {
                    Check(!paintFailed && !session.IsActive && translation.Attempts == 0, operation + " callback close prevents subsequent paint/normal traversal");
                }
                else
                {
                    Check(paintFailed && translation.Attempts == 1, operation + " actual paint exception remains visible after one established request");
                    translation.Throw = false;
                    int before = calls.Count;
                    Event.current = new Event
                    {
                        type = EventType.Repaint,
                        mousePosition = VerseUiExecution.Point(new UiLayoutPoint(x, 14))
                    };
                    NativeFixture.Draw(                    widget,rect, ctx);
                    Check(calls.Count == before, operation + " paint recovery does not retry business request");
                }
            }
    }

    private sealed class PaintFailureTranslation : IUiTranslation
    {
        public bool Throw;
        public int Attempts;
        public string Translate(string key)
        {
            Attempts++;
            if (Throw)
                throw new ApplicationException("paint-only");
            return key;
        }

        public int TranslationRevision => 0;
    }

    private static bool SameColor(UiColour left, UiColour right) => Math.Abs(left.Red - right.Red) < 0.001f && Math.Abs(left.Green - right.Green) < 0.001f && Math.Abs(left.Blue - right.Blue) < 0.001f && Math.Abs(left.Alpha - right.Alpha) < 0.001f;
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
                theme[UiPaletteRole.SelectedForeground] = new UiColour(0.93f, 0.14f, 0.27f, 1);
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
                var ctx = NativeFixture.Context(Scope, session, new Metrics(), theme, new Translation(), bindings, 200, "visual", node: node);
                IUiWidget widget = kind == "tree" ? new TreeWidget() : new CheckboxWidget();
                widget.Configure(spec);
                var rect = new UiLayoutRect(0, 0, 200, 28);
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
                        return colors.Count > 0 && SameColor(NativeFixture.Record<UiColour>(colors[colors.Count - 1]!), theme.Styles.Resolve(UiStatusTone.Active).Text);
                    }

                    IList rects = Recorded("DrawBoxSolidRects");
                    IList fills = Recorded("DrawBoxSolidColors");
                    for (int i = 0; i < rects.Count; i++)
                    {
                        UiLayoutRect painted = NativeFixture.Record<UiLayoutRect>(rects[i]!);
                        if (painted.X == rect.X && painted.Y == rect.Y && painted.Width == rect.Width && painted.Height == rect.Height && SameColor(NativeFixture.Record<UiColour>(fills[i]!), theme.SelectedSurface.Fill))
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
                        mousePosition = VerseUiExecution.Point(new UiLayoutPoint(180, 14))
                    };
                    if (used)
                        Event.current.Use();
                    NativeFixture.Draw(                    widget,rect, ctx);
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
        using var host = Host("<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\"   ><Choice Value=\"a\" Text=\"a\" HelpRef=\"a\"/><Choice Value=\"b\" Text=\"b\" HelpRef=\"b\"/></Widget>", bindings);
        UiLayoutRect rect = RectOf(host, "mode");
        UiLayoutPoint point = new(rect.X + rect.Width * 0.75f, rect.Y + rect.Height / 2);
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
            bindings.BindChoices("items", ()=> (new[] { "a", "b" }).Select(value=>new UiChoice<string>(value,value,value)).ToArray());
            using var host = Host("<Widget Id=\"pick\" Kind=\"input/dropdown\" OptionsBind=\"items\" />", bindings);
            using var neutral = new UiInteractionSession();
            var ctx = NativeFixture.Context(Scope, host.Session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, View.Width, "independent");
            UiLayoutRect trigger = RectOf(host, "pick");
            UiLayoutRect menu = new(trigger.X, trigger.YMax, trigger.Width, UiPopup.OptionHeight * 2);
            var trace = new List<string>();
            void Drive(EventType phase, bool option)
            {
                UiLayoutPoint point = option ? new UiLayoutPoint(menu.X + 10, menu.Y + UiPopup.OptionHeight * 1.5f) : Centre(trigger);
                if (route == 0)
                    Pump(host, phase, point);
                else if (route == 1)
                {
                    Event.current = new Event
                    {
                        type = phase,
                        button = 0,
                        mousePosition = VerseUiExecution.Point(point)
                    };
                    NativeFixture.DropdownButton(trigger, "pick", ctx);
                    if (host.Session.IsPopupOpen("pick"))
                    {
                        UiPopup.DrawChoices(menu, "pick", ctx, new[] { new UiChoice<string>("A", "a"), new UiChoice<string>("B", "b") }, selected, value =>
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
                    var input = new UiPointerSample(sample, point.X, point.Y);
                    var interaction = neutral.PointerActivation(target, input, new UiHitGeometry(trigger.X, trigger.Y, trigger.Width, trigger.Height), new(true));
                    UiValueBehavior.Request(target, UiValueBehavior.Expansion(interaction, !expanded, true), value => expanded = value);
                    if (expanded)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            string value = i == 0 ? "a" : "b";
                            target = new UiInteractionTarget("headless", "pick", "popup-option", value, "model");
                            interaction = neutral.PointerActivation(target, input, new UiHitGeometry(menu.X, menu.Y + i * UiPopup.OptionHeight, menu.Width, UiPopup.OptionHeight), new(true));
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
        bindings.BindChoices("choices", ()=> (new[] { "a", "b", "c" }).Select(value=>new UiChoice<string>(value,value,value)).ToArray());
        bindings.BindCommand("under", () => underlying++);
        using var host = Host("<Widget Id=\"choice\" Kind=\"input/dropdown\" Bind=\"choice\" OptionsBind=\"choices\" /><Widget Id=\"under\" Kind=\"input/button\" ActionBind=\"under\" Height=\"28\" /><Widget Id=\"tail\" Kind=\"input/button\" ActionBind=\"under\" Height=\"140\" />", bindings);
        UiLayoutPoint trigger = Centre(RectOf(host, "choice"));
        Pump(host, EventType.MouseDown, trigger);
        Check(host.Session.OpenPopupId == null, "dropdown press only arms trigger");
        Pump(host, EventType.MouseUp, trigger);
        Check(host.Session.IsPopupOpen("choice"), "dropdown release opens expansion");
        UiLayoutRect anchor = host.Session.OpenPopupAnchor!.Value;
        UiLayoutPoint option = new(anchor.X + 10, anchor.YMax + UiPopup.OptionHeight * 1.5f);
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
        UiLayoutPoint outside = Centre(RectOf(host, "tail"));
        // The menu is three rows high; move the tail command below that menu.
        outside = new UiLayoutPoint(outside.X, anchor.YMax + 110);
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
        UiLayoutRect tree = RectOf(host, "tree");
        UiLayoutPoint second = new(tree.X + tree.Width - 20, tree.Y + 42);
        Pump(host, EventType.MouseDown, second);
        Check(host.Session.Interactions.CaptureOwner?.ItemKey as string == "b", "Tree body uses stable row key");
        rows = new[]
        {
            rows[1],
            rows[0]
        };
        host.Session.BumpContentRevision();
        Pump(host, EventType.Repaint, second);
        UiLayoutPoint first = new(second.X, tree.Y + 14);
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
        var ctx = NativeFixture.Context(Scope, native, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, 100, "checkbox");
        var checkbox = new CheckboxWidget();
        checkbox.Configure(new UiElementSpec("flag", "input/checkbox", new Dictionary<string, string> { ["Bind"] = "flag", ["ActionBind"] = "after" }));
        var rect = new UiLayoutRect(0, 0, 100, 28);
        Event.current = new Event
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = VerseUiExecution.Point(new UiLayoutPoint(50, 14))
        };
        NativeFixture.Draw(        checkbox,rect, ctx);
        Event.current = new Event
        {
            type = EventType.MouseUp,
            button = 0,
            mousePosition = VerseUiExecution.Point(new UiLayoutPoint(50, 14))
        };
        try
        {
            NativeFixture.Draw(            checkbox,rect, ctx);
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
            UiLayoutPoint point = Centre(RectOf(host, "flag"));
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
        choices.BindChoices("items", ()=> (new[] { "a", "b", "c" }).Select(value=>new UiChoice<string>(value,value,value)).ToArray());
        closing = Host("<Widget Id=\"pick\" Kind=\"input/dropdown\" OptionsBind=\"items\" />", choices);
        using (closing)
        {
            UiLayoutPoint trigger = Centre(RectOf(closing, "pick"));
            Pump(closing, EventType.MouseDown, trigger);
            Pump(closing, EventType.MouseUp, trigger);
            UiLayoutRect anchor = closing.Session.OpenPopupAnchor!.Value;
            UiLayoutPoint first = new(anchor.X + 10, anchor.YMax + 10);
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
            UiElementSpec Spec(string binding) => new("same-node", kind, new Dictionary<string, string> { ["Bind"] = binding, ["ActionBind"] = "action-" + binding, ["Max"] = "10" },choices: new[]{new UiChoice<string>("V","v")});
            var ctx = NativeFixture.Context(Scope, session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, 200, "same-node");
            var rect = new UiLayoutRect(0, 0, 200, 28);
            widget.Configure(Spec("a"));
            Event.current = new Event
            {
                type = EventType.MouseDown,
                button = 0,
                mousePosition = VerseUiExecution.Point(new UiLayoutPoint(100, 14))
            };
            NativeFixture.Draw(            widget,rect, ctx);
            Check(session.Interactions.CaptureOwner.HasValue, kind + " established its original binding capture");
            int before = a;
            widget.Configure(Spec("b"));
            Event.current = new Event
            {
                type = EventType.MouseUp,
                button = 0,
                mousePosition = VerseUiExecution.Point(new UiLayoutPoint(100, 14))
            };
            NativeFixture.Draw(            widget,rect, ctx);
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
            NativeFixture.Trace = ends.Add;
            using var session = new UiSession();
            int calls = 0;
            var bindings = new UiBindings();
            bindings.BindReadOnly<IReadOnlyList<UiTreeRow>>("rows", () => new[] { new UiTreeRow("row", 0, "Row", expandable: true, checkable: true) });
            bindings.BindAction<string>("a", _ => calls++);
            bindings.BindAction<string>("b", _ => calls++);
            var widget = new TreeWidget();
            UiElementSpec Spec(string target) => new("tree", "container/tree", new Dictionary<string, string> { ["Bind"] = "rows", [attribute] = target, ["RowHeight"] = "28" });
            var ctx = NativeFixture.Context(Scope, session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, 200, "tree");
            float pad = UiTheme.Vanilla.Geometry.Padding;
            float side = Math.Max(6, 28 - pad * 2);
            float x = attribute == "ActionBind" ? 180 : pad + side / 2 + (attribute == "CheckBind" ? side + pad : 0);
            widget.Configure(Spec("a"));
            Event.current = new Event
            {
                type = EventType.MouseDown,
                button = 0,
                mousePosition = VerseUiExecution.Point(new UiLayoutPoint(x, 14))
            };
            NativeFixture.Draw(            widget,new UiLayoutRect(0, 0, 200, 28), ctx);
            Check(session.Interactions.CaptureOwner.HasValue, "Tree " + attribute + " starts its own gesture");
            widget.Configure(Spec("b"));
            Event.current = new Event
            {
                type = EventType.MouseUp,
                button = 0,
                mousePosition = VerseUiExecution.Point(new UiLayoutPoint(x, 14))
            };
            NativeFixture.Draw(            widget,new UiLayoutRect(0, 0, 200, 28), ctx);
            Check(calls == 0 && session.OwnedHotControl == null && ends.Any(line => line.Contains("reason=TargetChanged")), "Tree " + attribute + " replacement ends the same part without transferring the gesture");
        }

        NativeFixture.Trace = null;
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
        UiLayoutRect band = RectOf(host, "tree");
        float pad = UiTheme.Vanilla.Geometry.Padding;
        float side = Math.Max(6, 28 - pad * 2);
        UiLayoutPoint marker = new(band.X + pad + side / 2, band.Y + 14);
        UiLayoutPoint check = new(marker.X + side + pad, marker.Y);
        foreach (UiLayoutPoint point in new[]
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
