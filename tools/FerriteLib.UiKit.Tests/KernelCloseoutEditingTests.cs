using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.Tests;

// Real Host/layout/native event pump. Stub rendering is not target-game input or IME acceptance.
internal static class KernelCloseoutEditingTests
{
    private static readonly Dictionary<UiHost,VerseUiExecution> executions=new();
    private static void SetText(Func<UiLayoutRect,string,string>? read)
    {
        typeof(Verse.Widgets).GetField("TextControlOverride")!.SetValue(null,read==null?null:new Func<Rect,string,string>((r,t)=>read(VerseUiExecution.Rect(r),t)));
    }
    private static readonly UiLayoutRect View = new(0, 0, 400, 160);
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); Console.WriteLine("  ok: CLOSEOUT-EDIT " + name); }
    private static UiHost Host(string kind, UiBindings bindings) { var host = new UiHost("closeout-edit",
        UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"closeout-edit\"><Widget Id=\"field\" Kind=\"" + kind + "\" Bind=\"value\" Min=\"0\" Max=\"100\" Height=\"28\" /></UiPage>"),
        bindings, UiTheme.Vanilla, new Metrics(), new Translation());executions.Add(host,new VerseUiExecution());return host; }
    private static void Pump(UiHost host, EventType phase = EventType.Repaint, float x = -10)
    { Event.current = new Event { type = phase, button = 0, mousePosition = new Vector2(x, 14) };var native=executions[host]; host.DrawFrame(View,native.CaptureFrame(),native); }
    private static UiEditSession<float> Draft(UiHost host)
    {
        var codec = new UiNumericEdit(0, 100);
        return host.Session.Controls.Editor<float>(host.CreateContext(400).WithNode(host.Session.GetNodeByElementId("field")!), "value", codec.Format, codec.Parse);
    }
    private static void Type(UiHost host, string text, float x = 330)
    {
        SetText((_,_)=>text);
        Pump(host, EventType.MouseDown, x);
        SetText((_,shown)=>shown);
        Pump(host, EventType.MouseUp, x);
    }
    internal static int RunAll()
    {
        try
        {
            UiWidgetRegistry.InitializeCore();
            CompositePress(); RangeSamples(); ReturnedRequestReadFailure(); ModelAuthority();
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("  FAIL: CLOSEOUT-EDIT " + ex); return 1; }
        finally { SetText(null); Event.current=null;GUIUtility.hotControl=0;GUI.FocusControl(null);foreach(var execution in executions.Values)execution.Dispose();executions.Clear(); }
    }
    private static void CompositePress()
    {
        float model = 10; var requests = new List<float>(); var b = new UiBindings();
        b.BindValue("value", () => model, value => { requests.Add(value); model = value; });
        using var host = Host("input/stepper-slider", b); Pump(host); Type(host, "20");
        var editor = Draft(host); string node = host.Session.GetNodeByElementId("field")!.Id.Key;
        Check(requests.Count == 0 && editor.Draft == "20" && editor.Focused, "default Host holds draft20 before composite press");
        Pump(host, EventType.MouseDown, 388);
        Check(requests.Count == 0 && editor.Draft == "20", "declared plus capture outranks ordinary blur and does not publish20");
        Pump(host, EventType.MouseUp, 388);
        Check(requests.SequenceEqual(new[] { 21f }) && !editor.HasDraft && !host.InputState.IsEditing,
            "qualified plus release requests only21 and closes this editor's active record");
        Check(ReferenceEquals(editor, Draft(host)) && host.Session.GetNodeByElementId("field")!.Id.Key == node,
            "composite takeover keeps Host and editor instance identity");
        Console.WriteLine("CLOSEOUT-TRACE plus target=" + node + " requests=" + string.Join(",", requests) + " model=" + model + " draft=" + editor.Draft + " host=" + host.Session.Identity);
        Type(host, "-"); Pump(host, EventType.MouseDown, 388); Pump(host, EventType.MouseUp, 388);
        Check(requests.Count == 1 && editor.Draft == "-", "invalid real draft never becomes a zero step base");
        Pump(host, EventType.MouseDown, 150); Pump(host, EventType.MouseUp, 150);
        Check(requests.Count == 2 && !editor.HasDraft, "absolute real range selection replaces an invalid local draft once");
    }
    private static void RangeSamples()
    {
        float model = 50; var requests = new List<float>(); var b = new UiBindings();
        b.BindValue("value", () => model, value => { requests.Add(value); model = value; });
        using var host = Host("input/stepper-slider", b); Pump(host); Type(host, "20");
        // Range x=30..300, travel x=36..294. These are the actual default's arranged parts.
        Pump(host, EventType.MouseDown, 165);
        Check(requests.Count == 0 && Draft(host).Draft == "20", "thumb capture without a selected candidate preserves draft and performs no blur request");
        Pump(host, EventType.MouseDrag, 113.4f); Pump(host, EventType.MouseDrag, 139.2f);
        Pump(host, EventType.MouseUp, 139.2f);
        Check(requests.Count == 2 && Math.Abs(requests[0] - 30) < .001f && Math.Abs(requests[1] - 40) < .001f,
            "each real drag sample requests30 then40, never old20 or release replay");
        Console.WriteLine("CLOSEOUT-TRACE range requests=" + string.Join(",", requests) + " actual=" + model + " same-host=" + host.Session.Identity);
    }
    private static void ReturnedRequestReadFailure()
    {
        foreach (bool takeover in new[] { false, true })
        {
            float model = 10; bool broken = false; int requests = 0;
            var b = new UiBindings(); b.BindValue("value", () => broken ? throw new InvalidOperationException("post-return read") : model,
                value => { requests++; model = value; broken = true; });
            using var host = Host(takeover ? "input/stepper-slider" : "input/number-field", b);
            Pump(host); Type(host, "20", takeover ? 330 : 20); var editor = Draft(host);
            if (takeover) { Pump(host, EventType.MouseDown, 388); Pump(host, EventType.MouseUp, 388); }
            else { Check(host.TryHandleAccept(), "Host accepts held number draft"); Pump(host); }
            Check(requests == 1 && !editor.HasDraft && !editor.Focused,
                "normal request return ends pending edit before subsequent read failure: takeover=" + takeover);
            broken = false; b.NotifyChanged("value"); Pump(host);
            editor.Focus(); Pump(host); host.TryHandleAccept(); Pump(host);
            Check(requests == 1 && editor.Draft == (takeover ? "21" : "20"),
                "display recovery and fresh no-candidate confirmation do not replay completed request: takeover=" + takeover);
            Console.WriteLine("CLOSEOUT-TRACE post-return takeover=" + takeover + " requests=" + requests + " actual=" + model + " draft=" + editor.Draft);
        }
    }
    private static void ModelAuthority()
    {
        foreach (bool normalize in new[] { false, true })
        {
            float model = 10; int requests = 0; var b = new UiBindings();
            b.BindValue("value", () => model, _ => { requests++; if (normalize) model = 15; });
            using var host = Host("input/number-field", b); Pump(host); Type(host, "20", 20);
            Pump(host, EventType.MouseDown, 450); Pump(host);
            Check(requests == 1 && !Draft(host).HasDraft && Draft(host).Draft == (normalize ? "15" : "10"),
                "ordinary blur ends once and shows business refusal/normalization: normalize=" + normalize);
            host.TryHandleAccept(); Pump(host); Check(requests == 1, "no new candidate means no second request");
        }
    }
    private sealed class Metrics : ITextMetrics
    { public float MeasureWidth(string text, UiFont font) => text.Length * 8; public float MeasureText(string text, UiFont font, float width) => 16; }
    private sealed class Translation : IUiTranslation
    { public int TranslationRevision => 0; public string Translate(string key) => key; }
}


