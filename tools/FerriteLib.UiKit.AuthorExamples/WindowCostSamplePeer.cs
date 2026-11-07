using System;
using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.AuthorExamples;

/// <summary>Adapt an actual window's existing request/read functions; retain no second sample state here.</summary>
public sealed class WindowCostSamplePeer : IUiCostSampleProvider
{
    private readonly Func<int, bool> request;
    private readonly Func<UiCostSampleSnapshot> read;
    public WindowCostSamplePeer(Func<int, bool> request, Func<UiCostSampleSnapshot> read)
    {
        this.request = request ?? throw new ArgumentNullException(nameof(request));
        this.read = read ?? throw new ArgumentNullException(nameof(read));
    }
    public bool RequestSample(UiCostSampleRequest sample) => request(sample.Passes);
    public UiCostSampleSnapshot ReadSample() => read();
}
