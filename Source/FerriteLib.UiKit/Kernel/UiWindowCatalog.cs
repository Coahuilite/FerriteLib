using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The library's window-instance registry. It creates windows through a registered factory, keys them by
/// <see cref="UiWindowKey"/>, activates exactly one target and closes them through the game's own
/// <see cref="WindowStack"/> - it never builds a second cross-window scheduler.
/// <para>
/// <b>Compose, do not duplicate.</b> Cross-window order, layer, vanilla modality and the close lifecycle
/// stay the game's: every add goes through <c>WindowStack.Add</c> (so <c>PreOpen</c>/<c>PostOpen</c> and
/// the exact-type sibling rule still run) and every close through <c>WindowStack.TryRemove</c> (so
/// <c>OnCloseRequest</c> can refuse and <c>PreClose</c>/<c>PostClose</c> still fire). What the library
/// adds on top is the identity the game does not have - one instance per
/// <c>(consumer, kind, context)</c> - plus the active-target rule and the state each window keeps
/// across a target change.
/// </para>
/// <para>
/// <b>The stack is handed in, not looked up.</b> The constructor takes the <see cref="WindowStack"/> the
/// caller binds to (a consumer passes <c>Find.WindowStack</c> when it builds the catalog). Nothing here
/// reaches for a process-wide game lookup, so the whole class is drivable in the harness with a stack
/// double and two catalogs in one process cannot touch each other.
/// </para>
/// <para>
/// <b>What is NOT verified here.</b> Activation ordering against the real game's click path, real
/// keyboard routing and modal coexistence are in-game behaviour: the catalog's own rule is testable, the
/// composition with IMGUI is not. See the 0.5 verification checklist (A1/A2/A3/A3b).
/// </para>
/// </summary>
public sealed class UiWindowCatalog
{
    // Registration lookup is two ordinal levels rather than a tuple key: net472's ValueTuple is fine but
    // a nested dictionary keeps the registry readable by consumer and kind in a diagnostic, and the
    // lookup cost is one hash each.
    private readonly Dictionary<string, Dictionary<string, Registration>> registrations =
        new Dictionary<string, Dictionary<string, Registration>>(StringComparer.Ordinal);

    private readonly Dictionary<UiWindowKey, UiWindowHost> instances = new Dictionary<UiWindowKey, UiWindowHost>();
    private readonly List<UiWindowHost> openOrder = new List<UiWindowHost>();
    private readonly List<UiWindowHost> activationOrder = new List<UiWindowHost>();

    private readonly WindowStack stack;
    private readonly UiFocusPolicy focusPolicy;

    private UiWindowHost? active;

    public UiWindowCatalog(WindowStack stack, UiFocusPolicy focusPolicy = UiFocusPolicy.FollowClicks)
    {
        this.stack = stack ?? throw new ArgumentNullException(nameof(stack));
        this.focusPolicy = focusPolicy;
    }

    /// <summary>When a window becomes the active target. Fixed for the catalog's life.</summary>
    public UiFocusPolicy FocusPolicy => focusPolicy;

    /// <summary>The open instances, in the order they were opened.</summary>
    public IReadOnlyList<UiWindowHost> Instances => openOrder;

    /// <summary>The key of the one active target, or null when no window is the target.</summary>
    public UiWindowKey? ActiveKey
    {
        get
        {
            UiWindowHost? window = active;
            return window == null ? (UiWindowKey?)null : window.Key;
        }
    }

    /// <summary>The one active target, or null when no window is the target.</summary>
    public UiWindowHost? ActiveWindow => active;

    /// <summary>
    /// True when any instance this catalog opened carries <c>forcePause</c>. This is the catalog's view
    /// of its own instances; the game's <c>WindowStack.WindowsForcePause</c> is the authority for game
    /// behaviour because it also counts vanilla windows.
    /// </summary>
    public bool AnyForcesPause
    {
        get
        {
            for (int i = 0; i < openOrder.Count; i++)
            {
                if (openOrder[i].forcePause) return true;
            }

            return false;
        }
    }

