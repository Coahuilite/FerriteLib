using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The library's version axes, deliberately kept apart.
/// <para>
/// The <em>contract</em> axis is <see cref="Api"/>: a hand-authored <see cref="Version"/> that only
/// moves when the public surface a consumer compiles against moves. It is what <see cref="Require"/>
/// compares.
/// </para>
/// <para>
/// The <em>release</em> axis is <c>About/About.xml &lt;modVersion&gt;</c>, which the game parses and
/// exposes at runtime. It moves on every shipped build and says nothing about compatibility. The
/// harness asserts the two agree on major.minor, which is why no custom About.xml tag is needed: the
/// pair cannot drift apart silently.
/// </para>
/// <para>
/// Nothing here reads <c>AssemblyInformationalVersion</c>: it embeds the commit SHA, so it changes on
/// every commit including documentation-only ones and cannot express a contract. Nor does this type
/// reference Verse or UnityEngine - the check has to be callable from the earliest consumer
/// constructor, and it has to stay testable in the stub harness, which offers only BCL and its own
/// stubs at runtime.
/// </para>
/// </summary>
public static class FerriteLibVersion
{
    /// <summary>
    /// Assembly simple name of the carrier. Two mods shipping their own copy of this assembly is the
    /// failure shape <see cref="Require"/> exists to catch.
    /// </summary>
    public const string CarrierAssemblyName = "FerriteLib.UiKit";

    /// <summary>
    /// The contract axis. While <see cref="Version.Major"/> is 0, ANY change to the public surface a
    /// consumer compiles against — addition or break — bumps <see cref="Version.Minor"/>, so a
    /// consumer that pins the minor it was compiled against fails <see cref="Require"/> with a
    /// readable report when the loaded carrier is older than that, instead of surfacing later as a
    /// TypeLoadException at first draw. The rule was tightened on 2026-09-04 after exactly that
    /// failure shape reached a maintainer machine: an additive type (UiPopup) shipped without an Api
    /// bump, the old installed carrier passed Require, and the desync exploded inside UiHost.Draw.
    /// </summary>
    public static readonly Version Api = new Version(0, 2, 0);

    /// <summary>Human-readable identity for logs.</summary>
    public static string Describe()
    {
        return Api.ToString();
    }

    /// <summary>
    /// Checks the FerriteLib that actually got loaded into this process against one consumer's needs.
    /// Never throws on a mismatch: a prerequisite problem has to be reportable by the consumer, not
    /// turn into a load-time exception inside somebody else's mod.
    /// </summary>
    /// <param name="minimumInclusive">Lowest API version the consumer was compiled and verified against.</param>
    /// <param name="maximumExclusive">First API version the consumer has NOT been verified against.</param>
    /// <param name="consumerPackageId">Caller's packageId, for attribution in the report.</param>
    /// <param name="diagnostic">One summary line, one line per loaded copy, and one line naming the
    /// page trees this process has actually built. Safe to log whole.</param>
    public static bool Require(Version minimumInclusive, Version maximumExclusive, string consumerPackageId, out string diagnostic)
    {
        if (minimumInclusive == null) throw new ArgumentNullException(nameof(minimumInclusive));
        if (maximumExclusive == null) throw new ArgumentNullException(nameof(maximumExclusive));
        if (maximumExclusive <= minimumInclusive)
        {
            throw new ArgumentException("The accepted API range must end above where it starts.", nameof(maximumExclusive));
        }

        if (!Evaluate(minimumInclusive, maximumExclusive, consumerPackageId, CollectCarrierCopies(), out diagnostic))
        {
            diagnostic = diagnostic + Environment.NewLine + DescribeHosts();
            return false;
        }

        string hosts = DescribeHosts();
        if (hosts.Length > 0)
        {
            diagnostic = diagnostic + Environment.NewLine + hosts;
        }

        return true;
    }

    /// <summary>
    /// One line about which page trees this process actually built, from <see cref="UiHostLedger"/>.
    /// Empty when nothing has been built yet, which is the shape that answers "declared the
    /// prerequisite, does not use the runtime" without any handshake API between the two mods.
    /// </summary>
    private static string DescribeHosts()
    {
        string ledger = UiHostLedger.Describe();
        if (ledger.Length == 0)
        {
            return "  page trees built in this process: none yet";
        }

        return ledger;
    }

