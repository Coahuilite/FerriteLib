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

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new ButtonWidget(),
            AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "ActionBind", "Text", "TextKey", "Height"),
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
        return AtomVocabulary.ReadHeight(spec, ctx.Theme.Geometry.RowHeight);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        // Armed wins over hover: while the pointer is down inside the rect the element is doing the
        // thing it exists for, and that is the state worth painting. Both readings come from the
        // funnel seam, so a lane can drive the whole three-state ladder without a live event.
        bool armed = UiNative.IsMouseDownOver(rect);
        bool hovered = !armed && UiNative.IsMouseOver(rect);

        // No writability question here, deliberately: this atom's binding is a command, and the data
        // side's writability read answers "not writable" for anything that is not a value binding — so
        // asking would paint every bound button disabled. Command presence is a creation-time contract
        // instead (a missing ActionBind fails the host).
        UiResolvedStyle idle = AtomVocabulary.ResolveRole(spec, ctx, writable: null);
        Paint(rect, armed, hovered, idle, ctx);

        if (UiNative.Button(rect))
        {
            ctx.Bindings.Invoke(ReadActionKey());
        }
    }

    /// <summary>
    /// The element's own three looks. The idle row is the authored role's answer, so a toned button is
    /// a table query and not a fourth copy of the mapping; hover keeps the hover plane with the role's
    /// text, and armed takes the active treatment outright because live state beats the author. All
    /// three paint through the one surface outlet at the theme's hairline.
    /// </summary>
    private void Paint(Rect rect, bool armed, bool hovered, UiResolvedStyle idle, UiWidgetContext ctx)
    {
        UiTheme theme = ctx.Theme;
        UiResolvedStyle shown = idle;
        if (armed)
        {
            shown = theme.Styles.Resolve(UiStatusTone.Active);
        }
        else if (hovered)
        {
            shown = new UiResolvedStyle(theme.HoverSurface.Fill, theme.HoverSurface.Border, idle.Text);
        }

        UiThemeDraw.Surface(rect, shown.Surface, theme.Geometry.Hairline);
        UiThemeDraw.Label(
            new Rect(rect.x + theme.Geometry.Padding, rect.y, Math.Max(1f, rect.width - theme.Geometry.Padding * 2f), rect.height),
            AtomVocabulary.ResolveText(spec, ctx, "Text", "TextKey"),
            theme,
            shown.Text,
            AtomVocabulary.TextFont(ctx),
            TextAnchor.MiddleCenter,
            singleLine: true);
    }

    private string ReadActionKey()
    {
        return AtomVocabulary.Read(spec, "ActionBind");
    }
}
