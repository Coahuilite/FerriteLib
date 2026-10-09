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

/// <summary>Only the base type contract needed to construct consumer settings; no Scribe simulation.</summary>
public class ModSettings : IExposable
{
    public virtual void ExposeData() { }
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

    // Deterministic wrap model, so real widget Measure/Draw paths (e.g. ChromeBannerWidget, the US
    // kernel sections) can execute text-height layout without a real IMGUI text engine.
    //
    // 2026-09-24 (T27): this returned a constant 16f. A constant makes every height budget and every vertical
    // fit verdict vacuous in the harness - a wrapped paragraph cannot report taller than one line, so "the
    // band holds the text" was decided by the rect alone. The width model beside it was always real, which is
    // how one axis stayed measurable while the other did not. Both axes now share ONE line height; see
    // LineHeight, which CalcSize uses too.
    public static float CalcHeight(string text, float width)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        return WrappedLines(text, width) * LineHeight(Font);
    }

    /// <summary>
    /// How many lines <paramref name="text"/> occupies in <paramref name="width"/>: the half-width advance
    /// CalcSize reports, divided by the available width, rounded up and at least one.
    /// </summary>
    private static int WrappedLines(string text, float width)
    {
        float em = EmOf(Font);
        float units = 0f;
        foreach (char c in text) units += IsWide(c) ? 2f : 1f;
        float advance = units * em * 0.5f;
        return Math.Max(1, (int)Math.Ceiling(advance / Math.Max(1f, width)));
    }

    /// <summary>
    /// ONE line height, calibrated from in-game <c>ui.text.overflow</c> need values (tiny 18.0, small
    /// 21.33333, medium 30.0) and shared by <see cref="CalcHeight"/> and <see cref="CalcSize"/>. It is measured
    /// rather than derived: a wrong line height is not a small error, it is the difference between a band that
    /// holds its lines and one that does not. <b>Large is NOT calibrated</b> - no in-game value exists for it
    /// yet - so it borrows small's until someone measures it; filling that gap with a formula would put the
    /// same defect in a new place.
    /// </summary>
    private static float LineHeight(GameFont font)
    {
        return font switch
        {
            GameFont.Tiny => 18f,
            GameFont.Medium => 30f,
            _ => 21.33333f
        };
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
        // One line's height is LineHeight(font) - the same value CalcHeight multiplies by its line count.
        // Two different line heights inside one stub is the drift this pair exists to prevent.
        return new Vector2(units * em * 0.5f, LineHeight(Font));
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
        UnityEngine.GUI.BeginGroup(outRect);
        UnityEngine.GUI.BeginGroup(new Rect(-scrollPosition.x, -scrollPosition.y, viewRect.width, viewRect.height));
    }

    public static void EndScrollView()
    {
        if (ScrollViewDepth > 0) ScrollViewDepth--;
        EndScrollViewCalls++;
        UnityEngine.GUI.EndGroup();
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

    /// <summary>The rect with its position zeroed and its size kept - the game's draw-group face.</summary>
    public static Rect AtZero(this Rect rect)
    {
        return new Rect(0f, 0f, rect.width, rect.height);
    }
}

public static class Mouse
{
    public static bool IsOver(Rect rect)
    {
        Event? e = Event.current;
        if (e == null) return false;
        Vector2 p = e.mousePosition;
        return GUI.IsPointVisible(p) && p.x >= rect.x && p.x <= rect.xMax && p.y >= rect.y && p.y <= rect.yMax;
    }
}

public static class Log
{
    // Optional scoped observer for consumer diagnostics tests; no messages are retained by the stub.
    public static System.Action<string>? MessageObserver;
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
        MessageObserver?.Invoke(message);
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
    public bool onlyDrawInDevMode;
    // Field defaults are the GAME'S OWN, read from Source/Verse/Window.cs through the local source index.
    // A reference assembly advertises the fields but not their initializers, which is exactly how this
    // double drifted before (2026-09-15, the R-extra-3 finding): doCloseX, doCloseButton,
    // closeOnClickedOutside, forcePause, absorbInputAroundWindow, resizeable and draggable have no
    // initializer and rest false; closeOnAccept, closeOnCancel, preventCameraMotion, doWindowBackground,
    // drawShadow and focusWhenOpened rest true; onlyOneOfTypeAllowed rests true. A double whose resting
    // values disagree with the game is a harness that measures a different game, so
    // KernelWindowCatalogTests pins the five that drifted and a drift reddens a lane.
    public bool doCloseX;
    public bool doCloseButton;
    public bool closeOnAccept = true;
    public bool closeOnCancel = true;
    public bool closeOnClickedOutside;
    public bool forcePause;
    public bool preventCameraMotion = true;
    public bool doWindowBackground = true;
    public bool onlyOneOfTypeAllowed = true;
    public bool absorbInputAroundWindow;
    public bool resizeable;
    public bool draggable;
    public bool drawShadow = true;
    public bool focusWhenOpened = true;
    // Existence read from the 1.6.4871 reference assembly (public bool, no initializer -> rests false, the
    // game's own default). It is the eligibility half of the window stack's Accept/Cancel dispatch: a window
    // that does not close on a key can still ask to hear it. FL-IC2's shell does NOT set it, because the
    // library must not rewrite a vanilla convention on a consumer's behalf; the double carries it so a lane
    // can show the eligibility rule and a consumer's own wiring can be written against a member that exists.
    public bool forceCatchAcceptAndCancelEventEvenIfUnfocused;
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

