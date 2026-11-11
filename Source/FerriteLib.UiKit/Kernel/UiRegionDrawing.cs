using System;

namespace FerriteLib.UiKit.Kernel;

// An enforced pure-Paint clip uses the existing drawing group. Translate its drawing outlet back
// into the original logical coordinates, including numeric text and recursively nested clips.
internal sealed class UiRegionDrawing : IUiDrawing, IUiTextFontDrawing, IUiTextFontEditing
{
    private readonly IUiDrawing drawing;
    private UiLayoutPoint origin;
    internal UiRegionDrawing(IUiDrawing drawing, UiLayoutPoint origin) { this.drawing = drawing; this.origin = origin; }
    private UiLayoutRect Rect(UiLayoutRect r) => new(r.X - origin.X, r.Y - origin.Y, r.Width, r.Height);
    private UiLayoutPoint Point(UiLayoutPoint p) => new(p.X - origin.X, p.Y - origin.Y);
    public void Clip(UiLayoutRect bounds, Action body)
    {
        UiLayoutRect translated = Rect(bounds);
        drawing.Clip(translated, () => { UiLayoutPoint prior = origin; origin = bounds.Position; try { body(); } finally { origin = prior; } });
    }
    public void Fill(UiLayoutRect rect, UiColour colour) => drawing.Fill(Rect(rect), colour);
    public void Text(UiLayoutRect rect, string text, UiFont font, UiColour colour, UiTextAnchor anchor, bool wrap)
        => drawing.Text(Rect(rect), text, font, colour, anchor, wrap);
    public void Line(UiLayoutPoint from, UiLayoutPoint to, UiColour colour, float width) => drawing.Line(Point(from), Point(to), colour, width);
    public void Image(UiLayoutRect rect, UiImage image, UiColour tint) => drawing.Image(Rect(rect), image, tint);
    public void Tooltip(UiLayoutRect rect, string text) => throw new InvalidOperationException("Region Paint cannot register tooltips; use Operate.");
    public bool Supports(UiTextPurpose purpose) => (drawing as IUiTextFontDrawing)?.Supports(purpose) == true;
    public UiNativeEditResult Edit(UiTextEdit request) => throw new InvalidOperationException("Region Paint cannot drive a native editor; use Operate.");
    public void Text(UiLayoutRect rect, string text, UiTextFont font, UiColour colour, UiTextAnchor anchor, bool wrap)
        => ((IUiTextFontDrawing)drawing).Text(Rect(rect), text, font, colour, anchor, wrap);
}
