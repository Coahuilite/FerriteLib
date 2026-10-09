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
/// <para>
/// Contract on the collection vocabulary (0.5, P3): a <c>&lt;Templates&gt;</c> section declares the
/// per-item subtrees a <c>&lt;Repeat&gt;</c> materializes, each entry named by its <c>Id</c>; a Repeat names
/// one with <c>Template</c> and its ordered item keys with <c>Items</c>, and carries no children of its
/// own. Templates are deliberately outside <see cref="Roots"/>: their controls resolve binding keys
/// against a per-item scope the engine composes at row instantiation, so those keys cannot be — and must
/// not be — validated against the page bindings at Host creation. Their structural contract (kind and
/// attribute vocabulary) is validated by the layout engine's template validator, with the template name
/// and path in the refusal; the reference from a Repeat to a template, the one-Id-per-template and the
/// per-template Id grammar are refused here, at parse time, because this parser is what owns the table.
/// </para>
/// </summary>
public sealed class UiLayoutManifest
{
    private const int MaxDepth = 16;
    private const int MaxNodeCount = 512;

    /// <summary>Longest <c>&lt;Styles&gt;</c> section accepted, in characters. The style parser has its own bounds.</summary>
    private const int MaxStylesChars = 32768;

    /// <summary>The template table of a manifest that declares no <c>&lt;Templates&gt;</c> section.</summary>
    private static readonly IReadOnlyDictionary<string, UiElementSpec> NoTemplates =
        new Dictionary<string, UiElementSpec>(StringComparer.Ordinal);

    private static readonly HashSet<string> SupportedElementNames = new(StringComparer.Ordinal)
    {
        "UiPage", "Stack", "Row", "Column", "Wrap", "Overlay", "Section", "Surface", "Scroll", "Clip", "Widget", "Repeat"
    };

    /// <summary>Separator that joins an item key to a template element's declared Id at row instantiation.</summary>
    /// <remarks>
    /// The engine composes per-item identities and per-item binding keys from a template's declared names,
    /// so the two namespaces have to stay injective. A declared Id inside <c>&lt;Templates&gt;</c> therefore
    /// may not contain this character — refused at parse time, next to the '/' rule it is the sibling of —
    /// and neither may an item key (the engine refuses such a row rather than reconciling it). Page Ids are
    /// untouched: the rule is template vocabulary, so no existing manifest changes meaning.
    /// </remarks>
    internal const char ItemKeySeparator = '#';

    public string Source { get; }

    public string SchemaVersion { get; }

    public IReadOnlyList<UiElementSpec> Roots { get; }

    /// <summary>
    /// The optional <c>&lt;Styles&gt;</c> section, parsed by <see cref="UiStyleDocument.Parse"/> - the same
    /// parser and the same vocabulary the standalone style file uses, which is what keeps the two text
    /// origins from drifting. A manifest without the section carries the empty document.
    /// </summary>
    public UiStyleDocument Styles { get; }

    /// <summary>
    /// The <c>&lt;Templates&gt;</c> table: per-item subtrees a <c>&lt;Repeat&gt;</c> materializes, keyed by
    /// the declared <c>Id</c> that names them. Ordinal-keyed and immutable; a manifest without the section
    /// carries an empty table, and a <c>&lt;Repeat&gt;</c> that names nothing in it is refused at parse
    /// time rather than drawing an empty band.
    /// <para>
    /// The engine receives this table through its constructor (<see cref="UiHost"/> hands it the host's
    /// manifest), because the template subtree is intentionally outside <see cref="Roots"/>. Nothing else
    /// reads it: a template is not an element, has no node of its own, and only exists as the shape each
    /// row is instantiated from.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, UiElementSpec> Templates { get; }

    private UiLayoutManifest(
        string source,
        string schemaVersion,
        IReadOnlyList<UiElementSpec> roots,
        UiStyleDocument styles,
        IReadOnlyDictionary<string, UiElementSpec> templates)
    {
        Source = source;
        SchemaVersion = schemaVersion;
        Roots = roots;
        Styles = styles;
        Templates = templates;
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
                return new UiLayoutManifest(
                    source, schema, Array.Empty<UiElementSpec>(), UiStyleDocument.Empty, NoTemplates);
            }

