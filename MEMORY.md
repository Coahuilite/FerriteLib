# MEMORY

## Current durable state

- Repository split out of the Universal Squeaker tree on 2026-09-03. FerriteLib is a prerequisite mod,
  `coahuilite.ferritelib`, display name FerriteLib. The three version axes are re-derived from the tree and
  never quoted from this file (`grep -n 'Api = new Version' Source/FerriteLib.UiKit/Kernel/FerriteLibVersion.cs`,
  `<modVersion>` in `About/About.xml`, `<VersionPrefix>` in the csproj) — and note that `main` and `0.4.x`
  now differ on all three, the released line against the open sweep. **Published in the rc-only window:**
  GitHub repo `Coahuilite/FerriteLib` (public); releases `v0.2.0-rc1` and, cut 2026-09-10, `v0.3.0-rc1` from
  merge commit `6a92331` on `main`; `/releases/latest` still 404s, which is the correct
  state while only rc iterations exist. A bare `v0.2.0` was tagged the same day and **withdrawn the same
  day by maintainer ruling** - cutting the stable tag was outside the push authorization: the rc scheme
  exists precisely so "the trial is over" stays a deliberate decision (see `TODO.md` §5). Workshop still
  pending. History was rewritten once before the push (HANDOFF.md removed from all revisions; the buffer
  is now gitignored and local-only), so pre-rewrite lib hashes cited anywhere are stale. Maintainer
  ruling 2026-09-07 closes the follow-up duty: no repo in the series will be rewritten again to fix a
  hash citation and no more re-pointing - a dangling hash is archaeology, not damage. The one-time
  ledger stays at `.git/filter-repo/commit-map` for anyone who cares.
- Provenance: `Source/FerriteLib.UiKit/**` and `tools/FerriteLib.UiKit.Tests/**` were copied out of the
  US repo at US commit `6c7053a` after a file-for-file `diff -r` check, then amended there. US retains
  its own history; this repo's history starts at the split. `6c7053a` is the post-rewrite hash of what was
  `0fe60b0` until US's 2026-09-06 targeted blob rewrite (verified: same message byte-for-byte, reachable
  from US `main`; the old object is dangling locally only).
- **Cross-mod assembly binding is proven in a running game (2026-09-04).** US, shipping no FerriteLib
  payload of its own, loaded and ran its whole settings page and camera overlay with
  `FerriteLib.UiKit.dll` present only in the carrier mod, with no red text and no type-load failure. The
  sibling-`HintPath` + `<Private>False` design is therefore correct, and "ship a copy / go NuGet with
  Private=true" is rejected on evidence rather than preference. This was the top open risk and it is gone.
- **`UiPopup` is the single popup geometry and input primitive (2026-09-04, `4dd97bf`).** `RectFor`
  (below / flip above / pin to viewport top / horizontal clamp / unpublished-viewport pass-through)
  and `DrawOptionList` (panel, single-line option rows, `UiSession.SetPopupRect` publication, row
  hit-test, consume-close-callback) live in one place; `DropdownWidget` and the US composite
  `UsKernelDraw.Dropdown` both delegate to it. The publication is what makes
  `UiNative.DropdownButtonCore`'s yield guard able to fire at all — a consumer that redraws popup rows
  by hand and skips it silently reintroduces "covered trigger steals the option click", which is
  exactly the defect the US side reported and fixed the same day. Any new dropdown-shaped surface must
  go through `UiPopup`; a second copy of either rule is a defect, not a style choice.
- **Contract axis is 0.3.0 (2026-09-07, branch `feat/round-1-0.3.0`) and the bump rule is tightened**:
  pre-1.0, any change to the public surface — additions included — bumps `Api.Minor`, and
  `About/About.xml <modVersion>` moves with it. The 0.1.0 → 0.2.0 bump exists because an additive type
  (UiPopup) shipped without one and a consumer desynced into a TypeLoadException inside UiHost.Draw; the
  tightened rule is what makes Require's readable-report promise hold in both directions. The 0.2.0 →
  0.3.0 move is the entire US→FL round-1 surface (P1–P6 plus FL-side A–E) as **one** bump: a bump per
  item would make the axis measure churn instead of contract, and the release coupling the review resolved
  is one consumer migration against one carrier. The branch is not tagged and not released — cutting a
  stable stays a maintainer decision.
- **Pointer-space contract (2026-09-04, `72afa23`)**: inside scroll/group scopes `Event.current.mousePosition`
  arrives in the container's draw space, while popup rects are published in Host window space. Any
  comparison between the two must convert through the caller's `ctx` origin (`UiNative.PointerPositionIn`);
  comparing raw is the defect that stole every covered option click in game. The stub harness now models
  IMGUI group origins, hot-control capture/activation and per-pass control ids, and two pump lanes drive
  real event passes (flat and scrolled+offset); restoring the raw comparison fails the scrolled lane with
  the same trace signature as the in-game log.
- Still unverified in game, all cheap, all in `TODO.md` §1: the guard's deliberate duplicate-DLL branch,
  the failure shape when the carrier is absent, and — new with round 1 — the window shell against the real
  window stack, the two text paths that just came into the fit audit, and an engine-side recovery trip.
  Everything else in this file remains compile-time, stub-harness or reference-assembly evidence — say so
  rather than implying a game run.
- **Text fit: the harness measures a model, not a font.** There are two rulers and only one of them is
  assertable. The production one is `VerseFerriteTextMetrics` — `Text.CalcSize` / `Text.CalcHeight` under
  an explicit save/restore of `Text.Font`, `Text.Anchor` and `Text.WordWrap`, with `WordWrap = false` on
  the width path because wrapping caps the reported width and hides exactly the overflow the caller is
  looking for. The harness one is `StubTextWidth.Of`: `units × em × 0.5`, where `units` counts 2 for a
  character in the CJK/full-width ranges and 1 otherwise and `em` is 12/16/18 by `UiFont` — a **linear
  function of character count with a single script split**. Whatever Verse returns for a real font
  asset, the stub does not model it, so any agreement between a lane number and an in-game width is
  coincidence rather than evidence. What the lanes can therefore prove: that `Width="Auto"` consults a
  width budget, tracks the widest of the kind's declared label set, respects the `MinWidth`/`MaxWidth`
  clamp, and re-arranges when a `Breakpoint` threshold is crossed. What they cannot prove: that an Auto
  column actually hugs a translated string, or that a measured interval is degenerate, in game — which
  is why `TODO.md` §1 is the critical path and not the backlog. The round-3 "CJK-vs-Latin glyph positive
  control" is a control on the **model**, not a measurement of the game's font; do not cite it as
  in-game geometry. Re-derive both rulers from
  `Source/FerriteLib.UiKit/Kernel/VerseFerriteTextMetrics.cs` and `tools/FerriteLib.UiKit.Tests/StubTextWidth.cs`
  instead of from this paragraph.
- **Two hard rules inherited from the series, both easy to violate by accident.** Nothing in this library
  may persist data into a save - a prerequisite must survive being uninstalled, and a save-written flag is
  the one side effect a player cannot undo. And the Squeaky Ratkin repo (`coahuilite.squeakyratkin`) is
  never a write target: SR is a separate product with its own brand and `SR_` prefix, and this repo's
  neutrality lane exists to keep even its vocabulary out of here.
- **This repository answers for itself alone (maintainer ruling 2026-09-10, `AGENTS.md` Boundaries).** The
  repo is public, and a clone contains no carrier, no sibling mod and no consumer tree: the
  one-library/two-consumers lockstep layout is this maintainer's machine, not the project's shape. Four
  consequences, all now policy rather than preference: no rule, gate, script or piece of evidence here may
  require an outside tree; consumer evidence is **transcribed** into this file as
  `owner/repo@sha:path:line` plus the excerpt and what it proved (US is public so its permalinks resolve
  for anyone; a deferred candidate's never can, which is exactly why transcription is mandatory); the
  cross-repo raw-backend count is a maintainer-side number, not a reproducible measurement; and the
  round/buffer protocol lives only in maintainer-local `HANDOFF.md`, which is gitignored, so no tracked
  file may depend on reading it. Writing outside this repo is authorized per session and per instruction —
  the 2026-09-10 sibling doc fixes were granted that way and treated as maintenance, not as a new channel.
- **The public promise is now an artifact: `docs/api-tiers.md` plus its guard lane (2026-09-10).** The
  payload exports 40 types; the document classifies them 12 stable, 20 public-unstable, 8
  internalize-candidate, and every entry carries the reason it sits where it does.
  `tools/FerriteLib.UiKit.Tests/FerriteLibApiTierTests.cs` reads that file against the assembly's exported
  types and asserts four things: every public type is classified exactly once, no entry names a type that
  has gone, the stable list equals a second copy pinned inside the test (so a promotion or demotion needs two
  deliberate edits in one commit), and a planted unclassified name is actually reported. Mutation evidence:
  renaming one stable entry reddened all four lanes with the exact names, and restoring it turned them green
  again — that makes this the repo's only prose-backed claim of its shape that has been broken and re-fixed
  rather than merely written. What the list forces, and the reason to write it before the next release rather
  than after: the three known breaking debts block 20 of the 40 types from stability, so there is exactly one
  cheap window to pay them — **0.4.0 as one sweep** — while paying them separately would produce three
  breaking minors out of what should be one. The internalise sweep needs one thing first: the 8 candidates
  are kind classes whose `Kind` constants consumers may copy, so a stable container of kind-name constants
  has to exist before the classes go internal (`TODO.md` §3).
- **A `Require` desync is now a named verdict, and that is the durable part** (2026-09-04, `a05fddf`):
  the report must carry `MISMATCH`, the loaded `Api`, and the consumer's compiled floor, so a stale
  carrier is a readable prerequisite error rather than a `TypeLoadException` at first draw. Harness
  coverage for it is `FerriteLibVersionTests`' "A consumer compiled above the loaded carrier gets a named
  MISMATCH, not a pass". Lesson worth keeping even if the version numbers move again: **`Require` only
  protects a consumer if additive changes also move the axis** - pinning the compiled minor is what makes
  "the carrier is older than the DLL I built against" observable. So the axis is not only a breaking-change
  counter; it is a "does this carrier contain what I compiled against" counter, which pre-1.0 is the same
  bump.
- **Publication form decided (2026-09-05, maintainer): two repositories, two release pages, linked
  rather than copied.** `coahuilite.ferritelib` publishes its own GitHub Release and remains the **only**
  publisher of `FerriteLib.UiKit.dll`; Universal Squeaker publishes its own release carrying **only its own
  package**, and every US release body links to the specific lib release its `PrerequisiteApiMin` was
  compiled against. Deliberately rejected: rebuilding the lib artifact inside US's pipeline (two builders
  for one DLL, so the bytes a player holds have no single provenance) and re-attaching lib's zip to US's
  page (two pages that both look canonical, and a split download counter - the count is the demand signal
  during early testing). `actions/download-artifact` cannot cross repositories anyway, so Releases, not
  artifacts, is the sanctioned carrier between them.
- **There are three version axes, not two, and only two were locked.** Contract axis
  `FerriteLibVersion.Api`, release axis `About/About.xml <modVersion>`, and - the one no document named -
  the **build axis**, `Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj <VersionPrefix>`, which is what
  `pack-dev.ps1:45` derives the artifact name and `version.txt` from. The 0.2.0 move updated the first two
  and left the third at 0.1.0, so seven gates stayed green while the packaging script was preparing a zip
  labelled 0.1.0 around a 0.2.0 DLL. FerriteLibVersionTests now asserts all three agree
  ("Build axis in the csproj matches the other two axes"; the harness holds 84 named lanes as of 2026-09-10
  — re-derive with `grep -c 'Run("' tools/FerriteLib.UiKit.Tests/*Tests.cs`, never quote), and the assertion
  is **mutation-proven in one direction only**: reverting `<VersionPrefix>` to
  0.1.0 fails exactly that one assertion and nothing else. The reverse case - a future axis living in a
  fourth file - is guarded by no test, because no such file exists yet.
- **A release asset must be built after the gates run, not during them.** The Dev and Release build
  gates leave `1.6/Assemblies/FerriteLib.UiKit.dll` carrying a `-dev` version suffix, and that is the
  correct identity for a rehearsal — so the github and steam channels refuse it (`Payload is a dev
  build ...`, asserted in `stage-package.ps1` so the two cannot diverge on it) and the release workflow
  rebuilds with `-p:VersionSuffix=` between the gates and the pack to get the bare `0.3.0+<sha>`.
  Anyone wiring these steps in the other order gets a red pack step, not a mislabelled package.
- **Three channels, one staging engine — the consolidation of two copies of the same rules.**
  `stage-package.ps1` owns what a package *is*: a closed set of five allowed files, the content probe on
  named paths, the licence copy, `version.txt` (`build=` / `commit=` / source), and the optional
  deterministic archive. `pack-dev` / `pack-release` / `pack-steam` own identity and nothing else. The
  split they replaced had already drifted: the dev folder's `version.txt` carried no `build=` or
  `commit=` line, so a staged dev folder could not say which configuration built its DLL, and that
  had to be read out of the assembly's metadata by hand. The closed set is mutation-proved both ways —
  copying `1.6/` recursively instead of the one DLL trips "missing 1.6/Assemblies/FerriteLib.UiKit.dll",
  and a deliberately copied `.gitkeep` trips "carries files it must not". Deliberate asymmetries, each
  with a reason: dev tolerates a dirty tree and writes `-dirty` into its label; github has
  `-AllowDirtyTree` as a rehearsal hatch only; steam has none, because it is the last step.
- **Only the GitHub channel archives, and its archive is a function of the commit.** A dev rehearsal and
  a Workshop upload are folders used in place, so the entry-timestamp problem is confined to the one
  artifact whose digest is public. `Compress-Archive` stamps entries from staged mtimes, which staging
  itself rewrites — two packs of one commit gave different SHA-256s over identical content (measured
  2026-09-07) — and pinning mtimes first does not fix it, because NTFS re-dirties a directory during the
  compressor's own walk. So the timestamps are written into the archive: sorted enumeration, every entry
  at the tagged commit's author date, top-level `FerriteLib/` inside so a player unzipping into `Mods/`
  does not get a loose `LoadFolders.xml`. Re-verified after the consolidation by packing one commit twice
  across a clock tick and getting the same SHA-256 both times. No digest value is ever quoted in these
  files: it is a function of the commit, and a stale one reads like a live contract. The dev channel can
  still emit a zip on request (`-Zip`) for when a folder has to travel as one file; that one is not
  deterministic and nothing quotes its digest.
