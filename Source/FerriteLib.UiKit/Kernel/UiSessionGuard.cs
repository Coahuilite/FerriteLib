using System;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Session-scoped fallback guard for greenfield widgets. On a Measure/Draw exception it records the
/// component id, kind, layout path and full stack in the owning <see cref="UiSession"/>, trips that
/// session slot (logging once per session), restores GUI font/color, and invokes the stable fallback.
/// No process-global fallback registry or deferred dispatcher is used.
/// </summary>
public static class UiSessionGuard
{
    // Test seam: lets FerriteLib.UiKit.Tests capture warning text without a real game log.
    internal static Action<string>? LogWarningOverride;

    public static float MeasureOrFallback(
        UiSession session,
        string elementId,
        string kind,
        string elementPath,
        float fallbackHeight,
        Func<float> measure)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (string.IsNullOrEmpty(elementId)) throw new ArgumentException("Element id is required.", nameof(elementId));
        if (measure == null) throw new ArgumentNullException(nameof(measure));

        GameFont previousFont = Text.Font;
        Color previousColor = GUI.color;
        try
        {
            return measure();
        }
        catch (Exception ex)
        {
            Record(session, elementId, kind, elementPath, ex);
            Text.Font = previousFont;
            GUI.color = previousColor;
            return fallbackHeight;
        }
    }

    public static void DrawOrFallback(
        UiSession session,
        string elementId,
        string kind,
        string elementPath,
        Rect rect,
        Action draw,
        Action<Rect> fallback)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (string.IsNullOrEmpty(elementId)) throw new ArgumentException("Element id is required.", nameof(elementId));
        if (draw == null) throw new ArgumentNullException(nameof(draw));

        GameFont previousFont = Text.Font;
        Color previousColor = GUI.color;
        try
        {
            draw();
        }
        catch (Exception ex)
        {
            Record(session, elementId, kind, elementPath, ex);
            Text.Font = previousFont;
            GUI.color = previousColor;
            fallback?.Invoke(rect);
        }
    }

    private static void Record(UiSession session, string elementId, string kind, string elementPath, Exception ex)
    {
        bool first = !session.IsTripped(elementId);
        string diagnostic = BuildDiagnostic(elementId, kind, elementPath, ex);
        session.Trip(elementId, diagnostic);
        if (first)
        {
            LogWarning(diagnostic);
        }
    }

    private static string BuildDiagnostic(string elementId, string kind, string elementPath, Exception ex)
    {
        string stack = ex.StackTrace ?? "";
        string diagnostic =
            $"[FerriteLib.UiKit] fallback triggered for component '{elementId}'" +
            $" (Kind='{kind ?? ""}', path='{elementPath ?? ""}'): {ex}";
        if (stack.Length > 0)
        {
            diagnostic += Environment.NewLine + stack;
        }

        return diagnostic;
    }

    private static void LogWarning(string message)
    {
        if (LogWarningOverride != null)
        {
            LogWarningOverride(message);
        }
        else
        {
            Log.Warning(message);
        }
    }
}
