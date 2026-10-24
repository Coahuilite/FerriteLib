using System;
using System.Collections.Generic;
using System.Threading;

namespace FerriteLib.UiKit.Kernel;
/// <summary>
/// Per-Host transient interaction state. One Settings Window / Overlay owns exactly one
/// <see cref = "UiSession"/>; closing the host disposes the session and all transient state is lost.
/// This type intentionally replaces the old process-wide <c>UiValueStore</c>/<c>UiPageState</c>.
/// </summary>
public sealed class UiSession : IDisposable
{
    public UiControls Controls { get; }
    /// <summary>
    /// Creates an explicit control scope owned by this session, without a page, registry or Host.
    /// Keep the session for the control lifetime and use Child(name) for separate control identities.
    /// At each native callback capture one frame on the execution owner and call SetFrame once before
    /// operating controls. Context creation itself needs neither native execution nor reactive bindings.
    /// Dispose the session before disposing its execution owner.
    /// </summary>
    public UiWidgetContext CreateContext(string source, string scope, ITextMetrics metrics, UiTheme theme,
        IUiTranslation translation, IUiBindingAccess bindings, float viewWidth)
    {
        if (!IsActive) throw new ObjectDisposedException(nameof(UiSession));
        UiBindings.OwnerOf(bindings);
        UiNode node = GetOrCreateSubNode(Root, scope);
        return new UiWidgetContext(source, this, metrics, theme, translation, bindings, viewWidth, node.Path, node: node);
    }
    public Action<string>? Warning { get; set; }
    internal void ReportWarning(string message){if(Warning!=null)Warning(message);else if(execution!=null)execution.Warning(message);else System.Diagnostics.Trace.TraceWarning(message);}
    public UiFrame Input { get; private set; } = new UiFrame(UiFramePhase.None, inputAllowed:false);
    private IUiExecution? execution;
    internal IUiExecution? CurrentExecution => execution;
    public IUiExecution Execution => execution ?? throw new InvalidOperationException("Drawing or native input requires an explicit execution scope.");
    public IUiDrawing Drawing => Execution;
    /// <summary>
    /// Borrows execution for one actual callback. Idle replacement is legal; active capture/edit or an
    /// open hit pass must finish on the old execution. Rejection changes neither input nor ownership.
    /// The caller owns execution disposal and its scoped images, including after session cleanup fails.
    /// </summary>
    public void SetFrame(UiFrame frame, IUiExecution execution)
    {
        if (!IsActive) throw new ObjectDisposedException(nameof(UiSession));
        if(frame==null)throw new ArgumentNullException(nameof(frame));
        if(execution==null)throw new ArgumentNullException(nameof(execution));
        // Execution is borrowed, not owned. Idle owners may change it, including owners that share
        // an outer execution. A held interaction cannot silently migrate to another native mapping.
        if(this.execution!=null && !ReferenceEquals(this.execution,execution))
        {
            bool focused=false;
            foreach(UiNode node in nodes.Values)
                foreach(UiControlState state in node.ValueStates.Values)
                    focused |= state.IsFocused;
            if(hitPassOpen || ownedHotControl.HasValue || Interactions.CaptureOwner.HasValue || focused || activeEditState!=null)
                throw new InvalidOperationException("Cannot replace borrowed execution during a draw pass or active capture/edit. End the interaction on its current execution first.");
        }
        // Capture this once at the actual owner callback boundary, before an earlier control consumes
        // the shared frame. Independent C# editing uses the same fact without a Host/hit-pass prerequisite.
        if(!ReferenceEquals(Input,frame))
            passBeganWithPrimaryPress=frame.Phase==UiFramePhase.MouseDown&&frame.Button==0;
        Input = frame;
        this.execution = execution;
        PointerInputAllowed = frame.InputAllowed;
    }
    internal UiNode Root => unscopedNode;
    public UiInteractionSession Interactions { get; } = new UiInteractionSession();

    /// <summary>Native window qualification. Embedded hosts default to their existing input scope.</summary>
    public bool PointerInputAllowed { get; set; } = true;
    private static readonly IReadOnlyDictionary<string, UiControlState> NoValueStates =
        new Dictionary<string, UiControlState>(StringComparer.Ordinal);

    // Identity is per session, not per node: a diagnostic subscription is keyed by it, and the whole point
    // of that key is that it survives as a number after the session itself is gone. A counter is bounded
    // state (an int wraps in 2^31 sessions), never a table of live objects.
    private static int nextIdentity;

    // Scroll state is keyed by the scroll container's node, not by a display path: two scroll elements
    // whose display paths collide still hold two scroll positions (0.4.0 node step 2).
    private readonly Dictionary<UiNode, UiLayoutPoint> scrollPositions = new();

    // Every arranged element's node, keyed by identity, plus the "no element" node a host-level caller
    // resolves into. The nodes own their element's state, and the table is the session's, so disposing
    // the session drops nodes and state together (0.4.0 identity layer, node step 1).
    private readonly Dictionary<UiNodeId, UiNode> nodes = new();
    private readonly HashSet<UiNode> dirtyNodes = new();
    private readonly UiNode unscopedNode;

    // The owned hit stack: the last complete draw pass's paint order (bottom-to-top) plus the list the pass
    // in progress is building. Dispatch reads the published list, which is why a popup drawn after content
    // in pass N still wins the click in pass N+1 while no element has to know that popups exist.
    private readonly List<UiHitLayer> hitLayers = new();
    private bool hitPassOpen;
    public int DefinitionRevision { get; internal set; }
    internal bool IsInputGeometryReliable { get; private set; } = true;
    private readonly List<UiHitLayer> dispatchLayers = new();
    private readonly List<UiPointerLayer> contentInputLayers = new();
    internal void SetContentInputLayers(IEnumerable<UiPointerLayer> layers)
    {
        contentInputLayers.Clear();
        contentInputLayers.AddRange(layers);
    }
    private UiLayoutPoint currentWindowOrigin;

    // Recovery slots are keyed by the node that tripped, so two elements that print the same display
    // path cannot share one fallback slot (0.4.0 node step 2).
    private readonly HashSet<UiNode> trippedNodes = new();
    private readonly Dictionary<UiNode, string> trippedLogs = new();
    private UiControlKey? ownedHotControl;
    private readonly List<Action> popupDrawActions = new();
    private string? openPopupId;
    private UiInteractionTarget? popupTarget;
    private UiInteractionTarget? popupCaptureTarget;
    private UiLayoutRect? openPopupAnchor;
    // Whether the open popup's owner reported itself drawn in the pass in progress. Reset at every pass
    // boundary, where a false value releases the popup (see EndHitPass) - that is the half a call-site
    // refresh cannot see: an owner that is no longer arranged may never call its trigger again.
    private bool popupOwnerDrawn;
    // How far the open popup's option list is scrolled, in ROWS past its first option, and the largest scroll
    // the list published for the pass it last drew. The popup draws, hit-tests and scrolls through one rect
    // (UiPopup), so these two numbers are that rect's only vertical state; they belong with the rest of the
    // popup's session-local facts and die when the popup closes or the session is disposed.
    private int openPopupScrollRows;
    private int openPopupMaxScrollRows;
    // The element that holds this session's pointer capture, so "the owner is no longer valid" is a
    // readable fact rather than an anonymous int: a capture belongs to a session, a control id AND a node.
    private UiNode? ownedHotControlOwner;
    // Whether that owner reported itself drawn in the pass in progress, the capture half of the same
    // pass-boundary reconcile the open popup uses: a control that is hidden, removed or switched away
    // cannot run its own MouseUp release any more, and a capture nobody can release stops later presses
    // from reaching whatever is now on screen.
    private bool captureOwnerDrawn;
    // The pass-boundary press fact above; false outside a press pass and re-read at every BeginHitPass.
    private bool passBeganWithPrimaryPress;
    // The three things a single Cancel keypress can be asked to undo, in the order the interaction contract
    // puts them (FL-IC2, §3.2/§3.4/§3.6): the open popup, the held pointer capture, the open edit — and the
    // tree walk those three leave untouched starts from the last node that actually took an interaction, or
    // from a business target a consumer named. Each is a REFERENCE to a node, never an id string: id strings
    // are how the covered-trigger defect found two answers to "which element owns this", and the node table
    // already owns identity. They are cleared at the pass boundary when their node stops being arranged, and
    // with the session.
    private UiNode? activeEditNode;
    private UiControlState? activeEditState;
    private UiNode? lastInteractionNode;
    private UiNode? cancelTargetNode;
    internal UiLayoutRect? CurrentClip { get; set; }
    private UiLayoutRect hostViewport;
    private string? scrollTargetElementId;
    private string hoverClaim = "";
    private UiNodeId hoverClaimElement;
    private string hoverHeld = "";
    private UiNodeId hoverHeldElement;
    private UiNode activeNode;
    private int hoverClaimStamp;
    private int hoverGraceLeft;

