using UnityEngine;

namespace UnityEngine;

/// <summary>Executable stub for the Unity IMGUI types used by the UiKit runtime paths.</summary>
public enum EventType
{
    MouseDown = 0,
    MouseUp = 1,
    // Values match Krafs.Rimworld.Ref 1.6.4871: callers inline the reference enum constants.
    MouseDrag = 3,
    KeyDown = 4,
    KeyUp = 5,
    ScrollWheel = 6,
    Repaint = 7,
    Layout = 8,
    Used = 12,
    ValidateCommand = 13,
    ExecuteCommand = 14
}

public sealed class Event
{
    public bool control { get; set; }
    public bool command { get; set; }
    public bool shift { get; set; }
    public string commandName { get; set; } = "";
    public static Event? current { get; set; }

    private EventType _type;
    private EventType _rawType;
    public EventType rawType => _rawType;
    public EventType type
    {
        get => _type;
        set
        {
            _type = value;
            _rawType = value;
        }
    }

    private int _button;
    public int button
    {
        get => _button;
        set => _button = value;
    }

    private Vector2 _mousePosition;
    public Vector2 mousePosition
    {
        get => new Vector2(_mousePosition.x - GUI.GroupOrigin.x, _mousePosition.y - GUI.GroupOrigin.y);
        set => _mousePosition = value;
    }

    private Vector2 _delta;
    /// <summary>
    /// The wheel movement of this event. Declared because the kernel reads it through
    /// <c>UiNative.PointerWheelNotches()</c>, and a double that omits a member the code under test calls is a
    /// defect in the double, not in the caller. No group translation applies to it: a delta is not a position.
    /// </summary>
    public Vector2 delta
    {
        get => _delta;
        set => _delta = value;
    }
    private KeyCode _keyCode;
    public KeyCode keyCode
    {
        get => _keyCode;
        set => _keyCode = value;
    }

    private char _character;
    public char character
    {
        get => _character;
        set => _character = value;
    }

    private bool _used;
    public bool used
    {
        get => _used;
        set => _used = value;
    }

    public void Use()
    {
        used = true;
        _type = EventType.Used;
    }

    // The real UnityEngine.Event exposes this factory and no public constructor; the lanes compile
    // against the real type (Krafs ref), so this is the only instance source that exists on both sides.
    public static Event KeyboardEvent(string spec)
    {
        return new Event();
    }
}

public enum FocusType
{
    Keyboard = 0,
    Passive = 1,
    Native = 2
}

/// <summary>Unity's texture scaling mode; the library draws its own letterboxed rect, so this stays the
/// stretch form and the fit is <c>UiThemeDraw.FitImage</c>'s answer rather than Unity's.</summary>
public enum ScaleMode
{
    StretchToFill = 0,
    ScaleAndCrop = 1,
    ScaleToFit = 2
}

public static class GUIUtility
{
    private static int controlIdCounter;

    public static int hotControl { get; set; }

    // Real IMGUI hands the same id to the same control sequence on every event pass. The stub has no
    // frame callback to reset its counter, so it treats a new Event instance (or the transition to
    // null) as a new pass - which is exactly how the lanes pump events, one Event per DrawFrame.
    private static object? currentPass;

    public static int GetControlID(int hint, FocusType focusType)
    {
        object? pass = Event.current;
        if (!ReferenceEquals(pass, currentPass))
        {
            currentPass = pass;
            controlIdCounter = 0;
        }

        controlIdCounter++;
        // Model both documented hint matching and allocation sequence. Ignoring the hint aliases
        // unrelated owners; using the hint alone hides control-reorder capture bugs. This is not
        // Unity's private numeric algorithm.
        int id=unchecked(hint*397+controlIdCounter);
        return id==0?int.MinValue:id;
    }
}

public static class GUI
{
    // Recording hooks for structural scope cleanup tests. The test assembly compiles against the
    // Krafs ref assembly, so these are only accessible through reflection at runtime.
    public static readonly System.Collections.Generic.List<string> PaintTrace = new();
    public static int GroupDepth;
    public static int BeginGroupCalls;
    public static int EndGroupCalls;

