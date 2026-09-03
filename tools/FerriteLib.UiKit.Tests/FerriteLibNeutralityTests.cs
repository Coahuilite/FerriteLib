using System;
using System.Collections.Generic;
using System.IO;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Neutrality gate for the whole repository. FerriteLib is a prerequisite shared by several mods, so
/// the rule is stricter than "no product brand in the source text": a product literal that only shows
/// up when the comparison is case-insensitive still means some future consumer cannot build against
/// this library without inheriting someone else's vocabulary.
/// <para>
/// <c>coahuilite</c> is allowed on purpose - it is the series namespace and the carrier's own packageId
/// author segment, not a product.
/// </para>
/// </summary>
internal static class FerriteLibNeutralityTests
{
    private static readonly string[] Trees =
    {
        "Source/FerriteLib.UiKit",
        "tools/FerriteLib.UiKit.Tests"
    };

    // Case-insensitive product vocabulary. Any hit means the library or its harness knows a consumer.
    private static readonly string[] ProductWords =
    {
        "universalsqueaker",
        "squeakyratkin",
        "ratkin",
        "kiiro",
        "squeak",
        "voicepack"
    };

    // Case-sensitive identifier prefixes reserved for products. US_ also catches AUS_xxx on purpose;
    // that is the documented cost of the prefix convention, so no library identifier may contain it.
    private static readonly string[] ProductPrefixes =
    {
        "UniversalSqueaker",
        "SqueakyRatkin",
        "Ratkin",
        "Kiiro",
        "SR_",
        "US_"
    };

    /// <summary>
    /// Exactly one file is exempt: this one, because the lists above are the forbidden strings and a
    /// scanner cannot both name them and not find them. VerifyGateScopeIsOneFile pins the exemption so
    /// it can never quietly widen.
    /// </summary>
    private const string SelfExcludedFileName = "FerriteLibNeutralityTests.cs";

    public static int RunAll()
    {
        int failures = 0;
        failures += Run("No product vocabulary in the library or its harness", VerifyTreesAreNeutral);
        failures += Run("Gate fires on a planted literal in every scanned tree", VerifyGateIsNotVacuous);
        failures += Run("Exemption is pinned to this one file", VerifyGateScopeIsOneFile);
        return failures;
    }

