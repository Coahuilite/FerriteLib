using System;
namespace FerriteLib.UiKit.Kernel;
internal static class UiMath
{
    internal static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    internal static float Clamp01(float value) => Clamp(value, 0, 1);
    internal static int CeilToInt(float value) => (int)Math.Ceiling(value);
}