    /// <summary>
    /// One carrier assembly as seen from this process. Kept as plain data so the duplicate branch is
    /// reachable by a test: there is no way to get two same-identity assemblies loaded on demand, so
    /// without this seam the most important thing the guard reports would never have been executed.
    /// </summary>
    internal readonly struct CarrierCopy
    {
        internal CarrierCopy(string assemblyVersion, string location, bool isThisCopy)
        {
            AssemblyVersion = assemblyVersion;
            Location = location;
            IsThisCopy = isThisCopy;
        }

        internal string AssemblyVersion { get; }

        internal string Location { get; }

        /// <summary>True for the copy whose <see cref="Api"/> this process actually resolved.</summary>
        internal bool IsThisCopy { get; }
    }

    /// <summary>The decision, separated from how the facts were gathered. Internal test seam.</summary>
    internal static bool Evaluate(Version minimumInclusive, Version maximumExclusive, string consumerPackageId, List<CarrierCopy> copies, out string diagnostic)
    {
        if (copies == null) throw new ArgumentNullException(nameof(copies));

        string consumer = string.IsNullOrEmpty(consumerPackageId) ? "(unknown consumer)" : consumerPackageId;

        var report = new StringBuilder();
        report.Append("FerriteLib [")
              .Append(consumer)
              .Append("] expects API in [").Append(minimumInclusive).Append(", ").Append(maximumExclusive)
              .Append("), loaded API is ").Append(Api).Append('.');

        bool ok = Api >= minimumInclusive && Api < maximumExclusive;
        if (!ok)
        {
            report.Append(" MISMATCH.");
        }

        if (copies.Count > 1)
        {
            // RimWorld installs one global AssemblyResolve for mod assemblies, so with two copies on
            // disk the reference binds by load order and the copy that lost never finds out. Listing
            // every path is the only way the surviving one can say what happened.
            ok = false;
            report.Append(" DUPLICATE CARRIER: ").Append(copies.Count)
                  .Append(" copies of ").Append(CarrierAssemblyName)
                  .Append(" are loaded; exactly one mod may ship it.");
        }

        for (int i = 0; i < copies.Count; i++)
        {
            CarrierCopy copy = copies[i];
            report.AppendLine();
            report.Append("  copy[").Append(i).Append("] assemblyVersion=").Append(copy.AssemblyVersion)
                  .Append(" api=").Append(copy.IsThisCopy ? Api.ToString() : "(not this one)")
                  .Append(" from=").Append(copy.Location);
        }

        diagnostic = report.ToString();
        return ok;
    }

    private static List<CarrierCopy> CollectCarrierCopies()
    {
        var found = new List<CarrierCopy>();
        Assembly self = typeof(FerriteLibVersion).Assembly;

        Assembly[] loaded;
        try
        {
            loaded = AppDomain.CurrentDomain.GetAssemblies();
        }
        catch (Exception)
        {
            // No domain to enumerate (an exotic host). Report on the self copy alone.
            found.Add(new CarrierCopy(DescribeSelfVersion(self), "(unavailable)", true));
            return found;
        }

        for (int i = 0; i < loaded.Length; i++)
        {
            Assembly? candidate = loaded[i];
            if (candidate == null) continue;
            if (!string.Equals(candidate.GetName().Name, CarrierAssemblyName, StringComparison.OrdinalIgnoreCase)) continue;

            bool isSelf = ReferenceEquals(candidate, self);
            if (isSelf)
            {
                // Self first, so copy[0] is always the one whose API number can be stated truthfully.
                found.Insert(0, new CarrierCopy(DescribeSelfVersion(candidate), DescribeLocation(candidate), true));
            }
            else
            {
                found.Add(new CarrierCopy(DescribeSelfVersion(candidate), DescribeLocation(candidate), false));
            }
        }

        if (found.Count == 0)
        {
            found.Add(new CarrierCopy(DescribeSelfVersion(self), DescribeLocation(self), true));
        }

        return found;
    }

    private static string DescribeSelfVersion(Assembly assembly)
    {
        try
        {
            return assembly.GetName().Version?.ToString() ?? "(none)";
        }
        catch (Exception)
        {
            return "(unreadable)";
        }
    }

    private static string DescribeLocation(Assembly assembly)
    {
        try
        {
            string? location = assembly.IsDynamic ? null : assembly.Location;
            // Explicit null/empty test: net472's string.IsNullOrEmpty carries no [NotNullWhen(false)],
            // so flow analysis cannot narrow through it and returning it trips CS8603.
            if (location == null || location.Length == 0)
            {
                return "(in-memory)";
            }

            return location;
        }
        catch (Exception)
        {
            return "(unreadable)";
        }
    }
}
