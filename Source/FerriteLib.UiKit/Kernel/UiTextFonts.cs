using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Numeric authoring and paired resolution; legacy font readers retain their alias meaning.</summary>
public static class UiTextFonts
{
    public static UiPropertyDeclaration<int> Theme(UiTheme theme) => (theme ?? throw new ArgumentNullException(nameof(theme))).FontSizeDeclaration;
    public static UiPropertyDeclaration<int> Instance(UiElementProperties properties) => (properties ?? throw new ArgumentNullException(nameof(properties))).FontSizeDeclaration(false);
    public static UiPropertyDeclaration<int> Local(UiElementProperties properties) => (properties ?? throw new ArgumentNullException(nameof(properties))).FontSizeDeclaration(true);
    public static UiTextFont Requested(UiTheme theme) => new(theme.DefaultFont, theme.ReadFontSize);
    public static UiResolvedProperty<int?> ResolvedSize(UiElementProperties properties) => properties.ResolvedFontSize;
    public static UiWidgetContext Apply(UiWidgetContext context, UiTheme host) => UiTextColours.Resolve(context, host);
    public static UiLayoutRect PopupRect(UiWidgetContext context, UiLayoutRect anchor, int count, UiLayoutRect viewport)
        => UiPopup.RectFor(anchor, count, viewport, RowHeight(context, minimum: UiPopup.OptionHeight));
    public static UiTextFont FontOf(UiOverflowReport report) => report.ActualFont ?? new UiTextFont(report.Font);
    public static UiColour? ColourOf(UiOverflowReport report) => report.ActualColour;
    public static UiTextRun Resolve(ITextMetrics metrics, IUiDrawing? drawing, UiTextFont font, UiTextPurpose purpose = UiTextPurpose.Label, string elementPath = "(unscoped)")
    {
        var run = new UiTextRun(metrics, drawing, font, purpose, elementPath);
        if (run.FallbackReason != null) UiFitAudit.ReportFontFallback(elementPath, run);
        return run;
    }
    public static UiTextRun Resolve(UiWidgetContext context, UiTextPurpose purpose = UiTextPurpose.Label)
        => For(context, context.Theme.Styles.Font, purpose);
    internal static UiTextRun For(UiWidgetContext ctx, UiFont? alias = null, UiTextPurpose purpose = UiTextPurpose.Label)
    {
        var run = Resolve(ctx.Metrics, ctx.Session.CurrentExecution, new UiTextFont(alias ?? ctx.Theme.Styles.Font, ctx.Theme.ReadFontSize), purpose, ctx.ElementPath);
        ctx.Properties.TextRuns[purpose] = new UiTextFontFact(run);
        return run;
    }
    internal static UiTextRun Legacy(ITextMetrics metrics, UiFont font) => new(metrics, null, new UiTextFont(font), UiTextPurpose.Label);
    public static float RowHeight(UiWidgetContext ctx, UiTextPurpose purpose = UiTextPurpose.Label, float? minimum = null)
    {
        float floor = minimum ?? ctx.Theme.Geometry.RowHeight;
        UiTextRun run = For(ctx, purpose: purpose);
        return run.Font.Size.HasValue ? Math.Max(floor, run.MeasureText("Ag", 100000) + ctx.Theme.Geometry.Spacing * 2) : floor;
    }
    internal static float ControlHeight(UiWidgetContext ctx, UiSliderLayout label, float rowHeight)
        => label.Stacked ? label.LabelHeight + ctx.Theme.Geometry.Gap + rowHeight : Math.Max(label.Height, rowHeight);
    internal static UiMeasureInputs? Environment(UiWidgetContext ctx)
    {
        UiMeasureInputs? label = For(ctx).CaptureEnvironment();
        if (label == null) return null;
        if (!UsesEditor(ctx)) return label;
        UiMeasureInputs? editor = For(ctx, purpose: UiTextPurpose.Editor).CaptureEnvironment();
        return editor == null ? null : UiMeasureInputs.Join(label, editor);
    }
    internal static UiMeasureInputs MeasureInputs(UiWidgetContext ctx, UiMeasureInputs inputs)
    {
        if (!ctx.Theme.ReadFontSize.HasValue) return inputs;
        UiTextFont label = For(ctx).Font;
        UiTextFont editor = UsesEditor(ctx) ? For(ctx, purpose: UiTextPurpose.Editor).Font : new UiTextFont(ctx.Theme.Styles.Font);
        return UiMeasureInputs.Join(inputs, new UiMeasureInputs(numbers: new[] { (float)label.Alias, (float)(label.Size ?? 0), (float)editor.Alias, (float)(editor.Size ?? 0) }, usesTextMetrics: label.Size.HasValue || editor.Size.HasValue));
    }
    internal static bool UsesEditor(UiWidgetContext ctx) => ctx.Node.Kind == "input/text-field" || ctx.Node.Kind == "input/number-field"
        || ctx.Node.Kind == "input/stepper-slider" || ctx.Properties.TextRuns.ContainsKey(UiTextPurpose.Editor);
    internal static void Label(UiWidgetContext ctx, UiLayoutRect rect, string text, UiColour? colour = null, UiFont? font = null,
        UiTextAnchor anchor = UiTextAnchor.MiddleLeft, bool singleLine = false)
    {
        UiTextRun run = For(ctx, font);
        UiColour ink = colour ?? ctx.Theme.Styles.Resolve(UiStatusTone.Neutral).Text;
        run.Draw(rect, text, ink, anchor, singleLine);
        RecordPaint(ctx, UiTextPurpose.Label, run.Font, ink, "label");
    }
    internal static void RecordPaint(UiWidgetContext ctx, UiTextPurpose purpose, UiTextFont font, UiColour? ink, string source)
    {
        ctx.Properties.TextPaints[purpose] = new UiTextPaintFact(font, ink, source);
#if FER_DEV || FER_TOOLS
        UiDiagnosticHub.ActiveSubscription?.Geometry?.NoteText(ctx.Node.Id.Key, purpose, font, ink, source);
#endif
    }
    internal static UiPropertyDeclaration<int> Declaration(Action changed) => new(changed, UiTextFont.ValidateSize);
    internal static bool AnyInk(UiTheme theme)
    { foreach (string slot in UiTextColourProperties.Names) if (theme.ReadTextColour(slot).HasValue) return true; return false; }
}

internal readonly struct UiTextFontFact
{
    internal UiTextFontFact(UiTextRun run) { Requested = run.Requested; Font = run.Font; Reason = run.FallbackReason; }
    internal UiTextFont Requested { get; }
    internal UiTextFont Font { get; }
    internal string? Reason { get; }
}

internal readonly struct UiTextPaintFact
{
    internal UiTextPaintFact(UiTextFont font, UiColour? colour, string source) { Font = font; Colour = colour; Source = source; }
    internal UiTextFont Font { get; }
    internal UiColour? Colour { get; }
    internal string Source { get; }
}
