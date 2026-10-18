namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Typed drag result emitted by an editable <c>chart/line</c> widget. <see cref="Index"/> is the
/// current control-point index in the normalized point list; X/Y are the new normalized (0..1)
/// coordinates. Key is present only when PointKeysBind explicitly supplies stable identities.
/// </summary>
public readonly struct UiChartPointChange
{
    public readonly int Index;

    public readonly float X;

    public readonly float Y;
    public string? Key { get; }

    public UiChartPointChange(int index, float x, float y) : this(index, x, y, null) { }

    public UiChartPointChange(int index, float x, float y, string? key)
    {
        Index = index;
        X = x;
        Y = y;
        Key = key;
    }
}