- **One OutputPath for two configurations is a silent wrong-artifact hazard, and it bit packaging
  directly (measured 2026-09-07).** Dev and Release both write `1.6/Assemblies/FerriteLib.UiKit.dll`.
  Right after a full `verify-local -PackDev` run the file at that path was a **Dev-configuration
  assembly of 98,304 bytes**, where a forced Release rebuild of the same commit is **91,136**: an
  incremental `dotnet build -c Release` reported "up to date" against `obj\Release` and never touched the
  payload, because up-to-dateness is judged per-configuration while the output path is shared. The
  packager copies *that path*, so without a forced rebuild it stages the wrong bytes under a
  `version.txt` that still reads dev+sha — indistinguishable downstream, and the staged folder is exactly
  what a developer picks up for an in-game pass. `--no-incremental` fixes the freshness half only.
  **Forcing the rebuild did not make the label true.** Until 2026-09-07 `pack-dev` built `-c Release` while
  passing `BuildFlavor = 'dev'`, so every dev folder shipped a `version.txt` reading `build=dev` over an
  assembly stamped `AssemblyConfigurationAttribute = Release` — and because the Dev configuration is where
  `FER_DEV` lives, the artifact an in-game pass installs had the dev constants compiled out. The consumer's
  `build-dev.ps1` was building this project with `-c Dev` all along, so the two repos read the same label as
  opposite bytes. The gate is now on the measurement, not the claim: `stage-package.ps1` reads the
  configuration attribute out of the payload and refuses a channel whose bytes do not match its name
  (`dev` requires Dev, `github`/`steam` require Release), and `pack-dev` builds `-c Dev --no-incremental`.
  Read in a child process — `Assembly.LoadFile` in the packaging session would hold a handle on the shipped
  DLL, and `MetadataReader` is unavailable on the Store build of PowerShell, whose trimmed
  `System.Reflection.Metadata` has no `PEReader.GetMetadataReader` (measured 2026-09-07). General rule:
  **when two configurations share one output path, "the build said it was current" is not evidence about the
  bytes on disk** — compare the artifact, not the build log, and derive the label from the artifact rather
  than from a parameter. `FER_DEV` currently gates no source, so this changed no behaviour; it is the check
  that will catch the day it does. The same sharing explains a stale `.pdb` beside the payload (Release sets
  `DebugType=none`, so it neither rewrites nor removes the Dev gate's pdb); the closed set keeps it out of
  packages, which is why no strip step is needed for it.
- **GitHub policy does not constrain this shape of distribution** (read from `github/site-policy` and
  `github/docs`, 2026-09-05): Releases are documented as packaging software "for other people to download
  and use", with a stated limit of 1000 assets per release, 2 GiB per file, and **"no limit on the total
  size of a release, nor bandwidth usage"**. The AUP's excessive-bandwidth clause (§9) is a
  relative-to-similar-features test, and its 10 GiB/month figures are Git **LFS** quotas - which this repo
  does not use, because no binary is tracked at all. What the policies actually prohibit is using raw/file
  hosting as a hotlinked CDN and shipping malware; a 45 KiB mod zip is neither. Cross-repo consumption of
  a public release needs no special permission: `GITHUB_TOKEN` with `contents: read`, or a plain
  `browser_download_url`.

- **Licence shape across the series** (measured 2026-09-07): MPL-2.0 everywhere; `LICENSE` is
  byte-identical between this repo and Universal Squeaker (same SHA-256), while SqueakyRatkin carries
  the bare MPL text without the repo-level Exhibit A notice header — so "byte-identical in each repo"
  is true of the lib/consumer pair, not of the whole series. The notice deliberately carries no
  "Incompatible With Secondary Licenses" statement, so the assembly may still be combined with
  GPL-family mods. A distributed mod package is an *Executable Form*, so MPL 3.2's source-availability
  duty is met by `pack-dev.ps1` copying `LICENSE` into the package; gate 6 rejects a truncated paste
  or an applied incompatibility notice (and its limits are stated in "Gates and what each actually
  proves" — parity against a consumer's copy is consumer-side).

- **The payload gate now measures the build's own output path, and a fresh tree bootstraps itself (2026-09-11).**
  Both holes were found by an independent verifier on this repository's own gate suite, not by a failing
  build. Gate 4 asserted only that `1.6/Assemblies/FerriteLib.UiKit.dll` exists, so moving the csproj's
  `<OutputPath>` elsewhere stayed green while consumers bound to a stale, gitignored DLL; it now asks
  MSBuild for the evaluated `TargetPath` and compares that with the path consumers bind to (mutation:
  moved output = gate 4 red with the throw message printed; restored = 7/7). A `git archive` extraction
  also died at gate 1 with MSB3644 because every lane runs `--no-restore`, so the bootstrap restore is
  now one visible `[setup]` step before the lanes (fresh tree, single command = 7/7 in 9.1 s, reproduced
  independently by the verifier). `Invoke-Check` used to swallow a failed gate's thrown message and leave
  only a retry hint that could not address the contract it broke; the message is printed now. Commits
  `d0632ca`, `5399aa7`.

- **The leaf vocabulary exists as of 2026-09-11: five kinds, one of them carried as debt.**
  `text/wrapped`, `input/button`, `chrome/rule`, `input/slider` and `input/number-field` (commits
  `bab3c07`..`2ce61ce`), each justified against `AGENTS.md`'s "What earns a kind": a measure contract
  over its own content (wrapped text), per-element interaction state plus a hit rule (button, number
  field), label-band geometry plus a value contract (slider), and a geometry rule with a negative hit
  rule (rule). Two verdicts are worth carrying. `chrome/rule` is the weakest: it qualifies on the
  geometry clause alone, has no label set and no binding, and its closing condition is written down --
  if a container ever gains a `Divider="Top|Bottom"` attribute, the kind must be deleted. And
  `input/slider` is kept because the bare slider and the stepper composite are two different things
  whose coexistence is itself the proof that neither expresses the other. The kind strings are the
  contract, not the type names: a manifest names a kind, and the type's visibility is a separate
  question the 0.4.x internalise sweep answers. Known gap, filed rather than hidden: popup yield is not
  in the atoms -- `UiNative.YieldsToCoveringPopup` remains the dropdown trigger's private path, so a
  button under an open popup can rediscover the 2026-09-04 click-theft class; the closing item is
  `TODO.md` section 3's owned hit stack, and no atom may grow a second, private yield rule meanwhile.

- **The 0.4.x batch exists on the branch and is archived: style table, identity layer, leaf atoms (2026-09-11).**
  `origin/0.4.x` = `1ececed` carries all of it; `v0.3.0-rc1` remains the only published release. Landed in
  the order this ledger's own sequence asked for: the resolved-value store plus the per-surface token shape
  (`UiStyleTable` keyed by tone/emphasis[/writability], per-surface fill+border pairs, geometry tokens, and
  `LayoutRevision` so a density or font change re-arranges instead of reusing stale bands -- with the
  duplicated two-token mapping deleted from all four outlets: `StatusTreatment`, `StatusBadge`,
  `DropdownWidget`, `InputModeRowWidget` and `UiPopup`); the element identity layer (`UiNodeId`, per-element
  state keys, an ambient element scope set and restored in try/finally so a throwing sibling cannot move
  the next element's slot, plus `HoverClaimElement`/`ActiveElement` as its observation surface); the five
  leaf kinds; and `chrome/banner`/`state/empty` re-expressed over the text atom's band contract while
  keeping their names.
  **Two artifacts are recorded rather than hidden.** (1) Three commits -- `7110b0c`, `f516651`, `dfba792` --
  are red at the commit level: their public types reached `docs/api-tiers.md` one commit later because
  parallel lanes raced on that shared file. The tip was verified green in an isolated extraction before the
  push, and rewriting the batch was refused on purpose: it would invalidate the verifier's per-sha evidence
  and interrupt a lane still writing, while this branch's contract is the tip. The discipline that follows:
  a public type and its tier line go in the same commit, and the tier lane is re-run after any rewrite.
  The race has a second and worse harm, found by the verifier on the same batch: `dfba792` committed a
  491-line identity lane while `Program.cs` did not register it until `7afa1df`, so at that commit the lane
  was dead code -- the file existed, no assertion ran, and every gate was green. A late tier line reddens a
  gate; a late registration hides the evidence entirely. Same root cause, same rule: a public type, its tier
  line and its lane registration go in one commit, checked with `git show <sha>:<file> | grep`, never by
  trusting the working tree.
  The same class bit the lead's own gate 8 wiring the next day: `verify-local.ps1` called
  `scripts/net472-trap-scan.ps1` while the file was still untracked, so every clean extraction reddened on a
  gate whose target did not exist -- and an untracked script is invisible to a `git status` reading, a build
  and a local run alike. Two instances in two days make it a rule rather than an anecdote: anything a script,
  gate or lane refers to must be tracked, and the check is `git archive <sha>` plus a run, never the working
  tree. Gate 8 itself is verified both ways -- 8/8 on a clean tree, and red with `file:line` and the matched
  shape when `Split(',')` is planted.
  Gate 8 runs the scan with three exit codes -- 0 clean, 2 a hit, 3 not scanned (empty scope, or a
  directory with no git metadata) -- and the third one exists because the first version reported a clean
  `HITS=0` over zero files: `git rev-parse` fails with 128 rather than throwing, and a native git failure
  does not raise in PowerShell. A scan that finds nothing is a failure, not a pass. The net472 measurement
  probe deliberately calls every trap shape inside try/catch, so it must never be committed into the repo
  tree -- gate 8 correctly flags it there, which is the gate working, not a false positive.
  (2) Two unpushed commits were amended in flight (`a81182e`->`169cf62`, `085ebd1`->`7afa1df`), both
  reported by their author; that mapping is part of this session's record.
  **Two traps worth keeping.** A mutation check that restores a file with `Copy-Item` keeps the old mtime,
  MSBuild then skips the recompile, and the "green after restore" is the old binary -- touch the file or
  pass `--no-incremental`. And `tools/.../Stubs/**` is a de-facto published surface: the verse stub's
  `Label` gained three additive recording lists so the text-colour routes became observable, which the
  wired consumer sees when it re-pins to 0.4.
- **The 0.4 node step is a real compile break for the wired consumer, and this repo's own comment described
  that break backwards (2026-09-12).** `bd4d1b5` (node step 2) node-keyed session state with no string
  shim — `GetScrollPosition`/`SetScrollPosition` take a `UiNode`, `ScrollPositions` is
  `IReadOnlyDictionary<UiNode, Vector2>`, and the recovery surface moved with it (`TrippedComponentIds`
  became `TrippedNodes`; `IsTripped`/`Trip`/`TryGetTripLog` take a node). The wired consumer is blocked
  by exactly that, in committed code:
  `Coahuilite/UniversalSqueaker@4f7a9e2b8877802fda6d4b20c562e4e1194600bb:Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs:108`
  — `session.SetScrollPosition(ContentScrollId, Vector2.zero);`, where `ContentScrollId` is
  `private const string ContentScrollId = "content-scroll";` (`:118`) — and in its harness at
  `…:tools/UniversalSqueakerKernelHostTests/Program.cs:461` —
  `host.Session.SetScrollPosition("content-scroll", Vector2.zero);`, with the read side at
  `…:1336`, `Vector2 content = host.Session.GetScrollPosition("content-scroll");`. What it proved: a string
  where 0.4 wants a node is a compile break in a tree a player runs, not a shape preference, and the
  migration's bridge already exists — `UiSession.GetNodeByElementId`, "the bridge a caller uses to move from
  the one string a page owns to the node identity everything else keys on; it is a lookup, not a second key
  space". The cited revision is the consumer's committed HEAD, so the break re-derives from their tree
  alone; their in-flight migration is not what this line rests on.
  **The comment was the other half of the failure.** `UiLayoutEngine.ScrollKey`'s doc comment told a reader
  that a consumer reads `ScrollPositions` by that key while the property had already been node-keyed: the
  library shipped prose pointing at the call that no longer compiles — the same family as a lane nobody
  registered, where every gate is green and the evidence is invisible. Corrected in `cbfe680` together with
  `docs/api-tiers.md`'s "Breaking changes inside the open 0.4 window" section, which is now where a moved key
  space, the call that no longer binds and its bridge are recorded before a consumer finds them by compiling.

## Charter — what this library is for

- **The founding spec, transcribed.** `Coahuilite/UniversalSqueaker@09366f8:docs/ui-shared-library-design-zh.md`
  (状态：已接受, 2026-08-24) — written in the consumer's repository, which is why this one never held its own
  rationale until now. It set out to extract "XML 编排引擎 + 基础 UI 组件" out of US into a **private general
  UI library**, phase one serving only US and driven by US's needs, with neutrality as a hard red line (no
  consumer types, no product literals, `object ViewState` passthrough, neutral `UiCommand`, injected
  `ITextMetrics`), and these non-goals: not a public or general UI framework, no uGUI/UIElements path, no
  reflection, DI or code generation, no code or expressions inside manifest XML, and no requirement that hot
  reload be able to add a C# widget kind. Planned packageId was `coahuilite.ferritelib.uikit`; shipped is
  `coahuilite.ferritelib` with assembly `FerriteLib.UiKit`. **Two clauses have been overtaken by fact:** the
  repository has been public since 2026-09-07, and on 2026-09-10 the maintainer ruled the posture
  "referenceable by strangers, held to general-library standards". That retires "现阶段只服务 US" and keeps the
  non-goals, because the backend, reflection and codegen refusals were never about secrecy — they are the
  library's shape.
- **The irreducible core of a retained layer on an immediate host, and where this library actually stands**
  (surveyed 2026-09-10 against egui, Unity IMGUI and UIElements, Flutter, React, RmlUi, Godot; those are
  external sources, so the conclusion is recorded as reasoning and the FL half as measurement). An immediate
  host re-derives geometry and paints in frame order by itself, so a retained layer must own only five
  things: identity that survives insertion and reordering; a hit-and-layer stack with a capture owner; a
  focus owner plus a traversal rule; an invalidation and wake clock; coordinate-space bookkeeping. Layout
  algorithm, text shaping, style cascade, clipping, popup space, animation and IME are negotiable, and FL
  deliberately keeps them in-library — that is a choice, not a requirement. Measured standing, item by item:
  identity exists in *shape* (`UiSession.GetOrCreateValueState(elementId)` over a per-session dictionary of
  `UiValueState { FloatValue, EditText, Dragging, Focused, Cursor }`) but its key is a path string, and an
  unnamed sibling of the same kind collides — §3's element-identity item in `TODO.md` is precisely this; the
  hit stack is single-popup by design (`UiValueState`'s own doc says dropdown openness is session-owned, one
  popup per session) and §3's hit-stack item is its generalisation; focus is real for one control family
  (`UiNative.cs:173-236`) and **there is no traversal rule at all**, while the attribute word `Tab` here
  means a workspace tab (`UiLayoutEngine.cs:1249` resolves it against `UiBindings.ActiveTabKey`), a naming
  hazard the next reader will trip on; the wake clock is legitimately delegated to the game because the game
  repaints every frame, which leaves `Session.ContentRevision` as the only invalidation signal — that is what
  §3's announce item replaces; coordinate-space bookkeeping is the strongest column (the pointer-space
  contract and the `UiPopup` rect rules, both mutation-proved).
- **Three load-bearing premises demoted to their real evidence class (2026-09-10).** (i) "IMGUI composites
  above every Canvas" is the stated reason a uGUI backend is a non-goal, but what is measured in this file is
  only that those assemblies ship and that `Assembly-CSharp` and `Verse.Window` reference `IMGUIModule` and
  `TextRenderingModule` and never `UnityEngine.UI`/`UIModule`. The ordering claim is standard Unity behaviour
  with no measurement of this game's camera or sort setup on record: keep it as design inference. The
  non-goal survives without it, because the game's own windows are IMGUI adapters and a second backend could
  not reach the game's chrome anyway. (ii) The same reference read recorded
  `Verse.Window.Window(IWindowDrawing customWindowDrawing = null)` — a drawing seam inside the game's window
  manager that nobody has examined. Whether Verse honours it, and whether a non-IMGUI surface could live
  inside a real window, is an IL-level question and is unverified; it is the only known question that could
  move the backend non-goal. (iii) The recollection that XML was chosen for "原版布局和热更" is half true:
  declarative layout is real, hot update is owned by nobody. US supplies its manifest as an embedded
  assembly resource (`Coahuilite/UniversalSqueaker@09366f8:Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs:29,319`),
  so a layout edit recompiles the consumer, and this library's only disk-reading entry point
  (`UiLayoutManifest.ParseFile`) has zero callers. The spec never promised the harder half either — it
  explicitly declined hot-reloading a new kind — so `TODO.md` §3 now carries the fork openly: wire a real
  load path, or delete the dead entry and stop implying a capability nothing owns.
- **Ecosystem calibration for the public ruling (surveyed 2026-09-10, external sources).** The RimWorld
  ecosystem has no formal modding API and no way to declare a prerequisite version, which this repo's own
  read of `Verse.ModRequirement` confirms independently. The framework-prerequisite convention is real
  nevertheless: ship a runtime DLL under `Assemblies/`, publish a compile-time-only NuGet (often a `.Ref`
  package), and have consumers declare `modDependencies` plus `loadAfter`. The nearest UI-layer analogues are
  `Cosmere.Lightweave` — a composable IMGUI framework shipped as a prerequisite mod with NuGet, whose
  About.xml calls itself a shared dependency while its README says primitives may break their API freely
  because all its consumers are owned — `Nebulae.RimWorld.UI` (published, self-described personal library),
  and `BetterFloatMenu` (prerequisite-free NuGet helper whose major version tracks the game); `RimHUD` is the
  only UI mod found that carries an explicit `apiVersion`. The lesson worth refusing to skip: **structural
  invitation and contractual support are separate axes, and every precedent found picked one and neglected
  the other.** The 2026-09-10 ruling commits FL to saying both out loud — the README states who may compile
  against it, and the contributing doc, now owed, states what will not break.

- **What the industry converges on, transcribed (surveyed 2026-09-10 against Unity UXML/USS, WPF/MAUI/WinUI
  XAML, Android XML plus Compose, Godot scenes, Flutter, SwiftUI/UIKit, RmlUi and RimWorld Defs — external
  primary docs, so this is a conclusion, not a measurement).** Three decisions are made identically by every
  format that keeps a data file at all. A usable element kind is always a **compiled type resolved through a
  registry**: XAML maps a namespace to a CLR namespace and assembly, Android uses the qualified class name as
  the tag, Unity generates an element's attribute vocabulary from `[UxmlAttribute]` on a `[UxmlElement]`
  partial class, RmlUi binds tags to a registered `ElementInstancer`, and in RimWorld the tags literally *are*
  the public fields of a `Def` subclass. The data file always splits **structure, values and appearance**.
  And invalidation is **always an imperative call on the framework, never something the data expresses**.
  They diverge only on whether a data file exists (Flutter, SwiftUI and Compose delete the question) and how
  loudly unknown syntax fails (RmlUi ignores it, RimWorld errors, this library refuses at creation). So the
  instinct behind our manifest format — XML declares page layout over kinds the DLL already provides — is the
  common shape rather than an idiosyncrasy, and the two moves that would abandon it are code in the manifest
  and a vocabulary that grows without compiling, both already non-goals. Worth copying and already held: the
  per-kind attribute schema, and the kind-declared natural size behind `Width="Auto"`. Not held and owed: a
  written retirement rule for attributes (§5's contract proposal). Worth refusing on the record: loops,
  conditionals and expressions in data, runtime kind discovery, and blanket tolerance of unknown tags.
- **The closest specimen found, and the half of it that failed (Qz-UILib, `github.com/QuanhuZeYu/Qz-UILib`,
  Minecraft 1.7.10 / GTNH, Java; surveyed 2026-09-10 from its repository — external, recorded as report).**
  It has our host problem, an immediate per-frame GUI layer (vanilla `GuiScreen`), and solves it as a retained
  scene stack: reactive signals → per-node dirty marks that **bubble upward only** → incremental flex layout
  with `cachedLayout` short-circuits → an immutable paint plan rebuilt per frame from cached fragments →
  immediate GL behind a backend port. Its own ban list names the two designs it tried and threw out:
  version-number comparison, and downward recursive dirty marking. Identity is a keyed list reconciler that
  reuses nodes by key and computes a longest-increasing-subsequence over the old indices to find zero-move
  items. Keyboard focus is real there — a `focusable` flag whose signal drives Tab-ring membership — which is
  the cost we declined in §4. The cautionary half is that it began declarative in the maximal sense (an
  HTML-like document tree, a CSS-like stylesheet layer, documents pushed from a server) and **deleted the
  whole stack in a breaking major**, with recorded costs of roughly twenty-five browser-semantics bugfixes in
  one minor, layout-reuse debt from the downward marking, god-class splits and two dozen stale documents; its
  capability-boundary doc now says no HTML/CSS/JS parsing or browser semantics, and no exposing GUI lifecycle
  or GL internals to page authors. The lesson is a boundary, not a verdict: declarative *structure over a
  compiled vocabulary* is industry-normal, declarative *semantics imported from the web* is what was bought
  and sold back at this scale. Its own distribution answer is Maven plus JitPack with semver tags, and its
  stability promise is a hand-maintained three-tier list (stable / public-unstable / internal) enforced
  socially plus by architecture-guard tests, with no binary-compatibility tool — the shape §5's proposal
  copies, chosen because a tool heavier than the surface is a promise nobody keeps.
- **"Headless core, own persistence layer, decoupled" — what the author's phrase maps onto in code, and
  what it means for us.** The author told the maintainer (2026-09-10, oral relay) that Qz-UILib gave up the
  web-style implementation but kept a self-written headless core and a persistence layer, decoupled. Read
  against the repository, the sentence separates into two claims of different strength. The headless half is
  exactly what the code shows: `ui.scene`'s node/layout/runtime/input hold geometry, dirty marks, signals and
  interaction state and issue no GL; the frame ends in `ScenePaintEngine` producing an immutable paint plan
  from per-node cached fragments, replayed through a `UiRenderBackend` port, and its ban list forbids the
  paint engine from importing the deleted `ui.dom`/`ui.paint`/`ui.layout`/`ui.component`/`ui.control`
  namespaces. So "decoupled" means *one interface plus one replayer between what the UI is and how pixels
  appear* — and the thing they deleted was the browser-semantics layer, not the retained tree, which survived
  and got stricter. The "persistence" half is ambiguous and worth asking him about directly: in the code the
  persistent structure is the retained node tree with typed property slots, the signal store, and
  `SceneKeyedListReconciler` (reuse nodes by key, longest-increasing-subsequence over old indices to find
  zero-move items) — that is persistence of interaction state across frames. A separate `club.heiqi.config`
  family (~86 classes, `ConfigUI`, schema, field renderers) is the layer that persists settings to storage.
  If he meant the second one, it is a different seam than ours and we have no equivalent problem: this
  library writes nothing anywhere, by series rule.
  **Consequence for FerriteLib, and it is a refusal:** the shape validates our funnel design rather than
  asking us to copy it. We are already headless where it costs nothing — measurement goes through
  `ITextMetrics` and the harness runs because the engine never touches a real font — but our paint side is
  coupled to Verse on purpose, and the coupling is named and gated by five files (`KernelContainmentTests`).
  Adding a `UiRenderBackend` port would buy an abstraction with one implementation and a non-goal behind it,
  which is exactly the speculative surface this library's protocol exists to refuse. What we owe instead is
  keeping the host-free seams host-free: nothing outside the five funnel files learns that IMGUI exists.
- **The carrier's own diagnostic surface, evaluated 2026-09-10 (maintainer proposal: a settings page that
  shows the version contract and every atomic component, not the composites).** Three things it uniquely
  buys, none of which any lane can substitute: it is the only rendering evidence that exists when no consumer
  is installed; it is the only way to push the unconsumed kinds through real glyph metrics without waiting on
  somebody's page; and it turns `Require`'s verdict and the duplicate-carrier report from a log line into
  something a player can be pointed at. One thing it collides with, and the collision is the interesting
  part: this library owns **zero translation keys** (`AGENTS.md` identity), gate 5 refuses a `1.6/Languages/`
  directory by name, and `IUiTranslation` is injected precisely so the library never has to say a sentence
  alone. A page the library owns therefore has no legitimate source for its own text.
  **The entry-point fact that settles the shape (verified 2026-09-10).** This assembly contains no
  `Verse.Mod` subclass and no `ModSettings` (`grep -rn "class .*: Mod\\b\\|ModSettings\\|Harmony" Source` →
  0 hits), so the carrier has no door into the game's UI at all today. Any self-owned surface therefore needs
  one added: a settings row means a `Mod` subclass plus a `ModSettings`, which contradicts the README's
  current claim that enabling the carrier alone changes nothing in the game, and a dev-menu entry needs a
  Harmony patch, which the series refuses. So the shape that gets the evidence without buying a new game
  surface is neither a settings page nor a carrier-owned window: **the library ships the self-check as a page
  *spec*, and a consumer mounts it.** Concretely — a factory returning an `UiElementSpec` tree plus an
  `IUiBindings` view over live registry, version and carrier-collision data, with every caption passed in as
  a parameter. That single move satisfies four constraints at once: the carrier still ships zero Defs, zero
  keys and zero `1.6/Languages/`; the strings problem disappears because the mounting consumer owns the
  translation keys, which is exactly what the identity rule says the string's owner should do; the census
  reaches a real window in a real game session, which is what the evidence was needed for; and no new
  process-wide state is added, because the spec is built on demand.
  **Two rules with it.** *Our own demo is not consumption* (now in `AGENTS.md`): the factory renders a kind,
  measures it and recovers it under the real font, and proves nothing about whether the kind should exist, so
  it can never raise the validated-surface count or serve as promotion provenance. And the census section is
  **metadata only — never instantiate a foreign kind inside a page the carrier built**, because that runs a
  consumer's code under our recovery banner and inherits its failures; report scope, kind, allowed attributes
  and declared label set, capped and totalled the way `UiFitAudit.MaxReports` does it. Printing a consumer's
  scope or kind string at runtime is not a neutrality breach — the scan governs this repository's source, not
  observed data on screen.
  **The cost of this shape, stated so it is not discovered later:** an unmounted factory is precisely the
  speculative surface this library's own protocol refuses to keep, so it must ship with a mount, not before
  one. The mount that already exists is the consumer's diagnostics panel, whose migration onto
  `UiWindowHost` + `UiHost` is in flight on its side — which makes the self-check page round-4 material: a
  cited consumer surface that wants the thing, rather than a library gift nobody asked for. Until that lands,
  the harness is the only mount, and the harness cannot show real glyphs, so §1's geometric questions stay
  open no matter how good the factory is.

## What was verified, and how

- **RimWorld 1.6 UI surface**, read from `Krafs.Rimworld.Ref 1.6.4871` with dnlib (that package mirrors
  the game's `Data/Managed`, so the presence of an assembly there means it ships with the game):
  `Assembly-CSharp` has 16,158 types, 32 declaring `OnGUI`, 76 `Verse.Window` subclasses, 116
  `DoWindowContents` overrides, 123 `Dialog_*`, 261 `*FloatMenu*` types, and `Verse.Widgets` with 174
  methods / 152 public static. `Verse.Window` carries a `UnityEngine.GUI/WindowFunction` field, i.e. the
  game's window manager is an adapter over IMGUI's `GUI.Window`. It references `IMGUIModule` and
  `TextRenderingModule`, never `UnityEngine.UI` or `UIModule`.
- **The game has no style layer to inherit, measured from the same 1.6.4871 reference assembly (2026-09-10).**
  `Assembly-CSharp` has zero hits for `WidgetDef`, `WidgetAppearanceDef`, `appearanceDef`, `GUISkin`,
  `StyleSet`, `StyleSheet`, `VisualElement`, `UIElements`, `UXML` and `USS`, and its assembly references are
  exactly six modules (`AssetBundle`, `Audio`, `Core`, `IMGUIModule`, `Physics`, `TextRenderingModule`) — so
  the game uses neither Unity's uGUI nor UI Toolkit (the layer that carries UXML/USS) in its own code, even
  though `UnityEngine.UIElementsModule.dll` **does** ship in `Data/Managed` with a real selector engine
  inside it (`Selector` 35 hits, `StyleSheet` 29, `StyleSet` 39). `GUIStyle` appears once and `GUISkin` not
  at all, i.e. Unity's IMGUI skin mechanism is effectively unused: the game writes appearance at each call
  site through `GUI.color` and `Text.Font`. **This corrects a recollection that mattered** — a
  `WidgetDef`-shaped appearance Def was assumed to exist; it does not in 1.6. So the game's only
  data-driven appearance channel is ordinary Defs plus XML Patch operations, and that channel is closed to us
  by identity (zero Defs in the payload, gate 5 refuses a content directory). Consequence: a stylesheet here
  would not be adopting a host mechanism, it would be inventing a second resolver inside a library whose
  compositor cannot even be layered above IMGUI.
- **`Verse.Window`'s overridable surface, read member-by-member from the same 1.6.4871 reference
  assembly while writing `UiWindowHost` — these four facts each cost a failed run when guessed.** The type
  is `public abstract`; `DoWindowContents(Rect)` is **public abstract** (so `Margin`-style protected
  assumptions do not transfer to it); `Margin` is **protected virtual, getter only** (a `sealed override
  float Margin => 0f` is how the shell takes the content inset away from the game); `InitialSize` is
  **public virtual, getter only**. The constructor is `Window(IWindowDrawing customWindowDrawing = null)`
  — a derived parameterless `base()` compiles onto *that* slot, so a runtime stub that declares only an
  implicit parameterless constructor dies with `MissingMethodException: Void
  Verse.Window..ctor(Verse.IWindowDrawing)`, which is exactly what the first shell-lane run reported.
  And `Close` is `Close(bool doCloseSound = true)`, not `Close()`: same failure class, same discovery.
  `WindowLayer` is `{GameUI, Dialog, SubSuper, Super}`. Read the signatures from the reference assembly
  with `System.Reflection.Metadata` (`PEReader` → `GetMetadataReader`; in PowerShell that is the
  extension method `[System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)`, not an
  instance method) before writing either side of a stub.
- **GitHub's prerelease state is a stored boolean, never parsed from the tag name** (verified via
  `gh api` against the live SqueakyRatkin data and the REST docs, 2026-09-05): `POST /releases`
  defaults `prerelease:false`, and SR's `v0.1.0-rc1`/`v0.3.2-pre1` carry `true` only because their
  workflow set it. `/releases/latest` is "most recent non-prerelease, non-draft, ordered by
  **created_at of the tagged commit**" (SR's later-created `v0.3.2-pre1` did not dethrone `v0.3.0`),
  and returns **404 when only prereleases exist** - which is the normal state during a bare-repo
  rc-only window, not an error. Release assets additionally carry a **server-computed
  `assets[].digest`** (`sha256:<hex>`), verified byte-equal against a local `sha256sum` of SR's own
  zip: the platform can attest to artifact identity, so a self-published hash is not the only line
  of evidence. Consequence encoded in code: `release.yml` sets the flag from the tag dialect AND
  `scripts/verify-release.ps1` re-checks the platform state after publishing (draft/flag/asset-name/
  digest/latest-pointer/tags-without-release), because a manual web-UI release that forgot the flag
  would otherwise promote an rc to "Latest" silently. Exit codes: 0 verified, 1 mismatch, throw =
  tag shape outside the rc scheme.
- Two consequences of the stored-flag fact, both confirmed against docs and the gate logic:
  (a) the web UI's pre-release checkbox is "Optionally … select" - never auto-ticked from the tag
  name - so a hand-clicked rc release defaults to STABLE and steals "Latest"; that is precisely
  the state `verify-release.ps1` step 2 catches. (b) A bad rc is fixed by deleting release **and**
  tag, then re-pushing the same rc number: the ordering gate recomputes maxRc from tags surviving
  on the runner's checkout, so re-cutting rc2 after deleting rc2 passes while rc3-after-rc1 fails.
  Immutable releases (SR's live releases show `immutable:false`, the default) would forbid exactly
  that retag path, so do not enable the setting while rc churn is the workflow.
- **The release zip's digest was a function of the build clock, not the commit** (measured 2026-09-07,
  fixed in `f6347d4`). `Compress-Archive` stamps each entry from the staged file's mtime, and staging
  rewrites those mtimes to now: two packs of one commit gave different SHA-256s over identical content.
  Pinning the tree's mtimes first does not fix it — NTFS bumps a directory's mtime whenever anything
  under it is touched, and the compressor's own traversal re-dirties directories mid-run (measured:
  files held the pinned date, `1.6/` still carried the wall clock). The fix writes timestamps into the
  archive directly (`ZipArchive.CreateEntry` + explicit `LastWriteTime` = the commit's author date,
  sorted enumeration). Verified: two packs 5s apart, `cmp` clean, `1B7D57E0…`. The DLL was never the
  problem — same-path and clone-path Release builds hash identically (`0c62df…`), and the earlier
  "clone inserts CRs" vector is closed by `.gitattributes`. Consequence for the rehearsal: the
  "CI zip SHA-256 matches a local pack of the same commit" check in `TODO.md` §5 is now meaningful;
  before this fix it could only pass by luck of timing.
