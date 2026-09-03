using System;
using System.Collections.Generic;
using System.Linq;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Greenfield widget registry. Core registration is explicit (<see cref="InitializeCore"/>), not a
/// static constructor side effect. Scope resolution is exact-scope first, core scope fallback.
/// Kinds may register an allowed-attribute schema so the Host can reject unknown attributes at
/// creation time.
/// </summary>
public static class UiWidgetRegistry
{
    public const string CoreScope = "core";

    private static readonly object Gate = new();
    private static readonly Dictionary<string, Dictionary<string, Func<IUiWidget>>> Scopes =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Dictionary<string, IReadOnlyCollection<string>>> AttributeSchemas =
        new(StringComparer.Ordinal);
    private static bool coreInitialized;

    public static void InitializeCore()
    {
        lock (Gate)
        {
            if (coreInitialized) return;
            KernelCoreWidgetRegistrar.RegisterAll();
            coreInitialized = true;
        }
    }

    public static void Register(string scope, string kind, Func<IUiWidget> factory)
    {
        Register(scope, kind, factory, null);
    }

    /// <summary>
    /// Registers a widget kind with its allowed-attribute contract. When a schema is supplied, the
    /// Host rejects any attribute outside the schema at creation time (Id/Kind/Hidden/Tab are always
    /// allowed on top of it). Kinds registered without a schema are not attribute-checked.
    /// </summary>
    public static void Register(
        string scope,
        string kind,
        Func<IUiWidget> factory,
        IReadOnlyCollection<string>? allowedAttributes)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (kind == null) throw new ArgumentNullException(nameof(kind));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        lock (Gate)
        {
            if (!Scopes.TryGetValue(scope, out Dictionary<string, Func<IUiWidget>>? registry))
            {
                registry = new Dictionary<string, Func<IUiWidget>>(StringComparer.Ordinal);
                Scopes.Add(scope, registry);
            }

            if (registry.ContainsKey(kind))
            {
                throw new InvalidOperationException(
                    $"Widget kind '{kind}' is already registered in scope '{scope}'; duplicate registration is rejected. "
                    + "Clear the registry first (test reset) or use a distinct kind/scope.");
            }

            registry.Add(kind, factory);

            if (allowedAttributes != null)
            {
                if (!AttributeSchemas.TryGetValue(scope, out Dictionary<string, IReadOnlyCollection<string>>? schemas))
                {
                    schemas = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
                    AttributeSchemas.Add(scope, schemas);
                }

                schemas[kind] = new HashSet<string>(allowedAttributes, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Returns the allowed-attribute schema for a kind (exact scope first, core scope fallback), or
    /// null when the kind registered no schema.
    /// </summary>
    public static IReadOnlyCollection<string>? GetAttributeSchema(string scope, string kind)
    {
        lock (Gate)
        {
            if (AttributeSchemas.TryGetValue(scope, out Dictionary<string, IReadOnlyCollection<string>>? schemas)
                && schemas.TryGetValue(kind, out IReadOnlyCollection<string>? schema))
            {
                return schema;
            }

            if (!string.Equals(scope, CoreScope, StringComparison.Ordinal)
                && AttributeSchemas.TryGetValue(CoreScope, out Dictionary<string, IReadOnlyCollection<string>>? coreSchemas)
                && coreSchemas.TryGetValue(kind, out IReadOnlyCollection<string>? coreSchema))
            {
                return coreSchema;
            }
        }

        return null;
    }

    public static IUiWidget Resolve(string scope, string kind)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (kind == null) throw new ArgumentNullException(nameof(kind));

        lock (Gate)
        {
            if (Scopes.TryGetValue(scope, out Dictionary<string, Func<IUiWidget>>? registry)
                && registry.TryGetValue(kind, out Func<IUiWidget>? factory))
            {
                return factory();
            }

            if (!string.Equals(scope, CoreScope, StringComparison.Ordinal)
                && Scopes.TryGetValue(CoreScope, out Dictionary<string, Func<IUiWidget>>? core)
                && core.TryGetValue(kind, out Func<IUiWidget>? coreFactory))
            {
                return coreFactory();
            }
        }

        throw new UiUnknownWidgetKindException(scope, kind);
    }

    public static IReadOnlyCollection<string> KnownKinds(string? scope = null)
    {
        lock (Gate)
        {
            if (scope != null)
            {
                if (!Scopes.TryGetValue(scope, out Dictionary<string, Func<IUiWidget>>? registry))
                {
                    return Array.Empty<string>();
                }

                return registry.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            }

            var all = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Dictionary<string, Func<IUiWidget>> registry in Scopes.Values)
            {
                foreach (string kind in registry.Keys)
                {
                    all.Add(kind);
                }
            }

            return all.ToArray();
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Scopes.Clear();
            AttributeSchemas.Clear();
            coreInitialized = false;
        }
    }
}
