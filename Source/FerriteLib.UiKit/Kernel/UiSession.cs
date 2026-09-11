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

    private readonly Dictionary<string, Vector2> scrollPositions = new(StringComparer.Ordinal);

    // Per-element state, keyed by the element's identity and then by the widget's own state name.
    // The identity is the primary key: it is what makes two unnamed same-kind siblings separate, which
    // the bare path string this replaces could not express (0.4.0 identity layer).
    private readonly Dictionary<UiNodeId, Dictionary<string, UiValueState>> valueStates = new();

    private readonly HashSet<string> trippedComponentIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> trippedLogs = new(StringComparer.Ordinal);
    private int? ownedHotControl;
    private readonly List<Action> popupDrawActions = new();
    private string? openPopupId;
    private Rect? openPopupAnchor;
    private Rect? openPopupRect;
    private Rect hostViewport;
    private string? scrollTargetElementId;
    private string hoverClaim = "";
    private UiNodeId hoverClaimElement;
    private string hoverHeld = "";
    private UiNodeId hoverHeldElement;
    private UiNodeId activeElement;
    private int hoverClaimStamp;
    private int hoverGraceLeft;

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

    /// <summary>Monotonic revision bumped when dynamic content changes. Used for layout cache keys.</summary>
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
    /// The engine enters an element's identity around its own widget calls, and
    /// <see cref="GetOrCreateValueState(string)"/> resolves against it: that is how a widget written
    /// against the old bare-string key gets a per-element state namespace without changing a line.
    /// </summary>
    public UiNodeId ActiveElement => activeElement;

    /// <summary>Session-scoped scroll positions keyed by scroll container id.</summary>
    public IReadOnlyDictionary<string, Vector2> ScrollPositions => scrollPositions;

    /// <summary>Component ids currently tripped into session fallback.</summary>
    public IReadOnlyCollection<string> TrippedComponentIds => trippedComponentIds;

    /// <summary>Session-scoped popup draw callbacks. Not process-global.</summary>
    public IReadOnlyList<Action> PopupDrawActions => popupDrawActions;

    /// <summary>Id of the popup currently owned by this session, if any.</summary>
    public string? OpenPopupId => openPopupId;

    /// <summary>Anchor rect of the popup currently owned by this session, if any.</summary>
    public Rect? OpenPopupAnchor => openPopupAnchor;

    /// <summary>
    /// Window-space rect the open popup actually covered when it was last drawn. The popup pass runs
    /// after content, so this is the previous frame's rect — which is exactly the frame boundary a
    /// click on a popup row arrives in. Null between opening and the first draw.
    /// </summary>
    public Rect? OpenPopupRect => openPopupRect;

    /// <summary>Host viewport in window space, published once per frame before any content draws.</summary>
    public Rect HostViewport => hostViewport;

    /// <summary>Id of the element the host should scroll into view on the next arranged frame, if any.</summary>
    public string? ScrollTargetElementId => scrollTargetElementId;

    /// <summary>
    /// Requests the host to scroll the target element into view. The request stays pending until the
    /// host can resolve it against an arranged snapshot (which may be a later frame, e.g. after a tab
    /// switch makes the element visible).
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
        // A freshly opened popup has no drawn rect yet; the popup pass records it this frame.
        openPopupRect = null;
    }

    /// <summary>Closes the session-owned popup, if any.</summary>
    public void ClosePopup()
    {
        EnsureActive();
        openPopupId = null;
        openPopupAnchor = null;
        openPopupRect = null;
    }

    /// <summary>Records the window-space rect of the popup the session is drawing this frame.</summary>
    public void SetPopupRect(Rect rect)
    {
        EnsureActive();
        openPopupRect = rect;
    }

    /// <summary>
    /// True when <paramref name="point"/> (Host window space) falls inside the popup the session last
    /// drew. Content that lies under the popup must use this to give up the click: the popup is drawn
    /// after content, so it can only ever be the topmost thing the player sees.
    /// </summary>
    public bool IsPointOverPopup(Vector2 point)
    {
        if (!openPopupRect.HasValue) return false;
        Rect rect = openPopupRect.Value;

        // Written out instead of calling Rect.Contains: the harness's UnityEngine stub has no such
        // member, and a throw here would be swallowed by the draw guard and silently disable the rule.
        return point.x >= rect.x && point.x <= rect.xMax && point.y >= rect.y && point.y <= rect.yMax;
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
        return GetOrCreateValueState(activeElement, stateKey);
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
        if (!valueStates.TryGetValue(element, out Dictionary<string, UiValueState>? slots))
        {
            slots = new Dictionary<string, UiValueState>(StringComparer.Ordinal);
            valueStates.Add(element, slots);
        }

        if (!slots.TryGetValue(stateKey, out UiValueState? state))
        {
            state = new UiValueState();
            slots.Add(stateKey, state);
        }

        return state;
    }

    /// <summary>
    /// The state slots one element owns, keyed by the widget's state names, or an empty map when the
    /// element holds none. The read is by identity because that is the primary key; the old flat
    /// property could not name both dimensions.
    /// </summary>
    public IReadOnlyDictionary<string, UiValueState> GetValueStates(UiNodeId element)
    {
        return valueStates.TryGetValue(element, out Dictionary<string, UiValueState>? slots)
            ? slots
            : NoValueStates;
    }

    /// <summary>
    /// Enters <paramref name="element"/> for the duration of its own Measure/Draw and returns the
    /// previous active element, so the engine can hand it back to <see cref="ExitElement"/> in a
    /// <c>finally</c>. Nesting is a stack discipline: the engine draws one element at a time.
    /// </summary>
    internal UiNodeId EnterElement(UiNodeId element)
    {
        EnsureActive();
        UiNodeId previous = activeElement;
        activeElement = element;
        return previous;
    }

    /// <summary>Restores the element returned by <see cref="EnterElement"/>.</summary>
    internal void ExitElement(UiNodeId previous)
    {
        activeElement = previous;
    }

    public Vector2 GetScrollPosition(string elementId)
    {
        return elementId != null && scrollPositions.TryGetValue(elementId, out Vector2 pos)
            ? pos
            : Vector2.zero;
    }

    public void SetScrollPosition(string elementId, Vector2 position)
    {
        if (elementId == null) throw new ArgumentNullException(nameof(elementId));
        EnsureActive();
        scrollPositions[elementId] = position;
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
        hoverClaimElement = activeElement;
        hoverClaimStamp = Frame;
    }

    public bool IsTripped(string elementId)
    {
        return elementId != null && trippedComponentIds.Contains(elementId);
    }

    public void Trip(string elementId, string diagnostic)
    {
        if (string.IsNullOrEmpty(elementId)) return;
        EnsureActive();
        if (trippedComponentIds.Add(elementId))
        {
            trippedLogs[elementId] = diagnostic;
        }
    }

    public bool TryGetTripLog(string elementId, out string diagnostic)
    {
        return trippedLogs.TryGetValue(elementId, out diagnostic!);
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
        valueStates.Clear();
        activeElement = default;
        hoverClaim = "";
        hoverClaimElement = default;
        hoverHeld = "";
        hoverHeldElement = default;
        hoverClaimStamp = 0;
        hoverGraceLeft = 0;
        trippedComponentIds.Clear();
        trippedLogs.Clear();
        popupDrawActions.Clear();
        openPopupId = null;
        openPopupAnchor = null;
        openPopupRect = null;
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
