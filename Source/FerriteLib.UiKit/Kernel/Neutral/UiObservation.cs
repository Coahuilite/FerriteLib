using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
public enum UiObservationStatus
{
    Sampled,
    NotFound,
    Unsupported,
    NotSampled,
    Stale,
    Failed
}

/// <summary>Hard output budgets; a read never materializes another runtime tree.</summary>
public sealed class UiObservationBudget
{
    public UiObservationBudget(int nodes = 64, int depth = 8, int text = 512, int items = 32)
    {
        if (nodes < 1 || nodes > 512 || depth < 0 || depth > 32 || text < 1 || text > 4096 || items < 1 || items > 128)
            throw new ArgumentOutOfRangeException(nameof(nodes), "Observation budgets exceed the bounded read contract.");
        Nodes = nodes;
        Depth = depth;
        Text = text;
        Items = items;
    }

    public int Nodes { get; }
    public int Depth { get; }
    public int Text { get; }
    public int Items { get; }

    public string Limit(string value) => value.Length <= Text ? value : value.Substring(0, Text);
}

public sealed class UiObservedFact
{
    public UiObservedFact(string name, string value, UiObservationStatus status = UiObservationStatus.Sampled, string source = "", string reason = "", bool truncated = false)
    {
        Name = name;
        Value = value;
        Status = status;
        Source = source;
        Reason = reason;
        Truncated = truncated;
    }

    public string Name { get; }
    public string Value { get; }
    public UiObservationStatus Status { get; }
    public string Source { get; }
    public string Reason { get; }
    public bool Truncated { get; }
}

/// <summary>Detached read data, not handles to a node, editor, model or command.</summary>
public sealed class UiObservedNode
{
    public UiObservedNode(string key, string path, string kind, string? parent, int depth, IReadOnlyList<UiObservedFact> facts)
    {
        Key = key;
        Path = path;
        Kind = kind;
        Parent = parent;
        Depth = depth;
        Facts = facts;
    }

    public string Key { get; }
    public string Path { get; }
    public string Kind { get; }
    public string? Parent { get; }
    public int Depth { get; }
    public IReadOnlyList<UiObservedFact> Facts { get; }
}

public sealed class UiHostObservation
{
    public UiHostObservation(int host, string source, int frame, int definition, int? layoutDefinition, DateTime sampledAtUtc, UiObservationStatus status, string reason, bool truncated, IReadOnlyList<UiObservedNode> nodes, IReadOnlyList<UiObservedFact>? facts = null)
    {
        Host = host;
        Source = source;
        Frame = frame;
        Definition = definition;
        LayoutDefinition = layoutDefinition;
        SampledAtUtc = sampledAtUtc;
        Status = status;
        Reason = reason;
        Truncated = truncated;
        Nodes = nodes;
        Facts = facts ?? Array.Empty<UiObservedFact>();
    }

    public int Host { get; }
    public string Source { get; }
    public int Frame { get; }
    public int Definition { get; }
    public int? LayoutDefinition { get; }
    public DateTime SampledAtUtc { get; }
    public UiObservationStatus Status { get; }
    public string Reason { get; }
    public bool Truncated { get; }
    public IReadOnlyList<UiObservedNode> Nodes { get; }
    public IReadOnlyList<UiObservedFact> Facts { get; }
}

/// <summary>Bounded live-instance discovery, including an empty but incomplete result.</summary>
public sealed class UiHostDiscovery
{
    public UiHostDiscovery(IReadOnlyList<UiHostObservation> hosts, bool truncated, int historicalCapacityRefusals)
    {
        Hosts = hosts;
        Truncated = truncated;
        HistoricalCapacityRefusals = historicalCapacityRefusals;
    }

    public IReadOnlyList<UiHostObservation> Hosts { get; }
    public bool Truncated { get; }
    public int HistoricalCapacityRefusals { get; }
}
