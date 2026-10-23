using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>One composite's local geometry and shortage facts; it never enlarges the parent's allocation.</summary>
internal readonly struct UiStepperLayout
{
    private UiStepperLayout(UiLayoutRect minus, UiLayoutRect range, UiLayoutRect field, UiLayoutRect plus,
        float requiredWidth, float availableWidth)
    {
        Minus = minus;
        Range = range;
        Field = field;
        Plus = plus;
        RequiredWidth = requiredWidth;
        AvailableWidth = availableWidth;
    }
    public UiLayoutRect Minus { get; }
    public UiLayoutRect Range { get; }
    public UiLayoutRect Field { get; }
    public UiLayoutRect Plus { get; }
    public float RequiredWidth { get; }
    public float AvailableWidth { get; }
    public float Shortage => Math.Max(0, RequiredWidth - AvailableWidth);

    public static UiStepperLayout Arrange(float width, float height, UiSliderLayout label,
        float buttonWidth, float fieldWidth, float gap, float stackedLabelGap)
    {
        float x = label.LabelWidth > 0 && !label.Stacked ? label.LabelWidth + gap : 0;
        float y = label.LabelWidth > 0 && label.Stacked ? label.LabelHeight + stackedLabelGap : 0;
        float controlHeight = Math.Max(1, height - y);
        float requiredWidth = x + buttonWidth * 2 + fieldWidth + gap * 3 + 1;
        UiLayoutRect minus = new(x, y, buttonWidth, controlHeight);
        x += buttonWidth + gap;
        float rangeWidth = Math.Max(1, width - x - fieldWidth - gap * 2 - buttonWidth);
        UiLayoutRect range = new(x, y, rangeWidth, controlHeight);
        x += rangeWidth + gap;
        UiLayoutRect field = new(x, y, fieldWidth, controlHeight);
        x += fieldWidth + gap;
        UiLayoutRect plus = new(x, y, buttonWidth, controlHeight);
        return new UiStepperLayout(minus, range, field, plus, requiredWidth, width);
    }
}


