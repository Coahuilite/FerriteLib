using UnityEngine;

namespace UnityEngine;

/// <summary>Executable stub for the Unity IMGUI types used by the UiKit runtime paths.</summary>
public enum EventType
{
    MouseDown = 0,
    MouseUp = 1,
    MouseDrag = 2,
    KeyDown = 3,
    KeyUp = 4,
    Repaint = 5,
    Layout = 6,
    Used = 7
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
        get => _mousePosition;
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

    public static int GetControlID(int hint, FocusType focusType)
    {
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

    public static void BeginGroup(Rect position)
    {
        GroupDepth++;
        BeginGroupCalls++;
    }

    public static void EndGroup()
    {
        if (GroupDepth > 0) GroupDepth--;
        EndGroupCalls++;
    }
}
