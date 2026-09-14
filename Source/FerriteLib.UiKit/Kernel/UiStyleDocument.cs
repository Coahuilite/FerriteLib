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
    public UiStyleIssue(string message, int line = 0)
    {
        Message = message ?? "";
        Line = line;
    }

    /// <summary>What was dropped or replaced, in one sentence.</summary>
    public string Message { get; }

    /// <summary>1-based line when the source reported one, otherwise 0.</summary>
    public int Line { get; }

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
            return new UiStyleDocument(null, null, new Dictionary<string, SchemeDefinition>(StringComparer.Ordinal), new Dictionary<string, DensityDefinition>(StringComparer.Ordinal), issues);
        }
        catch (InvalidOperationException ex)
        {
            issues.Add(new UiStyleIssue("Invalid style XML: " + ex.Message));
            return new UiStyleDocument(null, null, new Dictionary<string, SchemeDefinition>(StringComparer.Ordinal), new Dictionary<string, DensityDefinition>(StringComparer.Ordinal), issues);
        }

        XmlElement? root = document.DocumentElement;
        var emptySchemes = new Dictionary<string, SchemeDefinition>(StringComparer.Ordinal);
        var emptyDensities = new Dictionary<string, DensityDefinition>(StringComparer.Ordinal);

        if (root == null || !string.Equals(root.Name, "Styles", StringComparison.Ordinal))
        {
            issues.Add(new UiStyleIssue("The style document root must be <Styles>; found <" + (root == null ? "(none)" : root.Name) + ">."));
            return new UiStyleDocument(null, null, emptySchemes, emptyDensities, issues);
        }

        string schema = root.GetAttribute("Schema") ?? "";
        if (!string.Equals(schema, "1", StringComparison.Ordinal))
        {
            issues.Add(new UiStyleIssue(schema.Length == 0
                ? "The style document is missing the required Schema=\"1\" on <Styles>."
                : "Unsupported <Styles> Schema '" + schema + "'; expected '1'."));
            return new UiStyleDocument(null, null, emptySchemes, emptyDensities, issues);
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
            return new UiStyleDocument(null, null, new Dictionary<string, SchemeDefinition>(StringComparer.Ordinal), new Dictionary<string, DensityDefinition>(StringComparer.Ordinal), issues);
        }

        return Parse(xml);
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
                case "Font":
                    ReadFont(name, entry, definition, issues);
                    break;
                default:
                    Record(issues, "Unknown element <" + entry.Name + "> inside <Scheme Name=\"" + name + "\">; ignored.");
                    break;
            }
        }
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

        if (definition.Font.HasValue)
        {
            Record(issues, "The font is set twice in scheme '" + scheme + "'; the first value is kept.");
            return;
        }

        definition.Font = font;
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

            string token = ReadOptionalName(entry, "Token") ?? "";
            string raw = entry.GetAttribute("Value") ?? "";
            if (token.Length == 0 || raw.Length == 0)
            {
                Record(issues, "A <Metric> in density '" + name + "' needs both Token and Value; the declaration is ignored.");
                continue;
            }

            if (!IsMetricToken(token))
            {
                Record(issues, "Unknown density token '" + token + "' in density '" + name + "'; the declaration is ignored.");
                continue;
            }

            if (definition.Metrics.ContainsKey(token))
            {
                Record(issues, "Density token '" + token + "' is set twice in density '" + name + "'; the first value is kept.");
                continue;
            }

            if (!float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || value < 0f)
            {
                Record(issues, "Density '" + name + "' sets '" + token + "' to an unreadable value '" + raw
                    + "'; expected a non-negative number. The declaration is ignored.");
                continue;
            }

            definition.Metrics.Add(token, value);
        }
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
            case "HoverPoint":
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

        internal UiFont? Font { get; set; }
    }

    internal sealed class DensityDefinition
    {
        internal Dictionary<string, float> Metrics { get; } = new(StringComparer.Ordinal);
    }
}
