namespace FerriteLib.UiKit.Kernel;

public sealed partial class UiStyleDocument
{
    static partial void ReportLegacyFont(string scheme, string font)
        => UiFitAudit.ReportStyleFallback("scheme:" + scheme, "styles", "Font", font, "Metric Token=\"Font\"");
}
