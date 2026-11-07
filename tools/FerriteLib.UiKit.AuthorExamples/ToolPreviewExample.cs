using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.AuthorExamples;

/// <summary>The normal author page is also the preview; only its explicit sample data is isolated.</summary>
public static class ToolPreviewExample
{
    public static UiPreviewRegistration Register(ITextMetrics metrics, IUiTranslation translation, string measurementSource)
        => new("author.counter", "Counter", UiPreviewKind.Component, () =>
        {
            var page = new CounterPage(metrics, translation);
            return new UiPreviewInstance(page.Host, "author.counter/sample-data", measurementSource, page,
                prepare: size => page.Host.SetLayoutInput("page", new UiLayoutInput(height: size.Y)));
        });
}
