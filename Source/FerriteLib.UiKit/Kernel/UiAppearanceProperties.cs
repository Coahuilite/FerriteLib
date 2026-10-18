using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

internal static class UiAppearanceProperties
{
    internal static readonly string[] Colours = { "Base", "Panel", "Raised", "Hover", "Selected", "Success", "Danger", "WorkspacePlane", "SectionBand", "TextPrimary", "TextSecondary", "TextOnGold", "TextOnDanger", "TextDisabled", "AccentGold", "SwitchThumbOff", "Border", "BorderStrong", "Divider", "BaseBorder", "PanelBorder", "RaisedBorder", "HoverBorder", "SelectedBorder", "SuccessBorder", "DangerBorder" };
    internal static readonly string[] Metrics = { "Padding", "Spacing", "Gap", "RowHeight", "Hairline" };
    internal static bool IsColour(string token) => Array.IndexOf(Colours, token) >= 0;
    internal static Color? ReadColour(UiTheme theme, string token)
        => token switch
        {
            "Base" => theme.Base,
            "Panel" => theme.Panel,
            "Raised" => theme.Raised,
            "Hover" => theme.Hover,
            "Selected" => theme.Selected,
            "Success" => theme.Success,
            "Danger" => theme.Danger,
            "WorkspacePlane" => theme.WorkspacePlane,
            "SectionBand" => theme.SectionBand,
            "TextPrimary" => theme.TextPrimary,
            "TextSecondary" => theme.TextSecondary,
            "TextOnGold" => theme.TextOnGold,
            "TextOnDanger" => theme.TextOnDanger,
            "TextDisabled" => theme.TextDisabled,
            "AccentGold" => theme.AccentGold,
            "SwitchThumbOff" => theme.SwitchThumbOff,
            "Border" => theme.Border,
            "BorderStrong" => theme.BorderStrong,
            "Divider" => theme.Divider,
            "BaseBorder" => theme.BaseBorder,
            "PanelBorder" => theme.PanelBorder,
            "RaisedBorder" => theme.RaisedBorder,
            "HoverBorder" => theme.HoverBorder,
            "SelectedBorder" => theme.SelectedBorder,
            "SuccessBorder" => theme.SuccessBorder,
            "DangerBorder" => theme.DangerBorder,
            _ => throw new ArgumentException("Unknown colour token '" + token + "'.")
        };
    internal static void ApplyColour(UiTheme theme, string token, Color? value)
    {
        switch (token)
        {
            case "Base": theme.Base = value ?? throw new ArgumentException("Base cannot be null."); break;
            case "Panel": theme.Panel = value ?? throw new ArgumentException("Panel cannot be null."); break;
            case "Raised": theme.Raised = value ?? throw new ArgumentException("Raised cannot be null."); break;
            case "Hover": theme.Hover = value ?? throw new ArgumentException("Hover cannot be null."); break;
            case "Selected": theme.Selected = value ?? throw new ArgumentException("Selected cannot be null."); break;
            case "Success": theme.Success = value ?? throw new ArgumentException("Success cannot be null."); break;
            case "Danger": theme.Danger = value ?? throw new ArgumentException("Danger cannot be null."); break;
            case "WorkspacePlane": theme.WorkspacePlane = value ?? throw new ArgumentException("WorkspacePlane cannot be null."); break;
            case "SectionBand": theme.SectionBand = value ?? throw new ArgumentException("SectionBand cannot be null."); break;
            case "TextPrimary": theme.TextPrimary = value ?? throw new ArgumentException("TextPrimary cannot be null."); break;
            case "TextSecondary": theme.TextSecondary = value ?? throw new ArgumentException("TextSecondary cannot be null."); break;
            case "TextOnGold": theme.TextOnGold = value ?? throw new ArgumentException("TextOnGold cannot be null."); break;
            case "TextOnDanger": theme.TextOnDanger = value ?? throw new ArgumentException("TextOnDanger cannot be null."); break;
            case "TextDisabled": theme.TextDisabled = value ?? throw new ArgumentException("TextDisabled cannot be null."); break;
            case "AccentGold": theme.AccentGold = value ?? throw new ArgumentException("AccentGold cannot be null."); break;
            case "SwitchThumbOff": theme.SwitchThumbOff = value ?? throw new ArgumentException("SwitchThumbOff cannot be null."); break;
            case "Border": theme.Border = value ?? throw new ArgumentException("Border cannot be null."); break;
            case "BorderStrong": theme.BorderStrong = value ?? throw new ArgumentException("BorderStrong cannot be null."); break;
            case "Divider": theme.Divider = value ?? throw new ArgumentException("Divider cannot be null."); break;
            case "BaseBorder": theme.BaseBorder = value; break;
            case "PanelBorder": theme.PanelBorder = value; break;
            case "RaisedBorder": theme.RaisedBorder = value; break;
            case "HoverBorder": theme.HoverBorder = value; break;
            case "SelectedBorder": theme.SelectedBorder = value; break;
            case "SuccessBorder": theme.SuccessBorder = value; break;
            case "DangerBorder": theme.DangerBorder = value; break;
            default: throw new ArgumentException("Unknown colour token '" + token + "'.");
        }
    }
    internal static float ReadMetric(UiDensity density, string token) => token switch
    {
        "Padding" => density.Padding,
        "Spacing" => density.Spacing,
        "Gap" => density.Gap,
        "RowHeight" => density.RowHeight,
        "Hairline" => density.Hairline,
        _ => throw new ArgumentException("Unknown density metric '" + token + "'.")
    };
}
