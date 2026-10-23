using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
public sealed partial class UiHost
{
    // Weak discovery references only: lifecycle/state remain owned by the real host/session.
    private static readonly List<WeakReference<UiHost>> observedHosts = new();
    private const int ObservedHostLimit = 512;
    private static int historicalObservationRefusals;
    private static void TrackObservationHost(UiHost host)
    {
        observedHosts.RemoveAll(reference => !reference.TryGetTarget(out UiHost? held) || !held.Session.IsActive);
        if (observedHosts.Count < ObservedHostLimit)
            observedHosts.Add(new WeakReference<UiHost>(host));
        else
            historicalObservationRefusals++;
    }

    private static void ForgetObservationHost(UiHost host)
    {
        observedHosts.RemoveAll(reference => !reference.TryGetTarget(out UiHost? held) || ReferenceEquals(held, host));
    }

    /// <summary>UI-thread read; discovery does not keep windows alive or invoke their business getters.</summary>
    public static UiHostDiscovery ObserveLiveHosts(int maximum = 16)
    {
        if (maximum < 1 || maximum > 128)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        observedHosts.RemoveAll(reference => !reference.TryGetTarget(out UiHost? held) || !held.Session.IsActive);
        var result = new List<UiHostObservation>();
        foreach (var reference in observedHosts)
        {
            if (!reference.TryGetTarget(out UiHost? host) || !host.Session.IsActive)
                continue;
            if (result.Count == maximum)
                break;
            result.Add(new UiHostObservation(host.Session.Identity, host.Source, host.Session.Frame,
                host.DefinitionRevision, null, DateTime.UtcNow, UiObservationStatus.NotSampled,
                "discovery-only; request-host-for-node-facts; discovery-cap=512; historical-cap-refusals=" + historicalObservationRefusals,
                observedHosts.Count > maximum || historicalObservationRefusals > 0,
                Array.AsReadOnly(Array.Empty<UiObservedNode>())));
        }

        return new UiHostDiscovery(Array.AsReadOnly(result.ToArray()), observedHosts.Count > maximum || historicalObservationRefusals > 0, historicalObservationRefusals);
    }

    /// <summary>Stable session identity from discovery, with the same bounded read as an owned host.</summary>
    public static UiHostObservation ObserveLiveHost(int identity, UiObservationBudget? budget = null, string? nodeKey = null)
    {
        observedHosts.RemoveAll(reference => !reference.TryGetTarget(out UiHost? held) || !held.Session.IsActive);
        foreach (var reference in observedHosts)
            if (reference.TryGetTarget(out UiHost? host) && host.Session.IsActive && host.Session.Identity == identity)
                return host.Observe(budget, nodeKey);
        bool absenceUnknown = historicalObservationRefusals > 0;
        UiObservationStatus status = absenceUnknown ? UiObservationStatus.NotSampled : UiObservationStatus.NotFound;
        string reason = absenceUnknown
            ? "discovery-capacity-has-refused-hosts; absence-is-unknown"
            : "no-live-host-with-identity";
        return new UiHostObservation(identity, "", 0, 0, null, DateTime.UtcNow, status, reason,
            absenceUnknown, Array.AsReadOnly(Array.Empty<UiObservedNode>()));
    }
}


