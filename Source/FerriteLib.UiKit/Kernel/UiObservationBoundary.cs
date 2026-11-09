using System;

namespace FerriteLib.UiKit.Kernel;

// Only optional observation/reporting uses this boundary; never wrap business or native work in it.
internal static class UiObservationBoundary
{
    internal static bool IsOrdinary(Exception error) => error is not OutOfMemoryException
        && error is not StackOverflowException && error is not AccessViolationException
        && error is not AppDomainUnloadedException && error is not CannotUnloadAppDomainException
        && error is not System.Runtime.InteropServices.SEHException && error is not System.Threading.ThreadAbortException;

    internal static void ReportOnce(ref bool reported, string boundary, Exception original, Exception? reporting = null)
    {
        if (reported) return;
        // Spend the owner's one fallback report before formatting or invoking listeners: a listener may
        // itself reenter the producer. No callback is removed and no error queue is retained.
        reported = true;
        string message;
        try
        {
            message = "[FerriteLib.UiKit] " + boundary + ": " + original
                + (reporting == null ? "" : "\nThe warning observer also failed: " + reporting);
        }
        catch (Exception error) when (IsOrdinary(error))
        { message = "[FerriteLib.UiKit] " + boundary + ": " + original.GetType().FullName + " (diagnostic formatting failed)"; }
        try { System.Diagnostics.Trace.TraceError(message); }
        catch (Exception error) when (IsOrdinary(error)) { }
    }
}
