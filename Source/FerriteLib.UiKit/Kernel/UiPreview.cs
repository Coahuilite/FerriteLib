using System;

namespace FerriteLib.UiKit.Kernel;
public enum UiPreviewKind { Component, Window, Settings }

/// <summary>An author's reusable sample factory. Use the production page, styles, registry and bindings.</summary>
public sealed class UiPreviewRegistration
{
    private readonly Func<UiPreviewInstance> create;
    public UiPreviewRegistration(string id, string name, UiPreviewKind kind, Func<UiPreviewInstance> create)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable author-qualified preview ID is required.", nameof(id));
        Id = id; Name = name; Kind = kind; this.create = create ?? throw new ArgumentNullException(nameof(create));
    }
    public string Id { get; }
    public string Name { get; }
    public UiPreviewKind Kind { get; }
    public UiPreviewInstance Create() => create();
}

/// <summary>Explicit sample lifetime. This object does not open a native window or write real settings.</summary>
public sealed class UiPreviewInstance : IDisposable
{
    private readonly IDisposable? resources;
    private bool disposed;
    public UiPreviewInstance(UiHost host, string sampleSource, string measurementSource, IDisposable? resources = null)
        : this(host, sampleSource, measurementSource, resources, null) { }
    /// <summary>Optional author preparation at the real Host's explicit headless/native arrangement boundary.</summary>
    public UiPreviewInstance(UiHost host, string sampleSource, string measurementSource, IDisposable? resources, Action<UiLayoutPoint>? prepare)
        : this(host, sampleSource, measurementSource, resources, prepare, null) { }
    /// <summary>Optional borrowed author document owner. Dispose it through resources when this sample owns it.</summary>
    public UiPreviewInstance(UiHost host, string sampleSource, string measurementSource, IDisposable? resources,
        Action<UiLayoutPoint>? prepare, UiDocumentService? documents)
        : this(host, sampleSource, measurementSource, resources, prepare, documents, null) { }
    /// <summary>Explicit formal window capability. The shell adopts this same Host and owns its disposal.</summary>
    public UiPreviewInstance(UiHost host, string sampleSource, string measurementSource, IDisposable? resources,
        Action<UiLayoutPoint>? prepare, UiDocumentService? documents, UiWindowShell? windowShell)
    {
        windowShell?.RequireAvailable();
        Host = host ?? throw new ArgumentNullException(nameof(host)); SampleSource = sampleSource;
        MeasurementSource = measurementSource; this.resources = resources;
        Documents = documents;
        if (prepare != null) Host.AttachPreviewPreparation(prepare);
        WindowShell = windowShell;
        if (windowShell != null)
        {
            windowShell.Closed += WindowClosed;
            try
            {
                windowShell.AdoptPreviewHost(host);
                if (windowShell.IsClosed) throw new ObjectDisposedException(nameof(windowShell), "The window closed during page attachment.");
            }
            catch { Dispose(); throw; }
        }
    }
    public UiHost Host { get; }
    public string SampleSource { get; }
    public string MeasurementSource { get; }
    public UiDocumentService? Documents { get; }
    public UiWindowShell? WindowShell { get; }
    private void WindowClosed()
    {
        if (disposed) return;
        disposed = true;
        WindowShell!.Closed -= WindowClosed;
        resources?.Dispose();
    }
    public void Dispose()
    {
        if (disposed) return;
        if (WindowShell != null)
        {
            // Closed runs after Host disposal, including a Dispose re-entered from HostDetached.
            WindowShell.Dispose();
            return;
        }
        disposed = true;
        try { Host.Dispose(); }
        finally { resources?.Dispose(); }
    }
}
