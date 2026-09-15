using System;
using System.Collections.Generic;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Invalidation lane: the per-key announcement seam. It holds four claims and the positive controls that
/// keep each of them from passing vacuously.
/// <list type="number">
/// <item>A key's revision moves when its owner announces it, and writing through <c>Set</c> is not an
/// announcement: only the owner of the model can say the model moved.</item>
/// <item>The class the binding declared decides what an announcement invalidates. A
/// <see cref="UiInvalidation.Paint"/> key leaves the arranged snapshot alone - a fixed-size readout must
/// not re-measure the page - while a <see cref="UiInvalidation.Measure"/> key on the same page
/// re-arranges. Those two are each other's control: neither can pass by the lane simply never
/// arranging.</item>
/// <item>Invalidation is per key. A <c>Measure</c>-class announcement of a key no arranged element
/// declares invalidates nothing at all, which is the assertion the old single global counter could not
/// have passed.</item>
/// <item>A batch is one commit. Announcing two keys five times together re-measures each declaring
/// element once and moves the arrangement clock exactly one step.</item>
/// </list>
/// The planted-failure evidence for this lane is a source mutation (dropping the per-key dirty marking in
/// <c>UiLayoutEngine.CommitNotifications</c> reddens claims 2 and 4) and it is recorded with the commit;
/// the in-lane controls are claim 2's Paint/Measure contrast and the classification reader check.
/// </summary>
internal static class KernelInvalidationTests
{
    private const string Scope = "invalidation-lane";
    private const string ReadoutKind = "test/readout";

