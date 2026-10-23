namespace FerriteLib.UiKit.Kernel;
/// <summary>Internal edit/chart ownership only. No writable compatibility projections.</summary>
internal sealed class UiControlState
{
    internal object? Editor;
    internal string Draft => (Editor as IUiEditState)?.Draft ?? "";
    internal bool IsFocused => (Editor as IUiEditState)?.Focused == true;
    internal UiInteractionTarget? EditorTarget;
    internal bool EditorMultiline;
    internal UiChartIdentity? ChartIdentity;
    internal bool CommitRequested, DiscardRequested;
}

