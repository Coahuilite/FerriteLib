using System;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// G1: the element-level help hook, engine-wide. A page declares <c>HelpKey</c> on any widget element and the
/// ENGINE claims it while the pointer is over that element, so a declarative page carries help with no
/// per-widget code — which is what the consumer's 30 static claim sites need before any of them can migrate.
/// <para>
/// The publication is the session's existing claim machine, not a new channel: the engine calls
/// <c>UiSession.ClaimHover</c> during its draw walk, and the consumer reads <c>HoverClaim</c> /
/// <c>HoverClaimElement</c> exactly as it already does. Two deliberate properties are pinned here because they
/// are the ones a reader would guess wrong: the claim is <b>not translated or interpreted</b> (it is the
/// identity a consumer's own catalog is keyed by), and a <b>disabled</b> element still claims — help explains
/// why a control is unavailable rather than activating it, which is why the hover test dropped its disabled
/// rule in this round. The covered case is the one rule that does belong: an element under another element's
/// popup is not hovered by anyone's reading.
/// </para>
/// <para>
/// Failure-sensitivity comes from mutated controls (the engine's claim call removed; the hover test put back
/// on the raw rect form), recorded in the commit and in <c>MEMORY.md</c>: an engine-wide hook has no pre-fix
/// revision to be red against.
/// </para>
/// </summary>
internal static class KernelElementHelpTests
{
    private const string Scope = "element-help-test";
    private const string ProbeKind = "test/help-probe";
    private const string CommandKind = "test/command-probe";
    private const float Width = 240f;
    private const float RowHeight = 24f;

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("HelpKey is engine-wide on widgets and refused on containers", VerifyAttributeGate);
        Run("Hovering an element publishes its declared help identity", VerifyHoverClaims);
        Run("An element without HelpKey claims nothing", VerifyNoDeclarationClaimsNothing);
        Run("The claim is the declared token, verbatim", VerifyClaimIsOpaque);
        Run("A disabled element still claims: help explains, it does not activate", VerifyDisabledStillClaims);
        Run("An element under a higher layer does not claim", VerifyCoveredDoesNotClaim);
        Run("A kind's own option claim refines the element claim", VerifyOptionClaimWins);
        Run("The grace machine carries the engine's claim across a gap", VerifyGraceHoldsTheClaim);
        ResetSeams();
        return failures;
    }

    // --- the creation-time gate -------------------------------------------------------------------

    private static void VerifyAttributeGate()
    {
        PrepareCore();

        // A custom kind whose schema does not list HelpKey still accepts it: the gate is the engine's common
        // widget vocabulary, which is what makes the hook usable by every kind including consumer kinds.
        UiWidgetRegistry.Register(Scope, ProbeKind, () => new ProbeWidget(), new[] { "Id", "Kind", "Height" });
        Host(ProbeKind, "HelpKey=\"help/probe\"");
        Check(true, "a kind whose schema never listed HelpKey still creates with one");

        // The atoms inherit the engine-wide set through their shared schema — chrome/rule needs nothing else,
        // so this check is about the help attribute rather than about some other required binding.
        Host("chrome/rule", "HelpKey=\"help/rule\"");
        Check(true, "and a core atom accepts it too");

        // Containers are not hit surfaces, so a declaration there could never claim: refused, not inert.
        Check(Reject("<Row Id=\"r\" HelpKey=\"help/row\"><Widget Id=\"w\" Kind=\"" + ProbeKind + "\" Height=\"24\" /></Row>", "HelpKey"),
            "a container declaring HelpKey is refused at creation");
        Check(Reject("<Section Id=\"s\" HelpKey=\"help/section\"><Widget Id=\"w\" Kind=\"" + ProbeKind + "\" Height=\"24\" /></Section>", "HelpKey"),
            "and so is a section");
    }

    // --- the claim --------------------------------------------------------------------------------

    private static void VerifyHoverClaims()
    {
        using UiHost host = TwoHelpElements();
        host.DrawFrame(Viewport());
        UiNode a = host.Session.GetNodeByElementId("a") ?? throw new Exception("'a' was not arranged");
        UiNode b = host.Session.GetNodeByElementId("b") ?? throw new Exception("'b' was not arranged");

        EnablePointer(new Vector2(10f, 10f));
        host.DrawFrame(Viewport());
        Check(host.Session.HoverClaim == "help/a", "hovering the first element claims its declared identity");
        Check(host.Session.HoverClaimElement == a.Id, "and the claim is attributed to that element");

        UiNative.DebugMousePosition = new Vector2(10f, RowHeight + 10f);
        host.DrawFrame(Viewport());
        Check(host.Session.HoverClaim == "help/b", "moving the pointer to the second element re-claims in the same pass");
        Check(host.Session.HoverClaimElement == b.Id, "with its own attribution");

        UiNative.DebugMousePosition = new Vector2(10f, 400f);
        host.DrawFrame(Viewport());
        Check(host.Session.HoverClaim == null, "leaving both releases the claim (the grace window is zero here)");
    }

    private static void VerifyNoDeclarationClaimsNothing()
    {
        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
                + "<Column Id=\"col\" Padding=\"0\" Gap=\"0\">"
                + "<Widget Id=\"plain\" Kind=\"" + ProbeKind + "\" Height=\"24\" />"
                + "</Column></UiPage>"),
            new UiBindings(),
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());

        EnablePointer(new Vector2(10f, 10f));
        host.DrawFrame(Viewport());
        Check(host.Session.HoverClaim == null, "an element that declares no HelpKey claims nothing, however hovered it is");
    }

    private static void VerifyClaimIsOpaque()
    {
        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
                + "<Column Id=\"col\" Padding=\"0\" Gap=\"0\">"
                + "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"24\" HelpKey=\"us/Whatever/Exact-Token\" />"
                + "</Column></UiPage>"),
            new UiBindings(),
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());

        EnablePointer(new Vector2(10f, 10f));
        host.DrawFrame(Viewport());
        Check(host.Session.HoverClaim == "us/Whatever/Exact-Token",
            "the declared token arrives verbatim: the library neither translates it nor rewrites it");
    }

    private static void VerifyDisabledStillClaims()
    {
        PrepareCore();
        UiWidgetRegistry.Register(Scope, CommandKind, () => new ProbeWidget(), new[] { "Id", "Kind", "ActionBind", "Height" });

        bool enabled = false;
        var bindings = new UiBindings();
        bindings.BindCommand("gate", () => { }, () => enabled);

        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
                + "<Column Id=\"col\" Padding=\"0\" Gap=\"0\">"
                + "<Widget Id=\"gate\" Kind=\"" + CommandKind + "\" ActionBind=\"gate\" Height=\"24\" HelpKey=\"help/unavailable\" />"
                + "</Column></UiPage>"),
            bindings,
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());

        EnablePointer(new Vector2(10f, 10f));
        host.DrawFrame(Viewport());
        UiNode gate = host.Session.GetNodeByElementId("gate") ?? throw new Exception("'gate' was not arranged");
        Check(gate.IsDisabled, "the command-bound element is published disabled while its predicate answers false");
        Check(host.Session.HoverClaim == "help/unavailable",
            "and it still claims: the help explains why the control is unavailable, which is when a player needs it");
    }

    private static void VerifyCoveredDoesNotClaim()
    {
        using UiHost host = TwoHelpElements();
        host.DrawFrame(Viewport());
        UiNode a = host.Session.GetNodeByElementId("a") ?? throw new Exception("'a' was not arranged");
        UiNode b = host.Session.GetNodeByElementId("b") ?? throw new Exception("'b' was not arranged");

        var covering = new Rect(0f, 0f, Width, RowHeight * 2f);
        EnablePointer(new Vector2(10f, 10f));

        // Plant B's popup over A, then let the engine's own frame boundary publish it as the dispatch stack.
        host.Session.PushHitLayer(b, covering, isPopup: true);
        host.DrawFrame(Viewport());

        Check(host.Session.HoverClaim != "help/a", "an element under another element's popup does not claim its own help");
        Check(a != b, "and the two nodes really are distinct, so the check above is about coverage rather than identity");
    }

    private static void VerifyOptionClaimWins()
    {
        PrepareCore();
        var bindings = new UiBindings();
        bindings.BindValue<string>("mode", () => "A", _ => { });

        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
                + "<Column Id=\"col\" Padding=\"0\" Gap=\"0\">"
                + "<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Height=\"20\" HelpKey=\"help/row\""
                + " Value1=\"A\" Title1=\"A\" Description1=\"help/row/option-a\" />"
                + "</Column></UiPage>"),
            bindings,
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());

        // The cell starts at the theme's Spacing inset; its centre is inside the option, not the row padding.
        UiGeometry geometry = UiTheme.DarkGold.Geometry;
        EnablePointer(new Vector2(Width / 2f, geometry.Spacing + 10f));
        host.DrawFrame(Viewport());

        Check(host.Session.HoverClaim == "help/row/option-a",
            "the kind's option claim refines the engine's element claim: the specific topic wins while an option is hovered");
    }

    private static void VerifyGraceHoldsTheClaim()
    {
        using UiHost host = TwoHelpElements();
        host.Session.HoverGraceFrames = 2;

        EnablePointer(new Vector2(10f, 10f));
        host.DrawFrame(Viewport());
        Check(host.Session.HoverClaim == "help/a", "the claim starts live");

        // The machine's own sequence, measured rather than assumed: the boundary after a live claim clears it so
        // widgets must re-claim, and only a pass that re-claims nothing restores the held claim for the grace
        // window. So the first gap pass reads empty and the next two carry the held claim.
        UiNative.DebugMousePosition = new Vector2(10f, 400f);
        host.DrawFrame(Viewport());
        Check(host.Session.HoverClaim == null, "the first gap pass re-claims nothing, which is the boundary's job");

        host.DrawFrame(Viewport());
        Check(host.Session.HoverClaim == "help/a", "the next pass re-presents the held claim");

        host.DrawFrame(Viewport());
        Check(host.Session.HoverClaim == "help/a", "and it holds for the second grace pass");

        host.DrawFrame(Viewport());
        Check(host.Session.HoverClaim == null, "then releases it, so the machine carries the engine's claim like any other");
    }

    // --- fixtures ---------------------------------------------------------------------------------

    private static UiHost TwoHelpElements()
    {
        PrepareCore();
        UiWidgetRegistry.Register(Scope, ProbeKind, () => new ProbeWidget(), new[] { "Id", "Kind", "Height" });

        return new UiHost(
            Scope,
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
                + "<Column Id=\"col\" Padding=\"0\" Gap=\"0\">"
                + "<Widget Id=\"a\" Kind=\"" + ProbeKind + "\" Height=\"24\" HelpKey=\"help/a\" />"
                + "<Widget Id=\"b\" Kind=\"" + ProbeKind + "\" Height=\"24\" HelpKey=\"help/b\" />"
                + "</Column></UiPage>"),
            new UiBindings(),
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());
    }

    private static Rect Viewport() => new(0f, 0f, Width, 600f);

    private static void PrepareCore()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
    }

    private static void Host(string kind, string attributes)
    {
        Host("<Widget Id=\"w\" Kind=\"" + kind + "\" Height=\"24\" " + attributes + " />");
    }

    private static void Host(string body)
    {
        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>"),
            new UiBindings(),
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());
    }

    private static bool Reject(string body, string expectedInMessage)
    {
        try
        {
            Host(body);
            return false;
        }
        catch (UiContractException ex)
        {
            return ex.Message.IndexOf(expectedInMessage, StringComparison.Ordinal) >= 0;
        }
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
            return RowHeight;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }
}