    /// <summary>
    /// Creates one session and its session-level node: the identity an element-less caller (a host-level
    /// call, a popup pass) resolves into, which keeps <see cref="GetControlState(string)"/> total
    /// outside a tree exactly as it was before the node step.
    /// </summary>
    public UiSession()
    {
        Identity = Interlocked.Increment(ref nextIdentity);
        Controls = new UiControls(this);
        unscopedNode = new UiNode(this, UiNodeId.None, "", -1);
        nodes.Add(UiNodeId.None, unscopedNode);
        activeNode = unscopedNode;
    }

    /// <summary>
    /// Process-unique session identity, stable from construction and never reused. A diagnostic
    /// subscription and every event it buffers are keyed by this number rather than by a session
    /// reference, which is what lets a buffered event outlive nothing at all: the session object can be
    /// collected while the record of what it reported stays readable.
    /// </summary>
    public int Identity { get; }

    /// <summary>True until <see cref="Dispose"/> is called.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// How many further passes a hover claim keeps explaining the panel after the pointer leaves, in
    /// <see cref="Frame"/> units. Part of the claim machine's contract rather than a library default:
    /// the consumer that needed the grace picks its length, the library only owns the rule.
    /// </summary>
    public int HelpGracePasses { get; set; }

    /// <summary>
    /// Monotonic per-pass counter, incremented once by <see cref="BeginFrame"/>. This is the clock the
    /// hover-claim machine runs on (US→FL round 1, P3) — deliberately not wall-clock: the settings
    /// window opens with <c>forcePause</c>, where game time freezes and a seconds-based grace would
    /// never expire. One increment is one IMGUI pass, not one rendered frame; the consumer's own
    /// hand-rolled machine already proved that base.
    /// </summary>
    public int Frame { get; private set; }

    /// <summary>
    /// The engine's arrangement-cache clock: monotonic, moved by things the band cache cannot see for
    /// itself - a layout-bearing theme token, or a <see cref="UiInvalidation.Measure"/> /
    /// <see cref="UiInvalidation.Structure"/> announcement the engine committed at a frame boundary.
    /// <para>
    /// It is deliberately <b>not</b> the library's invalidation API. A consumer announces a model change
    /// per key through <see cref="IUiBindingAccess.NotifyChanged"/>, and a <see cref="UiInvalidation.Paint"/>
    /// announcement leaves this clock alone on purpose: a fixed-size readout must not re-arrange the page.
    /// There is exactly one such clock; do not add a second.
    /// </para>
    /// </summary>
    public int ContentRevision { get; private set; }

    /// <summary>
    /// The current effective consumer topic reference, including retained grace, or null. Read the completed
    /// pass; the engine and widgets refresh it with
    /// <see cref="PublishHelp"/>.
    /// <para>
    /// The reference is opaque: FL does not resolve text, translation resources or bindings from it. The
    /// engine passes an element/choice's explicit HelpRef, never an inferred Value/Text. The element that
    /// made the claim is <see cref="CurrentHelpSource"/>.
    /// </para>
    /// </summary>
    public string? CurrentHelpRef => hoverClaim.Length > 0 ? hoverClaim : null;

    /// <summary>
    /// The element that made the claim <see cref="CurrentHelpRef"/> is presenting, or null when nothing is
    /// claimed. The claim string stays the consumer's vocabulary (a section key it compares against),
    /// so two unnamed siblings claiming the same string are still indistinguishable through
    /// <see cref="CurrentHelpRef"/> - this is the axis that tells them apart, and the grace machine carries
    /// it together with the string so a held claim is attributed to the element that made it.
    /// <see cref="UiNodeId.None"/> means the claim was made outside any element (a host-level caller).
    /// </summary>
    public UiNodeId? CurrentHelpSource => hoverClaim.Length > 0 ? hoverClaimElement : null;

    /// <summary>
    /// The element whose Measure/Draw is running, or <see cref="UiNodeId.None"/> between elements.
    /// The engine enters an element's node around its own widget calls, and
    /// <see cref="GetControlState(string)"/> resolves against it: that is how a widget written
    /// against the old bare-string key gets a per-element state namespace without changing a line.
    /// </summary>
    public UiNodeId ActiveElement => activeNode.Id;

    /// <summary>
    /// The node whose Measure/Draw is running - the session-level node between elements. This is the
    /// object a widget reads its own state from and marks dirty; the engine enters it in a
    /// <c>finally</c> around every element's Measure and Draw.
    /// </summary>
    internal UiNode ActiveNode => activeNode;

    /// <summary>Session-scoped scroll positions, keyed by the scroll container's node.</summary>
    internal IReadOnlyDictionary<UiNode, UiLayoutPoint> ScrollPositions => scrollPositions;

    /// <summary>
    /// Nodes currently tripped into session fallback. Keyed by node, so the display path a diagnostic
    /// prints plays no part in which element owns a recovery slot.
    /// </summary>
    internal IReadOnlyCollection<UiNode> TrippedNodes => trippedNodes;

    /// <summary>Session-scoped popup draw callbacks. Not process-global.</summary>
    internal IReadOnlyList<Action> PopupDrawActions => popupDrawActions;

    /// <summary>Id of the popup currently owned by this session, if any.</summary>
    public string? OpenPopupId => openPopupId;
    internal UiInteractionTarget? PopupTarget => popupTarget;

    /// <summary>Anchor rect of the popup currently owned by this session, if any.</summary>
    public UiLayoutRect? OpenPopupAnchor => openPopupAnchor;

    /// <summary>
    /// The owned hit stack in paint order, bottom-to-top, as built by the draw pass that is running or just
    /// finished: content layers as their elements draw, then the popup layer. Popups are the entries with
    /// <see cref="UiHitLayer.IsPopup"/>, and a diagnostic or a lane reads this instead of a single
    /// "covered rect".
    /// </summary>
    internal IReadOnlyList<UiHitLayer> HitLayers => hitLayers;

    /// <summary>
    /// The window-space origin of the element being drawn. The funnel lifts a draw-local pointer by it,
    /// which is how a primitive compares its pointer with the window-space hit layers.
    /// </summary>
    internal UiLayoutPoint CurrentWindowOrigin => currentWindowOrigin;

    internal void SetCurrentWindowOrigin(UiLayoutPoint origin)
    {
        currentWindowOrigin = origin;
    }

    /// <summary>
    /// Opens a hit pass: the pass that just finished becomes the stack input dispatch reads and a fresh one
    /// starts. Dispatch therefore sees a complete paint order - popups included - while the pass in
    /// progress is still being drawn.
    /// </summary>
    internal void BeginHitPass()
    {
        EnsureActive();
        hitPassOpen = true;
        popupOwnerDrawn = false;
        captureOwnerDrawn = false;

        CurrentClip = hostViewport.Width > 0f && hostViewport.Height > 0f ? hostViewport : (UiLayoutRect?)null;
        contentInputLayers.Clear();
        dispatchLayers.Clear();
        dispatchLayers.AddRange(hitLayers);
        hitLayers.Clear();
    }

