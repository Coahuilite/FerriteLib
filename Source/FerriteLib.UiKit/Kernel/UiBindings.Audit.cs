namespace FerriteLib.UiKit.Kernel;

// Platform audit adapter: the neutral binding production source has no platform dependency.
public sealed partial class UiBindings
{
    partial void ReportStyleFallback(string path, string kind, string attribute, string supplied, string fallback)
        => UiFitAudit.ReportStyleFallback(path, kind, attribute, supplied, fallback);
}
