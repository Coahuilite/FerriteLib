using System;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// FerriteLib UiKit kernel verification entry point. The legacy Schema-1 widget tree
/// (registry/context/interaction/value-store lanes) was deleted together with its production
/// types; every suite here runs against the greenfield <c>FerriteLib.UiKit.Kernel</c> surface:
/// Schema=2 manifest parsing, registry scope fallback, session-owned state, the two-pass layout
/// engine, contract validation at Host creation, popups in window space, and widget behavior.
/// </summary>
internal static class Program
{
    private static int failures;

    private static int Main()
    {
        try
        {
            RunAll();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("UNHANDLED: " + ex.GetType().FullName + " :: " + ex.Message);
        }

        if (failures == 0)
        {
            Console.WriteLine("ALL PASS");
            return 0;
        }

        Console.Error.WriteLine(failures + " test(s) failed.");
        return 1;
    }

    private static void RunAll()
    {
        Console.WriteLine("Kernel registry + manifest contracts...");
        failures += KernelRegistryManifestTests.RunAll();

        Console.WriteLine("Kernel widget behavior (stepper / mode-row)...");
        failures += KernelWidgetBehaviorTests.RunAll();

        Console.WriteLine("Kernel text-fit audit (half-width model, reporting policy)...");
        failures += KernelTextAuditTests.RunAll();

        Console.WriteLine("Kernel vertical slice (greenfield)...");
        failures += KernelSmokeTests.RunAll();

        Console.WriteLine("Kernel core widgets (greenfield)...");
        failures += KernelCoreWidgetTests.RunAll();

        Console.WriteLine("Kernel session/native (greenfield)...");
        failures += KernelSessionTests.RunAll();

        Console.WriteLine("Kernel layout (greenfield)...");
        failures += KernelLayoutTests.RunAll();

        Console.WriteLine("Kernel contract (greenfield)...");
        failures += KernelContractTests.RunAll();

        Console.WriteLine("Kernel window shell (P2 chrome + failure contract)...");
        failures += KernelWindowHostTests.RunAll();

        Console.WriteLine("Kernel window catalog (0.5 keyed instances + shell + active target + pause policy)...");
        failures += KernelWindowCatalogTests.RunAll();

        Console.WriteLine("Kernel backend containment (funnel allowlist)...");
        failures += KernelContainmentTests.RunAll();

        Console.WriteLine("Kernel popup/window-space (greenfield)...");
        failures += KernelPopupTests.RunAll();

        Console.WriteLine("FerriteLib version contract + carrier guard...");
        failures += FerriteLibVersionTests.RunAll();

        Console.WriteLine("FerriteLib public-API tiers (docs/api-tiers.md guard)...");
        failures += FerriteLibApiTierTests.RunAll();

        Console.WriteLine("FerriteLib neutrality (no product vocabulary anywhere)...");
        failures += FerriteLibNeutralityTests.RunAll();

        Console.WriteLine("Kernel leaf atoms (wrapped text / button / rule / slider / number field)...");
        failures += KernelAtomTests.RunAll();

        Console.WriteLine("Kernel resolved style table (tone x emphasis, one funnel)...");
        failures += KernelResolvedStyleTests.RunAll();

        Console.WriteLine("Kernel element identity (stable identity + per-element state)...");
        failures += KernelIdentityTests.RunAll();

        Console.WriteLine("Kernel writability (read-only bindings drive the disabled treatment)...");
        failures += KernelWritabilityTests.RunAll();

        Console.WriteLine("Kernel invalidation (per-key announce, class, batch commit)...");
        failures += KernelInvalidationTests.RunAll();

        Console.WriteLine("Kernel command state (canExecute, one disabled interaction)...");
        failures += KernelCommandStateTests.RunAll();

        Console.WriteLine("Kernel visibility (Visible/VisibleKey, identity, structural prune)...");
        failures += KernelVisibilityTests.RunAll();

        Console.WriteLine("Kernel role attributes (Tone/Emphasis, fallback recording, density)...");
        failures += KernelRoleAttributeTests.RunAll();

        Console.WriteLine("Kernel style documents (two origins, precedence chain, fail-soft recording)...");
        failures += KernelStyleDocumentTests.RunAll();

        Console.WriteLine("Kernel style scopes (region theme in the live tree, page level, drop visibility)...");
        failures += KernelStyleScopeTests.RunAll();

        Console.WriteLine("Kernel stub coverage (game members the harness must carry, trip guard)...");
        failures += KernelStubCoverageTests.RunAll();

        Console.WriteLine("Kernel documents (file sources, dependencies, atomic reload, last-known-good)...");
        failures += KernelDocumentReloadTests.RunAll();

        Console.WriteLine("Kernel diagnostics (per-host subscriptions, attribution, budgets, reload/fit/recovery)...");
        failures += KernelDiagnosticsTests.RunAll();

        Console.WriteLine("Kernel keyed repeater (item scope, key reuse, removal cleanup, template contract)...");
        failures += KernelRepeatTests.RunAll();

        Console.WriteLine("Kernel common controls (checkbox / progress / tree contracts)...");
        failures += KernelControlKindTests.RunAll();

        Console.WriteLine("Kernel neutral fixture page (data-driven rows + the new controls, library fixture only)...");
        failures += KernelFixturePageTests.RunAll();

        Console.WriteLine("Kernel ordinary-settings recipe (B: public-only authoring, explicit notify, lifecycle)...");
        failures += KernelOrdinarySettingsRecipeTests.RunAll();

        // --- T2 automatic reload scheduling (owner: reload) - one contiguous block; ---- //
        // --- mvvm and catalog add their own blocks and the Lead resolves the merge. ----- //
        Console.WriteLine("Kernel reload scheduling (quiet period, bounded retry, pause independence, deferral)...");
        failures += KernelReloadSchedulingTests.RunAll();
        // ------------------------------------------------------------------------------- //

        // ---- T1 MVVM / page lifecycle lanes (owner: mvvm) - begin -----------------------------
        Console.WriteLine("Kernel notification adapter (explicit mapping, bounded batch, unsubscribe, thread refusal)...");
        failures += KernelNotifyAdapterTests.RunAll();

        Console.WriteLine("Kernel page lifecycle (attach/detach order, reload invariance, the page-level door)...");
        failures += KernelPageLifecycleTests.RunAll();
        // ---- T1 MVVM / page lifecycle lanes (owner: mvvm) - end -------------------------------

        // --- T3 widget catalogue (owner: catalog) - one contiguous block, merge-conflict anchor ---
        Console.WriteLine("Kernel widget catalogue (read-only description, scope-preserving identity, no factories)...");
        failures += KernelWidgetCatalogTests.RunAll();
        // --- end T3 widget catalogue ---

        Console.WriteLine("Kernel architecture probes (headless button / two visuals / live resize)...");
        failures += KernelArchitectureProbeTests.RunAll();

        // ---- Batch 1 (0.7.x) lanes. Owners: layout (placement + density), tone. ---------------
        // ---- The Lead wires these; each owner's block is contiguous and merge-safe. ----------
        Console.WriteLine("Kernel placement (Overlay matrix, refusal matrix, flow boundary, envelope)...");
        failures += KernelPlacementTests.RunAll();

        // ---- Batch 1 / tone - begin ----------------------------------------------------------
        Console.WriteLine("Kernel tone vocabulary (four authored meanings, state redirects, one accent)...");
        failures += KernelToneVocabularyTests.RunAll();
        // ---- Batch 1 / tone - end ------------------------------------------------------------

        // ---- Batch 1 / independent verification (owner: verify) -------------------------------
        Console.WriteLine("Kernel Batch 1 verification (independent probe: tone, density, placement)...");
        failures += KernelBatch1VerificationTests.RunAll();
        // ---- Batch 1 / verify - end ----------------------------------------------------------

        // ---- 0.7.x additions (maintainer ruling 2026-09-20): additions land inside 0.7.0 --------
        Console.WriteLine("Kernel text field (B7: identity, draft, the commit rule, placeholder)...");
        failures += KernelTextFieldTests.RunAll();

        Console.WriteLine("Kernel mode-row hover help (per-option identity published, never painted)...");
        failures += KernelModeRowHelpTests.RunAll();

        Console.WriteLine("Kernel element help (engine-wide HelpKey claimed on hover)...");
        failures += KernelElementHelpTests.RunAll();

        Console.WriteLine("Kernel mode-row localization (TitleKeyN through the translation seam)...");
        failures += KernelModeRowLocalizationTests.RunAll();

        Console.WriteLine("Kernel option help (the shared HoverHelpKey contract, mode-row and dropdown)...");
        failures += KernelOptionHelpTests.RunAll();

        Console.WriteLine("Kernel container Tab (the coherence fix: the contract now admits the engine's read)...");
        failures += KernelContainerTabTests.RunAll();
        // ---- end 0.7.x additions ------------------------------------------------------------------

        // ---- 0.7.x height axis (owner: fl-dev) - one contiguous block ---------------------------
        Console.WriteLine("Kernel content height (Height=MatchContent: the reference, its matrix, its degradation)...");
        failures += KernelContentHeightTests.RunAll();
        // ----------------------------------------------------------------------------------------

        Console.WriteLine("Kernel section header (the chrome a manifest can now turn off)...");
        failures += KernelSectionHeaderTests.RunAll();

        Console.WriteLine("Kernel lane registration (every lane file is invoked from Program.cs)...");
        failures += KernelLaneRegistrationTests.RunAll();
    }
}
