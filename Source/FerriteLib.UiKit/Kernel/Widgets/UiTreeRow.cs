using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// One row of a <c>container/tree</c> row set: the consumer's already-flattened hierarchy, one level stated
/// per row.
/// <para>
/// <b>Why a data type and not a template.</b> A tree presents hierarchy; it does not own ordering and it
/// does not walk a parent graph. The consumer flattens its own structure - that is where the ordering
/// semantics live - and hands the result over in display order, each row carrying the level it should be
/// indented to. The library then owns what a manifest cannot express: the per-row band geometry, the
/// per-row hit rule, and the indent the level resolves to.
/// </para>
/// <para>
/// <see cref="Key"/> is a stable business key, and it is the only identity the tree uses: expansion and
/// selection state the consumer keys by it cannot desync across a reorder or an insert, and a row that
/// disappears takes its state with it in the model. <see cref="Expandable"/>/<see cref="Expanded"/> are
/// presentation inputs, not scheduling: the kind paints a marker for them and reports a hit; it never
/// evaluates a graph, never reorders rows and never decides what a level means.
/// </para>
/// <para>
/// Reference-type item data is not required: rows are values, so a consumer can project a model list into
/// this type without allocating per row. <see cref="TextKey"/> is resolved through the host's translation
/// seam when it is filled, exactly like a <c>LabelKey</c> attribute; <see cref="Text"/> is the literal
/// fallback.
/// </para>
/// </summary>
public readonly struct UiTreeRow
{
    public UiTreeRow(
        string key,
        int depth,
        string text,
        bool expandable = false,
        bool expanded = false,
        string textKey = "",
        bool selected = false,
        bool checkable = false,
        bool isChecked = false,
        Texture2D? image = null,
        Vector2 imageSize = default)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Depth = depth;
        Text = text ?? "";
        TextKey = textKey ?? "";
        Expandable = expandable;
        Expanded = expanded;
        Selected = selected;
        Checkable = checkable;
        Checked = isChecked;
        Image = image;
        ImageSize = imageSize;
    }

    /// <summary>
    /// The stable business key this row is identified by. Never derived, never renumbered by the library:
    /// a row keeps its identity when the list is reordered, and a blank key is refused rather than
    /// replaced by a positional one.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The nesting level to indent to, zero-based. The consumer states it; the tree multiplies it by one
    /// density-derived indent and draws the band, which is the whole of what "parent/child levels" means
    /// here.
    /// </summary>
    public int Depth { get; }

    /// <summary>The literal label, used when <see cref="TextKey"/> is empty.</summary>
    public string Text { get; }

    /// <summary>A translation key for the label; resolved through the host's seam when filled.</summary>
    public string TextKey { get; }

    /// <summary>True when this row should paint a level marker at all (a leaf row paints none).</summary>
    public bool Expandable { get; }

    /// <summary>The model's current expanded answer, painted as the marker's state.</summary>
    public bool Expanded { get; }

    /// <summary>
    /// The model's selection answer for this row. Painted in the resolved Active tone - the same tone a chosen
    /// mode-row cell and a chosen dropdown option use - so "selected" reads the same everywhere. State, like
    /// <see cref="Expanded"/>: the consumer keys it by <see cref="Key"/> and the kind only paints it.
    /// </summary>
    public bool Selected { get; }

    /// <summary>True when this row paints a checkbox at all; a row without one keeps its whole band for the label.</summary>
    public bool Checkable { get; }

    /// <summary>The model's current checkbox answer for this row, painted as the box's state.</summary>
    public bool Checked { get; }

    /// <summary>
    /// The consumer's own texture for this row, or null for a row with no image. The library never loads one:
    /// a consumer supplies the value (a Verse-side adapter is the usual way to turn a def/thing into a
    /// <see cref="Texture2D"/>) and the tree only lays it out, fits it and paints it.
    /// </summary>
    public Texture2D? Image { get; }

    /// <summary>
    /// An explicit size for <see cref="Image"/>, in the same units the manifest uses. A non-positive
    /// component means "the texture's own natural size on this axis", which is the default - so a consumer
    /// states a size only when the icon must be a fixed slot rather than the picture's own proportions.
    /// </summary>
    public Vector2 ImageSize { get; }
}
