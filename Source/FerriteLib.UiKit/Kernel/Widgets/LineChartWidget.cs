using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Greenfield line chart with typed bindings and native hotControl drag semantics.
///
/// Points come from a typed <c>Bind</c> value binding of <c>IReadOnlyList&lt;Vector2&gt;</c>
/// (normalized 0..1) or from a static <c>Points</c> attribute. When <c>ActionBind</c> is present and
/// <c>Editable="true"</c>, control points listed in <c>EditablePoints</c> are draggable:
/// MouseDown hit-tests and captures <see cref="GUIUtility.hotControl"/> through
/// <see cref="UiNative.CaptureHotControl"/>, MouseDrag updates the point while captured and emits a
/// typed <see cref="UiChartPointChange"/> through the action binding, and MouseUp releases the hot
/// control. Session state only remembers which point is being dragged; the native hot control is the
/// capture authority.
/// </summary>
public sealed class LineChartWidget : IUiWidget
{
    public const string Kind = "chart/line";

    private const float DefaultHeight = 120f;
    private const float PlotPadding = 8f;
    private const float PointSize = 5f;
    private const float HoverPointSize = 9f;
    private const float HitRadius = 6f;
    private const float Epsilon = 0.0001f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new LineChartWidget(),
            new[] { "Id", "Kind", "Bind", "Points", "Height", "Editable", "EditablePoints", "ActionBind", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        string bindKey = ReadBindKey();
        bool hasBind = bindKey.Length > 0;
        bool hasStatic = spec.TryGetAttribute("Points", out string raw) && raw.Trim().Length > 0;
        if (hasBind)
        {
            bindings.ValidateValue<IReadOnlyList<Vector2>>(bindKey, elementPath);
        }

        if (spec.TryGetAttribute("ActionBind", out string actionKey) && actionKey.Length > 0)
        {
            bindings.ValidateAction<UiChartPointChange>(actionKey, elementPath);
        }

        if (!hasBind && !hasStatic)
        {
            throw new InvalidOperationException(
                $"LineChartWidget at '{elementPath}' requires either a 'Bind' value binding or a static 'Points' attribute.");
        }
    }

    public float Measure(UiWidgetContext ctx)
    {
        return ReadHeight();
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        List<Vector2> points = ResolvePoints(ctx);
        if (points.Count < 2) return;

        bool editable = ReadBool("Editable", true)
            && spec.TryGetAttribute("ActionBind", out string actionKey)
            && actionKey.Length > 0;

        Rect plotRect = new(
            rect.x + PlotPadding,
            rect.y + PlotPadding,
            Math.Max(1f, rect.width - PlotPadding * 2f),
            Math.Max(1f, rect.height - PlotPadding * 2f));

        UiThemeDraw.Surface(plotRect, ctx.Theme, ctx.Theme.Panel, ctx.Theme.Border);
        DrawGrid(plotRect, ctx.Theme);

        for (int i = 1; i < points.Count; i++)
        {
            DrawLine(ToRectPoint(points[i - 1], plotRect), ToRectPoint(points[i], plotRect), ctx.Theme.AccentGold);
        }

        HashSet<int>? editablePoints = null;
        if (editable && spec.TryGetAttribute("EditablePoints", out string editableRaw)
            && editableRaw.Trim().Length > 0)
        {
            editablePoints = ParseEditablePoints(editableRaw);
        }

        int? hoveredPoint = null;
        if (editable)
        {
            hoveredPoint = FindHoveredPoint(plotRect, points, UiNative.PointerPosition(), editablePoints);
            HandleDrag(plotRect, points, editablePoints, ctx);
        }

        DrawPoints(plotRect, points, hoveredPoint, ctx.Theme);
    }

