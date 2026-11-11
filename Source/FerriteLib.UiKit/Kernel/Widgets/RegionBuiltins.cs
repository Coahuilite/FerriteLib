using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel.Widgets;

internal static class RegionBuiltin
{
    internal static bool Supports(UiRegionReadMap reads, UiElementSpec spec, bool text)
    {
        foreach (string attribute in text ? new[] { "Bind", "SelectedBind" } : new[] { "SelectedBind" })
        {
            string key = AtomVocabulary.Read(spec, attribute);
            UiRegionUse use = attribute == "Bind" ? UiRegionUse.Layout | UiRegionUse.Display : UiRegionUse.Display;
            if (key.Length != 0 && !reads.Find(key, false, use, out _, out _, out _, out _)) return false;
        }
        return true;
    }
    internal static T Get<T>(UiRegionSnapshot packet, string key)
        => packet.Get<T>(UiRegionSnapshot.Address(key, out _, out _, out string field) ? field : key);
    internal static T Read<T>(UiRegionSnapshot packet, string key, UiRegionSnapshot? root, UiRegionReadMap? reads)
    {
        if (root == null || reads == null) return Get<T>(packet, key);
        if (!reads.Find(key, false, UiRegionUse.Display, out var route, out var source, out var item, out var field)
            || route.Type != typeof(T)) throw new InvalidOperationException("Region display route mismatch: " + key + ".");
        return (source == null ? root : root.AddressedItem(source, item!)).Get<T>(field);
    }
    internal static UiResolvedStyle Role(UiElementSpec spec, UiWidgetContext ctx, UiRegionSnapshot packet, UiRegionSnapshot? root = null, UiRegionReadMap? reads = null)
    {
        string selected = AtomVocabulary.Read(spec, "SelectedBind");
        return AtomVocabulary.ResolveRegionRole(spec, ctx, selected.Length != 0 && Read<bool>(packet, selected, root, reads));
    }
    internal static float Height(UiElementSpec spec, float natural)
        => spec.TryGetNumber("Height", out float height) ? Math.Max(0, height) : natural;
    internal sealed class TextDisplay : UiRegionDisplay
    {
        internal readonly string Text;
        internal readonly UiResolvedStyle Style;
        internal TextDisplay(string text, UiResolvedStyle style, float height, float? label = null, IReadOnlyList<UiPointerRegion>? pointers = null)
            : base(height, label, pointers ?? Array.Empty<UiPointerRegion>()) { Text = text; Style = style; }
    }
}

internal sealed partial class WrappedTextWidget : IUiRegionParticipant, IUiRegionBuiltin
{
    bool IUiRegionBuiltin.Supports(UiRegionReadMap reads, UiElementSpec declaration) => RegionBuiltin.Supports(reads, declaration, true);
    UiRegionDisplay IUiRegionParticipant.Build(UiWidgetContext ctx, UiRegionSnapshot inputs)
        => BuildRegion(ctx, inputs, null, null);
    UiRegionDisplay IUiRegionBuiltin.BuildRegion(UiWidgetContext ctx, UiRegionSnapshot inputs, UiRegionSnapshot root, UiRegionReadMap reads)
        => BuildRegion(ctx, inputs, root, reads);
    private UiRegionDisplay BuildRegion(UiWidgetContext ctx, UiRegionSnapshot inputs, UiRegionSnapshot? root, UiRegionReadMap? reads)
    {
        string key = AtomVocabulary.Read(spec, "Bind");
        string text = key.Length == 0 ? AtomVocabulary.ResolveText(spec, ctx, "Text", "TextKey") : RegionBuiltin.Read<string>(inputs, key, root, reads) ?? "";
        float height = CaptureText(ctx, text).Calculate();
        return new RegionBuiltin.TextDisplay(text, RegionBuiltin.Role(spec, ctx, inputs, root, reads), height);
    }
    void IUiRegionParticipant.Operate(UiLayoutRect bounds, UiWidgetContext ctx, UiRegionDisplay display) { }
    void IUiRegionParticipant.Paint(UiLayoutRect bounds, UiWidgetContext ctx, UiRegionDisplay display)
    {
        var text = (RegionBuiltin.TextDisplay)display;
        if (bounds.Width > 1 && bounds.Height > 1 && text.Text.Length != 0) PaintText(bounds, ctx, text.Text, text.Style);
    }
}

