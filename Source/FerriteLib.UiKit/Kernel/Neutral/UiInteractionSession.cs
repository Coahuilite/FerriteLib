using System;

namespace FerriteLib.UiKit.Kernel;
/// <summary>Stable logical node and business target inside one interaction session.</summary>
public readonly struct UiInteractionTarget : IEquatable<UiInteractionTarget>
{
    public UiInteractionTarget(string node, string targetScope) : this(node, targetScope, "", null)
    {
    }

    public UiInteractionTarget(string node, string targetScope, string part, object? itemKey, string? businessScope = null)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        TargetScope = targetScope ?? throw new ArgumentNullException(nameof(targetScope));
        Part = part ?? throw new ArgumentNullException(nameof(part));
        ItemKey = itemKey;
        BusinessScope = businessScope ?? targetScope;
    }

    public string Node { get; }
    public string TargetScope { get; }
    public string BusinessScope { get; }
    public string Part { get; }
    public object? ItemKey { get; }

    public bool Equals(UiInteractionTarget other) => Node == other.Node && TargetScope == other.TargetScope && BusinessScope == other.BusinessScope && Part == other.Part && Equals(ItemKey, other.ItemKey);
    public override bool Equals(object? obj) => obj is UiInteractionTarget other && Equals(other);
    public override int GetHashCode() => (Node ?? "").GetHashCode() ^ (TargetScope ?? "").GetHashCode() ^ (Part ?? "").GetHashCode() ^ (ItemKey?.GetHashCode() ?? 0);
}

/// <summary>Neutral effective hit area. Coordinates are supplied in one space by the adapter.</summary>
public readonly struct UiHitGeometry
{
    public UiHitGeometry(float x, float y, float width, float height) : this(x, y, width, height, x, width)
    {
    }

    public UiHitGeometry(float x, float y, float width, float height, float travelStart, float travelLength)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        TravelStart = travelStart;
        TravelLength = travelLength;
    }

    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
    public float TravelStart { get; }
    public float TravelLength { get; }
    public bool IsValid => Width > 0 && Height > 0 && TravelLength > 0 && !float.IsNaN(X + Y + Width + Height + TravelStart + TravelLength) && !float.IsInfinity(X + Y + Width + Height + TravelStart + TravelLength);

    public bool Contains(float x, float y) => IsValid && x >= X && x < X + Width && y >= Y && y < Y + Height;
}

public enum UiPointerPhase
{
    None,
    Press,
    Drag,
    Release,
    Lost
}

public enum UiInteractionEnd
{
    None,
    Released,
    Ineligible,
    CaptureLost,
    Hidden,
    Removed,
    TargetChanged,
    Aborted,
    Closed
}

/// <summary>Platform facts; receive eligibility is distinct from continuing an owned capture.</summary>
public readonly struct UiInputEligibility
{
    public UiInputEligibility(bool enabled, bool writable = true, bool canReceive = true)
    {
        Enabled = enabled;
        Writable = writable;
        CanReceive = canReceive;
    }

    public bool Enabled { get; }
    public bool Writable { get; }
    public bool CanReceive { get; }
    public bool CanOperate => Enabled && Writable;
}

public readonly struct UiPointerSample
{
    public UiPointerSample(UiPointerPhase phase, float x, float y, bool consumed = false)
    {
        Phase = phase;
        X = x;
        Y = y;
        Consumed = consumed;
    }

    public UiPointerPhase Phase { get; }
    public float X { get; }
    public float Y { get; }
    public bool Consumed { get; }
}

public readonly struct UiRange
{
    public UiRange(float min, float max)
    {
        if (float.IsNaN(min) || float.IsNaN(max) || float.IsInfinity(min) || float.IsInfinity(max) || max < min)
            throw new ArgumentException("Range requires finite Min <= Max.");
        Min = min;
        Max = max;
    }

    public float Min { get; }
    public float Max { get; }

    public float Clamp(float value) => Math.Max(Min, Math.Min(Max, value));
    public float At(float x, UiHitGeometry travel) => Clamp(Min + (Max - Min) * ((x - travel.TravelStart) / travel.TravelLength));
}

/// <summary>Request is an operation, not business acceptance. Rendering reads this immutable result.</summary>
public readonly struct UiInteractionResult
{
    internal UiInteractionResult(bool request, float value, bool consume, bool captured, UiInteractionEnd ended)
    {
        Request = request;
        Value = value;
        Consume = consume;
        Captured = captured;
        Ended = ended;
    }

    public bool Request { get; }
    public float Value { get; }
    public bool Consume { get; }
    public bool Captured { get; }
    public UiInteractionEnd Ended { get; }
}

/// <summary>
/// The one activation/range state owner, used by default, independent and non-rendering clients.
/// Call semantic operations once per actual operation; None (repaint) never issues requests.
/// A session holds at most one pointer capture. The adapter applies capture/consume results.
/// </summary>
public sealed class UiInteractionSession : IDisposable
{
    private UiInteractionTarget? owner;
    private bool rangeCapture;
    private float lastSelection;
    private bool closed;
    public UiInteractionTarget? CaptureOwner => owner;
    public UiInteractionEnd LastEnd { get; private set; }

