using System;
using System.Collections.Generic;

using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The Cancel ladder lane (FL-IC2, interaction contract §3.2, §3.5, §3.6, §3.7). One press, one undone layer;
/// the layer is chosen by what the player actually did, not by what was drawn last.
/// <list type="bullet">
/// <item><b>Order</b> — the open option menu first, then a held pointer capture, then an open edit, then the
/// tree. The hook that starts all of this runs before the page is drawn, which is what lets a key be answered
/// by a control that has not seen it yet.</item>
/// <item><b>Starting point</b> — the element that TOOK the interaction, or the business subject a consumer
/// named. An element that merely drew, and an element nobody pressed, are not subjects: that is the difference
/// between a tree walk and "whatever was last on screen".</item>
/// <item><b>Nearest executable layer</b> — the walk climbs and runs the first <c>CancelBind</c> whose command
/// can run now. A layer that declares nothing is passed through (a layout container is a legitimate rung), a
/// layer whose command is vetoed is climbed past, and a layer naming a key nobody registered is climbed past
/// rather than allowed to throw.</item>
/// <item><b>Row scope</b> — inside a <c>Repeat</c>, the declared key is the item's own, so returning from one
/// row cannot be answered by another row's command.</item>
/// <item><b>Lifecycle</b> — a subject that is hidden, removed or switched away stops being a subject, and the
/// key that finds nothing to undo keeps Verse's meaning: it closes the window.</item>
/// </list>
/// <para>
/// <b>What this lane cannot show.</b> The window stack is the harness double, whose Accept/Cancel dispatch
/// order is the plan's reading made executable (see <c>VerseStubs.cs</c>) rather than a game run; the game's
/// own <c>GetsInput</c> also consults obscuring and mouse position, which no claim here depends on. Which
/// window the real stack hands the key to, with several mod windows open, and whether a rebind of the Cancel
/// key reaches the same hook, remain the short human pass.
/// </para>
/// </summary>
internal static class KernelCancelLadderTests
{
    private const string Scope = "ladder";

    private const string PageXml =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Templates>"
        + "<Row Id=\"row\"><Widget Id=\"pick\" Kind=\"input/button\" Text=\"pick\" ActionBind=\"openRow\""
        + " CancelBind=\"closeRow\" Height=\"20\" /></Row>"
        + "</Templates>"
        + "<Column Id=\"col\" Padding=\"0\" Gap=\"2\">"
        + "<Widget Id=\"menu\" Kind=\"input/dropdown\" OptionsBind=\"Options\" Height=\"28\" />"
        + "<Widget Id=\"chart\" Kind=\"chart/line\" Bind=\"points\" ActionBind=\"changed\" Editable=\"true\""
        + " EditablePoints=\"1\" Height=\"120\" />"
        + "<Section Id=\"detail\" VisibleKey=\"showDetail\" CancelBind=\"closeDetail\" Gap=\"2\">"
        + "<Column Id=\"plain\" Gap=\"2\">"
        + "<Repeat Id=\"rows\" Items=\"items\" Template=\"row\" />"
        + "</Column>"
        + "</Section>"
        + "<Widget Id=\"never\" Kind=\"input/button\" Text=\"never\" ActionBind=\"neverPressed\""
        + " CancelBind=\"closeNever\" Height=\"24\" />"
        + "</Column>"
        + "</UiPage>";

    private static int failures;

