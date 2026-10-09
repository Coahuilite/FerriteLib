using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Per-kind window policy, applied by <see cref="UiWindowCatalog"/> to a window before it enters the
/// vanilla stack.
/// <para>
/// <b>Null means "the game's value, not mine".</b> Every modality switch here is a nullable
/// <c>bool?</c>: leaving one null does not write a library default into the window, it leaves whatever
/// <see cref="Verse.Window"/> already carries. That is deliberate for the two that are not local to a
/// window at all - <c>WindowsForcePause</c> and <c>WindowsPreventCameraMotion</c> are any-true over
/// every open window, so a library-chosen value would silently change another consumer's pause
/// behaviour. This library ships no product default for either; the consumer sets them.
/// </para>
/// <para>
/// <b><see cref="AllowMultipleInstances"/> is different in kind</b> and is not nullable, because a
/// shared page shell makes its answer load-bearing: with <c>false</c> the vanilla add path removes a
/// same-typed sibling. It defaults to <c>true</c> so the library's own key - not the C# type - is what
/// deduplicates instances. A false here is the vanilla rule, exact C# type included, so two different
/// kinds that happen to share one window class still evict each other; leave it true unless the kind is
/// the only user of its window type.
/// </para>
/// </summary>
public sealed class UiWindowOptions
{
    /// <summary>
    /// True (default) sets the window's <c>onlyOneOfTypeAllowed</c> to false, so a sibling of the same C#
    /// type is not removed by the vanilla add path; the catalog's key is the only dedup rule. False is
    /// the vanilla rule, and the catalog records the evicted sibling through
    /// <see cref="Verse.Window.PostClose"/> rather than pretending it did not happen.
    /// <para>
    /// <b>The sharp edge, stated where the switch is.</b> The eviction is by <b>exact</b> C# type - the
    /// game publishes <c>TryRemove(Type)</c> and <c>TryRemoveAssignableFromType(Type)</c> as separate
    /// operations, and the add path uses the former - so two window <i>kinds</i> that share one window
    /// class still evict each other when this is false. A generic shell is exactly that case:
    /// <see cref="UiPageWindow"/> is one class serving every kind, so a kind that sets this false closes
    /// other kinds' panels too. Keep it true, or give the kind its own shell type. This repeats
    /// <c>docs/development/0.5/30-consumer-handoff.md</c> §4 ("same-type multi-open needs an explicit
    /// choice"); the two must not drift.
    /// </para>
    /// </summary>
    public bool AllowMultipleInstances { get; set; } = true;

    /// <summary>Null leaves the game's own value. Any-true over all windows, so never a library default.</summary>
    public bool? ForcePause { get; set; }

    /// <summary>Null leaves the game's own value. Any-true over all windows, so never a library default.</summary>
    public bool? PreventCameraMotion { get; set; }

    /// <summary>Null leaves the game's own value; true makes a window swallow input around its rect.</summary>
    public bool? AbsorbInputAroundWindow { get; set; }

    /// <summary>Null leaves the game's own value.</summary>
    public bool? Draggable { get; set; }

    /// <summary>Null leaves the game's own value.</summary>
    public bool? Resizeable { get; set; }

    /// <summary>Null leaves the game's own value.</summary>
    public bool? CloseOnAccept { get; set; }

    /// <summary>Null leaves the game's own value.</summary>
    public bool? CloseOnCancel { get; set; }

    /// <summary>Null leaves the game's own value.</summary>
    public bool? CloseOnClickedOutside { get; set; }

    /// <summary>
    /// True (default) lets activation call the vanilla <c>WindowStack.Notify_ManuallySetFocus</c>, which
    /// sets the game's focused window without reordering the stack. False keeps the library's active
    /// target purely internal for a consumer whose own focus story would fight the call.
    /// </summary>
    public bool SetFocusOnActivate { get; set; } = true;

    /// <summary>
    /// The size the game uses to place the window on first open, surfaced through the shell's
    /// <c>InitialSizePolicy</c>. Null keeps the game's own <c>Window.InitialSize</c>: one consumer's
    /// screen math must not become the library's default.
    /// </summary>
    public Vector2? InitialSize { get; set; }

    /// <summary>
    /// The size re-applied to <c>windowRect</c> at <c>PreOpen</c>, i.e. the resting size a reopened
    /// window comes back at. Null leaves the rect the game set. Distinct from
    /// <see cref="InitialSize"/>: that one is the game's first-placement provider, this one a size the
    /// consumer restores on every open.
    /// <para>
    /// <b>Provisional semantics, recorded as such.</b> No consumer asked for this and nothing measures it
    /// beyond its own unit assertion in the catalog lane, which makes it the weakest P1 claim of the 0.5
    /// window round; that is the classification
    /// <c>docs/development/0.5/30-consumer-handoff.md</c> §4 carries for consumers. Verify it on a real
    /// window before treating it as a usable default, and expect the shape to change if a consumer's
    /// actual reopen behaviour disagrees with "the resting size is re-applied on every open".
    /// </para>
    /// </summary>
    public Vector2? NormalSize { get; set; }

    /// <summary>
    /// The consumer's close veto, or null for "closing is always allowed". The shell answers the vanilla
    /// <c>OnCloseRequest</c> hook with it, and <see cref="UiWindowCatalog.Close(UiWindowKey)"/> reports a
    /// refusal instead of removing the instance - the "a consumer's refusal to close is preserved" half
    /// of the window contract.
    /// </summary>
    public Func<UiWindowKey, bool>? CanClose { get; set; }
}
