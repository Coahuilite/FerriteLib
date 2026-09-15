using System;
using System.Collections.Generic;

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
/// </summary>
public static class UiWidgetCatalog
{
    /// <summary>
    /// Every declared <c>(scope, kind)</c> pair, ordered by scope then kind, both ordinal. Kinds sharing a
    /// name across scopes appear once per scope. The returned list is a copy the caller owns.
    /// </summary>
    public static IReadOnlyList<UiWidgetDescriptor> Snapshot()
    {
        throw new NotImplementedException("T3: UiWidgetCatalog.Snapshot");
    }

    /// <summary>Looks up one kind in the scope it was declared in. False when that exact pair is not registered.</summary>
    public static bool TryGet(string scope, string kind, out UiWidgetDescriptor descriptor)
    {
        throw new NotImplementedException("T3: UiWidgetCatalog.TryGet");
    }

    /// <summary>Every scope that has at least one registration, ordinal order. The returned list is a copy.</summary>
    public static IReadOnlyList<string> Scopes()
    {
        throw new NotImplementedException("T3: UiWidgetCatalog.Scopes");
    }
}
