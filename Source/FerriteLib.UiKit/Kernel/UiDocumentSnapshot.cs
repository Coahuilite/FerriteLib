using System;
using System.IO;
using System.Xml;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Detached author-source facts for the last accepted candidate, not the current disk contents.</summary>
public sealed class UiDocumentSnapshot
{
    internal UiDocumentSnapshot(UiDocumentSource source, string version, string text,
        UiLayoutManifest? layout, UiStyleDocument? style, bool watching, string failedVersion)
    { Source = source; Version = version; Text = text; Layout = layout; Style = style; Watching = watching; FailedVersion = failedVersion; }
    public UiDocumentSource Source { get; }
    public string Version { get; }
    public string Text { get; }
    public UiLayoutManifest? Layout { get; }
    public UiStyleDocument? Style { get; }
    public bool Watching { get; }
    public string FailedVersion { get; }
    public bool UsesEmbeddedFallback => Version.StartsWith("embedded:", StringComparison.Ordinal);
    public bool HasLastGood => Version.Length > 0;

    /// <summary>Locate a declared element/template in the same accepted full author text. Never reads disk.</summary>
    public UiDocumentLocation Locate(string elementId)
    {
        UiDocumentLocation? exact = FindLocation(elementId, null);
        if (exact != null) return exact;
        int suffix = elementId.IndexOf(UiLayoutManifest.ItemKeySeparator);
        if (suffix > 0 && Layout != null)
        {
            string declared = elementId.Substring(0, suffix);
            string? templateName = null;
            bool Contains(UiElementSpec spec)
            {
                if (spec.Id == declared) return true;
                foreach (UiElementSpec child in spec.Children) if (Contains(child)) return true;
                return false;
            }
            foreach (var template in Layout.Templates)
                if (Contains(template.Value))
                {
                    if (templateName != null)
                        return new UiDocumentLocation(Source.Id, Source.Path, declared, 0, 0, Version, UsesEmbeddedFallback,
                            "ambiguous-template-element; specify the author template instead");
                    templateName = template.Key;
                }
            if (templateName != null)
            {
                UiDocumentLocation? location = FindLocation(declared, templateName);
                if (location != null) return location;
            }
        }
        return new UiDocumentLocation(Source.Id, Source.Path, elementId, 0, 0, Version, UsesEmbeddedFallback,
            HasLastGood ? "no-declared-Id-match; generated nodes belong to their author template" : "no-accepted-author-text");
    }
    private UiDocumentLocation? FindLocation(string elementId, string? template)
    {
        UiDocumentLocation? result = null;
        if (HasLastGood && elementId.Length > 0)
        {
            using var input = new StringReader(Text);
            using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            int templatesDepth = -1, templateDepth = -1;
            string currentTemplate = "";
            while (reader.Read())
            {
                if (templateDepth >= 0 && reader.Depth <= templateDepth) { currentTemplate = ""; templateDepth = -1; }
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (reader.Depth == templateDepth) { currentTemplate = ""; templateDepth = -1; }
                    if (reader.Depth == templatesDepth) { templatesDepth = -1; currentTemplate = ""; }
                }
                if (reader.NodeType != XmlNodeType.Element) continue;
                if (reader.Name == "Templates") templatesDepth = reader.Depth;
                else if (templatesDepth >= 0 && reader.Depth == templatesDepth + 1)
                { currentTemplate = reader.GetAttribute("Id") ?? ""; templateDepth = reader.Depth; }
                if (reader.GetAttribute("Id") == elementId && (template == null || template == currentTemplate))
                {
                    if (result != null) return new UiDocumentLocation(Source.Id, Source.Path, elementId, 0, 0, Version,
                        UsesEmbeddedFallback, "ambiguous-declared-Id-in-full-author-text");
                    var line = (IXmlLineInfo)reader;
                    string selector = template == null ? "//*[@Id='" + elementId + "']"
                        : "//Templates/*[@Id='" + template + "']" + (template == elementId ? "" : "//*[@Id='" + elementId + "']");
                    result = new UiDocumentLocation(Source.Id, Source.Path, selector, line.LineNumber,
                        line.LinePosition, Version, UsesEmbeddedFallback, template == null ? "accepted-author-text" : "accepted-author-template:" + template);
                }
            }
        }
        return result;
    }
}

public sealed class UiDocumentLocation
{
    internal UiDocumentLocation(string document, string path, string selector, int line, int column, string version, bool embedded, string reason)
    { Document = document; Path = path; Selector = selector; Line = line; Column = column; Version = version; Embedded = embedded; Reason = reason; }
    public string Document { get; }
    public string Path { get; }
    public string Selector { get; }
    public int Line { get; }
    public int Column { get; }
    public string Version { get; }
    public bool Embedded { get; }
    public string Reason { get; }
}
