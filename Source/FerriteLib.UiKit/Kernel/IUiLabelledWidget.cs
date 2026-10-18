namespace FerriteLib.UiKit.Kernel;
/// <summary>A wrapper's actual label demand, without exposing its private controls.</summary>
public interface IUiLabelledWidget
{
    float MeasureLabel(UiWidgetContext context);
}