    /// <summary>
    /// True when the pass now running began as a primary-button press, read before any control could consume
    /// it. The field-side blur rule needs it because consumption erases the evidence from the event itself.
    /// </summary>
    internal bool PassBeganWithPrimaryPress => passBeganWithPrimaryPress;

    /// <summary>
    /// Appends one layer to the pass in progress. Content layers are appended as their elements draw, so the
    /// stack ends up in paint order; a popup appends after content and is therefore above it.
    /// </summary>
    internal void PushHitLayer(UiNode element, UiLayoutRect windowRect, bool isPopup, string? popupOwnerId = null)
    {
        if (element == null) return;
        hitLayers.Add(new UiHitLayer(element, windowRect, isPopup, popupOwnerId));
    }

    /// <summary>
    /// Topmost-first dispatch: true when <paramref name="windowPoint"/> falls inside a popup layer that
    /// covers the caller, so it must not take the click. Element identity grants no exemption, and owning the
    /// popup is not an exemption either: the covering layer's rect is the whole question. A dropdown trigger
    /// therefore keeps its toggle-to-close, because a trigger click that lands OUTSIDE the menu's rect is not
    /// covered by anything, while a click that lands INSIDE it belongs to the option row under the pointer -
    /// which is the ruling of the interaction contract: one session's option menu outranks the trigger bar and
    /// the chart beneath it.
    /// <para>
    /// The owner-id exemption this method used to carry is the confirmed F09/D1 defect: <c>UiPopup.RectFor</c>
    /// can place a menu over its own anchor (the clamp branch, and any bounded menu taller than the room on
    /// both sides of the trigger), the covered trigger then consumed the press first, and the option row drew
    /// afterwards into an event that was already used. It was never an exemption for a SIBLING of the same
    /// composite element, and geometry does not need one for the owner either.
    /// </para>
    /// <para>
    /// Current content regions are prepared from declared input geometry in final paint order before any
    /// native widget runs. The neutral UiPointerLayers decision handles content coverage without reversing
    /// IMGUI calls; unknown custom outer rectangles never become guessed opaque input layers.
    /// </para>
    /// </summary>
    public bool IsPointerOverHigherLayer(UiNode element, UiLayoutPoint windowPoint)
        => IsPointerOverPopup(windowPoint) || UiPointerLayers.IsCovered(contentInputLayers, ContentLayerOwner(element), windowPoint.X, windowPoint.Y);

    private string ContentLayerOwner(UiNode element)
    {
        // A ctx.Child has its own interaction identity, but draws inside its owning widget's layer.
        for (UiNode? candidate = element; candidate != null; candidate = candidate.Parent)
            for (int i = contentInputLayers.Count - 1; i >= 0; i--)
                if (contentInputLayers[i].Owner == candidate.Id.Key) return candidate.Id.Key;
        return element.Id.Key;
    }
    internal bool IsPointerOverPopup(UiLayoutPoint windowPoint)
    {
        for (int i = dispatchLayers.Count - 1; i >= 0; i--)
            if (dispatchLayers[i].IsPopup && Contains(dispatchLayers[i].Rect, windowPoint)) return true;
        return false;
    }
    /// <summary>
    /// Same dispatch as <see cref="IsPointerOverHigherLayer(UiNode,Vector2)"/>, with the caller's own popup id
    /// beside it. The id is a RECORD, not a licence: coverage is geometric for every caller, including the
    /// dropdown that owns the covering menu, since FL-IC1 retired the owner exemption this overload used to
    /// apply. The signature stays because it is public surface and a removal belongs at a minor boundary, not
    /// inside a behaviour slice; a call site keeps compiling and starts yielding the way the contract reads.
    /// </summary>
    public bool IsPointerOverHigherLayer(UiNode element, UiLayoutPoint windowPoint, string? ownPopupId)
    {
        return IsPointerOverHigherLayer(element, windowPoint);
    }

    /// <summary>
    /// True when the pointer sits inside the rect of the popup this session has open, read from the same
    /// published layers as every other coverage decision. The bounded option list uses it to decide whether
    /// the wheel belongs to the menu.
    /// </summary>
    internal bool OpenPopupCoversPointer(UiLayoutPoint windowPoint)
    {
        if (openPopupId == null) return false;

        for (int i = dispatchLayers.Count - 1; i >= 0; i--)
        {
            UiHitLayer layer = dispatchLayers[i];
            if (!layer.IsPopup) continue;
            if (layer.PopupOwnerId != null
                && string.Equals(layer.PopupOwnerId, openPopupId, StringComparison.Ordinal))
            {
                return Contains(layer.Rect, windowPoint);
            }
        }

        return false;
    }

    /// <summary>
    /// How far the open option list is scrolled, in rows past its first option. <see cref="UiPopup"/> is the
    /// only frame that knows how many rows the bounded menu shows, so it is the only writer of the extent:
    /// the list publishes its own limit every pass it draws and clamps here, which is what makes the geometry,
    /// the hit test and the scroll read ONE finite viewport instead of three opinions. Zero whenever no popup
    /// is open, and a new open starts at the top.
    /// </summary>
    public int OpenPopupScrollRows => openPopupScrollRows;

    /// <summary>The largest scroll the menu published for the pass it last drew.</summary>
    internal int OpenPopupMaxScrollRows => openPopupMaxScrollRows;

    /// <summary>
    /// Records the scrollable range of the option list that is drawing, and pulls the stored scroll back
    /// inside it. A menu whose list shrank (a filter, a removal) must not keep a scroll past its own end:
    /// that would hit-test rows that are not on screen.
    /// </summary>
    internal void NotePopupScrollExtent(int maxScrollRows)
    {
        if (openPopupId == null)
        {
            openPopupScrollRows = 0;
            openPopupMaxScrollRows = 0;
            return;
        }

        openPopupMaxScrollRows = maxScrollRows < 0 ? 0 : maxScrollRows;
        if (openPopupScrollRows > openPopupMaxScrollRows) openPopupScrollRows = openPopupMaxScrollRows;
        if (openPopupScrollRows < 0) openPopupScrollRows = 0;
    }

    /// <summary>
    /// Moves the open menu's scroll by <paramref name="deltaRows"/> rows, clamped to the range the list
    /// published. Negative deltas scroll toward the first option. Returns true when the scroll moved, so the
    /// caller can say whether the wheel actually did something.
    /// </summary>
    internal bool ScrollPopupByRows(int deltaRows)
    {
        if (openPopupId == null || deltaRows == 0) return false;

        int next = openPopupScrollRows + deltaRows;
        if (next < 0) next = 0;
        if (next > openPopupMaxScrollRows) next = openPopupMaxScrollRows;
        if (next == openPopupScrollRows) return false;

        openPopupScrollRows = next;
        if (Controls.Trace != null)
        {
            // Caller-agnostic facts only, like every other line the funnel reports: which menu moved, where
            // it stands, how far it can go. This session cannot read the backend - the event phase belongs to
            // the funnel's own trace lines, not to a session that has no business touching Event.current.
            Controls.Trace("wheel popup id=" + openPopupId + " rows=" + openPopupScrollRows
                + " of " + openPopupMaxScrollRows);
        }

        return true;
    }

