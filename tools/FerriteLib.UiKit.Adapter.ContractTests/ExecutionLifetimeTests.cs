using System;
using System.Collections;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

static class ExecutionLifetime
{
    static readonly UiLayoutRect Rect=new(0,0,200,30);
    static readonly UiNumericEdit Codec=new(0,100);
    sealed class Metrics:ITextMetrics {public float MeasureWidth(string s,UiFont f)=>s.Length*7;public float MeasureText(string s,UiFont f,float w)=>20;}
    sealed class Translation:IUiTranslation {public int TranslationRevision=>0;public string Translate(string key)=>key;}
    static UiWidgetContext Context(UiSession session,UiBindings bindings)=>session.CreateContext("author","independent",new Metrics(),new UiTheme(),new Translation(),bindings,200);
    static UiFrame Frame(VerseUiExecution execution,EventType phase,bool allowed=true)
    {
        Event.current=new Event{type=phase,button=0,mousePosition=new Vector2(10,10)};
        return execution.CaptureFrame(allowed);
    }
    static bool Refused(Action action){try{action();return false;}catch(InvalidOperationException error){return error.Message.Contains("replace borrowed execution");}}
    internal static void Run(Action<bool,string> check)
    {
        GUI.FocusControl(null);GUIUtility.hotControl=0;
        using var a=new VerseUiExecution();using var b=new VerseUiExecution();
        using(var session=new UiSession())
        {
            var bindings=new UiBindings();var context=Context(session,bindings);
            UiFrame original=Frame(a,EventType.Repaint);session.SetFrame(original,a);
            bool nullRejected=false;try{session.SetFrame(new UiFrame(UiFramePhase.MouseDown,inputAllowed:false),null!);}catch(ArgumentNullException){nullRejected=true;}
            check(nullRejected&&ReferenceEquals(session.Input,original)&&ReferenceEquals(session.Execution,a)&&session.PointerInputAllowed,"LIFETIME null execution refuses before frame/qualification/owner mutation");
            session.SetFrame(Frame(b,EventType.Repaint),b);
            check(ReferenceEquals(session.Execution,b),"LIFETIME idle standalone session legitimately changes borrowed execution");
            session.SetFrame(Frame(a,EventType.MouseDown),a);
            var press=context.Controls.Activation(Rect,context);
            original=session.Input;UiControlKey? capture=session.OwnedHotControl;int hot=GUIUtility.hotControl;
            check(press.Captured&&hot!=0&&original.Consumed,"LIFETIME standalone real native press acquires logical and native capture");
            bool refused=Refused(()=>session.SetFrame(Frame(b,EventType.MouseUp,false),b));
            check(refused&&ReferenceEquals(session.Input,original)&&ReferenceEquals(session.Execution,a)&&session.PointerInputAllowed&&session.OwnedHotControl==capture&&GUIUtility.hotControl==hot,"LIFETIME dangerous capture replacement refuses before mutating consumed input/qualification/owners");
            using(var other=new UiSession())
            {
                other.SetFrame(Frame(a,EventType.Repaint),a);
                other.SetFrame(Frame(b,EventType.Repaint),b);
            }
            check(session.OwnedHotControl==capture&&GUIUtility.hotControl==hot,"LIFETIME another idle session may share then switch the outer execution without releasing this capture");
            session.SetFrame(Frame(a,EventType.MouseUp),a);
            var release=context.Controls.Activation(Rect,context);
            check(release.Request&&session.OwnedHotControl==null&&GUIUtility.hotControl==0,"LIFETIME rejection leaves original native owner able to finish its real release once");
            session.SetFrame(Frame(b,EventType.Repaint),b);
            check(ReferenceEquals(session.Execution,b),"LIFETIME execution may switch after actual capture release");
        }
        float model=10;int writes=0;var values=new UiBindings();values.BindValue("value",()=>model,v=>{model=v;writes++;});
        var focused=new UiSession();var editorContext=Context(focused,values);
        var overrideField=typeof(Widgets).GetField("TextControlOverride",BindingFlags.Public|BindingFlags.Static)!;
        var duringField=typeof(Widgets).GetField("TextControlDuring",BindingFlags.Public|BindingFlags.Static)!;
        overrideField.SetValue(null,new Func<UnityEngine.Rect,string,string>((r,t)=>"20"));
        void Edit()=>editorContext.Controls.Edit(Rect,editorContext,"value",model,Codec.Format,Codec.Parse,false,false,v=>{model=v;writes++;},()=>model);
        focused.SetFrame(Frame(a,EventType.MouseDown),a);Edit();overrideField.SetValue(null,null);
        var editor=editorContext.Controls.Editor<float>(editorContext,"value",Codec.Format,Codec.Parse);var before=focused.Input;
        check(editor.Focused&&editor.Draft=="20"&&writes==0&&GUI.GetNameOfFocusedControl()!="","LIFETIME actual no-Host editor holds one deferred draft and native focus");
        check(Refused(()=>focused.SetFrame(Frame(b,EventType.Repaint,false),b))&&ReferenceEquals(before,focused.Input)&&ReferenceEquals(focused.Execution,a)&&editor.Draft=="20"&&editor.Focused&&writes==0,"LIFETIME focused replacement preserves old frame/focus/draft and never submits");
        editorContext.Controls.SuspendEditor(editorContext,"value");
        focused.SetFrame(Frame(b,EventType.Repaint),b);Edit();
        check(!editor.Focused&&editor.Draft=="20"&&model==10&&writes==0&&ReferenceEquals(focused.Execution,b),"LIFETIME explicit suspension allows idle replacement while retaining a reliable draft");
        duringField.SetValue(null,new Action(()=>{Text.Font=GameFont.Tiny;throw new InvalidOperationException("native-lifetime-fault");}));
        Text.Font=GameFont.Medium;focused.SetFrame(Frame(b,EventType.MouseDown),b);
        bool nativeFailed=false;try{Edit();}catch(InvalidOperationException e){nativeFailed=e.Message=="native-lifetime-fault";}
        duringField.SetValue(null,null);
        check(nativeFailed&&editor.Focused&&Text.Font==GameFont.Medium&&Refused(()=>focused.SetFrame(Frame(a,EventType.Repaint),a)),"LIFETIME native throw restores GUI but preserves legitimate focus and its execution lease");
        focused.Dispose();
        bool stale=false;try{Edit();}catch(ObjectDisposedException){stale=true;}
        check(stale&&!focused.IsActive&&GUI.GetNameOfFocusedControl()==""&&writes==0,"LIFETIME standalone disposal releases the original focus and stale context cannot replay a write");

        using(var faulting=new ReleaseFault(a))
        {
            var closing=new UiSession();var ctx=Context(closing,values);
            closing.SetFrame(Frame(a,EventType.MouseDown),faulting);
            ctx.Controls.Edit(Rect,ctx,"value",model,Codec.Format,Codec.Parse,false,false,v=>writes++,()=>model);
            closing.SetFrame(Frame(a,EventType.MouseDown),faulting);ctx.Controls.Activation(Rect,ctx);
            faulting.ThrowFocus=true;
            bool failed=false;try{closing.Dispose();}catch(AggregateException e){failed=e.ToString().Contains("release-focus-fault");}
            int calls=faulting.FocusReleases;closing.Dispose();
            check(failed&&!closing.IsActive&&closing.OwnedHotControl==null&&closing.Input.Phase==UiFramePhase.None&&GUIUtility.hotControl==0&&GUI.GetNameOfFocusedControl()==""&&faulting.FocusReleases==calls,"LIFETIME throwing native focus cleanup still ends capture/state and leaves Dispose idempotent");
        }
        var command=new UiBindings();int requests=0;command.BindCommand("go",()=>requests++);
        using(var host=new UiHost("author",UiLayoutManifest.Parse("<UiPage Schema='2' Source='author'><Widget Id='go' Kind='input/button' ActionBind='go' Text='Go'/></UiPage>"),command,new UiTheme(),new Metrics(),new Translation()))
        {
            host.DrawFrame(Rect,Frame(a,EventType.Repaint),a);
            host.DrawFrame(Rect,Frame(a,EventType.MouseDown),a);
            int pass=host.Session.Frame;before=host.Session.Input;
            bool refused=Refused(()=>host.DrawFrame(Rect,Frame(b,EventType.MouseUp,false),b));
            check(refused&&host.Session.Frame==pass&&ReferenceEquals(before,host.Session.Input)&&requests==0,"LIFETIME Host rejects capture replacement before BeginFrame or input mutation");
            host.DrawFrame(Rect,Frame(a,EventType.MouseUp),a);
            host.DrawFrame(Rect,Frame(b,EventType.Repaint),b);
            check(requests==1&&ReferenceEquals(host.Session.Execution,b),"LIFETIME same Host completes original request once then legitimately changes idle execution");
        }
        var texture=new Texture2D(40,20);UiImage token=b.Images.Add(texture);
        int images=((IList)typeof(GUI).GetField("DrawTextureRects",BindingFlags.Public|BindingFlags.Static)!.GetValue(null)!).Count;
        b.Image(Rect,token,UiColour.White);
        check(((IList)typeof(GUI).GetField("DrawTextureRects",BindingFlags.Public|BindingFlags.Static)!.GetValue(null)!).Count==images+1,"LIFETIME Host close does not dispose borrowed execution or its supplier resource references");
        var raw=new VerseUiExecution();var key=new UiControlKey("raw/owned");
        raw.CaptureFrame();raw.Edit(new UiNativeEdit(key,Rect,"value",UiFont.Small,false,true,true,false));raw.PrepareControl(key);raw.Capture(key);
        raw.Dispose();raw.Dispose();
        check(GUI.GetNameOfFocusedControl()==""&&GUIUtility.hotControl==0&&texture.width==40,"LIFETIME execution disposal releases direct native focus/capture without destroying supplier texture");
        var sameKey=new UiControlKey("field");
        a.Edit(new UiNativeEdit(sameKey,Rect,"a",UiFont.Small,false,true,true,false));
        b.Edit(new UiNativeEdit(sameKey,Rect,"b",UiFont.Small,false,true,true,false));
        string currentName=GUI.GetNameOfFocusedControl();
        check(b.HasFocus(sameKey)&&!a.HasFocus(sameKey),"LIFETIME identical public logical keys in different executions have different native focus owners");
        a.ReleaseFocus(sameKey);a.Dispose();
        check(b.HasFocus(sameKey)&&GUI.GetNameOfFocusedControl()==currentName,"LIFETIME releasing and disposing A with the same logical key cannot release B actual focus");
        b.Dispose();check(GUI.GetNameOfFocusedControl()=="","LIFETIME only B disposal releases B same-key native focus");
        GUI.FocusControl(null);GUIUtility.hotControl=0;Event.current=null;
    }
    sealed class ReleaseFault:IUiExecution,IDisposable
    {
        readonly VerseUiExecution inner;internal bool ThrowFocus;internal int FocusReleases;
        internal ReleaseFault(VerseUiExecution inner)=>this.inner=inner;
        public void PrepareControl(UiControlKey k)=>inner.PrepareControl(k);
        public bool HasCapture(UiControlKey k)=>inner.HasCapture(k);
        public bool CaptureAvailable=>inner.CaptureAvailable;
        public void Capture(UiControlKey k)=>inner.Capture(k);
        public void Release(UiControlKey k)=>inner.Release(k);
        public bool HasFocus(UiControlKey k)=>inner.HasFocus(k);
        public void ReleaseFocus(UiControlKey k){FocusReleases++;inner.ReleaseFocus(k);if(ThrowFocus)throw new InvalidOperationException("release-focus-fault");}
        public UiNativeEditResult Edit(UiNativeEdit r)=>inner.Edit(r);
        public void ConsumeEvent()=>inner.ConsumeEvent();
        public void WithState(Action body)=>inner.WithState(body);
        public void Scroll(UiLayoutRect v,UiLayoutRect c,UiLayoutPoint p,bool show,Action<UiLayoutPoint> body)=>inner.Scroll(v,c,p,show,body);
        public void Warning(string s)=>inner.Warning(s);
        public void Clip(UiLayoutRect r,Action body)=>inner.Clip(r,body);
        public void Fill(UiLayoutRect r,UiColour c)=>inner.Fill(r,c);
        public void Text(UiLayoutRect r,string s,UiFont f,UiColour c,UiTextAnchor a,bool w)=>inner.Text(r,s,f,c,a,w);
        public void Line(UiLayoutPoint a,UiLayoutPoint b,UiColour c,float w)=>inner.Line(a,b,c,w);
        public void Image(UiLayoutRect r,UiImage i,UiColour c)=>inner.Image(r,i,c);
        public void Tooltip(UiLayoutRect r,string s)=>inner.Tooltip(r,s);
        public void Dispose(){}
    }
}

