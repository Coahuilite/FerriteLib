using System;
using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.AuthorExamples;

/// <summary>A native editor in an existing OnGUI callback. No Host, XML, registry or reactive runtime.</summary>
public sealed class StandaloneNativeNumber : IDisposable
{
    private readonly VerseUiExecution execution = new();
    private readonly UiSession session = new();
    private readonly UiWidgetContext context;
    private readonly Func<float> read;
    private readonly Action<float> write;
    private readonly UiNumericEdit codec = new(0, 100);

    public StandaloneNativeNumber(ITextMetrics metrics, IUiTranslation translation,
        Func<float> read, Action<float> write)
    {
        this.read = read;
        this.write = write;
        var bindings = new UiBindings();
        bindings.BindValue("value", read, write);
        context = session.CreateContext("author", "number", metrics, new UiTheme(), translation, bindings, 200);
    }

    public UiEditSession<float> Editor => context.Controls.Editor<float>(context, "value", codec.Format, codec.Parse);

    public void OnGUI(UiLayoutRect rect)
    {
        // This is the actual native callback boundary, not a helper called for every control.
        session.SetFrame(execution.CaptureFrame(), execution);
        context.Controls.Edit(rect, context, "value", read(), codec.Format, codec.Parse,
            false, false, write, read);
    }

    public void Dispose()
    {
        session.Dispose();
        execution.Dispose();
    }
}
