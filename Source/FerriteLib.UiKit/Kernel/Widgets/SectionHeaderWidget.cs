using System;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Greenfield section header: a themed title, and - unless the author turns it off - a divider under it.
/// <para>
/// <b>Why the divider has a switch (0.7.x).</b> It is painted by this kind alone, and it used to be painted
/// unconditionally: not in the schema, reachable by no manifest attribute, which is exactly what this library's
/// "appearance is declarable or themed" rule forbids. Any page that separates sections by whitespace - the
/// no-frames/no-dividers direction a consumer asked for - could not use a header without a rule under it. The
/// switch is <c>Chrome="none"</c>, the vocabulary the button already uses for "paint no chrome": no new
/// attribute name, and the colour stays the theme's <c>Divider</c> token, so a scope's scheme can still restyle
/// the line (transparent included) instead of switching it off.
/// </para>
/// </summary>
internal sealed class SectionHeaderWidget : IUiWidget, IUiMeasureParticipant, IUiDescriptiveWidget, IUiRetainedDisplay
{
    public const string Kind = "section/header";

    private const float DefaultHeight = 24f;

    private UiElementSpec spec = UiElementSpec.Empty;
    private readonly UiRetainedPaint retained = new();
    bool IUiRetainedDisplay.PaintLast(UiLayoutRect rect, UiWidgetContext ctx) => retained.Paint(rect,ctx);
    UiMeasurement? IUiRetainedDisplay.MeasureLast(UiWidgetContext ctx) => retained.Measure(ctx);

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new SectionHeaderWidget(),
            new[] { "Id", "Kind", "Title", "TitleKey", "Height", "Tab", "Hidden", "Chrome" },
            new[] { "Title", "TitleKey" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindingReader bindings, string elementPath)
    {
        // No required binding. The one attribute this kind does not merely read but implements a closed set of
        // values for is Chrome: a value it would ignore is an inert declaration, refused here where the element
        // and the path are still known (the A2/A3 shape).
        string chrome = AtomVocabulary.Read(spec, "Chrome").Trim();
        if (chrome.Length > 0 && !string.Equals(chrome, "none", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SectionHeaderWidget at '{elementPath}' declares Chrome='{chrome}'; the only accepted value is 'none'"
                + " (paints no divider). Omit the attribute for the header's plain look, with its divider.");
        }
    }

    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        float fixedHeight = AtomVocabulary.ReadHeight(spec, 0);
        if (fixedHeight > 0) return new UiMeasurement(new UiMeasureInputs(numbers: new[] { fixedHeight }), () => fixedHeight);
        string text = Describe(ctx).Text;
        retained.SetMeasure(c=>CaptureText(c,text));
        return CaptureText(ctx,text);
    }
    private UiMeasurement CaptureText(UiWidgetContext ctx,string text)
    {
        return new UiMeasurement(new UiMeasureInputs(new[] { text }, new[] { ctx.ViewWidth, (float)AtomVocabulary.TextFont(ctx) }, true), () =>
        {
            float declared = AtomVocabulary.ReadHeight(spec, 0);
            return declared > 0 ? declared : Math.Max(ReadHeight(), ctx.Metrics.MeasureText(text, AtomVocabulary.TextFont(ctx), Math.Max(1, ctx.ViewWidth)));
        });
    }

    public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();

    public UiDescription Describe(UiWidgetContext ctx) => new UiDescription(UiDescriptionRole.Heading, text: Title(ctx));

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;

        string title = Describe(ctx).Text;
        PaintText(rect,ctx,title);
        retained.Capture(ctx,(r,c)=>PaintText(r,c,title));
    }
    private void PaintText(UiLayoutRect rect,UiWidgetContext ctx,string title)
    {
        UiThemeDraw.Label(ctx.Drawing,rect, title, ctx.Theme, ctx.Theme[UiPaletteRole.Foreground], AtomVocabulary.TextFont(ctx), UiTextAnchor.MiddleLeft);

        // Chrome="none": the title without the rule under it. Read here rather than cached in Configure so the
        // decision sits next to the paint it suppresses and a reloaded spec needs no second path.
        if (string.Equals(AtomVocabulary.Read(spec, "Chrome").Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        UiThemeDraw.Surface(
ctx.Drawing,            new UiLayoutRect(rect.X, rect.YMax - 1f, rect.Width, 1f),
            ctx.Theme,
            ctx.Theme[UiPaletteRole.Divider],
            ctx.Theme[UiPaletteRole.Divider]);
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

    private string Title(UiWidgetContext ctx) => AtomVocabulary.ResolveText(spec, ctx, "Title", "TitleKey");
}