    // An aborted traversal must not publish its partial hit list or reconcile unvisited identities.
    internal void AbortHitPass()
    {
        contentInputLayers.Clear();
        if (!IsActive) return;
        if (hitPassOpen)
        {
            hitLayers.Clear();
            hitLayers.AddRange(dispatchLayers);
        }
        hitPassOpen = false;
        IsInputGeometryReliable = false;
        Interactions.End(UiInteractionEnd.Aborted);
        if (ownedHotControl.HasValue) ReleaseHotControl(ownedHotControl.Value);
    }
    internal void EndHitPass()
    {
        hitPassOpen = false;
        IsInputGeometryReliable = true;
        if (openPopupId != null && !popupOwnerDrawn) ClosePopup();

        // An owner that stopped being drawn owns no live interaction. Release THIS session's capture and no
        // other's, and never touch a native control the session never recorded: the native slider's own drag
        // identity stays Verse's, which is why only a capture with a recorded element owner is reconciled.
        if (ownedHotControl.HasValue && ownedHotControlOwner != null && !captureOwnerDrawn && !HasPopupOptionCapture)
        {
            Interactions.End(UiInteractionEnd.Hidden);
            ReleaseHotControl(ownedHotControl.Value);
        }

        ReconcileInteractionRecords();
    }

    private bool HasPopupOptionCapture => openPopupId != null && popupCaptureTarget.HasValue
        && Interactions.CaptureOwner?.Equals(popupCaptureTarget.Value) == true;

    internal void NotePopupOptionCapture(UiInteractionTarget target, bool captured)
    {
        if (captured) popupCaptureTarget = target;
        else if (popupCaptureTarget?.Equals(target) == true) popupCaptureTarget = null;
    }

    internal void EndPopupPass()
    {
        foreach (UiNode node in new List<UiNode>(nodes.Values))
            if (node.OptionHelpOwner != null && node.OptionHelpPass != Frame)
                Widgets.OptionHelp.Release(node);
        if (ownedHotControl.HasValue && HasPopupOptionCapture && !captureOwnerDrawn)
        {
            Interactions.End(UiInteractionEnd.Hidden);
            ReleaseHotControl(ownedHotControl.Value);
        }
    }
    /// <summary>
    /// The node holding this session's open edit, or null when no field is being edited. This is the record
    /// the window's Accept and Cancel hooks act through (FL-IC2): the hooks run before the page is drawn, so
    /// they can only mark a state the funnel will read at that field's next draw.
    /// </summary>
    public UiNode? ActiveEditNode => activeEditNode;

    /// <summary>
    /// The node that last TOOK an interaction through the funnel — a button press that fired, a dropdown that
    /// opened or closed, a capture that was taken, an edit that was opened. The Cancel tree walk starts here
    /// when no consumer named a target, because the contract's starting point is the actual interaction
    /// subject, never the last thing drawn.
    /// </summary>
    public UiNode? LastInteractionNode => lastInteractionNode;

    /// <summary>
    /// The element a consumer named as the business subject a Cancel should act on, or null. Read through the
    /// node table by id, so a stale id cannot outlive the element it named.
    /// </summary>
    public UiNode? CancelTargetNode => cancelTargetNode;

    /// <summary>
    /// Names the business subject of a Cancel: the row a player selected, the panel a filter opened. The id is
    /// resolved here and now, so an unknown or not-yet-arranged id is refused by the return value instead of
    /// becoming a dangling string the ladder would later fail to find. Returns true when the target is set.
    /// </summary>
    public bool SetCancelTarget(string elementId)
    {
        EnsureActive();
        return SetCancelTarget(GetNodeByElementId(elementId));
    }

    /// <summary>
    /// The node form of the same entry, for the subject a declared id cannot reach: a composite's OWN
    /// sub-control, minted through <see cref="UiWidgetContext.Child(string)"/>, carries no element id and no
    /// arranged rect of its own, yet a consumer may well want "return from this branch" to start there. The
    /// node is accepted only when this session's table still holds it, so a released or foreign node is refused
    /// rather than becoming a subject the walk would climb from into nothing.
    /// </summary>
    public bool SetCancelTarget(UiNode? node)
    {
        EnsureActive();
        if (node == null) return false;
        if (!nodes.TryGetValue(node.Id, out UiNode? held) || !ReferenceEquals(held, node)) return false;
        cancelTargetNode = node;
        return true;
    }

    /// <summary>
    /// The state of the open edit, so the window's hooks can mark it without reaching into the widget that
    /// owns it. Null exactly when <see cref="ActiveEditNode"/> is.
    /// </summary>
    internal UiControlState? ActiveEditState => activeEditState;

    internal void NoteEditOpened(UiControlState state, UiNode node)
    {
        EnsureActive();
        if (state == null) throw new ArgumentNullException(nameof(state));
        activeEditNode = node;
        activeEditState = state;
        lastInteractionNode = node;
    }

    /// <summary>
    /// Records that an edit ended. Only the edit the session still points at can clear the pointer, so an
    /// older field finishing after a newer one opened cannot drop the newer record.
    /// </summary>
    internal void NoteEditClosed(UiControlState state)
    {
        if (state == null || !ReferenceEquals(state, activeEditState)) return;
        activeEditNode = null;
        activeEditState = null;
    }

    /// <summary>
    /// Records that this node took the interaction the current event carried. The funnel calls it where a
    /// press or an open actually took effect, so a click that was refused (disabled, covered, clipped) never
    /// becomes a Cancel starting point.
    /// </summary>
    internal void NoteInteractionTarget(UiNode node)
    {
        if (node == null) return;
        EnsureActive();
        lastInteractionNode = node;
    }

    /// <summary>
    /// Drops the business target. The pass boundary also drops it when its node stops being arranged, so a
    /// consumer that forgets this call still cannot leave the ladder pointing at a dead row.
    /// </summary>
    public void ClearCancelTarget()
    {
        EnsureActive();
        cancelTargetNode = null;
    }

    /// <summary>
    /// The third arm of the pass-boundary reconcile, next to the popup and the capture: an open edit, an
    /// interaction record and a business target all point at nodes, and a node that has left the page (hidden
    /// by a Tab or a VisibleBind, removed from the manifest, or switched away from) owns no live interaction.
    /// Leaving them would be the failure the contract names — a Cancel that acts on a subject the player can no
    /// longer see.
    /// <para>
    /// <b>Leaving the page ENDS an edit; it does not commit it.</b> Clearing the reference alone left the
    /// state's own <c>Focused</c> flag standing, so a field that came back (the same node, the same retained
    /// state, a Tab switched away and returned) still claimed to be editing while the session no longer knew
    /// which edit was open — and the window's Accept hook, which acts through that record, then refused a key
    /// the still-focused field was waiting for. The entry and the state are one fact and have to move together:
    /// the edit is ended on the state, which drops its draft back onto the model at the next draw. A value a
    /// player typed into a row they switched away from is not a commit (§3.7).
    /// </para>
    /// <para>
    /// <b>A sub-node is in play while its element is.</b> A composite's child carries no geometry by design, so
    /// reading <see cref="UiNode.IsArranged"/> alone would drop a press the player really made on a control the
    /// page really drew, and the tree walk would start nowhere. Liveness is inherited from the element that
    /// minted the child, which is the same parent-chain rule the disabled answer and the hit layer already use.
    /// </para>
    /// </summary>
    private void ReconcileInteractionRecords()
    {
        if (activeEditNode != null && (!InPlay(activeEditNode) || activeEditState == null))
        {
            UiControlState? dropped = activeEditState;
            if (dropped != null)
            {
                if (dropped.Editor is IUiEditState editor) editor.Suspend();
                Controls.ReleaseEditFocus(dropped, this);
                dropped.CommitRequested = false;
                dropped.DiscardRequested = false;
            }

            activeEditNode = null;
            activeEditState = null;
        }

        if (lastInteractionNode != null && !InPlay(lastInteractionNode)) lastInteractionNode = null;
        if (cancelTargetNode != null && !InPlay(cancelTargetNode)) cancelTargetNode = null;
    }

    /// <summary>
    /// Whether a node the interaction records point at is still on the page: an arranged element is, a
    /// composite's child is while the element that minted it is, and anything else is not.
    /// </summary>
    private static bool InPlay(UiNode node)
    {
        for (UiNode? cursor = node; cursor != null; cursor = cursor.Parent)
        {
            if (cursor.IsArranged) return true;
            if (!IsSubNode(cursor.Id)) return false;
        }

        return false;
    }