- **What advancing to 0.4.0 means, and why 0.3.0 is the number that should ship first (reasoned 2026-09-10).**
  All three axes sit at 0.3.0 (`FerriteLibVersion.Api`, `About/About.xml <modVersion>`, csproj
  `VersionPrefix`) while the sole tag is `v0.2.0-rc1`, so **0.3.0 is currently a contract value that no
  release has ever carried** — and US already pins `[0.3.0, 0.4.0)` against a number nobody can install. The
  pending work (leaf atoms plus the three style classes plus the per-surface token restructure) is public
  additions with one breaking restructure, which under the pre-1.0 rule — a minor bump IS breaking, and any
  public addition bumps minor — is exactly 0.4.0 material. Cutting `v0.3.0-rc1` first costs nothing the rc
  discipline does not already handle: the prerelease flag is derived from the tag dialect,
  `verify-release.ps1` re-checks platform state after publishing, and a bad rc is fixed by deleting release
  and tag and re-pushing the same number. The sequencing that does matter runs the other way: **do not let a
  second consumer wire against 0.3.x and then break `UiTheme`** — 0.4.0 is the last minor where the
  per-surface restructure is cheap, and because the API freeze is gated on the second wired consumer, the
  style sweep has to land before NGS or anyone else builds against the shell.
- **v0.3.0-rc1 is cut, and the deterministic-archive claim is proven across machines (2026-09-10).**
  Sequence: `0.3.x` merged into `main` at `6a92331` — the release workflow demands the tag commit be in
  `main`'s history ("Merge to main, then tag"), so a release cannot come from a feature-branch tip — then a
  lightweight tag `v0.3.0-rc1` (matching `v0.2.0-rc1`'s type) was pushed and CI ran the same three steps the
  local rehearsal does, finishing green including its own post-publish platform check. Local
  `verify-release.ps1 -Tag v0.3.0-rc1`: published, `prerelease = True` matching the tag dialect, single
  asset `FerriteLib-v0.3.0-rc1.zip` at 50166 bytes, no stable release so `/releases/latest` 404s, every `v*`
  tag has a release. **The cross-machine proof:** a local `dotnet build -c Release -p:VersionSuffix=`
  followed by `pack-release.ps1` at the tag commit produced `sha256 1609552d…`, byte-identical to the
  platform's server-computed `assets[].digest` — the first time §5's "CI zip SHA-256 matches a local pack of
  the same commit" check has run against a real published asset instead of a rehearsal. An earlier rehearsal
  at the pre-merge tip produced a different size, which is the commit label doing its job rather than drift.
- **A consumer's CI follows the default branch, not the dev line (measured in US's `ci.yml:37-40`,
  2026-09-10).** Its carrier checkout passes `repository: Coahuilite/FerriteLib` with **no `ref:`**, so it
  builds whatever `main` holds. Consequence for the newly opened `0.4.x`: bumping all three axes there
  cannot break US's CI, because `main` still carries the 0.3 contract — but a machine whose sibling
  `../ferritelib` checkout sits on `0.4.x` will make US fail `Require` with a readable report until US
  re-pins to `[0.4.0, 0.5.0)`. That re-pin is a cross-repo write: report it in a round, never edit it from
  this repository.
