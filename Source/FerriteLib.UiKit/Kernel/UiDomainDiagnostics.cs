using System;

namespace FerriteLib.UiKit.Kernel;

// Optional outbound observation, not an input, business result or second fact buffer.
internal enum UiDiagnosticLevel { Normal, Warning, Error }

internal interface IUiDiagnosticOutput : IDisposable
{
    bool IsActive { get; }
    bool DetailEnabled { get; }
    void BindSession(int identity);
    void Publish(UiDomainDiagnostic fact, UiDiagnosticMoment moment);
}

internal readonly struct UiDomainDiagnostic
{
    internal UiDomainDiagnostic(string code, UiDiagnosticLevel level, string node, string category,
        string impact, string next, UiOverflowReport? overflow = null, UiStyleFallbackReport? fallback = null,
        UiDiagnosticTiming? timing = null, string detail = "")
    { Code=code; Level=level; Node=node; Category=category; Impact=impact; Next=next;
      Overflow=overflow; Fallback=fallback; Timing=timing; Detail=detail; }
    internal string Code { get; }
    internal UiDiagnosticLevel Level { get; }
    internal string Node { get; }
    internal string Category { get; }
    internal string Impact { get; }
    internal string Next { get; }
    internal UiOverflowReport? Overflow { get; }
    internal UiStyleFallbackReport? Fallback { get; }
    internal UiDiagnosticTiming? Timing { get; }
    internal string Detail { get; }
}

// Detached values sampled at publication, never reconstructed during a later drain.
internal readonly struct UiDiagnosticMoment
{
    internal UiDiagnosticMoment(UiSession? session)
    {
        Sampled = session?.CurrentExecution != null;
        Pass = session?.Frame ?? 0;
        Phase = session?.Input.Phase ?? UiFramePhase.None;
        RawPhase = session?.Input.RawPhase ?? UiFramePhase.None;
        Consumed = session?.Input.Consumed ?? false;
        ContentRevision = session?.ContentRevision ?? -1;
        DefinitionRevision = session?.DefinitionRevision ?? -1;
        ArrangedContentRevision = session?.DiagnosticArrangedContentRevision ?? -1;
        ArrangedDefinitionRevision = session?.DiagnosticArrangedDefinitionRevision ?? -1;
        Dirty = session?.HasDirtyNodes ?? false;
    }
    internal bool Sampled { get; }
    internal int Pass { get; }
    internal UiFramePhase Phase { get; }
    internal UiFramePhase RawPhase { get; }
    internal bool Consumed { get; }
    internal int ContentRevision { get; }
    internal int DefinitionRevision { get; }
    internal int ArrangedContentRevision { get; }
    internal int ArrangedDefinitionRevision { get; }
    internal bool Dirty { get; }
}

internal static class UiDomainDiagnostics
{
    internal static bool OwnsOutput(IUiDiagnosticOutput? output) => output?.IsActive == true;

    internal static void Publish(IUiDiagnosticOutput? output, UiSession? session, UiDomainDiagnostic fact)
    {
        if (!OwnsOutput(output)) return;
        try
        {
            if (fact.Level == UiDiagnosticLevel.Normal && !output!.DetailEnabled) return;
            output!.Publish(fact, output.DetailEnabled ? new UiDiagnosticMoment(session) : default);
        }
        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error))
        { session?.ReportObservationFailure("domain diagnostic output failed", error); }
    }

    internal static void Overflow(IUiDiagnosticOutput? output, UiSession? session, UiOverflowReport report)
    {
        if (!OwnsOutput(output)) return;
        Publish(output, session, new UiDomainDiagnostic("fit.overflow", UiDiagnosticLevel.Warning,
            report.ElementPath, report.Axis + "/" + UiTextFonts.FontOf(report), "text-clipped", "review-control-budget", overflow: report));
    }

    internal static void Style(IUiDiagnosticOutput? output, UiSession? session, UiStyleFallbackReport report)
    {
        if (!OwnsOutput(output)) return;
        Publish(output, session, new UiDomainDiagnostic("fit.style-fallback", UiDiagnosticLevel.Warning,
            report.ElementPath, report.Kind + "/" + report.Attribute, "fallback-appearance", "review-style-or-host-capability", fallback: report));
    }

    internal static void Reactive(UiSession session, UiNode? node, string code, UiReactiveFault? fault)
    {
        if (fault == null || !OwnsOutput(session.DiagnosticOutput)) return;
        Publish(session.DiagnosticOutput, session, new UiDomainDiagnostic(code, UiDiagnosticLevel.Warning,
            node?.Id.Key ?? "unknown", fault.InnerException?.GetType().Name ?? nameof(UiReactiveFault),
            "pure-presentation-unavailable", "change-related-input-or-explicit-retry"));
    }
}
