using System;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

// Test callback boundaries only. Drawing/control helpers never call this event pump.
internal static class NativeEvents
{
    internal static Event Begin(EventType phase,UiLayoutPoint point=default)
    {var current=new Event{type=phase,button=0,mousePosition=VerseUiExecution.Point(point)};Event.current=current;return current;}
    internal static void Click(UiLayoutPoint point,Action callback)
    {Begin(EventType.MouseDown,point);callback();Begin(EventType.MouseUp,point);callback();}
}
