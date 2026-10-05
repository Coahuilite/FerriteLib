using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One appearance value the style document could not accept, kept so that fail-soft is not silent: a
/// dropped declaration that leaves no trace is found by a player instead of by its author, which is the
/// weakness this record exists to close.
/// </summary>
public readonly struct UiStyleIssue
{
    public UiStyleIssue(string message, int line = 0, string? elementPath = null)
    {
        Message = message ?? "";
        Line = line;
        ElementPath = string.IsNullOrEmpty(elementPath) ? null : elementPath;
    }

    /// <summary>What was dropped or replaced, in one sentence.</summary>
    public string Message { get; }

    /// <summary>1-based line when the source reported one, otherwise 0.</summary>
    public int Line { get; }

    /// <summary>
    /// The element that DECLARED the dropped value, when the drop belongs to one element rather than to the
    /// document (0.7.x): a scope naming a scheme or density the document does not define is recorded with its
    /// declaring element's path, so the published finding can say <i>where</i> the name was written. A
    /// document-level drop (a bad value inside <c>&lt;Styles&gt;</c>) has no element and stays null, and the
    /// audit surface then attributes it to the style origin as it always has.
    /// </summary>
    public string? ElementPath { get; }

    public override string ToString()
    {
        return Line > 0 ? "line " + Line.ToString(CultureInfo.InvariantCulture) + ": " + Message : Message;
    }
}

/// <summary>
/// One node's own style declarations as the tree carries them - the element's attributes in the layout
/// manifest. <see cref="UiStyleResolver"/> takes a nearest-first list of these (the element itself, then
/// its containers outward, then the document's page level) and applies the written precedence chain.
/// </summary>
public readonly struct UiStyleDeclaration
{
    public UiStyleDeclaration(string? scheme = null, string? density = null, UiStatusTone? tone = null, UiEmphasis? emphasis = null)
    {
        Scheme = string.IsNullOrEmpty(scheme) ? null : scheme;
        Density = string.IsNullOrEmpty(density) ? null : density;
        Tone = tone;
        Emphasis = emphasis;
    }

    /// <summary>Named colour scheme this node adopts; inherited by descendants, nearest wins.</summary>
    public string? Scheme { get; }

    /// <summary>Named density this node adopts; inherited by descendants, nearest wins.</summary>
    public string? Density { get; }

    /// <summary>Role tag for this node only; roles never inherit (see <see cref="UiStyleResolver"/>).</summary>
    public UiStatusTone? Tone { get; }

    /// <summary>Compact-versus-form emphasis for this node only; roles never inherit.</summary>
    public UiEmphasis? Emphasis { get; }
}

/// <summary>
/// The parsed style document: named schemes, named densities and the page-level defaults, from either of
/// the two text origins (a standalone <c>&lt;Styles&gt;</c> file, or the <c>&lt;Styles&gt;</c> section of a
/// layout manifest). Both origins call <see cref="Parse"/>, so there is one vocabulary and one validation
/// entry point, and the values loop back into <see cref="UiTheme.Styles"/> rather than into a second
/// resolver.
/// <para>
/// Failure policy, per the ledger's ruling that appearance must never take a page down: everything a style
/// document can get wrong is appearance-class. A malformed document, an unknown element or attribute, an
/// unknown token or an illegal value never throws - the declaration is dropped, the page renders with the
/// values it would have had without it, and the drop is recorded in <see cref="Issues"/> so a lane and a
/// host can both see it. (The one fail-closed case is the layout manifest's own XML: if that text does not
/// parse, the page tree itself is broken, which is the existing structural ladder.)
/// </para>
/// <para>
/// One consequence of the two origins is worth stating plainly, because it is the real price of letting a
/// style section live inside the layout manifest: that section is part of one XML text, so a
/// <c>&lt;Styles&gt;</c> section which is not well-formed makes the whole manifest not well-formed - which
/// is the existing page-level failure (a page tree that cannot be read), not an appearance fallback. The
/// identical style content handed in as a standalone document is appearance-class and soft. This asymmetry
/// is recorded rather than papered over: it is co-location's cost, and the standalone file is what
/// appearance-only authoring should use.
/// </para>
/// <para>
/// The instance is immutable once constructed; issues are bounded so a pathological document cannot grow
/// the record without limit.
/// </para>
/// </summary>
public sealed class UiStyleDocument
{
    /// <summary>Distinct issues recorded before the document stops recording. Bounds a pathological file.</summary>
    public const int MaxIssues = 32;

