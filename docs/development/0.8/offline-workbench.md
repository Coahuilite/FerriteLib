# Optional offline workbench maintenance extension

The normative contract is [developer-tools](../../openspec/specs/contracts/developer-tools/spec.md); this guide owns current API usage and implementation evidence. New native work follows RimWorld 1.7 APIs rather than a fixed minor build; historical build identities below remain evidence, not a support ceiling.

Use one author document while iterating a real page: edit the file in your editor, reload it, and keep
the original Host/Session. This extension is explicitly selected for the 0.8 maintenance line; it does
not imply that arbitrary historical 0.8 builds support Tools.

## Identity and adoption

The API/build/release axis remains **0.8.0**. `UiOfflineWorkbench.Capability` is
`uikit.offline-workbench.v1`; `UiOfflineWorkbench.Profile` is `Legacy08`. A workbench selects the
`[0.8.0,0.9.0)` API range **and** that maintenance capability, and records its exact fixed DLL/MVID.
Current09 retains its separate `[0.9.0,0.10.0)` gate. Never mix generations in one process.

The new symbols and explicitly listed advanced members are **public-unstable maintenance extensions**,
authorized as additive development tooling. They do not promote or replace the frozen Stable surface.
Ordinary `UiHost`, `Create`, independent/native/no-Host usage, R9 and existing diagnostic subscriptions
retain their original signatures, meaning and legal conditions. Players need neither Tools nor FER_DEV.

The selected reuse is the existing optional Host queue, wrapper semantic actions, preparation, layout
inputs and dormant instruments, not a replacement of the 0.8 core with the 0.9 tree. The original 0.8
version, document transaction, reactive/layout core and 1.6 production entry remain authoritative.

## Keep author selection inside the document transaction

```csharp
var documents = new UiDocumentService(autoWatch: false);
documents.AddProjectedLayout(
    new UiDocumentSource("author.page", UiDocumentKind.Layout, authorPath),
    embeddedFullDocument,
    ProjectAuthorDocument); // Func<string, UiLayoutManifest>, pure, includes templates/styles

var accepted = documents.GetSnapshot("author.page")!;
if (!accepted.HasLastGood) throw new InvalidOperationException("No valid author document.");
var host = CreateActualPage(accepted.Layout!); // original registry, typed bindings and isolated sample
documents.Attach(host, "author.page");
var preview = new UiPreviewInstance(host, "isolated author sample", measurementSource,
    resources: documents,
    prepare: available => PrepareActualPage(host, available),
    documents: documents);
```

The service reads one candidate byte snapshot and validates the complete XML (DTD/entities prohibited).
The author's pure projection extracts its own region, templates and styles from that string. The
projected layout and styles pass the existing per-Host prepare/stage/seal transaction. Any projection,
parse or binding refusal keeps the last-good layout **and its full source text/revision**. Projection
callbacks must not mutate Hosts, sample state, files or registrations.

`GetSnapshot(id).Layout` and `.Text` are the last accepted definition and full author text. Prepare
must use that authority or `host.SetLayoutInput`, never reconstruct an embedded definition. A standalone
style file may still use the unchanged `Add` plus `Attach(host, layoutId, styleId)` route. An attached
standalone style is intentionally the existing overriding style authority, not a merge of two owners.

`UiPreviewInstance` retains both four- and five-argument constructors. The additive six-argument form
borrows `Documents`; lifetime is still controlled by `resources`. An author has no Tools DLL dependency.
Register through `UiPreviewRegistration` and an explicit `Register(ITextMetrics,IUiTranslation,string)`
factory. Registration itself does not open a game window or save settings.

For explicit author roots and startup language, expose the optional **static `Register(UiPreviewContext)`**
overload, returning the same registration type or sequence. The workbench prefers it when present and
otherwise invokes the original three-argument entry unchanged. The context contains `Metrics`,
`Translation`, `MeasurementSource`, `AuthorDirectory` (absolute root or null), `Watch`, and `Language`
(`zh-Hans` or `en`). `--author-directory` supplies the root; `--watch` and `--language` supply the other
options. The author chooses filenames and fallback policy. Built-in Tools `--documents` remains a
separate existing option, not an implicit author root. No environment variable or Tools reference is
needed. Language is a startup input; rebuild/restart remains the boundary for changed translation code.

## Read source facts and reload safely

`Snapshots`/`GetSnapshot` expose source ID/kind/path, accepted version, external versus first-load
embedded origin, watcher and failed-version facts. `snapshot.Locate(elementId)` locates the declared Id
or template in the accepted **full** author XML, including 1-based line/column. Generated nodes without
a declared match report that reason; they do not invent a disk line. A fallback location is explicitly
embedded, not a claim that its bytes exist at the external path. These reads do not re-read disk.

`StatusRevision` is an owner-thread status stamp, not a document version or report count. It changes
when registered sources, watcher state, accepted candidates or active refusals change. Poll it to
refresh an inspector only on change. Restoring the exact last-good bytes clears an active refusal
even though the unchanged-byte reload is skipped and the historical report list remains intact.

Manual `Reload`/`ReloadAll` is immediate. Opt-in watch uses the original signal/debounce/bounded retry
and owner-frame pump. Capture can defer a commit for at most the existing policy limit; this is not
general focus/IME protection. Compatible full identity paths and kinds retain Session state, scroll,
expansion and drafts; removal, kind changes or identity-path changes retire the affected state. Reload
never commits a draft or overwrites/reverts the source file. Use the editor to save or undo disk changes.

## Semantic edits and build targets

Tools connects explicitly on the original Host thread. A capture mints connection-local addresses with
definition/mount/item lifetimes. Queue requests are bounded and attempted once by the owner; stale,
closed, disabled, modal or otherwise ineligible targets are refused. `Handled` means the original owner
returned, not that business data was accepted or persisted. Custom editor wrappers may compose
`UiControls.EditSemantic`; SetDraft remains local until explicit CommitEdit or CancelEdit.

The normal default build is still net472/1.6. `-p:UiKitTarget=Neutral` builds the same real Kernel on
net10.0. `-p:UiKitTarget=RimWorld17 -p:RimWorld17Dir=<exact-local-assemblies>` builds the 0.8 core with
the existing native adapter compiled against the supplied local 1.7.5000 rev1275 references. Label that
route **FL0.8 core / 1.7 rendering reference**; it is not an accurate standalone 1.6 host. No game assets
are copied. C# bindings, controls or logic changes require rebuild and process restart.