    /// <summary>
    /// The close-refusal hook, read from the 1.6.4871 reference assembly
    /// (<c>public virtual bool OnCloseRequest()</c>). The game's <c>WindowStack.TryRemove</c> asks it
    /// before removing, and a false keeps the window in the stack - which is the "a consumer's refusal
    /// to close is preserved" half of the 0.5.x window contract.
    /// </summary>
    public virtual bool OnCloseRequest()
    {
        return true;
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
        // The game's own route: a window closing asks the stack to remove it, and the stack asks
        // OnCloseRequest first, so a refusal keeps the window open. The double had an empty body while no
        // lane needed the consequence; FL-IC2's late second Cancel check reads exactly that consequence (a
        // window closed during its own pass is no longer IsOpen), so an empty Close would have made the
        // double answer a question the game answers differently.
        Find.WindowStack.TryRemove(this, doCloseSound);
    }

    /// <summary>
    /// The game's Cancel hook (<c>public virtual void OnCancelKeyPressed()</c>, existence read from the
    /// 1.6.4871 reference assembly). The body below is the plan's reading made executable, the same stance
    /// <see cref="WindowStack"/> documents for the add/remove order: close when the window says it closes on
    /// Cancel, and consume the event so the key cannot also reach a control drawn later in the pass. A lane
    /// asserts only what that reading and the library's own behaviour agree on — that the hook runs BEFORE
    /// <c>DoWindowContents</c>, that a consumed key performs one action, and that a window which declines to
    /// close leaves the event alone.
    /// </summary>
    public virtual void OnCancelKeyPressed()
    {
        if (closeOnCancel)
        {
            Close();
            Event? current = Event.current;
            if (current != null) current.Use();
        }
    }

    /// <summary>The Accept half of <see cref="OnCancelKeyPressed"/>, same provenance and same reading.</summary>
    public virtual void OnAcceptKeyPressed()
    {
        if (closeOnAccept)
        {
            Close();
            Event? current = Event.current;
            if (current != null) current.Use();
        }
    }

    /// <summary>
    /// Whether this window is still in the stack - the game's own answer, which the late second Cancel check
    /// below reads: a window that a control closed during the contents pass must not be closed a second time
    /// by the same key.
    /// </summary>
    public bool IsOpen => Find.WindowStack.IsOpen(this);

    /// <summary>One window pass, as the game's window stack would drive it.</summary>
    public virtual void WindowOnGUI()
    {
        // The key dispatch, at the top of the pass and before the contents - the order FL-IC2's ladder depends
        // on, and the reason a lane may not claim "the hook fired" by calling Widget.Draw alone. The game
        /// asks a player-rebindable key binding here; the double reads the two default keys directly, which
        /// is a documented simplification: nothing in this repository's claim depends on WHICH key is bound,
        /// only on when the dispatch happens and what a consumed key cannot do afterwards.
        Event? keyEvent = Event.current;
        if (keyEvent != null && keyEvent.type == EventType.KeyDown)
        {
            if (keyEvent.keyCode == KeyCode.Escape) Find.WindowStack.Notify_PressedCancel();
            else if (keyEvent.keyCode == KeyCode.Return || keyEvent.keyCode == KeyCode.KeypadEnter)
            {
                Find.WindowStack.Notify_PressedAccept();
            }
        }

        float margin = Margin;
        var inRect = new Rect(
            windowRect.x + margin,
            windowRect.y + margin,
            Math.Max(1f, windowRect.width - margin * 2f),
            Math.Max(1f, windowRect.height - margin * 2f));
        DoWindowContents(inRect);

        // The late second check the game performs after the contents: a key the window never consumed (no
        // eligible handler, or a handler that declined) still closes a Cancel-closing window at the end of
        // the pass. Re-reading IsOpen is the point - a window closed during its own contents pass is gone,
        // and a key consumed by the ladder above reads as Used, not KeyDown, so it cannot fire twice.
        Event? late = Event.current;
        if (late != null && late.type == EventType.KeyDown && late.keyCode == KeyCode.Escape && IsOpen)
        {
            OnCancelKeyPressed();
        }
    }
}


