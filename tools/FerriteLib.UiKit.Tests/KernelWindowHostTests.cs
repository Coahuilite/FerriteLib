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

        public bool SessionAlive => observedSession != null && observedSession.IsActive;

        public bool LastFailureMentionsPlantedError
            => FailureText.IndexOf("planted window host failure", StringComparison.Ordinal) >= 0;

        protected override UiTheme Theme => UiTheme.DarkGold;

        protected override string Title => "title";

        protected override string Subtitle => "subtitle";

        protected override string CloseText => "close";

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

        public float MeasureWidth(string text, UiFont font) => text.Length * 7f;
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
