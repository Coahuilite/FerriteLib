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
        return ButtonInvisibleCore(rect);
    }

    public static bool ButtonInvisible(Rect rect, bool doSound)
    {
        return ButtonInvisibleCore(rect);
    }

    // Faithful to the contract the game actually runs: MouseDown over the rect captures the hot
    // control and consumes the event; MouseUp with a matching hot control over the rect activates and
    // consumes it. A stub that always returned false could not express a click being stolen by a
    // control drawn earlier in the same pass - the exact failure shape reported from the game.
    private static bool ButtonInvisibleCore(Rect rect)
    {
        Event? e = Event.current;
        if (e == null || e.button != 0) return false;

        int id = GUIUtility.GetControlID(0, FocusType.Passive);
        if (e.type == EventType.MouseDown && Mouse.IsOver(rect))
        {
            GUIUtility.hotControl = id;
            e.Use();
            return false;
        }

        if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
        {
            GUIUtility.hotControl = 0;
            e.Use();
            return Mouse.IsOver(rect);
        }

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
        BeginScrollView(outRect, ref scrollPosition, viewRect, true);
    }

    public static void BeginScrollView(Rect outRect, ref Vector2 scrollPosition, Rect viewRect, bool showVerticalScrollbar)
    {
        ScrollViewDepth++;
        BeginScrollViewCalls++;
        // Real scroll views open a GUI group whose origin is the visible out rect minus the scroll
        // offset; controls inside then draw and hit-test in content-local space. The stub models that
        // so pointer-space defects like the 2026-09-04 click theft are expressible in the harness.
        UnityEngine.GUI.BeginGroup(new Rect(outRect.x - scrollPosition.x, outRect.y - scrollPosition.y, viewRect.width, viewRect.height));
    }

    public static void EndScrollView()
    {
        if (ScrollViewDepth > 0) ScrollViewDepth--;
        EndScrollViewCalls++;
        UnityEngine.GUI.EndGroup();
    }
}

public static class Mouse
{
    public static bool IsOver(Rect rect)
    {
        Event? e = Event.current;
        if (e == null) return false;
        Vector2 p = e.mousePosition;
        return p.x >= rect.x && p.x <= rect.xMax && p.y >= rect.y && p.y <= rect.yMax;
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

/// <summary>
/// Real RimWorld layer order, measured from the 1.6.4871 reference assembly. Only the names the shell
/// and its consumers assign to are declared.
/// </summary>
public enum WindowLayer
{
    GameUI = 0,
    Dialog = 1,
    SubSuper = 2,
    Super = 3
}

/// <summary>
/// Only the type identity is load-bearing: it appears in <see cref="Window"/>'s constructor signature,
/// which every derived constructor binds to. The game's interface carries the drawing hooks a window
/// delegates its chrome to — exactly the job <c>UiWindowHost</c> does in-library — so no harness path
/// ever supplies an implementation.
/// </summary>
public interface IWindowDrawing
{
}

/// <summary>
/// Minimal executable slice of <c>Verse.Window</c>, added for the US→FL round-1 window shell (P2,
/// condition b: the shell is invisible to the kernel-host lane without it). The shape mirrors the real
/// type wherever the difference would change which IL binds — abstract type, public abstract
/// <c>DoWindowContents</c>, protected virtual <c>Margin</c>, public virtual <c>InitialSize</c>, public
/// virtual <c>WindowOnGUI</c>, and the <c>IWindowDrawing</c> constructor.
/// <para>
/// <c>WindowOnGUI</c> performs the inRect plumbing the game does — offset the content rect by
/// <c>Margin</c> and dispatch — so a lane can drive a real window pass through a member that exists
/// in the game as well. No stub-only recorders: a lane observes behaviour by overriding the real
/// virtuals (<c>Close</c>, <c>PreClose</c>), which is also what proves those overrides bind.
/// </para>
/// </summary>
public abstract class Window
{
    public const float StandardMargin = 12f;

    /// <summary>
    /// Real constructor, read from the 1.6.4871 reference assembly:
    /// <c>public Window(IWindowDrawing customWindowDrawing = null)</c>. A derived constructor's
    /// parameterless <c>base()</c> call compiles onto this slot, so a stub with only an implicit
    /// parameterless constructor dies at first use with
    /// <c>MissingMethodException: Void Verse.Window..ctor(Verse.IWindowDrawing)</c> — which is exactly
    /// what the first run of the window-shell lane reported.
    /// </summary>
    public Window(IWindowDrawing? customWindowDrawing = null)
    {
    }

    public WindowLayer layer;
    public string optionalTitle = "";
    public bool doCloseX = true;
    public bool doCloseButton = true;
    public bool closeOnAccept = true;
    public bool closeOnCancel = true;
    public bool closeOnClickedOutside;
    public bool forcePause;
    public bool preventCameraMotion = true;
    public bool doWindowBackground = true;
    public bool absorbInputAroundWindow;
    public bool draggable = true;
    public bool drawShadow = true;
    public bool focusWhenOpened = true;
    public Rect windowRect;

    protected virtual float Margin => StandardMargin;

    public virtual Vector2 InitialSize => new Vector2(600f, 600f);
    public abstract void DoWindowContents(Rect inRect);

    public virtual void PreOpen()
    {
    }

    public virtual void PostOpen()
    {
    }

    public virtual void PreClose()
    {
    }

    public virtual void PostClose()
    {
    }

    /// <summary>
    /// Real signature, read from the 1.6.4871 reference assembly: <c>public virtual void
    /// Close(bool doCloseSound = true)</c>. The parameter is load-bearing — a shell compiled against
    /// the ref assembly emits a call to the bool overload, so a stub that declares a parameterless
    /// Close is a MissingMethodException at first click instead of a closed window.
    /// </summary>
    public virtual void Close(bool doCloseSound = true)
    {
    }

    /// <summary>One window pass, as the game's window stack would drive it.</summary>
    public virtual void WindowOnGUI()
    {
        float margin = Margin;
        var inRect = new Rect(
            windowRect.x + margin,
            windowRect.y + margin,
            Math.Max(1f, windowRect.width - margin * 2f),
            Math.Max(1f, windowRect.height - margin * 2f));
        DoWindowContents(inRect);
    }
}