            break;
        }

        if (reader.EOF)
        {
            throw new FormatException("UI layout XML is empty; expected <UiPage Schema=\"2\" Source=\"...\">.");
        }

        var roots = new List<UiElementSpec>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var templates = new Dictionary<string, UiElementSpec>(StringComparer.Ordinal);
        UiStyleDocument styles = UiStyleDocument.Empty;
        bool stylesSeen = false;
        bool templatesSeen = false;

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

                    if (string.Equals(reader.Name, "Styles", StringComparison.Ordinal))
                    {
                        if (stylesSeen)
                        {
                            throw ParseError(lineInfo, "Duplicate <Styles> section; one manifest may carry one.");
                        }

                        stylesSeen = true;
                        styles = ReadStylesSection(reader, lineInfo);
                        break;
                    }

                    if (string.Equals(reader.Name, "Templates", StringComparison.Ordinal))
                    {
                        if (templatesSeen)
                        {
                            throw ParseError(lineInfo, "Duplicate <Templates> section; one manifest may carry one.");
                        }

                        templatesSeen = true;
                        ReadTemplatesSection(reader, lineInfo, ref nodeCount, ids, templates);
                        break;
                    }

                    if (!SupportedElementNames.Contains(reader.Name))
                    {
                        throw ParseError(lineInfo,
                            $"Unsupported element <{reader.Name}>; expected one of: Stack, Row, Column, Wrap, Overlay, Section, Surface, Scroll, Clip, Widget, Repeat, Styles, Templates.");
                    }

                    UiElementSpec root = ReadElement(reader, lineInfo, ref nodeCount, ids);
                    roots.Add(root);
                    break;

                case XmlNodeType.EndElement:
                    if (string.Equals(reader.Name, "UiPage", StringComparison.Ordinal))
                    {
                        ValidateIdentitySegments(roots, pageLine);
                        ValidateTemplateReferences(roots, templates);
                        return new UiLayoutManifest(source, schema, roots, styles, templates);
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

    /// <summary>
    /// Captures the <c>&lt;Styles&gt;</c> subtree and hands it to the one style parser. The manifest owns
    /// only the capture: the section's vocabulary, its validation and its failures belong to
    /// <see cref="UiStyleDocument"/>. A malformed section is not appearance-class here, because the section
    /// is part of this XML text - a manifest that cannot be read is the existing page-level failure.
    /// </summary>
    private static UiStyleDocument ReadStylesSection(XmlReader reader, IXmlLineInfo lineInfo)
    {
        string xml;
        using (XmlReader subtree = reader.ReadSubtree())
        {
            subtree.MoveToContent();
            xml = subtree.ReadOuterXml();
        }

        if (xml.Length > MaxStylesChars)
        {
            throw ParseError(lineInfo,
                $"The <Styles> section exceeds the maximum of {MaxStylesChars} characters.");
        }

        return UiStyleDocument.Parse(xml);
    }

    /// <summary>
    /// Captures the <c>&lt;Templates&gt;</c> section: every direct child is one named template subtree, kept
    /// out of <see cref="Roots"/> on purpose so the Host's creation-time walk never validates a subtree whose
    /// binding keys are item-scoped. The section's own structure is refused here — a missing or duplicated
    /// template Id, an Id or template name carrying a reserved separator, and a nested <c>&lt;Repeat&gt;</c>
    /// (a row template that materializes rows is a second reconciliation the engine does not have, and
    /// half-supporting it is exactly the silent shape this library refuses).
    /// </summary>
    private static void ReadTemplatesSection(
        XmlReader reader,
        IXmlLineInfo lineInfo,
        ref int nodeCount,
        HashSet<string> ids,
        Dictionary<string, UiElementSpec> templates)
    {
        if (reader.IsEmptyElement) return;

        int sectionDepth = reader.Depth;
        while (reader.Read())
        {
            nodeCount = BumpNode(nodeCount, lineInfo);

            if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (reader.Depth == sectionDepth
                    && string.Equals(reader.Name, "Templates", StringComparison.Ordinal))
                {
                    return;
                }

                continue;
            }

            if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA)
            {
                throw ParseError(lineInfo, "Text content is not allowed inside <Templates>; expected template elements only.");
            }

            if (reader.NodeType != XmlNodeType.Element) continue;

            if (reader.Depth > MaxDepth)
            {
                throw ParseError(lineInfo, $"UI layout exceeds the maximum depth of {MaxDepth}.");
            }

            if (reader.Depth != sectionDepth + 1)
            {
                throw ParseError(lineInfo,
                    $"Unexpected nested element <{reader.Name}> inside <Templates>; one template element per direct child.");
            }

            if (!SupportedElementNames.Contains(reader.Name) || string.Equals(reader.Name, "UiPage", StringComparison.Ordinal))
            {
                throw ParseError(lineInfo,
                    $"Unsupported template element <{reader.Name}>; expected a container or <Widget>.");
            }

            string name = reader.GetAttribute("Id") ?? reader.GetAttribute("id") ?? "";
            if (name.Length == 0)
            {
                throw ParseError(lineInfo,
                    $"Template <{reader.Name}> is missing the required Id; the Id is the name a <Repeat Template=\"...\"> refers to.");
            }

            if (name.IndexOf('/') >= 0 || name.IndexOf(ItemKeySeparator) >= 0)
            {
                throw ParseError(lineInfo,
                    $"Template Id '{name}' carries a reserved separator ('/' or '{ItemKeySeparator}'); a template name is one identity segment.");
            }

            if (templates.ContainsKey(name))
            {
                throw ParseError(lineInfo, $"Duplicate template Id '{name}'; one template name names one subtree.");
            }

            UiElementSpec template = ReadElement(reader, lineInfo, ref nodeCount, ids);
            ValidateTemplateIdentityGrammar(template, name);
            templates.Add(name, template);
        }

        throw new FormatException("UI layout XML ended inside the <Templates> element.");
    }

    /// <summary>
    /// The Id grammar inside a template subtree. Page-element Ids keep exactly the rules they had; a template
    /// element adds one: it may not carry <see cref="ItemKeySeparator"/>, because the engine composes the
    /// per-item identity of that element as <c>&lt;declaredId&gt;#&lt;itemKey&gt;</c> and a declared Id that
    /// already spells the separator could collide with a different row's composed identity.
    /// </summary>
    private static void ValidateTemplateIdentityGrammar(UiElementSpec spec, string templateName)
    {
        if (spec.Id.IndexOf(ItemKeySeparator) >= 0)
        {
            throw new FormatException(
                $"Invalid UI layout XML: template '{templateName}' declares element Id '{spec.Id}', which carries "
                + $"the reserved item-key separator '{ItemKeySeparator}'.");
        }

        if (string.Equals(spec.Kind, "Repeat", StringComparison.Ordinal))
        {
            throw new FormatException(
                $"Invalid UI layout XML: template '{templateName}' contains a <Repeat>. The engine materializes one "
                + "row set per <Repeat>; a row template that materializes rows is refused rather than half-supported.");
        }

        for (int i = 0; i < spec.Children.Count; i++)
        {
            ValidateTemplateIdentityGrammar(spec.Children[i], templateName);
        }
    }

    /// <summary>
    /// Every <c>&lt;Repeat&gt;</c> names a template the same manifest declares and carries no children of its
    /// own. Refused at parse time rather than at the first arrange because the table belongs to this parser:
    /// a dangling reference is a manifest error, and the answer must not depend on which frame happens to
    /// materialize the rows.
    /// </summary>
    private static void ValidateTemplateReferences(
        IReadOnlyList<UiElementSpec> roots,
        IReadOnlyDictionary<string, UiElementSpec> templates)
    {
        for (int i = 0; i < roots.Count; i++)
        {
            UiElementSpec root = roots[i];
            ValidateTemplateReference(root, templates, root.Id.Length > 0 ? root.Id : root.Kind);
        }
    }

    private static void ValidateTemplateReference(
        UiElementSpec spec,
        IReadOnlyDictionary<string, UiElementSpec> templates,
        string path)
    {
        if (string.Equals(spec.Kind, "Repeat", StringComparison.Ordinal))
        {
            if (spec.Children.Count > 0)
            {
                throw new FormatException(
                    $"Invalid UI layout XML: <Repeat Id=\"{spec.Id}\"> at '{path}' contains elements; the per-item "
                    + "template is declared once in <Templates> and named by Template=\"...\".");
            }

            string name = spec.TryGetAttribute("Template", out string raw) ? raw.Trim() : "";
            if (name.Length == 0)
            {
                throw new FormatException(
                    $"Invalid UI layout XML: <Repeat Id=\"{spec.Id}\"> at '{path}' is missing the required Template "
                    + "attribute naming a template declared in <Templates>.");
            }

            if (!templates.ContainsKey(name))
            {
                throw new FormatException(
                    $"Invalid UI layout XML: <Repeat Id=\"{spec.Id}\"> at '{path}' names template '{name}', which "
                    + "<Templates> does not declare.");
            }
        }

        for (int i = 0; i < spec.Children.Count; i++)
        {
            UiElementSpec child = spec.Children[i];
            ValidateTemplateReference(child, templates, path + "/" + (child.Id.Length > 0 ? child.Id : child.Kind));
        }
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
