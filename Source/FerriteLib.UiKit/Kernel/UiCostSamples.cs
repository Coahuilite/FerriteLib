using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Projection of the author's existing sampler, not a library sampling state machine.</summary>
public enum UiCostSampleState { NotSampled, Requested, Sampling, Complete, Partial }

public sealed class UiCostSampleRequest
{
    public UiCostSampleRequest(int passes)
    {
        if (passes < 1 || passes > 256) throw new ArgumentOutOfRangeException(nameof(passes), "Request 1..256 owner passes; there is no library default.");
        Passes = passes;
    }
    public int Passes { get; }
}

/// <summary>Optional capability supplied by the actual window. Neither call creates a sampler.</summary>
public interface IUiCostSampleProvider
{
    /// <summary>Called at most once per queued request on the original Host thread. The owner may decline.</summary>
    bool RequestSample(UiCostSampleRequest request);
    /// <summary>Pure projection of already retained owner facts. Must not begin, drain, advance or reset sampling.</summary>
    UiCostSampleSnapshot ReadSample();
}

/// <summary>One author-classified original accumulator. Unknown values are null, not measured zero.</summary>
public sealed class UiCostMetric
{
    public UiCostMetric(string category, string source, UiObservationStatus status, long? calls = null,
        double? totalMilliseconds = null, double? maximumMilliseconds = null)
    {
        Category = UiCostSampleSnapshot.Limit(category); Source = UiCostSampleSnapshot.Limit(source);
        Truncated = Category != category || Source != source;
        if (calls < 0 || !Valid(totalMilliseconds) || !Valid(maximumMilliseconds)) throw new ArgumentOutOfRangeException(nameof(calls));
        Status = status; Calls = calls; TotalMilliseconds = totalMilliseconds; MaximumMilliseconds = maximumMilliseconds;
    }
    private static bool Valid(double? value) => !value.HasValue || (value.Value >= 0 && !double.IsInfinity(value.Value) && !double.IsNaN(value.Value));
    public string Category { get; }
    public string Source { get; }
    public UiObservationStatus Status { get; }
    public long? Calls { get; }
    public double? TotalMilliseconds { get; }
    public double? MaximumMilliseconds { get; }
    public bool Truncated { get; }
}

/// <summary>Detached bounded original-owner facts. Counts are owner passes, never inferred render frames/FPS.</summary>
public sealed class UiCostSampleSnapshot
{
    public UiCostSampleSnapshot(UiObservationStatus status, string source, UiCostSampleState? state = null,
        int? session = null, long? sample = null, string page = "", string scene = "", UiLayoutPoint? size = null,
        int? definition = null, int? revision = null, int? requestedPasses = null, int? completedPasses = null,
        long? firstPass = null, long? lastPass = null, bool? sceneChanged = null, string endReason = "",
        IReadOnlyList<UiCostMetric>? metrics = null, IReadOnlyList<UiObservedFact>? environment = null)
    {
        Status = status; State = state; Session = session; Sample = sample; Size = size;
        Source = Limit(source); Page = Limit(page); Scene = Limit(scene); EndReason = Limit(endReason);
        Truncated = Source != source || Page != page || Scene != scene || EndReason != endReason;
        Definition = definition; Revision = revision; RequestedPasses = requestedPasses; CompletedPasses = completedPasses;
        FirstPass = firstPass; LastPass = lastPass; SceneChanged = sceneChanged;
        var metricCopy = new List<UiCostMetric>();
        if (metrics != null)
        {
            for (int i = 0; i < metrics.Count && i < 64; i++)
            { metricCopy.Add(metrics[i]); Truncated |= metrics[i].Truncated; }
            Truncated |= metrics.Count > 64;
        }
        Metrics = metricCopy.AsReadOnly();
        var factCopy = new List<UiObservedFact>();
        if (environment != null)
        {
            for (int i = 0; i < environment.Count && i < 64; i++)
            {
                UiObservedFact fact = environment[i];
                string name = Limit(fact.Name), value = Limit(fact.Value), origin = Limit(fact.Source), reason = Limit(fact.Reason);
                bool truncated = fact.Truncated || name != fact.Name || value != fact.Value || origin != fact.Source || reason != fact.Reason;
                Truncated |= truncated;
                factCopy.Add(new UiObservedFact(name, value, fact.Status, origin, reason, truncated));
            }
            Truncated |= environment.Count > 64;
        }
        Environment = factCopy.AsReadOnly();
    }
    internal static string Limit(string text) => text.Length <= 512 ? text : text.Substring(0, 512);
    public UiObservationStatus Status { get; }
    public string Source { get; }
    public UiCostSampleState? State { get; }
    public int? Session { get; }
    public long? Sample { get; }
    public string Page { get; }
    public string Scene { get; }
    public UiLayoutPoint? Size { get; }
    public int? Definition { get; }
    public int? Revision { get; }
    public int? RequestedPasses { get; }
    public int? CompletedPasses { get; }
    public long? FirstPass { get; }
    public long? LastPass { get; }
    public bool? SceneChanged { get; }
    public string EndReason { get; }
    public IReadOnlyList<UiCostMetric> Metrics { get; }
    public IReadOnlyList<UiObservedFact> Environment { get; }
    public bool Truncated { get; }
}

// An attachment incarnation is held only by this Host/connection. Detach/re-attach cannot revive a request.
internal sealed class UiCostSampleAttachment
{
    internal UiCostSampleAttachment(IUiCostSampleProvider provider) { Provider = provider; }
    internal IUiCostSampleProvider Provider { get; }
}