    public static int RunAll()
    {
        failures = 0;

        failures += Run("The open menu is the first layer, and the hook answers it before the page is drawn",
            VerifyMenuIsFirstLayerBeforeContents);
        failures += Run("Two presses undo two layers: menu, then the tree, then the window",
            VerifyOnePressOneLayerDownTheStack);
        failures += Run("A held chart drag is ended by the press that cancels it, before the tree is reached",
            VerifyHeldDragEndsBeforeTheTree);
        failures += Run("The nearest EXECUTABLE layer answers, and a vetoed one is climbed past",
            VerifyNearestExecutableLayerAnswers);
        failures += Run("A layer that declares nothing is passed through, not treated as an answer",
            VerifyTransparentLayerIsPassedThrough);
        failures += Run("A row's Cancel names that row's own command, not a sibling's",
            VerifyRowScopeOfTheCancelKey);
        failures += Run("A business target the consumer named starts the walk above the control that was clicked",
            VerifyBusinessTargetStartsTheWalk);
        failures += Run("A key nobody registered is climbed past instead of throwing",
            VerifyUnregisteredKeyIsClimbedPast);
        failures += Run("A subject hidden out of the page stops being a cancel subject",
            VerifyHiddenSubjectStopsBeingATarget);
        failures += Run("An element that was never pressed is not a subject, so the window still closes",
            VerifyUnpressedElementIsNotASubject);
        failures += Run("A composite's child press is a cancel subject whose element's layer answers",
            VerifyAChildPressIsACancelSubjectOfItsElement);

        return failures;
    }

    // --- order ---------------------------------------------------------------------------------------

    private static void VerifyMenuIsFirstLayerBeforeContents()
    {
        using Page page = new();
        LadderShell shell = page.AttachShell();
        try
        {
            page.OpenMenu();
            Check(page.Session.OpenPopupId != null, "the fixture opened its option menu");

            bool consumed = shell.PumpKey(KeyCode.Escape);
            Check(consumed, "the Cancel key was taken by the menu");
            Check(shell.EventBeforeContents == "Used",
                "the hook answered it BEFORE the page drew, so no control underneath can also react");
            Check(page.Session.OpenPopupId == null, "the menu closed");
            Check(page.CloseDetailCalls == 0 && page.WindowCloses == 0,
                "and neither the tree layer nor the window was touched by the same press");
            Check(Find.WindowStack.IsOpen(shell), "the window is still open");
        }
        finally
        {
            page.DetachShell(shell);
        }
    }

    private static void VerifyOnePressOneLayerDownTheStack()
    {
        using Page page = new();
        LadderShell shell = page.AttachShell();
        try
        {
            page.OpenMenu();
            page.PressRow("b");

            shell.PumpKey(KeyCode.Escape);
            Check(page.Session.OpenPopupId == null, "the first press closed the menu");
            Check(page.RowCloseCalls == 0 && page.CloseDetailCalls == 0, "and answered nothing else");

            shell.PumpKey(KeyCode.Escape);
            Check(page.RowCloseCalls == 1, "the second press answered the row the player clicked: "
                + page.RowCloseCalls);
            Check(page.CloseDetailCalls == 0, "and did not also climb to the section");

            // The row's own answer is that it is no longer open, which is what makes its layer inert; the walk
            // always runs the nearest layer that can answer NOW, so "逐层返回" is the business state moving up,
            // not the library remembering how far it has climbed.
            page.ClosedRows.Add("b");

            shell.PumpKey(KeyCode.Escape);
            Check(page.RowCloseCalls == 1 && page.CloseDetailCalls == 1,
                "the third press reached the section, one layer up: row=" + page.RowCloseCalls
                + " detail=" + page.CloseDetailCalls);

            page.DetailCancellable = false;

            shell.PumpKey(KeyCode.Escape);
            Check(page.WindowCloses == 1, "the fourth press found nothing to undo and closed the window");
            Check(!Find.WindowStack.IsOpen(shell), "with the window out of the stack");
        }
        finally
        {
            page.DetachShell(shell);
        }
    }

    private static void VerifyHeldDragEndsBeforeTheTree()
    {
        using Page page = new();
        LadderShell shell = page.AttachShell();
        try
        {
            page.BeginChartDrag();
            Check(page.Session.OwnedHotControl != null, "the chart holds the pointer capture");

            bool consumed = shell.PumpKey(KeyCode.Escape);

            Check(consumed, "the press that cancels a drag belongs to the drag");
            Check(page.Session.OwnedHotControl == null, "the capture is released");
            Check(page.RowCloseCalls == 0 && page.CloseDetailCalls == 0,
                "and the tree under it is not reached by the same press");
            Check(page.Changes.Count >= 1, "the value the drag already wrote stays written (§3.2: a Cancel ends an interaction, it does not roll back a result)");

            page.PumpLayout();
            Check(!page.Session.GetOrCreateValueState("chart").Dragging,
                "the chart sees it no longer holds the control and ends its own drag");
        }
        finally
        {
            page.DetachShell(shell);
        }
    }

