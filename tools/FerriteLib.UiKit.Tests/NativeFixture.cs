using System;
using System.Collections.Generic;
using System.Collections;
using System.Runtime.CompilerServices;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;
namespace FerriteLib.UiKit.Tests;

// Test input translation only. Product controls still run their real pointer/edit state machines and native adapter.
// Every call carries its actual owner. These fields are fixture inputs, not a product ambient backend.
internal static class NativeFixture
{
    internal static bool DebugMousePositionEnabled=false,DebugMouseDown=false,DebugMouseDrag=false,DebugMouseUp=false,DebugEnter=false,DebugFocusLost=false;
    internal static UiLayoutPoint DebugMousePosition;
    internal static float DebugWheelNotches=0;
    internal static int DebugControlIdCounter=0;
    internal static int DebugHotControl {get=>GUIUtility.hotControl;set=>GUIUtility.hotControl=value;}
    internal static Func<UiLayoutRect,bool>? ButtonOverride=null;
    internal static Func<UiLayoutRect,float,float,float,float>? SliderOverride=null;
    internal static Action<string>? Trace=null;
    private static Func<UiLayoutRect,string,string>? textFieldOverride;
    internal static Func<UiLayoutRect,string,string>? TextFieldOverride
    {
        get=>textFieldOverride;
        set
        {
            textFieldOverride=value;
            typeof(Verse.Widgets).GetField("TextControlOverride")!.SetValue(null,value==null?null:new Func<Rect,string,string>((r,s)=>value(VerseUiExecution.Rect(r),s)));
        }
    }
    private sealed class Owner
    {
        internal VerseUiExecution Execution=new();
        internal UiNode? Node;
        internal Event? NativeEvent;
        internal UiFrame? Frame;
    }
    private static readonly ConditionalWeakTable<UiSession,Owner> owners=new();
    internal static readonly VerseUiExecution Paint=new();
    // Native stub recorders intentionally keep native values. Convert only at the assertion read edge.
    internal static T Record<T>(object? value)
    {
        if(value is T actual)return actual;
        object? neutral=value is Rect r?VerseUiExecution.Rect(r):value is Color c?VerseUiExecution.Colour(c):value is Vector2 p?VerseUiExecution.Point(p):value;
        return neutral is T converted?converted:throw new InvalidCastException("Native recording is not "+typeof(T).Name+": "+value?.GetType().Name);
    }
    internal static List<T> Records<T>(object value)
    {var result=new List<T>();foreach(object? item in (IEnumerable)value)result.Add(Record<T>(item));return result;}
    internal sealed class LocalState
    { internal string EditText="";internal float FloatValue=0;internal bool Focused=false,Dragging=false;internal int Cursor=0; }
    internal static LocalState Custom(UiNode? node,string key="")=>(node??throw new InvalidOperationException("Custom state requires its actual node.")).GetState(key,()=>new LocalState());
    internal static LocalState Custom(UiSession session,string key)=>Custom(session.Root,key);
    internal static LocalState Custom(UiSession session,UiNodeId node,string key="")=>Custom(session.GetNode(node)??throw new Exception("Missing actual test node: "+node),key);
    internal static IReadOnlyDictionary<string,LocalState> CustomSlots(UiNode node)
    {
        var owned=(Dictionary<string,object>)typeof(UiNode).GetField("customState",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(node)!;
        var result=new Dictionary<string,LocalState>();foreach(var p in owned)if(p.Value is LocalState state)result.Add(p.Key,state);return result;
    }
    internal static IReadOnlyDictionary<string,LocalState> CustomSlots(UiSession session,UiNodeId node)=>session.GetNode(node) is UiNode owned?CustomSlots(owned):new Dictionary<string,LocalState>();
    internal static bool Released(UiNode? node)
    {
        if(node==null||CustomSlots(node).Count!=0)return false;
        try{node.GetState("after-removal",()=>new object());return false;}
        catch(ObjectDisposedException){return true;}
    }
    internal static UiControlState Slot(UiSession session,string key)
    {
        UiNode node=Of(session).Node??session.GetNodeByElementId(key)??throw new InvalidOperationException("Test must name its actual editor node: "+key);
        return session.GetControlState(node.Id,key);
    }
    internal static UiControlState Slot(UiSession session,UiNodeId node,string key)=>session.GetControlState(node,key);
    internal static float Number(UiControlState state)=>float.Parse(state.Draft,System.Globalization.CultureInfo.InvariantCulture);
    private static VerseUiExecution? ExistingExecution(UiSession session)
    {
        IUiExecution execution;
        try { execution=session.Execution; }
        catch(InvalidOperationException) { return null; }
        return execution as VerseUiExecution??throw new InvalidOperationException("Native fixture cannot replace an explicitly supplied non-native execution owner.");
    }
    private static Owner Of(UiSession session)=>owners.GetValue(session,s=>
    {
        VerseUiExecution? execution=ExistingExecution(s);
        return execution==null?new Owner():new Owner {Execution=execution};
    });
    // Only the test boundary that just captured an external frame can attest it belongs to Event.current.
    internal static void AttachCurrentFrame(UiSession session)
    {Owner owner=Of(session);owner.Execution=ExistingExecution(session)??throw new InvalidOperationException("No externally captured frame to attach.");owner.Frame=session.Input;owner.NativeEvent=Event.current;}
    internal static UiWidgetContext Context(string source,UiSession session,ITextMetrics metrics,UiTheme theme,IUiTranslation translation,IUiBindingReader bindings,float viewWidth,string elementPath,UiLayoutPoint windowOrigin=default,UiNodeId elementId=default,UiNode? node=null,IReadOnlyList<UiStyleDeclaration>? styleChain=null)
    {
        Owner owner=Of(session);
        node??=elementId.IsNone?session.GetOrCreateNode(UiNodeId.Root(new UiElementSpec(elementPath.Replace('/','_'),"fixture"),0),"fixture",0):session.GetOrCreateNode(elementId,"fixture",0);
        owner.Node=node;
        var ctx=new UiWidgetContext(source,session,metrics,theme,translation,bindings,viewWidth,elementPath,windowOrigin,node.Id,node,styleChain);
        Prepare(session);return ctx;
    }
    internal static UiWidgetContext Context(UiSession session)=>new("fixture",session,new Metrics(),new UiTheme(),new Translation(),new UiBindings(),400,"fixture",node:Of(session).Node??session.GetOrCreateNode(UiNodeId.Root(new UiElementSpec("fixture","fixture"),0),"fixture",0));
    // Explicit boundary for old fixture inputs. Never called by a draw/control helper.
    internal static void BeginDebugEvent()
    {
        EventType phase=DebugEnter?EventType.KeyDown:DebugFocusLost||DebugMouseDown?EventType.MouseDown:DebugMouseDrag?EventType.MouseDrag:DebugMouseUp?EventType.MouseUp:DebugWheelNotches!=0?EventType.ScrollWheel:EventType.Repaint;
        Event.current=new Event {type=phase,button=0,keyCode=DebugEnter?KeyCode.Return:KeyCode.None,
            mousePosition=VerseUiExecution.Point(DebugFocusLost?new UiLayoutPoint(-10000,-10000):DebugMousePosition),delta=new Vector2(0,DebugWheelNotches)};
    }
    internal static void ResetInput()
    {
        Event.current=null;GUIUtility.hotControl=0;GUI.FocusControl(null);
        DebugMousePositionEnabled=DebugMouseDown=DebugMouseDrag=DebugMouseUp=DebugEnter=DebugFocusLost=false;
        DebugWheelNotches=0;ButtonOverride=null;SliderOverride=null;TextFieldOverride=null;Trace=null;
    }
    internal static void Prepare(UiSession session,bool? inputAllowed=null)
    {
        Owner owner=Of(session);Event? current=Event.current;
        // An actual window callback may already have captured and set its own frame. Adopt it,
        // never swap its execution for a fixture port or restart its native control allocation.
        if(owner.Frame!=null&&!ReferenceEquals(session.Input,owner.Frame))
        {
            owner.Execution=ExistingExecution(session)??throw new InvalidOperationException("Lost native execution owner.");
            owner.Frame=session.Input;owner.NativeEvent=current;
        }
        if(owner.Frame==null||!ReferenceEquals(current,owner.NativeEvent))
        {
            owner.NativeEvent=current;
            owner.Frame=owner.Execution.CaptureFrame(inputAllowed??session.PointerInputAllowed);
            session.SetFrame(owner.Frame,owner.Execution);
        }
        // A native control outside this adapter can consume after capture. Preserve the captured
        // raw phase and owner while synchronizing this one-way transition into the SAME frame.
        if(current?.type==EventType.Used)owner.Frame.Consume();
        session.Controls.Trace=Trace;
    }
    internal static void DrawFrame(UiHost host,UiLayoutRect viewport){Prepare(host.Session);host.DrawFrame(viewport,host.Session.Input,Of(host.Session).Execution);}
    internal static void DrawFrame(UiHost host,Rect viewport)=>DrawFrame(host,VerseUiExecution.Rect(viewport));
    internal static void DrawFrame(UiHost host,Rect viewport,Window owner){Prepare(host.Session,!owner.IsOpen||Find.WindowStack.GetsInput(owner));host.DrawFrame(VerseUiExecution.Rect(viewport),host.Session.Input,Of(host.Session).Execution);}
    internal static void Draw(UiHost host,UiLayoutRect viewport,UiLayoutSnapshot snapshot){Prepare(host.Session);host.Draw(viewport,snapshot);}
    internal static void Draw(IUiWidget widget,UiLayoutRect rect,UiWidgetContext ctx){Prepare(ctx.Session);widget.Draw(rect,ctx);}
    internal static UiEditSession<T> Editor<T>(UiWidgetContext ctx,string binding,Func<T,string> format,UiEditParser<T> parse)=>ctx.Controls.Editor(ctx,binding,format,parse);
    internal static void Edit<T>(UiLayoutRect rect,UiWidgetContext ctx,string binding,T model,Func<T,string> format,UiEditParser<T> parse,bool live,bool multiline,Action<T> request,Func<T> read){Prepare(ctx.Session);ctx.Controls.Edit(rect,ctx,binding,model,format,parse,live,multiline,request,read);}
    internal static UiInteractionTarget InteractionTarget(UiWidgetContext ctx,string binding,string part="",object? key=null)=>ctx.Controls.InteractionTarget(ctx,binding,part,key);
    internal static UiInteractionResult Activation(UiLayoutRect rect,UiWidgetContext ctx,string binding="activation",string part="",object? itemKey=null,bool writable=true)
    {Prepare(ctx.Session);return ctx.Controls.Activation(rect,ctx,binding,part,itemKey,writable);}
    internal static UiInteractionResult Range(UiLayoutRect rect,UiWidgetContext ctx,string binding,float value,float min,float max,bool writable=true)
    {Prepare(ctx.Session);return ctx.Controls.Range(rect,ctx,binding,value,min,max,writable);}
    internal static UiInteractionResult Point(UiLayoutRect rect,UiLayoutRect plot,UiWidgetContext ctx,string binding,object key,float x,float y,bool writable=true)
    {Prepare(ctx.Session);return ctx.Controls.Point(rect,plot,ctx,binding,key,x,y,writable);}
    internal static bool Button(UiLayoutRect rect,UiWidgetContext ctx)=>Activation(rect,ctx).Request;
    internal static bool CanReceivePointerPress(UiLayoutRect rect,UiWidgetContext ctx){Prepare(ctx.Session);return ctx.Controls.CanReceivePointerPress(rect,ctx);}
    internal static bool IsMouseOver(UiLayoutRect rect,UiWidgetContext ctx){Prepare(ctx.Session);return ctx.Controls.IsMouseOver(rect,ctx);}
    internal static bool IsMouseOver(UiLayoutRect rect)=>Mouse.IsOver(VerseUiExecution.Rect(rect));
    internal static UiLayoutPoint WindowPointer(UiWidgetContext ctx){Prepare(ctx.Session);return ctx.Controls.WindowPointer(ctx);}
    internal static UiLayoutPoint PointerPosition()=>VerseUiExecution.Point(Event.current?.mousePosition??default);
    internal static bool IsPointerDown()=>Event.current?.type==EventType.MouseDown;
    internal static bool IsPointerUp()=>Event.current?.type==EventType.MouseUp;
    internal static bool IsEnterPressed()=>Event.current?.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Return;
    internal static bool IsLayoutEvent()=>Event.current?.type==EventType.Layout;
    internal static float PointerWheelNotches()=>Event.current?.type==EventType.ScrollWheel?Event.current.delta.y:0;
    internal static bool DropdownButton(UiLayoutRect rect,string binding,UiWidgetContext ctx){Prepare(ctx.Session);return ctx.Controls.DropdownButton(rect,binding,ctx);}
    internal static UiInteractionResult PopupOption(UiLayoutRect rect,string binding,UiWidgetContext ctx,object? key,bool selectable=true){Prepare(ctx.Session);return ctx.Controls.PopupOption(rect,binding,ctx,key,selectable);}
    internal static bool HasHotControl(UiControlKey control)=>throw new InvalidOperationException("Native capture assertions must name their execution owner.");
    internal static UiControlKey Key(int id)=>new("fixture/"+id);
    // Only for explicit ownership/lifetime fixtures, not an alternative pointer state machine.
    internal static void Capture(UiSession session,UiControlKey key,UiNode? node=null)
    {Prepare(session);session.Execution.PrepareControl(key);session.CaptureHotControlForNode(key,node??session.Root);}
    internal static bool HasHotControl(UiSession session,UiControlKey key)=>session.Execution.HasCapture(key);
    internal static void ConsumePointerEvent()=>Event.current?.Use();
    internal static void ConsumeKeyEvent()=>Event.current?.Use();
    internal static void TakePopupWheelIfCovering(UiSession session){Prepare(session);session.Controls.TakePopupWheelIfCovering(session);}
    internal static void DismissPopupOnOutsidePress(UiSession session){Prepare(session);session.Controls.DismissPopupOnOutsidePress(session);}
    private sealed class Metrics:ITextMetrics{public float MeasureWidth(string s,UiFont f)=>s.Length*8;public float MeasureText(string s,UiFont f,float w)=>16;}
    private sealed class Translation:IUiTranslation{public int TranslationRevision=>0;public string Translate(string key)=>key;}
}
