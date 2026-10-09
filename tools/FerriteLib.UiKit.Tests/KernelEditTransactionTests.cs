using System;
using System.Collections.Generic;

using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The edit-transaction lane (FL-IC2, interaction contract §3.4 and §3.5). One rule, four doors:
/// a field's parsed draft reaches the model exactly ONCE, and the only things that end an edit are the
/// field's own Enter, its own loss of focus, the window's Accept hook, and the window's Cancel hook — the
/// last two arriving one step BEFORE the page is drawn, which is why they travel as a mark on the state and
/// not as a call into the widget.
/// <list type="bullet">
/// <item><b>Witness 2, faithfully</b> — a <c>Live=false</c> number field lost whatever the player typed: the
/// keystroke frame deferred the write because the field held focus, and the frame that released the focus
/// reported no commit at all, so the next frame reset the buffer onto the model. The string field never had
/// this hole (its commit rule names the exit frame), which is exactly how a rule stated in two places drifts.
/// </item>
/// <item><b>Invalid text is never a value</b> — unparseable text stays in the buffer, is never written, and
/// is dropped silently when the edit ends: no default is invented, in either direction.</item>
/// <item><b>The two window keys</b> — Accept commits the open edit and keeps the window open; Cancel drops
/// the draft and keeps the window open. Both consume the key, so the same press cannot also reach the field
/// through its own read, and the game's late second Cancel check finds an event that is no longer a KeyDown.</item>
/// <item><b>Coverage pauses an edit</b> — a menu drawn over a field the player is typing into neither commits
/// nor cancels it: the native control is not reached, the option row keeps its press, and the draft survives
/// until the field's own door ends it.</item>
/// <item><b>An edit whose element stops being interactive leaves no record behind</b> — the session's active-edit
/// pointer is cleared with it, so a later Cancel cannot be answered by a field that is no longer on screen.</item>
/// </list>
/// <para>
/// <b>What this lane cannot show.</b> It drives the funnel through the kernel's own seams and the harness
/// doubles: the native text control is a delegate, the key arrives as one stub <c>Event</c>, and the window
/// stack is the double documented in <c>VerseStubs.cs</c> (whose Accept/Cancel dispatch order is the plan's
/// reading made executable, not a game run). Real IME text entry, a real keyboard, and the game's own
/// window-stack ordering remain the short human pass.
/// </para>
/// </summary>
internal static class KernelEditTransactionTests
{
    private const string Scope = "editx";

    private const string PageXml =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Column Id=\"col\" Padding=\"0\" Gap=\"2\">"
        + "<Widget Id=\"menu\" Kind=\"input/dropdown\" OptionsBind=\"Options\" Height=\"28\" />"
        + "<Widget Id=\"num\" Kind=\"input/number-field\" Bind=\"num\" Min=\"0\" Max=\"1\" Live=\"false\""
        + " VisibleKey=\"showNum\" Height=\"24\" />"
        + "<Widget Id=\"live\" Kind=\"input/number-field\" Bind=\"live\" Min=\"0\" Max=\"1\" Live=\"true\" Height=\"24\" />"
        + "<Widget Id=\"gated\" Kind=\"" + GatedNumber.KindName + "\" ActionBind=\"gate\" Height=\"24\" />"
        + "<Widget Id=\"text\" Kind=\"input/text-field\" Bind=\"text\" Live=\"false\" Height=\"24\" />"
        + "</Column>"
        + "</UiPage>";

    private static int failures;

    public static int RunAll()
    {
        failures = 0;

        failures += Run("A deferred number field writes its parsed draft once, on the frame that ends the edit",
            VerifyDeferredDraftCommitsOnceOnExit);
        failures += Run("Unparseable text is never written and leaves no value behind when the edit ends",
            VerifyInvalidDraftIsNeverWritten);
        failures += Run("A live number field still writes on the keystroke frame, as it always did",
            VerifyLiveFieldStillWritesPerKeystroke);
        failures += Run("The string field's deferred commit keeps working through the same shared rule",
            VerifyTextFieldDeferredCommit);
        failures += Run("Accept commits the open edit exactly once and keeps the window open",
            VerifyAcceptCommitsAndKeepsTheWindow);
        failures += Run("A hidden edit ends on its own state, so the entry and the field cannot disagree",
            VerifyHiddenEditEndsCoherently);
        failures += Run("A press an earlier control consumed still ends the edit and commits the draft once",
            VerifyConsumedOutsidePressCommitsOnce);
        failures += Run("Cancel drops the draft, writes nothing, and keeps the window open",
            VerifyCancelDiscardsAndKeepsTheWindow);
        failures += Run("With no edit open, Cancel still closes the window the way the consumer declared",
            VerifyNothingToCancelStillCloses);
        failures += Run("A menu drawn over an open edit pauses it: no native call, no write, draft intact",
            VerifyCoveredEditPausesInsteadOfEnding);
        failures += Run("An edit that loses its element to a disabled command leaves no active-edit record",
            VerifyDisabledElementEndsTheRecord);

        return failures;
    }

