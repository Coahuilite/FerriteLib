using System;
using System.Collections.Generic;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Window-catalog lane (0.5.x, package P1): keyed window instances, the concrete page shell, the
/// active-target rule and the pause/camera options - all driven through the real
/// <see cref="Verse.WindowStack"/> virtuals, never through a second scheduler.
/// <para>
/// <b>What this lane proves and what it cannot.</b> It proves the library's own rule: one instance per
/// <c>(consumer, kind, context)</c>, reopen-as-activate, a close veto that survives, options that reach
/// the vanilla window fields, exactly one active target, session state kept across a target change and
/// the page's capture released when the target moves. It cannot prove IMGUI behaviour in the game -
/// keyboard routing, real stacking, modal coexistence - so the two claims that depend on it (the
/// consequence of consuming the activating click, and how the vanilla focus call composes with the
/// game's own click path) are labelled in the code and carried by the in-game checklist, not asserted
/// here as if a stub could decide them.
/// </para>
/// <para>
/// The layout page is a container plus the core <c>chrome/rule</c> atom, so the lane needs no per-test
/// widget registration and a trip in
/// <see cref="KernelTripGuard"/> still means "the page path under test did not run".
/// </para>
/// </summary>
internal static class KernelWindowCatalogTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        ResetSeams();
        try
        {
            VerifyKeyIdentityIsLoadBearing();
            VerifySameKindDifferentContextsCoexist();
            VerifySameKeyReopenActivatesTheExistingInstance();
            VerifyAllowMultipleInstancesFalseUsesTheVanillaExactTypeRule();
            VerifyVanillaRuleIsExactTypeNotAssignableFrom();
            VerifyCloseDisposesTheSessionAndDropsTheInstance();
            VerifyRefusedCloseIsPreserved();
            VerifyOptionsReachTheWindowAndUnsetLeavesTheGameValue();
            VerifyActiveTargetIsExactlyOneAndFollowsClicks();
            VerifyOpenOnlyAndManualPoliciesDoNotFollowClicks();
            VerifyDeactivationKeepsSessionAndReleasesCapture();
            VerifyGenericShellNeedsNoWindowSubclass();
            VerifyGenericShellFailureNoticeIsDeferredAndTerminal();
            VerifyChromeScopeTellsTwoInstancesOfOneTypeApart();
            VerifyShellFindingsRouteToTheOwningSubscription();
        }
        finally
        {
            ResetSeams();
        }

        return failures;
    }

    /// <summary>
    /// The identity contract, plus the lane's planted control: the comparison the dedup checks rest on
    /// must be able to tell a near-miss key apart, and a dictionary that ignored the context key would
    /// otherwise make "different contexts coexist" pass for the wrong reason.
    /// </summary>
    private static void VerifyKeyIdentityIsLoadBearing()
    {
        var first = new UiWindowKey("c", "page", "ctx");
        var same = new UiWindowKey("c", "page", "ctx");
        var otherContext = new UiWindowKey("c", "page", "ctx2");
        var otherKind = new UiWindowKey("c", "page2", "ctx");
        var otherConsumer = new UiWindowKey("c2", "page", "ctx");

        Check(first == same, "the same triple is the same key");
        Check(first != otherContext, "a different context key is a different key");
        Check(first != otherKind, "a different window kind is a different key");
        Check(first != otherConsumer, "a different consumer is a different key");
        Check(first.GetHashCode() == same.GetHashCode(), "equal keys share a hash");

        var map = new Dictionary<UiWindowKey, int>();
        map[first] = 1;
        map[same] = 2;
        Check(map.Count == 1 && map[first] == 2, "a dictionary treats the same triple as one key");

        // Planted control: one character of difference in each field must be reported as a different key,
        // or every "different context coexists" assertion in this lane is vacuous.
        Check(!map.ContainsKey(otherContext), "planted: a one-character context difference is not the same key");
        Check(!map.ContainsKey(otherKind), "planted: a kind difference is not the same key");
        Check(!map.ContainsKey(otherConsumer), "planted: a consumer difference is not the same key");
        map[otherContext] = 3;
        Check(map.Count == 2, "and the different context coexists in the same map");

        var noContext = new UiWindowKey("c", "page", "");
        var alsoNoContext = new UiWindowKey("c", "page", null!);
        Check(noContext == alsoNoContext, "an empty or null context key is the kind's one context-free instance");

        CheckThrows("a consumer id is required", () => new UiWindowKey("", "page", "ctx"));
        CheckThrows("a window kind is required", () => new UiWindowKey("c", "", "ctx"));
    }

    /// <summary>
    /// Two contexts of one kind, opened through one generic shell class, must coexist: the vanilla add
    /// path removes siblings by exact C# type, and the library's key - not the type - is the identity.
    /// </summary>
    private static void VerifySameKindDifferentContextsCoexist()
    {
        var harness = new Harness();
        harness.Register("c", "overview");
        var alpha = new UiWindowKey("c", "overview", "alpha");
        var beta = new UiWindowKey("c", "overview", "beta");

        Check(harness.Catalog.Open(alpha), "the first context opens a new instance");
        Check(harness.Catalog.Open(beta), "the second context opens a second instance");
        Check(harness.Catalog.Instances.Count == 2, "both instances are open at once");
        Check(harness.Stack.Count == 2, "and both are in the vanilla stack, so neither closed the other");
        if (!harness.Catalog.TryGet(alpha, out UiWindowHost first)
            || !harness.Catalog.TryGet(beta, out UiWindowHost second))
        {
            Check(false, "each key resolves to its own open instance");
            return;
        }

        Check(!ReferenceEquals(first, second), "each key resolves to its own open instance");
        Check(ReferenceEquals(first.GetType(), second.GetType()),
            "and they share one C# window type, which is the case the exact-type rule would close");

        harness.Place(alpha, new Rect(0f, 0f, 320f, 240f));
        harness.Place(beta, new Rect(400f, 0f, 320f, 240f));
        first.WindowOnGUI();
        second.WindowOnGUI();

        Check(harness.Catalog.IsActive(beta), "the most recently opened instance is the active target");
        ExpectDrew(first, "the first same-type instance draws its page");
        ExpectDrew(second, "the second same-type instance draws its page");
    }

    /// <summary>
    /// Reopening a key activates the instance already open - it never creates a second one, and the
    /// existing instance keeps the session it had, which is where scroll, selection and drafts live.
    /// </summary>
    private static void VerifySameKeyReopenActivatesTheExistingInstance()
    {
        var harness = new Harness();
        harness.Register("c", "page");
        var alpha = new UiWindowKey("c", "page", "alpha");
        var beta = new UiWindowKey("c", "page", "beta");

        harness.Catalog.Open(alpha);
        harness.Place(alpha, new Rect(0f, 0f, 320f, 240f));
        harness.Catalog.TryGet(alpha, out UiWindowHost opened);
        opened.WindowOnGUI();
        UiSession session = RequireSession(opened);
        session.GetOrCreateValueState("draft").EditText = "typed";
        session.GetOrCreateValueState("draft").FloatValue = 7f;

        Check(harness.Catalog.Open(beta), "opening the other context creates an instance");
        Check(harness.Catalog.ActiveKey == beta, "and that instance becomes the active target");

        // The duplicate-open contract is asserted on the state BEFORE the reopen, so it is reachable even
        // when a broken implementation throws from inside Open() instead of returning: the key already
        // resolves to the instance, and the reopen must neither add a third nor route the target away.
        Check(harness.Catalog.TryGet(alpha, out UiWindowHost beforeReopen) && ReferenceEquals(beforeReopen, opened),
            "the key already resolves to the open instance before the reopen");

        bool reopened = TryOpen(
            harness.Catalog, alpha, "reopening an open key activates the existing instance", out bool created);
        Check(reopened && !created, "reopening an open key activates instead of creating");
        Check(harness.Catalog.Instances.Count == 2, "reopening created no third instance");
        Check(harness.Catalog.TryGet(alpha, out UiWindowHost again) && ReferenceEquals(opened, again),
            "the instance that was already open is the one activated");
        Check(harness.Catalog.ActiveKey == alpha, "and it is the active target again");
        Check(ReferenceEquals(opened.Session, session), "its page session is unchanged");
        Check(session.GetOrCreateValueState("draft").EditText == "typed"
            && Math.Abs(session.GetOrCreateValueState("draft").FloatValue - 7f) < 0.001f,
            "and its draft survived the target change");
        Check(harness.Stack.Count == 2, "the vanilla stack still holds exactly the two instances");
    }

    /// <summary>
    /// The option that decides the vanilla exact-type rule must actually reach it: with multiple
    /// instances disallowed, the sibling is evicted by the game's own path and the catalog records the
    /// eviction instead of holding a ghost.
    /// </summary>
    private static void VerifyAllowMultipleInstancesFalseUsesTheVanillaExactTypeRule()
    {
        var harness = new Harness();
        harness.Register("c", "settings", new UiWindowOptions { AllowMultipleInstances = false });
        var first = new UiWindowKey("c", "settings", "a");
        var second = new UiWindowKey("c", "settings", "b");

        harness.Catalog.Open(first);
        Check(harness.Catalog.Open(second), "the second key still opens");
        Check(!harness.Catalog.TryGet(first, out _),
            "the vanilla exact-type rule evicted the sibling, as the option asked");
        Check(harness.Catalog.Instances.Count == 1 && harness.Catalog.TryGet(second, out _),
            "the catalog recorded the eviction through PostClose instead of keeping a ghost instance");
        Check(harness.Stack.Count == 1, "and the stack holds only the survivor");
    }

    /// <summary>
    /// The other half of the vanilla rule: the add path evicts by EXACT C# type, never assignable-from.
    /// <para>
    /// The harness otherwise only ever opens one window class, so without this assertion switching the
    /// double's rule to <c>IsAssignableFrom</c> would redden nothing - the gap the P1 adversarial pass
    /// recorded. Both orders are checked because "assignable" is directional: a derived window added after
    /// its base and a base added after its derived window are two different mutation shapes, and one
    /// assertion cannot see both. Both windows carry <c>onlyOneOfTypeAllowed</c> (the option is false), so
    /// the flag cannot hide the distinction the way it would under the default.
    /// </para>
    /// </summary>
    private static void VerifyVanillaRuleIsExactTypeNotAssignableFrom()
    {
        var baseFirst = new Harness();
        baseFirst.RegisterTypedFamily("c", "typed", new UiWindowOptions { AllowMultipleInstances = false });
        var baseKey = new UiWindowKey("c", "typed", "base");
        var derivedKey = new UiWindowKey("c", "typed", "derived");

        Check(baseFirst.Catalog.Open(baseKey), "the base-class window opens");
        Check(baseFirst.Catalog.Open(derivedKey), "a derived-class window of the same kind opens beside it");
        Check(baseFirst.Catalog.TryGet(baseKey, out _),
            "the base window survives a derived sibling: the vanilla rule is exact type, not assignable-from");
        Check(baseFirst.Catalog.Instances.Count == 2 && baseFirst.Stack.Count == 2,
            "and both classes coexist in the catalog and in the vanilla stack");

        var derivedFirst = new Harness();
        derivedFirst.RegisterTypedFamily("c", "typed", new UiWindowOptions { AllowMultipleInstances = false });
        Check(derivedFirst.Catalog.Open(derivedKey), "the derived-class window opens first here");
        Check(derivedFirst.Catalog.Open(baseKey), "then the base-class window of the same kind");
        Check(derivedFirst.Catalog.TryGet(derivedKey, out _),
            "the derived window survives a base sibling: assignable-from the other way is not the rule either");
        Check(derivedFirst.Catalog.Instances.Count == 2 && derivedFirst.Stack.Count == 2,
            "and both classes coexist in this order too");
    }

    private static void VerifyCloseDisposesTheSessionAndDropsTheInstance()
    {
        var harness = new Harness();
        harness.Register("c", "page");
        var key = new UiWindowKey("c", "page", "");
        harness.Catalog.Open(key);
        harness.Place(key, new Rect(0f, 0f, 320f, 240f));
        harness.Catalog.TryGet(key, out UiWindowHost window);
        window.WindowOnGUI();
        UiSession session = RequireSession(window);
        Check(session.IsActive, "the page session is alive while the window is open");

        Check(harness.Catalog.Close(key), "closing an open key goes through the vanilla stack");
        Check(!harness.Catalog.TryGet(key, out _), "the identity map dropped the instance");
        Check(harness.Catalog.Instances.Count == 0, "and it is no longer enumerated");
        Check(!session.IsActive, "the page session was disposed with the host");
        Check(harness.Stack.Count == 0, "the vanilla stack no longer holds it");
        Check(harness.Catalog.ActiveKey == null, "the active target is cleared when the last window closes");
        Check(!harness.Catalog.Close(key), "closing a key that is not open reports false");
    }

    /// <summary>The vanilla close hook can be refused, and a refusal leaves the instance fully alive.</summary>
    private static void VerifyRefusedCloseIsPreserved()
    {
        bool allow = false;
        var harness = new Harness();
        harness.Register("c", "page", new UiWindowOptions { CanClose = _ => allow });
        var key = new UiWindowKey("c", "page", "");
        harness.Catalog.Open(key);
        harness.Place(key, new Rect(0f, 0f, 320f, 240f));
        harness.Catalog.TryGet(key, out UiWindowHost window);
        window.WindowOnGUI();
        UiSession session = RequireSession(window);

        Check(!harness.Catalog.Close(key), "a refused close reports false");
        Check(harness.Catalog.TryGet(key, out UiWindowHost stillOpen) && ReferenceEquals(stillOpen, window),
            "and the instance is still open");
        Check(harness.Stack.IsOpen(window), "and still in the vanilla stack");
        Check(session.IsActive, "with its page session alive");
        Check(!window.OnCloseRequest(), "the vanilla close hook answers the veto as well");

        allow = true;
        Check(window.OnCloseRequest(), "and answers true once the consumer allows it");
        Check(harness.Catalog.Close(key), "then the close goes through");
        Check(!harness.Catalog.TryGet(key, out _), "and the instance is gone");
    }

    /// <summary>
    /// Options must reach the window fields, and a null option must write nothing: pause and camera are
    /// any-true over every window, so a library default there would change another consumer's game.
    /// </summary>
    private static void VerifyOptionsReachTheWindowAndUnsetLeavesTheGameValue()
    {
        var explicitOptions = new UiWindowOptions
        {
            ForcePause = true,
            PreventCameraMotion = false,
            AbsorbInputAroundWindow = true,
            Draggable = false,
            Resizeable = false,
            CloseOnAccept = false,
            CloseOnCancel = false,
            CloseOnClickedOutside = true,
        };

        var harness = new Harness();
        harness.Register("c", "explicit", explicitOptions);
        harness.Register("c", "plain");
        var explicitKey = new UiWindowKey("c", "explicit", "");
        var plainKey = new UiWindowKey("c", "plain", "");
        harness.Catalog.Open(explicitKey);
        harness.Catalog.Open(plainKey);

        harness.Catalog.TryGet(explicitKey, out UiWindowHost explicitHost);
        harness.Catalog.TryGet(plainKey, out UiWindowHost plainHost);

        Check(explicitHost.forcePause
            && !explicitHost.preventCameraMotion
            && explicitHost.absorbInputAroundWindow
            && !explicitHost.draggable
            && !explicitHost.resizeable
            && !explicitHost.closeOnAccept
            && !explicitHost.closeOnCancel
            && explicitHost.closeOnClickedOutside,
            "every explicit option reaches the vanilla window field");

        // The baseline is a freshly constructed window, so this asserts what the library did NOT write
        // rather than a number the library chose.
        var vanilla = new BaselineWindow();
        Check(plainHost.forcePause == vanilla.forcePause
            && plainHost.preventCameraMotion == vanilla.preventCameraMotion
            && plainHost.absorbInputAroundWindow == vanilla.absorbInputAroundWindow
            && plainHost.draggable == vanilla.draggable
            && plainHost.resizeable == vanilla.resizeable
            && plainHost.closeOnAccept == vanilla.closeOnAccept
            && plainHost.closeOnCancel == vanilla.closeOnCancel
            && plainHost.closeOnClickedOutside == vanilla.closeOnClickedOutside,
            "an unset option leaves the game's own value untouched, so the library chose no product default");
        Check(!harness.Catalog.AnyForcesPause == !plainHost.forcePause || explicitHost.forcePause,
            "the catalog's pause view is the any-true rule over its own instances");
        Check(plainHost.onlyOneOfTypeAllowed == false,
            "the default allows same-type instances, so the library key, not the C# type, is the identity");

        var plainHarness = new Harness();
        plainHarness.Register("c", "plain");
        plainHarness.Catalog.Open(plainKey);
        Check(!plainHarness.Catalog.AnyForcesPause,
            "a registration that says nothing about pause does not force pause");
        Check(new UiPageWindow(
                new UiWindowKey("c", "standalone", ""),
                Page(),
                new UiBindings(),
                UiTheme.DarkGold,
                new LaneTranslation(),
                "standalone",
                "close",
                _ => "unavailable",
                new LaneMetrics()).IsActiveTarget,
            "a shell outside a catalog keeps its unchanged active-target behaviour");
    }

    /// <summary>
    /// Exactly one active target, and a pointer-down inside a window moves it there. The activating click
    /// is marked used before the page draws - the harness consequence of that mark is in-game.
    /// </summary>
    private static void VerifyActiveTargetIsExactlyOneAndFollowsClicks()
    {
        var harness = new Harness();
        harness.Register("c", "page");
        var alpha = new UiWindowKey("c", "page", "alpha");
        var beta = new UiWindowKey("c", "page", "beta");

        harness.Catalog.Open(alpha);
        harness.Place(alpha, new Rect(0f, 0f, 300f, 200f));
        Check(harness.Catalog.ActiveKey == alpha, "opening a window makes it the target");
        harness.Catalog.Open(beta);
        harness.Place(beta, new Rect(400f, 0f, 300f, 200f));
        Check(harness.Catalog.ActiveKey == beta, "and the next opened window takes the target");

        harness.Catalog.TryGet(alpha, out UiWindowHost first);
        harness.Catalog.TryGet(beta, out UiWindowHost second);
        Check(!first.IsActiveTarget && second.IsActiveTarget, "exactly one window holds the target");

        Event inFirst = PumpMouseDown(first, new Vector2(50f, 50f));
        Check(harness.Catalog.ActiveKey == alpha, "a click inside a window makes it the target");
        Check(EventWasUsed(inFirst),
            "and the activating click is marked used before the page draws, so it cannot also operate a control");
        Check(first.IsActiveTarget && !second.IsActiveTarget, "the target moved and is still exactly one");

        // The activation combination, stated so it cannot drift: the game's own focus setter is called,
        // and nothing here reorders the stack. That is the whole difference between "set focus" and
        // "bring to front" - whether the game's routing agrees is the in-game checklist's job.
        Check(ReferenceEquals(FocusedWindow(harness.Stack), first),
            "activation uses the game's own focus setter rather than a private notion of focus");
        Check(harness.Stack.Windows.Count == 2 && ReferenceEquals(harness.Stack.Windows[0], first),
            "and it does not reorder the vanilla stack, so it is not a bring-to-front");

        Event inSecond = PumpMouseDown(second, new Vector2(450f, 50f));
        Check(harness.Catalog.ActiveKey == beta, "a click inside the other window moves the target back");
        Check(EventWasUsed(inSecond), "and that activating click is consumed too");

        Event outside = PumpMouseDown(first, new Vector2(1000f, 1000f));
        Check(harness.Catalog.ActiveKey == null, "a click outside every instance clears the target");
        Check(!EventWasUsed(outside), "and is left to the game, because no window was selected by it");
    }

    /// <summary>The other two policies pin the rule from both sides, so "follows clicks" is a choice and not an accident.</summary>
    private static void VerifyOpenOnlyAndManualPoliciesDoNotFollowClicks()
    {
        var openOnly = new Harness(UiFocusPolicy.OpenOnly);
        openOnly.Register("c", "page");
        var alpha = new UiWindowKey("c", "page", "alpha");
        var beta = new UiWindowKey("c", "page", "beta");
        openOnly.Catalog.Open(alpha);
        openOnly.Place(alpha, new Rect(0f, 0f, 300f, 200f));
        openOnly.Catalog.Open(beta);
        openOnly.Place(beta, new Rect(400f, 0f, 300f, 200f));
        openOnly.Catalog.TryGet(alpha, out UiWindowHost first);

        Event click = PumpMouseDown(first, new Vector2(50f, 50f));
        Check(openOnly.Catalog.ActiveKey == beta, "OpenOnly ignores a click");
        Check(!EventWasUsed(click), "and does not consume the click it ignored");
        openOnly.Catalog.Activate(alpha);
        Check(openOnly.Catalog.ActiveKey == alpha, "but an explicit activate still moves the target");

        var manual = new Harness(UiFocusPolicy.Manual);
        manual.Register("c", "page");
        manual.Catalog.Open(alpha);
        Check(manual.Catalog.ActiveKey == null, "Manual leaves opening alone: only an explicit activate moves it");
        manual.Catalog.Activate(alpha);
        Check(manual.Catalog.ActiveKey == alpha, "and the explicit activate works");
    }

    /// <summary>
    /// Deactivation keeps everything the window owns and takes back the pointer capture it held, so a
    /// control in a window the user left cannot keep being dragged.
    /// </summary>
    private static void VerifyDeactivationKeepsSessionAndReleasesCapture()
    {
        var harness = new Harness();
        harness.Register("c", "page");
        var alpha = new UiWindowKey("c", "page", "alpha");
        var beta = new UiWindowKey("c", "page", "beta");
        harness.Catalog.Open(alpha);
        harness.Place(alpha, new Rect(0f, 0f, 320f, 240f));
        harness.Catalog.TryGet(alpha, out UiWindowHost first);
        first.WindowOnGUI();
        UiSession session = RequireSession(first);
        session.GetOrCreateValueState("draft").EditText = "typed";
        session.CaptureHotControl(4242);
        Check(session.OwnedHotControl == 4242, "the page session owns the pointer capture");
        Check(UiNative.HasHotControl(4242), "and the backend hot control is that capture");

        harness.Catalog.Open(beta);
        harness.Place(beta, new Rect(400f, 0f, 320f, 240f));
        harness.Catalog.TryGet(beta, out UiWindowHost second);
        second.WindowOnGUI();

        Check(!first.IsActiveTarget && second.IsActiveTarget, "the target moved to the new window");
        Check(ReferenceEquals(first.Session, session), "the deactivated window keeps its page session");
        Check(session.IsActive, "which is still alive, not disposed");
        Check(session.GetOrCreateValueState("draft").EditText == "typed", "and keeps its draft");
        Check(!session.OwnedHotControl.HasValue, "the lost target released the capture it owned");
        Check(!UiNative.HasHotControl(4242), "so the backend no longer routes pointer events to that control");

        harness.Catalog.Activate(alpha);
        Check(first.IsActiveTarget, "returning to the window restores the target");
        Check(ReferenceEquals(first.Session, session), "with the same session and state");
        ExpectDrew(first, "and the reinstated window still draws its page");
    }

    /// <summary>
    /// The point of the round: an ordinary XML page opens with no bespoke C# window subclass at all.
    /// </summary>
    private static void VerifyGenericShellNeedsNoWindowSubclass()
    {
        var harness = new Harness();
        harness.Register("c", "ordinary");
        var key = new UiWindowKey("c", "ordinary", "one");

        Check(harness.Catalog.Open(key), "an ordinary page opens through the catalog");
        Check(harness.Catalog.TryGet(key, out UiWindowHost window), "and is addressable by its key");
        Check(window is UiPageWindow, "the opened window is the library's concrete page shell");
        Check(ReferenceEquals(window.GetType(), typeof(UiPageWindow)),
            "no bespoke Window subclass is involved in the page");
        Check(window.Key == key, "and it carries the key it was opened under");

        harness.Place(key, new Rect(0f, 0f, 420f, 320f));
        window.WindowOnGUI();
        window.WindowOnGUI();
        ExpectDrew(window, "the generic shell draws its page across passes");
        Check(window.Session!.Frame >= 2, "and the same page session spans both passes");
    }

    /// <summary>
    /// The deferred notice contract, on the shell that now serves ordinary pages: the failing pass draws
    /// nothing, the next pass draws the notice, and the notice is terminal for the instance.
    /// </summary>
    private static void VerifyGenericShellFailureNoticeIsDeferredAndTerminal()
    {
        var harness = new Harness();
        harness.Catalog.Register(
            "c",
            "broken",
            null,
            key => new UiPageWindow(
                key,
                UiLayoutManifest.Parse(
                    "<UiPage Schema='2' Source='window-catalog-broken'><Widget Id='x' Kind='lane/not-registered' /></UiPage>"),
                new UiBindings(),
                UiTheme.DarkGold,
                new LaneTranslation(),
                "broken",
                "close",
                notice =>
                {
                    harness.Notices.Add(notice);
                    return "unavailable";
                },
                new LaneMetrics()));

        var key = new UiWindowKey("c", "broken", "");
        Check(harness.Catalog.Open(key), "the window opens even though its page cannot be entered");
        harness.Place(key, new Rect(0f, 0f, 420f, 320f));
        harness.Catalog.TryGet(key, out UiWindowHost window);

        window.WindowOnGUI();
        Check(harness.Notices.Count == 0, "the failing pass draws no notice - the switch is deferred");
        Check(window.Session == null, "and the failed page host was disposed");
        Check(harness.Catalog.TryGet(key, out _), "the window itself stays open, carrying the notice");

        window.WindowOnGUI();
        Check(harness.Notices.Count == 1 && harness.Notices[0] == UiWindowNotice.PageUnavailable,
            "the next pass draws the page-unavailable notice");

        window.WindowOnGUI();
        window.WindowOnGUI();
        Check(harness.Notices.Count == 3,
            "the notice is terminal: one per remaining pass and no retry of the failed page");
    }

    /// <summary>
    /// One generic shell class serves every page, so the chrome identity must carry the window key or two
    /// open panels share one audit path and a finding about one reads as a finding about the other.
    /// </summary>
    private static void VerifyChromeScopeTellsTwoInstancesOfOneTypeApart()
    {
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(new LaneMetrics(), reports.Add);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;
        try
        {
            var harness = new Harness();
            harness.Register("c", "panel", null, new string('t', 80));
            var alpha = new UiWindowKey("c", "panel", "one");
            var beta = new UiWindowKey("c", "panel", "two");
            harness.Catalog.Open(alpha);
            harness.Catalog.Open(beta);
            harness.Place(alpha, new Rect(0f, 0f, 320f, 240f));
            harness.Place(beta, new Rect(400f, 0f, 320f, 240f));

            harness.Catalog.TryGet(alpha, out UiWindowHost first);
            harness.Catalog.TryGet(beta, out UiWindowHost second);
            first.WindowOnGUI();
            second.WindowOnGUI();

            var chromePaths = new List<string>();
            foreach (UiOverflowReport report in reports)
            {
                if (report.ElementPath.EndsWith("/chrome", StringComparison.Ordinal))
                {
                    chromePaths.Add(report.ElementPath);
                }
            }

            Check(chromePaths.Count >= 2, "both instances' chrome bands report a finding");
            bool distinct = true;
            for (int i = 1; i < chromePaths.Count; i++)
            {
                if (string.Equals(chromePaths[i], chromePaths[0], StringComparison.Ordinal))
                {
                    distinct = false;
                }
            }

            Check(distinct, "and two instances of one generic shell class have different audit identities");
            Check(chromePaths.Exists(path => path.IndexOf("[c/panel/one]", StringComparison.Ordinal) >= 0),
                "the identity carries the first window's key");
            Check(chromePaths.Exists(path => path.IndexOf("[c/panel/two]", StringComparison.Ordinal) >= 0),
                "and the second window's key");
            Check(!reports.Exists(report => string.Equals(report.ElementPath, "(unscoped)", StringComparison.Ordinal)),
                "nothing the shell drew is left unattributed");
        }
        finally
        {
            UiFitAudit.Enabled = false;
            UiFitAudit.Detach();
        }
    }

    /// <summary>
    /// The routing half of the multi-window diagnostic contract: the shell draws its chrome and its notice
    /// outside <see cref="UiHost.DrawFrame"/>, so those findings need the owning host's diagnostic scope or
    /// they fall back to the process-wide channel and two open windows become indistinguishable.
    /// <para>
    /// Two subscribed shells are drawn, and each finding must land in its own subscription with the
    /// sibling's buffer untouched; the notice is checked the same way through a prerequisite that goes
    /// unmet while the host is still alive. The last phase draws an unsubscribed shell to pin the other
    /// side: no subscription is created for it, and its finding still reaches the legacy channel.
    /// </para>
    /// </summary>
    private static void VerifyShellFindingsRouteToTheOwningSubscription()
    {
        var legacy = new List<UiOverflowReport>();
        var harness = new Harness();
        SubscribedShell? detached = null;
        int subscriptionsBefore = UiDiagnosticHub.SubscriptionCount;

        UiFitAudit.Attach(new LaneMetrics(), legacy.Add);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;
        try
        {
            harness.Catalog.Register(
                "c",
                "diag",
                null,
                key => new SubscribedShell(
                    "diag-lane/" + key.ContextKey,
                    string.Equals(key.ContextKey, "alpha", StringComparison.Ordinal)
                        ? new string('a', 60)
                        : new string('b', 60),
                    subscribe: true));

            var alpha = new UiWindowKey("c", "diag", "alpha");
            var beta = new UiWindowKey("c", "diag", "beta");
            harness.Catalog.Open(alpha);
            harness.Catalog.Open(beta);
            harness.Place(alpha, new Rect(0f, 0f, 220f, 200f));
            harness.Place(beta, new Rect(300f, 0f, 220f, 200f));

            if (!harness.Catalog.TryGet(alpha, out UiWindowHost alphaWindow)
                || !harness.Catalog.TryGet(beta, out UiWindowHost betaWindow))
            {
                Check(false, "both diag windows are open");
                return;
            }

            // Pass 1 creates each host, and its subscription with it; chrome on that pass is drawn before
            // the host exists, which is the documented boundary ("where a host exists"). Pass 2 is the
            // steady state the game runs in, and that is the pass that must route.
            alphaWindow.WindowOnGUI();
            betaWindow.WindowOnGUI();
            alphaWindow.WindowOnGUI();
            betaWindow.WindowOnGUI();

            UiDiagnosticSubscription? alphaSub = (alphaWindow as SubscribedShell)?.Subscription;
            UiDiagnosticSubscription? betaSub = (betaWindow as SubscribedShell)?.Subscription;
            if (alphaSub == null || betaSub == null)
            {
                Check(false, "both shells subscribed the host they created");
                return;
            }

            // Guarded reads, not a crash: when the routing is broken there is no event at all, and a
            // default event's path is null. The lane must name that shape instead of throwing on it.
            if (!alphaSub.TryGetLatest(UiDiagnosticKind.Fit, out UiDiagnosticEvent alphaEvent)
                || !betaSub.TryGetLatest(UiDiagnosticKind.Fit, out UiDiagnosticEvent betaEvent))
            {
                Check(false, "both subscriptions received their own chrome finding instead of the legacy channel");
                return;
            }

            Check(string.Equals(alphaEvent.Code, "fit.overflow", StringComparison.Ordinal),
                "as a fit.overflow event on the fit channel");
            Check(alphaEvent.ElementPath.IndexOf("[c/diag/alpha]", StringComparison.Ordinal) >= 0,
                "attributed to the window that produced it");
            Check(string.Equals(alphaEvent.Host, "diag-lane/alpha", StringComparison.Ordinal),
                "and to that host's own identity");
            Check(betaEvent.ElementPath.IndexOf("[c/diag/beta]", StringComparison.Ordinal) >= 0,
                "attributed to the sibling, not to the first window");
            Check(!string.Equals(alphaEvent.ElementPath, betaEvent.ElementPath, StringComparison.Ordinal),
                "the two findings are different findings, not one shared record");

            // One assertion rather than two: an empty buffer must not let the isolation half pass for the
            // wrong reason.
            bool isolated = alphaSub.CountOf(UiDiagnosticKind.Fit) == 1
                && betaSub.CountOf(UiDiagnosticKind.Fit) == 1
                && !HasEventFor(alphaSub, "[c/diag/beta]")
                && !HasEventFor(betaSub, "[c/diag/alpha]");
            Check(isolated, "each finding landed exactly once in its own buffer, with no cross-window copy");

            // The notice half: the same shell, its host still alive, its prerequisite goes unmet.
            ((SubscribedShell)alphaWindow).Prerequisite = false;
            alphaWindow.WindowOnGUI();
            Check(alphaSub.TryGetLatest(UiDiagnosticKind.Fit, out UiDiagnosticEvent noticeEvent)
                && noticeEvent.ElementPath != null
                && noticeEvent.ElementPath.EndsWith("/notice", StringComparison.Ordinal),
                "the notice routes to the owning subscription and names its own band");
            Check(string.Equals(noticeEvent.Host, "diag-lane/alpha", StringComparison.Ordinal),
                "with the owning host's identity");
            Check(betaSub.CountOf(UiDiagnosticKind.Fit) == 1,
                "and the sibling's buffer is untouched by it");

            // The other side of the contract: a host nobody subscribed stays cheap and quiet.
            legacy.Clear();
            int subscriptionsAfterWindows = UiDiagnosticHub.SubscriptionCount;
            detached = new SubscribedShell("diag-lane/detached", new string('d', 60), subscribe: false);
            detached.windowRect = new Rect(0f, 0f, 220f, 200f);
            detached.WindowOnGUI();
            detached.WindowOnGUI();
            Check(UiDiagnosticHub.SubscriptionCount == subscriptionsAfterWindows,
                "an unsubscribed shell creates no subscription");
            Check(legacy.Count > 0, "and its finding still reaches the legacy channel");
        }
        finally
        {
            harness.Catalog.CloseAll();
            detached?.PreClose();
            UiFitAudit.Enabled = false;
            UiFitAudit.Reset();
            UiFitAudit.Detach();
        }

        Check(UiDiagnosticHub.SubscriptionCount == subscriptionsBefore,
            "closing the windows released their subscriptions");
    }

    /// <summary>True when any retained event of a subscription names one element-path fragment.</summary>
    private static bool HasEventFor(UiDiagnosticSubscription subscription, string fragment)
    {
        foreach (UiDiagnosticEvent retained in subscription.Snapshot())
        {
            if (retained.ElementPath.IndexOf(fragment, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The lane's own page: a container plus one core atom, no per-test kind registration.</summary>
    private static UiLayoutManifest Page()
    {
        return UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"window-catalog-lane\">"
            + "<Column Id=\"root\" Gap=\"4\">"
            + "<Widget Id=\"rule\" Kind=\"chrome/rule\" Height=\"4\" />"
            + "</Column>"
            + "</UiPage>");
    }

    private static UiSession RequireSession(UiWindowHost window)
    {
        UiSession? session = window.Session;
        if (session == null)
        {
            throw new Exception("the pass did not build a page session for " + window.Key);
        }

        return session;
    }

    private static void ExpectDrew(UiWindowHost window, string lane)
    {
        UiSession session = RequireSession(window);
        KernelTripGuard.ExpectNoTrips(session, lane);
    }

    /// <summary>
    /// One pointer-down pass over one window, through the member the game calls.
    /// <para>
    /// Compiled against the real UnityEngine surface, which has no public <c>Event</c> constructor, so the
    /// instance comes from the static factory and the fields a lane needs are overwritten - the same idiom
    /// the popup lane uses. A fresh instance per pass is also what resets the stub's control-id counter.
    /// </para>
    /// </summary>
    private static Event PumpMouseDown(UiWindowHost window, Vector2 point)
    {
        Event raised = Event.KeyboardEvent("space");
        raised.type = EventType.MouseDown;
        raised.button = 0;
        raised.mousePosition = point;
        Event.current = raised;
        try
        {
            window.WindowOnGUI();
        }
        finally
        {
            Event.current = null;
        }

        return raised;
    }

    /// <summary>
    /// Whether the shell marked the event used before the page drew.
    /// <para>
    /// <b>Read by reflection on purpose.</b> The game's reference assembly does not advertise
    /// <c>Event.used</c> (only <c>Use()</c>), so the lane cannot compile against it; the harness double
    /// declares it, exactly like <c>Widgets.LabelTexts</c>. A missing property throws rather than
    /// reporting "not used", so the assertion cannot pass vacuously if the double changes.
    /// </para>
    /// </summary>
    private static bool EventWasUsed(Event raised)
    {
        PropertyInfo? property = typeof(Event).GetProperty("used");
        if (property == null)
        {
            throw new Exception(
                "the harness Event double no longer exposes 'used', so the activating-click consumption "
                + "assertion would pass vacuously; restore the property or retire the assertion.");
        }

        object? value = property.GetValue(raised);
        return value is bool used && used;
    }

    /// <summary>
    /// The stack double's own focus slot, which mirrors the real type's private <c>focusedWindow</c>.
    /// Read by reflection for the same reason as <see cref="EventWasUsed"/>: the game reference assembly
    /// does not advertise it, and a missing member throws instead of quietly reporting "not focused".
    /// </summary>
    private static object? FocusedWindow(WindowStack stack)
    {
        FieldInfo? field = typeof(WindowStack).GetField("focusedWindow");
        if (field == null)
        {
            throw new Exception(
                "the harness WindowStack double no longer exposes 'focusedWindow', so the "
                + "'activation uses the vanilla focus setter' assertion would pass vacuously.");
        }

        return field.GetValue(stack);
    }

    /// <summary>
    /// Reopens a key and turns a thrown break into a NAMED failure.
    /// <para>
    /// <b>Why this exists (P1 adversarial pass).</b> Disabling the keyed dedup in
    /// <c>UiWindowCatalog.Open</c> makes the identity map's own duplicate-key guard throw before any
    /// assertion in this lane can report, so that regression used to produce an
    /// <c>UNHANDLED: ArgumentException</c> and a dead run instead of naming the contract it broke. A lane
    /// whose job is to name a broken contract has to name it whether the break returns or throws, so the
    /// call is wrapped here: the throw becomes a named FAIL and the caller's own assertion still runs.
    /// </para>
    /// </summary>
    private static bool TryOpen(UiWindowCatalog catalog, UiWindowKey key, string lane, out bool created)
    {
        created = false;
        try
        {
            created = catalog.Open(key);
            return true;
        }
        catch (Exception ex)
        {
            Check(false, lane + ": Open threw " + ex.GetType().Name + " :: " + ex.Message);
            return false;
        }
    }

    private static void CheckThrows(string what, Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            Check(true, what);
            return;
        }

        Check(false, "planted: " + what);
    }

    private static void ResetSeams()
    {
        Event.current = null;
        GUIUtility.hotControl = 0;
        UiNative.DebugMousePositionEnabled = false;
        UiNative.ButtonOverride = null;
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

    /// <summary>One catalog plus the stack double it drives, with the shell the tests register.</summary>
    private sealed class Harness
    {
        internal readonly WindowStack Stack = new WindowStack();
        internal readonly UiWindowCatalog Catalog;
        internal readonly List<UiWindowNotice> Notices = new List<UiWindowNotice>();

        internal Harness(UiFocusPolicy policy = UiFocusPolicy.FollowClicks)
        {
            Catalog = new UiWindowCatalog(Stack, policy);
        }

        internal void Register(string consumer, string kind, UiWindowOptions? options = null, string title = "title")
        {
            Catalog.Register(
                consumer,
                kind,
                options,
                key => new UiPageWindow(
                    key,
                    Page(),
                    new UiBindings(),
                    UiTheme.DarkGold,
                    new LaneTranslation(),
                    title,
                    "close",
                    notice =>
                    {
                        Notices.Add(notice);
                        return "unavailable";
                    },
                    new LaneMetrics()));
        }

        /// <summary>
        /// Registers a kind whose factory returns a base-class shell for one context and a derived-class
        /// shell for another, which is the only way this lane can tell exact-type removal from
        /// assignable-from removal.
        /// </summary>
        internal void RegisterTypedFamily(string consumer, string kind, UiWindowOptions? options)
        {
            Catalog.Register(
                consumer,
                kind,
                options,
                key => string.Equals(key.ContextKey, "derived", StringComparison.Ordinal)
                    ? (UiWindowHost)new LaneShellDerived()
                    : new LaneShellBase());
        }

        internal void Place(UiWindowKey key, Rect rect)
        {
            if (!Catalog.TryGet(key, out UiWindowHost host))
            {
                throw new Exception("no open instance for " + key);
            }

            host.windowRect = rect;
        }
    }

    /// <summary>A window with no catalog, used as the untouched baseline for the option assertions.</summary>
    private sealed class BaselineWindow : Window
    {
        public override void DoWindowContents(Rect inRect)
        {
        }
    }

    /// <summary>
    /// A shell that opts the host it created in to diagnostics - the shape a consumer uses, because the
    /// shell is the only place that knows the <see cref="UiHost"/> it built - with a long title and a long
    /// notice so both of its own bands produce a fit finding. <see cref="Prerequisite"/> is mutable so the
    /// notice path can be driven while the host is still alive.
    /// </summary>
    private sealed class SubscribedShell : UiWindowHost
    {
        private readonly string source;
        private readonly string title;
        private readonly bool subscribe;
        private readonly LaneMetrics ruler = new LaneMetrics();

        internal SubscribedShell(string source, string title, bool subscribe)
        {
            this.source = source;
            this.title = title;
            this.subscribe = subscribe;
        }

        internal UiDiagnosticSubscription? Subscription { get; private set; }

        internal bool Prerequisite = true;

        protected override UiTheme Theme => UiTheme.DarkGold;

        protected override string Title => title;

        protected override bool PrerequisiteVerified => Prerequisite;

        protected override ITextMetrics Metrics => ruler;

        protected override UiHost CreateHost()
        {
            var host = new UiHost(
                source,
                Page(),
                new UiBindings(),
                UiTheme.DarkGold,
                ruler,
                new LaneTranslation());

            if (subscribe)
            {
                Subscription = UiDiagnosticHub.Subscribe(host);
            }

            return host;
        }

        protected override void DrawNotice(Rect rect, UiWindowNotice notice)
        {
            UiThemeDraw.Label(rect, "notice:" + notice + ":" + new string('n', 60), Theme, singleLine: true);
        }
    }

    /// <summary>
    /// A base shell class and a derived one, so the lane can tell "exact C# type" from "assignable-from".
    /// <c>UiPageWindow</c> is sealed and one shared class is exactly the blind spot the adversarial pass
    /// found, so the two-class family has to live here.
    /// </summary>
    private class LaneShellBase : UiWindowHost
    {
        protected override UiTheme Theme => UiTheme.DarkGold;

        protected override UiHost CreateHost()
        {
            return new UiHost(
                "window-catalog-lane",
                Page(),
                new UiBindings(),
                UiTheme.DarkGold,
                new LaneMetrics(),
                new LaneTranslation());
        }

        protected override void DrawNotice(Rect rect, UiWindowNotice notice)
        {
        }
    }

    private sealed class LaneShellDerived : LaneShellBase
    {
    }

    private sealed class LaneMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 16f;

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class LaneTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
    }
}
