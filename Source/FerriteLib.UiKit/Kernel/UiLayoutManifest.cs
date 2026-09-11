using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Schema=2 layout manifest parser. The parser is strict and safe: DTD/external entities are
/// prohibited, depth/node limits are enforced, and text content is rejected. Kind registration and
/// typed binding validation happen later in <see cref="UiHost"/> so parsing stays independent.
/// <para>
/// Contract on the <c>Tab</c> attribute: an element carrying <c>Tab="X"</c> is hidden unless the
/// consumer exposes <see cref="UiBindings.ActiveTabKey"/> as a <c>string</c> value binding whose current
/// value equals <c>X</c> (case-insensitive). The engine reads that one key and no other; elements
/// without <c>Tab</c> are always visible.
/// <para>
/// Contract on the responsive vocabulary (US→FL round 3, N1+N2): a child's <c>Width</c> is a
/// positive number or <c>Auto</c> — Auto resolves to the text-natural width of the label
/// attributes its kind declared at registration (containers: <c>Title</c>/<c>TitleKey</c>),
/// clamped by <c>MinWidth</c>/<c>MaxWidth</c> and capped by the budget the fixed siblings leave;
/// a kind that declared no label set falls back to the unsized equal distribution, unchanged.
/// A container's <c>Breakpoint</c> is one positive number measured against that container's own
/// inner width; below it the declared variants apply — <c>Narrow</c> (direction), <c>NarrowCols</c>
/// (Wrap column count, alongside <c>Cols</c>), <c>NarrowHidden</c> (per child). Auto widths are
/// arranged geometry: they land in the snapshot, so the engine's existing content/translation
/// revision discipline is their invalidation — a label change or language switch re-arranges,
/// and no consumer may cache an Auto rect across revisions.
/// </para>
/// </summary>
public sealed class UiLayoutManifest
{
    private const int MaxDepth = 16;
    private const int MaxNodeCount = 512;

    private static readonly HashSet<string> SupportedElementNames = new(StringComparer.Ordinal)
    {
        "UiPage", "Stack", "Row", "Column", "Wrap", "Overlay", "Section", "Surface", "Scroll", "Clip", "Widget"
    };

    public string Source { get; }

    public string SchemaVersion { get; }

    public IReadOnlyList<UiElementSpec> Roots { get; }

    private UiLayoutManifest(string source, string schemaVersion, IReadOnlyList<UiElementSpec> roots)
    {
        Source = source;
        SchemaVersion = schemaVersion;
        Roots = roots;
    }

    public static UiLayoutManifest Parse(string xml)
    {
        if (xml == null) throw new ArgumentNullException(nameof(xml));

        try
        {
            return ParseCore(xml);
        }
        catch (XmlException ex)
        {
            throw new FormatException(
                $"Invalid UI layout XML at line {ex.LineNumber}, position {ex.LinePosition}: {ex.Message}",
                ex);
        }
    }

    public static UiLayoutManifest ParseFile(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        return Parse(File.ReadAllText(path));
    }

    private static UiLayoutManifest ParseCore(string xml)
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
        IXmlLineInfo lineInfo = reader as IXmlLineInfo ?? NullLineInfo.Instance;

        int nodeCount = 0;
        int pageLine = 0;
        string source = "";
        string schema = "";

        while (reader.Read())
        {
            nodeCount = BumpNode(nodeCount, lineInfo);
            if (reader.NodeType != XmlNodeType.Element) continue;

            if (reader.Depth > MaxDepth)
            {
                throw ParseError(lineInfo, $"UI layout exceeds the maximum depth of {MaxDepth}.");
            }

            if (!string.Equals(reader.Name, "UiPage", StringComparison.Ordinal))
            {
                throw ParseError(lineInfo, $"Expected root element <UiPage> but found <{reader.Name}>.");
            }

            schema = reader.GetAttribute("Schema") ?? "";
            source = reader.GetAttribute("Source") ?? "";
            pageLine = lineInfo.LineNumber;

            if (schema.Length == 0)
            {
                throw ParseError(lineInfo, "Root <UiPage> is missing the required Schema attribute.");
            }

            if (!string.Equals(schema, "2", StringComparison.Ordinal))
            {
                throw ParseError(lineInfo, $"Unsupported UiPage Schema '{schema}'; expected '2'.");
            }

            if (source.Length == 0)
            {
                throw ParseError(lineInfo, "Root <UiPage> is missing the required Source attribute.");
            }

            if (reader.IsEmptyElement)
            {
                return new UiLayoutManifest(source, schema, Array.Empty<UiElementSpec>());
            }

            break;
        }