    // --- the field's own doors ---------------------------------------------------------------------

    /// <summary>
    /// Witness 2. The keystroke frame must not write (the editor is deferred), and the frame that ENDS the
    /// edit must write the draft it is holding — once. Reverting the exit-frame half of the rule (drop the
    /// <c>parsed &amp;&amp; draft != model</c> commit) leaves the model at 0.5 with the buffer reset under it,
    /// which is the failure the player reported.
    /// </summary>
    private static void VerifyDeferredDraftCommitsOnceOnExit()
    {
        using Page page = new();
        try
        {
            page.FocusNumberField("num", "0.7");
            Check(page.Num == 0.5f && page.NumWrites == 0,
                "a keystroke inside a deferred field writes nothing, however valid the draft");
            Check(page.Session.ActiveEditNode != null, "the session records which element holds the open edit");

            page.HoldFrame("num");
            Check(page.NumWrites == 0, "a frame that typed nothing still writes nothing");

            page.PressEnter();
            Check(page.Num == 0.7f, "the frame that ends the edit commits the parsed draft: " + page.Num);
            Check(page.NumWrites == 1, "exactly once, not once per frame");
            Check(!page.State("num").Focused, "and the edit is closed");
            Check(page.Session.ActiveEditNode == null, "with the session's record released");

            page.IdleFrame();
            Check(page.NumWrites == 1 && page.Num == 0.7f, "a following frame with no edit writes nothing again");
            Check(page.Shown("num") == "0.7", "the field now shows the model it just received");
        }
        finally
        {
            page.Restore();
        }
    }

    private static void VerifyInvalidDraftIsNeverWritten()
    {
        using Page page = new();
        try
        {
            page.FocusNumberField("num", "abc");
            Check(page.State("num").EditText == "abc",
                "the unparseable text stays in the buffer, because that is what the player typed");

            page.PressEnter();
            Check(page.Num == 0.5f && page.NumWrites == 0, "ending the edit on an invalid draft writes nothing");
            Check(page.Shown("num") == "0.5", "and the field falls back to the model, not to a made-up value");
        }
        finally
        {
            page.Restore();
        }
    }

    private static void VerifyLiveFieldStillWritesPerKeystroke()
    {
        using Page page = new();
        try
        {
            page.FocusNumberField("live", "0.7");
            Check(page.Live == 0.7f && page.LiveWrites == 1,
                "a live editor still reaches the model on the frame the draft parses (normal path preserved)");

            page.PressEnter();
            Check(page.LiveWrites == 1, "and the exit frame does not write the same value a second time");
        }
        finally
        {
            page.Restore();
        }
    }

    private static void VerifyTextFieldDeferredCommit()
    {
        using Page page = new();
        try
        {
            page.FocusNumberField("text", "draft");
            Check(page.Text == "start" && page.TextWrites == 0, "the string field defers while focused");

            page.PressEnter();
            Check(page.Text == "draft" && page.TextWrites == 1, "and commits once when the edit ends");
        }
        finally
        {
            page.Restore();
        }
    }

    // --- the window's two doors --------------------------------------------------------------------

    private static void VerifyAcceptCommitsAndKeepsTheWindow()
    {
        using Page page = new();
        EditShell shell = page.AttachShell();
        try
        {
            page.FocusNumberField("num", "0.7");
            Check(page.NumWrites == 0, "the draft is still only a draft");

            bool consumed = shell.PumpKey(KeyCode.Return);

            Check(consumed, "the window's Accept hook consumed the key before any control saw it");
            Check(shell.EventBeforeContents == "Used",
                "the hook ran BEFORE the page was drawn: the event is already used when the contents pass starts");
            Check(page.Num == 0.7f && page.NumWrites == 1, "the open edit committed exactly once");
            Check(!page.State("num").Focused, "and the edit is closed");
            Check(Find.WindowStack.IsOpen(shell), "Enter on an open edit must not close the window");
        }
        finally
        {
            page.DetachShell(shell);
            page.Restore();
        }
    }

