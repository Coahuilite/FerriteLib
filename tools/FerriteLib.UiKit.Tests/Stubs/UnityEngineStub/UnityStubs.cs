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

    public static Vector2 up => new(0f, 1f);

    public static Vector2 down => new(0f, -1f);

    public static Vector2 left => new(-1f, 0f);

    public static Vector2 right => new(1f, 0f);

    public static Vector2 positiveInfinity => new(float.PositiveInfinity, float.PositiveInfinity);

    public static Vector2 negativeInfinity => new(float.NegativeInfinity, float.NegativeInfinity);

    public float sqrMagnitude => x * x + y * y;

    public float magnitude => (float)System.Math.Sqrt(sqrMagnitude);

    // Definition-based value arithmetic. NOT carried: normalized/Normalize/ClampMagnitude (all defined
    // through a magnitude-under-epsilon guard the stripped reference cannot show, and the zero-vector answer
    // is exactly where a guess would show), Angle/SignedAngle (the same guarded acos form), the indexer
    // (component order is documented, not readable here), the implicit Vector2<->Vector3 conversions (they
    // widen every overload set a consumer calls, and nothing references them), the equality operators (the
    // epsilon question) and SmoothDamp* (a state machine).
    public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;

    public static float SqrMagnitude(Vector2 a) => a.sqrMagnitude;

    public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t)
    {
        return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
    }

    public static Vector2 Min(Vector2 a, Vector2 b) => new(System.Math.Min(a.x, b.x), System.Math.Min(a.y, b.y));

    public static Vector2 Max(Vector2 a, Vector2 b) => new(System.Math.Max(a.x, b.x), System.Math.Max(a.y, b.y));

    public static Vector2 Scale(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);

    public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);

    public static Vector2 operator /(Vector2 a, Vector2 b) => new(a.x / b.x, a.y / b.y);

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

    public static Vector3 up => new(0f, 1f, 0f);

    public static Vector3 down => new(0f, -1f, 0f);

    public static Vector3 left => new(-1f, 0f, 0f);

    public static Vector3 right => new(1f, 0f, 0f);

    public static Vector3 forward => new(0f, 0f, 1f);

    public static Vector3 back => new(0f, 0f, -1f);

    public static Vector3 positiveInfinity => new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

    public static Vector3 negativeInfinity => new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

    // Same exclusions as Vector2 (normalized, Angle/SignedAngle, indexer, implicit conversions, equality
    // operators, SmoothDamp*), and here also Project/Reflect/Slerp/RotateTowards, whose rules are guarded
    // or iterative. What is carried is the arithmetic defined by its own name.
    public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

    public static float SqrMagnitude(Vector3 a) => a.sqrMagnitude;

    public static float Magnitude(Vector3 a) => a.magnitude;

    public static Vector3 Cross(Vector3 a, Vector3 b)
    {
        return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
    }

    public static Vector3 Min(Vector3 a, Vector3 b)
    {
        return new Vector3(System.Math.Min(a.x, b.x), System.Math.Min(a.y, b.y), System.Math.Min(a.z, b.z));
    }

    public static Vector3 Max(Vector3 a, Vector3 b)
    {
        return new Vector3(System.Math.Max(a.x, b.x), System.Math.Max(a.y, b.y), System.Math.Max(a.z, b.z));
    }

    public static Vector3 Scale(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);

    public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t)
    {
        return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
    }

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

    // The rest of the value family (task-105): pure reads of the four stored numbers, so each rule is the
    // arithmetic it names and nothing else. NOT carried from Rect, with reasons: Contains/Overlaps (their
    // edge-inclusive rules are not in the stripped reference, and a wrong boundary silently changes a hit
    // test), the equality operators (the epsilon question), the four OBSOLETE aliases (the reference marks
    // left/top/right/bottom [Obsolete] in favour of xMin/yMin/xMax/yMax, and the stub carries the current
    // names so a consumer calling an alias gets the same compiler warning here as in the game, which is why
    // no lane can exercise them), and MinMaxRect/OrderMinMax/NormalizedToPoint/
    // PointToNormalized/Set (behaviour over authored numbers, referenced by nothing).
    public float xMin => _x;

    public float yMin => _y;

    public Vector2 min => new(_x, _y);

    public Vector2 max => new(_x + _width, _y + _height);

    public Vector2 size => new(_width, _height);

    public Vector2 center => new(_x + _width * 0.5f, _y + _height * 0.5f);
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

    // Component-wise colour arithmetic, defined by its own name. NOT carried: grayscale and
    // maxColorComponent (their exact reduction is not in the stripped reference), linear/gamma (the sRGB
    // conversion), the HSV pair (long documented formulas would be a from-memory port), the Vector4
    // implicit conversions (Vector4 is not stubbed at all), the equality operators (the epsilon
    // question) and RGBMultiplied/AlphaMultiplied (the reference carries them but they are not public
    // there, so they are not surface a consumer can call).
    // question).
    public static Color operator +(Color a, Color b) => new(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);

    public static Color operator -(Color a, Color b) => new(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);

    public static Color operator *(Color a, Color b) => new(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

    public static Color operator *(Color a, float s) => new(a.r * s, a.g * s, a.b * s, a.a * s);

    public static Color operator *(float s, Color a) => a * s;

    public static Color operator /(Color a, float s) => new(a.r / s, a.g / s, a.b / s, a.a / s);

    public static Color Lerp(Color a, Color b, float t)
    {
        float clamped = Mathf.Clamp01(t);
        return new Color(
            a.r + (b.r - a.r) * clamped,
            a.g + (b.g - a.g) * clamped,
            a.b + (b.b - a.b) * clamped,
            a.a + (b.a - a.a) * clamped);
    }

    public static Color LerpUnclamped(Color a, Color b, float t)
    {
        return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
    }
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

    // ----------------------------------------------------------------------------------------------------
    // The pure math family (2026-09-12, task-101). Every one of these is its BCL counterpart or its own
    // published definition, so the double carries the exact rule instead of a plausible number: Sqrt, the
    // integer Abs, the rounding functions and FloorToInt, the six trigonometry functions, Pow/Exp/Log/Log10,
    // and the four that are defined by their own formulas (Repeat, PingPong, LerpUnclamped, SmoothStep).
    // The gap became visible the loud way: a consumer's circle drawing called Mathf.Sqrt, the trip guard
    // named the member and the call path, and a lane that calls a member now puts it under the
    // reference-driven gate permanently.
    //
    // Deliberately NOT carried, under the same rule that keeps the vector comparison operators out: a member
    // whose rule cannot be verified must fail loudly rather than guess. That covers Sign (its zero case is
    // not the BCL's), MoveTowards / LerpAngle / MoveTowardsAngle / DeltaAngle (all defined through Sign),
    // Approximately (an epsilon nobody can read), SmoothDamp* (an iterative state machine), the array
    // Min/Max overloads (their empty-array answer is not published here), Epsilon, and the engine internals
    // (Perlin noise, gamma and colour-temperature conversions, the numeric-formatting helpers).
    public static float Sqrt(float value) => (float)System.Math.Sqrt(value);

    public static int Abs(int value) => System.Math.Abs(value);

    public static float Floor(float value) => (float)System.Math.Floor(value);

    public static float Ceil(float value) => (float)System.Math.Ceiling(value);

    // System.Math.Round is banker's rounding and so is Unity's; the lane pins 2.5 -> 2 so the rule is
    // recorded rather than assumed.
    public static float Round(float value) => (float)System.Math.Round(value);

    public static int FloorToInt(float value) => (int)System.Math.Floor(value);

    public static float Sin(float value) => (float)System.Math.Sin(value);

    public static float Cos(float value) => (float)System.Math.Cos(value);

    public static float Tan(float value) => (float)System.Math.Tan(value);

    public static float Asin(float value) => (float)System.Math.Asin(value);

    public static float Acos(float value) => (float)System.Math.Acos(value);

    public static float Atan(float value) => (float)System.Math.Atan(value);

    public static float Atan2(float y, float x) => (float)System.Math.Atan2(y, x);

    public static float Pow(float f, float p) => (float)System.Math.Pow(f, p);

    public static float Exp(float power) => (float)System.Math.Exp(power);

    public static float Log(float f) => (float)System.Math.Log(f);

    public static float Log(float f, float p) => (float)System.Math.Log(f, p);

    public static float Log10(float f) => (float)System.Math.Log10(f);

    // Repeat is Unity's own definition: t - floor(t / length) * length. PingPong composes it, which is why
    // the two arrive together - one without the other would be half a rule.
    public static float Repeat(float t, float length) => t - Floor(t / length) * length;

    public static float PingPong(float t, float length)
    {
        float repeated = Repeat(t, length * 2f);
        return length - (float)System.Math.Abs(repeated - length);
    }

    public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;

    // Unity's smooth step: clamp t, then the Hermite curve 3t^2 - 2t^3.
    public static float SmoothStep(float from, float to, float t)
    {
        float clamped = Clamp01(t);
        clamped = -2f * clamped * clamped * clamped + 3f * clamped * clamped;
        return to * clamped + from * (1f - clamped);
    }

    public static float Infinity => float.PositiveInfinity;

    public static float NegativeInfinity => float.NegativeInfinity;

    // Both are arithmetic identities over PI, so they need no external source.
    public static float Deg2Rad => PI * 2f / 360f;

    public static float Rad2Deg => 360f / (PI * 2f);
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