    internal void ConstrainClip(UiLayoutRect rect)
    {
        if (CurrentClip.HasValue)
        {
            UiLayoutRect clip = CurrentClip.Value;
            float x = Math.Max(clip.X, rect.X), y = Math.Max(clip.Y, rect.Y);
            rect = new UiLayoutRect(x, y, Math.Max(0f, Math.Min(clip.XMax, rect.XMax) - x),
                Math.Max(0f, Math.Min(clip.YMax, rect.YMax) - y));
        }
        CurrentClip = rect;
    }

    /// <summary>
    /// True when a window-space point is inside the clip the engine published for the element being drawn -
    /// the Host viewport, narrowed by every Scroll/Clip scope around it. A control that takes the pointer
    /// without handing the press to a native control has to answer this itself: nothing underneath it does,
    /// which is why the raw-pointer chart had to ask and a covered chart kept writing values. An unpublished
    /// clip (a frame with no Host viewport) refuses nothing.
    /// </summary>
    internal bool IsPointInEffectiveClip(UiLayoutPoint windowPoint)
    {
        UiLayoutRect? clip = CurrentClip;
        return !clip.HasValue || Contains(clip.Value, windowPoint);
    }

    /// <summary>
    /// The owner of the open popup reports that it is drawn, and where it now is in Host window space. This
    /// is the only writer of an open popup's anchor after the open itself, and that is the point: a trigger
    /// inside a scroll keeps moving while its menu is open, and an anchor stored once at open time leaves the
    /// menu behind at the position it was opened at. The caller has already computed this frame's
    /// window-space rect for its own hit test, so rendering and hit geometry read one number instead of
    /// drifting apart.
    /// </summary>
    internal void NotePopupOwnerDrawn(string ownerId, UiLayoutRect windowRect)
    {
        if (ownerId == null) throw new ArgumentNullException(nameof(ownerId));
        if (openPopupId == null || !string.Equals(openPopupId, ownerId, StringComparison.Ordinal)) return;

        if (CurrentClip.HasValue && !Intersects(windowRect, CurrentClip.Value))
        {
            ClosePopup();
            return;
        }

        openPopupAnchor = windowRect;
        popupOwnerDrawn = true;
    }

    /// <summary>True when two rects share area. Touching edges do not count, so an element exactly against
    /// the viewport border reads as outside it - the same "fits" reading <see cref="UiPopup.RectFor"/> has.
    /// </summary>
    private static bool Intersects(UiLayoutRect left, UiLayoutRect right)
    {
        return right.Width > 0f && right.Height > 0f && left.XMax > right.X && left.X < right.XMax && left.YMax > right.Y && left.Y < right.YMax;
    }

    private static bool Contains(UiLayoutRect rect, UiLayoutPoint point)
    {
        return point.X >= rect.X && point.X <= rect.XMax && point.Y >= rect.Y && point.Y <= rect.YMax;
    }

    /// <summary>Host viewport in window space, published once per frame before any content draws.</summary>
    public UiLayoutRect HostViewport => hostViewport;

    /// <summary>Id of the element the host should scroll into view on the next arranged frame, if any.</summary>
    public string? ScrollTargetElementId => scrollTargetElementId;

    /// <summary>
    /// Requests the engine to scroll the element with this declared <c>Id</c> into view. The request is
    /// consumer vocabulary - a declared Id, which the manifest keeps globally unique - and the engine
    /// resolves it to a node once per arrange, writes that scroll container's node-keyed position and
    /// clears the request, so one request moves the view exactly once.
    /// <para>
    /// An Id that no arranged element carries leaves the request pending rather than clearing it: the
    /// documented case is a target that becomes visible on a later frame (a Tab switch), and the existing
    /// lane in <c>KernelContractTests</c> pins exactly that. There is no third behaviour - a request is
    /// either consumed by a successful resolution or still pending.
    /// </para>
    /// </summary>
    public void SetScrollTarget(string elementId)
    {
        if (elementId == null) throw new ArgumentNullException(nameof(elementId));
        EnsureActive();
        scrollTargetElementId = elementId;
    }

    /// <summary>Clears a pending scroll-target request.</summary>
    public void ClearScrollTarget()
    {
        EnsureActive();
        scrollTargetElementId = null;
    }

    /// <summary>Returns true when this session owns a popup for <paramref name="ownerId"/>.</summary>
    public bool IsPopupOpen(string ownerId)
    {
        return ownerId != null
            && openPopupId != null
            && string.Equals(openPopupId, ownerId, StringComparison.Ordinal);
    }

    internal bool IsPopupOpen(string ownerId, UiInteractionTarget target)
        => IsPopupOpen(ownerId) && (!popupTarget.HasValue || popupTarget.Value.Equals(target));

    internal bool IsPopupOpen(string ownerId, UiNode node)
        => IsPopupOpen(ownerId) && (!popupTarget.HasValue
            || (popupTarget.Value.Node == node.Id.Key && popupTarget.Value.BusinessScope == node.InteractionScope));

    /// <summary>
    /// Reconciles the open menu with its actual trigger target; a replacement cannot inherit its popup.
    /// </summary>
    internal void RefreshPopupTarget(string ownerId, UiInteractionTarget target)
    {
        if (!IsPopupOpen(ownerId)) return;
        if (popupTarget.HasValue && (popupTarget.Value.Node != target.Node
            || popupTarget.Value.Part != target.Part || !Equals(popupTarget.Value.ItemKey, target.ItemKey))) return;
        if (popupTarget.HasValue && !popupTarget.Value.Equals(target))
        {
            ClosePopup();
            return;
        }
        popupTarget = target;
    }

    /// <summary>Opens or replaces this session's popup. No process-global popup state is created.</summary>
    public void OpenPopup(string ownerId, UiLayoutRect anchor)
    {
        if (ownerId == null) throw new ArgumentNullException(nameof(ownerId));
        EnsureActive();
        if (openPopupId != null) ClosePopup();
        popupTarget = null;
        popupCaptureTarget = null;
        openPopupId = ownerId;
        openPopupAnchor = anchor;
        // A menu that opens is at its top: the scroll belongs to this open, not to the element, so a second
        // open of the same dropdown never inherits where the last one happened to be parked.
        openPopupScrollRows = 0;
        openPopupMaxScrollRows = 0;
        // The owner is drawn by definition - it is reporting this open from inside its own Draw - so the
        // pass-boundary reconcile must not release the popup on the next pass for lack of a report.
        popupOwnerDrawn = true;
    }

    /// <summary>
    /// Closes the session-owned popup, if any. Its hit layers go with it: a popup that is gone must not keep
    /// blocking clicks on what it used to cover, not even for the pass that closed it.
    /// </summary>
    public void ClosePopup() => ClosePopup(releaseHelp: true);

    internal void ClosePopup(bool releaseHelp)
    {
        EnsureActive();
        UiNode? helpOwner = null;
        foreach (UiNode node in nodes.Values)
            if (node.OptionHelpPopup && node.OptionHelpOwner != null)
                helpOwner = node;
        if (HasPopupOptionCapture)
        {
            Interactions.End(UiInteractionEnd.Hidden);
            if (ownedHotControl.HasValue) ReleaseHotControl(ownedHotControl.Value);
        }
        popupTarget = null;
        popupCaptureTarget = null;
        openPopupId = null;
        openPopupAnchor = null;
        openPopupScrollRows = 0;
        openPopupMaxScrollRows = 0;
        popupOwnerDrawn = false;
        DropPopupLayers(hitLayers);
        DropPopupLayers(dispatchLayers);
        if (releaseHelp && helpOwner != null) Widgets.OptionHelp.Release(helpOwner);
    }