    // --- the walk ------------------------------------------------------------------------------------

    private static void VerifyNearestExecutableLayerAnswers()
    {
        using Page page = new();
        LadderShell shell = page.AttachShell();
        try
        {
            page.DetailCancellable = false;
            page.PressRow("a");
            shell.PumpKey(KeyCode.Escape);
            Check(page.RowCloseCalls == 1 && page.CloseDetailCalls == 0,
                "the row's own layer answers while the section above it is vetoed");

            page.ClosedRows.Add("a");
            page.DetailCancellable = true;
            page.PressRow("a");
            shell.PumpKey(KeyCode.Escape);
            Check(page.RowCloseCalls == 1 && page.CloseDetailCalls == 1,
                "with the row's command vetoed the walk climbs to the section instead of stopping: row="
                + page.RowCloseCalls + " detail=" + page.CloseDetailCalls);
        }
        finally
        {
            page.DetachShell(shell);
        }
    }

    private static void VerifyTransparentLayerIsPassedThrough()
    {
        using Page page = new();
        LadderShell shell = page.AttachShell();
        try
        {
            page.ClosedRows.Add("a");
            page.PressRow("a");
            shell.PumpKey(KeyCode.Escape);

            Check(page.CloseDetailCalls == 1,
                "the plain Column between the row and the section declares nothing and is passed through");
        }
        finally
        {
            page.DetachShell(shell);
        }
    }

    private static void VerifyRowScopeOfTheCancelKey()
    {
        using Page page = new();
        LadderShell shell = page.AttachShell();
        try
        {
            page.PressRow("b");
            UiNode rowNode = page.Session.LastInteractionNode!;
            Check(rowNode.CancelBindingKey == "items.b.closeRow",
                "the row publishes ITS OWN scoped key, not the declared one: " + rowNode.CancelBindingKey);

            shell.PumpKey(KeyCode.Escape);
            Check(page.ClosedRow == "b", "and the press closed that row: " + page.ClosedRow);

            page.PressRow("a");
            shell.PumpKey(KeyCode.Escape);
            Check(page.ClosedRow == "a" && page.RowCloseCalls == 2,
                "a different row answers with its own command, not the first row's");
        }
        finally
        {
            page.DetachShell(shell);
        }
    }

    private static void VerifyBusinessTargetStartsTheWalk()
    {
        using Page page = new();
        LadderShell shell = page.AttachShell();
        try
        {
            page.PressRow("a");
            Check(page.Session.SetCancelTarget("detail"), "the consumer named the section as the subject");

            shell.PumpKey(KeyCode.Escape);
            Check(page.CloseDetailCalls == 1 && page.RowCloseCalls == 0,
                "the walk starts at the named subject, above the control that was clicked");

            page.Session.ClearCancelTarget();
            page.PressRow("a");
            shell.PumpKey(KeyCode.Escape);
            Check(page.RowCloseCalls == 1, "and with the target released the walk is back on the interaction");
        }
        finally
        {
            page.DetachShell(shell);
        }
    }

    private static void VerifyUnregisteredKeyIsClimbedPast()
    {
        using Page page = new();
        LadderShell shell = page.AttachShell();
        try
        {
            page.PressRow("c");
            Check(page.Session.LastInteractionNode!.CancelBindingKey == "items.c.closeRow",
                "the row declares a key the consumer never registered as a command");

            shell.PumpKey(KeyCode.Escape);
            Check(page.CloseDetailCalls == 1,
                "the walk climbs past the inert declaration and answers the section, and nothing threw");
        }
        finally
        {
            page.DetachShell(shell);
        }
    }

