using System;
using System.Collections.Generic;
using System.ComponentModel;

using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Notification-adapter lane (0.6, T1): the explicit <see cref="INotifyPropertyChanged"/> bridge, driven
/// through the real <see cref="UiBindings"/> revision counter so "the key was written" is a number rather
/// than an inference.
/// <list type="number">
/// <item>Mapping is explicit. One property may feed many keys; <c>MapAll</c> answers the empty property name
/// and is not a fallback for a named property, which announces nothing and is counted; a repeated
/// <c>Map</c> replaces its mapping; a null or empty key is refused without touching what is in force.</item>
/// <item>A batch is coalescing with a ceiling. Nesting commits once at the outermost scope, the ceiling fires
/// inside a batch, a repeated key is one write, and a dropped inner scope cannot strand the pending set. What
/// a leaked <em>outermost</em> scope actually does is asserted, not assumed.</item>
/// <item>Unsubscribe is deterministic. Attaching twice is one subscription with exactly one release; a stale
/// handle cannot release the subscription that replaced it; <c>Dispose</c> releases every source, disposes
/// none of them and leaves handles and <c>Detach</c> harmless.</item>
/// <item>An off-main-thread notification is refused before the mapping is even resolved: nothing is delivered,
/// no revision moves, the refusal is counted and surfaced with the property name, and it is never replayed.</item>
/// </list>
/// The main-thread answer is an injected double, so the lane never pretends the process moved threads. What
/// this lane cannot show: that the game really is single-threaded, and that a consumer's background producer
/// marshals to the frame thread itself - those are the boundary stated in <c>docs/api-tiers.md</c>, not a
/// harness claim.
/// </summary>
internal static class KernelNotifyAdapterTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Mapping: one property feeds many keys, and Map replaces what it mapped before", VerifyExplicitMapping);
        Run("MapAll answers the empty property name only, and an unmapped name is counted", VerifyMapAllIsNotAFallback);
        Run("A null or empty binding key is refused without replacing the mapping in force", VerifyKeyRefusal);
        Run("Batch: nesting coalesces to one write and a repeated key is written once", VerifyNestedBatch);
        Run("Batch: the ceiling fires inside a batch and a dropped inner scope strands nothing", VerifyBoundedBatch);
        Run("Unsubscribe: attach twice is one subscription and a handle releases exactly once", VerifyHandleSemantics);
        Run("Dispose: releases every source, disposes none, and leaves handles and Detach harmless", VerifyDisposeOwnership);
        Run("Off-thread: refused before mapping, counted, surfaced, and never replayed", VerifyOffThreadRefusal);
        return failures;
    }

    // --- (1) explicit mapping -------------------------------------------------------------------

    private static void VerifyExplicitMapping()
    {
        var bindings = new UiBindings();
        using var adapter = new UiNotifyAdapter(bindings, new MainThreadDouble());
        var vm = new LaneVm();

        Check(ReferenceEquals(adapter.Map("Title", "title.text", "title.width", "header"), adapter),
            "Map is fluent and returns the adapter");
        Check(adapter.MappedPropertyCount == 1, "one property name counts once however many keys it feeds");
        Check(adapter.AttachedSourceCount == 0, "mapping subscribes nothing");
        adapter.Attach(vm);
        Check(adapter.AttachedSourceCount == 1, "Attach subscribes the source");

        vm.Raise("Title");
        Check(bindings.GetRevision("title.text") == 1
            && bindings.GetRevision("title.width") == 1
            && bindings.GetRevision("header") == 1,
            "one property announcement writes every key it was mapped to");
        Check(adapter.FlushCount == 1, "and outside a batch those keys are one write");
        Check(adapter.UnmappedNotificationCount == 0, "a mapped property is never counted as unmapped");

        adapter.Map("Title", "title.text");
        Check(adapter.MappedPropertyCount == 1, "a repeated Map is one mapping, not a second one");

        vm.Raise("Title");
        Check(bindings.GetRevision("title.text") == 2, "the replacement mapping still feeds the key it kept");
        Check(bindings.GetRevision("title.width") == 1 && bindings.GetRevision("header") == 1,
            "and the keys it dropped are no longer announced");
        Check(bindings.GetRevision("unrelated") == 0, "no unrelated key moved");
    }

    private static void VerifyMapAllIsNotAFallback()
    {
        var bindings = new UiBindings();
        using var adapter = new UiNotifyAdapter(bindings, new MainThreadDouble());
        var vm = new LaneVm();
        adapter.MapAll("all.one", "all.two");
        adapter.Map("Named", "named.key");
        adapter.Attach(vm);

        vm.Raise("");
        Check(bindings.GetRevision("all.one") == 1 && bindings.GetRevision("all.two") == 1,
            "the empty property name is what MapAll answers");
        Check(adapter.UnmappedNotificationCount == 0, "and that convention is not an unmapped omission");

        vm.Raise("Names");
        Check(bindings.GetRevision("all.one") == 1 && bindings.GetRevision("all.two") == 1,
            "a named property with no mapping does not fall back to MapAll");
        Check(bindings.GetRevision("named.key") == 0, "and announces nothing at all");
        Check(adapter.UnmappedNotificationCount == 1, "it is counted as an unmapped notification");

        vm.Raise("Named");
        Check(bindings.GetRevision("named.key") == 1, "its own mapping is what announces it");

        // Map("", keys) is the same convention spelled the other way.
        var conventionBindings = new UiBindings();
        using var convention = new UiNotifyAdapter(conventionBindings, new MainThreadDouble());
        var conventionVm = new LaneVm();
        convention.Map("", "alias.key");
        convention.Attach(conventionVm);
        conventionVm.Raise("");
        Check(conventionBindings.GetRevision("alias.key") == 1, "Map(\"\", keys) is the empty-name convention");

        // No MapAll declared: the empty name is an unmapped notification rather than silence.
        var bareBindings = new UiBindings();
        using var bare = new UiNotifyAdapter(bareBindings, new MainThreadDouble());
        var bareVm = new LaneVm();
        bare.Attach(bareVm);
        bareVm.Raise("");
        Check(bare.UnmappedNotificationCount == 1, "with no MapAll declared the empty name is counted too");
    }

    private static void VerifyKeyRefusal()
    {
        var bindings = new UiBindings();
        using var adapter = new UiNotifyAdapter(bindings, new MainThreadDouble());
        var vm = new LaneVm();
        adapter.Map("Keep", "keep.key");
        adapter.Attach(vm);

        Check(Throws<ArgumentException>(() => adapter.Map("Keep", "keep.key", null!)),
            "a null binding key is refused");
        Check(Throws<ArgumentException>(() => adapter.MapAll("all.key", null!)),
            "so is a null key in the all-properties convention");
        Check(Throws<ArgumentException>(() => adapter.Map("Keep", "")),
            "an empty binding key is refused: the binding surface cannot announce it either");
        Check(Throws<ArgumentNullException>(() => adapter.Map("Keep", (string[])null!)),
            "a null key list is a caller error rather than a silent empty mapping");
        Check(adapter.MappedPropertyCount == 1, "a refused mapping does not add a mapping");

        vm.Raise("Keep");
        Check(bindings.GetRevision("keep.key") == 1,
            "and the refused Map did not replace the mapping that was already in force");
        Check(adapter.UnmappedNotificationCount == 0, "the surviving mapping still answers the property");
    }

    // --- (2) bounded batching -------------------------------------------------------------------

    private static void VerifyNestedBatch()
    {
        var bindings = new UiBindings();
        using var adapter = new UiNotifyAdapter(bindings, new MainThreadDouble());
        var vm = new LaneVm();
        adapter.Map("A", "a.key");
        adapter.Map("B", "b.key");
        adapter.Attach(vm);

        using (IDisposable outer = adapter.BeginBatch())
        {
            vm.Raise("A");
            Check(adapter.PendingKeyCount == 1 && bindings.GetRevision("a.key") == 0,
                "inside a batch the key is held, not written");

            using (IDisposable inner = adapter.BeginBatch())
            {
                vm.Raise("B");
                vm.Raise("A");
                vm.Raise("A");
                Check(bindings.GetRevision("a.key") == 0 && bindings.GetRevision("b.key") == 0,
                    "a nested batch still holds both keys");
            }

            Check(bindings.GetRevision("a.key") == 0 && adapter.PendingKeyCount == 2,
                "closing the inner scope does not write: the outer scope is the commit point");
        }

        Check(bindings.GetRevision("a.key") == 1 && bindings.GetRevision("b.key") == 1,
            "the outer scope writes each distinct key exactly once");
        Check(adapter.FlushCount == 1, "the whole nested batch is one write");
        Check(adapter.PendingKeyCount == 0, "and the pending set is empty afterwards");
    }

    /// <summary>
    /// The bounded half, including the case the contract phrases as "a key pending in a batch is still
    /// delivered even if the batch is never ended". Two distinct shapes are asserted, both of which really do
    /// deliver, and the residual a leaked <em>outermost</em> scope creates is asserted as well rather than
    /// implied away: that shape is held until the ceiling fires, which is what makes the ceiling load-bearing.
    /// </summary>
    private static void VerifyBoundedBatch()
    {
        // (a) The ceiling fires inside a batch.
        var ceilingBindings = new UiBindings();
        using var ceiling = new UiNotifyAdapter(ceilingBindings, new MainThreadDouble(), maxPendingKeys: 2);
        var ceilingVm = new LaneVm();
        ceiling.Map("A", "a.key");
        ceiling.Map("B", "b.key");
        ceiling.Map("C", "c.key");
        ceiling.Attach(ceilingVm);

        using (IDisposable batch = ceiling.BeginBatch())
        {
            ceilingVm.Raise("A");
            Check(ceilingBindings.GetRevision("a.key") == 0 && ceiling.PendingKeyCount == 1,
                "one key is below the ceiling and stays held");
            ceilingVm.Raise("B");
            Check(ceilingBindings.GetRevision("a.key") == 1 && ceilingBindings.GetRevision("b.key") == 1,
                "reaching MaxPendingKeys writes what is held even inside a batch");
            Check(ceiling.FlushCount == 1 && ceiling.PendingKeyCount == 0,
                "and that early write empties the set");
            ceilingVm.Raise("C");
            ceilingVm.Raise("C");
            Check(ceilingBindings.GetRevision("c.key") == 0 && ceiling.PendingKeyCount == 1,
                "a key raised twice afterwards is held once");
        }

        Check(ceilingBindings.GetRevision("c.key") == 1 && ceiling.FlushCount == 2,
            "the batch's own handle ending writes it exactly once more");

        // (b) A dropped inner scope does not strand the set: ending the outer scope commits.
        var innerBindings = new UiBindings();
        using var innerAdapter = new UiNotifyAdapter(innerBindings, new MainThreadDouble());
        var innerVm = new LaneVm();
        innerAdapter.Map("P", "p.key");
        innerAdapter.Attach(innerVm);

        IDisposable outer = innerAdapter.BeginBatch();
        IDisposable droppedInner = innerAdapter.BeginBatch();
        innerVm.Raise("P");
        Check(innerBindings.GetRevision("p.key") == 0 && innerAdapter.PendingKeyCount == 1,
            "a key raised inside two nested batches is held");

        outer.Dispose();
        Check(innerBindings.GetRevision("p.key") == 1,
            "ending the outer scope delivers it even though the inner scope was never ended");
        Check(innerAdapter.PendingKeyCount == 0 && innerAdapter.FlushCount == 1, "exactly once, nothing stranded");

        droppedInner.Dispose();
        Check(innerAdapter.FlushCount == 1, "releasing the dropped inner handle afterwards is a harmless no-op");
        innerVm.Raise("P");
        Check(innerBindings.GetRevision("p.key") == 2,
            "and it did not leave a batch open: the next announcement is delivered");

        // (c) The residual, pinned: a leaked OUTERMOST scope holds the key below the ceiling; the ceiling is
        // the escape that resolves it, and Flush deliberately does not.
        var heldBindings = new UiBindings();
        using var heldAdapter = new UiNotifyAdapter(heldBindings, new MainThreadDouble(), maxPendingKeys: 2);
        var heldVm = new LaneVm();
        heldAdapter.Map("P", "p.key");
        heldAdapter.Map("Q", "q.key");
        heldAdapter.Attach(heldVm);

        IDisposable neverEndedOuter = heldAdapter.BeginBatch();
        heldVm.Raise("P");
        Check(heldBindings.GetRevision("p.key") == 0 && heldAdapter.PendingKeyCount == 1,
            "an outermost scope that is never ended holds the key below the ceiling");

        heldAdapter.Flush();
        Check(heldBindings.GetRevision("p.key") == 0,
            "Flush is a no-op while a batch is open: the outermost scope is the commit point");

        heldVm.Raise("Q");
        Check(heldBindings.GetRevision("p.key") == 1 && heldBindings.GetRevision("q.key") == 1,
            "the ceiling is the escape that delivers a set a leaked outermost scope is holding");

        neverEndedOuter.Dispose();
        Check(heldAdapter.FlushCount == 1, "and ending the leaked scope afterwards writes nothing more");
    }

    // --- (3) deterministic unsubscribe ----------------------------------------------------------

    private static void VerifyHandleSemantics()
    {
        var bindings = new UiBindings();
        using var adapter = new UiNotifyAdapter(bindings, new MainThreadDouble());
        var vm = new LaneVm();
        adapter.Map("P", "p.key");

        IDisposable first = adapter.Attach(vm);
        IDisposable second = adapter.Attach(vm);
        Check(vm.Subscriptions == 1, "attaching the same source twice is one subscription");
        Check(adapter.AttachedSourceCount == 1, "and one attached source");

        first.Dispose();
        Check(vm.Unsubscriptions == 1, "releasing either handle releases the one subscription exactly once");
        Check(adapter.AttachedSourceCount == 0, "and the source is off the adapter");

        first.Dispose();
        Check(vm.Unsubscriptions == 1, "releasing the same handle again releases nothing more");

        second.Dispose();
        Check(vm.Unsubscriptions == 1, "the second handle of an already-released subscription releases nothing");

        adapter.Attach(vm);
        Check(vm.Subscriptions == 2 && adapter.AttachedSourceCount == 1, "re-attaching subscribes one again");
        Check(adapter.Detach(vm), "Detach reports the source it removed");
        Check(!adapter.Detach(vm), "and answers false when there is nothing left to remove");
        Check(vm.Unsubscriptions == 2, "each release is exactly one unsubscription");

        // A handle from a subscription that was already gone must not release its replacement.
        IDisposable stale = adapter.Attach(vm);
        Check(adapter.Detach(vm), "the first generation is removed by name");
        adapter.Attach(vm);
        stale.Dispose();
        Check(vm.Unsubscriptions == 3, "the stale handle released nothing: the removed generation was already gone");
        Check(adapter.AttachedSourceCount == 1, "so the subscription that replaced it is still live");

        vm.Raise("P");
        Check(bindings.GetRevision("p.key") == 1, "and it still delivers");

        // Part of the same claim: the equality of two sources is the consumer's business, not the adapter's.
        var equalA = new EqualByValueVm();
        var equalB = new EqualByValueVm();
        Check(equalA.Equals(equalB), "the fixture sources really do answer equal to each other");
        adapter.Attach(equalA);
        adapter.Attach(equalB);
        Check(adapter.AttachedSourceCount == 3, "two value-equal sources are two subscriptions, not one");
        Check(equalA.Subscriptions == 1 && equalB.Subscriptions == 1, "each of them was subscribed once");
    }

    private static void VerifyDisposeOwnership()
    {
        var bindings = new UiBindings();
        var adapter = new UiNotifyAdapter(bindings, new MainThreadDouble());
        var sourceA = new LaneVm();
        var sourceB = new LaneVm();
        adapter.Map("P", "p.key");
        IDisposable handleA = adapter.Attach(sourceA);
        IDisposable handleB = adapter.Attach(sourceB);
        IDisposable batch = adapter.BeginBatch();

        Check(adapter.AttachedSourceCount == 2, "two sources are attached before the dispose");

        adapter.Dispose();
        Check(sourceA.Unsubscriptions == 1 && sourceB.Unsubscriptions == 1,
            "Dispose unsubscribes every source");
        Check(adapter.AttachedSourceCount == 0, "and forgets them");
        Check(sourceA.Disposals == 0 && sourceB.Disposals == 0,
            "Dispose never disposes a source: the consumer owns its own model");

        // A handle and a batch scope that outlive the adapter must not throw out of a using block.
        bool handleThrew = ThrowsAny(() => handleA.Dispose());
        bool batchThrew = ThrowsAny(() => batch.Dispose());
        bool duplicateThrew = ThrowsAny(adapter.Dispose);
        Check(!handleThrew, "a handle disposed after the adapter is harmless");
        Check(!batchThrew, "a batch scope disposed after the adapter is harmless");
        Check(!duplicateThrew, "disposing the adapter twice is harmless");
        Check(sourceA.Unsubscriptions == 1, "and none of that released anything a second time");

        // Detach is the one member that must not throw after Dispose.
        bool detachThrew = ThrowsAny(() => adapter.Detach(sourceA));
        Check(!detachThrew, "Detach does not throw after Dispose");
        Check(!adapter.Detach(sourceA), "and reports that there was nothing to release");
        Check(ThrowsAny(() => adapter.Detach(null!)), "while a null source stays a caller error");

        Check(Throws<ObjectDisposedException>(() => adapter.Map("P", "p.key")), "Map throws after Dispose");
        Check(Throws<ObjectDisposedException>(() => adapter.MapAll()), "MapAll throws after Dispose");
        Check(Throws<ObjectDisposedException>(() => adapter.Attach(sourceA)), "Attach throws after Dispose");
        Check(Throws<ObjectDisposedException>(() => adapter.BeginBatch()), "BeginBatch throws after Dispose");
        Check(Throws<ObjectDisposedException>(adapter.Flush), "Flush throws after Dispose");

        handleB.Dispose();
        Check(sourceB.Unsubscriptions == 1, "the other handle stays harmless too");
    }

    // --- (4) off-main-thread refusal ------------------------------------------------------------

    private static void VerifyOffThreadRefusal()
    {
        var bindings = new UiBindings();
        var thread = new MainThreadDouble { IsCurrent = false };
        using var adapter = new UiNotifyAdapter(bindings, thread);
        var vm = new LaneVm();
        adapter.Map("Value", "value.key");
        adapter.Attach(vm);
        var rejected = new List<string>();
        adapter.NotificationRejected += name => rejected.Add(name);

        vm.Raise("Value");
        Check(bindings.GetRevision("value.key") == 0, "an off-thread notification delivers nothing");
        Check(adapter.OffThreadNotificationCount == 1, "it is counted as refused");
        Check(rejected.Count == 1 && rejected[0] == "Value", "and surfaced with the property name");
        Check(adapter.PendingKeyCount == 0 && adapter.FlushCount == 0, "it is never queued: there is nothing pending");

        vm.Raise("Unmapped");
        Check(adapter.OffThreadNotificationCount == 2 && adapter.UnmappedNotificationCount == 0,
            "the thread question is asked before the mapping, so an unmapped name is not counted as delivered-nothing");
        Check(rejected.Count == 2 && rejected[1] == "Unmapped", "and the rejection still names the property");

        vm.Raise("");
        Check(rejected.Count == 3 && rejected[2].Length == 0, "the empty all-properties name is rejected the same way");

        thread.IsCurrent = true;
        vm.Raise("Value");
        Check(bindings.GetRevision("value.key") == 1, "the same notification on the main thread is delivered");
        Check(adapter.OffThreadNotificationCount == 3 && adapter.FlushCount == 1, "nothing refused was replayed later");
    }

    // --- helpers --------------------------------------------------------------------------------

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

    private static bool Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return true;
        }
        catch
        {
            return false;
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

    /// <summary>
    /// The injected answer to "may I touch the game's objects". The lane flips it; the process never moves
    /// a thread, which is the whole reason the seam exists.
    /// </summary>
    private sealed class MainThreadDouble : IUiMainThread
    {
        public bool IsCurrent { get; set; } = true;
    }

    /// <summary>
    /// A plain C# view model. Its subscription traffic is observable (adds, removes, disposal), which is what
    /// lets "one subscription" and "releases exactly once" be counted rather than inferred from the adapter's
    /// own bookkeeping.
    /// </summary>
    private sealed class LaneVm : INotifyPropertyChanged, IDisposable
    {
        private PropertyChangedEventHandler? subscribers;

        public int Subscriptions;

        public int Unsubscriptions;

        public int Disposals;

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
            Disposals++;
        }
    }

    /// <summary>
    /// A source whose own <c>Equals</c> answers "equal" for every instance: the adapter's dictionaries must
    /// key on identity, or the second one would be mistaken for the first and never subscribed.
    /// </summary>
    private sealed class EqualByValueVm : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? subscribers;

        public int Subscriptions;

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
            }
        }

        public override bool Equals(object? obj) => obj is EqualByValueVm;

        public override int GetHashCode() => 7;
    }
}
