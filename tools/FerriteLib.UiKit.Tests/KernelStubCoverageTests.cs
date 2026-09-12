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

        Check(fired, "the guard fails the lane when an element ended the frame in recovery");
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