/// <summary>
/// Minimal executable slice of <c>Verse.WindowStack</c> for the 0.5.x window-instance round.
/// <para>
/// <b>Why the double carries the lifecycle at all.</b> The library composes the game's stack instead of
/// building a second scheduler, so the behaviour under test IS the vanilla add/remove contract: <c>Add</c>
/// removes same-typed windows by the <c>onlyOneOfTypeAllowed</c> flag the <i>existing</i> windows carry
/// (which is why a generic shell that ignores it lets two panels close each other), and <c>TryRemove</c>
/// asks <c>OnCloseRequest</c> first, so a refusal keeps the window. Without those two bodies a lane could
/// only assert what the library did to its own map, never that the vanilla path it relies on behaves the
/// way the plan records.
/// </para>
/// <para>
/// <b>Provenance of the emulation.</b> The ordering (Add -> RemoveWindowsOfType, then
/// PreOpen/PostOpen; TryRemove -> OnCloseRequest, then PreClose/PostClose) is read from the master plan's
/// IL-level note for the 1.6 Assembly-CSharp build, not from a game run; the exact intra-<c>Add</c>
/// ordering is not observable in a reference assembly, so this double is the plan's reading made
/// executable and the lanes assert only what both readings agree on. <c>GetsInput</c>,
/// <c>Notify_ClickedInsideWindow</c>, immediate windows and the resolution-change path are deliberately
/// absent: nothing this repository references needs them, and a double that grows guessed behaviour is
/// worse than one that is honestly small.
/// </para>
/// <para>
/// <c>focusedWindow</c> is public here while the real field is private: the test assembly compiles
/// against the game reference assembly, so a lane can only read it through reflection, the same shape
/// <c>Widgets.LabelTexts</c> already uses. It exists so "activation called the vanilla focus setter" is
/// observable rather than asserted.
/// </para>
/// </summary>
public class WindowStack
{
    private readonly List<Window> windows = new List<Window>();

    public IList<Window> Windows => windows;

    public int Count => windows.Count;

    public Window this[int index] => windows[index];

    /// <summary>The window the game would route Accept/Cancel to; set by <see cref="Notify_ManuallySetFocus"/>.</summary>
    public Window? focusedWindow;