        if (reader.EOF)
        {
            throw new FormatException("UI layout XML is empty; expected <UiPage Schema=\"2\" Source=\"...\">.");
        }

        var roots = new List<UiElementSpec>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        while (reader.Read())
        {
            nodeCount = BumpNode(nodeCount, lineInfo);

            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    if (reader.Depth > MaxDepth)
                    {
                        throw ParseError(lineInfo, $"UI layout exceeds the maximum depth of {MaxDepth}.");
                    }

                    if (reader.Depth != 1)
                    {
                        throw ParseError(lineInfo,
                            $"Unexpected nested element <{reader.Name}> at depth {reader.Depth}; expected a root-level element.");
                    }

                    if (!SupportedElementNames.Contains(reader.Name))
                    {
                        throw ParseError(lineInfo,
                            $"Unsupported element <{reader.Name}>; expected one of: Stack, Row, Column, Wrap, Overlay, Section, Surface, Scroll, Clip, Widget.");
                    }

                    UiElementSpec root = ReadElement(reader, lineInfo, ref nodeCount, ids);
                    roots.Add(root);
                    break;

                case XmlNodeType.EndElement:
                    if (string.Equals(reader.Name, "UiPage", StringComparison.Ordinal))
                    {
                        ValidateIdentitySegments(roots, pageLine);
                        return new UiLayoutManifest(source, schema, roots);
                    }

                    throw ParseError(lineInfo, $"Unexpected end element </{reader.Name}>.");

                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                    throw ParseError(lineInfo, "Text content is not allowed inside <UiPage>; expected layout elements only.");

                default:
                    continue;
            }
        }

        throw new FormatException("UI layout XML ended before the <UiPage> element was closed.");
    }

    private static UiElementSpec ReadElement(XmlReader reader, IXmlLineInfo lineInfo, ref int nodeCount, HashSet<string> ids)
    {
        string elementName = reader.Name;
        int elementLine = lineInfo.LineNumber;
        string id = reader.GetAttribute("Id") ?? reader.GetAttribute("id") ?? "";

        // An Id names one element and is one identity segment (0.4.0 identity layer): the '/' that joins
        // identity keys is the tree's structure, so an Id carrying one could spell a deeper path and
        // hand two different elements the same key. Refused here, at creation time, beside the
        // duplicate-Id rule this parser already owns; a real child element is how a tree adds a level.
        if (id.IndexOf('/') >= 0)
        {
            throw ParseError(lineInfo,
                $"Element Id '{id}' contains the path separator '/'; an Id names one element, so give it "
                + "a name without '/'.");
        }

        string kind = elementName;

        if (string.Equals(elementName, "Widget", StringComparison.Ordinal))
        {
            kind = reader.GetAttribute("Kind") ?? "";
            if (kind.Length == 0)
            {
                throw ParseError(lineInfo, $"<Widget id=\"{id}\"> is missing the required Kind attribute.");
            }
        }
        else if (!SupportedElementNames.Contains(elementName) || string.Equals(elementName, "UiPage", StringComparison.Ordinal))
        {
            throw ParseError(lineInfo,
                $"Expected <Widget> or a container element but found <{elementName}>.");
        }

        if (id.Length > 0 && !ids.Add(id))
        {
            throw ParseError(lineInfo,
                $"Duplicate element Id '{id}' in UI layout (first occurrence at line {elementLine}).");
        }

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (reader.HasAttributes)
        {
            for (int i = 0; i < reader.AttributeCount; i++)
            {
                reader.MoveToAttribute(i);
                attributes[reader.Name] = reader.Value;
            }

            reader.MoveToElement();
        }

        if (reader.IsEmptyElement)
        {
            return new UiElementSpec(id, kind, attributes, Array.Empty<UiElementSpec>());
        }

        var children = new List<UiElementSpec>();

        while (reader.Read())
        {
            nodeCount = BumpNode(nodeCount, lineInfo);

            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    if (reader.Depth > MaxDepth)
                    {
                        throw ParseError(lineInfo, $"UI layout exceeds the maximum depth of {MaxDepth}.");
                    }

                    if (string.Equals(elementName, "Widget", StringComparison.Ordinal))
                    {
                        throw ParseError(lineInfo,
                            $"<Widget id=\"{id}\" Kind=\"{kind}\"> must not contain nested elements; found <{reader.Name}>.");
                    }

                    if (!SupportedElementNames.Contains(reader.Name) || string.Equals(reader.Name, "UiPage", StringComparison.Ordinal))
                    {
                        throw ParseError(lineInfo,
                            $"Unexpected element <{reader.Name}> inside <{elementName} id=\"{id}\">.");
                    }

                    children.Add(ReadElement(reader, lineInfo, ref nodeCount, ids));
                    break;

                case XmlNodeType.EndElement:
                    if (!string.Equals(reader.Name, elementName, StringComparison.Ordinal))
                    {
                        throw ParseError(lineInfo, $"Unexpected end element </{reader.Name}> inside <{elementName} id=\"{id}\">.");
                    }

                    ValidateIdentitySegments(children, elementLine);
                    return new UiElementSpec(id, kind, attributes, children);

                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                    throw ParseError(lineInfo, $"<{elementName} id=\"{id}\" Kind=\"{kind}\"> must not contain text content.");

                default:
                    continue;
            }
        }

        throw new FormatException($"UI layout XML ended inside <{elementName} id=\"{id}\" Kind=\"{kind}\">.");
    }

    /// <summary>
    /// A declared <c>Id</c> may not spell an unnamed sibling's generated identity segment (0.4.0
    /// identity layer). Identity keys are structural paths, and an element without an <c>Id</c>
    /// contributes <c>Kind[declaredIndex]</c>; an Id written as exactly that would give two siblings
    /// one key, which is the alias the identity layer exists to remove. Refused here, at creation
    /// time, next to the duplicate-Id rule it is the sibling of: a silently shared identity surfaces
    /// far from the manifest that caused it, as a control that keeps another control's state.
    /// </summary>
    private static void ValidateIdentitySegments(IReadOnlyList<UiElementSpec> siblings, int line)
    {
        HashSet<string>? generated = null;
        for (int i = 0; i < siblings.Count; i++)
        {
            UiElementSpec sibling = siblings[i];
            if (sibling.Id.Length > 0) continue;
            generated ??= new HashSet<string>(StringComparer.Ordinal);
            generated.Add(UiNodeId.GeneratedSegment(sibling.Kind, i));
        }

        if (generated == null) return;

        for (int i = 0; i < siblings.Count; i++)
        {
            UiElementSpec sibling = siblings[i];
            if (sibling.Id.Length == 0 || !generated.Contains(sibling.Id)) continue;
            throw new FormatException(
                $"Invalid UI layout XML at line {line}: element Id '{sibling.Id}' is also the generated "
                + "identity segment of an unnamed sibling of the same parent (an unnamed element's identity "
                + "is its Kind plus its declared index). Two siblings would share one identity key; give "
                + "either sibling a distinct Id.");
        }
    }

    private static int BumpNode(int count, IXmlLineInfo lineInfo)
    {
        count++;
        if (count > MaxNodeCount)
        {
            throw ParseError(lineInfo, $"UI layout exceeds the maximum node count of {MaxNodeCount}.");
        }

        return count;
    }

    private static FormatException ParseError(IXmlLineInfo lineInfo, string message)
    {
        return new FormatException(
            $"Invalid UI layout XML at line {lineInfo.LineNumber}, position {lineInfo.LinePosition}: {message}");
    }

    private sealed class NullLineInfo : IXmlLineInfo
    {
        internal static readonly NullLineInfo Instance = new();

        public int LineNumber => 0;

        public int LinePosition => 0;

        public bool HasLineInfo() => false;
    }
}
