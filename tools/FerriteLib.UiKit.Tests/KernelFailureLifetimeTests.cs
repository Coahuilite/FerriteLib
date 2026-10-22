using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Tests;
internal static class KernelFailureLifetimeTests
{
    private static readonly Rect View=new(0,0,320,300);
    private static IList Labels=>(IList)typeof(Verse.Widgets).GetField("LabelTexts",BindingFlags.Public|BindingFlags.Static)!.GetValue(null)!;
    private static void C(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("  ok: RU3-HOST "+name);}
    private static bool Painted(int start,string text){for(int i=start;i<Labels.Count;i++)if((string)Labels[i]! == text)return true;return false;}
    private static UiHost Host(string body,UiBindings bindings)=>new("failure",UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"failure\">"+body+"</UiPage>"),bindings,UiTheme.Vanilla,new Metrics(),new Translation());
    private static UiNode Node(UiHost host,string key)=>host.Session.GetNodeByElementId(key)??throw new Exception("Missing "+key);
    private static void Pump(UiHost host,EventType type=EventType.Repaint,Vector2? point=null){Event.current=new Event{type=type,button=0,mousePosition=point??new Vector2(-100,-100)};host.DrawFrame(View);}
    private static void Click(UiHost host,string key){Rect r=Node(host,key).Rect!.Value;var p=new Vector2(r.x+r.width/2,r.y+r.height/2);Pump(host,EventType.MouseDown,p);Pump(host,EventType.MouseUp,p);}
    internal static int RunAll()
    {
        try
        {
            UiWidgetRegistry.Clear();UiWidgetRegistry.InitializeCore();
            CounterFault();InteractiveRetained();RetainedCollections();BranchFailureDraw();FirstFailure();SharedGeometry();DemandAndDraft();UnknownCustom();MultiHost();RequestFailure();
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine("  FAIL: RU3-HOST "+ex);return 1;}
        finally{Event.current=null;UiNative.TextFieldOverride=null;UiNative.ButtonOverride=null;UiNative.DebugHotControl=0;GUIUtility.hotControl=0;}
    }
    private const string CounterBody="<Column Padding=\"0\" Gap=\"0\"><Widget Id=\"caption\" Kind=\"text/wrapped\" Bind=\"caption\"/><Widget Id=\"reset\" Kind=\"input/button\" ActionBind=\"reset\" Text=\"Reset\"/></Column>";
    private static void CounterFault()
    {
        using var example=new FailureCounterExample();using var host=Host(CounterBody,example.Bindings);
        Pump(host);UiNode caption=Node(host,"caption");float height=caption.Rect!.Value.height;
        example.Broken.Value=true;int start=Labels.Count;Pump(host);
        C(Painted(start,"Count 4") && Painted(start,"!") && caption.ReactiveFault?.Identity=="counter/format" && caption.Rect!.Value.height==height,"default wrapped output retains last measured text and explicit theme fault marker");
        C(!host.Session.IsTripped(caption) && host.Session.IsActive && !Node(host,"reset").IsDisabled,"managed display fault does not trip legacy node or disable independent reset");
        int formats=example.Formats;for(int i=0;i<8;i++)Pump(host);
        C(example.Formats==formats,"eight repeated draws do not retry or create new fault attempts");
        Click(host,"reset");C(example.Resets==1 && example.Count.Value==0,"real pointer release executes reliable reset beside faulty display");
        Pump(host);C(example.Formats==formats+1 && caption.ReactiveFault!=null,"reset's related value permits one fresh failed attempt");
        example.Broken.Value=false;Pump(host);C(caption.ReactiveFault==null && host.Presentation!.GetStatus("caption").IsCurrent,"related input clears fault after successful default presentation");
        Console.WriteLine("RU3-HOST-FACT counter formats="+example.Formats+" resets="+example.Resets+" height="+height+" session="+host.Session.IsActive);
    }
    private static void FirstFailure()
    {
        using var example=new FailureCounterExample();example.Broken.Value=true;using var host=Host(CounterBody,example.Bindings);int start=Labels.Count;Pump(host);
        C(!Painted(start,"Count 0") && !Painted(start,"Count 4") && Painted(start,"counter/format") && !host.Presentation!.GetStatus("caption").HasLastValue,"first failure paints unavailable identity, not fabricated successful text");
        Click(host,"reset");C(example.Resets==1,"first-failure page still accepts independent reliable operation");
        int formats=example.Formats;host.SetDisplayDemand(false);example.Bindings.Invoke("retry-display");Pump(host);C(example.Formats==formats,"explicit pure Retry is deferred while view has no display demand");
        host.SetDisplayDemand(true);Pump(host);C(example.Formats==formats+1 && host.Session.IsActive,"show consumes pending Retry once without rebuilding session");
    }
    private static void SharedGeometry()
    {
        var rt=new UiReactiveRuntime();using var owner=rt.CreateOwner("layout");var broken=rt.Boolean();var size=rt.Value(80f,(a,b)=>a==b);int reads=0;
        var width=owner.Compute("shared-width",()=>{reads++;float w=size.Value;if(broken.Value)throw new Exception("geometry");return w;});
        var eligible=owner.Compute("eligibility",()=>{if(broken.Value)throw new Exception("qualification");return true;});
        var b=new UiBindings(rt);b.BindTracked("width",()=>width.Value,equal:(a,c)=>a==c);int requests=0;b.BindCommand("act",()=>requests++,()=>eligible.Value);
        using var host=Host("<Row Padding=\"0\"><Widget Kind=\"input/button\" Id=\"a\" WidthKey=\"width\" ActionBind=\"act\" Text=\"A\"/><Widget Kind=\"text/wrapped\" Id=\"after\" Text=\"After\"/></Row>",b);
        Pump(host);Rect before=Node(host,"a").Rect!.Value;broken.Value=true;Pump(host);
        Console.WriteLine("RU3-DIAG layout="+host.LayoutFault+" width="+Node(host,"a").Rect!.Value.width+" before="+before.width+" reliable="+host.Session.IsInputGeometryReliable);
        C(host.LayoutFault?.Identity=="layout/shared-width" && Node(host,"a").Rect!.Value.width==before.width && !host.Session.IsInputGeometryReliable,"shared failed geometry retains complete old arrangement but cannot publish a current hit pass");
        int count=reads;for(int i=0;i<5;i++)Pump(host);C(reads==count && requests==0,"shared geometry fault cannot resume on drawing");
        broken.Value=false;size.Value=120;Pump(host);C(host.LayoutFault==null && Node(host,"a").Rect!.Value.width==120 && host.Session.IsInputGeometryReliable,"related safe geometry publishes completely before input becomes eligible");
        using var qualified=Host("<Widget Kind=\"input/button\" Id=\"q\" ActionBind=\"act\" Text=\"Q\"/>",b);Pump(qualified);broken.Value=true;Pump(qualified);
        C(Node(qualified,"q").IsDisabled && Node(qualified,"q").ReactiveFault?.Identity=="layout/eligibility" && qualified.Session.IsActive,"failed latest eligibility disables affected command without failing the page");
        Click(qualified,"q");C(requests==0,"failed eligibility is never interpreted as permission");
    }
    private static void DemandAndDraft()
    {
        var rt=new UiReactiveRuntime();var source=rt.Int(1);var visible=rt.Boolean(true);var text=rt.Text("model");var b=new UiBindings(rt);int textRuns=0;
        b.BindTracked("caption",()=>{textRuns++;return "Value "+source.Value;});b.BindTracked("visible",()=>visible.Value);b.BindTracked("edit",()=>text.Value,v=>text.Value=v);
        using var host=Host("<Column Padding=\"0\" Gap=\"0\"><Surface Id=\"panel\" VisibleKey=\"visible\" Padding=\"0\"><Widget Kind=\"text/wrapped\" Id=\"caption\" Bind=\"caption\"/><Widget Kind=\"input/text-field\" Id=\"field\" Bind=\"edit\"/></Surface><Widget Kind=\"text/wrapped\" Id=\"independent\" Text=\"Independent\"/></Column>",b);
        Pump(host);UiNode field=Node(host,"field");bool Parse(string t,out string v){v=t;return true;}
        var editor=UiNative.Editor<string>(host.CreateContext(320).WithNode(field),"edit",v=>v,Parse);editor.Observe("model");editor.Focus();editor.Input("uncommitted",true,false,false,_=>throw new Exception("unexpected submit"),()=>"model");
        visible.Value=false;source.Value=2;Pump(host);int hidden=textRuns;source.Value=3;source.Value=4;Pump(host);Pump(host);
        C(textRuns==hidden && hidden==1 && Node(host,"independent").IsArranged && !field.IsArranged,"true hidden panel delays exclusive pure display and measure while independent content continues");
        C(editor.Draft=="uncommitted" && text.Value=="model","hidden same-target draft stays uncommitted and retained");
        visible.Value=true;int start=Labels.Count;Pump(host);C(textRuns==2 && Painted(start,"Value 4") && ReferenceEquals(field,Node(host,"field")) && editor.Draft=="uncommitted","show takes latest input once and keeps same-target editor identity and draft");
        Console.WriteLine("RU3-HOST-FACT hidden-runs="+hidden+" shown-runs="+textRuns+" draft="+editor.Draft);
    }
    private sealed class OldCustom:IUiWidget
    {
        public string Kind=>"old-custom";public void Configure(UiElementSpec spec){}public void Validate(IUiBindings b,string path){}
        public float Measure(UiWidgetContext c)=>20;public void Draw(Rect r,UiWidgetContext c){_=c.Bindings.Get<string>("unknown");}
    }
    private sealed class DeclaredCustom:IUiWidget,IUiPresentationDemand
    {
        public string Kind=>"declared-custom";public void Configure(UiElementSpec spec){}public void Validate(IUiBindings b,string path){}
        public IEnumerable<string> PresentationKeys(UiWidgetContext c){yield return "known";}
        public float Measure(UiWidgetContext c)=>20;public void Draw(Rect r,UiWidgetContext c){_=c.Bindings.Get<string>("known");}
    }
    private static void UnknownCustom()
    {
        var rt=new UiReactiveRuntime();var input=rt.Int();int known=0,unknown=0;var b=new UiBindings(rt);
        b.BindTracked("known",()=>{known++;return input.Value.ToString();});b.BindTracked("unknown",()=>{unknown++;return input.Value.ToString();});
        UiWidgetRegistry.Register("failure","old-custom",()=>new OldCustom());UiWidgetRegistry.Register("failure","declared-custom",()=>new DeclaredCustom());
        using(var old=Host("<Widget Kind=\"old-custom\"/>",b)){Pump(old);input.Value=1;Pump(old);C(known==2 && unknown==2,"undeclared old custom keeps conservative full tracked service without a new prerequisite");}
        int prior=unknown;using(var declared=Host("<Widget Kind=\"declared-custom\"/>",b)){Pump(declared);input.Value=2;Pump(declared);C(unknown==prior && known==4,"explicit complete custom demand permits only known-unused pure work to remain deferred");}
    }
    private sealed class Window:UiWindowHost
    {
        private readonly UiBindings bindings;internal UiHost? Page;internal int Creates,Notices,Failures,Detached;internal FailureWorkshopExample.Confirmation? Pending;
        internal Window(UiBindings b){bindings=b;windowRect=new Rect(0,0,400,350);HostDetached+=_=>{Detached++;Pending?.Dispose();Pending=null;};}
        protected override UiTheme Theme=>UiTheme.Vanilla;
        protected override UiHost CreateHost(){Creates++;return Page=Host("<Widget Kind=\"text/wrapped\" Id=\"caption\" Bind=\"caption\"/>",bindings);}
        protected override void DrawNotice(Rect r,UiWindowNotice n){Notices++;}
        protected override void OnDrawFailure(Exception e){Failures++;}
    }
    private static void MultiHost()
    {
        var rt=new UiReactiveRuntime();using var sharedOwner=rt.CreateOwner("shared-owner");var value=rt.Int();var broken=rt.Boolean();int sharedRuns=0,bRuns=0;
        var shared=sharedOwner.Compute("caption",()=>{sharedRuns++;int n=value.Value;if(broken.Value)throw new Exception("shared caption");return "Shared "+n;});
        var aBindings=new UiBindings(rt);aBindings.BindTracked("caption",()=>shared.Value);var bBindings=new UiBindings(rt);bBindings.BindTracked("caption",()=>{bRuns++;return shared.Value;});
        var catalog=new UiWindowCatalog(Find.WindowStack);Window? a=null,b=null;
        catalog.Register("ru3","a",new UiWindowOptions{AbsorbInputAroundWindow=false},_=>a=new Window(aBindings));catalog.Register("ru3","b",new UiWindowOptions{AbsorbInputAroundWindow=false},_=>b=new Window(bBindings));
        var ka=new UiWindowKey("ru3","a","A");var kb=new UiWindowKey("ru3","b","B");catalog.Open(ka);catalog.Open(kb);
        Event.current=new Event{type=EventType.Repaint};a!.DoWindowContents(View);b!.DoWindowContents(View);catalog.Activate(ka);value.Value=1;a.DoWindowContents(View);b.DoWindowContents(View);
        C(!b.IsActiveTarget && b.Page!.Presentation!.GetStatus("caption").IsCurrent && bRuns==2 && sharedRuns==2,"B unfocused still updates real shell display with shared calculation cached once");
        using var workshop=new FailureWorkshopExample();b.Pending=workshop.OpenConfirmation();var pending=b.Pending;workshop.Selected="B23";
        b.SetDisplayDemand(false);value.Value=2;a.DoWindowContents(View);b.DoWindowContents(View);
        C(sharedRuns==3 && bRuns==2 && b.Creates==1 && pending.Target=="A17" && !pending.Closed,"B true hidden leaves A shared work running and retained owned confirmation unchanged");
        broken.Value=true;a.DoWindowContents(View);b.SetDisplayDemand(true);b.DoWindowContents(View);int attempts=sharedRuns,displayAttempts=bRuns;
        b.SetDisplayDemand(false);b.SetDisplayDemand(true);b.DoWindowContents(View);a.DoWindowContents(View);
        C(sharedRuns==attempts && bRuns==displayAttempts && b.Page!.Session.IsActive && a.Failures==0 && b.Failures==0 && b.Notices==0,"show and repeated shell draw cannot clear R1 fault or release recoverable sessions");
        workshop.Business.A17Cancelable=false;pending.Confirm();C(pending.Result==false && workshop.Business.LastTarget=="A17","B-owned nonblocking confirmation rechecks original A17 business target after selection B23");
        b.Pending=workshop.OpenConfirmation();var doomed=b.Pending;catalog.Close(kb);catalog.Close(kb);b.PostClose();doomed.Confirm();
        C(doomed.Closed && b.Pending==null && b.Detached==1 && !b.Page!.Session.IsActive && a.Page!.Session.IsActive && workshop.Business.Requests==1,"destroy B clears owned pending UI exactly once; repeated close and late UI notification issue no business call");
        broken.Value=false;value.Value=3;a.DoWindowContents(View);C(a.Page!.Presentation!.GetStatus("caption").IsCurrent && sharedRuns==attempts+1,"closing one window does not release shared owner or survivor");
        catalog.Close(ka);C(sharedOwner.Compute("still-owned",()=>value.Value).Value==3,"window destruction does not dispose explicit shared calculation owner");
        Console.WriteLine("RU3-HOST-FACT multi shared-runs="+sharedRuns+" B-runs="+bRuns+" B-detach="+b.Detached+" business-requests="+workshop.Business.Requests);
    }
    private static void RequestFailure()
    {
        var b=new UiBindings(new UiReactiveRuntime());int attempts=0;b.BindCommand("request",()=>{attempts++;if(attempts==1)throw new Exception("business already applied");});
        using var host=Host("<Widget Kind=\"input/button\" Id=\"request\" ActionBind=\"request\" Text=\"Request\"/>",b);Pump(host);Click(host,"request");Pump(host);Click(host,"request");
        C(attempts==2 && !host.Session.IsTripped(Node(host,"request")),"ordinary button failure preserves applied write and next click is a new qualified request");
    }

    private static void InteractiveRetained()
    {
        var rt=new UiReactiveRuntime();using var owner=rt.CreateOwner("interactive");var broken=rt.Boolean();var flag=rt.Boolean(true);int reads=0,writes=0,resets=0;
        var boolean=owner.Compute("boolean",()=>{reads++;bool v=flag.Value;if(broken.Value)throw new Exception("bool render");return v;});
        var b=new UiBindings(rt);b.BindTracked("flag",()=>boolean.Value,v=>{writes++;flag.Value=v;});b.BindCommand("reset",()=>resets++);
        using var host=Host("<Column Padding=\"0\" Gap=\"0\"><Widget Kind=\"input/checkbox\" Id=\"check\" Bind=\"flag\" Label=\"Checked\"/><Widget Kind=\"input/button\" Id=\"reset\" ActionBind=\"reset\" Text=\"Reset\"/></Column>",b);
        Pump(host);broken.Value=true;int start=Labels.Count;Pump(host);
        C(Painted(start,"Checked") && Painted(start,"!") && !Painted(start,"interactive/boolean") && Node(host,"check").PresentationUnavailable,"Checkbox retains its last checked control paint with fault, not a recovery replacement");
        for(int i=0;i<5;i++)Pump(host);Click(host,"check");Click(host,"reset");
        bool invalid=false;try{_=boolean.Value;}catch(UiReactiveFault){invalid=true;}
        C(reads==2 && writes==0 && resets==1 && invalid,"retained Checkbox never reads old state into toggle or qualification; independent reset remains usable");
        using(var first=Host("<Widget Kind=\"input/checkbox\" Id=\"first\" Bind=\"flag\" Label=\"Checked\"/>",b))
        {start=Labels.Count;Pump(first);C(!Painted(start,"Checked") && Painted(start,"interactive/boolean"),"first failed Checkbox has unavailable feedback without invented unchecked state");}
        foreach(string kind in new[]{"input/text-field","input/number-field","input/stepper-slider","input/slider"})
        {
            var local=new UiReactiveRuntime();using var editOwner=local.CreateOwner(kind);var fail=local.Boolean();var text=local.Text("model");var number=local.Value(1f,(a,c)=>a==c);int setters=0;
            var editBindings=new UiBindings(local);
            var stringValue=editOwner.Compute("string",()=>{string v=text.Value;if(fail.Value)throw new Exception("edit string");return v;});
            var floatValue=editOwner.Compute("number",()=>{float v=number.Value;if(fail.Value)throw new Exception("edit number");return v;});
            if(kind=="input/text-field")editBindings.BindTracked("edit",()=>stringValue.Value,v=>{setters++;text.Value=v;});
            else editBindings.BindTracked("edit",()=>floatValue.Value,v=>{setters++;number.Value=v;},equal:(a,c)=>a==c);
            editBindings.BindCommand("reset",()=>resets++);
            string extra=kind=="input/text-field"?" Multiline=\"true\"":" Min=\"0\" Max=\"100\"";
            using var editHost=Host("<Column Padding=\"0\" Gap=\"0\"><Widget Kind=\""+kind+"\" Id=\"edit\" Bind=\"edit\" Label=\"Value\""+extra+"/><Widget Kind=\"input/button\" Id=\"reset\" ActionBind=\"reset\" Text=\"Reset\"/></Column>",editBindings);
            Pump(editHost);UiNode node=Node(editHost,"edit");IUiEditState? retainedEditor=null;
            if(kind=="input/text-field")
            {bool Parse(string t,out string v){v=t;return true;}var e=UiNative.Editor<string>(editHost.CreateContext(320).WithNode(node),"edit",v=>v,Parse);e.Observe("model");e.Focus();e.Input("draft text",true,false,false,_=>throw new Exception("commit"),()=>"model");retainedEditor=e;}
            else if(kind!="input/slider")
            {var n=new UiNumericEdit(0,100,"0.##");var e=UiNative.Editor<float>(editHost.CreateContext(320).WithNode(node),"edit",n.Format,n.Parse);e.Observe(1);e.Focus();e.Input("20",true,false,false,_=>throw new Exception("commit"),()=>1);retainedEditor=e;}
            Pump(editHost);string draft=retainedEditor?.Draft??"";float oldHeight=node.Rect!.Value.height;
            fail.Value=true;start=Labels.Count;Pump(editHost);
            C(Painted(start,"Value") && Painted(start,"!") && node.PresentationUnavailable && node.Rect!.Value.height==oldHeight,kind+" retains dedicated pure control paint and validated last-display measurement");
            if(retainedEditor!=null)C(retainedEditor.Draft==draft && Painted(start,draft) && setters==0 && !retainedEditor.Focused,kind+" retained same-target draft paints without Observe, native editing, submission or rollback");
            int before=resets;Click(editHost,"reset");Pump(editHost);C(setters==0 && resets==before+1,kind+" failure blocks dependent edits but leaves independent real operation available");
            fail.Value=false;Pump(editHost);C(setters==0 && node.ReactiveFault==null,kind+" recovery does not replay a queued edit or mutate business");
        }
    }
    private static void RetainedCollections()
    {
        foreach(string kind in new[]{"input/dropdown","input/mode-row","container/tree","chart/line"})
        {
            var rt=new UiReactiveRuntime();using var owner=rt.CreateOwner(kind);var fail=rt.Boolean();int reads=0,writes=0;var b=new UiBindings(rt);string extra="";
            if(kind=="container/tree")
            {var rows=owner.Compute<IReadOnlyList<UiTreeRow>>("rows",()=>{reads++;if(fail.Value)throw new Exception("rows");return new[]{new UiTreeRow("one",0,"Retained row")};});b.BindTracked("data",()=>rows.Value,equal:(a,c)=>ReferenceEquals(a,c));}
            else if(kind=="chart/line")
            {var points=owner.Compute<IReadOnlyList<Vector2>>("points",()=>{reads++;if(fail.Value)throw new Exception("points");return new[]{new Vector2(0,0),new Vector2(1,1)};});b.BindTracked("data",()=>points.Value,equal:(a,c)=>ReferenceEquals(a,c));}
            else if(kind=="input/mode-row")
            {b.BindTracked("data",()=>1,_=>writes++);var choices=owner.Compute<IReadOnlyList<UiChoice<int>>>("choices",()=>{reads++;if(fail.Value)throw new Exception("options");return new[]{new UiChoice<int>("Retained choice",1)};});b.BindTrackedOptions("data",()=>choices.Value,(a,c)=>a.Text==c.Text && a.Value==c.Value);}
            else
            {extra=" OptionsBind=\"data\"";b.BindTracked("data",()=>"alpha",_=>writes++);var options=owner.Compute<IReadOnlyList<string>>("options",()=>{reads++;if(fail.Value)throw new Exception("options");return new[]{"alpha","beta"};});b.BindTrackedOptions("data",()=>options.Value,(a,c)=>a==c);}
            using var host=Host("<Widget Kind=\""+kind+"\" Id=\"value\" Bind=\"data\""+extra+"/>",b);Pump(host);float height=Node(host,"value").Rect!.Value.height;
            if(kind=="input/dropdown"){Click(host,"value");Pump(host);}
            fail.Value=true;int start=Labels.Count;Pump(host);
            C(Painted(start,"!") && !Painted(start,kind+"/options") && Node(host,"value").PresentationUnavailable && Node(host,"value").Rect!.Value.height==height,kind+" preserves last control output and complete geometry after tracked collection failure");
            if(kind!="chart/line")C(Painted(start,kind=="input/dropdown"?"alpha":kind=="input/mode-row"?"Retained choice":"Retained row"),kind+" still paints the last selected or row label");
            for(int i=0;i<4;i++)Pump(host);Click(host,"value");C(writes==0 && reads==2 && host.Session.OpenPopupAnchor==null,kind+" retained paint cannot publish selection, request, popup input or repeat failed work");
            fail.Value=false;Pump(host);C(Node(host,"value").ReactiveFault==null && writes==0,kind+" actual related recovery resumes current read without replay");
        }
    }
    private static void BranchFailureDraw()
    {
        var rt=new UiReactiveRuntime();using var owner=rt.CreateOwner("branch-paint");var branch=rt.Boolean();var left=rt.Int(1);var right=rt.Int(2);int runs=0;
        var value=owner.Compute("value",()=>{runs++;if(!branch.Value)return left.Value.ToString();int n=right.Value;if(n<5)throw new Exception("right");return n.ToString();});
        var b=new UiBindings(rt);b.BindTracked("text",()=>value.Value);using var host=Host("<Widget Kind=\"text/wrapped\" Bind=\"text\"/>",b);Pump(host);branch.Value=true;Pump(host);left.Value=99;for(int i=0;i<5;i++)Pump(host);
        C(runs==2,"failed current branch ignores obsolete successful source across actual repeated Host draws");right.Value=5;Pump(host);C(runs==3,"actual failed branch input restores Host presentation once");
    }
    private sealed class Metrics:ITextMetrics{public float MeasureText(string t,UiFont f,float w)=>Math.Max(18,t.Length*2);public float MeasureWidth(string t,UiFont f)=>t.Length*8;}
    private sealed class Translation:IUiTranslation{public string Translate(string k)=>k;public int TranslationRevision=>0;}
}
