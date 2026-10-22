using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Descriptive status text with a Tiny undeclared fallback; explicit font declarations override it.
/// </summary>
public sealed class ChromeBannerWidget : IUiWidget, IUiMeasureParticipant, IUiDescriptiveWidget, IUiRetainedDisplay
{
    public const string Kind = "chrome/banner";

    private const float DefaultHeight = 22f;

    /// <summary>
    /// This kind's undeclared fallback type size. Measure and Draw read the one name, so the band the layout reserves
    /// and the band that is painted cannot drift apart — which is the risk the deleted copy of the
    /// wrapped-height rule used to carry.
    /// </summary>
    private const UiFont BandFont = UiFont.Tiny;

    private UiElementSpec spec = UiElementSpec.Empty;
    private readonly UiRetainedPaint retained = new();
    bool IUiRetainedDisplay.PaintLast(Rect rect, UiWidgetContext ctx) => retained.Paint(rect,ctx);
    UiMeasurement? IUiRetainedDisplay.MeasureLast(UiWidgetContext ctx) => retained.Measure(ctx);

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new ChromeBannerWidget(),
            // G5: the banner joins the atoms' role pair instead of keeping a literal list that could not
            // take a Tone. Its own look is text-only, so it reads the role's TEXT colour, and its default
            // emphasis is Muted (ParseEmphasis below) - which is what keeps an untone banner's colour exactly
            // what it was before the role pair arrived.
            AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "Bind", "Text", "TextKey", "Height"),
            new[] { "Text", "TextKey" }, null, BandFont);
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            bindings.ValidateValue<string>(bindKey, elementPath);
        }
    }

    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        string text = Describe(ctx).Text;
        retained.SetMeasure(c=>CaptureText(c,text));
        return CaptureText(ctx,text);
    }
    private UiMeasurement CaptureText(UiWidgetContext ctx,string text)
    {
        return new UiMeasurement(new UiMeasureInputs(new[] { text }, new[] { ctx.ViewWidth, (float)AtomVocabulary.TextFont(ctx, BandFont), ctx.Theme.Geometry.Padding }, text.Length > 0), () => Math.Max(ReadHeight(), WrappedTextWidget.MeasureBand(ctx, text, AtomVocabulary.TextFont(ctx, BandFont), ctx.Theme.Geometry.Padding)));
    }

    public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();

    public UiDescription Describe(UiWidgetContext ctx) => AtomVocabulary.DescribeText(spec, ctx, UiDescriptionRole.Status, "Text", "TextKey");

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string text = Describe(ctx).Text;
        if (text.Length == 0) return;

        Color ink=AtomVocabulary.ResolveRole(spec, ctx, writable: null, defaultEmphasis: UiEmphasis.Muted).Text;
        PaintText(rect,ctx,text,ink);
        retained.Capture(ctx,(r,c)=>PaintText(r,c,text,ink));
    }
    private void PaintText(Rect rect,UiWidgetContext ctx,string text,Color ink)
    {
        UiThemeDraw.Label(
            rect,
            text,
            ctx.Theme,
            ink,
            AtomVocabulary.TextFont(ctx, BandFont),
            TextAnchor.MiddleLeft);
    }

    private float ReadHeight()
    {
        if (spec.TryGetAttribute("Height", out string raw)
            && float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float height)
            && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
    }
}