internal sealed partial class ButtonWidget : IUiRegionParticipant, IUiRegionBuiltin
{
    private bool regionArmed, regionHovered;
    bool IUiRegionBuiltin.Supports(UiRegionReadMap reads, UiElementSpec declaration) => RegionBuiltin.Supports(reads, declaration, false);
    UiRegionDisplay IUiRegionParticipant.Build(UiWidgetContext ctx, UiRegionSnapshot inputs)
        => BuildRegion(ctx, inputs, null, null);
    UiRegionDisplay IUiRegionBuiltin.BuildRegion(UiWidgetContext ctx, UiRegionSnapshot inputs, UiRegionSnapshot root, UiRegionReadMap reads)
        => BuildRegion(ctx, inputs, root, reads);
    private UiRegionDisplay BuildRegion(UiWidgetContext ctx, UiRegionSnapshot inputs, UiRegionSnapshot? root, UiRegionReadMap? reads)
    {
        string caption = AtomVocabulary.ResolveText(spec, ctx, "Text", "TextKey");
        bool automatic = string.Equals(AtomVocabulary.Read(spec, "Height").Trim(), "Auto", StringComparison.OrdinalIgnoreCase);
        float height = CaptureCaption(ctx, caption, automatic).Calculate();
        return new RegionBuiltin.TextDisplay(caption, RegionBuiltin.Role(spec, ctx, inputs, root, reads), height,
            pointers: new[] { new UiPointerRegion(new UiLayoutRect(0, 0, ctx.ViewWidth, RegionBuiltin.Height(spec, height))) });
    }
    void IUiRegionParticipant.Operate(UiLayoutRect bounds, UiWidgetContext ctx, UiRegionDisplay display)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1) return;
        UiInteractionResult interaction = ctx.Controls.Activation(bounds, ctx, ReadActionKey());
        regionArmed = interaction.Captured && ctx.Session.Interactions.CaptureOwner?.Node == ctx.Node.Id.Key;
        regionHovered = !regionArmed && ctx.Controls.IsMouseOver(bounds);
        UiInteractionRequests.Activation(ctx.Controls.InteractionTarget(ctx, ReadActionKey()), interaction, () => RequestActivation(ctx));
    }
    void IUiRegionParticipant.Paint(UiLayoutRect bounds, UiWidgetContext ctx, UiRegionDisplay display)
    {
        var text = (RegionBuiltin.TextDisplay)display;
        if (bounds.Width > 1 && bounds.Height > 1) Paint(bounds, regionArmed, regionHovered, text.Style, ctx, text.Text);
    }
}

internal sealed partial class RuleWidget : IUiRegionParticipant, IUiRegionBuiltin
{
    bool IUiRegionBuiltin.Supports(UiRegionReadMap reads, UiElementSpec declaration) => RegionBuiltin.Supports(reads, declaration, false);
    UiRegionDisplay IUiRegionParticipant.Build(UiWidgetContext ctx, UiRegionSnapshot inputs)
        => new RegionBuiltin.TextDisplay("", RegionBuiltin.Role(spec, ctx, inputs), ReadThickness(ctx));
    UiRegionDisplay IUiRegionBuiltin.BuildRegion(UiWidgetContext ctx, UiRegionSnapshot inputs, UiRegionSnapshot root, UiRegionReadMap reads)
        => new RegionBuiltin.TextDisplay("", RegionBuiltin.Role(spec, ctx, inputs, root, reads), ReadThickness(ctx));
    void IUiRegionParticipant.Operate(UiLayoutRect bounds, UiWidgetContext ctx, UiRegionDisplay display) { }
    void IUiRegionParticipant.Paint(UiLayoutRect rect, UiWidgetContext ctx, UiRegionDisplay display)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        float thickness = Math.Min(ReadThickness(ctx), rect.Height), inset = Math.Max(0, AtomVocabulary.ReadFloat(spec, "Inset", 0));
        if (rect.Width > inset * 2) UiThemeDraw.Solid(ctx.Drawing,
            new UiLayoutRect(rect.X + inset, rect.Y + (rect.Height - thickness) * 0.5f, rect.Width - inset * 2, thickness),
            ((RegionBuiltin.TextDisplay)display).Style.Border);
    }
}
