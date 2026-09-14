using System;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Session-scoped fallback guard for greenfield widgets. On a Measure/Draw exception it records the
/// component id, kind, layout path and full stack in the owning <see cref="UiSession"/>, trips that
/// session slot (logging once per session), restores GUI font/color, and applies a stable fallback.
/// No process-global fallback registry or deferred dispatcher is used.
/// <para>
/// Two entry shapes exist on purpose. <see cref="DrawWidget"/> and <see cref="MeasureWidget"/> are what
/// the layout engine calls: they take the widget itself, because the engine draws every element on
/// every IMGUI pass and a delegate argument would allocate a closure object at each of those calls.
/// <see cref="DrawOrFallback"/> and <see cref="MeasureOrFallback"/> are what a consumer's own base class
/// calls, where the fallback paint is the consumer's design and the closure is the price of that
/// flexibility.
/// </para>
/// </summary>
public static class UiSessionGuard
{
    // Test seam: lets FerriteLib.UiKit.Tests capture warning text without a real game log.
    internal static Action<string>? LogWarningOverride;

    /// <summary>
    /// Draws one widget, recovering into the standard band if it throws. The engine passes the element's
    /// node, so the recovery slot is the element - not a display path, which two elements can share.
    /// </summary>
    public static void DrawWidget(IUiWidget widget, Rect rect, UiWidgetContext ctx, UiNode node)
    {
        if (widget == null) throw new ArgumentNullException(nameof(widget));
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (node == null) throw new ArgumentNullException(nameof(node));

        GameFont previousFont = Text.Font;
        Color previousColor = GUI.color;
        try
        {
            widget.Draw(rect, ctx);
        }
        catch (Exception ex)
        {
            Record(ctx.Session, node, node.Path, widget.Kind, node.Path, ex);
            Text.Font = previousFont;
            GUI.color = previousColor;
            UiThemeDraw.RecoveryBand(rect, ctx.Theme, node.Path);
        }
    }

    /// <summary>
    /// Measures one widget, recovering to <paramref name="fallbackHeight"/>. Allocation-free twin of
    /// <see cref="DrawWidget"/>; see it for why it takes the widget and the node.
    /// </summary>
    public static float MeasureWidget(IUiWidget widget, UiWidgetContext ctx, UiNode node, float fallbackHeight)
    {
        if (widget == null) throw new ArgumentNullException(nameof(widget));
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (node == null) throw new ArgumentNullException(nameof(node));

        GameFont previousFont = Text.Font;
        Color previousColor = GUI.color;
        try
        {
            return widget.Measure(ctx);
        }
        catch (Exception ex)
        {
            Record(ctx.Session, node, node.Path, widget.Kind, node.Path, ex);
            Text.Font = previousFont;
            GUI.color = previousColor;
            return fallbackHeight;
        }
    }

    /// <summary>
    /// Guard for a consumer's own control, where the fallback paint is the consumer's design. The
    /// recovery slot is the node whose Draw is running (<see cref="UiSession.ActiveNode"/>, which the
    /// engine entered around this control); <paramref name="elementId"/> stays the label a diagnostic
    /// prints, because that is the consumer's own vocabulary.
    /// </summary>
    public static void DrawOrFallback(
        UiSession session,
        string elementId,
        string kind,
        string elementPath,
        Rect rect,
        Action draw,
        Action<Rect>? fallback)
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
            Record(session, session.ActiveNode, elementId, kind, elementPath, ex);
            Text.Font = previousFont;
            GUI.color = previousColor;
            fallback?.Invoke(rect);
        }
    }

    /// <summary>Measure-side twin of <see cref="DrawOrFallback"/>.</summary>
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
            Record(session, session.ActiveNode, elementId, kind, elementPath, ex);
            Text.Font = previousFont;
            GUI.color = previousColor;
            return fallbackHeight;
        }
    }

    /// <summary>
    /// Records one recovery against the node that owns it. <paramref name="label"/> is the name the
    /// diagnostic prints - the engine passes the element's display path, a consumer passes its own id -
    /// while <paramref name="key"/> is what decides which slot is occupied.
    /// </summary>
    private static void Record(UiSession session, UiNode key, string label, string kind, string elementPath, Exception ex)
    {
        bool first = !session.IsTripped(key);
        string diagnostic = BuildDiagnostic(label, kind, elementPath, ex);
        session.Trip(key, diagnostic);
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
