using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Kernel;
/// <summary>Explicit 1.6 execution owner. Native identities and supplier resources never enter the retained core.</summary>
public sealed class VerseUiExecution : IUiExecution, IDisposable
{
    private readonly Dictionary<UiControlKey, int> controls = new();
    private UiFrame? frame;
    private UiControlKey? captureOwner;
    public VerseUiImages Images { get; } = new VerseUiImages();

    public UiFrame CaptureFrame(bool inputAllowed = true)
    {
        int held = captureOwner.HasValue && HasCapture(captureOwner.Value) ? Control(captureOwner.Value) : 0;
        controls.Clear();
        if (held != 0)
            controls.Add(captureOwner!.Value, held);
        else
            captureOwner = null;
        Event? e = Event.current;
        frame = new UiFrame(e == null ? UiFramePhase.None : Phase(e.type), e == null ? default : Point(e.mousePosition), e == null ? UiFramePhase.None : Phase(e.rawType), e?.button ?? 0, e?.delta.y ?? 0, e == null ? UiKey.None : Key(e.keyCode), e?.control ?? false, e?.command ?? false, e?.shift ?? false, Input.compositionString.Length != 0, inputAllowed);
        return frame;
    }

    private static UiFramePhase Phase(EventType type) => Enum.TryParse(type.ToString(), out UiFramePhase p) ? p : UiFramePhase.None;
    private static UiKey Key(KeyCode key) => key switch
    {
        KeyCode.UpArrow => UiKey.Up,
        KeyCode.DownArrow => UiKey.Down,
        KeyCode.LeftArrow => UiKey.Left,
        KeyCode.RightArrow => UiKey.Right,
        _ => Enum.TryParse(key.ToString(), out UiKey k) ? k : UiKey.None
    };
    public static UiLayoutRect Rect(Rect r) => new(r.x, r.y, r.width, r.height);
    public static Rect Rect(UiLayoutRect r) => new(r.X, r.Y, r.Width, r.Height);
    public static UiLayoutPoint Point(Vector2 p) => new(p.x, p.y);
    public static Vector2 Point(UiLayoutPoint p) => new(p.X, p.Y);
    public static UiColour Colour(Color c) => new(c.r, c.g, c.b, c.a);
    public static Color Colour(UiColour c) => new(c.Red, c.Green, c.Blue, c.Alpha);
    public void PrepareControl(UiControlKey key)
    {
        // Logical names may repeat in another execution owner. Keep the native hint owner-scoped;
        // only the native allocator decides the resulting ID and sequence remains part of that mapping.
        int allocated = GUIUtility.GetControlID(unchecked(key.GetHashCode()*397 ^ Images.Scope.GetHashCode()), FocusType.Passive);
        if (captureOwner != key || !HasCapture(key))
            controls[key] = allocated;
    }

    private int Control(UiControlKey key) => controls.TryGetValue(key, out int id) ? id : throw new InvalidOperationException("Unprepared native control: " + key);
    public bool HasCapture(UiControlKey key) => controls.TryGetValue(key, out int id) && GUIUtility.hotControl == id;
    public bool CaptureAvailable => GUIUtility.hotControl == 0;

    public void Capture(UiControlKey key)
    {
        GUIUtility.hotControl = Control(key);
        captureOwner = key;
    }

    public void Release(UiControlKey key)
    {
        if (HasCapture(key))
            GUIUtility.hotControl = 0;
        if (captureOwner == key)
            captureOwner = null;
    }

    public bool HasFocus(UiControlKey key) => GUI.GetNameOfFocusedControl() == key.Key;
    public void ReleaseFocus(UiControlKey key)
    {
        if (HasFocus(key))
            GUI.FocusControl(null);
    }

    public UiNativeEditResult Edit(UiNativeEdit request)
    {
        string result = request.Text;
        WithState(() =>
        {
            Verse.Text.Font = UiKitFonts.ToGameFont(request.Font);
            GUI.SetNextControlName(request.Control.Key);
            if (request.Focus && !HasFocus(request.Control))
                GUI.FocusControl(request.Control.Key);
            Event? current = Event.current;
            if (HasFocus(request.Control) && !request.Writable)
            {
                if (current?.type == EventType.KeyDown)
                {
                    KeyCode key = current.keyCode;
                    bool shortcut = current.control || current.command;
                    bool copy = shortcut && (key == KeyCode.A || key == KeyCode.C);
                    bool navigation = key == KeyCode.UpArrow || key == KeyCode.DownArrow || key == KeyCode.LeftArrow || key == KeyCode.RightArrow || key == KeyCode.Home || key == KeyCode.End || key == KeyCode.PageUp || key == KeyCode.PageDown;
                    if (!copy && !navigation)
                        current.Use();
                }

                if ((current?.type == EventType.ExecuteCommand || current?.type == EventType.ValidateCommand) && current.commandName != "Copy" && current.commandName != "SelectAll")
                    current.Use();
            }

            result = request.Multiline ? Verse.Widgets.TextArea(Rect(request.Rect), request.Text, !request.Writable) : Verse.Widgets.TextField(Rect(request.Rect), request.Text);
        });
        bool consumed = Event.current?.type == EventType.Used;
        if (consumed)
            frame?.Consume();
        return new UiNativeEditResult(result, HasFocus(request.Control), consumed);
    }

