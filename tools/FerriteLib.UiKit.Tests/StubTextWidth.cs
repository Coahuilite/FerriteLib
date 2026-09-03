using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Shared deterministic width model for the lane text stubs.
///
/// A CJK ideograph or full-width punctuation mark occupies one em; a Latin letter or digit occupies
/// about half an em. Encoding that ratio here — instead of returning one constant per font — is what
/// lets a lane assert that Chinese text is roughly twice as wide as the same character count of
/// English, which is the only way a text-fitting test can fail for the right reason.
/// </summary>
internal static class StubTextWidth
{
    internal static float Of(string text, UiFont font)
    {
        float em = font switch
        {
            UiFont.Tiny => 12f,
            UiFont.Medium => 18f,
            _ => 16f
        };

        float units = 0f;
        foreach (char c in text ?? "")
        {
            units += IsWide(c) ? 2f : 1f;
        }

        return units * em * 0.5f;
    }

    private static bool IsWide(char c)
    {
        return c >= '\u2E80' && (
            c <= '\u303F'
            || (c >= '\u3400' && c <= '\u4DBF')
            || (c >= '\u4E00' && c <= '\u9FFF')
            || (c >= '\uAC00' && c <= '\uD7AF')
            || (c >= '\uF900' && c <= '\uFAFF')
            || (c >= '\uFF00' && c <= '\uFF60')
            || (c >= '\uFFE0' && c <= '\uFFE6'));
    }
}