    private static void VerifyCancelDiscardsAndKeepsTheWindow()
    {
        using Page page = new();
        EditShell shell = page.AttachShell();
        try
        {
            page.FocusNumberField("num", "0.7");

            bool consumed = shell.PumpKey(KeyCode.Escape);

            // The window's own state is read first on purpose: a press the ladder answered but did not consume
            // lets the game close the window at the end of the same pass, and a disposed page would otherwise
            // turn that into an element-lookup error rather than the assertion this lane is about.
            Check(consumed, "the Cancel key was taken by the edit, not by the window");
            Check(Find.WindowStack.IsOpen(shell), "and the window stays open - one press, one undone layer");
            Check(page.Num == 0.5f && page.NumWrites == 0,
                "the draft is dropped and nothing is written — a Cancel does not invent a value");
            Check(!page.State("num").Focused, "the edit is closed");
            Check(page.Shown("num") == "0.5", "the buffer is restored from the model");
        }
        finally
        {
            page.DetachShell(shell);
            page.Restore();
        }
    }

    /// <summary>
    /// The preservation half: a page with nothing to undo must keep Verse's meaning of Escape. The double's
    /// late second Cancel check is what makes a doubled close observable, so the count is asserted as well as
    /// the removal.
    /// </summary>
    private static void VerifyNothingToCancelStillCloses()
    {
        using Page page = new();
        EditShell shell = page.AttachShell();
        try
        {
            shell.PumpLayout();
            Check(shell.CloseCount == 0, "a healthy page draws without closing anything");

            bool consumed = shell.PumpKey(KeyCode.Escape);

            Check(consumed, "the key the ladder declined is consumed by the window that closes");
            Check(shell.CloseCount == 1, "the window closed once — the late second check found a used event: "
                + shell.CloseCount);
            Check(!Find.WindowStack.IsOpen(shell), "and it is out of the stack");
        }
        finally
        {
            page.DetachShell(shell);
            page.Restore();
        }
    }

    // --- coverage and lifecycle --------------------------------------------------------------------

    private static void VerifyCoveredEditPausesInsteadOfEnding()
    {
        using Page page = new();
        try
        {
            page.FocusNumberField("num", "0.7");
            page.Session.OpenPopup("menu", page.RectOf("menu"));
            page.PointInsideMenuOver("num");

            // The layer the dispatch reads is the one the PREVIOUS pass published, exactly as in the game:
            // the first frame draws the menu, the second is the frame in which the field underneath it is
            // covered. Asserting inside the first would prove nothing about coverage.
            page.IdleFrame();
            int nativeBefore = page.NativeCalls;
            page.IdleFrame();

            Check(page.NativeCalls == nativeBefore,
                "the covered field never reaches the native control, so the option row keeps its press");
            Check(page.State("num").Focused, "the edit is paused, not ended");
            Check(page.State("num").EditText == "0.7", "the draft survives the pass untouched");
            Check(page.NumWrites == 0 && page.Num == 0.5f, "and nothing was written in either direction");

            page.Session.ClosePopup();
            page.PressEnter();
            Check(page.Num == 0.7f && page.NumWrites == 1,
                "when the field's own door finally ends the edit, the paused draft commits once");
        }
        finally
        {
            page.Restore();
        }
    }

    private static void VerifyDisabledElementEndsTheRecord()
    {
        using Page page = new();
        try
        {
            page.FocusNumberField("gated", "0.7");
            Check(page.Session.ActiveEditNode != null, "the edit is open and recorded");

            page.GateEnabled = false;
            page.IdleFrame();
            page.IdleFrame();

            Check(page.Session.ActiveEditNode == null,
                "an element taken out of play ends the session's active-edit record (§3.7)");
            Check(page.Gated == 0.5f && page.GatedWrites == 0, "and its draft is dropped, not written");

            page.GateEnabled = true;
            page.IdleFrame();
            page.IdleFrame();
            Check(page.Shown("gated") == "0.5", "re-enabled, the field resumes on the model, not on a dead edit");
        }
        finally
        {
            page.Restore();
        }
    }


