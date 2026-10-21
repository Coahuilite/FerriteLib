namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Opt-in complete measurement contract. Capture only real measure inputs, without effects; Calculate
/// uses the captured values and may be skipped when they and the declared metric environment match.
/// Notify existing binding keys or MarkDirty when external inputs change. Legacy IUiWidget is unchanged.
/// </summary>
public interface IUiMeasureParticipant
{
    UiMeasurement CaptureMeasurement(UiWidgetContext context);
}
