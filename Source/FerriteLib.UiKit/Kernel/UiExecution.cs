using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Facts for one synchronous owner call, captured by the enclosing platform.</summary>
public sealed class UiFrame
{
    public UiFrame(UiFramePhase phase, UiLayoutPoint pointer = default, UiFramePhase? rawPhase = null,
        int button = 0, float wheel = 0, UiKey key = UiKey.None, bool control = false, bool command = false,
        bool shift = false, bool composing = false, bool inputAllowed = true)
    {
        Phase = phase; RawPhase = rawPhase ?? phase; Pointer = pointer; Button = button; Wheel = wheel;
        Key = key; Control = control; Command = command; Shift = shift; Composing = composing; InputAllowed = inputAllowed;
    }
    public UiFramePhase Phase { get; private set; }
    public UiFramePhase RawPhase { get; }
    public UiLayoutPoint Pointer { get; }
    public int Button { get; }
    public float Wheel { get; }
    public UiKey Key { get; }
    public bool Control { get; }
    public bool Command { get; }
    public bool Shift { get; }
    public bool Composing { get; }
    public bool InputAllowed { get; }
    public bool Consumed => Phase == UiFramePhase.Used;
    public void Consume() => Phase = UiFramePhase.Used;
}

public enum UiFramePhase { None, Layout, Repaint, MouseDown, MouseUp, MouseDrag, MouseMove, ScrollWheel, KeyDown, KeyUp, ValidateCommand, ExecuteCommand, Used, Ignore }
public enum UiKey { None, Return, KeypadEnter, Escape, A, C, Up, Down, Left, Right, Home, End, PageUp, PageDown }
public enum UiTextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }

/// <summary>Logical control identity, never a platform control ID.</summary>
public readonly struct UiControlKey : IEquatable<UiControlKey>
{
    public UiControlKey(string key) => Key = key ?? throw new ArgumentNullException(nameof(key));
    public string Key { get; }
    public bool Equals(UiControlKey other) => string.Equals(Key, other.Key, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is UiControlKey other && Equals(other);
    public override int GetHashCode() => Key?.GetHashCode() ?? 0;
    public override string ToString() => Key ?? "";
    public static bool operator ==(UiControlKey a, UiControlKey b) => a.Equals(b);
    public static bool operator !=(UiControlKey a, UiControlKey b) => !a.Equals(b);
}

/// <summary>A scope-owned image reference plus neutral intrinsic dimensions. No native object travels with it.</summary>
public readonly struct UiImage : IEquatable<UiImage>
{
    public UiImage(long scope, int key, float width, float height)
    { Scope = scope; Key = key; Width = width; Height = height; }
    public long Scope { get; }
    public int Key { get; }
    public float Width { get; }
    public float Height { get; }
    public bool IsEmpty => Scope == 0 || Key == 0;
    public bool Equals(UiImage other) => Scope == other.Scope && Key == other.Key && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is UiImage other && Equals(other);
    public override int GetHashCode() => Scope.GetHashCode() ^ Key;
}

public readonly struct UiNativeEdit
{
    public UiNativeEdit(UiControlKey control, UiLayoutRect rect, string text, UiFont font,
        bool multiline, bool writable, bool focus, bool composing)
    { Control = control; Rect = rect; Text = text; Font = font; Multiline = multiline; Writable = writable; Focus = focus; Composing = composing; }
    public UiControlKey Control { get; }
    public UiLayoutRect Rect { get; }
    public string Text { get; }
    public UiFont Font { get; }
    public bool Multiline { get; }
    public bool Writable { get; }
    public bool Focus { get; }
    public bool Composing { get; }
}

public readonly struct UiNativeEditResult
{
    public UiNativeEditResult(string text, bool focused, bool consumed = false)
    { Text = text; Focused = focused; Consumed = consumed; }
    public string Text { get; }
    public bool Focused { get; }
    public bool Consumed { get; }
}

/// <summary>Only synchronous platform execution. Identity, draft, capture policy and layout stay in the owner.</summary>
public interface IUiExecution : IUiDrawing
{
    void PrepareControl(UiControlKey key);
    bool HasCapture(UiControlKey key);
    bool CaptureAvailable { get; }
    void Capture(UiControlKey key);
    void Release(UiControlKey key);
    bool HasFocus(UiControlKey key);
    void ReleaseFocus(UiControlKey key);
    UiNativeEditResult Edit(UiNativeEdit request);
    void ConsumeEvent();
    void WithState(Action action);
    void Scroll(UiLayoutRect viewport, UiLayoutRect content, UiLayoutPoint position, bool showScrollbar, Action<UiLayoutPoint> body);
    void Warning(string message);
}

/// <summary>Leaf painting for both retained pages and independent visual composition.</summary>
public interface IUiDrawing
{
    void Clip(UiLayoutRect bounds, Action body);
    void Fill(UiLayoutRect rect, UiColour colour);
    void Text(UiLayoutRect rect, string text, UiFont font, UiColour colour, UiTextAnchor anchor, bool wrap);
    void Line(UiLayoutPoint from, UiLayoutPoint to, UiColour colour, float width);
    void Image(UiLayoutRect rect, UiImage image, UiColour tint);
    void Tooltip(UiLayoutRect rect, string text);
}


