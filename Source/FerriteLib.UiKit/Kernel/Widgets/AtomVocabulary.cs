using System;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Which role attributes a kind accepts. <c>Emphasis</c> is a text-colour axis, so a kind that paints no
/// text does not accept it: an attribute that cannot move a pixel is the silent no-op this library
/// refuses at creation rather than shipping it.
/// </summary>
internal enum AtomRoles
{
    /// <summary><c>Tone</c> alone — a kind whose answer is one colour, like a painted chrome line.</summary>
    Tone,

    /// <summary><c>Tone</c> plus <c>Emphasis</c> — a kind that paints text, a surface, or both.</summary>
    ToneAndEmphasis
}

/// <summary>
/// Shared vocabulary of the core leaf atoms. One type owns the three things the atoms must agree on: the
/// creation-time attribute schema each kind registers, the way their XML values are read, and the role
/// attributes (<c>Tone</c>/<c>Emphasis</c>) that bind the manifest to the theme's resolved-value table.
/// <para>
/// The role names live here, once, and every atom declares its schema through <see cref="Schema"/> — a
/// kind that kept its own literal array would refuse the role attributes at creation while its
/// neighbours accepted them. The table's answer is resolved through <see cref="ResolveRole"/>, which is
/// also where an unknown value falls back and is recorded.
/// </para>
/// </summary>
internal static class AtomVocabulary
{
    internal const string ToneAttribute = "Tone";

    internal const string EmphasisAttribute = "Emphasis";

    // What a retired authored tone name redirects to, as the appearance record spells it. There is no
    // second channel for a deprecation: the note rides the existing appearance record, whose dedup key
    // (element path, kind, attribute, authored text) already means "once per declaration, not once per
    // frame". Both names are refused at the next minor boundary.
    private const string ActiveRedirect = "the Active state (deprecated alias; the selected treatment)";

    private const string DisabledRedirect = "the Disabled state (deprecated alias; derived from the bindings)";

    // The names the engine already reads on every widget regardless of kind (UiHost's common widget
    // vocabulary) plus the two identity names. Listed explicitly so an atom's schema reads as the
    // complete contract instead of "whatever the engine adds on the side" — the shape the six
    // pre-existing core kinds use as well.
    // Visible/VisibleKey are engine-wide like Hidden: the engine reads them for every kind, so an
    // atom that refused them would reject a page its container accepted.
    private static readonly string[] EngineWideAttributes = { "Id", "Kind", "Tab", "Hidden", "Visible", "VisibleKey" };

    /// <summary>
    /// The allowed-attribute array a core atom registers: the engine-wide names, the role names the kind
    /// accepts, then the names the kind itself reads. Anything not listed is refused at Host creation.
    /// </summary>
    internal static string[] Schema(AtomRoles acceptedRoles, params string[] kindAttributes)
    {
        int roleCount = acceptedRoles == AtomRoles.ToneAndEmphasis ? 2 : 1;
        var allowed = new string[EngineWideAttributes.Length + roleCount + kindAttributes.Length];
        EngineWideAttributes.CopyTo(allowed, 0);
        allowed[EngineWideAttributes.Length] = ToneAttribute;
        if (roleCount == 2) allowed[EngineWideAttributes.Length + 1] = EmphasisAttribute;
        kindAttributes.CopyTo(allowed, EngineWideAttributes.Length + roleCount);
        return allowed;
    }

    /// <summary>Raw attribute value, or "" when absent — never null, so callers do not branch twice.</summary>
    internal static string Read(UiElementSpec spec, string name)
    {
        return spec.TryGetAttribute(name, out string value) ? value : "";
    }

