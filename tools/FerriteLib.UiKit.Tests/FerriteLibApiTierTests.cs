using FerriteLib.UiKit.Kernel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The public-API tier list is the only place "we will not break you" is written down, so it is enforced
/// rather than trusted: every public type in the payload must appear in exactly one tier, no tier entry may
/// name a type that has disappeared, and the stable tier is additionally pinned here so that promoting or
/// demoting a type takes two deliberate edits - one in the document, one in this lane.
/// <para>
/// The shape is borrowed from the precedent this library can see: the nearest comparable modding UI library
/// keeps a hand-maintained stable/public-unstable/internal tier list with no binary-compatibility tool, and
/// it holds because the list is short enough to read. A hand-kept list without a guard drifts the way
/// <c>UiWidgetRegistry.Clear</c> did when it went internal in 0.3.0 and no lane noticed.
/// </para>
/// </summary>
internal static class FerriteLibApiTierTests
{
    private const string TierDocRelativePath = "docs/api-tiers.md";

    private const string StableHeading = "## Stable";
    private const string UnstableHeading = "## Public-unstable";
    private const string InternalizeHeading = "## Internalize-candidate";

    /// <summary>
    /// The promise, pinned. Additions to the library surface that belong here are a decision, not a side
    /// effect: this array and the document change together, in the same commit as the minor bump.
    /// </summary>
    private static readonly string[] PinnedStableTier =
    {
        "FerriteLibVersion",
        "ITextMetrics",
        "VerseFerriteTextMetrics",
        "UiFont",
        "UiKitFonts",
        "IUiTranslation",
        "IUiWidget",
        "UiWidgetRegistry",
        "UiContractException",
        "UiUnknownWidgetKindException",
        "UiStatusTone",
        "UiOverflowAxis",
    };

    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Every public payload type is classified in exactly one tier", VerifyAllPublicTypesAreClassified);
        failures += Run("No tier entry names a type that no longer exists", VerifyNoTierEntryIsStale);
        failures += Run("The stable tier matches the pinned promise", VerifyStableTierIsPinned);
        failures += Run("Tier comparison fires on a planted unclassified type", VerifyComparisonIsNotVacuous);
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
            Console.WriteLine("  FAIL: " + name + " - " + ex.Message);
            return 1;
        }
    }

    private static void VerifyAllPublicTypesAreClassified()
    {
        Dictionary<string, string> tiers = ReadTiers();
        List<string> missing = PublicTypeNames().Where(name => !tiers.ContainsKey(name)).ToList();
        if (missing.Count > 0)
        {
            throw new Exception(missing.Count + " public type(s) have no tier in " + TierDocRelativePath + ": "
                + string.Join(", ", missing)
                + " - classify them (stable / public-unstable / internalize-candidate) in the same commit.");
        }
    }

    private static void VerifyNoTierEntryIsStale()
    {
        HashSet<string> present = new HashSet<string>(PublicTypeNames(), StringComparer.Ordinal);
        List<string> stale = ReadTiers().Keys.Where(name => !present.Contains(name)).ToList();
        if (stale.Count > 0)
        {
            // A tier entry whose type is gone is a lost boundary, not a tidy document: it means the surface
            // changed without the promise being re-read.
            throw new Exception(stale.Count + " tier entr(y/ies) name no public type any more: "
                + string.Join(", ", stale) + " - they were internalised, renamed or deleted; update the tier list too.");
        }
    }

    private static void VerifyStableTierIsPinned()
    {
        Dictionary<string, string> tiers = ReadTiers();
        List<string> documented = tiers
            .Where(pair => string.Equals(pair.Value, StableHeading, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        List<string> pinned = PinnedStableTier.OrderBy(name => name, StringComparer.Ordinal).ToList();

        List<string> added = documented.Where(name => !pinned.Contains(name, StringComparer.Ordinal)).ToList();
        List<string> removed = pinned.Where(name => !documented.Contains(name, StringComparer.Ordinal)).ToList();
        if (added.Count > 0 || removed.Count > 0)
        {
            throw new Exception("the stable tier moved without a decision here too: +["
                + string.Join(", ", added) + "] -[" + string.Join(", ", removed)
                + "] - a type gains or loses the no-break promise only in the commit that bumps the minor.");
        }
    }

    private static void VerifyComparisonIsNotVacuous()
    {
        // Positive control: the classifier must be able to catch the exact mistake it exists to catch -
        // a public type nobody classified. Without this, a parser that silently reads no tiers (a renamed
        // heading, a moved document) would report a clean library.
        List<string> publicNames = PublicTypeNames().ToList();
        Dictionary<string, string> tiers = ReadTiers();
        if (tiers.Count < publicNames.Count)
        {
            throw new Exception("tier document classifies " + tiers.Count + " of " + publicNames.Count
                + " public types; the classification lane names them - this control only proves the reader "
                + "and the reflection set are looking at the same assembly.");
        }

        string planted = publicNames[0] + "PlantedUnclassified";
        List<string> missing = publicNames.Concat(new[] { planted })
            .Where(name => !tiers.ContainsKey(name))
            .ToList();
        if (missing.Count != 1 || !string.Equals(missing[0], planted, StringComparison.Ordinal))
        {
            throw new Exception("the comparison reported [" + string.Join(", ", missing)
                + "] for a single planted unclassified type; it must report exactly that type.");
        }

        // And the stale direction: a tier entry naming a type that has gone must be reported, not skipped.
        if (!publicNames.Contains(tiers.Keys.First()))
        {
            throw new Exception("a tier entry names a type the payload does not export: the two lanes see "
                + "different type sets, so the check is comparing against the wrong assembly.");
        }
    }

    /// <summary>Type names the payload exports, nested ones dotted, order irrelevant.</summary>
    private static List<string> PublicTypeNames()
    {
        Assembly payload = typeof(FerriteLibVersion).Assembly;
        return payload.GetExportedTypes()
            .Where(type => type.Namespace != null && type.Namespace.StartsWith("FerriteLib.UiKit", StringComparison.Ordinal))
            .Select(type => type.IsNested && type.DeclaringType != null
                ? type.DeclaringType.Name + "." + type.Name
                : type.Name)
            .ToList();
    }

    /// <summary>Type name to the heading of the section that lists it.</summary>
    private static Dictionary<string, string> ReadTiers()
    {
        string doc = Path.Combine(RepoRoot(), TierDocRelativePath);
        if (!File.Exists(doc))
        {
            throw new FileNotFoundException("the tier document is gone, so the API promise has no home", doc);
        }

        var tiers = new Dictionary<string, string>(StringComparer.Ordinal);
        string section = "";
        foreach (string line in File.ReadAllLines(doc))
        {
            string trimmed = TrimLeading(line);
            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                section = trimmed.Trim();
                continue;
            }

            if (!string.Equals(section, StableHeading, StringComparison.Ordinal)
                && !string.Equals(section, UnstableHeading, StringComparison.Ordinal)
                && !string.Equals(section, InternalizeHeading, StringComparison.Ordinal))
            {
                continue;
            }

            if (!trimmed.StartsWith("- `", StringComparison.Ordinal)) continue;
            int end = trimmed.IndexOf('`', 3);
            if (end < 0) continue;

            string name = trimmed.Substring(3, end - 3);
            if (tiers.ContainsKey(name))
            {
                throw new Exception(name + " is classified in two tiers (" + tiers[name] + " and " + section + ")");
            }

            tiers.Add(name, section);
        }

        if (tiers.Count == 0)
        {
            throw new Exception("no tier entries parsed from " + TierDocRelativePath + " - the document's "
                + "section headings or entry format changed and the guard would now pass vacuously.");
        }

        return tiers;
    }

    /// <summary>
    /// Leading-space strip by index. The parameterless <c>TrimStart()</c> overload is one of the net472
    /// reference-assembly traps this repo has already recorded: it compiles and then throws
    /// <c>MissingMethodException</c> at run time (MEMORY.md, "net472 reference-assembly traps").
    /// </summary>
    private static string TrimLeading(string value)
    {
        int start = 0;
        while (start < value.Length && value[start] == ' ') start++;
        return value.Substring(start);
    }

    private static string RepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && current != null; i++)
        {
            if (Directory.Exists(Path.Combine(current, "Source", "FerriteLib.UiKit"))
                && File.Exists(Path.Combine(current, "About", "About.xml")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        throw new DirectoryNotFoundException("Could not locate the FerriteLib repo root from " + AppContext.BaseDirectory);
    }
}
