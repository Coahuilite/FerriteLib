using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

public sealed partial class UiTheme
{
    private readonly Dictionary<string, UiPropertyDeclaration<UiColour?>> textColours = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, UiColour?> appliedTextColours = new(System.StringComparer.Ordinal);

    internal UiPropertyDeclaration<UiColour?> TextColourDeclaration(string property)
        => UiTextColourProperties.Declaration(textColours, property, () => applicationRevision++);
    internal bool HasTextColourOverride(string property)
        => textColours.TryGetValue(property, out var declaration) && declaration.IsDeclared;
    internal UiColour? ReadTextColour(string property)
        => appliedTextColours.TryGetValue(property, out UiColour? applied) ? applied
            : HasTextColourOverride(property) ? textColours[property].Value : null;
    internal UiColour? DeclaredTextColour(string property) => textColours[property].Value;
    internal void ApplyStyleTextColour(string property, UiColour? value)
    {
        if (appliedTextColours.TryGetValue(property, out UiColour? prior) && prior == value) return;
        appliedTextColours[property] = value;
        applicationRevision++;
    }
    private void ClearTextColourApplications()
    {
        if (appliedTextColours.Count == 0) return;
        appliedTextColours.Clear();
        applicationRevision++;
    }
    private void RestoreTextColours(UiTheme source)
    {
        // Keep declarations already held by an author live across theme restoration.
        foreach (var pair in textColours) pair.Value.Remove();
        foreach (var pair in source.textColours)
            if (pair.Value.IsDeclared) TextColourDeclaration(pair.Key).Set(pair.Value.Value);
        appliedTextColours.Clear();
        foreach (var pair in source.appliedTextColours) appliedTextColours.Add(pair.Key, pair.Value);
    }
}
