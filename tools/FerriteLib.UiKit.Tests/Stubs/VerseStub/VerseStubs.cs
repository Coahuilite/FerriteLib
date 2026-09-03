using System;
using System.Collections.Generic;
using UnityEngine;

namespace Verse;

/// <summary>Executable stub for the Verse IMGUI types the UiKit test paths touch at runtime.</summary>
public enum GameFont
{
    Tiny,
    Small,
    Medium
}

/// <summary>
/// Minimal Verse persistence contract. Production settings records implement it, so the stub
/// must declare it for those types to load when the tuning/package widget paths run in the
/// harness.
/// </summary>
public interface IExposable
{
    void ExposeData();
}

/// <summary>
/// Minimal Verse float-pair value type (settings/tuning records reference it; only the type and
/// the min/max fields are needed for the widget paths to load).
/// </summary>
public struct FloatRange
{
    public float min;
    public float max;

    public FloatRange(float min, float max)
    {
        this.min = min;
        this.max = max;
    }
}

/// <summary>
/// Minimal Verse tagged string. The real RimWorld type carries rich-text tags and implicit
/// string conversions; the harness only needs the identity/string-conversion surface so the
/// real kernel translation seam can execute without a game language database.
/// </summary>
public struct TaggedString
{
    private readonly string rawText;

    public TaggedString(string rawText)
    {
        this.rawText = rawText ?? "";
    }

    public string RawText => rawText;

    public static implicit operator string(TaggedString taggedString)
    {
        return taggedString.rawText;
    }

    public static implicit operator TaggedString(string str)
    {
        return new TaggedString(str);
    }

    public override string ToString()
    {
        return rawText;
    }
}

/// <summary>
/// Minimal stand-in for Verse's argument wrapper so production code can use the idiomatic
/// <c>"Key".Translate(a, b)</c> form under the harness. Formatting is invariant-culture
/// <c>string.Format</c>, matching what the shipped call sites rely on.
/// </summary>
public readonly struct NamedArgument
{
    private readonly object? value;

    public NamedArgument(object? value)
    {
        this.value = value;
    }

    public static implicit operator NamedArgument(string? value) => new NamedArgument(value);

    public static implicit operator NamedArgument(int value) => new NamedArgument(value);

    public static implicit operator NamedArgument(float value) => new NamedArgument(value);

    public static implicit operator NamedArgument(bool value) => new NamedArgument(value);

    public static implicit operator NamedArgument(TaggedString value) => new NamedArgument(value.RawText);

    public override string ToString()
    {
        return value?.ToString() ?? "";
    }
}

/// <summary>
/// Deterministic translation stub. By default keys pass through unchanged, which keeps the neutral
/// UiKit lanes free of any product vocabulary. A harness may install <see cref="Resolve"/> to make
/// key lookups behave like the real language database, so production widgets can be measured against
/// the exact strings a player would see.
/// </summary>
public static class Translator
{
    /// <summary>Optional resolver: key to displayed text. Null keeps the pass-through behaviour.</summary>
    public static Func<string, string>? Resolve;

    public static TaggedString Translate(this string key)
    {
        Func<string, string>? resolver = Resolve;
        if (resolver != null && key != null)
        {
            string? text = resolver(key);
            if (text != null) return new TaggedString(text);
        }

        return new TaggedString(key ?? "");
    }

    /// <summary>
    /// Argumented translate forms. Verse declares these on
    /// <c>TranslatorFormattedStringExtensions</c> and the compiler binds call sites there, so the stub
    /// carries the same members under both type names; see that class below.
    /// </summary>
    public static TaggedString Translate(this string key, NamedArgument arg)
    {
        return TranslateFormatted(key, arg);
    }

    public static TaggedString Translate(this string key, NamedArgument arg0, NamedArgument arg1)
    {
        return TranslateFormatted(key, arg0, arg1);
    }

    public static TaggedString Translate(
        this string key,
        NamedArgument arg0,
        NamedArgument arg1,
        NamedArgument arg2)
    {
        return TranslateFormatted(key, arg0, arg1, arg2);
    }

    public static TaggedString Translate(
        this string key,
        NamedArgument arg0,
        NamedArgument arg1,
        NamedArgument arg2,
        NamedArgument arg3)
    {
        return TranslateFormatted(key, arg0, arg1, arg2, arg3);
    }

    private static TaggedString TranslateFormatted(string key, params NamedArgument[] args)
    {
        return new TaggedString(FormatWith(Translate(key).RawText, args));
    }

    internal static string FormatWith(string template, params NamedArgument[] args)
    {
        if (args.Length == 0) return template;

        object[] values = new object[args.Length];
        for (int i = 0; i < args.Length; i++) values[i] = args[i].ToString();
        try
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, template, values);
        }
        catch (FormatException)
        {
            // A placeholder/argument mismatch is a content bug. Return the raw template instead of
            // throwing so one bad Keyed row cannot abort an entire harness sweep.
            return template;
        }
    }
}

/// <summary>
/// Stub for the Verse type that actually declares <c>Translate(this string, NamedArgument…)</c>.
/// Reference assemblies bind call sites here, so the name has to exist at runtime too.
/// </summary>
public static class TranslatorFormattedStringExtensions
{
    public static TaggedString Translate(this TaggedString taggedString, NamedArgument arg)
    {
        return new TaggedString(Translator.FormatWith(taggedString.RawText, arg));
    }

