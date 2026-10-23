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
    internal void SetMeasure(Func<UiWidgetContext,UiMeasurement> calculate) => measure=calculate;
    internal UiMeasurement? Measure(UiWidgetContext ctx)
        => paint!=null && definition==ctx.Session.DefinitionRevision && target==(ctx.Node??ctx.Session.ActiveNode).InteractionScope ? measure?.Invoke(ctx) : null;
    internal void Capture(UiWidgetContext ctx, Action<UiLayoutRect,UiWidgetContext> render)
    {
        target=(ctx.Node??ctx.Session.ActiveNode).InteractionScope;
        definition=ctx.Session.DefinitionRevision;paint=render;
    }
    internal bool Paint(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if(paint==null || definition!=ctx.Session.DefinitionRevision || target!=(ctx.Node??ctx.Session.ActiveNode).InteractionScope)return false;
        paint(rect,ctx);return true;
    }
    internal static void Field(UiLayoutRect rect,UiWidgetContext ctx,string draft)
    {
        UiThemeDraw.Surface(ctx.Drawing,rect,ctx.Theme,ctx.Theme[UiPaletteRole.ControlSurface],ctx.Theme[UiPaletteRole.Border]);
        float pad=ctx.Theme.Geometry.Spacing;
        UiThemeDraw.Label(ctx.Drawing,new UiLayoutRect(rect.X+pad,rect.Y,Math.Max(1,rect.Width-pad*2),rect.Height),draft,ctx.Theme,ctx.Theme[UiPaletteRole.Foreground],ctx.Theme.Styles.Font);
    }
}



