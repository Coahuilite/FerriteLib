using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One layer of the owned hit stack (0.4.0): the element whose paint covers <see cref="Rect"/> in Host
/// window space, plus whether that layer is a popup. The stack is stored bottom-to-top in paint order, and
/// input dispatch walks it topmost-first (<see cref="UiSession.IsPointerOverHigherLayer"/>) - that single
/// rule decides whether an element may take a click, in place of the per-element yield branch the funnel
/// used to carry.
/// </summary>
public readonly struct UiHitLayer
{
    public UiHitLayer(UiNode element, Rect rect, bool isPopup)
    {
        Element = element;
        Rect = rect;
        IsPopup = isPopup;
    }

    /// <summary>The element this layer belongs to; dispatch compares node identity, never a path.</summary>
    public UiNode Element { get; }

    /// <summary>The layer's rect in Host window space.</summary>
    public Rect Rect { get; }

    /// <summary>True for a popup layer, which always sits above content.</summary>
    public bool IsPopup { get; }
}