    public void ConsumeEvent()
    {
        Event.current?.Use();
        frame?.Consume();
    }

    public void WithState(Action action)
    {
        GameFont font = Verse.Text.Font;
        TextAnchor anchor = Verse.Text.Anchor;
        bool wrap = Verse.Text.WordWrap;
        Color colour = GUI.color, background = GUI.backgroundColor, content = GUI.contentColor;
        bool enabled = GUI.enabled;
        try
        {
            action();
        }
        finally
        {
            Verse.Text.Font = font;
            Verse.Text.Anchor = anchor;
            Verse.Text.WordWrap = wrap;
            GUI.color = colour;
            GUI.backgroundColor = background;
            GUI.contentColor = content;
            GUI.enabled = enabled;
        }
    }

    public void Scroll(UiLayoutRect viewport, UiLayoutRect content, UiLayoutPoint position, bool showScrollbar, Action<UiLayoutPoint> body)
    {
        Vector2 offset = Point(position);
        Verse.Widgets.BeginScrollView(Rect(viewport), ref offset, Rect(content), showScrollbar);
        try
        {
            if (Event.current?.type == EventType.Used)
                frame?.Consume();
            body(Point(offset));
        }
        finally
        {
            Verse.Widgets.EndScrollView();
        }
    }

    public void Clip(UiLayoutRect bounds, Action body)
    {
        GUI.BeginGroup(Rect(bounds));
        try
        {
            body();
        }
        finally
        {
            GUI.EndGroup();
        }
    }

    public void Fill(UiLayoutRect rect, UiColour colour) => WithState(() => Verse.Widgets.DrawBoxSolid(Rect(rect), Colour(colour)));
    public void Text(UiLayoutRect rect, string text, UiFont font, UiColour colour, UiTextAnchor anchor, bool wrap)
    {
        WithState(() =>
        {
            Verse.Text.Font = UiKitFonts.ToGameFont(font);
            Verse.Text.Anchor = (TextAnchor)(int)anchor;
            Verse.Text.WordWrap = wrap;
            GUI.color = Colour(colour);
            Verse.Widgets.Label(Rect(rect), text);
        });
    }

    public void Line(UiLayoutPoint from, UiLayoutPoint to, UiColour colour, float width) => WithState(() => Verse.Widgets.DrawLine(Point(from), Point(to), Colour(colour), width));
    public void Image(UiLayoutRect rect, UiImage image, UiColour tint)
    {
        Texture? texture = Images.Resolve(image);
        if (texture != null)
            WithState(() =>
            {
                GUI.color = Colour(tint);
                GUI.DrawTexture(Rect(rect), texture, ScaleMode.StretchToFill, true);
            });
    }

    public void Tooltip(UiLayoutRect rect, string text) => TooltipHandler.TipRegion(Rect(rect), text);
    public void Warning(string message) => Log.Warning(message);
    public void Dispose()
    {
        foreach (var key in controls.Keys)
            Release(key);
        controls.Clear();
        Images.Dispose();
        frame = null;
    }
}

/// <summary>Owns references, not textures. Release and Dispose never destroy supplier resources.</summary>
public sealed class VerseUiImages : IDisposable
{
    private static long nextScope;
    private readonly long scope = Interlocked.Increment(ref nextScope);
    internal long Scope => scope;
    private readonly Dictionary<int, Texture> textures = new();
    private int nextKey;
    public UiImage Add(Texture texture)
    {
        if (texture == null)
            throw new ArgumentNullException(nameof(texture));
        int key = ++nextKey;
        textures.Add(key, texture);
        return new UiImage(scope, key, texture.width, texture.height);
    }

    public void Release(UiImage image)
    {
        if (image.Scope == scope)
            textures.Remove(image.Key);
    }

    internal Texture? Resolve(UiImage image) => image.Scope == scope && textures.TryGetValue(image.Key, out Texture texture) ? texture : null;
    public void Dispose() => textures.Clear();
}