    /// <summary>
    /// The hide-and-return path. The session's active-edit record and the field's own <c>Focused</c> flag are
    /// one fact seen from two places, and clearing only the record left a re-shown field still claiming to be
    /// editing while the window's Accept hook had nothing to act on — the key the player pressed was swallowed
    /// by neither side. Hiding therefore ENDS the edit (dropping its draft, which is what §3.7 says a switched
    /// away row is), and what remains is a field with no edit and a session with no record.
    /// </summary>
    private static void VerifyHiddenEditEndsCoherently()
    {
        using Page page = new();
        try
        {
            page.FocusNumberField("num", "0.7");
            Check(page.State("num").Focused && page.Session.ActiveEditNode != null,
                "the edit is open and recorded before the page changes");

            page.ShowNum = false;
            page.Host.Bindings.NotifyChanged("showNum");
            page.IdleFrame();
            page.IdleFrame();

            Check(!page.State("num").Focused, "hiding the field ended the edit on the state itself");
            Check(page.Session.ActiveEditNode == null, "and the session's record went with it");
            Check(page.Num == 0.5f && page.NumWrites == 0, "the draft was dropped, never written");

            page.ShowNum = true;
            page.Host.Bindings.NotifyChanged("showNum");
            page.IdleFrame();
            page.IdleFrame();

            bool handled = page.Host.TryHandleAccept();
            Check(!handled, "with no edit open, Accept is not swallowed by the page");
            Check(page.NumWrites == 0, "and the returned field shows the model, not the abandoned draft");
            Check(page.Shown("num") == "0.5", "the buffer is the model's text again: " + page.Shown("num"));
        }
        finally
        {
            page.Restore();
        }
    }

    /// <summary>
    /// The ordinary outside click, in the form that used to depend on draw order: the dropdown draws BEFORE
    /// the field, takes the press, and IMGUI hands the rest of the pass an event standing as
    /// <c>Used</c>. A blur rule that reads only a live MouseDown never fires for a field drawn later, so the
    /// deferred draft was lost while the field still believed it was being edited. The pass boundary is the one
    /// place that cannot be too late.
    /// </summary>
    private static void VerifyConsumedOutsidePressCommitsOnce()
    {
        using Page page = new();
        try
        {
            page.FocusNumberField("num", "0.7");
            Check(page.NumWrites == 0 && page.State("num").Focused,
                "the draft is deferred while the field holds focus");

            page.RealClick("menu");

            Check(page.Num == 0.7f && page.NumWrites == 1,
                "the press another control consumed still committed the draft, exactly once: writes="
                + page.NumWrites);
            Check(!page.State("num").Focused, "and the edit closed");
            Check(page.Session.ActiveEditNode == null, "with the session's record released");
        }
        finally
        {
            page.Restore();
        }
    }
    // --- fixture -----------------------------------------------------------------------------------

    /// <summary>
    /// One page, its bindings, and the seams the fields read. The pointer is aimed at the arranged rect the
    /// widget is drawn with, so a lane aims at real geometry rather than at a number it invented.
    /// </summary>
    private sealed class Page : IDisposable
    {
        private static readonly List<string> Options = new() { "x", "y", "z" };

        internal readonly UiHost Host;
        internal readonly UiBindings Bindings;
        internal readonly Rect Viewport = new(0f, 0f, 300f, 320f);

        internal float Num = 0.5f;
        internal int NumWrites;
        internal float Live = 0.5f;
        internal int LiveWrites;
        internal string Text = "start";
        internal int TextWrites;
        internal string Choice = "x";
        internal int NativeCalls;
        internal float Gated = 0.5f;
        internal int GatedWrites;
        internal bool GateEnabled = true;
        internal bool ShowNum = true;