    private static void DropPopupLayers(List<UiHitLayer> layers)
    {
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (layers[i].IsPopup) layers.RemoveAt(i);
        }
    }

    /// <summary>Publishes the frame's Host viewport so popups can clamp themselves into it.</summary>
    internal void SetHostViewport(UiLayoutRect viewport)
    {
        EnsureActive();
        hostViewport = viewport;
    }

    public void BeginFrame()
    {
        EnsureActive();
        Frame++;
        BeginHelpPass();
        // Per-frame transient flags are reset by each control as it reads/writes its own state;
        // no global reset is needed because state is session-owned.
    }

    public void EndFrame()
    {
        EnsureActive();
        // Popup draw callbacks are consumed by the Host after the content pass.
        popupDrawActions.Clear();
    }

    /// <summary>
    /// Moves the arrangement-cache clock one step. The host commits a layout-bearing theme change with it,
    /// and the engine commits a batch of <see cref="UiInvalidation.Measure"/> /
    /// <see cref="UiInvalidation.Structure"/> announcements with it - once per batch, not once per key.
    /// </summary>
    public void BumpContentRevision()
    {
        EnsureActive();
        ContentRevision++;
    }
internal UiControlState GetControlState(UiNodeId element, string stateKey)
    {
        if (stateKey == null) throw new ArgumentNullException(nameof(stateKey));
        EnsureActive();
        return GetOrCreateNode(element, "", -1).GetOrCreateState(stateKey);
    }

    /// <summary>
    /// The state slots one element owns, keyed by the widget's state names, or an empty map when the
    /// element holds none. The read is by identity because that is the primary key; the old flat
    /// property could not name both dimensions.
    /// </summary>
    internal IReadOnlyDictionary<string, UiControlState> GetValueStates(UiNodeId element)
    {
        return nodes.TryGetValue(element, out UiNode? node) ? node.ValueStates : NoValueStates;
    }

    /// <summary>Finds an already owned runtime node by its canonical observation key; never creates it.</summary>
    internal UiNode? FindObservationNode(string key)
    {
        foreach (var pair in nodes)
            if (pair.Key.Key == key) return pair.Value;
        return null;
    }

    /// <summary>
    /// The node an identity names, or null when this session has never arranged such an element.
    /// Read-only: element nodes are created by the engine during arrange.
    /// </summary>
    public UiNode? GetNode(UiNodeId element)
    {
        return nodes.TryGetValue(element, out UiNode? node) ? node : null;
    }

    /// <summary>
    /// The node of the element a declared <c>Id</c> names, or null when this session holds no such
    /// identity. This is the bridge a caller uses to move from the one string a page owns to the node
    /// identity everything else keys on; it is a lookup, not a second key space.
    /// <para>
    /// The answer is about the identity, not about the current arrangement, and the difference is
    /// load-bearing. A declared element hidden by <c>Visible</c>/<c>VisibleBind</c>/<c>Tab</c> is not
    /// arranged but still exists in the definition and keeps its node and state, so this finds it - that
    /// is what keeps a hidden element's draft or scroll position reachable. A null answer means the
    /// identity is <b>gone from the session</b>: the current definition no longer declares it and
    /// <see cref="PruneNodesExcept"/> released it. The earlier wording said "no arranged element carries
    /// it", which was false for exactly the hidden case and read as false for the removed case until the
    /// prune existed.
    /// </para>
    /// </summary>
    public UiNode? GetNodeByElementId(string elementId)
    {
        if (string.IsNullOrEmpty(elementId)) return null;
        foreach (UiNode node in nodes.Values)
        {
            if (string.Equals(node.ElementId, elementId, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// The node a widget minted for one of its own sub-controls, created on first use under
    /// <paramref name="parent"/> and re-linked on every call. The node - and therefore its state - is
    /// created once per identity and reused, which is what lets a control inside a widget outlive a
    /// widget instance, and it carries no arranged geometry: sub-controls are the widget's own business,
    /// not tree elements.
    /// </summary>
    internal UiNode GetOrCreateSubNode(UiNode parent, string name)
    {
        EnsureActive();
        if (parent == null) throw new ArgumentNullException(nameof(parent));

        UiNodeId id = parent.Id.SubNode(name);
        if (!nodes.TryGetValue(id, out UiNode? node))
        {
            node = new UiNode(this, id, "", -1, needsMeasure: false);
            nodes.Add(id, node);
        }

        node.SetUpdateScope(parent.UpdateScopeRoot);
        parent.AddChild(node);
        return node;
    }

    /// <summary>
    /// Drops every node whose identity the current definition no longer declares - the release half of a
    /// structural change. Without it a reload that removes an element (or changes its kind under what used
    /// to be a stable Id) leaves the old <see cref="UiNode"/> in this table with a stale
    /// <see cref="UiNode.Kind"/> and orphaned state: this table owns node lifetime, so this is the only
    /// place that can end it.
    /// <para>
    /// A declared but hidden element keeps its node on purpose - it is skipped by the arrange, but its
    /// identity is still part of the definition and its state must survive the hide. Only an identity the
    /// definition no longer contains is released, and everything the node owned goes with it: its
    /// sub-nodes, its state slots, its scroll position, its dirty and recovery records and any hit layer
    /// it still occupies, so nothing points at an identity that is gone.
    /// </para>
    /// <para>
    /// <paramref name="declared"/> is the identity set of the definition being arranged (every declared
    /// element, visible or not), which the engine walks from the roots. The cost is one pass over this
    /// table per successful arrange, next to a layout pass that already touches every element.
    /// </para>
    /// </summary>
    /// <returns>How many nodes were released.</returns>
    internal int PruneNodesExcept(HashSet<UiNodeId> declared)
    {
        EnsureActive();
        if (declared == null) throw new ArgumentNullException(nameof(declared));

        var doomed = new List<UiNode>();
        foreach (UiNode node in nodes.Values)
        {
            if (ReferenceEquals(node, unscopedNode)) continue;

            if (!IsSubNode(node.Id))
            {
                // An element identity (an arranged element, or a node a caller created state for). It goes
                // when the definition does not declare it - a hidden element is still declared, so this is
                // "removed", not "not arranged".
                if (!declared.Contains(node.Id)) doomed.Add(node);
                continue;
            }

            // A widget's sub-node goes with the element that owns it: an element identity the definition
            // no longer declares, or an owner that an earlier prune already released.
            if (!OwnerSurvives(node, declared)) doomed.Add(node);
        }

        if (doomed.Count == 0) return 0;

        var released = new HashSet<UiNode>();
        foreach (UiNode node in doomed)
        {
            released.Add(node);
            SuspendNodeEditors(node);
            node.ReleaseState();
            nodes.Remove(node.Id);
            dirtyNodes.Remove(node);
            trippedNodes.Remove(node);
            trippedLogs.Remove(node);
            scrollPositions.Remove(node);
            if (ReferenceEquals(activeNode, node)) activeNode = unscopedNode;
        }

        // A released identity cannot hold a pointer capture any more: that is "the owner is no longer valid",
        // and the release goes through the same owner-scoped door as a normal release, so another session's
        // capture and another window's GUIUtility state stay exactly as they were.
        if (ownedHotControl.HasValue && ownedHotControlOwner != null && released.Contains(ownedHotControlOwner))
        {
            Interactions.End(UiInteractionEnd.Removed);
            ReleaseHotControl(ownedHotControl.Value);
        }

        DropLayersOf(released, hitLayers);
        DropLayersOf(released, dispatchLayers);
        return released.Count;
    }

    /// <summary>True when the identity was minted by <see cref="UiNodeId.SubNode"/> for a widget's own control.</summary>
    private static bool IsSubNode(UiNodeId id)
    {
        return id.Key.IndexOf(UiNodeId.SubNodeMarker) >= 0;
    }

    /// <summary>
    /// True when a sub-node's owning element is still part of the definition: the walk climbs past any
    /// intermediate sub-nodes to the first element identity, and that one has to be declared.
    /// </summary>
    private static bool OwnerSurvives(UiNode node, HashSet<UiNodeId> declared)
    {
        for (UiNode? cursor = node.Parent; cursor != null; cursor = cursor.Parent)
        {
            if (IsSubNode(cursor.Id)) continue;
            return declared.Contains(cursor.Id);
        }

        return false;
    }

    private static void DropLayersOf(HashSet<UiNode> released, List<UiHitLayer> layers)
    {
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (released.Contains(layers[i].Element)) layers.RemoveAt(i);
        }
    }

    // Capture derived arrangement facts; recovery never restores business state or ended capture.
    internal Action CaptureArrangement()
    {
        var priorNodes = new Dictionary<UiNodeId, UiNode>(nodes);
        var restoreNodes = new List<Action>();
        foreach (UiNode node in nodes.Values)
            restoreNodes.Add(node.CaptureArrangement());
        return () =>
        {
            if (!IsActive)
                return;
            foreach(var entry in nodes)if(!priorNodes.ContainsKey(entry.Key))entry.Value.ReleaseState();
            nodes.Clear();
            foreach (KeyValuePair<UiNodeId, UiNode> entry in priorNodes)
                nodes.Add(entry.Key, entry.Value);
            dirtyNodes.RemoveWhere(node => !priorNodes.TryGetValue(node.Id, out UiNode? prior) || !ReferenceEquals(node, prior));
            foreach (Action restore in restoreNodes)
                restore();
            AbortHitPass();
        };
    }

    /// <summary>
    /// Opens a fresh arrange: every node forgets its children and its geometry, and the arrange that
    /// follows publishes them again for the elements it visits. That is what makes "not arranged in this
    /// tree" a state a caller can read (<see cref="UiNode.IsArranged"/>) instead of a stale rect, and it
    /// keeps a Tab-hidden element's node and state alive while its rect is gone.
    /// </summary>
    internal void BeginArrange()
    {
        EnsureActive();
        foreach (UiNode node in nodes.Values)
        {
            node.ClearElementChildren();
            node.ClearGeometry();
        }
    }

    /// <summary>
    /// The node an element's identity names, created on first use. The engine calls this for every
    /// arranged entry, so a re-arrange reuses the node - and therefore its state - instead of replacing
    /// it, which is what makes identity survive a frame.
    /// </summary>
    /// <summary>
    /// The node an arranged element owns: identity-only reuse, plus the definition-owned facts refreshed.
    /// A reload can keep an identity and change the kind under it, and the node has to move with it - a
    /// node that still reported the old kind is the stale diagnostic the document service cannot fix
    /// because it does not own this table.
    /// </summary>
    internal UiNode GetOrCreateElementNode(UiNodeId element, UiElementSpec spec, int ordinal)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        UiNode node = GetOrCreateNode(element, spec.Kind, ordinal, spec.Id);
        node.RefreshIdentity(spec.Kind, ordinal, spec.Id);
        return node;
    }

    internal UiNode GetOrCreateNode(UiNodeId element, string kind, int ordinal, string elementId = "")
    {
        EnsureActive();
        if (nodes.TryGetValue(element, out UiNode? node))
        {
            return node;
        }

        node = new UiNode(this, element, kind, ordinal, needsMeasure: true, elementId: elementId);
        nodes.Add(element, node);
        dirtyNodes.Add(node);
        return node;
    }

    /// <summary>True when at least one live node still owes a Measure.</summary>
    internal bool HasDirtyNodes => dirtyNodes.Count > 0;

    /// <summary>
    /// Clears every dirty flag. The engine calls this once a whole arrange has succeeded, which is what
    /// makes <see cref="UiNode.MarkDirty"/> mean "recompute me on the next pass".
    /// </summary>
    internal void ClearDirtyNodes()
    {
        foreach (UiNode node in dirtyNodes)
        {
            node.ClearDirtyFlag();
        }

        dirtyNodes.Clear();
    }

    /// <summary>Records that a node asked for a re-measure. Called by <see cref="UiNode.MarkDirty"/>.</summary>
    internal void NodeMarkedDirty(UiNode node)
    {
        dirtyNodes.Add(node);
    }

    /// <summary>
    /// Enters <paramref name="node"/> for the duration of its own Measure/Draw and returns the previous
    /// active node, so the engine can hand it back to <see cref="ExitNode"/> in a <c>finally</c>.
    /// Nesting is a stack discipline: the engine draws one element at a time.
    /// </summary>
    internal UiNode EnterNode(UiNode node)
    {
        EnsureActive();
        UiNode previous = activeNode;
        activeNode = node ?? throw new ArgumentNullException(nameof(node));
        return previous;
    }

    /// <summary>Restores the node returned by <see cref="EnterNode"/>.</summary>
    internal void ExitNode(UiNode previous)
    {
        activeNode = IsActive ? previous : unscopedNode;
    }

    /// <summary>The scroll position one scroll container's node holds; zero when it holds none.</summary>
    public UiLayoutPoint GetScrollPosition(UiNode node)
    {
        return node != null && scrollPositions.TryGetValue(node, out UiLayoutPoint pos) ? pos : UiLayoutPoint.Zero;
    }

    /// <summary>Writes one scroll container's position, keyed by its node.</summary>
    public void SetScrollPosition(UiNode node, UiLayoutPoint position)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        EnsureActive();
        scrollPositions[node] = position;
    }

    /// <summary>
    /// Runs the hover-claim frame boundary: a claim refreshed by <see cref="PublishHelp"/> during the
    /// previous pass is held and cleared (widgets re-claim as they draw, so a stationary pointer never
    /// reads as stale); with no fresh claim the held one is restored for <see cref="HelpGracePasses"/>
    /// more passes, which is what stops the pointer crossing a gap between two controls from flashing
    /// the overview. A live claim always replaces the held one in the same pass, so nothing pins.
    /// Called by <see cref="BeginFrame"/>; exposed for hosts that drive passes by hand.
    /// </summary>
    public void BeginHelpPass()
    {
        EnsureActive();
        bool claimedLastFrame = hoverClaim.Length > 0 && hoverClaimStamp == Frame - 1;
        if (claimedLastFrame)
        {
            hoverHeld = hoverClaim;
            hoverHeldElement = hoverClaimElement;
            hoverGraceLeft = HelpGracePasses;
            hoverClaim = "";
            hoverClaimElement = default;
        }
        else if (hoverGraceLeft > 0)
        {
            hoverClaim = hoverHeld;
            hoverClaimElement = hoverHeldElement;
            hoverGraceLeft--;
        }
        else
        {
            hoverClaim = "";
            hoverClaimElement = default;
        }
    }

    /// <summary>
    /// Publishes an opaque <paramref name="helpRef"/> for this pass. Frame-stamped, so
    /// <see cref="BeginHelpPass"/> can tell a claim made this pass from one it restored itself —
    /// the distinction a consumer had to hand-roll before this existed.
    /// <para>
    /// The parameter is a consumer topic reference, not text, a translation key, binding or element id.
    /// The caller supplies its own catalog reference, and this
    /// library never interprets it. The element that made the claim is recorded separately as
    /// <see cref="CurrentHelpSource"/>, which is what tells two unnamed siblings apart when they claim the same
    /// token.
    /// </para>
    /// </summary>
    public void PublishHelp(string helpRef) => PublishHelp(activeNode, helpRef);

    internal void PublishHelp(UiNode source, string helpRef)
    {
        if (helpRef == null) throw new ArgumentNullException(nameof(helpRef));
        EnsureActive();
        if (!ReferenceEquals(source.Owner, this)) throw new ArgumentException("Help source belongs to another Session.", nameof(source));
        hoverClaim = helpRef;
        hoverClaimElement = source.Id;
        hoverClaimStamp = Frame;
    }

    /// <summary>True when this node's element is in session fallback.</summary>
    public bool IsTripped(UiNode node)
    {
        return node != null && trippedNodes.Contains(node);
    }

    /// <summary>
    /// Records one node's fallback and its diagnostic. The first trip of a node keeps its log; a retry
    /// does not replace it, which is what makes the guard log once per session slot.
    /// </summary>
    public void Trip(UiNode node, string diagnostic)
    {
        if (node == null) return;
        EnsureActive();
        if (trippedNodes.Add(node))
        {
            trippedLogs[node] = diagnostic;
        }
    }

    /// <summary>The diagnostic the first trip of this node recorded, if it ever tripped.</summary>
    public bool TryGetTripLog(UiNode node, out string diagnostic)
    {
        if (node == null)
        {
            diagnostic = null!;
            return false;
        }

        return trippedLogs.TryGetValue(node, out diagnostic!);
    }
    /// <summary>Registers one session-owned popup draw callback for the current Host frame.</summary>
    public void RegisterPopupDraw(Action draw)
    {
        if (draw == null) throw new ArgumentNullException(nameof(draw));
        EnsureActive();
        popupDrawActions.Add(draw);
    }


    /// <summary>Native hot control id currently captured by this session, if any.</summary>
    public UiControlKey? OwnedHotControl => ownedHotControl;

    /// <summary>
    /// The element that holds <see cref="OwnedHotControl"/>, or null when the capture was taken outside any
    /// element draw. Capture identity is a session, a control id AND a node: "the owner is no longer valid"
    /// has to be answerable, and an int alone cannot say whose it was. A capture with no recorded element is
    /// deliberately NOT reconciled at the pass boundary - nothing owns its draw - and is still released by
    /// <see cref="Dispose"/>, which is the close path for everything.
    /// </summary>
    public UiNode? OwnedHotControlOwner => ownedHotControlOwner;

    /// <summary>True when <paramref name="controlId"/> is the hot control this session owns.</summary>
    public bool IsHotControlOwned(UiControlKey controlId)
    {
        return ownedHotControl.HasValue && ownedHotControl.Value == controlId;
    }

    internal void CaptureHotControlForNode(UiControlKey controlId, UiNode owner)
    {
        EnsureActive();
        ownedHotControl = controlId;
        ownedHotControlOwner = ReferenceEquals(owner, unscopedNode) ? null : owner;
        // Taking the capture is itself a report that the owner is drawing right now.
        captureOwnerDrawn = true;
        Execution.Capture(controlId);
    }

    /// <summary>
    /// The capture half of <see cref="NotePopupOwnerDrawn"/>: a raw-pointer control reports that it drew while
    /// it owns the capture, so the pass-boundary reconcile can tell "this owner is still on screen and still
    /// holds the press" from "this owner stopped being drawn and nobody else can release what it took".
    /// A no-op unless this session owns <paramref name="controlId"/>.
    /// </summary>
    internal void NoteHotControlOwnerDrawn(UiControlKey controlId)
    {
        if (IsHotControlOwned(controlId)) captureOwnerDrawn = true;
    }

    /// <summary>
    /// Releases the native hot control, but only when this session owns it. No-ops otherwise so a
    /// closing host cannot release a capture owned by another session.
    /// </summary>
    public void ReleaseHotControl(UiControlKey controlId)
    {
        EnsureActive();
        if (ownedHotControl.HasValue && ownedHotControl.Value == controlId)
        {
            Interactions.End(UiInteractionEnd.CaptureLost);
            Execution.Release(controlId);
            ownedHotControl = null;
            ownedHotControlOwner = null;
        }
    }

    private static void SuspendNodeEditors(UiNode node)
    {
        foreach (UiControlState state in node.ValueStates.Values)
        {
            if (state.Editor is IUiEditState editor) editor.Suspend();
            state.CommitRequested = state.DiscardRequested = false;
        }
        node.EditingState = null;
    }
    internal void SuspendFailedInput(UiNode node)
    {
        SuspendNodeEditors(node);
        if (ReferenceEquals(activeEditNode,node))
        {
            if(activeEditState!=null) Controls.ReleaseEditFocus(activeEditState,this);
            activeEditNode=null;activeEditState=null;
        }
        if(Interactions.CaptureOwner?.Node==node.Id.Key) Interactions.End(UiInteractionEnd.Aborted);
        if(ReferenceEquals(ownedHotControlOwner,node) && ownedHotControl.HasValue) ReleaseHotControl(ownedHotControl.Value);
        if(popupTarget?.Node==node.Id.Key) ClosePopup();
    }
    internal void SuspendDisplayInput()
    {
        if(activeEditState!=null) Controls.ReleaseEditFocus(activeEditState,this);
        foreach(UiNode node in nodes.Values) SuspendNodeEditors(node);
        activeEditNode=null;activeEditState=null;
        ClosePopup();
        ReleaseOptionHelpOutputs();
    }

    internal void ReleaseOptionHelpOutputs()
    {
        List<Exception>? errors = null;
        foreach (UiNode node in new List<UiNode>(nodes.Values))
            try { Widgets.OptionHelp.Release(node); }
            catch (Exception error) when (error is not OutOfMemoryException) { (errors ??= new()).Add(error); }
        if (errors != null) throw new AggregateException("Option help outputs released; one or more writes failed.", errors);
    }

    public void Dispose()
    {
        if (!IsActive) return;
        IsActive = false;
        var errors = new List<Exception>();
        void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is not OutOfMemoryException) { errors.Add(error); }
        }
        // Close logically before calling external/native cleanup; even a throwing release must not
        // leave a reusable owner or skip the other owned resources. Dispose never commits a draft.
        Cleanup(Interactions.Dispose);
        foreach (UiNode node in nodes.Values)
            foreach (UiControlState state in node.ValueStates.Values)
                Cleanup(() => Controls.ReleaseEditFocus(state, this));

        // The session is the subscription's owner: disposing it releases the subscription, its bounded
        // buffers and its registry entry, and never a sibling session's. This is the torn-down-window
        // half of the isolation contract.
        Cleanup(() => UiDiagnosticHub.ReleaseSession(this));

        if (ownedHotControl.HasValue)
        {
            // Dispose is the close path: release only this session's capture, then clear the
            // ownership record together with popup/focus/drag transient state.
            UiControlKey captured = ownedHotControl.Value;
            Cleanup(() => Execution.Release(captured));
            ownedHotControl = null;
            ownedHotControlOwner = null;
        }

        scrollPositions.Clear();
        foreach (UiNode node in nodes.Values) Cleanup(() => {SuspendNodeEditors(node);node.ReleaseState();});
        nodes.Clear();
        dirtyNodes.Clear();
        trippedNodes.Clear();
        activeNode = unscopedNode;
        // The ladder's three records name nodes, and the node table above is gone with them: a torn-down
        // session must not leave a Cancel pointing at an element that no longer exists anywhere (§3.7).
        activeEditNode = null;
        activeEditState = null;
        lastInteractionNode = null;
        cancelTargetNode = null;
        hoverClaim = "";
        hoverClaimElement = default;
        hoverHeld = "";
        hoverHeldElement = default;
        hoverClaimStamp = 0;
        hoverGraceLeft = 0;
        trippedLogs.Clear();
        popupDrawActions.Clear();
        popupTarget = null;
        popupCaptureTarget = null;
        openPopupId = null;
        openPopupAnchor = null;
        openPopupScrollRows = 0;
        openPopupMaxScrollRows = 0;
        captureOwnerDrawn = false;
        hitPassOpen = false;
        hitLayers.Clear();
        dispatchLayers.Clear();
        contentInputLayers.Clear();
        currentWindowOrigin = default;
        scrollTargetElementId = null;
        execution = null;
        Input = new UiFrame(UiFramePhase.None, inputAllowed: false);
        PointerInputAllowed = false;
        if (errors.Count != 0) throw new AggregateException("Session closed; one or more owned resource releases failed.", errors);
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("UiSession is disposed; create a new host/session.");
        }
    }
}
