using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One option of a dynamic options binding: the text a reader sees and the value the page commits when it is
/// chosen (FL-16, 0.7.x). The static <c>OptionN</c>/<c>ValueN</c> pairs always had this separation; this type is
/// the same thing for a binding, so consumer data can carry a display name and a machine token without the
/// library guessing which is which.
/// <para>
/// Both halves are strings on purpose: the binding stays a list of one element type, so a page that binds a
/// plain <c>IReadOnlyList&lt;string&gt;</c> keeps working exactly as before with display == value, and the
/// element type a page declares is what selects the read - never a guess, and never a mismatch report for a
/// page that declared one of the two accepted shapes.
/// </para>
/// </summary>
public readonly struct UiOption
{
    public UiOption(string text, string value)
    {
        Text = text ?? "";
        Value = value ?? "";
    }

    /// <summary>What the option shows.</summary>
    public string Text { get; }

    /// <summary>What choosing it writes through the element's value binding.</summary>
    public string Value { get; }
}