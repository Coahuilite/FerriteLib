using System;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Empty-state status text sharing native width measurement and effective environment typography.
/// </summary>
internal sealed class EmptyStateWidget : IUiWidget, IUiMeasureParticipant, IUiDescriptiveWidget, IUiRetainedDisplay
{
    public const string Kind = "state/empty";

    private const float DefaultHeight = 48f;

    /// <summary>This kind's undeclared fallback type size, read by both Measure and Draw so they cannot drift apart.</summary>
    private const UiFont BandFont = UiFont.Small;

    private UiElementSpec spec = UiElementSpec.Empty;
    private readonly UiRetainedPaint retained = new();
    bool IUiRetainedDisplay.PaintLast(UiLayoutRect rect, UiWidgetContext ctx) => retained.Paint(rect,ctx);
    UiMeasurement? IUiRetainedDisplay.MeasureLast(UiWidgetContext ctx) => retained.Measure(ctx);

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
UiWidgetRegistry.Define(new UiWidgetDefinition(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new EmptyStateWidget(),
            new[] { "Id", "Kind", "Text", "TextKey", "Height", "Tab", "Hidden" },
            new[] { "Text", "TextKey" }));
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindingAccess bindings, string elementPath)
    {
        // No required binding.
    }

    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        string text = ResolveText(ctx);
        retained.SetMeasure(c=>CaptureText(c,text));
        return CaptureText(ctx,text);
    }
    private UiMeasurement CaptureText(UiWidgetContext ctx,string text)
    {
        return new UiMeasurement(new UiMeasureInputs(new[] { text }, new[] { ctx.ViewWidth, (float)AtomVocabulary.TextFont(ctx, BandFont), ctx.Theme.Geometry.Padding }, text.Length > 0), () => Math.Max(ReadHeight(), WrappedTextWidget.MeasureBand(ctx, text, AtomVocabulary.TextFont(ctx, BandFont), ctx.Theme.Geometry.Padding * 2)));
    }

    public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();

    public UiDescription Describe(UiWidgetContext ctx) => new UiDescription(UiDescriptionRole.Status, text: ResolveText(ctx));

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;

        string text=Describe(ctx).Text;
        PaintText(rect,ctx,text);
        retained.Capture(ctx,(r,c)=>PaintText(r,c,text));
    }
    private void PaintText(UiLayoutRect rect,UiWidgetContext ctx,string text)
    {
        UiThemeDraw.Label(ctx.Drawing,rect, text, ctx.Theme, ctx.Theme[UiPaletteRole.SecondaryForeground], AtomVocabulary.TextFont(ctx, BandFont), UiTextAnchor.MiddleCenter);
    }

    /// <summary>Shared by Measure and Draw so the allocated band always matches the drawn string.</summary>
    private string ResolveText(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("TextKey", out string key) && key.Length > 0)
        {
            return ctx.Translation.Translate(key);
        }

        return spec.TryGetAttribute("Text", out string literal) ? literal : "";
    }

    private float ReadHeight()
    {
        if (spec.TryGetAttribute("Height", out string raw)
            && spec.TryGetNumber("Height", out float height)
            && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
    }
}
