using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FerriteLib.UiKit.Kernel;

internal static class NeutralDependencyTests
{
    internal static void Run(Action<bool,string> check)
    {
        var attributes=new Dictionary<string,string>{{"Text","first"}};
        var spec=new UiElementSpec("caption",UiKinds.WrappedText,attributes);attributes["Text"]="outside";
        var program=UiLayoutManifest.Create("neutral-page",new[]{spec});
        var xml=UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"neutral-page\"><Styles Schema=\"1\"><Scheme Name=\"base\"><Metric Token=\"Font\" Value=\"Tiny\"/><Metric Token=\"Gap\" Value=\"bad\"/><Metric Token=\"RowHeight\" Value=\"40\"/></Scheme></Styles><Widget Id=\"caption\" Kind=\"text/wrapped\" Text=\"first\"/></UiPage>");
        check(program.Roots[0].TryGetAttribute("Text",out string text) && text=="first" && xml.Roots[0].Kind==UiKinds.WrappedText,
            "NEUTRAL actual program/XML page definitions parse and copy without any engine assembly");
        check(xml.Styles.TryGetScheme("base",out var scheme) && scheme.Font==UiFont.Tiny && scheme.Metrics["RowHeight"]==40 && xml.Styles.Issues.Count==1,
            "NEUTRAL actual style/font/metric parser uses one typography declaration and retains located invalid-metric issue");
        var retired=UiStyleDocument.Parse("<Styles Schema=\"1\"><Scheme Name=\"base\"><Font Value=\"Tiny\"/></Scheme></Styles>");check(retired.StructurallyInvalid&&retired.Issues[0].Message.Contains("retired"),"NEUTRAL retired Font XML is diagnosed and refused, never redirected or silently ignored");
        string[] sources=File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"production-sources.txt"));
        check(sources.Length>0 && sources.All(File.Exists),"NEUTRAL actual recorded production source inventory exists");
        string[] forbidden={"UnityEngine","UnityStubs","Verse","RimWorld","Waylith","Harmony","Assembly.Load","Type.GetType"};
        bool Safe(string source)
        {
            string code=System.Text.RegularExpressions.Regex.Replace(source,@"^\s*//.*$","",System.Text.RegularExpressions.RegexOptions.Multiline);
            return !forbidden.Any(name=>System.Text.RegularExpressions.Regex.IsMatch(code,@"\b"+System.Text.RegularExpressions.Regex.Escape(name)+@"\b"));
        }
        check(sources.All(path=>Safe(File.ReadAllText(path))),"NEUTRAL production sources do not hide engine access through named lookup/reflection");
        check(!Safe("Type.GetType(\"UnityStubs.Rect\")"),"NEUTRAL hidden-platform source guard rejects planted lookup");
        var seen=new HashSet<string>(StringComparer.Ordinal);var pending=new Queue<Assembly>();pending.Enqueue(typeof(UiBindings).Assembly);
        string runtimeDirectory=Path.GetDirectoryName(typeof(object).Assembly.Location)!+Path.DirectorySeparatorChar;
        while(pending.Count>0)
        {
            Assembly assembly=pending.Dequeue();if(!seen.Add(assembly.GetName().Name!))continue;
            foreach(AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                Assembly resolved=Assembly.Load(reference);
                if(!resolved.Location.StartsWith(runtimeDirectory,StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Non-neutral transitive dependency: "+assembly.GetName().Name+" -> "+reference.FullName);
                pending.Enqueue(resolved);
            }
        }
        check(seen.Count>1,"NEUTRAL complete resolved transitive assembly closure contains only actual BCL dependencies");
        foreach(string name in seen.OrderBy(s=>s,StringComparer.Ordinal))Console.WriteLine("NEUTRAL-DEPENDENCY "+name);
    }
}