    /// IMGUI capture semantics). Capture ownership is recorded in the session so closing the host
    /// releases exactly this session's hot control and no other session's.
    /// </summary>
    private void HandleDrag(Rect plotRect, List<Vector2> points, HashSet<int>? editablePoints, UiWidgetContext ctx)
    {
        string elementId = spec.Id.Length > 0 ? spec.Id : spec.Kind;
        int controlId = UiNative.GetControlId(elementId);
        bool captured = ctx.Session.IsHotControlOwned(controlId);
        UiValueState state = ctx.Session.GetOrCreateValueState(elementId);
        Vector2 pointer = UiNative.PointerPosition();

        if (UiNative.IsPointerDown() && !captured)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (editablePoints != null && !editablePoints.Contains(i)) continue;
                Vector2 pixel = ToRectPoint(points[i], plotRect);
                if (Distance(pointer, pixel) <= HitRadius)
                {
                    ctx.Session.CaptureHotControl(controlId);
                    state.Dragging = true;
                    state.Cursor = i;
                    UiNative.ConsumePointerEvent();
                    break;
                }
            }
        }

        if (captured && state.Dragging && state.Cursor >= 0 && state.Cursor < points.Count)
        {
            if (UiNative.IsPointerDragging())
            {
                Vector2 updated = ToNormalized(plotRect, pointer);
                Vector2 previous = points[state.Cursor];
                if (Math.Abs(previous.x - updated.x) > Epsilon || Math.Abs(previous.y - updated.y) > Epsilon)
                {
                    points[state.Cursor] = updated;
                    InvokeChange(state.Cursor, updated, ctx);
                }

                UiNative.ConsumePointerEvent();
            }
        }

        if (UiNative.IsPointerUp() && captured)
        {
            ctx.Session.ReleaseHotControl(controlId);
            state.Dragging = false;
            UiNative.ConsumePointerEvent();
        }
    }
    private void InvokeChange(int index, Vector2 point, UiWidgetContext ctx)
    {
        if (!spec.TryGetAttribute("ActionBind", out string actionKey) || actionKey.Length == 0) return;
        ctx.Bindings.Invoke(actionKey, new UiChartPointChange(index, point.x, point.y));
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
        if (raw == null) return result;

        string[] pointParts = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string pointPart in pointParts)
        {
            string[] xy = pointPart.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (xy.Length != 2)
                throw new FormatException($"Invalid chart point '{pointPart.Trim()}'; expected \"x,y\".");

            if (!float.TryParse(xy[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(xy[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
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
            UiThemeDraw.Solid(new Rect(fx, plotRect.y, 1f, plotRect.height), theme.Divider);

            float fy = plotRect.y + plotRect.height * i / 4f;
            UiThemeDraw.Solid(new Rect(plotRect.x, fy, plotRect.width, 1f), theme.Divider);
        }
    }

    private static void DrawPoints(Rect plotRect, IReadOnlyList<Vector2> points, int? hoveredPoint, UiTheme theme)
    {
        int hovered = hoveredPoint ?? -1;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 pixel = ToRectPoint(points[i], plotRect);
            bool isHovered = i == hovered;
            float size = isHovered ? HoverPointSize : PointSize;
            var pointRect = new Rect(pixel.x - size * 0.5f, pixel.y - size * 0.5f, size, size);
            UiThemeDraw.Solid(pointRect, isHovered ? theme.HoverPoint : theme.AccentGold);
        }
    }

    internal static int? FindHoveredPoint(Rect plotRect, IReadOnlyList<Vector2> points, Vector2 pointer, HashSet<int>? editablePoints)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if (editablePoints != null && !editablePoints.Contains(i)) continue;
            if (Distance(pointer, ToRectPoint(points[i], plotRect)) <= HitRadius)
            {
                return i;
            }
        }

        return null;
    }

    private static Vector2 ToRectPoint(Vector2 normalized, Rect plotRect)
    {
        return new Vector2(
            plotRect.x + Clamp01(normalized.x) * plotRect.width,
            plotRect.yMax - Clamp01(normalized.y) * plotRect.height);
    }

    private static Vector2 ToNormalized(Rect plotRect, Vector2 pixel)
    {
        return new Vector2(
            Clamp01((pixel.x - plotRect.x) / Math.Max(1f, plotRect.width)),
            Clamp01(1f - (pixel.y - plotRect.y) / Math.Max(1f, plotRect.height)));
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
        if (spec.TryGetAttribute("Height", out string raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float height)
            && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
    }

    private bool ReadBool(string name, bool fallback)
    {
        if (!spec.TryGetAttribute(name, out string raw)) return fallback;
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "1", StringComparison.Ordinal);
    }

    private static float Clamp01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }
}
