using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Page lifecycle lane (0.6, T1): the window shell's attach/detach door, the reload invariance of a page
/// that has a view model wired into it, and the one end-to-end proof that the door is usable from outside.
/// <list type="number">
/// <item><c>HostAttached</c> fires exactly once per host and before that host has drawn a frame;
/// <c>HostDetached</c> fires while the host is still alive, on close and on a failed pass; a reopened window
/// gets a fresh host and a fresh announcement.</item>
/// <item>A lifecycle handler that throws is the window's failure: the notice is deferred to the next pass,
/// the host is still disposed, and the throw never escapes the guarded pass or the close path.</item>
/// <item>Closing one window releases that window's own subscription and leaves another window's host,
/// session and subscription alone.</item>
/// <item>Reloading a page's layout and style documents does not rebuild the host or its session, does not
/// re-subscribe the view model, runs no command and clears no uncommitted draft.</item>
/// <item>The page-level door is usable end to end: a subscription made in <c>HostAttached</c> is live for the
/// page's first draw, and one made after that first draw still works through <c>UiPageWindow.PageHost</c>.
/// The first half is the new guarantee of this round; the second is the previously shipped path, asserted
/// next to it so the new door cannot be mistaken for the only way in.</item>
/// </list>
/// Everything here is harness evidence over the stub game surface. It shows what the shell does under a
/// driven pass; it does not show how the real window stack orders its own hooks in a running game.
/// </summary>
internal static class KernelPageLifecycleTests
{
    private const string ProbeWidgetXml = "<Widget Id=\"page\" Kind=\"" + ProbeWidget.KindName + "\"/>";

