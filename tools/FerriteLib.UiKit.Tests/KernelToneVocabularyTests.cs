using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Tone-vocabulary lane (0.7 / CP-6, items ①③④). It holds what the batch decided, from the outside:
/// <list type="bullet">
/// <item>the authored <c>Tone</c> vocabulary is exactly <c>Neutral</c>/<c>Success</c>/<c>Warning</c>/<c>Danger</c>,
/// and each of the four is a rule that records nothing;</item>
/// <item><c>Active</c> and <c>Disabled</c> stopped being authored names: both still paint exactly what they
/// painted before for one minor, as redirects, and each writes one <b>deduplicated deprecation note</b> on
/// the appearance channel rather than a silent fallback;</item>
/// <item>the value ladder itself did not move — an unknown <i>value</i> still falls back and is recorded,
/// while an unknown attribute <i>name</i> is still refused at creation;</item>
/// <item><c>UiTheme.HoverPoint</c> is deleted outright and the hover step is computed from the one stored
/// <c>AccentGold</c>, so re-tinting the accent cannot leave a stale second token;</item>
/// <item>a <b>role</b> is code-owned and resolves in every skin, while a document-owned <c>Scheme</c> name
/// may dangle with the existing recorded fallback;</item>
/// <item><c>UiStatusTone</c> keeps every member, because it is a stable type.</item>
/// </list>
/// The <c>HoverPoint</c> half is read reflectively on purpose: an absence cannot be named in compiled code,
/// and reflection is also what proves there is no alias left behind.
/// </summary>
internal static class KernelToneVocabularyTests
{
    private const string Scope = "tone-vocabulary-test";

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("The four authored meanings resolve without a note", VerifyFourAuthoredMeanings);
        Run("Tone=\"Active\" redirects to the selected state and records one note", VerifyActiveRedirect);
        Run("Tone=\"Disabled\" redirects to the disabled state and records one note", VerifyDisabledRedirect);
        Run("The value ladder is unchanged: unknown value falls back, unknown name stays fatal", VerifyValueLadderUnchanged);
        Run("HoverPoint is deleted and its step is derived from the one stored accent", VerifyAccentIsOneStoredColour);
        Run("A code-owned role cannot dangle; a document-owned scheme name may", VerifyDanglingRule);
        Run("UiStatusTone keeps every member the stable tier promises", VerifyStableMembersSurvive);

