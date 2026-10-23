using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Describes current local pointer regions before native Draw, without input, paint or business effects.
/// Reuse the same private geometry as Draw. An empty sequence is decoration/pass-through.
/// This optional capability adds precise overlap arbitration; IUiWidget's existing entry points remain.
/// </summary>
public interface IUiPointerLayout
{
    IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext context, UiLayoutRect bounds);
}


