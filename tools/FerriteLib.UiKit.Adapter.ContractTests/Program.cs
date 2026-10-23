using System;
using System.Collections;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;
using NativeWidgets=Verse.Widgets;
static class Program
{
    static int checks;
    static void Check(bool result,string label){if(!result)throw new Exception(label);checks++;Console.WriteLine("PASS ADAPTER "+label);}
    static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Public|BindingFlags.Static)??throw new Exception("Missing stub fact: "+name);
    sealed class Metrics:ITextMetrics { public float MeasureWidth(string text,UiFont font)=>text.Length*7;public float MeasureText(string text,UiFont font,float width)=>20; }
    sealed class Translation:IUiTranslation {public int TranslationRevision=>0;public string Translate(string key)=>key;}
    static int Main(){try{Run();return 0;}catch(Exception e){Console.Error.WriteLine(e.GetType().FullName+": "+e.Message);return 1;}}
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Run()
    {
        using var execution=new VerseUiExecution();var rect=new UiLayoutRect(0,0,200,30);var control=new UiControlKey("edit/current");
        Event.current=new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(10,10)};execution.CaptureFrame();
        GUI.color=new Color(.2f,.3f,.4f,1);Text.Font=GameFont.Medium;Text.Anchor=TextAnchor.LowerRight;Text.WordWrap=false;
        int before=(int)Field(typeof(NativeWidgets),"TextControlCalls").GetValue(null)!;
        Field(typeof(NativeWidgets),"TextControlOverride").SetValue(null,new Func<Rect,string,string>((r,s)=>"candidate"));
        var result=execution.Edit(new UiNativeEdit(control,rect,"model",UiFont.Tiny,false,true,true,false));
        Check(result.Text=="candidate"&&result.Focused&&execution.HasFocus(control),"one native editor execution preserves its valid focus");
        Check((int)Field(typeof(NativeWidgets),"TextControlCalls").GetValue(null)! ==before+1,"native control executes exactly once");
        Check(Text.Font==GameFont.Medium&&Text.Anchor==TextAnchor.LowerRight&&!Text.WordWrap&&GUI.color.r==.2f,"temporary font/anchor/wrap/colour restore after edit");
        Field(typeof(NativeWidgets),"TextControlDuring").SetValue(null,new Action(()=>{Text.Font=GameFont.Tiny;GUI.color=Color.white;throw new Exception("native-control-fault");}));
        bool failed=false;try{execution.Edit(new UiNativeEdit(control,rect,"model",UiFont.Small,false,true,true,false));}catch(Exception ex){failed=ex.Message=="native-control-fault";}
        Check(failed&&Text.Font==GameFont.Medium&&GUI.color.r==.2f&&execution.HasFocus(control),"native throw restores temporary state, not legitimate focus");
        Field(typeof(NativeWidgets),"TextControlDuring").SetValue(null,null);Field(typeof(NativeWidgets),"TextControlOverride").SetValue(null,null);
        Event.current=new Event{type=EventType.KeyDown,keyCode=KeyCode.X};execution.CaptureFrame();execution.Edit(new UiNativeEdit(control,rect,"model",UiFont.Small,false,false,true,false));
        Check(Event.current.type==EventType.Used,"readonly native edit blocks mutating key before executing selection engine");
        Event.current=new Event{type=EventType.KeyDown,keyCode=KeyCode.C,control=true};execution.CaptureFrame();execution.Edit(new UiNativeEdit(control,rect,"model",UiFont.Small,false,false,true,false));
        Check(Event.current.type==EventType.KeyDown,"readonly native edit keeps selection/copy route");
        var a=new UiControlKey("pointer/a");Event.current=new Event{type=EventType.MouseDown};execution.CaptureFrame();execution.PrepareControl(a);execution.Capture(a);int held=GUIUtility.hotControl;
        Event.current=new Event{type=EventType.MouseDrag};execution.CaptureFrame();execution.PrepareControl(new UiControlKey("new-earlier-sibling"));execution.PrepareControl(a);
        Check(execution.HasCapture(a)&&GUIUtility.hotControl==held,"logical owner keeps original native capture through sequence reorder");execution.Release(a);
        Check(GUIUtility.hotControl==0,"only matching native owner releases capture");
        var texture=new Texture2D(40,20);UiImage token=execution.Images.Add(texture);using var foreign=new VerseUiExecution();
        int images=((IList)Field(typeof(GUI),"DrawTextureRects").GetValue(null)!).Count;
        execution.Image(rect,token,UiColour.White);foreign.Image(rect,token,UiColour.White);
        Check(((IList)Field(typeof(GUI),"DrawTextureRects").GetValue(null)!).Count==images+1,"neutral image token resolves only inside its native resource scope");execution.Images.Release(token);execution.Image(rect,token,UiColour.White);
        Check(((IList)Field(typeof(GUI),"DrawTextureRects").GetValue(null)!).Count==images+1&&texture.width==40,"resource release drops reference without destroying supplier texture");
        Field(typeof(NativeWidgets),"ScrollOffsetOverride").SetValue(null,new Func<Vector2,Vector2>(_=>new Vector2(0,17)));
        UiLayoutPoint seen=default;execution.Scroll(rect,new UiLayoutRect(0,0,200,200),default,true,p=>seen=p);
        Check(seen.Y==17&&(int)Field(typeof(GUI),"GroupDepth").GetValue(null)! ==0&&(int)Field(typeof(NativeWidgets),"ScrollViewDepth").GetValue(null)! ==0,"same-stack scroll exposes immediate offset and restores native groups");
        bool clipFailed=false;try{execution.Clip(rect,()=>throw new Exception("clip-fault"));}catch{clipFailed=true;}
        Check(clipFailed&&(int)Field(typeof(GUI),"GroupDepth").GetValue(null)! ==0,"native clip unwinds on child failure");
        Field(typeof(NativeWidgets),"ScrollOffsetOverride").SetValue(null,null);
        int lower=0,upper=0;var bindings=new UiBindings();bindings.BindCommand("lower",()=>lower++);bindings.BindCommand("upper",()=>upper++);
        string xml="<UiPage Schema='2' Source='adapter'><Scroll Id='scroll' Height='60' Padding='0'><Overlay Id='layers' Height='120' Padding='0'><Widget Id='lower' Kind='input/button' ActionBind='lower' Text='Lower' Height='120'/><Widget Id='upper' Kind='input/button' ActionBind='upper' Text='Upper' Height='20' AlignY='Top' OffsetY='30'/></Overlay></Scroll></UiPage>";
        using(var host=new UiHost("adapter",UiLayoutManifest.Parse(xml),bindings,new UiTheme(),new Metrics(),new Translation()))
        {
            void Pump(EventType phase){Event.current=new Event{type=phase,button=0,mousePosition=new Vector2(20,20)};host.DrawFrame(new UiLayoutRect(0,0,200,60),execution.CaptureFrame(),execution);}
            Pump(EventType.Repaint);
            Field(typeof(NativeWidgets),"ScrollOffsetOverride").SetValue(null,new Func<Vector2,Vector2>(_=>new Vector2(0,17)));
            Pump(EventType.MouseDown);Pump(EventType.MouseUp);
            Check(lower==0&&upper==1,"real Host hit ordering uses the offset changed by BeginScrollView in this same call");
            Field(typeof(NativeWidgets),"ScrollOffsetOverride").SetValue(null,null);
        }
        StandaloneNative();
        FerriteLib.UiKit.Tests.KernelNativeEventConsumptionTests.Run(Check);
        Check(FerriteLib.UiKit.Tests.KernelCloseoutEditingTests.RunAll()==0,"unchanged closeout E/N/composite/readback counterexamples migrate through actual 1.6 execution");
        int readonlyChecks=0;
        FerriteLib.UiKit.AuthorExamples.ReadOnlyOutputs.Verify(new Metrics(),new Translation(),(value,label)=>{Check(value,"READONLY "+label);readonlyChecks++;});
        Check(readonlyChecks==92,"all original 92 public-only readonly-view counterexamples execute on the changed product");
        Console.WriteLine("ADAPTER PASS "+checks+" protocol assertions; native font/IME/window acceptance remains pending human playtest");
    }
    static void StandaloneNative()
    {
        GUI.FocusControl(null);GUIUtility.hotControl=0;
        float model=10;int writes=0;bool failRead=false;
        var author=new FerriteLib.UiKit.AuthorExamples.StandaloneNativeNumber(new Metrics(),new Translation(),()=>failRead?throw new Exception("display-fault"):model,v=>{model=v;writes++;failRead=true;});
        var rect=new UiLayoutRect(0,0,200,30);
        int calls=(int)Field(typeof(NativeWidgets),"TextControlCalls").GetValue(null)!;
        Field(typeof(NativeWidgets),"TextControlOverride").SetValue(null,new Func<Rect,string,string>((r,t)=>"23"));
        Event.current=new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(20,10)};
        author.OnGUI(rect);
        Check(author.Editor.Draft=="23"&&author.Editor.Focused&&model==10&&writes==0&&(int)Field(typeof(NativeWidgets),"TextControlCalls").GetValue(null)! == calls+1,"external no-friend independent native author uses public session scope and the actual synchronous deferred editor");
        Field(typeof(NativeWidgets),"TextControlOverride").SetValue(null,null);
        Event.current=new Event{type=EventType.KeyDown,keyCode=KeyCode.Return};
        bool failed=false;try{author.OnGUI(rect);}catch(Exception e){failed=e.Message=="display-fault";}
        Check(failed&&model==23&&writes==1,"standalone native scope commits once before display read failure");
        failRead=false;Event.current=new Event{type=EventType.Repaint};author.OnGUI(rect);
        Check(writes==1&&author.Editor.Draft=="23","standalone native repaint reads recovered actual model without replay");
        Event.current=new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(20,10)};
        Field(typeof(NativeWidgets),"TextControlDuring").SetValue(null,new Action(()=>{GUI.color=Color.white;Text.Font=GameFont.Tiny;throw new Exception("standalone-native-fault");}));
        GUI.color=new Color(.2f,.3f,.4f,1);Text.Font=GameFont.Medium;
        failed=false;try{author.OnGUI(rect);}catch(Exception e){failed=e.Message=="standalone-native-fault";}
        Check(failed&&GUI.color.r==.2f&&Text.Font==GameFont.Medium&&author.Editor.Focused,"standalone real native failure restores temporary GUI state without rolling back valid focus");
        Field(typeof(NativeWidgets),"TextControlDuring").SetValue(null,null);
        author.Dispose();bool stale=false;try{_ = author.Editor;}catch(ObjectDisposedException){stale=true;}
        Check(stale&&GUI.GetNameOfFocusedControl()=="","standalone scope disposal releases native focus and rejects stale owner handle");
    }

}
