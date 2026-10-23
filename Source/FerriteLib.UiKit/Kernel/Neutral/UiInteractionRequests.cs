using System;
namespace FerriteLib.UiKit.Kernel;

/// <summary>One request boundary for every presentation. Callback success is not business acceptance.</summary>
public static class UiInteractionRequests
{
    public static void Activation(UiInteractionTarget target, UiInteractionResult result, Action request)
    {
        if (!result.Request) return;
        try { request(); }
        catch (Exception ex) { throw Failure(target, ex); }
    }
    public static void Range(UiInteractionTarget target, UiInteractionResult result, Action<float> request)
    {
        if (!result.Request) return;
        try { request(result.Value); }
        catch (Exception ex) { throw Failure(target, ex); }
    }
    private static InvalidOperationException Failure(UiInteractionTarget target, Exception ex)
        => new UiRequestFailure("Interaction request failed for node '" + target.Node + "', target '" + target.TargetScope + "'. Side effects may already have occurred; the request is not retried.", ex);
}