    private static void VerifyHiddenSubjectStopsBeingATarget()
    {
        using Page page = new();
        LadderShell shell = page.AttachShell();
        try
        {
            Check(page.Session.SetCancelTarget("detail"), "the section is the subject");
            page.ShowDetail = false;
            page.Host.Bindings.NotifyChanged("showDetail");
            page.PumpLayout();
            page.PumpLayout();


            Check(page.Session.CancelTargetNode == null,
                "a subject hidden out of the page is no longer a subject (§3.7)");

            shell.PumpKey(KeyCode.Escape);
            Check(page.CloseDetailCalls == 0, "and the key does not run the hidden layer's command");
            Check(page.WindowCloses == 1, "with nothing else to undo, the window keeps its native meaning");
        }
        finally
        {
            page.DetachShell(shell);
        }
    }

    private static void VerifyUnpressedElementIsNotASubject()
    {
        using Page page = new();
        LadderShell shell = page.AttachShell();
        try
        {
            page.PumpLayout();
            Check(page.Session.LastInteractionNode == null, "a page that drew but was never touched has no subject");

            shell.PumpKey(KeyCode.Escape);
            Check(page.CloseNeverCalls == 0, "the button that drew with a CancelBind never answered for it");
            Check(page.WindowCloses == 1, "and the key closed the window, as it did before this ladder existed");
        }
        finally
        {
            page.DetachShell(shell);
        }
    }

    /// <summary>
    /// The combined-control case the contract names for the tree walk (§3.6): a composite draws its own button
    /// through <see cref="UiWidgetContext.Child(string)"/>, so the node that took the press carries no declared
    /// element id and no arranged rect of its own. Judging liveness by geometry alone dropped a press the player
    /// really made at the pass boundary, and the walk then started nowhere — the Cancel key fell through to the
    /// window, which is the failure of a composite that cannot return along the tree rather than a return.
    /// </summary>
    private static void VerifyAChildPressIsACancelSubjectOfItsElement()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        BranchComposite composite = new();
        UiWidgetRegistry.Register(Scope, BranchComposite.KindName, () => composite);

        int cancellations = 0;
        UiBindings bindings = new();
        bindings.BindCommand("closeDetail", () => cancellations++);

        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"col\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"branch\" Kind=\"" + BranchComposite.KindName + "\" CancelBind=\"closeDetail\""
            + " Height=\"28\" />"
            + "</Column>"
            + "</UiPage>";

