using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Schema=2 layout manifest parser. The parser is strict and safe: DTD/external entities are
/// prohibited, depth/node limits are enforced, and text content is rejected. Kind registration and
/// typed binding validation happen later in <see cref="UiHost"/> so parsing stays independent.
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
