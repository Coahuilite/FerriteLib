namespace FerriteLib.UiKit.Kernel;

/// <summary>Wrapper adapter for read-only facts. Calling this does not paint, capture, or issue requests.</summary>
public interface IUiDescriptiveWidget
{
    UiDescription Describe(UiWidgetContext context);
}
