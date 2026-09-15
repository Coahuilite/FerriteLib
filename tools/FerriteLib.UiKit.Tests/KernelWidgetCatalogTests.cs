using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// T3 lane: the read-only widget description that keeps scope.
/// <para>
/// The claims, one lane each:
/// <list type="bullet">
/// <item><b>Complete and ordered.</b> <c>Snapshot()</c> returns every declared <c>(scope, kind)</c> pair -
/// including two scopes that both declare <c>alpha</c> - in scope-then-kind ordinal order, with attribute
/// collections sorted the same way.</item>
/// <item><b>Isolated.</b> Each call returns a fresh list and fresh collections; a caller cannot mutate what it
/// got, and mutating a collection a caller passed to <c>Register</c> or to the descriptor constructor cannot
/// move the registry or the descriptor.</item>
/// <item><b>Exact identity.</b> <c>TryGet</c> matches the declared pair only: a kind that exists in another
/// scope, or only in <c>core</c>, is false. The lane asserts the contrast with
/// <c>GetAttributeSchema</c>, which <i>does</i> fall back to core.</item>
/// <item><b>Scopes.</b> Ordered, complete, and a scope carrying no kinds is absent - injected into the private
/// table by reflection, because the public surface cannot produce that state.</item>
/// <item><b>Zero factories.</b> Listing never invokes a factory: proven twice, with a counting factory and with
/// one that throws (whose kind is still described).</item>
/// <item><b>Undeclared is not forbidden.</b> A kind with no schema still lists with
/// <c>HasAttributeSchema == false</c>; a kind that declared an <i>empty</i> schema is still a declaration.</item>
/// <item><b>Late registration.</b> A kind registered after a snapshot appears in the next snapshot, and the
/// earlier snapshot stays unchanged.</item>
/// <item><b>Lock boundary.</b> No listing call enumerates a caller-supplied collection or runs a factory, so
/// no consumer code runs while the registry lock is held on any listing path. What this does NOT pin:
/// <c>Register</c>'s own copy of a caller-supplied collection and <c>Resolve</c>'s factory call both happen
/// under that lock; they are pre-existing public behaviour this package must not change, and the lane records
/// the registration-time observation without freezing it.</item>
/// </list>
/// </para>
/// </summary>
internal static class KernelWidgetCatalogTests
{
    private const string ScopeA = "cat-a";
    private const string ScopeB = "cat-b";

    private static int failures;
    private static int factoryCalls;
    private static int throwingFactoryCalls;

    public static int RunAll()
    {
        failures = 0;
        Run("Snapshot lists every declared pair, scope-major and ordinal", VerifySnapshotIsCompleteAndOrdered);
        Run("Snapshot and its descriptors are copies, not registry state", VerifySnapshotIsIsolated);
        Run("TryGet answers the exact declared pair and never the core fallback", VerifyTryGetIsExact);
        Run("Scopes() is complete, ordinal and free of empty scopes", VerifyScopesAreComplete);
        Run("Listing invokes no factory, even one that throws", VerifyListingInvokesNoFactory);
        Run("A kind with no declared schema or label set still lists", VerifyUndeclaredSchemaStillLists);
        Run("A kind registered after a snapshot appears in the next one", VerifyLateRegistration);
        Run("Listing runs no consumer code while the registry lock is held", VerifyNoConsumerCodeUnderTheLock);

        // Leave the process in the state every later lane expects: core kinds registered, no probe scopes.
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        return failures;
    }

    // --- lanes -----------------------------------------------------------------------------------

    private static void VerifySnapshotIsCompleteAndOrdered()
    {
        Reset();
        Register(ScopeB, "alpha", new[] { "BetaAttr", "AlphaAttr" });
        Register(ScopeB, "gamma");
        Register(ScopeA, "zeta", new[] { "Zeta" }, new[] { "Label", "LabelKey" });
        Register(ScopeA, "alpha", new[] { "AlphaInA" });
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, "probe-core", () => new ProbeWidget(), new[] { "CoreAttr" });

