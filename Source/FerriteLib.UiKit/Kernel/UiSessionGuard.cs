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
        => DrawWidgetCore(widget, rect, ctx, node, null, false, false, false);

    internal static void DrawWidgetForEvent(IUiWidget widget, UiLayoutRect rect, UiWidgetContext ctx, UiNode node,
        IUiIsolatedPaint paint, bool clipped, bool tiered, bool repaint)
        => DrawWidgetCore(widget, rect, ctx, node, paint, clipped, tiered, repaint);

    private static void DrawWidgetCore(IUiWidget widget, UiLayoutRect rect, UiWidgetContext ctx, UiNode node,
        IUiIsolatedPaint? paint, bool clipped, bool tiered, bool repaint)
    {
        if (widget == null) throw new ArgumentNullException(nameof(widget));
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (node == null) throw new ArgumentNullException(nameof(node));

        using UiBindings.DisplayScope? display = UiWidgetCapabilities.Description(widget) != null && UiWidgetCapabilities.Pointer(widget) == null ? UiBindings.OwnerOf(ctx.Bindings).DisplayOnly() : null;
        try
        {
            UiDisplayResult result = UiDisplayResult.Completed;
            ctx.Session.Execution.WithState(() =>
            {
                if (paint != null && widget is IUiDeferredDisplay deferred)
                    result = deferred.DrawForEvent(rect, ctx, clipped, tiered, repaint, paint);
                else widget.Draw(rect, ctx);
            });
            if (result == UiDisplayResult.Deferred) return;
            node.PresentationFault = display?.Fault;
            node.PresentationUnavailable = false;
            UiDomainDiagnostics.Reactive(ctx.Session, node, "presentation.fault", node.PresentationFault);
            if (node.ReactiveFault != null)
            {
                void MarkFailure() => UiThemeDraw.FailureMarker(ctx.Drawing,rect, ctx.Theme, node.ReactiveFault.Identity);
                if (paint == null) MarkFailure(); else paint.IsolatePaint(MarkFailure);
            }
        }
        catch (UiReactiveFault failure)
        {
            node.PresentationFault = failure; node.PresentationUnavailable = true;
            UiDomainDiagnostics.Reactive(ctx.Session, node, "presentation.fault", failure);
            node.Selection = null;
            ctx.Session.SuspendFailedInput(node);
            void Recover()
            {
                if (widget is IUiRetainedDisplay last && last.PaintLast(rect, ctx))
                    UiThemeDraw.FailureMarker(ctx.Drawing,rect, ctx.Theme, failure.Identity);
                else UiThemeDraw.RecoveryBand(ctx.Drawing,rect, ctx.Theme, failure.Identity);
            }
            if (paint == null) Recover(); else paint.IsolatePaint(Recover);
        }
        catch (UiRequestFailure failure)
        {
            ctx.Session.RecordRequestFailure(node, failure);
            void Recover() => UiThemeDraw.RecoveryBand(ctx.Drawing,rect, ctx.Theme, node.Path);
            if (paint == null) Recover(); else paint.IsolatePaint(Recover);
        }
        catch (Exception ex) when (ex is not UiReactiveFault)
        {
            Record(ctx.Session, node, node.Path, widget.Kind, node.Path, ex);
            void Recover() => UiThemeDraw.RecoveryBand(ctx.Drawing,rect, ctx.Theme, node.Path);
            if (paint == null) Recover(); else paint.IsolatePaint(Recover);
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
            UiDomainDiagnostics.Reactive(session, ctx.Node, "presentation.fault", failure);
            fallback?.Invoke(rect);
        }
        catch (UiRequestFailure failure)
        {
            session.RecordRequestFailure(ctx.Node, failure);
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

        // A speculative budget must fail with its original cause, not trip/suspend a committed owner
        // or make recovery geometry look like a successful measurement.
        if (session.IsBudgetMeasuring) return measure();

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
    internal static void RecordRegionFailure(UiWidgetContext ctx, IUiWidget widget, Exception error)
    {
        if (error is UiReactiveFault failure)
        {
            ctx.Node.PresentationFault = failure;
            UiDomainDiagnostics.Reactive(ctx.Session, ctx.Node, "presentation.fault", failure);
        }
        else Record(ctx.Session, ctx.Node, ctx.ElementPath, widget.Kind, ctx.ElementPath, error);
    }

    private static void Record(UiSession session, UiNode key, string label, string kind, string elementPath, Exception ex)
    {
        bool first = !session.IsTripped(key);
        string diagnostic;
        try { diagnostic = BuildDiagnostic(label, kind, elementPath, ex); }
        catch (Exception reporting) when (UiObservationBoundary.IsOrdinary(reporting))
        {
            diagnostic = "[FerriteLib.UiKit] fallback triggered for component '" + label
                + "' (Kind='" + kind + "', path='" + elementPath + "'): " + ex.GetType().FullName
                + " (exception diagnostic formatting failed)";
            session.ReportObservationFailure("recovery diagnostic formatting failed", reporting);
        }
        session.Trip(key, diagnostic);
        if (first)
        {
            session.ReportCleanupWarning(diagnostic);

            // The session-scoped record is where the trip is attributed: the session owns the subscription,
            // so one window's recovery lands on that window's subscriber and on nobody else's. A session
            // with no subscription no-ops, which is why this adds nothing to the unsubscribed path.
            try { UiDiagnosticHub.PublishRecovery(session, key, kind, elementPath, diagnostic); }
            catch (Exception reporting) when (UiObservationBoundary.IsOrdinary(reporting))
            { session.ReportObservationFailure("recovery diagnostic publication failed", reporting); }
            UiDomainDiagnostics.Publish(session.DiagnosticOutput, session, new UiDomainDiagnostic("recovery.trip", UiDiagnosticLevel.Warning,
                key.Id.Key, kind + "/" + ex.GetType().Name, "recovery-display-input-suspended", "inspect-original-fault-before-reopening"));
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