        internal Page()
        {
            UiWidgetRegistry.Clear();
            UiWidgetRegistry.InitializeCore();
            UiWidgetRegistry.Register(Scope, GatedNumber.KindName, () => new GatedNumber());

            Bindings = new UiBindings();
            Bindings.BindValue("num", () => Num, value => { Num = value; NumWrites++; });
            Bindings.BindValue("live", () => Live, value => { Live = value; LiveWrites++; });
            Bindings.BindValue("text", () => Text, value => { Text = value; TextWrites++; });
            Bindings.BindValue("gated", () => Gated, value => { Gated = value; GatedWrites++; });
            Bindings.BindValue("menu", () => Choice, value => Choice = value);
            Bindings.BindOptions("Options", () => Options);
            Bindings.BindValue("showNum", () => ShowNum, value => ShowNum = value);
            Bindings.BindCommand("gate", () => { }, () => GateEnabled);

            Host = new UiHost(Scope, UiLayoutManifest.Parse(PageXml), Bindings, UiTheme.Vanilla,
                new LaneMetrics(), new LaneTranslation());
            UiNative.TextFieldOverride = (rect, text) =>
            {
                NativeCalls++;
                return text;
            };
        }

        internal UiSession Session => Host.Session;

        // The state a field owns lives on the ELEMENT's node, not on the session-level node an out-of-draw
        // call resolves against; reading the wrong slot is how a lane passes by looking at an empty bag.
        internal UiValueState State(string elementId)
        {
            UiNode node = Session.GetNodeByElementId(elementId)
                ?? throw new Exception("the page never arranged element '" + elementId + "'");
            return Session.GetOrCreateValueState(node.Id, elementId);
        }

        internal Rect RectOf(string elementId) => Host
            .MeasureAndArrange(new Vector2(Viewport.width, Viewport.height))
            .RectById[elementId];

        /// <summary>
        /// One click inside the field while the native control reports the given text: the frame that both
        /// focuses and types, which is how a real IMGUI pass delivers the first keystroke.
        /// </summary>
        internal void FocusNumberField(string elementId, string typed)
        {
            Rect rect = RectOf(elementId);
            UiNative.TextFieldOverride = (r, text) =>
            {
                NativeCalls++;
                return typed;
            };
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMousePosition = Centre(rect);
            UiNative.DebugMouseDown = true;
            Host.DrawFrame(Viewport);
            UiNative.DebugMouseDown = false;
            UiNative.TextFieldOverride = (r, text) =>
            {
                NativeCalls++;
                return text;
            };
        }

        /// <summary>A frame with the pointer still inside the field and nothing typed.</summary>
        internal void HoldFrame(string elementId)
        {
            UiNative.DebugMousePosition = Centre(RectOf(elementId));
            Host.DrawFrame(Viewport);
        }

        internal void PointInsideMenuOver(string elementId)
        {
            UiNative.DebugMousePosition = Centre(RectOf(elementId));
        }

        internal void IdleFrame()
        {
            Host.DrawFrame(Viewport);
        }

        /// <summary>
        /// A click the backend actually dispatches: separate Layout / MouseDown / MouseUp / Repaint events with
        /// the pointer in the element's own rect, and the debug seams off. A lane that needs a press some other
        /// control can genuinely consume cannot express that through a seam, because the seam is read by every
        /// widget alike and consumed by none.
        /// </summary>
        internal void RealClick(string elementId)
        {
            Vector2 point = Centre(RectOf(elementId));
            UiNative.DebugMousePositionEnabled = false;
            UiNative.DebugMouseDown = false;
            foreach (EventType type in new EventType[]
            {
                EventType.Layout, EventType.MouseDown, EventType.MouseUp, EventType.Repaint,
            })
            {
                Event e = Event.KeyboardEvent("click");
                e.type = type;
                e.button = 0;
                e.mousePosition = point;
                Event.current = e;
                Host.DrawFrame(Viewport);
                Event.current = null;
            }
        }

        internal void PressEnter()
        {
            UiNative.DebugEnter = true;
            Host.DrawFrame(Viewport);
            UiNative.DebugEnter = false;
        }

        /// <summary>The text the field would show after a frame, read from the state the funnel owns.</summary>
        internal string Shown(string elementId) => State(elementId).EditText;

        internal EditShell AttachShell()
        {
            EditShell opening = new EditShell(this);
            Find.WindowStack.Add(opening);
            opening.windowRect = new Rect(0f, 0f, Viewport.width, Viewport.height);

            // One pass THROUGH THE WINDOW: the shell adopts its page host lazily inside its own guarded pass,
            // so a lane that only drew the host directly would hand the key to a shell holding no host.
            opening.PumpLayout();
            return opening;
        }

        internal void DetachShell(EditShell closing)
        {
            Find.WindowStack.TryRemove(closing);
        }

