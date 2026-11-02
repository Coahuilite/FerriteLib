using System;

namespace FerriteLib.UiKit.Kernel;

// Adapter-only last successful paint. Never invokes Draw, native input, bindings, editor Observe or requests.
internal interface IUiRetainedDisplay
{
    bool PaintLast(UiLayoutRect rect, UiWidgetContext context);
    UiMeasurement? MeasureLast(UiWidgetContext context);
}
internal sealed class UiRetainedPaint
{
    private Action<UiLayoutRect,UiWidgetContext>? paint;
    private string target="";
    private int definition;
    private Func<UiWidgetContext,UiMeasurement>? measure;
    private UiTheme? theme, originalTheme;
    private UiTextFont font;
    private int colourRevision, layoutRevision;
    internal void SetMeasure(Func<UiWidgetContext,UiMeasurement> calculate) => measure=calculate;
    internal UiMeasurement? Measure(UiWidgetContext ctx)
        => paint!=null && definition==ctx.Session.DefinitionRevision && target==(ctx.Node??ctx.Session.ActiveNode).InteractionScope ? measure?.Invoke(theme == null ? ctx : ctx.WithTheme(theme)) : null;
    internal void Capture(UiWidgetContext ctx, Action<UiLayoutRect,UiWidgetContext> render)
    {
        target=(ctx.Node??ctx.Session.ActiveNode).InteractionScope;
        UiTextFont effective = UiTextFonts.For(ctx).Font;
        if (theme == null || !ReferenceEquals(originalTheme, ctx.Theme) || colourRevision != ctx.Theme.ColourRevision || layoutRevision != ctx.Theme.LayoutRevision || font != effective)
        {
            originalTheme = ctx.Theme; colourRevision = ctx.Theme.ColourRevision; layoutRevision = ctx.Theme.LayoutRevision; font = effective;
            theme = ctx.Theme.Clone(); theme.ApplyStyleFont(effective.Alias); theme.ApplyStyleFontSize(effective.Size);
        }
        definition=ctx.Session.DefinitionRevision;paint=render;
    }
    internal bool Paint(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if(paint==null || definition!=ctx.Session.DefinitionRevision || target!=(ctx.Node??ctx.Session.ActiveNode).InteractionScope)return false;
        paint(rect,theme == null ? ctx : ctx.WithTheme(theme));return true;
    }
    internal static void Field(UiLayoutRect rect,UiWidgetContext ctx,string draft, UiColour? ink = null)
    {
        UiThemeDraw.Surface(ctx.Drawing,rect,ctx.Theme,ctx.Theme[UiPaletteRole.ControlSurface],ctx.Theme[UiPaletteRole.Border]);
        float pad=ctx.Theme.Geometry.Spacing;
        UiTextRun text = UiTextFonts.For(ctx, purpose: UiTextPurpose.Editor);
        UiColour colour = ink ?? ctx.Theme.Styles.Resolve(UiStatusTone.Neutral).Text;
        text.Draw(new UiLayoutRect(rect.X+pad,rect.Y,Math.Max(1,rect.Width-pad*2),rect.Height),draft,colour);
        UiTextFonts.RecordPaint(ctx, UiTextPurpose.Editor, text.Font, colour, "retained-display-only");
    }
}
