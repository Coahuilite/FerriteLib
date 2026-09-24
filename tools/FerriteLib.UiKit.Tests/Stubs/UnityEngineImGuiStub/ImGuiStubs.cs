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
    Repaint = 7,
    Layout = 8,
    Used = 12
}

public sealed class Event
{
    public static Event? current { get; set; }

    private EventType _type;
    public EventType type
    {
        get => _type;
        set => _type = value;
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
        type = EventType.Used;
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
        return controlIdCounter;
    }
}

public static class GUI
{
    // Recording hooks for structural scope cleanup tests. The test assembly compiles against the
    // Krafs ref assembly, so these are only accessible through reflection at runtime.
    public static int GroupDepth;
    public static int BeginGroupCalls;
    public static int EndGroupCalls;

    public static Color color { get; set; } = Color.white;

    public static void SetNextControlName(string name)
    {
    }

    public static string GetNameOfFocusedControl()
    {
        return "";
    }

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
