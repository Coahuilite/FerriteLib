using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.AuthorExamples;

// This assembly has no InternalsVisibleTo grant. Every required entry must compile as a stranger's code.
public sealed class CounterPage : IDisposable
{
    public UiReactiveRuntime Runtime { get; } = new();
    public UiBindings Bindings { get; }
    public UiReactiveValue<int> Count { get; }
    public UiHost Host { get; }
    private readonly UiReactiveOwner owner;
    public CounterPage(ITextMetrics metrics, IUiTranslation translation)
    {
        if (!FerriteLibVersion.Require(new Version(0, 8, 0), new Version(0, 9, 0), "author.example", out string message))
            throw new InvalidOperationException(message);
        Count = Runtime.Int(1); Bindings = new UiBindings(Runtime); owner = Runtime.CreateOwner("counter");
        var caption = owner.Compute("caption", () => "Count " + Count.Value);
        Bindings.BindTracked("caption", () => caption.Value);
        Bindings.BindCommand("add", () => Count.Value++);
        Host = new UiHost("author", Manifest(), Bindings, Theme(), metrics, translation);
    }
    public static UiLayoutManifest Manifest() => UiLayoutManifest.Create("author", new[] {
        new UiElementSpec("page", "Column", new Dictionary<string,string> { ["Padding"]="0" }, new[] {
            new UiElementSpec("caption", UiKinds.WrappedText, new Dictionary<string,string> { ["Bind"]="caption" }),
            new UiElementSpec("add", UiKinds.Button, new Dictionary<string,string> { ["ActionBind"]="add", ["Text"]="Add" }) }) });
    public static UiTheme Theme()
    { var theme = new UiTheme(); theme.DefaultFont = UiFont.Small; theme.Metric("RowHeight").Set(32); theme.Palette[UiPaletteRole.Accent] = new UiColour(.2f,.5f,.8f); return theme; }
    public UiPageWindow Standard(IUiTranslation translation) => new(new UiWindowKey("author", "settings", "one"),
        Manifest(), Bindings, Theme(), translation, "Settings", "Close", notice => notice.ToString());
    public void Embedded(Rect rect, Window surroundingOwner) => Host.DrawFrame(rect, surroundingOwner);
    public void Dispose() { Host.Dispose(); owner.Dispose(); }
}

public sealed class CustomControl : IUiWidget, IUiPointerLayout
{
    private string binding = "";
    public string Kind => "author/control";
    public void Configure(UiElementSpec spec) { if (!spec.TryGetAttribute("Bind", out binding)) binding = spec.Id; }
    public void Validate(IUiBindings bindings, string path) => bindings.ValidateValue<float>(binding, path);
    public float Measure(UiWidgetContext ctx) => ctx.Theme.Geometry.RowHeight;
    public IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds) { yield return new UiPointerRegion(bounds); }
    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        var codec = new UiNumericEdit(0, 100);
        UiNative.Edit(rect, ctx, binding, ctx.Bindings.Get<float>(binding), codec.Format, codec.Parse,
            false, false, value => ctx.Bindings.Set(binding, value), () => ctx.Bindings.Get<float>(binding));
        if (ctx.IsActive) ctx.State("own-paint-count").FloatValue++;
    }
    public static void Register() => UiWidgetRegistry.Register("author", "author/control", () => new CustomControl(), new[] { "Id", "Kind", "Bind" }, Array.Empty<string>());
}

public static class IndependentVisual
{
    public static UiLayoutRect Draw(Rect rect, ITextMetrics metrics)
    {
        var theme = CounterPage.Theme(); theme.Metric("Gap").Set(7); theme.Metric("Gap").Remove();
        UiThemeDraw.StatusBadge(rect, "Ready", theme, UiStatusTone.Success);
        return UiImageLayout.Arrange(new UiLayoutRect(rect.x, rect.y, rect.width, rect.height), 100, 50);
    }
}

public sealed class ExternalRow { public string Key = ""; public float Value; }
public sealed class SnapshotBridge : IDisposable
{
    public UiItemBindings<ExternalRow> Items { get; }
    public SnapshotBridge(UiBindings bindings, Func<IReadOnlyList<ExternalRow>> source, Action<ExternalRow, float> write)
    {
        Items = new UiItemBindings<ExternalRow>(bindings, "rows", source, row => row.Key, fields =>
            fields.Value("value", row => row.Value, write, (a,b) => a == b));
    }
    // Active service notifications or third-party safe snapshots use the same owning-context entry.
    public void OnActualChange() => Items.Refresh();
    public void Dispose() => Items.Dispose();
}
