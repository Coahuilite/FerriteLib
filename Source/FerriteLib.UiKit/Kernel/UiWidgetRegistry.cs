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
    // The non-text half of a kind's natural width: the body a kind draws (a box, a track and its spacing)
    // that a label-set measurement cannot see. Kind-level metadata on purpose - it is declared at
    // registration exactly like the schema and the label set, so nothing has to reach a widget INSTANCE to
    // measure, and IUiWidget.Measure keeps its one signature. Absent means "text-only", the previous answer.
    private static readonly Dictionary<string, Dictionary<string, Func<UiElementSpec, UiWidgetContext, float>>> NaturalBodies =
        new(StringComparer.Ordinal);
    // The looks a kind can draw, declared at registration like the schema and the label set. This is the
    // set the shared appearance seam (UiAppearanceResolver) resolves an element's `Appearance` against, and
    // it is kind-level metadata on purpose: the natural-body entry point measures a look WITHOUT a widget
    // instance, so the accepted names have to be readable without reaching one. A kind that declares none
    // has no appearance axis at all, which is every kind but `input/checkbox` today.
    private static readonly Dictionary<string, Dictionary<string, UiAppearanceResolver>> SupportedLooks =
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
        Register(scope, kind, factory, allowedAttributes, labelAttributes, null);
    }

    /// <summary>
    /// The same registration plus the kind's non-text natural-width contribution. This is a NEW overload
    /// rather than an optional parameter on the one above: adding a parameter changes that method's CLR
    /// signature, so an already-compiled consumer would stop binding. The five-parameter overload stays
    /// exactly as it was, and a kind that declares no body keeps the text-only answer.
    /// </summary>
    public static void Register(
        string scope,
        string kind,
        Func<IUiWidget> factory,
        IReadOnlyCollection<string>? allowedAttributes,
        IReadOnlyCollection<string>? labelAttributes,
        Func<UiElementSpec, UiWidgetContext, float>? naturalBody)
    {
        Register(scope, kind, factory, allowedAttributes, labelAttributes, naturalBody, null);
    }

    /// <summary>
    /// The same registration plus the kind's declared appearance looks - the set the shared seam
    /// (<see cref="UiAppearanceResolver"/>) resolves an element's <c>Appearance</c> against, and the set a
    /// creation-time contract validates a declaration against. A NEW overload again, and for the same
    /// reason as the one above: the six-parameter overload keeps its exact CLR signature, so a kind
    /// registered through it keeps binding and keeps its behaviour.
    /// <para>
    /// <b>Internal, and that is a boundary rather than an oversight.</b> The seam is this batch's shape and
    /// no consumer has asked for an appearance axis on its own kind, so it stays inside the assembly while
    /// the one kind that has looks uses it. Exposing it would be an addition in <c>docs/api-tiers.md</c> and
    /// a vocabulary a stranger's kind would then be frozen to, which is exactly the decision a citation is
    /// supposed to trigger. Re-open it with a consumer that has hand-rolled the same axis.
    /// </para>
    /// </summary>
    internal static void Register(
        string scope,
        string kind,
        Func<IUiWidget> factory,
        IReadOnlyCollection<string>? allowedAttributes,
        IReadOnlyCollection<string>? labelAttributes,
        Func<UiElementSpec, UiWidgetContext, float>? naturalBody,
        UiAppearanceResolver? supportedLooks)
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

            if (naturalBody != null)
            {
                if (!NaturalBodies.TryGetValue(scope, out Dictionary<string, Func<UiElementSpec, UiWidgetContext, float>>? bodies))
                {
                    bodies = new Dictionary<string, Func<UiElementSpec, UiWidgetContext, float>>(StringComparer.Ordinal);
                    NaturalBodies.Add(scope, bodies);
                }

                bodies[kind] = naturalBody;
            }

            if (supportedLooks != null)
            {
                if (!SupportedLooks.TryGetValue(scope, out Dictionary<string, UiAppearanceResolver>? looks))
                {
                    looks = new Dictionary<string, UiAppearanceResolver>(StringComparer.Ordinal);
                    SupportedLooks.Add(scope, looks);
                }

                looks[kind] = supportedLooks;
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

    /// <summary>
    /// Returns a kind's non-text natural-width contribution (exact scope first, core scope fallback), or
    /// null when the kind declared none. This is the body half of <c>Width="Auto"</c>: a box, a track or a
    /// thumb that the label measurement cannot see. Null keeps the previous, text-only answer, so a kind
    /// that declares nothing is measured exactly as it was before this seam existed.
    /// </summary>
    public static Func<UiElementSpec, UiWidgetContext, float>? GetNaturalBody(string scope, string kind)
    {
        lock (Gate)
        {
            if (NaturalBodies.TryGetValue(scope, out Dictionary<string, Func<UiElementSpec, UiWidgetContext, float>>? bodies)
                && bodies.TryGetValue(kind, out Func<UiElementSpec, UiWidgetContext, float>? body))
            {
                return body;
            }

            if (!string.Equals(scope, CoreScope, StringComparison.Ordinal)
                && NaturalBodies.TryGetValue(CoreScope, out Dictionary<string, Func<UiElementSpec, UiWidgetContext, float>>? coreBodies)
                && coreBodies.TryGetValue(kind, out Func<UiElementSpec, UiWidgetContext, float>? coreBody))
            {
                return coreBody;
            }
        }

        return null;
    }

    /// <summary>
    /// The appearance seam a kind declared (exact scope first, core scope fallback), or null when the kind
    /// has no appearance axis. The resolver it hands back is the same object the kind registered, so the
    /// set a caller validates against and the set the widget resolves through cannot be two lists.
    /// </summary>
    internal static UiAppearanceResolver? GetAppearanceResolver(string scope, string kind)
    {
        lock (Gate)
        {
            if (SupportedLooks.TryGetValue(scope, out Dictionary<string, UiAppearanceResolver>? looks)
                && looks.TryGetValue(kind, out UiAppearanceResolver? resolver))
            {
                return resolver;
            }

            if (!string.Equals(scope, CoreScope, StringComparison.Ordinal)
                && SupportedLooks.TryGetValue(CoreScope, out Dictionary<string, UiAppearanceResolver>? coreLooks)
                && coreLooks.TryGetValue(kind, out UiAppearanceResolver? coreResolver))
            {
                return coreResolver;
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
    /// One declared <c>(scope, kind)</c> pair with the two optional collections the registry holds for it.
    /// Internal because the description surface (<see cref="UiWidgetCatalog"/>) is the only caller: a public
    /// shape here would be a second promise to keep in step with the tier list, and the frozen 0.6 contract
    /// adds a type rather than widening this one.
    /// <para>
    /// <see cref="AllowedSchema"/> and <see cref="LabelSet"/> are the registry's own collections. They are
    /// built once at registration and never mutated afterwards (<see cref="Register"/> replaces an entry, it
    /// does not edit one; <see cref="Clear"/> drops the tables, not the sets), so handing the references back is
    /// safe - and the description layer copies them before exposing anything, so no caller can reach registry
    /// state through a descriptor.
    /// </para>
    /// </summary>
    internal readonly struct CatalogDeclaration
    {
        internal CatalogDeclaration(
            string scope,
            string kind,
            IReadOnlyCollection<string>? allowedSchema,
            IReadOnlyCollection<string>? labelSet)
        {
            Scope = scope;
            Kind = kind;
            AllowedSchema = allowedSchema;
            LabelSet = labelSet;
        }

        internal string Scope { get; }

        internal string Kind { get; }

        internal IReadOnlyCollection<string>? AllowedSchema { get; }

        internal IReadOnlyCollection<string>? LabelSet { get; }
    }

    /// <summary>
    /// Every declaration, ordered by scope then kind (both ordinal), as one array the caller owns. This is the
    /// single read the catalogue needs: it walks the registration keys, invokes no factory, and runs no consumer
    /// code. It deliberately does not apply the core-scope fallback of <see cref="Resolve"/>: a declaration
    /// belongs to the scope it was registered in, and "this kind also resolves from core" is a different
    /// question from "where is it declared".
    /// <para>
    /// The sort lives here rather than in the catalogue so every description path shares one order and no caller
    /// can inherit <see cref="Dictionary{TKey,TValue}"/> enumeration order by accident. The whole body is inside
    /// the registry lock, and everything it touches is registry-owned state.
    /// </para>
    /// </summary>
    internal static CatalogDeclaration[] Declarations()
    {
        lock (Gate)
        {
            int count = 0;
            foreach (Dictionary<string, Func<IUiWidget>> registry in Scopes.Values)
            {
                count += registry.Count;
            }

            var declarations = new List<CatalogDeclaration>(count);
            foreach (KeyValuePair<string, Dictionary<string, Func<IUiWidget>>> scope in Scopes)
            {
                foreach (string kind in scope.Value.Keys)
                {
                    declarations.Add(new CatalogDeclaration(
                        scope.Key, kind, SchemaIn(scope.Key, kind), LabelsIn(scope.Key, kind)));
                }
            }

            declarations.Sort(CompareDeclarations);
            return declarations.ToArray();
        }
    }

    /// <summary>
    /// The scopes that still have at least one declaration, ordinal. Derived from the same walk as
    /// <see cref="Declarations"/> rather than from the table's keys, so a scope key carrying no kinds - which
    /// the public surface cannot produce, and which a lane therefore injects to test this - cannot be listed as
    /// if it had any.
    /// </summary>
    internal static string[] DeclaredScopes()
    {
        lock (Gate)
        {
            var scopes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Dictionary<string, Func<IUiWidget>>> scope in Scopes)
            {
                if (scope.Value.Count > 0)
                {
                    scopes.Add(scope.Key);
                }
            }

            return scopes.ToArray();
        }
    }

    /// <summary>
    /// The exact declaration for one <c>(scope, kind)</c> pair, with no core-scope fallback, or false when that
    /// pair was never registered. Null or empty input is "not declared" rather than an argument error: this is
    /// the lookup behind <see cref="UiWidgetCatalog.TryGet(string, string, out UiWidgetDescriptor)"/>, which a
    /// catalogue UI calls while walking data it may not control.
    /// </summary>
    internal static bool TryGetDeclaration(string? scope, string? kind, out CatalogDeclaration declaration)
    {
        declaration = default;
        if (string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(kind))
        {
            return false;
        }

        lock (Gate)
        {
            if (!Scopes.TryGetValue(scope!, out Dictionary<string, Func<IUiWidget>>? registry)
                || !registry.ContainsKey(kind!))
            {
                return false;
            }

            declaration = new CatalogDeclaration(scope!, kind!, SchemaIn(scope!, kind!), LabelsIn(scope!, kind!));
            return true;
        }
    }

    /// <summary>
    /// The schema declared for one exact scope/kind pair, or null. It is the exact lookup
    /// <see cref="GetAttributeSchema"/> performs <b>without</b> its core-scope fallback, and the two cannot be
    /// one method: the public one answers "what contract applies here", the catalogue answers "what did this
    /// scope declare".
    /// </summary>
    private static IReadOnlyCollection<string>? SchemaIn(string scope, string kind)
    {
        return AttributeSchemas.TryGetValue(scope, out Dictionary<string, IReadOnlyCollection<string>>? schemas)
            && schemas.TryGetValue(kind, out IReadOnlyCollection<string>? schema)
            ? schema
            : null;
    }

    /// <summary>The label set declared for one exact scope/kind pair, or null. The exact half of <see cref="GetLabelAttributes"/>.</summary>
    private static IReadOnlyCollection<string>? LabelsIn(string scope, string kind)
    {
        return LabelAttributes.TryGetValue(scope, out Dictionary<string, IReadOnlyCollection<string>>? labelSets)
            && labelSets.TryGetValue(kind, out IReadOnlyCollection<string>? set)
            ? set
            : null;
    }

    private static int CompareDeclarations(CatalogDeclaration left, CatalogDeclaration right)
    {
        int byScope = string.CompareOrdinal(left.Scope, right.Scope);
        return byScope != 0 ? byScope : string.CompareOrdinal(left.Kind, right.Kind);
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
            NaturalBodies.Clear();
            SupportedLooks.Clear();
            coreInitialized = false;
        }
    }
}
