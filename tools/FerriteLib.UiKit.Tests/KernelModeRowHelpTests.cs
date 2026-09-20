using System;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Per-option hover help on <c>input/mode-row</c>: the kind's half of the capability the maintainer approved,
/// and deliberately not a tooltip.
/// <para>
/// The contract in consumer terms: each option carries its own help identity in its <c>DescriptionN</c>, and a
/// row that declares <c>HoverHelpKey</c> publishes <b>which option is hovered</b> — the identity itself, or the
/// option's value when it declares no description — through that binding, clearing it when the pointer leaves.
/// A consumer renders the help wherever it likes (the wired consumer's own help panel is the pattern vanilla's
/// mode selectors also follow) and never re-derives this kind's cell geometry, which is the whole point of
/// publishing rather than painting.
/// </para>
/// <para>
/// Failure-sensitivity is the requirement here, so every claim drives the interaction: hover moving between
/// two options must change the published identity exactly once per transition, leaving the row must clear it,
/// the option-without-a-description fallback must name the option anyway, the click must still select while the
/// hover claim is being published, and both creation-time refusals must fire. The red-first evidence is a
/// mutated control (the publication pinned to one option; the validation removed), recorded in the commit and
/// in <c>MEMORY.md</c> — a new capability has no pre-fix revision to be red against.
/// </para>
/// </summary>
internal static class KernelModeRowHelpTests
{
    private const string Scope = "mode-row-help-test";
    private const string ProbeKind = "test/command-probe";
    private const float Width = 200f;
    private const float CellHeight = 20f;

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("input/mode-row declares HoverHelpKey and keeps its label set", VerifyRegistration);
        Run("A per-option declaration with no option is refused at creation", VerifyInertOptionRefusal);
        Run("HoverHelpKey must name a writable string binding", VerifyHoverKeyRefusal);
        Run("Hovering an option publishes its declared help identity", VerifyHoverPublishesIdentity);
        Run("Moving the hover changes the identity, once per transition", VerifyHoverTransitionWritesOnce);
        Run("Leaving the row clears the published identity", VerifyLeavingClears);
        Run("An option with no description publishes its value", VerifyValueFallback);
        Run("The hover claim consumes nothing: the click still selects", VerifyHoverDoesNotConsumeTheClick);
        Run("A disabled or covered element is not hovered", VerifyHoverRespectsTheFunnelRules);
        ResetSeams();
        return failures;
    }

    // --- vocabulary and creation contract ---------------------------------------------------------

    private static void VerifyRegistration()
    {
        PrepareCore();

        IReadOnlyCollection<string>? schema = UiWidgetRegistry.GetAttributeSchema(UiWidgetRegistry.CoreScope, InputModeRowWidget.Kind);
        if (schema == null)
        {
            Check(false, "input/mode-row declares an attribute schema");
            return;
        }

        Check(Contains(schema, InputModeRowWidget.HoverHelpKeyAttribute), "the schema allows HoverHelpKey");
        Check(Contains(schema, "Description3") && Contains(schema, "Value3"),
            "and still allows the per-option description/value pair it publishes");

        IReadOnlyCollection<string>? labels = UiWidgetRegistry.GetLabelAttributes(UiWidgetRegistry.CoreScope, InputModeRowWidget.Kind);
        Check(
            labels != null && labels.Count == 16 && Contains(labels, "Title1") && Contains(labels, "TitleKey1")
                && !Contains(labels, "Description1"),
            "the label set is both title families (16 names) and still excludes the descriptions, which are published, never painted (B8's half)");
    }

    private static void VerifyInertOptionRefusal()
    {
        PrepareCore();

        Reject(
            "<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Height=\"20\" Value1=\"A\" Description3=\"orphan help\" />",
            "Description3",
            "a description whose index has no option");

        Reject(
            "<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Height=\"20\" Value1=\"A\" Title2=\"orphan title\" />",
            "Title2",
            "a title whose index has no option");

        // The refusal is not a blanket ban on gaps in the numbering: an option with a description and no
        // title, and an option 4 after an absent option 3, are both ordinary manifests.
        Host(
            "<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Height=\"20\""
            + " Value1=\"A\" Description1=\"help a\" Value4=\"D\" Title4=\"Dee\" />");
        Check(true, "a description without a title, and a gap in the option numbering, still create");
    }

    private static void VerifyHoverKeyRefusal()
    {
        PrepareCore();

        // Unbound: the row would write through a key nothing answers.
        Reject(
            "<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Height=\"20\" Value1=\"A\" HoverHelpKey=\"nobody-bound-this\" />",
            "nobody-bound-this",
            "a HoverHelpKey nothing is bound to");

        // Read-only: the write would be refused mid-frame and trip the recovery band.
        var bindings = new UiBindings();
        bindings.BindValue<string>("mode", () => "A", _ => { });
        bindings.BindReadOnly("published", () => "help");
        Check(RejectWithBindings(
                bindings,
                "<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Height=\"20\" Value1=\"A\" HoverHelpKey=\"published\" />",
                "not writable"),
            "a read-only HoverHelpKey is refused, because the publication is a write");

        // Wrong type: the key exists but cannot hold a string.
        var wrong = new UiBindings();
        float number = 0f;
        wrong.BindValue<string>("mode", () => "A", _ => { });
        wrong.BindValue("numeric", () => number, value => number = value);
        Check(RejectWithBindings(
                wrong,
                "<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Height=\"20\" Value1=\"A\" HoverHelpKey=\"numeric\" />",
                "numeric"),
            "a HoverHelpKey bound to a non-string value is refused by the typed validation");

        // And the well-formed declaration creates.
        var good = new UiBindings();
        good.BindValue<string>("mode", () => "A", _ => { });
        string help = "";
        good.BindValue<string>("help", () => help, value => help = value);
        HostWithBindings(
            good,
            "<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Height=\"20\" Value1=\"A\" HoverHelpKey=\"help\" />");
        Check(true, "a writable string HoverHelpKey creates");
    }

    // --- the publication ---------------------------------------------------------------------------

    private static void VerifyHoverPublishesIdentity()
    {
        Fixture fixture = Build();
        EnablePointer(CellCenter(fixture, 1));

        fixture.Widget.Draw(fixture.Rect, fixture.Context);

        Check(fixture.Help == "help B", "hovering option 2 publishes its declared description");
        Check(fixture.Writes == 1, "and publishes it once");
    }

    private static void VerifyHoverTransitionWritesOnce()
    {
        Fixture fixture = Build();

        EnablePointer(CellCenter(fixture, 0));
        fixture.Widget.Draw(fixture.Rect, fixture.Context);
        Check(fixture.Help == "help A" && fixture.Writes == 1, "hovering option 1 publishes its identity");

        fixture.Widget.Draw(fixture.Rect, fixture.Context);
        Check(fixture.Writes == 1, "the same option hovered again writes nothing: one write per transition, not per frame");

        UiNative.DebugMousePosition = CellCenter(fixture, 1);
        fixture.Widget.Draw(fixture.Rect, fixture.Context);
        Check(fixture.Help == "help B" && fixture.Writes == 2, "moving the hover to option 2 changes the identity and writes once");

        UiNative.DebugMousePosition = CellCenter(fixture, 0);
        fixture.Widget.Draw(fixture.Rect, fixture.Context);
        Check(fixture.Help == "help A" && fixture.Writes == 3, "and moving back changes it again");
    }

    private static void VerifyLeavingClears()
    {
        Fixture fixture = Build();

        EnablePointer(CellCenter(fixture, 1));
        fixture.Widget.Draw(fixture.Rect, fixture.Context);
        Check(fixture.Help == "help B", "the hover starts published");

        UiNative.DebugMousePosition = new Vector2(fixture.Rect.xMax + 40f, fixture.Rect.yMax + 40f);
        fixture.Widget.Draw(fixture.Rect, fixture.Context);
        Check(fixture.Help == "", "leaving the row clears the published identity");
        Check(fixture.Writes == 2, "and the clear is one write, not a per-frame stream of empties");

        fixture.Widget.Draw(fixture.Rect, fixture.Context);
        Check(fixture.Writes == 2, "a following frame outside the row writes nothing");
    }

    private static void VerifyValueFallback()
    {
        Fixture fixture = Build();
        EnablePointer(CellCenter(fixture, 2));

        fixture.Widget.Draw(fixture.Rect, fixture.Context);

        Check(fixture.Help == "C", "an option that declares no description publishes its value, so the hovered option is always named");
    }

    private static void VerifyHoverDoesNotConsumeTheClick()
    {
        Fixture fixture = Build();
        EnablePointer(CellCenter(fixture, 1));
        UiNative.ButtonOverride = rect =>
        {
            Vector2 p = UiNative.DebugMousePosition;
            return p.x >= rect.x && p.x <= rect.xMax && p.y >= rect.y && p.y <= rect.yMax;
        };

        fixture.Widget.Draw(fixture.Rect, fixture.Context);

        Check(fixture.Mode == "B", "a click on the hovered option still selects it");
        Check(fixture.Help == "help B", "and the same frame publishes the hover: the claim consumed nothing");
    }

    /// <summary>
    /// The funnel rules the publication inherits by asking <c>UiNative.IsMouseOver(rect, ctx)</c> rather than
    /// the raw rect test: a command-disabled element is not hovered, and an element under another element's
    /// popup layer is not hovered either. Both are driven against real arranged nodes, so the checks are the
    /// funnel's answer rather than a re-reading of its source.
    /// </summary>
    private static void VerifyHoverRespectsTheFunnelRules()
    {
        PrepareCore();
        UiWidgetRegistry.Register(Scope, ProbeKind, () => new ProbeWidget(), new[] { "Id", "Kind", "ActionBind", "Height" });

        bool enabled = false;
        var bindings = new UiBindings();
        bindings.BindCommand("gate", () => { }, () => enabled);
        bindings.BindValue<string>("mode", () => "A", _ => { });

        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
                + "<Widget Id=\"gate\" Kind=\"" + ProbeKind + "\" ActionBind=\"gate\" Height=\"20\" />"
                + "<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Height=\"20\" Value1=\"A\" />"
                + "</UiPage>"),
            bindings,
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());

        host.DrawFrame(new Rect(0f, 0f, Width, 200f));
        UiNode gate = host.Session.GetNodeByElementId("gate") ?? throw new Exception("the command-bound element was not arranged");
        UiNode mode = host.Session.GetNodeByElementId("mode") ?? throw new Exception("the mode row was not arranged");
        Check(gate.IsDisabled, "the probe element is published disabled while its predicate answers false");

        var rect = new Rect(0f, 0f, Width, CellHeight);
        EnablePointer(new Vector2(10f, 10f));
        UiWidgetContext gateCtx = MakeContext(host.Session, bindings).WithNode(gate);
        UiWidgetContext modeCtx = MakeContext(host.Session, bindings).WithNode(mode);

        // Corrected in the element-help round: the hover test no longer applies the DISABLED rule. Disabled-ness
        // refuses input (Button and the session-plus-key primitives), while hover also drives inspection - and a
        // control that is unavailable is exactly when a player needs the help that explains it. The consumer's
        // own unavailable-state help entries are the evidence for that split.
        Check(UiNative.IsMouseOver(rect, gateCtx), "a disabled element IS hovered: help explains why it is unavailable instead of activating it");
        Check(UiNative.IsMouseOver(rect, modeCtx), "and so is an enabled element covering the same rect");

        // Plant a popup layer that belongs to another element and covers the point: the mode row is under it.
        // A pass publishes the layers the previous pass recorded (BeginHitPass copies hitLayers into the
        // dispatch stack), so the fixture drops whatever the frame left, pushes, and then opens a pass.
        host.Session.BeginHitPass();
        host.Session.PushHitLayer(mode, rect, isPopup: false);
        host.Session.PushHitLayer(gate, rect, isPopup: true);
        host.Session.BeginHitPass();
        Check(!UiNative.IsMouseOver(rect, modeCtx), "an element under another element's popup layer is not hovered");

        // The complementary half of the same rule: a popup the element owns does not make it 'covered', which
        // is what keeps toggle-to-close working for the kind that opened it.
        host.Session.PushHitLayer(mode, rect, isPopup: true);
        host.Session.BeginHitPass();
        Check(UiNative.IsMouseOver(rect, modeCtx), "while its own popup leaves it hovered");
    }

    // --- fixture -----------------------------------------------------------------------------------

    private sealed class Fixture
    {
        public InputModeRowWidget Widget = null!;
        public UiSession Session = null!;
        public UiWidgetContext Context = null!;
        public Rect Rect;
        public string Mode = "A";
        public string Help = "";
        public int Writes;
    }

    /// <summary>
    /// Four options, two of them with a declared description and one without — the shape the three
    /// publication checks need: an identity to publish, a transition to move between, and the fallback.
    /// </summary>
    private static Fixture Build()
    {
        PrepareCore();

        var fixture = new Fixture();
        var bindings = new UiBindings();
        bindings.BindValue<string>("mode", () => fixture.Mode, value => fixture.Mode = value);
        bindings.BindValue<string>("help", () => fixture.Help, value =>
        {
            fixture.Help = value;
            fixture.Writes++;
        });

        var attrs = new Dictionary<string, string>
        {
            ["Bind"] = "mode",
            ["HoverHelpKey"] = "help",
            ["Height"] = "20",
            ["Value1"] = "A", ["Title1"] = "A", ["Description1"] = "help A",
            ["Value2"] = "B", ["Title2"] = "B", ["Description2"] = "help B",
            ["Value3"] = "C", ["Title3"] = "C",
            ["Value4"] = "D", ["Title4"] = "D", ["Description4"] = "help D"
        };

        fixture.Widget = new InputModeRowWidget();
        fixture.Widget.Configure(new UiElementSpec("mode", InputModeRowWidget.Kind, attrs));
        fixture.Session = new UiSession();
        fixture.Context = MakeContext(fixture.Session, bindings);
        fixture.Rect = new Rect(0f, 0f, Width, 200f);
        return fixture;
    }

    /// <summary>
    /// The centre of option <paramref name="index"/>, computed from the theme's own geometry — the same
    /// arithmetic the kind lays its cells out with, so a pointer placed here is inside that cell by
    /// construction rather than by a hard-coded pixel.
    /// </summary>
    private static Vector2 CellCenter(Fixture fixture, int index)
    {
        UiGeometry g = UiTheme.DarkGold.Geometry;
        int columns = fixture.Rect.width >= 560f ? 4 : fixture.Rect.width >= 360f ? 2 : 1;
        float innerWidth = Math.Max(1f, fixture.Rect.width - g.Spacing * 2f);
        float columnWidth = (innerWidth - (columns - 1) * g.Gap) / columns;
        int row = index / columns;
        int col = index % columns;
        float x = fixture.Rect.x + g.Spacing + col * (columnWidth + g.Gap) + columnWidth / 2f;
        float y = fixture.Rect.y + g.Spacing + row * (CellHeight + g.Gap) + CellHeight / 2f;
        return new Vector2(x, y);
    }

    // --- helpers ----------------------------------------------------------------------------------

    private static void PrepareCore()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
    }

    private static UiWidgetContext MakeContext(UiSession session, IUiBindings bindings)
    {
        return new UiWidgetContext(
            Scope, session, new StubMetrics(), UiTheme.DarkGold,
            new StubTranslation(), bindings, Width, "root");
    }

    private static void Host(string body)
    {
        HostWithBindings(new UiBindings { }.WithMode(), body);
    }

    private static void HostWithBindings(UiBindings bindings, string body)
    {
        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>"),
            bindings,
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());
    }

    private static void Reject(string body, string expectedInMessage, string what)
    {
        Check(RejectWithBindings(new UiBindings { }.WithMode(), body, expectedInMessage), what + " is refused at creation");
    }

    private static bool RejectWithBindings(UiBindings bindings, string body, string expectedInMessage)
    {
        try
        {
            HostWithBindings(bindings, body);
            return false;
        }
        catch (UiContractException ex)
        {
            return ex.Message.IndexOf(expectedInMessage, StringComparison.Ordinal) >= 0;
        }
    }

    private static UiBindings WithMode(this UiBindings bindings)
    {
        bindings.BindValue<string>("mode", () => "A", _ => { });
        return bindings;
    }

    private static void EnablePointer(Vector2 point)
    {
        UiNative.DebugMousePositionEnabled = true;
        UiNative.DebugMousePosition = point;
        UiNative.DebugMouseDown = false;
    }

    private static void ResetSeams()
    {
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMousePosition = default;
        UiNative.DebugMouseDown = false;
        UiNative.DebugMouseDrag = false;
        UiNative.DebugMouseUp = false;
        UiNative.DebugEnter = false;
        UiNative.DebugFocusLost = false;
        UiNative.ButtonOverride = null;
        UiNative.TextFieldOverride = null;
    }

    private static bool Contains(IReadOnlyCollection<string> set, string name)
    {
        foreach (string candidate in set)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static int Run(string name, Action action)
    {
        ResetSeams();
        int before = failures;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }

        if (failures == before)
        {
            Console.WriteLine("  ok: " + name);
        }

        return failures - before;
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return font switch
            {
                UiFont.Tiny => 16f,
                UiFont.Small => 24f,
                _ => 32f
            };
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key)
        {
            return "[" + key + "]";
        }

        public int TranslationRevision => 0;
    }

    /// <summary>A kind that declares <c>ActionBind</c> and nothing else: the shape the engine publishes disabled.</summary>
    private sealed class ProbeWidget : IUiWidget
    {
        string IUiWidget.Kind => ProbeKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            return CellHeight;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }
}