    /// <summary>Declared number, or <paramref name="fallback"/> when absent or malformed.</summary>
    internal static float ReadFloat(UiElementSpec spec, string name, float fallback)
    {
        return spec.TryGetAttribute(name, out string raw)
            && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    /// <summary>Declared boolean ("true"/"1"), or <paramref name="fallback"/> when absent.</summary>
    internal static bool ReadBool(UiElementSpec spec, string name, bool fallback)
    {
        if (!spec.TryGetAttribute(name, out string raw)) return fallback;
        string value = raw.Trim();
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }

    /// <summary>The kind's own band height, or <paramref name="fallback"/> when none is declared.</summary>
    internal static float ReadHeight(UiElementSpec spec, float fallback)
    {
        float height = ReadFloat(spec, "Height", 0f);
        return height > 0f ? height : fallback;
    }

    /// <summary>
    /// The typed binding key every value atom uses: the <c>Bind</c> attribute when present, else the
    /// element Id. Both may be empty, and the atom's Validate is what refuses that at creation.
    /// </summary>
    internal static string ReadBindKey(UiElementSpec spec)
    {
        string bind = Read(spec, "Bind");
        return bind.Length > 0 ? bind : spec.Id;
    }

    /// <summary>
    /// The kind's own display string: the key attribute resolved through the translation seam when
    /// filled, else the literal attribute. The engine's <c>Width="Auto"</c> seam resolves the very
    /// same attribute pair, which is why a kind's declared label set names these two attributes.
    /// </summary>
    internal static string ResolveText(UiElementSpec spec, UiWidgetContext ctx, string literalAttribute, string keyAttribute)
    {
        if (spec.TryGetAttribute(keyAttribute, out string key) && key.Length > 0)
        {
            return ctx.Translation.Translate(key);
        }

        return Read(spec, literalAttribute);
    }

    /// <summary>
    /// The one font a text atom measures and writes with. It is the theme's resolved font rather than a
    /// literal of the kind's own, which is this vocabulary's density landing point: moving
    /// <see cref="UiTheme.DefaultFont"/> moves the atom's text, the band it reserves and the font the
    /// fitting audit measures with, together — there is no second size to keep in sync, and a kind that
    /// pinned its own font would simply stop following density.
    /// </summary>
    internal static UiFont TextFont(UiWidgetContext ctx)
    {
        return ctx.Theme.Styles.Font;
    }

    /// <summary>
    /// A fail-soft typed read for the kinds whose key may be item-local: an absent key and a key bound to
    /// another type both answer <paramref name="fallback"/> and are recorded once through the appearance
    /// channel, instead of reaching the tree's recovery band. That is the contract the collection kinds
    /// publish - a data-driven row composes its key from the consumer's item key at instantiation, so
    /// "the value is not there yet" and "the model hands back another type" are ordinary states of a page
    /// that is still drawing, not a reason to replace the slot.
    /// <para>
    /// The bool read goes through <see cref="IUiBindings.TryGetBool"/>, the query P2 added for exactly this
    /// shape, so it needs no exception at all. The generic one has to catch, because
    /// <see cref="IUiBindings.TryGet{T}"/> reports a type mismatch as an <see cref="InvalidOperationException"/>
    /// and the surface has no non-throwing typed read for anything else. The catch is narrowed to that one
    /// exception type and the fallback is recorded, so a consumer getter that throws anything else still
    /// reaches the tree's recovery path, and one that throws this type is answered - not silently.
    /// </para>
    /// <para>
    /// Scope, stated because it is a boundary and not an accident: this is the collection kinds' value
    /// contract. The pre-existing atoms keep their own read paths (a bound string atom draws an empty label
    /// for an unresolvable key; a read that throws reaches the recovery band once per slot), and
    /// <c>KernelRepeatTests</c> pins that split from both sides.
    /// </para>
    /// </summary>
    internal static bool ReadBoolOr(UiWidgetContext ctx, string kind, string key, bool fallback)
    {
        if (ctx.Bindings.TryGetBool(key, out bool value)) return value;
        ReportUnresolved(ctx, kind, key, fallback ? "true" : "false");
        return fallback;
    }

    /// <summary>The generic half of <see cref="ReadBoolOr(UiWidgetContext, string, string, bool)"/>.</summary>
    internal static T ReadOr<T>(UiWidgetContext ctx, string kind, string key, T fallback, string recordedDefault)
    {
        try
        {
            if (ctx.Bindings.TryGet<T>(key, out T value)) return value;
        }
        catch (InvalidOperationException)
        {
            // TryGet<T> reports a type mismatch this way; narrowed to that type so a consumer getter that
            // throws anything else still reaches the tree's recovery path (see the summary above).
        }

        ReportUnresolved(ctx, kind, key, recordedDefault);
        return fallback;
    }

    /// <summary>
    /// Records one unresolved-binding answer on the bounded appearance channel, deduplicated by element, kind,
    /// attribute and key. It is the single place this vocabulary spells that report, so a kind that answers an
    /// unresolvable key with a default - a collection control or the wrapped-text atom - records the same
    /// finding in the same shape, and "fail-soft must not mean silent" is one call rather than a convention.
    /// </summary>
    internal static void ReportUnresolved(UiWidgetContext ctx, string kind, string key, string resolved)
    {
        UiFitAudit.ReportStyleFallback(ctx.ElementPath, kind, "Bind", key, resolved);
    }

    /// <summary>
    /// The data side's writability for a value key, or null when the element carries no value binding.
    /// Null is "not known" and resolves by role alone; false is state, and state beats the author.
    /// </summary>
    internal static bool? WritableOf(UiWidgetContext ctx, string bindKey)
    {
        return string.IsNullOrEmpty(bindKey) ? (bool?)null : ctx.Bindings.IsWritable(bindKey);
    }

    /// <summary>
    /// The element's resolved appearance answer: the authored <c>Tone</c>/<c>Emphasis</c> through the
    /// theme's one table, with the data side's writability as the state that beats the author.
    /// <para>
    /// A value outside the vocabulary is an appearance-class failure, so it falls back to the default row
    /// and is recorded on the fitting audit's channel with the element path, the kind, the attribute and
    /// the authored text — fail-soft must not mean silent. The attribute <i>name</i> is a different
    /// matter and stays fail-closed: it is part of the kind's schema, so a typo in the name is refused at
    /// Host creation while a typo in the value still renders.
    /// </para>
    /// </summary>
    internal static UiResolvedStyle ResolveRole(UiElementSpec spec, UiWidgetContext ctx, bool? writable)
    {
        return ctx.Theme.Styles.Resolve(ParseTone(spec, ctx), ParseEmphasis(spec, ctx), writable);
    }

    /// <summary>
    /// The authored tone, or <see cref="UiStatusTone.Neutral"/> when none is declared. Names are matched
    /// case-insensitively, like every other attribute in this vocabulary, and only the declared names
    /// count: a numeric value is not a tone, which an <c>Enum.TryParse</c> would have silently accepted.
    /// <para>
    /// The authored vocabulary is the four <b>meanings</b> — <see cref="UiStatusTone.Neutral"/>,
    /// <see cref="UiStatusTone.Success"/>, <see cref="UiStatusTone.Warning"/> and
    /// <see cref="UiStatusTone.Danger"/>. <see cref="UiStatusTone.Active"/> and
    /// <see cref="UiStatusTone.Disabled"/> are <b>states</b> (interaction, and the bindings' read side),
    /// not values an author writes: for one minor each still resolves to the state it always meant and
    /// records one deduplicated deprecation note on the appearance channel, so a page that writes it keeps
    /// painting and is told where to move. The names are refused at the next minor boundary; the two enum
    /// members stay regardless, because <see cref="UiStatusTone"/> is a stable type.
    /// </para>
    /// </summary>
    private static UiStatusTone ParseTone(UiElementSpec spec, UiWidgetContext ctx)
    {
        string authored = Read(spec, ToneAttribute).Trim();
        if (authored.Length == 0) return UiStatusTone.Neutral;

        if (string.Equals(authored, "Neutral", StringComparison.OrdinalIgnoreCase)) return UiStatusTone.Neutral;
        if (string.Equals(authored, "Success", StringComparison.OrdinalIgnoreCase)) return UiStatusTone.Success;
        if (string.Equals(authored, "Warning", StringComparison.OrdinalIgnoreCase)) return UiStatusTone.Warning;
        if (string.Equals(authored, "Danger", StringComparison.OrdinalIgnoreCase)) return UiStatusTone.Danger;

        // Retired authored names, still redirecting for one minor. Deliberately after the four declared
        // meanings and before the unknown-value fallback: a typo resolves to the default row and a retired
        // name resolves to the state it names, and neither can be mistaken for the other. The value is the
        // one the table already derived (Active = the selected treatment, Disabled = the disabled state),
        // so the redirect is the existing answer, not a second mapping.
        if (string.Equals(authored, "Active", StringComparison.OrdinalIgnoreCase))
        {
            ReportFallback(spec, ctx, ToneAttribute, authored, ActiveRedirect);
            return UiStatusTone.Active;
        }

        if (string.Equals(authored, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            ReportFallback(spec, ctx, ToneAttribute, authored, DisabledRedirect);
            return UiStatusTone.Disabled;
        }

        ReportFallback(spec, ctx, ToneAttribute, authored, "Neutral");
        return UiStatusTone.Neutral;
    }

    /// <summary>The authored emphasis, or <see cref="UiEmphasis.Normal"/> when none is declared.</summary>
    private static UiEmphasis ParseEmphasis(UiElementSpec spec, UiWidgetContext ctx)
    {
        string authored = Read(spec, EmphasisAttribute).Trim();
        if (authored.Length == 0) return UiEmphasis.Normal;

        if (string.Equals(authored, "Normal", StringComparison.OrdinalIgnoreCase)) return UiEmphasis.Normal;
        if (string.Equals(authored, "Muted", StringComparison.OrdinalIgnoreCase)) return UiEmphasis.Muted;

        ReportFallback(spec, ctx, EmphasisAttribute, authored, "Normal");
        return UiEmphasis.Normal;
    }

    /// <summary>
    /// Writes one appearance-class note for an authored role value the vocabulary does not declare: the
    /// fallback an unknown value gets, and the deprecation a retired name gets. One write path, so the two
    /// cannot drift apart.
    /// </summary>
    private static void ReportFallback(UiElementSpec spec, UiWidgetContext ctx, string attribute, string authored, string resolved)
    {
        UiFitAudit.ReportStyleFallback(ctx.ElementPath, spec.Kind, attribute, authored, resolved);
    }
}
