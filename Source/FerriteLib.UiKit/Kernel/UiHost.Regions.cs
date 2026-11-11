using System;

namespace FerriteLib.UiKit.Kernel;

public sealed partial class UiHost
{
    private UiRegionRuntime? regions;
    internal UiRegionAttachment AttachRegion(string root, Func<UiRegionSnapshot?> capture, UiRegionReadMap reads)
    {
        RequireRegionOwner();
        return (regions ??= new UiRegionRuntime(this, session)).Attach(root, capture, reads, manifest);
    }
    internal void RequireRegionOwner()
    {
        RequireToolThread(); RequireOutsideBudget();
        if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
    }
    internal void RegionInputsChanged()
    { cycleSnapshot = null; engine.RequestInputValidation(); }
}
