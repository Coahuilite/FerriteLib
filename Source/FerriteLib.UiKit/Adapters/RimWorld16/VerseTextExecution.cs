using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Text capabilities over one native owner; never duplicates capture, focus, images or controls.</summary>
public sealed class VerseTextExecution : IUiExecution, IUiTextFontDrawing, IUiTextFontEditing, IUiExecutionOwner, IDisposable
{
    private readonly bool ownsOwner;
    public VerseTextExecution() : this(new VerseUiExecution(), true) { }
    public VerseTextExecution(VerseUiExecution owner, bool ownsOwner = false)
    { Owner = owner ?? throw new ArgumentNullException(nameof(owner)); this.ownsOwner = ownsOwner; }
    public VerseUiExecution Owner { get; }
    IUiExecution IUiExecutionOwner.NativeOwner => Owner;
    public VerseUiImages Images => Owner.Images;
    internal static bool SupportsPurpose(UiTextPurpose purpose) => VerseFerriteTextMetrics.SupportsTextPurpose(purpose);
    public bool Supports(UiTextPurpose purpose) => SupportsPurpose(purpose);
    public UiFrame CaptureFrame(bool inputAllowed = true) => Owner.CaptureFrame(inputAllowed);
    public void PrepareControl(UiControlKey key) => Owner.PrepareControl(key);
    public bool HasCapture(UiControlKey key) => Owner.HasCapture(key);
    public bool CaptureAvailable => Owner.CaptureAvailable;
    public void Capture(UiControlKey key) => Owner.Capture(key);
    public void Release(UiControlKey key) => Owner.Release(key);
    public bool HasFocus(UiControlKey key) => Owner.HasFocus(key);
    public void ReleaseFocus(UiControlKey key) => Owner.ReleaseFocus(key);
    public UiNativeEditResult Edit(UiNativeEdit request) => Owner.Edit(request);
    public UiNativeEditResult Edit(UiTextEdit request)
    {
        if (request.Font.Size.HasValue && !Supports(UiTextPurpose.Editor)) throw new NotSupportedException("Numeric editor font is unsupported; resolve to the preset first.");
        return Owner.EditText(request);
    }
    public void ConsumeEvent() => Owner.ConsumeEvent();
    public void WithState(Action action) => Owner.WithState(action);
    public void Scroll(UiLayoutRect viewport, UiLayoutRect content, UiLayoutPoint position, bool scrollbar, Action<UiLayoutPoint> body) => Owner.Scroll(viewport, content, position, scrollbar, body);
    public void Warning(string message) => Owner.Warning(message);
    public void Clip(UiLayoutRect bounds, Action body) => Owner.Clip(bounds, body);
    public void Fill(UiLayoutRect rect, UiColour colour) => Owner.Fill(rect, colour);
    public void Text(UiLayoutRect rect, string text, UiFont font, UiColour colour, UiTextAnchor anchor, bool wrap) => Owner.Text(rect, text, font, colour, anchor, wrap);
    public void Text(UiLayoutRect rect, string text, UiTextFont font, UiColour colour, UiTextAnchor anchor, bool wrap)
    {
        if (font.Size.HasValue && !Supports(UiTextPurpose.Label)) throw new NotSupportedException("Numeric label font is unsupported; resolve to the preset first.");
        Owner.TextSized(rect, text, font, colour, anchor, wrap);
    }
    public void Line(UiLayoutPoint from, UiLayoutPoint to, UiColour colour, float width) => Owner.Line(from, to, colour, width);
    public void Image(UiLayoutRect rect, UiImage image, UiColour tint) => Owner.Image(rect, image, tint);
    public void Tooltip(UiLayoutRect rect, string text) => Owner.Tooltip(rect, text);
    public void Dispose() { if (ownsOwner) Owner.Dispose(); }
}
