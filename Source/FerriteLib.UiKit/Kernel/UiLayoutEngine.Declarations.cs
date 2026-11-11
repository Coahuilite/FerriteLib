using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

internal sealed partial class UiLayoutEngine
{
    // This is a declaration result, not a credential for reads, layout, input or presentation.
    // Specs/child lists are immutable. A hit checks their actual owner and root identity once;
    // it never walks descendant definitions to prove that the same definitions are unchanged.
    private sealed class RowDeclarations
    {
        internal readonly UiElementSpec Spec;
        internal readonly UiNodeId Id;
        internal readonly UiNode? Node;
        internal readonly long Mount;
        internal readonly UiNodeId[] Identities;
        internal RowDeclarations(UiElementSpec spec, UiNodeId id, UiNode? node)
        {
            Spec = spec; Id = id; Node = node; Mount = node?.MountLifetime ?? 0;
            var identities = new HashSet<UiNodeId> { id };
            CollectChildIdentities(spec, id, identities);
            Identities = new UiNodeId[identities.Count]; identities.CopyTo(Identities);
        }
    }
    private sealed class RepeatDeclarations
    {
        internal readonly UiRegionAttachment Attachment;
        internal readonly UiNode Owner;
        internal readonly UiNode? Parent;
        internal readonly long Mount, ParentMount;
        internal readonly UiElementSpec Declaration, Template;
        internal readonly Dictionary<string, RowDeclarations> Rows;
        internal RepeatDeclarations(UiRegionAttachment attachment, UiNode owner, UiElementSpec declaration, UiElementSpec template)
        {
            Attachment = attachment; Owner = owner; Parent = owner.Parent; Mount = owner.MountLifetime;
            ParentMount = owner.Parent?.MountLifetime ?? 0;
            Declaration = declaration; Template = template; Rows = new(StringComparer.Ordinal);
        }
        private RepeatDeclarations(RepeatDeclarations prior)
        {
            Attachment = prior.Attachment; Owner = prior.Owner; Parent = prior.Parent; Mount = prior.Mount;
            ParentMount = prior.ParentMount;
            Declaration = prior.Declaration; Template = prior.Template;
            Rows = new(prior.Rows, StringComparer.Ordinal);
        }
        // The full transaction already copies RepeatState. Only immutable row results are shared;
        // candidate additions/removals cannot alter the rollback state's membership or records.
        internal RepeatDeclarations Copy() => new(this);
        internal void Retain(Dictionary<string, UiElementSpec> derived)
        {
            var gone = new List<string>();
            foreach (string key in Rows.Keys) if (!derived.ContainsKey(key)) gone.Add(key);
            foreach (string key in gone) Rows.Remove(key);
        }
    }
    private RepeatDeclarations? RepeatDeclarationProof(UiNodeId id, UiElementSpec? spec, RepeatState state)
    {
        UiWidgetContext? env = regionEnvironment;
        UiNode? owner = env?.Session.GetNode(id);
        if (env == null || Regions == null || spec == null || owner == null || !state.Initialized
            || !ReferenceEquals(state.Declaration, spec) || !RegionBackend(env)
            || !ReferenceEquals(env.Bindings, UiBindings.OwnerOf(env.Bindings))
            || !templates.TryGetValue(state.TemplateName, out UiElementSpec? template)) return null;
        UiRegionAttachment? attachment = Regions.For(spec, owner);
        if (attachment == null || Regions.Inputs(attachment) == null) return null;
        RepeatDeclarations? proof = state.Declarations;
        if (proof != null && ReferenceEquals(proof.Attachment, attachment) && ReferenceEquals(proof.Owner, owner)
            && ReferenceEquals(proof.Parent, owner.Parent) && proof.Mount == owner.MountLifetime
            && proof.ParentMount == (owner.Parent?.MountLifetime ?? 0)
            && ReferenceEquals(proof.Declaration, spec) && ReferenceEquals(proof.Template, template)) return proof;
        return new RepeatDeclarations(attachment, owner, spec, template);
    }
    private void CollectRepeatDeclarations(UiNodeId id, UiElementSpec? spec, RepeatState state, HashSet<UiNodeId> into)
    {
        RepeatDeclarations? proof = RepeatDeclarationProof(id, spec, state);
        state.Declarations = proof;
        foreach (ItemRow row in state.Rows)
        {
            UiNodeId rowId = id.Child(row.Spec, row.Index);
            if (proof == null)
            {
                // Unknown/not enrolled keeps the old complete collector, including hidden rows.
                into.Add(rowId); CollectChildIdentities(row.Spec, rowId, into); continue;
            }
            UiNode? node = regionEnvironment!.Session.GetNode(rowId);
            if (!proof.Rows.TryGetValue(row.Key, out RowDeclarations? declared)
                || !ReferenceEquals(declared.Spec, row.Spec) || declared.Id != rowId || !ReferenceEquals(declared.Node, node)
                || declared.Mount != (node?.MountLifetime ?? 0))
                proof.Rows[row.Key] = declared = new RowDeclarations(row.Spec, rowId, node);
            // Merge every declared descendant, not just visible entries. Prune still receives the
            // full keep set at its original point; a cached array holds no draft or native state.
            into.UnionWith(declared.Identities);
        }
    }
}
