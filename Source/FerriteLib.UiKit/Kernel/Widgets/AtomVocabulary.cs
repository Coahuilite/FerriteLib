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

    // The names the engine already reads on every widget regardless of kind (UiHost's common widget
    // vocabulary) plus the two identity names. Listed explicitly so an atom's schema reads as the
    // complete contract instead of "whatever the engine adds on the side" — the shape the six
    // pre-existing core kinds use as well.
    private static readonly string[] EngineWideAttributes = { "Id", "Kind", "Tab", "Hidden" };

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
    /// </summary>
    private static UiStatusTone ParseTone(UiElementSpec spec, UiWidgetContext ctx)
    {
        string authored = Read(spec, ToneAttribute).Trim();
        if (authored.Length == 0) return UiStatusTone.Neutral;

        if (string.Equals(authored, "Neutral", StringComparison.OrdinalIgnoreCase)) return UiStatusTone.Neutral;
        if (string.Equals(authored, "Active", StringComparison.OrdinalIgnoreCase)) return UiStatusTone.Active;
        if (string.Equals(authored, "Success", StringComparison.OrdinalIgnoreCase)) return UiStatusTone.Success;
        if (string.Equals(authored, "Warning", StringComparison.OrdinalIgnoreCase)) return UiStatusTone.Warning;
        if (string.Equals(authored, "Danger", StringComparison.OrdinalIgnoreCase)) return UiStatusTone.Danger;
        if (string.Equals(authored, "Disabled", StringComparison.OrdinalIgnoreCase)) return UiStatusTone.Disabled;

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

    private static void ReportFallback(UiElementSpec spec, UiWidgetContext ctx, string attribute, string authored, string resolved)
    {
        UiFitAudit.ReportStyleFallback(ctx.ElementPath, spec.Kind, attribute, authored, resolved);
    }
}
