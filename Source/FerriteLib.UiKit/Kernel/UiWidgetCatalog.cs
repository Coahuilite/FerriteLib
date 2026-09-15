using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The read-only description of what is actually registered, for a caller that must list kinds without
/// instantiating them.
/// <para>
/// <b>Why this is not the registry.</b> <see cref="UiWidgetRegistry.Resolve"/> builds a widget: it runs the
/// consumer's factory, which may allocate, may read the game state and may be the wrong thing to do at all
/// while a settings screen is being laid out. A catalogue that listed kinds by resolving them would run every
/// registered consumer's constructor on open. This one never calls a factory, never hands back a writable
/// registry, and never runs consumer code while holding the registry's lock.
/// </para>
/// <para>
/// <b>Snapshot, not a live view.</b> Registrations happen at mod load and may also happen later; each call
/// returns a fresh, isolated copy in a deterministic order (scope, then kind, both ordinal), so a caller can
/// refresh by asking again and a caller that keeps the old list keeps a truthful record of what it saw.
/// </para>
/// <para>
/// <b>Identity is the pair.</b> Every descriptor carries the scope its kind was declared in, and the lookup is
/// exact: <see cref="TryGet"/> answers "declared where", so it applies no core-scope fallback. That is a
/// different question from <see cref="UiWidgetRegistry.GetAttributeSchema"/>, which answers "what contract
/// applies to an element here" and does fall back; <c>KernelWidgetCatalogTests</c> asserts the difference
/// rather than leaving it to prose.
/// </para>
/// <para>
/// <b>The lock boundary, stated.</b> Every listing call copies registry-owned data under the registry's lock
/// and builds descriptors after releasing it, so no consumer code runs inside a listing: no factory is
/// invoked, and no collection a caller ever passed in is enumerated. The registration path's own enumeration
/// of a caller-supplied schema/label collection, and <see cref="UiWidgetRegistry.Resolve"/>'s factory call,
/// happen under that lock - both are pre-existing behaviour of the public surface this package must not
/// change, and <c>docs/development/0.6/verification/t3-catalogue-lanes.md</c> records them as not pinned here.
/// </para>
/// </summary>
public static class UiWidgetCatalog
{
    /// <summary>
    /// Every declared <c>(scope, kind)</c> pair, ordered by scope then kind, both ordinal. Kinds sharing a
    /// name across scopes appear once per scope. The returned list is a copy the caller owns.
    /// </summary>
    public static IReadOnlyList<UiWidgetDescriptor> Snapshot()
    {
        UiWidgetRegistry.CatalogDeclaration[] declarations = UiWidgetRegistry.Declarations();
        var descriptors = new List<UiWidgetDescriptor>(declarations.Length);
        for (int i = 0; i < declarations.Length; i++)
        {
            descriptors.Add(Describe(declarations[i]));
        }

        return new ReadOnlyCollection<UiWidgetDescriptor>(descriptors);
    }

    /// <summary>
    /// Looks up one kind in the scope it was declared in. False when that exact pair is not registered - a kind
    /// that only exists in another scope, or only in <see cref="UiWidgetRegistry.CoreScope"/>, is not a match,
    /// because the answer is "declared here", not "resolvable here". False also for a null or empty scope/kind.
    /// On false, <paramref name="descriptor"/> is <c>default</c>: a struct default bypasses the constructor, so
    /// its collections are null - the caller owns the answer by checking this return value.
    /// </summary>
    public static bool TryGet(string scope, string kind, out UiWidgetDescriptor descriptor)
    {
        if (!UiWidgetRegistry.TryGetDeclaration(scope, kind, out UiWidgetRegistry.CatalogDeclaration declaration))
        {
            descriptor = default;
            return false;
        }

        descriptor = Describe(declaration);
        return true;
    }

    /// <summary>
    /// Every scope that has at least one registration, ordinal order. The returned list is a copy. A scope whose
    /// last kind is gone is absent: the list is derived from the declarations, not from the registration table's
    /// keys.
    /// </summary>
    public static IReadOnlyList<string> Scopes()
    {
        return new ReadOnlyCollection<string>(UiWidgetRegistry.DeclaredScopes());
    }

    /// <summary>
    /// One descriptor from one declaration. This is the copy step that makes the description the caller's own,
    /// and it deliberately happens outside the registry's lock - the lock is only ever held while
    /// registry-owned state is being read into plain data.
    /// </summary>
    private static UiWidgetDescriptor Describe(UiWidgetRegistry.CatalogDeclaration declaration)
    {
        return new UiWidgetDescriptor(
            declaration.Scope, declaration.Kind, declaration.AllowedSchema, declaration.LabelSet);
    }
}