        internal void Restore()
        {
            UiNative.DebugMousePositionEnabled = false;
            UiNative.DebugMouseDown = false;
            UiNative.DebugEnter = false;
            UiNative.DebugFocusLost = false;
            UiNative.TextFieldOverride = null;
        }

        public void Dispose()
        {
            Restore();
            Host.Dispose();
        }

        private static Vector2 Centre(Rect rect) => new(rect.x + rect.width / 2f, rect.y + rect.height / 2f);
    }

    /// <summary>
    /// The window half of the fixture: a real <see cref="UiWindowHost"/> pass, driven through the double's
    /// <c>WindowOnGUI</c> so the Accept/Cancel hooks fire where the game fires them — before the contents.
    /// <c>BeforeDraw</c> records the event phase the page is about to be drawn in, which is what turns
    /// "the hook ran first" from an assertion about the library into an observation of one pass.
    /// </summary>
    private sealed class EditShell : UiWindowHost
    {
        private readonly Page page;

        internal EditShell(Page page)
        {
            this.page = page;
        }

        internal int CloseCount;

        internal string EventBeforeContents = "none";

        protected override UiTheme Theme => UiTheme.Vanilla;

        protected override string Title => "title";

        protected override string CloseText => "close";

        // The chrome is not what these lanes are about, and the pointer seams read the same draw-local space
        // the arranged rects live in: with the chrome measured out, the content face IS the fixture viewport,
        // so a lane aims at the rect the field is actually drawn with instead of at a number it invented.
        protected override float TitleBarHeight => 0f;

        protected override float SidePadding => 0f;

        protected override float AccentBarHeight => 0f;

        protected override UiHost CreateHost() => page.Host;

        protected override void DrawNotice(Rect rect, UiWindowNotice notice)
        {
        }

        protected override void BeforeDraw(Rect contentRect)
        {
            Event? current = Event.current;
            EventBeforeContents = current == null ? "none" : current.type.ToString();
        }

        public override void Close(bool doCloseSound = true)
        {
            CloseCount++;
            base.Close(doCloseSound);
        }

        /// <summary>One key press as the window stack dispatches it. Returns whether the key was consumed.</summary>
        internal bool PumpKey(KeyCode key)
        {
            Event e = Event.KeyboardEvent("key");
            e.type = EventType.KeyDown;
            e.keyCode = key;
            Event.current = e;
            WindowOnGUI();
            bool consumed = e.type == EventType.Used;
            Event.current = null;
            return consumed;
        }

        internal void PumpLayout()
        {
            Event e = Event.KeyboardEvent("layout");
            e.type = EventType.Layout;
            Event.current = e;
            WindowOnGUI();
            Event.current = null;
        }
    }


    private static int Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.Message);
            return 1;
        }
    }

    private static void Check(bool condition, string claim)
    {
        if (!condition) throw new Exception(claim);
    }

    private sealed class LaneMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 24f;

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class LaneTranslation : IUiTranslation
    {
        public string Translate(string key) => "[" + key + "]";

        public int TranslationRevision => 0;
    }

    /// <summary>
    /// A consumer-shaped specimen: a kind that drives <see cref="UiNative.NumberField"/> by hand and declares
    /// a command, which is the only way a page-model element becomes disabled. No core editable kind carries
    /// <c>ActionBind</c> in its schema today, so a lane that wants "the element an edit is open on was taken
    /// out of play" has to own the declaration itself — and that is also the shape a real consumer composite
    /// has, which is the point: the funnel's transaction applies to a hand-driven field exactly as it does to
    /// the core atom.
    /// </summary>
    private sealed class GatedNumber : IUiWidget
    {
        internal const string KindName = "test/gated-number";

        public string Kind => KindName;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
            bindings.ValidateValue<float>("gated", elementPath);
            bindings.ValidateCommand("gate", elementPath);
        }

        public float Measure(UiWidgetContext ctx) => 24f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            float current = ctx.Bindings.TryGet("gated", out float bound) ? bound : 0f;
            UiNative.NumberField(rect, "gated", ctx.Session, current, 0f, 1f, "0.##", out bool committed);
            if (!committed) return;

            // Deferred exactly like the core atom: while the field holds the edit, a valid parse is a draft
            // and not a write. A specimen that wrote on every keystroke would test nothing about §3.4.
            UiValueState edited = ctx.Session.GetOrCreateValueState("gated");
            if (!edited.Focused) ctx.Bindings.Set("gated", edited.FloatValue);
        }
    }
}
