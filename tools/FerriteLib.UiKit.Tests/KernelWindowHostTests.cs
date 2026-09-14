using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Window-shell lane (US→FL round 1, P2, condition b): the shell must be drivable from the kernel-host
/// harness, which is why the <c>Verse.Window</c> stub slice landed with it rather than after it.
/// <para>
/// The load-bearing tests are the failure contract, because that is the part a rewrite gets wrong: a
/// pass that throws must NOT show the notice inside itself, and must not retry synchronously. Note the
/// division of labour with item C — a widget that throws mid-draw is recovered by the engine and never
/// reaches this shell; what the shell guards is a page that cannot be entered at all.
/// </para>
/// </summary>
internal static class KernelWindowHostTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        ResetSeams();

        try
        {
            VerifyHappyPassBuildsOneHostAndDrawsNoNotice();
            VerifyFailingPassTripsTheNoticeOnTheNextPassOnly();
            VerifyFailingNoticeIsTerminalAndNeverRetries();
            VerifyUnmetPrerequisiteBlocksHostCreation();
            VerifyWidgetFailureIsRecoveredBelowTheShell();
            VerifyCloseAffordanceClosesTheWindow();
            VerifyCloseAffordanceSizesToItsText();
            VerifyShellTextIsScopedNotUnscoped();
            VerifyPreCloseDisposesTheOwnedHost();
            VerifyWindowSizeComesOnlyFromTheProvider();
            VerifyZeroMarginLeavesTheContentInsetToTheShell();
        }
        finally
        {
            ResetSeams();
        }

        return failures;
    }

    private static void VerifyHappyPassBuildsOneHostAndDrawsNoNotice()
    {
        var window = new TestWindow(LaneScope());
        window.windowRect = new Rect(0f, 0f, 900f, 700f);

        window.WindowOnGUI();
        window.WindowOnGUI();

        Check(window.Notices.Count == 0, "a healthy page draws no notice");
        Check(window.HostCreations == 1, "the shell builds its host once and reuses it across passes");
        Check(window.SessionAlive, "the owned session is alive after two clean passes");

        // Alive is not drawn: the shell's page must also have ended both frames without recovering an
        // element. The 2026-09-12 stub hole left exactly this shape of lane green while the page never ran,
        // because liveness survives a page whose elements were all replaced by recovery bands.
        window.ExpectPageDrew("window shell healthy pass");
    }

    /// <summary>
    /// Condition (c). The failing pass stays quiet and the notice appears on the following pass —
    /// a synchronous switch would draw the notice inside the IMGUI pass that already claimed layout
    /// state for the page.
    /// </summary>
    private static void VerifyFailingPassTripsTheNoticeOnTheNextPassOnly()
    {
        var window = new TestWindow(LaneScope()) { FailOnCreate = true };
        window.windowRect = new Rect(0f, 0f, 900f, 700f);

        window.WindowOnGUI();
        Check(window.Failures == 1, "the failing pass reported the exception once");
        Check(window.Notices.Count == 0, "the failing pass drew no notice — the switch is deferred");

        window.WindowOnGUI();
        Check(window.Notices.Count == 1 && window.Notices[0] == UiWindowNotice.PageUnavailable,
            "the next pass draws the page-unavailable notice");
        Check(window.LastFailureMentionsPlantedError, "and the exception reached the notice provider");
    }

    /// <summary>
    /// The other half of condition (c): the notice is terminal for the instance. An "improvement" that
    /// retried the page each pass would loop on a deterministic failure and re-enter the same throw.
    /// </summary>
    private static void VerifyFailingNoticeIsTerminalAndNeverRetries()
    {
        var window = new TestWindow(LaneScope()) { FailOnCreate = true };
        window.windowRect = new Rect(0f, 0f, 900f, 700f);

        for (int i = 0; i < 4; i++)
        {
            window.WindowOnGUI();
        }

        Check(window.HostCreations == 1, "after a failure the shell never builds a second host");
        Check(window.Failures == 1, "a terminal notice does not re-run the failing pass");
        Check(window.Notices.Count == 3, "the notice is then drawn on every remaining pass");
    }

    private static void VerifyUnmetPrerequisiteBlocksHostCreation()
    {
        var window = new TestWindow(LaneScope()) { Prerequisite = false };
        window.windowRect = new Rect(0f, 0f, 900f, 700f);

        window.WindowOnGUI();

        Check(window.HostCreations == 0, "an unmet prerequisite never reaches page creation");
        Check(window.Notices.Count == 1 && window.Notices[0] == UiWindowNotice.Prerequisite,
            "and the window names which failure it is, not a generic one");
    }

    /// <summary>
    /// The two recovery layers must not collapse into one. Item C gives the engine per-element
    /// recovery; if that ever stopped working, this widget failure would climb into the shell and the
    /// whole window would go terminal — which is what this asserts does NOT happen.
    /// </summary>
    private static void VerifyWidgetFailureIsRecoveredBelowTheShell()
    {
        var window = new TestWindow(LaneScope()) { ExplodeWidget = true };
        window.windowRect = new Rect(0f, 0f, 900f, 700f);

        window.WindowOnGUI();
        window.WindowOnGUI();

        Check(window.Notices.Count == 0, "a widget that throws never reaches the shell's failure contract");
        Check(window.Failures == 0, "and the window does not go terminal over one control");
        Check(window.HostCreations == 1, "the page keeps the host it already built");
    }

    private static void VerifyCloseAffordanceClosesTheWindow()
    {
        var window = new TestWindow(LaneScope());
        window.windowRect = new Rect(0f, 0f, 900f, 700f);

        window.WindowOnGUI();
        Check(window.CloseCalls == 0, "no button hit means no close");

        UiNative.ButtonOverride = rect =>
        {
            window.CloseRects.Add(rect);
            return true;
        };
        window.WindowOnGUI();

        Check(window.CloseCalls == 1, "the close affordance closes the window");
        Check(window.Notices.Count == 0, "and closing is not mistaken for a failure");

        // One button only, inside the title band and against the right edge: the shell owns this
        // geometry, so it is the shell that must place it.
        Check(window.CloseRects.Count == 1, "the chrome draws exactly one interactive affordance");
        Rect close = window.CloseRects[0];
        Check(close.y >= 0f && close.yMax <= TestShell.TitleBar, "the affordance lives in the title band");
        Check(Math.Abs(close.xMax - (900f - TestShell.Padding)) < 0.01f,
            "and it is right-aligned inside the shell's padding");
    }

    /// <summary>
    /// The shell's close label is the consumer's string, so its box follows the text instead of a constant:
    /// a consumer whose wording or translation grows must not be clipped by the shell's own geometry. The
    /// samples are neutral on purpose — a long ASCII label and a CJK label, nothing from any consumer — and
    /// the same <see cref="ITextMetrics"/> ruler the fit audit uses is the one the shell measures with.
    /// </summary>
    private static void VerifyCloseAffordanceSizesToItsText()
    {
        const string LongAscii = "close the window and return to the previous screen";
        const string Cjk = "关闭窗口并返回上一个界面";

        var ruler = new StubMetrics();

        var ascii = new TestWindow(LaneScope()) { CloseLabel = LongAscii, Ruler = ruler };
        Check(ascii.CloseSize.x >= ruler.MeasureWidth(LongAscii, UiFont.Tiny),
            "a long ASCII label fits inside the affordance the shell sized for it");
        Check(ascii.CloseSize.x > 110f,
            "which is wider than the constant this shell used to ship (got " + ascii.CloseSize.x + ")");

        var cjk = new TestWindow(LaneScope()) { CloseLabel = Cjk, Ruler = ruler };
        Check(cjk.CloseSize.x >= ruler.MeasureWidth(Cjk, UiFont.Tiny),
            "a CJK label gets a box measured by the same ruler, not a Latin-length estimate");
        Check(cjk.CloseSize.x > 110f, "and that label is wider than the old constant too");

        var brief = new TestWindow(LaneScope()) { CloseLabel = "close", Ruler = ruler };
        Check(brief.CloseSize.x == 110f && brief.CloseSize.y == 30f,
            "a short label keeps the shipped 110 x 30 look");

        const float Fixed = 40f;
        var pinned = new TestWindow(LaneScope()) { CloseLabel = LongAscii, Ruler = ruler, FixedCloseSize = new Vector2(Fixed, 20f) };
        Check(pinned.CloseSize.x == Fixed,
            "a consumer override still replaces the whole computation");

        // End to end: the rect the label is handed is the sized one, and the audit that watches that label
        // finds nothing to report — the two halves of the same ruler, so they cannot disagree.
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(ruler, reports.Add);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;

        var drawn = new TestWindow(LaneScope()) { CloseLabel = LongAscii, Ruler = ruler };
        drawn.windowRect = new Rect(0f, 0f, 900f, 700f);
        UiNative.ButtonOverride = rect =>
        {
            drawn.CloseRects.Add(rect);
            return false;
        };
        drawn.WindowOnGUI();

        Check(drawn.CloseRects.Count == 1 && drawn.CloseRects[0].width >= ruler.MeasureWidth(LongAscii, UiFont.Tiny),
            "the drawn affordance is at least as wide as the measured label");
        Check(reports.Count == 0,
            "and the fit audit reports no finding for it (got " + reports.Count + ")");

        ResetSeams();
        UiFitAudit.Detach();
    }

    /// <summary>
    /// The shell's own text carries its own identity. Two windows can be on screen at once, so an
    /// "(unscoped)" finding about chrome or a notice cannot say which window produced it; the shell is what
    /// knows, and this holds that it says so for both bands it draws.
    /// </summary>
    private static void VerifyShellTextIsScopedNotUnscoped()
    {
        string longTitle = new string('t', 60);
        string longNotice = new string('n', 60);

        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(new StubMetrics(), reports.Add);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;

        var window = new TestWindow(LaneScope()) { TitleText = longTitle, NoticeText = longNotice };
        window.windowRect = new Rect(0f, 0f, 200f, 600f);
        window.WindowOnGUI();

        Check(ScopedReports(reports, "/chrome").Count >= 1,
            "a chrome finding names the window and the band it came from");
        Check(!reports.Exists(r => r.ElementPath == "(unscoped)"),
            "and nothing the shell drew is left unattributed");

        UiFitAudit.Reset();
        var blocked = new TestWindow(LaneScope()) { Prerequisite = false, NoticeText = longNotice };
        blocked.windowRect = new Rect(0f, 0f, 200f, 600f);
        blocked.WindowOnGUI();

        Check(ScopedReports(reports, "/notice").Count >= 1,
            "a notice finding is attributed to the shell's notice band");
        Check(!reports.Exists(r => r.ElementPath == "(unscoped)"),
            "with no unscoped leftover from the notice pass either");

        ResetSeams();
        UiFitAudit.Detach();
    }

    private static List<UiOverflowReport> ScopedReports(List<UiOverflowReport> reports, string suffix)
    {
        var scoped = new List<UiOverflowReport>();
        foreach (UiOverflowReport report in reports)
        {
            if (report.ElementPath.EndsWith(suffix, StringComparison.Ordinal))
            {
                scoped.Add(report);
            }
        }

        return scoped;
    }

    private static void VerifyPreCloseDisposesTheOwnedHost()
    {
        var window = new TestWindow(LaneScope());
        window.windowRect = new Rect(0f, 0f, 900f, 700f);
        window.WindowOnGUI();
        Check(window.SessionAlive, "the host exists before close");

        window.PreClose();
        Check(!window.SessionAlive, "PreClose disposes the owned host and its session");
        Check(window.BasePreCloseCalls == 1, "and still runs the base window-close contract");
    }

    /// <summary>
    /// Condition (a): the first consumer's screen-fraction clamp must not become the library's default.
    /// With no provider the shell reports the game's own size; with one it reports exactly that.
    /// </summary>
    private static void VerifyWindowSizeComesOnlyFromTheProvider()
    {
        var unset = new TestWindow(LaneScope());
        Check(unset.InitialSize.x == 600f && unset.InitialSize.y == 600f,
            "no provider means the game's InitialSize, not a consumer number");

        var provided = new TestWindow(LaneScope()) { Size = new Vector2(1234f, 567f) };
        Check(provided.InitialSize.x == 1234f && provided.InitialSize.y == 567f,
            "a provider's size passes through unchanged");
    }

    /// <summary>
    /// The shell overrides <c>Margin</c> to zero because it insets content itself. The pass is driven
    /// through the window stack's own dispatch, so a leaked margin would show up as double-insetting.
    /// </summary>
    private static void VerifyZeroMarginLeavesTheContentInsetToTheShell()
    {
        var window = new TestWindow(LaneScope());
        window.windowRect = new Rect(0f, 0f, 800f, 600f);

        window.WindowOnGUI();

        Rect content = window.ContentRect;
        Check(content.x == TestShell.Padding && content.y == TestShell.TitleBar,
            "content is inset by the shell's own padding and title band");
        Check(Math.Abs(content.width - (800f - TestShell.Padding * 2f)) < 0.01f
            && Math.Abs(content.height - (600f - TestShell.TitleBar - TestShell.Padding)) < 0.01f,
            "and the game's default margin is not applied on top");
    }

    private static string LaneScope() => "window-lane-" + Guid.NewGuid().ToString("N");

    private static void ResetSeams()
    {
        UiNative.DebugMousePositionEnabled = false;
        UiNative.ButtonOverride = null;
    }

    /// <summary>Geometry the shell is expected to own, restated so a silent change is a test failure.</summary>
    private static class TestShell
    {
        public const float TitleBar = 56f;
        public const float Padding = 20f;
    }

    /// <summary>The page the shell hosts; optionally throws mid-draw to exercise item C's recovery.</summary>
    private sealed class PageWidget : IUiWidget
    {
        public string Kind => TestWindow.WidgetKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
            bindings.ValidateValue<bool>("explode", elementPath);
        }

        public float Measure(UiWidgetContext ctx) => 24f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            if (ctx.Bindings.TryGet("explode", out bool explode) && explode)
            {
                throw new InvalidOperationException("planted widget failure");
            }

            UiThemeDraw.Label(rect, "page", ctx.Theme);
        }
    }

    private sealed class TestWindow : UiWindowHost
    {
        public const string WidgetKind = "test/window-page";

        private readonly string scope;
        private readonly UiBindings bindings = new();
        private UiSession? observedSession;

        public TestWindow(string scope)
        {
            this.scope = scope;
            UiWidgetRegistry.Register(scope, WidgetKind, () => new PageWidget());
            bindings.BindValue("explode", () => ExplodeWidget, _ => { });
        }

        public readonly List<UiWindowNotice> Notices = new();
        public readonly List<Rect> CloseRects = new();
        public int HostCreations;
        public int Failures;
        public int CloseCalls;
        public int BasePreCloseCalls;
        public bool Prerequisite = true;
        public bool FailOnCreate;
        public bool ExplodeWidget;
        public Vector2? Size;
        public Rect ContentRect;
        public string FailureText = "";

        public string CloseLabel = "close";
        public string TitleText = "title";
        public string NoticeText = "";
        public ITextMetrics Ruler = new StubMetrics();
        public Vector2? FixedCloseSize;

        public bool SessionAlive => observedSession != null && observedSession.IsActive;

        /// <summary>
        /// The page session the shell built, or null when no pass reached that far. The lane uses it to
        /// assert what "alive" cannot say: that the page's elements actually drew.
        /// </summary>
        public UiSession? ObservedSession => observedSession;

        /// <summary>Fails when the last pass recovered any element; see <see cref="KernelTripGuard"/>.</summary>
        public void ExpectPageDrew(string lane)
        {
            if (observedSession == null) throw new InvalidOperationException("no page session was built by the pass");
            KernelTripGuard.ExpectNoTrips(observedSession, lane);
        }

        /// <summary>The affordance size the shell computes; the lane reads it the way the shell does.</summary>
        public Vector2 CloseSize => CloseButtonSize;

        public bool LastFailureMentionsPlantedError
            => FailureText.IndexOf("planted window host failure", StringComparison.Ordinal) >= 0;

        protected override UiTheme Theme => UiTheme.DarkGold;

        /// <summary>The same ruler the lane hands the fit audit, so the shell and the audit cannot disagree.</summary>
        protected override ITextMetrics Metrics => Ruler;

        protected override Vector2 CloseButtonSize => FixedCloseSize ?? base.CloseButtonSize;

        protected override string Title => TitleText;

        protected override string Subtitle => "subtitle";

        protected override string CloseText => CloseLabel;

        protected override bool PrerequisiteVerified => Prerequisite;

        protected override Func<Vector2>? InitialSizePolicy => Size == null ? null : () => Size.Value;

        protected override UiHost CreateHost()
        {
            HostCreations++;
            if (FailOnCreate)
            {
                throw new InvalidOperationException("planted window host failure");
            }

            UiLayoutManifest manifest = UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"" + scope + "\">"
                + "<Widget Id=\"page\" Kind=\"" + WidgetKind + "\" />"
                + "</UiPage>");
            UiHost host = new(scope, manifest, bindings, Theme, new StubMetrics(), new StubTranslation());
            observedSession = host.Session;
            return host;
        }

        protected override void DrawNotice(Rect rect, UiWindowNotice notice)
        {
            Notices.Add(notice);
            FailureText = LastFailure?.Message ?? "";
            // The notice body is consumer text, but the shell is what calls it — that is the case this
            // lane needs: an empty body is silent, a long one is a finding the shell must attribute.
            UiThemeDraw.Label(rect, NoticeText, Theme, singleLine: true);
        }

        protected override void BeforeDraw(Rect contentRect)
        {
            ContentRect = contentRect;
        }

        protected override void OnDrawFailure(Exception error)
        {
            Failures++;
        }

        public override void Close(bool doCloseSound = true)
        {
            CloseCalls++;
        }

        public override void PreClose()
        {
            BasePreCloseCalls++;
            base.PreClose();
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 16f;

        // The shared half-width model, not a per-character constant: the chrome's label can be CJK, and a
        // ruler that counted characters would size a wide-glyph label exactly like a narrow one — which is
        // the mistake this lane exists to catch.
        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
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
}
