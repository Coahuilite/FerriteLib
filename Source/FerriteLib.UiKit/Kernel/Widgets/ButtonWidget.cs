using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Default activation wrapper: caption, optional surface and typed command binding.
/// Pointer timing/qualification/capture come from UiInteractionSession through UiNative.Activation;
/// presentation observes that result and never recreates the activation rule.
/// A missing command is diagnosed at host creation, not rendered as a dead control.
/// </summary>
internal sealed class ButtonWidget : IUiWidget, IUiMeasureParticipant, IUiPointerLayout, IUiRetainedDisplay
{
    public const string Kind = "input/button";

    private UiElementSpec spec = UiElementSpec.Empty;
    private readonly UiRetainedPaint retained = new();
    bool IUiRetainedDisplay.PaintLast(Rect rect, UiWidgetContext ctx) => retained.Paint(rect,ctx);
    UiMeasurement? IUiRetainedDisplay.MeasureLast(UiWidgetContext ctx) => retained.Measure(ctx);

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new ButtonWidget(),
            // G2/G3 (0.7.x): PayloadKey gives the command a data payload (the row's own key inside a
            // template), and Chrome="none" makes the button a bare hit area whose Height="Auto" is measured
            // from its content instead of the theme's row height.
            AtomVocabulary.Schema(
                AtomRoles.ToneAndEmphasis, "ActionBind", "Text", "TextKey", "Height", "PayloadKey", "Chrome"),
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

        // The two shapes are different contracts, so the creation-time check must know which one this
        // element declares: with a payload the consumer binds BindAction<string>, without one BindCommand.
        string payloadKey = AtomVocabulary.Read(spec, "PayloadKey").Trim();
        if (payloadKey.Length > 0)
        {
            bindings.ValidateValue<string>(payloadKey, elementPath);
            bindings.ValidateAction<string>(actionKey, elementPath);
        }
        else
        {
            bindings.ValidateCommand(actionKey, elementPath);
        }

        // A Chrome value the kind does not implement is an inert declaration, so it is refused here rather
        // than ignored (the A2/A3 shape). Only "none" is meaningful; absent means the normal painted button.
        string chrome = AtomVocabulary.Read(spec, "Chrome").Trim();
        if (chrome.Length > 0 && !string.Equals(chrome, "none", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ButtonWidget at '{elementPath}' declares Chrome='{chrome}'; the only accepted value is 'none'"
                + " (a bare hit area). Omit the attribute for the painted button.");
        }
    }

    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        bool automatic = string.Equals(AtomVocabulary.Read(spec, "Height").Trim(), "Auto", StringComparison.OrdinalIgnoreCase);
        string text = automatic ? AtomVocabulary.ResolveText(spec, ctx, "Text", "TextKey") : "";
        retained.SetMeasure(c=>CaptureCaption(c,text,automatic));
        return CaptureCaption(ctx,text,automatic);
    }
    private UiMeasurement CaptureCaption(UiWidgetContext ctx,string text,bool automatic)
    {
        return new UiMeasurement(new UiMeasureInputs(new[] { text }, automatic && text.Length > 0 ? new[] { ctx.ViewWidth, (float)AtomVocabulary.TextFont(ctx), ctx.Theme.Geometry.Padding } : new[] { ctx.Theme.Geometry.RowHeight }, automatic && text.Length > 0), () =>
            automatic && text.Length > 0
                ? Math.Max(1, ctx.Metrics.MeasureText(text, AtomVocabulary.TextFont(ctx), Math.Max(1, ctx.ViewWidth - ctx.Theme.Geometry.Padding * 2)))
                : AtomVocabulary.ReadHeight(spec, ctx.Theme.Geometry.RowHeight));
    }

    public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();

    public System.Collections.Generic.IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds)
    {
        yield return new UiPointerRegion(bounds);
    }
    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        // A held operation wins over hover. Capture comes from shared behavior rather than raw
        // MouseDown geometry; painting neither infers an established activation nor requests it.
        // The same state is available to independent and non-rendering clients.
        UiInteractionResult interaction = UiNative.Activation(rect, ctx, ReadActionKey());
        bool armed = interaction.Captured && ctx.Session.Interactions.CaptureOwner?.Node == (ctx.Node ?? ctx.Session.ActiveNode).Id.Key;
        bool hovered = !armed && UiNative.IsMouseOver(rect);

        // No writability question here, deliberately: this atom's binding is a command, and the data
        // side's writability read answers "not writable" for anything that is not a value binding — so
        // asking would paint every bound button disabled. Command presence is a creation-time contract
        // instead (a missing ActionBind fails the host).
        UiResolvedStyle idle = AtomVocabulary.ResolveRole(spec, ctx, writable: null);

        UiInteractionRequests.Activation(UiNative.InteractionTarget(ctx, ReadActionKey()), interaction, () =>
        {
            // G2: with a PayloadKey the command receives the bound payload - the row's own key, which is what
            // a repeated row needs to say which item it is. Without one the call is exactly what it was.
            string payloadKey = AtomVocabulary.Read(spec, "PayloadKey").Trim();
            string payload = "";
            bool hasPayload = payloadKey.Length > 0;
            if (hasPayload)
            {
                try
                {
                    hasPayload = ctx.Bindings.TryGet(payloadKey, out payload);
                }
                catch (InvalidOperationException ex) when (ex is not UiReactiveFault)
                {
                    // A key bound to something that is not a string: fail-soft and loud, then fire without it.
                    AtomVocabulary.ReportUnresolved(ctx, Kind, payloadKey, "no payload");
                    hasPayload = false;
                }
            }

            if (hasPayload)
            {
                ctx.Bindings.Invoke(ReadActionKey(), payload);
            }
            else
            {
                ctx.Bindings.Invoke(ReadActionKey());
            }
        });
        if (ctx.Session.IsActive)
        {
            string caption=AtomVocabulary.ResolveText(spec,ctx,"Text","TextKey");
            Paint(rect, armed, hovered, idle, ctx,caption);
            bool automatic=string.Equals(AtomVocabulary.Read(spec,"Height").Trim(),"Auto",StringComparison.OrdinalIgnoreCase);
            retained.SetMeasure(c=>CaptureCaption(c,caption,automatic));
            retained.Capture(ctx,(r,c)=>Paint(r,false,false,idle,c,caption));
        }
    }

    /// <summary>
    /// The element's own three looks. The idle row is the authored role's answer, so a toned button is
    /// a table query and not a fourth copy of the mapping; hover keeps the hover plane with the role's
    /// text, and armed takes the active treatment outright because live state beats the author. All
    /// three paint through the one surface outlet at the theme's hairline.
    /// </summary>
    private void Paint(Rect rect, bool armed, bool hovered, UiResolvedStyle idle, UiWidgetContext ctx,string caption)
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

        // G3: Chrome="none" paints no surface at all - a hit area, not a control - while the caption keeps
        // the same inset and font, so the two shapes cannot disagree about where the text sits.
        if (!string.Equals(AtomVocabulary.Read(spec, "Chrome").Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            UiThemeDraw.Surface(rect, shown.Surface, theme.Geometry.Hairline);
        }

        UiThemeDraw.Label(
            new Rect(rect.x + theme.Geometry.Padding, rect.y, Math.Max(1f, rect.width - theme.Geometry.Padding * 2f), rect.height),
            caption,
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
