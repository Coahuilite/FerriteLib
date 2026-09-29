using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// A non-shipping palette that lives in the harness.
/// <para>
/// The library ships exactly one look (<see cref="UiTheme.Vanilla"/>), and most lanes use it for the plain
/// reason that a lane about layout, identity, input or invalidation needs SOME complete token set and does not
/// care which. A handful of lanes are different: they assert what a SECOND, different palette does - that two
/// palettes are genuinely different looks, that the per-surface edges of a palette that claims none fall back
/// to the shared borders, and that the resolved-value cells of that palette carry its own colours. Those lanes
/// need a palette whose values are pinned somewhere they can read, and pinning them in the payload would mean
/// the library shipping a look nobody consumes. So the palette moved here, into the harness that asserts it.
/// </para>
/// <para>
/// It is a test fixture, not a product: nothing in <c>Source/</c> may reference it, and
/// <c>docs/api-tiers.md</c> has no entry for it because it is not exported.
/// </para>
/// </summary>
internal static class TestPalettes
{
    /// <summary>
    /// The warm-gold look the lanes pin. It leaves every per-surface edge unclaimed on purpose: that is the
    /// property the "unclaimed edges fall back to the shared tokens" lanes assert, and it is why this palette
    /// and <see cref="UiTheme.Vanilla"/> - which claims its window and section edges - are distinguishable by
    /// more than their fills.
    /// </summary>
    internal static UiTheme DarkGold => new()
    {
        Base = new Color(0.065f, 0.065f, 0.063f, 1f),
        Panel = new Color(0.095f, 0.095f, 0.090f, 1f),
        Raised = new Color(0.135f, 0.135f, 0.128f, 1f),
        Hover = new Color(0.17f, 0.17f, 0.16f, 1f),
        Selected = new Color(0.17f, 0.14f, 0.08f, 1f),
        Success = new Color(0.11f, 0.24f, 0.15f, 1f),
        Danger = new Color(0.38f, 0.14f, 0.11f, 1f),
        WorkspacePlane = new Color(0.045f, 0.045f, 0.044f, 1f),
        SectionBand = new Color(0.085f, 0.085f, 0.080f, 1f),
        TextPrimary = new Color(0.88f, 0.88f, 0.84f, 1f),
        TextSecondary = new Color(0.58f, 0.58f, 0.55f, 1f),
        TextOnGold = new Color(1f, 0.84f, 0.55f, 1f),
        TextOnDanger = new Color(1f, 0.65f, 0.46f, 1f),
        TextDisabled = new Color(0.42f, 0.42f, 0.40f, 1f),
        AccentGold = new Color(0.82f, 0.60f, 0.22f, 1f),
        Border = new Color(0.21f, 0.21f, 0.20f, 1f),
        BorderStrong = new Color(0.32f, 0.32f, 0.30f, 1f),
        Divider = new Color(0.14f, 0.14f, 0.13f, 1f),
        BaseBorder = null,
        PanelBorder = null,
        RaisedBorder = null,
        HoverBorder = null,
        SelectedBorder = null,
        SuccessBorder = null,
        DangerBorder = null,
    };
}
