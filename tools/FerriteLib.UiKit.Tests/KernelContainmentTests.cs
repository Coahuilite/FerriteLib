using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Backend-containment gate (FL→US round 1, item B). The library's claim is that raw IMGUI/Verse
/// contact is funnelled, not forbidden-by-hope: exactly a handful of files may touch the game's
/// immediate-mode surface, and everything else must go through them. Until this lane existed that
/// rule lived in comments, which is how <c>LineChartWidget</c> ended up calling
/// <c>Event.current.Use()</c> and <c>StepperSliderWidget</c> a private copy of the label renderer —
/// both invisible to every other gate.
/// <para>
/// The check is a file allowlist plus a per-file symbol allowlist, so a funnel file cannot quietly
/// grow a new kind of contact either, and it is paired with a positive control that plants violations
/// into a scratch copy: a scan that finds nothing because a path moved is the failure class this
/// repository has already been burned by twice (see <c>MEMORY.md</c>, "Neutrality lane").
/// </para>
/// </summary>
internal static class KernelContainmentTests
{
    /// <summary>
    /// Qualified member accesses that count as raw backend contact.
    /// <para>
    /// The optional <c>UnityEngine.</c> / <c>Verse.</c> prefix is what closes the shape a mutation
    /// test found: <c>Verse.Mouse.IsOver(rect)</c> is the same contact as <c>Mouse.IsOver(rect)</c>,
    /// and a scanner that only matches the short form is a gate you can walk under by typing the
    /// namespace. Bare <c>Widgets.X</c> is deliberately NOT contact — that is this library's own
    /// <c>Kernel.Widgets</c> namespace; it counts only behind <c>Verse.</c>, where it is the real
    /// <c>Verse.Widgets</c> class.
    /// </para>
    /// <para>
    /// <c>GenMapUI</c> is in the owner set because this lane and the rule (c) scan a consumer runs over
    /// its own tree (<c>tools/dependency-reality.ps1</c>) are two halves of one metric: the owner set is
    /// shared vocabulary, so a term ratified on either half is added to both or the halves stop
    /// describing one boundary. In this tree the term can only ever be a violation - world-space
    /// rendering is a permanent non-goal here, so the library has no legitimate call to the in-world
    /// labeling surface and no allowance is filed for it - while a consumer that keeps an in-world
    /// marker declares it in its own allowlist, counted and then exempted rather than invisible.
    /// </para>
    /// </summary>
    private static readonly Regex BackendAccess = new Regex(
        @"(?<![A-Za-z0-9_.])(?<qual>UnityEngine\s*\.\s*|Verse\s*\.\s*)?(?<owner>GUI|GUIUtility|GenMapUI|Mouse|Text|VerseWidgets|Widgets|Event)\s*\.\s*(?<member>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The funnel. Each entry is a file that legitimately owns backend contact, with the exact
    /// qualified names it is allowed to reach. Adding a symbol here is the only way to widen the
    /// boundary, and every entry must be justified by what that file is for.
    /// </summary>
    private static readonly Dictionary<string, string[]> Allowed = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        // The IMGUI interop seam: the library's only input authority.
        ["UiNative.cs"] = new[]
        {
            "Event.current",
            "GUIUtility.GetControlID",
            "GUIUtility.hotControl",
            "Mouse.IsOver",
            "VerseWidgets.ButtonInvisible",
            "VerseWidgets.HorizontalSlider",
            "VerseWidgets.TextField",
        },
        // The drawing outlet: everything that paints or writes a string goes through here.
        ["UiThemeDraw.cs"] = new[]
        {
            "GUI.color",
            "Text.Anchor",
            "Text.Font",
            "VerseWidgets.DrawBoxSolid",
            "VerseWidgets.Label",
        },
        // The engine owns structural scopes only — no painting, no input, no text.
        ["UiLayoutEngine.cs"] = new[]
        {
            "GUI.BeginGroup",
            "GUI.EndGroup",
            "VerseWidgets.BeginScrollView",
            "VerseWidgets.EndScrollView",
        },
        // The recovery guard must read and restore the two global states a throwing widget can leave dirty.
        ["UiSessionGuard.cs"] = new[]
        {
            "GUI.color",
            "Text.Font",
        },
        // Measurement is the one visual-core job that cannot be expressed without the text engine.
        ["VerseFerriteTextMetrics.cs"] = new[]
        {
            "Text.Anchor",
            "Text.CalcHeight",
            "Text.CalcSize",
            "Text.Font",
            "Text.WordWrap",
        },
    };

    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Backend contact stays inside the funnel files", VerifySourceIsContained);
        failures += Run("Containment scan fires on a planted violation", VerifyGateIsNotVacuous);
        failures += Run("Funnel allowlist has no dead entries", VerifyAllowlistIsStillLoadBearing);
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

