using System;
using System.Collections.Generic;
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
    private static UiHost Host(string content, UiBindings bindings)
        => new("reactive", UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"reactive\"><Column Padding=\"0\" Gap=\"0\">"+content+"</Column></UiPage>"), bindings, UiTheme.Vanilla, new Metrics(), new Translation());
    internal static int RunAll()
    {
        try
        {
            UiWidgetRegistry.Clear(); UiWidgetRegistry.InitializeCore();
            CounterAndCheckbox();
            SameRoundInputs();
            IndependentProjection();
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine("  FAIL: REACTIVE-HOST "+ex); return 1; }
        finally { Event.current=null; UiNative.DebugMousePositionEnabled=false; UiNative.DebugMouseDown=false; UiNative.DebugMouseUp=false; UiNative.DebugHotControl=0; }
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
        internal int Measured,Drawn; internal float Height; internal Action? OnDraw;
        public void Configure(UiElementSpec spec) { }
        public void Validate(IUiBindings bindings,string path)=>bindings.ValidateValue<int>("value",path);
        public float Measure(UiWidgetContext ctx) { Measured=ctx.Bindings.Get<int>("value");return Measured*10; }
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

