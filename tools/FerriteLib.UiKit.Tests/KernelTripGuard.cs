using System;
using System.Collections.Generic;
using System.Text;

using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The rule a page-drawing lane owes its own coverage claim: after a frame, no element may be sitting in
/// recovery.
/// <para>
/// Why this is a guard rather than a convention (measured 2026-09-12). A consumer's page called
/// <c>rect.ContractedBy(8f)</c>, a member the harness's Verse stub did not declare, so the call threw
/// <c>TypeLoadException</c> at JIT time. The session guard did exactly what it is designed to do - replaced
/// that element with a recovery band and kept the frame alive - and the consumer's lane, which asserted
/// only that the session was still <c>IsActive</c>, stayed green. In the game the same code draws, so the
/// harness was reporting coverage it did not have: the product path under test never ran, and the only
/// trace was a trip log nobody read.
/// </para>
/// <para>
/// So a lane that draws a page asserts this: <c>"the frame survived"</c> is not <c>"the element drew"</c>.
/// A lane that trips on purpose passes <c>deliberateTrips: true</c> and keeps its own assertions about the
/// recovery - the switch exists so the choice is visible in the call, never an omission.
/// </para>
/// </summary>
internal static class KernelTripGuard
{
    /// <summary>
    /// Fails the calling lane when any element ended the frame in recovery.
    /// <paramref name="deliberateTrips"/> is the explicit switch for the lanes that test recovery itself.
    /// </summary>
    internal static void ExpectNoTrips(UiSession session, string lane, bool deliberateTrips = false)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (deliberateTrips) return;
        if (session.TrippedNodes.Count == 0) return;

        throw new Exception(Describe(session, lane));
    }

    /// <summary>
    /// True when the session holds at least one element in recovery. The positive-control surface: a lane
    /// asserts this before asserting that <see cref="ExpectNoTrips"/> reports it, so the guard itself is
    /// never the thing under test and the thing being measured.
    /// </summary>
    internal static bool AnyTripped(UiSession session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        return session.TrippedNodes.Count > 0;
    }

    /// <summary>The failure text: which lane, which elements, what each one threw, and what it means.</summary>
    internal static string Describe(UiSession session, string lane)
    {
        var text = new StringBuilder();
        text.Append(lane).Append(": ").Append(session.TrippedNodes.Count)
            .Append(" element(s) ended the frame in recovery, so the page was not fully drawn. ")
            .Append("A trip means the element was replaced by its recovery band; in a harness that usually "
                + "means the runtime stub is missing a game member the product called, so the product path "
                + "under test never ran. If the trip is deliberate, pass deliberateTrips: true.");

        foreach (UiNode node in session.TrippedNodes)
        {
            text.Append(Environment.NewLine).Append("  ").Append(node.Path);
            if (session.TryGetTripLog(node, out string logged))
            {
                text.Append(" :: ").Append(logged);
            }
        }

        return text.ToString();
    }
}
