using System;
using System.Collections.Generic;
using System.Linq;

namespace FerriteLib.UiKit.Kernel;
/// <summary>One immutable kind declaration: factory, schema, labels, natural width and font have one owner.</summary>
public sealed class UiWidgetDefinition
{
    public UiWidgetDefinition(string scope, string kind, Func<IUiWidget> factory, UiAttributeSchema? schema = null, IReadOnlyCollection<string>? labels = null, Func<UiElementSpec, UiWidgetContext, float>? naturalBody = null, UiFont fallbackFont = UiFont.Small)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Schema = schema;
        Labels = labels == null ? null : Array.AsReadOnly(labels.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        NaturalBody = naturalBody;
        FallbackFont = fallbackFont;
    }

    internal UiWidgetDefinition(string scope, string kind, Func<IUiWidget> factory, IReadOnlyCollection<string>? attributes, IReadOnlyCollection<string>? labels, Func<UiElementSpec, UiWidgetContext, float>? naturalBody, UiAppearanceResolver? looks) : this(scope, kind, factory, attributes == null ? null : UiAttributeSchema.FromNames(attributes), labels, naturalBody)
    {
        Looks = looks;
    }

    internal UiWidgetDefinition(string scope, string kind, Func<IUiWidget> factory, IReadOnlyCollection<string> attributes, IReadOnlyCollection<string>? labels = null, Func<UiElementSpec, UiWidgetContext, float>? naturalBody = null, UiFont fallbackFont = UiFont.Small) : this(scope, kind, factory, UiAttributeSchema.FromNames(attributes), labels, naturalBody, fallbackFont)
    {
    }

    public string Scope { get; }
    public string Kind { get; }
    public UiAttributeSchema? Schema { get; }
    public IReadOnlyCollection<string>? Attributes => Schema?.Names;
    public IReadOnlyCollection<string>? Labels { get; }
    public Func<UiElementSpec, UiWidgetContext, float>? NaturalBody { get; }
    public UiFont FallbackFont { get; }
    internal Func<IUiWidget> Factory { get; }
    internal UiAppearanceResolver? Looks { get; }
}

/// <summary>Kind lookup. Every authoring and catalog read resolves the same immutable definition.</summary>
public static class UiWidgetRegistry
{
    public const string CoreScope = "core";
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Dictionary<string, UiWidgetDefinition>> definitions = new(StringComparer.Ordinal);
    private static bool coreInitialized;
    public static void InitializeCore()
    {
        lock (Gate)
        {
            if (coreInitialized)
                return;
            KernelCoreWidgetRegistrar.RegisterAll();
            coreInitialized = true;
        }
    }

    public static void Define(UiWidgetDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        lock (Gate)
        {
            if (!definitions.TryGetValue(definition.Scope, out var scope))
                definitions.Add(definition.Scope, scope = new Dictionary<string, UiWidgetDefinition>(StringComparer.Ordinal));
            if (scope.ContainsKey(definition.Kind))
                throw new InvalidOperationException("Widget kind '" + definition.Kind + "' is already defined in scope '" + definition.Scope + "'.");
            scope.Add(definition.Kind, definition);
        }
    }

    private static UiWidgetDefinition? Find(string scope, string kind, bool fallback = true)
    {
        if (definitions.TryGetValue(scope, out var local) && local.TryGetValue(kind, out var d))
            return d;
        return fallback && scope != CoreScope && definitions.TryGetValue(CoreScope, out var core) && core.TryGetValue(kind, out var c) ? c : null;
    }

    public static IUiWidget Resolve(string scope, string kind)
    {
        UiWidgetDefinition? definition;
        lock (Gate)
            definition = Find(scope, kind);
        return definition?.Factory() ?? throw new UiUnknownWidgetKindException(scope, kind);
    }

    internal static UiAttributeSchema? Schema(string scope, string kind)
    {
        lock (Gate)
            return Find(scope, kind)?.Schema;
    }

    public static IReadOnlyCollection<string>? GetAttributeSchema(string scope, string kind)
    {
        lock (Gate)
            return Find(scope, kind)?.Attributes;
    }

    public static IReadOnlyCollection<string>? GetLabelAttributes(string scope, string kind)
    {
        lock (Gate)
            return Find(scope, kind)?.Labels;
    }

    public static Func<UiElementSpec, UiWidgetContext, float>? GetNaturalBody(string scope, string kind)
    {
        lock (Gate)
            return Find(scope, kind)?.NaturalBody;
    }

    public static UiFont GetFallbackFont(string scope, string kind)
    {
        lock (Gate)
            return Find(scope, kind)?.FallbackFont ?? UiFont.Small;
    }

    internal static UiAppearanceResolver? GetAppearanceResolver(string scope, string kind)
    {
        lock (Gate)
            return Find(scope, kind)?.Looks;
    }

    public static IReadOnlyCollection<string> KnownKinds(string? scope = null)
    {
        lock (Gate)
            return scope != null ? (definitions.TryGetValue(scope, out var d) ? d.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray() : Array.Empty<string>()) : definitions.Values.SelectMany(d => d.Keys).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToArray();
    }

    internal readonly struct CatalogDeclaration
    {
        internal CatalogDeclaration(UiWidgetDefinition d)
        {
            Scope = d.Scope;
            Kind = d.Kind;
            AllowedSchema = d.Attributes;
            LabelSet = d.Labels;
        }

        internal string Scope { get; }
        internal string Kind { get; }
        internal IReadOnlyCollection<string>? AllowedSchema { get; }
        internal IReadOnlyCollection<string>? LabelSet { get; }
    }

    internal static CatalogDeclaration[] Declarations()
    {
        lock (Gate)
            return definitions.Values.SelectMany(d => d.Values).OrderBy(d => d.Scope, StringComparer.Ordinal).ThenBy(d => d.Kind, StringComparer.Ordinal).Select(d => new CatalogDeclaration(d)).ToArray();
    }

    internal static string[] DeclaredScopes()
    {
        lock (Gate)
            return definitions.Where(d => d.Value.Count > 0).Select(d => d.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
    }

    internal static bool TryGetDeclaration(string? scope, string? kind, out CatalogDeclaration declaration)
    {
        lock (Gate)
        {
            var d = string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(kind) ? null : Find(scope!, kind!, false);
            declaration = d == null ? default : new CatalogDeclaration(d);
            return d != null;
        }
    }

    internal static void Clear()
    {
        lock (Gate)
        {
            definitions.Clear();
            coreInitialized = false;
        }
    }
}