    public static TaggedString Translate(this TaggedString taggedString, NamedArgument arg0, NamedArgument arg1)
    {
        return new TaggedString(Translator.FormatWith(taggedString.RawText, arg0, arg1));
    }

    public static TaggedString Translate(this string key, NamedArgument arg)
    {
        return Translator.Translate(key, arg);
    }

    public static TaggedString Translate(this string key, NamedArgument arg0, NamedArgument arg1)
    {
        return Translator.Translate(key, arg0, arg1);
    }

    public static TaggedString Translate(
        this string key,
        NamedArgument arg0,
        NamedArgument arg1,
        NamedArgument arg2)
    {
        return Translator.Translate(key, arg0, arg1, arg2);
    }

    public static TaggedString Translate(
        this string key,
        NamedArgument arg0,
        NamedArgument arg1,
        NamedArgument arg2,
        NamedArgument arg3)
    {
        return Translator.Translate(key, arg0, arg1, arg2, arg3);
    }
}

public static class Text
{
    public static GameFont Font { get; set; }

    public static TextAnchor Anchor { get; set; }

    public static bool WordWrap { get; set; } = true;

    // Deterministic stub so real widget Measure/Draw paths (e.g. ChromeBannerWidget, the US
    // kernel sections) can execute text-height layout without a real IMGUI text engine.
    public static float CalcHeight(string text, float width)
    {
        return 16f;
    }

    /// <summary>
    /// Half-width advance model, the same convention every real UI font follows: a CJK ideograph or
    /// full-width punctuation occupies one em, and a Latin/digit character occupies about half an em.
    /// That makes the stub's widths track what a real font engine reports closely enough to catch a
    /// label that no longer fits its rect, while staying bit-deterministic across runs.
    /// </summary>
    public static Vector2 CalcSize(string text)
    {
        float em = EmOf(Font);
        float units = 0f;
        foreach (char c in text ?? "") units += IsWide(c) ? 2f : 1f;
        return new Vector2(units * em * 0.5f, em * 1.25f);
    }

    private static float EmOf(GameFont font)
    {
        return font switch
        {
            GameFont.Tiny => 12f,
            GameFont.Medium => 18f,
            _ => 16f
        };
    }

    private static bool IsWide(char c)
    {
        return c >= '\u2E80' && (
            c <= '\u303F' || (c >= '\u3400' && c <= '\u4DBF') || (c >= '\u4E00' && c <= '\u9FFF')
            || (c >= '\uAC00' && c <= '\uD7AF') || (c >= '\uF900' && c <= '\uFAFF')
            || (c >= '\uFF00' && c <= '\uFF60') || (c >= '\uFFE0' && c <= '\uFFE6'));
    }
}

public static class Widgets
{
    // Recording hooks used by visual regression tests. The test assembly compiles against the
    // Krafs ref assembly, so these are only accessible through reflection at runtime.
    public static readonly List<Rect> DrawBoxSolidRects = new();
    public static readonly List<Color> DrawBoxSolidColors = new();
    public static int ScrollViewDepth;
    public static int BeginScrollViewCalls;
    public static int EndScrollViewCalls;

    public static void Label(Rect rect, string text)
    {
    }

    public static void DrawBoxSolid(Rect rect, Color color)
    {
        DrawBoxSolidRects.Add(rect);
        DrawBoxSolidColors.Add(color);
    }

    public static void ClearDrawBoxSolidCalls()
    {
        DrawBoxSolidRects.Clear();
        DrawBoxSolidColors.Clear();
    }

    public static bool ButtonInvisible(Rect rect)
    {
        return false;
    }

    public static bool ButtonInvisible(Rect rect, bool doSound)
    {
        return false;
    }

    public static float HorizontalSlider(
        Rect rect,
        float value,
        float min,
        float max,
        bool middleAlignment = false,
        string? label = null,
        string? leftAlignedLabel = null,
        string? rightAlignedLabel = null,
        float roundTo = -1f)
    {
        return value;
    }

    public static string TextField(Rect rect, string text)
    {
        return text ?? "";
    }

    public static void BeginScrollView(Rect outRect, ref Vector2 scrollPosition, Rect viewRect)
    {
        ScrollViewDepth++;
        BeginScrollViewCalls++;
    }

    public static void BeginScrollView(Rect outRect, ref Vector2 scrollPosition, Rect viewRect, bool showVerticalScrollbar)
    {
        ScrollViewDepth++;
        BeginScrollViewCalls++;
    }

    public static void EndScrollView()
    {
        if (ScrollViewDepth > 0) ScrollViewDepth--;
        EndScrollViewCalls++;
    }
}

public static class Mouse
{
    public static bool IsOver(Rect rect)
    {
        return false;
    }
}

public static class Log
{
    public static void Warning(string message)
    {
    }

    // Consumer mods report a failed prerequisite check through Log.Error. The surface has to exist in
    // the stub even though no harness constructs a Mod today, or the first one that does gets a
    // MissingMethodException instead of a readable failure.
    public static void Error(string message)
    {
    }

    public static void Message(string message)
    {
    }
}

public sealed class LoadedLanguage
{
    public string folderName = "";
}

// Only the surface UsKernelTranslation reads to report the active language. Real RimWorld exposes
// these as public static fields on a static class; the shape must match or the production type-load
// differs between harness and game.
public static class LanguageDatabase
{
    public static LoadedLanguage? activeLanguage;
    public static LoadedLanguage? defaultLanguage;
    public static string DefaultLangFolderName = "";
}
