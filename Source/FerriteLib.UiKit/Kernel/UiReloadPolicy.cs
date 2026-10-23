using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// How the document service schedules the moment it reads and commits a changed file. One object, handed in
/// once, so "why did that reload wait" has one answer instead of a constant per call site.
/// <para>
/// It says nothing about <em>whether</em> files are watched: that is the service's own
/// <see cref="UiDocumentService.AutoWatch"/> and its build-configuration default, because a shipping build
/// must not start file watchers it did not opt into while a development build should not have to remember to
/// ask. This type only answers "when is a signal safe to act on".
/// </para>
/// </summary>
public sealed class UiReloadPolicy
{
    /// <summary>The defaults this round ships.</summary>
    public static readonly UiReloadPolicy Default = new UiReloadPolicy();

    /// <summary>
    /// Creates a policy. Every value is validated: a non-positive quiet period, retry interval or deferral
    /// bound would mean "never wait" or "wait forever", and both of those are defects rather than settings.
    /// </summary>
    public UiReloadPolicy(
        double quietSeconds = 0.25,
        double retrySeconds = 0.5,
        int maxRetryAttempts = 3,
        double maxDeferSeconds = 2.0)
    {
        if (!(quietSeconds > 0)) throw new ArgumentOutOfRangeException(nameof(quietSeconds));
        if (!(retrySeconds > 0)) throw new ArgumentOutOfRangeException(nameof(retrySeconds));
        if (maxRetryAttempts < 0) throw new ArgumentOutOfRangeException(nameof(maxRetryAttempts));
        if (!(maxDeferSeconds > 0)) throw new ArgumentOutOfRangeException(nameof(maxDeferSeconds));

        QuietSeconds = quietSeconds;
        RetrySeconds = retrySeconds;
        MaxRetryAttempts = maxRetryAttempts;
        MaxDeferSeconds = maxDeferSeconds;
    }

    /// <summary>
    /// How long a changed file must stay untouched before its candidate is read. A save arrives as several
    /// events, and this is what turns them into one commit instead of three partial reads.
    /// <para>
    /// <b>The window is a trailing edge.</b> A later signal restarts it: signals at 0.0s and 0.2s with a
    /// 0.25s window produce one read after 0.45s, not one read after 0.25s. That is what makes an editor's
    /// multi-write save coalesce instead of being read half-written.
    /// </para>
    /// <para>
    /// <b>The effective delay is this value plus at most one host frame.</b> The watcher thread posts a
    /// signal and reads no clock, so the window does not open at the instant the file changed: it opens when
    /// the main-thread pump first observes the signal. On a hosted window that is one
    /// <see cref="UiHost.BeginFrame"/> after the watch event, so the observed delay is
    /// <c>[QuietSeconds, QuietSeconds + frame interval]</c>. This is a bound on coalescing, never a
    /// guarantee of latency.
    /// </para>
    /// </summary>
    public double QuietSeconds { get; }

    /// <summary>How long to wait before re-reading after a transient read failure (a lock, a rename).</summary>
    public double RetrySeconds { get; }

    /// <summary>
    /// How many times one signal may be retried before the attempt is reported as a failure and the previous
    /// valid version stays in force. Zero retries is legal; an unbounded retry loop is not.
    /// </summary>
    public int MaxRetryAttempts { get; }

    /// <summary>
    /// The longest a commit may be deferred for the user's own interaction before it is applied anyway. A
    /// deferral with no upper bound is a save that never takes effect.
    /// </summary>
    public double MaxDeferSeconds { get; }
}
