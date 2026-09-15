using System;

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
    public UiTreeRow(string key, int depth, string text, bool expandable = false, bool expanded = false, string textKey = "")
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Depth = depth;
        Text = text ?? "";
        TextKey = textKey ?? "";
        Expandable = expandable;
        Expanded = expanded;
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
}