        // Leave the shared registry in the state the next lane expects, and drop this lane's sinks.
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiFitAudit.Detach();
        UiFitAudit.Reset();
        return failures;
    }

    // --- ① the four authored meanings --------------------------------------------------------------

    private static void VerifyFourAuthoredMeanings()
    {
        PrepareRegistry();
        UiTheme theme = UiTheme.DarkGold;
        using UiSession session = new();
        var bindings = new UiBindings();
        bindings.BindCommand("go", () => { });
        UiWidgetContext ctx = Context(session, theme, bindings, 160f);

        var meanings = new[]
        {
            ("Neutral", UiStatusTone.Neutral),
            ("Success", UiStatusTone.Success),
            ("Warning", UiStatusTone.Warning),
            ("Danger", UiStatusTone.Danger)
        };

        foreach ((string authored, UiStatusTone tone) in meanings)
        {
            var records = new List<UiStyleFallbackReport>();
            UiFitAudit.AttachStyleFallback(records.Add);
            UiFitAudit.Reset();
            try
            {
                Color painted = PaintedRule(ctx, ("Tone", authored));
                Check(SameColor(painted, theme.Styles.Resolve(tone).Border),
                    "Tone=\"" + authored + "\" paints that role's treatment");
                Check(records.Count == 0 && UiFitAudit.StyleFallbackCount == 0,
                    "Tone=\"" + authored + "\" is a rule, not a fallback: it records nothing");
            }
            finally
            {
                UiFitAudit.Detach();
                UiFitAudit.Reset();
            }
        }
    }

    // --- ① Active / Disabled: redirects for one minor, with one note --------------------------------

    private static void VerifyActiveRedirect()
    {
        PrepareRegistry();
        UiTheme theme = UiTheme.DarkGold;
        using UiSession session = new();
        var bindings = new UiBindings();
        bindings.BindCommand("go", () => { });
        UiWidgetContext ctx = Context(session, theme, bindings, 160f);

        Color authored = default;
        var records = new List<UiStyleFallbackReport>();
        try
        {
            UiFitAudit.AttachStyleFallback(records.Add);
            UiFitAudit.Reset();

            authored = PaintedRule(ctx, ("Tone", "Active"));
            Check(SameColor(authored, theme.Styles.Resolve(UiStatusTone.Active).Border),
                "Tone=\"Active\" still paints the selected treatment for one minor");
            Check(records.Count == 1, "and records exactly one deprecation note (got " + records.Count + ")");
            if (records.Count == 1)
            {
                Check(records[0].Attribute == "Tone" && records[0].Authored == "Active",
                    "the note names the attribute and the authored text");
                Check(records[0].Resolved.IndexOf("Active", StringComparison.Ordinal) >= 0,
                    "and the state it redirects to (got '" + records[0].Resolved + "')");
                Check(records[0].Diagnostic.IndexOf("deprecat", StringComparison.OrdinalIgnoreCase) >= 0,
                    "the note reads as a deprecation (got '" + records[0].Diagnostic + "')");
            }

            PaintedRule(ctx, ("Tone", "Active"));
            Check(records.Count == 1 && UiFitAudit.StyleFallbackCount == 1,
                "a second pass reports the same note once, not per frame");
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }

        Color lower = PaintedRule(ctx, ("Tone", "active"));
        Color neutral = PaintedRule(ctx, ("Tone", "Neutral"));
        Check(SameColor(lower, authored),
            "the name is matched case-insensitively, like every other attribute value in this vocabulary");
        Check(!SameColor(authored, neutral),
            "and the redirect is not the neutral fallback an unknown value gets: it is the state, still");
    }

    private static void VerifyDisabledRedirect()
    {
        PrepareRegistry();
        UiTheme theme = UiTheme.DarkGold;
        using UiSession session = new();
        var bindings = new UiBindings();
        bindings.BindCommand("go", () => { });
        UiWidgetContext ctx = Context(session, theme, bindings, 160f);

        Color authored = default;
        var records = new List<UiStyleFallbackReport>();
        try
        {
            UiFitAudit.AttachStyleFallback(records.Add);
            UiFitAudit.Reset();

            authored = PaintedRule(ctx, ("Tone", "Disabled"));
            Check(SameColor(authored, theme.Styles.Resolve(UiStatusTone.Disabled).Border),
                "Tone=\"Disabled\" still paints the disabled treatment for one minor");
            Check(records.Count == 1, "and records exactly one deprecation note (got " + records.Count + ")");
            if (records.Count == 1)
            {
                Check(records[0].Attribute == "Tone" && records[0].Authored == "Disabled",
                    "the note names the attribute and the authored text");
                Check(records[0].Resolved.IndexOf("Disabled", StringComparison.Ordinal) >= 0,
                    "and the state it redirects to (got '" + records[0].Resolved + "')");
                Check(records[0].Diagnostic.IndexOf("deprecat", StringComparison.OrdinalIgnoreCase) >= 0,
                    "the note reads as a deprecation (got '" + records[0].Diagnostic + "')");
            }

            PaintedRule(ctx, ("Tone", "Disabled"));
            Check(records.Count == 1 && UiFitAudit.StyleFallbackCount == 1,
                "a second pass reports the same note once, not per frame");
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }

        Color neutral = PaintedRule(ctx, ("Tone", "Neutral"));
        Check(!SameColor(authored, neutral),
            "the disabled redirect is not the neutral fallback: state and role stay distinguishable");
    }

    // --- the ladder around the redirects -----------------------------------------------------------

    private static void VerifyValueLadderUnchanged()
    {
        PrepareRegistry();
        UiTheme theme = UiTheme.DarkGold;
        using UiSession session = new();
        var bindings = new UiBindings();
        bindings.BindCommand("go", () => { });
        UiWidgetContext ctx = Context(session, theme, bindings, 160f);

        var records = new List<UiStyleFallbackReport>();
        try
        {
            UiFitAudit.AttachStyleFallback(records.Add);
            UiFitAudit.Reset();

            Color unknown = PaintedRule(ctx, ("Tone", "chartreuse"));
            Check(SameColor(unknown, theme.Styles.Resolve(UiStatusTone.Neutral).Border),
                "a value outside the vocabulary still falls back to the neutral treatment");
            Check(records.Count == 1 && records[0].Attribute == "Tone" && records[0].Authored == "chartreuse"
                && records[0].Resolved == "Neutral",
                "and is still recorded as a fallback, with the value it resolved to");
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }

        // The other end of the ladder is the attribute NAME, and it did not move.
        Reject(Page("<Widget Id=\"r\" Kind=\"chrome/rule\" Ton=\"Danger\" />"),
            "a misspelled role attribute name is still refused at creation");
        Reject(Page("<Widget Id=\"r\" Kind=\"chrome/rule\" Tone=\"Danger\" Emphasis=\"Muted\" />"),
            "a role the kind never declared is still refused at creation");
        Reject(Page("<Stack Id=\"s\" Tone=\"Danger\"><Widget Id=\"n\" Kind=\"text/wrapped\" Text=\"x\" /></Stack>"),
            "Tone on a container is still refused: roles do not inherit");
    }

    // --- ④ one accent, and a derived hover step ----------------------------------------------------

    private static void VerifyAccentIsOneStoredColour()
    {
        MemberInfo[] survivors = typeof(UiTheme).GetMember(
            "HoverPoint", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        Check(survivors.Length == 0,
            "UiTheme.HoverPoint is deleted outright: no property, no field, no alias, no redirect");

        int leftovers = 0;
        foreach (Type type in typeof(UiTheme).Assembly.GetTypes())
        {
            leftovers += type.GetMember(
                "HoverPoint", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Length;
        }

        Check(leftovers == 0,
            "and no other type in the payload carries the name either: it is gone, not moved (found " + leftovers + ")");

        PropertyInfo? stored = typeof(UiTheme).GetProperty("AccentGold", BindingFlags.Public | BindingFlags.Instance);
        Check(stored != null && stored.CanRead && stored.CanWrite, "the accent stays the one stored colour");

        PropertyInfo? derived = typeof(UiTheme).GetProperty("AccentHover", BindingFlags.Public | BindingFlags.Instance);
        Check(derived != null, "the hover step is a computed member of UiTheme (AccentHover)");
        Check(derived != null && derived.CanRead && !derived.CanWrite,
            "which a caller cannot assign: it is derived from the accent, not stored beside it");
        if (derived == null) return;

        UiTheme probe = UiTheme.Vanilla;
        var accent = new Color(0.2f, 0.4f, 0.6f, 0.5f);
        probe.AccentGold = accent;
        Color hover = (Color)derived.GetValue(probe)!;

        Check(hover.r > accent.r && hover.g > accent.g && hover.b > accent.b,
            "the derived step lightens every channel of the accent");
        Check(Near(hover.a, accent.a),
            "and keeps its alpha, so a transparent accent gets a transparent hover step, not an opaque one");

        float liftR = Lift(accent.r, hover.r);
        float liftG = Lift(accent.g, hover.g);
        float liftB = Lift(accent.b, hover.b);
        Check(Near(liftR, liftG) && Near(liftG, liftB),
            "one fixed fraction toward white on every channel, not an adaptive lighten (got "
            + liftR + ", " + liftG + ", " + liftB + ")");

        probe.AccentGold = new Color(0.9f, 0.1f, 0.3f, 1f);
        Color moved = (Color)derived.GetValue(probe)!;
        Check(!SameColor(moved, hover),
            "re-tinting the accent moves the derived step: nothing stored beside it went stale");
        Check(SameColor((Color)derived.GetValue(probe)!, moved),
            "and reading it twice in a row is deterministic");

        // The name left the document vocabulary too, so a scheme writing it is reported rather than
        // silently ignored - the one failure mode the maintainer's ruling exists to prevent.
        UiStyleDocument document = UiStyleDocument.Parse(
            "<Styles Schema=\"1\"><Scheme Name=\"ghost\">"
            + "<Color Token=\"HoverPoint\" Value=\"#FF0000FF\" />"
            + "<Color Token=\"AccentGold\" Value=\"#00FF00FF\" />"
            + "</Scheme></Styles>");
        Check(document.Issues.Count == 1,
            "a scheme declaring HoverPoint records one issue instead of silently doing nothing (got "
            + document.Issues.Count + ")");
        Check(document.Issues.Count == 1 && document.Issues[0].Message.IndexOf("HoverPoint", StringComparison.Ordinal) >= 0,
            "and the message names the dropped token");
        Check(document.TryGetScheme("ghost", out UiStyleDocument.SchemeDefinition definition)
            && !definition.Colours.ContainsKey("HoverPoint") && definition.Colours.ContainsKey("AccentGold"),
            "the dropped declaration never reaches the theme while the rest of the scheme still does");
    }

    // --- ③ what may dangle -------------------------------------------------------------------------

    private static void VerifyDanglingRule()
    {
        // A role is code-owned: the vocabulary is a closed enum, and the table answers every member even
        // for a skin that claims no token at all. A role reference therefore cannot dangle.
        UiTheme unpainted = new();
        foreach (UiStatusTone tone in Enum.GetValues(typeof(UiStatusTone)))
        {
            UiResolvedStyle style = unpainted.Styles.Resolve(tone);
            Check(style.Fill.a == 0f && style.Border.a == 0f && style.Text.a == 0f,
                "an unpainted skin still answers the role " + tone + " with the bag's own values");
        }

        Check(unpainted.Styles.FallbackCount == 0,
            "and answering a declared role is a rule, never a recorded fallback");

        // A Scheme name is document-owned: a name no skin declares cannot resolve, and that is recorded
        // and survivable rather than fatal.
        UiStyleDocument document = UiStyleDocument.Parse("<Styles Schema=\"1\"></Styles>");
        var resolver = new UiStyleResolver(UiTheme.DarkGold, document);
        UiTheme scope = resolver.ThemeFor(new[] { new UiStyleDeclaration(scheme: "nobody-declares-this") });
        Check(resolver.Issues.Count == 1, "a dangling scheme name is recorded at resolution time");
        Check(SameColor(scope.Panel, UiTheme.DarkGold.Panel),
            "and the scope keeps the values it would have had without it: the page still renders");
    }

    // --- the stable type ---------------------------------------------------------------------------

    private static void VerifyStableMembersSurvive()
    {
        string[] names = Enum.GetNames(typeof(UiStatusTone));
        foreach (string member in new[] { "Neutral", "Active", "Success", "Warning", "Danger", "Disabled" })
        {
            Check(Array.IndexOf(names, member) >= 0,
                "UiStatusTone." + member + " is still a member: the type is stable and no member may be removed");
        }

        Check(names.Length == 6,
            "the stable vocabulary gained no member either: the shrink is in the manifest vocabulary (got "
            + names.Length + " members)");
    }

    // --- harness -----------------------------------------------------------------------------------

    private static void PrepareRegistry()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
    }

    /// <summary>Draws one rule and returns the single colour it painted.</summary>
    private static Color PaintedRule(UiWidgetContext ctx, params (string Name, string Value)[] attributes)
    {
        RuleWidget rule = new();
        rule.Configure(new UiElementSpec("probe", RuleWidget.Kind, Attributes(attributes)));
        ClearBoxes();
        rule.Draw(new Rect(0f, 0f, 100f, 9f), ctx);

        IList colors = RecordedBoxColors();
        if (colors.Count != 1)
        {
            Check(false, "expected chrome/rule to paint one solid, recorded " + colors.Count);
            return default;
        }

        return (Color)colors[0]!;
    }

    private static Dictionary<string, string> Attributes(params (string Name, string Value)[] pairs)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string value) in pairs)
        {
            attributes[name] = value;
        }

        return attributes;
    }

    private static UiWidgetContext Context(UiSession session, UiTheme theme, IUiBindings bindings, float viewWidth)
    {
        return new UiWidgetContext(Scope, session, new StubMetrics(), theme, new StubTranslation(), bindings, viewWidth, "root");
    }

    private static string Page(string body)
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>";
    }

    private static void Reject(string xml, string what)
    {
        try
        {
            using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), new UiBindings(), UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
            Check(false, what + " - but the host accepted it");
        }
        catch (UiContractException)
        {
            Check(true, what);
        }
    }

    /// <summary>The fraction of the remaining distance to white that the derived step travelled.</summary>
    private static float Lift(float from, float to)
    {
        return from >= 1f ? 0f : (to - from) / (1f - from);
    }

    private static bool SameColor(Color left, Color right)
    {
        return Near(left.r, right.r) && Near(left.g, right.g) && Near(left.b, right.b) && Near(left.a, right.a);
    }

    private static bool Near(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= 0.0001f;
    }

    private static void ClearBoxes()
    {
        MethodInfo? clear = typeof(Verse.Widgets).GetMethod("ClearDrawBoxSolidCalls", BindingFlags.Public | BindingFlags.Static);
        if (clear == null) throw new Exception("Verse stub is missing ClearDrawBoxSolidCalls");
        clear.Invoke(null, null);
    }

    private static IList RecordedBoxColors()
    {
        FieldInfo? colors = typeof(Verse.Widgets).GetField("DrawBoxSolidColors", BindingFlags.Public | BindingFlags.Static);
        if (colors == null) throw new Exception("Verse stub is missing DrawBoxSolidColors");
        return (IList)colors.GetValue(null)!;
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.GetType().Name + ": " + ex.Message);
        }
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
        public float MeasureText(string text, UiFont font, float width) => 0f;

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
    }
}
