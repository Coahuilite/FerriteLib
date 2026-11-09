using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

internal sealed partial class UiLayoutEngine
{
    // The first sparse geometry domain is natural vertical flow with unchanged membership/widths.
    // Scroll range, shared columns, Fill/MatchContent and other algorithms keep the complete path.
    private bool ReflowStableStacks(Dictionary<int, float> heights, Dictionary<int, PlacedEntry> changes,
        Dictionary<UiNodeId, StackRecipe> plans)
    {
        if (cachedSnapshot!.Diagnostics.Count != 0) return false;
        // Roots are a vertical sequence too: the complete path passes height - prior root heights
        // to each later root. Moving a retained root cannot update its Fill/descendant Fill budget.
        // Until that external constraint is propagated, preserve the complete transaction on growth
        // or shrinkage. Idle validation and natural multi-root flow still retain their geometry.
        if (rootEntries.Count > 1)
            foreach (int root in rootEntries)
                if (ConsumesAvailableHeight(lastEntries[root].Spec)) return false;
        var parents = new SortedSet<int>();
        foreach (int index in heights.Keys)
        {
            for (UiNode? parent = lastEntries[index].Node.Parent; parent != null; parent = parent.Parent)
            {
                if (!entryIndex.TryGetValue(parent.Id, out int position) || !stackRecipes.TryGetValue(parent.Id, out StackRecipe? recipe)
                    || recipe.Frame.Height.Fixed.HasValue || recipe.Frame.Height.Fill
                    || lastEntries[position].Spec.TryGetBoolean("SharedLabels", out bool shared) && shared) return false;
                foreach (ChildSlot slot in recipe.Slots)
                    if (ConsumesAvailableHeight(slot.Spec) || UiPlacement.IsMatchContentHeight(slot.Spec)) return false;
                parents.Add(position);
            }
        }
        PlacedEntry Current(int index) => changes.TryGetValue(index, out var value) ? value : lastEntries[index];
        void Set(int index, UiLayoutRect rect)
        {
            if (SameRect(rect, lastEntries[index].Rect)) changes.Remove(index);
            else changes[index] = WithRect(Current(index), rect);
        }
        void Move(int index, float x, float y)
        {
            if (x == 0 && y == 0) return;
            int end = index + lastEntries[index].SubtreeCount;
            for (int i = index; i < end; i++)
            {
                UiLayoutRect rect = Current(i).Rect;
                Set(i, new UiLayoutRect(rect.X + x, rect.Y + y, rect.Width, rect.Height));
            }
        }
        foreach (var pair in heights)
        {
            UiLayoutRect old = Current(pair.Key).Rect;
            Set(pair.Key, new UiLayoutRect(old.X, old.Y, old.Width, pair.Value));
        }
        var ordered = new List<int>(parents); ordered.Reverse();
        foreach (int index in ordered)
        {
            PlacedEntry entry = Current(index); StackRecipe recipe = stackRecipes[entry.Id];
            var children = new int[recipe.Slots.Count];
            for (int i = 0; i < children.Length; i++)
            {
                ChildSlot slot = recipe.Slots[i];
                if (!entryIndex.TryGetValue(entry.Id.Child(slot.Spec, slot.DeclaredIndex), out children[i])) return false;
                if (recipe.Children[i].Width != Current(children[i]).MeasureWidth) return false;
            }
            UiLayoutSize Measure(int child, float width, float available, float? reference)
            { PlacedEntry value = Current(children[child]); return new UiLayoutSize(value.Rect.Width, value.Rect.Height); }
            UiLayoutReuse next = UiLayoutReuse.Resolve(recipe.Reuse, recipe.Inputs, Measure,
                m => UiLayout.Stack(recipe.Frame, recipe.Children, m), out bool reused);
            UiLayoutPlan plan = next.Plan;
            if (plan.HeightOverflow) return false;
            foreach (UiLayoutRect slot in plan.Slots)
                if (slot.X < -.01f || slot.Y < -.01f || slot.XMax > plan.Size.Width + .01f || slot.YMax > plan.Size.Height + .01f) return false;
            if (!reused) entry.Node.LayoutArrangeCount++;
            for (int i = 0; i < children.Length; i++)
            {
                UiLayoutRect child = Current(children[i]).Rect, slot = plan.Slots[i];
                Move(children[i], entry.Rect.X + slot.X - child.X, entry.Rect.Y + slot.Y - child.Y);
            }
            Set(index, new UiLayoutRect(entry.Rect.X, entry.Rect.Y, plan.Size.Width, plan.Size.Height));
            plans[entry.Id] = new StackRecipe { Node = recipe.Node, Frame = recipe.Frame, Slots = recipe.Slots,
                Children = recipe.Children, Inputs = recipe.Inputs, Reuse = next, Scope = recipe.Scope, Key = recipe.Key };
        }
        float top = 0;
        foreach (int root in rootEntries) { Move(root, 0, top - Current(root).Rect.Y); top += Current(root).Rect.Height; }
        return true;
    }
}
