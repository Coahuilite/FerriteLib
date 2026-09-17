using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

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
        Run("UiTheme.DarkGold carries the frozen 0.6 palette as explicit values (C)", VerifyDarkGoldIsFrozenHistory);
        Run("new UiTheme() is the vanilla-aligned default and instances stay independent (C)", VerifyDefaultIsVanillaAligned);
        ResetPointerSeams();
        return failures;
    }

    private static void VerifyEveryCell()
    {
        UiTheme theme = UiTheme.DarkGold;

        ExpectCell(theme, UiStatusTone.Neutral, UiEmphasis.Normal, theme.Raised, theme.Border, theme.TextPrimary,
            "Neutral/Normal (a form control's value)");
        ExpectCell(theme, UiStatusTone.Neutral, UiEmphasis.Muted, theme.Raised, theme.Border, theme.TextSecondary,
            "Neutral/Muted (a badge's compact text)");
        ExpectCell(theme, UiStatusTone.Active, UiEmphasis.Normal, theme.Selected, theme.AccentGold, theme.TextOnGold,
            "Active/Normal");
        ExpectCell(theme, UiStatusTone.Active, UiEmphasis.Muted, theme.Selected, theme.AccentGold, theme.TextOnGold,
            "Active/Muted");
        ExpectCell(theme, UiStatusTone.Success, UiEmphasis.Normal, theme.Success, theme.BorderStrong, theme.TextOnGold,
            "Success/Normal");
        ExpectCell(theme, UiStatusTone.Success, UiEmphasis.Muted, theme.Success, theme.BorderStrong, theme.TextOnGold,
            "Success/Muted");
        ExpectCell(theme, UiStatusTone.Warning, UiEmphasis.Normal, theme.Danger, theme.Danger, theme.TextOnDanger,
            "Warning/Normal");
        ExpectCell(theme, UiStatusTone.Warning, UiEmphasis.Muted, theme.Danger, theme.Danger, theme.TextOnDanger,
            "Warning/Muted");
        ExpectCell(theme, UiStatusTone.Danger, UiEmphasis.Normal, theme.Danger, theme.Danger, theme.TextOnDanger,
            "Danger/Normal");
        ExpectCell(theme, UiStatusTone.Danger, UiEmphasis.Muted, theme.Danger, theme.Danger, theme.TextOnDanger,
            "Danger/Muted");
        ExpectCell(theme, UiStatusTone.Disabled, UiEmphasis.Normal, theme.Base, theme.Divider, theme.TextDisabled,
            "Disabled/Normal");
        ExpectCell(theme, UiStatusTone.Disabled, UiEmphasis.Muted, theme.Base, theme.Divider, theme.TextDisabled,
            "Disabled/Muted");

        Check(
            SameStyle(theme.Styles.Resolve(UiStatusTone.Active, UiEmphasis.Normal), theme.Styles.Resolve(UiStatusTone.Active, UiEmphasis.Muted)),
            "a saturated tone paints the same text under either emphasis (the axis carries one measured difference, not a scale)");

        // The 0.4 window first collapsed the two names and then put one back as a redirect, once a
        // consumer's build proved it was used. So the property exists -- and it is a view onto Danger,
        // which is the claim that matters: a redirect is a name, not a second token.
        Check(typeof(UiTheme).GetProperty("Warning") != null,
            "the pre-0.4 alarm name survives as a redirect for one minor");
        UiTheme redirectProbe = UiTheme.DarkGold;
        redirectProbe.Warning = new Color(0.11f, 0.22f, 0.33f, 1f);
        Check(SameColor(redirectProbe.Danger, redirectProbe.Warning),
            "assigning the pre-0.4 name assigns the alarm surface it redirects to");
        Check(SameStyle(redirectProbe.Styles.Resolve(UiStatusTone.Warning), redirectProbe.Styles.Resolve(UiStatusTone.Danger)),
            "both tone names resolve to one surface: the redirect is a name, not a second token");

        UiTheme other = UiTheme.DarkGold;
        Check(!ReferenceEquals(theme.Styles, other.Styles), "two themes never share one resolved-value store");
        Check(!typeof(UiTheme).GetProperty("Styles")!.CanWrite, "the store cannot be swapped out for another one");
        Check(!typeof(UiStyleTable).GetProperty("FallbackCount")!.CanWrite, "a store's own state is read-only to callers");

        // The store is a read-through view of its theme, which is what keeps a consumer's re-tint alive:
        // a copy taken at construction would silently freeze every token the old bag let them move.
        UiTheme retinted = UiTheme.DarkGold;
        Color before = retinted.Styles.Resolve(UiStatusTone.Active).Fill;
        retinted.Selected = new Color(0.9f, 0.1f, 0.2f, 1f);
        Check(
            SameColor(retinted.Styles.Resolve(UiStatusTone.Active).Fill, retinted.Selected) && !SameColor(before, retinted.Selected),
            "a re-tint moves the table's answer instead of leaving a stale copy behind");
    }

    /// <summary>
    /// One token, every painting site: the fill and the edge are chosen so that a site still holding its
    /// own copy of the mapping paints a visibly different colour, which is the assertion that goes red if
    /// the duplication comes back. The neutral round repeats it for the shared-border default.
    /// </summary>
    private static void VerifyOutletsShareOneTable()
    {
        var fill = new Color(0.11f, 0.22f, 0.33f, 1f);
        var edge = new Color(0.44f, 0.55f, 0.66f, 1f);
        var decoyAccent = new Color(0.77f, 0.88f, 0.99f, 1f);

        UiTheme active = UiTheme.DarkGold;
        active.Selected = fill;
        active.SelectedBorder = edge;
        active.AccentGold = decoyAccent;

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

        var raised = new Color(0.12f, 0.13f, 0.14f, 1f);
        var raisedEdge = new Color(0.21f, 0.22f, 0.23f, 1f);
        var decoyShared = new Color(0.31f, 0.32f, 0.33f, 1f);

        UiTheme neutral = UiTheme.DarkGold;
        neutral.Raised = raised;
        neutral.RaisedBorder = raisedEdge;
        neutral.Border = decoyShared;

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
        UiTheme theme = UiTheme.DarkGold;
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
        Check(SameColor(mutedFallback.Text, theme.TextPrimary),
            "an undeclared emphasis falls back to Normal");
        Check(table.FallbackCount == 2, "the emphasis fallback was recorded too");
    }

    private static void VerifyWritabilityBeatsAuthor()
    {
        UiTheme theme = UiTheme.DarkGold;
        UiStyleTable table = theme.Styles;

        UiResolvedStyle writable = table.Resolve(UiStatusTone.Danger, UiEmphasis.Normal, writable: true);
        Check(SameColor(writable.Fill, theme.Danger), "a writable element keeps its authored tone");

        UiResolvedStyle readOnly = table.Resolve(UiStatusTone.Danger, UiEmphasis.Normal, writable: false);
        Check(
            SameColor(readOnly.Fill, theme.Base)
                && SameColor(readOnly.Border, theme.Divider)
                && SameColor(readOnly.Text, theme.TextDisabled),
            "a read-only element is painted disabled whatever tone it was authored with (state beats author)");

        UiResolvedStyle unknown = table.Resolve(UiStatusTone.Active, UiEmphasis.Normal, writable: null);
        Check(SameColor(unknown.Fill, theme.Selected), "an unknown writability resolves by tone alone");

        Check(table.FallbackCount == 0, "the writability rule is a rule, not a fallback: nothing was recorded");
    }

    private static void VerifySurfacePairs()
    {
        var shared = new Color(0.31f, 0.32f, 0.33f, 1f);
        var own = new Color(0.41f, 0.42f, 0.43f, 1f);

        UiTheme sharedEdge = UiTheme.DarkGold;
        sharedEdge.Border = shared;
        ClearRecordedBoxes();
        UiThemeDraw.Panel(new Rect(0f, 0f, 40f, 40f), sharedEdge);
        CheckPaint(0, sharedEdge.Panel, shared, "Panel() still uses the shared border when no surface overrides it");

        UiTheme perSurface = UiTheme.DarkGold;
        perSurface.Border = shared;
        perSurface.PanelBorder = own;
        ClearRecordedBoxes();
        UiThemeDraw.Panel(new Rect(0f, 0f, 40f, 40f), perSurface);
        CheckPaint(0, perSurface.Panel, own, "Panel() uses its own edge override instead of the shared border");

        var pairFill = new Color(0.51f, 0.52f, 0.53f, 1f);
        var pairEdge = new Color(0.61f, 0.62f, 0.63f, 1f);
        UiTheme assigned = UiTheme.DarkGold;
        assigned.SelectedSurface = new UiSurfaceStyle(pairFill, pairEdge);

        UiResolvedStyle active = assigned.Styles.Resolve(UiStatusTone.Active);
        Check(
            SameColor(active.Fill, pairFill) && SameColor(active.Border, pairEdge),
            "assigning a whole surface pair moves the table's Active cell with it");

        ClearRecordedBoxes();
        UiThemeDraw.StatusTreatment(new Rect(0f, 0f, 40f, 20f), assigned, UiStatusTone.Active);
        CheckPaint(0, pairFill, pairEdge, "StatusTreatment paints the assigned pair");
    }

    // --- C (0.7): the palette split. The freeze half is written FIRST and pins the 0.6 values as
    // literals; only then may the constructor default move. Provenance for the new defaults lives
    // with UiTheme itself and in docs/development/0.7/40-verification.md: the four substrate/edge
    // pairs and the two option references are carried from the previous vanilla Verse.Widgets
    // investigation (service build identity NOT established); everything else in the new default is
    // DERIVED in the same direction, and this lane asserts exactly the shipped numbers.

    private static void VerifyDarkGoldIsFrozenHistory()
    {
        UiTheme t = UiTheme.DarkGold;
        Check(SameColor(new Color(0.065f, 0.065f, 0.063f, 1f), t.Base), "DarkGold Base = the 0.6 literal");
        Check(SameColor(new Color(0.095f, 0.095f, 0.090f, 1f), t.Panel), "DarkGold Panel = the 0.6 literal");
        Check(SameColor(new Color(0.135f, 0.135f, 0.128f, 1f), t.Raised), "DarkGold Raised = the 0.6 literal");
        Check(SameColor(new Color(0.17f, 0.17f, 0.16f, 1f), t.Hover), "DarkGold Hover = the 0.6 literal");
        Check(SameColor(new Color(0.17f, 0.14f, 0.08f, 1f), t.Selected), "DarkGold Selected = the 0.6 literal");
        Check(SameColor(new Color(0.11f, 0.24f, 0.15f, 1f), t.Success), "DarkGold Success = the 0.6 literal");
        Check(SameColor(new Color(0.38f, 0.14f, 0.11f, 1f), t.Danger), "DarkGold Danger = the 0.6 literal");
        Check(SameColor(new Color(0.045f, 0.045f, 0.044f, 1f), t.WorkspacePlane), "DarkGold WorkspacePlane = the 0.6 literal");
        Check(SameColor(new Color(0.085f, 0.085f, 0.080f, 1f), t.SectionBand), "DarkGold SectionBand = the 0.6 literal");
        Check(SameColor(new Color(0.88f, 0.88f, 0.84f, 1f), t.TextPrimary), "DarkGold TextPrimary = the 0.6 literal");
        Check(SameColor(new Color(0.58f, 0.58f, 0.55f, 1f), t.TextSecondary), "DarkGold TextSecondary = the 0.6 literal");
        Check(SameColor(new Color(1f, 0.84f, 0.55f, 1f), t.TextOnGold), "DarkGold TextOnGold = the 0.6 literal");
        Check(SameColor(new Color(1f, 0.65f, 0.46f, 1f), t.TextOnDanger), "DarkGold TextOnDanger = the 0.6 literal");
        Check(SameColor(new Color(0.42f, 0.42f, 0.40f, 1f), t.TextDisabled), "DarkGold TextDisabled = the 0.6 literal");
        Check(SameColor(new Color(0.82f, 0.60f, 0.22f, 1f), t.AccentGold), "DarkGold AccentGold = the 0.6 literal");
        Check(SameColor(new Color(0.96f, 0.80f, 0.42f, 1f), t.HoverPoint), "DarkGold HoverPoint = the 0.6 literal");
        Check(SameColor(new Color(0.21f, 0.21f, 0.20f, 1f), t.Border), "DarkGold Border = the 0.6 literal");
        Check(SameColor(new Color(0.32f, 0.32f, 0.30f, 1f), t.BorderStrong), "DarkGold BorderStrong = the 0.6 literal");
        Check(SameColor(new Color(0.14f, 0.14f, 0.13f, 1f), t.Divider), "DarkGold Divider = the 0.6 literal");
        Check(t.BaseBorder == null && t.PanelBorder == null && t.RaisedBorder == null
            && t.HoverBorder == null && t.SelectedBorder == null && t.SuccessBorder == null
            && t.DangerBorder == null,
            "DarkGold keeps the 0.6 per-surface edges unclaimed");
        Check(t.DefaultFont == UiFont.Small && t.Geometry == UiGeometry.Default,
            "DarkGold keeps the 0.6 font and density untouched");

        UiTheme again = UiTheme.DarkGold;
        Check(!ReferenceEquals(t, again), "DarkGold hands out a fresh instance, never one shared object");
        t.Base = new Color(0.5f, 0.5f, 0.5f, 1f);
        Check(!SameColor(t.Base, again.Base), "and re-tinting one DarkGold instance cannot repaint the next");
    }

    private static void VerifyDefaultIsVanillaAligned()
    {
        UiTheme d = new();
        // Verified references (previous vanilla Verse.Widgets investigation; build identity not established).
        Check(SameColor(new Color(21f / 255f, 25f / 255f, 29f / 255f, 1f), d.Base),
            "default Base = the vanilla window substrate RGB(21,25,29)");
        Check(SameColor(new Color(42f / 255f, 43f / 255f, 44f / 255f, 1f), d.Panel),
            "default Panel = the vanilla section substrate RGB(42,43,44)");
        Check(d.BaseBorder.HasValue && SameColor(new Color(97f / 255f, 108f / 255f, 122f / 255f, 1f), d.BaseBorder.GetValueOrDefault()),
            "default BaseBorder = the vanilla window edge RGB(97,108,122)");
        Check(d.PanelBorder.HasValue && SameColor(new Color(135f / 255f, 135f / 255f, 135f / 255f, 1f), d.PanelBorder.GetValueOrDefault()),
            "default PanelBorder = the vanilla section edge RGB(135,135,135)");
        Check(SameColor(new Color(0.21f, 0.21f, 0.21f, 1f), d.Raised),
            "default Raised = the vanilla unselected option reference RGB(0.21,0.21,0.21)");
        Check(SameColor(new Color(0.32f, 0.28f, 0.21f, 1f), d.Selected),
            "default Selected = the vanilla selected option reference RGB(0.32,0.28,0.21)");

        // Derived roles: neutral steps in the same direction; the accent is a candidate yellow, not a
        // claimed vanilla constant (the table does not establish it).
        Check(SameColor(new Color(0.27f, 0.27f, 0.27f, 1f), d.Hover), "default Hover is derived one step above Raised");
        Check(SameColor(new Color(0.13f, 0.30f, 0.18f, 1f), d.Success), "default Success is derived legible on neutral");
        Check(SameColor(new Color(0.42f, 0.15f, 0.12f, 1f), d.Danger), "default Danger is derived legible on neutral");
        Check(SameColor(new Color(0.93f, 0.77f, 0.22f, 1f), d.AccentGold), "default accent is the stated yellow candidate");
        Check(d.AccentGold.r == d.AccentWith(0.5f).r && d.AccentGold.g == d.AccentWith(0.5f).g,
            "AccentWith still derives from the accent hue");

        // Yellow stays reserved: only selection's edge and the accent roles carry it; the common
        // surfaces and text are neutral grays or near-white.
        Check(d.Base.b > d.Base.r && d.Panel.g >= d.Panel.r, "substrate planes stay neutral/cool, not gold");
        Check(!SameColor(d.PanelBorder.GetValueOrDefault(), d.AccentGold) && !SameColor(d.Border, d.AccentGold),
            "the shared edges are not painted in the accent");

        // Geometry and fonts do not move in package C.
        Check(d.DefaultFont == UiFont.Small && d.Geometry == UiGeometry.Default,
            "the new default keeps the shipped font and density");

        // Fresh-instance isolation, the same template rule DarkGold has.
        UiTheme a = new();
        UiTheme b = new();
        Check(!ReferenceEquals(a, b), "two constructor defaults are two objects");
        a.TextPrimary = new Color(0.1f, 0.2f, 0.3f, 1f);
        Check(!SameColor(a.TextPrimary, b.TextPrimary), "and re-tinting one cannot repaint the other");

        // Clone: complete token copy, then full independence.
        UiTheme original = new();
        UiTheme copy = original.Clone();
        Check(!ReferenceEquals(original, copy), "Clone returns a fresh bag");
        Check(SameColor(original.Base, copy.Base) && SameColor(original.Panel, copy.Panel)
            && SameColor(original.AccentGold, copy.AccentGold) && SameColor(original.TextPrimary, copy.TextPrimary)
            && SameColor(original.Border, copy.Border)
            && copy.BaseBorder.HasValue && SameColor(original.BaseBorder.GetValueOrDefault(), copy.BaseBorder.GetValueOrDefault())
            && copy.SelectedBorder == null && original.SelectedBorder == null,
            "Clone copies every token, the claimed per-surface edges included");
        copy.AccentGold = new Color(0.05f, 0.9f, 0.05f, 1f);
        Check(!SameColor(original.AccentGold, copy.AccentGold),
            "and moving a token on the clone leaves the original untouched");

        // A custom theme built on the default keeps its own overrides and inherits only the
        // unspecified defaults; an explicit DarkGold base keeps the OLD baseline instead.
        UiTheme custom = new() { Hover = new Color(0.5f, 0.1f, 0.1f, 1f) };
        Check(SameColor(custom.Hover, new Color(0.5f, 0.1f, 0.1f, 1f)) && SameColor(custom.Base, d.Base),
            "custom overrides survive on top of the new default");
        UiTheme darkCustom = UiTheme.DarkGold;
        darkCustom.Hover = new Color(0.5f, 0.1f, 0.1f, 1f);
        Check(SameColor(darkCustom.Base, new Color(0.065f, 0.065f, 0.063f, 1f)),
            "the consumer route for the old baseline is DarkGold + overrides");

        // Two hosts on different palettes resolve through their own theme, one draw outlet each call.
        UiTheme windowOne = new();
        UiTheme windowTwo = UiTheme.DarkGold;
        Check(!ReferenceEquals(windowOne.Styles, windowTwo.Styles), "the two themes hold separate stores");
        ClearRecordedBoxes();
        UiThemeDraw.Panel(new Rect(0f, 0f, 40f, 40f), windowOne);
        CheckPaint(0, windowOne.Panel, windowOne.PanelSurface.Border, "one window paints the vanilla plane");
        ClearRecordedBoxes();
        UiThemeDraw.Panel(new Rect(0f, 0f, 40f, 40f), windowTwo);
        CheckPaint(0, windowTwo.Panel, windowTwo.Border, "the other paints the frozen DarkGold plane");

        // The table answers on the new bag without re-derivation: Active carries the yellow edge.
        UiResolvedStyle active = windowOne.Styles.Resolve(UiStatusTone.Active);
        Check(SameColor(active.Border, windowOne.AccentGold) && SameColor(active.Fill, windowOne.Selected),
            "Active resolves to the selected plane outlined in the accent, not a filled-yellow plane");
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

        UiTheme theme = UiTheme.DarkGold;
        theme.Geometry = new UiGeometry(6f, 4f, 6f, 28f, 3f);
        ClearRecordedBoxes();
        UiThemeDraw.Panel(new Rect(0f, 0f, 40f, 40f), theme);
        IList rects = RecordedBoxRects();
        Check(rects.Count == 5, "a painted surface is one fill plus four edges");
        Check(rects.Count == 5 && Near(((Rect)rects[1]).height, 3f), "the hairline token sets the edge width");

        using UiSession session = new();
        var bindings = new UiBindings();
        string mode = "A";
        bindings.BindValue("mode", () => mode, value => mode = value);
        InputModeRowWidget widget = new();
        widget.Configure(new UiElementSpec("mode", InputModeRowWidget.Kind, new Dictionary<string, string>
        {
            ["Title1"] = "A", ["Value1"] = "A",
            ["Title2"] = "B", ["Value2"] = "B",
            ["Title3"] = "C", ["Value3"] = "C",
            ["Title4"] = "D", ["Value4"] = "D"
        }));
        UiTheme dense = UiTheme.DarkGold;
        dense.Geometry = new UiGeometry(6f, 2f, 2f, 20f, 1f);
        float measured = widget.Measure(MakeContext(session, dense, bindings, 150f));
        CheckClose(90f, measured, "row height, gap and spacing all reach the mode-row's measure (4x20 + 3x2 + 2x2)");
    }

    private static void VerifyResolvedFontIsMeasured()
    {
        UiTheme theme = UiTheme.DarkGold;
        Check(theme.Styles.Font == UiFont.Small, "the store resolves the theme's font");

        theme.DefaultFont = UiFont.Medium;
        Check(theme.Styles.Font == UiFont.Medium, "a font-size change moves the resolved font");

        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(new ModelMetrics(), reports.Add);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;
        try
        {
            UiThemeDraw.Label(new Rect(0f, 0f, 10f, 24f), "abcd", theme, null, null, TextAnchor.MiddleLeft, true);
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
        var onGold = new Color(0.91f, 0.12f, 0.13f, 1f);
        var primary = new Color(0.21f, 0.22f, 0.23f, 1f);
        var secondary = new Color(0.31f, 0.32f, 0.33f, 1f);

        UiTheme theme = UiTheme.DarkGold;
        theme.TextOnGold = onGold;
        theme.TextPrimary = primary;
        theme.TextSecondary = secondary;

        DrawStatusBadge(theme, UiStatusTone.Active);
        CheckLastLabel(onGold, "StatusBadge(Active) writes the table's Active text colour");
        DrawStatusBadge(theme, UiStatusTone.Neutral);
        CheckLastLabel(secondary, "StatusBadge(Neutral) writes the Muted text colour (the measured coordinate)");
        DrawStatusBadge(theme, UiStatusTone.Danger);
        CheckLastLabel(theme.TextOnDanger, "StatusBadge(Danger) writes the alarm text colour");

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

        UiTheme theme = UiTheme.DarkGold;
        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"density-test\">"
            + "<Widget Id=\"dd\" Kind=\"input/dropdown\" Option1=\"A\" Value1=\"A\" />"
            + "</UiPage>");
        var bindings = new UiBindings();
        bindings.BindValue("dd", () => "A", _ => { });
        using UiHost host = new("density-test", manifest, bindings, theme, new ModelMetrics(), new FixedTranslation());

        UiLayoutSnapshot first = host.MeasureAndArrange(new Vector2(240f, 200f));
        CheckClose(28f, first.RectById["dd"].height, "the default density gives the control its default height");

        theme.Geometry = new UiGeometry(6f, 4f, 6f, 20f, 1f);
        UiLayoutSnapshot dense = host.MeasureAndArrange(new Vector2(240f, 200f));
        Check(!ReferenceEquals(first, dense), "a density change re-arranges instead of reusing the old bands");
        CheckClose(20f, dense.RectById["dd"].height, "the dense row height reaches the arranged rect");

        theme.DefaultFont = UiFont.Medium;
        UiLayoutSnapshot remeasured = host.MeasureAndArrange(new Vector2(240f, 200f));
        Check(!ReferenceEquals(dense, remeasured), "a font-size change re-arranges too: Measure sees the resolved font");

        theme.Panel = new Color(0.2f, 0.3f, 0.4f, 1f);
        UiLayoutSnapshot retinted = host.MeasureAndArrange(new Vector2(240f, 200f));
        Check(ReferenceEquals(remeasured, retinted), "a re-tint reuses the bands: colour is not layout-bearing");
    }

    private static void DrawStatusTreatment(UiTheme theme, UiStatusTone tone = UiStatusTone.Active)
    {
        ClearRecordedBoxes();
        UiThemeDraw.StatusTreatment(new Rect(0f, 0f, 80f, 20f), theme, tone);
    }

    private static void DrawStatusBadge(UiTheme theme, UiStatusTone tone = UiStatusTone.Active)
    {
        ClearRecordedBoxes();
        UiThemeDraw.StatusBadge(new Rect(0f, 0f, 80f, 20f), "badge", theme, tone);
    }

    private static void DrawDropdown(UiTheme theme, string currentValue)
    {
        using UiSession session = new();
        var bindings = new UiBindings();
        string current = currentValue;
        bindings.BindValue("dd", () => current, value => current = value);
        DropdownWidget widget = new();
        widget.Configure(new UiElementSpec("dd", DropdownWidget.Kind, new Dictionary<string, string>
        {
            ["Option1"] = "A", ["Value1"] = "A",
            ["Option2"] = "B", ["Value2"] = "B"
        }));

        ClearRecordedBoxes();
        widget.Draw(new Rect(0f, 0f, 160f, 28f), MakeContext(session, theme, bindings, 160f));
    }

    private static void DrawModeRow(UiTheme theme, string currentValue)
    {
        using UiSession session = new();
        var bindings = new UiBindings();
        string current = currentValue;
        bindings.BindValue("mode", () => current, value => current = value);
        InputModeRowWidget widget = new();
        widget.Configure(new UiElementSpec("mode", InputModeRowWidget.Kind, new Dictionary<string, string>
        {
            ["Title1"] = "A", ["Value1"] = "A"
        }));

        ClearRecordedBoxes();
        widget.Draw(new Rect(0f, 0f, 150f, 36f), MakeContext(session, theme, bindings, 150f));
    }

    private static void DrawPopupRow(UiTheme theme, string currentValue)
    {
        using UiSession session = new();
        var bindings = new UiBindings();
        var options = new List<KeyValuePair<string, string>> { new("B", "B") };

        ClearRecordedBoxes();
        UiPopup.DrawOptionList(
            new Rect(0f, 0f, 120f, 24f),
            "dd",
            MakeContext(session, theme, bindings, 120f),
            options,
            currentValue,
            _ => { });
    }

    private static UiWidgetContext MakeContext(UiSession session, UiTheme theme, IUiBindings bindings, float width)
    {
        return new UiWidgetContext("style-test", session, new ModelMetrics(), theme, new FixedTranslation(), bindings, width, "root");
    }

    /// <summary>
    /// Asserts the five solids one painted surface emits — the fill plus four edges — start at
    /// <paramref name="offset"/> in the recorder. The popup draws its panel first, so its option row is
    /// the second surface in the log; every other site starts at zero.
    /// </summary>
    private static void CheckPaint(int offset, Color fill, Color border, string outlet)
    {
        IList colors = RecordedBoxColors();
        if (colors.Count < offset + 5)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + outlet + " recorded " + colors.Count
                + " solid(s); expected at least " + (offset + 5));
            return;
        }

        Check(SameColor((Color)colors[offset], fill), outlet + " paints the table's fill");

        bool edgesMatch = true;
        for (int i = 1; i < 5; i++)
        {
            edgesMatch &= SameColor((Color)colors[offset + i], border);
        }

        Check(edgesMatch, outlet + " outlines with the table's border");
    }

    private static void ExpectCell(
        UiTheme theme, UiStatusTone tone, UiEmphasis emphasis, Color fill, Color border, Color text, string label)
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
    private static void CheckLastLabel(Color expected, string name)
    {
        IList colors = RecordedLabelColors();
        if (colors.Count == 0)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " (no label was recorded)");
            return;
        }

        Color actual = (Color)colors[colors.Count - 1];
        Check(SameColor(actual, expected),
            name + " (expected " + Describe(expected) + ", got " + Describe(actual) + ")");
    }

    private static bool SameStyle(UiResolvedStyle left, UiResolvedStyle right)
    {
        return SameColor(left.Fill, right.Fill)
            && SameColor(left.Border, right.Border)
            && SameColor(left.Text, right.Text);
    }

    private static bool SameColor(Color left, Color right)
    {
        return Near(left.r, right.r) && Near(left.g, right.g) && Near(left.b, right.b) && Near(left.a, right.a);
    }

    private static bool Near(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= 0.0001f;
    }

    private static string Describe(Color color)
    {
        return "(" + color.r + ", " + color.g + ", " + color.b + ", " + color.a + ")";
    }

    private static void ResetPointerSeams()
    {
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMouseDown = false;
        UiNative.DebugMouseDrag = false;
        UiNative.DebugMouseUp = false;
        UiNative.DebugEnter = false;
        UiNative.DebugFocusLost = false;
        UiNative.ButtonOverride = null;
        UiNative.SliderOverride = null;
        UiNative.TextFieldOverride = null;
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
