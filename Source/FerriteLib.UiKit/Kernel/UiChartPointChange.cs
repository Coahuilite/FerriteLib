namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Typed drag result emitted by an editable <c>chart/line</c> widget. <see cref="Index"/> is the
/// control-point index in the normalized point list; X/Y are the new normalized (0..1) coordinates.
/// </summary>
public readonly struct UiChartPointChange
{
    public readonly int Index;

    public readonly float X;

    public readonly float Y;

    public UiChartPointChange(int index, float x, float y)
    {
        Index = index;
        X = x;
        Y = y;
    }
}
