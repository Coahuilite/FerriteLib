# Definitions, instances and updates

Use ordinary C# functions to return immutable UiElementSpec component definitions, then assemble a
manifest and let UiHost validate the actual binding types. XML follows the same host contract; C# is
not serialized into XML and XML does not execute arbitrary C#.

```csharp
UiElementSpec NameField(string id, string binding) => new UiElementSpec(id, "input/text-field",
    new Dictionary<string, string> { ["Bind"] = binding, ["Label"] = "Name" });
UiLayoutManifest page = UiLayoutManifest.Create("my-page", new[] {
    new UiElementSpec("form", "Stack", children: new[] { NameField("name", "model.name") })
});
// Supply the usual typed bindings, theme, text metrics and translation to UiHost.
host.UpdateDefinition(page); // explicit replacement; no document-service registration prerequisite
```

Definitions are copied into read-only collections. Runtime nodes/state belong to the host session,
not to the component function or its immutable definition. Create checks bounded structure, identities,
template names/references and unsupported widget children; UiHost checks kind/attribute/layout/typed
binding contracts. Finite same-parent anchor references/cycles use the same production grammar and
neutral dependency resolver at preflight and actual arrangement. The regular first arrange still
uses real width/text services; its runtime recovery is not claimed as a native acceptance result.

UpdateDefinition uses the existing prepare/stage/seal transaction. A refused type/graph keeps the old
definition and editor; a later staging failure restores the old tree/resolver and exact current theme
claims, not a value copy that promotes undeclared defaults. Successful compatible identities keep their
instance-local state, while removed/kind-changed owners release their editor/focus/compatibility state
without publishing a draft or retrying business operations. DefinitionRevision advances only at a
successful seal; snapshots carry the accepted revision. File-backed UiDocumentService retains its
bounded watcher/scheduler, preflight-across-hosts, batch rollback and last-good behavior; program updates
do not create another file or update service.

## Repeat keeps the item scope and its old failure conditions

A named template is instantiated under each accepted stable item key. Reorder moves geometry without
changing the item's node, editor, pointer target or business key. Translation/catalog keys remain global.
The exact binding roles qualified to items.itemKey are Bind, ActionBind, OptionsBind, VisibleKey,
PayloadKey, SelectedKey, CancelBind, PointKeysBind, DisclosureBind, CheckBind, WidthKey and HoverHelpKey.
The latter five were missing from the old explicit role list: previously they accidentally resolved a
page-level key (or a missing key); now the real per-item bindings are consumed. No suffix heuristic is
used: TextKey/LabelKey/HelpKey are not reinterpreted as value bindings.

No new per-item registration prerequisite is introduced. The established missing/mistyped/late item
binding fail-soft contracts remain: the affected wrapper reports its real unavailable/default fact or
its established throwing-read recovery, and a later binding can arrive normally. This is distinct from
page-level typed preflight. Template/raw MatchContent without an independent parent reference retains
its natural-height fallback and now has a located ContentHeightUnavailable diagnostic. Finite anchors
inside each template are qualified to the exact same item; arbitrary cross-item references are absent.

## Page appearance is an application, not a host snapshot

UiTheme keeps its current explicit host values/claims and finite page-style applications separately.
The effective read overlays only the font, metrics and colours the page actually declared. Clearing an
outgoing page application reveals the current host declaration, including changes made after host
creation. It does not restore the entire theme from a creation-time snapshot or erase unrelated tints.
A sparse Gap3 page plus a later host RowHeight45/Gap9/font Medium/tint therefore reveals those actual
host values when the page density is removed; source/removal intent survives as well. Transaction
rollback copies both written claims and the prior effective applications exactly, including the thumb
colour and nullable surface edges. Public UiStyleResolver.ApplyTo retains its old explicit application
behavior for caller-owned themes; no new prerequisite or theme service is added.

P1 confirms existing fonts, the semantic palette and five individual metrics, with one Scheme and one
Density per level. Density overrides Scheme per metric; explicit instance declarations win. Default
private structure stays read-only. E1–E5/N1–N3 and L1 are confirmed; the editing/property contracts
record implementation and evidence independently of native acceptance. S1–S7 dependency and migration
commitments are in [the public contract](public-contract.md).

Evidence: KernelAssemblyTests exercises XML/program geometry, immutable program updates, real
prepare/stage failure and editor cleanup, and actual two-item Tree/check/disclosure/chart-key/Dropdown
help/width/anchor behavior through reorder. KernelDocumentReloadTests covers real document-service
Gap-only removal with current host values/sources and the existing multi-host atomicity/cleanup cases.
Host integration and bounded R9 are documented in `host-observation-contract.md`. Native RimWorld1.6 acceptance remains pending.
