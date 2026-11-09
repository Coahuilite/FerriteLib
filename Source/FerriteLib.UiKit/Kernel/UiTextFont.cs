using System;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

/// <summary>A font resource alias and an optional logical GUI size; never a physical-pixel scale.</summary>
public readonly struct UiTextFont : IEquatable<UiTextFont>
{
    public UiTextFont(UiFont alias, int? size = null)
    {
        if (size.HasValue) ValidateSize(size.Value);
        Alias = alias; Size = size;
    }
    public UiFont Alias { get; }
    public int? Size { get; }
    internal static void ValidateSize(int size)
    { if (size < 1 || size > 96) throw new ArgumentOutOfRangeException(nameof(size), "FontSize must be an integer from 1 to 96."); }
    internal static bool TrySize(string raw, out int size)
    {
        size = 0;
        if (string.IsNullOrEmpty(raw)) return false;
        foreach (char c in raw) if (c < '0' || c > '9') return false;
        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out size) && size >= 1 && size <= 96;
    }
    public bool Equals(UiTextFont other) => Alias == other.Alias && Size == other.Size;
    public override bool Equals(object? other) => other is UiTextFont font && Equals(font);
    public override int GetHashCode() => ((int)Alias * 397) ^ Size.GetHashCode();
    public static bool operator ==(UiTextFont a, UiTextFont b) => a.Equals(b);
    public static bool operator !=(UiTextFont a, UiTextFont b) => !a.Equals(b);
    public override string ToString() => Alias + (Size.HasValue ? "/" + Size.Value.ToString(CultureInfo.InvariantCulture) : "/preset");
}

public enum UiTextPurpose { Label, Editor }

/// <summary>Optional numeric ruler. Null environment revokes reuse, including for a supported size.</summary>
public interface IUiTextFontMetrics
{
    bool Supports(UiTextPurpose purpose);
    float MeasureText(string text, UiTextFont font, float width, UiTextPurpose purpose);
    float MeasureWidth(string text, UiTextFont font, UiTextPurpose purpose);
    UiMeasureInputs? CaptureEnvironment(UiTextFont font, UiTextPurpose purpose);
}
public interface IUiTextFontDrawing
{
    bool Supports(UiTextPurpose purpose);
    void Text(UiLayoutRect rect, string text, UiTextFont font, UiColour colour, UiTextAnchor anchor, bool wrap);
}
public interface IUiTextFontEditing
{
    UiNativeEditResult Edit(UiTextEdit request);
}

/// <summary>New edit data composes the unchanged legacy request; native identity stays with its owner.</summary>
public readonly struct UiTextEdit
{
    public UiTextEdit(UiNativeEdit edit, UiTextFont font, UiColour? colour = null)
    {
        if (edit.Font != font.Alias) throw new ArgumentException("Edit and effective font aliases must agree.", nameof(font));
        Edit = edit; Font = font; Colour = colour;
    }
    public UiNativeEdit Edit { get; }
    public UiTextFont Font { get; }
    public UiColour? Colour { get; }
}

/// <summary>One effective description and the borrowed pair that measures and paints it.</summary>
public readonly struct UiTextRun
{
    private readonly ITextMetrics metrics;
    private readonly IUiDrawing? drawing;
    internal UiTextRun(ITextMetrics metrics, IUiDrawing? drawing, UiTextFont requested, UiTextPurpose purpose, string path = "(unscoped)")
    {
        this.metrics = metrics ?? throw new ArgumentNullException(nameof(metrics)); this.drawing = drawing;
        Requested = requested; Purpose = purpose;
        Path = path;
        bool supported = !requested.Size.HasValue || (metrics as IUiTextFontMetrics)?.Supports(purpose) == true
            && (drawing as IUiTextFontDrawing)?.Supports(purpose) == true
            && (purpose != UiTextPurpose.Editor || drawing is IUiTextFontEditing);
        Font = supported ? requested : new UiTextFont(requested.Alias);
        FallbackReason = supported ? null : "host does not support " + purpose;
    }
    public UiTextFont Requested { get; }
    public UiTextFont Font { get; }
    public UiTextPurpose Purpose { get; }
    public string? FallbackReason { get; }
    internal string Path { get; }
    public float MeasureText(string text, float width) => Font.Size.HasValue
        ? ((IUiTextFontMetrics)metrics).MeasureText(text, Font, width, Purpose) : metrics.MeasureText(text, Font.Alias, width);
    public float MeasureWidth(string text) => Font.Size.HasValue
        ? ((IUiTextFontMetrics)metrics).MeasureWidth(text, Font, Purpose) : metrics.MeasureWidth(text, Font.Alias);
    public UiMeasureInputs? CaptureEnvironment() => Font.Size.HasValue
        ? ((IUiTextFontMetrics)metrics).CaptureEnvironment(Font, Purpose) : (metrics as IUiTextMetricsEnvironment)?.CaptureEnvironment(Font.Alias);
    internal UiTextRun WithMetrics(ITextMetrics source) => new(source, drawing, Font, Purpose, Path);
    public UiNativeEditResult Edit(UiNativeEdit request, UiColour? colour = null)
    {
        if (Purpose != UiTextPurpose.Editor) throw new InvalidOperationException("Resolve an Editor run before editing.");
        UiNativeEditResult result;
        if (drawing is IUiTextFontEditing editor) result = editor.Edit(new UiTextEdit(request, Font, colour));
        else if (drawing is IUiExecution execution)
        {
            if (colour.HasValue) UiFitAudit.ReportStyleFallback(Path, "Editor", "TextColor", "explicit colour", "host does not support Editor colour");
            result = execution.Edit(request);
        }
        else throw new InvalidOperationException("Editing requires an explicit execution owner.");
        if (Requested.Size.HasValue || colour.HasValue) UiFitAudit.CheckText(request.Rect, result.Text, this, drawing is IUiTextFontEditing ? colour : null, !request.Multiline);
        return result;
    }
    public void Draw(UiLayoutRect rect, string text, UiColour colour, UiTextAnchor anchor = UiTextAnchor.MiddleLeft, bool singleLine = false)
    {
        if (drawing == null) throw new InvalidOperationException("Painting requires an explicit drawing owner.");
        UiFitAudit.CheckText(rect, text, this, colour, singleLine);
        if (Font.Size.HasValue) ((IUiTextFontDrawing)drawing).Text(rect, text, Font, colour, anchor, !singleLine);
        else drawing.Text(rect, text, Font.Alias, colour, anchor, !singleLine);
    }
}
