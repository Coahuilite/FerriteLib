using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Optional complete tracked-binding demand of a custom widget. Old widgets need no declaration
/// and retain conservative all-key synchronization while visible. This does not enroll ordinary getters.</summary>
public interface IUiPresentationDemand
{
    IEnumerable<string> PresentationKeys(UiWidgetContext context);
}


