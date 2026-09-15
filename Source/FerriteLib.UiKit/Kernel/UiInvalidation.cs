using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// What a binding announcement invalidates, declared where the binding is registered rather than inferred
/// from the shape of the value: the engine asks the key what it means instead of re-reading a page's
/// source text to guess.
/// <para>
/// <see cref="Paint"/> is the cheap class. A fixed-size readout - a count, a state word, a progress fill -
/// changes without moving a single rect, so its announcement must reuse the arranged snapshot and let the
/// next paint read the new value; re-measuring the page for it is cost with no answer.
/// <see cref="Measure"/> is the class for a value the geometry depends on: a label, a measured
/// auto-width, anything whose text length reaches a band. <see cref="Structure"/> says in addition that
/// the element's slot may change shape - an element appears or disappears, a container's child set moves -
/// so the container that owns the slot is invalidated with the element.
/// </para>
/// <para>
/// The flags combine because a page may need more than one. A binding that declares nothing gets
/// <see cref="Everything"/>: under-invalidating leaves stale geometry on screen while over-invalidating
/// costs one arrangement, and only the author knows which side their value is on.
/// </para>
/// </summary>
[Flags]
public enum UiInvalidation
{
    /// <summary>Nothing declared: a key registered this way invalidates nothing by itself.</summary>
    None = 0,

    /// <summary>The next paint pass reads the new value; the measured arrangement is reused as it stands.</summary>
    Paint = 1,

    /// <summary>The elements that declare the key are re-measured.</summary>
    Measure = 2,

    /// <summary>Like <see cref="Measure"/>, and the container that owns the element's slot reflows too.</summary>
    Structure = 4,

    /// <summary>Everything an announcement can invalidate. The conservative default for an undeclared binding.</summary>
    Everything = Paint | Measure | Structure
}
