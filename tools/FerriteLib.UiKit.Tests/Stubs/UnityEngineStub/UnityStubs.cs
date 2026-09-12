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

    // Pure component-wise arithmetic is language, not game behaviour, so the double can carry it exactly.
    // The comparison operators are deliberately NOT carried: in the game a vector compares through an
    // epsilon (SqrMagnitude < kEpsilon * kEpsilon), the reference assembly ships stripped bodies so that
    // rule cannot be read from it, and a guessed epsilon would silently change which branch a lane takes.
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);

    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);

    public static Vector2 operator -(Vector2 a) => new(-a.x, -a.y);

    public static Vector2 operator *(Vector2 a, float s) => new(a.x * s, a.y * s);

    public static Vector2 operator *(float s, Vector2 a) => new(a.x * s, a.y * s);

    public static Vector2 operator /(Vector2 a, float s) => new(a.x / s, a.y / s);
}

/// <summary>
/// Added 2026-09-12 with the language/constant batch. A consumer's in-world math reads <c>x/y/z</c>,
/// <c>sqrMagnitude</c> and the arithmetic operators on this type exactly as it does on
/// <see cref="Vector2"/>, and every one of those was a harness-only death while the type did not exist.
/// Field and accessor shapes are the game's (x/y/z are fields, magnitudes are properties), verified
/// against the 1.6.4871 reference assembly; the comparison operators are left out for the reason stated
/// on <see cref="Vector2"/>.
/// </summary>
public struct Vector3
{
    public float x;
    public float y;
    public float z;

    public Vector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static Vector3 zero => new(0f, 0f, 0f);

    public static Vector3 one => new(1f, 1f, 1f);

    public float sqrMagnitude => x * x + y * y + z * z;

    public float magnitude => (float)System.Math.Sqrt(sqrMagnitude);

    public static float Distance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        float dz = a.z - b.z;
        return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
    {
        return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
    }

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);

    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);

    public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);

    public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);

    public static Vector3 operator *(float s, Vector3 a) => new(a.x * s, a.y * s, a.z * s);

    public static Vector3 operator /(Vector3 a, float s) => new(a.x / s, a.y / s, a.z / s);
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

    // A static property in the game (verified against the reference assembly), so a consumer reads it
    // instead of building an empty rect by hand.
    public static Rect zero => new(0f, 0f, 0f, 0f);

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

    // The named constants a consumer reads. Values are Unity's documented constants (the numeric yellow
    // is the HTML one the game uses); white and clear were already here. Color's arithmetic and equality
    // operators are NOT carried: equality compares through Vector4 with an epsilon that the stripped
    // reference assembly cannot show, and a guessed epsilon would silently change a branch.
    public static Color black => new(0f, 0f, 0f, 1f);

    public static Color red => new(1f, 0f, 0f, 1f);

    public static Color green => new(0f, 1f, 0f, 1f);

    public static Color blue => new(0f, 0f, 1f, 1f);

    public static Color yellow => new(1f, 0.9215686f, 0.01568628f, 1f);

    public static Color cyan => new(0f, 1f, 1f, 1f);

    public static Color magenta => new(1f, 0f, 1f, 1f);

    public static Color gray => new(0.5f, 0.5f, 0.5f, 1f);

    public static Color grey => new(0.5f, 0.5f, 0.5f, 1f);
}

public static class Mathf
{
    public const float PI = 3.14159274f;

    public static float Abs(float value) => System.Math.Abs(value);

    public static float Min(float a, float b) => a < b ? a : b;

    // The integer half of the pair above. Measured 2026-09-12: a consumer called Mathf.Clamp(int, int, int)
    // and this stub carried only the float forms, so the call died with MissingMethodException inside the
    // harness only - the page's timing card was replaced by its recovery band and never drew.
    public static int Min(int a, int b) => a < b ? a : b;

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

    // Same order as the float overload (minimum first), which is Unity's own order for both and the only
    // order a consumer can rely on: if the two variants disagreed, code that switches between int and
    // float would clamp differently for the same numbers.
    public static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static float Clamp01(float value) => Clamp(value, 0f, 1f);

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public static float InverseLerp(float a, float b, float value) => a == b ? 0f : (value - a) / (b - a);
}

/// <summary>
/// The base type of every game object, carried for its two operators rather than for its identity model.
/// <para>
/// What this double cannot model, stated rather than hidden: the game's <c>==</c> answers "is this object
/// alive?" through a native instance id, so a destroyed object compares equal to null there. A managed
/// double has no native side, so <c>==</c> here is reference identity plus the null cases, and a lane that
/// needs destroyed-object behaviour has to fake the null itself and say so.
/// </para>
/// <para>
/// <see cref="Equals(object)"/> and <see cref="GetHashCode"/> are overridden because C# requires them
/// beside an operator pair, and they use the same identity rule so the two cannot drift apart.
/// </para>
/// </summary>
public class Object
{
    public static bool operator ==(Object? a, Object? b) => ReferenceEquals(a, b);

    public static bool operator !=(Object? a, Object? b) => !ReferenceEquals(a, b);

    public override bool Equals(object? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
}

/// <summary>
/// The engine clock. The reference assembly declares both members get-only, so this double can only
/// answer, and it answers zero: this harness never advances the engine clock, and a stub that returned a
/// made-up ticking value would let a lane assert against a number the game would not produce at that
/// point. A lane that needs a fixed clock has to say what it is faking instead of reading it here.
/// </summary>
public static class Time
{
    public static int frameCount => 0;

    public static float realtimeSinceStartup => 0f;
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
