namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// A snapshot of what the reload scheduler is currently holding, for a diagnostic readout or a test that
/// asserts timing behaviour by observation rather than by reading source text.
/// <para>
/// It is a value: taking it can never mutate the scheduler, and holding it can never stale.
/// </para>
/// </summary>
public readonly struct UiReloadSchedulerState
{
    public UiReloadSchedulerState(int pending, int deferred, int retrying, double oldestPendingSeconds)
    {
        Pending = pending;
        Deferred = deferred;
        Retrying = retrying;
        OldestPendingSeconds = oldestPendingSeconds;
    }

    /// <summary>Signals accepted and waiting for their quiet period to elapse.</summary>
    public int Pending { get; }

    /// <summary>Signals whose quiet period has elapsed but whose commit is deferred for the user's input.</summary>
    public int Deferred { get; }

    /// <summary>Signals being retried after a transient read failure.</summary>
    public int Retrying { get; }

    /// <summary>How long the oldest pending signal has been waiting, in seconds.</summary>
    public double OldestPendingSeconds { get; }
}
