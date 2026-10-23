using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Greenfield Session/Native-interaction lane tests: session-owned popup state, per-session fallback
/// guards, GUI state restoration, and session-scoped popup draw callbacks.
/// </summary>
internal static class KernelSessionTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        ResetStaticTestSeams();

        try
        {
            VerifyPopupStateIsolated();
            VerifyDisposeClearsPopupState();
            VerifyGuardLogsOncePerSessionAndClearsOnNewSession();
            VerifyDrawFallbackRestoresGuiState();
            VerifyPopupDrawCallbacksAreSessionScoped();
            VerifyDropdownHelpersUseSessionState();
            VerifyHotControlOwnershipIsSessionScoped();
            VerifyHoverClaimHoldsAcrossATransitGap();
            VerifyHoverClaimReleasesAfterItsGraceWindow();
            VerifyLiveHoverClaimReplacesTheHeldOne();
            VerifyZeroGraceHoverClaimClearsEveryPass();
        }
        finally
        {
            ResetStaticTestSeams();
        }

        return failures;
    }

    private static void VerifyPopupStateIsolated()
    {
        using UiSession session1 = new();
        using UiSession session2 = new();

        UiLayoutRect anchor1 = new(10f, 20f, 100f, 30f);
        session1.OpenPopup("owner-a", anchor1);

        Check(session1.OpenPopupId == "owner-a", "session1 owns its popup id");
        Check(session1.OpenPopupAnchor.HasValue
            && session1.OpenPopupAnchor.Value.X == 10f
            && session1.OpenPopupAnchor.Value.Y == 20f,
            "session1 stores the popup anchor");
        Check(session2.OpenPopupId == null && !session2.OpenPopupAnchor.HasValue,
            "session2 starts with no popup state");

        session2.OpenPopup("owner-b", new UiLayoutRect(1f, 2f, 3f, 4f));
        Check(session1.IsPopupOpen("owner-a") && !session1.IsPopupOpen("owner-b"),
            "session1 popup is unaffected by session2");
        Check(session2.IsPopupOpen("owner-b") && !session2.IsPopupOpen("owner-a"),
            "session2 popup is independent of session1");

        session1.ClosePopup();
        Check(!session1.IsPopupOpen("owner-a"), "ClosePopup closes session1 popup");
        Check(session2.IsPopupOpen("owner-b"), "ClosePopup on session1 does not close session2");
    }

    private static void VerifyDisposeClearsPopupState()
    {
        UiSession session = new();
        session.OpenPopup("owner", new UiLayoutRect(0f, 0f, 50f, 20f));
        session.RegisterPopupDraw(() => { });

        Check(session.OpenPopupId == "owner", "popup is open before dispose");
        Check(session.PopupDrawActions.Count == 1, "popup draw callback is registered before dispose");

        session.Dispose();

        Check(session.OpenPopupId == null && !session.OpenPopupAnchor.HasValue,
            "Dispose clears popup id and anchor");
        Check(!session.IsPopupOpen("owner"), "Dispose clears IsPopupOpen");
        Check(session.PopupDrawActions.Count == 0, "Dispose clears popup draw callbacks");
        Check(!session.IsActive, "Dispose deactivates the session");
    }

    private static void VerifyGuardLogsOncePerSessionAndClearsOnNewSession()
    {
        var messages = new List<string>();

        using UiSession session1 = new();
        session1.Warning=messages.Add;
        UiWidgetContext firstContext=NativeFixture.Context(session1);
        Exception first = new InvalidOperationException("first boom");
        float fallback = UiSessionGuard.MeasureOrFallback(
            firstContext,
            "slider",
            "input/stepper-slider",
            "root/slider",
            42f,
            () => throw first);

        Check(fallback == 42f, "MeasureOrFallback returns fallback height");
        Check(messages.Count == 1, "first failure logs once");
        // The guard keys a recovery slot on the node whose pass is running; called outside a tree that is
        // the session's own node, and the diagnostic still carries the consumer's element id.
        Check(session1.IsTripped(firstContext.Node), "failure marks the explicitly named node slot tripped");

        Check(session1.TryGetTripLog(firstContext.Node, out string diagnostic)
            && diagnostic.Contains("slider")
            && diagnostic.Contains("input/stepper-slider")
            && diagnostic.Contains("root/slider")
            && diagnostic.Contains("first boom")
            && diagnostic.Contains("KernelSessionTests"),
            "trip log records element id, kind, path and full stack");

        UiSessionGuard.MeasureOrFallback(
            firstContext,
            "slider",
            "input/stepper-slider",
            "root/slider",
            43f,
            () => throw new InvalidOperationException("second boom"));
        Check(messages.Count == 1, "same session does not log the same component again");

        using UiSession session2 = new();
        session2.Warning=messages.Add;
        UiSessionGuard.MeasureOrFallback(
            NativeFixture.Context(session2),
            "slider",
            "input/stepper-slider",
            "root/slider",
            44f,
            () => throw new InvalidOperationException("third boom"));
        Check(messages.Count == 2, "new session can log the same component again");
    }

    private static void VerifyDrawFallbackRestoresGuiState()
    {
        using UiSession session = new();
        Text.Font = GameFont.Medium;
        GUI.color = VerseUiExecution.Colour(new UiColour(1f, 0f, 0f, 1f));

        GameFont seenFont = GameFont.Small;
        UiColour seenColor = UiColour.White;
        bool fallbackDrawn = false;

        UiSessionGuard.DrawOrFallback(
            NativeFixture.Context(session),
            "draw-control",
            "input/dropdown",
            "root/draw-control",
            new UiLayoutRect(0f, 0f, 10f, 10f),
            () => throw new InvalidOperationException("draw boom"),
            rect =>
            {
                fallbackDrawn = true;
                seenFont = Text.Font;
                seenColor = VerseUiExecution.Colour(GUI.color);
            });

        Check(fallbackDrawn, "DrawOrFallback invokes the fallback");
        Check(seenFont == GameFont.Medium, "fallback sees the caller's Text.Font restored");
        Check(seenColor.Red == 1f && seenColor.Green == 0f && seenColor.Blue == 0f,
            "fallback sees the caller's GUI.color restored");
        Check(Text.Font == GameFont.Medium
            && GUI.color.r == 1f && GUI.color.g == 0f && GUI.color.b == 0f,
            "GUI state remains restored after DrawOrFallback returns");
    }

    private static void VerifyPopupDrawCallbacksAreSessionScoped()
    {
        using UiSession session1 = new();
        using UiSession session2 = new();

        int session1Draws = 0;
        int session2Draws = 0;
        session1.RegisterPopupDraw(() => session1Draws++);
        session2.RegisterPopupDraw(() => session2Draws++);

        Check(session1.PopupDrawActions.Count == 1 && session2.PopupDrawActions.Count == 1,
            "each session owns its own popup draw callback list");

        session2.EndFrame();
        Check(session2.PopupDrawActions.Count == 0, "EndFrame clears session2 popup draw callbacks");
        Check(session1.PopupDrawActions.Count == 1, "EndFrame on session2 does not touch session1");

        session1.Dispose();
        Check(session1.PopupDrawActions.Count == 0, "Dispose clears session1 popup draw callbacks");
    }

    private static void VerifyHotControlOwnershipIsSessionScoped()
    {
        NativeFixture.DebugMousePositionEnabled = true;
        NativeFixture.DebugHotControl = 0;
        try
        {
            using UiSession session1 = new();
            using UiSession session2 = new();

            NativeFixture.Capture(session1,NativeFixture.Key(111));
            Check(NativeFixture.HasHotControl(session1,NativeFixture.Key(111)) && session1.OwnedHotControl == NativeFixture.Key(111),
                "capture records session ownership and takes the native slot");

            NativeFixture.Capture(session2,NativeFixture.Key(222));
            int secondNative=GUIUtility.hotControl;
            Check(NativeFixture.HasHotControl(session2,NativeFixture.Key(222)) && session2.OwnedHotControl == NativeFixture.Key(222),
                "second session capture takes the native slot");

            session1.ReleaseHotControl(NativeFixture.Key(222));
            Check(NativeFixture.DebugHotControl == secondNative,
                "session cannot release a hot control it does not own");

            session1.ReleaseHotControl(NativeFixture.Key(111));
            Check(NativeFixture.DebugHotControl == secondNative && session1.OwnedHotControl == null,
                "owning session ends its record without touching another session's native slot");

            session2.ReleaseHotControl(NativeFixture.Key(111));
            Check(NativeFixture.DebugHotControl == secondNative,
                "session cannot release another session's ownership record");

            session2.ReleaseHotControl(NativeFixture.Key(222));
            Check(NativeFixture.DebugHotControl == 0 && session2.OwnedHotControl == null,
                "owning session releases its capture and clears its record");
        }
        finally
        {
            NativeFixture.DebugMousePositionEnabled = false;
            NativeFixture.DebugHotControl = 0;
        }
    }

    private static void VerifyDisposeReleasesOnlyOwnedCapture()
    {
        NativeFixture.DebugMousePositionEnabled = true;
        NativeFixture.DebugHotControl = 0;
        try
        {
            UiSession session1 = new();
            UiSession session2 = new();

            NativeFixture.Capture(session1,NativeFixture.Key(111));
            NativeFixture.Capture(session2,NativeFixture.Key(222));
            int secondNative=GUIUtility.hotControl;

            session1.Dispose();
            Check(NativeFixture.DebugHotControl == secondNative,
                "disposing session1 does not release session2's native capture");
            Check(session1.OwnedHotControl == null && !session1.IsActive,
                "disposing session1 clears its ownership record and deactivates it");

            session2.Dispose();
            Check(NativeFixture.DebugHotControl == 0,
                "disposing session2 releases its own native capture");
            Check(session2.OwnedHotControl == null && !session2.IsActive,
                "disposing session2 clears its ownership record");
        }
        finally
        {
            NativeFixture.DebugMousePositionEnabled = false;
            NativeFixture.DebugHotControl = 0;
        }
    }

    private static void VerifyDropdownHelpersUseSessionState()
    {
        using UiSession session1 = new();
        using UiSession session2 = new();

        UiLayoutRect trigger = new(0f, 0f, 100f, 28f);
        UiLayoutRect row = new(0f, 28f, 100f, 24f);
        UiWidgetContext first=NativeFixture.Context(session1),second=NativeFixture.Context(session2);
        void EventAt(EventType phase,float y){Event.current=new Event{type=phase,button=0,mousePosition=new Vector2(10,y)};}
        bool Trigger(){EventAt(EventType.MouseDown,10);NativeFixture.DropdownButton(trigger,"dropdown",first);EventAt(EventType.MouseUp,10);return NativeFixture.DropdownButton(trigger,"dropdown",first);}
        Check(Trigger(),"real qualified trigger release opens popup");
        Check(session1.IsPopupOpen("dropdown")&&session1.OpenPopupAnchor!.Value.Width==100,"trigger stores session-owned popup state");
        Check(!session2.IsPopupOpen("dropdown"),"other session is unaffected");
        EventAt(EventType.MouseDown,35);var pressed=NativeFixture.PopupOption(row,"dropdown",first,"row");
        var other=NativeFixture.PopupOption(row,"dropdown",second,"row");
        EventAt(EventType.MouseUp,35);var selected=NativeFixture.PopupOption(row,"dropdown",first,"row");
        Check(pressed.Captured&&selected.Request&&!other.Captured&&!other.Request,"popup row uses actual qualified release and refuses the other session");
        Check(Trigger()&&!session1.IsPopupOpen("dropdown"),"same trigger click closes only the owning session popup");
        EventAt(EventType.Repaint,10);
        Check(!NativeFixture.DropdownButton(trigger,"dropdown",first),"paint alone does not open popup");
    }

    // ---- P3: the session-owned hover-claim machine ----------------------------------------
    // These pin the two rules the consumer had to hand-roll, and the one distinction that is easy
    // to lose in a rewrite: a claim the boundary restored is NOT the same as a claim a widget made,
    // so restoring must not re-arm the grace window forever.

    private static void VerifyHoverClaimHoldsAcrossATransitGap()
    {
        using UiSession session = new();
        session.HoverGraceFrames = 2;

        session.BeginFrame();
        session.ClaimHover("control-a");
        Check(session.HoverClaim == "control-a", "the claim is visible in the pass that made it");

        session.BeginFrame();
        Check(session.HoverClaim == null, "the frame boundary clears the claim so widgets must re-claim");

        // Pointer crossing the gap between two controls: nobody claims this pass.
        session.BeginFrame();
        Check(session.HoverClaim == "control-a", "a gap pass re-presents the held claim instead of flashing the overview");
    }

    private static void VerifyHoverClaimReleasesAfterItsGraceWindow()
    {
        using UiSession session = new();
        session.HoverGraceFrames = 2;

        session.BeginFrame();
        session.ClaimHover("control-a");

        string?[] seen = new string?[4];
        for (int i = 0; i < seen.Length; i++)
        {
            session.BeginFrame();
            seen[i] = session.HoverClaim;
        }

        Check(seen[0] == null, "pass 1 after the claim is the boundary pass, which clears");
        Check(seen[1] == "control-a" && seen[2] == "control-a", "grace holds the claim for exactly the configured passes");
        Check(seen[3] == null, "the claim is released once the grace window is spent");
    }

    private static void VerifyLiveHoverClaimReplacesTheHeldOne()
    {
        using UiSession session = new();
        session.HoverGraceFrames = 3;

        session.BeginFrame();
        session.ClaimHover("control-a");

        session.BeginFrame();
        session.ClaimHover("control-b");
        Check(session.HoverClaim == "control-b", "a live claim replaces the held one in the same pass");

        // Three gap passes with no claims. The first is the boundary pass that follows a real claim,
        // so it is blank by design (a still-hovered widget re-claims inside it); the grace window
        // carries from the second. None of them may show a: if a restored claim re-armed the window,
        // the superseded one would come back and pin the panel.
        session.BeginFrame();
        string? gap1 = session.HoverClaim;
        session.BeginFrame();
        string? gap2 = session.HoverClaim;
        session.BeginFrame();
        string? gap3 = session.HoverClaim;

        Check(gap2 == "control-b" && gap3 == "control-b", "the grace window carries the newer claim");
        Check(gap1 != "control-a" && gap2 != "control-a" && gap3 != "control-a",
            "the superseded claim never comes back");
    }

    private static void VerifyZeroGraceHoverClaimClearsEveryPass()
    {
        using UiSession session = new();
        Check(session.HoverGraceFrames == 0, "grace defaults to zero, so a host that opts out behaves as before");

        session.BeginFrame();
        session.ClaimHover("control-a");

        session.BeginFrame();
        Check(session.HoverClaim == null, "with no grace the machine is the plain per-pass clear");

        session.BeginFrame();
        Check(session.HoverClaim == null, "and nothing is carried into a later pass");
    }
    private static void ResetStaticTestSeams()
    {

        NativeFixture.ButtonOverride = null;
        Text.Font = GameFont.Small;
        GUI.color = VerseUiExecution.Colour(UiColour.White);
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
}
