using System;
using System.Collections.Generic;
using System.Globalization;

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
    bool IUiRetainedDisplay.PaintLast(UiLayoutRect rect, UiWidgetContext ctx) => retained.Paint(rect, ctx);
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

    public void Validate(IUiBindingReader bindings, string elementPath)
    {
        string bindKey = ReadBindKey();
        bool hasStatic = spec.TryGetAttribute("Points", out string raw) && raw.Trim().Length > 0;
        bool explicitBind = spec.TryGetAttribute("Bind", out string declaredBind) && declaredBind.Length > 0;
        bool hasBind = bindKey.Length > 0 && (explicitBind || !hasStatic);
        if (hasBind)
        {
            bindings.ValidateValue<IReadOnlyList<UiLayoutPoint>>(bindKey, elementPath);
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
        List<UiLayoutPoint> points = ResolvePoints(ctx);
        if (points.Count < 2) yield break;
        HashSet<int>? editablePoints = null;
        if (spec.TryGetAttribute("EditablePoints", out string raw) && raw.Trim().Length > 0)
            editablePoints = ParseEditablePoints(raw);
        UiLayoutRect plot = PlotRect(new UiLayoutRect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        for (int i = 0; i < points.Count; i++)
        {
            if (editablePoints != null && !editablePoints.Contains(i)) continue;
            UiLayoutRect hit = PointHit(points[i], plot);
            yield return new UiPointerRegion(new UiLayoutRect(hit.X, hit.Y, hit.Width, hit.Height));
        }
    }
    private static UiLayoutRect PlotRect(UiLayoutRect rect) => new(rect.X + PlotPadding, rect.Y + PlotPadding,
        Math.Max(1, rect.Width - PlotPadding * 2), Math.Max(1, rect.Height - PlotPadding * 2));
    private static UiLayoutRect PointHit(UiLayoutPoint point, UiLayoutRect plot)
    {
        UiLayoutPoint pixel = ToRectPoint(point, plot);
        return new UiLayoutRect(pixel.X - HitRadius, pixel.Y - HitRadius, HitRadius * 2, HitRadius * 2);
    }
    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f)
            return;
        List<UiLayoutPoint> points = ResolvePoints(ctx);
        if (points.Count < 2)
        {
            ctx.Controls.EndPointCapture(ctx, UiInteractionEnd.Removed);
            return;
        }

        bool editable = AllowsPointEditing;
        UiLayoutRect plotRect = PlotRect(rect);
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
            if (ctx.Controls.IsMouseOver(rect, ctx))
            {
                hoveredPoint = FindHoveredPoint(plotRect, points, ctx.Controls.PointerPosition(), editablePoints);
            }

            HandleDrag(plotRect, points, editablePoints, ctx);
        }
        else
            ctx.Controls.EndPointCapture(ctx, UiInteractionEnd.Ineligible);
        if (!ctx.Session.IsActive)
            return;
        points = ResolvePoints(ctx);
        UiThemeDraw.Surface(ctx.Drawing,plotRect, ctx.Theme, ctx.Theme[UiPaletteRole.PanelSurface], ctx.Theme[UiPaletteRole.Border]);
        DrawGrid(ctx.Drawing, plotRect, ctx.Theme);
        for (int i = 1; i < points.Count; i++)
            DrawLine(ctx.Drawing, ToRectPoint(points[i - 1], plotRect), ToRectPoint(points[i], plotRect), ctx.Theme[UiPaletteRole.Accent]);
        DrawPoints(ctx.Drawing, plotRect, points, hoveredPoint, ctx.Theme);
        retained.Capture(ctx,(r,c)=>
        {
            UiLayoutRect lastPlot=PlotRect(r);
            UiThemeDraw.Surface(c.Drawing,lastPlot,c.Theme,c.Theme[UiPaletteRole.PanelSurface],c.Theme[UiPaletteRole.Border]);
            DrawGrid(ctx.Drawing, lastPlot,c.Theme);
            for(int i=1;i<points.Count;i++) DrawLine(ctx.Drawing, ToRectPoint(points[i-1],lastPlot),ToRectPoint(points[i],lastPlot),c.Theme[UiPaletteRole.Accent]);
            DrawPoints(ctx.Drawing, lastPlot,points,null,c.Theme);
        });
    }

    private void HandleDrag(UiLayoutRect plotRect, List<UiLayoutPoint> points, HashSet<int>? editablePoints, UiWidgetContext ctx)
    {
        UiNode node = ctx.Node;
        UiValueState state = ctx.Session.GetOrCreateValueState(ctx.Node.Id, "chart-identity");
        UiChartIdentity identity = state.ChartIdentity ??= new UiChartIdentity();
        string keysBinding = spec.TryGetAttribute("PointKeysBind", out string keysKey) ? keysKey : "";
        IReadOnlyList<string>? keys;
        try
        {
            keys = ResolveKeys(points.Count, keysBinding, ctx);
        }
        catch
        {
            ctx.Controls.EndPointCapture(ctx, UiInteractionEnd.Aborted);
            throw;
        }

        UiInteractionTarget? owner = ctx.Session.Interactions.CaptureOwner;
        object? heldKey = owner?.Node == node.Id.Key && owner?.Part == "chart-point" ? owner.Value.ItemKey : null;
        var coordinates = new UiChartCoordinate[points.Count];
        for (int i = 0; i < points.Count; i++)
            coordinates[i] = new UiChartCoordinate(points[i].X, points[i].Y);
        if (!identity.Observe(coordinates, keys, keysBinding, heldKey))
            ctx.Controls.EndPointCapture(ctx, UiInteractionEnd.TargetChanged);
        spec.TryGetAttribute("ActionBind", out string actionKey);
        string targetBinding = ReadBindKey() + "\u001f" + actionKey + "\u001f" + keysBinding;
        for (int i = 0; i < points.Count; i++)
        {
            object key = keys == null ? i : keys[i];
            bool allowed = editablePoints == null || editablePoints.Contains(i);
            UiLayoutRect hit = PointHit(points[i], plotRect);
            UiInteractionResult operation = ctx.Controls.Point(hit, plotRect, ctx, targetBinding, key, points[i].X, points[i].Y, allowed && ctx.Bindings.CanExecute(actionKey));
            if (!operation.Request)
                continue;
            identity.NoteRequest(key, operation.Value, operation.Y);
            int index = i;
            var change = new UiChartPointChange(index, operation.Value, operation.Y, keys == null ? null : keys[i]);
            UiInteractionRequests.Activation(ctx.Controls.InteractionTarget(ctx, targetBinding, "chart-point", key), operation, () => ctx.Bindings.Invoke(actionKey, change));
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

    private List<UiLayoutPoint> ResolvePoints(UiWidgetContext ctx)
    {
        string bindKey = ReadBindKey();
        if (bindKey.Length > 0 && ctx.Bindings.TryGet(bindKey, out IReadOnlyList<UiLayoutPoint> bound))
        {
            var result = new List<UiLayoutPoint>(bound.Count);
            foreach (UiLayoutPoint point in bound)
            {
                result.Add(new UiLayoutPoint(Clamp01(point.X), Clamp01(point.Y)));
            }

            return result;
        }

        return ParsePoints(spec.TryGetAttribute("Points", out string raw) ? raw : "");
    }

    internal static List<UiLayoutPoint> ParsePoints(string raw)
    {
        var result = new List<UiLayoutPoint>();
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

            result.Add(new UiLayoutPoint(Clamp01(x), Clamp01(y)));
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

    private static void DrawGrid(IUiDrawing drawing, UiLayoutRect plotRect, UiTheme theme)
    {
        for (int i = 1; i <= 3; i++)
        {
            float fx = plotRect.X + plotRect.Width * i / 4f;
            UiThemeDraw.Solid(drawing,new UiLayoutRect(fx, plotRect.Y, 1f, plotRect.Height), theme[UiPaletteRole.Divider]);
            float fy = plotRect.Y + plotRect.Height * i / 4f;
            UiThemeDraw.Solid(drawing,new UiLayoutRect(plotRect.X, fy, plotRect.Width, 1f), theme[UiPaletteRole.Divider]);
        }
    }

    private static void DrawPoints(IUiDrawing drawing, UiLayoutRect plotRect, IReadOnlyList<UiLayoutPoint> points, int? hoveredPoint, UiTheme theme)
    {
        int hovered = hoveredPoint ?? -1;
        for (int i = 0; i < points.Count; i++)
        {
            UiLayoutPoint pixel = ToRectPoint(points[i], plotRect);
            bool isHovered = i == hovered;
            float size = isHovered ? HoveredPointSize : PointSize;
            var pointRect = new UiLayoutRect(pixel.X - size * 0.5f, pixel.Y - size * 0.5f, size, size);
            UiThemeDraw.Solid(drawing,pointRect, isHovered ? theme.AccentHover : theme[UiPaletteRole.Accent]);
        }
    }

    internal static int? FindHoveredPoint(UiLayoutRect plotRect, IReadOnlyList<UiLayoutPoint> points, UiLayoutPoint pointer, HashSet<int>? editablePoints)
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

    private static UiLayoutPoint ToRectPoint(UiLayoutPoint normalized, UiLayoutRect plotRect)
    {
        return new UiLayoutPoint(plotRect.X + Clamp01(normalized.X) * plotRect.Width, plotRect.YMax - Clamp01(normalized.Y) * plotRect.Height);
    }

    private static float Distance(UiLayoutPoint a, UiLayoutPoint b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt((double)(dx * dx + dy * dy));
    }

    private static void DrawLine(IUiDrawing drawing, UiLayoutPoint a, UiLayoutPoint b, UiColour color)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float distance = (float)Math.Sqrt((double)(dx * dx + dy * dy));
        int steps = Math.Max(1, UiMath.CeilToInt(distance / 2f));
        for (int step = 0; step < steps; step++)
        {
            float t = steps == 1 ? 1f : step / (float)(steps - 1);
            float px = a.X + dx * t;
            float py = a.Y + dy * t;
            UiThemeDraw.Solid(drawing,new UiLayoutRect(px, py, 1f, 1f), color);
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



