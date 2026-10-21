using System;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Kernel;
public static partial class UiNative
{
    private static bool ContentCoversEditor(Rect rect, UiSession session, UiNode node, Vector2 windowPoint)
        => (session.PassBeganWithPrimaryPress || PointerPhase() != UiPointerPhase.None)
            && IsMouseOver(rect) && session.IsPointerOverHigherLayer(node, windowPoint);
    private static string LegacyEditor<T>(Rect rect, string binding, UiSession session, T model, Func<T, string> format, UiEditParser<T> parse, out bool committed, out T candidate)
    {
        UiNode node = session.ActiveNode;
        var target = new UiInteractionTarget(node.Id.Key, binding + "\u001f" + node.InteractionScope, "editor", null, node.InteractionScope);
        UiEditSession<T> editor = Editor(session, node, binding, target, format, parse);
        UiValueState state = session.GetOrCreateValueState(node.Id, binding);
        bool disabled = IsElementDisabled(session, binding);
        // The historical return-value primitive explicitly discarded on disable. Preserve that entry's policy.
        if (disabled)
            editor.Cancel(model);
        bool requested = false;
        T result = model;
        DriveEditor(rect, session, node, state, editor, model, !disabled, IsCoveredByPopup(session) || ContentCoversEditor(rect, session, node, PointerPosition() + session.CurrentWindowOrigin), true, IsMouseDownOver(rect), true, false, value =>
        {
            requested = true;
            result = value;
        }, () => requested ? result : model);
        committed = requested;
        candidate = result;
        return editor.Draft;
    }

    public static UiEditSession<T> Editor<T>(UiWidgetContext ctx, string binding, Func<T, string> format, UiEditParser<T> parse)
    {
        UiNode node = ctx.Node ?? ctx.Session.ActiveNode;
        return Editor(ctx.Session, node, binding, InteractionTarget(ctx, binding, "editor"), format, parse);
    }

    private static UiEditSession<T> Editor<T>(UiSession session, UiNode node, string binding, UiInteractionTarget target, Func<T, string> format, UiEditParser<T> parse)
    {
        UiValueState state = session.GetOrCreateValueState(node.Id, binding);
        if (node.EditingState != null && !ReferenceEquals(node.EditingState, state))
        {
            UiValueState previous = node.EditingState;
            previous.Focused = previous.CommitRequested = previous.DiscardRequested = false;
            if (previous.Editor is IUiEditState previousEditor) previousEditor.Suspend();
            previous.Editor = null;
            ReleaseEditFocus(previous, session);
            session.NoteEditClosed(previous);
        }

        node.EditingState = state;
        if (state.Editor is not UiEditSession<T> editor || state.EditorTarget?.Equals(target) != true)
        {
            session.NoteEditClosed(state);
            ReleaseEditFocus(state, session);
            if (state.Editor is IUiEditState priorEditor) priorEditor.Suspend();
            editor = new UiEditSession<T>(format, parse);
            state.Editor = editor;
            state.EditorTarget = target;
            state.CommitRequested = state.DiscardRequested = false;
        }

        editor.Configure(format, parse);
        return editor;
    }

    public static void Edit<T>(Rect rect, UiWidgetContext ctx, string binding, T model, Func<T, string> format, UiEditParser<T> parse, bool live, bool multiline, Action<T> request, Func<T> readModel)
    {
        UiSession session = ctx.Session;
        UiNode node = ctx.Node ?? session.ActiveNode;
        UiValueState state = session.GetOrCreateValueState(node.Id, binding);
        UiEditSession<T> editor = Editor(ctx, binding, format, parse);
        bool writable = ctx.Bindings.IsWritable(binding);
        bool available = !IsInputDisabled(node) && session.PointerInputAllowed && session.IsInputGeometryReliable;
        bool covered = IsCoveredByPopup(session) || ContentCoversEditor(rect, session, node, WindowPointer(ctx));
        void Request(T candidate)
        {
            UiInteractionResult operation = session.Interactions.Activate(InteractionTarget(ctx, binding, "editor"), new UiInputEligibility(available, writable));
            UiInteractionRequests.Activation(InteractionTarget(ctx, binding, "editor"), operation, () => request(candidate));
        }

        GameFont previousFont = Text.Font;
        try
        {
            Text.Font = UiKitFonts.ToGameFont(ctx.Theme.Styles.Font);
            DriveEditor(rect, session, node, state, editor, model, available, covered, writable, CanReceivePointerPress(rect, ctx) && IsPointerDown(), live, multiline, Request, readModel);
        }
        finally
        {
            Text.Font = previousFont;
        }
    }

