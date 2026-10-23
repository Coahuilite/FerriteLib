using System;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

// Each native event is made exactly once here. Helpers must never manufacture its replacement.
internal static class KernelNativeEventConsumptionTests
{
    internal static int RunAll()
    {try{Run((condition,label)=>{if(!condition)throw new Exception(label);Console.WriteLine("  ok: NATIVE-EVENT "+label);});return 0;}catch(Exception error){Console.Error.WriteLine("  FAIL: NATIVE-EVENT "+error);return 1;}}
    internal static void Run(Action<bool,string> check)
    {
        using var first = new UiSession();
        using var second = new UiSession();
        var top = NativeFixture.Context("event",first,new Metrics(),new UiTheme(),new Translation(),new UiBindings(),200,"top");
        var lower = NativeFixture.Context("event",first,new Metrics(),new UiTheme(),new Translation(),new UiBindings(),200,"lower");
        var foreign = NativeFixture.Context("event",second,new Metrics(),new UiTheme(),new Translation(),new UiBindings(),200,"foreign");
        var rect = new UiLayoutRect(0,0,100,30);
        NativeFixture.DebugMousePositionEnabled = true;
        NativeFixture.DebugMouseDown = true;
        NativeFixture.DebugMousePosition = new UiLayoutPoint(10,10);
        try
        {
            var down = new Event {type=EventType.MouseDown,button=0,mousePosition=new Vector2(10,10)};
            Event.current=down;
            var pressed=NativeFixture.Activation(rect,top);
            check(pressed.Captured && ReferenceEquals(Event.current,down) && down.type==EventType.Used,"actual press is consumed by its real logical owner");
            UiFrame frame=first.Input;IUiExecution execution=first.Execution;
            NativeFixture.Editor(top,"unused",s=>s,(string text,out string value)=>{value=text;return true;});
            var lowerResult=NativeFixture.Activation(rect,lower);
            var repeated=NativeFixture.Activation(rect,top);
            var foreignResult=NativeFixture.Activation(rect,foreign);
            check(ReferenceEquals(Event.current,down) && down.type==EventType.Used && ReferenceEquals(first.Input,frame) && ReferenceEquals(first.Execution,execution),"later helpers keep the same consumed native event, frame and execution owner");
            check(!lowerResult.Request && !lowerResult.Captured && !repeated.Request && !foreignResult.Captured && second.Input.Consumed,"same consumed press never replays to lower control or another Host");
            NativeFixture.DebugMouseDown=false;NativeFixture.DebugMouseUp=true;
            var up=new Event {type=EventType.MouseUp,button=0,mousePosition=new Vector2(10,10)};
            Event.current=up;
            var released=NativeFixture.Activation(rect,top);frame=first.Input;
            check(released.Request && up.type==EventType.Used,"qualified real release requests exactly once");
            repeated=NativeFixture.Activation(rect,top);lowerResult=NativeFixture.Activation(rect,lower);foreignResult=NativeFixture.Activation(rect,foreign);
            check(ReferenceEquals(Event.current,up) && ReferenceEquals(first.Input,frame) && frame.RawPhase==UiFramePhase.MouseUp && frame.Consumed && !repeated.Request && !lowerResult.Request && !foreignResult.Request,"same actual consumed release keeps raw phase but cannot request again");
            Event.current=new Event {type=EventType.MouseDown,button=0,mousePosition=new Vector2(10,10)};
            NativeFixture.Prepare(first);frame=first.Input;Event.current.Use();
            var externallyConsumed=NativeFixture.Activation(rect,top);
            check(ReferenceEquals(first.Input,frame) && frame.Consumed && !externallyConsumed.Captured && !externallyConsumed.Request,"external native Use synchronizes consumption into the already captured frame");
            using var supplied=new VerseUiExecution();
            Event.current=new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(10,10)};
            UiFrame suppliedFrame=supplied.CaptureFrame();second.SetFrame(suppliedFrame,supplied);
            Event.current.Use();NativeFixture.AttachCurrentFrame(second);
            var suppliedResult=NativeFixture.Activation(rect,foreign);
            check(ReferenceEquals(second.Input,suppliedFrame)&&ReferenceEquals(second.Execution,supplied)&&suppliedFrame.Consumed&&!suppliedResult.Captured,"explicitly attached external owner keeps its actual frame and consumed native event");
            bool wrongOwner=false;
            try{first.Controls.Editor(foreign,"foreign",s=>s,(string text,out string value)=>{value=text;return true;});}
            catch(InvalidOperationException){wrongOwner=true;}
            check(wrongOwner&&ReferenceEquals(second.Input,suppliedFrame),"foreign controls cannot redirect a context's editor to another execution owner");
        }
        finally
        {
            NativeFixture.DebugMousePositionEnabled=false;NativeFixture.DebugMouseDown=false;NativeFixture.DebugMouseUp=false;
            Event.current=null;GUIUtility.hotControl=0;
        }
    }
    private sealed class Metrics:ITextMetrics {public float MeasureWidth(string text,UiFont font)=>text.Length*7;public float MeasureText(string text,UiFont font,float width)=>20;}
    private sealed class Translation:IUiTranslation {public int TranslationRevision=>0;public string Translate(string key)=>key;}
}
