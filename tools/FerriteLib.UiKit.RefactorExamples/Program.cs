using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

static class Program
{
    static int assertions;
    static void Check(bool condition,string name) { if(!condition)throw new Exception(name);assertions++;Console.WriteLine("PASS "+name); }
    static void Main()
    {
        var binding=new UiBindings();float number=10;int writes=0,choice=1;
        binding.BindValue("number",()=>number,v=>{number=v;writes++;});
        binding.BindValue("choice",()=>choice,v=>choice=v);
        binding.BindChoices("choice",()=>new[]{new UiChoice<int>("First",1,"first-help"),new UiChoice<int>("Second",2,"second-help")});
        var a=new Row{Key="a",Text="old-a"};var b=new Row{Key="b",Text="b"};IReadOnlyList<Row> rows=new[]{a,b};
        using var items=new UiItemBindings<Row>(binding,"rows",()=>rows,r=>r.Key,s=>{s.Value("name",r=>r.Text,(r,v)=>r.Text=v);s.Command("apply",r=>r.Hits++);});
        UiWidgetRegistry.Register("author","author/visual",()=>new CustomVisual(),new[]{"Id","Kind"});
        var xml="<UiPage Schema='2' Source='author'><Templates><Row Id='row'><Widget Id='name' Kind='input/text-field' Bind='name'/></Row></Templates><Stack Id='body'><Widget Id='editor' Kind='input/number-field' Bind='number' Min='0' Max='100'/><Widget Id='choice-ui' Kind='input/dropdown' Bind='choice'/><Repeat Id='repeat' Items='rows' Template='row'/><Widget Id='custom' Kind='author/visual'/></Stack></UiPage>";
        using var host=new UiHost("author",UiLayoutManifest.Parse(xml),binding,new UiTheme(),new Metrics(),new Translation());
        var execution=new Execution();var viewport=new UiLayoutRect(0,0,600,600);
        void Frame(UiFramePhase phase,UiLayoutPoint pointer=default,UiKey key=UiKey.None) => host.DrawFrame(viewport,new UiFrame(phase,pointer,key:key),execution);
        UiLayoutSnapshot Layout()=>host.MeasureAndArrange(new UiLayoutPoint(600,600));
        Frame(UiFramePhase.Repaint);var snapshot=Layout();
        Check(snapshot.RectById.ContainsKey("name#a")&&snapshot.RectById.ContainsKey("name#b"),"actual Repeat materializes both item identities in neutral Host");
        var edit=snapshot.RectById["editor"];execution.NextText="20";Frame(UiFramePhase.MouseDown,new UiLayoutPoint(edit.X+edit.Width/2,edit.Y+edit.Height/2));
        Check(number==10&&writes==0&&execution.Edits>0,"deferred editor executes exactly once per field call without submitting draft");
        int before=execution.Edits;Frame(UiFramePhase.KeyDown,key:UiKey.Return);
        Check(number==20&&writes==1&&execution.Edits-before==3,"Enter submits current draft once; each of three real fields executes once");
        var choiceRect=Layout().RectById["choice-ui"];var point=new UiLayoutPoint(choiceRect.X+choiceRect.Width/2,choiceRect.Y+choiceRect.Height/2);
        Frame(UiFramePhase.MouseDown,point);Frame(UiFramePhase.MouseUp,point);
        Check(host.Session.OpenPopupId=="choice","typed choice trigger uses same actual capture policy");
        var anchor=host.Session.OpenPopupAnchor!.Value;var popup=UiPopup.RectFor(anchor,2,viewport);var second=new UiLayoutPoint(popup.X+popup.Width/2,popup.Y+UiPopup.OptionHeight*1.5f);
        Frame(UiFramePhase.MouseDown,second);Frame(UiFramePhase.MouseUp,second);
        Check(choice==2,"popup writes actual int value, never label or row number");
        var replacement=new Row{Key="a",Text="current-a"};rows=new[]{b,replacement};items.Refresh();
        binding.Invoke(new UiBindingAddress("rows","a","apply").Key);
        Check(replacement.Hits==1&&a.Hits==0&&b.Hits==0,"same-key replacement command resolves current object after reorder");
        Check(new UiBindingAddress("x.y","z","f").Key!=new UiBindingAddress("x","y.z","f").Key,"structured item address distinguishes dotted source/key collision");
        Frame(UiFramePhase.Repaint);Check(execution.Texts.Contains("custom-public")&&CustomVisual.Draws>0,"external no-friend custom participates in actual page draw");
        int paints=execution.Texts.Count;UiThemeDraw.StatusBadge(execution,new UiLayoutRect(0,0,100,30),"independent",new UiTheme(),UiStatusTone.Success);
        var interactions=new UiInteractionSession();var target=new UiInteractionTarget("business","operation");int independent=0;
        UiInteractionRequests.Activation(target,interactions.Activate(target,new UiInputEligibility(true)),()=>independent++);
        Check(execution.Texts.Count>paints&&independent==1,"independent C# visual and headless behavior require neither Host nor fabricated node/rect");
        var staticXml=UiLayoutManifest.Parse("<UiPage Schema='2' Source='author'><Widget Id='static' Kind='input/dropdown'><Choice Value='a' Text='Alpha' Help='a-help'/></Widget></UiPage>");
        Check(staticXml.Roots[0].Choices[0].Value=="a"&&staticXml.Roots[0].Choices[0].Help=="a-help","XML static choices normalize into same label/value/help carrier");
        Console.WriteLine("REFACTOR-AUTHOR assertions="+assertions+" writes="+writes+" choice="+choice+" replacement-hits="+replacement.Hits);
    }
    sealed class Row { public string Key="",Text="";public int Hits; }
    sealed class CustomVisual:IUiWidget
    {
        public static int Draws;
        public string Kind=>"author/visual";
        public void Configure(UiElementSpec spec){}
        public void Validate(IUiBindingReader bindings,string path){}
        public float Measure(UiWidgetContext ctx)=>30;
        public void Draw(UiLayoutRect rect,UiWidgetContext ctx){Draws++;UiThemeDraw.Label(ctx.Drawing,rect,"custom-public",ctx.Theme);}
    }
    sealed class Metrics:ITextMetrics { public float MeasureWidth(string text,UiFont font)=>text.Length*7;public float MeasureText(string text,UiFont font,float width)=>20; }
    sealed class Translation:IUiTranslation {public string Translate(string key)=>key;public int TranslationRevision=>0;}
    sealed class Execution:IUiExecution
    {
        UiControlKey? capture,focus;public string? NextText;public int Edits;public readonly List<string> Texts=new();
        public void PrepareControl(UiControlKey key){}
        public bool HasCapture(UiControlKey key)=>capture==key;
        public bool CaptureAvailable=>capture==null;
        public void Capture(UiControlKey key)=>capture=key;
        public void Release(UiControlKey key){if(capture==key)capture=null;}
        public bool HasFocus(UiControlKey key)=>focus==key;
        public void ReleaseFocus(UiControlKey key){if(focus==key)focus=null;}
        public UiNativeEditResult Edit(UiNativeEdit request){Edits++;if(request.Focus)focus=request.Control;string text=NextText??request.Text;NextText=null;return new UiNativeEditResult(text,focus==request.Control);}
        public void ConsumeEvent(){}
        public void WithState(Action body)=>body();
        public void Scroll(UiLayoutRect viewport,UiLayoutRect content,UiLayoutPoint position,bool showScrollbar,Action<UiLayoutPoint> body)=>body(position);
        public void Clip(UiLayoutRect bounds,Action body)=>body();
        public void Fill(UiLayoutRect rect,UiColour colour){}
        public void Text(UiLayoutRect rect,string text,UiFont font,UiColour colour,UiTextAnchor anchor,bool wrap)=>Texts.Add(text);
        public void Line(UiLayoutPoint from,UiLayoutPoint to,UiColour colour,float width){}
        public void Image(UiLayoutRect rect,UiImage image,UiColour tint){}
        public void Tooltip(UiLayoutRect rect,string text){}
        public void Warning(string text)=>Console.WriteLine("WARNING "+text);
    }
}

