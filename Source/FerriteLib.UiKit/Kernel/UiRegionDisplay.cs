using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

public interface IUiRegionParticipant
{
    UiRegionDisplay Build(UiWidgetContext context, UiRegionSnapshot inputs);
    void Operate(UiLayoutRect bounds, UiWidgetContext context, UiRegionDisplay display);
    void Paint(UiLayoutRect bounds, UiWidgetContext context, UiRegionDisplay display);
}

/// <summary>Immutable pure presentation data. Pointer regions and PaintClip use widget-local coordinates.</summary>
public abstract class UiRegionDisplay
{
    protected UiRegionDisplay(float naturalHeight, float? labelWidth, IReadOnlyList<UiPointerRegion> pointerRegions,
        UiLayoutRect? paintClip = null)
    {
        if (!Nonnegative(naturalHeight)) throw new ArgumentOutOfRangeException(nameof(naturalHeight));
        if (labelWidth.HasValue && !Nonnegative(labelWidth.Value)) throw new ArgumentOutOfRangeException(nameof(labelWidth));
        if (pointerRegions == null) throw new ArgumentNullException(nameof(pointerRegions));
        var copy = new UiPointerRegion[pointerRegions.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = pointerRegions[i]; UiLayoutRect r = copy[i].Bounds;
            if (!Finite(r.X) || !Finite(r.Y) || !Nonnegative(r.Width) || !Nonnegative(r.Height))
                throw new ArgumentOutOfRangeException(nameof(pointerRegions));
        }
        if (paintClip.HasValue)
        {
            UiLayoutRect r = paintClip.Value;
            if (!Finite(r.X) || !Finite(r.Y) || !Nonnegative(r.Width) || !Nonnegative(r.Height))
                throw new ArgumentOutOfRangeException(nameof(paintClip));
        }
        NaturalHeight = naturalHeight; LabelWidth = labelWidth; PointerRegions = Array.AsReadOnly(copy); PaintClip = paintClip;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Nonnegative(float value) => Finite(value) && value >= 0;
    public float NaturalHeight { get; }
    public float? LabelWidth { get; }
    public IReadOnlyList<UiPointerRegion> PointerRegions { get; }
    public UiLayoutRect? PaintClip { get; }
}

public enum UiRegionState { Active, Unknown, Fallback, Failed, Detached, Closed }

/// <summary>Detached last-observed region facts, not a request to refresh the source.</summary>
public sealed class UiRegionStatus
{
    internal UiRegionStatus(string root, string? mount, UiRegionState state, string reason,
        long? source, long? layout, long? paint, bool? current)
    { RootElementId = root; MountId = mount; State = state; Reason = reason; SourceVersion = source; LayoutVersion = layout; LastPaintPass = paint; InputsCurrent = current; }
    public string RootElementId { get; }
    public string? MountId { get; }
    public UiRegionState State { get; }
    public string Reason { get; }
    public long? SourceVersion { get; }
    public long? LayoutVersion { get; }
    public long? LastPaintPass { get; }
    public bool? InputsCurrent { get; }
}
