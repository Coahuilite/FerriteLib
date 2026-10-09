using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The OPTIONAL typed half of <see cref="IUiBindings"/> (R4-A, 0.7.x): the type-erased read/write a non-generic
/// widget needs to serve a genuinely typed value/options binding.
/// <para>
/// <b>Why a companion interface and not a member on <see cref="IUiBindings"/>.</b> net472 has no default
/// interface members, so a new required member on <see cref="IUiBindings"/> would stop every existing external
/// implementation from compiling. This seam is purely additive: <see cref="UiBindings"/> implements it, a
/// widget probes for it, and an <see cref="IUiBindings"/> implementation without it keeps compiling and keeps
/// working string-only - its pages take today's path unchanged.
/// </para>
/// <para>
/// <b>What it carries.</b> A widget cannot name the consumer's <c>T</c>, so this interface answers the four
/// questions such a control has: what type the key's value is bound as, what the current value IS (boxed), what
/// the declared type accepts, and the typed option list (label plus boxed value) so the control can render
/// labels and commit values. <b>Nothing here converts a value to or from a string</b>, and the write refuses a
/// value of the wrong type without performing any write at all.
/// </para>
/// </summary>
public interface IUiTypedChoices
{
    /// <summary>
    /// The declared type of <paramref name="key"/>'s value binding; when the key carries no value binding, the
    /// value type its typed choices declare. False when the key is neither, in which case
    /// <paramref name="valueType"/> is <c>object</c>.
    /// </summary>
    bool TryGetValueType(string key, out Type valueType);

    /// <summary>The current value of <paramref name="key"/> as its real instance, boxed. False when the key has
    /// no value binding, in which case <paramref name="value"/> is null.</summary>
    bool TryGetTypedValue(string key, out object? value);

    /// <summary>
    /// True when <paramref name="value"/> is an instance of <paramref name="key"/>'s declared type and may
    /// therefore be written - the same acceptance test <see cref="TrySetTypedValue"/> applies, exposed so a
    /// control can judge a whole option list at creation time instead of discovering a mismatch one refused
    /// click at a time. False for an unbound key. Nothing is written.
    /// </summary>
    bool AcceptsValue(string key, object? value);

    /// <summary>
    /// Writes <paramref name="value"/> through the key's own <c>Action&lt;T&gt;</c> setter, but only when it is
    /// an instance of the declared type. A mismatched value returns false and performs <b>no write</b>, no
    /// coercion, no notification and no exception - a refusal is an answer, not a fault. False too when the key
    /// has no value binding or its binding is read-only.
    /// </summary>
    bool TrySetTypedValue(string key, object? value);

    /// <summary>
    /// The typed options list bound at <paramref name="key"/>: each entry's label and its boxed value. False
    /// when the key has no options binding or its options are not <see cref="UiChoice{T}"/> entries - a plain
    /// string list and a <see cref="UiOption"/> list keep their own paths untouched, and
    /// <paramref name="choices"/> is then empty.
    /// </summary>
    bool TryGetChoices(string key, out IReadOnlyList<UiChoice<object?>> choices);
}
