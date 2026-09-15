using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Guard for this repository's most expensive invisible failure: a lane file that exists, compiles and
/// declares <c>RunAll</c>, while <c>Program.cs</c> never invokes it. Every gate stays green because the
/// missing lane simply does not run — <c>MEMORY.md</c> records the 491-line lane that landed while nothing
/// called it. Identity is the <em>class that declares RunAll</em>, never the file name, so a file whose
/// class name does not match its file name is neither misreported nor allowed to hide. The reverse shape is
/// reported too: a <c>Program.cs</c> registration whose lane file has gone.
/// </summary>
internal static class KernelLaneRegistrationTests
{
    private const string TestsProjectRelativePath = "tools/FerriteLib.UiKit.Tests";
    private const string ProgramFileName = "Program.cs";

    /// <summary>A floor, not a promise: if discovery collapses, this lane must fail rather than pass clean.</summary>
    private const int MinimumExpectedLaneFiles = 5;

    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Every lane file that declares RunAll is invoked from Program.cs", VerifyEveryDeclaredLaneIsRegistered);
        failures += Run("Every Program.cs RunAll registration names a lane that exists", VerifyNoRegistrationIsStale);
        failures += Run("The comparison fires on a planted unregistered lane and ignores a renamed file", VerifyComparisonIsNotVacuous);
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
            Console.WriteLine("  FAIL: " + name + " - " + ex.Message);
            return 1;
        }
    }

    private static void VerifyEveryDeclaredLaneIsRegistered()
    {
        LaneScan scan = Scan(RepoRoot());
        if (scan.DeclaredLaneClasses.Count == 0 || scan.RegisteredNames.Count == 0)
        {
            throw new Exception("the scan found " + scan.DeclaredLaneClasses.Count + " lane class(es) and "
                + scan.RegisteredNames.Count + " registration(s); the reader is broken, not the tree clean.");
        }

        if (scan.DeclaredTestsLaneClasses.Count < MinimumExpectedLaneFiles)
        {
            throw new Exception("only " + scan.DeclaredTestsLaneClasses.Count + " *Tests.cs lane file(s) discovered under "
                + TestsProjectRelativePath + "; discovery is broken (expected at least " + MinimumExpectedLaneFiles + ").");
        }

        if (scan.Unregistered.Count > 0)
        {
            throw new Exception(scan.Unregistered.Count + " lane file(s) declare RunAll but Program.cs never invokes them: "
                + string.Join(", ", scan.Unregistered)
                + " - register each one; a lane that does not run is not wired.");
        }
    }

    private static void VerifyNoRegistrationIsStale()
    {
        LaneScan scan = Scan(RepoRoot());
        if (scan.StaleRegistrations.Count > 0)
        {
            throw new Exception(scan.StaleRegistrations.Count + " Program.cs registration(s) name a lane no file declares: "
                + string.Join(", ", scan.StaleRegistrations) + " - the lane was renamed or deleted; fix the registration.");
        }
    }

    /// <summary>
    /// Positive control. A parser that silently reads nothing reports a clean repository, which is the
    /// failure this lane exists to catch, so the control plants the exact shape it must reject: one lane
    /// file that is not registered, one registered lane whose class name differs from its file name (must
    /// NOT be reported), and one registration with no lane file at all.
    /// </summary>
    private static void VerifyComparisonIsNotVacuous()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "ferritelib-lane-registration-" + Guid.NewGuid().ToString("N"));
        try
        {
            string directory = Path.Combine(sandbox, TestsProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, ProgramFileName),
                "class Program { static int failures; static void RunAll() {"
                + " failures += GoodLaneTests.RunAll();"
                + " failures += RenamedFileLane.RunAll();"
                + " failures += GhostLane.RunAll(); } }");
            WriteLane(directory, "GoodLaneTests.cs", "GoodLaneTests");
            WriteLane(directory, "RenamedFileLaneFile.cs", "RenamedFileLane");
            WriteLane(directory, "UnregisteredLaneTests.cs", "PlantedUnregisteredLane");

            LaneScan scan = Scan(sandbox);

            if (scan.Unregistered.Count != 1 || scan.Unregistered[0] != "PlantedUnregisteredLane")
            {
                throw new Exception("a planted unregistered lane was not isolated; reported ["
                    + string.Join(", ", scan.Unregistered) + "] - exactly PlantedUnregisteredLane must be reported.");
            }

            if (scan.StaleRegistrations.Count != 1 || scan.StaleRegistrations[0] != "GhostLane")
            {
                throw new Exception("a registration with no lane file was not isolated; reported ["
                    + string.Join(", ", scan.StaleRegistrations) + "] - exactly GhostLane must be reported.");
            }
        }
        finally
        {
            try { Directory.Delete(sandbox, true); } catch (IOException) { }
        }
    }

    private static void WriteLane(string directory, string fileName, string className)
    {
        File.WriteAllText(Path.Combine(directory, fileName),
            "namespace FerriteLib.UiKit.Tests;\ninternal static class " + className
            + "\n{\n    public static int RunAll()\n    {\n        return 0;\n    }\n}\n");
    }

    private sealed class LaneScan
    {
        public readonly List<string> RegisteredNames = new List<string>();
        public readonly List<string> DeclaredLaneClasses = new List<string>();
        public readonly List<string> DeclaredTestsLaneClasses = new List<string>();
        public readonly List<string> Unregistered = new List<string>();
        public readonly List<string> StaleRegistrations = new List<string>();
    }

    private static LaneScan Scan(string repoRoot)
    {
        string projectDirectory = Path.Combine(repoRoot, TestsProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(projectDirectory))
        {
            throw new DirectoryNotFoundException("the harness project directory is gone: " + projectDirectory);
        }

        string programPath = Path.Combine(projectDirectory, ProgramFileName);
        if (!File.Exists(programPath))
        {
            throw new FileNotFoundException("Program.cs is gone, so no lane can be shown to run", programPath);
        }

        LaneScan scan = new LaneScan();
        scan.RegisteredNames.AddRange(RegisteredNames(StripComments(File.ReadAllText(programPath))));

        foreach (string file in Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (IsExcluded(file, projectDirectory)) continue;
            string? lane = LaneClassDeclaringRunAll(StripComments(File.ReadAllText(file)));
            if (lane == null) continue;

            if (!scan.DeclaredLaneClasses.Contains(lane)) scan.DeclaredLaneClasses.Add(lane);
            if (file.EndsWith("Tests.cs", StringComparison.Ordinal) && !scan.DeclaredTestsLaneClasses.Contains(lane))
            {
                scan.DeclaredTestsLaneClasses.Add(lane);
            }
        }

        foreach (string lane in scan.DeclaredTestsLaneClasses)
        {
            if (!scan.RegisteredNames.Contains(lane)) scan.Unregistered.Add(lane);
        }

        foreach (string name in scan.RegisteredNames)
        {
            if (!scan.DeclaredLaneClasses.Contains(name)) scan.StaleRegistrations.Add(name);
        }

        return scan;
    }

    /// <summary>
    /// Removes line and block comments before matching: a commented-out registration is exactly the
    /// "lane that does not run" shape, and matching raw text would read it as live. The routine is
    /// deliberately simple (no string-literal awareness); Program.cs contains no '//' inside a literal,
    /// and a lane declaration is code, so a string literal is the only way to fool it.
    /// </summary>
    private static string StripComments(string text)
    {
        return Regex.Replace(Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline), @"//[^\r\n]*", " ");
    }

    /// <summary>Program.cs is the only registration surface; the call shape is what the reader keys on.</summary>
    private static List<string> RegisteredNames(string programText)
    {
        List<string> names = new List<string>();
        foreach (Match match in Regex.Matches(programText, @"\b([A-Za-z_][A-Za-z0-9_]*)\.RunAll\s*\(\s*\)"))
        {
            string name = match.Groups[1].Value;
            if (!names.Contains(name)) names.Add(name);
        }

        return names;
    }

    /// <summary>
    /// The class that declares <c>RunAll</c>: the nearest preceding type declaration, because the lane's
    /// identity is its class and a file may legally be named something else.
    /// </summary>
    private static string? LaneClassDeclaringRunAll(string text)
    {
        Match method = Regex.Match(text, @"\bstatic\s+int\s+RunAll\s*\(");
        if (!method.Success) return null;

        MatchCollection types = Regex.Matches(
            text.Substring(0, method.Index), @"\b(?:class|struct)\s+([A-Za-z_][A-Za-z0-9_]*)");
        return types.Count == 0 ? null : types[types.Count - 1].Groups[1].Value;
    }

    private static bool IsExcluded(string file, string projectDirectory)
    {
        string root = Path.GetFullPath(projectDirectory);
        if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            root += Path.DirectorySeparatorChar;
        }

        string full = Path.GetFullPath(file);
        string relative = full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? full.Substring(root.Length)
            : full;

        foreach (string segment in relative.Split(new[] { '\\', '/' }))
        {
            if (string.Equals(segment, "Stubs", StringComparison.Ordinal)
                || string.Equals(segment, "bin", StringComparison.Ordinal)
                || string.Equals(segment, "obj", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
