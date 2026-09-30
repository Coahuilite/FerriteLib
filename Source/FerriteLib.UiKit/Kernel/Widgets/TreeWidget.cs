using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Core kind <c>container/tree</c>: a data-driven hierarchy presentation over one
/// <c>IReadOnlyList&lt;UiTreeRow&gt;</c> value binding.
/// <para>
/// <b>Hierarchy presentation only, and that is the whole boundary.</b> The consumer flattens its own
/// structure in display order and states each row's level; this kind multiplies that level by one
/// density-derived indent, measures one band per row, paints one label per band and hit-tests one band
/// at a time. It does not order rows, does not walk a parent graph, does not schedule anything and does
/// not evaluate a task graph - a tree of the wrong shape is the consumer's data, and the only thing the
/// kind refuses is a row it cannot identify.
/// </para>
/// <para>
/// <b>Why this earns a kind</b> - a measure contract over its own content plus a hit/geometry rule. The
/// band height, the indent each level resolves to, the marker geometry and the per-row target are all
/// facts the manifest cannot state, and no container can express "one band per data row".
/// </para>
/// <para>
/// <b>Identity is the row's stable key, never its position.</b> Expansion is the model's answer, carried
/// on the row and keyed by <see cref="UiTreeRow.Key"/>; the kind re-reads it every pass, so a reorder
/// moves bands without moving state, and the hit payload is the clicked row's own key. A row with a blank
/// key, a negative level or a key a sibling already used is refused - it produces no band and one
/// deduplicated report - because rendering it would silently give two model rows one identity. That is
/// the same refusal the keyed repeater makes, with the same reason.
/// </para>
/// <para>
/// <b>The composed row (R4-B).</b> One band can carry, left to right: the indent, the disclosure marker, a
/// checkbox, an image and the label - each part opt-in per row, and every part absent by default, so a page
/// that declares none of them gets the band this kind always drew. The checkbox and the marker are their own
/// targets only when the page binds <c>CheckBind</c>/<c>DisclosureBind</c> (both <c>Action&lt;string&gt;</c>
/// with the row's key); the row body stays <c>ActionBind</c>, and a part that took the click is not offered
/// to the body as well. The row's selection is <see cref="UiTreeRow.Selected"/>, its expansion
/// <see cref="UiTreeRow.Expanded"/>: both are the model's answers, re-read every pass.
/// </para>
/// </summary>
internal sealed class TreeWidget : IUiWidget
{
    internal const string Kind = "container/tree";

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    internal static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new TreeWidget(),
            AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "Bind", "ActionBind", "DisclosureBind", "CheckBind", "RowHeight"));
        // Deliberately no label set: this kind's labels are row data, not attributes, so Width="Auto" has
        // nothing honest to measure and falls back to the unsized distribution.
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    /// <summary>
    /// The creation-time contract: the row-list binding, the optional action a hit reports through (a
    /// <c>string</c> payload - the row key, which is the only thing the consumer cannot already know),
    /// and a declared <c>RowHeight</c> that has to be a positive number if it is written at all.
    /// </summary>
    public void Validate(IUiBindings bindings, string elementPath)
    {
        string key = ReadBindKey();
        if (key.Length == 0)
        {
            throw new InvalidOperationException(
                "TreeWidget at '" + elementPath + "' requires a Bind naming the value binding that holds its "
                + "rows; a hierarchy with no row source draws nothing.");
        }

        bindings.ValidateValue<IReadOnlyList<UiTreeRow>>(key, elementPath);

        string actionKey = AtomVocabulary.Read(spec, "ActionBind").Trim();
        if (actionKey.Length > 0)
        {
            bindings.ValidateAction<string>(actionKey, elementPath);
        }

        // R4-B: the two optional part targets, each carrying the row's stable key like the body action does.
        string disclosureKey = AtomVocabulary.Read(spec, "DisclosureBind").Trim();
        if (disclosureKey.Length > 0)
        {
            bindings.ValidateAction<string>(disclosureKey, elementPath);
        }

        string checkKey = AtomVocabulary.Read(spec, "CheckBind").Trim();
        if (checkKey.Length > 0)
        {
            bindings.ValidateAction<string>(checkKey, elementPath);
        }

        if (spec.TryGetAttribute("RowHeight", out string raw) && raw.Trim().Length > 0
            && (!float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float declared)
                || declared <= 0f))
        {
            throw new InvalidOperationException(
                "TreeWidget at '" + elementPath + "' declares RowHeight '" + raw + "'; expected a positive number.");
        }
    }

    public float Measure(UiWidgetContext ctx)
    {
        // One band per accepted row, each measured from its own content: a row carrying an image reserves at
        // least the picture's natural height plus padding, and every other row keeps the declared or
        // density-derived height this kind has always used. The label is single-line, so no row's text can
        // move the measurement.
        List<UiTreeRow> rows = CollectRows(ctx, ReadBindKey());
        float rowHeight = ReadRowHeight(ctx);
        float total = 0f;
        for (int i = 0; i < rows.Count; i++)
        {
            total += UiRowBand.Measure(rows[i], ctx.Theme, rowHeight);
        }

        return total;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string key = ReadBindKey();
        List<UiTreeRow> rows = CollectRows(ctx, key);
        if (rows.Count == 0) return;

        UiTheme theme = ctx.Theme;
        float rowHeight = ReadRowHeight(ctx);
        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable: null);
        string actionKey = AtomVocabulary.Read(spec, "ActionBind").Trim();
        string disclosureKey = AtomVocabulary.Read(spec, "DisclosureBind").Trim();
        string checkKey = AtomVocabulary.Read(spec, "CheckBind").Trim();

        // One actions instance per pass, not per row: the callbacks are keyed by the row the PRIMITIVE hands
        // them, so the band's own routing stays the primitive's and the kind only decides what a part does.
        var actions = new UiRowBandActions(
            actionKey.Length > 0 ? key => ctx.Bindings.Invoke<string>(actionKey, key) : null,
            disclosureKey.Length > 0 ? key => ctx.Bindings.Invoke<string>(disclosureKey, key) : null,
            checkKey.Length > 0 ? key => ctx.Bindings.Invoke<string>(checkKey, key) : null);

        float y = rect.y;
        for (int i = 0; i < rows.Count; i++)
        {
            UiTreeRow row = rows[i];
            float bandHeight = UiRowBand.Measure(row, theme, rowHeight);
            var band = new Rect(rect.x, y, rect.width, bandHeight);
            y += bandHeight;
            if (band.y >= rect.yMax) break;

            // The element's own interaction state, read from the funnel: armed beats hover, exactly as the
            // leaf atoms order it.
            bool armed = UiNative.IsMouseDownOver(band);
            bool hovered = !armed && UiNative.IsMouseOver(band);
            if (armed)
            {
                UiThemeDraw.Solid(band, theme.SelectedSurface.Fill);
            }
            else if (hovered)
            {
                UiThemeDraw.Solid(band, theme.HoverSurface.Fill);
            }

            // The band itself is the shared public primitive (R4-B): this kind is its first driver, and a
            // consumer's own composite calls the identical entry point for its rows.
            UiRowBand.Draw(row, band, ctx, rowHeight, style, actions);

            if (band.yMax >= rect.yMax) break;
        }
    }

    private float ReadRowHeight(UiWidgetContext ctx)
    {
        return Math.Max(1f, AtomVocabulary.ReadFloat(spec, "RowHeight", ctx.Theme.Geometry.RowHeight));
    }

    /// <summary>
    /// The rows this pass may draw: the model's list, with every row it cannot identify removed. Measure
    /// and Draw both come through here, so the bands they compute are always the same set - a refusal
    /// cannot shift the geometry of the rows that survive. The list itself is read under the collection
    /// kinds' fail-soft contract (<see cref="AtomVocabulary.ReadOr{T}"/>): an absent or mistyped row source
    /// draws no rows and records one report, rather than tripping the slot.
    /// </summary>
    private List<UiTreeRow> CollectRows(UiWidgetContext ctx, string key)
    {
        var accepted = new List<UiTreeRow>();
        IReadOnlyList<UiTreeRow> rows = AtomVocabulary.ReadOr<IReadOnlyList<UiTreeRow>>(
            ctx, Kind, key, Array.Empty<UiTreeRow>(), "no rows");

        HashSet<string>? seen = null;
        for (int i = 0; i < rows.Count; i++)
        {
            UiTreeRow row = rows[i];
            if (string.IsNullOrEmpty(row.Key) || row.Key.Trim().Length == 0)
            {
                ReportRefusal(ctx, "Key", "(blank)");
                continue;
            }

            if (row.Depth < 0)
            {
                ReportRefusal(ctx, "Depth", row.Key);
                continue;
            }

            seen ??= new HashSet<string>(StringComparer.Ordinal);
            if (!seen.Add(row.Key))
            {
                ReportRefusal(ctx, "Key", row.Key);
                continue;
            }

            accepted.Add(row);
        }

        return accepted;
    }

    private void ReportRefusal(UiWidgetContext ctx, string attribute, string authored)
    {
        UiFitAudit.ReportStyleFallback(ctx.ElementPath, Kind, attribute, authored, "row refused");
    }

    private string ReadBindKey()
    {
        return AtomVocabulary.ReadBindKey(spec);
    }
}
