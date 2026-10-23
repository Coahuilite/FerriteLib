using System;
using FerriteLib.UiKit.AuthorExamples;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;
internal static class KernelReadOnlyOutputTests
{
    internal static int RunAll()
    {
        try
        {
            UiWidgetRegistry.Clear(); UiWidgetRegistry.InitializeCore(); int assertions = 0;
            ReadOnlyOutputs.Verify(new Metrics(), new Translation(), (value, name) =>
            { if (!value) throw new Exception(name); assertions++; Console.WriteLine("  ok: READONLY " + name); });
            Console.WriteLine("READONLY PASS " + assertions + " external-consumer assertions"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("  FAIL: READONLY " + ex); return 1; }
        finally { Event.current = null; }
    }
    private sealed class Metrics : ITextMetrics
    { public float MeasureWidth(string text, UiFont font) => text.Length * 8; public float MeasureText(string text, UiFont font, float width) => 16; }
    private sealed class Translation : IUiTranslation
    { public int TranslationRevision => 0; public string Translate(string key) => key; }
}
