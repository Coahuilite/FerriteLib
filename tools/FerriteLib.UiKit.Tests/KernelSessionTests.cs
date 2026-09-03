using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

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
            VerifyDisposeReleasesOnlyOwnedCapture();
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

        Rect anchor1 = new(10f, 20f, 100f, 30f);
        session1.OpenPopup("owner-a", anchor1);

        Check(session1.OpenPopupId == "owner-a", "session1 owns its popup id");
        Check(session1.OpenPopupAnchor.HasValue
            && session1.OpenPopupAnchor.Value.x == 10f
            && session1.OpenPopupAnchor.Value.y == 20f,
            "session1 stores the popup anchor");
        Check(session2.OpenPopupId == null && !session2.OpenPopupAnchor.HasValue,
            "session2 starts with no popup state");

        session2.OpenPopup("owner-b", new Rect(1f, 2f, 3f, 4f));
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
        session.OpenPopup("owner", new Rect(0f, 0f, 50f, 20f));
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
        UiSessionGuard.LogWarningOverride = messages.Add;

        using UiSession session1 = new();
        Exception first = new InvalidOperationException("first boom");
        float fallback = UiSessionGuard.MeasureOrFallback(
            session1,
            "slider",
            "input/stepper-slider",
            "root/slider",
            42f,
            () => throw first);

        Check(fallback == 42f, "MeasureOrFallback returns fallback height");
        Check(messages.Count == 1, "first failure logs once");
        Check(session1.IsTripped("slider"), "failure marks the session slot tripped");

        Check(session1.TryGetTripLog("slider", out string diagnostic)
            && diagnostic.Contains("slider")
            && diagnostic.Contains("input/stepper-slider")
            && diagnostic.Contains("root/slider")
            && diagnostic.Contains("first boom")
            && diagnostic.Contains("KernelSessionTests"),
            "trip log records element id, kind, path and full stack");

        UiSessionGuard.MeasureOrFallback(
            session1,
            "slider",
            "input/stepper-slider",
            "root/slider",
            43f,
            () => throw new InvalidOperationException("second boom"));
        Check(messages.Count == 1, "same session does not log the same component again");

        using UiSession session2 = new();
        UiSessionGuard.MeasureOrFallback(
            session2,
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
        GUI.color = new Color(1f, 0f, 0f, 1f);

        GameFont seenFont = GameFont.Small;
        Color seenColor = Color.white;
        bool fallbackDrawn = false;

        UiSessionGuard.DrawOrFallback(
            session,
            "draw-control",
            "input/dropdown",
            "root/draw-control",
            new Rect(0f, 0f, 10f, 10f),
            () => throw new InvalidOperationException("draw boom"),
            rect =>
            {
                fallbackDrawn = true;
                seenFont = Text.Font;
                seenColor = GUI.color;
            });

        Check(fallbackDrawn, "DrawOrFallback invokes the fallback");
        Check(seenFont == GameFont.Medium, "fallback sees the caller's Text.Font restored");
        Check(seenColor.r == 1f && seenColor.g == 0f && seenColor.b == 0f,
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
        UiNative.DebugMousePositionEnabled = true;
        UiNative.DebugHotControl = 0;
        try
        {
            using UiSession session1 = new();
            using UiSession session2 = new();

            session1.CaptureHotControl(111);
            Check(UiNative.DebugHotControl == 111 && session1.OwnedHotControl == 111,
                "capture records session ownership and takes the native slot");

            session2.CaptureHotControl(222);
            Check(UiNative.DebugHotControl == 222 && session2.OwnedHotControl == 222,
                "second session capture takes the native slot");

            session1.ReleaseHotControl(222);
            Check(UiNative.DebugHotControl == 222,
                "session cannot release a hot control it does not own");

            session1.ReleaseHotControl(111);
            Check(UiNative.DebugHotControl == 222 && session1.OwnedHotControl == null,
                "owning session ends its record without touching another session's native slot");

            session2.ReleaseHotControl(111);
            Check(UiNative.DebugHotControl == 222,
                "session cannot release another session's ownership record");

            session2.ReleaseHotControl(222);
            Check(UiNative.DebugHotControl == 0 && session2.OwnedHotControl == null,
                "owning session releases its capture and clears its record");
        }
        finally
        {
            UiNative.DebugMousePositionEnabled = false;
            UiNative.DebugHotControl = 0;
        }
    }

    private static void VerifyDisposeReleasesOnlyOwnedCapture()
    {
        UiNative.DebugMousePositionEnabled = true;
        UiNative.DebugHotControl = 0;
        try
        {
            UiSession session1 = new();
            UiSession session2 = new();

            session1.CaptureHotControl(111);
            session2.CaptureHotControl(222);

            session1.Dispose();
            Check(UiNative.DebugHotControl == 222,
                "disposing session1 does not release session2's native capture");
            Check(session1.OwnedHotControl == null && !session1.IsActive,
                "disposing session1 clears its ownership record and deactivates it");

            session2.Dispose();
            Check(UiNative.DebugHotControl == 0,
                "disposing session2 releases its own native capture");
            Check(session2.OwnedHotControl == null && !session2.IsActive,
                "disposing session2 clears its ownership record");
        }
        finally
        {
            UiNative.DebugMousePositionEnabled = false;
            UiNative.DebugHotControl = 0;
        }
    }

    private static void VerifyDropdownHelpersUseSessionState()
    {
        using UiSession session1 = new();
        using UiSession session2 = new();

        Rect trigger = new(0f, 0f, 100f, 28f);
        Rect row = new(0f, 28f, 100f, 24f);
        UiNative.ButtonOverride = _ => true;

        Check(UiNative.DropdownButton(trigger, "dropdown", session1), "dropdown trigger click opens popup");
        Check(session1.IsPopupOpen("dropdown")
            && session1.OpenPopupAnchor.HasValue
            && session1.OpenPopupAnchor.Value.width == 100f,
            "DropdownButton stores session-owned popup state");
        Check(!session2.IsPopupOpen("dropdown"), "other session is not affected by dropdown open");

        Check(UiNative.DropdownOptionRow(row, "dropdown", session1),
            "open popup row hit-test succeeds");
        Check(!UiNative.DropdownOptionRow(row, "dropdown", session2),
            "popup row hit-test is session-scoped");

        Check(UiNative.DropdownButton(trigger, "dropdown", session1), "dropdown trigger click closes popup");
        Check(!session1.IsPopupOpen("dropdown"), "same trigger click closes the session popup");

        UiNative.ButtonOverride = null;
        Check(!UiNative.DropdownButton(trigger, "dropdown", session1),
            "no native button hit does not open the popup");
    }

    private static void ResetStaticTestSeams()
    {
        UiSessionGuard.LogWarningOverride = null;
        UiNative.ButtonOverride = null;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
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
