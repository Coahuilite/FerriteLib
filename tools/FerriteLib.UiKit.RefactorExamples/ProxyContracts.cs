using System;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.AuthorExamples;

static partial class Program
{
    static void ProxyContracts()
    {
        const string xml="<UiPage Schema='2' Source='author'><Widget Id='caption' Kind='text/wrapped' Bind='caption'/></UiPage>";
        UiLayoutManifest page=UiLayoutManifest.Parse(xml);
        UiHost Host(ForwardingBindings reader)=>new("author",page,reader,new UiTheme(),new Metrics(),new Translation());
        var ordinary=new UiBindings();string ordinaryText="ordinary-a";int ordinaryReads=0;
        ordinary.BindReadOnly("caption",()=>{ordinaryReads++;return ordinaryText;});
        var proxy=new ForwardingBindings(ordinary);var draw=new Execution();
        using(var host=Host(proxy))
        {
            void Frame(){draw.Texts.Clear();host.DrawFrame(new UiLayoutRect(0,0,300,100),new UiFrame(UiFramePhase.Repaint),draw);}
            Frame();int identity=host.Session.Identity;
            Check(host.Presentation==null&&ordinaryReads>0&&draw.Texts.Contains("ordinary-a"),"PROXY ordinary forwarding keeps live getters without enrolling reactive presentation");
            var next=new UiBindings();next.BindReadOnly("caption",()=>"ordinary-b");proxy.Current=next;
            host.UpdateDefinition(page);Frame();
            Check(host.Session.Identity==identity&&host.Presentation==null&&draw.Texts.Contains("ordinary-b"),"PROXY same Host reads a replacement ordinary backing, even when both revisions start at zero");
            proxy.Current=new UiBindings();bool refused=false;
            try{host.UpdateDefinition(page);}catch(UiContractException){refused=true;}
            proxy.Current=next;Frame();
            Check(refused&&host.Session.Identity==identity&&draw.Texts.Contains("ordinary-b"),"PROXY failed definition restores the previous backing and same Host without losing its usable display");
        }
        var runtime=new UiReactiveRuntime();var value=runtime.Int(1);int trackedReads=0;
        var tracked=new UiBindings(runtime);
        tracked.BindTracked("caption",()=>{trackedReads++;int v=value.Value;if(v<0)throw new InvalidOperationException("projection-fault");return "tracked-"+v;});
        proxy.Current=tracked;
        using(var host=Host(proxy))
        {
            void Frame(){draw.Texts.Clear();host.DrawFrame(new UiLayoutRect(0,0,300,100),new UiFrame(UiFramePhase.Repaint),draw);}
            Frame();UiBindingPresentation first=host.Presentation!;
            Check(first!=null&&draw.Texts.Contains("tracked-1"),"PROXY tracked wrapper owns a real per-Host presentation rather than silently becoming an ordinary reader");
            value.Value=2;Frame();
            Check(draw.Texts.Contains("tracked-2")&&host.Presentation!.GetStatus("caption").IsCurrent,"PROXY tracked source invalidation refreshes actual Host display");
            value.Value=-1;Frame();bool businessFailed=false;
            try{_ = proxy.Get<string>("caption");}catch(InvalidOperationException error){businessFailed=error.Message=="projection-fault";}
            Check(draw.Texts.Contains("tracked-2")&&host.Presentation!.GetStatus("caption").Fault!=null&&businessFailed,"PROXY DisplayOnly retains last successful text while business read still reports the current fault");
            value.Value=3;Frame();
            Check(draw.Texts.Contains("tracked-3")&&host.Presentation!.GetStatus("caption").IsCurrent,"PROXY related source recovery resumes the same real presentation without replay");
            int reads=trackedReads;
            proxy.Current=new UiBindings();bool refused=false;
            try{host.UpdateDefinition(page);}catch(UiContractException){refused=true;}
            proxy.Current=tracked;Frame();
            Check(refused&&ReferenceEquals(first,host.Presentation)&&trackedReads==reads&&draw.Texts.Contains("tracked-3"),"PROXY rejected definition does not destroy the previous tracked projection or its subscriptions");
            var nextRuntime=new UiReactiveRuntime();var nextValue=nextRuntime.Int(10);var next=new UiBindings(nextRuntime);
            next.BindTracked("caption",()=>"replacement-"+nextValue.Value);
            proxy.Current=next;host.UpdateDefinition(page);Frame();
            int oldReads=trackedReads;value.Value=4;nextValue.Value=11;Frame();
            Check(!ReferenceEquals(first,host.Presentation)&&trackedReads==oldReads&&draw.Texts.Contains("replacement-11"),"PROXY committed tracked backing switch replaces only the Host projection and releases old source demand");
        }
        bool missing=false;
        try{using var host=Host(new MissingOwner(tracked));}catch(InvalidOperationException error){missing=error.Message.Contains("BindingOwner");}
        Check(missing,"PROXY missing capability is diagnosed at Host creation, not silently dropped tracked refresh");
        using(var standalone=new UiSession())
        {
            bool refused=false;
            try{standalone.CreateContext("author","bad",new Metrics(),new UiTheme(),new Translation(),new MissingOwner(tracked),200);}
            catch(InvalidOperationException error){refused=error.Message.Contains("BindingOwner");}
            Check(refused&&standalone.IsActive,"PROXY no-Host context also diagnoses a missing backing capability without disposing the caller");
        }
        var wrong=new UiBindings();wrong.BindReadOnly("caption",()=>"wrong");
        bool mismatch=false;int before=trackedReads;
        try{using var host=Host(new WrongOwner(tracked,wrong));}catch(UiContractException error){mismatch=error.Message.Contains("BindingOwner");}
        Check(mismatch&&trackedReads==before,"PROXY endpoint/backing mismatch refuses before a tracked getter or model operation");

        var pureRuntime=new UiReactiveRuntime();var pureValue=pureRuntime.Int(1);var pure=new UiBindings(pureRuntime);
        pure.BindTracked("caption",()=>{_ = pureValue.Value;pureValue.Value=99;return "never";});
        using(var host=Host(new ForwardingBindings(pure)))
        {
            host.DrawFrame(new UiLayoutRect(0,0,300,100),new UiFrame(UiFramePhase.Repaint),draw);
            Check(pureValue.Value==1&&host.Presentation!.GetStatus("caption").Fault!=null,"PROXY tracked wrapper retains the real pure-read write guard");
        }
        Check(ordinaryReads>0,"PROXY ordinary reader remained executable independently of tracked capability cases");
        var compoundRuntime=new UiReactiveRuntime();var selected=compoundRuntime.Boolean(false);var after=compoundRuntime.Int(0);
        var compound=new UiBindings(compoundRuntime);compound.BindTracked("selected",()=>selected.Value,v=>selected.Value=v);
        compound.BindCommand("after",()=>after.Value=1);
        using var observer=compoundRuntime.CreateOwner("compound");
        var observations=new System.Collections.Generic.List<int>();
        using var response=observer.Respond("actual",()=>observations.Add((selected.Value?1:0)+after.Value));
        using(var host=new UiHost("author",UiLayoutManifest.Parse("<UiPage Schema='2' Source='author'><Widget Id='check' Kind='input/checkbox' Bind='selected' ActionBind='after'/></UiPage>"),new ForwardingBindings(compound),new UiTheme(),new Metrics(),new Translation()))
        {
            var viewport=new UiLayoutRect(0,0,300,100);
            host.DrawFrame(viewport,new UiFrame(UiFramePhase.Repaint),draw);
            var rect=host.MeasureAndArrange(new UiLayoutPoint(300,100)).RectById["check"];var point=new UiLayoutPoint(rect.X+10,rect.Y+10);
            host.DrawFrame(viewport,new UiFrame(UiFramePhase.MouseDown,point),draw);
            host.DrawFrame(viewport,new UiFrame(UiFramePhase.MouseUp,point),draw);
            Check(selected.Value&&after.Value==1&&observations.Count==2&&observations[1]==2,"PROXY actual checkbox Set-before-Invoke stays one response batch through its reader");
        }
        var choiceRuntime=new UiReactiveRuntime();var label=choiceRuntime.Int(1);var choices=new UiBindings(choiceRuntime);
        choices.BindReadOnly("pick",()=>1);
        choices.BindTrackedChoices<int>("pick",()=>new[]{new UiChoice<int>("tracked-choice-"+label.Value,1,"explicit-help")},(a,b)=>a.Text==b.Text&&a.Value==b.Value&&a.HelpRef==b.HelpRef);
        var choicePage=UiLayoutManifest.Parse("<UiPage Schema='2' Source='author'><Widget Id='choices' Kind='input/mode-row' Bind='pick'/></UiPage>");
        using(var host=new UiHost("author",choicePage,new ForwardingBindings(choices),new UiTheme(),new Metrics(),new Translation()))
        {
            var viewport=new UiLayoutRect(0,0,300,100);host.DrawFrame(viewport,new UiFrame(UiFramePhase.Repaint),draw);
            label.Value=2;draw.Texts.Clear();host.DrawFrame(viewport,new UiFrame(UiFramePhase.Repaint),draw);
            Check(draw.Texts.Contains("tracked-choice-2")&&host.Presentation!.GetStatus("pick",true).IsCurrent&&choices.Get<int>("pick")==1,"PROXY tracked choice endpoint refreshes labels/help without changing the actual typed value");
        }
        var foreignChoices=new UiBindings();foreignChoices.BindChoices<int>("pick",()=>new[]{new UiChoice<int>("foreign",1)});
        bool choiceMismatch=false;
        try{using var host=new UiHost("author",choicePage,new WrongChoiceOwner(choices,foreignChoices),new UiTheme(),new Metrics(),new Translation());}
        catch(UiContractException error){choiceMismatch=error.ElementId=="choices"&&error.Message.Contains("BindingOwner");}
        Check(choiceMismatch,"PROXY implicit same-key choice list cannot hide a different backing behind a matching value endpoint");
    }
    sealed class MissingOwner:ForwardingBindings
    {
        internal MissingOwner(UiBindings current):base(current){}
        public override UiBindings BindingOwner=>null!;
    }
    sealed class WrongOwner:ForwardingBindings
    {
        readonly UiBindings owner;
        internal WrongOwner(UiBindings current,UiBindings owner):base(current){this.owner=owner;}
        public override UiBindings BindingOwner=>owner;
    }
    sealed class WrongChoiceOwner:ForwardingBindings
    {
        readonly UiBindings foreign;
        internal WrongChoiceOwner(UiBindings current,UiBindings foreign):base(current){this.foreign=foreign;}
        public override UiChoiceEndpoint Choices(string key)=>foreign.Choices(key);
    }
}

