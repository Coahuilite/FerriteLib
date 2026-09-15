using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Per-Host transient interaction state. One Settings Window / Overlay owns exactly one
/// <see cref="UiSession"/>; closing the host disposes the session and all transient state is lost.
/// This type intentionally replaces the old process-wide <c>UiValueStore</c>/<c>UiPageState</c>.
/// </summary>
public sealed class UiSession : IDisposable
{
    private static readonly IReadOnlyDictionary<string, UiValueState> NoValueStates =
        new Dictionary<string, UiValueState>(StringComparer.Ordinal);

    // Scroll state is keyed by the scroll container's node, not by a display path: two scroll elements
    // whose display paths collide still hold two scroll positions (0.4.0 node step 2).
    private readonly Dictionary<UiNode, Vector2> scrollPositions = new();

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
    private readonly List<UiHitLayer> dispatchLayers = new();
    private Vector2 currentWindowOrigin;

    // Recovery slots are keyed by the node that tripped, so two elements that print the same display
    // path cannot share one fallback slot (0.4.0 node step 2).
    private readonly HashSet<UiNode> trippedNodes = new();
    private readonly Dictionary<UiNode, string> trippedLogs = new();
    private int? ownedHotControl;
    private readonly List<Action> popupDrawActions = new();
    private string? openPopupId;
    private Rect? openPopupAnchor;
    private Rect hostViewport;
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
    /// call, a popup pass) resolves into, which keeps <see cref="GetOrCreateValueState(string)"/> total
    /// outside a tree exactly as it was before the node step.
    /// </summary>
    public UiSession()
    {
        unscopedNode = new UiNode(this, UiNodeId.None, "", -1);
        nodes.Add(UiNodeId.None, unscopedNode);
        activeNode = unscopedNode;
    }

    /// <summary>True until <see cref="Dispose"/> is called.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// How many further passes a hover claim keeps explaining the panel after the pointer leaves, in
    /// <see cref="Frame"/> units. Part of the claim machine's contract rather than a library default:
    /// the consumer that needed the grace picks its length, the library only owns the rule.
    /// </summary>
    public int HoverGraceFrames { get; set; }

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
    /// per key through <see cref="IUiBindings.NotifyChanged"/>, and a <see cref="UiInvalidation.Paint"/>
    /// announcement leaves this clock alone on purpose: a fixed-size readout must not re-arrange the page.
    /// There is exactly one such clock; do not add a second.
    /// </para>
    /// </summary>
    public int ContentRevision { get; private set; }

    /// <summary>
    /// The id a hover claim is currently explaining, or null when nothing is claimed. Read it after
    /// <see cref="BeginFrame"/>; widgets refresh it during the pass with <see cref="ClaimHover"/>.
    /// </summary>
    public string? HoverClaim => hoverClaim.Length > 0 ? hoverClaim : null;

    /// <summary>
    /// The element that made the claim <see cref="HoverClaim"/> is presenting, or null when nothing is
    /// claimed. The claim string stays the consumer's vocabulary (a section key it compares against),
    /// so two unnamed siblings claiming the same string are still indistinguishable through
    /// <see cref="HoverClaim"/> - this is the axis that tells them apart, and the grace machine carries
    /// it together with the string so a held claim is attributed to the element that made it.
    /// <see cref="UiNodeId.None"/> means the claim was made outside any element (a host-level caller).
    /// </summary>
    public UiNodeId? HoverClaimElement => hoverClaim.Length > 0 ? hoverClaimElement : null;

    /// <summary>
    /// The element whose Measure/Draw is running, or <see cref="UiNodeId.None"/> between elements.
    /// The engine enters an element's node around its own widget calls, and
    /// <see cref="GetOrCreateValueState(string)"/> resolves against it: that is how a widget written
    /// against the old bare-string key gets a per-element state namespace without changing a line.
    /// </summary>
    public UiNodeId ActiveElement => activeNode.Id;

