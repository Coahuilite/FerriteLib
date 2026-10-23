using UnityEngine;
using Verse;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Resolved-style lane (0.4.x style work): the token shape and the one table every appearance decision
/// goes through. In order it holds: every (tone, emphasis) cell's value; that the painting sites read
/// that table instead of re-deriving it — driven through the Verse stub's recorded DrawBoxSolid calls,
/// so a hand-derived site shows up as a wrong colour rather than as a code-review opinion; that an
/// undeclared key falls back instead of throwing and records what happened; that writability is the
/// reserved third key with state beating author; the per-surface (fill, border) pairs and the density
/// tokens; that the resolved font is the font the fitting audit measured with; and that a layout-token
/// change re-arranges the bands while a re-tint reuses them. Absolute pixels and the real font engine
/// remain outside this lane's claim.
/// </summary>
internal static class KernelResolvedStyleTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Every tone x emphasis cell resolves to a value", VerifyEveryCell);
        Run("The five painting sites read one table", VerifyOutletsShareOneTable);
        Run("Unknown keys fall back softly and loudly", VerifyUnknownKeysFallBack);
        Run("Writability is the reserved third key", VerifyWritabilityBeatsAuthor);
        Run("Per-surface pairs: one plane can override the shared edge", VerifySurfacePairs);
        Run("Density tokens move geometry and clamp instead of throwing", VerifyDensityTokens);
        Run("The resolved font is the font the fitting audit measured with", VerifyResolvedFontIsMeasured);
        Run("Text sites write the colour the table hands out", VerifyTextSitesFollowTheTable);
        Run("A layout-token change re-arranges; a re-tint reuses the bands", VerifyLayoutRevisionInvalidatesBands);
        Run("A colour assignment moves the paint clock and never the layout clock", VerifyColourRevisionIsThePaintClock);
        Run("A re-tint reaches a cached region theme without moving a rect", VerifyScopedPaletteRefresh);
        Run("A re-tint preserves the focused text field's draft through drawing", VerifyRetintPreservesFocusedDraft);
        Run("An unset token answers Vanilla; a transparent one stays transparent", VerifyPartialBagFallsBackToVanilla);
        Run("A partial bag's per-surface edges follow Vanilla until they are claimed", VerifyPartialBagEdgeFallback);
        Run("Every per-surface edge token is covered: claim, fallback, and the pair it feeds", VerifyPerSurfaceEdgeCoverage);
        Run("A scheme selects the typography metric while the retired Font declaration is refused", VerifySchemeFontAndRetirement);
        Run("A selected font reaches the real host measure; density does not move", VerifySelectedFontReachesHostMeasure);
        Run("TestPalettes.DarkGold is a harness-only warm-gold palette, a peer of Vanilla (C)", VerifyDarkGoldPalette);
        Run("UiTheme.Vanilla is the one shipped palette and the partial bag's fallback (C)", VerifyVanillaPaletteAndBag);
        ResetPointerSeams();
        return failures;
    }

    private static void VerifyEveryCell()
    {
        UiTheme theme = TestPalettes.DarkGold;

        ExpectCell(theme, UiStatusTone.Neutral, UiEmphasis.Normal, theme[UiPaletteRole.ControlSurface], theme[UiPaletteRole.Border], theme[UiPaletteRole.Foreground],
            "Neutral/Normal (a form control's value)");
        ExpectCell(theme, UiStatusTone.Neutral, UiEmphasis.Muted, theme[UiPaletteRole.ControlSurface], theme[UiPaletteRole.Border], theme[UiPaletteRole.SecondaryForeground],
            "Neutral/Muted (a badge's compact text)");
        ExpectCell(theme, UiStatusTone.Active, UiEmphasis.Normal, theme[UiPaletteRole.SelectedSurface], theme[UiPaletteRole.Accent], theme[UiPaletteRole.SelectedForeground],
            "Active/Normal");
        ExpectCell(theme, UiStatusTone.Active, UiEmphasis.Muted, theme[UiPaletteRole.SelectedSurface], theme[UiPaletteRole.Accent], theme[UiPaletteRole.SelectedForeground],
            "Active/Muted");
        ExpectCell(theme, UiStatusTone.Success, UiEmphasis.Normal, theme[UiPaletteRole.SuccessSurface], theme[UiPaletteRole.StrongBorder], theme[UiPaletteRole.SelectedForeground],
            "Success/Normal");
        ExpectCell(theme, UiStatusTone.Success, UiEmphasis.Muted, theme[UiPaletteRole.SuccessSurface], theme[UiPaletteRole.StrongBorder], theme[UiPaletteRole.SelectedForeground],
            "Success/Muted");
        ExpectCell(theme, UiStatusTone.Warning, UiEmphasis.Normal, theme[UiPaletteRole.DangerSurface], theme[UiPaletteRole.DangerSurface], theme[UiPaletteRole.DangerForeground],
            "Warning/Normal");
        ExpectCell(theme, UiStatusTone.Warning, UiEmphasis.Muted, theme[UiPaletteRole.DangerSurface], theme[UiPaletteRole.DangerSurface], theme[UiPaletteRole.DangerForeground],
            "Warning/Muted");
        ExpectCell(theme, UiStatusTone.Danger, UiEmphasis.Normal, theme[UiPaletteRole.DangerSurface], theme[UiPaletteRole.DangerSurface], theme[UiPaletteRole.DangerForeground],
            "Danger/Normal");
        ExpectCell(theme, UiStatusTone.Danger, UiEmphasis.Muted, theme[UiPaletteRole.DangerSurface], theme[UiPaletteRole.DangerSurface], theme[UiPaletteRole.DangerForeground],
            "Danger/Muted");
        ExpectCell(theme, UiStatusTone.Disabled, UiEmphasis.Normal, theme[UiPaletteRole.WindowSurface], theme[UiPaletteRole.Divider], theme[UiPaletteRole.DisabledForeground],
            "Disabled/Normal");
        ExpectCell(theme, UiStatusTone.Disabled, UiEmphasis.Muted, theme[UiPaletteRole.WindowSurface], theme[UiPaletteRole.Divider], theme[UiPaletteRole.DisabledForeground],
            "Disabled/Muted");

        Check(
            SameStyle(theme.Styles.Resolve(UiStatusTone.Active, UiEmphasis.Normal), theme.Styles.Resolve(UiStatusTone.Active, UiEmphasis.Muted)),
            "a saturated tone paints the same text under either emphasis (the axis carries one measured difference, not a scale)");

        UiTheme independent = TestPalettes.DarkGold;
        UiColour dangerBefore = independent[UiPaletteRole.DangerSurface];
        independent.Palette[UiPaletteRole.WarningSurface] = new UiColour(.11f,.22f,.33f);
        Check(SameColor(independent[UiPaletteRole.DangerSurface], dangerBefore), "warning declaration does not change danger");
        Check(!SameStyle(independent.Styles.Resolve(UiStatusTone.Warning), independent.Styles.Resolve(UiStatusTone.Danger)),
            "warning and danger resolve independent semantic treatments");
        UiTheme other = TestPalettes.DarkGold;
        Check(!ReferenceEquals(theme.Styles, other.Styles), "two themes never share one resolved-value store");
        Check(!typeof(UiTheme).GetProperty("Styles")!.CanWrite, "the store cannot be swapped out for another one");
        Check(!typeof(UiStyleTable).GetProperty("FallbackCount")!.CanWrite, "a store's own state is read-only to callers");

        // The store is a read-through view of its theme, which is what keeps a consumer's re-tint alive:
        // a copy taken at construction would silently freeze every token the old bag let them move.
        UiTheme retinted = TestPalettes.DarkGold;
        UiColour before = retinted.Styles.Resolve(UiStatusTone.Active).Fill;
        retinted[UiPaletteRole.SelectedSurface] = new UiColour(0.9f, 0.1f, 0.2f, 1f);
        Check(
            SameColor(retinted.Styles.Resolve(UiStatusTone.Active).Fill, retinted[UiPaletteRole.SelectedSurface]) && !SameColor(before, retinted[UiPaletteRole.SelectedSurface]),
            "a re-tint moves the table's answer instead of leaving a stale copy behind");
    }

    /// <summary>
    /// One token, every painting site: the fill and the edge are chosen so that a site still holding its
    /// own copy of the mapping paints a visibly different colour, which is the assertion that goes red if
    /// the duplication comes back. The neutral round repeats it for the shared-border default.
    /// </summary>
    private static void VerifyOutletsShareOneTable()
    {
        var fill = new UiColour(0.11f, 0.22f, 0.33f, 1f);
        var edge = new UiColour(0.44f, 0.55f, 0.66f, 1f);
        var decoyAccent = new UiColour(0.77f, 0.88f, 0.99f, 1f);

        UiTheme active = TestPalettes.DarkGold;
        active[UiPaletteRole.SelectedSurface] = fill;
        active[UiPaletteRole.SelectedBorder] = edge;
        active[UiPaletteRole.Accent] = decoyAccent;

        DrawStatusTreatment(active);
        CheckPaint(0, fill, edge, "StatusTreatment(Active)");

        DrawStatusBadge(active);
        CheckPaint(0, fill, edge, "StatusBadge(Active)");

        DrawDropdown(active, "B");
        CheckPaint(0, fill, edge, "DropdownWidget field (selected)");

        DrawModeRow(active, "A");
        CheckPaint(0, fill, edge, "InputModeRowWidget option (selected)");

        DrawPopupRow(active, "B");
        CheckPaint(5, fill, edge, "UiPopup option row (selected)");

        var raised = new UiColour(0.12f, 0.13f, 0.14f, 1f);
        var raisedEdge = new UiColour(0.21f, 0.22f, 0.23f, 1f);
        var decoyShared = new UiColour(0.31f, 0.32f, 0.33f, 1f);

        UiTheme neutral = TestPalettes.DarkGold;
        neutral[UiPaletteRole.ControlSurface] = raised;
        neutral[UiPaletteRole.ControlBorder] = raisedEdge;
        neutral[UiPaletteRole.Border] = decoyShared;

        DrawStatusTreatment(neutral, UiStatusTone.Neutral);
        CheckPaint(0, raised, raisedEdge, "StatusTreatment(Neutral)");

        DrawDropdown(neutral, "");
        CheckPaint(0, raised, raisedEdge, "DropdownWidget field (unselected)");

        DrawModeRow(neutral, "");
        CheckPaint(0, raised, raisedEdge, "InputModeRowWidget option (unselected)");

        DrawPopupRow(neutral, "");
        CheckPaint(5, raised, raisedEdge, "UiPopup option row (unselected)");
    }

    private static void VerifyUnknownKeysFallBack()
    {
        UiTheme theme = TestPalettes.DarkGold;
        UiStyleTable table = theme.Styles;

        Check(table.FallbackCount == 0, "a fresh store has recorded no fallback");
        Check(table.LastFallbackDiagnostic == null, "a fresh store has no diagnostic to report");

        UiResolvedStyle fallback = table.Resolve((UiStatusTone)77);
        Check(SameStyle(fallback, table.Resolve(UiStatusTone.Neutral)),
            "an undeclared tone falls back to the neutral treatment instead of throwing");
        Check(table.FallbackCount == 1, "the tone fallback was recorded");
        Check(
            table.LastFallbackDiagnostic != null
                && table.LastFallbackDiagnostic.IndexOf("77", StringComparison.Ordinal) >= 0,
            "the diagnostic names the undeclared value (got: " + (table.LastFallbackDiagnostic ?? "(null)") + ")");

        UiResolvedStyle mutedFallback = table.Resolve(UiStatusTone.Neutral, (UiEmphasis)9);
        Check(SameColor(mutedFallback.Text, theme[UiPaletteRole.Foreground]),
            "an undeclared emphasis falls back to Normal");
        Check(table.FallbackCount == 2, "the emphasis fallback was recorded too");
    }

    private static void VerifyWritabilityBeatsAuthor()
    {
        UiTheme theme = TestPalettes.DarkGold;
        UiStyleTable table = theme.Styles;

        UiResolvedStyle writable = table.Resolve(UiStatusTone.Danger, UiEmphasis.Normal, writable: true);
        Check(SameColor(writable.Fill, theme[UiPaletteRole.DangerSurface]), "a writable element keeps its authored tone");

        UiResolvedStyle readOnly = table.Resolve(UiStatusTone.Danger, UiEmphasis.Normal, writable: false);
        Check(
            SameColor(readOnly.Fill, theme[UiPaletteRole.WindowSurface])
                && SameColor(readOnly.Border, theme[UiPaletteRole.Divider])
                && SameColor(readOnly.Text, theme[UiPaletteRole.DisabledForeground]),
            "a read-only element is painted disabled whatever tone it was authored with (state beats author)");

        UiResolvedStyle unknown = table.Resolve(UiStatusTone.Active, UiEmphasis.Normal, writable: null);
        Check(SameColor(unknown.Fill, theme[UiPaletteRole.SelectedSurface]), "an unknown writability resolves by tone alone");

        Check(table.FallbackCount == 0, "the writability rule is a rule, not a fallback: nothing was recorded");
    }

    private static void VerifySurfacePairs()
    {
        var shared = new UiColour(0.31f, 0.32f, 0.33f, 1f);
        var own = new UiColour(0.41f, 0.42f, 0.43f, 1f);

        UiTheme sharedEdge = TestPalettes.DarkGold;
        sharedEdge[UiPaletteRole.PanelBorder] = shared;
        ClearRecordedBoxes();
        UiThemeDraw.Panel(NativeFixture.Paint,new UiLayoutRect(0f, 0f, 40f, 40f), sharedEdge);
        CheckPaint(0, sharedEdge[UiPaletteRole.PanelSurface], shared, "Panel() still uses the shared border when no surface overrides it");

        UiTheme perSurface = TestPalettes.DarkGold;
        perSurface[UiPaletteRole.Border] = shared;
        perSurface[UiPaletteRole.PanelBorder] = own;
        ClearRecordedBoxes();
        UiThemeDraw.Panel(NativeFixture.Paint,new UiLayoutRect(0f, 0f, 40f, 40f), perSurface);
        CheckPaint(0, perSurface[UiPaletteRole.PanelSurface], own, "Panel() uses its own edge override instead of the shared border");

        var pairFill = new UiColour(0.51f, 0.52f, 0.53f, 1f);
        var pairEdge = new UiColour(0.61f, 0.62f, 0.63f, 1f);
        UiTheme assigned = TestPalettes.DarkGold;
        assigned[UiPaletteRole.SelectedSurface] = pairFill;
        assigned[UiPaletteRole.SelectedBorder] = pairEdge;

        UiResolvedStyle active = assigned.Styles.Resolve(UiStatusTone.Active);
        Check(
            SameColor(active.Fill, pairFill) && SameColor(active.Border, pairEdge),
            "assigning a whole surface pair moves the table's Active cell with it");

        ClearRecordedBoxes();
        UiThemeDraw.StatusTreatment(NativeFixture.Paint,new UiLayoutRect(0f, 0f, 40f, 20f), assigned, UiStatusTone.Active);
        CheckPaint(0, pairFill, pairEdge, "StatusTreatment paints the assigned pair");
    }

    // --- C (0.7): one shipped palette, a PARTIAL bag that falls back to it, and the paint-side clock.
    // Vanilla's substrate/edge/option references come from the earlier vanilla Verse.Widgets
    // investigation (service build identity NOT established); remaining Vanilla roles are DERIVED.
    // The warm-gold look this lane pins moved into TestPalettes: it is a harness fixture now, because the
    // library ships exactly one look and a lane about a SECOND palette is the only thing that needs one.

    private static void VerifyDarkGoldPalette()
    {
        UiTheme t = TestPalettes.DarkGold;
        Check(SameColor(new UiColour(0.065f, 0.065f, 0.063f, 1f), t[UiPaletteRole.WindowSurface]), "DarkGold Base");
        Check(SameColor(new UiColour(0.095f, 0.095f, 0.090f, 1f), t[UiPaletteRole.PanelSurface]), "DarkGold Panel");
        Check(SameColor(new UiColour(0.135f, 0.135f, 0.128f, 1f), t[UiPaletteRole.ControlSurface]), "DarkGold Raised");
        Check(SameColor(new UiColour(0.17f, 0.17f, 0.16f, 1f), t[UiPaletteRole.HoverSurface]), "DarkGold Hover");
        Check(SameColor(new UiColour(0.17f, 0.14f, 0.08f, 1f), t[UiPaletteRole.SelectedSurface]), "DarkGold Selected");
        Check(SameColor(new UiColour(0.11f, 0.24f, 0.15f, 1f), t[UiPaletteRole.SuccessSurface]), "DarkGold Success");
        Check(SameColor(new UiColour(0.38f, 0.14f, 0.11f, 1f), t[UiPaletteRole.DangerSurface]), "DarkGold Danger");
        Check(SameColor(new UiColour(0.045f, 0.045f, 0.044f, 1f), t[UiPaletteRole.WorkspaceSurface]), "DarkGold WorkspacePlane");
        Check(SameColor(new UiColour(0.085f, 0.085f, 0.080f, 1f), t[UiPaletteRole.SectionSurface]), "DarkGold SectionBand");
        Check(SameColor(new UiColour(0.88f, 0.88f, 0.84f, 1f), t[UiPaletteRole.Foreground]), "DarkGold TextPrimary");
        Check(SameColor(new UiColour(0.58f, 0.58f, 0.55f, 1f), t[UiPaletteRole.SecondaryForeground]), "DarkGold TextSecondary");
        Check(SameColor(new UiColour(1f, 0.84f, 0.55f, 1f), t[UiPaletteRole.SelectedForeground]), "DarkGold TextOnGold");
        Check(SameColor(new UiColour(1f, 0.65f, 0.46f, 1f), t[UiPaletteRole.DangerForeground]), "DarkGold TextOnDanger");
        Check(SameColor(new UiColour(0.42f, 0.42f, 0.40f, 1f), t[UiPaletteRole.DisabledForeground]), "DarkGold TextDisabled");
        Check(SameColor(new UiColour(0.82f, 0.60f, 0.22f, 1f), t[UiPaletteRole.Accent]), "DarkGold AccentGold");
        // Batch 1 (CP-6④): one stored accent. The hover step is computed from it - 30% of the remaining
        // distance toward white on every channel, alpha kept - so there is no second token to assert.
        Check(SameColor(new UiColour(0.874f, 0.72f, 0.454f, 1f), t.AccentHover),
            "DarkGold's hover step is the accent lifted toward white, not a second stored colour");
        Check(SameColor(new UiColour(0.21f, 0.21f, 0.20f, 1f), t[UiPaletteRole.Border]), "DarkGold Border");
        Check(SameColor(new UiColour(0.32f, 0.32f, 0.30f, 1f), t[UiPaletteRole.StrongBorder]), "DarkGold BorderStrong");
        Check(SameColor(new UiColour(0.14f, 0.14f, 0.13f, 1f), t[UiPaletteRole.Divider]), "DarkGold Divider");
        Check(SameColor(t[UiPaletteRole.ControlBorder], t[UiPaletteRole.Border]), "fixture declares matching control/shared borders explicitly");
        Check(t.DefaultFont == UiFont.Small && t.Geometry == UiGeometry.Default,
            "DarkGold keeps the shipped font and density");

        UiTheme again = TestPalettes.DarkGold;
        Check(!ReferenceEquals(t, again), "DarkGold hands out a fresh instance, never one shared object");
        t[UiPaletteRole.WindowSurface] = new UiColour(0.5f, 0.5f, 0.5f, 1f);
        Check(!SameColor(t[UiPaletteRole.WindowSurface], again[UiPaletteRole.WindowSurface]), "and re-tinting one DarkGold instance cannot repaint the next");

        UiTheme vanilla = UiTheme.Vanilla;
        Check(!SameColor(again[UiPaletteRole.WindowSurface], vanilla[UiPaletteRole.WindowSurface]) && !SameColor(again[UiPaletteRole.Accent], vanilla[UiPaletteRole.Accent]),
            "DarkGold and Vanilla are different looks, not two names for one bag");
    }

    private static void VerifyVanillaPaletteAndBag()
    {
        UiTheme d = UiTheme.Vanilla;
        Check(SameColor(new UiColour(21f / 255f, 25f / 255f, 29f / 255f, 1f), d[UiPaletteRole.WindowSurface]),
            "Vanilla Base = the vanilla window substrate RGB(21,25,29)");
        Check(SameColor(new UiColour(42f / 255f, 43f / 255f, 44f / 255f, 1f), d[UiPaletteRole.PanelSurface]),
            "Vanilla Panel = the vanilla section substrate RGB(42,43,44)");
        Check(SameColor(new UiColour(97f / 255f, 108f / 255f, 122f / 255f, 1f), d[UiPaletteRole.WindowBorder]),
            "Vanilla BaseBorder = the vanilla window edge RGB(97,108,122)");
        Check(SameColor(new UiColour(135f / 255f, 135f / 255f, 135f / 255f, 1f), d[UiPaletteRole.PanelBorder]),
            "Vanilla PanelBorder = the vanilla section edge RGB(135,135,135)");
        Check(SameColor(new UiColour(0.21f, 0.21f, 0.21f, 1f), d[UiPaletteRole.ControlSurface]),
            "Vanilla Raised = the vanilla unselected option reference RGB(0.21,0.21,0.21)");
        Check(SameColor(new UiColour(0.32f, 0.28f, 0.21f, 1f), d[UiPaletteRole.SelectedSurface]),
            "Vanilla Selected = the vanilla selected option reference RGB(0.32,0.28,0.21)");

        Check(SameColor(new UiColour(0.27f, 0.27f, 0.27f, 1f), d[UiPaletteRole.HoverSurface]), "Vanilla Hover is derived one step above Raised");
        Check(SameColor(new UiColour(0.13f, 0.30f, 0.18f, 1f), d[UiPaletteRole.SuccessSurface]), "Vanilla Success is derived legible on neutral");
        Check(SameColor(new UiColour(0.42f, 0.15f, 0.12f, 1f), d[UiPaletteRole.DangerSurface]), "Vanilla Danger is derived legible on neutral");
        Check(SameColor(new UiColour(0.93f, 0.77f, 0.22f, 1f), d[UiPaletteRole.Accent]), "Vanilla accent is the stated yellow candidate");
        Check(SameColor(new UiColour(0.951f, 0.839f, 0.454f, 1f), d.AccentHover),
            "Vanilla's hover step is derived from that one accent, not stored beside it");
        Check(d[UiPaletteRole.Accent].Red == d.AccentWith(0.5f).Red && d[UiPaletteRole.Accent].Green == d.AccentWith(0.5f).Green,
            "AccentWith still derives from the accent hue");

        Check(d[UiPaletteRole.WindowSurface].Blue > d[UiPaletteRole.WindowSurface].Red && d[UiPaletteRole.PanelSurface].Green >= d[UiPaletteRole.PanelSurface].Red, "Vanilla substrate planes stay neutral/cool, not gold");
        Check(!SameColor(d[UiPaletteRole.PanelBorder], d[UiPaletteRole.Accent]) && !SameColor(d[UiPaletteRole.Border], d[UiPaletteRole.Accent]),
            "Vanilla shared edges are not painted in the accent");

        Check(d.DefaultFont == UiFont.Small && d.Geometry == UiGeometry.Default,
            "Vanilla keeps the shipped font and density");

        UiTheme a = UiTheme.Vanilla;
        UiTheme b = UiTheme.Vanilla;
        Check(!ReferenceEquals(a, b), "two Vanilla factories are two objects");
        a[UiPaletteRole.Foreground] = new UiColour(0.1f, 0.2f, 0.3f, 1f);
        Check(!SameColor(a[UiPaletteRole.Foreground], b[UiPaletteRole.Foreground]), "and re-tinting one cannot repaint the other");

        UiTheme original = UiTheme.Vanilla;
        UiTheme copy = original.Clone();
        Check(!ReferenceEquals(original, copy), "Clone returns a fresh bag");
        Check(SameColor(original[UiPaletteRole.WindowSurface], copy[UiPaletteRole.WindowSurface]) && SameColor(original[UiPaletteRole.PanelSurface], copy[UiPaletteRole.PanelSurface])
            && SameColor(original[UiPaletteRole.Accent], copy[UiPaletteRole.Accent]) && SameColor(original[UiPaletteRole.Foreground], copy[UiPaletteRole.Foreground])
            && SameColor(original[UiPaletteRole.Border], copy[UiPaletteRole.Border])
            && SameColor(original[UiPaletteRole.WindowBorder], copy[UiPaletteRole.WindowBorder])
            && copy[UiPaletteRole.SelectedBorder] == original[UiPaletteRole.SelectedBorder] && original[UiPaletteRole.SelectedBorder] == original[UiPaletteRole.SelectedBorder],
            "Clone copies every token, the claimed per-surface edges included");
        copy[UiPaletteRole.Accent] = new UiColour(0.05f, 0.9f, 0.05f, 1f);
        Check(!SameColor(original[UiPaletteRole.Accent], copy[UiPaletteRole.Accent]),
            "and moving a token on the clone leaves the original untouched");

        UiTheme bag = new();
        Check(SameColor(bag[UiPaletteRole.WindowSurface], d[UiPaletteRole.WindowSurface]) && SameColor(bag[UiPaletteRole.PanelSurface], d[UiPaletteRole.PanelSurface]) && SameColor(bag[UiPaletteRole.Border], d[UiPaletteRole.Border])
            && SameColor(bag[UiPaletteRole.Accent], d[UiPaletteRole.Accent]),
            "new UiTheme() answers the Vanilla palette for every token it never assigned");
        Check(SameColor(bag[UiPaletteRole.WindowBorder], d[UiPaletteRole.WindowBorder])
            && SameColor(bag[UiPaletteRole.PanelBorder], d[UiPaletteRole.PanelBorder]),
            "including the per-surface edges Vanilla claims, so a partial bag is a complete look");
        Check(!ReferenceEquals(bag.Styles, d.Styles),
            "while the bag still owns its resolved-value store: two themes never share one");
        bag[UiPaletteRole.HoverSurface] = new UiColour(0.5f, 0.1f, 0.1f, 1f);
        Check(SameColor(bag[UiPaletteRole.HoverSurface], new UiColour(0.5f, 0.1f, 0.1f, 1f)) && SameColor(bag[UiPaletteRole.WindowSurface], d[UiPaletteRole.WindowSurface]),
            "an override on a partial bag moves that token and leaves the inherited ones alone");

        UiTheme vanillaCustom = UiTheme.Vanilla;
        vanillaCustom[UiPaletteRole.HoverSurface] = new UiColour(0.5f, 0.1f, 0.1f, 1f);
        Check(SameColor(vanillaCustom[UiPaletteRole.WindowSurface], d[UiPaletteRole.WindowSurface]), "Vanilla + overrides keeps the Vanilla substrate");
        UiTheme darkCustom = TestPalettes.DarkGold;
        darkCustom[UiPaletteRole.HoverSurface] = new UiColour(0.5f, 0.1f, 0.1f, 1f);
        Check(SameColor(darkCustom[UiPaletteRole.WindowSurface], new UiColour(0.065f, 0.065f, 0.063f, 1f)),
            "DarkGold + overrides keeps the DarkGold substrate — a peer start, not a fallback");

        UiTheme windowOne = UiTheme.Vanilla;
        UiTheme windowTwo = TestPalettes.DarkGold;
        Check(!ReferenceEquals(windowOne.Styles, windowTwo.Styles), "the two themes hold separate stores");
        ClearRecordedBoxes();
        UiThemeDraw.Panel(NativeFixture.Paint,new UiLayoutRect(0f, 0f, 40f, 40f), windowOne);
        CheckPaint(0, windowOne[UiPaletteRole.PanelSurface], windowOne.PanelSurface.Border, "one window paints the Vanilla plane");
        ClearRecordedBoxes();
        UiThemeDraw.Panel(NativeFixture.Paint,new UiLayoutRect(0f, 0f, 40f, 40f), windowTwo);
        CheckPaint(0, windowTwo[UiPaletteRole.PanelSurface], windowTwo[UiPaletteRole.Border], "the other paints the DarkGold plane");

        UiResolvedStyle active = windowOne.Styles.Resolve(UiStatusTone.Active);
        Check(SameColor(active.Border, windowOne[UiPaletteRole.Accent]) && SameColor(active.Fill, windowOne[UiPaletteRole.SelectedSurface]),
            "Vanilla Active resolves to the selected plane outlined in the accent, not a filled-yellow plane");
    }

    private static void VerifyDensityTokens()
    {
        UiGeometry geometry = UiGeometry.Default;
        Check(geometry == UiGeometry.Default, "the density bundle supports value equality");
        CheckClose(6f, geometry.Padding, "default Padding is the text inset both widgets used to spell by hand");
        CheckClose(4f, geometry.Spacing, "default Spacing is the composite's own inner margin");
        CheckClose(6f, geometry.Gap, "default Gap is the cell distance");
        CheckClose(28f, geometry.RowHeight, "default RowHeight is the control height widgets used to hard-code");
        CheckClose(1f, geometry.Hairline, "default Hairline is the rule width");

        Check(new UiGeometry(-6f, -4f, -6f, -28f, -1f) == new UiGeometry(0f, 0f, 0f, 0f, 0f),
            "a negative density clamps to zero instead of throwing (appearance fails soft)");

        UiTheme theme = TestPalettes.DarkGold;
        theme.Geometry = new UiGeometry(6f, 4f, 6f, 28f, 3f);
        ClearRecordedBoxes();
        UiThemeDraw.Panel(NativeFixture.Paint,new UiLayoutRect(0f, 0f, 40f, 40f), theme);
        IList rects = RecordedBoxRects();
        Check(rects.Count == 5, "a painted surface is one fill plus four edges");
        Check(rects.Count == 5 && Near((NativeFixture.Record<UiLayoutRect>(rects[1])).Height, 3f), "the hairline token sets the edge width");

        using UiSession session = new();
        var bindings = new UiBindings();
        string mode = "A";
        bindings.BindValue("mode", () => mode, value => mode = value);
        InputModeRowWidget widget = new();
        widget.Configure(new UiElementSpec("mode",InputModeRowWidget.Kind,choices:new[]{new UiChoice<string>("A","A","A"),new UiChoice<string>("B","B","B"),new UiChoice<string>("C","C","C"),new UiChoice<string>("D","D","D")}));
        UiTheme dense = TestPalettes.DarkGold;
        dense.Geometry = new UiGeometry(6f, 2f, 2f, 20f, 1f);
        float measured = widget.Measure(MakeContext(session, dense, bindings, 150f));
        CheckClose(90f, measured, "row height, gap and spacing all reach the mode-row's measure (4x20 + 3x2 + 2x2)");
    }

    private static void VerifyResolvedFontIsMeasured()
    {
        UiTheme theme = TestPalettes.DarkGold;
        Check(theme.Styles.Font == UiFont.Small, "the store resolves the theme's font");

        theme.DefaultFont = UiFont.Medium;
        Check(theme.Styles.Font == UiFont.Medium, "a font-size change moves the resolved font");

        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(new ModelMetrics(), reports.Add);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;
        try
        {
            UiThemeDraw.Label(NativeFixture.Paint,new UiLayoutRect(0f, 0f, 10f, 24f), "abcd", theme, null, null, UiTextAnchor.MiddleLeft, true);
            Check(reports.Count == 1, "the fitting audit saw the single-line label");
            Check(
                reports.Count == 1 && reports[0].Font == theme.Styles.Font,
                "the font the audit measured with is the font the store resolved");
        }
        finally
        {
            UiFitAudit.Detach();
        }
    }

    /// <summary>
    /// The text half of the same table, observed through the stub's Label recorder: a label carries the
    /// colour the outlet applied through GUI.color, so "which colour did this site write" is checkable.
    /// Every expectation is a colour that no hand-picked default coincides with, and the neutral pair
    /// separates the two meanings the second axis exists for: a badge's compact text is secondary, a
    /// field's value is primary.
    /// </summary>
    private static void VerifyTextSitesFollowTheTable()
    {
        var onGold = new UiColour(0.91f, 0.12f, 0.13f, 1f);
        var primary = new UiColour(0.21f, 0.22f, 0.23f, 1f);
        var secondary = new UiColour(0.31f, 0.32f, 0.33f, 1f);

        UiTheme theme = TestPalettes.DarkGold;
        theme[UiPaletteRole.SelectedForeground] = onGold;
        theme[UiPaletteRole.Foreground] = primary;
        theme[UiPaletteRole.SecondaryForeground] = secondary;

        DrawStatusBadge(theme, UiStatusTone.Active);
        CheckLastLabel(onGold, "StatusBadge(Active) writes the table's Active text colour");
        DrawStatusBadge(theme, UiStatusTone.Neutral);
        CheckLastLabel(secondary, "StatusBadge(Neutral) writes the Muted text colour (the measured coordinate)");
        DrawStatusBadge(theme, UiStatusTone.Danger);
        CheckLastLabel(theme[UiPaletteRole.DangerForeground], "StatusBadge(Danger) writes the alarm text colour");

        DrawDropdown(theme, "B");
        CheckLastLabel(onGold, "DropdownWidget field (selected) writes the Active text colour");
        DrawDropdown(theme, "");
        CheckLastLabel(primary, "DropdownWidget field (unselected) writes the Normal text colour");

        DrawModeRow(theme, "A");
        CheckLastLabel(onGold, "InputModeRowWidget option (selected) writes the Active text colour");
        DrawModeRow(theme, "");
        CheckLastLabel(primary, "InputModeRowWidget option (unselected) writes the Normal text colour");

        DrawPopupRow(theme, "B");
        CheckLastLabel(onGold, "UiPopup option row (selected) writes the Active text colour");
        DrawPopupRow(theme, "");
        CheckLastLabel(primary, "UiPopup option row (unselected) writes the Normal text colour");
    }

    private static void VerifyLayoutRevisionInvalidatesBands()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        UiTheme theme = TestPalettes.DarkGold;
        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"density-test\"><Widget Id=\"dd\" Kind=\"input/dropdown\"   ><Choice Value=\"A\" Text=\"A\" Help=\"A\"/></Widget></UiPage>");
        var bindings = new UiBindings();
        bindings.BindValue("dd", () => "A", _ => { });
        using UiHost host = new("density-test", manifest, bindings, theme, new ModelMetrics(), new FixedTranslation());

        UiLayoutSnapshot first = host.MeasureAndArrange(new UiLayoutPoint(240f, 200f));
        CheckClose(28f, first.RectById["dd"].Height, "the default density gives the control its default height");

        theme.Geometry = new UiGeometry(6f, 4f, 6f, 20f, 1f);
        UiLayoutSnapshot dense = host.MeasureAndArrange(new UiLayoutPoint(240f, 200f));
        Check(!ReferenceEquals(first, dense), "a density change re-arranges instead of reusing the old bands");
        CheckClose(20f, dense.RectById["dd"].Height, "the dense row height reaches the arranged rect");

        theme.DefaultFont = UiFont.Medium;
        UiLayoutSnapshot remeasured = host.MeasureAndArrange(new UiLayoutPoint(240f, 200f));
        Check(!ReferenceEquals(dense, remeasured), "a font-size change re-arranges too: Measure sees the resolved font");

        theme[UiPaletteRole.PanelSurface] = new UiColour(0.2f, 0.3f, 0.4f, 1f);
        UiLayoutSnapshot retinted = host.MeasureAndArrange(new UiLayoutPoint(240f, 200f));
        Check(ReferenceEquals(remeasured, retinted), "a re-tint reuses the bands: colour is not layout-bearing");
    }

    /// <summary>
    /// The two clocks a theme carries, and the reason they are two: a colour assignment has to be visible to
    /// whoever cached a clone of the bag, and it must not be visible to whoever cached an arranged rect. The
    /// assertion is not "a counter moved" but "one counter moved and the other did not".
    /// </summary>
    private static void VerifyColourRevisionIsThePaintClock()
    {
        UiTheme theme = UiTheme.Vanilla;
        int layout = theme.LayoutRevision;
        int colour = theme.ColourRevision;

        theme[UiPaletteRole.PanelSurface] = new UiColour(0.2f, 0.3f, 0.4f, 1f);
        Check(theme.ColourRevision == colour + 1,
            "assigning a colour moves the paint clock (was " + colour + ", now " + theme.ColourRevision + ")");
        Check(theme.LayoutRevision == layout,
            "and leaves the layout clock exactly where it was: no rect depends on a fill");

        theme[UiPaletteRole.StrongBorder] = new UiColour(0.9f, 0.9f, 0.2f, 1f);
        theme[UiPaletteRole.SelectedBorder] = new UiColour(0.1f, 0.1f, 0.1f, 1f);
        Check(theme.ColourRevision == colour + 3, "a shared edge and a per-surface edge each move it too");
        Check(theme.LayoutRevision == layout, "while the layout clock is still untouched");

        int settled = theme.ColourRevision;
        theme[UiPaletteRole.PanelSurface] = new UiColour(0.2f, 0.3f, 0.4f, 1f);
        Check(theme.ColourRevision == settled,
            "and re-assigning the value a token already holds is not a change: nothing is invalidated");
        Check(theme.LayoutRevision == layout, "with the layout clock still untouched");

        theme.Geometry = new UiGeometry(5f, 4f, 6f, 28f, 1f);
        Check(theme.LayoutRevision == layout + 1 && theme.ColourRevision == settled,
            "the two clocks are disjoint: a density move is layout-only, exactly as a colour is paint-only");
    }

    /// <summary>
    /// The stale-colour defect and its fix, on the seam that had it: a region theme is a CLONE, so a palette
    /// change on the injected theme used to leave the region painting the colours it was built with.
    /// <para>
    /// Four things this lane has to get right to be evidence at all. (i) The scope is <b>defined and
    /// selected</b>: the document declares the scheme the page names, and the element is drawn with the chain
    /// that scope resolves, so the region really is a cached clone rather than a missing scheme. (ii) The
    /// colour re-tinted is the token the drawn surface actually READS - this dropdown has a value and an
    /// open popup, so its fill is the selected plane. (iii) The observation is
    /// <b>actual paint</b>: the colour is read from the stub's recorded solids after a real <c>DrawFrame</c>,
    /// which is the half a cached property read cannot prove. (iv) Identity and live interaction state are
    /// checked across a real redraw - same node object, same arranged rect, same scroll position, and a
    /// session popup record the repaint must not disturb.
    /// </para>
    /// </summary>
    private static void VerifyScopedPaletteRefresh()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"retint-test\"><Styles Schema=\"1\"><Scheme Name=\"tint\"/></Styles><Column Id=\"c\" Scheme=\"tint\" Padding=\"0\" Gap=\"0\"><Scroll Id=\"scroll\" Height=\"120\" Padding=\"0\" Gap=\"0\"><Widget Kind=\"text/wrapped\" Text=\"\" Height=\"40\" /><Widget Id=\"dd\" Kind=\"input/dropdown\"     Width=\"120\" ><Choice Value=\"A\" Text=\"A\" Help=\"A\"/><Choice Value=\"B\" Text=\"B\" Help=\"B\"/></Widget><Widget Kind=\"text/wrapped\" Text=\"\" Height=\"200\" /></Scroll></Column></UiPage>";
        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        string selected = "B";
        bindings.BindValue<string>("dd", () => selected, value => selected = value);

        UiTheme baseline = UiTheme.Vanilla;
        using UiHost host = new("retint-test", manifest, bindings, baseline, new ModelMetrics(), new FixedTranslation());
        var repaint = new UiColour(0.77f, 0.11f, 0.22f, 1f);

        UiLayoutSnapshot before = host.MeasureAndArrange(new UiLayoutPoint(240f, 200f));
        UiLayoutRect beforeRect = before.RectById["dd"];
        UiTheme scope = ScopeThemeOf(host, "tint");
        Check(SameColor(scope[UiPaletteRole.SelectedSurface], baseline[UiPaletteRole.SelectedSurface]),
            "the selected scope starts from the injected palette (its own scheme declares no colours)");
        Check(host.StyleResolver.ScopeCount >= 1 && !ReferenceEquals(scope, baseline),
            "and it is a real cached clone of its own, not the injected theme handed back");

        UiNode? beforeNode = host.Session.GetNodeByElementId("dd");
        Check(beforeNode != null, "the dropdown arranged into a node");
        UiNode node = beforeNode!;
        UiNode scrollNode = host.Session.GetNodeByElementId("scroll")
            ?? throw new Exception("the scroll container has no node");
        var scrolled = new UiLayoutPoint(0f, 24f);
        Event? previousEvent = Event.current;
        ResetPointerSeams();
        try
        {
        Event.current = null;
        NativeEvents.Click(beforeRect.Center,()=>NativeFixture.DrawFrame(host,new UiLayoutRect(0,0,240,200)));
        Check(host.Session.IsPopupOpen("dd"),"the actual press/release trigger opened its popup");        host.Session.SetScrollPosition(scrollNode, scrolled);

        ClearRecordedBoxes();
        {NativeEvents.Begin(EventType.Repaint,new UiLayoutPoint(-10,-10));NativeFixture.DrawFrame(        host,new UiLayoutRect(0f, 0f, 240f, 200f));}
        IList beforeColors = RecordedBoxColors();
        Check(beforeColors.Count >= 5, "the field painted its surface before the re-tint: " + beforeColors.Count);
        Check(beforeColors.Count >= 5 && SameRect(NativeFixture.Record<UiLayoutRect>(RecordedBoxRects()[0]), beforeRect)
                && SameColor(NativeFixture.Record<UiColour>(beforeColors[0]), baseline[UiPaletteRole.SelectedSurface]),
            "with the SELECTED plane the valued, open field actually reads at its drawn rect");
        UiLayoutRect? popupAnchor = host.Session.OpenPopupAnchor;
        Check(host.Session.IsPopupOpen("dd") && popupAnchor.HasValue,
            "the drawn, scrolled owner keeps its popup registered");

        baseline[UiPaletteRole.SelectedSurface] = repaint;
        Check(SameColor(host.StyleResolver.ThemeFor(ScopeChain("tint"))[UiPaletteRole.SelectedSurface], repaint),
            "after a re-tint the scope resolves the new colour: the cached clone was dropped, not kept stale");

        ClearRecordedBoxes();
        {NativeEvents.Begin(EventType.Repaint,new UiLayoutPoint(-10,-10));NativeFixture.DrawFrame(        host,new UiLayoutRect(0f, 0f, 240f, 200f));}
        IList afterColors = RecordedBoxColors();
        Check(afterColors.Count >= 5 && SameRect(NativeFixture.Record<UiLayoutRect>(RecordedBoxRects()[0]), beforeRect)
                && SameColor(NativeFixture.Record<UiColour>(afterColors[0]), repaint),
            "and the NEXT PAINT of the same element shows it: the refresh reaches the draw, not only the lookup");

        // The layout is untouched: same rect, same arranged snapshot, same node identity.
        UiLayoutSnapshot after = host.MeasureAndArrange(new UiLayoutPoint(240f, 200f));
        Check(SameRect(beforeRect, after.RectById["dd"]),
            "no rect moved: a colour is not layout-bearing, so the arranged page is reused");
        Check(ReferenceEquals(before, after),
            "and the snapshot is literally the same one, which is what 'reused' has to mean");
        Check(ReferenceEquals(node, host.Session.GetNodeByElementId("dd")) && node.Kind == "input/dropdown",
            "the SAME node object still carries the element: a repaint is not a rebuild");
        UiLayoutPoint afterScroll = host.Session.GetScrollPosition(scrollNode);
        Check(Math.Abs(afterScroll.X - scrolled.X) < 0.001f && Math.Abs(afterScroll.Y - scrolled.Y) < 0.001f,
            "and the real scroll container's nonzero position survives the repaint");
        Check(selected == "B", "the non-default typed selection survives both draws without a write");
        Check(host.Session.IsPopupOpen("dd") && host.Session.OpenPopupAnchor.HasValue
                && popupAnchor.HasValue && SameRect(host.Session.OpenPopupAnchor.Value, popupAnchor.Value),
            "as does the live owner's popup and its scrolled anchor");
        }
        finally
        {
            host.Session.ClosePopup();
            ResetPointerSeams();
            Event.current = previousEvent;
        }
    }

    // Native keyboard/IME selection is outside the stub. This observes the actual widget's focus and
    // draft slot through DrawFrame, separately from the popup which legitimately blocks text input.
    private static void VerifyRetintPreservesFocusedDraft()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        const string xml = "<UiPage Schema=\"2\" Source=\"retint-edit\">"
            + "<Styles Schema=\"1\"><Scheme Name=\"tint\"/></Styles>"
            + "<Widget Id=\"edit\" Kind=\"input/text-field\" Scheme=\"tint\" Live=\"false\" />"
            + "</UiPage>";
        string model = "committed";
        var bindings = new UiBindings();
        bindings.BindValue<string>("edit", () => model, value => model = value);
        UiTheme theme = UiTheme.Vanilla;
        using UiHost host = new("retint-edit", UiLayoutManifest.Parse(xml), bindings, theme,
            new ModelMetrics(), new FixedTranslation());
        UiLayoutSnapshot before = host.MeasureAndArrange(new UiLayoutPoint(240f, 200f));
        UiNode node = host.Session.GetNodeByElementId("edit")!;
        UiLayoutRect field = before.RectById["edit"];
        Event? previousEvent = Event.current;
        ResetPointerSeams();
        try
        {
            Event.current = null;
            NativeFixture.DebugMousePositionEnabled = true;
            NativeFixture.DebugMousePosition = new UiLayoutPoint(field.X + field.Width / 2f, field.Y + field.Height / 2f);
            NativeFixture.DebugMouseDown = true;
            NativeFixture.TextFieldOverride = (_, _) => "draft";
            {NativeFixture.BeginDebugEvent();NativeFixture.DrawFrame(            host,new UiLayoutRect(0f, 0f, 240f, 200f));}
            UiControlState state = host.Session.GetControlState(node.Id, "edit");
            Check(state.IsFocused && state.Draft == "draft" && model == "committed",
                "the actual text widget owns a focused, uncommitted draft before the retint");
            NativeFixture.DebugMouseDown = false;
            NativeFixture.TextFieldOverride = (_, text) => text;
            NativeFixture.Capture(host.Session,NativeFixture.Key(731));
            theme[UiPaletteRole.ControlSurface] = new UiColour(0.77f, 0.11f, 0.22f, 1f);
            {NativeFixture.BeginDebugEvent();NativeFixture.DrawFrame(            host,new UiLayoutRect(0f, 0f, 240f, 200f));}
            Check(ReferenceEquals(state, host.Session.GetControlState(node.Id, "edit"))
                    && state.IsFocused && state.Draft == "draft" && model == "committed",
                "the same focused draft survives actual recolour drawing without committing");
            Check(ReferenceEquals(node, host.Session.GetNodeByElementId("edit"))
                    && ReferenceEquals(before, host.MeasureAndArrange(new UiLayoutPoint(240f, 200f)))
                    && host.Session.OwnedHotControl == NativeFixture.Key(731),
                "the text node, arranged snapshot and nonzero session capture survive the repaint");
        }
        finally
        {
            host.Session.ReleaseHotControl(NativeFixture.Key(731));
            ResetPointerSeams();
            Event.current = previousEvent;
        }
    }

    /// <summary>A region theme for one scheme name, through the host's own resolver.</summary>
    private static UiTheme ScopeThemeOf(UiHost host, string scheme)
    {
        return host.StyleResolver.ThemeFor(ScopeChain(scheme));
    }

    private static IReadOnlyList<UiStyleDeclaration> ScopeChain(string scheme)
    {
        return new[] { new UiStyleDeclaration(scheme: scheme) };
    }

    /// <summary>
    /// The partial bag, first half: an unset COLOUR token answers Vanilla, an assigned one answers the
    /// assignment, and an explicit TRANSPARENT is a value the fallback must not replace. The derived
    /// properties (<see cref="UiTheme.BaseSurface"/> and friends) are checked here too, because they are what
    /// the rest of this rule is read through: a derived property reads its two tokens, so an inherited edge
    /// shows up as an inherited surface pair.
    /// </summary>
    private static void VerifyPartialBagFallsBackToVanilla()
    {
        UiTheme vanilla = UiTheme.Vanilla;
        UiTheme bag = new();

        Check(SameColor(bag[UiPaletteRole.PanelSurface], vanilla[UiPaletteRole.PanelSurface]) && SameColor(bag[UiPaletteRole.Accent], vanilla[UiPaletteRole.Accent])
            && SameColor(bag[UiPaletteRole.Foreground], vanilla[UiPaletteRole.Foreground]) && bag.Geometry == vanilla.Geometry
            && bag.DefaultFont == vanilla.DefaultFont,
            "a bag that assigns nothing is the shipped look, not an empty one");

        var mine = new UiColour(0.12f, 0.34f, 0.56f, 1f);
        bag[UiPaletteRole.ControlSurface] = mine;
        Check(SameColor(bag[UiPaletteRole.ControlSurface], mine) && SameColor(bag[UiPaletteRole.PanelSurface], vanilla[UiPaletteRole.PanelSurface]),
            "assigning one token moves one token and leaves the rest on the fallback");

        // The transparent claim, checked on the token the derived pair actually READS: BaseSurface is
        // (Base, BaseBorder ?? Border), so a transparent Base is what has to survive - asserting it through
        // Panel would compare two unrelated tokens.
        bag[UiPaletteRole.WindowSurface] = new UiColour(0f, 0f, 0f, 0f);
        Check(bag[UiPaletteRole.WindowSurface].Alpha == 0f && bag[UiPaletteRole.WindowSurface].Red == 0f && bag[UiPaletteRole.WindowSurface].Green == 0f && bag[UiPaletteRole.WindowSurface].Blue == 0f,
            "an explicitly transparent colour is a VALUE: the fallback must not repaint it");
        Check(!SameColor(bag[UiPaletteRole.WindowSurface], vanilla[UiPaletteRole.WindowSurface]),
            "and it is distinguishable from the Vanilla token it would otherwise have answered");

        UiTheme edges = new();
        Check(edges[UiPaletteRole.StrongBorder].Alpha > 0f, "an unset shared edge answers the Vanilla value");
        edges[UiPaletteRole.StrongBorder] = new UiColour(0f, 0f, 0f, 0f);
        Check(edges[UiPaletteRole.StrongBorder].Alpha == 0f, "and a transparent shared edge is a claimed transparent edge");

        // The derived read: a surface pair is its fill plus (its own edge, else the shared edge), and neither
        // half is a special case of the other. This is the same rule the other half of this lane reads on the
        // nullable tokens - stated once here through the pair the painting outlets actually use.
        Check(SameColor(edges.RaisedSurface.Fill, UiTheme.Vanilla[UiPaletteRole.ControlSurface])
                && SameColor(edges.RaisedSurface.Border, edges[UiPaletteRole.Border]),
            "RaisedSurface reads the inherited fill and the shared edge, with no second copy of either");
        Check(SameColor(bag.BaseSurface.Fill, bag[UiPaletteRole.WindowSurface]) && bag.BaseSurface.Fill.Alpha == 0f
                && SameColor(bag.BaseSurface.Border, bag[UiPaletteRole.WindowBorder]),
            "and the transparent fill stays transparent through the derived pair while its edge is inherited");
    }

    /// <summary>
    /// The second half of the same rule, on the nullable tokens: an unclaimed per-surface edge answers the
    /// Vanilla default - so a partial bag's window edge IS the vanilla window edge, not "no edge" - while an
    /// edge Vanilla itself leaves unclaimed stays unclaimed, because there <c>null</c> carries its documented
    /// meaning of "use the shared token". Claiming one answers the claim.
    /// </summary>
    private static void VerifyPartialBagEdgeFallback()
    {
        UiTheme vanilla = UiTheme.Vanilla;
        UiTheme partial = new();

        Check(!vanilla.Palette.IsDeclared(UiPaletteRole.ControlBorder),
            "Vanilla claims its window and section edges and leaves the raised one to the shared token");
        Check(SameColor(partial[UiPaletteRole.WindowBorder], vanilla[UiPaletteRole.WindowBorder]),
            "an unclaimed window edge on a partial bag falls back to Vanilla's, not to null");
        Check(!partial.Palette.IsDeclared(UiPaletteRole.ControlBorder),
            "and an edge Vanilla itself leaves unclaimed stays unclaimed, so the shared token still answers");

        var own = new UiColour(0.61f, 0.62f, 0.63f, 1f);
        partial[UiPaletteRole.WindowBorder] = own;
        Check(SameColor(partial[UiPaletteRole.WindowBorder], own),
            "claiming the edge answers the claim");
        Check(SameColor(partial.BaseSurface.Border, own),
            "and the surface pair reads the claim instead of the shared token");
    }

    /// <summary>
    /// Palette coverage, in full: every per-surface edge token is enumerated and each one is checked for
    /// <b>explicit claim</b> (an assigned value is the value read) and <b>fallback</b> (an unassigned one
    /// answers the template's value, whatever that is - the Vanilla default for the two edges Vanilla
    /// claims, and <c>null</c>, meaning "use the shared token", for the five it leaves open).
    /// <para>
    /// The reason this is a separate lane rather than one more assertion inside a neighbouring one: a sweep
    /// with a hand-written list is exactly how a token gets left out of coverage, and the omission is
    /// invisible because the lane still passes. This lane is the single place that list lives, it enumerates
    /// ALL SEVEN nullable per-surface edges explicitly, and it derives each token's property and the surface
    /// pair that token feeds by name, so adding an eighth edge means adding one line HERE and every half of
    /// the contract is then asserted for it. It is deliberately NOT auto-derived: enumerating
    /// <c>typeof(UiTheme)</c> for <c>Color?</c> properties would silently pick up any future nullable colour
    /// that is not an edge token, and a lane that asserts the wrong set is worse than one that asserts a
    /// shorter set loudly. Equality is never pressed into service as ownership: the fallback half asserts the
    /// template's own value, not merely "different from the claim".
    /// </para>
    /// </summary>
    private static void VerifyPerSurfaceEdgeCoverage()
    {
        var pairs = new[] {
            (UiPaletteRole.WindowSurface, UiPaletteRole.WindowBorder),
            (UiPaletteRole.PanelSurface, UiPaletteRole.PanelBorder),
            (UiPaletteRole.ControlSurface, UiPaletteRole.ControlBorder),
            (UiPaletteRole.HoverSurface, UiPaletteRole.HoverBorder),
            (UiPaletteRole.SelectedSurface, UiPaletteRole.SelectedBorder),
            (UiPaletteRole.SuccessSurface, UiPaletteRole.SuccessBorder),
            (UiPaletteRole.WarningSurface, UiPaletteRole.WarningBorder),
            (UiPaletteRole.DangerSurface, UiPaletteRole.DangerBorder)
        };
        foreach (var pair in pairs)
        {
            UiTheme theme = new();
            UiColour baseline = UiPalette.Default(pair.Item2);
            Check(theme.Palette[pair.Item2] == baseline && !theme.Palette.IsDeclared(pair.Item2), "untouched border uses readonly default: " + pair.Item2);
            theme.Palette[pair.Item2] = new UiColour(.31f,.42f,.53f);
            Check(SameColor(theme.Surface(pair.Item1,pair.Item2).Border, new UiColour(.31f,.42f,.53f,1)), "declared border reaches real surface: " + pair.Item2);
            theme.Palette.Remove(pair.Item2);
            Check(theme.Palette[pair.Item2] == baseline, "border removal reveals current default: " + pair.Item2);
        }
    }
    private static (string Surface, UiColour FallbackEdge) SurfaceFor(string borderToken)
    {
        UiTheme v = UiTheme.Vanilla;
        switch (borderToken)
        {
            case "BaseBorder": return ("BaseSurface", v[UiPaletteRole.Border]);
            case "PanelBorder": return ("PanelSurface", v[UiPaletteRole.Border]);
            case "RaisedBorder": return ("RaisedSurface", v[UiPaletteRole.Border]);
            case "HoverBorder": return ("HoverSurface", v[UiPaletteRole.StrongBorder]);
            case "SelectedBorder": return ("SelectedSurface", v[UiPaletteRole.Accent]);
            case "SuccessBorder": return ("SuccessSurface", v[UiPaletteRole.StrongBorder]);
            default: return ("DangerSurface", v[UiPaletteRole.DangerSurface]);
        }
    }

    /// <summary>
    /// The typography axis and its legacy spelling. Every fixture here SELECTS the scope it tests, because a
    /// document only applies a scheme that is selected - page-level through <c>ApplyTo</c>, or by a
    /// per-element scheme chain through the resolver - and a named scheme nobody selects proves nothing.
    /// <list type="number">
    /// <item>a selected PAGE-LEVEL scheme sets the font, and the theme it is applied to answers it;</item>
    /// <item>a selected per-element scheme does the same through the resolver, and carries its other tokens
    /// with it, so a redirect is not a whole-scheme bail-out;</item>
    /// <item>the selection is the one <b>paint</b> uses - the label outlet receives the selected font, read
    /// back from the fit audit's report, which is recorded from the font the outlet actually drew with;</item>
    /// <item>the legacy <c>&lt;Font Value="…"/&gt;</c> redirects to that SAME font axis with a reported,
    /// documented successor, and NOT to a distance: density stays exactly where it was;</item>
    /// <item>an unknown spelling is reported instead of being ignored.</item>
    /// </list>
    /// </summary>
    private static void VerifySchemeFontAndRetirement()
    {
        UiFitAudit.Reset();

        // (1) A page-level scheme, SELECTED through the same entry point a host uses.
        UiStyleDocument legacy = UiStyleDocument.Parse(
            "<Styles Schema=\"1\" Scheme=\"legacy\"><Scheme Name=\"legacy\">"
            + "<Palette Role=\"PanelSurface\" Value=\"#0000ffff\"/>"
            + "<Metric Token=\"Font\" Value=\"Medium\" />"
            + "</Scheme></Styles>");
        Check(legacy.Issues.Count==0 && UiFitAudit.StyleFallbackCount==0,"canonical Font metric applies without a redirect or fallback report");
        UiTheme legacyTheme = UiTheme.Vanilla;
        new UiStyleResolver(legacyTheme, legacy).ApplyTo(legacyTheme);
        Check(legacyTheme.DefaultFont == UiFont.Medium,
            "the page-level declaration selects the font (got " + legacyTheme.DefaultFont + ")");
        Check(legacyTheme.Geometry == UiGeometry.Default,
            "and the legacy font moved NO distance: density is a separate axis (got "
            + legacyTheme.Geometry.RowHeight + ")");
        Check(SameColor(legacyTheme[UiPaletteRole.PanelSurface], new UiColour(0f, 0f, 1f, 1f)),
            "the rest of the same scheme still applies, so the redirect is not a whole-scheme bail-out");
        Check(legacyTheme.LayoutRevision > 0,
            "and it moved the layout clock before the first Measure, because a typeface measures differently");

        // (1b) The same scheme selected per ELEMENT, through the resolver's own chain.
        UiTheme scopedBaseline = UiTheme.Vanilla;
        var legacyResolver = new UiStyleResolver(scopedBaseline, legacy);
        UiTheme legacyScope = legacyResolver.ThemeFor(ScopeChain("legacy"));
        Check(legacyResolver.Issues.Count == 0, "the selected scheme exists, so nothing is dropped at resolution");
        Check(legacyScope.DefaultFont == UiFont.Medium && legacyScope.Geometry == UiGeometry.Default,
            "a per-element selection reaches the same font axis and leaves density alone");
        Check(SameColor(legacyScope[UiPaletteRole.PanelSurface], new UiColour(0f, 0f, 1f, 1f)),
            "and it carries the scheme's other tokens, being a clone of the baseline");

        // (2) The selection drives PAINT: the fit audit records the font the outlet drew with.
        var metrics = new ModelMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;
        try
        {
            UiThemeDraw.Label(NativeFixture.Paint,new UiLayoutRect(0f, 0f, 4f, 24f), "Flag", legacyScope, null, null, UiTextAnchor.MiddleLeft, true);
            Check(reports.Count == 1,
                "the outlet reported exactly one overflow for a 4px band (got " + reports.Count + ")");
            Check(reports.Count == 1 && reports[0].Font == UiFont.Medium,
                "and the font it painted with is the one the scheme selected, not the theme's old one (got "
                + (reports.Count == 1 ? reports[0].Font.ToString() : "(none)") + ")");
        }
        finally
        {
            UiFitAudit.Detach();
        }

        // (3) The successor spelling, SELECTED at page level: the same axis, declared as the metric that IS a
        // font, with the distance axis moved independently beside it.
        UiStyleDocument successor = UiStyleDocument.Parse(
            "<Styles Schema=\"1\" Scheme=\"mod\"><Scheme Name=\"mod\">"
            + "<Metric Token=\"Font\" Value=\"Tiny\" />"
            + "<Metric Token=\"RowHeight\" Value=\"25\" />"
            + "</Scheme></Styles>");
        Check(successor.Issues.Count == 0,
            "the working successor records nothing: " + (successor.Issues.Count > 0 ? successor.Issues[0].Message : "(none)"));
        UiTheme metric = UiTheme.Vanilla;
        new UiStyleResolver(metric, successor).ApplyTo(metric);
        Check(metric.DefaultFont == UiFont.Tiny, "the selected Font metric sets the font on the typography axis");
        CheckClose(25f, metric.Geometry.RowHeight, "while RowHeight independently moves the distance axis");

        // (4) The legacy declaration is superseded by the metric, selected at page level, and said out loud.
        UiStyleDocument both = UiStyleDocument.Parse(
            "<Styles Schema=\"1\" Scheme=\"both\"><Scheme Name=\"both\">"
            + "<Font Value=\"Medium\" />"
            + "<Metric Token=\"Font\" Value=\"Tiny\" />"
            + "</Scheme></Styles>");
        Check(both.Issues.Count == 1,
            "declaring both is reported once rather than resolved silently (got " + both.Issues.Count + ")");
        UiTheme chosen = UiTheme.Vanilla;
        new UiStyleResolver(chosen, both).ApplyTo(chosen);
        Check(both.StructurallyInvalid && chosen.DefaultFont==UiTheme.Vanilla.DefaultFont,"a retired declaration refuses the document instead of silently redirecting or partly applying");

        // (5) An unknown spelling is refused and reported, never silently kept as the default.
        UiStyleDocument invalid = UiStyleDocument.Parse(
            "<Styles Schema=\"1\"><Scheme Name=\"bad\"><Metric Token=\"Font\" Value=\"Huge\" /></Scheme></Styles>");
        Check(invalid.Issues.Count == 1
            && invalid.Issues[0].Message.IndexOf("Huge", StringComparison.Ordinal) >= 0,
            "an unknown font spelling is reported with the value that was refused (got "
            + (invalid.Issues.Count > 0 ? invalid.Issues[0].Message : "(none)") + ")");
        UiStyleDocument invalidLegacy = UiStyleDocument.Parse(
            "<Styles Schema=\"1\"><Scheme Name=\"bad\"><Font Value=\"Huge\" /></Scheme></Styles>");
        Check(invalidLegacy.StructurallyInvalid && !invalidLegacy.TryGetScheme("bad", out _),
            "and a refused declaration never reaches the scheme, so the theme keeps the font it had");

        // A named density has no typography axis, so a Font metric written there is refused with a message
        // that says where it belongs - the one remaining way a typeface could be mistaken for a number.
        UiStyleDocument inDensity = UiStyleDocument.Parse(
            "<Styles Schema=\"1\"><Density Name=\"d\"><Metric Token=\"Font\" Value=\"Tiny\" /></Density></Styles>");
        Check(inDensity.Issues.Count == 1
            && inDensity.Issues[0].Message.IndexOf("Scheme", StringComparison.Ordinal) >= 0,
            "a Font metric inside a <Density> is refused and named as a scheme declaration (got "
            + (inDensity.Issues.Count > 0 ? inDensity.Issues[0].Message : "(none)") + ")");
        Check(inDensity.TryGetDensity("d", out UiStyleDocument.DensityDefinition emptyDensity)
            && emptyDensity.Metrics.Count == 0,
            "and it never becomes a numeric metric, so no distance can carry a font");
    }

    /// <summary>
    /// The same typography selection taken through the REAL host and element measure path - the half a direct
    /// <c>ITextMetrics</c> call cannot prove, because a metrics probe says only that the ruler can measure,
    /// never that the selected font reached it.
    /// <para>
    /// Two hosts are built from the SAME page text and the SAME metrics adapter, differing only in the
    /// selected root scheme's font, and an element with <c>Width="Auto"</c> is measured in each. The
    /// arrangement must differ; the DENSITY must not. The second half is what separates "a font moved the
    /// layout" - true and expected, since a glyph run is wider - from "a font was implemented as a spacing
    /// change", which is the defect this batch exists to remove.
    /// </para>
    /// </summary>
    private static void VerifySelectedFontReachesHostMeasure()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        const string page =
            "<UiPage Schema=\"2\" Source=\"font-measure\">"
            + "<Styles Schema=\"1\" Scheme=\"{0}\"><Scheme Name=\"small\"/>"
            + "<Scheme Name=\"medium\"><Metric Token=\"Font\" Value=\"Medium\" /></Scheme></Styles>"
            + "<Row Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"w\" Kind=\"input/button\" Text=\"Flag\" ActionBind=\"go\" Width=\"Auto\" />"
            + "<Widget Kind=\"text/wrapped\" Text=\"\" /></Row>"
            + "</UiPage>";

        float smallWidth, mediumWidth;
        UiGeometry smallGeometry, mediumGeometry;
        using (UiHost host = HostForFontMeasure(string.Format(page, "small"), out UiGeometry geometry))
        {
            smallGeometry = geometry;
            smallWidth = host.MeasureAndArrange(new UiLayoutPoint(240f, 200f)).RectById["w"].Width;
        }

        using (UiHost host = HostForFontMeasure(string.Format(page, "medium"), out UiGeometry geometry))
        {
            mediumGeometry = geometry;
            mediumWidth = host.MeasureAndArrange(new UiLayoutPoint(240f, 200f)).RectById["w"].Width;
        }

        Check(mediumWidth > smallWidth,
            "an Auto-width element measures WIDER under the selected Medium font than under Small, through the "
            + "real host: small=" + smallWidth.ToString("0.###") + " medium=" + mediumWidth.ToString("0.###"));
        Check(smallGeometry == mediumGeometry && smallGeometry == UiGeometry.Default,
            "while density is identical in both: the font moved the layout and the spacing did not move "
            + "(small=" + smallGeometry.RowHeight + ", medium=" + mediumGeometry.RowHeight + ")");
    }

    /// <summary>Builds the measure fixture's host and reports the geometry the selected scheme resolved to.</summary>
    private static UiHost HostForFontMeasure(string xml, out UiGeometry geometry)
    {
        var bindings = new UiBindings();
        bindings.BindCommand("go", () => { });
        UiTheme theme = UiTheme.Vanilla;
        var host = new UiHost(
            "font-measure", UiLayoutManifest.Parse(xml), bindings, theme, new ModelMetrics(), new FixedTranslation());
        geometry = theme.Geometry;
        return host;
    }

    private static void DrawStatusTreatment(UiTheme theme, UiStatusTone tone = UiStatusTone.Active)
    {
        ClearRecordedBoxes();
        UiThemeDraw.StatusTreatment(NativeFixture.Paint,new UiLayoutRect(0f, 0f, 80f, 20f), theme, tone);
    }

    private static void DrawStatusBadge(UiTheme theme, UiStatusTone tone = UiStatusTone.Active)
    {
        ClearRecordedBoxes();
        UiThemeDraw.StatusBadge(NativeFixture.Paint,new UiLayoutRect(0f, 0f, 80f, 20f), "badge", theme, tone);
    }

    private static void DrawDropdown(UiTheme theme, string currentValue)
    {
        using UiSession session = new();
        var bindings = new UiBindings();
        string current = currentValue;
        bindings.BindValue("dd", () => current, value => current = value);
        DropdownWidget widget = new();
        widget.Configure(new UiElementSpec("dd",DropdownWidget.Kind,choices:new[]{new UiChoice<string>("A","A","A"),new UiChoice<string>("B","B","B")}));

        ClearRecordedBoxes();
        NativeFixture.Draw(        widget,new UiLayoutRect(0f, 0f, 160f, 28f), MakeContext(session, theme, bindings, 160f));
    }

    private static void DrawModeRow(UiTheme theme, string currentValue)
    {
        using UiSession session = new();
        var bindings = new UiBindings();
        string current = currentValue;
        bindings.BindValue("mode", () => current, value => current = value);
        InputModeRowWidget widget = new();
        widget.Configure(new UiElementSpec("mode",InputModeRowWidget.Kind,choices:new[]{new UiChoice<string>("A","A","A")}));

        ClearRecordedBoxes();
        NativeFixture.Draw(        widget,new UiLayoutRect(0f, 0f, 150f, 36f), MakeContext(session, theme, bindings, 150f));
    }

    private static void DrawPopupRow(UiTheme theme, string currentValue)
    {
        using UiSession session = new();
        var bindings = new UiBindings();
        var options = new List<UiChoice<string>> { new("B", "B") };

        ClearRecordedBoxes();
        UiPopup.DrawChoices(
            new UiLayoutRect(0f, 0f, 120f, 24f),
            "dd",
            MakeContext(session, theme, bindings, 120f),
            options,
            currentValue,
            _ => { });
    }

    private static UiWidgetContext MakeContext(UiSession session, UiTheme theme, IUiBindingReader bindings, float width)
    {
        return NativeFixture.Context("style-test", session, new ModelMetrics(), theme, new FixedTranslation(), bindings, width, "root");
    }

    /// <summary>
    /// Asserts the five solids one painted surface emits — the fill plus four edges — start at
    /// <paramref name="offset"/> in the recorder. The popup draws its panel first, so its option row is
    /// the second surface in the log; every other site starts at zero.
    /// </summary>
    private static void CheckPaint(int offset, UiColour fill, UiColour border, string outlet)
    {
        IList colors = RecordedBoxColors();
        if (colors.Count < offset + 5)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + outlet + " recorded " + colors.Count
                + " solid(s); expected at least " + (offset + 5));
            return;
        }

        Check(SameColor(NativeFixture.Record<UiColour>(colors[offset]), fill), outlet + " paints the table's fill");

        bool edgesMatch = true;
        for (int i = 1; i < 5; i++)
        {
            edgesMatch &= SameColor(NativeFixture.Record<UiColour>(colors[offset + i]), border);
        }

        Check(edgesMatch, outlet + " outlines with the table's border");
    }

    private static void ExpectCell(
        UiTheme theme, UiStatusTone tone, UiEmphasis emphasis, UiColour fill, UiColour border, UiColour text, string label)
    {
        UiResolvedStyle style = theme.Styles.Resolve(tone, emphasis);
        bool ok = SameColor(style.Fill, fill) && SameColor(style.Border, border) && SameColor(style.Text, text);
        Check(ok, label + " resolves to the documented fill/border/text");
        if (!ok)
        {
            Console.Error.WriteLine("      got fill=" + Describe(style.Fill) + " border=" + Describe(style.Border)
                + " text=" + Describe(style.Text));
        }
    }

    /// <summary>The colour of the most recent label call, or a recorded failure when none happened.</summary>
    private static void CheckLastLabel(UiColour expected, string name)
    {
        IList colors = RecordedLabelColors();
        if (colors.Count == 0)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " (no label was recorded)");
            return;
        }

        UiColour actual = NativeFixture.Record<UiColour>(colors[colors.Count - 1]);
        Check(SameColor(actual, expected),
            name + " (expected " + Describe(expected) + ", got " + Describe(actual) + ")");
    }

    private static bool SameStyle(UiResolvedStyle left, UiResolvedStyle right)
    {
        return SameColor(left.Fill, right.Fill)
            && SameColor(left.Border, right.Border)
            && SameColor(left.Text, right.Text);
    }

    /// <summary>Component-wise, because the harness's UnityEngine stub is not obliged to implement Rect equality.</summary>
    private static bool SameRect(UiLayoutRect left, UiLayoutRect right)
    {
        return Near(left.X, right.X) && Near(left.Y, right.Y)
            && Near(left.Width, right.Width) && Near(left.Height, right.Height);
    }

    private static bool SameColor(UiColour left, UiColour right)
    {
        return Near(left.Red, right.Red) && Near(left.Green, right.Green) && Near(left.Blue, right.Blue) && Near(left.Alpha, right.Alpha);
    }

    private static bool Near(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= 0.0001f;
    }

    private static string Describe(UiColour color)
    {
        return "(" + color.Red + ", " + color.Green + ", " + color.Blue + ", " + color.Alpha + ")";
    }

    /// <summary>The same rendering for a nullable token, so an unclaimed edge prints as <c>null</c>.</summary>
    private static string Describe(UiColour? color)
    {
        return color.HasValue ? Describe(color.Value) : "null";
    }

    private static void ResetPointerSeams()
    {
        NativeFixture.DebugMousePositionEnabled = false;
        NativeFixture.DebugMouseDown = false;
        NativeFixture.DebugMouseDrag = false;
        NativeFixture.DebugMouseUp = false;
        NativeFixture.DebugEnter = false;
        NativeFixture.DebugFocusLost = false;
        NativeFixture.ButtonOverride = null;
        NativeFixture.SliderOverride = null;
        NativeFixture.TextFieldOverride = null;
    }

    private static void ClearRecordedBoxes()
    {
        MethodInfo? clear = typeof(Verse.Widgets).GetMethod(
            "ClearDrawBoxSolidCalls", BindingFlags.Public | BindingFlags.Static);
        if (clear == null) throw new Exception("Verse stub is missing ClearDrawBoxSolidCalls");
        clear.Invoke(null, null);
    }

    private static IList RecordedBoxColors()
    {
        FieldInfo? colors = typeof(Verse.Widgets).GetField(
            "DrawBoxSolidColors", BindingFlags.Public | BindingFlags.Static);
        if (colors == null) throw new Exception("Verse stub is missing DrawBoxSolidColors");
        return (IList)colors.GetValue(null)!;
    }

    private static IList RecordedLabelColors()
    {
        FieldInfo? colors = typeof(Verse.Widgets).GetField(
            "LabelColors", BindingFlags.Public | BindingFlags.Static);
        if (colors == null) throw new Exception("Verse stub is missing LabelColors");
        return (IList)colors.GetValue(null)!;
    }

    private static IList RecordedBoxRects()
    {
        FieldInfo? rects = typeof(Verse.Widgets).GetField(
            "DrawBoxSolidRects", BindingFlags.Public | BindingFlags.Static);
        if (rects == null) throw new Exception("Verse stub is missing DrawBoxSolidRects");
        return (IList)rects.GetValue(null)!;
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
        }
    }

    private static void CheckClose(float expected, float actual, string name)
    {
        Check(Near(expected, actual), name + " (expected " + expected + ", got " + actual + ")");
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private sealed class ModelMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return font switch
            {
                UiFont.Tiny => 16f,
                UiFont.Small => 24f,
                _ => 32f
            };
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class FixedTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
    }
}
