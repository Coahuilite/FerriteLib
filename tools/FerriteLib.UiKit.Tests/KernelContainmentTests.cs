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
/// <para>
/// A second family is measured beside it: <see cref="UiNative.Button(Rect)"/>, the context-free hit
/// overload. It is not backend contact - it is the library's own seam - but it is the one call that
/// bypasses the owned hit stack, so a call site that keeps it is the same kind of boundary breach as a
/// raw <c>GUI.</c> call and is counted by the same allowlist discipline. Every library widget passes
/// the context instead (<see cref="UiNative.Button(Rect, UiWidgetContext)"/>); the exception, its
/// contract and its recovery condition are in <c>docs/api-tiers.md</c>, and this lane pins that
/// contract to the measured call sites so the document cannot promise something the tree stopped
/// doing.
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
    /// its own tree (<c>tools/dependency-reality.ps1</c>) are two halves of one metric, and the pattern
    /// set is the one thing they share: a term ratified on either half is added to both, or the halves
    /// stop describing one boundary. The allowlists are not shared - each side rules on its own
    /// exemptions, so a file sanctioned in one tree means nothing in the other. In this tree the term can
    /// only ever be a violation: world-space rendering is a permanent non-goal here, so the library has
    /// no legitimate call to the in-world labeling surface and no allowance is filed for it. A consumer
    /// that keeps an in-world marker declares it in its own allowlist, counted and then exempted rather
    /// than invisible.
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

    /// <summary>
    /// The context-free hit overload, called by name: <c>UiNative.Button(&lt;one argument&gt;)</c>. One
    /// argument is the whole point - the two-argument form passes the context and is the migration the
    /// contract asks for, so a matcher that also reddened it would fail the answer it recommends. The
    /// argument may be a simple expression or an inline construction with its own parentheses
    /// (<c>new Rect(0f, 0f, 1f, 1f)</c>); a top-level comma is what separates the two overloads, so the
    /// pattern accepts nesting and refuses a second argument.
    /// <para>
    /// The shared pattern set with the rule (c) scan a consumer runs
    /// (<c>tools/dependency-reality.ps1</c>) carries this same term, so both halves count one shape; the
    /// allowlists stay each side's own ruling. This matcher reads whole-file code with whole-line comments
    /// blanked, so a call split across lines is still seen; the rule (c) scan works line by line and
    /// documents that narrower reach.
    /// </para>
    /// </summary>
    private static readonly Regex ContextFreeHit = new Regex(
        @"(?<![A-Za-z0-9_.])UiNative\s*\.\s*Button\s*\((?:[^,()]|\([^()]*\))*\)",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The exception in <c>docs/api-tiers.md</c>, in the document's own words. The sentence is a promise
    /// about this tree ("the one library call site left raw"), and a promise no gate reads is a comment:
    /// <see cref="VerifyDocContractMatchesTheMetric"/> fails when the sentence disappears, so rewording it
    /// is a deliberate two-place edit rather than a silent drift.
    /// </summary>
    private const string DocumentedRawCallClaim = "the one library call site left raw";

    /// <summary>
    /// Files allowed to call the context-free hit overload by name. Each entry is one documented
    /// exception, and the value is its ruling: what the file is for, when the exception was recorded and
    /// what retires it. An entry that stops firing is reported by
    /// <see cref="VerifyContextFreeHitAllowanceIsLoadBearing"/>, because a hole kept open for a call site
    /// that no longer exists is how a boundary becomes a formality.
    /// </summary>
    private static readonly Dictionary<string, string> ContextFreeHitAllowed = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // docs/api-tiers.md, "Contract for the context-free overload": the window shell's own chrome
        // button. The chrome draws outside the page tree, so there is no context to hand the overload and
        // no hit layer to arbitrate against - this is the one library call site the contract names.
        // Recorded 2026-09-12. Retire it when the close affordance becomes a tree element (a page or a
        // registered kind), or when the overload itself is removed at a minor boundary.
        ["UiWindowHost.cs"] = "chrome close affordance draws outside the tree; no context and no layer to arbitrate (recorded 2026-09-12)",
    };

    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Backend contact stays inside the funnel files", VerifySourceIsContained);
        failures += Run("Containment scan fires on a planted violation", VerifyGateIsNotVacuous);
        failures += Run("Funnel allowlist has no dead entries", VerifyAllowlistIsStillLoadBearing);
        failures += Run("The context-free hit overload stays inside its allowance", VerifyContextFreeHitIsContained);
        failures += Run("Context-free hit allowance has no dead entries", VerifyContextFreeHitAllowanceIsLoadBearing);
        failures += Run("The documented contract matches the measured call sites", VerifyDocContractMatchesTheMetric);
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
    /// The second family: a named call to the context-free hit overload outside its allowance. The scope
    /// is the same tree the backend scan reads, so one file cannot be contained by one half and loose in
    /// the other.
    /// </summary>
    private static void VerifyContextFreeHitIsContained()
    {
        string tree = SourceTree();
        List<string> violations = ScanContextFree(tree, tree, allowedFiles: false);
        if (violations.Count > 0)
        {
            throw new Exception(
                violations.Count + " context-free hit call(s) outside the allowance:\n    "
                + string.Join("\n    ", violations));
        }
    }

    /// <summary>
    /// Every allowance must still be used. The same rule the funnel allowlist is held to, for the same
    /// reason: an exception nobody uses is a hole kept open, and it is what lets a boundary become a
    /// formality one commit at a time.
    /// </summary>
    private static void VerifyContextFreeHitAllowanceIsLoadBearing()
    {
        string root = SourceTree();
        foreach (KeyValuePair<string, string> entry in ContextFreeHitAllowed)
        {
            string path = Path.Combine(root, "Kernel", entry.Key);
            if (!File.Exists(path))
            {
                throw new Exception("The context-free hit allowance names a file that is gone: " + entry.Key);
            }

            if (CollectContextFreeHits(File.ReadAllLines(path)).Count == 0)
            {
                throw new Exception(
                    entry.Key + " no longer calls the context-free overload; drop the allowance (its stated reason: "
                    + entry.Value + ") instead of keeping an unused hole in the boundary.");
            }
        }
    }

    /// <summary>
    /// The document's promise, measured. <c>docs/api-tiers.md</c> names the exception ("the one library
    /// call site left raw", the window shell's chrome button) and the migration, but before this check no
    /// gate read either: the contract could keep promising one raw call site after the tree had five, or
    /// after somebody reworded it into a different claim. Both directions fail here, so the sentence and
    /// the call sites are one fact written in two places.
    /// </summary>
    private static void VerifyDocContractMatchesTheMetric()
    {
        string doc = Path.Combine(RepoRoot(), "docs", "api-tiers.md");
        if (!File.Exists(doc))
        {
            throw new Exception("The tier document is missing, so the exception this metric measures has no home: " + doc);
        }

        string flattened = Flatten(File.ReadAllText(doc));
        if (flattened.IndexOf("UiNative.Button(Rect)", StringComparison.Ordinal) < 0)
        {
            throw new Exception(
                "docs/api-tiers.md no longer states the context-free overload contract (UiNative.Button(Rect)); "
                + "the metric and the promise have come apart.");
        }

        if (flattened.IndexOf(DocumentedRawCallClaim, StringComparison.Ordinal) < 0)
        {
            throw new Exception(
                "docs/api-tiers.md no longer promises \"" + DocumentedRawCallClaim + "\". That sentence is the "
                + "exception this metric measures, so reword it only together with this check.");
        }

        string tree = SourceTree();
        int measured = ScanContextFree(tree, tree, allowedFiles: true).Count;
        if (measured != 1)
        {
            throw new Exception(
                "docs/api-tiers.md promises exactly one raw call site (the window shell's chrome button); the "
                + "tree has " + measured + ". Either the call site moved or the contract did.");
        }
    }

    /// <summary>
    /// Whole-line comments blanked, newlines kept: a call split across lines is still one call, and the
    /// comment convention stays what the backend scan uses (a trailing comment after code counts as its
    /// code line, because the code is what binds).
    /// </summary>
    private static List<string> CollectContextFreeHits(string[] lines)
    {
        var code = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int first = 0;
            while (first < line.Length && char.IsWhiteSpace(line[first])) first++;
            bool wholeLineComment = first + 1 < line.Length && line[first] == '/' && line[first + 1] == '/';
            if (!wholeLineComment) code.Append(line);
            code.Append('\n');
        }

        var calls = new List<string>();
        foreach (Match match in ContextFreeHit.Matches(code.ToString()))
        {
            calls.Add(match.Value);
        }

        return calls;
    }

    /// <summary>
    /// Call sites of the context-free overload under <paramref name="tree"/>, either outside the allowance
    /// (<paramref name="allowedFiles"/> false) or inside it (true), with paths relative to
    /// <paramref name="root"/> so the positive control can drive the real scanner over a scratch copy.
    /// </summary>
    private static List<string> ScanContextFree(string root, string tree, bool allowedFiles)
    {
        var found = new List<string>();
        foreach (string file in Directory.GetFiles(tree, "*.cs", SearchOption.AllDirectories))
        {
            if (IsGenerated(file)) continue;
            if (ContextFreeHitAllowed.ContainsKey(Path.GetFileName(file)) != allowedFiles) continue;

            foreach (string call in CollectContextFreeHits(File.ReadAllLines(file)))
            {
                found.Add(Relative(root, file) + ": " + call);
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>
    /// The tree both scans read. It is not the repository root: the harness itself contains this lane's
    /// planted samples and the pattern text, so a scanner pointed at the root would report its own
    /// positive control as a violation - which is exactly what the first version did.
    /// </summary>
    private static string SourceTree()
    {
        return Path.Combine(RepoRoot(), "Source", "FerriteLib.UiKit");
    }

    /// <summary>Whitespace collapsed, so a sentence keeps its meaning across a reflow of the document.</summary>
    private static string Flatten(string text)
    {
        string[] words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words);
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

            // The context-free hit overload, called by name: the same boundary, the other half of it.
            File.WriteAllText(
                Path.Combine(tree, "PlantedRawHit.cs"),
                "class PlantedRawHit { void Hit(Rect r) { if (UiNative.Button(r)) { } } }");

            // The same defect with the rect built inline, so the matcher's reach past its own parentheses
            // is tested rather than assumed.
            File.WriteAllText(
                Path.Combine(tree, "PlantedInlineRect.cs"),
                "class PlantedInlineRect { void Hit() { if (UiNative.Button(new Rect(0f, 0f, 1f, 1f))) { } } }");

            // The migration the contract asks for must NOT fire: a matcher that reddens the two-argument
            // form would fail the answer it recommends, and the next reader would widen the allowance.
            File.WriteAllText(
                Path.Combine(tree, "PlantedMigratedHit.cs"),
                "class PlantedMigratedHit { void Hit(Rect r, UiWidgetContext ctx) { if (UiNative.Button(r, ctx)) { } } }");

            // A call split across lines is still one call.
            File.WriteAllText(
                Path.Combine(tree, "PlantedSplitHit.cs"),
                "class PlantedSplitHit { void Hit(Rect r) { if (UiNative.Button(\n    r\n)) { } } }");

            List<string> contextFree = ScanContextFree(tree, tree, allowedFiles: false);
            Expect(contextFree, "PlantedRawHit.cs", "UiNative.Button(r)");
            Expect(contextFree, "PlantedInlineRect.cs", "UiNative.Button(new Rect(0f, 0f, 1f, 1f))");
            if (!contextFree.Exists(v => v.IndexOf("PlantedSplitHit.cs", StringComparison.Ordinal) >= 0))
            {
                throw new Exception("A call split across lines was not reported; the context-free scan is line-bound.");
            }

            if (contextFree.Exists(v => v.IndexOf("PlantedMigratedHit.cs", StringComparison.Ordinal) >= 0))
            {
                throw new Exception(
                    "The migrated two-argument form was reported as a context-free call; the pattern would "
                    + "redden the very answer the contract asks for.");
            }

            // An allowlisted name is exempt by file, not by shape: the entry it does not have is what the
            // funnel discipline is for, and this proves the allowance is consulted rather than assumed.
            File.WriteAllText(
                Path.Combine(tree, "UiWindowHost.cs"),
                "class PlantedShell { void Hit(Rect r) { if (UiNative.Button(r)) { } } }");
            if (ScanContextFree(tree, tree, allowedFiles: false).Exists(v => v.IndexOf("UiWindowHost.cs", StringComparison.Ordinal) >= 0))
            {
                throw new Exception("The allowlisted file was reported anyway; the context-free allowance is not consulted.");
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