    /// <summary>
    /// The node whose Measure/Draw is running - the session-level node between elements. This is the
    /// object a widget reads its own state from and marks dirty; the engine enters it in a
    /// <c>finally</c> around every element's Measure and Draw.
    /// </summary>
    public UiNode ActiveNode => activeNode;

    /// <summary>Session-scoped scroll positions, keyed by the scroll container's node.</summary>
    public IReadOnlyDictionary<UiNode, Vector2> ScrollPositions => scrollPositions;

    /// <summary>
    /// Nodes currently tripped into session fallback. Keyed by node, so the display path a diagnostic
    /// prints plays no part in which element owns a recovery slot.
    /// </summary>
    public IReadOnlyCollection<UiNode> TrippedNodes => trippedNodes;

    /// <summary>Session-scoped popup draw callbacks. Not process-global.</summary>
    public IReadOnlyList<Action> PopupDrawActions => popupDrawActions;

    /// <summary>Id of the popup currently owned by this session, if any.</summary>
    public string? OpenPopupId => openPopupId;

    /// <summary>Anchor rect of the popup currently owned by this session, if any.</summary>
    public Rect? OpenPopupAnchor => openPopupAnchor;

    /// <summary>
    /// The owned hit stack in paint order, bottom-to-top, as built by the draw pass that is running or just
    /// finished: content layers as their elements draw, then the popup layer. Popups are the entries with
    /// <see cref="UiHitLayer.IsPopup"/>, and a diagnostic or a lane reads this instead of a single
    /// "covered rect".
    /// </summary>
    public IReadOnlyList<UiHitLayer> HitLayers => hitLayers;

    /// <summary>
    /// The window-space origin of the element being drawn. The funnel lifts a draw-local pointer by it,
    /// which is how a primitive compares its pointer with the window-space hit layers.
    /// </summary>
    internal Vector2 CurrentWindowOrigin => currentWindowOrigin;

