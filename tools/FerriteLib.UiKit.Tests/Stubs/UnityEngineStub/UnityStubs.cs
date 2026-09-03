namespace UnityEngine;

/// <summary>Executable stub for the Unity types the UiKit test paths touch at runtime.</summary>
public struct Vector2
{
    public float x;
    public float y;

    public Vector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public static Vector2 zero => new(0f, 0f);

    public static Vector2 one => new(1f, 1f);

    public static float Distance(Vector2 a, Vector2 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return (float)System.Math.Sqrt(dx * dx + dy * dy);
    }

    public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
    {
        return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
    }
}

public struct Rect
{
    private float _x;
    private float _y;
    private float _width;
    private float _height;

    public Rect(float x, float y, float width, float height)
    {
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }

    public float x
    {
        get => _x;
        set => _x = value;
    }

    public float y
    {
        get => _y;
        set => _y = value;
    }

    public float width
    {
        get => _width;
        set => _width = value;
    }

    public float height
    {
        get => _height;
        set => _height = value;
    }

    public float xMax => _x + _width;

    public float yMax => _y + _height;

    public Vector2 position => new(_x, _y);
}

public struct Color
{
    public float r;
    public float g;
    public float b;
    public float a;

    public Color(float r, float g, float b)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        a = 1f;
    }

    public Color(float r, float g, float b, float a)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    public static Color white => new(1f, 1f, 1f, 1f);

    public static Color clear => new(0f, 0f, 0f, 0f);
}

public static class Mathf
{
    public const float PI = 3.14159274f;

    public static float Abs(float value) => System.Math.Abs(value);

    public static float Min(float a, float b) => a < b ? a : b;

    public static float Max(float a, float b) => a > b ? a : b;

    public static int Max(int a, int b) => a > b ? a : b;

    public static int CeilToInt(float value) => (int)System.Math.Ceiling(value);

    public static int RoundToInt(float value) => (int)System.Math.Round(value);

    public static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static float Clamp01(float value) => Clamp(value, 0f, 1f);

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public static float InverseLerp(float a, float b, float value) => a == b ? 0f : (value - a) / (b - a);
}

public enum KeyCode
{
    None = 0,
    Return = 13,
    KeypadEnter = 271
}

public enum TextAnchor
{
    UpperLeft = 0,
    UpperCenter = 1,
    UpperRight = 2,
    MiddleLeft = 3,
    MiddleCenter = 4,
    MiddleRight = 5,
    LowerLeft = 6,
    LowerCenter = 7,
    LowerRight = 8
}
