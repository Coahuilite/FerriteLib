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
    /// effect: this array and the document change together, in one commit. The 0.8 closeout does not
    /// inherit the historical 0.7 public-addition exemption.
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
        "UiItemBindings",
        "UiItemBindingSchema",
        "UiMeasureInputs",
        "UiMeasurement",
        "IUiTextMetricsEnvironment",
        "IUiMeasureParticipant",
        "UiReactiveRuntime",
        "UiReactiveValue",
        "UiReactiveList",
        "UiReactiveComputed",
        "UiReactiveOwner",
        "UiBindingPresentation",
        "UiPresentationStatus",
        "UiReactiveFault",
        "UiReactiveResponse",
        "IUiPresentationDemand",
        "UiInteractionTarget",
        "UiHitGeometry",
        "UiPointerPhase",
        "UiInteractionEnd",
        "UiInputEligibility",
        "UiPointerSample",
        "UiRange",
        "UiInteractionResult",
        "UiInteractionSession",
        "UiValueOperation",
        "UiEditSession",
        "UiEditParser",
        "IUiEditState",
        "UiNumericEdit",
        "UiDescriptionRole",
        "UiRangeObservation",
        "UiDescription",
        "IUiDescriptiveWidget",
        "IUiLabelledWidget",
        "UiLayoutDiagnostic",
        "UiLayoutSize",
        "UiLayoutPoint",
        "UiLayoutRect",
        "UiLayoutHeight",
        "UiLayoutFrame",
        "UiLayoutChild",
        "UiLayoutOffset",
        "UiLayoutAnchor",
        "UiLayoutPlan",
        "UiLayoutMeasure",
        "UiLayout",
        "UiWidthMode",
        "UiWidthDemand",
        "UiWidthPlan",
        "UiSelectionObservation",
        "UiValueBehavior",
        "UiInteractionRequests",
        "UiPropertyOrigin",
        "UiPropertyDeclaration",
        "UiResolvedProperty",
        "UiPropertyResolver",
        "IUiPointerLayout",
        "UiPointerRegion",
        "UiImageFit",
        "UiImageAlignX",
        "UiImageAlignY",
        "UiImageOptions",
        "UiImageLayout",
        "UiWindowBackground",
        "UiPaletteRole",
        "UiColour",
        "UiPalette",
        "UiDensity",
        "UiSliderLayout",
        "UiElementProperties",
        "UiOption",
        "UiChoice",
        "UiHost",
        "UiValueState",
        "UiElementSpec",
        "UiWidgetContext",
        "IUiBindings",
        "IUiTypedChoices",
        "UiBindings",
        "UiNative",
        "UiTheme",
        "UiThemeDraw",
        "UiFitAudit",
        "UiLayoutManifest",
        "UiLayoutSnapshot",
        "UiWindowHost",
        "UiWindowChrome",
        "UiWindowNotice",
        "UiChartPointChange",
        "UiOverflowReport",
        "UiSurfaceStyle",
        "UiGeometry",
        "UiEmphasis",
        "UiResolvedStyle",
        "UiStyleTable",
        "UiNodeId",
        "UiStyleFallbackReport",
        "UiStyleDocument",
        "UiStyleResolver",
        "UiStyleDeclaration",
        "UiStyleIssue",
        "UiWindowKey",
        "UiWindowOptions",
        "UiWindowCatalog",
        "UiPageWindow",
        "UiFocusPolicy",
        "UiDocumentKind",
        "UiDocumentSource",
        "UiDocumentService",
        "UiReloadReport",
        "UiInvalidation",
        "UiDiagnosticSubscription",
        "UiDiagnosticEvent",
        "UiDiagnosticKind",
        "UiDiagnosticTiming",
        "UiTreeRow",
        "UiRowBand",
        "UiRowBandLayout",
        "UiRowBandActions",
        "IUiMainThread",
        "VerseFerriteMainThread",
        "UiNotifyAdapter",
        "IUiTimeSource",
        "VerseFerriteTimeSource",
        "UiReloadPolicy",
        "UiReloadSchedulerState",
        "UiWidgetDescriptor",
        "UiWidgetCatalog",
        "UiKinds",
        "UiHostInputState",
    };

    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Every public payload type is classified in exactly one tier", VerifyAllPublicTypesAreClassified);
        failures += Run("No tier entry names a type that no longer exists", VerifyNoTierEntryIsStale);
        failures += Run("The stable tier matches the pinned promise", VerifyStableTierIsPinned);
        failures += Run("Stable public usage includes necessary signature dependencies", VerifyStableDependencies);
        failures += Run("Stable signatures and optional defaults match the frozen surface", VerifyStableSignatures);
        failures += Run("Tier comparison fires on a planted unclassified type", VerifyComparisonIsNotVacuous);
        return failures;
    }

    private static bool Advanced(MemberInfo member)
    {
        string type = member.DeclaringType!.Name;
        string name = member.Name.StartsWith("get_", StringComparison.Ordinal) || member.Name.StartsWith("set_", StringComparison.Ordinal) ? member.Name.Substring(4) : member.Name;
        if (type == "UiWidgetContext" && (name == "Session" || name == "Node" || name == "WithNode" || member is ConstructorInfo)) return true;
        if ((type == "UiHost" || type == "UiWindowHost") && name == "Session") return true;
        if (type == "UiHost" && (name == "Observe" || name == "ObserveLiveHosts" || name == "ObserveLiveHost")) return true;
        if (type == "UiDiagnosticSubscription" && new[] { "GeometryEnabled", "GeometryOverlay", "DumpGeometry", "TryGetGeometrySnapshot" }.Contains(name)) return true;
        if (type == "UiNative" && (name == "Trace" || member is MethodBase method && method.GetParameters().Any(p => p.ParameterType == typeof(UiSession)))) return true;
        return false;
    }
    internal static void WriteStableSurface(string path) => File.WriteAllLines(path, StableSurface());
    private static string[] StableSurface()
    {
        var tiers = ReadTiers(); var lines = new List<string>();
        foreach (Type type in typeof(UiHost).Assembly.GetExportedTypes().Where(t => tiers.TryGetValue(TierName(t), out string? tier) && tier == StableHeading))
        {
            lines.Add("TYPE " + type.FullName + " : " + type.BaseType);
            foreach (Type contract in type.GetInterfaces().Where(t => t.IsPublic)) lines.Add(type.FullName + " implements " + contract);
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (Advanced(member)) continue;
                if (member is MethodBase method && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly))
                {
                    string defaults = string.Join(",", method.GetParameters().Where(p => p.HasDefaultValue).Select(p => p.Name + "=" + (p.DefaultValue == null ? "null" : Convert.ToString(p.DefaultValue, System.Globalization.CultureInfo.InvariantCulture))));
                    lines.Add(type.FullName + " " + method + (defaults.Length == 0 ? "" : " defaults[" + defaults + "]"));
                }
                if (member is FieldInfo field && (field.IsPublic || field.IsFamily))
                    lines.Add(type.FullName + " " + field + (field.IsLiteral ? "=" + Convert.ToString(field.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture) : ""));
            }
        }
        return lines.Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray();
    }
    private static void VerifyStableSignatures()
    {
        string path = Path.Combine(RepoRoot(), "docs", "development", "0.8", "stable-api.txt");
        string[] expected = File.ReadAllLines(path); string[] actual = StableSurface();
        string[] missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
        string[] added = actual.Except(expected, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0 || added.Length != 0)
            throw new Exception("Stable signature change requires an explicit compatible migration/minor decision. Missing: " + string.Join("; ", missing) + " Added: " + string.Join("; ", added));
    }
    private static IEnumerable<Type> Dependencies(Type type)
    {
        if (type.HasElementType) { foreach (var item in Dependencies(type.GetElementType()!)) yield return item; yield break; }
        if (type.IsGenericParameter) yield break;
        yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        if (type.IsGenericType) foreach (Type argument in type.GetGenericArguments()) foreach (var item in Dependencies(argument)) yield return item;
    }
    private static string TierName(Type type) => StripArity(type.Name);
    private static void VerifyStableDependencies()
    {
        var tiers = ReadTiers(); Assembly payload = typeof(UiHost).Assembly;
        var errors = new List<string>();
        void Need(Type type, string location)
        {
            foreach (Type dependency in Dependencies(type))
                if (dependency.Assembly == payload && (!tiers.TryGetValue(TierName(dependency), out string? tier) || tier != StableHeading))
                    errors.Add(location + " requires " + dependency.FullName);
        }
        foreach (Type type in payload.GetExportedTypes().Where(t => tiers.TryGetValue(TierName(t), out string? tier) && tier == StableHeading))
        {
            if (type.BaseType != null) Need(type.BaseType, type.Name + " base");
            foreach (Type contract in type.GetInterfaces()) if (contract.IsPublic) Need(contract, type.Name + " interface");
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (Advanced(member)) continue;
                if (member is MethodBase method && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly))
                {
                    if (method is MethodInfo returning) Need(returning.ReturnType, type.Name + "." + member.Name);
                    foreach (var argument in method.GetParameters()) Need(argument.ParameterType, type.Name + "." + member.Name + "(" + argument.Name + ")");
                }
                if (member is FieldInfo field && (field.IsPublic || field.IsFamily)) Need(field.FieldType, type.Name + "." + field.Name);
            }
        }
        if (errors.Count != 0) throw new Exception(string.Join("; ", errors));
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
                + "] - a type gains or loses the no-break promise only through a decision in this array too.");
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

    /// <summary>
    /// Type names the payload exports, nested ones dotted, order irrelevant, de-duplicated. A generic type's
    /// <c>Name</c> carries an arity suffix (<c>UiChoice`1</c>) which is STRIPPED here: the tier document is
    /// prose whose entries are read back between two backticks, so an entry can never spell the suffix, and two
    /// types differing only by arity would be one classification decision in any case. The generic form is
    /// written out inside the entry's own sentence instead.
    /// </summary>
    private static List<string> PublicTypeNames()
    {
        Assembly payload = typeof(FerriteLibVersion).Assembly;
        return payload.GetExportedTypes()
            .Where(type => type.Namespace != null && type.Namespace.StartsWith("FerriteLib.UiKit", StringComparison.Ordinal))
            .Select(type => type.IsNested && type.DeclaringType != null
                ? StripArity(type.DeclaringType.Name) + "." + StripArity(type.Name)
                : StripArity(type.Name))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The name without the arity suffix a generic type's <c>Name</c> carries.</summary>
    private static string StripArity(string name)
    {
        int tick = name.IndexOf('`');
        return tick < 0 ? name : name.Substring(0, tick);
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