    internal void SetCurrentWindowOrigin(Vector2 origin)
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
        dispatchLayers.Clear();
        dispatchLayers.AddRange(hitLayers);
        hitLayers.Clear();
    }

    /// <summary>
    /// Appends one layer to the pass in progress. Content layers are appended as their elements draw, so the
    /// stack ends up in paint order; a popup appends after content and is therefore above it.
    /// </summary>
    internal void PushHitLayer(UiNode element, Rect windowRect, bool isPopup)
    {
        if (element == null) return;
        hitLayers.Add(new UiHitLayer(element, windowRect, isPopup));
    }

    /// <summary>
    /// Topmost-first dispatch: true when <paramref name="windowPoint"/> falls inside a popup layer that
    /// belongs to another element, so the caller must not take the click. The topmost covering popup wins,
    /// and one belonging to the caller (its own trigger's popup) keeps the click here, which is what
    /// preserves toggle-to-close.
    /// <para>
    /// Content layers are recorded in the stack in paint order but do not arbitrate one another yet: IMGUI
    /// already serialises content input by draw order, and a rect lookup cannot tell a real pointer from an
    /// injected one. The overlay decision that matters today - and the one the old per-element yield branch
    /// got wrong for every primitive outside its funnel - is popup-over-content.
    /// </para>
    /// </summary>
    public bool IsPointerOverHigherLayer(UiNode element, Vector2 windowPoint)
    {
        for (int i = dispatchLayers.Count - 1; i >= 0; i--)
        {
            UiHitLayer layer = dispatchLayers[i];
            if (!layer.IsPopup) continue;
            if (!Contains(layer.Rect, windowPoint)) continue;
            return !ReferenceEquals(layer.Element, element);
        }

        return false;
    }

    private static bool Contains(Rect rect, Vector2 point)
    {
        return point.x >= rect.x && point.x <= rect.xMax && point.y >= rect.y && point.y <= rect.yMax;
    }

    /// <summary>Host viewport in window space, published once per frame before any content draws.</summary>
    public Rect HostViewport => hostViewport;

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

    /// <summary>
    /// Opens (or replaces) the session-owned popup for <paramref name="ownerId"/>.
    /// This is per-session state, not a process-global popup queue.
    /// </summary>
    public void OpenPopup(string ownerId, Rect anchor)
    {
        if (ownerId == null) throw new ArgumentNullException(nameof(ownerId));
        EnsureActive();
        openPopupId = ownerId;
        openPopupAnchor = anchor;
    }

    /// <summary>
    /// Closes the session-owned popup, if any. Its hit layers go with it: a popup that is gone must not keep
    /// blocking clicks on what it used to cover, not even for the pass that closed it.
    /// </summary>
    public void ClosePopup()
    {
        EnsureActive();
        openPopupId = null;
        openPopupAnchor = null;
        DropPopupLayers(hitLayers);
        DropPopupLayers(dispatchLayers);
    }

    private static void DropPopupLayers(List<UiHitLayer> layers)
    {
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (layers[i].IsPopup) layers.RemoveAt(i);
        }
    }

    /// <summary>Publishes the frame's Host viewport so popups can clamp themselves into it.</summary>
    internal void SetHostViewport(Rect viewport)
    {
        EnsureActive();
        hostViewport = viewport;
    }

    public void BeginFrame()
    {
        EnsureActive();
        Frame++;
        BeginHoverClaimFrame();
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

    /// <summary>
    /// The value state for <paramref name="stateKey"/> inside the element currently being arranged or
    /// drawn (<see cref="ActiveElement"/>). This is what the backend funnels and every existing widget
    /// call; resolving it against the element is what keeps two unnamed same-kind siblings from sharing
    /// one slot the way the bare key did.
    /// <para>
    /// That element scope lasts for one element's Measure or Draw inside one arrange/draw traversal and
    /// is restored in a <c>finally</c>, so it is never observable between passes. Called outside such a
    /// traversal - a host-level call, a popup pass, a harness driving a widget by hand - the overload
    /// resolves against <see cref="UiNodeId.None"/> rather than throwing: that is the session-level
    /// namespace, which is what an element-less caller had before the identity layer and what keeps the
    /// old call shape working for code that never was inside a tree.
    /// </para>
    /// </summary>
    public UiValueState GetOrCreateValueState(string stateKey)
    {
        if (stateKey == null) throw new ArgumentNullException(nameof(stateKey));
        EnsureActive();
        return activeNode.GetOrCreateState(stateKey);
    }

    /// <summary>The element's own value state, i.e. the slot it owns without naming a sub-key.</summary>
    public UiValueState GetOrCreateValueState(UiNodeId element)
    {
        return GetOrCreateValueState(element, "");
    }

    /// <summary>
    /// The value state for one named slot of one element. A composite that drives several stateful
    /// controls inside a single element (the consumer's interval row drives a slider and a number field)
    /// keeps them apart by name; two elements never share a slot whatever names they use.
    /// </summary>
    public UiValueState GetOrCreateValueState(UiNodeId element, string stateKey)
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
    public IReadOnlyDictionary<string, UiValueState> GetValueStates(UiNodeId element)
    {
        return nodes.TryGetValue(element, out UiNode? node) ? node.ValueStates : NoValueStates;
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
    /// load-bearing. A declared element hidden by <c>Visible</c>/<c>VisibleKey</c>/<c>Tab</c> is not
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
            nodes.Remove(node.Id);
            dirtyNodes.Remove(node);
            trippedNodes.Remove(node);
            trippedLogs.Remove(node);
            scrollPositions.Remove(node);
            if (ReferenceEquals(activeNode, node)) activeNode = unscopedNode;
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
            node.ClearChildren();
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
        activeNode = previous;
    }

    /// <summary>The scroll position one scroll container's node holds; zero when it holds none.</summary>
    public Vector2 GetScrollPosition(UiNode node)
    {
        return node != null && scrollPositions.TryGetValue(node, out Vector2 pos) ? pos : Vector2.zero;
    }

    /// <summary>Writes one scroll container's position, keyed by its node.</summary>
    public void SetScrollPosition(UiNode node, Vector2 position)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        EnsureActive();
        scrollPositions[node] = position;
    }

    /// <summary>
    /// Runs the hover-claim frame boundary: a claim refreshed by <see cref="ClaimHover"/> during the
    /// previous pass is held and cleared (widgets re-claim as they draw, so a stationary pointer never
    /// reads as stale); with no fresh claim the held one is restored for <see cref="HoverGraceFrames"/>
    /// more passes, which is what stops the pointer crossing a gap between two controls from flashing
    /// the overview. A live claim always replaces the held one in the same pass, so nothing pins.
    /// Called by <see cref="BeginFrame"/>; exposed for hosts that drive passes by hand.
    /// </summary>
    public void BeginHoverClaimFrame()
    {
        EnsureActive();
        bool claimedLastFrame = hoverClaim.Length > 0 && hoverClaimStamp == Frame - 1;
        if (claimedLastFrame)
        {
            hoverHeld = hoverClaim;
            hoverHeldElement = hoverClaimElement;
            hoverGraceLeft = HoverGraceFrames;
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
    /// Claims the hover-help surface for <paramref name="elementId"/> for this pass. Frame-stamped, so
    /// <see cref="BeginHoverClaimFrame"/> can tell a claim made this pass from one it restored itself —
    /// the distinction a consumer had to hand-roll before this existed.
    /// </summary>
    public void ClaimHover(string elementId)
    {
        if (elementId == null) throw new ArgumentNullException(nameof(elementId));
        EnsureActive();
        hoverClaim = elementId;
        hoverClaimElement = activeNode.Id;
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
    public int? OwnedHotControl => ownedHotControl;

    /// <summary>True when <paramref name="controlId"/> is the hot control this session owns.</summary>
    public bool IsHotControlOwned(int controlId)
    {
        return ownedHotControl.HasValue && ownedHotControl.Value == controlId;
    }

    /// <summary>
    /// Captures the native hot control for <paramref name="controlId"/> and records session
    /// ownership. Only the owning session may release it; closing/disposing the host releases
    /// exactly this session's capture and never another session's.
    /// </summary>
    public void CaptureHotControl(int controlId)
    {
        EnsureActive();
        ownedHotControl = controlId;
        UiNative.CaptureHotControl(controlId);
    }

    /// <summary>
    /// Releases the native hot control, but only when this session owns it. No-ops otherwise so a
    /// closing host cannot release a capture owned by another session.
    /// </summary>
    public void ReleaseHotControl(int controlId)
    {
        EnsureActive();
        if (ownedHotControl.HasValue && ownedHotControl.Value == controlId)
        {
            UiNative.ReleaseHotControl(controlId);
            ownedHotControl = null;
        }
    }

    public void Dispose()
    {
        if (!IsActive) return;
        IsActive = false;
        if (ownedHotControl.HasValue)
        {
            // Dispose is the close path: release only this session's capture, then clear the
            // ownership record together with popup/focus/drag transient state.
            UiNative.ReleaseHotControl(ownedHotControl.Value);
            ownedHotControl = null;
        }

        scrollPositions.Clear();
        nodes.Clear();
        dirtyNodes.Clear();
        trippedNodes.Clear();
        activeNode = unscopedNode;
        hoverClaim = "";
        hoverClaimElement = default;
        hoverHeld = "";
        hoverHeldElement = default;
        hoverClaimStamp = 0;
        hoverGraceLeft = 0;
        trippedLogs.Clear();
        popupDrawActions.Clear();
        openPopupId = null;
        openPopupAnchor = null;
        hitLayers.Clear();
        dispatchLayers.Clear();
        currentWindowOrigin = default;
        scrollTargetElementId = null;
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("UiSession is disposed; create a new host/session.");
        }
    }
}