    public bool WindowsForcePause
    {
        get
        {
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i].forcePause) return true;
            }

            return false;
        }
    }

    public bool WindowsPreventCameraMotion
    {
        get
        {
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i].preventCameraMotion) return true;
            }

            return false;
        }
    }

    public void Add(Window window)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));

        RemoveWindowsOfType(window.GetType());
        window.PreOpen();
        windows.Add(window);
        window.PostOpen();
    }

    public bool TryRemove(Window window, bool doCloseSound = true)
    {
        if (window == null) return false;
        if (!windows.Contains(window)) return false;
        if (!window.OnCloseRequest()) return false;

        window.PreClose();
        windows.Remove(window);
        window.PostClose();
        if (ReferenceEquals(focusedWindow, window)) focusedWindow = null;
        return true;
    }

    public bool IsOpen(Window window)
    {
        return window != null && windows.Contains(window);
    }

    public void Notify_ManuallySetFocus(Window window)
    {
        focusedWindow = window;
    }

    /// <summary>
    /// Routes Cancel to the window the stack would route it to. The eligibility test is the plan's reading
    /// made executable (see the class header): a window hears the key when it closes on Cancel or asks to
    /// catch the key anyway, AND the stack lets it receive input. Only the first eligible window is asked,
    /// and it is asked once - the game breaks after dispatching, which is half of why one press cannot undo
    /// two layers through this door.
    /// </summary>
    public void Notify_PressedCancel()
    {
        for (int i = 0; i < windows.Count; i++)
        {
            Window window = windows[i];
            if ((window.closeOnCancel || window.forceCatchAcceptAndCancelEventEvenIfUnfocused) && GetsInput(window))
            {
                window.OnCancelKeyPressed();
                return;
            }
        }
    }

    /// <summary>The Accept half of <see cref="Notify_PressedCancel"/>, same eligibility shape.</summary>
    public void Notify_PressedAccept()
    {
        for (int i = 0; i < windows.Count; i++)
        {
            Window window = windows[i];
            if ((window.closeOnAccept || window.forceCatchAcceptAndCancelEventEvenIfUnfocused) && GetsInput(window))
            {
                window.OnAcceptKeyPressed();
                return;
            }
        }
    }

    /// <summary>
    /// The input-receiving test, narrowed to the one rule this repository has a written fact about: a window
    /// above it in the stack that absorbs input around itself takes the key away (<c>00-baseline.md</c> §
    /// vanilla-boundary, "GetsInput 受上层 absorbInputAroundWindow 影响"). Beyond that, the topmost added window
    /// or the focused one receives. The real method also consults obscuring and mouse position; no lane here
    /// depends on either, and this double states what it does not model rather than guessing at it.
    /// </summary>
    public bool GetsInput(Window window)
    {
        int index = windows.IndexOf(window);
        if (index < 0) return false;

        for (int i = index + 1; i < windows.Count; i++)
        {
            if (windows[i].absorbInputAroundWindow) return false;
        }

        return index == windows.Count - 1 || ReferenceEquals(focusedWindow, window);
    }

    /// <summary>
    /// The exact-type sibling removal the vanilla <c>Add</c> performs, driven by the flag on the windows
    /// already in the stack. Exact type, not assignable-from: the vanilla type publishes
    /// <c>TryRemove(Type)</c> and <c>TryRemoveAssignableFromType(Type)</c> as separate operations, and the
    /// add path is the exact-type one.
    /// <para>
    /// The distinction is pinned from the lane rather than only declared here:
    /// <c>KernelWindowCatalogTests.VerifyVanillaRuleIsExactTypeNotAssignableFrom</c> opens a base-class and
    /// a derived-class shell in both orders, so rewriting this condition as <c>IsAssignableFrom</c> in
    /// either direction reddens a named assertion (measured: two FAILs per direction).
    /// </para>
    /// </summary>
    private void RemoveWindowsOfType(Type type)
    {
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            Window existing = windows[i];
            if (existing.onlyOneOfTypeAllowed && existing.GetType() == type)
            {
                TryRemove(existing);
            }
        }
    }
}

/// <summary>
/// The game's own thread answer, stubbed as "yes" because the harness host is single-threaded: the payload
/// delegates to this property rather than re-deriving the comparison, so a lane that needs the other answer
/// injects <c>IUiMainThread</c> instead of pretending the process moved threads.
/// </summary>
public static class UnityData
{
    public static bool IsInMainThread => true;
}

/// <summary>
/// Minimal loaded-definition base for consumer production-projection harnesses. The public fields
/// match the reference assembly; this does not implement the game definition database.
/// </summary>
public class Def
{
    public string defName = "";
    public string label = "";
}
/// <summary>
/// Sound definition type token only. Audio lookup and playback remain game-only paths.
/// </summary>
public class SoundDef : Def
{
}

/// <summary>
/// The game's Mod base, stubbed as an empty class: a consumer's mod singleton type derives from it, so any
/// harness path that touches the singleton's type needs the base to exist. No stubbed member is
/// read on those paths (the settings mutators' notification seam is documented as game-only).
/// </summary>
public class Mod
{
}

/// <summary>
/// The game's pawn, stubbed as an empty reference type: a diagnostics source names it in its
/// lookup dictionaries and row-factory signatures, so the TYPES must load; the harness overlay is
/// empty, so no member of it is ever executed (a lane that needs live pawns is a game walkthrough,
/// not a stub invention).
/// </summary>
public class Pawn
{
}

/// <summary>
/// The game's camera cell rect, stubbed as an empty struct: the periodic-population snapshot names
/// it as a field type, so the containing types must load; the harness never fills a snapshot, so no
/// member of it executes.
/// </summary>
public struct CellRect
{
}

/// <summary>
/// The game's static world locator, stubbed with the single slot a consumer's window seams name at JIT
/// time: a method that mentions Find.WindowStack must be able to resolve the type even when the
/// harness always injects its own stack and never executes that branch. The default instance is a
/// live stub WindowStack, so a production call that forgot to inject lands in a visible empty
/// stack instead of a null reference.
/// </summary>
public static class Find
{
    public static WindowStack WindowStack { get; set; } = new WindowStack();
}
