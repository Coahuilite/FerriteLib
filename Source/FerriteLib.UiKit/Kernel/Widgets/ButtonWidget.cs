using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Core leaf atom <c>input/button</c>: one caption, one command, and the element's own hover and
/// armed appearance.
/// <para>
/// <b>Why this earns a kind</b> — per-element interaction state plus the hit rule behind it. The
/// library draws no game texture (<see cref="UiNative.Button"/> is <c>Widgets.ButtonInvisible</c>), so
/// vanilla's free hover/pressed chrome does not exist here: the three appearances below are paint this
/// element owns, chosen from a hit test against its own arranged rect. The manifest can express
/// neither "this leaf is the click target" nor "show me armed while the pointer holds me down", and
/// the funnel seam is what keeps that hit test drivable by the kernel-host lane.
/// </para>
/// <para>
/// The command is required, not optional. A button with no action would be a control that can never
/// fire — the silent-no-op shape this library's creation-time contract exists to refuse — so a missing
/// <c>ActionBind</c> fails at Host creation instead of drawing a dead rect.
/// </para>
/// </summary>
public sealed class ButtonWidget : IUiWidget
{
    public const string Kind = "input/button";

    private const float DefaultHeight = 28f;
    private const float TextPadding = 6f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new ButtonWidget(),
            AtomVocabulary.Schema("ActionBind", "Text", "TextKey", "Height"),
            new[] { "Text", "TextKey" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        string actionKey = ReadActionKey();
        if (actionKey.Length == 0)
        {
            throw new InvalidOperationException(
                $"ButtonWidget at '{elementPath}' requires an ActionBind naming the command it fires; "
                + "a button with no action is a creation-time contract error, not a dead control.");
        }

        bindings.ValidateCommand(actionKey, elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        return AtomVocabulary.ReadHeight(spec, DefaultHeight);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        // Armed wins over hover: while the pointer is down inside the rect the element is doing the
        // thing it exists for, and that is the state worth painting. Both readings come from the
        // funnel seam, so a lane can drive the whole three-state ladder without a live event.
        bool armed = UiNative.IsMouseDownOver(rect);
        bool hovered = !armed && UiNative.IsMouseOver(rect);
        Paint(rect, armed, hovered, ctx);

        if (UiNative.Button(rect))
        {
            ctx.Bindings.Invoke(ReadActionKey());
        }
    }

    /// <summary>
    /// The element's own three looks. Idle, hover and armed go through the theme's status-treatment
    /// outlet where a tone exists for the state (<c>Neutral</c>, <c>Active</c>), so this atom inherits
    /// the library's one tone table instead of keeping a fourth copy of it; hover has no tone and
    /// reads the theme's <see cref="UiTheme.Hover"/> token directly.
    /// </summary>
    private void Paint(Rect rect, bool armed, bool hovered, UiWidgetContext ctx)
    {
        UiTheme theme = ctx.Theme;
        Color color;
        if (armed)
        {
            UiThemeDraw.StatusTreatment(rect, theme, UiStatusTone.Active);
            color = theme.TextOnGold;
        }
        else if (hovered)
        {
            UiThemeDraw.Surface(rect, theme, theme.Hover, theme.BorderStrong);
            color = theme.TextPrimary;
        }
        else
        {
            UiThemeDraw.StatusTreatment(rect, theme, UiStatusTone.Neutral);
            color = theme.TextPrimary;
        }

        UiThemeDraw.Label(
            new Rect(rect.x + TextPadding, rect.y, Math.Max(1f, rect.width - TextPadding * 2f), rect.height),
            AtomVocabulary.ResolveText(spec, ctx, "Text", "TextKey"),
            theme,
            color,
            theme.DefaultFont,
            TextAnchor.MiddleCenter,
            singleLine: true);
    }

    private string ReadActionKey()
    {
        return AtomVocabulary.Read(spec, "ActionBind");
    }
}