    private static readonly Dictionary<string, int> DrawCounts = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Action?> DrawHooks = new(StringComparer.Ordinal);

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Lifecycle: HostAttached fires once per host and before that host's first draw", VerifyAttachOrder);
        Run("Lifecycle: HostDetached precedes disposal on close, and a reopened window gets a fresh host", VerifyCloseAndReopen);
        Run("Lifecycle: a throwing handler is the window's failure and disposal still happens", VerifyThrowingHandlers);
        Run("Lifecycle: closing one window leaves another window's subscription live", VerifyClosingOneKeepsTheOther);
        Run("Reload: a page with a VM attached survives layout and style reloads untouched", VerifyReloadInvariance);
        Run("Door: HostAttached sees the first draw, and PageHost works after it", VerifyTheDoorIsUsable);
        return failures;
    }

    // --- (5) attach/detach ordering -------------------------------------------------------------

    private static void VerifyAttachOrder()
    {
        string scope = NewScope();
        var shell = new ProbeShell(scope);
        shell.windowRect = new Rect(0f, 0f, 800f, 600f);

        shell.WindowOnGUI();
        Check(shell.AttachCalls == 1, "the first pass announces the host exactly once");
        Check(shell.AttachedAtDrawCount == 0, "HostAttached runs before that host has drawn a frame");
        Check(DrawCountOf(scope) >= 1, "and the same pass did draw the page");
        Check(ReferenceEquals(shell.LastAttached, shell.LiveHost),
            "the announced host is the one the shell keeps and draws with");

        shell.WindowOnGUI();
        shell.WindowOnGUI();
        Check(shell.AttachCalls == 1, "further passes reuse the host and do not re-announce it");
        Check(shell.HostCreations == 1, "because the shell did not build a second one");
        Check(shell.Session != null && ReferenceEquals(shell.Session, shell.LastAttached!.Session),
            "the live session is the announced host's own");
        Check(shell.Notices.Count == 0, "a healthy page draws no notice");
        KernelTripGuard.ExpectNoTrips(shell.Session!, "page lifecycle: attach ordering");
    }

    private static void VerifyCloseAndReopen()
    {
        string scope = NewScope();
        var shell = new ProbeShell(scope);
        shell.windowRect = new Rect(0f, 0f, 800f, 600f);
        shell.WindowOnGUI();

        UiHost firstHost = shell.LiveHost!;
        UiSession firstSession = firstHost.Session;
        Check(shell.AttachCalls == 1, "the host was announced on its first pass");

        shell.PreClose();
        Check(shell.DetachCalls == 1, "closing announces the host once");
        Check(shell.DetachedWithLiveSession,
            "HostDetached runs before the host is disposed: the session it hands over is still active");
        Check(shell.Events.Count == 2 && shell.Events[0] == "attached" && shell.Events[1] == "detached",
            "the order is attach then detach");
        Check(!firstSession.IsActive, "and the host's session is disposed by the close");
        Check(shell.LiveHost == null && shell.Session == null, "the shell no longer holds a page");

        shell.WindowOnGUI();
        Check(shell.HostCreations == 2, "reopening builds a fresh host");
        Check(shell.AttachCalls == 2, "and announces the new one");
        Check(shell.LastAttached != null && !ReferenceEquals(shell.LastAttached, firstHost),
            "the reopened window's host is not the disposed one");
        Check(shell.LastAttached!.Session.IsActive, "and its session is live");
        Check(DrawCountOf(scope) >= 2, "the reopened page drew as well");
        shell.PreClose();
    }

    // --- (5) a handler that throws ---------------------------------------------------------------

    private static void VerifyThrowingHandlers()
    {
        // (a) HostAttached throws: the window fails exactly as it would if the page had thrown.
        string scopeA = NewScope();
        var shellA = new ProbeShell(scopeA) { AttachThrows = true };
        shellA.windowRect = new Rect(0f, 0f, 800f, 600f);

        shellA.WindowOnGUI();
        Check(shellA.Notices.Count == 0, "the pass whose HostAttached threw does not draw the notice inside itself");
        Check(shellA.Failures == 1, "the handler's exception is reported as the window's failure");
        Check(shellA.DetachCalls == 1, "and the host it had just announced is torn down through HostDetached");
        Check(shellA.LiveHost == null && shellA.LastDetached != null && !shellA.LastDetached.Session.IsActive,
            "with that host disposed rather than leaked");

        shellA.WindowOnGUI();
        Check(shellA.Notices.Count == 1 && shellA.Notices[0] == UiWindowNotice.PageUnavailable,
            "the following pass draws the page-unavailable notice");
        Check(shellA.HostCreations == 1, "and the failed window never retries the page");

        // (b) HostDetached throws on close: disposal is not conditional on the handler behaving.
        string scopeB = NewScope();
        var shellB = new ProbeShell(scopeB) { DetachThrows = true };
        shellB.windowRect = new Rect(0f, 0f, 800f, 600f);
        shellB.WindowOnGUI();
        UiHost hostB = shellB.LiveHost!;

        bool escapedClose = ThrowsAny(shellB.PreClose);
        Check(!escapedClose, "a throwing HostDetached does not escape the close path into the game's stack code");
        Check(!hostB.Session.IsActive, "and the host is disposed anyway, so the session is not leaked");
        Check(shellB.LiveHost == null, "the shell drops it too");

        // (c) Both handlers throw on the same failed pass: the pass's own failure still wins and nothing escapes.
        string scopeC = NewScope();
        var shellC = new ProbeShell(scopeC) { AttachThrows = true, DetachThrows = true };
        shellC.windowRect = new Rect(0f, 0f, 800f, 600f);

        bool escapedPass = ThrowsAny(shellC.WindowOnGUI);
        Check(!escapedPass, "a page failure whose teardown handler also throws stays inside the guarded pass");
        Check(shellC.Failures == 1, "the pass's own failure is still reported once");
        Check(shellC.DetachCalls == 1 && shellC.LiveHost == null, "and the host is torn down exactly once");

        shellC.WindowOnGUI();
        Check(shellC.Notices.Count == 1 && shellC.Notices[0] == UiWindowNotice.PageUnavailable,
            "so the window still reaches its failure notice on the next pass");
    }

    // --- (6) one window's close does not touch another's subscription ----------------------------

    private static void VerifyClosingOneKeepsTheOther()
    {
        string scopeA = NewScope();
        string scopeB = NewScope();
        var shellA = new ProbeShell(scopeA);
        var shellB = new ProbeShell(scopeB);
        shellA.windowRect = new Rect(0f, 0f, 800f, 600f);
        shellB.windowRect = new Rect(0f, 0f, 800f, 600f);

        var bindingsA = new UiBindings();
        var bindingsB = new UiBindings();
        var thread = new MainThreadDouble();
        using var adapterA = new UiNotifyAdapter(bindingsA, thread);
        using var adapterB = new UiNotifyAdapter(bindingsB, thread);
        var vmA = new LaneVm();
        var vmB = new LaneVm();
        adapterA.Map("Value", "value.key");
        adapterB.Map("Value", "value.key");

        // The consumer's own subscription lifetime: subscribe in HostAttached, release in HostDetached.
        shellA.HostAttached += _ => adapterA.Attach(vmA);
        shellA.HostDetached += _ => adapterA.Detach(vmA);
        shellB.HostAttached += _ => adapterB.Attach(vmB);
        shellB.HostDetached += _ => adapterB.Detach(vmB);

        shellA.WindowOnGUI();
        shellB.WindowOnGUI();
        UiHost hostB = shellB.LiveHost!;
        Check(adapterA.AttachedSourceCount == 1 && adapterB.AttachedSourceCount == 1,
            "both windows subscribed their own view model");

        shellA.PreClose();
        Check(adapterA.AttachedSourceCount == 0, "closing the first window released its own subscription");
        Check(adapterB.AttachedSourceCount == 1, "and did not touch the second window's");
        Check(ReferenceEquals(shellB.LiveHost, hostB) && hostB.Session.IsActive,
            "the second window's host and session are still alive");

        vmB.Raise("Value");
        Check(bindingsB.GetRevision("value.key") == 1, "the survivor's VM still reaches its page's bindings");
        Check(bindingsA.GetRevision("value.key") == 0, "and the closed window's bindings moved for nothing");

        shellB.PreClose();
        Check(adapterB.AttachedSourceCount == 0, "closing the survivor releases its own subscription too");
    }

    // --- (6) reload invariance ------------------------------------------------------------------

    /// <summary>
    /// The reload half of the T1 claim, driven through the real document service so the layout and the style
    /// document each take the production commit path. The view model is the consumer's object; what is
    /// observable is that the adapter was subscribed exactly once for the page's whole life, that the host and
    /// its session are not replaced, and that nothing a reload could accidentally run - a command, a
    /// notification, the clearing of an uncommitted draft - actually ran.
    /// </summary>
    private static void VerifyReloadInvariance()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ferritelib-mvvm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            VerifyReloadInvarianceIn(directory);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>The body of <see cref="VerifyReloadInvariance"/>, run against a directory the caller owns.</summary>
    private static void VerifyReloadInvarianceIn(string directory)
    {
        string scope = NewScope();
        string layoutPath = Path.Combine(directory, "page.xml");
        string stylePath = Path.Combine(directory, "theme.xml");

        string layoutOne = Page(scope, "<Column Id=\"panel\">" + ProbeWidgetXml + "</Column>");
        string layoutTwo = Page(scope,
            "<Column Id=\"panel\">" + ProbeWidgetXml
            + "<Widget Id=\"added\" Kind=\"chrome/rule\" Height=\"4\"/></Column>");
        const string styleOne = "<Styles Schema=\"1\"></Styles>";
        const string styleTwo = "<Styles Schema=\"1\"><Scheme Name=\"ice\">"
            + "<Color Token=\"Panel\" Value=\"#00ff00\"/></Scheme></Styles>";
        File.WriteAllText(layoutPath, layoutOne);
        File.WriteAllText(stylePath, styleOne);

        var bindings = new UiBindings();
        bindings.BindReadOnly("value.key", () => 0f);
        int commandRuns = 0;
        bindings.BindCommand("act", () => commandRuns++);

        using var service = new UiDocumentService(autoWatch: false);
        service.Add(new UiDocumentSource("l", UiDocumentKind.Layout, layoutPath), layoutOne);
        service.Add(new UiDocumentSource("s", UiDocumentKind.Style, stylePath), styleOne);

        var shell = new ProbeShell(scope, layoutOne, bindings);
        shell.windowRect = new Rect(0f, 0f, 800f, 600f);

        var vm = new LaneVm();
        using var adapter = new UiNotifyAdapter(bindings, new MainThreadDouble());
        adapter.Map("Value", "value.key");
        int attachCalls = 0;
        shell.HostAttached += host =>
        {
            attachCalls++;
            service.Attach(host, "l", "s");
            adapter.Attach(vm);
        };

        shell.WindowOnGUI();
        UiHost pageHost = shell.LiveHost!;
        UiSession session = pageHost.Session;
        Check(attachCalls == 1 && adapter.AttachedSourceCount == 1,
            "the page's view model is subscribed exactly once, in HostAttached");
        Check(service.DependencyCount == 1, "and the page is the document service's one dependent host");

        UiNode? panel = session.GetNodeByElementId("panel");
        Check(panel != null, "the page arranged the element the draft hangs on");
        panel!.State.EditText = "uncommitted-draft";

        int mappedBefore = adapter.MappedPropertyCount;
        File.WriteAllText(layoutPath, layoutTwo);
        File.WriteAllText(stylePath, styleTwo);
        Check(service.Reload("l")?.Accepted == true, "the layout document reload commits");
        Check(service.Reload("s")?.Accepted == true, "the style document reload commits");
        Check(HasElement(pageHost.Manifest, "added"), "the layout reload really did install a new tree");

        Check(ReferenceEquals(pageHost, shell.LiveHost), "a document reload does not rebuild the page host");
        Check(ReferenceEquals(session, pageHost.Session) && session.IsActive, "nor replace its session");
        Check(shell.AttachCalls == 1 && attachCalls == 1, "HostAttached is not fired again by a reload");
        Check(adapter.AttachedSourceCount == 1, "the adapter is not re-subscribed by a reload");
        Check(adapter.MappedPropertyCount == mappedBefore && adapter.FlushCount == 0,
            "the reload announced nothing and ran nothing through the adapter");
        Check(commandRuns == 0, "a reload never invokes a command");
        Check(ReferenceEquals(session.GetNodeByElementId("panel"), panel),
            "the stable identity keeps its node object across both reloads");
        Check(panel.State.EditText == "uncommitted-draft", "and the uncommitted draft survived both reloads");

        vm.Raise("Value");
        Check(bindings.GetRevision("value.key") == 1, "the view model's subscription is still live after the reloads");
        Check(adapter.FlushCount == 1, "and the one announcement it raised was delivered exactly once");

        shell.PreClose();
    }

    // --- (7) the page-level door is usable ------------------------------------------------------

    private static void VerifyTheDoorIsUsable()
    {
        const string Consumer = "door";

        // The new guarantee: a subscription made in HostAttached is live for the page's very first draw.
        string kindOne = "page-" + Guid.NewGuid().ToString("N");
        string scopeOne = Consumer + "/" + kindOne;
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(scopeOne, ProbeWidget.KindName, () => new ProbeWidget(scopeOne));

        var bindingsOne = new UiBindings();
        bindingsOne.BindReadOnly("value.key", () => 0f);
        var vmOne = new LaneVm();
        using var adapterOne = new UiNotifyAdapter(bindingsOne, new MainThreadDouble());
        adapterOne.Map("Value", "value.key");

        UiPageWindow first = NewPageWindow(Consumer, kindOne, scopeOne, bindingsOne);
        int drawsWhenAttached = -1;
        first.HostAttached += _ =>
        {
            adapterOne.Attach(vmOne);
            drawsWhenAttached = DrawCountOf(scopeOne);
        };
        SetDrawHook(scopeOne, () => vmOne.Raise("Value"));

        first.WindowOnGUI();
        Check(drawsWhenAttached == 0, "HostAttached runs before the page's first draw");
        Check(DrawCountOf(scopeOne) >= 1, "and that pass did draw the page");
        Check(adapterOne.AttachedSourceCount == 1, "the subscription it made exists");
        Check(bindingsOne.GetRevision("value.key") == 1,
            "a subscription made in HostAttached is live for the first draw: the notification raised while it drew arrived");
        first.PreClose();

        // The path that already existed: subscribe after the first draw and reach the host through PageHost.
        string kindTwo = "page-" + Guid.NewGuid().ToString("N");
        string scopeTwo = Consumer + "/" + kindTwo;
        UiWidgetRegistry.Register(scopeTwo, ProbeWidget.KindName, () => new ProbeWidget(scopeTwo));

        var bindingsTwo = new UiBindings();
        bindingsTwo.BindReadOnly("value.key", () => 0f);
        var vmTwo = new LaneVm();
        using var adapterTwo = new UiNotifyAdapter(bindingsTwo, new MainThreadDouble());
        adapterTwo.Map("Value", "value.key");

        UiPageWindow second = NewPageWindow(Consumer, kindTwo, scopeTwo, bindingsTwo);
        second.WindowOnGUI();
        Check(second.PageHost != null, "after the first draw the page host is reachable through PageHost");
        Check(second.PageHost!.Session.IsActive, "and its session is live");
        Check(adapterTwo.AttachedSourceCount == 0, "with nothing subscribed through it yet");

        SetDrawHook(scopeTwo, () => vmTwo.Raise("Value"));
        adapterTwo.Attach(vmTwo);
        second.WindowOnGUI();
        Check(bindingsTwo.GetRevision("value.key") == 1,
            "a subscription made after the first draw still delivers on the next draw through PageHost");
        Check(DrawCountOf(scopeTwo) >= 2, "and the page drew that pass");
        second.PreClose();
    }

    // --- helpers --------------------------------------------------------------------------------

    private static UiPageWindow NewPageWindow(string consumer, string kind, string source, IUiBindings bindings)
    {
        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            Page(source, "<Column Id=\"root\">" + ProbeWidgetXml + "</Column>"));
        return new UiPageWindow(
            new UiWindowKey(consumer, kind, ""),
            manifest,
            bindings,
            UiTheme.DarkGold,
            new LaneTranslation(),
            "title",
            "close",
            _ => "unavailable",
            new LaneMetrics());
    }

    private static string DefaultManifest(string scope)
    {
        return Page(scope, "<Column Id=\"root\">" + ProbeWidgetXml + "</Column>");
    }

    private static string Page(string source, string body)
    {
        return "<UiPage Schema=\"2\" Source=\"" + source + "\">" + body + "</UiPage>";
    }

    private static string NewScope() => "mvvm-page-lane-" + Guid.NewGuid().ToString("N");

    private static int DrawCountOf(string scope)
    {
        return DrawCounts.TryGetValue(scope, out int count) ? count : 0;
    }

    private static void SetDrawHook(string scope, Action hook)
    {
        DrawHooks[scope] = hook;
    }

    private static bool HasElement(UiLayoutManifest manifest, string id)
    {
        for (int i = 0; i < manifest.Roots.Count; i++)
        {
            if (HasElement(manifest.Roots[i], id)) return true;
        }

        return false;
    }

    private static bool HasElement(UiElementSpec spec, string id)
    {
        if (string.Equals(spec.Id, id, StringComparison.Ordinal)) return true;
        for (int i = 0; i < spec.Children.Count; i++)
        {
            if (HasElement(spec.Children[i], id)) return true;
        }

        return false;
    }

    private static bool ThrowsAny(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            return true;
        }

        return false;
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
        }
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// The one element the lifecycle pages draw. It records every pass it drew, so "before the first draw" is
    /// a counted frame rather than an assumed one, and it runs a per-scope hook so a lane can raise a
    /// view-model notification from inside the draw - the shape a page really has when its model moved.
    /// </summary>
    private sealed class ProbeWidget : IUiWidget
    {
        internal const string KindName = "probe/page";

        private readonly string scope;

        internal ProbeWidget(string scope)
        {
            this.scope = scope;
        }

        public string Kind => KindName;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
            // This page is about lifecycle, not values, so it declares no binding of its own.
        }

        public float Measure(UiWidgetContext ctx) => 24f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            DrawCounts[scope] = DrawCounts.TryGetValue(scope, out int count) ? count + 1 : 1;
            UiThemeDraw.Label(rect, "page", ctx.Theme);
            if (DrawHooks.TryGetValue(scope, out Action? hook) && hook != null)
            {
                hook();
            }
        }
    }

    /// <summary>
    /// The shell double: a real <see cref="UiWindowHost"/> whose lifecycle hooks are observable and whose
    /// page is the probe widget. It can plant a throw in either handler, which is how "a handler that throws
    /// is treated as that window's failure" is asserted instead of assumed.
    /// </summary>
    private sealed class ProbeShell : UiWindowHost
    {
        private readonly string scope;
        private readonly UiLayoutManifest manifest;

        internal ProbeShell(string scope, string? manifestXml = null, IUiBindings? bindings = null)
        {
            this.scope = scope;
            Bindings = bindings ?? new UiBindings();
            manifest = UiLayoutManifest.Parse(manifestXml ?? DefaultManifest(scope));
            UiWidgetRegistry.InitializeCore();
            UiWidgetRegistry.Register(scope, ProbeWidget.KindName, () => new ProbeWidget(scope));
            HostAttached += OnHostAttached;
            HostDetached += OnHostDetached;
        }

        internal IUiBindings Bindings { get; }

        internal readonly List<string> Events = new();

        internal readonly List<UiWindowNotice> Notices = new();

        internal int HostCreations;

        internal int Failures;

        internal int AttachCalls;

        internal int DetachCalls;

        internal int AttachedAtDrawCount = -1;

        internal bool DetachedWithLiveSession;

        internal UiHost? LastAttached;

        internal UiHost? LastDetached;

        internal bool AttachThrows;

        internal bool DetachThrows;

        internal string NoticeText = "";

        internal UiHost? LiveHost => Host;

        protected override UiTheme Theme => UiTheme.DarkGold;

        protected override string Title => "title";

        protected override string CloseText => "close";

        protected override UiHost CreateHost()
        {
            HostCreations++;
            return new UiHost(scope, manifest, Bindings, Theme, new LaneMetrics(), new LaneTranslation());
        }

        protected override void DrawNotice(Rect rect, UiWindowNotice notice)
        {
            Notices.Add(notice);
            UiThemeDraw.Label(rect, NoticeText, Theme, singleLine: true);
        }

        protected override void OnDrawFailure(Exception error)
        {
            Failures++;
        }

        private void OnHostAttached(UiHost host)
        {
            AttachCalls++;
            LastAttached = host;
            AttachedAtDrawCount = DrawCountOf(scope);
            Events.Add("attached");
            if (AttachThrows)
            {
                throw new InvalidOperationException("planted HostAttached failure");
            }
        }

        private void OnHostDetached(UiHost host)
        {
            DetachCalls++;
            LastDetached = host;
            DetachedWithLiveSession = host.Session.IsActive;
            Events.Add("detached");
            if (DetachThrows)
            {
                throw new InvalidOperationException("planted HostDetached failure");
            }
        }
    }

    /// <summary>The same injected main-thread answer the adapter lane uses: the process never moves a thread.</summary>
    private sealed class MainThreadDouble : IUiMainThread
    {
        public bool IsCurrent { get; set; } = true;
    }

    /// <summary>A plain view model whose subscription traffic is countable.</summary>
    private sealed class LaneVm : INotifyPropertyChanged, IDisposable
    {
        private PropertyChangedEventHandler? subscribers;

        public int Subscriptions;

        public int Unsubscriptions;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                subscribers += value;
                Subscriptions++;
            }

            remove
            {
                subscribers -= value;
                Unsubscriptions++;
            }
        }

        public void Raise(string propertyName)
        {
            subscribers?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
        }
    }

    private sealed class LaneMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
        }

        public float MeasureWidth(string text, UiFont font)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
        }
    }

    private sealed class LaneTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
    }
}
