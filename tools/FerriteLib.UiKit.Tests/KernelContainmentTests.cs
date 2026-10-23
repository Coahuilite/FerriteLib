using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace FerriteLib.UiKit.Tests;

/// <summary>Path-pinned native contacts; the entire retained Kernel has no backend allowance.</summary>
internal static class KernelContainmentTests
{
    private static readonly Regex BackendAccess = new(
        @"(?<![A-Za-z0-9_.])(?<qual>UnityEngine\s*\.\s*|Verse\s*\.\s*)?(?<owner>GUI|GUIUtility|GenMapUI|Mouse|Text|VerseWidgets|Widgets|Event|Input)\s*\.\s*(?<member>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant);
    private static readonly Regex RetiredNative = new(@"\bUiNative\s*\.", RegexOptions.CultureInvariant);
    private static readonly Dictionary<string,string[]> Allowed = new(StringComparer.Ordinal)
    {
        ["Adapters/RimWorld16/VerseUiExecution.cs"] = new[] {
            "Event.current", "Input.compositionString", "GUIUtility.GetControlID", "GUIUtility.hotControl",
            "GUI.GetNameOfFocusedControl", "GUI.FocusControl", "GUI.SetNextControlName",
            "Text.Font", "Text.Anchor", "Text.WordWrap", "GUI.color", "GUI.backgroundColor", "GUI.contentColor", "GUI.enabled",
            "VerseWidgets.TextArea", "VerseWidgets.TextField", "VerseWidgets.BeginScrollView", "VerseWidgets.EndScrollView",
            "GUI.BeginGroup", "GUI.EndGroup", "VerseWidgets.DrawBoxSolid", "VerseWidgets.Label", "VerseWidgets.DrawLine", "GUI.DrawTexture" },
        ["Adapters/RimWorld16/VerseFerriteTextMetrics.cs"] = new[] {
            "Text.Font", "Text.Anchor", "Text.WordWrap", "Text.CurFontStyle", "Text.CalcHeight", "Text.CalcSize" },
        // Only actual window chrome/input attribution. It does not supply page semantics or draft state.
        ["Adapters/RimWorld16/UiWindowHost.cs"] = new[] { "Event.current", "Mouse.IsOver", "VerseWidgets.ButtonInvisible" }
    };

    public static int RunAll()
    {
        int failures=0;
        failures+=Run("Backend contact stays inside path-pinned adapter files",()=>ExpectEmpty(Scan(SourceTree()),"backend contact"));
        failures+=Run("Containment scan fires on wrong path, new symbol and qualified contact",VerifyControls);
        failures+=Run("Adapter allowlist has no dead files or symbols",()=>{
            foreach(var entry in Allowed)
            {
                string path=Path.Combine(SourceTree(),entry.Key);
                if(!File.Exists(path))throw new Exception("Missing adapter allowance: "+entry.Key);
                var actual=Collect(File.ReadAllLines(path));
                foreach(string symbol in entry.Value)if(!actual.Contains(symbol))throw new Exception("Unused allowance: "+entry.Key+": "+symbol);
            }
        });
        failures+=Run("Retired ambient native bridge has zero production references",()=>ExpectEmpty(ScanRetired(SourceTree()),"retired bridge"));
        failures+=Run("Documented native boundary matches the production allowance",()=>{
            string doc=File.ReadAllText(Path.Combine(RepoRoot(),"docs/api-tiers.md"));
            if(!doc.Contains("No ambient UiNative bridge or context-free core hit overload remains."))throw new Exception("Update the explicit native boundary contract with its metric.");
            foreach(string path in Allowed.Keys)if(!doc.Contains(path))throw new Exception("Undocumented adapter contact: "+path);
        });
        return failures;
    }
    private static int Run(string name,Action action)
    {try{action();Console.WriteLine("  ok: "+name);return 0;}catch(Exception e){Console.Error.WriteLine("  FAIL: "+name+" :: "+e.Message);return 1;}}
    private static void ExpectEmpty(List<string> facts,string kind)
    {if(facts.Count>0)throw new Exception(kind+": "+string.Join("; ",facts));}
    private static bool Generated(string path)=>path.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar)||path.Contains(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar);
    private static string Relative(string root,string path)=>path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Replace('\\','/');
    private static List<string> Scan(string root)
    {
        var result=new List<string>();
        foreach(string file in Directory.GetFiles(root,"*.cs",SearchOption.AllDirectories))
        {
            if(Generated(file))continue;
            string relative=Relative(root,file);string[] allowed=Allowed.TryGetValue(relative,out var found)?found:Array.Empty<string>();
            foreach(string symbol in Collect(File.ReadAllLines(file)))if(Array.IndexOf(allowed,symbol)<0)result.Add(relative+": "+symbol);
        }
        return result;
    }
    private static HashSet<string> Collect(string[] lines)
    {
        var symbols=new HashSet<string>(StringComparer.Ordinal);
        bool usesNativeInput=Array.Exists(lines,line=>line.Contains("using UnityEngine"));
        foreach(string line in lines)
        {
            if(line.TrimStart(' ','\t').StartsWith("//",StringComparison.Ordinal))continue;
            foreach(Match match in BackendAccess.Matches(line))
            {
                string owner=match.Groups["owner"].Value;
                if(owner=="Input" && !usesNativeInput && !match.Groups["qual"].Value.Contains("UnityEngine")) continue;
                if(owner=="Widgets")
                {if(match.Groups["qual"].Value.IndexOf("Verse",StringComparison.Ordinal)>=0)symbols.Add("VerseWidgets."+match.Groups["member"].Value);}
                else symbols.Add(owner+"."+match.Groups["member"].Value);
            }
        }
        return symbols;
    }
    private static List<string> ScanRetired(string root)
    {
        var result=new List<string>();
        foreach(string file in Directory.GetFiles(root,"*.cs",SearchOption.AllDirectories))
        {if(Generated(file))continue;foreach(string line in File.ReadAllLines(file))if(!line.TrimStart(' ','\t').StartsWith("//",StringComparison.Ordinal)&&RetiredNative.IsMatch(line))result.Add(Relative(root,file));}
        return result;
    }
    private static void VerifyControls()
    {
        string root=Path.Combine(Path.GetTempPath(),"fl-contained-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        void Plant(string relative,string text){string file=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllText(file,text);}
        try
        {
            Plant("Kernel/VerseUiExecution.cs","class WrongPath { void Draw() { GUI.color=default; } }");
            Plant("Adapters/RimWorld16/VerseUiExecution.cs","class WrongSymbol { void Draw() { GUI.Box(default,\"x\"); } }");
            Plant("Kernel/Planted.cs","class Raw { void Draw() { Verse.Widgets.Label(default,\"x\"); Mouse.IsOver(default); GenMapUI.DrawPawnLabel(default,\"x\",default); UnityEngine.Input.GetMouseButton(0); } }");
            var violations=Scan(root);
            foreach(string expected in new[]{"Kernel/VerseUiExecution.cs: GUI.color","Adapters/RimWorld16/VerseUiExecution.cs: GUI.Box","Kernel/Planted.cs: VerseWidgets.Label","Kernel/Planted.cs: Mouse.IsOver","Kernel/Planted.cs: GenMapUI.DrawPawnLabel","Kernel/Planted.cs: Input.GetMouseButton"})
                if(!violations.Contains(expected))throw new Exception("Positive control did not fire: "+expected);
            Plant("Kernel/Comments.cs","/// GUI.color\n// Verse.Widgets.Label(default,\"x\");\nclass Comments {} ");
            if(Scan(root).Exists(v=>v.StartsWith("Kernel/Comments.cs",StringComparison.Ordinal)))throw new Exception("Comment-only contact was counted.");
            Plant("Kernel/Retired.cs","class Retired { void Go() { UiNative.Button(\nF(Rect(0,0))\n); } }");
            if(!ScanRetired(root).Contains("Kernel/Retired.cs"))throw new Exception("Retired bridge escaped through a nested call.");
        }
        finally{Directory.Delete(root,true);}
    }
    private static string SourceTree()=>Path.Combine(RepoRoot(),"Source","FerriteLib.UiKit");
    private static string RepoRoot()
    {
        string? current=AppContext.BaseDirectory;
        for(int i=0;i<8&&current!=null;i++,current=Path.GetDirectoryName(current))if(Directory.Exists(Path.Combine(current,"Source","FerriteLib.UiKit")))return current;
        throw new DirectoryNotFoundException("Cannot locate source tree.");
    }
}
