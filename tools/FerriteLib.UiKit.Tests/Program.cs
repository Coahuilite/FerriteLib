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

        Console.WriteLine("Kernel role attributes (Tone/Emphasis, fallback recording, density)...");
        failures += KernelRoleAttributeTests.RunAll();

        Console.WriteLine("Kernel style documents (two origins, precedence chain, fail-soft recording)...");
        failures += KernelStyleDocumentTests.RunAll();
    }
}
