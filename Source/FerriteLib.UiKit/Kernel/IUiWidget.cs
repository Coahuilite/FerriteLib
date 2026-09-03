using UnityEngine;

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
    void Validate(IUiBindings bindings, string elementPath);

    /// <summary>Returns the natural content height for the current context.</summary>
    float Measure(UiWidgetContext ctx);

    /// <summary>Draws into the arranged rect and handles the current IMGUI event synchronously.</summary>
    void Draw(Rect rect, UiWidgetContext ctx);
}
