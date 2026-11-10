
namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Greenfield widget contract. A widget is configured once from its XML spec, validates its typed
/// binding contract at Host-creation time, measures its natural height, and draws synchronously
/// into a Host-arranged rect. Widgets never own window/session state.
/// </summary>
public interface IUiWidget
{
    string Kind { get; }

    /// <summary>Called once after factory instantiation with the immutable XML spec.</summary>
    void Configure(UiElementSpec spec);

    /// <summary>
    /// Called by the Host during creation. Implementations must check every binding/action they
    /// need and throw <see cref="UiContractException"/> with the element path on mismatch.
    /// </summary>
    void Validate(IUiBindingAccess bindings, string elementPath);

    /// <summary>Returns the natural content height for the current context.</summary>
    float Measure(UiWidgetContext ctx);

    /// <summary>Draws into the arranged rect and handles the current IMGUI event synchronously.</summary>
    void Draw(UiLayoutRect rect, UiWidgetContext ctx);
}

// Experimental opt-in only. An operation still runs on every pass; Deferred says only that
// presentation did not run and therefore must not replace the last successful presentation.
internal enum UiDisplayResult { Completed, Deferred }
internal interface IUiDeferredDisplay
{
    UiDisplayResult DrawForClip(UiLayoutRect rect, UiWidgetContext ctx, bool clipped, IUiIsolatedPaint paint);
}

internal interface IUiIsolatedPaint
{
    bool CanIsolatePaint { get; }
    void IsolatePaint(System.Action paint);
}