    /// <summary>A document that declares nothing, so every source falls through to the theme.</summary>
    public static UiStyleDocument Empty { get; } = new UiStyleDocument(null, null, new Dictionary<string, SchemeDefinition>(StringComparer.Ordinal), new Dictionary<string, DensityDefinition>(StringComparer.Ordinal), new List<UiStyleIssue>());

    private readonly Dictionary<string, SchemeDefinition> schemes;
    private readonly Dictionary<string, DensityDefinition> densities;
    private readonly List<UiStyleIssue> issues;

    private UiStyleDocument(
        string? defaultScheme,
        string? defaultDensity,
        Dictionary<string, SchemeDefinition> schemes,
        Dictionary<string, DensityDefinition> densities,
        List<UiStyleIssue> issues)
    {
        DefaultScheme = defaultScheme;
        DefaultDensity = defaultDensity;
        this.schemes = schemes;
        this.densities = densities;
        this.issues = issues;
    }

    /// <summary>Page-level scheme every scope starts from, or null when the document declares none.</summary>
    public string? DefaultScheme { get; }

    /// <summary>Page-level density every scope starts from, or null when the document declares none.</summary>
    public string? DefaultDensity { get; }

    /// <summary>True when nothing was declared at all (no schemes, no densities, no page defaults).</summary>
    public bool IsEmpty => schemes.Count == 0 && densities.Count == 0 && DefaultScheme == null && DefaultDensity == null;

    /// <summary>Everything the document dropped, in the order it was found. Empty for a correct document.</summary>
    public IReadOnlyList<UiStyleIssue> Issues => issues;

    /// <summary>
    /// True when the document was refused as a whole - unreadable XML, a foreign root, a missing or foreign
    /// <c>Schema</c>, or a file the loader could not read at all. The document service reads this to keep
    /// the last valid version rather than committing an empty one.
    /// <para>
    /// A document that parsed and then dropped individual declarations is deliberately NOT structurally
    /// invalid: that fail-soft path is the style policy, the readable rest still applies, and the drops are
    /// already on <see cref="Issues"/>. Only the whole-document refusal is a version the service refuses.
    /// </para>
    /// </summary>
    internal bool StructurallyInvalid { get; private set; }

    /// <summary>Declared scheme names, for diagnostics and authoring tools.</summary>
    public IReadOnlyCollection<string> SchemeNames => schemes.Keys;

    /// <summary>Declared density names, for diagnostics and authoring tools.</summary>
    public IReadOnlyCollection<string> DensityNames => densities.Keys;

    /// <summary>
    /// The one parser. Root must be <c>&lt;Styles Schema="1"&gt;</c>; the layout manifest hands this method
    /// the same element it captured from its own file, which is what keeps the two origins from drifting.
    /// Never throws for anything the document itself can get wrong.
    /// </summary>
    public static UiStyleDocument Parse(string xml)
    {
        if (xml == null) throw new ArgumentNullException(nameof(xml));
        if (xml.Trim().Length == 0) return Empty;

        var issues = new List<UiStyleIssue>();
        XmlDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = true
            };

