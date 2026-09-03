using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Registry and manifest lanes migrated out of the deleted legacy Program.cs suite: core-scope
/// fallback with exact-scope precedence, duplicate-registration rejection, the unknown-kind
/// exception payload, case-insensitive attribute lookup, and the unsafe-XML / legacy-schema
/// rejections that the Schema=2 parser still enforces.
/// </summary>
internal static class KernelRegistryManifestTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        VerifyCoreScopeFallbackAndExactScopeWins();
        VerifyAttributeSchemaFallsBackToCore();
        VerifyDuplicateRegistrationRejected();
        VerifyUnknownKindCarriesScopeAndKind();
        VerifyManifestAttributesAreCaseInsensitive();
        VerifyManifestRejectsUnsafeXml();
        VerifyManifestRejectsLegacySchema();
        UiWidgetRegistry.Clear();
        return failures;
    }

    private static void VerifyCoreScopeFallbackAndExactScopeWins()
    {
        UiWidgetRegistry.Clear();

        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, "test/probe",
            () => new ProbeWidget("core"));

        IUiWidget throughCore = UiWidgetRegistry.Resolve("consumer-scope", "test/probe");
        Check(throughCore.Kind == "core", "core kind resolves through another scope");

        UiWidgetRegistry.Register("consumer-scope", "test/probe",
            () => new ProbeWidget("exact"));

        IUiWidget exact = UiWidgetRegistry.Resolve("consumer-scope", "test/probe");
        IUiWidget other = UiWidgetRegistry.Resolve("third-scope", "test/probe");
        Check(exact.Kind == "exact", "exact scope wins over core fallback");
        Check(other.Kind == "core", "other scopes still fall back to core");
    }

    private static void VerifyAttributeSchemaFallsBackToCore()
    {
        UiWidgetRegistry.Clear();

        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, "test/schema-probe",
            () => new ProbeWidget("schema"), new[] { "Height", "Label" });

        IReadOnlyCollection<string>? schema = UiWidgetRegistry.GetAttributeSchema(
            "consumer-scope", "test/schema-probe");
        Check(schema != null, "core attribute schema is visible from another scope");
        if (schema != null)
        {
            bool hasHeight = false;
            foreach (string name in schema)
            {
                if (string.Equals(name, "Height", StringComparison.OrdinalIgnoreCase)) hasHeight = true;
            }

            Check(hasHeight, "core schema contents survive the scope fallback");
        }

        Check(UiWidgetRegistry.GetAttributeSchema("consumer-scope", "test/unregistered") == null,
            "unknown kind reports no schema");
    }

    private static void VerifyDuplicateRegistrationRejected()
    {
        UiWidgetRegistry.Clear();

        UiWidgetRegistry.Register("dupe-scope", "test/probe", () => new ProbeWidget("first"));
        try
        {
            UiWidgetRegistry.Register("dupe-scope", "test/probe", () => new ProbeWidget("second"));
            Check(false, "duplicate registration should throw");
        }
        catch (InvalidOperationException ex)
        {
            Check(ex.Message.Contains("dupe-scope") && ex.Message.Contains("test/probe"),
                "duplicate registration names the scope and kind");
        }
    }

    private static void VerifyUnknownKindCarriesScopeAndKind()
    {
        UiWidgetRegistry.Clear();

        try
        {
            UiWidgetRegistry.Resolve("some-scope", "missing/kind");
            Check(false, "unknown kind should throw");
        }
        catch (UiUnknownWidgetKindException ex)
        {
            Check(ex.Scope == "some-scope" && ex.Kind == "missing/kind",
                "UiUnknownWidgetKindException carries scope and kind");
        }
    }

    private static void VerifyManifestAttributesAreCaseInsensitive()
    {
        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"a\" Kind=\"test/fixed\" Height=\"74\" />"
            + "</UiPage>");

        CheckEqual("test", manifest.Source, "manifest source parsed");
        CheckEqual("2", manifest.SchemaVersion, "manifest schema version parsed");
        Check(manifest.Roots.Count == 1, "manifest root count");
        Check(manifest.Roots[0].Id == "a" && manifest.Roots[0].Kind == "test/fixed",
            "manifest root id and kind parsed");
        Check(manifest.Roots[0].TryGetAttribute("height", out string height) && height == "74",
            "attributes are readable case-insensitively");
    }

    private static void VerifyManifestRejectsUnsafeXml()
    {
        string xxe = "<!DOCTYPE UiPage [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]>"
            + "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"a\" Kind=\"test/fixed\" Label=\"&xxe;\" /></UiPage>";

        CheckThrows(() => UiLayoutManifest.Parse(xxe),
            ex => ex is FormatException,
            "FormatException",
            "XXE DOCTYPE is rejected");

        CheckThrows(
            () => UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"test\"><Widget Id=\"a\" Kind=\"test/fixed\"></UiPage>"),
            ex => ex is FormatException,
            "FormatException",
            "malformed XML is rejected");
    }

    private static void VerifyManifestRejectsLegacySchema()
    {
        CheckThrows(
            () => UiLayoutManifest.Parse("<UiPage Schema=\"1\" Source=\"test\"></UiPage>"),
            ex => ex is FormatException && ex.Message.Contains("Schema '1'"),
            "FormatException naming the rejected schema",
            "legacy Schema=1 manifests are rejected outright (old manifest format cannot return)");

        CheckThrows(
            () => UiLayoutManifest.Parse("<UiPage Source=\"test\"></UiPage>"),
            ex => ex is FormatException,
            "FormatException",
            "a missing Schema attribute is rejected");
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

    private static void CheckEqual<T>(T expected, T actual, string name)
    {
        if (Equals(expected, actual))
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " (expected '" + expected + "', got '" + actual + "')");
        }
    }

    private static void CheckThrows(Action action, Func<Exception, bool> isExpected, string expectedName, string name)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (isExpected(ex))
            {
                Console.WriteLine("  ok: " + name);
            }
            else
            {
                failures++;
                Console.Error.WriteLine("  FAIL: " + name + " (expected " + expectedName + ", got "
                    + ex.GetType().FullName + " :: " + ex.Message + ")");
            }

            return;
        }

        failures++;
        Console.Error.WriteLine("  FAIL: " + name + " (no exception thrown)");
    }

    private sealed class ProbeWidget : IUiWidget
    {
        public ProbeWidget(string kind)
        {
            Kind = kind;
        }

        public string Kind { get; }

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 10f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }
}
