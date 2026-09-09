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
    // Which attributes carry the text a kind draws as its own label. This is what Width="Auto"
    // measures: the kind, not the engine, names its label attributes (US->FL round 3, N1).
    private static readonly Dictionary<string, Dictionary<string, IReadOnlyCollection<string>>> LabelAttributes =
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
        Register(scope, kind, factory, null, null);
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
        Register(scope, kind, factory, allowedAttributes, null);
    }

    /// <summary>
    /// Additionally declares which of the kind's attributes carry its own label text. A container
    /// child with <c>Width="Auto"</c> is measured over this set (each value resolved through
    /// translation when the attribute name ends in <c>Key</c>); kinds that register no label set
    /// cannot be Auto-measured and fall back to the unsized distribution.
    /// </summary>
    public static void Register(
        string scope,
        string kind,
        Func<IUiWidget> factory,
        IReadOnlyCollection<string>? allowedAttributes,
        IReadOnlyCollection<string>? labelAttributes)
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

            if (labelAttributes != null)
            {
                if (!LabelAttributes.TryGetValue(scope, out Dictionary<string, IReadOnlyCollection<string>>? labelSets))
                {
                    labelSets = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
                    LabelAttributes.Add(scope, labelSets);
                }

                labelSets[kind] = new HashSet<string>(labelAttributes, StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// Returns the label-attribute set for a kind (exact scope first, core scope fallback), or null
    /// when the kind declared none. Null means "not Auto-measurable", never "measure everything".
    /// </summary>
    public static IReadOnlyCollection<string>? GetLabelAttributes(string scope, string kind)
    {
        lock (Gate)
        {
            if (LabelAttributes.TryGetValue(scope, out Dictionary<string, IReadOnlyCollection<string>>? labelSets)
                && labelSets.TryGetValue(kind, out IReadOnlyCollection<string>? set))
            {
                return set;
            }

            if (!string.Equals(scope, CoreScope, StringComparison.Ordinal)
                && LabelAttributes.TryGetValue(CoreScope, out Dictionary<string, IReadOnlyCollection<string>>? coreSets)
                && coreSets.TryGetValue(kind, out IReadOnlyCollection<string>? coreSet))
            {
                return coreSet;
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
    /// <summary>
    /// Wipes every scope's registrations. Internal (FL→US round 1, item D): as a public call it let any
    /// third-party mod erase registrations it never made, process-wide, and the mods that lost their
    /// kinds would have had no way to find out who did it — the same invisibility that makes two
    /// carriers of this DLL a hazard. Only this library's own harness needs it, and
    /// <c>InternalsVisibleTo("FerriteLib.UiKit.Tests")</c> is already in place. Verified before the flip:
    /// no call site in the wired consumer's Source tree, tools or scripts.
    /// </summary>
    internal static void Clear()
    {
        lock (Gate)
        {
            Scopes.Clear();
            AttributeSchemas.Clear();
            LabelAttributes.Clear();
            coreInitialized = false;
        }
    }
}
