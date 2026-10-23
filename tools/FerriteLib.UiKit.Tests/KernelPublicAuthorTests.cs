using UnityEngine;
using Verse;
using System;
using FerriteLib.UiKit.AuthorExamples;
using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.Tests;
internal static class KernelPublicAuthorTests
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("  ok: PUBLIC-AUTHOR " + name); }
    internal static int RunAll()
    {
        try
        {
            UiWidgetRegistry.Clear(); UiWidgetRegistry.InitializeCore(); CustomControl.Register();
            using var page = new CounterPage(new Metrics(), new Translation());
            Event.current = new Event { type = EventType.Repaint }; NativeFixture.DrawFrame(page.Host,new UiLayoutRect(0,0,300,200));
            page.Bindings.Invoke("add"); NativeFixture.DrawFrame(page.Host,new UiLayoutRect(0,0,300,200));
            Check(page.Count.Value == 2 && page.Host.InputState.IsActive && !page.Host.InputState.IsEditing,
                "separately compiled public-only page runs real Host/reactive command without internals grant");
            page.Host.ElementProperties("caption").Metric("RowHeight").Set(40);
            page.Host.UpdateDefinition(CounterPage.Manifest()); NativeFixture.DrawFrame(page.Host,new UiLayoutRect(0,0,300,200));
            Check(page.Host.DefinitionRevision == 1, "public program definition update keeps the same usable Host");
            using var bridge = new SnapshotBridge(page.Bindings, () => new[] { new ExternalRow { Key="one", Value=3 } }, (row, value) => row.Value=value);
            bridge.OnActualChange(); Check(page.Bindings.Get<float>(new UiBindingAddress("rows","one","value").Key) == 3, "separate active/third-party snapshot bridge uses complete item dependency path");
            Check(IndependentVisual.Draw(NativeFixture.Paint,new UiLayoutRect(0,0,200,40),new Metrics()).Width == 80, "standalone visual example measures/draws without Host registration");
            var standard=page.Standard(new Translation()); Check(!standard.InputState.IsActive && standard.PageHost == null, "standard concrete window requires no custom shell or eager session");
            float value=10;int requests=0;var bindings=new UiBindings();bindings.BindValue("value",()=>value,v=>{requests++;value=v;});
            using var custom=new UiHost("author",UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"author\"><Widget Id=\"custom\" Kind=\"author/control\" Bind=\"value\"/></UiPage>"),bindings,new UiTheme(),new Metrics(),new Translation());
            NativeFixture.TextFieldOverride=(_,_)=>"20";Event.current=new Event{type=EventType.MouseDown,button=0,mousePosition=VerseUiExecution.Point(new UiLayoutPoint(20,14))};NativeFixture.DrawFrame(custom,new UiLayoutRect(0,0,200,100));
            NativeFixture.TextFieldOverride=(_,shown)=>shown;Check(custom.InputState.IsEditing && requests==0,"public-only custom editor uses same held deferred draft");
            custom.TryHandleAccept();Event.current=new Event{type=EventType.Repaint};NativeFixture.DrawFrame(custom,new UiLayoutRect(0,0,200,100));
            Check(value==20 && requests==1 && !custom.InputState.IsEditing,"public-only custom editor confirms once without Session/Node or default class access");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine("  FAIL: PUBLIC-AUTHOR "+ex);return 1;}
        finally{Event.current=null;NativeFixture.TextFieldOverride=null;}
    }
    private sealed class Metrics:ITextMetrics { public float MeasureWidth(string text,UiFont font)=>text.Length*8;public float MeasureText(string text,UiFont font,float width)=>16; }
    private sealed class Translation:IUiTranslation { public int TranslationRevision=>0;public string Translate(string key)=>key; }
}