- **uGUI / UIElements assemblies do ship** (`UnityEngine.UI.dll`, `UnityEngine.UIModule.dll`,
  `Unity.TextMeshPro.dll`, `UnityEngine.UIElementsModule.dll`), so they are referenceable by a mod. The
  constraint that actually matters is compositing, not availability: IMGUI draws above every Canvas.
- **The game's own UI palette** is `Verse.Widgets`' 18 Color fields — five (fill, border) pairs plus
  normal/mouseover/inactive, separator, highlight, range-control text. **No accent slot exists**, and
  vanilla chrome is carried by `ButtonBGAtlas` + mouseover/click variants and nine `AtlasUV_*` 9-slice
  rects. Consequence for theming: the default DarkGold palette is a theme over the game's *content*
  palette (dark neutral, warm highlight, gold objects), not a reproduction of its widget chrome, and the
  token bag currently cannot express the game's per-surface fill/border pairing at all because
  `UiTheme` has one global `Border`.
- **Prerequisite mechanics.** `Verse.ModRequirement` = {packageId, alternativePackageIds, displayName}
  and `ModDependency` adds only download URLs, so no version can be declared. `RimWorld.VersionControl`
  offers `TryParseVersionString` / `IsCompatible` / `VersionFromString`; `ModMetaData.ModVersion` is
  readable at runtime; `Verse.ModLister.GetModWithIdentifier` / `GetActiveModWithIdentifier` enumerate
  mods. `Verse.ModAssemblyHandler` holds `List<Assembly> loadedAssemblies` and a
  `globalResolverIsSet` flag, which is why duplicate carriers resolve by load order silently.
- **NuGet behaviour, measured with a throwaway probe** (not asserted from memory): a package republished
  under the same version is NOT re-fetched — the consumer's global folder kept the first copy after
  `obj`/`bin` deletion and a fresh `dotnet restore`. A `1.0.0-dev+<sha>` version is worse than useless:
  the nuspec keeps the metadata, the filename and the resolved cache path both drop it, so every hash
  shares one cache entry. Only a prerelease label (`1.0.0-dev.<sha>`) produces a distinct package, which
  then requires a floating version, which the consumer gates defeat by running with restore disabled.
  Hence sibling relative paths.
- **net472 reference-assembly traps** hit while writing this repo's own code, all compile-verified:
  `string.IsNullOrEmpty` carries no `[NotNullWhen(false)]` (CS8602 on the ternary that returns it);
  `string.Split(char, StringSplitOptions)` is advertised but throws `MissingMethodException` at
  runtime; `string.Contains(string, StringComparison)` likewise does not exist at runtime. **How the first one hides
   from a search (measured 2026-09-11):** `text.Split(',')` binds to `Split(char, StringSplitOptions)`
   through that overload's default argument, so a grep for the enum name finds only the call sites that pass
   it explicitly and misses the one-argument form entirely; search `.Split(` and read the argument, or better
   let a lane run the path -- the harness caught this one while two independent greps did not.
   **The measured shape list is longer than the four this repo had recorded (probe, 2026-09-11):**
   `Split(char)`, `Split(char, StringSplitOptions)`, `Contains(char)`, `Contains(string, StringComparison)`,
   `StartsWith(char)`, `EndsWith(char)`, `Replace(string, string, StringComparison)`, `string.Join(char, string[])`,
   argument-less `TrimStart()`/`TrimEnd()` and `Path.GetRelativePath(string, string)`. Arrays are present and
   safe, so the rule of thumb is the *argument shape*: `Split(new[]{','})` is fine, `Split(',')` is not.
   One of them is worse than the rest because it does not even raise the expected type: `EndsWith(char)`
   throws **MethodAccessException**, so a guard that filters by exception type would miss it. The fix for the
   class is a static scan of argument shapes with positive and negative controls, not an exception-type filter. Two more
  found writing the containment lane, same class and same asymmetry (compile green, runtime red):
  **`Path.GetRelativePath(string, string)`** and **`string.TrimStart()`** with no arguments. The scan
  now walks leading whitespace by hand and computes the relative path itself. The rule that generalises
  these: a member the reference assembly advertises is a *claim*, and the runtime this harness actually
  executes against is `net472`, so only a run proves it. **Recurred 2026-09-10 while writing the API-tier
  lane**, which is the strongest argument for the bullet existing: the trap was already written down, and
  the code still reached for the advertised overload until the run refused it.

## Cross-repo couplings that no gate in this repo can see