            using var textReader = new StringReader(xml);
            using XmlReader reader = XmlReader.Create(textReader, settings);
            document = new XmlDocument { XmlResolver = null, PreserveWhitespace = false };
            document.Load(reader);
        }
        catch (XmlException ex)
        {
            issues.Add(new UiStyleIssue(
                "Invalid style XML at line " + ex.LineNumber.ToString(CultureInfo.InvariantCulture)
                + ", position " + ex.LinePosition.ToString(CultureInfo.InvariantCulture) + ": " + ex.Message,
                ex.LineNumber));
            return Invalid(issues);
        }
        catch (InvalidOperationException ex)
        {
            issues.Add(new UiStyleIssue("Invalid style XML: " + ex.Message));
            return Invalid(issues);
        }

        XmlElement? root = document.DocumentElement;
        var emptySchemes = new Dictionary<string, SchemeDefinition>(StringComparer.Ordinal);
        var emptyDensities = new Dictionary<string, DensityDefinition>(StringComparer.Ordinal);

        if (root == null || !string.Equals(root.Name, "Styles", StringComparison.Ordinal))
        {
            issues.Add(new UiStyleIssue("The style document root must be <Styles>; found <" + (root == null ? "(none)" : root.Name) + ">."));
            return Invalid(emptySchemes, emptyDensities, issues);
        }

        string schema = root.GetAttribute("Schema") ?? "";
        if (!string.Equals(schema, "1", StringComparison.Ordinal))
        {
            issues.Add(new UiStyleIssue(schema.Length == 0
                ? "The style document is missing the required Schema=\"1\" on <Styles>."
                : "Unsupported <Styles> Schema '" + schema + "'; expected '1'."));
            return Invalid(emptySchemes, emptyDensities, issues);
        }

        var schemes = new Dictionary<string, SchemeDefinition>(StringComparer.Ordinal);
        var densities = new Dictionary<string, DensityDefinition>(StringComparer.Ordinal);

        foreach (XmlAttribute attribute in root.Attributes)
        {
            if (!IsPageAttribute(attribute.Name))
            {
                Record(issues, "Unknown <Styles> attribute '" + attribute.Name + "'; ignored.");
            }
        }

        foreach (XmlNode child in root.ChildNodes)
        {
            if (child is not XmlElement element) continue;
            switch (element.Name)
            {
                case "Scheme":
                    ReadScheme(element, schemes, issues);
                    break;
                case "Density":
                    ReadDensity(element, densities, issues);
                    break;
                default:
                    Record(issues, "Unknown <Styles> element <" + element.Name + ">; ignored (its declarations do not apply).");
                    break;
            }
        }

        string? defaultScheme = ReadOptionalName(root, "Scheme");
        string? defaultDensity = ReadOptionalName(root, "Density");
        return new UiStyleDocument(defaultScheme, defaultDensity, schemes, densities, issues);
    }

    /// <summary>
    /// The standalone-document loader: the consumer hands a path (nothing here scans a directory, as with
    /// any file the consumer owns), and it is read once. A file that cannot be read or parsed is
    /// appearance-class like every other style failure: the caller gets a document that declares nothing
    /// plus the issue that says why.
    /// </summary>
    public static UiStyleDocument ParseFile(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));

        string xml;
        try
        {
            xml = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or System.Security.SecurityException)
        {
            var issues = new List<UiStyleIssue>();
            Record(issues, "Style document '" + path + "' could not be read: " + ex.Message);
            return Invalid(issues);
        }

        return Parse(xml);
    }

    /// <summary>
    /// The one whole-document refusal, so every branch marks the same way and no caller has to guess which
    /// issue text means "nothing applied" (the issue text is for a human; this flag is for the service).
    /// </summary>
    private static UiStyleDocument Invalid(List<UiStyleIssue> issues)
    {
        return new UiStyleDocument(
            null,
            null,
            new Dictionary<string, SchemeDefinition>(StringComparer.Ordinal),
            new Dictionary<string, DensityDefinition>(StringComparer.Ordinal),
            issues)
        {
            StructurallyInvalid = true
        };
    }

    private static UiStyleDocument Invalid(
        Dictionary<string, SchemeDefinition> schemes,
        Dictionary<string, DensityDefinition> densities,
        List<UiStyleIssue> issues)
    {
        return new UiStyleDocument(null, null, schemes, densities, issues) { StructurallyInvalid = true };
    }

    internal bool TryGetScheme(string name, out SchemeDefinition definition)
    {
        return schemes.TryGetValue(name, out definition!);
    }

    internal bool TryGetDensity(string name, out DensityDefinition definition)
    {
        return densities.TryGetValue(name, out definition!);
    }

    private static void ReadScheme(XmlElement element, Dictionary<string, SchemeDefinition> schemes, List<UiStyleIssue> issues)
    {
        string name = ReadOptionalName(element, "Name") ?? "";
        if (name.Length == 0)
        {
            Record(issues, "A <Scheme> is missing its required Name attribute; the whole scheme is ignored.");
            return;
        }

        if (schemes.ContainsKey(name))
        {
            Record(issues, "Scheme '" + name + "' is declared twice; the first declaration is kept.");
            return;
        }

        foreach (XmlAttribute attribute in element.Attributes)
        {
            if (!string.Equals(attribute.Name, "Name", StringComparison.Ordinal))
            {
                Record(issues, "Unknown <Scheme Name=\"" + name + "\"> attribute '" + attribute.Name + "'; ignored.");
            }
        }

        var definition = new SchemeDefinition();
        schemes.Add(name, definition);

        foreach (XmlNode child in element.ChildNodes)
        {
            if (child is not XmlElement entry) continue;
            switch (entry.Name)
            {
                case "Color":
                    ReadColor(name, entry, definition, issues);
                    break;
                case "Metric":
                    // A scheme may carry the DENSITY metric vocabulary, which is what makes the premeasure
                    // style axis one vocabulary instead of two: the spacing numbers that move a rect are
                    // declared the same way in a scheme and in a named density. The Font key is the one
                    // exception, and it is separated here rather than inside ReadMetric because its value is
                    // a typeface: it lands on the typography axis, never on a numeric metric.
                    if (string.Equals(ReadOptionalName(entry, "Token"), FontMetricToken, StringComparison.Ordinal))
                    {
                        ReadFontMetric(name, entry, definition, issues);
                        break;
                    }

                    ReadMetric("scheme '" + name + "'", entry, definition.Metrics, issues);
                    break;
                case "Font":
                    ReadFont(name, entry, definition, issues);
                    break;
                default:
                    Record(issues, "Unknown element <" + entry.Name + "> inside <Scheme Name=\"" + name + "\">; ignored.");
                    break;
            }
        }

        ApplyLegacyFont(name, definition, issues);
    }

    private static void ReadColor(string scheme, XmlElement element, SchemeDefinition definition, List<UiStyleIssue> issues)
    {
        string token = ReadOptionalName(element, "Token") ?? "";
        string raw = element.GetAttribute("Value") ?? "";
        if (token.Length == 0 || raw.Length == 0)
        {
            Record(issues, "A <Color> in scheme '" + scheme + "' needs both Token and Value; the declaration is ignored.");
            return;
        }

        if (!IsColourToken(token))
        {
            Record(issues, "Unknown colour token '" + token + "' in scheme '" + scheme + "'; the declaration is ignored.");
            return;
        }

        if (definition.Colours.ContainsKey(token))
        {
            Record(issues, "Colour token '" + token + "' is set twice in scheme '" + scheme + "'; the first value is kept.");
            return;
        }

        if (!TryParseColour(raw, out Color colour))
        {
            Record(issues, "Scheme '" + scheme + "' sets '" + token + "' to an unreadable colour '" + raw
                + "'; expected #RRGGBB, #RRGGBBAA or r,g,b[,a] in 0..1. The declaration is ignored.");
            return;
        }

        definition.Colours.Add(token, colour);
    }

    /// <summary>
    /// The legacy <c>&lt;Font Value="…"/&gt;</c> declaration: a FONT CLASS, and nothing else. It is stored so
    /// the redirect can be reported after the whole scheme is read; the value it carries is a typeface, which
    /// is what <see cref="SchemeDefinition.Font"/> applies, so the old spelling keeps its meaning rather than
    /// being translated into some other axis.
    /// <para>
    /// An unknown Token is still refused - a historical spelling that names something this library never had
    /// is not a legacy declaration - and an unreadable value is reported exactly as it always was.
    /// </para>
    /// </summary>
    private static void ReadFont(string scheme, XmlElement element, SchemeDefinition definition, List<UiStyleIssue> issues)
    {
        string raw = element.GetAttribute("Value") ?? "";
        string token = ReadOptionalName(element, "Token") ?? "DefaultFont";
        if (!string.Equals(token, "DefaultFont", StringComparison.Ordinal))
        {
            Record(issues, "Unknown font token '" + token + "' in scheme '" + scheme + "'; the declaration is ignored.");
            return;
        }

        if (!TryParseFont(raw, out UiFont font))
        {
            Record(issues, "Scheme '" + scheme + "' sets the font to an unknown size '" + raw
                + "'; expected Tiny, Small or Medium. The declaration is ignored.");
            return;
        }

        if (definition.LegacyFont.HasValue)
        {
            Record(issues, "The font is set twice in scheme '" + scheme + "'; the first value is kept.");
            return;
        }

        definition.LegacyFont = font;
    }

    /// <summary>The successor of the legacy <c>&lt;Font&gt;</c> declaration: one key of the scheme's metric
    /// vocabulary whose VALUE is a font class rather than a number.</summary>
    internal const string FontMetricToken = "Font";

    /// <summary>
    /// One <c>&lt;Metric Token="Font" Value="…"/&gt;</c>, the working spelling of a typography selection. Its
    /// value goes through the same <see cref="TryParseFont"/> the legacy element uses, so the two spellings
    /// cannot disagree about what a font class is.
    /// </summary>
    private static void ReadFontMetric(string scheme, XmlElement entry, SchemeDefinition definition, List<UiStyleIssue> issues)
    {
        string raw = entry.GetAttribute("Value") ?? "";
        if (raw.Length == 0)
        {
            Record(issues, "A <Metric Token=\"Font\"> in scheme '" + scheme + "' needs a Value; the declaration is ignored.");
            return;
        }

        if (!TryParseFont(raw, out UiFont font))
        {
            Record(issues, "Scheme '" + scheme + "' sets Font to an unknown size '" + raw
                + "'; expected Tiny, Small or Medium. The declaration is ignored.");
            return;
        }

        if (definition.Font.HasValue)
        {
            Record(issues, "The font is set twice in scheme '" + scheme + "'; the first value is kept.");
            return;
        }

        definition.Font = font;
    }

    /// <summary>
    /// Resolves the legacy font declaration into the typography it always meant, once per scheme, after every
    /// child element has been read - so the answer does not depend on whether the author wrote
    /// <c>&lt;Font&gt;</c> before or after a <c>Font</c> metric. The metric wins when both are declared, and
    /// that is said out loud rather than resolved silently.
    /// <para>
    /// The redirect maps a font to a FONT, never to a row height: density is a separate axis, and translating
    /// a typeface into spacing would have destroyed the declaration's meaning while appearing to honour it.
    /// </para>
    /// </summary>
    private static void ApplyLegacyFont(string scheme, SchemeDefinition definition, List<UiStyleIssue> issues)
    {
        if (!definition.LegacyFont.HasValue) return;

        UiFont font = definition.LegacyFont.Value;

        if (definition.Font.HasValue)
        {
            Record(issues, "Scheme '" + scheme + "' declares both <Font Value=\"" + FontName(font)
                + "\"/> and a Font metric; the metric wins and the legacy declaration is ignored.");
            return;
        }

        definition.Font = font;
        Record(issues, "Scheme '" + scheme + "' declares the legacy <Font Value=\"" + FontName(font)
            + "\"/>; it is redirected to the typography metric <Metric Token=\"Font\" Value=\""
            + FontName(font) + "\"/>, which is the supported successor. The selected font is what text "
            + "measurement and painting both read, and density stays a separate axis.");

        // Reported through the appearance channel as well, exactly once per declared scheme (the parser runs
        // once per document, and the dedup key is the declaration itself): a redirect that only left a line in
        // the document's issue list would be found by a reader of that list and by nobody else.
        UiFitAudit.ReportStyleFallback(
            "scheme:" + scheme, "styles", "Font", FontName(font), "Metric Token=\"Font\"");
    }

    /// <summary>The spelling an author used, for the redirect report.</summary>
    private static string FontName(UiFont font)
    {
        switch (font)
        {
            case UiFont.Tiny:
                return "Tiny";
            case UiFont.Medium:
                return "Medium";
            default:
                return "Small";
        }
    }

    private static void ReadDensity(XmlElement element, Dictionary<string, DensityDefinition> densities, List<UiStyleIssue> issues)
    {
        string name = ReadOptionalName(element, "Name") ?? "";
        if (name.Length == 0)
        {
            Record(issues, "A <Density> is missing its required Name attribute; the whole density is ignored.");
            return;
        }

        if (densities.ContainsKey(name))
        {
            Record(issues, "Density '" + name + "' is declared twice; the first declaration is kept.");
            return;
        }

        foreach (XmlAttribute attribute in element.Attributes)
        {
            if (!string.Equals(attribute.Name, "Name", StringComparison.Ordinal))
            {
                Record(issues, "Unknown <Density Name=\"" + name + "\"> attribute '" + attribute.Name + "'; ignored.");
            }
        }

        var definition = new DensityDefinition();
        densities.Add(name, definition);

        foreach (XmlNode child in element.ChildNodes)
        {
            if (child is not XmlElement entry) continue;
            if (!string.Equals(entry.Name, "Metric", StringComparison.Ordinal))
            {
                Record(issues, "Unknown element <" + entry.Name + "> inside <Density Name=\"" + name + "\">; ignored.");
                continue;
            }

            ReadMetric("density '" + name + "'", entry, definition.Metrics, issues);
        }
    }

    /// <summary>
    /// One <c>&lt;Metric Token="…" Value="…"/&gt;</c>, read identically for a scheme and for a density: the
    /// two vocabularies are one, and the <paramref name="origin"/> only shapes the message a drop prints.
    /// <para>
    /// This reader handles NUMBERS only. The one metric key whose value is not a number - the typography
    /// selection - is refused here with a message that names its own home, so a typeface can never be parsed
    /// into a distance even when an author writes it under a named density, where there is no typography axis
    /// to receive it.
    /// </para>
    /// </summary>
    private static void ReadMetric(string origin, XmlElement entry, Dictionary<string, float> metrics, List<UiStyleIssue> issues)
    {
        string token = ReadOptionalName(entry, "Token") ?? "";
        string raw = entry.GetAttribute("Value") ?? "";
        if (token.Length == 0 || raw.Length == 0)
        {
            Record(issues, "A <Metric> in " + origin + " needs both Token and Value; the declaration is ignored.");
            return;
        }

        if (string.Equals(token, FontMetricToken, StringComparison.Ordinal))
        {
            Record(issues, "Metric 'Font' in " + origin + " selects a typeface, which only a <Scheme> can carry; "
                + "the declaration is ignored (density carries numbers, not fonts).");
            return;
        }

        if (!IsMetricToken(token))
        {
            Record(issues, "Unknown density token '" + token + "' in " + origin + "; the declaration is ignored.");
            return;
        }

        if (metrics.ContainsKey(token))
        {
            Record(issues, "Density token '" + token + "' is set twice in " + origin + "; the first value is kept.");
            return;
        }

        if (!float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || value < 0f)
        {
            Record(issues, origin + " sets '" + token + "' to an unreadable value '" + raw
                + "'; expected a non-negative number. The declaration is ignored.");
            return;
        }

        metrics.Add(token, value);
    }

    private static bool IsPageAttribute(string name)
    {
        return string.Equals(name, "Schema", StringComparison.Ordinal)
            || string.Equals(name, "Scheme", StringComparison.Ordinal)
            || string.Equals(name, "Density", StringComparison.Ordinal);
    }

    private static bool IsColourToken(string token)
    {
        switch (token)
        {
            case "Base":
            case "Panel":
            case "Raised":
            case "Hover":
            case "Selected":
            case "Success":
            case "Danger":
            case "WorkspacePlane":
            case "SectionBand":
            case "TextPrimary":
            case "TextSecondary":
            case "TextOnGold":
            case "TextOnDanger":
            case "TextDisabled":
            case "AccentGold":
            // SA1.1(r3): the switch thumb's OFF ink is declarable like every other colour role, so a
            // consumer configures it through its single palette document instead of a C#-side assignment
            // that would bypass the source and split scope consistency. Unset it still answers the theme's
            // own TextPrimary at READ time (UiTheme.SwitchThumbOff) - this gate only recognises the name.
            case "SwitchThumbOff":
            // Batch 1 (CP-6④): HoverPoint is no longer a token. A scheme that still declares it is
            // refused here - one recorded issue, never a silent no-op - and the hover step is derived
            // from AccentGold.
            case "Border":
            case "BorderStrong":
            case "Divider":
            case "BaseBorder":
            case "PanelBorder":
            case "RaisedBorder":
            case "HoverBorder":
            case "SelectedBorder":
            case "SuccessBorder":
            case "DangerBorder":
                return true;
            default:
                return false;
        }
    }

    private static bool IsMetricToken(string token)
    {
        switch (token)
        {
            case "Padding":
            case "Spacing":
            case "Gap":
            case "RowHeight":
            case "Hairline":
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseFont(string raw, out UiFont font)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "tiny":
                font = UiFont.Tiny;
                return true;
            case "small":
                font = UiFont.Small;
                return true;
            case "medium":
                font = UiFont.Medium;
                return true;
            default:
                font = UiFont.Small;
                return false;
        }
    }

    /// <summary>
    /// <c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c>, or a comma-separated
    /// <c>r,g,b[,a]</c> in 0..1 - the float form is the one the theme's own colours are written in, and the
    /// hex form is the one an author reaches for first.
    /// </summary>
    private static bool TryParseColour(string raw, out Color colour)
    {
        colour = default;
        string text = raw.Trim();
        if (text.Length == 0) return false;

        if (text[0] == '#') return TryParseHexColour(text.Substring(1), out colour);

        // Hand-scanned rather than String.Split: net472 advertises Split(char, StringSplitOptions) to the
        // compiler and then throws MissingMethodException at run time, the reference-assembly trap this
        // repo has recorded twice (MEMORY.md, "net472 reference-assembly traps").
        var channels = new float[4];
        channels[3] = 1f;
        int start = 0;
        int index = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i != text.Length && text[i] != ',') continue;
            if (index > 3) return false;

            if (!float.TryParse(text.Substring(start, i - start).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                || value < 0f || value > 1f)
            {
                return false;
            }

            channels[index] = value;
            index++;
            start = i + 1;
        }

        if (index is < 3 or > 4) return false;

        colour = new Color(channels[0], channels[1], channels[2], channels[3]);
        return true;
    }

    private static bool TryParseHexColour(string hex, out Color colour)
    {
        colour = default;
        if (!IsHex(hex)) return false;

        float a = 1f;
        string r;
        string g;
        string b;
        switch (hex.Length)
        {
            case 3:
                r = Duplicate(hex, 0);
                g = Duplicate(hex, 1);
                b = Duplicate(hex, 2);
                break;
            case 4:
                r = Duplicate(hex, 0);
                g = Duplicate(hex, 1);
                b = Duplicate(hex, 2);
                a = ByteOf(Duplicate(hex, 3));
                break;
            case 6:
                r = hex.Substring(0, 2);
                g = hex.Substring(2, 2);
                b = hex.Substring(4, 2);
                break;
            case 8:
                r = hex.Substring(0, 2);
                g = hex.Substring(2, 2);
                b = hex.Substring(4, 2);
                a = ByteOf(hex.Substring(6, 2));
                break;
            default:
                return false;
        }

        colour = new Color(ByteOf(r), ByteOf(g), ByteOf(b), a);
        return true;
    }

    private static bool IsHex(string text)
    {
        if (text.Length == 0) return false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool digit = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!digit) return false;
        }

        return true;
    }

    private static string Duplicate(string text, int index)
    {
        char c = text[index];
        return new string(new[] { c, c });
    }

    private static float ByteOf(string hex)
    {
        return int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
    }

    private static string? ReadOptionalName(XmlElement element, string attribute)
    {
        string value = (element.GetAttribute(attribute) ?? "").Trim();
        return value.Length == 0 ? null : value;
    }

    private static void Record(List<UiStyleIssue> issues, string message, int line = 0)
    {
        if (issues.Count >= MaxIssues) return;
        issues.Add(new UiStyleIssue(message, line));
    }

    internal sealed class SchemeDefinition
    {
        internal Dictionary<string, Color> Colours { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The premeasure half of a scheme: the metric vocabulary a named density carries too. These are
        /// NUMBERS - spacing, including the row height - and they are applied through
        /// <see cref="UiStyleResolver"/> before the first Measure, which is the one place a density fact can be
        /// resolved and still reach the arrangement. A typeface is deliberately NOT in here: it belongs to
        /// <see cref="Font"/>, so a font can never be expressed as, or confused with, a distance.
        /// </summary>
        internal Dictionary<string, float> Metrics { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The scheme's typography selection, set by <c>&lt;Metric Token="Font" Value="…"/&gt;</c> and by the
        /// legacy <c>&lt;Font Value="…"/&gt;</c> redirect. This is the value <see cref="UiTheme.DefaultFont"/>
        /// receives, so it is the font every text measurement and every painted label in the scope resolves.
        /// </summary>
        internal UiFont? Font { get; set; }

        /// <summary>
        /// The legacy <c>&lt;Font Value="…"/&gt;</c> declaration, kept only so the redirect can be reported
        /// after the whole scheme is read. It is applied as a FONT - the same axis as <see cref="Font"/> -
        /// never as a metric value.
        /// </summary>
        internal UiFont? LegacyFont { get; set; }
    }

    internal sealed class DensityDefinition
    {
        internal Dictionary<string, float> Metrics { get; } = new(StringComparer.Ordinal);
    }
}
