using System;

namespace FerriteLib.UiKit.Kernel;
public sealed partial class UiControls
{
    /// <summary>Runs a custom field's semantic draft/completion on its original editor and Session owner.</summary>
    /// <remarks>
    /// Public-unstable. Use the same binding, formatter, parser, native Live policy and typed callbacks as Edit.
    /// SetDraft is always local, including when live is true; only explicit CommitEdit submits it.
    /// This synchronous owner-thread entry needs no Tools connection. Host dispatch still owns visibility,
    /// target lifetime and current command qualification. False means the intent was not handled, not saved.
    /// </remarks>
    public bool EditSemantic<T>(UiWidgetContext ctx, string binding, UiToolOperation operation, string? text,
        Func<T, string> format, UiEditParser<T> parse, bool live, Action<T> request, Func<T> readModel)
    {
        RequireOwner(ctx);
        if (operation != UiToolOperation.SetDraft && operation != UiToolOperation.CommitEdit && operation != UiToolOperation.CancelEdit) return false;
        if (operation == UiToolOperation.SetDraft && text == null) return false;
        if (Input.Composing || IsInputDisabled(ctx.Node) || !ctx.Session.PointerInputAllowed || !ctx.Session.IsInputGeometryReliable
            || ctx.Session.OpenPopupId != null || ctx.Session.Interactions.CaptureOwner.HasValue) return false;
        for (UiNode? owner = ctx.Node; owner != null; owner = owner.Parent)
            if (owner.Kind.Length > 0 && !owner.IsArranged) return false;
        if (ctx.Session.ActiveEditNode != null && !ReferenceEquals(ctx.Session.ActiveEditNode, ctx.Node)) return false;
        UiControlState state = ctx.Session.GetControlState(ctx.Node.Id, binding);
        if (operation != UiToolOperation.SetDraft && state.Editor is not UiEditSession<T>) return false;
        if (operation != UiToolOperation.CancelEdit && !ctx.Bindings.IsWritable(binding)) return false;
        UiEditSession<T> editor = Editor(ctx, binding, format, parse);
        state.CommitRequested = state.DiscardRequested = false;
        try
        {
            editor.Observe(readModel());
            if (operation == UiToolOperation.SetDraft)
            {
                editor.Focus();
                // Setting a draft is deliberately local; only the explicit completion submits it.
                editor.Input(text!, true, false, false, request, readModel);
                editor.HoldSemanticDraft();
                return true;
            }
            if (operation == UiToolOperation.CancelEdit) editor.Cancel(readModel());
            else
            {
                if (!editor.Valid) return false;
                var target = InteractionTarget(ctx, binding, "editor");
                editor.Confirm(true, candidate => UiInteractionRequests.Activation(target,
                    ctx.Session.Interactions.Activate(target, new UiInputEligibility(true, ctx.Bindings.IsWritable(binding))), () => request(candidate)), readModel);
            }
            return true;
        }
        finally
        {
            // Match the native editor exit even when a parser/request/readback throws or closes the Host.
            try { if (!editor.Focused) ReleaseEditFocus(state, ctx.Session); }
            finally
            {
                if (ctx.Session.IsActive)
                {
                    if (editor.Focused) ctx.Session.NoteEditOpened(state, ctx.Node);
                    else ctx.Session.NoteEditClosed(state);
                    ctx.Node.MarkDirty();
                }
            }
        }
    }
    private bool ContentCoversEditor(UiLayoutRect rect, UiSession session, UiNode node, UiLayoutPoint windowPoint) => (session.PassBeganWithPrimaryPress || PointerPhase() != UiPointerPhase.None) && IsMouseOver(rect) && session.IsPointerOverHigherLayer(node, windowPoint);
    public UiEditSession<T> Editor<T>(UiWidgetContext ctx, string binding, Func<T, string> format, UiEditParser<T> parse)
    {
        RequireOwner(ctx);
        UiNode node = ctx.Node;
        return Editor(ctx.Session, node, binding, InteractionTarget(ctx, binding, "editor"), format, parse);
    }

    /// <summary>End native focus without submitting or discarding a participating composite's draft.</summary>
    public void SuspendEditor(UiWidgetContext ctx, string binding)
    {
        RequireOwner(ctx);
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
        RequireOwner(ctx);
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
            UiTextRun text = UiTextFonts.For(ctx, purpose: UiTextPurpose.Editor);
            UiColour? colour = null;
            foreach (string slot in UiTextColourProperties.Names)
                if (ctx.Theme.ReadTextColour(slot).HasValue)
                {
                    colour = ctx.Properties.TextStyleSpec is UiElementSpec written
                        ? Widgets.AtomVocabulary.ResolveRole(written, ctx, node.IsDisabled ? false : writable).Text
                        : ctx.Theme.Styles.Resolve(node.IsDisabled ? UiStatusTone.Disabled : UiStatusTone.Neutral, writable: writable).Text;
                    break;
                }
            if (colour.HasValue && Execution is not IUiTextFontEditing)
                UiFitAudit.ReportStyleFallback(ctx.ElementPath, "Editor", "TextColor", "explicit colour", "host does not support Editor colour");
            DriveEditor(rect, session, node, state, editor, model, available, covered, writable, CanReceivePointerPress(rect, ctx) && IsPointerDown(), live, multiline, text, colour, Request, readModel);
        }
    }

    private void DriveEditor<T>(UiLayoutRect rect, UiSession session, UiNode node, UiControlState state, UiEditSession<T> editor, T model, bool available, bool covered, bool writable, bool focus, bool live, bool multiline, UiTextRun textRun, UiColour? colour, Action<T> request, Func<T> readModel)
    {
        UiTextFont font = textRun.Font;
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
            UiNativeEdit nativeRequest = new UiNativeEdit(controlName, rect, editor.Draft, font.Alias, multiline, writable, editor.Focused, composing);
            UiNativeEditResult native = Execution is IUiTextFontEditing sized
                ? sized.Edit(new UiTextEdit(nativeRequest, font, colour)) : Execution.Edit(nativeRequest);
            UiColour? actualColour = Execution is IUiTextFontEditing ? colour : null;
            if (textRun.Requested.Size.HasValue || colour.HasValue) UiFitAudit.CheckText(rect, native.Text, textRun, actualColour, !multiline);
            node.Properties.TextPaints[UiTextPurpose.Editor] = new UiTextPaintFact(font, actualColour, "native-editor");
#if FER_DEV || FER_TOOLS
            UiDiagnosticHub.ActiveSubscription?.Geometry?.NoteText(node.Id.Key, UiTextPurpose.Editor, font, actualColour, "native-editor");
#endif
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
        IUiExecution? execution=session.CurrentExecution;
        if (execution?.HasFocus(EditControlName(session, state))==true)
            execution.ReleaseFocus(EditControlName(session, state));
    }
}