    private static void VerifySourceIsContained()
    {
        List<string> violations = Scan(Path.Combine(RepoRoot(), "Source", "FerriteLib.UiKit"));
        if (violations.Count > 0)
        {
            throw new Exception(
                violations.Count + " raw backend contact(s) outside the funnel:\n    "
                + string.Join("\n    ", violations));
        }
    }

    /// <summary>
    /// Positive control. Three shapes have to fail: contact in a file nobody allowed, an unlisted
    /// symbol inside a file that is allowed, and the fully-qualified alias form. A gate that only
    /// caught the first would let the funnel files absorb the whole backend one member at a time.
    /// </summary>
    private static void VerifyGateIsNotVacuous()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "ferritelib-containment-" + Guid.NewGuid().ToString("N"));
        try
        {
            string tree = Path.Combine(sandbox, "Kernel");
            Directory.CreateDirectory(tree);

            // A widget-shaped file: not allowlisted at all.
            File.WriteAllText(
                Path.Combine(tree, "PlantedWidget.cs"),
                "class PlantedWidget { void Draw() { Verse.Widgets.Label(default, \"x\"); } }");

            // An allowlisted filename reaching for a symbol its entry does not grant.
            File.WriteAllText(
                Path.Combine(tree, "UiNative.cs"),
                "class PlantedNative { void Read() { GUI.Box(default, \"x\"); } }");

            // The aliased form every real call site uses.
            File.WriteAllText(
                Path.Combine(tree, "PlantedAlias.cs"),
                "class PlantedAlias { void Read() { bool b = Mouse.IsOver(default); } }");

            // The shared in-world term: this half of the metric must count it too, or the two halves
            // would disagree about what the boundary is.
            File.WriteAllText(
                Path.Combine(tree, "PlantedWorldLabel.cs"),
                "class PlantedWorldLabel { void Mark() { GenMapUI.DrawPawnLabel(default, \"x\", default); } }");

            List<string> violations = ScanWithRoot(tree, tree);
            Expect(violations, "PlantedWidget.cs", "VerseWidgets.Label");
            Expect(violations, "UiNative.cs", "GUI.Box");
            Expect(violations, "PlantedAlias.cs", "Mouse.IsOver");
            Expect(violations, "PlantedWorldLabel.cs", "GenMapUI.DrawPawnLabel");

            // Comment-only contact must not fire: the funnel files document the very calls they own,
            // and a gate that counted <see cref> text would be worked around by deleting the docs.
            File.WriteAllText(
                Path.Combine(tree, "PlantedComment.cs"),
                "// see VerseWidgets.Label\n/// <summary>GUI.color</summary>\nclass PlantedComment { }");
            if (ScanWithRoot(tree, tree).Exists(v => v.IndexOf("PlantedComment.cs", StringComparison.Ordinal) >= 0))
            {
                throw new Exception("Comment text was reported as backend contact; the scan is not comment-aware.");
            }
        }
        finally
        {
            try { Directory.Delete(sandbox, true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// Every allowance must still be used by the file it names. An entry that no longer fires is not
    /// harmless: it is a hole kept open for a call site that no longer exists, and it is how a
    /// boundary quietly becomes a formality.
    /// </summary>
    private static void VerifyAllowlistIsStillLoadBearing()
    {
        string kernel = Path.Combine(RepoRoot(), "Source", "FerriteLib.UiKit", "Kernel");
        foreach (KeyValuePair<string, string[]> entry in Allowed)
        {
            string path = Path.Combine(kernel, entry.Key);
            if (!File.Exists(path))
            {
                throw new Exception("Funnel file vanished from the allowlist: " + entry.Key);
            }

            HashSet<string> found = Collect(File.ReadAllLines(path));
            foreach (string symbol in entry.Value)
            {
                if (!found.Contains(symbol))
                {
                    throw new Exception(
                        entry.Key + " no longer reaches '" + symbol
                        + "'; drop the allowance instead of keeping an unused hole in the boundary.");
                }
            }
        }
    }

    private static List<string> Scan(string root) => ScanWithRoot(root, root);

    /// <summary>
    /// Scans <paramref name="tree"/> and reports paths relative to <paramref name="root"/>, so the
    /// positive control can keep the real repository out of its results while naming files the same
    /// way the production scan does.
    /// </summary>
    private static List<string> ScanWithRoot(string root, string tree)
    {
        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(tree, "*.cs", SearchOption.AllDirectories))
        {
            if (IsGenerated(file)) continue;

            string name = Path.GetFileName(file);
            string[] allowed = Allowed.TryGetValue(name, out string[]? listed) ? listed : Array.Empty<string>();
            foreach (string symbol in Collect(File.ReadAllLines(file)))
            {
                if (Array.IndexOf(allowed, symbol) < 0)
                {
                    violations.Add(Relative(root, file) + ": " + symbol);
                }
            }
        }

        violations.Sort(StringComparer.Ordinal);
        return violations;
    }

    private static HashSet<string> Collect(string[] lines)
    {
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            // Whole-line comments only: a trailing comment after code still counts as its code line,
            // because the code is what binds. Leading whitespace is walked by hand — the harness
            // runtime lacks the parameterless TrimStart the reference assembly advertises.
            int first = 0;
            while (first < line.Length && char.IsWhiteSpace(line[first])) first++;
            if (first + 1 < line.Length && line[first] == '/' && line[first + 1] == '/') continue;

            foreach (Match match in BackendAccess.Matches(line))
            {
                string owner = match.Groups["owner"].Value;
                string member = match.Groups["member"].Value;
                bool verseQualified = match.Groups["qual"].Value.IndexOf("Verse", StringComparison.Ordinal) >= 0;

                if (owner == "Widgets")
                {
                    // Bare Widgets.X is this library's own namespace; only Verse.Widgets is contact.
                    if (verseQualified) symbols.Add("VerseWidgets." + member);
                    continue;
                }

                symbols.Add(owner + "." + member);
            }
        }

        return symbols;
    }

    private static void Expect(List<string> violations, string fileName, string symbol)
    {
        string expected = fileName + ": " + symbol;
        if (!violations.Exists(v => v.EndsWith(expected, StringComparison.Ordinal)))
        {
            throw new Exception(
                "Planted violation not reported: " + expected + " (got " + violations.Count + " violation(s)).");
        }
    }

    /// <summary>
    /// Root-relative display path. Hand-rolled because <c>Path.GetRelativePath</c> is advertised by
    /// the net472 reference assembly and missing at runtime — the trap class <c>MEMORY.md</c> records
    /// for <c>string.Split(char, StringSplitOptions)</c>.
    /// </summary>
    private static string Relative(string root, string file)
    {
        if (file.StartsWith(root, StringComparison.Ordinal))
        {
            string trimmed = file.Substring(root.Length);
            while (trimmed.Length > 0
                && (trimmed[0] == Path.DirectorySeparatorChar || trimmed[0] == Path.AltDirectorySeparatorChar))
            {
                trimmed = trimmed.Substring(1);
            }

            return trimmed.Length > 0 ? trimmed : Path.GetFileName(file);
        }

        return file;
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
            if (Directory.Exists(Path.Combine(current, "Source", "FerriteLib.UiKit")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        throw new DirectoryNotFoundException("Could not locate repository root from " + AppContext.BaseDirectory);
    }
}
