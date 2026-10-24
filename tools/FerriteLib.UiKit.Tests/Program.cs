using UnityEngine;
using Verse;
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

    private static int Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--suite")
        {
            try{return (int)typeof(Program).Assembly.GetType("FerriteLib.UiKit.Tests."+args[1],true)!.GetMethod("RunAll",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,null)!;}
            catch(Exception error){Console.Error.WriteLine(error);return 1;}
        }
        if (args.Length == 2 && args[0] == "--write-stable-surface")
        { FerriteLibApiTierTests.WriteStableSurface(args[1]); return 0; }
        if (args.Length == 1 && args[0] == "--readonly-outputs")
            return RunReadOnlyOutputs();
        if (args.Length == 1 && args[0] == "--readonly-api")
            return RunApiContract();
        if (args.Length == 1 && args[0] == "--closeout-editing")
            return RunEditing();
        if (args.Length == 1 && args[0] == "--closeout-appearance")
            return RunAppearance();
        if (args.Length == 1 && args[0] == "--closeout-items")
            return RunItems();
        if (args.Length == 1 && args[0] == "--closeout-api")
            return RunApi();
        if (args.Length == 1 && args[0] == "--closeout-inventory")
            return RunInventory();
        try
        {
            RunAll();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("UNHANDLED: " + ex);
        }

        if (failures == 0)
        {
            Console.WriteLine("ALL PASS");
            return 0;
        }

        Console.Error.WriteLine(failures + " test(s) failed.");
        return 1;
    }

    private static int RunEditing() => KernelSharedEditingTests.RunAll() + KernelCloseoutEditingTests.RunAll();
    private static int RunAppearance() => KernelPaletteTests.RunAll() + KernelStyleScopeTests.RunAll() + KernelCloseoutAppearanceTests.RunAll();
    private static int RunItems() => KernelItemBindingTests.RunAll();
    private static int RunApiContract() => FerriteLibApiTierTests.RunAll() + KernelContainmentTests.RunAll();
    private static int RunApi() => RunApiContract() + KernelPublicAuthorTests.RunAll();
    private static int RunReadOnlyOutputs() => KernelReadOnlyOutputTests.RunAll();
    private static int RunInventory() => KernelCloseoutInventoryTests.RunAll();

    private static int RunSuite(Func<int> suite,string name)
    {NativeFixture.ResetInput();try{return suite();}catch(Exception error){Console.Error.WriteLine("  FAIL: "+name+" "+error);return 1;}}
    private static void RunAll()
    {
        Console.WriteLine("0.8 S0/S1 production shared behavior...");
        failures += RunSuite(()=>KernelNativeEventConsumptionTests.RunAll(),"KernelNativeEventConsumptionTests.RunAll");
        failures += RunSuite(()=>KernelFailureLifetimeTests.RunAll(),"KernelFailureLifetimeTests.RunAll");
        failures += RunSuite(()=>KernelLayoutReuseTests.RunAll(),"KernelLayoutReuseTests.RunAll");
        failures += RunSuite(()=>KernelReactiveFoundationTests.RunAll(),"KernelReactiveFoundationTests.RunAll");
        failures += RunSuite(()=>KernelReactiveInventoryTests.RunAll(),"KernelReactiveInventoryTests.RunAll");
        failures += RunSuite(()=>KernelLayerDispatchTests.RunAll(),"KernelLayerDispatchTests.RunAll");
        failures += RunSuite(()=>KernelImagePlacementTests.RunAll(),"KernelImagePlacementTests.RunAll");
        failures += RunSuite(()=>KernelSharedBehaviorTests.RunAll(),"KernelSharedBehaviorTests.RunAll");
        failures += RunSuite(()=>RunEditing(),"RunEditing");
        failures += RunSuite(()=>RunAppearance(),"RunAppearance");
        failures += RunSuite(()=>RunReadOnlyOutputs(),"RunReadOnlyOutputs");
        failures += RunSuite(()=>RunItems(),"RunItems");
        failures += RunSuite(()=>RunInventory(),"RunInventory");
        failures += RunSuite(()=>KernelSharedChartTests.RunAll(),"KernelSharedChartTests.RunAll");
        failures += RunSuite(()=>KernelDescriptiveFactsTests.RunAll(),"KernelDescriptiveFactsTests.RunAll");
        failures += RunSuite(()=>KernelBooleanSelectionExpansionTests.RunAll(),"KernelBooleanSelectionExpansionTests.RunAll");

        Console.WriteLine("Kernel registry + manifest contracts...");
        failures += RunSuite(()=>KernelRegistryManifestTests.RunAll(),"KernelRegistryManifestTests.RunAll");

        Console.WriteLine("Kernel widget behavior (stepper / mode-row)...");
        failures += RunSuite(()=>KernelWidgetBehaviorTests.RunAll(),"KernelWidgetBehaviorTests.RunAll");

        Console.WriteLine("Kernel text-fit audit (half-width model, reporting policy)...");
        failures += RunSuite(()=>KernelTextAuditTests.RunAll(),"KernelTextAuditTests.RunAll");

        Console.WriteLine("Kernel vertical slice (greenfield)...");
        failures += RunSuite(()=>KernelSmokeTests.RunAll(),"KernelSmokeTests.RunAll");

        Console.WriteLine("Kernel core widgets (greenfield)...");
        failures += RunSuite(()=>KernelCoreWidgetTests.RunAll(),"KernelCoreWidgetTests.RunAll");

        Console.WriteLine("Kernel session/native (greenfield)...");
        failures += RunSuite(()=>KernelSessionTests.RunAll(),"KernelSessionTests.RunAll");

        Console.WriteLine("Kernel layout (greenfield)...");
        failures += RunSuite(()=>KernelLayoutTests.RunAll(),"KernelLayoutTests.RunAll");

        Console.WriteLine("Kernel contract (greenfield)...");
        failures += RunSuite(()=>KernelContractTests.RunAll(),"KernelContractTests.RunAll");

        Console.WriteLine("Kernel window shell (P2 chrome + failure contract)...");
        failures += RunSuite(()=>KernelWindowHostTests.RunAll(),"KernelWindowHostTests.RunAll");

        Console.WriteLine("Kernel window catalog (0.5 keyed instances + shell + active target + pause policy)...");
        failures += RunSuite(()=>KernelWindowCatalogTests.RunAll(),"KernelWindowCatalogTests.RunAll");

        Console.WriteLine("Kernel backend containment (funnel allowlist)...");

        Console.WriteLine("Kernel popup/window-space (greenfield)...");
        failures += RunSuite(()=>KernelPopupTests.RunAll(),"KernelPopupTests.RunAll");

        Console.WriteLine("Kernel receive eligibility and capture ownership (FL-IC1: own-popup overlap, chart gates, bounded menu)...");
        failures += RunSuite(()=>KernelInputEligibilityTests.RunAll(),"KernelInputEligibilityTests.RunAll");

        Console.WriteLine("Kernel edit transactions and the Cancel ladder (FL-IC2: commit-once, native hooks, tree return)...");
        failures += RunSuite(()=>KernelEditTransactionTests.RunAll(),"KernelEditTransactionTests.RunAll");
        failures += RunSuite(()=>KernelCancelLadderTests.RunAll(),"KernelCancelLadderTests.RunAll");

        Console.WriteLine("FerriteLib version contract + carrier guard...");
        failures += RunSuite(()=>FerriteLibVersionTests.RunAll(),"FerriteLibVersionTests.RunAll");

        Console.WriteLine("FerriteLib public-API tiers (docs/api-tiers.md guard)...");
        failures += RunSuite(()=>RunApi(),"RunApi");

        Console.WriteLine("FerriteLib neutrality (no product vocabulary anywhere)...");
        failures += RunSuite(()=>FerriteLibNeutralityTests.RunAll(),"FerriteLibNeutralityTests.RunAll");

        Console.WriteLine("Kernel leaf atoms (wrapped text / button / rule / slider / number field)...");
        failures += RunSuite(()=>KernelAtomTests.RunAll(),"KernelAtomTests.RunAll");

        Console.WriteLine("Kernel resolved style table (tone x emphasis, one funnel)...");
        failures += RunSuite(()=>KernelResolvedStyleTests.RunAll(),"KernelResolvedStyleTests.RunAll");

        Console.WriteLine("Kernel element identity (stable identity + per-element state)...");
        failures += RunSuite(()=>KernelIdentityTests.RunAll(),"KernelIdentityTests.RunAll");

        Console.WriteLine("Kernel writability (read-only bindings drive the disabled treatment)...");
        failures += RunSuite(()=>KernelWritabilityTests.RunAll(),"KernelWritabilityTests.RunAll");

        Console.WriteLine("Kernel invalidation (per-key announce, class, batch commit)...");
        failures += RunSuite(()=>KernelInvalidationTests.RunAll(),"KernelInvalidationTests.RunAll");

        Console.WriteLine("Kernel command state (canExecute, one disabled interaction)...");
        failures += RunSuite(()=>KernelCommandStateTests.RunAll(),"KernelCommandStateTests.RunAll");

        Console.WriteLine("Kernel visibility (Visible/VisibleBind, identity, structural prune)...");
        failures += RunSuite(()=>KernelVisibilityTests.RunAll(),"KernelVisibilityTests.RunAll");

        Console.WriteLine("Kernel role attributes (Tone/Emphasis, fallback recording, density)...");
        failures += RunSuite(()=>KernelRoleAttributeTests.RunAll(),"KernelRoleAttributeTests.RunAll");

        Console.WriteLine("Kernel style documents (two origins, precedence chain, fail-soft recording)...");
        failures += RunSuite(()=>KernelStyleDocumentTests.RunAll(),"KernelStyleDocumentTests.RunAll");

        Console.WriteLine("Kernel style scopes (region theme in the live tree, page level, drop visibility)...");

        Console.WriteLine("Kernel stub coverage (game members the harness must carry, trip guard)...");
        failures += RunSuite(()=>KernelStubCoverageTests.RunAll(),"KernelStubCoverageTests.RunAll");

        Console.WriteLine("Kernel documents (file sources, dependencies, atomic reload, last-known-good)...");
        failures += RunSuite(()=>KernelDocumentReloadTests.RunAll(),"KernelDocumentReloadTests.RunAll");

        Console.WriteLine("Kernel diagnostics (per-host subscriptions, attribution, budgets, reload/fit/recovery)...");
        failures += RunSuite(()=>KernelDiagnosticsTests.RunAll(),"KernelDiagnosticsTests.RunAll");

        Console.WriteLine("Kernel keyed repeater (item scope, key reuse, removal cleanup, template contract)...");
        failures += RunSuite(()=>KernelRepeatTests.RunAll(),"KernelRepeatTests.RunAll");
        failures += RunSuite(()=>KernelAssemblyTests.RunAll(),"KernelAssemblyTests.RunAll");
        failures += RunSuite(()=>KernelObservationTests.RunAll(),"KernelObservationTests.RunAll");

        Console.WriteLine("Kernel common controls (checkbox / progress / tree contracts)...");
        failures += RunSuite(()=>KernelControlKindTests.RunAll(),"KernelControlKindTests.RunAll");

        Console.WriteLine("Kernel neutral fixture page (data-driven rows + the new controls, library fixture only)...");
        failures += RunSuite(()=>KernelFixturePageTests.RunAll(),"KernelFixturePageTests.RunAll");

        Console.WriteLine("Kernel ordinary-settings recipe (B: public-only authoring, explicit notify, lifecycle)...");
        failures += RunSuite(()=>KernelOrdinarySettingsRecipeTests.RunAll(),"KernelOrdinarySettingsRecipeTests.RunAll");

        // --- T2 automatic reload scheduling (owner: reload) - one contiguous block; ---- //
        // --- mvvm and catalog add their own blocks and the Lead resolves the merge. ----- //
        Console.WriteLine("Kernel reload scheduling (quiet period, bounded retry, pause independence, deferral)...");
        failures += RunSuite(()=>KernelReloadSchedulingTests.RunAll(),"KernelReloadSchedulingTests.RunAll");
        // ------------------------------------------------------------------------------- //

        // ---- T1 MVVM / page lifecycle lanes (owner: mvvm) - begin -----------------------------
        Console.WriteLine("Kernel notification adapter (explicit mapping, bounded batch, unsubscribe, thread refusal)...");
        failures += RunSuite(()=>KernelNotifyAdapterTests.RunAll(),"KernelNotifyAdapterTests.RunAll");

        Console.WriteLine("Kernel page lifecycle (attach/detach order, reload invariance, the page-level door)...");
        failures += RunSuite(()=>KernelPageLifecycleTests.RunAll(),"KernelPageLifecycleTests.RunAll");
        // ---- T1 MVVM / page lifecycle lanes (owner: mvvm) - end -------------------------------

        // --- T3 widget catalogue (owner: catalog) - one contiguous block, merge-conflict anchor ---
        Console.WriteLine("Kernel widget catalogue (read-only description, scope-preserving identity, no factories)...");
        failures += RunSuite(()=>KernelWidgetCatalogTests.RunAll(),"KernelWidgetCatalogTests.RunAll");
        // --- end T3 widget catalogue ---

        Console.WriteLine("Kernel architecture probes (headless button / two visuals / live resize)...");
        failures += RunSuite(()=>KernelArchitectureProbeTests.RunAll(),"KernelArchitectureProbeTests.RunAll");

        // ---- Batch 1 (0.7.x) lanes. Owners: layout (placement + density), tone. ---------------
        // ---- The Lead wires these; each owner's block is contiguous and merge-safe. ----------
        Console.WriteLine("Kernel placement (Overlay matrix, refusal matrix, flow boundary, envelope)...");
        failures += RunSuite(()=>KernelPlacementTests.RunAll(),"KernelPlacementTests.RunAll");

        // ---- Batch 1 / tone - begin ----------------------------------------------------------
        Console.WriteLine("Kernel tone vocabulary (four authored meanings, state redirects, one accent)...");
        failures += RunSuite(()=>KernelToneVocabularyTests.RunAll(),"KernelToneVocabularyTests.RunAll");
        // ---- Batch 1 / tone - end ------------------------------------------------------------

        // ---- Batch 1 / independent verification (owner: verify) -------------------------------
        Console.WriteLine("Kernel Batch 1 verification (independent probe: tone, density, placement)...");
        failures += RunSuite(()=>KernelBatch1VerificationTests.RunAll(),"KernelBatch1VerificationTests.RunAll");
        // ---- Batch 1 / verify - end ----------------------------------------------------------

        // ---- 0.7.x additions (maintainer ruling 2026-09-20): additions land inside 0.7.0 --------
        Console.WriteLine("Kernel text field (B7: identity, draft, the commit rule, placeholder)...");
        failures += RunSuite(()=>KernelTextFieldTests.RunAll(),"KernelTextFieldTests.RunAll");

        Console.WriteLine("Kernel mode-row hover help (per-option identity published, never painted)...");
        failures += RunSuite(()=>KernelModeRowHelpTests.RunAll(),"KernelModeRowHelpTests.RunAll");

        Console.WriteLine("Kernel element help (engine-wide HelpRef claimed on hover)...");
        failures += RunSuite(()=>KernelElementHelpTests.RunAll(),"KernelElementHelpTests.RunAll");

        Console.WriteLine("Kernel mode-row localization (TitleKeyN through the translation seam)...");
        failures += RunSuite(()=>KernelModeRowLocalizationTests.RunAll(),"KernelModeRowLocalizationTests.RunAll");

        Console.WriteLine("Kernel option help (the shared HoveredOptionHelpOutputBind contract, mode-row and dropdown)...");
        failures += RunSuite(()=>KernelOptionHelpTests.RunAll(),"KernelOptionHelpTests.RunAll");

        Console.WriteLine("Kernel container Tab (the coherence fix: the contract now admits the engine's read)...");
        failures += RunSuite(()=>KernelContainerTabTests.RunAll(),"KernelContainerTabTests.RunAll");
        // ---- end 0.7.x additions ------------------------------------------------------------------

        // ---- 0.7.x height axis (owner: fl-dev) - one contiguous block ---------------------------
        Console.WriteLine("Kernel content height (Height=MatchContent: the reference, its matrix, its degradation)...");
        failures += RunSuite(()=>KernelContentHeightTests.RunAll(),"KernelContentHeightTests.RunAll");
        // ----------------------------------------------------------------------------------------

        Console.WriteLine("Kernel section header (the chrome a manifest can now turn off)...");
        failures += RunSuite(()=>KernelSectionHeaderTests.RunAll(),"KernelSectionHeaderTests.RunAll");

        // ---- 0.7.x development instrument (owner: fl-dev) - one contiguous block ----------------------
        Console.WriteLine("Kernel dev geometry instrument (numeric rects, height mode, viewport, press verdicts)...");
        failures += RunSuite(()=>KernelDevGeometryTests.RunAll(),"KernelDevGeometryTests.RunAll");
        // ----------------------------------------------------------------------------------------------

        Console.WriteLine("Kernel lane registration (every lane file is invoked from Program.cs)...");
        failures += RunSuite(()=>KernelLaneRegistrationTests.RunAll(),"KernelLaneRegistrationTests.RunAll");
    }
}
