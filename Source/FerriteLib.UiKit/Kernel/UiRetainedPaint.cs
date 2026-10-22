using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

// Adapter-only last successful paint. Never invokes Draw, native input, bindings, editor Observe or requests.
internal interface IUiRetainedDisplay
{
    bool PaintLast(Rect rect, UiWidgetContext context);
    UiMeasurement? MeasureLast(UiWidgetContext context);
}
internal sealed class UiRetainedPaint
{
    private Action<Rect,UiWidgetContext>? paint;
    private string target="";
    private int definition;
    private Func<UiWidgetContext,UiMeasurement>? measure;
    internal void SetMeasure(Func<UiWidgetContext,UiMeasurement> calculate) => measure=calculate;
    internal UiMeasurement? Measure(UiWidgetContext ctx)
        => paint!=null && definition==ctx.Session.DefinitionRevision && target==(ctx.Node??ctx.Session.ActiveNode).InteractionScope ? measure?.Invoke(ctx) : null;
    internal void Capture(UiWidgetContext ctx, Action<Rect,UiWidgetContext> render)
    {
        target=(ctx.Node??ctx.Session.ActiveNode).InteractionScope;
        definition=ctx.Session.DefinitionRevision;paint=render;
    }
    internal bool Paint(Rect rect, UiWidgetContext ctx)
    {
        if(paint==null || definition!=ctx.Session.DefinitionRevision || target!=(ctx.Node??ctx.Session.ActiveNode).InteractionScope)return false;
        paint(rect,ctx);return true;
    }
    internal static void Field(Rect rect,UiWidgetContext ctx,string draft)
    {
        UiThemeDraw.Surface(rect,ctx.Theme,ctx.Theme[UiPaletteRole.ControlSurface],ctx.Theme[UiPaletteRole.Border]);
        float pad=ctx.Theme.Geometry.Spacing;
        UiThemeDraw.Label(new Rect(rect.x+pad,rect.y,Math.Max(1,rect.width-pad*2),rect.height),draft,ctx.Theme,ctx.Theme[UiPaletteRole.Foreground],ctx.Theme.Styles.Font);
    }
}
