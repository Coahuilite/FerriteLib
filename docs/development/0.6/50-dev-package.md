# Dev package — 0.6.0-dev

## 1. Where it is

```
ferritelib/dist/dev/FerriteLib/          (gitignored; staged by scripts/verify-local.ps1 -PackDev)
  About/About.xml
  LICENSE
  LoadFolders.xml
  version.txt
  1.6/Assemblies/FerriteLib.UiKit.dll
```

Five files, one DLL, no Defs, no Patches, no Languages, no textures, no `PublishedFileId.txt`. Installing it
means copying the folder into a game `Mods/` directory yourself — **no script in this repository places a mod
in the game**, and this round did not place one.

## 2. What it was built from

| | |
| --- | --- |
| Branch | `0.6.x` |
| Content freeze (the tree this package was staged from, clean) | **`f3595a1`** |
| `version.txt` | `FerriteLib 0.6.0-dev` / `build=dev` / `commit=f3595a1b28f1` |

```
LICENSE                                        15780   71B96808D967417BCE548B8159ED0409909763CC4371575CCF58455DE044F968
LoadFolders.xml                                  102   B99E238DAA898972E726527E8D19A21F6E5EF1E7457B9622032F9D5A343DC759
version.txt                                      103   0FD93D51920F254C45BB0D7DF91284E343185D785014B1E504DAA1B08248CB93
1.6/Assemblies/FerriteLib.UiKit.dll           249344   185C57601FB0D66781B6145B79711221FBB2096334F6355F18D8B7357388B365
About/About.xml                                 2198   1EBA1798C5124AF8DA1EEE83F727A128E92B74F2EF379D9E0B4481CABA293E99
```

**One honest note about the revision.** The commits after `f3595a1` are documentation-only — this file and
the verifier's `verification/t5c-final-integrated.md` — so the branch tip is a few documentation commits
ahead of the tree the payload was built from. The delivered artefact is the staged folder above, and the DLL
inside it embeds `f3595a1` in its informational version, which is never a compatibility value. Nothing in
the payload depends on those files; the verifier confirmed the difference is
`git diff --name-status f3595a1 <tip>` = documentation additions only.

## 3. The demo mod

A separate local repository beside this one, `ferritelib_uikit_demo` (`master`, **no remote**, never
pushed, never installed into a game):

| | |
| --- | --- |
| Tip | `d47b221` |
| Staged package | `ferritelib_uikit_demo/dist/FerriteLibUiKitDemo/` — 8 files |
| Demo DLL SHA-256 | `8F30685E68A8D05CFA0EAA5B10D7586DA104F788AE88243DDDAB05674886DEFA` |
| Carried carrier | **none** — the package contains no `FerriteLib.UiKit.dll` by construction (the reference is `Private=false`) |

Installing both means copying **both** staged folders into `Mods/` and enabling them in that order:
`coahuilite.ferritelib` first (the demo declares it as a dependency and loads after it).

## 4. How to rebuild

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev          # gates + staged dev folder
# demo:
pwsh -NoProfile -File pack.ps1                                   # in the demo repo
```

Nothing here pushes, tags, releases or creates a remote. Each of those is outside this round's
authorization and would need the maintainer's.
