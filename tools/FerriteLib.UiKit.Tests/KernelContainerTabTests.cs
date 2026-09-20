using System;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The container-side <c>Tab</c> contract, and the coherence fix that closed it. The engine always read
/// <c>Tab</c> when deciding visibility — the same <c>IsHidden</c> that reads <c>NarrowHidden</c> and
/// <c>Hidden</c> — but the container attribute contract omitted the name, so the read was unreachable for a
/// container and a page could not declare one to appear on a single tab. The fix is two words in two mirrored
/// lists; this lane is what keeps the omission from returning.
/// <para>
/// What it pins, in the order the ruling states it: the declaration now creates (and a container attribute
/// outside the contract is still refused, so the check is not vacuous); a <c>Tab</c>-gated container takes its
/// whole <b>subtree</b> with it and comes back on a switch; the announcement path works on the container,
/// because <c>RecordDeclaredKeys</c> already registered the shared active-tab key for any declarer — that
/// plumbing being container-inclusive while the contract was not is exactly why this is a coherence fix
/// rather than new surface; and a hidden container keeps its node and its state, like every other hidden
/// element. The last lane reads both lists by reflection, which is the direct pin of the two changed lines.
/// </para>
/// </summary>
internal static class KernelContainerTabTests
{
    private const string Scope = "container-tab-lane";
    private const string ProbeKind = "test/tab-probe";

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("A Tab-gated container is part of the creation-time contract", VerifyContainerTabCreates);
        Run("A Tab-gated container takes its subtree with it", VerifyContainerTabHidesSubtree);
        Run("Both container lists carry Tab and the same vocabulary", VerifyVocabularyPinned);
        Run("Switching tabs re-arranges in place and the hidden container keeps its node", VerifyInvalidationAndIdentity);
        return failures;
    }

    // --- the contract -------------------------------------------------------------------------------

    private static void VerifyContainerTabCreates()
    {
        Prepare();

        // The unlock: this exact shape was a UiContractException before the fix.
        using UiHost host = Host(
            "<Section Id=\"card\" Title=\"Card\" Tab=\"Packs\">"
            + Child("inside")
            + "</Section>",
            ActiveTab("Packs"));
        Check(true, "a container declaring Tab creates: the engine's read is now reachable for it");

        // The contrast that keeps the check honest: a container attribute genuinely outside the contract is
        // still refused, and the refusal names the attribute.
        Check(Rejects("<Section Id=\"card\" Bind=\"nope\">" + Child("inside") + "</Section>", "Bind"),
            "while an attribute that is not container vocabulary is still refused at creation");
    }

    private static void VerifyContainerTabHidesSubtree()
    {
        Prepare();
        string xml =
            "<Column Id=\"column\" Padding=\"0\" Gap=\"0\">"
            + "<Section Id=\"packs\" Title=\"Packs\" Tab=\"Packs\">" + Child("packs-child") + "</Section>"
            + "<Section Id=\"basic\" Title=\"Basic\" Tab=\"Basic\">" + Child("basic-child") + "</Section>"
            + "</Column>";

        string tab = "Packs";
        var bindings = new UiBindings();
        bindings.BindValue(UiBindings.ActiveTabKey, () => tab, value => tab = value);

        using UiHost host = Host(xml, bindings);
        UiLayoutSnapshot packs = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            packs.RectById.ContainsKey("packs") && packs.RectById.ContainsKey("packs-child")
                && !packs.RectById.ContainsKey("basic") && !packs.RectById.ContainsKey("basic-child"),
            "the gated container and its whole subtree are arranged; the other tab's are not");

        tab = "Basic";
        bindings.NotifyChanged(UiBindings.ActiveTabKey);
        UiLayoutSnapshot basic = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            basic.RectById.ContainsKey("basic") && basic.RectById.ContainsKey("basic-child")
                && !basic.RectById.ContainsKey("packs") && !basic.RectById.ContainsKey("packs-child"),
            "and the switch mirrors it: the subtree goes with its container, both ways");

        tab = "Packs";
        bindings.NotifyChanged(UiBindings.ActiveTabKey);
        UiLayoutSnapshot again = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            again.RectById.ContainsKey("packs") && again.RectById.ContainsKey("packs-child"),
            "switching back restores it with no residue, on the same host (nothing was reopened)");
    }

    /// <summary>
    /// The direct pin of the two changed lines: both mirrored lists must carry <c>Tab</c>, and they must remain
    /// the same vocabulary. <c>KernelRepeatTests</c> already asserts the equality from the outside; reading
    /// the names here is what makes this lane red if either half of the fix is reverted.
    /// </summary>
    private static void VerifyVocabularyPinned()
    {
        HashSet<string> hostContainers = PrivateSet(typeof(UiHost), "ContainerAttributes");
        HashSet<string> engineContainers = PrivateSet(typeof(UiLayoutEngine), "TemplateContainerAttributes");

        Check(hostContainers.Contains("Tab"), "UiHost's container vocabulary carries Tab");
        Check(engineContainers.Contains("Tab"), "and the engine's mirrored list for template subtrees carries it too");
        Check(hostContainers.SetEquals(engineContainers),
            "the two container lists are still the same vocabulary, so the fix cannot half-land again");
    }

    private static void VerifyInvalidationAndIdentity()
    {
        Prepare();
        string tab = "Packs";
        var bindings = new UiBindings();
        bindings.BindValue(UiBindings.ActiveTabKey, () => tab, value => tab = value);

        using UiHost host = Host(
            "<Section Id=\"card\" Title=\"Card\" Tab=\"Packs\">" + Child("inside") + "</Section>",
            bindings);

        host.MeasureAndArrange(new Vector2(200f, 200f));
        UiNode card = host.Session.GetNodeByElementId("card") ?? throw new Exception("the container was not arranged");
        card.GetOrCreateState("draft").EditText = "typed";

        tab = "Other";
        bindings.NotifyChanged(UiBindings.ActiveTabKey);
        UiLayoutSnapshot hidden = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(!hidden.RectById.ContainsKey("card"), "announcing active-tab hides the gated container");

        UiNode? afterHide = host.Session.GetNodeByElementId("card");
        Check(afterHide != null && ReferenceEquals(afterHide, card),
            "and a hidden container keeps its node: hidden is not removed, exactly as for Visible/Hidden");
        Check(afterHide != null && afterHide.ValueStates["draft"].EditText == "typed",
            "so its state survives the hide rather than being handed to another element");

        tab = "Packs";
        bindings.NotifyChanged(UiBindings.ActiveTabKey);
        host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            ReferenceEquals(host.Session.GetNodeByElementId("card"), card)
                && card.ValueStates["draft"].EditText == "typed",
            "and switching back resumes on the same node with the same state");
    }

    // --- helpers ------------------------------------------------------------------------------------

    private static string Child(string id)
    {
        return "<Widget Id=\"" + id + "\" Kind=\"" + ProbeKind + "\" Height=\"12\" />";
    }

    private static UiHost Host(string body, UiBindings bindings)
    {
        return new UiHost(
            Scope,
            UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>"),
            bindings,
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());
    }

    private static UiBindings ActiveTab(string initial)
    {
        string tab = initial;
        var bindings = new UiBindings();
        bindings.BindValue(UiBindings.ActiveTabKey, () => tab, value => tab = value);
        return bindings;
    }

    private static bool Rejects(string body, string expectedInMessage)
    {
        try
        {
            using UiHost host = Host(body, ActiveTab("Packs"));
            return false;
        }
        catch (UiContractException ex)
        {
            return ex.Message.IndexOf(expectedInMessage, StringComparison.Ordinal) >= 0;
        }
    }

    private static void Prepare()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, ProbeKind, () => new ProbeWidget(), new[] { "Id", "Kind", "Height" });
    }

    private static HashSet<string> PrivateSet(Type type, string fieldName)
    {
        FieldInfo? field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null) throw new Exception(type.Name + "." + fieldName + " is gone; re-point this lane deliberately.");
        var set = field.GetValue(null) as HashSet<string>;
        if (set == null) throw new Exception(type.Name + "." + fieldName + " is no longer a HashSet<string>.");
        return set;
    }

    private static int Run(string name, Action action)
    {
        int before = failures;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }

        if (failures == before)
        {
            Console.WriteLine("  ok: " + name);
        }

        return failures - before;
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

    private sealed class StubMetrics : ITextMetrics
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

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key)
        {
            return "[" + key + "]";
        }

        public int TranslationRevision => 0;
    }

    private sealed class ProbeWidget : IUiWidget
    {
        string IUiWidget.Kind => ProbeKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            return 12f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }
}