        Rect viewport = new Rect(0f, 0f, 300f, 200f);
        using UiHost host = new UiHost(Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla,
            new LaneMetrics(), new LaneTranslation());
        try
        {
            foreach (EventType type in new EventType[]
            {
                EventType.Layout, EventType.MouseDown, EventType.MouseUp, EventType.Repaint,
            })
            {
                Event e = Event.KeyboardEvent("click");
                e.type = type;
                e.button = 0;
                e.mousePosition = new Vector2(20f, 14f);
                Event.current = e;
                host.DrawFrame(viewport);
                Event.current = null;
            }

            Check(composite.Presses == 1, "the composite's own button actually fired: " + composite.Presses);
            Check(composite.LastTarget != null && !composite.LastTarget.IsArranged,
                "the child that took it carries no arranged rect, which is what the old liveness rule judged it by");
            Check(ReferenceEquals(host.Session.LastInteractionNode, composite.LastTarget),
                "and the session still holds it as the subject after the pass boundary");

            Event key = Event.KeyboardEvent("escape");
            key.type = EventType.KeyDown;
            key.keyCode = KeyCode.Escape;
            Event.current = key;
            bool handled = host.TryHandleCancel();
            Event.current = null;

            Check(handled && cancellations == 1,
                "the walk climbs from the child to its element's declared layer and runs it once: handled="
                + handled + " cancelCalls=" + cancellations);
            Check(key.type == EventType.Used, "and the key that answered is consumed");
            KernelTripGuard.ExpectNoTrips(host.Session, "child cancel subject");
        }
        finally
        {
            UiWidgetRegistry.Clear();
        }
    }

    // --- fixture -------------------------------------------------------------------------------------

    /// <summary>
    /// One page with a menu, a chart, a section, and a two-row <c>Repeat</c> inside it, with every command
    /// counted separately so "one press, one action" is an observation rather than a reading of the code.
    /// </summary>
    private sealed class Page : IDisposable
    {
        private static readonly List<string> Options = new() { "x", "y", "z" };

        internal readonly UiHost Host;
        internal readonly UiBindings Bindings;
        internal readonly Rect Viewport = new(0f, 0f, 300f, 420f);
        internal readonly List<UiChartPointChange> Changes = new();

        internal string Choice = "x";
        internal bool ShowDetail = true;
        internal bool DetailCancellable = true;
        internal readonly HashSet<string> ClosedRows = new();
        internal string ClosedRow = "";

        internal int CloseDetailCalls;
        internal int CloseNeverCalls;
        internal int RowCloseCalls;
        internal int WindowCloses;

        private readonly List<string> keys = new() { "a", "b", "c" };
        // The same three-point shape the IC1 drag lane drives: its middle control point sits at the plot
        // centre, which is where a lane aiming at the chart's middle actually lands a press.
        private readonly List<UnityEngine.Vector2> points = new()
        {
            new UnityEngine.Vector2(0f, 1f),
            new UnityEngine.Vector2(0.5f, 0.5f),
            new UnityEngine.Vector2(1f, 0f),
        };


        internal Page()
        {
            UiWidgetRegistry.Clear();
            UiWidgetRegistry.InitializeCore();

            Bindings = new UiBindings();
            Bindings.BindValue("menu", () => Choice, value => Choice = value);
            Bindings.BindOptions("Options", () => Options);
            Bindings.BindValue("showDetail", () => ShowDetail, value => ShowDetail = value);
            Bindings.BindReadOnly<IReadOnlyList<string>>("items", () => keys, UiInvalidation.Structure);
            Bindings.BindReadOnly<IReadOnlyList<UnityEngine.Vector2>>("points", () => points);
            Bindings.BindAction<UiChartPointChange>("changed", Changes.Add);
            Bindings.BindCommand("closeDetail", () => CloseDetailCalls++, () => DetailCancellable);
            Bindings.BindCommand("closeNever", () => CloseNeverCalls++);
            Bindings.BindCommand("neverPressed", () => { });
            Bindings.BindCommand("openRow", () => { });

            foreach (string key in keys)
            {
                string captured = key;
                Bindings.BindCommand("items." + captured + ".openRow", () => { });

                // Row "c" is the specimen for the authoring mistake the ladder has to survive: the template
                // declares a Cancel layer whose command nobody ever registered. The row still draws, still
                // takes presses, and its declared layer must be climbed past rather than thrown.
                if (captured == "c") continue;
                Bindings.BindCommand("items." + captured + ".closeRow",
                    () => { ClosedRow = captured; RowCloseCalls++; ClosedRows.Add(captured); },
                    () => !ClosedRows.Contains(captured));
            }

            Host = new UiHost(Scope, UiLayoutManifest.Parse(PageXml), Bindings, UiTheme.Vanilla,
                new LaneMetrics(), new LaneTranslation());
        }

        internal UiSession Session => Host.Session;

        internal Rect RectOf(string elementId) => Host
            .MeasureAndArrange(new Vector2(Viewport.width, Viewport.height))
            .RectById[elementId];

        internal void OpenMenu()
        {
            Click(RectOf("menu"));
            if (Session.OpenPopupId == null) throw new Exception("the fixture's dropdown did not open");
        }

        /// <summary>
        /// Clicks one row of the <c>Repeat</c>. A materialized element carries its own id plus the item key
        /// (<c>pick#a</c>), which is the identity the row's state and its scoped bindings hang off; the rect is
        /// read from the snapshot under that id rather than derived from a row height.
        /// </summary>
        internal void PressRow(string key)
        {
            string id = "pick#" + key;
            Click(RectOf(id));
            UiNode? took = Session.LastInteractionNode;
            if (took == null || took.ElementId != id)
            {
                throw new Exception("the row button did not take the press: " + (took?.ElementId ?? "<none>"));
            }
        }

        /// <summary>
        /// The press-drag sequence the IC1 lane uses: a press at the plot centre, one settling pass, then the
        /// drag. A native hot control is allocated per pass, so a lane that drags in the breath between the
        /// press and the settling pass is testing its own timing rather than the chart's.
        /// </summary>
        internal void BeginChartDrag()
        {
            Vector2 centre = Centre(RectOf("chart"));
            Pump(EventType.Layout, centre);
            Pump(EventType.MouseDown, centre);
            Pump(EventType.Repaint, centre);
            Pump(EventType.MouseDrag, centre + new Vector2(24f, -16f));
        }

        /// <summary>One click as the game dispatches it: separate events, not one flag.</summary>
        private void Click(Rect rect)
        {
            Vector2 point = Centre(rect);
            Pump(EventType.Layout, point);
            Pump(EventType.MouseDown, point);
            Pump(EventType.MouseUp, point);
            Pump(EventType.Repaint, point);
        }

        private void Pump(EventType type, Vector2 point)
        {
            Event e = Event.KeyboardEvent("pump");
            e.type = type;
            e.button = 0;
            e.mousePosition = point;
            Event.current = e;
            Host.DrawFrame(Viewport);
            Event.current = null;
        }

        internal void PumpLayout()
        {
            Pump(EventType.Layout, Vector2.zero);
        }

        private static Vector2 Centre(Rect rect) => new(rect.x + rect.width / 2f, rect.y + rect.height / 2f);

        internal LadderShell AttachShell()
        {
            LadderShell opening = new LadderShell(this);
            Find.WindowStack.Add(opening);
            opening.windowRect = new Rect(0f, 0f, Viewport.width, Viewport.height);

            // One pass THROUGH THE WINDOW: the shell adopts its page host lazily inside its own guarded pass,
            // so a lane that only ever drew the host directly would hand the key to a shell holding no host -
            // and the ladder would silently never run.
            opening.PumpLayout();
            return opening;
        }

        internal void DetachShell(LadderShell closing)
        {
            UiNative.DebugMousePositionEnabled = false;
            UiNative.DebugMouseDown = false;
            UiNative.DebugMouseDrag = false;
            UiNative.DebugMouseUp = false;
            Find.WindowStack.TryRemove(closing);
        }

        public void Dispose() => Host.Dispose();
    }

    /// <summary>
    /// The window half: a real shell pass through the double's <c>WindowOnGUI</c>, recording the event phase
    /// the page is about to be drawn in and counting closes, so a doubled action is visible as a number.
    /// </summary>
    private sealed class LadderShell : UiWindowHost
    {
        private readonly Page page;

        internal LadderShell(Page page)
        {
            this.page = page;
        }

        internal string EventBeforeContents = "none";

        protected override UiTheme Theme => UiTheme.Vanilla;

        protected override string Title => "title";

        protected override string CloseText => "close";

        // Chrome measured out, so the content face IS the fixture viewport and a lane aims at the rect the
        // widget is actually drawn with.
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
            page.WindowCloses++;
            base.Close(doCloseSound);
        }

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

        /// <summary>One ordinary pass: the shell adopts its host and the page draws.</summary>
        internal void PumpLayout()
        {
            Event e = Event.KeyboardEvent("layout");
            e.type = EventType.Layout;
            Event.current = e;
            WindowOnGUI();
            Event.current = null;
        }
    }

    private static void Check(bool condition, string claim)
    {
        if (!condition) throw new Exception(claim);
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
    /// A consumer-shaped specimen: one element that owns a sub-control drawn through the child context the page
    /// model hands a composite. It exists so the child path is tested the way a consumer builds it, not the way
    /// a lane could fake it.
    /// </summary>
    private sealed class BranchComposite : IUiWidget
    {
        internal const string KindName = "test/branch-composite";

        internal int Presses;

        internal UiNode? LastTarget;

        public string Kind => KindName;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 28f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            UiWidgetContext branch = ctx.Child("branch");
            LastTarget = branch.Node;
            if (UiNative.Button(rect, branch)) Presses++;
        }
    }
}