    private static readonly Dictionary<string, int> MeasureCounts = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, float> DrawnHeights = new(StringComparer.Ordinal);
    private static bool announceInDraw;
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("A key's revision moves only when its owner announces it", VerifyRevisionSemantics);
        Run("The classification reader is not vacuous", VerifyClassificationIsRead);
        Run("Paint reuses the arrangement, Measure re-arranges", VerifyClassification);
        Run("A key no arranged element declares invalidates nothing", VerifyUndeclaredKeyTargetsNothing);
        Run("A batch of announcements is one commit", VerifyBatchCommit);
        Run("A notification raised inside a draw pass commits at the next boundary", VerifyDrawPassNotificationIsDeferred);
        Run("A node marked dirty re-runs the arrange without moving the clock", VerifyDirtyFlagForcesReArrange);
        return failures;
    }

    private static void VerifyRevisionSemantics()
    {
        var bindings = new UiBindings();
        float value = 1f;
        bindings.BindValue("editable", () => value, updated => value = updated);
        bindings.BindReadOnly("published", () => value);

        Check(bindings.GetRevision("editable") == 0, "a registered key starts at revision zero");
        Check(bindings.GetRevision("nobody-registered-this") == 0, "an unknown key answers zero rather than throwing");

        bindings.Set("editable", 2f);
        Check(
            bindings.GetRevision("editable") == 0,
            "writing through Set is not an announcement: only the model's owner can say it moved");

        bindings.NotifyChanged("editable");
        Check(bindings.GetRevision("editable") == 1, "one announcement moves the key's revision by one");

        bindings.NotifyChanged("editable", "editable");
        Check(
            bindings.GetRevision("editable") == 3,
            "repeating a key counts again - the counter counts announcements; the commit is what deduplicates");

        Check(bindings.GetRevision("published") == 0, "and an unrelated key's revision is untouched by any of it");

        Check(ThrewNull(bindings), "a null announcement batch is a caller error");
        Check(ThrewEmpty(bindings), "an empty key is a caller error rather than a silent no-op");
    }

    private static void VerifyClassificationIsRead()
    {
        var bindings = new UiBindings();
        bindings.BindReadOnly("paint", () => 1f, UiInvalidation.Paint);
        bindings.BindReadOnly("measure", () => 1f, UiInvalidation.Measure);
        bindings.BindReadOnly("structure", () => 1f, UiInvalidation.Structure);
        bindings.BindReadOnly("undeclared", () => 1f);
        bindings.BindCommand("apply", () => { }, () => false);

        Check(bindings.GetInvalidation("paint") == UiInvalidation.Paint, "a declared class is what the reader returns");
        Check(
            bindings.GetInvalidation("structure") == UiInvalidation.Structure,
            "each key returns its own declaration rather than a fixed default");
        Check(
            bindings.GetInvalidation("undeclared") == UiInvalidation.Everything,
            "a binding that declared nothing answers conservatively");
        Check(
            bindings.GetInvalidation("never-registered") == UiInvalidation.Everything,
            "and so does a key nobody registered: not knowing what a key moves is not a reason to under-invalidate");
        Check(
            bindings.GetInvalidation("apply") == UiInvalidation.Paint,
            "a command's executability move is a paint-class default - a disabled plane, not a re-measure");
        Check(
            (bindings.GetInvalidation("measure") & UiInvalidation.Structure) == 0,
            "the flags are per class, not all-or-nothing");
    }

    private static void VerifyClassification()
    {
        float heightA = 10f;
        float heightB = 20f;
        var bindings = new UiBindings();
        bindings.BindReadOnly("a", () => heightA, UiInvalidation.Paint);
        bindings.BindReadOnly("b", () => heightB, UiInvalidation.Measure);

        using UiHost host = Host(Page("a", "b"), bindings);
        UiLayoutSnapshot first = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            Near(first.RectById["a"].height, 10f) && Near(first.RectById["b"].height, 20f),
            "the first arrange measures every readout from its binding");

        UiLayoutSnapshot cached = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(ReferenceEquals(first, cached), "an unchanged page reuses its arranged snapshot");

        int clockBefore = host.Session.ContentRevision;
        heightA = 40f;
        bindings.NotifyChanged("a");
        UiLayoutSnapshot afterPaint = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            ReferenceEquals(cached, afterPaint),
            "a Paint-class key reuses the arrangement: a fixed-size readout must not re-measure the page");
        Check(
            host.Session.ContentRevision == clockBefore,
            "and a Paint-class announcement must not move the arrangement clock");

        // The control for the pair above: the same page, a key declared Measure. If either half of the
        // engine's decision were missing this lane would pass the reuse claim for the wrong reason.
        heightB = 50f;
        bindings.NotifyChanged("b");
        UiLayoutSnapshot afterMeasure = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            !ReferenceEquals(afterPaint, afterMeasure),
            "a Measure-class key re-arranges, so the reuse above is the class's decision");
        Check(Near(afterMeasure.RectById["b"].height, 50f), "and the new value is what the arrange measured");
        Check(
            Near(afterMeasure.RectById["a"].height, 40f),
            "while the Paint-class value is read by that same arrange - the next paint would have shown it");
        Check(
            host.Session.ContentRevision == clockBefore + 1,
            "and the batch moved the arrangement clock exactly one step");
    }

    private static void VerifyUndeclaredKeyTargetsNothing()
    {
        float heightA = 10f;
        float orphan = 1f;
        var bindings = new UiBindings();
        bindings.BindReadOnly("a", () => heightA, UiInvalidation.Paint);
        bindings.BindReadOnly("orphan", () => orphan, UiInvalidation.Measure);

        using UiHost host = Host(Page("a"), bindings);
        host.MeasureAndArrange(new Vector2(200f, 200f));
        UiLayoutSnapshot cached = host.MeasureAndArrange(new Vector2(200f, 200f));
        int clock = host.Session.ContentRevision;
        int aBefore = Count("a");

        orphan = 5f;
        bindings.NotifyChanged("orphan");
        UiLayoutSnapshot after = host.MeasureAndArrange(new Vector2(200f, 200f));

        Check(
            ReferenceEquals(cached, after),
            "a Measure-class announcement for a key no arranged element declares invalidates nothing");
        Check(host.Session.ContentRevision == clock, "and leaves the arrangement clock alone");
        Check(Count("a") == aBefore, "and re-measures no element: invalidation is per key, not page-wide");
    }

    private static void VerifyBatchCommit()
    {
        float heightB = 20f;
        float heightC = 30f;
        var bindings = new UiBindings();
        bindings.BindReadOnly("b", () => heightB, UiInvalidation.Measure);
        bindings.BindReadOnly("c", () => heightC, UiInvalidation.Structure);

        using UiHost host = Host(Page("b", "c"), bindings);
        host.MeasureAndArrange(new Vector2(200f, 200f));
        host.MeasureAndArrange(new Vector2(200f, 200f));
        int clock = host.Session.ContentRevision;
        int bBefore = Count("b");
        int cBefore = Count("c");

        heightB = 60f;
        heightC = 70f;
        bindings.NotifyChanged("b", "b", "c", "c", "c");
        UiLayoutSnapshot after = host.MeasureAndArrange(new Vector2(200f, 200f));

        Check(Count("b") == bBefore + 1, "three announcements of one key re-measure its element once");
        Check(Count("c") == cBefore + 1, "and two announcements of another do the same");
        Check(
            Near(after.RectById["b"].height, 60f) && Near(after.RectById["c"].height, 70f),
            "the commit uses the model's current values for the whole batch");
        Check(
            host.Session.ContentRevision == clock + 1,
            "five announcements across two keys coalesce into exactly one clock step, not five");
    }

    /// <summary>
    /// The boundary half of the batch claim, raised from the place a page actually raises it: a widget
    /// callback inside the draw pass. A commit there would mutate the tree the pass is painting, so the
    /// announcement must be recorded and committed at the next arrangement boundary instead. The lens is
    /// the drawn rect: the pass that announced finishes with the geometry it started with, and the value it
    /// announced arrives one frame later.
    /// </summary>
    private static void VerifyDrawPassNotificationIsDeferred()
    {
        float height = 10f;
        var bindings = new UiBindings();
        bindings.BindReadOnly("k", () => height, UiInvalidation.Measure);

        using UiHost host = Host(Page("k"), bindings);
        host.MeasureAndArrange(new Vector2(200f, 200f));
        host.MeasureAndArrange(new Vector2(200f, 200f));
        int clock = host.Session.ContentRevision;
        int measured = Count("k");

        var viewport = new Rect(0f, 0f, 200f, 200f);
        height = 50f;
        announceInDraw = true;
        host.DrawFrame(viewport);

        Check(Count("k") == measured, "a notification raised inside the draw pass does not re-arrange that pass");
        Check(Near(DrawnHeightOf("k"), 10f), "and the pass finishes with the tree it started with");
        Check(host.Session.ContentRevision == clock, "so nothing committed mid-pass");

        announceInDraw = false;
        host.DrawFrame(viewport);

        Check(Count("k") == measured + 1, "the commit lands on the next arrangement boundary");
        Check(Near(DrawnHeightOf("k"), 50f), "carrying the value the model moved to");
        Check(host.Session.ContentRevision == clock + 1, "and the arrangement clock moves exactly once for it");
    }

    /// <summary>
    /// The dirty-flag half of the arrangement cache, held to the claim the a2 result left open: the
    /// per-key commit sets the node flag and the clock together, so removing only the marking changes
    /// nothing - which is a statement about the commit, not about the flag. This drives the flag alone:
    /// one node marked by hand, no announcement, and the content revision asserted equal before and after,
    /// so the re-arrange can only have come from the dirty set. `KernelIdentityTests` already pins that a
    /// dirty node re-measures; what is added here is the clock standing still, plus the flag being cleared
    /// by the arrange that answered it. Removing the dirty check from the arrangement cache reddens both.
    /// </summary>
    private static void VerifyDirtyFlagForcesReArrange()
    {
        float height = 10f;
        var bindings = new UiBindings();
        bindings.BindReadOnly("k", () => height, UiInvalidation.Paint);

        using UiHost host = Host(Page("k"), bindings);
        host.MeasureAndArrange(new Vector2(200f, 200f));
        UiLayoutSnapshot cached = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            ReferenceEquals(cached, host.MeasureAndArrange(new Vector2(200f, 200f))),
            "an idle page keeps reusing its arranged snapshot");

        UiNode node = host.Session.GetNodeByElementId("k")
            ?? throw new Exception("the readout was not arranged");
        int clock = host.Session.ContentRevision;
        int measured = Count("k");

        node.MarkDirty();
        Check(node.IsDirty, "MarkDirty raises the node's own flag");

        UiLayoutSnapshot after = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            !ReferenceEquals(cached, after),
            "one dirty node re-runs the arrange with the content revision unchanged");
        Check(
            host.Session.ContentRevision == clock,
            "and the arrangement clock is not what did it: this is the flag's own path");
        Check(!node.IsDirty, "the arrange clears the flag it answered");
        Check(Count("k") == measured + 1, "and the element was measured again");
    }

    // --- helpers -------------------------------------------------------------------------------

    /// <summary>One row per test, carrying exactly the readouts whose keys that test binds.</summary>
    private static string Page(params string[] keys)
    {
        var xml = new System.Text.StringBuilder();
        xml.Append("<UiPage Schema=\"2\" Source=\"").Append(Scope).Append("\"><Row Id=\"row\" Gap=\"4\">");
        foreach (string key in keys)
        {
            xml.Append("<Widget Id=\"").Append(key).Append("\" Kind=\"").Append(ReadoutKind)
                .Append("\" Bind=\"").Append(key).Append("\" />");
        }

        xml.Append("</Row></UiPage>");
        return xml.ToString();
    }

    private static UiHost Host(string xml, UiBindings bindings)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, ReadoutKind, () => new ReadoutWidget(), new[] { "Id", "Kind", "Bind" });
        MeasureCounts.Clear();
        DrawnHeights.Clear();
        announceInDraw = false;
        return new UiHost(
            Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
    }

    private static int Count(string key)
    {
        return MeasureCounts.TryGetValue(key, out int calls) ? calls : 0;
    }

    private static bool ThrewNull(UiBindings bindings)
    {
        try
        {
            bindings.NotifyChanged((string[])null!);
        }
        catch (ArgumentNullException)
        {
            return true;
        }

        return false;
    }

    private static bool ThrewEmpty(UiBindings bindings)
    {
        try
        {
            bindings.NotifyChanged("");
        }
        catch (ArgumentException)
        {
            return true;
        }

        return false;
    }

    private static float DrawnHeightOf(string key)
    {
        return DrawnHeights.TryGetValue(key, out float height) ? height : float.NaN;
    }

    private static bool Near(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= 0.01f;
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
    /// One readout: its height is whatever its declared key currently answers, and every Measure is
    /// counted. The count is what makes "this element was re-measured" and "this element was not"
    /// observable at all.
    /// </summary>
    private sealed class ReadoutWidget : IUiWidget
    {
        private UiElementSpec spec = UiElementSpec.Empty;

        string IUiWidget.Kind => ReadoutKind;

        public void Configure(UiElementSpec spec)
        {
            this.spec = spec;
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
            bindings.ValidateValue<float>(Key(), elementPath);
        }

        public float Measure(UiWidgetContext ctx)
        {
            string key = Key();
            MeasureCounts[key] = MeasureCounts.TryGetValue(key, out int calls) ? calls + 1 : 1;
            return ctx.Bindings.Get<float>(key);
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            string key = Key();
            DrawnHeights[key] = rect.height;
            if (announceInDraw)
            {
                // The shape a real page has: the model moved and a widget notices while it paints.
                ctx.Bindings.NotifyChanged(key);
            }
        }

        private string Key()
        {
            return spec.TryGetAttribute("Bind", out string bind) && bind.Trim().Length > 0 ? bind.Trim() : spec.Id;
        }
    }

    private sealed class StubMetrics : ITextMetrics
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

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key)
        {
            return "[" + key + "]";
        }

        public int TranslationRevision => 0;
    }
}
