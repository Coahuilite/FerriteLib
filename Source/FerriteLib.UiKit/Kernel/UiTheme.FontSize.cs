namespace FerriteLib.UiKit.Kernel;

public sealed partial class UiTheme
{
    private UiPropertyDeclaration<int>? fontSize;
    private bool hasStyleFontSize;
    private int? styleFontSize, observedFontSize;
    private bool restoringFontSize;
    internal UiPropertyDeclaration<int> FontSizeDeclaration => fontSize ??= UiTextFonts.Declaration(FontSizeChanged);
    internal bool HasFontSizeOverride => fontSize?.IsDeclared == true;
    internal int? ReadFontSize => hasStyleFontSize ? styleFontSize : HasFontSizeOverride ? fontSize!.Value : null;
    internal int DeclaredFontSize => fontSize!.Value;
    internal void RemoveFontSizeOverride() => fontSize?.Remove();
    private void FontSizeChanged()
    {
        if (restoringFontSize) return;
        PropertyRevision++;
        if (observedFontSize != ReadFontSize) LayoutRevision++;
        observedFontSize = ReadFontSize;
    }
    internal void ApplyStyleFontSize(int? value)
    {
        int? previous = ReadFontSize;
        hasStyleFontSize = true; styleFontSize = value;
        if (ReadFontSize != previous) LayoutRevision++;
        observedFontSize = ReadFontSize;
    }
    private void ClearFontSizeApplication()
    {
        int? previous = ReadFontSize;
        hasStyleFontSize = false; styleFontSize = null;
        if (ReadFontSize != previous) LayoutRevision++;
        observedFontSize = ReadFontSize;
    }
    private void RestoreFontSize(UiTheme source)
    {
        int? previous = ReadFontSize;
        restoringFontSize = true;
        try
        {
            fontSize?.Remove();
            if (source.HasFontSizeOverride) FontSizeDeclaration.Set(source.DeclaredFontSize);
            hasStyleFontSize = source.hasStyleFontSize; styleFontSize = source.styleFontSize;
        }
        finally { restoringFontSize = false; }
        if (ReadFontSize != previous) LayoutRevision++;
        observedFontSize = ReadFontSize;
    }
}
