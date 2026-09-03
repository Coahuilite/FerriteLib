using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Theme token bag injected by the Host. 0.1 ships only Dark Gold, but the API is deliberately
/// theme-ready so a future theme can replace this bag without touching widgets.
/// </summary>
public sealed class UiTheme
{
    // Surfaces: restrained near-black planes with small luminance steps.
    public Color Base { get; set; } = new(0.065f, 0.065f, 0.063f, 1f);
    public Color Panel { get; set; } = new(0.095f, 0.095f, 0.090f, 1f);
    public Color Raised { get; set; } = new(0.135f, 0.135f, 0.128f, 1f);
    public Color Hover { get; set; } = new(0.17f, 0.17f, 0.16f, 1f);
    public Color Selected { get; set; } = new(0.17f, 0.14f, 0.08f, 1f);
    public Color Warning { get; set; } = new(0.38f, 0.14f, 0.11f, 1f);
    public Color Success { get; set; } = new(0.11f, 0.24f, 0.15f, 1f);
    public Color Danger { get; set; } = new(0.38f, 0.14f, 0.11f, 1f);

    // Named planes for reusable multi-column chrome primitives.
    public Color WorkspacePlane { get; set; } = new(0.045f, 0.045f, 0.044f, 1f);
    public Color SectionBand { get; set; } = new(0.085f, 0.085f, 0.080f, 1f);

    // Text
    public Color TextPrimary { get; set; } = new(0.88f, 0.88f, 0.84f, 1f);
    public Color TextSecondary { get; set; } = new(0.58f, 0.58f, 0.55f, 1f);
    public Color TextOnGold { get; set; } = new(1f, 0.84f, 0.55f, 1f);
    public Color TextOnDanger { get; set; } = new(1f, 0.65f, 0.46f, 1f);
    public Color TextDisabled { get; set; } = new(0.42f, 0.42f, 0.40f, 1f);

    // Accents: reserve gold for activity and focus states.
    public Color AccentGold { get; set; } = new(0.82f, 0.60f, 0.22f, 1f);
    public Color HoverPoint { get; set; } = new(0.96f, 0.80f, 0.42f, 1f);

    /// <summary>
    /// The accent at a reduced alpha. Derived rather than stored: a consumer that re-tints
    /// <see cref="AccentGold"/> must not be left with alpha variants still holding the old hue.
    /// </summary>
    public Color AccentWith(float alpha) => new Color(AccentGold.r, AccentGold.g, AccentGold.b, alpha);

    // Borders
    public Color Border { get; set; } = new(0.21f, 0.21f, 0.20f, 1f);
    public Color BorderStrong { get; set; } = new(0.32f, 0.32f, 0.30f, 1f);
    public Color Divider { get; set; } = new(0.14f, 0.14f, 0.13f, 1f);

    // Typography. DefaultFont is geometry-bearing (it feeds text measurement); the colour tokens
    // above are not, and KernelContractTests holds that line.
    public UiFont DefaultFont { get; set; } = UiFont.Small;

    /// <summary>
    /// The shipped default palette, handed out as a fresh instance on every call. It is a template,
    /// not a shared singleton: two consumer mods re-tinting "the default" must never repaint each
    /// other. A caller that wants one theme for a whole window keeps the returned instance.
    /// </summary>
    public static UiTheme DarkGold => new();
}
