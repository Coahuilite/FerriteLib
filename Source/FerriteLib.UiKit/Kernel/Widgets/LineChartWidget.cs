using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;
/// <summary>
/// Normalized line data and optional editable points. Capture and 2D sampling use the common pointer
/// owner; optional explicit keys permit stable dynamic reordering while legacy index usage remains.
/// </summary>
internal sealed class LineChartWidget : IUiWidget, IUiMeasureParticipant, IUiDescriptiveWidget, IUiPointerLayout, IUiRetainedDisplay
{
    public const string Kind = "chart/line";
    private const float DefaultHeight = 120f;
    private const float PlotPadding = 8f;
    private const float PointSize = 5f;
    private const float HoveredPointSize = 9f;
    private const float HitRadius = 6f;
    private UiElementSpec spec = UiElementSpec.Empty;
    private readonly UiRetainedPaint retained = new();
    bool IUiRetainedDisplay.PaintLast(Rect rect, UiWidgetContext ctx) => retained.Paint(rect, ctx);
    UiMeasurement? IUiRetainedDisplay.MeasureLast(UiWidgetContext ctx) => retained.Measure(ctx);
    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, Kind, () => new LineChartWidget(), new[] { "Id", "Kind", "Bind", "Points", "PointKeysBind", "Height", "Editable", "EditablePoints", "ActionBind", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        string bindKey = ReadBindKey();
        bool hasStatic = spec.TryGetAttribute("Points", out string raw) && raw.Trim().Length > 0;
        bool explicitBind = spec.TryGetAttribute("Bind", out string declaredBind) && declaredBind.Length > 0;
        bool hasBind = bindKey.Length > 0 && (explicitBind || !hasStatic);
        if (hasBind)
        {
            bindings.ValidateValue<IReadOnlyList<Vector2>>(bindKey, elementPath);
        }

        if (spec.TryGetAttribute("PointKeysBind", out string keys) && keys.Length > 0)
            bindings.ValidateValue<IReadOnlyList<string>>(keys, elementPath);
        if (spec.TryGetAttribute("ActionBind", out string actionKey) && actionKey.Length > 0)
        {
            bindings.ValidateAction<UiChartPointChange>(actionKey, elementPath);
        }

        if (!hasBind && !hasStatic)
        {
            throw new InvalidOperationException($"LineChartWidget at '{elementPath}' requires either a 'Bind' value binding or a static 'Points' attribute.");
        }
    }

    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        return new UiMeasurement(new UiMeasureInputs(), () => Measure(ctx));
    }

    public float Measure(UiWidgetContext ctx)
    {
        return ReadHeight();
    }

    internal bool AllowsPointEditing => ReadBool("Editable", true)
        && spec.TryGetAttribute("ActionBind", out string actionKey) && actionKey.Length > 0;

    public UiDescription Describe(UiWidgetContext ctx) => new UiDescription(UiDescriptionRole.Chart, count: ResolvePoints(ctx).Count);
    public IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds)
    {
        if (!AllowsPointEditing) yield break;
        List<Vector2> points = ResolvePoints(ctx);
        if (points.Count < 2) yield break;
        HashSet<int>? editablePoints = null;
        if (spec.TryGetAttribute("EditablePoints", out string raw) && raw.Trim().Length > 0)
            editablePoints = ParseEditablePoints(raw);
        Rect plot = PlotRect(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        for (int i = 0; i < points.Count; i++)
        {
            if (editablePoints != null && !editablePoints.Contains(i)) continue;
            Rect hit = PointHit(points[i], plot);
            yield return new UiPointerRegion(new UiLayoutRect(hit.x, hit.y, hit.width, hit.height));
        }
    }
    private static Rect PlotRect(Rect rect) => new(rect.x + PlotPadding, rect.y + PlotPadding,
        Math.Max(1, rect.width - PlotPadding * 2), Math.Max(1, rect.height - PlotPadding * 2));
    private static Rect PointHit(Vector2 point, Rect plot)
    {
        Vector2 pixel = ToRectPoint(point, plot);
        return new Rect(pixel.x - HitRadius, pixel.y - HitRadius, HitRadius * 2, HitRadius * 2);
    }
    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f)
            return;
        List<Vector2> points = ResolvePoints(ctx);
        if (points.Count < 2)
        {
            UiNative.EndPointCapture(ctx, UiInteractionEnd.Removed);
            return;
        }

        bool editable = AllowsPointEditing;
        Rect plotRect = PlotRect(rect);
        HashSet<int>? editablePoints = null;
        if (editable && spec.TryGetAttribute("EditablePoints", out string editableRaw) && editableRaw.Trim().Length > 0)
        {
            editablePoints = ParseEditablePoints(editableRaw);
        }

        int? hoveredPoint = null;
        if (editable)
        {
            // The hover answer is the context-carrying one: a point under someone else's open menu is not
            // hovered by anyone's reading, and the highlight must not promise a drag the press gate refuses.
            // Disabled is deliberately not part of the hover question (help still explains an unavailable
            // control); it IS part of the press question, in CanReceivePointerPress below.
            if (UiNative.IsMouseOver(rect, ctx))
            {
                hoveredPoint = FindHoveredPoint(plotRect, points, UiNative.PointerPosition(), editablePoints);
            }

            HandleDrag(plotRect, points, editablePoints, ctx);
        }
        else
            UiNative.EndPointCapture(ctx, UiInteractionEnd.Ineligible);
        if (!ctx.Session.IsActive)
            return;
        points = ResolvePoints(ctx);
        UiThemeDraw.Surface(plotRect, ctx.Theme, ctx.Theme[UiPaletteRole.PanelSurface], ctx.Theme[UiPaletteRole.Border]);
        DrawGrid(plotRect, ctx.Theme);
        for (int i = 1; i < points.Count; i++)
            DrawLine(ToRectPoint(points[i - 1], plotRect), ToRectPoint(points[i], plotRect), ctx.Theme[UiPaletteRole.Accent]);
        DrawPoints(plotRect, points, hoveredPoint, ctx.Theme);
        retained.Capture(ctx,(r,c)=>
        {
            Rect lastPlot=PlotRect(r);
            UiThemeDraw.Surface(lastPlot,c.Theme,c.Theme[UiPaletteRole.PanelSurface],c.Theme[UiPaletteRole.Border]);
            DrawGrid(lastPlot,c.Theme);
            for(int i=1;i<points.Count;i++) DrawLine(ToRectPoint(points[i-1],lastPlot),ToRectPoint(points[i],lastPlot),c.Theme[UiPaletteRole.Accent]);
            DrawPoints(lastPlot,points,null,c.Theme);
        });
    }

    private void HandleDrag(Rect plotRect, List<Vector2> points, HashSet<int>? editablePoints, UiWidgetContext ctx)
    {
        UiNode node = ctx.Node ?? ctx.Session.ActiveNode;
        UiValueState state = ctx.Session.GetOrCreateValueState((ctx.Node ?? ctx.Session.ActiveNode).Id, "chart-identity");
        UiChartIdentity identity = state.ChartIdentity ??= new UiChartIdentity();
        string keysBinding = spec.TryGetAttribute("PointKeysBind", out string keysKey) ? keysKey : "";
        IReadOnlyList<string>? keys;
        try
        {
            keys = ResolveKeys(points.Count, keysBinding, ctx);
        }
        catch
        {
            UiNative.EndPointCapture(ctx, UiInteractionEnd.Aborted);
            throw;
        }

        UiInteractionTarget? owner = ctx.Session.Interactions.CaptureOwner;
        object? heldKey = owner?.Node == node.Id.Key && owner?.Part == "chart-point" ? owner.Value.ItemKey : null;
        var coordinates = new UiChartCoordinate[points.Count];
        for (int i = 0; i < points.Count; i++)
            coordinates[i] = new UiChartCoordinate(points[i].x, points[i].y);
        if (!identity.Observe(coordinates, keys, keysBinding, heldKey))
            UiNative.EndPointCapture(ctx, UiInteractionEnd.TargetChanged);
        spec.TryGetAttribute("ActionBind", out string actionKey);
        string targetBinding = ReadBindKey() + "\u001f" + actionKey + "\u001f" + keysBinding;
        for (int i = 0; i < points.Count; i++)
        {
            object key = keys == null ? i : keys[i];
            bool allowed = editablePoints == null || editablePoints.Contains(i);
            Rect hit = PointHit(points[i], plotRect);
            UiInteractionResult operation = UiNative.Point(hit, plotRect, ctx, targetBinding, key, points[i].x, points[i].y, allowed && ctx.Bindings.CanExecute(actionKey));
            if (!operation.Request)
                continue;
            identity.NoteRequest(key, operation.Value, operation.Y);
            int index = i;
            var change = new UiChartPointChange(index, operation.Value, operation.Y, keys == null ? null : keys[i]);
            UiInteractionRequests.Activation(UiNative.InteractionTarget(ctx, targetBinding, "chart-point", key), operation, () => ctx.Bindings.Invoke(actionKey, change));
            return;
        }
    }

    private static IReadOnlyList<string>? ResolveKeys(int count, string binding, UiWidgetContext ctx)
    {
        if (binding.Length == 0)
            return null;
        IReadOnlyList<string> keys = ctx.Bindings.Get<IReadOnlyList<string>>(binding);
        if (keys == null || keys.Count != count)
            throw new InvalidOperationException("Chart '" + ctx.ElementPath + "' requires PointKeysBind length to match its points.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < keys.Count; i++)
            if (string.IsNullOrEmpty(keys[i]) || !seen.Add(keys[i]))
                throw new InvalidOperationException("Chart '" + ctx.ElementPath + "' requires unique nonempty point keys; index=" + i + ".");
        return keys;
    }

    private string ReadBindKey()
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            return bindKey;
        }

        return spec.Id;
    }

    private List<Vector2> ResolvePoints(UiWidgetContext ctx)
    {
        string bindKey = ReadBindKey();
        if (bindKey.Length > 0 && ctx.Bindings.TryGet(bindKey, out IReadOnlyList<Vector2> bound))
        {
            var result = new List<Vector2>(bound.Count);
            foreach (Vector2 point in bound)
            {
                result.Add(new Vector2(Clamp01(point.x), Clamp01(point.y)));
            }

            return result;
        }

        return ParsePoints(spec.TryGetAttribute("Points", out string raw) ? raw : "");
    }

    internal static List<Vector2> ParsePoints(string raw)
    {
        var result = new List<Vector2>();
        if (raw == null)
            return result;
        string[] pointParts = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string pointPart in pointParts)
        {
            string[] xy = pointPart.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (xy.Length != 2)
                throw new FormatException($"Invalid chart point '{pointPart.Trim()}'; expected \"x,y\".");
            if (!float.TryParse(xy[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) || !float.TryParse(xy[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                throw new FormatException($"Invalid chart point '{pointPart.Trim()}'; expected numeric x,y.");
            }

            result.Add(new Vector2(Clamp01(x), Clamp01(y)));
        }

        return result;
    }

    private static HashSet<int> ParseEditablePoints(string raw)
    {
        var result = new HashSet<int>();
        string[] parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            if (int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
            {
                result.Add(index);
            }
        }

        return result;
    }

    private static void DrawGrid(Rect plotRect, UiTheme theme)
    {
        for (int i = 1; i <= 3; i++)
        {
            float fx = plotRect.x + plotRect.width * i / 4f;
            UiThemeDraw.Solid(new Rect(fx, plotRect.y, 1f, plotRect.height), theme[UiPaletteRole.Divider]);
            float fy = plotRect.y + plotRect.height * i / 4f;
            UiThemeDraw.Solid(new Rect(plotRect.x, fy, plotRect.width, 1f), theme[UiPaletteRole.Divider]);
        }
    }

    private static void DrawPoints(Rect plotRect, IReadOnlyList<Vector2> points, int? hoveredPoint, UiTheme theme)
    {
        int hovered = hoveredPoint ?? -1;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 pixel = ToRectPoint(points[i], plotRect);
            bool isHovered = i == hovered;
            float size = isHovered ? HoveredPointSize : PointSize;
            var pointRect = new Rect(pixel.x - size * 0.5f, pixel.y - size * 0.5f, size, size);
            UiThemeDraw.Solid(pointRect, isHovered ? theme.AccentHover : theme[UiPaletteRole.Accent]);
        }
    }

    internal static int? FindHoveredPoint(Rect plotRect, IReadOnlyList<Vector2> points, Vector2 pointer, HashSet<int>? editablePoints)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if (editablePoints != null && !editablePoints.Contains(i))
                continue;
            if (Distance(pointer, ToRectPoint(points[i], plotRect)) <= HitRadius)
            {
                return i;
            }
        }

        return null;
    }

    private static Vector2 ToRectPoint(Vector2 normalized, Rect plotRect)
    {
        return new Vector2(plotRect.x + Clamp01(normalized.x) * plotRect.width, plotRect.yMax - Clamp01(normalized.y) * plotRect.height);
    }

    private static float Distance(Vector2 a, Vector2 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return (float)Math.Sqrt((double)(dx * dx + dy * dy));
    }

    private static void DrawLine(Vector2 a, Vector2 b, UnityEngine.Color color)
    {
        float dx = b.x - a.x;
        float dy = b.y - a.y;
        float distance = (float)Math.Sqrt((double)(dx * dx + dy * dy));
        int steps = Math.Max(1, Mathf.CeilToInt(distance / 2f));
        for (int step = 0; step < steps; step++)
        {
            float t = steps == 1 ? 1f : step / (float)(steps - 1);
            float px = a.x + dx * t;
            float py = a.y + dy * t;
            UiThemeDraw.Solid(new Rect(px, py, 1f, 1f), color);
        }
    }

    private float ReadHeight()
    {
        if (spec.TryGetAttribute("Height", out string raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float height) && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
    }

    private bool ReadBool(string name, bool fallback)
    {
        if (!spec.TryGetAttribute(name, out string raw))
            return fallback;
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(raw, "1", StringComparison.Ordinal);
    }

    private static float Clamp01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        if (value < 0f)
            return 0f;
        if (value > 1f)
            return 1f;
        return value;
    }
}