    public UiInteractionResult Activate(UiInteractionTarget target, UiInputEligibility eligibility)
    {
        EnsureOpen();
        LastEnd = UiInteractionEnd.None;
        return new UiInteractionResult(eligibility.CanOperate, 0, false, owner?.Equals(target) == true, LastEnd);
    }

    public UiInteractionResult Select(UiInteractionTarget target, UiRange range, float candidate, UiInputEligibility eligibility)
    {
        EnsureOpen();
        if (float.IsNaN(candidate) || float.IsInfinity(candidate))
            throw new ArgumentException("Selection must be finite.", nameof(candidate));
        LastEnd = UiInteractionEnd.None;
        return new UiInteractionResult(eligibility.CanOperate, range.Clamp(candidate), false, owner?.Equals(target) == true, LastEnd);
    }

    public UiInteractionResult PointerActivation(UiInteractionTarget target, UiPointerSample input, UiHitGeometry geometry, UiInputEligibility eligibility) => Pointer(target, input, geometry, eligibility, false, default, 0);
    public UiInteractionResult PointerRange(UiInteractionTarget target, UiPointerSample input, UiHitGeometry geometry, UiInputEligibility eligibility, UiRange range, float current) => Pointer(target, input, geometry, eligibility, true, range, current);
    private UiInteractionResult Pointer(UiInteractionTarget target, UiPointerSample input, UiHitGeometry geometry, UiInputEligibility eligibility, bool isRange, UiRange range, float current)
    {
        EnsureOpen();
        LastEnd = UiInteractionEnd.None;
        // Changing a business target or capability cannot inherit the previous press on this node.
        UiInteractionTarget heldTarget = owner.GetValueOrDefault();
        bool sameNode = owner.HasValue && heldTarget.Node == target.Node;
        bool businessTargetChanged = sameNode && heldTarget.BusinessScope != target.BusinessScope;
        bool samePartItem = sameNode && !businessTargetChanged
            && heldTarget.Part == target.Part && Equals(heldTarget.ItemKey, target.ItemKey);
        bool partTargetChanged = samePartItem
            && (heldTarget.TargetScope != target.TargetScope || rangeCapture != isRange);
        if (businessTargetChanged || partTargetChanged)
            End(UiInteractionEnd.TargetChanged);
        bool owns = owner.HasValue && owner.Value.Equals(target);
        if (owns && (!eligibility.CanOperate || input.Phase == UiPointerPhase.Lost))
        {
            End(eligibility.CanOperate ? UiInteractionEnd.CaptureLost : UiInteractionEnd.Ineligible);
            return Result(false, current);
        }

        if (!geometry.IsValid)
        {
            if (owns)
                End(UiInteractionEnd.Aborted);
            throw new InvalidOperationException("Pointer operation for '" + target.Node + "' requires valid effective geometry.");
        }

        if (input.Phase == UiPointerPhase.Press && !input.Consumed && !owner.HasValue && eligibility.CanOperate && eligibility.CanReceive && geometry.Contains(input.X, input.Y))
        {
            owner = target;
            rangeCapture = isRange;
            lastSelection = range.Clamp(current);
            if (!isRange)
                return Result(false, current, true);
            return RangeSample(range.At(input.X, geometry), true);
        }

        if (!owns)
            return new UiInteractionResult(false, current, false, false, LastEnd);
        if (input.Phase == UiPointerPhase.Release)
        {
            bool activate = !input.Consumed && !isRange && eligibility.CanReceive && geometry.Contains(input.X, input.Y);
            End(UiInteractionEnd.Released);
            return Result(activate, current, true);
        }

        if (!input.Consumed && isRange && input.Phase == UiPointerPhase.Drag)
            return RangeSample(range.At(input.X, geometry), true);
        return Result(false, current);
    }

    private UiInteractionResult RangeSample(float candidate, bool consume)
    {
        bool changed = candidate != lastSelection;
        lastSelection = candidate;
        return Result(changed, candidate, consume);
    }

    private UiInteractionResult Result(bool request, float value, bool consume = false) => new UiInteractionResult(request, value, consume, owner.HasValue, LastEnd);
    public void End(UiInteractionEnd reason)
    {
        if (!owner.HasValue)
            return;
        owner = null;
        LastEnd = reason;
    }

    public void EndNode(string node, UiInteractionEnd reason)
    {
        if (owner.HasValue && owner.Value.Node == node)
            End(reason);
    }

    public void Dispose()
    {
        End(UiInteractionEnd.Closed);
        closed = true;
    }

    private void EnsureOpen()
    {
        if (closed)
            throw new ObjectDisposedException(nameof(UiInteractionSession));
    }
}
