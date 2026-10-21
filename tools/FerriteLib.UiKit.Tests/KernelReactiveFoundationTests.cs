using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;
internal static class KernelReactiveFoundationTests
{
    private static readonly Rect View = new(20, 30, 240, 200);
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("  ok: REACTIVE-HOST " + name);
    }
    private static void Pump(UiHost host, EventType phase, Vector2 point)
    {
        Event.current = new Event { type=phase, button=0, mousePosition=point };
        host.DrawFrame(View);
    }
    private static UiHost Host(string content, UiBindings bindings, ITextMetrics? metrics = null)
        => new("reactive", UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"reactive\"><Column Padding=\"0\" Gap=\"0\">"+content+"</Column></UiPage>"), bindings, UiTheme.Vanilla, metrics ?? new Metrics(), new Translation());
    internal static int RunAll()
    {
        try
        {
            UiWidgetRegistry.Clear(); UiWidgetRegistry.InitializeCore();
            DefaultCounterAndWorkshop();
            CounterAndCheckbox();
            LegacyPurityBoundary();
            SameRoundInputs();
            IndependentProjection();
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine("  FAIL: REACTIVE-HOST "+ex); return 1; }
        finally { Event.current=null; UiNative.DebugMousePositionEnabled=false; UiNative.DebugMouseDown=false; UiNative.DebugMouseUp=false; UiNative.DebugHotControl=0; }
    }
    private static void DefaultCounterAndWorkshop()
    {
        using(var counter=new CounterPageExample())
        {
            counter.Bindings.Set("count",9);
            DefaultWidgetsRoundTrip(counter.Bindings,"increment",()=>counter.Count.Value,()=>counter.Operations,"Count ","counter");
        }
        var service=new WorkshopService();for(int i=0;i<9;i++)service.Complete();
        using var projection=new WorkshopProjectionExample(service);
        DefaultWidgetsRoundTrip(projection.Bindings,"complete",()=>projection.Completed.Value,()=>service.Requests,"Completed ","safe workshop projection");
    }
    private static void DefaultWidgetsRoundTrip(UiBindings bindings,string command,Func<int> value,Func<int> requests,string prefix,string example)
    {
        var metrics=new ContentMetrics();
        using var host=Host("<Widget Kind=\"input/button\" Id=\"add\" ActionBind=\""+command+"\" Text=\"Add\" /><Widget Kind=\"text/wrapped\" Id=\"caption\" Bind=\"text\" />",bindings,metrics);
        IList labels=(IList)(typeof(Verse.Widgets).GetField("LabelTexts",BindingFlags.Public|BindingFlags.Static)?.GetValue(null)
            ?? throw new Exception("Verse stub label recorder missing"));
        bool PaintedSince(int start,string text) { for(int i=start;i<labels.Count;i++)if((string)labels[i]! == text)return true;return false; }
        int first=labels.Count;
        Pump(host,EventType.Repaint,new Vector2(-10,-10));
        float height=host.Session.GetNodeByElementId("caption")!.Rect!.Value.height;
        Check(PaintedSince(first,prefix+"9") && metrics.LastText==prefix+"9",example+" default wrapped text paints the synchronized value used by actual measurement");
        Rect button=host.Session.GetNodeByElementId("add")!.Rect!.Value;
        var point=new Vector2(View.x+button.x+button.width/2,View.y+button.y+button.height/2);
        int before=requests();
        Pump(host,EventType.MouseDown,point);
        Check(value()==9 && requests()==before,example+" default Button press captures without executing the command");
        int releaseLabels=labels.Count;
        Pump(host,EventType.MouseUp,point);
        Check(value()==10 && requests()==before+1 && PaintedSince(releaseLabels,prefix+"9"),example+" default Button qualified release dispatches once while current text keeps its synchronized input");
        int nextLabels=labels.Count;
        Pump(host,EventType.Repaint,point);
        Check(PaintedSince(nextLabels,prefix+"10") && metrics.LastText==prefix+"10" && host.Session.GetNodeByElementId("caption")!.Rect!.Value.height==height+2,
            example+" next legal Host synchronization updates real wrapped text and content-dependent geometry without notification");
        Pump(host,EventType.Repaint,point);
        Check(requests()==before+1,example+" repaint does not repeat the established business operation");
    }
    private static void CounterAndCheckbox()
    {
        using var example=new CounterPageExample();
        var b=example.Bindings;
        int observations=0; bool seenSelected=false; int seenCount=0;
        using var owner=example.Runtime.CreateOwner("adapter-business");
        owner.Respond("both",()=>{ seenSelected=example.Selected.Value; seenCount=example.Count.Value; observations++; });
        using var host=Host("<Widget Kind=\"input/checkbox\" Id=\"toggle\" Bind=\"selected\" ActionBind=\"changed\" />",b);
        Pump(host,EventType.Repaint,new Vector2(-10,-10));
        Rect rect=host.Session.GetNodeByElementId("toggle")!.Rect!.Value;
        var point=new Vector2(View.x+rect.x+10,View.y+rect.y+rect.height/2);
        Pump(host,EventType.MouseDown,point);
        int before=observations;
        Pump(host,EventType.MouseUp,point);
        Check(example.Selected.Value && example.Count.Value==1 && example.Operations==1,"real Checkbox DrawFrame takes one release Set then Invoke");
        Check(observations==before+1 && seenSelected && seenCount==1,"no automatic response between Checkbox Set and command");
        b.Set("count",99);
        using(b.BeginOperation()) { for(int i=0;i<160;i++) b.Set("count",i); Check(seenCount==99,"capacity crossing does not flush automatic responses early"); }
        Check(seenCount==159,"large operation ends at outer batch not old notification capacity");
        int operations=example.Operations;
        Check(!b.TryInvokeCommand("increment") && example.Operations==operations,"execution rechecks latest command qualification");
        host.Close(); host.Close(); b.Set("count",7);
        Check(example.Observed==7 && seenCount==7,"Host Close twice releases view only and independent business responses continue");
    }
    private static void LegacyPurityBoundary()
    {
        var b=new UiBindings(); int operations=0;
        b.BindCommand("legacy-write",()=>operations++);
        b.BindReadOnly("value",()=>{ b.Invoke("legacy-write"); return 1; });
        var probe=new Probe { OnMeasure=()=>b.Invoke("legacy-write") };
        UiWidgetRegistry.Register("reactive","legacy-probe",()=>probe,Array.Empty<string>());
        using(var host=Host("<Widget Kind=\"legacy-probe\" Id=\"legacy\" />",b))
            Pump(host,EventType.Repaint,new Vector2(-10,-10));
        Check(operations>1,"legacy getter and custom Measure keep original operation conditions");
        var runtime=new UiReactiveRuntime(); var source=runtime.Int(); var tracked=new UiBindings(runtime);
        tracked.BindTracked("value",()=>{ source.Value++; return source.Value; });
        bool refused=false;
        using(var host=Host("<Widget Kind=\"legacy-probe\" Id=\"new\" />",tracked))
        {
            try { Pump(host,EventType.Repaint,new Vector2(-10,-10)); }
            catch(InvalidOperationException ex) { refused=ex.ToString().Contains("forbidden"); }
        }
        Check(refused && source.Value==0,"new BindTracked getter refuses writes without retrofitting legacy purity");
    }
    private static void SameRoundInputs()
    {
        var runtime=new UiReactiveRuntime(); var value=runtime.Int(1); var b=new UiBindings(runtime);
        int reads=0; b.BindTracked("value",()=>{reads++;return value.Value;});
        b.BindCommand("change",()=>{ value.Value=20; Check(b.Get<int>("value")==20,"command Get bypasses presentation snapshot"); });
        var probe=new Probe();
        UiWidgetRegistry.Register("reactive","probe",()=>probe,Array.Empty<string>());
        using var host=Host("<Widget Kind=\"probe\" Id=\"probe\" />",b);
        probe.OnDraw=()=>b.Invoke("change");
        Pump(host,EventType.Repaint,new Vector2(-10,-10));
        Check(probe.Measured==1 && probe.Drawn==1 && probe.Height==10 && value.Value==20,"synchronize measure and draw share one presentation version despite input operation");
        probe.OnDraw=null;
        Pump(host,EventType.Repaint,new Vector2(-10,-10));
        Check(probe.Measured==20 && probe.Drawn==20 && probe.Height==200,"next allowed Host frame adopts value and recomputes geometry without manual notification");
        int before=reads; host.Close(); value.Value=30;
        Check(reads==before,"closed Host no longer evaluates view calculations");
        bool fail=true;
        b.BindTracked<int>("broken",()=>fail?throw new Exception("host-sync"):1);
        using var failingHost=Host("<Widget Kind=\"probe\" Id=\"probe\" />",b);
        bool refused=false;
        try { Pump(failingHost,EventType.Repaint,new Vector2(-10,-10)); } catch(InvalidOperationException ex) { refused=ex.ToString().Contains("presentation/broken"); }
        Check(refused && failingHost.Session.GetNodeByElementId("probe")==null,"failed tracking publishes neither half-layout nor hit nodes");
        fail=false; Pump(failingHost,EventType.Repaint,new Vector2(-10,-10));
        Check(probe.Drawn==30,"new explicit frame after failure has restored context and no replayed operation");
    }
    private static void IndependentProjection()
    {
        var service=new WorkshopService(); using var projection=new WorkshopProjectionExample(service);
        var probe=new WorkshopProbe(); UiWidgetRegistry.Register("reactive","workshop",()=>probe,Array.Empty<string>());
        using var host=Host("<Widget Kind=\"workshop\" Id=\"orders\" />",projection.Bindings);
        projection.Bindings.Invoke("complete"); Pump(host,EventType.Repaint,new Vector2(-10,-10));
        Check(probe.Completed==1 && probe.Height==11,"real Host consumes independent safe business projection through same neutral runtime");
        host.Close(); service.Complete(); projection.Apply(service.ReadSnapshot());
        Check(projection.LastBusinessObservation==2,"external service and independent projection remain active after Host closes");
    }
    private sealed class Probe:IUiWidget
    {
        public string Kind=>"probe";
        internal int Measured,Drawn; internal float Height; internal Action? OnDraw; internal Action? OnMeasure;
        public void Configure(UiElementSpec spec) { }
        public void Validate(IUiBindings bindings,string path)=>bindings.ValidateValue<int>("value",path);
        public float Measure(UiWidgetContext ctx) { OnMeasure?.Invoke(); Measured=ctx.Bindings.Get<int>("value");return Measured*10; }
        public void Draw(Rect rect,UiWidgetContext ctx) { OnDraw?.Invoke(); Drawn=ctx.Bindings.Get<int>("value"); Height=rect.height; }
    }
    private sealed class WorkshopProbe:IUiWidget
    {
        public string Kind=>"workshop"; internal int Completed;internal float Height;
        public void Configure(UiElementSpec spec) { }
        public void Validate(IUiBindings bindings,string path)=>bindings.ValidateValue<int>("completed",path);
        public float Measure(UiWidgetContext ctx)=>ctx.Bindings.Get<string>("text").Length;
        public void Draw(Rect rect,UiWidgetContext ctx) { Completed=ctx.Bindings.Get<int>("completed");Height=rect.height; }
    }
    private sealed class ContentMetrics:ITextMetrics
    {
        internal string LastText="";
        public float MeasureText(string text,UiFont font,float width) { LastText=text;return text.Length*2; }
        public float MeasureWidth(string text,UiFont font)=>text.Length*8;
    }
    private sealed class Metrics:ITextMetrics
    {
        public float MeasureText(string text,UiFont font,float width)=>24;
        public float MeasureWidth(string text,UiFont font)=>text.Length*8;
    }
    private sealed class Translation:IUiTranslation
    {
        public string Translate(string key)=>key;public int TranslationRevision=>0;
    }
}