    private static void DriveEditor<T>(Rect rect, UiSession session, UiNode node, UiValueState state, UiEditSession<T> editor, T model, bool available, bool covered, bool writable, bool focus, bool live, bool multiline, Action<T> request, Func<T> readModel)
    {
        state.EditorMultiline = multiline;
        string previousDraft = editor.Draft;
        editor.Observe(model);
        string controlName = EditControlName(session, state);
        if (GUI.GetNameOfFocusedControl() == controlName)
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
            bool composing = Input.compositionString.Length != 0;
            bool confirm = editor.Focused && !composing && (state.CommitRequested || (IsEnterPressed() && (!multiline || Event.current?.control == true)));
            bool blur = editor.Focused && IsFocusLost(rect, session);
            // Confirm before the native text control uses Return. Composition owns its own Return/Escape.
            if (confirm)
            {
                state.CommitRequested = false;
                if (writable)
                    editor.Confirm(true, request, readModel);
                ReleaseEditFocus(state, session);
                Event.current?.Use();
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
            GUI.SetNextControlName(controlName);
            bool keyboardOwner = DebugMousePositionEnabled ? editor.Focused : GUI.GetNameOfFocusedControl() == controlName;
            string typed = NativeEditor(rect, editor.Draft, multiline, writable, keyboardOwner);
            if (!confirm && !blur && GUI.GetNameOfFocusedControl() == controlName)
                editor.Focus();
            // Preserve the original primitive condition: a native change without a held edit is immediate.
            bool nativeChanged = !string.Equals(typed, editor.Draft, StringComparison.Ordinal);
            editor.Input(typed, writable, composing, live || (!editor.Focused && nativeChanged), request, readModel);
            if (!session.IsActive)
                return;
            if (composing && IsCompositionKey())
                Event.current?.Use();
        }
        finally
        {
            if (!editor.Focused)
                ReleaseEditFocus(state, session);
            if (session.IsActive)
            {
                if (multiline && !string.Equals(previousDraft, editor.Draft, StringComparison.Ordinal)) node.MarkDirty();
                state.EditText = editor.Draft;
                state.Focused = editor.Focused;
                if (editor.Focused)
                    session.NoteEditOpened(state, node);
                else
                    session.NoteEditClosed(state);
            }
        }
    }

    internal static bool NativeCompositionActive => Input.compositionString.Length != 0;

    internal static bool NativeMultilineReturn(UiValueState edit) => edit.EditorMultiline && IsEnterPressed() && Event.current?.control != true;
    private static bool IsCompositionKey() => Event.current?.type == EventType.KeyDown && (IsEnterPressed() || Event.current.keyCode == KeyCode.Escape);
    private static string EditControlName(UiSession session, UiValueState state) => "fl-edit/" + session.Identity + "/" + state.EditorTarget?.Node + "/" + state.EditorTarget?.TargetScope;
    internal static void ReleaseEditFocus(UiValueState state, UiSession session)
    {
        if (GUI.GetNameOfFocusedControl() == EditControlName(session, state))
            GUI.FocusControl(null);
    }

    private static string NativeEditor(Rect rect, string text, bool multiline, bool writable, bool keyboardOwner)
    {
        Event? current = Event.current;
        // Read-only still reaches the native selection/copy engine, but never its mutating key commands.
        if (keyboardOwner && !writable && current?.type == EventType.KeyDown)
        {
            KeyCode key = current.keyCode;
            bool shortcut = current.control || current.command;
            bool selectionOrCopy = shortcut && (key == KeyCode.A || key == KeyCode.C);
            bool navigation = key == KeyCode.UpArrow || key == KeyCode.DownArrow || key == KeyCode.LeftArrow || key == KeyCode.RightArrow || key == KeyCode.Home || key == KeyCode.End || key == KeyCode.PageUp || key == KeyCode.PageDown;
            if (!selectionOrCopy && !navigation)
                current.Use();
        }

        if (keyboardOwner && !writable && (current?.type == EventType.ExecuteCommand || current?.type == EventType.ValidateCommand) && current.commandName != "Copy" && current.commandName != "SelectAll")
            current.Use();
        if (TextFieldOverride != null)
            return TextFieldOverride(rect, text);
        return multiline ? Verse.Widgets.TextArea(rect, text, !writable) : Verse.Widgets.TextField(rect, text);
    }
}
