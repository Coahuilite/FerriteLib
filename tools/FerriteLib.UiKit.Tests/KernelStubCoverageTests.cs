using System;
using System.Collections.Generic;

using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Stub-coverage lane: the harness must not be able to claim a page drew while the game-API call inside it
/// was failing.
/// <para>
/// Two things are asserted here, and they are different. First, the game members the stub now carries
/// behave the way the game behaves - a stub that returned its input unchanged would satisfy "the call did
/// not throw" and nothing else. Second, a page whose element calls such a member runs its own draw: the
/// guard from <see cref="KernelTripGuard"/> reports zero recovery bands *and* the element's draw counter
/// moved, which is what separates "the frame survived" from "the code under test executed".
/// </para>
/// <para>
/// The positive control at the end plants a throwing draw and requires the guard to fail on it: a guard
/// that cannot go red is a comment with a name.
/// </para>
/// </summary>
internal static class KernelStubCoverageTests
{
    private const string Scope = "stub-coverage";
    private const string ProbeKind = "stub/probe";

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("The stub's Rect insets behave the way the game's do", VerifyStubSemantics);
        Run("The stub carries the integer overloads a consumer calls", VerifyIntegerOverloads);
        Run("The stub's language and constant members behave the way the game's do", VerifyLanguageAndConstants);
        Run("The stub's pure game helpers behave the way the game's do", VerifyPureHelpers);
        Run("The stub's math helpers behave the way the game's do", VerifyMathHelpers);
        Run("A page calling a game inset draws instead of recovering", VerifyConsumerShapedCallDraws);
        Run("The trip guard fires on a planted trip (positive control)", VerifyGuardFiresOnAPlantedTrip);
        Run("The guard's deliberate switch is explicit and works", VerifyDeliberateSwitch);
        Reset();
        return failures;
    }

    /// <summary>
    /// Semantics copied from the game's own GenUI: four-sided inset, no clamping, negative shrinks the
    /// inset to nothing and expands the rect, and the per-axis overload insets each axis by its own value.
    /// </summary>
    private static void VerifyStubSemantics()
    {
        Rect rect = new Rect(10f, 20f, 100f, 50f);

        Rect inset = rect.ContractedBy(8f);
        CheckClose(18f, inset.x, "the inset moves x in by the margin");
        CheckClose(28f, inset.y, "and y the same way");
        CheckClose(84f, inset.width, "and removes the margin from both horizontal sides");
        CheckClose(34f, inset.height, "and from both vertical sides");

        Rect perAxis = rect.ContractedBy(1f, 2f);
        CheckClose(11f, perAxis.x, "the per-axis overload insets x by its own value");
        CheckClose(22f, perAxis.y, "and y by its own value");
        CheckClose(98f, perAxis.width, "leaving the width inset by twice the x margin");
        CheckClose(46f, perAxis.height, "and the height by twice the y margin");

        Rect grown = rect.ExpandedBy(4f, 6f);
        CheckClose(6f, grown.x, "the game's growth mirror moves the origin out");
        CheckClose(14f, grown.y, "on both axes");
        CheckClose(108f, grown.width, "and grows the extent by twice the margin");
        CheckClose(62f, grown.height, "per axis");

        Rect expanded = rect.ContractedBy(-5f);
        CheckClose(5f, expanded.x, "a negative margin expands instead of shrinking");
        CheckClose(110f, expanded.width, "with no clamping at any size");

        CheckClose(-90f, new Rect(0f, 0f, 100f, 50f).ContractedBy(95f).width,
            "an inset past the extent goes negative rather than being clamped");
    }

    /// <summary>
    /// The math family (2026-09-12, task-101), and the loud way it arrived: a consumer's circle drawing
    /// called Mathf.Sqrt, the trip guard named the member and the call path, and this lane now calls every
    /// member of the family so the reference-driven gate watches it. Each assertion is the member's own
    /// rule - the BCL counterpart, or the published formula for Repeat/PingPong/SmoothStep - including the
    /// two edge rules a caller could otherwise assume wrong: Round is the BCL's banker's rounding (2.5 goes
    /// to 2), and the infinities are float's own.
    /// </summary>
    private static void VerifyMathHelpers()
    {
        CheckClose(3f, Mathf.Sqrt(9f), "Sqrt is the square root");
        Check(Mathf.Abs(-3) == 3 && Mathf.Abs(3) == 3, "integer Abs drops the sign and keeps the magnitude");

        CheckClose(1f, Mathf.Floor(1.7f), "Floor rounds down");
        CheckClose(2f, Mathf.Ceil(1.2f), "Ceil rounds up");
        CheckClose(2f, Mathf.Round(2.5f), "Round is the BCL's banker's rounding, so 2.5 goes to 2");
        Check(Mathf.FloorToInt(1.9f) == 1, "FloorToInt truncates toward negative infinity");

        CheckClose(0f, Mathf.Sin(0f), "Sin(0)");
        CheckClose(1f, Mathf.Cos(0f), "Cos(0)");
        CheckClose(0f, Mathf.Tan(0f), "Tan(0)");
        CheckClose(0f, Mathf.Asin(0f), "Asin(0)");
        CheckClose(0f, Mathf.Acos(1f), "Acos(1)");
        CheckClose(0f, Mathf.Atan(0f), "Atan(0)");
        CheckClose(0f, Mathf.Atan2(0f, 1f), "Atan2 of the positive x axis");

        CheckClose(8f, Mathf.Pow(2f, 3f), "Pow raises to a power");
        CheckClose(1f, Mathf.Exp(0f), "Exp(0) is one");
        CheckClose(0f, Mathf.Log(1f), "Log(1) is zero");
        CheckClose(3f, Mathf.Log(8f, 2f), "and Log with a base is the inverse of Pow");
        CheckClose(2f, Mathf.Log10(100f), "Log10 of a power of ten");

        CheckClose(1f, Mathf.Repeat(7f, 3f), "Repeat loops the value into the range");
        CheckClose(1f, Mathf.PingPong(7f, 3f), "PingPong reflects it at the length");
        CheckClose(20f, Mathf.LerpUnclamped(0f, 10f, 2f), "LerpUnclamped does not clamp its t");
        CheckClose(5f, Mathf.SmoothStep(0f, 10f, 0.5f), "SmoothStep is the Hermite curve, symmetric at its midpoint");

        Check(float.IsPositiveInfinity(Mathf.Infinity) && float.IsNegativeInfinity(Mathf.NegativeInfinity),
            "the infinities are the float ones, not a large number");
        CheckClose(0.0174533f, Mathf.Deg2Rad, "Deg2Rad is the PI identity");
        CheckClose(57.29578f, Mathf.Rad2Deg, "and Rad2Deg is its inverse");
    }

    /// <summary>
    /// The pure-helper batch (2026-09-12, task-97): members whose bodies were read from the game's own
    /// source and that depend on nothing but their arguments - the two GenText string predicates, the two
    /// GenCollection list helpers, TaggedString concatenation, FloatRange.One, and Translator's lookup
    /// contract. Each is asserted rather than trusted, and asserting them here is also what makes this lane
    /// reference them, which is what puts them under the reference-driven gate permanently.
    /// </summary>
    private static void VerifyPureHelpers()
    {
        Check(((string?)null).NullOrEmpty(), "GenText.NullOrEmpty accepts a null receiver");
        Check("".NullOrEmpty(), "and an empty string");
        Check(!"x".NullOrEmpty(), "and calls a real string non-empty");

        // Characters from the fixed tail, not from the platform's set: the assertion must not depend on the
        // platform the harness runs on, while the platform set is exactly what the double inherits from the
        // game rather than inventing.
        Check(GenText.SanitizeFilename("a?b*c|d") == "a_b_c_d",
            "SanitizeFilename collapses runs of invalid characters into one underscore");
        Check(GenText.SanitizeFilename("name...") == "name", "and trims trailing dots the way the game does");
        Check(GenText.SanitizeFilename("clean-name") == "clean-name", "and leaves a clean name alone");

        List<int> numbers = new() { 1, 2, 3, 4 };
        Check(numbers.Any(n => n > 3), "GenCollection.Any finds a matching element");
        Check(!numbers.Any(n => n > 4), "and reports none when nothing matches");
        Check(numbers.Count(n => n % 2 == 0) == 2, "GenCollection.Count counts the matches");
        Check(numbers.Count(n => n > 4) == 0, "and counts zero when nothing matches");

        TaggedString left = new("left");
        TaggedString right = new("right");
        Check((left + right).RawText == "leftright", "TaggedString concatenation joins the raw texts");
        Check(("prefix" + left).RawText == "prefixleft", "and accepts a string on the left");
        Check((left + "suffix").RawText == "leftsuffix", "and on the right");

        Check(FloatRange.One.min == 1f && FloatRange.One.max == 1f, "FloatRange.One is a range of exactly one");

        string key = "neutral.test.key";
        bool found = key.TryTranslate(out TaggedString missing);
        Check(!found && missing.RawText == key, "a key the language data does not hold is not found and echoes itself");

        bool empty = "".TryTranslate(out TaggedString emptyResult);
        Check(!empty && emptyResult.RawText == "", "an empty key is not found either");

        // The stub's language-data hook is a stub-only member, so this lane cannot name it - the harness
        // compiles against the game's reference assembly - and reaches it the way this repo reads its other
        // stub-only recorders: through reflection. What is asserted is still the product-facing contract,
        // namely what TryTranslate answers for a key the data does and does not hold.
        System.Reflection.FieldInfo? resolver = typeof(Translator).GetField(
            "Resolve",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Check(resolver != null, "the stub's language-data hook is reachable for a lane to drive");
        if (resolver != null)
        {
            object? previous = resolver.GetValue(null);
            try
            {
                resolver.SetValue(null, (Func<string, string>)(candidate => candidate == key ? "resolved text" : candidate));
                bool resolved = key.TryTranslate(out TaggedString text);
                Check(resolved && text.RawText == "resolved text",
                    "and a key the language data holds is found, carrying its text");
            }
            finally
            {
                resolver.SetValue(null, previous);
            }
        }
    }

    /// <summary>
    /// The language/constant batch (2026-09-12): the members a consumer reads that carry no game logic at
    /// all - the zero constants, component-wise vector arithmetic, the magnitudes, the named colours, the
    /// object identity operators, the engine clock and the UI surface size. Each was a harness-only death
    /// while the double did not declare it, and each is asserted here rather than trusted: a double that
    /// returns a plausible-looking wrong number is worse than one that throws. Asserting them also makes
    /// this lane reference them, which is what puts them under the reference-driven gate permanently.
    /// </summary>
    private static void VerifyLanguageAndConstants()
    {
        Rect empty = Rect.zero;
        Check(empty.x == 0f && empty.y == 0f && empty.width == 0f && empty.height == 0f,
            "Rect.zero is an empty rect at the origin");

        Vector2 sum = new Vector2(1f, 2f) + new Vector2(3f, 4f);
        Check(sum.x == 4f && sum.y == 6f, "Vector2 addition is component-wise");
        Vector2 difference = new Vector2(3f, 4f) - new Vector2(1f, 2f);
        Check(difference.x == 2f && difference.y == 2f, "Vector2 subtraction is component-wise");
        Vector2 scaled = new Vector2(1f, 2f) * 2f;
        Check(scaled.x == 2f && scaled.y == 4f, "Vector2 scales by a scalar");

        Vector3 point = new Vector3(1f, 2f, 2f);
        Check(point.x == 1f && point.y == 2f && point.z == 2f, "Vector3 carries the game's three fields");
        Check(point.sqrMagnitude == 9f, "Vector3.sqrMagnitude is the squared length");
        Check(point.magnitude == 3f, "and magnitude is its square root");
        Vector3 moved = point - new Vector3(1f, 1f, 1f);
        Check(moved.x == 0f && moved.y == 1f && moved.z == 1f, "Vector3 subtraction is component-wise");
        Check(Vector3.Distance(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 2f)) == 2f,
            "Vector3.Distance measures between two points");

        Color black = Color.black;
        Check(black.r == 0f && black.g == 0f && black.b == 0f && black.a == 1f, "Color.black is opaque black");
        Color grey = Color.grey;
        Check(grey.r == 0.5f && grey.g == 0.5f && grey.b == 0.5f && grey.a == 1f, "the grey aliases agree");
        Check(Color.yellow.g > 0.9f && Color.yellow.g < 0.93f,
            "the named yellow is the game's numeric yellow, not pure yellow");

        UnityEngine.Object first = new();
        UnityEngine.Object second = new();
        UnityEngine.Object same = first;
        Check(first == same, "the object operator reports identity for the same reference");
        Check(first != second, "and inequality for two different objects");
        Check(!(first == null) && !(null == first), "a live object is neither null nor null-equal");
        Check((UnityEngine.Object?)null == null,
            "and two nulls compare equal, which is the branch a consumer's null guard takes");

        Check(Time.frameCount == 0 && Time.realtimeSinceStartup == 0f,
            "the engine clock answers zero rather than a ticking value the game would not produce here");

        int width = Verse.UI.screenWidth;
        Check(width > 0 && Verse.UI.screenHeight > 0, "the UI surface has a size a consumer can clamp against");
        try
        {
            Verse.UI.screenWidth = 1024;
            Check(Verse.UI.screenWidth == 1024, "and a lane can pin that size the way the game's fields allow");
        }
        finally
        {
            Verse.UI.screenWidth = width;
        }
    }

    /// <summary>
    /// The second measured hole of the same class (2026-09-12): <c>Mathf.Clamp(int, int, int)</c> and
    /// <c>Mathf.Min(int, int)</c> existed only in their float forms, so the consumer's timing card died
    /// with <c>MissingMethodException</c> inside the harness and never drew. These assertions are the
    /// semantics a caller relies on, not "it did not throw" - and the last one pins the two variants to
    /// the same order, because a stub whose int and float clamps disagreed would be its own trap.
    /// </summary>
    private static void VerifyIntegerOverloads()
    {
        Check(Mathf.Clamp(5, 0, 10) == 5, "an in-range integer passes through Clamp untouched");
        Check(Mathf.Clamp(-3, 0, 10) == 0, "a value below the minimum is raised to it");
        Check(Mathf.Clamp(42, 0, 10) == 10, "a value above the maximum is lowered to it");
        Check(Mathf.Min(3, 7) == 3, "the integer Min returns the smaller argument");
        Check(Mathf.Min(7, 3) == 3, "and the same value when the arguments are swapped");
        Check(Mathf.Clamp(5, 10, 0) == (int)Mathf.Clamp(5f, 10f, 0f),
            "the integer and float Clamp agree when the minimum is above the maximum");
    }

    /// <summary>
    /// The exact shape of the 2026-09-12 hole: a consumer element that calls a game member while it draws.
    /// The element must run to completion, and the guard must see no recovery band.
    /// </summary>
    private static void VerifyConsumerShapedCallDraws()
    {
        PrepareRegistry();
        ProbeWidget.Draws = 0;

        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"probe\" Kind=\"" + ProbeKind + "\" Height=\"24\"/>"
            + "</UiPage>");

        using UiHost host = new(Scope, manifest, new UiBindings(), UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        host.DrawFrame(new Rect(0f, 0f, 200f, 120f));

        KernelTripGuard.ExpectNoTrips(host.Session, "stub-coverage page draw");
        Check(ProbeWidget.Draws == 1,
            "the element drew its own code (got " + ProbeWidget.Draws + " draws): a recovery band would leave this at zero");
    }

    /// <summary>
    /// The control. A widget that throws must trip its slot, and the guard must say so with the element and
    /// the exception in the message - otherwise the guard would be green on exactly the frames it exists for.
    /// </summary>
    private static void VerifyGuardFiresOnAPlantedTrip()
    {
        PrepareRegistry();
        ProbeWidget.Draws = 0;

        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"boom\" Kind=\"" + ThrowingProbeKind + "\" Height=\"24\"/>"
            + "</UiPage>");

        using UiHost host = new(Scope, manifest, new UiBindings(), UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        host.DrawFrame(new Rect(0f, 0f, 200f, 120f));

        Check(KernelTripGuard.AnyTripped(host.Session),
            "the planted throw is recorded as a tripped element");

        bool fired = false;
        try
        {
            KernelTripGuard.ExpectNoTrips(host.Session, "stub-coverage positive control");
        }
        catch (Exception ex)
        {
            fired = true;
            Check(ex.Message.IndexOf("boom", StringComparison.Ordinal) >= 0,
                "the guard names the element that recovered");
            Check(ex.Message.IndexOf("planted stub-coverage draw failure", StringComparison.Ordinal) >= 0,
                "and carries the exception that tripped it");
            Check(ex.Message.IndexOf("stub-coverage positive control", StringComparison.Ordinal) >= 0,
                "and names the lane that is failing");
        }

        Check(fired, "the guard fails the lane when an element of the session sits in recovery");
    }

    private static void VerifyDeliberateSwitch()
    {
        PrepareRegistry();

        using UiHost host = new(Scope, ProbeManifest(), new UiBindings(), UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        host.DrawFrame(new Rect(0f, 0f, 200f, 120f));

        // A healthy session passes either way; what the switch changes is that a tripping lane can say so
        // in the call instead of leaving the guard out and hoping nobody notices.
        KernelTripGuard.ExpectNoTrips(host.Session, "stub-coverage healthy frame");
        KernelTripGuard.ExpectNoTrips(host.Session, "stub-coverage healthy frame, explicit switch", deliberateTrips: true);
        Check(!KernelTripGuard.AnyTripped(host.Session), "a page of healthy elements trips nothing");
    }

    private static UiLayoutManifest ProbeManifest()
    {
        return UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"probe\" Kind=\"" + ProbeKind + "\" Height=\"24\"/>"
            + "</UiPage>");
    }

    private static void PrepareRegistry()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, ProbeKind, () => new ProbeWidget(), new[] { "Height" });
        UiWidgetRegistry.Register(Scope, ThrowingProbeKind, () => new ThrowingProbeWidget(), new[] { "Height" });
    }

    private const string ThrowingProbeKind = "stub/probe-throws";

    /// <summary>
    /// Draws through a game member the stub has to carry, the way a consumer's element does, and counts its
    /// own draws so "the code ran" is measured rather than inferred from the absence of a complaint.
    /// </summary>
    private sealed class ProbeWidget : IUiWidget
    {
        internal static int Draws;

        public string Kind => ProbeKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 24f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            Rect inner = rect.ContractedBy(8f);
            Draws++;
            UiThemeDraw.Label(inner, "probe", ctx.Theme);
        }
    }

    private sealed class ThrowingProbeWidget : IUiWidget
    {
        public string Kind => ThrowingProbeKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 24f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            throw new InvalidOperationException("planted stub-coverage draw failure");
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 16f;

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
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

    private static void CheckClose(float expected, float actual, string name)
    {
        Check(Math.Abs(expected - actual) <= 0.001f, name + " (expected " + expected + ", got " + actual + ")");
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void Reset()
    {
        UiFitAudit.Detach();
    }
}
