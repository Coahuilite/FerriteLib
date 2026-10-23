namespace FerriteLib.UiKit.Kernel;

/// <summary>Stable registered vocabulary, independent of private default implementation types.</summary>
public static class UiKinds
{
    public const string Button = "input/button", Checkbox = "input/checkbox", ModeRow = "input/mode-row",
        Dropdown = "input/dropdown", TextField = "input/text-field", NumberField = "input/number-field",
        Slider = "input/slider", StepperSlider = "input/stepper-slider", WrappedText = "text/wrapped",
        SectionHeader = "section/header", Banner = "chrome/banner", Empty = "state/empty", Rule = "chrome/rule",
        Image = "display/image", Progress = "display/progress", Tree = "container/tree", LineChart = "chart/line", Repeat = "Repeat";
}
