using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One option of a TYPED options binding: the text a reader sees and the value the page commits when it is
/// chosen (R4-A, 0.7.x). <see cref="UiOption"/> is the same carrier for string data; this one keeps the real
/// <typeparamref name="T"/>, so a control can hand the value back through the consumer's own
/// <c>Action&lt;T&gt;</c> setter with no string round-trip at any point - type identity is what validates a
/// value here, never its spelling.
/// <para>
/// The element type a page declares is still what selects the read: <c>BindOptions&lt;UiChoice&lt;T&gt;&gt;</c>
/// is a distinct registration from a <c>IReadOnlyList&lt;string&gt;</c> or a <c>IReadOnlyList&lt;UiOption&gt;</c>
/// one, and a page that declared either of those keeps working exactly as before.
/// </para>
/// </summary>
public readonly struct UiChoice<T> : IUiChoice
{
    public UiChoice(string text, T value)
    {
        Text = text ?? "";
        Value = value;
    }

    /// <summary>What the option shows.</summary>
    public string Text { get; }

    /// <summary>What choosing it writes through the element's typed value binding.</summary>
    public T Value { get; }

    object? IUiChoice.BoxedValue => Value;

    Type IUiChoice.ValueType => typeof(T);
}

/// <summary>
/// The type-erased face of one <see cref="UiChoice{T}"/>, for the library's own widgets: a widget is not
/// generic, so it cannot name <c>T</c>, and this is how it reads a choice's label and hands the choice's value
/// back as a boxed object.
/// <para>
/// Internal on purpose. A consumer binds and receives <see cref="UiChoice{T}"/> directly; implementing this
/// keeps the two erased members off the public carrier's surface while still making a
/// <c>UiChoice&lt;T&gt;</c> recognisable without reflection - the shape is answered by
/// <c>default(T) is IUiChoice</c>, which is true exactly when the registered item type is a choice.
/// </para>
/// </summary>
internal interface IUiChoice
{
    /// <summary>What the option shows.</summary>
    string Text { get; }

    /// <summary>The option's value as its real instance, boxed.</summary>
    object? BoxedValue { get; }

    /// <summary>The declared type of <see cref="BoxedValue"/>.</summary>
    Type ValueType { get; }
}
