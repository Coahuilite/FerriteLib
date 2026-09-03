namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Text measurement seam. Production implementations use Verse's IMGUI text engine;
/// tests inject deterministic stubs.
/// </summary>
public interface ITextMetrics
{
    /// <summary>Measures the height needed to render <paramref name="text"/> at <paramref name="font"/> within <paramref name="width"/>.</summary>
    float MeasureText(string text, UiFont font, float width);

    /// <summary>
    /// Measures the width a single unwrapped line of <paramref name="text"/> needs at <paramref name="font"/>.
    /// Height alone cannot detect horizontal clipping, and content-driven column widths need a width budget;
    /// this is the seam both use. Implementations must resolve through the same font mapping as drawing.
    /// </summary>
    float MeasureWidth(string text, UiFont font);
}
