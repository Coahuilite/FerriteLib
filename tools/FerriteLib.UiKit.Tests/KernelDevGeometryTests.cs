using System;
using System.Globalization;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The development-only numeric instrument on the diagnostic surface: what the layout actually did, as
/// numbers, for the questions a screenshot cannot answer.
/// <para>
/// The instrument is compiled into a development build and out of a release one, so this lane has two halves
/// and each asserts its own truth, the way the watching lane does:
/// <list type="bullet">
/// <item><b>dev half</b> - every arranged node is reported with its arranged rect, the rect its widget was
/// handed, that rect converted into the pointer's own space, the origin the conversion used, its height mode
/// and the height that mode resolved to; a scoped container reports its viewport and its real content
/// extent; and a press is attributed to the element that claimed it. The mutation these assertions are
/// failure-sensitive to is a change to the <b>geometry producer</b> (the engine's resolved height) and not
/// to the instrument: an instrument whose numbers do not move when the layout moves is a green light with
/// no meaning.</item>
/// <item><b>release half</b> - the instrument is not compiled in, the opt-in is refused rather than
/// accepted, and a full frame leaves the dump empty. Fail-closed, because "enable it and read nothing" is
/// the diagnostic failure this whole surface exists to end.</item>
/// </list>
/// </para>
/// </summary>
internal static class KernelDevGeometryTests
{
    private const string Scope = "dev-geometry-lane";

    private static readonly Rect Viewport = new Rect(0f, 0f, 200f, 200f);

    /// <summary>
    /// net472 has no Split(char, StringSplitOptions) overload, and the one-argument call binds to it under
    /// this language version; the array form is the one that exists in the framework this payload ships on.
    /// </summary>
    private static readonly char[] Lines = new[] { '\n' };

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("The event double agrees with the reference constants and consumption contract", VerifyEventSemantics);
#if FER_DEV
        Run("Every arranged node is reported with both rects, its height mode and its content extent", VerifyGeometryNumbers);
        Run("A press is attributed to the element that claimed it, in the pointer's own space", VerifyInputVerdicts);
        Run("Consumed input remains visible without changing native dispatch", VerifyConsumedInput);
        Run("The overlay is refused until the instrument is on", VerifyOverlayRequiresTheInstrument);
#else
        Run("A release payload has no instrument and refuses to pretend it has one", VerifyAbsentInRelease);
#endif
        return failures;
    }

    private static void VerifyEventSemantics()
    {
        foreach (var pair in new[] {
            (EventType.MouseDown, "MouseDown"), (EventType.MouseUp, "MouseUp"),
            (EventType.MouseDrag, "MouseDrag"), (EventType.KeyDown, "KeyDown"),
            (EventType.KeyUp, "KeyUp"), (EventType.Repaint, "Repaint"),
            (EventType.Layout, "Layout"), (EventType.Used, "Used") })
        {
            Check(Enum.GetName(typeof(EventType), pair.Item1) == pair.Item2,
                "the runtime event double matches the compiler's reference constant: " + pair.Item2);
        }
        Event raised = Event.KeyboardEvent("");
        raised.type = EventType.MouseDown;
        raised.Use();
        Check(raised.type == EventType.Used, "Use changes the event type to Used, as the real IMGUI contract requires");
    }