- **The consumer's harness compiles this repo's stub projects.**
  `Coahuilite/UniversalSqueaker@09366f8:tools/UniversalSqueakerKernelHostTests/UniversalSqueakerKernelHostTests.csproj`
  points `FerriteLibHarness` at a sibling checkout of this repo and, in a post-build target, runs
  `dotnet build` on all four projects under `tools/FerriteLib.UiKit.Tests/Stubs/` and copies
  their output from `bin/stubs/<name>/`. So the Stubs tree - directory names, project names, assembly
  names (`Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`, ...) and that `bin/stubs/` output shape -
  is a de-facto published surface. Renaming or relocating any of it breaks the consumer's gate 13 while
  **all seven gates in this repo stay green**, because nothing here builds or references the sibling.
  Same shape one level up: US's gate 6 asserts, through its own sibling path, that this repo's payload
  exists at `1.6/Assemblies/FerriteLib.UiKit.dll`, so a broken build here goes red over there first.
  Evidence upgraded 2026-09-07 from source-level to **runner-proven**: US's `ci.yml` checks this repo out
  by canonical name and stages the WHOLE tree at the sibling path - its first run died on gate 13 with
  DLL-only staging while gates 1-12 passed, proving the stub sources and `LICENSE` are as load-bearing
  as the payload. The checkout is deliberately unpinned (tracks this repo's default branch, so lib-side
  breakage goes red in US's CI early); the release-body link is the pinned half - see `TODO.md` §5. That
  channel is **two-directional, and only the hostile direction was ever written down**: an unpinned
  default branch also carries every capability we merge but do not publish, which is how round 3's
  `Width="Auto"`/`Breakpoint` reached US's CI while no 0.3.0 asset exists. Consequence: "not released
  yet" is never a reason a surface on `main` is unusable, so the rc window's freeze discipline has to be
  enforced at the tag and not at the branch.
  **Round 1 grew that published surface twice, in both directions.** `VerseStubs` gained
  `Verse.Window`/`Verse.WindowLayer`/`Verse.IWindowDrawing` so `UiWindowHost` is drivable at all (P2
  condition b), and `UiWidgetRegistry.Clear` went **internal** (item D), so a consumer harness that had
  ever called it would stop compiling against the payload while every gate here stayed green. Neither is
  visible to a gate in this repo; both are exactly the shape `TODO.md` §3's "give the harness `Stubs/**`
  a local guard" item exists to catch, and that item is now load-bearing rather than tidy.
- **FL→US round 2 (2026-09-07) — CLOSED, and it started as a confession.** Gathering
  packaging evidence to offer the consumer, FL's own `pack-dev` was found asserting `build=dev` over a
  Release-configured assembly — the rule under S1 below was broken here first (fixed `f2f4dd0`). Six items
  are on US, all cited to `file:line`, none edited: S1 measure the payload's configuration instead of
  trusting a caller-supplied label (observable in US because `US_DEV` gates `SqueakLog`'s `Auto` dev-logging
  and the footer revision, so a mis-staged dev folder loses the diagnostics an in-game pass reads);
  S2 `stage-package.ps1`'s `-CreateZip` archives the stage dir's *contents*, which `release.yml:128-131`
  documents as a hazard and works around in the caller while `pack-dev` still passes it unconditionally;
  S3 five separate places guard the one fact that Workshop identity must not ship, where copying
  `About.xml` as a file instead of `About/` as a directory removes the need for all five; S4 two archive
  writers with two naming schemes and a CI digest no second run reproduces; S5 US's carrier "gate" only
  tests existence — `Invoke-Check`'s second parameter is a display string — and FL's S1 fix is what makes
  that visible, since the sibling path now legitimately holds Dev bytes; S6 `US_STEAM` gates two source
  sites that no configuration, workflow, or script in US defines. Closed 2026-09-08: US executed
  same-day (`1a8dd51`), re-reviewed by fresh reproduction rather than self-report (negative-control
  refusal, byte-identical double-pack digest, dev-carrier gate-red-then-green, final 14/14), and FL
  verified the disposition by running US's gate 14 read-only — zero raw hovers, exactly two
  rule-backed exemptions, scanner self-test armed.
- **US→FL round 3 (2026-09-08, filed as "round 2" and renumbered — the round counter is now explicitly
  global across directions, next-unused at filing, later filer yields) is CLOSED, implemented on 0.3.x
  2026-09-09.** The first consumer-cited engine shortfall: N1/N2 accepted as one package —
  `Width="Auto"` resolves to text-natural width via `ITextMetrics.MeasureWidth` (the seam that has always
  said "content-driven column widths need a width budget" while the engine never called it), capped after
  fixed siblings, clamped, equal-split fallback intact; plus one container-level `Breakpoint` — together
  closing the "有限 responsive vocabulary" deliverable
  (`Coahuilite/UniversalSqueaker@09366f8:docs/uikit-rebuild/02-brownfield-cutover-matrix-zh.md:44`) and its
  acceptance row (`…:docs/uikit-rebuild/04-verification-and-acceptance-zh.md:105`, same repository and
  revision) that
  the rebuild contract shipped as a promise and not as code. N3 verdict (b): layered editing stays
  consumer-side; `input/stepper-slider`'s reshape is exactly the N1 mechanism proving itself — its
  `LabelWidth = 80f` (the library-side specimen of the complaint) is now measured. Recorded with approval:
  this is the filing template — both-sides-recomputable provenance (the 4 px degenerate interval
  recomputed and confirmed), boundary-first, explicit not-asked list, consumer's own unblocked plan.
  **The maintainer refiled the package from 0.4.0 to 0.3.0 (2026-09-09): 0.3.0 had never been published,
  so additions before first publication cost no contract axis** — the freeze argument ("don't move the
  goalpost again") applied to a release already visible to strangers; an rc that never shipped has no
  goalpost to move. US's pin `[0.3.0, 0.4.0)` already covers it. US's own docs still say "0.4.0 package"
  (its TODO lines pinning the threshold ban and the reopen note to 0.4.0) — report, never edit; the
  reclassification is in FL's buffer annex for its next session.
  Implementation deviations, each forced by evidence the review text did not carry: (1) the width clamps
  are **`MinWidth`/`MaxWidth`, not `Min`/`Max`** — StepperSlider's schema already owns `Min`/`Max` as its
  slider value range, and the control named in N1's own provenance would have silently received
  value-range numbers as width clamps; (2) `Width`/`MinWidth`/`MaxWidth`/`NarrowHidden` joined the
  common widget attributes because the engine read `Width` on children while **no core kind's schema
  listed it** — the N1 defect had a library-side twin: a manifest could not size a stepper-slider column
  at all; (3) `Narrow`/`NarrowCols`/`NarrowHidden` are rejected without a governing `Breakpoint`
  (self/parent) because a narrow-state attribute under no threshold is the silent no-op the creation
  contract exists to stop. SCHEDULED→CLOSED 2026-09-09: package implemented and lane-proven — **289
  assertion results at run time** (re-derive: `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release
  --nologo | grep -c '^  ok:'`; distinct from the *named lanes*, which is a source count — and both move
  with every lane, which is the point of recording the predicate: a count without one is not a measurement),
  including the narrow→wide re-arrange on one engine, the
  CJK-vs-Latin glyph positive control (a control on the stub's model, see "Text fit"), and seven
  creation-time refusals; permanent record is this bullet plus the manifest contract
  doc; US's migration `7777cbe` and the maintainer's trial decision remain the only gates on the ship.
- **The Store build of PowerShell ships a trimmed `System.Reflection.Metadata`: `PEReader` has no
  `GetMetadataReader` (measured 2026-09-07, `Microsoft.PowerShell_7.6.5` Appx).** Any packaging or gate
  code that reads assembly attributes must therefore either go through `Assembly.LoadFile` in a **child
  process** — FL's `Get-AssemblyConfiguration` does, because the payload is copied moments later in the
  same session and a loaded handle would survive it — or accept that the check works only on hosts where
  the metadata reader exists. A gate that passes on the maintainer's machine and throws on a
  contributor's is worse than no gate; this is the same failure class as a vacuous enumeration.

## Consumer coverage, measured 2026-09-04

- **The vocabulary has no leaf atoms, and that is the measured cause of both the unconsumed kinds and the
  consumer's re-wrapping (counted 2026-09-10).** This library registers 7 kinds — re-derive with
  `grep -rhoE 'const string Kind = "[^"]+"' Source | sort -u` — and not one of them is a leaf a page author
  would reach for first: there is no plain text, no button, no rule, no spacer, no badge, no icon. What the
  wired consumer does instead is measurable: `grep -rhoE "UiNative\\.[A-Za-z]+" --include=*.cs
  <consumer>/Source | sort | uniq -c` reports `Button` **14**, `IsMouseOver` 5, `ClampValue` 5,
  `NumberField` 4, `Slider` 3, `TextField` 2 — i.e. the consumer builds its own controls out of the funnel's
  primitives because the vocabulary offers no atoms, and it registers `us/*` kinds for the results (23
  distinct kind strings today; re-derive against its published tree, this number is not ours to keep).
  Consequences, all source-level evidence with no new game run: manifest-referenced library kinds number
  **one** (`chrome/banner`) out of the whole vocabulary; two more (`input/dropdown`, `chart/line`) are used by
  instantiating the widget class as a composition part, which is the bypass the tree-membership rule counts
  even when it stays inside the tree; and four kinds are referenced nowhere in the consumer's source. The
  honest coverage statement is still "3 of 7 kinds touched", and the reason is structural, not
  marketing: a page author who wants a label and a button is offered neither.
- **Which is why "start from atoms" is the right instinct — and why the surviving composites keep their
  names.** The `AGENTS.md` rule ("What earns a kind") is ownership-based, not atomicity-based, and the survey
  in the next bullet is what pinned that down after an earlier draft of the rule said the opposite. Under the
  ownership test the seven split: `chart/line`, `input/dropdown`, `input/stepper-slider` and `input/mode-row`
  own state or geometry a manifest cannot express; `state/empty` and `chrome/banner` own one capability
  between them — a wrapped string whose `Measure` reserves the wrapped height — which is the missing text atom
  named twice; `section/header` owns nothing the container's existing `Title`/`TitleKey` band does not already
  give, and that duplication (not compositeness) is the only reason a name is up for retirement here. So the
  0.4.x sequence is additive-then-re-filling: introduce the leaf set (`core/label`, `core/text` carrying the
  wrap measure, `core/button`, `core/rule`, `core/slider`, `core/number-field`), prove each in the self-check
  spec under real glyphs, then rebuild the composites' innards as explicit compositions over those atoms.
- **Atoms-first is industry-normal; "composites should not be registered vocabulary" is not (surveyed
  2026-09-10; ten targets, official documentation opened in-session, not blogs).** The claim "a UI kit's core
  is its atoms" is true in all ten. The claim "therefore composites should be functions, not names" is false
  in all ten, and four of them actively instruct users to add named composite types to the vocabulary.
  - WPF documents the atom taxonomy (`ContentControl`, `ItemsControl`, `Panel`, `Decorator`, `TextBlock`) and
    keeps composites named, restyling rather than dissolving them: "Classes that inherit from the Control
    class contain a ControlTemplate, which allows the consumer of a control to radically change the control's
    appearance without having to create a new subclass."
    (learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/ and .../wpf/controls/wpf-content-model)
  - .NET MAUI: "The main control groups used to create the user interface of a .NET MAUI app are pages,
    layouts, and views," with `CollectionView`/`DatePicker`/`TwoPaneView` as named rows and `ContentView` as
    the named type for a reusable custom control (learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/).
  - Unity UIElements registers a custom control into the document vocabulary through `[UxmlElement]` on a
    `VisualElement` subclass, and prices exactly the artifacts our rule prices — attribute schema ("Keep UXML
    attributes primitive"), namespace prefix, library visibility. Composition appears once, as a tip ("you
    might achieve the same outcomes … Assemble your UI from existing elements"), not as a rule
    (docs.unity3d.com/6000.6/Documentation/Manual/UIE-create-custom-controls.html).
  - Android: "Android provides a straightforward XML vocabulary that corresponds to the View classes and
    subclasses," and the compound-control recipe "brings together a number of more atomic controls or views
    into a logical group of items that can be treated as a single thing" — a new *named* thing
    (developer.android.com/develop/ui/views/layout/custom-views/custom-components).
  - Jetpack Compose is the strongest industry case for names-as-functions ("You can write your own composable
    function to combine these layouts into a more elaborate layout that suits your app"), and even there the
    same page uses named composites that Material ships (`Card`, `Scaffold`)
    (developer.android.com/develop/ui/compose/layouts/basics).
  - Flutter: "You create a layout by composing widgets to build more complex widgets," and the architecture
    doc makes the two tiers coexist by design — Material and Cupertino are named control sets built on the
    composition primitives (docs.flutter.dev/ui/layout and docs.flutter.dev/resources/architectural-overview).
  - SwiftUI: "You compose custom views out of built-in views that SwiftUI provides, plus other composite views
    that you've already defined," and the composed result is declared as a named `View` struct
    (developer.apple.com/documentation/swiftui/declaring-a-custom-view).
  - Godot keeps `Label`/`Button`/`Panel`/`HBoxContainer` named and runs a whole tutorial on adding your own
    named `Control` subclass ("Creating your own custom controls that act just the way you want them to is an
    obsession of almost every GUI programmer"), covering `_draw`, `_gui_input`, `_get_minimum_size` and theme
    notifications (docs.godotengine.org/en/latest/tutorials/ui/custom_gui_controls.html).
  - RmlUi is the atom-minimalist and the nearest miss on the second claim — "Very few custom elements are
    required as most of the power in RmlUi comes from styling elements with RCSS to produce the desired
    layout" — and that same library's composites (the `input` family, `tabset`/`tab`, `datagrid`) are
    registered tags bound by name through `RegisterElementInstancer()`
    (mikke89.github.io/RmlUiDoc/pages/rml/elements.html and .../pages/cpp_manual/custom_elements.html).
  - Qz-UILib (Minecraft, the founding spec's own author, closest to us in scale and motivation) exposes both
    tiers: a scene/flex/portal/virtual-grid structure layer plus roughly twenty named composite controls
    assembled through the Java API. It deleted its HTML-like declarative stack wholesale in a breaking major
    and **still** kept the composites as named classes, with a hand-maintained LTS stable-API list as the
    contract (github.com/QuanhuZeYu/Qz-UILib, `docs/使用文档/01-入门/项目定位与能力边界.md` at branch `4.0`).
  **What does not transfer, and why the correction is narrower than the headline:** every toolkit above keeps
  named composites *alongside* a style or template layer that absorbs the long tail (WPF `ControlTemplate`,
  Unity USS, RmlUi RCSS, Compose functions, SwiftUI view bodies). This library has no style layer — the
  manifest is the only data surface — so our kind list carries pressure theirs do not, and that is the local
  reason the "composites become recipes" draft looked right. The draft was still wrong: with no style layer,
  dissolving composites into C# deletes the only place a mod author can reach them without compiling, which is
  exactly the hole the consumer fell into. Evidence class: documentation read this session; no game run, no
  code change beyond this correction.
- **The appearance layer exists in C# and is unreachable from data (census 2026-09-10).** Three measurements,
  all from this repository's source. (1) Across the seven kinds' allowed-attribute whitelists the only visual
  knobs are `Height`, `ButtonWidth`, `FieldWidth`, `Tab` and `Hidden` — no `Tone`, no `Style`, no `Variant`,
  no color, no font, no padding anywhere in the manifest vocabulary, so a page author cannot express "this
  banner is a warning" without writing code. (2) `UiTheme` is a token bag of **colors and one font**: no
  geometry tokens (padding, spacing, gap, radius), and the seven widget files hold 275 numeric-literal
  tokens (`grep -rhno` over the files — a token count, not a per-constant audit), so density is not
  adjustable at any granularity. (3) The semantic style layer that does exist is code-side: `UiThemeDraw`
  exposes 14 named outlets (`Surface`, `SectionBand`, `AccentRail`, `FocusRail`,
  `StatusTreatment`, `RecoveryBand`, `StatusBadge`, …) and `UiStatusTone` is a six-value enum (`Neutral`,
  `Active`, `Success`, `Warning`, `Danger`, `Disabled`). The roles are therefore already named centrally;
  what is missing is a **binding from a manifest attribute to an existing role**, not a rule engine.
  Evidence against building more than that today: the wired consumer never re-tints — it takes
  `UiTheme.DarkGold` as-is (`Mod.cs:154`; both diagnostics windows hold a
  `static readonly UiTheme WindowTheme = UiTheme.DarkGold`) and its source has zero `GUI.color`, zero
  `new Color(` and zero `ColorDef.` hits — the measured pressure is in the atom axis, not the appearance
  axis. The cheap shape, once a citation exists: add `Tone` to the atom schemas (closed enum, already named,
  no new semantics) and move the geometry constants into `UiTheme` so `ButtonWidth`/`FieldWidth` stop being
  per-kind vocabulary. The expensive shape (selectors, cascade, specificity, a parsed style file) has no
  provenance, has no host counterpart to interoperate with, and collides with measurement order: the fit
  audit must know the resolved font before `Measure` (`ITextMetrics`, the `Breakpoint` rule).
- **The divergence from the game's look is total on the chrome side and honest on the internals side (census
  2026-09-10, raised by the maintainer's question "our buttons already look different — is that style?").**
  Yes, it is style: appearance, not behaviour. Where it lives, measured: the kernel has **zero** texture
  references (no `Texture2D`, no `TexUI`, no `ContentFinderProxy`), every surface is a flat
  `UiThemeDraw.Solid` → `Verse.Widgets.DrawBoxSolid` fill plus 1px solid rules, and `UiNative.Button` is
  `VerseWidgets.ButtonInvisible` — the chrome-free hit-test. Of the game's chrome-producing helpers
  (`ButtonText` 9 string hits, `DrawTab` 37, `DrawWindow` 6, `TexUI` present) in 1.6.4871 the kernel calls
  none. The one inherited thing is the typeface: `UiKitFonts.ToGameFont` maps our three sizes onto
  `GameFont.Tiny/Small/Medium`. **Two atoms still render vanilla pixels inside our chrome**:
  `UiNative.TextField` → `VerseWidgets.TextField` and `UiNative.Slider` → `VerseWidgets.HorizontalSlider`, so
  a text field's caret and a stepper-slider's track are the game's while their surroundings are ours.
  Consequence for the style-layer question: because we own every state appearance — vanilla gets
  hover/pressed/disabled free from its texture button, we draw `Selected`/`Raised`/`AccentGold` ourselves —
  the attribute-to-role binding is worth more here than in a toolkit that inherits a host look. It is still
  not a reason to build a resolver: a selector engine in front of these outlets would delete no drawing code
  and only add matching.
- **The tone vocabulary is half-wired, and one pair of tokens is a duplicate (measured 2026-09-10).** Across
  the seven widget files the only theme tokens ever selected are `Selected`, `Raised`, `Border`,
  `AccentGold` and `HoverPoint`; `Warning`, `Danger`, `Success`, `TextSecondary` and `TextDisabled` are picked
  by no library widget, and the state mapping is `selected ? Selected : Raised` (dropdown, mode-row) plus
  `isHovered ? HoverPoint : AccentGold` (chart). The semantic outlets `UiThemeDraw.StatusTreatment` and
  `StatusBadge` are exercised **only by the consumer** (`UsNavWidget.cs:104`, `UsDiagnosticsWidgets.cs:317`),
  using `Active`/`Neutral`/`Success`. And `UiTheme.Warning` and `UiTheme.Danger` are the same RGB
  (`0.38, 0.14, 0.11`) under two names with no users — unproven surface inside the token bag, which this
  library's own rule treats as debt rather than inventory: either a cited consumer shapes them apart or one
  of the two goes. Evidence: source-level counts in both repositories; no game run.
- **The scattering is real, and its cause is an unqueryable table, not a missing stylesheet (measured
  2026-09-10).** Three vectors, named at the line. (1) `DropdownWidget.DrawField:127` and
  `InputModeRowWidget.DrawOption:114` hold the same two-token mapping verbatim — `selected ? Selected :
  Raised` plus `selected ? AccentGold : Border` — which is exactly what `UiThemeDraw.StatusTreatment`
  already computes for `Active` and `Neutral` (its switch: `Active` ⇒ Selected + AccentGold, default ⇒
  Raised + Border). (2) The tone-to-text mapping lives inside `StatusBadge:204-213` as a private `switch`,
  so the two widgets re-derive `selected ? TextOnGold : TextPrimary` by hand; the tables disagree on one
  entry deliberately (a badge's neutral text is `TextSecondary`, a field's is `TextPrimary`), which is the
  first concrete evidence that **tone alone does not determine text colour** — prominence is a second axis,
  and any role vocabulary must carry it or it will keep being re-invented per widget. (3) The same text
  inset is spelled two ways: `DropdownWidget` uses a named `TextPadding` constant, `InputModeRowWidget:117`
  hardcodes `6f` and `12f`; across the seven widget files the geometry constants run 40 × `1f` (the
  hairline), 7 × `2f` (the rail width), 5 × `6f`, 3 × `28f`, with no token source anywhere. **Why a
  stylesheet would not have prevented this:** a component that fills a rect imperatively needs the resolved
  value *back*, and CSS-shaped systems do not hand it the rules — they hand it a resolved view. Unity's
  UIElements makes that view first-class: `IResolvedStyle` appears 157 times in the metadata of
  `UnityEngine.UIElementsModule.dll` in 1.6.4871, with `resolvedStyle` on the element (`get_resolvedStyle`)
  and `resolvedValues` beside it, while `Specificity` appears 6 times and `StyleResolver`/`ComputedStyles`
  not at all. That module ships in `Data/Managed` and the game never references it. So the prerequisite is a
  queryable, complete treatment table shared by the outlets and the widgets; whether a data-side `Tone=`
  attribute ever sits on top of it stays the separate, citation-gated question.
- **Two different things get called "resolution", and keeping them apart is the whole argument (2026-09-10,
  written after the maintainer asked why resolution must precede the table).** (A) *Style resolution* is the
  computation from a rule set plus an element's place in the tree to that element's effective values —
  matching, cascade, inheritance — and **its output is the per-element resolved-value table**. Nothing draws
  from rules directly, so "resolution before table" is not a sequencing preference: the table simply *is* the
  output. (B) *Pre-measure determinacy* is a separate requirement that concrete numbers exist before text is
  measured, which is why any future style surface must settle before `Measure` (`ITextMetrics`, the
  `Breakpoint` rule). FerriteLib has no rule set and needs no selectors, so its table key is already
  explicit — tone plus prominence — and what it lacks is the output side of (A): turning the two switches
  that already exist into values somebody can read. Consequence for sequencing: build the table first. A
  data-side `Tone=` is only a new input to it, whereas shipping `Tone=` first leaves every widget copying
  the switch again, now with an XML value in hand.
- **Four layers, and which two we actually lack (map set down 2026-09-10; it is what dissolves "does a
  cascade table fight the treatment table").** ① *Rule source* — who may specify an element's appearance: a
  CSS is one kind, an explicit `tone` argument is another. ② *Adjudication* — how competing sources plus
  live state resolve to one answer; cascade and specificity live **here and only here**. ③ *The resolved
  value store* — the answer, kept queryable: this is the "table" this session keeps naming, and Unity's
  `IResolvedStyle` sits in this layer too. ④ *Painting outlets* — values become pixels: `UiThemeDraw`'s 14
  outlets, complete enough that no widget touches IMGUI outside the funnel. The library has ④, has a thin ①
  (code arguments only), and is missing ③ outright while holding only the state-mapping fragment of ②
  implicitly. **Why the maintainer's instinct read the gap as "we have what to draw but not how":** with ③
  absent, adjudication parasitises ④ — `StatusTreatment` and `StatusBadge` each decide values inside a
  switch and then throw them away — so the painting layer looks incomplete when it is in fact over-scoped.
  Naming the layers fixes the confusion; naming ③ as the missing one fixes the code.
- **The second rule source already exists and is blocked by a missing query, not by a missing engine
  (measured 2026-09-10).** `IUiBindings` declares `BindReadOnly<T>` and the wired consumer uses it six times
  (`UsDiagnosticsHost.cs:70-75`), but the interface exposes no writability read — its whole read side is
  `Get`, `TryGet`, `GetOptions`, `Invoke` and the four `Validate*` methods. No widget can ask whether its
  value is writable, which is why `UiStatusTone.Disabled` has **zero producers**: its only two appearances in
  the assembly are inside `UiThemeDraw`'s own switches. Consequence for the cascade question, stated as the
  trigger rather than a preference: the first time two sources address the same property of one element will
  be a read-only element whose data side says `Tone="danger"`, and that is settled by one written precedence
  rule (state beats author). Cascade machinery becomes a requirement only once competing sources outnumber
  what a written rule can carry; today the count is one.
- **What a third consumer could do today, measured rather than assumed (2026-09-10; the §5
  invited-vs-unsupported call was ruled invited the same day, and this census is written for what that
  ruling now owes).** Appearance is already customisable at **window** granularity with zero
  library change: `UiWindowHost.Theme` is an abstract property, `UiTheme` exposes **20 settable colours plus
  one settable font**, and `DarkGold` hands out a fresh instance per call so two mods re-tinting "the
  default" cannot repaint each other. A third mod could therefore ship a wholly different palette today, and
  could register its own kinds through the public registry and draw as it likes — ladder rung 1. What it
  could not do is assign a role per element from data (`Tone=`) or adjust density at all (no geometry
  tokens). Note also that `DefaultFont` is geometry-bearing — it feeds text measurement — so that knob moves
  layout with it and is covered by `ITextMetrics` and the fit audit; it is not a cosmetic setting. Finally,
  the binding constraints on a third consumer are not version numbers: they are the single-carrier invariant
  (only this mod ships the DLL, and `FerriteLibVersion.Require`'s collision report is the only detector) and
  the invitation itself, which turns the pre-stable debt list in §3/§4 into a schedule rather than an
  option. A third wired consumer does not get blocked by the API freeze — it is the event the freeze waits for.
- **What a cascading style capability actually requires here, decomposed 2026-09-10.** The §5 INVITED
  ruling turns the breaking parts into pre-stable debt rather than options, so the work separates into five
  pieces, and the surveyed systems say the first three are the whole useful part. (1) *The resolved-value
  store* — one table keyed by `(role, prominence[, writability])` returning fill/border/text, immutable per
  element and session-scoped: session-scoped because the promotion gate forbids new process-wide mutable
  statics, immutable because `Measure` must see the same font the draw will use. (2) *The token shape fix*
  already filed in §4 — per-surface `(fill, border)` pairs, which is also what makes the game's own look
  representable at all — plus geometry tokens, since a role that cannot vary spacing has bought very little.
  (3) *Role vocabulary on the atoms* — `Tone` and `Emphasis` as attributes, which is why the leaf atoms are a
  prerequisite: a role has nothing to attach to while the manifest offers no text or button. (4) *Scope
  carriers* — page- and container-level style with a **written** precedence chain, state > element >
  container > page > theme > default. This is the only piece where "cascade" earns its keep and the only one
  needing inheritance, and inheritance stays limited to font and emphasis because arbitrary inheritance is
  what makes a resolved value unauditable. (5) *The writability read* on `IUiBindings`, which finally
  produces `Disabled`. Explicit non-goals, each with its reason: no selector matching and no specificity
  arithmetic, because positional precedence covers the real cases and stays auditable; no `@media`, because
  the cited narrower mechanism is the shipped `Breakpoint`; no separate stylesheet file, because the game has
  no style layer to interoperate with, so a new file kind buys a loader, a path contract and a second
  validation entry point and nothing else; no runtime style mutation, because live state is already resolved
  per frame in the draw and that is what keeps it harness-drivable. Role names take the §5 deprecation clause
  like any other attribute: redirect for at least one minor, removal only at a minor boundary.
- **The carrier rationale is not in question, and it is not what the §5 ruling was about (restated
  2026-09-10).** This mod exists as a prerequisite that ships the library to other consumers — that is its
  identity, not a cost to justify, and nothing about a style layer changes it. What the invited/unsupported
  call was actually about is narrower and remains the reason the ordering above matters: whether strangers may
  compile against the surface, which decides when the freeze clock starts and therefore how cheap each
  breaking piece still is. Ruled invited on 2026-09-10, so the pre-stable list is now a debt schedule.
- **What a ".css file" actually is, and where each of its jobs already happens here (mapping set down
  2026-09-10, after the maintainer asked what our counterpart to the CSS part is and how it would be used).**
  A stylesheet file carries five separable jobs, and this library already performs four of them somewhere
  else: the *value source* (`:root` custom properties) is `UiTheme`, injected per window through the abstract
  `UiWindowHost.Theme` and carried down the tree by `UiWidgetContext`; the *rules* (`selector { prop: value }`)
  are the hardcoded switches inside the painting outlets — `StatusTreatment`'s tone switch and `StatusBadge`'s
  text switch — which is the same mechanism with an enum as the selector and nothing reachable from outside;
  *pseudo-classes* (`:hover`, `:disabled`) are live session state read in the draw (`selected ? … : …`,
  `isHovered ? …`); *media queries* are the shipped `Breakpoint`, deliberately narrower. Only one job is
  genuinely absent: the *author's class attribute* (`class="danger"`), which is exactly what the planned
  `Tone`/`Emphasis` pair is — and it is a manifest **attribute**, not a document. Consequence for usage:
  nothing scans a styles directory, there is no second loader, and the payload file set is unchanged, so gate
  5 and the packaging probe stay as they are. If a rule-set document is ever wanted, the shape that keeps
  those properties is a `<Styles>` section inside the existing manifest, matching on kind and role names only
  with no descendant selectors, and it is deferred not because it is hard but because the number of competing
  sources it would have to arbitrate between is one.
- **Failure granularity, and what co-locating style in the manifest really costs (read from the shell and the
  guard, 2026-09-10, answering the maintainer's "写错了整个炸了").** Four buckets, not one wall. (1) A widget
  throwing in Draw or Measure is tripped **per element id** by `UiSessionGuard` and paints a `RecoveryBand`
  in that element's rect while the rest of the page continues. (2) A creation-time contract failure — unknown
  element name, unknown attribute, missing required attribute, a narrow-state attribute with no governing
  `Breakpoint` — takes the **page** down, not the window: `UiWindowHost.DoWindowContents` wraps `CreateHost()`
  and `DrawFrame()` in one try, keeps the exception, and trips `UiWindowNotice.PageUnavailable` on the
  **next** frame — deliberately, because the throwing pass already claimed layout state, and the code comment
  forbids "improving" that into a synchronous retry. (3) An unmet version contract shows
  `UiWindowNotice.Prerequisite` from `Require`'s readable report. (4) A duplicate carrier is
  `FerriteLibVersion.Require`'s collision report. **So the accurate cost of putting style in the manifest is
  not "it blows up" — it is that style errors land in bucket 2 (page-fatal) instead of bucket 1
  (element-contained), because creation-time validation is page-scoped by design. **Ruled by the maintainer
  on 2026-09-10: appearance must never take the page down — the same way a broken stylesheet leaves a web
  document still rendering, only badly dressed. So stage 3 fails closed on structure, because a mis-named
  element means the page means something else, and soft on appearance values: an unknown `Tone` falls back to
  the default treatment. Fail-soft must not mean silent — the web's own weakness here is that a dropped
  declaration fails invisibly and is found by a user rather than by its author — so the fallback is logged,
  reported through the fit audit's channel, and exercised in a harness lane.
- **The web analogy corrected: we are not HTML and CSS merged, we are HTML with no author CSS at all
  (2026-09-10).** The manifest carries structure, identity (`Id`, `Kind`, `Tab`, `Hidden`), binding references
  and three geometry attributes (`Height`, `ButtonWidth`, `FieldWidth`); style is in no file — it lives in
  `UiThemeDraw` and the two switches, with the values in a C# bag. In web terms `UiThemeDraw` is the **UA
  stylesheet** and the author layer simply does not exist. Which is why the maintainer's ordering intuition is
  both right and useful: the browser runs HTML parse, CSSOM, **style recalc**, layout, paint, and the planned
  resolve-before-`Measure` pass is exactly the style-recalc slot, existing for the same reason the browser
  puts it there — rules need a tree to attach to, and layout needs resolved numbers. What co-location buys
  here is one loader, one validation entry, an unchanged closed payload file set, and atomicity, since a page
  cannot ship apart from its own role assignments. What it costs is sharing across pages — two pages wanting
  one look copy the attributes, which moves today's duplication from C# into XML — and the fact that every
  appearance knob added to the schema is permanent vocabulary.
- **The requirement, stated as a boundary rather than a feature (maintainer Q&A, 2026-09-10).** "XML 就能
  调用组件、确定布局、改变外观；其他作者用 C# 注册自己的组件." Read as a rule: **the use surface must not
  require a compiler; the extension surface may.** Measured against it, layout already complies and
  appearance does not — the manifest's entire visual vocabulary is `Height`, `ButtonWidth`, `FieldWidth`,
  `Tab`, `Hidden` — so the gap is not "we lack CSS", it is a hole in this library's own stated boundary, and
  that is why closing it needs no provenance citation: the boundary is the justification. Confirmed scope is
  all three classes, not one: a colour scheme selectable per window and per region (the NGS ice-blue case),
  density (row height, padding, font size), and role tags per element. Precedence is nearest-wins — element
  > region > window > library default — because the sources are nested in the tree, so no matching modes and
  no specificity arithmetic are required; the engine CSS exists for is exactly the thing our shape lacks the
  need for. **Inheritance is asymmetric by design: scheme and density inherit, roles do not.** A region
  tagged danger would make every control inside it read as dangerous and destroy the information the tag
  exists to carry, and that is the argument any future request to make `Tone` inherit must answer.
  Cross-page sharing was declined, with a real substitute: reuse rides the extension surface (a registered
  kind), not a shared style document.
- **The style surface is a standalone document with its own loader, not an embedded manifest section — maintainer ruling 2026-09-10, superseding the same day's "no separate style file" non-goal.** The ruling's grounds: an embedded `<Styles>` section keeps structure and appearance tangled in one file, leaves cross-page sharing as copy-paste (the co-location cost that entry itself already recorded), and the "second resolver with no host counterpart" objection was written for an interop-shaped plan rather than for what this assembly is. The chrome is already 100% self-owned — zero texture references, `UiNative.Button` is `ButtonInvisible`, every fill through `UiThemeDraw` — so there is no host style layer to fight, and "the game has no style layer" flips from objection to enabler: the only contract the document must honour is this library's own, exactly as the layout manifest already does. What survives of the old ruling on purpose: no selector matching and no specificity (matching stays kind and role names, nearest-wins); no `@media` (`Breakpoint` is the cited narrower mechanism); no runtime mutation (parsed once at host creation, live state still resolves per frame); nothing scans a styles directory (the consumer hands the path, as with any file it owns); and the failure ladder is already ruled — a malformed or unknown-value style document is appearance-class, so the page renders with defaults, the fallback is logged through the fit audit's channel and exercised in a harness lane. The design move that keeps the old costs out: **one parser, one `<Style>` vocabulary, two text origins** — the standalone document and a manifest `<Styles>` section both feed the same document type, so there is still exactly one validation entry point, not two. New debts the document shape owes: a public document type classified in `docs/api-tiers.md` in the same commit; the precedence-chain slot (the document enters as the page/window-level source — the cited need stage 4 was waiting for); and resolve-before-`Measure` (the document resolves into the value store before the first arrange, in the style-recalc slot). Name collisions checked: `Region`, `Scheme`, `Density` hit nothing in the assembly (measured this session).
- **Provenance correction on the ruling above (maintainer, 2026-09-10): "IMGUI as the drawing
  backend" is a survey finding, not the maintainer's intuition, and the earlier session that recorded
  the style entries failed to note which half of the chrome facts were already on the ledger.** The
  measured half was here all along: zero texture references, `UiNative.Button` = `ButtonInvisible`,
  every fill through `UiThemeDraw` (`MEMORY.md`, "The divergence from the game's look is total on the
  chrome side"). What was missing was the connective claim it supports — that the kernel's chrome is
  100% self-owned chrome over the game's fonts, so a style document has no host stylesheet to
  interoperate with — and that line exists too ("Our look is self-owned chrome", census 2026-09-10).
  The session that proposed the embedded-`<Styles>` compromise then cited the self-ownership as if it
  were fresh evidence and mis-attributed the backend choice to intuition; the maintainer's correction
  is that the backend choice itself was derived from the same survey, and it is this file's job to
  carry attribution, not just conclusions. Lesson filed with it: when a ruling's grounds restate an
  existing ledger line, cite the line — do not re-derive it in the ruling and drop the original.
- **The privacy gate measures accounts, not display names (ruled and implemented 2026-09-10).** `gh api
  user` reports login `Coahuilite`, id `19252128`, name `Fe`, email `null` — so the `Fe` sitting on the
  PR #1 web-merge commit is that account's own GitHub-published display name, which is precisely what
  GitHub's merge button stamps as author, and the committer `GitHub <noreply@github.com>` is platform
  boilerplate. One human, one account, one noreply address: the earlier "three identities, rewrite the
  history" reading was a gate bug, not a leak. The email address is the leak vector, so `privacy-audit.ps1`
  now fails on any non-noreply author or committer address and on more than one distinct noreply account,
  while reporting display-name variance rather than treating it as fatal. Positive control: a real mailbox
  → foreign 1; two accounts → 2; one name across two accounts → 2; this repository → account 1, names 2,
  `PRIVACY AUDIT CLEAN` across 76 revisions. Consequences: no rewrite, no force-push, `v0.3.0-rc1` is
  unblocked on this axis, and the §5 instruction to amend and force-push is superseded by this record.
- **The library ships 7 widget kinds; 3 are reachable from outside, 4 have no consumer at all.** US's two
  Schema=2 manifests reference exactly one library kind, `chrome/banner`. Two more library kinds are used, but
  *not* through a manifest: `UsFilterBarWidget` instantiates `DropdownWidget` and
  `UsAttenuationEditorWidget` instantiates `LineChartWidget` directly as composition parts.
  `section/header`, `state/empty`, `input/mode-row` and `input/stepper-slider` are registered here and appear
  nowhere in the consumer's source — `input/mode-row` most pointedly, because the consumer ships its own.
  Consequence for the freeze and for the invited-vs-unsupported call: "validated against one consumer" is
  narrower than it sounds; those four kinds are unvalidated in a running game, not merely unused, and a third
  party compiling against them would be the first real test of them. Evidence: source-level counts; no game run.
- **`UiThemeDraw.Label` is the single text outlet as of 2026-09-07; it was not before.** The two bypasses
  (`UiLayoutEngine`'s container `Title`/`TitleKey` band and `StepperSliderWidget`'s label and `−`/`+`
  glyphs) were deleted by round-1 item A, so `UiFitAudit.Check` now sees those strings and
  `BeginElement(entry.Path)` has something to attribute. Still a coverage fact, not a fixed bug: no
  overflow on either path has ever been observed, in game or in the harness. Two consequences of routing
  rather than re-implementing: those paths now set `Text.Anchor` explicitly (`MiddleLeft`, the outlet's
  default) where the deleted copies inherited the ambient anchor, and the harness audit tests still drive
  the outlet directly, so they cover the outlet and not the routes into it.
- **`UiWindowHost` owns window chrome; a consumer's window is a subclass, not a re-implementation.**
  Round-1 P2, and the only round-1 item that *shrinks* the escape surface instead of adding capability:
  a consumer forced to write its own `Window` keeps being tempted to write widgets there too, and the
  proven result was a settings window whose close button reached past the kernel. What the shell owns:
  exactly one `UiHost` (built inside the guarded pass, disposed on `PreClose`), the chrome paint (one
  theme, no second palette), the close affordance, and `Margin => 0f` so it insets its own content. What
  it never owns: the window's **size** — `InitialSizePolicy` is a provider whose unset default is the
  game's own `Window.InitialSize`, because freezing the first consumer's screen-fraction clamp would put
  its taste into the freeze surface. Modality (`forcePause`, `absorbInputAroundWindow`,
  `preventCameraMotion`, `layer`) is likewise left to the consumer to set.
  The failure contract is the part that looks wrong and is not: a pass that throws reports once,
  disposes its host and draws **nothing**; the notice appears on the *next* pass, drawn from a clean
  IMGUI state, and is terminal for the instance (no retry, no second host). A synchronous switch would
  repaint the notice inside the pass that already claimed layout for the page. Do not "improve" it.
  Chrome geometry virtuals (title band 56, padding 20, accent rule 3, close 110×30) are the shell's own
  layout, generalised from the first proven implementation and overridable — a different kind of thing
  from window size, and worth keeping distinct when a second consumer disagrees with them.
- **The hover-claim machine is the session's (P3), and its clock is IMGUI passes.** `Session.Frame`
  increments once per `BeginFrame`, i.e. once per IMGUI pass — **not** once per rendered frame. Say the
  unit out loud before sizing a grace window: the settings window opens with `forcePause`, where game
  time freezes and any seconds-based grace would never expire, which is why the consumer's hand-rolled
  machine was already frame-count based and why the proposal's `graceSeconds` was dropped during review.
  `HoverGraceFrames` is a session property the consumer sets and defaults to **0** (the plain per-pass
  clear, which is what the library did before), so no consumer's number became a library default.
  Two rules the tests pin, because both are easy to lose in a rewrite: a claim the boundary *restored*
  does not re-arm the grace window (that is what stops a superseded claim pinning the panel forever),
  and the pass immediately after a live claim is blank by design — a still-hovered widget re-claims
  inside it.
- **`UiThemeDraw.Solid` and `UiThemeDraw.RecoveryBand` joined the visual core in 0.3.0.** `Solid` is the
  library's only fill primitive, and widgets call it instead of `Verse.Widgets.DrawBoxSolid` — that is
  what makes the funnel short enough to gate. `RecoveryBand` is the fixed paint of a tripped element.
  Neither takes a session, so both stay on the visual-core side of the boundary.
- **Backend containment is a gate, not a convention, and the funnel is five files.**
  `KernelContainmentTests` scans `Source/**` for qualified calls to `GUI.` / `GUIUtility` / `Event.current`
  / `Mouse.` / `Text.` / `VerseWidgets` (plus the `Verse.`/`UnityEngine.`-qualified forms and
  `Verse.Widgets` spelled out) and requires each hit to be a name its file is allowed to reach. The list
  after item A is `UiNative.cs`, `UiThemeDraw.cs`, `UiLayoutEngine.cs` (its four structural scopes only),
  `UiSessionGuard.cs`, `VerseFerriteTextMetrics.cs`. Two rules keep the list honest: an allowlisted file
  that no longer reaches one of its granted names is a failure, and `UiWindowHost.cs` is deliberately
  **absent**, which is what makes the shell's "zero raw backend calls" acceptance line enforceable
  (mutation-proved: a planted `Verse.Mouse.IsOver` there reddens the lane with file and name).
  Two corrections against the review's own prose, both measured after A: the sketched list named
  `UiKitFonts.cs`, which has no backend call at all (it maps an enum), and item A's text named three
  violations where its acceptance line implied nine — `Widgets/` also held four `DrawBoxSolid` fills in
  the chart and one in the stepper, and the engine held a five-rect copy of `UiThemeDraw.Surface`. The
  acceptance line was the requirement; the enumeration was not.
- **Recovery belongs to the tree now (item C).** `UiLayoutEngine` wraps every element's Measure and Draw
  through `UiSessionGuard`: a throwing control records into its session slot (one log line per slot, with
  kind, path and stack), restores font/colour, and paints `UiThemeDraw.RecoveryBand` — a warning-toned
  band carrying its layout path, no strings the library does not own. Before this the guard existed and
  nothing in the library called it, so the documented contract was consumer opt-in fiction. Two details
  that cost a cycle each: the recovery key is the engine's arranged **entry path**, not
  `ctx.ElementPath` (the draw pass reuses the host context with only width and origin adjusted, so the
  context path is the page root for every element and would collapse every recovery into one slot), and
  the engine-facing entry points take the widget rather than an `Action`/`Func`, because the engine draws
  every element on every IMGUI pass and a delegate argument allocates a closure per call on the hottest
  path in the library. Consumer-facing delegate forms remain for controls whose fallback paint is the
  consumer's design.

## Ecosystem protocol — full form, provenance, and the metric

The AGENTS section carries the operational rules; this is why they exist and how they were derived, so
an edge case can be judged without re-running the audit that produced them.

- **Derived from measurement, not taste.** The FL-side adversarial audit (2026-09-07) graded the
  library retained-B / boundary-B / ecosystem-C; the same day's US compliance round produced the
  P1–P5 proposals. US's 16 `us/*` kinds are the *success* case — a consumer weighed the options,
  built, and stayed in the tree. The failures are exactly the raw-backend call sites: 4 in US
  (`Mouse.IsOver` at the settings-window close button, `UsKernelDraw` help-hover ×2, the Mod-settings
  shell button) plus one in FL's own `UiNative.IsFocusLost` — raw `Mouse.IsOver` beside its own
  private wrapper. Every one of the five traces to a missing primitive, which is why the ladder is
  "make the tree the cheap, reliable, visible path" and not "forbid the platform's only GUI API"
  (that one cannot be enforced by anything in .NET or in RimWorld).
- **The metric is tree-membership, not built-in usage.** The library can never ship every control —
  one manifest reference out of 17 shipped kinds (US's own pages, "Consumer coverage" above) is the
  proof that speculative coverage misses. What the library can own is identity, state, invalidation,
  recovery, and the audit surface. A bespoke kind inside a manifest page feeds all of those; a raw
  call replacing even a correct library kind feeds none. Baseline was 5 raw sites across both trees
  (2026-09-07); today the count is **zero** outside registered exemptions: FL's own leak
  (`UiNative.IsFocusLost`) is gone and the funnel is containment-gated, while US's four flipped with
  its 0.3.0 migration — FL verified by a read-only live run of US's gate 14 (2026-09-08). The one
  registered exemption left is world-space rendering
  (`GenMapUI` class of calls — no session, no hit-test, permanent by ruling). The count is what the
  consumer's gate should report against `tools/dependency-reality.ps1` rule (c); this repo cannot
  measure it without depending on a consumer tree, which is the vacuous-guard shape already recorded
  twice in the neutrality lane.
- **The exemption is now counted, and the two halves share the pattern set — not the allowlists
  (2026-09-12).** `\bGenMapUI\.` joined the shared pattern set on both halves after the maintainer
  ratified that the consumer's in-world pawn marker stays (a fixed-purpose part, not a redesign
  candidate). The consumer's file carries the matching entry with date, reason and recovery condition;
  **this repo carries none, because the measured count here is zero** — `GenMapUI` appears in
  `Source/**` nowhere, and the only two matches are the prose declaring map-layer rendering a permanent
  non-goal (`AGENTS.md`, this file). An entry that matches nothing would be a hole opened in advance.
  Wording precision: the halves are identical at the **term** level, not in regex shape — rule (c) here
  is owner-level (`Mouse.` any member, `Text.`, `Widgets.*`, optional `UnityEngine.`/`Verse.` prefix)
  while the consumer's is a member-level enumeration. And the shared object is the pattern set alone:
  **allowlists are ratified per side**; syncing entries would launder one side's exemption into the
  other side's boundary. Proven by three mutations in the isolated tree: removing the term from the
  lane (red), removing it from rule (c) (`-SelfTest` exits 1, `expected exactly 2 planted backend call
  sites, got 1`), and planting a real `GenMapUI.DrawText` in `Source/**` (production scan red) — i.e.
  zero here is a measurement, not a blind spot.
- **Why exemptions carry rent.** An allowlist without a named closing item drifts back into permanent
  undocumented self-implementation; the live specimen is US's diagnostics panel — 703 lines of
  hand-rolled immediate UI borrowing only the theme vocabulary, pinned to a revision so that count cannot
  rot silently (`Coahuilite/UniversalSqueaker@09366f8:Source/UniversalSqueaker/Diagnostics/SqueakDiagnosticsPanel.cs`).
  Written ruling + capability gap + TODO reference turns each bypass into debt with a due date instead
  of a precedent. **The due date is now dated by the consumer, not by us:** US
  opened the migration of this panel onto `UiWindowHost` + `UiHost` in its own buffer on 2026-09-09
  (§1, "devpanel"), stating that the shell surface it measured is sufficient and it asks FL for nothing
  — so it is not a round and takes no number, and the round-4 slot stays unused. Two things follow when
  it lands: US's gate 14 whitelist goes 2 → 1 (re-derive from its `scripts/ui-boundary-audit.ps1`), and
  this bullet must be re-derived, because its specimen will no longer exist. If the migration instead
  turns up a shell-level defect, that is the event that opens FL round 4.
- **Promotion-gate provenance, by item.** P1 (hover primitive), P2 (window shell), P3 (hover-claim
  machine) each cite consumer code that was forced to hand-roll them — that is the provenance test
  passing. The P2 ruling that US's 60–75 % / 800×600 clamp is consumer policy (size arrives as a
  provider, never as a library default) is the no-consumer-numbers clause applied before anything
  shipped. The gate's inverse also binds: `input/dropdown`, `input/stepper-slider`, `input/mode-row`
  and friends carry zero manifest consumption and are dealt with by the P5/§3 fix-or-delete, not kept
  "for symmetry".
- **Harvest loop — run once end to end, template set (2026-09-07).** Consumer compliance audit →
  HANDOFF round → FL review against code, per item a verdict / correction / refusal, line-cited → the
  accepted set lands as ONE pre-1.0 minor bump, shipped in lockstep with the consumer's migration
  commit → FL answers with the items it owes the consumer (self-containment fixes, the containment
  gate, engine-owned recovery, static retirement, the D-1 checker). The corrections are the point: two
  of five proposals misstated the very code they cited (`Dragging` is live, read by `LineChartWidget`;
  P3's grace machine is frame-count, not seconds, and there is no "frame counter the layout cache
  uses" to reuse — the cache is revision-keyed). Review discipline: verdict against code, not intent —
  and the same discipline applied to the review itself found two more (see the containment fact: an
  allowlisted file with nothing in it, and an enumeration narrower than its own acceptance line).
  Round lifecycle and section kinds are pinned in each repo's HANDOFF header; round numbers
  count on the library side and name the initiating consumer in full (`US→FL round 1`) —
  attribution is deliberate incentive, since this library grows only from consumer feedback.
  **Round 1 is CLOSED on the library side**: landed on `feat/round-1-0.3.0`, its bodies trimmed from
  the buffer, the permanent record here and in `TODO.md` §0. Closing was contingent on the work
  existing, not on the prose being filed — a trimmed round whose items are still open would be the
  triage failure the buffer header names. Not shipped: the tag waits for US's migration commit and for
  a maintainer decision.
- **What a closed round costs the next one.** Round 1 answered a consumer audit with six public-surface
  items and five library-owed items in one bump; that is the largest single change this repo has made
  and it stayed reviewable only because every item cited consumer code that already existed. The
  template's constraint is therefore not effort per item but evidence per item: an ask with no citation
  into a consumer tree does not enter a round at all, and this repo's owed items are owed items, not
  speculative features — the containment gate, engine recovery and the static retirement all existed as
  defects before anyone proposed them.
- **One-assembly stance reaffirmed under this protocol.** The visual-core/page-model split already
  passes a name-level gate; a second assembly would double the version axes to keep in step for zero
  new boundary. The re-open trigger stays exactly one: a consumer needs the visual core without the
  whole DLL.

## Structure and where things live

Paths and roles only; any line/file count here would be false within a day (see the predicate rule in
"Enduring corrections"). Re-derive with `git ls-files`.

- `Source/FerriteLib.UiKit/Kernel/` - the whole payload surface. `net472`, `TreatWarningsAsErrors`,
  `Nullable` on, output pinned to `1.6/Assemblies/` because that path is what consumers bind to.
  - Page model: `UiHost` (per-window façade, `IDisposable`), `UiWindowHost` (the window shell: chrome,
    one owned host, the deferred-notice failure contract), `UiSession`, `UiLayoutEngine` (the largest
    file by far), `UiLayoutManifest`, `UiLayoutSnapshot`, `UiBindings`/`IUiBindings`, `UiWidgetRegistry`,
    `KernelCoreWidgetRegistrar`, `UiWidgetContext`, `UiElementSpec`, `UiSessionGuard`, `UiValueState`,
    `UiNative` (the IMGUI interop seam), `UiPopup`, `UiChartPointChange`, the contract exceptions, and
    `Widgets/` (7 kinds).
  - Visual core: `UiTheme`, `UiThemeDraw`, `UiFitAudit`, `UiKitFonts`, `UiFont`, `ITextMetrics`,
    `VerseFerriteTextMetrics`.
  - `FerriteLibVersion` is BCL-only on purpose: no Verse, no UnityEngine, so a consumer can call
    `Require` from its earliest constructor and the harness can test it with no stubs at all. That claim
    now has one bounded exception worth knowing before it is repeated: since item E, `Require` reads
    `UiHostLedger`, which is itself BCL-only (a `List<string>` behind a lock, append-only, no reset), so
    the version lane still runs without loading a single stub — but it is no longer a *pure* function of
    its arguments. The pure decision stays in `Evaluate`, which is what the tests drive directly; the
    ledger line is added in `Require` only.
- `tools/FerriteLib.UiKit.Tests/` - a plain `Main()` runner (no test framework), lane files plus
  `Program` and `StubTextWidth`, with 4 standalone stub projects under `Stubs/`. Two of the lanes are
  gates over source text rather than over behaviour: `FerriteLibNeutralityTests` (product vocabulary)
  and `KernelContainmentTests` (backend contact, per-file and per-symbol allowlist). Both carry positive
  controls, because a scan over files is exactly the check that passes vacuously when a path moves.
- `tools/dependency-reality.ps1` - the D-1 reference checker FL owns the rule text for: AssemblyRef,
  page-model MemberRef contact, and declared-chrome allowlisting over a consumer tree, with `-SelfTest`
  proving the source scan can fire.
- `scripts/stub-coverage-scan.ps1` - the reference-driven door for the class above (gate 8's second half,
  added with the `Mathf.Clamp(int, int, int)` instance). It reads the MemberRef tables of the payload and
  of the harness assembly and requires every member they take from a stub-replaced game assembly
  (`Assembly-CSharp`, `UnityEngine.CoreModule`, `UnityEngine.IMGUIModule`,
  `UnityEngine.TextRenderingModule` - a rule list, deliberately not read off the stub) to be declared by the
  stub or listed in `scripts/stub-coverage-exemptions.txt` with a reason. The table is exact both ways: an
  unlisted unresolved member fails, a listed member that resolves again fails as STALE, a reasonless entry
  fails; it is empty today, which is the measured state (payload 57 references, harness 37, unresolved 0).
  `-SelfTest` adds three fixture controls: a stub set missing one whole assembly must fail and name a
  member, an empty stub directory and a tree with no target must exit 3 (not scanned) instead of reporting
  a clean zero. Fixture 1 failed on its first run - the replaced-assembly list had been derived from the
  stub, so removing a whole stub assembly removed its references from scope - which is why that list is a
  rule now.
- `scripts/verify-local.ps1` (9 gates, `-PackDev` adds packaging) and `scripts/pack-dev.ps1`. Gate 8 has two
  halves since 2026-09-12 - the net472 source-shape scan and the stub-coverage scan above, the second with
  its own `-SelfTest` - so the gate count is unchanged while the trap surface is not. Gate 9 was
  added 2026-09-12: it runs `dependency-reality.ps1 -SelfTest` and a TEMP fixture tree, so the boundary
  tool's pattern set and its allowance are proven to be able to go red on every full run instead of only
  when a human remembers to call `-SelfTest`. The three
  source-text gates and the version axes all run *inside* gate 1; the gate count is not the check count.
- `About/About.xml`, `LoadFolders.xml`, `LICENSE`, `1.6/Assemblies/` (the DLL and PDB are gitignored; only
  `.gitkeep` is tracked, so a fresh clone has no payload until it builds).

## Consumer contract, in the direction that hurts

- **`UiHost` is per-window and disposable; the consumer disposes it.** US does so from its own window's
  `PreClose`, a Verse hook that does not exist here. Nothing in this library may hold process-wide
  interaction state - that is what keeps two hosts in one game session from touching each other.
- **Schema=2 is the only shipped manifest schema**, and the consumer embeds its manifests as assembly
  resources and loads them with `GetManifestResourceStream` - a call that lives in US, not here.
  `UiLayoutManifest.ParseFile` has no production caller.
- **Popups are session-owned and window-spaced.** A surface that opens a popup publishes its covered rect
  through `UiSession.SetPopupRect`, which only `UiPopup.DrawOptionList` does; `UiNative`'s yield guard
  cannot fire without it. Geometry comes from `UiPopup.RectFor`, never from a local copy.
- **Naming hazard from the consumer side**: five substrings are banned in US's own `UI/**` (see
  "Enduring corrections"); all are legal here, but a public type carrying one becomes the consumer's red
  gate the moment it appears in a US file.
- **Byte-comparison against this repo only means something if the consumer pins LF too.** US's licence
  gate compares its `LICENSE` against this repo's byte-for-byte; with only a corpus rule in its own
  `.gitattributes`, a windows-latest checkout (`core.autocrlf=true`) gave US 16127 B against this repo's
  15780 B - every CI run would have failed on line endings alone. US adopted `* text=auto eol=lf`
  (`eadb931`, 2026-09-07). Read back as a lib-side constraint: this repo's own `* text=auto eol=lf` line
  is part of the consumer contract - dropping it would silently break any consumer that hashes or
  byte-compares carrier files on a Windows runner.
- Three symbols people keep assuming live here are **not** from this repo: `SessionRevisionBumper` (a US
  private class), `PreClose` (Verse), `GetManifestResourceStream` (BCL, called in US). Searching this tree
  for them returns nothing, by design.
- **A consumer's string is not a layout constant** (`a306cae`, 2026-09-12): the window shell used to size its
  close affordance from a fixed `110x30` constant, so a consumer whose close text was longer (US's
  diagnostics panel: `关闭（或连按两次 Esc）`) ran out of the box - that was the first real in-game
  fit-audit finding. `UiWindowHost.CloseButtonSize` is now
  `max(110, Metrics.MeasureWidth(CloseText, CloseFont) + 20)` over a `protected virtual ITextMetrics Metrics`
  (the same ruler the fit audit uses); 110 survives only as the lower bound. Fixed here rather than per
  consumer because three consumer windows ship through this shell: the constant made the library's geometry
  the place consumer text gets clipped.
- **An overflow record may carry exactly two non-content discriminators** (`UiOverflowReport.RectWidth` and
  the new `TextLength`, `a306cae`). Because a record must not carry UI text, those two are the only pair that
  can tell two candidates apart - the real-game finding needed `text_len` to separate a 24-character key
  literal from a 13-character CJK value.
- **Chrome and notices now draw inside element scopes** (`<windowType>/chrome`, `<windowType>/notice`,
  `a306cae`), so `(unscoped)` is no longer the identity a chrome finding carries. Trade-off recorded in the
  code: the identity is the window *type*, because the shell has neither an id nor a manifest while drawing
  chrome, so two instances of one window class share the path; findings dedupe by path+text, which merges
  rather than misattributes.
- **A missing translation key is drawn as the key, on purpose.** `IUiTranslation` now states the policy and
  its cost (visible but misleading), and names the owner of the check: only the host sees both its keys and
  the loaded language data, so the dev-only self-check belongs there, not here.

## How verification is described here

- **A lane that prints a failure without counting it is not a gate (found 2026-09-11).** The identity lane's
  own runner logged each failure and never incremented the failure count, so from the day it landed it could
  print red and still exit 0. Every mutation check run against it before that date was therefore evidence
  that the assertion *fired*, not that the suite *failed* -- those claims are regression guards, and the
  ledger now says so rather than keeping them as mutation proof. The criterion for a new gate is not "the
  console shows FAIL" but **"plant the defect and the process exits non-zero"**, and the same scan belongs
  on the older lanes, which nobody has audited for this shape yet.
  **That audit happened the next day and the family has no second instance:** all 21 lanes were probed
  through their own failure channel and every one exited non-zero with its marker printed, every lane is
  registered in `Program.cs` (so F-E's shape is empty too), and the control run went back to zero. The
  audit's own first attempt was invalid and the verifier said so: a bare `return` made the rest of the lane
  unreachable, `TreatWarningsAsErrors` turned that into CS0162, and all 21 lanes "exited non-zero" while
  compiling nothing -- a green that only holds if nobody reads the marker. Its stated limit is honest: this
  proves the failure channel and the process exit, not that each lane fails on a real defect.
- **A session that is alive is not a page that drew (measured 2026-09-12, found by the consumer's own
  report).** A consumer's page called `rect.ContractedBy(8f)`, a member of the game's `Verse.GenUI` that
  this repo's Verse stub did not declare. The call threw `TypeLoadException` at JIT time **inside the
  harness only**: the session guard did its job (that element became a recovery band, the frame survived),
  and the consumer's lane asserted nothing more than `Session.IsActive`, so it stayed green while the
  element under test never ran. In the game the same code draws, which is why nothing else noticed - the one
  trace was a trip log. Two rules came out of it, and both are now enforced here rather than remembered:
  (1) the stub is the game API's test double, so a missing member is the stub's defect, not the consumer's
  constraint - `Verse/GenUI.cs` was read from the game's own source (single-margin and per-axis
  `ContractedBy`, `ExpandedBy`; four-sided inset, no clamping, negative margin expands) and the stub now
  carries those three; (2) **a lane that draws a page asserts the page drew, not that the frame lived**:
  `KernelTripGuard.ExpectNoTrips` fails a lane when any element ended a frame in recovery, the lanes that
  trip on purpose pass `deliberateTrips: true` so the choice is visible in the call, and
  `KernelStubCoverageTests` carries the consumer-shaped call plus a planted-throw positive control. The
  hole class is wider than this member: any stub gap dissolves the consumer code under it while the
  surrounding assertions keep passing, so a new game member reaches the stub the moment a lane needs it -
  and the trip guard is what makes that need visible.
- **The same class twice in one day, and the door that stops waiting for a lane's luck (measured
  2026-09-12).** The guard found the second instance within hours: a consumer's timing card called
  `Mathf.Clamp(int, int, int)` while this stub carried only `Clamp(float, float, float)` - and `Max(int, int)`
  without its `Min(int, int)` pair - so that card became a recovery band, the page drew one card short, and
  the lane asserting `IsActive` had been green over it; in the game the same code draws. Both integer
  overloads are now declared and mirror the float forms (minimum branch first, Unity's own order for both,
  so a consumer cannot clamp differently by switching between int and float), and the lane asserts the
  semantics rather than "it did not throw". The hole class no longer depends on a lane reaching it:
  `scripts/stub-coverage-scan.ps1` (gate 8's second half) reads the payload's and the harness assembly's
  MemberRef tables against the stub surface, with a written exemption table that is exact both ways.
  **Mutation provenance** (every run with the touch / `--no-incremental` discipline, green after each
  revert): M1 delete `Verse.GenUI` -> `KernelStubCoverageTests` fails naming the `TypeLoadException` and the
  guard reports the recovery band; M2 make `ContractedBy` return its input -> four semantics assertions red;
  M3 make the guard vacuous -> the planted-throw positive control red; M4 delete `Mathf.Clamp(int, int,
  int)` -> the lane reds and the scan names
  `UnityEngine.CoreModule!UnityEngine.Mathf::Clamp(Int32,Int32,Int32)`; M5 leave one stale exemption in the
  table -> the scan reds as STALE; M6 declare the same name with a different signature
  (`Clamp(long, long, long)`) -> the scan still reds, so it matches signatures and not names. **M6 failed
  the first time, and the reason is a rule**: that run changed the stub *source* and ran the scan without
  rebuilding, so the scan read the old stub DLL and reported OK - the scan's inputs are built assemblies.
- **The double's language/constant batch (task-95, measured 2026-09-12).** Of the 188 members a consumer's
  built payload takes from the four stub-replaced game assemblies and this stub did not declare, the ones
  that carry no game logic are now declared, because every consumer hits them and none of them needed a
  judgement about game behaviour: `Rect.zero`; the arithmetic operators on `Vector2` and the whole
  `Vector3` type (`x/y/z` fields, `zero`/`one`, `+ - * /`, `magnitude`, `sqrMagnitude`, `Distance`,
  `Lerp`); `Color.black` and the named constants; `UnityEngine.Object.op_Equality/op_Inequality`;
  `Time.frameCount` and `realtimeSinceStartup`; `Verse.UI.screenWidth/screenHeight` (declared as the two
  public static FIELDS the reference assembly carries, not as properties - verified against 1.6.4871).
  Every one is exercised by `KernelStubCoverageTests`, which is what puts it under the reference-driven
  gate permanently: the scan now reads 57 payload references and 59 harness references (was 37), 0
  unresolved, 116 in total. Mutation M7 (remove `Color.black`, rebuild) reds the lane with
  `MissingMethodException` and makes the scan name
  `UnityEngine.CoreModule!UnityEngine.Color::get_black()`; green after the revert.
  **Two deliberate refusals belong to this batch.** The comparison operators on vectors and colours are
  NOT carried: in the game they compare through an epsilon, a stripped reference assembly cannot show that
  rule, and a guessed epsilon would silently change which branch a lane takes - a hole that throws is
  better than a double that lies quietly. And `UnityEngine.Object`'s destroyed-object/fake-null behaviour
  cannot be modelled by a managed double at all, so the operator is reference identity plus the null cases
  and its doc-comment says exactly that. The game-object and rendering-backend members (Pawn/Scribe/Sound/
  Def/Mod, Camera, Transform, `Widgets.Label`, `GenMapUI.DrawText`) stay OUT of the stub on purpose and
  belong in a consumer's exemption table with reasons: two of them are containment-whitelist surface, and
  growing the double to cover them would blur a boundary the product measures.
- **The trip guard is session-level by design, and its wording now says so (maintainer ruling 2026-09-12).**
  `UiSession.TrippedNodes` is cleared by `UiSession.Dispose` (`UiSession.cs:638`) and never by `BeginFrame`
  (`:288-291`), so a recovery in frame 3 is still reported by a check taken after frame 9. The guard's docs
  and failure text said "ended the frame in recovery", which reads as a per-frame sweep and invites the
  wrong bug report when a later healthy frame still trips; they now say "in this session were replaced by a
  recovery band" and the doc states the property and why it is stronger this way. Clearing in `BeginFrame`
  was considered and **refused**: it would let frame 3's silent recovery be forgotten by frame 4, which
  weakens the guard. A lane that deliberately trips and then draws healthy frames declares
  `deliberateTrips: true`; none of FL's own lanes needs that switch (checked: the three guarded call sites
  are healthy draws, and the one lane that plants a trip *expects* the guard to fail).
- **A correction the lead owes the record (2026-09-11).** While reviewing the scroll-target work the lead
  asked for "clear the request when the id does not resolve". That preference was wrong: the contract test
  at `KernelContractTests.cs:297-303` already required an unresolvable target to **stay pending**, because a
  target can legitimately sit in a tab that is not arranged yet -- clearing it would silently kill the
  cross-tab jump the request exists for. The implementer kept the existing semantics and said why, which is
  the behaviour this ledger wants from a lane that finds its instructions disagreeing with the code.

- A PASS is recorded with its scope and its evidence class. The classes in this repo are, weakest to
  strongest: reference-assembly read, stub harness, compile-time, and in-game observation - and only the
  last is written as "proven in a running game".
- **Scope an assertion to the region that can legitimately carry the defect.** Gate 6's first version
  searched the whole `LICENSE` for the Exhibit B sentence and went red on a *correct* licence, because the
  reproduced MPL body always contains that sample notice.
- **Assertion order is part of an assertion.** A `Copy-Item LICENSE` placed before the staging directory's
  `Remove-Item` would have shipped a package with no licence while every gate stayed green; any claim
  about package contents must run after the tree is final.
- Mutation-test a new gate, not only the new feature. Half of `VerifyThemeColorsDoNotAffectLayout` is a
  future-regression guard rather than present evidence, and it is labelled that way for exactly this
  reason.

## Gates and what each actually proves

Seven gates in `scripts/verify-local.ps1`: 1 harness, 2 Dev build (`FER_DEV`, warnings-as-errors), 3
Release build (warnings-as-errors), 4 payload present, 5 content-free (named-path probe for `Defs`,
`Patches`, `Languages`, `Sounds`, `Textures`, `ThingSets` under `1.6/` - probed as names, not filtered
from an enumeration, so an empty tree cannot pass vacuously), 6 LICENSE, 7 About.xml identity. Gate 6
proves only what is visible from inside this repo: the file exists, carries the MPL-2.0 title, still
contains Exhibit B and section 10.4, and does not apply the incompatibility notice in its header block.
It does **not** compare the text against a consumer's copy, and no script in this repo refers to a
sibling repo at all (checked: no `Get-FileHash`, no `..\` path in `scripts/`). The byte-parity assertion
is consumer-side - US's own gate 10 hashes both copies.

Dated correction, 2026-09-04: earlier text here credited gate 6 (as "the seventh") with a SHA-256
comparison to the consumer's copy. It never ran one. The consequence is not cosmetic: **a `LICENSE`
edited or truncated in this repo cannot turn any gate here red**; only a US gate run notices, and only
when the sibling tree is present. Anything that repeats "byte-identical to the consumer's copy" as a
property of this repo's gates is wrong in the same way.

The harness is 14 test files carrying 84 named lanes and 293 assertion results per run (counted 2026-09-10;
all three figures move within a day of work — re-derive with `ls tools/FerriteLib.UiKit.Tests/*Tests.cs |
wc -l`, `grep -c 'Run("' tools/FerriteLib.UiKit.Tests/*Tests.cs`, and `grep -c '^  ok:'` on a release run).
The earlier wording here — "13 lanes", "63 named assertions" — conflated files with assertions and rotted
twice, which is the reason the commands are written next to the numbers now. Three assertion
groups are worth naming because they are the reason this repo can be trusted across a boundary:

- Version contract lane (11 assertions): range accept/reject, the pre-1.0 bump rule, inverted range as
  a caller error, exact-API acceptance, duplicate-carrier detection driven through the internal
  decision function, range-vs-duplicate report separation, the named `MISMATCH` desync report,
  empty-copy-list safety, and the `Api` ↔ `modVersion` major/minor lock. Mutation-checked: breaking the
  duplicate condition and desyncing `modVersion` each fail exactly one lane.
- Neutrality lane (3 assertions): scans both trees case-insensitively for product words and
  case-sensitively for prefixes, **throws if a scanned tree is missing** rather than passing on an
  empty enumeration, plants literals into both trees as a positive control, and pins the single
  self-exemption to one full path. The guard on the guard exists because the previous arrangement —
  US scanning this library from over the fence — passed vacuously the moment the trees moved.
- Visual-core boundary lane: the seven visual-core files may not name any page-model type.
  Mutation-checked by planting a `UiSession` reference.
- The guard is a **name-list check, not a transitive one**: it reads the seven visual-core files and
  rejects any line naming one of fourteen page-model symbols. So it catches `UiThemeDraw` → `UiSession`
  directly, but not `UiThemeDraw` → `UiPopup` → `UiSession`, because `UiPopup` is in neither list — it
  joined the tree on `4dd97bf`, after the guard was written, and appears in neither array of
  `KernelContractTests.VerifyVisualCoreIsPageModelFree`. The two-layer claim is true of the code as it
  stands and unenforced along that one new path. Read from both arrays; no mutation test of this hole has
  been run, and the hole is currently hypothetical — nothing in the visual core calls `UiPopup`.

Honest limit on the theme lane: `VerifyThemeColorsDoNotAffectLayout` has two halves. The shared-instance
half is mutation-proven. The rect-equality half has **no available failing mutation** today, because no
colour token currently feeds layout — it is a future-regression guard, not present evidence.

## Enduring corrections

- **The `Warning`/`Danger` collapse rested on a census that read the wire as empty (correction, 2026-09-11).**
  The 2026-09-10 census said the two names had no users, and the 0.4 window deleted `UiTheme.Warning` on that
  basis. It measured **this** repository's source and inferred the consumer from it, and the inference was
  wrong: `Coahuilite/UniversalSqueaker` `Source/UniversalSqueaker/UI/Kernel/UsKernelDraw.cs:27` takes
  `theme.Warning` as the fill while the same expression takes `theme.Danger` as the border -- so removing the
  name broke a consumer's build at compile time rather than a pixel at runtime (found by the independent
  verifier while pairing a 0.4 carrier with the consumer tree, then confirmed here by compiling that tree
  against the 0.4 payload: with the name absent it fails, with the name restored it builds clean). The name
  is back as a redirect onto `Danger` -- the two always held one RGB -- and retires at the next minor
  boundary, once the consumer has moved. **Rule reinforced:** a census of this repository is evidence about
  *this* repository; "no users anywhere" is a cross-repo claim and is only as good as its citation.

- A library **can** assert its own neutrality. The note in the US harness claiming otherwise was written
  before the blocklist self-exemption was pinned to a single path with a positive control.
- `01-product-and-architecture-decisions-zh.md:208` (US repo) says the kernel's fallback text uses
  Ferrite-owned translation keys. Not implemented and, as it stands, not needed: `UiSessionGuard` logs
  only an English line and every visible fallback string comes from the consumer's `fallback` delegate.
  If a future theme or fallback wants its own key, that key belongs here and needs both language files.
- `UiTheme.DarkGold` is a **template**: each access returns a fresh instance. It used to be a shared
  mutable singleton, which is harmless with one consumer and cross-talk with two.
- **A count without its predicate is not a measurement.** A `find -not -path '*/obj/*'` never matches on
  this platform (paths are printed with backslashes), so any count taken that way silently includes
  MSBuild's generated `AssemblyInfo`/`AssemblyAttributes` files. Count with `git ls-files` + `wc -l` /
  `grep -c` instead: generated output is untracked, so it cannot leak in. **Every file and line figure this
  ledger once carried has been replaced by its command**, because two of them rotted while their own bullet
  was warning about rot; if you find a bare number here again, treat it as a stale claim until the command
  beside it is run.
- **The consumer's banned-substring list is six names, enforced by a C# invariant test — not by any
  `scripts/*.ps1` gate.** `UiSourceInvariantTests` forbids `UiInteract`, `Palette`, `SurfaceFrame`,
  `UiText`, `UiValueStore`, `UiPanel`, at
  `Coahuilite/UniversalSqueaker@09366f8:tools/UniversalSqueakerUiLogicTests/UiSourceInvariantTests.cs:153`.
  Two traps live here. First, a cross-repo re-check that greps the consumer's `scripts/`
  finds "no scan at all" and concludes the rule is a phantom — the gate is a test project, so the check
  must target `tools/`. Second, `UiPanel` used to be prose-only (five names scanned, six claimed); after
  FL→US round 2 reported it, US added it to the list rather than deleting it from the rule, so the scan
  and the memory now agree at six. The phantom is resolved at source; do not re-report it.
- **Documentation describes a moment, not a state.** A commit anchor in a prose doc rots the next time
  code lands; during this tidy the tree moved `958ac7d → 4dd97bf → 632a9a3 → a05fddf → 12dacb4` in about
  thirty minutes, retiring a "measured at <sha>" claim twice. Prefer a re-derivable command and a date
  over an anchor nobody re-checks.
- **A report-only item with no delivery channel is not queued, it is lost.** The reclassification of the
  round-3 package from 0.4.0 to 0.3.0 was recorded here and in the buffer annex with the instruction that
  US's docs "should re-point when that repo is next open". US then rewrote its own `TODO.md` and
  `HANDOFF.md` on 2026-09-09, and its three "0.4.0 package" lines plus its summary of FL's round-3 state
  ("REVIEWED, pending scheduling") survived that rewrite unchanged. A sibling session does not read our
  buffer, and a buffer rewrite is precisely the moment when stale cross-repo status is *not* consulted.
  **Resolved 2026-09-10, and the resolution is the rule**: the maintainer authorised this session to
  inspect and then edit the sibling's docs directly, and the four stale claims landed as US `2951934`
  (its `TODO.md:8,9,46,60`, one durable line in its `MEMORY.md`, its local buffer's FL-state summary and
  pointer index). So an outstanding ledger item goes to the user *with an offer to apply it*: `AGENTS.md`
  Boundaries make the sibling read-only by default, only an authorization turns a report into a fix, and
  the report alone was never the delivery.
- **A ref pair rots like a SHA anchor.** "`0.3.x` and `main` both at `19cfdcc`" was true for hours. Say
  the predicate instead and let it be re-checked: `git diff --name-only main 0.3.x` returning only `.md`
  paths is what "the tested bytes and the shipped bytes are one tree" means.
- **A green pre-push privacy scan says nothing about the identity a remote merge button will stamp.**
  The 2026-09-07 scan passed at the pushed tip; the 2026-09-09 PR #1 merge added one commit whose
  author is the clicker's display name (`Fe <…@users.noreply…>`), GitHub itself as committer, and the
  `-FullHistory` identity vector went red through no edit of ours. The scan must be re-run after
  remote-side history events, not only before pushes — the same rule it already states for commits,
  extended to merge buttons, tags' absence and any other hand that writes to the graph. **The fix planned
  here — amend the merge commit's author and force-push both branches — was overtaken on 2026-09-10:
  `gh api user` shows `Fe` is this account's own GitHub-published display name, so the vector was a gate bug
  rather than a leak, and the gate now measures accounts instead of name strings (see the privacy-gate
  record in this file). The lesson about re-running after remote-side history events stands; the
  history-rewrite advice does not.**
