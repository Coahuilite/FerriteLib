namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// When a window becomes <see cref="UiWindowCatalog.ActiveKey"/> - the one target that receives
/// keyboard input.
/// <para>
/// The confirmed requirement is <see cref="FollowClicks"/>: keyboard follows the click that landed in a
/// window, and switching the target keeps each window's own session state. The other two values exist
/// because a consumer may want a panel that never loses the target to a click, and because a policy
/// that can only be observed in one direction is a policy nobody can test.
/// </para>
/// <para>
/// <b>What this type does not claim.</b> Nothing here reorders the vanilla stack. Activation sets the
/// library's active target, keeps the deactivated window alive with its state, releases the capture that
/// window owned and (unless the options say otherwise) calls the game's own
/// <c>WindowStack.Notify_ManuallySetFocus</c>, which sets focus without moving the window in the list.
/// Whether that combination is enough for real keyboard routing is an in-game check, not a harness
/// result, and the checklist item that carries it is named in the 0.5 verification document.
/// </para>
/// </summary>
public enum UiFocusPolicy
{
    /// <summary>
    /// Default. A window becomes the active target when it opens, when the same key is reopened and when
    /// a pointer-down lands inside it; a pointer-down outside every instance clears the target.
    /// </summary>
    FollowClicks = 0,

    /// <summary>A window becomes active when it opens or is activated explicitly; clicks never move it.</summary>
    OpenOnly = 1,

    /// <summary>Only an explicit <see cref="UiWindowCatalog.Activate(UiWindowKey)"/> moves the target.</summary>
    Manual = 2
}
