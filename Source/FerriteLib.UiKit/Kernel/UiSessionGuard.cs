using System;

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
    

    /// <summary>Restores native paint state while leaving a shell-background failure to the existing window recovery boundary.</summary>
    internal static void DrawWindowBackground(IUiExecution execution, UiLayoutRect rect, Action<UiLayoutRect> draw)
    {
        execution.WithState(() => draw(rect));
    }

    /// <summary>
    /// Draws one widget, recovering into the standard band if it throws. The engine passes the element's
    /// node, so the recovery slot is the element - not a display path, which two elements can share.
    /// </summary>
    public static void DrawWidget(IUiWidget widget, UiLayoutRect rect, UiWidgetContext ctx, UiNode node)
    {
        if (widget == null) throw new ArgumentNullException(nameof(widget));
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (node == null) throw new ArgumentNullException(nameof(node));

        using UiBindings.DisplayScope? display = UiWidgetCapabilities.Description(widget) != null && UiWidgetCapabilities.Pointer(widget) == null ? UiBindings.OwnerOf(ctx.Bindings).DisplayOnly() : null;
        try
        {
            ctx.Session.Execution.WithState(() => widget.Draw(rect, ctx));
            node.PresentationFault = display?.Fault;
            node.PresentationUnavailable = false;
            if (node.ReactiveFault != null)
                UiThemeDraw.FailureMarker(ctx.Drawing,rect, ctx.Theme, node.ReactiveFault.Identity);
        }
        catch (UiReactiveFault failure)
        {
            node.PresentationFault = failure; node.PresentationUnavailable = true;
            node.Selection = null;
            ctx.Session.SuspendFailedInput(node);
            if (widget is IUiRetainedDisplay last && last.PaintLast(rect, ctx))
                UiThemeDraw.FailureMarker(ctx.Drawing,rect, ctx.Theme, failure.Identity);
            else UiThemeDraw.RecoveryBand(ctx.Drawing,rect, ctx.Theme, failure.Identity);
        }
        catch (UiRequestFailure)
        {
            UiThemeDraw.RecoveryBand(ctx.Drawing,rect, ctx.Theme, node.Path);
        }
        catch (Exception ex) when (ex is not UiReactiveFault)
        {
            Record(ctx.Session, node, node.Path, widget.Kind, node.Path, ex);
            UiThemeDraw.RecoveryBand(ctx.Drawing,rect, ctx.Theme, node.Path);
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

        try
        {
            return widget.Measure(ctx);
        }
        catch (Exception ex) when (ex is not UiReactiveFault)
        {
            Record(ctx.Session, node, node.Path, widget.Kind, node.Path, ex);
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
        UiWidgetContext ctx,
        string elementId,
        string kind,
        string elementPath,
        UiLayoutRect rect,
        Action draw,
        Action<UiLayoutRect>? fallback)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        UiSession session = ctx.Session;
        if (string.IsNullOrEmpty(elementId)) throw new ArgumentException("Element id is required.", nameof(elementId));
        if (draw == null) throw new ArgumentNullException(nameof(draw));

        try
        {
            session.Execution.WithState(draw);
        }
        catch (UiReactiveFault failure)
        {
            ctx.Node.PresentationFault = failure;
            ctx.Node.PresentationUnavailable = true;
            fallback?.Invoke(rect);
        }
        catch (UiRequestFailure)
        {
            fallback?.Invoke(rect);
        }
        catch (Exception ex)
        {
            Record(session, ctx.Node, elementId, kind, elementPath, ex);
            fallback?.Invoke(rect);
        }
    }

    /// <summary>Measure-side twin of <see cref="DrawOrFallback"/>.</summary>
    public static float MeasureOrFallback(
        UiWidgetContext ctx,
        string elementId,
        string kind,
        string elementPath,
        float fallbackHeight,
        Func<float> measure)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        UiSession session = ctx.Session;
        if (string.IsNullOrEmpty(elementId)) throw new ArgumentException("Element id is required.", nameof(elementId));
        if (measure == null) throw new ArgumentNullException(nameof(measure));

        try
        {
            return measure();
        }
        catch (Exception ex) when (ex is not UiReactiveFault)
        {
            Record(session, ctx.Node, elementId, kind, elementPath, ex);
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
            session.ReportWarning(diagnostic);

            // The session-scoped record is where the trip is attributed: the session owns the subscription,
            // so one window's recovery lands on that window's subscriber and on nobody else's. A session
            // with no subscription no-ops, which is why this adds nothing to the unsubscribed path.
            UiDiagnosticHub.PublishRecovery(session, key, kind, elementPath, diagnostic);
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

}
