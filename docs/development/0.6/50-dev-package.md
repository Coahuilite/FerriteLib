# Dev package — 0.6.0-dev (R06-fixed)

## 1. Where it is

```
ferritelib/dist/dev/FerriteLib/                  (gitignored; staged by scripts/verify-local.ps1 -PackDev)
  About/About.xml
  LICENSE
  LoadFolders.xml
  version.txt
  1.6/Assemblies/FerriteLib.UiKit.dll
```

Five files, one DLL, no Defs, no Patches, no Languages, no textures, no `PublishedFileId.txt`. Installing it
means copying the folder into a game `Mods/` directory yourself — **no script in this repository places a mod
in the game**, and neither this round nor the review round placed one.

## 2. What it was built from

**This is the R06-fixed package.** Re-staged after the independent review was dispositioned; it contains the
R06-2 Attach fix and every other 0.6 library fix, and it is the artefact the outside probe and the verifier
both ran against.

| | |
| --- | --- |
| Branch | `0.6.x` |
| Content freeze (the tree this package was staged from, clean) | **`7b62416`** |
| `version.txt` | `FerriteLib 0.6.0-dev` / `build=dev` / `commit=7b62416fa623` |

File-by-file SHA-256 (verified against the staged folder before zipping):

```
LICENSE                                        15780   71B96808D967417BCE548B8159ED0409909763CC4371575CCF58455DE044F968
LoadFolders.xml                                  102   B99E238DAA898972E726527E8D19A21F6E5EF1E7457B9622032F9D5A343DC759
version.txt                                      103   D796B630CF704CE16604375DCB23EAF48162A5BB442BA7A8AAC4CCCDEB188D38
1.6/Assemblies/FerriteLib.UiKit.dll           249856   2A7F9C9E9B1D060B32037A82E2989817A19B8317D2FB8B48A68F15B13DDECC0D
About/About.xml                                 2198   1EBA1798C5124AF8DA1EEE83F727A128E92B74F2EF379D9E0B4481CABA293E99
```

Transfer zip: `dist/dev/FerriteLib-0.6.0-dev.zip` (same five entries, compressed), SHA-256
`486902439E1977903BDE80464169838E2C44CDE1864C193D35EFC37D9DC83979`, 111 494 bytes.

**Honest note about the revision.** The commits after `7b62416` are documentation-only (this file, the
`consumers/` docs, and the verification/disposition records), so the branch tip is several documentation
commits ahead of the tree the payload was built from. The delivered artefact is the staged folder above; the
DLL inside it embeds `7b62416` in its informational version, which is never a compatibility value.

A consumer takes **this** package — the earlier `f3595a1`/185C5760 build predates the R06 fixes and must not
be used. `docs/consumers/consume-from-0.6.0.md` is the permanent pointer to it.

## 3. The demo mod

A separate local repository beside this one, `ferritelib_uikit_demo` (`master`, **no remote**, never
pushed, never installed into a game):

| | |
| --- | --- |
| Tip | `587bf160` |
| Staged package | `ferritelib_uikit_demo/dist/FerriteLibUiKitDemo/` — 8 files |
| `LoadFolders.xml` (200 B) | `3FD398611C5E6013B590BB1E13A5FB328F88AE9A1C6608F6AE076DD0D63D5C49`, declares `/` + `1.6` |
| Demo DLL (49664 B) | `53CBF59E3BDD8602CC0E43E45A3BF01412119BEB64339B3F2FAB6FB2EC4685E7` |
| Carried carrier | **none** — no `FerriteLib.UiKit.dll` in the package (the reference is `Private=false`) |

Installing both means copying **both** staged folders into `Mods/` and enabling them in that order:
`coahuilite.ferritelib` first (the demo declares it as a dependency and loads after it).

## 4. How to rebuild

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev          # gates + staged dev folder
# demo:
pwsh -NoProfile -File pack.ps1                                   # in the demo repo (validate-then-swap staging)
```

Nothing here pushes, tags, releases or creates a remote. Each of those is outside this round's authorization
and would need the maintainer's. If you fetch a new carrier, rebuild consumers with `--no-incremental`
(the path-resolved reference does not invalidate MSBuild's up-to-date check).
