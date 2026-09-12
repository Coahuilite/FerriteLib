using System;
using System.Collections.Generic;
using UnityEngine;

// Harness stub, and part of the de-facto published shape: a consumer's kernel-host lane builds these
// projects in place and copies them out of bin/stubs/<name>/ (AGENTS.md, "Build and verification").
//
// 2026-09-12: GenUI gained the Rect insets a consumer reaches for. The lesson is the reason this header
// carries it: a stub missing one game member does not fail loudly - the call throws TypeLoadException at
// JIT time, the session guard swaps that element for a recovery band, and a lane that only asserts
// "the session is still alive" keeps passing while the product code under test never runs. Coverage in
// the harness is "the element drew", never "the frame survived" (KernelStubCoverageTests is that rule
// with a positive control, and KernelTripGuard is the assertion lanes call).
//
// 2026-09-11: Widgets.Label stopped being a no-op. It now appends three records - LabelRects,
// LabelTexts and LabelColors (the colour the outlet had applied through GUI.color). Purely additive:
// no existing member changed shape, nothing was removed, and the lists are only read by this repo's
// own lanes through reflection. They exist because "which colour did this outlet write" is otherwise
// unobservable in the harness, and that is exactly what the resolved-style lane's one-table
// assertions have to check (KernelResolvedStyleTests, the four text outlets).

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

    // The game's own definition, read from Source/Verse/FloatRange.cs:15 - a range of exactly one.
    public static FloatRange One => new FloatRange(1f, 1f);

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

    // Concatenation, verbatim from Source/Verse/TaggedString.cs:130-145: the raw texts join and the result
    // is a new tagged string. The game keeps both directions and the string operands, so the double does,
    // and a lane can tell the three apart because the raw text is observable.
    public static TaggedString operator +(TaggedString t1, TaggedString t2)
    {
        return new TaggedString(t1.rawText + t2.rawText);
    }

    public static TaggedString operator +(string t1, TaggedString t2)
    {
        return new TaggedString(t1 + t2.rawText);
    }

    public static TaggedString operator +(TaggedString t1, string t2)
    {
        return new TaggedString(t1.rawText + t2);
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
/// <summary>
/// The two string predicates a consumer reaches for constantly. Both bodies are the game's own, read from
/// Source/Verse/GenText.cs (NullOrEmpty is an extension there too, and SanitizeFilename composes the
/// platform's invalid set with a fixed tail before collapsing runs and trimming trailing dots). The double
/// calls the same BCL member for the platform set, so it inherits the same platform dependence rather than
/// inventing one.
/// </summary>
public static class GenText
{
    public static bool NullOrEmpty(this string str)
    {
        return string.IsNullOrEmpty(str);
    }

    public static string SanitizeFilename(string str)
    {
        // The game writes ToArray() there because that file already imports System.Linq; ToCharArray is the
        // same char sequence without dragging a using directive in for one call.
        return string.Join("_", str.Split(GetInvalidFilenameCharacters().ToCharArray(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    private static string GetInvalidFilenameCharacters()
    {
        return new string(System.IO.Path.GetInvalidFileNameChars()) + "/\\{}<>:*|!@#$%^&*?";
    }
}

/// <summary>
/// The two list helpers a consumer's filters call. Bodies read from Source/Verse/GenCollection.cs:1032 and
/// :1224 - <c>Any</c> is <c>FindIndex != -1</c> and <c>Count</c> walks the list - so both can be carried
/// exactly; they depend on nothing but their arguments.
/// </summary>
public static class GenCollection
{
    public static bool Any<T>(this List<T> list, Predicate<T> predicate)
    {
        return list.FindIndex(predicate) != -1;
    }

    public static int Count<T>(this List<T> list, Predicate<T> predicate)
    {
        int num = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (predicate(list[i]))
            {
                num++;
            }
        }
        return num;
    }
}

public static class Translator
{
    /// <summary>Optional resolver: key to displayed text. Null keeps the pass-through behaviour.</summary>
    public static Func<string, string>? Resolve;

    /// <summary>
    /// The game's lookup contract (Source/Verse/Translator.cs:27-45): an empty key is not found and echoes
    /// itself; a found key returns the language's text. The double's language data IS
    /// <see cref="Resolve" />, so found means the resolver produced text. Two deviations are deliberate and
    /// named here: the game logs an error and reports SUCCESS when no language is active (mirroring that
    /// would make every lookup in a harness read as found), and the double never logs.
    /// </summary>
    public static bool TryTranslate(this string key, out TaggedString result)
    {
        if (key.NullOrEmpty())
        {
            result = key;
            return false;
        }

        Func<string, string>? resolver = Resolve;
        if (resolver != null)
        {
            string? text = resolver(key);
            if (text != null)
            {
                result = new TaggedString(text);
                return true;
            }
        }

        result = new TaggedString(key);
        return false;
    }

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
    // Label calls are recorded together with the colour the outlet had applied through GUI.color.
    // The text colour a site resolves is otherwise invisible to the harness, and "which colour did
    // this site write" is exactly what a one-table assertion has to observe.
    public static readonly List<Rect> LabelRects = new();
    public static readonly List<string> LabelTexts = new();
    public static readonly List<Color> LabelColors = new();
    public static int ScrollViewDepth;
    public static int BeginScrollViewCalls;
    public static int EndScrollViewCalls;

    public static void Label(Rect rect, string text)
    {
        LabelRects.Add(rect);
        LabelTexts.Add(text ?? "");
        LabelColors.Add(GUI.color);
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

/// <summary>
/// The game's Rect insets, because a consumer's page code reaches for them.
/// <para>
/// This type exists because of a measured hole, not for completeness (2026-09-12). A consumer drawing
/// <c>rect.ContractedBy(8f)</c> called into <c>Verse.GenUI</c>, which this stub did not declare, so the
/// call threw <c>TypeLoadException</c> at JIT time inside the harness; the session guard replaced that
/// element with a recovery band and the consumer's lane - which asserted only that the session was still
/// alive - stayed green for as long as the hole existed. In the game the same code draws, so nothing
/// failed until somebody read a trip log. The member belongs to the game, so the stub is where it goes:
/// a stub that lacks it makes the harness stop drawing the very code it claims to cover, and every later
/// consumer pays the same tax.
/// </para>
/// <para>
/// Semantics copied from the game's own <c>Verse/GenUI.cs</c> (read 2026-09-12 through the local RimWorld
/// source index; <c>ContractedBy</c> at lines 578-586, <c>ExpandedBy</c> at 573-576): a plain four-side
/// inset with no clamping, so a margin larger than half the extent produces a negative width or height,
/// and a negative margin expands. The per-axis overload and <c>ExpandedBy</c> are that class's immediate
/// neighbours and are included because they sit one lookup away from the same failure. What is *not*
/// claimed: that these are the only members a consumer might need - the class is much larger, and the
/// next missing one is found the same way, by a lane that asserts the element drew.
/// </para>
/// </summary>
public static class GenUI
{
    /// <summary>Insets all four sides by <paramref name="margin"/>; no clamping, so a negative value expands.</summary>
    public static Rect ContractedBy(this Rect rect, float margin)
    {
        return new Rect(rect.x + margin, rect.y + margin, rect.width - margin * 2f, rect.height - margin * 2f);
    }

    /// <summary>Per-axis inset: x and width by <paramref name="marginX"/>, y and height by <paramref name="marginY"/>.</summary>
    public static Rect ContractedBy(this Rect rect, float marginX, float marginY)
    {
        return new Rect(rect.x + marginX, rect.y + marginY, rect.width - marginX * 2f, rect.height - marginY * 2f);
    }

    /// <summary>The game's mirror image: negative margins on the inset are growth here.</summary>
    public static Rect ExpandedBy(this Rect rect, float marginX, float marginY)
    {
        return new Rect(rect.x - marginX, rect.y - marginY, rect.width + marginX * 2f, rect.height + marginY * 2f);
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

/// <summary>
/// The UI-scale surface a consumer's window clamp reads. The reference assembly declares these as two
/// public static int FIELDS (verified against 1.6.4871), not as properties, so the double matches that
/// shape and a lane can pin a viewport the way the game pins it at startup. Before this type existed,
/// every read of it died in the harness only (task-95, the language/constant batch).
/// </summary>
public static class UI
{
    public static int screenWidth = 1920;

    public static int screenHeight = 1080;
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
