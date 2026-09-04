using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Contract tests for the two version axes and the duplicate-carrier guard. These run with no Verse
/// and no UnityEngine stubs because <see cref="FerriteLib.UiKit.Kernel.FerriteLibVersion"/> is deliberately
/// BCL-only - which is also what lets a consumer call it from its earliest constructor.
/// </summary>
internal static class FerriteLibVersionTests
{
    private const string Carrier = FerriteLib.UiKit.Kernel.FerriteLibVersion.CarrierAssemblyName;

    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Accepts a range that covers the loaded API", VerifyAcceptsCoveringRange);
        failures += Run("Rejects a range entirely above the loaded API", VerifyRejectsTooNewRange);
        failures += Run("Rejects a range entirely below the loaded API", VerifyRejectsTooOldRange);
        failures += Run("Inverted range is a caller error, not a silent pass", VerifyInvertedRangeThrows);
        failures += Run("Exactly one carrier copy is loaded here", VerifySingleCarrierCopy);
        failures += Run("Two carrier copies are reported as a duplicate", VerifyDuplicateCopiesDetected);
        failures += Run("A range mismatch is never labelled a duplicate", VerifyRangeMismatchIsNotDuplicate);
        failures += Run("A consumer compiled above the loaded carrier gets a named MISMATCH, not a pass", VerifyDesyncReportNamesLoadedApi);
        failures += Run("No copies still yields a range verdict", VerifyEmptyCopyListIsSafe);
        failures += Run("Contract axis agrees with About.xml release axis", VerifyApiMatchesModVersion);
        failures += Run("Build axis in the csproj matches the other two axes", VerifyBuildAxisMatchesContract);
        failures += Run("Carrier name matches this assembly", VerifyCarrierName);
        return failures;
    }

    private static int Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.Message);
            return 1;
        }
    }

    private static void VerifyAcceptsCoveringRange()
    {
        Version min = new Version(FerriteLib.UiKit.Kernel.FerriteLibVersion.Api.Major, 0, 0);
        Version max = new Version(min.Major + 1, 0, 0);
        if (!FerriteLib.UiKit.Kernel.FerriteLibVersion.Require(min, max, "coahuilite.testconsumer", out string diagnostic))
        {
            throw new Exception("Own API must satisfy [major.0, major+1). Got: " + diagnostic);
        }

        if (diagnostic.IndexOf(FerriteLib.UiKit.Kernel.FerriteLibVersion.Api.ToString(), StringComparison.Ordinal) < 0)
        {
            throw new Exception("Accepted report must still state the loaded API, for post-mortem logs");
        }

        if (diagnostic.IndexOf("coahuilite.testconsumer", StringComparison.Ordinal) < 0)
        {
            throw new Exception("Report must attribute which consumer asked");
        }
    }

    private static void VerifyRejectsTooNewRange()
    {
        Version future = new Version(FerriteLib.UiKit.Kernel.FerriteLibVersion.Api.Major + 2, 0, 0);
        if (FerriteLib.UiKit.Kernel.FerriteLibVersion.Require(future, new Version(future.Major + 1, 0, 0), "test", out string diagnostic))
        {
            throw new Exception("A range above the loaded API must not be accepted");
        }

        if (diagnostic.IndexOf("MISMATCH", StringComparison.Ordinal) < 0)
        {
            throw new Exception("A rejected range must say why: " + diagnostic);
        }
    }

    private static void VerifyRejectsTooOldRange()
    {
        // Pre-1.0 discipline: minor is a breaking axis, so a consumer pinned to 0.0 must not be told
        // that 0.1 satisfies it just because the major matches.
        if (FerriteLib.UiKit.Kernel.FerriteLibVersion.Api.Major == 0)
        {
            if (FerriteLib.UiKit.Kernel.FerriteLibVersion.Require(new Version(0, 0, 0), new Version(0, 1, 0), "test", out _))
            {
                throw new Exception("While major is 0, a minor below the loaded API must be rejected");
            }
        }
    }

    private static void VerifyInvertedRangeThrows()
    {
        try
        {
            FerriteLib.UiKit.Kernel.FerriteLibVersion.Require(new Version(2, 0, 0), new Version(1, 0, 0), "test", out _);
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new Exception("maxExclusive <= minInclusive is a caller bug and must not pass silently");
    }

    private static void VerifySingleCarrierCopy()
    {
        if (!FerriteLib.UiKit.Kernel.FerriteLibVersion.Require(
                FerriteLib.UiKit.Kernel.FerriteLibVersion.Api,
                new Version(FerriteLib.UiKit.Kernel.FerriteLibVersion.Api.Major, FerriteLib.UiKit.Kernel.FerriteLibVersion.Api.Minor + 1, 0),
                "test",
                out string diagnostic))
        {
            throw new Exception("Exact-API range must pass: " + diagnostic);
        }

        if (diagnostic.IndexOf("DUPLICATE CARRIER", StringComparison.Ordinal) >= 0)
        {
            throw new Exception(
                "More than one copy of " + Carrier + " is loaded in this harness run. That is the real "
                + "failure shape: " + diagnostic);
        }

        // One copy line for one copy. If a second assembly with the carrier name ever reaches the
        // output directory, this count is what notices.
        int copyLines = 0;
        foreach (string line in diagnostic.Split(new char[] { '\n' }))
        {
            if (line.IndexOf("copy[", StringComparison.Ordinal) >= 0) copyLines++;
        }

        if (copyLines != 1)
        {
            throw new Exception("Expected exactly one carrier copy line, saw " + copyLines + " in: " + diagnostic);
        }
    }

    private static void VerifyDuplicateCopiesDetected()
    {
        // Two same-identity assemblies cannot be loaded on demand, so the branch that matters most in
        // production - another mod shipping its own copy - is driven through the internal decision
        // function instead of being left unexecuted.
        var copies = new List<FerriteLib.UiKit.Kernel.FerriteLibVersion.CarrierCopy>
        {
            new FerriteLib.UiKit.Kernel.FerriteLibVersion.CarrierCopy("0.1.0.0", "Mods/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll", true),
            new FerriteLib.UiKit.Kernel.FerriteLibVersion.CarrierCopy("0.1.0.0", "Mods/SomeOtherMod/1.6/Assemblies/FerriteLib.UiKit.dll", false)
        };

        bool ok = FerriteLib.UiKit.Kernel.FerriteLibVersion.Evaluate(
            FerriteLib.UiKit.Kernel.FerriteLibVersion.Api,
            new Version(FerriteLib.UiKit.Kernel.FerriteLibVersion.Api.Major + 1, 0, 0),
            "coahuilite.consumerone",
            copies,
            out string diagnostic);

        if (ok)
        {
            throw new Exception("Two carrier copies must fail the check even when the range matches");
        }

        if (diagnostic.IndexOf("DUPLICATE CARRIER", StringComparison.Ordinal) < 0)
        {
            throw new Exception("The duplicate verdict must say so: " + diagnostic);
        }

        if (diagnostic.IndexOf("2 copies", StringComparison.Ordinal) < 0)
        {
            throw new Exception("The report must state how many copies collided: " + diagnostic);
        }

        if (CountMatches(diagnostic, "copy[") != 2)
        {
            throw new Exception("Every colliding path must be listed, otherwise the fix is guesswork: " + diagnostic);
        }

        if (diagnostic.IndexOf("(not this one)", StringComparison.Ordinal) < 0)
        {
            throw new Exception("The report must distinguish the copy that won binding: " + diagnostic);
        }
    }

    private static void VerifyRangeMismatchIsNotDuplicate()
    {
        var copies = new List<FerriteLib.UiKit.Kernel.FerriteLibVersion.CarrierCopy>
        {
            new FerriteLib.UiKit.Kernel.FerriteLibVersion.CarrierCopy("0.1.0.0", "Mods/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll", true)
        };

        bool ok = FerriteLib.UiKit.Kernel.FerriteLibVersion.Evaluate(
            new Version(9, 0, 0),
            new Version(10, 0, 0),
            "coahuilite.consumerone",
            copies,
            out string diagnostic);

        if (ok) throw new Exception("A range above the loaded API must fail");
        if (diagnostic.IndexOf("MISMATCH", StringComparison.Ordinal) < 0) throw new Exception("Must report MISMATCH");
        if (diagnostic.IndexOf("DUPLICATE", StringComparison.Ordinal) >= 0)
        {
            throw new Exception("The two failure reasons must stay distinguishable: " + diagnostic);
        }
    }

    private static void VerifyEmptyCopyListIsSafe()
    {
        // Defensive path: enumeration can come back empty on an exotic host, and a guard that throws
        // there would take the consumer's load path down with it.
        bool ok = FerriteLib.UiKit.Kernel.FerriteLibVersion.Evaluate(
            FerriteLib.UiKit.Kernel.FerriteLibVersion.Api,
            new Version(FerriteLib.UiKit.Kernel.FerriteLibVersion.Api.Major + 1, 0, 0),
            "",
            new List<FerriteLib.UiKit.Kernel.FerriteLibVersion.CarrierCopy>(),
            out string diagnostic);

        if (!ok) throw new Exception("Range verdict must survive an empty copy list: " + diagnostic);
        if (diagnostic.IndexOf("(unknown consumer)", StringComparison.Ordinal) < 0)
        {
            throw new Exception("An empty packageId must still be attributed as unknown: " + diagnostic);
        }
    }

    private static int CountMatches(string text, string needle)
    {
        int count = 0;
        int at = text.IndexOf(needle, StringComparison.Ordinal);
        while (at >= 0)
        {
            count++;
            at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static void VerifyApiMatchesModVersion()
    {
        // This is the reason no custom About.xml tag is needed: the release axis the game can read is
        // pinned to the contract axis by this assertion, so neither can drift away from the other.
        string aboutPath = Path.Combine(RepoRoot(), "About", "About.xml");
        XmlDocument doc = new XmlDocument();
        doc.Load(aboutPath);
        XmlNode? node = doc.SelectSingleNode("/ModMetaData/modVersion");
        if (node == null)
        {
            throw new Exception("About/About.xml has no <modVersion>; the release axis has no home");
        }

        string modVersion = (node.InnerText ?? "").Trim();
        if (!Version.TryParse(Normalize(modVersion), out Version? parsed))
        {
            throw new Exception("About.xml <modVersion> is not a Version: '" + modVersion + "'");
        }

        Version api = FerriteLib.UiKit.Kernel.FerriteLibVersion.Api;
        if (parsed.Major != api.Major || parsed.Minor != api.Minor)
        {
            throw new Exception(
                "Contract axis " + api + " and release axis " + modVersion
                + " disagree on major.minor. Bump both together or neither.");
        }
    }

    /// <summary>
    /// The third axis: what the SHIPPED ARTIFACT is called. pack-dev.ps1 and pack-github.ps1 both
    /// derive the package label and version.txt from csproj &lt;VersionPrefix&gt;, so if that value drifts
    /// from the contract and release axes the published page announces one number while the DLL inside
    /// the zip reports another - and the two gates that exist for the other axes cannot see it, because
    /// neither reads the csproj. This happened on 2026-09-04: the axes moved to 0.2.0 for the UiPopup
    /// addition while VersionPrefix stayed at 0.1.0, and seven gates stayed green.
    /// </summary>
    private static void VerifyBuildAxisMatchesContract()
    {
        string projectPath = Path.Combine(
            RepoRoot(), "Source", "FerriteLib.UiKit", "FerriteLib.UiKit.csproj");
        if (!File.Exists(projectPath))
        {
            throw new Exception("No csproj to read the build axis from: " + projectPath);
        }

        string text = File.ReadAllText(projectPath);
        string open = "<VersionPrefix>", close = "</VersionPrefix>";
        int at = text.IndexOf(open, StringComparison.Ordinal);
        if (at < 0)
        {
            throw new Exception("csproj carries no <VersionPrefix>; the packaging label has no source");
        }

        int from = at + open.Length;
        int end = text.IndexOf(close, from, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new Exception("csproj <VersionPrefix> is not closed");
        }

        string label = text.Substring(from, end - from).Trim();
        // Exactly one occurrence: two means the packaging scripts' first-match selector is picking
        // arbitrarily between them, which is the same defect class as an unpinned axis.
        if (CountMatches(text, open) != 1)
        {
            throw new Exception(
                "csproj declares <VersionPrefix> " + CountMatches(text, open)
                + " times; the packaging label would come from whichever match is read first");
        }

        if (!Version.TryParse(label, out Version? build))
        {
            throw new Exception("csproj <VersionPrefix> is not a Version: '" + label + "'");
        }

        Version api = FerriteLib.UiKit.Kernel.FerriteLibVersion.Api;
        if (build.Major != api.Major || build.Minor != api.Minor)
        {
            throw new Exception(
                "Build axis " + label + " disagrees with contract axis " + api
                + "; a published package would carry a label the shipped DLL contradicts.");
        }
    }

    private static void VerifyCarrierName()
    {
        string actual = typeof(FerriteLib.UiKit.Kernel.FerriteLibVersion).Assembly.GetName().Name ?? "";
        if (!string.Equals(actual, Carrier, StringComparison.Ordinal))
        {
            throw new Exception("Guard looks for '" + Carrier + "' but lives in '" + actual + "'");
        }
    }

    /// <summary>
    /// The 2026-09-04 incident, at the seam that can actually express it: the decision compares the
    /// range against the Api of the library assembly that is EXECUTING (in production, the loaded
    /// carrier), so "consumer compiled against a newer surface" is a range whose floor sits above the
    /// executing Api. That direction must fail, and the report must name both the loaded Api and the
    /// consumer's floor, because that report is the whole readable-error promise.
    /// </summary>
    private static void VerifyDesyncReportNamesLoadedApi()
    {
        Version api = FerriteLib.UiKit.Kernel.FerriteLibVersion.Api;
        Version compiledAgainst = new Version(api.Major, api.Minor + 1, 0);
        var copies = new List<FerriteLib.UiKit.Kernel.FerriteLibVersion.CarrierCopy>
        {
            new FerriteLib.UiKit.Kernel.FerriteLibVersion.CarrierCopy(api + ".0", "Mods/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll", true)
        };

        bool ok = FerriteLib.UiKit.Kernel.FerriteLibVersion.Evaluate(
            compiledAgainst,
            new Version(api.Major, api.Minor + 2, 0),
            "coahuilite.consumerone",
            copies,
            out string diagnostic);

        if (ok)
        {
            throw new Exception("A consumer whose compiled floor sits above the loaded Api must fail Require");
        }

        if (diagnostic.IndexOf("MISMATCH", StringComparison.Ordinal) < 0
            || diagnostic.IndexOf(api.ToString(), StringComparison.Ordinal) < 0
            || diagnostic.IndexOf(compiledAgainst.ToString(), StringComparison.Ordinal) < 0)
        {
            throw new Exception("The desync report must name MISMATCH, the loaded Api and the consumer floor: " + diagnostic);
        }
    }

    /// <summary>Strips a prerelease suffix (0.1.0-dev) so the release axis can be compared as a Version.</summary>
    private static string Normalize(string version)
    {
        int dash = version.IndexOf('-');
        return dash > 0 ? version.Substring(0, dash) : version;
    }

    private static string RepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && current != null; i++)
        {
            if (File.Exists(Path.Combine(current, "About", "About.xml"))
                && Directory.Exists(Path.Combine(current, "Source", "FerriteLib.UiKit")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        throw new DirectoryNotFoundException("Could not locate the FerriteLib repo root from " + AppContext.BaseDirectory);
    }
}