    /// <summary>
    /// True when any instance this catalog opened carries <c>preventCameraMotion</c>. Same scope note as
    /// <see cref="AnyForcesPause"/>: the game's own aggregate counts vanilla windows too.
    /// </summary>
    public bool AnyPreventsCameraMotion
    {
        get
        {
            for (int i = 0; i < openOrder.Count; i++)
            {
                if (openOrder[i].preventCameraMotion) return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Registers the factory and policy for one <c>(consumer, window kind)</c> pair. Re-registering the
    /// same pair replaces it; instances already open are untouched, because the registration governs
    /// creation and not the live windows.
    /// </summary>
    public void Register(
        string consumer,
        string windowKind,
        UiWindowOptions? options,
        Func<UiWindowKey, UiWindowHost> factory)
    {
        if (string.IsNullOrEmpty(consumer)) throw new ArgumentException("A registration needs a consumer id.", nameof(consumer));
        if (string.IsNullOrEmpty(windowKind)) throw new ArgumentException("A registration needs a window kind.", nameof(windowKind));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        if (!registrations.TryGetValue(consumer, out Dictionary<string, Registration>? byKind))
        {
            byKind = new Dictionary<string, Registration>(StringComparer.Ordinal);
            registrations.Add(consumer, byKind);
        }

        byKind[windowKind] = new Registration(options ?? new UiWindowOptions(), factory);
    }

    /// <summary>
    /// Opens the instance for <paramref name="key"/>. Returns true when a new window was created and
    /// false when the instance already open for that key was activated instead - reopening never creates
    /// a second instance, and the existing one keeps its session, scroll and drafts.
    /// </summary>
    public bool Open(UiWindowKey key)
    {
        if (instances.TryGetValue(key, out UiWindowHost? existing))
        {
            Activate(existing);
            return false;
        }

        Registration registration = FindRegistration(key);
        UiWindowHost window = registration.Create(key);
        if (window == null)
        {
            throw new UiContractException(
                "The registered window factory returned no window.",
                key.Consumer,
                key.WindowKind,
                key.WindowKind,
                key.ToString());
        }

        if (window.Key is UiWindowKey declared && declared != key)
        {
            throw new UiContractException(
                "The factory returned a window whose key is '" + declared + "' for the requested key '" + key + "'.",
                key.Consumer,
                key.WindowKind,
                key.WindowKind,
                key.ToString());
        }

        window.Key = key;
        window.AttachToCatalog(this);
        window.ApplyOptions(registration.Options);

        instances.Add(key, window);
        openOrder.Add(window);
        activationOrder.Add(window);

        try
        {
            // The vanilla add path runs PreOpen/PostOpen and, when an existing sibling carries
            // onlyOneOfTypeAllowed, removes that sibling first - which is why the option that sets the
            // flag is load-bearing for a shared window class.
            stack.Add(window);
        }
        catch
        {
            // A window that never entered the stack must not stay in the identity map: leaving it would
            // make the next Open(key) "activate" an instance the game never knew about.
            instances.Remove(key);
            openOrder.Remove(window);
            activationOrder.Remove(window);
            throw;
        }

        if (focusPolicy != UiFocusPolicy.Manual)
        {
            Activate(window);
        }

        return true;
    }

    /// <summary>
    /// Asks the vanilla stack to close the instance for <paramref name="key"/>. False means nothing was
    /// closed - the key is not open, or the consumer's <see cref="UiWindowOptions.CanClose"/> refused -
    /// and a refusal leaves the instance open with its session intact.
    /// </summary>
    public bool Close(UiWindowKey key)
    {
        if (!instances.TryGetValue(key, out UiWindowHost? window))
        {
            return false;
        }

        return stack.TryRemove(window);
    }

    /// <summary>Closes every open instance, honouring each one's close veto. Returns how many closed.</summary>
    public int CloseAll()
    {
        int closed = 0;
        for (int i = openOrder.Count - 1; i >= 0; i--)
        {
            if (stack.TryRemove(openOrder[i]))
            {
                closed++;
            }
        }

        return closed;
    }

    /// <summary>The open instance for <paramref name="key"/>, or false when nothing is open for it.</summary>
    public bool TryGet(UiWindowKey key, out UiWindowHost host)
    {
        if (instances.TryGetValue(key, out UiWindowHost? found))
        {
            host = found;
            return true;
        }

        host = null!;
        return false;
    }

    /// <summary>True when <paramref name="key"/> is the one active target.</summary>
    public bool IsActive(UiWindowKey key)
    {
        UiWindowHost? window = active;
        return window != null && window.Key == key;
    }

    /// <summary>Makes the open instance for <paramref name="key"/> the active target; unknown keys are ignored.</summary>
    public void Activate(UiWindowKey key)
    {
        if (instances.TryGetValue(key, out UiWindowHost? window))
        {
            Activate(window);
        }
    }

    // ---- lifecycle, called by the shell at the vanilla hooks ----

    /// <summary>
    /// A window is about to enter the stack: make sure the catalog holds it. Called from
    /// <see cref="Verse.Window.PreOpen"/>, i.e. before the window is in the vanilla list, so a window the
    /// catalog did not create still becomes addressable by its key instead of being invisible to the
    /// identity map.
    /// </summary>
    internal void NotifyPreOpen(UiWindowHost window)
    {
        if (window.Key is UiWindowKey key && !instances.ContainsKey(key))
        {
            instances.Add(key, window);
        }

        if (!openOrder.Contains(window))
        {
            openOrder.Add(window);
        }

        if (!activationOrder.Contains(window))
        {
            activationOrder.Add(window);
        }
    }

    /// <summary>A window is in the stack: the focus policy decides whether that makes it the target.</summary>
    internal void NotifyPostOpen(UiWindowHost window)
    {
        if (focusPolicy != UiFocusPolicy.Manual)
        {
            Activate(window);
        }
    }

    /// <summary>
    /// A close was requested and the vanilla hook has not refused it. Nothing structural happens here:
    /// the instance is dropped at <see cref="NotifyPostClose"/>, which is the only place removal is real.
    /// </summary>
    internal void NotifyCloseRequested(UiWindowHost window)
    {
    }

    /// <summary>
    /// A window is closing: it stops being the active target first, so no input or capture reaches a
    /// window that is on its way out.
    /// </summary>
    internal void NotifyPreClose(UiWindowHost window)
    {
        if (ReferenceEquals(active, window))
        {
            SetActive(null);
        }
    }

    /// <summary>A window is out of the stack: drop every reference the catalog held to it.</summary>
    internal void NotifyPostClose(UiWindowHost window)
    {
        if (window.Key is UiWindowKey key)
        {
            instances.Remove(key);
        }

        openOrder.Remove(window);
        activationOrder.Remove(window);

        if (ReferenceEquals(active, window))
        {
            // The target moves to the most recently activated survivor, or to nothing when this was the
            // last window. A closing window never leaves a dangling active target behind.
            UiWindowHost? next = activationOrder.Count > 0 ? activationOrder[activationOrder.Count - 1] : null;
            SetActive(next);
        }

        window.SetActiveTarget(false);
    }

    /// <summary>
    /// A pointer-down happened somewhere on screen. Under <see cref="UiFocusPolicy.FollowClicks"/> the
    /// target becomes the instance whose rect contains the pointer - resolved against every open
    /// instance, so it does not matter which window's pass reports it - and a click outside every
    /// instance clears the target.
    /// <para>
    /// <paramref name="reportingRect"/> is the rect the game handed the reporting window during this pass,
    /// which is authoritative for that one instance; every other instance is resolved against its last
    /// known <c>windowRect</c>. Overlapping windows are then resolved by most-recently-activated order
    /// rather than by IMGUI draw order. <b>Known boundary, not a silent assumption:</b> the stubs cannot
    /// model real stacking, so overlap is heuristic outside the non-overlapping case and the in-game
    /// checklist carries that scenario.
    /// </para>
    /// </summary>
    internal void NotifyPointerDown(Vector2 position, UiWindowHost reporting, Rect reportingRect)
    {
        if (focusPolicy != UiFocusPolicy.FollowClicks)
        {
            return;
        }

        UiWindowHost? target = Contains(reportingRect, position) ? reporting : null;
        if (target == null)
        {
            for (int i = activationOrder.Count - 1; i >= 0; i--)
            {
                UiWindowHost candidate = activationOrder[i];
                if (ReferenceEquals(candidate, reporting))
                {
                    continue;
                }

                if (Contains(candidate.windowRect, position))
                {
                    target = candidate;
                    break;
                }
            }
        }

        SetActive(target);
    }

    private Registration FindRegistration(UiWindowKey key)
    {
        if (registrations.TryGetValue(key.Consumer, out Dictionary<string, Registration>? byKind)
            && byKind.TryGetValue(key.WindowKind, out Registration? registration))
        {
            return registration;
        }

        throw new UiContractException(
            "No window kind '" + key.WindowKind + "' is registered for consumer '" + key.Consumer + "'.",
            key.Consumer,
            key.WindowKind,
            key.WindowKind,
            key.ToString());
    }

    private void Activate(UiWindowHost window)
    {
        SetActive(window);
    }

    private void SetActive(UiWindowHost? window)
    {
        if (ReferenceEquals(active, window))
        {
            if (window != null)
            {
                TouchActivationOrder(window);
            }

            return;
        }

        UiWindowHost? previous = active;
        active = window;

        previous?.SetActiveTarget(false);
        if (window != null)
        {
            window.SetActiveTarget(true);
            TouchActivationOrder(window);

            UiWindowOptions? options = window.AppliedOptions;
            if (options == null || options.SetFocusOnActivate)
            {
                // The vanilla focus setter, and nothing that reorders the list: Notify_ManuallySetFocus
                // does not bring a window to front, and this library must not claim it does. Whether the
                // call plus IMGUI's own click routing is sufficient for real keyboard routing is an
                // in-game question (0.5 checklist A1/A2/A3b).
                stack.Notify_ManuallySetFocus(window);
            }
        }
    }

    private void TouchActivationOrder(UiWindowHost window)
    {
        activationOrder.Remove(window);
        activationOrder.Add(window);
    }

    private static bool Contains(Rect rect, Vector2 point)
    {
        return point.x >= rect.x && point.x <= rect.xMax && point.y >= rect.y && point.y <= rect.yMax;
    }

    /// <summary>One registered kind's creation policy; the options instance is shared by every instance it creates.</summary>
    private sealed class Registration
    {
        internal Registration(UiWindowOptions options, Func<UiWindowKey, UiWindowHost> factory)
        {
            Options = options;
            Factory = factory;
        }

        internal UiWindowOptions Options { get; }

        internal Func<UiWindowKey, UiWindowHost> Factory { get; }

        internal UiWindowHost Create(UiWindowKey key)
        {
            return Factory(key);
        }
    }
}
