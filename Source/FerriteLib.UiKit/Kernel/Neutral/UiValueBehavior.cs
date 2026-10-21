using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
/// <summary>An immutable target-value request. Value is an intent, never a stored business answer.</summary>
public readonly struct UiValueOperation<T>
{
    internal UiValueOperation(UiInteractionResult interaction, T value, bool available)
    {
        Interaction = interaction;
        Value = value;
        Available = available;
    }

    public UiInteractionResult Interaction { get; }
    public T Value { get; }
    public bool Available { get; }
    public bool Request => Available && Interaction.Request;
}

/// <summary>Model-derived selection facts; observing a missing value never repairs the model.</summary>
public readonly struct UiSelectionObservation
{
    public UiSelectionObservation(int index, bool selectable)
    {
        Index = index;
        Selectable = selectable;
    }

    public int Index { get; }
    public bool Missing => Index < 0;
    public bool Selectable { get; }
}

/// <summary>Shared boolean, discrete-selection and expansion operations, independent of presentation.</summary>
public static class UiValueBehavior
{
    public static UiValueOperation<bool> Boolean(UiInteractionResult interaction, bool targetValue) => new UiValueOperation<bool>(interaction, targetValue, true);
    public static UiValueOperation<bool> Expansion(UiInteractionResult interaction, bool targetExpanded, bool hasContent) => new UiValueOperation<bool>(interaction, targetExpanded, hasContent);
    public static UiValueOperation<T> Selection<T>(UiInteractionResult interaction, T candidate, bool selectable = true) => new UiValueOperation<T>(interaction, candidate, selectable);
    public static UiSelectionObservation Observe<T>(IReadOnlyList<T> options, T current, Func<T, bool>? selectable = null)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        for (int i = 0; i < options.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(options[i], current))
                return new UiSelectionObservation(i, selectable == null || selectable(options[i]));
        }

        return new UiSelectionObservation(-1, false);
    }

    /// <summary>Dispatch once per established operation, including explicit same-value selection.</summary>
    public static void Request<T>(UiInteractionTarget target, UiValueOperation<T> operation, UiBindings bindings, Action<T> request)
    {
        if (!operation.Request) return;
        using IDisposable batch = bindings.BeginOperation();
        Request(target, operation, request);
    }

    public static void Request<T>(UiInteractionTarget target, UiValueOperation<T> operation, Action<T> request)
    {
        if (!operation.Request)
            return;
        UiInteractionRequests.Activation(target, operation.Interaction, () => request(operation.Value));
    }
}
