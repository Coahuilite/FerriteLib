using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;

namespace FerriteLib.UiKit.Tests;
internal static class KernelCloseoutInventoryTests
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("  ok: INVENTORY " + name); }
    private static void Pump(UiHost host) { Event.current = new Event { type=EventType.Repaint }; NativeFixture.DrawFrame(host,new UiLayoutRect(0,0,320,300)); }
    internal static int RunAll()
    {
        try
        {
            string[] kinds=typeof(UiKinds).GetFields(BindingFlags.Public|BindingFlags.Static).Select(f=>(string)f.GetRawConstantValue()!).ToArray();
            Check(kinds.Length==18 && kinds.Distinct().Count()==18,"all18 stable kind identifiers are distinct; no reduced wrapper sample");
            foreach(string kind in kinds) Verify(kind);
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine("  FAIL: INVENTORY "+ex);return 1;}
        finally{Event.current=null;}
    }
    private static void Verify(string kind)
    {
        UiWidgetRegistry.Clear();UiWidgetRegistry.InitializeCore();var rt=new UiReactiveRuntime();var b=new UiBindings(rt);
        var number=rt.Value(10f,(a,c)=>a==c);var text=rt.Text("first");var flag=rt.Boolean(true);var revision=rt.Int();
        using var native=new VerseUiExecution();
        var translation=new Translation();int reads=0,requests=0;string attributes="",key="value";
        if(kind==UiKinds.Button){b.BindCommand("go",()=>requests++,()=>flag.Value);attributes="ActionBind=\"go\" Text=\"Go\"";key="";}
        else if(kind==UiKinds.SectionHeader){attributes="TitleKey=\"caption\"";key="";}
        else if(kind==UiKinds.Empty){attributes="TextKey=\"caption\"";key="";}
        else if(kind==UiKinds.Rule){key="";}
        else if(kind==UiKinds.Checkbox){b.BindTracked("value",()=>{reads++;return flag.Value;},v=>flag.Value=v);attributes="Bind=\"value\" Label=\"Flag\"";}
        else if(kind==UiKinds.TextField || kind==UiKinds.WrappedText || kind==UiKinds.Banner)
        {b.BindTracked("value",()=>{reads++;return text.Value;},v=>text.Value=v);attributes="Bind=\"value\"";}
        else if(kind==UiKinds.Dropdown || kind==UiKinds.ModeRow)
        {b.BindTracked("value",()=>{reads++;return text.Value;},v=>text.Value=v);b.BindTrackedChoices(kind==UiKinds.ModeRow?"value":"options",()=> (new[]{text.Value,"other"}).Select(value=>new UiChoice<string>(value,value,value)).ToArray(),(a,c)=>a.Text==c.Text&&a.Value==c.Value&&a.Help==c.Help);attributes="Bind=\"value\""+(kind==UiKinds.Dropdown?" OptionsBind=\"options\"":"");}
        else if(kind==UiKinds.Tree)
        {b.BindTracked<IReadOnlyList<UiTreeRow>>("value",()=>{reads++;return new[]{new UiTreeRow("row",0,text.Value)};},equal:(a,c)=>a[0].Text==c[0].Text);attributes="Bind=\"value\"";}
        else if(kind==UiKinds.LineChart)
        {b.BindTracked<IReadOnlyList<UiLayoutPoint>>("value",()=>{reads++;return new[]{new UiLayoutPoint(0,number.Value),new UiLayoutPoint(1,20)};},equal:(a,c)=>a[0].Y==c[0].Y);attributes="Bind=\"value\" Editable=\"false\"";}
        else if(kind==UiKinds.Image)
        {var first=native.Images.Add(new Texture2D(40,20));var second=native.Images.Add(new Texture2D(80,30));b.BindTracked<UiImage>("value",()=>{reads++;return revision.Value==0?first:second;},equal:(a,c)=>a.Equals(c));attributes="Bind=\"value\" AspectHeight=\"true\"";}
        else if(kind==UiKinds.Repeat)
        {key="items";b.BindTracked<IReadOnlyList<string>>(key,()=>{reads++;return revision.Value==0?new[]{"a"}:new[]{"b"};},equal:(a,c)=>a.SequenceEqual(c));b.BindTracked(new UiBindingAddress("items","a","caption").Key,()=>text.Value);b.BindTracked(new UiBindingAddress("items","b","caption").Key,()=>text.Value);}
        else{b.BindTracked("value",()=>{reads++;return number.Value;},v=>number.Value=v,(a,c)=>a==c);attributes="Bind=\"value\" Max=\"100\"";}
        string body=kind==UiKinds.Repeat?"<Templates><Widget Id=\"entry\" Kind=\"text/wrapped\" Bind=\"caption\"/></Templates><Repeat Id=\"x\" Items=\"items\" Template=\"entry\"/>":"<Widget Id=\"x\" Kind=\""+kind+"\" "+attributes+"/>";
        using var host=new UiHost("inventory-closeout",UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"inventory-closeout\">"+body+"</UiPage>"),b,new UiTheme(),new Metrics(),translation);
        host.Session.SetFrame(native.CaptureFrame(),native);
        Pump(host);int identity=host.Session.Identity;int before=reads,translated=translation.Reads;
        using(rt.Batch()){number.Value=30;text.Value="second";flag.Value=false;revision.Value=1;}
        translation.Text="second";translation.Revision++;host.ElementProperties("x").Metric("Hairline").Set(3);Pump(host);
        UiNode node=host.Session.GetNodeByElementId("x")!;
        Check(host.Session.Identity==identity && host.Session.IsActive && !host.Session.IsTripped(node) && node.ReactiveFault==null && requests==0,
            kind+" retains real Host/node/qualification without fabricated operations");
        if(key.Length>0)Check(reads>before && host.Presentation!.GetStatus(key).IsCurrent,kind+" consumes current tracked production input after actual source change");
        else if(kind==UiKinds.Button)Check(node.IsDisabled,kind+" rechecks actual command qualification after external change");
        else if(kind==UiKinds.Rule)Check(node.Rect!.Value.Height==3,kind+" consumes changed applicable metric without artificial business binding");
        else Check(translation.Reads>translated,kind+" consumes actual current translation/definition path, not an invented value binding");
        if(kind==UiKinds.Repeat)Check(host.Session.GetNodeByElementId("entry#a")==null && host.Session.GetNodeByElementId("entry#b")!=null,"Repeat reconciles actual changed item source in same Host");
        Console.WriteLine("INVENTORY-TRACE kind="+kind+" host="+identity+" reads="+before+"->"+reads+" actual-source=current requests="+requests);
    }
    private sealed class Metrics:ITextMetrics{public float MeasureWidth(string text,UiFont font)=>text.Length*8;public float MeasureText(string text,UiFont font,float width)=>16;}
    private sealed class Translation:IUiTranslation{internal string Text="first";internal int Revision,Reads;public int TranslationRevision=>Revision;public string Translate(string key){Reads++;return Text;}}
}