    // The texture outlet's recording hooks, in the same shape as Verse.Widgets' draw recorders: what was
    // handed to DrawTexture, in call order, so a lane can assert the FIT rect and the TINT it was drawn with.
    public static readonly System.Collections.Generic.List<Rect> DrawTextureRects = new();
    public static readonly System.Collections.Generic.List<Texture> DrawTextureTextures = new();
    public static readonly System.Collections.Generic.List<Color> DrawTextureColors = new();
    public static readonly System.Collections.Generic.List<ScaleMode> DrawTextureModes = new();

    public static void ClearDrawTextureCalls()
    {
        DrawTextureRects.Clear();
        DrawTextureTextures.Clear();
        DrawTextureColors.Clear();
        DrawTextureModes.Clear();
    }

    public static System.Exception? DrawTextureFailure;

    public static void DrawTexture(Rect rect, Texture texture, ScaleMode mode, bool alphaBlend)
    {
        if (DrawTextureFailure != null) throw DrawTextureFailure;
        PaintTrace.Add("image");
        DrawTextureRects.Add(rect);
        DrawTextureTextures.Add(texture);
        DrawTextureColors.Add(color);
        DrawTextureModes.Add(mode);
    }

    public static Color color { get; set; } = Color.white;
    public static Color backgroundColor { get; set; } = Color.white;
    public static Color contentColor { get; set; } = Color.white;
    public static bool enabled { get; set; } = true;
    public static string NextControlName = "";
    private static string focusedControl = "";

    public static void SetNextControlName(string name) { NextControlName = name; }

    public static string GetNameOfFocusedControl()
    {
        return focusedControl;
    }

    public static void FocusControl(string? name) { focusedControl = name ?? ""; }

    private static readonly System.Collections.Generic.Stack<Vector2> groupOrigins = new();
    private static readonly System.Collections.Generic.Stack<Rect> groupClips = new();

    // Mouse hit tests respect every enclosing GUI clip, including a scroll viewport.
    public static bool IsPointVisible(Vector2 local)
    {
        Vector2 origin = GroupOrigin;
        var window = new Vector2(local.x + origin.x, local.y + origin.y);
        foreach (Rect clip in groupClips)
        {
            if (window.x < clip.x || window.x >= clip.xMax || window.y < clip.y || window.y >= clip.yMax) return false;
        }
        return true;
    }

    /// <summary>
    /// Accumulated origin of the innermost open group; zero outside any group. The stub presents
    /// Event.mousePosition relative to this, mirroring real IMGUI: controls inside a group draw and
    /// hit-test in group-local space, and the event pointer arrives in the same local space.
    /// </summary>
    public static Vector2 GroupOrigin => groupOrigins.Count > 0 ? groupOrigins.Peek() : default;

    public static void BeginGroup(Rect position)
    {
        GroupDepth++;
        BeginGroupCalls++;
        Vector2 parent = GroupOrigin;
        groupClips.Push(new Rect(parent.x + position.x, parent.y + position.y, position.width, position.height));
        groupOrigins.Push(new Vector2(parent.x + position.x, parent.y + position.y));
    }

    public static void EndGroup()
    {
        if (GroupDepth > 0) GroupDepth--;
        EndGroupCalls++;
        if (groupOrigins.Count > 0) groupOrigins.Pop();
        if (groupClips.Count > 0) groupClips.Pop();
    }
}

public enum ImagePosition { ImageLeft, ImageAbove, ImageOnly, TextOnly }
public enum TextClipping { Overflow, Clip }
public sealed class GUIStyle
{
    public Font font { get; set; } = new();
    public int fontSize { get; set; }
    public FontStyle fontStyle { get; set; }
    public bool richText { get; set; }
    public float fixedWidth { get; set; }
    public float fixedHeight { get; set; }
    public bool stretchWidth { get; set; }
    public bool stretchHeight { get; set; }
    public ImagePosition imagePosition { get; set; }
    public TextClipping clipping { get; set; }
    public Vector2 contentOffset { get; set; }
    public RectOffset padding { get; set; } = new();
    public RectOffset border { get; set; } = new();
}
