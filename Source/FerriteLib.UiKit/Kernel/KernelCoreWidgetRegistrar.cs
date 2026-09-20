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
        // Leaf atoms: the vocabulary a manifest reaches for when it needs one of the things a
        // consumer would otherwise hand-roll inside its own kind.
        Widgets.WrappedTextWidget.Register();
        Widgets.ButtonWidget.Register();
        Widgets.RuleWidget.Register();
        Widgets.SliderWidget.Register();
        Widgets.NumberFieldWidget.Register();
        Widgets.TextFieldWidget.Register();
        // 0.5 common controls and the collection element. The three controls are ordinary kinds; Repeat is
        // a container the engine materializes, registered here because its declaration (attribute schema,
        // Items/Template contract) still needs the creation-time owner every element has.
        Widgets.CheckboxWidget.Register();
        Widgets.ProgressWidget.Register();
        Widgets.TreeWidget.Register();
        Widgets.RepeatTemplateWidget.Register();
    }
}
