namespace FerriteLib.UiKit.Kernel;
/// <summary>A wrapper's actual label demand, without exposing its private controls.</summary>
public interface IUiLabelledWidget
{
    float MeasureLabel(UiWidgetContext context);
}

/// <summary>A wrapper reports a local allocation problem without making the parent arrange its private parts.</summary>
internal interface IUiLayoutDiagnosticSource
{
    UiLayoutDiagnostic? DiagnoseLayout(UiWidgetContext context, float allocatedHeight);
}


