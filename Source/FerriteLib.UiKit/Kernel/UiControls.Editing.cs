using System;

namespace FerriteLib.UiKit.Kernel;
public sealed partial class UiControls
{
    private bool ContentCoversEditor(UiLayoutRect rect, UiSession session, UiNode node, UiLayoutPoint windowPoint) => (session.PassBeganWithPrimaryPress || PointerPhase() != UiPointerPhase.None) && IsMouseOver(rect) && session.IsPointerOverHigherLayer(node, windowPoint);
    public UiEditSession<T> Editor<T>(UiWidgetContext ctx, string binding, Func<T, string> format, UiEditParser<T> parse)
    {
        UiNode node = ctx.Node;
        return Editor(ctx.Session, node, binding, InteractionTarget(ctx, binding, "editor"), format, parse);
    }

    /// <summary>End native focus without submitting or discarding a participating composite's draft.</summary>
    public void SuspendEditor(UiWidgetContext ctx, string binding)
    {
        UiNode node = ctx.Node;
        UiControlState state = ctx.Session.GetControlState(node.Id, binding);
        if (state.Editor is IUiEditState editor)
            editor.Suspend();
        state.CommitRequested = false;
        ReleaseEditFocus(state, ctx.Session);
        ctx.Session.NoteEditClosed(state);
    }

    private UiEditSession<T> Editor<T>(UiSession session, UiNode node, string binding, UiInteractionTarget target, Func<T, string> format, UiEditParser<T> parse)
    {
        UiControlState state = session.GetControlState(node.Id, binding);
        if (node.EditingState != null && !ReferenceEquals(node.EditingState, state))
        {
            UiControlState previous = node.EditingState;
            previous.CommitRequested = previous.DiscardRequested = false;
            if (previous.Editor is IUiEditState previousEditor)
                previousEditor.Suspend();
            previous.Editor = null;
            ReleaseEditFocus(previous, session);
            session.NoteEditClosed(previous);
        }

        node.EditingState = state;
        if (state.Editor is not UiEditSession<T> editor || state.EditorTarget?.Equals(target) != true)
        {
            session.NoteEditClosed(state);
            ReleaseEditFocus(state, session);
            if (state.Editor is IUiEditState priorEditor)
                priorEditor.Suspend();
            editor = new UiEditSession<T>(format, parse);
            state.Editor = editor;
            state.EditorTarget = target;
            state.CommitRequested = state.DiscardRequested = false;
        }

        editor.Configure(format, parse);
        return editor;
    }

    public void Edit<T>(UiLayoutRect rect, UiWidgetContext ctx, string binding, T model, Func<T, string> format, UiEditParser<T> parse, bool live, bool multiline, Action<T> request, Func<T> readModel)
    {
        UiSession session = ctx.Session;
        UiNode node = ctx.Node;
        UiControlState state = session.GetControlState(node.Id, binding);
        UiEditSession<T> editor = Editor(ctx, binding, format, parse);
        bool writable = ctx.Bindings.IsWritable(binding);
        bool available = !IsInputDisabled(node) && session.PointerInputAllowed && session.IsInputGeometryReliable;
        bool covered = IsCoveredByPopup(session) || ContentCoversEditor(rect, session, node, WindowPointer(ctx));
        void Request(T candidate)
        {
            UiInteractionResult operation = session.Interactions.Activate(InteractionTarget(ctx, binding, "editor"), new UiInputEligibility(available, writable));
            UiInteractionRequests.Activation(InteractionTarget(ctx, binding, "editor"), operation, () => request(candidate));
        }

        {
            DriveEditor(rect, session, node, state, editor, model, available, covered, writable, CanReceivePointerPress(rect, ctx) && IsPointerDown(), live, multiline, ctx.Theme.Styles.Font, Request, readModel);
        }
    }

    private void DriveEditor<T>(UiLayoutRect rect, UiSession session, UiNode node, UiControlState state, UiEditSession<T> editor, T model, bool available, bool covered, bool writable, bool focus, bool live, bool multiline, UiFont font, Action<T> request, Func<T> readModel)
    {
        state.EditorMultiline = multiline;
        string previousDraft = editor.Draft;
        editor.Observe(model);
        UiControlKey controlName = EditControlName(session, state);
        if (Execution.HasFocus(controlName))
            editor.Focus();
        try
        {
            if (state.DiscardRequested)
            {
                state.DiscardRequested = state.CommitRequested = false;
                editor.Cancel(readModel());
                ReleaseEditFocus(state, session);
                return;
            }

            if (!available)
            {
                editor.Suspend();
                ReleaseEditFocus(state, session);
                state.CommitRequested = false;
                return;
            }

            if (covered)
                return;
            if (focus)
                editor.Focus();
            bool composing = Input.Composing;
            bool confirm = editor.Focused && !composing && (state.CommitRequested || (IsEnterPressed() && (!multiline || Input.Control)));
            bool blur = editor.Focused && IsFocusLost(rect, session);
            // Confirm before the native text control uses Return. Composition owns its own Return/Escape.
            if (confirm)
            {
                state.CommitRequested = false;
                if (writable)
                    editor.Confirm(true, request, readModel);
                ReleaseEditFocus(state, session);
                ConsumeKeyEvent();
            }
            else if (blur)
            {
                if (writable)
                    editor.Confirm(false, request, readModel);
                else
                    editor.Suspend();
                ReleaseEditFocus(state, session);
            }

            if (!session.IsActive)
                return;
            UiNativeEditResult native = Execution.Edit(new UiNativeEdit(controlName, rect, editor.Draft, font, multiline, writable, editor.Focused, composing));
            string typed = native.Text;
            if (native.Consumed)
                Input.Consume();
            if (!confirm && !blur && native.Focused)
                editor.Focus();
            // Preserve the original primitive condition: a native change without a held edit is immediate.
            bool nativeChanged = !string.Equals(typed, editor.Draft, StringComparison.Ordinal);
            editor.Input(typed, writable, composing, live || (!editor.Focused && nativeChanged), request, readModel);
            if (!session.IsActive)
                return;
            if (composing && IsCompositionKey())
                ConsumeKeyEvent();
        }
        finally
        {
            if (!editor.Focused)
                ReleaseEditFocus(state, session);
            if (session.IsActive)
            {
                if (multiline && !string.Equals(previousDraft, editor.Draft, StringComparison.Ordinal))
                    node.MarkDirty();
                if (editor.Focused)
                    session.NoteEditOpened(state, node);
                else
                    session.NoteEditClosed(state);
            }
        }
    }

    internal bool NativeCompositionActive => Input.Composing;

    internal bool NativeMultilineReturn(UiControlState edit) => edit.EditorMultiline && IsEnterPressed() && !Input.Control;
    private bool IsCompositionKey() => Input.Phase == UiFramePhase.KeyDown && (IsEnterPressed() || Input.Key == UiKey.Escape);
    private UiControlKey EditControlName(UiSession session, UiControlState state) => new UiControlKey("fl-edit/" + session.Identity + "/" + state.EditorTarget?.Node + "/" + state.EditorTarget?.TargetScope);
    internal void ReleaseEditFocus(UiControlState state, UiSession session)
    {
        if (Execution.HasFocus(EditControlName(session, state)))
            Execution.ReleaseFocus(EditControlName(session, state));
    }
}