        IReadOnlyList<UiWidgetDescriptor> snapshot = UiWidgetCatalog.Snapshot();
        string[] actual = Pairs(snapshot);
        Check(
            SameSequence(actual, new[] { "cat-a/alpha", "cat-a/zeta", "cat-b/alpha", "cat-b/gamma", "core/probe-core" }),
            "every declared pair is listed once, ordered by scope then kind: " + string.Join(", ", actual));

        UiWidgetDescriptor alphaInA = snapshot[0];
        UiWidgetDescriptor alphaInB = snapshot[2];
        Check(alphaInA.Kind == "alpha" && alphaInB.Kind == "alpha" && alphaInA.Scope != alphaInB.Scope,
            "two scopes that both declare the same kind name appear once per scope");
        Check(
            SameSequence(ToList(alphaInB.AllowedAttributes), new[] { "AlphaAttr", "BetaAttr" }),
            "the attribute collection is ordinal-sorted, not registration-ordered: "
            + string.Join(", ", ToList(alphaInB.AllowedAttributes)));
        Check(
            SameSequence(ToList(snapshot[1].LabelAttributes), new[] { "Label", "LabelKey" }),
            "and so is the label set, so a catalogue renders what is declared and not the insertion sequence");
    }

    private static void VerifySnapshotIsIsolated()
    {
        Reset();
        Register(ScopeA, "zeta", new[] { "Zeta" });

        IReadOnlyList<UiWidgetDescriptor> first = UiWidgetCatalog.Snapshot();
        IReadOnlyList<UiWidgetDescriptor> second = UiWidgetCatalog.Snapshot();
        Check(!ReferenceEquals(first, second), "each call returns a fresh list");
        Check(!ReferenceEquals(first[0].AllowedAttributes, second[0].AllowedAttributes),
            "and fresh per-descriptor collections rather than one shared table view");

        Check(Throws<NotSupportedException>(() => ((IList<UiWidgetDescriptor>)first).Add(default)),
            "the returned list refuses mutation even when a caller casts it to IList");
        Check(Throws<NotSupportedException>(() => ((IList<string>)first[0].AllowedAttributes).Add("Injected")),
            "and so does a descriptor's attribute collection");

        // A caller that keeps the collection it registered and edits it must not move the registry.
        var callerSchema = new List<string> { "One", "Two" };
        var callerLabels = new List<string> { "Label" };
        UiWidgetRegistry.Register("cat-mut", "kind", () => new ProbeWidget(), callerSchema, callerLabels);
        UiWidgetCatalog.TryGet("cat-mut", "kind", out UiWidgetDescriptor described);
        callerSchema.Clear();
        callerLabels.Clear();
        Check(SameSequence(ToList(described.AllowedAttributes), new[] { "One", "Two" }),
            "mutating the list after registration cannot change the descriptor that was already built");
        Check(
            UiWidgetRegistry.GetAttributeSchema("cat-mut", "kind") is IReadOnlyCollection<string> live
                && SameSequence(ToList(live), new[] { "One", "Two" }),
            "nor the registry's own copy");

        // And the descriptor constructor honours the same promise for a caller that passes its own collection.
        var directSchema = new List<string> { "Direct" };
        var directLabels = new List<string> { "DirectLabel" };
        var direct = new UiWidgetDescriptor("cat-mut", "direct", directSchema, directLabels);
        directSchema.Clear();
        directLabels.Clear();
        Check(SameSequence(ToList(direct.AllowedAttributes), new[] { "Direct" })
            && SameSequence(ToList(direct.LabelAttributes), new[] { "DirectLabel" }),
            "a descriptor built from a caller's collection owns a copy of it");

        Check(Pairs(UiWidgetCatalog.Snapshot()).Length == 2,
            "and none of those mutations added, removed or reordered a declaration");
    }

    private static void VerifyTryGetIsExact()
    {
        Reset();
        Register(ScopeA, "zeta", new[] { "Zeta" });
        Register(ScopeB, "gamma");
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, "probe-core", () => new ProbeWidget(), new[] { "CoreAttr" });

        Check(
            UiWidgetCatalog.TryGet(ScopeA, "zeta", out UiWidgetDescriptor zeta)
                && zeta.Scope == ScopeA && zeta.Kind == "zeta",
            "an exact pair is found and reports the scope it was declared in");
        Check(!UiWidgetCatalog.TryGet(ScopeB, "zeta", out _), "a kind declared in another scope is not a match");
        Check(!UiWidgetCatalog.TryGet(ScopeA, "gamma", out _), "and neither is one declared elsewhere again");
        Check(!UiWidgetCatalog.TryGet(ScopeA, "nobody-registered-this", out _), "an unknown kind is not a match");

        Check(UiWidgetRegistry.GetAttributeSchema(ScopeA, "probe-core") != null,
            "the engine's schema read falls back to the core scope - the behaviour the catalogue must not copy");
        Check(!UiWidgetCatalog.TryGet(ScopeA, "probe-core", out _),
            "while TryGet answers 'declared here', so the core-scope kind is false in another scope");
        Check(
            UiWidgetCatalog.TryGet(UiWidgetRegistry.CoreScope, "probe-core", out UiWidgetDescriptor core)
                && core.Scope == UiWidgetRegistry.CoreScope,
            "and true in the scope that declares it");

        Check(!UiWidgetCatalog.TryGet(null!, "zeta", out _)
            && !UiWidgetCatalog.TryGet(ScopeA, null!, out _)
            && !UiWidgetCatalog.TryGet("", "", out _),
            "null or empty input is 'not declared', not an argument error, because a catalogue walks data it may not control");
    }

    private static void VerifyScopesAreComplete()
    {
        Reset();
        Register(ScopeA, "zeta");
        Register(ScopeB, "alpha");
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, "probe-core", () => new ProbeWidget());

        IReadOnlyList<string> scopes = UiWidgetCatalog.Scopes();
        Check(SameSequence(ToList(scopes), new[] { "cat-a", "cat-b", "core" }),
            "every scope with a registration, ordinal: " + string.Join(", ", ToList(scopes)));
        Check(!ReferenceEquals(scopes, UiWidgetCatalog.Scopes()), "each call returns a fresh list");
        Check(Throws<NotSupportedException>(() => ((IList<string>)scopes).Add("Injected")),
            "and the copy refuses mutation through IList");

        // The public surface cannot leave a scope key with no kinds, so the state is injected: a scope whose
        // kinds are all gone must be absent from the listing, not rendered as an empty row.
        InjectEmptyScope("cat-empty");
        Check(!Contains(ToList(UiWidgetCatalog.Scopes()), "cat-empty"),
            "a scope with no kinds is absent from Scopes()");
        Check(!Contains(Pairs(UiWidgetCatalog.Snapshot()), "cat-empty/"), "and no pair claims it");
        Check(Pairs(UiWidgetCatalog.Snapshot()).Length == 3, "while every real pair is still there");

        UiWidgetRegistry.Clear();
        Check(UiWidgetCatalog.Scopes().Count == 0 && UiWidgetCatalog.Snapshot().Count == 0,
            "clearing the registry empties both the scope list and the snapshot");
    }

    private static void VerifyListingInvokesNoFactory()
    {
        Reset();
        factoryCalls = 0;
        throwingFactoryCalls = 0;
        UiWidgetRegistry.Register("cat-factory", "counted", () => { factoryCalls++; return new ProbeWidget(); });
        UiWidgetRegistry.Register(
            "cat-factory",
            "throwing",
            () =>
            {
                throwingFactoryCalls++;
                throw new InvalidOperationException("a factory must not run while listing");
            },
            new[] { "Attr" });

        for (int i = 0; i < 3; i++)
        {
            UiWidgetCatalog.Snapshot();
            UiWidgetCatalog.Scopes();
            UiWidgetCatalog.TryGet("cat-factory", "counted", out _);
            UiWidgetCatalog.TryGet("cat-factory", "throwing", out _);
            UiWidgetCatalog.TryGet("cat-factory", "absent", out _);
        }

        Check(factoryCalls == 0, "the counting factory was never invoked by any listing call");
        Check(throwingFactoryCalls == 0, "nor was the throwing one: a listing that resolved to describe would throw here");
        Check(
            UiWidgetCatalog.TryGet("cat-factory", "throwing", out UiWidgetDescriptor throwing)
                && throwing.Kind == "throwing" && throwing.HasAttributeSchema,
            "listing still describes the kind whose factory throws, with its declared schema");

        // The premise, proven rather than assumed: the factory really does throw - only explicit resolution runs it.
        try
        {
            UiWidgetRegistry.Resolve("cat-factory", "throwing");
            Check(false, "an explicit Resolve ran the throwing factory without raising it");
        }
        catch (InvalidOperationException ex)
        {
            Check(
                ex.Message.Contains("a factory must not run while listing"),
                "an explicit Resolve is what runs the factory, and it raises exactly that: " + ex.Message);
        }

        Check(throwingFactoryCalls == 1, "and listing still never ran it");
    }

    private static void VerifyUndeclaredSchemaStillLists()
    {
        Reset();
        Register(ScopeB, "gamma");
        Register(ScopeB, "schema-only", new[] { "Alpha" });
        Register(ScopeB, "labels-only", null, new[] { "Label" });
        Register(ScopeB, "empty-schema", Array.Empty<string>());

        Check(
            UiWidgetCatalog.TryGet(ScopeB, "gamma", out UiWidgetDescriptor gamma)
                && gamma.HasAttributeSchema == false && gamma.AllowedAttributes.Count == 0,
            "a kind registered with no schema still lists, with HasAttributeSchema false");
        Check(!gamma.HasLabelSet && gamma.LabelAttributes.Count == 0, "and HasLabelSet false, not 'measure everything'");
        Check(UiWidgetRegistry.GetAttributeSchema(ScopeB, "gamma") == null,
            "the registry agrees it declared none, so the descriptor's false is the truth and not a default");

        UiWidgetCatalog.TryGet(ScopeB, "schema-only", out UiWidgetDescriptor schemaOnly);
        Check(schemaOnly.HasAttributeSchema && !schemaOnly.HasLabelSet,
            "a kind that declared only a schema reports exactly that half");

        UiWidgetCatalog.TryGet(ScopeB, "labels-only", out UiWidgetDescriptor labelsOnly);
        Check(!labelsOnly.HasAttributeSchema && labelsOnly.HasLabelSet,
            "and one that declared only a label set reports the other half");

        UiWidgetCatalog.TryGet(ScopeB, "empty-schema", out UiWidgetDescriptor emptySchema);
        Check(emptySchema.HasAttributeSchema && emptySchema.AllowedAttributes.Count == 0,
            "a declared-but-empty schema is still a declaration: 'not declared' and 'none allowed' are opposite claims");

        Check(Pairs(UiWidgetCatalog.Snapshot()).Length == 4, "and every one of them is in the snapshot");
    }

    private static void VerifyLateRegistration()
    {
        Reset();
        Register(ScopeA, "zeta");

        IReadOnlyList<UiWidgetDescriptor> before = UiWidgetCatalog.Snapshot();
        Check(!Contains(Pairs(before), "cat-late/newcomer"), "the newcomer is not in the snapshot taken before it existed");

        Register("cat-late", "newcomer", new[] { "Attr" });
        Check(!Contains(Pairs(before), "cat-late/newcomer"),
            "and the snapshot already taken is unchanged: it is a record, not a live view");

        IReadOnlyList<UiWidgetDescriptor> after = UiWidgetCatalog.Snapshot();
        Check(after.Count == before.Count + 1 && Contains(Pairs(after), "cat-late/newcomer"),
            "the next snapshot sees the late registration with no restart and no refresh call");
        Check(UiWidgetCatalog.TryGet("cat-late", "newcomer", out _),
            "and an exact lookup answers for it immediately");
        Check(Contains(ToList(UiWidgetCatalog.Scopes()), "cat-late"),
            "and its scope appeared in the scope list");

        Register("cat-late", "second");
        Check(Pairs(UiWidgetCatalog.Snapshot()).Length == 3,
            "a second late registration is seen too, so nothing was cached on the first listing");
    }

    private static void VerifyNoConsumerCodeUnderTheLock()
    {
        Reset();
        factoryCalls = 0;
        object gate = RegistryLock();
        var probe = new LockProbeCollection(gate, new[] { "Alpha", "Beta" });
        UiWidgetRegistry.Register(
            "cat-lock", "probed", () => { factoryCalls++; return new ProbeWidget(); }, probe);

        int atRegistration = probe.Enumerations;
        Console.WriteLine(
            "  note: a caller-supplied schema collection was enumerated " + atRegistration
            + " time(s) at registration, lockHeld=" + probe.LockHeldWhenEnumerated
            + " - Register's own copy, the pre-existing path this package does not change");

        probe.Reset();
        for (int i = 0; i < 3; i++)
        {
            UiWidgetCatalog.Snapshot();
            UiWidgetCatalog.Scopes();
            UiWidgetCatalog.TryGet("cat-lock", "probed", out _);
        }

        Check(probe.Enumerations == 0,
            "no listing call enumerates a caller-supplied collection, so no consumer code runs while the lock is held");
        Check(factoryCalls == 0,
            "and no listing call invokes the factory: listing touches registry-owned data only");
    }

    // --- fixture ---------------------------------------------------------------------------------

    /// <summary>
    /// A known registration set: two scopes sharing one kind name, one kind with both halves declared, one with
    /// neither, and one kind that exists only in the core scope so the fallback contrast is assertable.
    /// </summary>
    private static void Reset()
    {
        UiWidgetRegistry.Clear();
        factoryCalls = 0;
        throwingFactoryCalls = 0;
    }

    private static void Register(
        string scope, string kind, IReadOnlyCollection<string>? schema = null, IReadOnlyCollection<string>? labels = null)
    {
        UiWidgetRegistry.Register(scope, kind, () => new ProbeWidget(), schema, labels);
    }

    private static void InjectEmptyScope(string scope)
    {
        FieldInfo? field = typeof(UiWidgetRegistry).GetField("Scopes", BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.GetValue(null) is not Dictionary<string, Dictionary<string, Func<IUiWidget>>> table)
        {
            throw new Exception("the registry's scope table moved; this lane injects an empty scope into it");
        }

        table[scope] = new Dictionary<string, Func<IUiWidget>>(StringComparer.Ordinal);
    }

    private static object RegistryLock()
    {
        FieldInfo? field = typeof(UiWidgetRegistry).GetField("Gate", BindingFlags.NonPublic | BindingFlags.Static);
        return field?.GetValue(null)
            ?? throw new Exception("the registry's lock field moved; the lock-boundary lane needs it");
    }

    private static string[] Pairs(IReadOnlyList<UiWidgetDescriptor> snapshot)
    {
        var pairs = new string[snapshot.Count];
        for (int i = 0; i < snapshot.Count; i++)
        {
            pairs[i] = snapshot[i].Scope + "/" + snapshot[i].Kind;
        }

        return pairs;
    }

    private static List<string> ToList(IReadOnlyCollection<string> values)
    {
        return new List<string>(values);
    }

    private static bool SameSequence(IReadOnlyList<string> actual, IReadOnlyList<string> expected)
    {
        if (actual.Count != expected.Count) return false;
        for (int i = 0; i < actual.Count; i++)
        {
            if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal)) return false;
        }

        return true;
    }

    private static bool Contains(IReadOnlyList<string> values, string value)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
        }
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>A widget that exists only so a factory can be registered; no lane draws it.</summary>
    private sealed class ProbeWidget : IUiWidget
    {
        string IUiWidget.Kind => "probe";

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            return 0f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }

    /// <summary>
    /// A caller-supplied schema collection that records whether the registry's lock was held when it was
    /// enumerated. It is the only way to observe the lock from outside: the collection is consumer-owned, so any
    /// enumeration of it is consumer code running.
    /// </summary>
    private sealed class LockProbeCollection : IReadOnlyCollection<string>
    {
        private readonly object gate;
        private readonly string[] values;

        internal LockProbeCollection(object gate, string[] values)
        {
            this.gate = gate;
            this.values = values;
        }

        internal int Enumerations { get; private set; }

        internal bool LockHeldWhenEnumerated { get; private set; }

        public int Count => values.Length;

        public IEnumerator<string> GetEnumerator()
        {
            Enumerations++;
            LockHeldWhenEnumerated |= Monitor.IsEntered(gate);
            for (int i = 0; i < values.Length; i++)
            {
                yield return values[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void Reset()
        {
            Enumerations = 0;
            LockHeldWhenEnumerated = false;
        }
    }
}