#if FER_DEV
    // --- dev half ---------------------------------------------------------------------------------

    private static void VerifyGeometryNumbers()
    {
        using UiHost host = NewHost(Page);
        UiDiagnosticSubscription diagnostics = host.Diagnostics;
        Check(!diagnostics.GeometryEnabled, "the instrument is off until a subscription asks: default off");
        diagnostics.GeometryEnabled = true;
        Check(diagnostics.GeometryEnabled, "and the opt-in is remembered on the subscription that asked");

        UiLayoutSnapshot snapshot = Pass(host);
        string dump = diagnostics.DumpGeometry();
        Print(dump);

        Check(LineWith(dump, "path=root ") != null, "the root container is reported");
        Check(LineWith(dump, "path=root/list ") != null, "the scoped container is reported");
        Check(LineWith(dump, "path=root/list/inner ") != null, "and so is the element inside it");

        string band = Require(dump, "path=root/row/band ");
        Check(band.IndexOf("height=MatchContent", StringComparison.Ordinal) >= 0,
            "the MatchContent band reports the mode it declared: " + band);
        Check(band.IndexOf("container=no", StringComparison.Ordinal) >= 0, "and reports itself as a widget");

        // The height axis as a number rather than as prose: the mode's whole contract is that this element's
        // height IS its sibling's measured height.
        string caption = Require(dump, "path=root/row/caption ");
        float bandHeight = Number(band, " h=");
        float captionHeight = Number(caption, " h=");
        Check(bandHeight > 0f && Same(bandHeight, captionHeight),
            "the band's recorded height is the sibling's measured height: " + Text(bandHeight) + " vs " + Text(captionHeight));

        // Not a second source of truth: what it reports is the snapshot's own geometry.
        Rect fromSnapshot = snapshot.RectById["band"];
        TryRect(band, "arranged=", out Rect arranged);
        Check(SameRect(arranged, fromSnapshot),
            "and its reported arranged rect is the snapshot's own rect for that element: " + Show(arranged) + " vs " + Show(fromSnapshot));

        // At the page level the two spaces coincide, and a reader can now see that instead of assuming it.
        TryRect(band, "window=", out Rect bandWindow);
        Check(SameRect(arranged, bandWindow),
            "at the page level the arranged and drawn rects agree: " + Show(arranged) + " vs " + Show(bandWindow));

        // The trap the instrument exists for: inside the scoped container the two spaces genuinely differ,
        // and the dump says so with both numbers and the origin between them.
        string inner = Require(dump, "path=root/list/inner ");
        TryRect(inner, "arranged=", out Rect innerArranged);
        TryRect(inner, "draw=", out Rect innerDraw);
        Check(!SameRect(innerArranged, innerDraw),
            "an element inside the scoped container is reported in the other space, not in the page's: "
            + Show(innerArranged) + " vs " + Show(innerDraw));

        // The conversion itself, per line: this half is a future-regression guard (it holds by construction
        // today) rather than the mutation-proving half, which is the height equality above.
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (line.Length == 0 || line[0] == '[' || line.StartsWith("input ", StringComparison.Ordinal)) continue;
            if (!TryRect(line, "draw=", out Rect draw) || !TryRect(line, "window=", out Rect window)) continue;
            if (!TryPoint(line, "origin=", out Vector2 origin)) continue;
            Check(Same(window.x, draw.x + origin.x) && Same(window.y, draw.y + origin.y),
                "guard: the window rect is the drawn rect plus the reported origin: " + line);
        }

        // The scoped container's viewport and its real content extent, both from the arrange.
        string list = Require(dump, "path=root/list ");
        Check(list.StartsWith("viewport ", StringComparison.Ordinal),
            "a scoped container is reported as a viewport rather than as an ordinary rect: " + list);
        TryRect(list, "window=", out Rect viewport);
        Check(TryRect(list, "content=", out Rect content) && content.height > viewport.height,
            "and its content extent is the scrollable extent, not the visible band: " + Show(content) + " over " + Show(viewport));
    }

    private static void VerifyInputVerdicts()
    {
        using UiHost host = NewHost(Page);
        host.Diagnostics.GeometryEnabled = true;
        UiLayoutSnapshot snapshot = Pass(host);
        Rect band = snapshot.RectById["band"];

        string bandLine = Require(host.Diagnostics.DumpGeometry(), "path=root/row/band ");
        TryRect(bandLine, "window=", out Rect bandWindow);

        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMouseDown = true;

            // A press that lands: the native control fires inside the band's own rect.
            UiNative.DebugMousePosition = band.center;
            UiNative.ButtonOverride = rect => true;
            host.DrawFrame(Viewport);
            string hit = Require(host.Diagnostics.DumpGeometry(), "verdict=hit");
            PrintInputs(host.Diagnostics.DumpGeometry());
            Check(hit.IndexOf("path=root/row/band ", StringComparison.Ordinal) >= 0,
                "a claimed press names the element that claimed it: " + hit);
            TryPoint(hit, "point=", out Vector2 hitPoint);
            Check(Same(hitPoint.x, bandWindow.center.x) && Same(hitPoint.y, bandWindow.center.y),
                "in the pointer's own space, so the two can be compared instead of guessed at: "
                + Show(hitPoint) + " vs " + Show(bandWindow.center));

            // The chokepoint agreement that matters for "the click did nothing": the rect the funnel
            // queried is the same window-space rect the engine reported drawing.
            TryRect(hit, "rect=", out Rect queried);
            Check(SameRect(queried, bandWindow),
                "and the queried rect is the drawn rect the engine reported, in one space: "
                + Show(queried) + " vs " + Show(bandWindow));

            // A press that misses: still sampled, because the pointer is down. This is what answers
            // "the click did nothing" instead of an empty log that answers nothing.
            UiNative.ButtonOverride = _ => false;
            host.DrawFrame(Viewport);
            string miss = Require(host.Diagnostics.DumpGeometry(), "verdict=miss");
            PrintInputs(host.Diagnostics.DumpGeometry());
            Check(miss.IndexOf("path=root/row/band ", StringComparison.Ordinal) >= 0,
                "a press the element ignored is reported as ignored, with the element named: " + miss);
        }
        finally
        {
            UiNative.ButtonOverride = null;
            UiNative.DebugMousePosition = default;
            UiNative.DebugMouseDown = false;
            UiNative.DebugMousePositionEnabled = false;
        }

        // A refusal reports its reason: the funnel's own disabled rule.
        using UiHost disabled = NewHost(Page, commandEnabled: false);
        disabled.Diagnostics.GeometryEnabled = true;
        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMouseDown = true;
            UiNative.DebugMousePosition = band.center;
            disabled.DrawFrame(Viewport);
            string blocked = Require(disabled.Diagnostics.DumpGeometry(), "verdict=disabled");
            PrintInputs(disabled.Diagnostics.DumpGeometry());
            Check(blocked.IndexOf("path=root/row/band ", StringComparison.Ordinal) >= 0,
                "a disabled element reports why the press went nowhere: " + blocked);
        }
        finally
        {
            UiNative.DebugMousePosition = default;
            UiNative.DebugMouseDown = false;
            UiNative.DebugMousePositionEnabled = false;
        }
    }

    private static void VerifyConsumedInput()
    {
        const string xml = "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Column Id=\"root\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"first\" Kind=\"input/button\" Text=\"First\" Height=\"30\" ActionBind=\"act\" />"
            + "<Widget Id=\"later\" Kind=\"input/button\" Text=\"Later\" Height=\"30\" ActionBind=\"other\" />"
            + "</Column></UiPage>";
        int actions = 0, otherActions = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("act", () => actions++);
        bindings.BindCommand("other", () => otherActions++);
        using var host = new UiHost(Scope, UiLayoutManifest.Parse(xml), bindings,
            UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        Rect first = Pass(host).RectById["first"];
        try
        {
            foreach (bool enabled in new[] { true, false })
            {
                host.Diagnostics.GeometryEnabled = enabled;
                foreach (EventType phase in new[] { EventType.MouseDown, EventType.MouseUp })
                {
                    Event raised = Event.KeyboardEvent("");
                    raised.type = phase;
                    raised.button = 0;
                    raised.mousePosition = first.center;
                    Event.current = raised;
                    host.DrawFrame(Viewport);
                    Check(raised.type == EventType.Used, "the native button consumes the event with diagnostics=" + enabled);
                    if (!enabled) continue;
                    string dump = host.Diagnostics.DumpGeometry();
                    string firstInput = Require(dump, "input path=root/first ");
                    string laterInput = Require(dump, "input path=root/later ");
                    Check(firstInput.Contains("event-before=" + phase + " event-after=Used"),
                        "the consuming control reports the phase transition: " + firstInput);
                    Check(laterInput.Contains("verdict=miss event-before=Used event-after=Used"),
                        "a later query remains visible after consumption: " + laterInput);
                    Check(dump.Contains(" event=" + phase), "the host retains the original event phase");
                    PrintInputs(dump);
                }
            }
            Check(actions == 2 && otherActions == 0, "diagnostics on and off dispatch exactly one command per native click");
            host.Diagnostics.GeometryEnabled = true;
            Event repaint = Event.KeyboardEvent("");
            repaint.type = EventType.Repaint;
            Event.current = repaint;
            host.DrawFrame(Viewport);
            Check(!host.Diagnostics.DumpGeometry().Contains("input path="), "a repaint does not inherit consumed input from the previous pass");
        }
        finally { Event.current = null; GUIUtility.hotControl = 0; }
    }

    private static void VerifyOverlayRequiresTheInstrument()
    {
        using UiHost host = NewHost(Page);
        bool refused = false;
        try
        {
            host.Diagnostics.GeometryOverlay = true;
        }
        catch (InvalidOperationException ex)
        {
            refused = ex.Message.IndexOf("GeometryEnabled", StringComparison.Ordinal) >= 0;
        }

        Check(refused, "the overlay is refused, naming the switch it depends on, before anything is sampled");

        host.Diagnostics.GeometryEnabled = true;
        host.Diagnostics.GeometryOverlay = true;
        Check(host.Diagnostics.GeometryOverlay, "and it is accepted once the instrument is on");
        host.Diagnostics.GeometryOverlay = false;
        Check(!host.Diagnostics.GeometryOverlay, "and can be turned off again");
    }
#else
    // --- release half -----------------------------------------------------------------------------

    /// <summary>
    /// The shipped configuration. The instrument does not exist here, and the enable path says so instead of
    /// accepting the request and then answering every question with emptiness.
    /// </summary>
    private static void VerifyAbsentInRelease()
    {
        using UiHost host = NewHost(Page);
        UiDiagnosticSubscription diagnostics = host.Diagnostics;
        Check(!diagnostics.GeometryEnabled, "a release payload reports the instrument as unavailable");

        bool refused = false;
        try
        {
            diagnostics.GeometryEnabled = true;
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        Check(refused, "and refuses the opt-in rather than accepting it");
        Check(!diagnostics.GeometryEnabled, "so it stays off");

        Pass(host);
        Check(diagnostics.DumpGeometry().Length == 0,
            "and a full frame leaves the dump empty, which is what compiling it out means");

        bool overlayRefused = false;
        try
        {
            diagnostics.GeometryOverlay = true;
        }
        catch (InvalidOperationException)
        {
            overlayRefused = true;
        }

        Check(overlayRefused, "the overlay is refused the same way");
    }
#endif

    // --- page and helpers --------------------------------------------------------------------------

    private const string Page =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Column Id=\"root\" Padding=\"0\" Gap=\"0\">"
        + "<Row Id=\"row\" Padding=\"0\" Gap=\"0\">"
        + "<Widget Id=\"band\" Kind=\"input/button\" Text=\"Band\" Chrome=\"none\""
        + " Height=\"MatchContent\" ActionBind=\"act\" />"
        + "<Widget Id=\"caption\" Kind=\"chrome/banner\" Text=\"Band\" Emphasis=\"Muted\" />"
        + "</Row>"
        + "<Scroll Id=\"list\" Padding=\"0\" Gap=\"0\" Height=\"40\">"
        + "<Widget Id=\"inner\" Kind=\"chrome/banner\" Text=\"Inner\" Height=\"120\" />"
        + "</Scroll>"
        + "</Column>"
        + "</UiPage>";

    private static UiHost NewHost(string xml, bool commandEnabled = true)
    {
        var bindings = new UiBindings();
        if (commandEnabled)
        {
            bindings.BindCommand("act", () => { });
        }
        else
        {
            bindings.BindCommand("act", () => { }, () => false);
        }

        return new UiHost(
            Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
    }

    /// <summary>One complete frame (draw included), then the snapshot the same geometry produced.</summary>
    private static UiLayoutSnapshot Pass(UiHost host)
    {
        host.DrawFrame(Viewport);
        return host.MeasureAndArrange(new Vector2(Viewport.width, Viewport.height));
    }

    private static void Print(string dump)
    {
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (line.Length > 0) Console.WriteLine("    | " + line);
        }
    }

    /// <summary>
    /// Prints only the sampled presses of a dump. The geometry block is printed in full once; the input half
    /// answers the other question this instrument was asked, so its lines are evidence the gate can see too.
    /// </summary>
    private static void PrintInputs(string dump)
    {
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (line.StartsWith("input ", StringComparison.Ordinal)) Console.WriteLine("    | " + line);
        }
    }

    private static string Require(string dump, string token)
    {
        string? line = LineWith(dump, token);
        if (line == null) throw new Exception("the dump carries no line with '" + token + "':" + Environment.NewLine + dump);
        return line;
    }

    private static string? LineWith(string dump, string token)
    {
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (line.IndexOf(token, StringComparison.Ordinal) >= 0) return line;
        }

        return null;
    }

    private static bool TryRect(string line, string name, out Rect rect)
    {
        rect = default;
        if (!Slice(line, name, 4, out string[] parts)) return false;
        rect = new Rect(Value(parts[0]), Value(parts[1]), Value(parts[2]), Value(parts[3]));
        return true;
    }

    private static bool TryPoint(string line, string name, out Vector2 point)
    {
        point = default;
        if (!Slice(line, name, 2, out string[] parts)) return false;
        point = new Vector2(Value(parts[0]), Value(parts[1]));
        return true;
    }

    private static bool Slice(string line, string name, int count, out string[] parts)
    {
        parts = Array.Empty<string>();
        int start = line.IndexOf(name + "(", StringComparison.Ordinal);
        if (start < 0) return false;
        start += name.Length + 1;
        int end = line.IndexOf(')', start);
        if (end < 0) return false;
        parts = line.Substring(start, end - start).Split(new[] { ',' }, StringSplitOptions.None);
        return parts.Length == count;
    }

    private static float Number(string line, string name)
    {
        int start = line.IndexOf(name, StringComparison.Ordinal);
        if (start < 0) return float.NaN;
        start += name.Length;
        int end = start;
        while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.' || line[end] == '-')) end++;
        return Value(line.Substring(start, end - start));
    }

    private static float Value(string text)
    {
        return float.Parse(text, CultureInfo.InvariantCulture);
    }

    private static bool Same(float left, float right)
    {
        return Math.Abs(left - right) <= 0.001f;
    }

    private static bool SameRect(Rect left, Rect right)
    {
        return Same(left.x, right.x) && Same(left.y, right.y)
            && Same(left.width, right.width) && Same(left.height, right.height);
    }

    private static string Text(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Show(Rect rect)
    {
        return "(" + Text(rect.x) + "," + Text(rect.y) + "," + Text(rect.width) + "," + Text(rect.height) + ")";
    }

    private static string Show(Vector2 point)
    {
        return "(" + Text(point.x) + "," + Text(point.y) + ")";
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
        }
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
        }

        public float MeasureWidth(string text, UiFont font)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
        }
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key) => "[" + key + "]";

        public int TranslationRevision => 0;
    }
}
