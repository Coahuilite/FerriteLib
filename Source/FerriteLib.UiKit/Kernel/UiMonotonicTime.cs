using System.Diagnostics;
namespace FerriteLib.UiKit.Kernel;
internal sealed class UiMonotonicTime : IUiTimeSource
{
    internal static readonly UiMonotonicTime Instance = new UiMonotonicTime();
    public double NowSeconds => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
}
