using System;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;
/// <summary>Owner-local control composition over the shared headless interaction and edit state machines.</summary>
public sealed partial class UiControls
{
    private readonly UiSession session;
    internal UiControls(UiSession session) => this.session = session;
    private UiFrame Input => session.Input;
    private IUiExecution Execution => session.Execution;
    private IUiDrawing Drawing => Execution;
    public Action<string>? Trace { get; set; }

    public UiControlKey GetControlId(string key)
    {
        var result = new UiControlKey(key);
        Execution.PrepareControl(result);
        return result;
    }

    private bool HasHotControl(UiControlKey key) => Execution.HasCapture(key);
    public bool Button(UiLayoutRect rect, UiWidgetContext ctx) => Activation(rect, ctx).Request;
    public bool CanReceivePointerPress(UiLayoutRect rect, UiWidgetContext ctx) => CanReceivePointer(ctx) && IsMouseOver(rect);
    private bool CanReceivePointer(UiWidgetContext ctx)
    {
        RequireOwner(ctx);
        return !IsInputDisabled(ctx.Node) && session.PointerInputAllowed && session.IsInputGeometryReliable && !session.IsPointerOverHigherLayer(ctx.Node, WindowPointer(ctx)) && session.IsPointInEffectiveClip(WindowPointer(ctx));
    }

    private void RequireOwner(UiWidgetContext ctx)
    {
        if (ctx.Node.Id.IsNone)
            throw new InvalidOperationException("Pointer/native controls require an owner-created element context; use headless behaviors for semantic operations.");
        if (!ReferenceEquals(ctx.Session, session))
            throw new InvalidOperationException("Controls require their owning session context.");
    }

    public UiLayoutPoint WindowPointer(UiWidgetContext ctx)
    {
        RequireOwner(ctx);
        return Input.Pointer;
    }

    internal string DiagnosticEventName() => Input.Phase.ToString();
    internal static bool IsInputDisabled(UiNode? node)
    {
        for (UiNode? n = node; n != null; n = n.Parent)
            if (n.IsDisabled || n.PresentationUnavailable || n.QualificationFault != null)
                return true;
        return false;
    }

    public bool DropdownButton(UiLayoutRect rect, string key, UiWidgetContext ctx) => DropdownButton(rect, key, ctx, ctx.Node.Id.Key);
    public bool DropdownButton(UiLayoutRect rect, string key, UiWidgetContext ctx, string triggerKey)
    {
        RequireOwner(ctx);
        if (IsInputDisabled(ctx.Node))
            return false;
        var target = InteractionTarget(ctx, key, "trigger", triggerKey);
        session.RefreshPopupTarget(key, target);
        if (session.IsPopupOpen(key, target))
            session.NotePopupOwnerDrawn(key, ctx.ToWindowRect(rect));
        if (session.IsPointerOverHigherLayer(ctx.Node, WindowPointer(ctx)))
            return false;
        var input = Activation(rect, ctx, key, "trigger", triggerKey);
        var operation = UiValueBehavior.Expansion(input, !session.IsPopupOpen(key, target), true);
        UiValueBehavior.Request(target, operation, expanded =>
        {
            if (!expanded)
                session.ClosePopup();
            else
            {
                session.OpenPopup(key, ctx.ToWindowRect(rect));
                session.RefreshPopupTarget(key, target);
            }
        });
        if (operation.Request)
            session.NoteInteractionTarget(ctx.Node);
        return operation.Request;
    }

    private bool IsCoveredByPopup(UiSession owner) => owner.IsPointerOverPopup(Input.Pointer);
    public UiLayoutPoint PointerPosition() => Input.Pointer - session.CurrentWindowOrigin;
    public bool IsPointerDown() => Input.Phase == UiFramePhase.MouseDown && Input.Button == 0;
    public bool IsPointerDragging() => Input.Phase == UiFramePhase.MouseDrag && Input.Button == 0;
    public bool IsPointerUp() => Input.Phase == UiFramePhase.MouseUp && Input.Button == 0;
    public float PointerWheelNotches() => Input.Phase == UiFramePhase.ScrollWheel ? Input.Wheel : 0;
    internal void TakePopupWheelIfCovering(UiSession owner)
    {
        float notches = PointerWheelNotches();
        if (notches == 0 || owner.OpenPopupId == null || !owner.OpenPopupCoversPointer(Input.Pointer))
            return;
        int rows = (int)Math.Round(notches * UiPopup.WheelRowsPerNotch, MidpointRounding.AwayFromZero);
        owner.ScrollPopupByRows(rows == 0 ? (notches > 0 ? 1 : -1) : rows);
        ConsumePointerEvent();
    }

    internal void DismissPopupOnOutsidePress(UiSession owner)
    {
        if (!owner.PointerInputAllowed || owner.OpenPopupId == null || !IsPointerDown())
            return;
        if (owner.OpenPopupCoversPointer(Input.Pointer) || owner.OpenPopupAnchor?.Contains(Input.Pointer) == true)
            return;
        owner.ClosePopup();
        ConsumePointerEvent();
    }

    internal static string Describe(UiLayoutRect r) => r.ToString();
    public void ConsumePointerEvent()
    {
        Input.Consume();
        Execution.ConsumeEvent();
    }

    public void ConsumeKeyEvent() => ConsumePointerEvent();
    internal static bool TryParseNumber(string text, out float value) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    internal static float ClampValue(float value, float min, float max) => UiMath.Clamp(value, min, max);
    internal static string FormatValue(float value, string format) => value.ToString(string.IsNullOrEmpty(format) ? "0.##" : format, CultureInfo.InvariantCulture);
    public bool IsMouseDownOver(UiLayoutRect rect) => IsPointerDown() && IsMouseOver(rect);
    public bool IsMouseDragOver(UiLayoutRect rect) => IsPointerDragging() && IsMouseOver(rect);
    public bool IsMouseUpOver(UiLayoutRect rect) => IsPointerUp() && IsMouseOver(rect);
    public bool IsMouseOver(UiLayoutRect rect) => Input.InputAllowed && rect.Contains(PointerPosition());
    public bool IsMouseOver(UiLayoutRect rect, UiWidgetContext ctx) => !session.IsPointerOverHigherLayer(ctx.Node, WindowPointer(ctx)) && IsMouseOver(rect);
    public bool IsEnterPressed() => Input.Phase == UiFramePhase.KeyDown && (Input.Key == UiKey.Return || Input.Key == UiKey.KeypadEnter);
    public bool IsLayoutEvent() => Input.Phase == UiFramePhase.Layout;
    internal bool IsFocusLost(UiLayoutRect rect, UiSession owner) => owner.PassBeganWithPrimaryPress && !IsMouseOver(rect);
}