    private static int Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.Message);
            return 1;
        }
    }

    private static void VerifyTreesAreNeutral()
    {
        List<string> hits = Scan(RepoRoot());
        if (hits.Count > 0)
        {
            throw new Exception("Neutrality violation(s):\n" + string.Join("\n", hits));
        }
    }

    /// <summary>
    /// Positive control. A scan that silently finds nothing - because a path moved, a filter widened,
    /// or an exception got swallowed - is the failure mode this library's parent project has already
    /// been burned by twice, so the gate has to prove it can fire before it is allowed to report clean.
    /// Both trees are seeded: a control that only exercises one of them cannot notice the other
    /// disappeared.
    /// </summary>
    private static void VerifyGateIsNotVacuous()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "ferritelib-neutrality-" + Guid.NewGuid().ToString("N"));
        try
        {
            // One planted word case-insensitively, one planted prefix case-sensitively.
            Seed(sandbox, Trees[0], "VoicePack");
            Seed(sandbox, Trees[1], "US_Voice");

            List<string> hits = Scan(sandbox);

            foreach (string tree in Trees)
            {
                int inTree = 0;
                foreach (string hit in hits)
                {
                    if (hit.IndexOf(tree, StringComparison.Ordinal) >= 0) inTree++;
                }

                if (inTree == 0)
                {
                    throw new Exception("Planted literals in " + tree + " were not reported; that tree is not being scanned.");
                }
            }

            bool sawWord = false;
            bool sawPrefix = false;
            foreach (string hit in hits)
            {
                if (hit.IndexOf("product word", StringComparison.Ordinal) >= 0) sawWord = true;
                if (hit.IndexOf("product prefix", StringComparison.Ordinal) >= 0) sawPrefix = true;
            }

            if (!sawWord || !sawPrefix)
            {
                throw new Exception("Both the case-insensitive word list and the case-sensitive prefix list must fire.");
            }

            // The generated-code exclusion must not be a hole that swallows real files.
            string objDir = Path.Combine(sandbox, Trees[0], "obj");
            Directory.CreateDirectory(objDir);
            File.WriteAllText(Path.Combine(objDir, "Generated.cs"), "class G { const string B = \"ratkin\"; }");
            int after = Scan(sandbox).Count;
            if (after != hits.Count)
            {
                throw new Exception("obj/ handling changed the hit count (" + hits.Count + " -> " + after + ")");
            }
        }
        finally
        {
            try { Directory.Delete(sandbox, true); } catch (IOException) { }
        }
    }

    private static void Seed(string sandbox, string relativeTree, string productLiteral)
    {
        string tree = Path.Combine(sandbox, relativeTree);
        Directory.CreateDirectory(tree);
        File.WriteAllText(
            Path.Combine(tree, "Planted.cs"),
            "class Planted { const string X = \"" + productLiteral + "\"; }");
    }

    /// <summary>
    /// The exemption above is the only way this gate can be made to miss a file. Pin it: if anyone adds
    /// a second exempt name, or exempts something other than the scanner itself, the gate has been
    /// weakened and must say so.
    /// </summary>
    private static void VerifyGateScopeIsOneFile()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "ferritelib-scope-" + Guid.NewGuid().ToString("N"));
        try
        {
            // A file bearing the exempt name in a *different* tree must still be scanned, so the
            // exemption is one path and not a name pattern that spreads.
            string other = Path.Combine(sandbox, Trees[1]);
            Directory.CreateDirectory(other);
            Directory.CreateDirectory(Path.Combine(sandbox, Trees[0]));
            File.WriteAllText(Path.Combine(other, SelfExcludedFileName), "class X { const string B = \"ratkin\"; }");
            if (Scan(sandbox).Count == 0)
            {
                throw new Exception("The exemption matched by file name alone; it must be the scanner file, not any file with that name.");
            }
        }
        finally
        {
            try { Directory.Delete(sandbox, true); } catch (IOException) { }
        }
    }

    private static List<string> Scan(string root)
    {
        var hits = new List<string>();
        string selfPath = Path.GetFullPath(
            Path.Combine(RepoRoot(), "tools", "FerriteLib.UiKit.Tests", SelfExcludedFileName));

        foreach (string relativeTree in Trees)
        {
            string tree = Path.Combine(root, relativeTree);
            if (!Directory.Exists(tree))
            {
                // A missing tree means the repository layout moved, not that the code is clean.
                throw new Exception("Neutrality tree is gone: " + relativeTree);
            }

            foreach (string file in Directory.EnumerateFiles(tree, "*.cs", SearchOption.AllDirectories))
            {
                if (IsGenerated(file)) continue;
                if (string.Equals(Path.GetFullPath(file), selfPath, StringComparison.OrdinalIgnoreCase)) continue;

                string text = File.ReadAllText(file);
                string lower = text.ToLowerInvariant();
                foreach (string word in ProductWords)
                {
                    if (lower.IndexOf(word, StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(file + " contains product word '" + word + "'");
                    }
                }

                foreach (string prefix in ProductPrefixes)
                {
                    if (text.IndexOf(prefix, StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(file + " contains product prefix '" + prefix + "'");
                    }
                }
            }
        }

        return hits;
    }

    private static bool IsGenerated(string path)
    {
        string normalized = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        return normalized.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal) >= 0
            || normalized.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) >= 0;
    }

    private static string RepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && current != null; i++)
        {
            if (Directory.Exists(Path.Combine(current, "Source", "FerriteLib.UiKit"))
                && File.Exists(Path.Combine(current, "About", "About.xml")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        throw new DirectoryNotFoundException("Could not locate the FerriteLib repo root from " + AppContext.BaseDirectory);
    }
}
