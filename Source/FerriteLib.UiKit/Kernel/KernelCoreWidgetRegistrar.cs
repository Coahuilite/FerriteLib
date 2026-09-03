namespace FerriteLib.UiKit.Kernel;

/// <summary>Explicit registration for greenfield core widget kinds.</summary>
public static class KernelCoreWidgetRegistrar
{
    public static void RegisterAll()
    {
        Widgets.StepperSliderWidget.Register();
        Widgets.ChromeBannerWidget.Register();
        Widgets.InputModeRowWidget.Register();
        Widgets.DropdownWidget.Register();
        Widgets.SectionHeaderWidget.Register();
        Widgets.LineChartWidget.Register();
        Widgets.EmptyStateWidget.Register();
    }
}
