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
            AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "Bind", "ActionBind", "RowHeight"));
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
        // One band per accepted row, at the declared or density-derived height. The label is single-line,
        // so no row's text can move the measurement.
        return CollectRows(ctx, ReadBindKey()).Count * ReadRowHeight(ctx);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string key = ReadBindKey();
        List<UiTreeRow> rows = CollectRows(ctx, key);
        if (rows.Count == 0) return;

        UiTheme theme = ctx.Theme;
        float rowHeight = ReadRowHeight(ctx);
        float pad = theme.Geometry.Padding;
        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable: null);
        string actionKey = AtomVocabulary.Read(spec, "ActionBind").Trim();

        for (int i = 0; i < rows.Count; i++)
        {
            UiTreeRow row = rows[i];
            var band = new Rect(rect.x, rect.y + i * rowHeight, rect.width, rowHeight);
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

            float x = band.x + pad + Math.Max(0f, row.Depth * theme.Geometry.Spacing);
            if (row.Expandable)
            {
                float side = Math.Max(6f, rowHeight - pad * 2f);
                var marker = new Rect(x, band.y + (band.height - side) * 0.5f, side, side);
                if (row.Expanded)
                {
                    // Filled and outlined both come from the resolved answer, so a tone move carries the
                    // marker with the label instead of leaving a second colour literal here.
                    UiThemeDraw.Surface(marker, style.Surface, theme.Geometry.Hairline);
                    float inset = Math.Max(2f, side * 0.25f);
                    UiThemeDraw.Solid(
                        new Rect(marker.x + inset, marker.y + inset, Math.Max(1f, side - inset * 2f), Math.Max(1f, side - inset * 2f)),
                        style.Text);
                }
                else
                {
                    UiThemeDraw.Surface(marker, new UiSurfaceStyle(theme.Base, style.Text), theme.Geometry.Hairline);
                }

                x = marker.xMax + pad;
            }

            float labelWidth = band.xMax - x;
            if (labelWidth > 1f)
            {
                UiThemeDraw.Label(
                    new Rect(x, band.y, labelWidth, band.height),
                    ReadText(row, ctx),
                    theme,
                    style.Text,
                    AtomVocabulary.TextFont(ctx),
                    TextAnchor.MiddleLeft,
                    singleLine: true);
            }

            // One band, one target: the funnel owns whether the element may take it, and the payload is the
            // row's own key, read by the model - never an index the library would have to keep stable.
            if (actionKey.Length > 0 && UiNative.Button(band, ctx))
            {
                ctx.Bindings.Invoke<string>(actionKey, row.Key);
            }
        }
    }

    private static string ReadText(UiTreeRow row, UiWidgetContext ctx)
    {
        return row.TextKey.Length > 0 ? ctx.Translation.Translate(row.TextKey) : row.Text;
    }

    private float ReadRowHeight(UiWidgetContext ctx)
    {
        return Math.Max(1f, AtomVocabulary.ReadFloat(spec, "RowHeight", ctx.Theme.Geometry.RowHeight));
    }

    /// <summary>
    /// The rows this pass may draw: the model's list, with every row it cannot identify removed. Measure
    /// and Draw both come through here, so the bands they compute are always the same set - a refusal
    /// cannot shift the geometry of the rows that survive.
    /// </summary>
    private List<UiTreeRow> CollectRows(UiWidgetContext ctx, string key)
    {
        var accepted = new List<UiTreeRow>();
        if (!ctx.Bindings.TryGet<IReadOnlyList<UiTreeRow>>(key, out IReadOnlyList<UiTreeRow> rows) || rows == null)
        {
            UiFitAudit.ReportStyleFallback(ctx.ElementPath, Kind, "Bind", key, "no rows");
            return accepted;
        }

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
