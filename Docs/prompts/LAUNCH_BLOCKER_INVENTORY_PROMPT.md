# Prompt — index what ships, and prove we are licensed to ship it

Paste everything below into a fresh session.

---

Two questions have to be answerable before a build goes to people outside the studio, and neither
has an answer in the repository today:

1. **What is in `Assets/` that should not ship** — dead folders, vestigial systems, duplicated
   content, and anything whose presence in a player build would be a defect or an embarrassment.
2. **What third-party software is in here, and are we entitled to ship it.** Several of the largest
   folders are commercial Unity Asset Store products. The repository proves they are *present*; only
   purchase records prove we may *distribute* them.

This produces an **index and a licence register**, not a deletion spree. Deleting an asset that
turns out to be load-bearing is a worse day than shipping a tidy repository.

Read `Docs/STEAM_RELEASE_TASKS.md` (item R14) first. `Docs/VESSEL_CONSTRUCTION.md` and
`Docs/VESSEL_CONSTRUCTION_FOLLOWUP.md` already carry a salvage-before-delete gate for the model
vestiges — apply the same posture everywhere.

## Measured 10 Sep 2026 — the starting inventory. Re-verify; this will drift

### Third-party under `Assets/`, by size

| Size | Folder | What it is | Licence file present? |
|---|---|---|---|
| 269 MB | `Plugins/` | FMOD, DOTween (Demigiant), Obvious.Soap, NativeShare, ParrelSync, platform natives | FMOD only |
| 47 MB | `NiceVibrations/` | **Commercial** haptics asset (Lofelt / More Mountains) | `3RD-PARTY-LICENSES.md`, `HapticPackCClicense.txt` |
| 19 MB | `Unity Assests/` | Typo'd grab-bag, **199 files** | **none** |
| 8.2 MB | `Effects Library/` | VFX pack | **none** |
| 4.9 MB | `PlayFabEditorExtensions/` | Legacy backend tooling | — |
| 4.7 MB | `PlayFabSDK/` | Legacy backend SDK, inert | — |
| 4.4 MB | `YethGameDev/` | QuickScenePro — an editor tool | `QuickScenePro/LICENSE.md` |
| 1.0 MB | `PrimitivePlus/` | Model pack | **none** |
| 816 KB | `ExternalDependencyManager/` | Google EDM4U | `LICENSE` |
| 584 KB | `Shift - Complete Sci-Fi UI/` | **Commercial** UI kit | **none** |
| 84 KB | `Wwise/` | **Zero non-meta files** — an empty skeleton of `.meta`s | — |
| 76 KB | `Parse/` | Legacy backend | — |

Package-manager dependencies are clean by comparison: **77 deps, only 3 non-Unity**, all git and
all permissive — UniTask, Reflex, ParrelSync. No scoped registries. The exposure is all in
`Assets/`.

### Dead or suspicious folders

| Size | Path | Note |
|---|---|---|
| 3.4 MB | `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/` | 9 files. **Do not delete blind** — it currently hosts `GameModeProgressionService`, which is exactly why the progression system is not in the build. See the audit §02. |
| 19 MB | `Assets/Unity Assests/` | 199 files behind a typo'd name. Nothing indicates what it is or who added it. |
| 288 KB | `Assets/_Scripts/Game/` | Vestigial — CLAUDE.md records it as holding only non-code assets plus `PRISM_PERFORMANCE_AUDIT.md`. |
| 84 KB | `Assets/Wwise/` | Inert: **zero first-party references**, no `AkSoundEngine` usage anywhere. Audio is FMOD. |
| 4.7 MB | `Assets/PlayFabSDK/` | PlayFab is legacy and inert; auth, cloud save and analytics are all UGS. |

Also worth indexing: the **20 arcade cards whose scene does not exist** (measured — `ArcadeGameBlockBandit`,
`BotDuel`, `CatNMouse`, `CellularBrawl`, `Curvatious`, `DashAndGrab`, `Denial`, `Distraction`,
`DolphinDarts`, `Elimination`, `KickinMass`, `MasterExploder`, `MazeRun`, `ObstacleCourse`,
`PumpNDump`, `RiskyDriftness`, `SlipNStride`, `Soar`, `_ArcadeGameCosmicDrift`,
`_ArcadeGameShootingGallery`). They are already pruned from every roster —
`python3 Tools/Build/check_gamelist_scenes.py` passes — so they are inert, not broken. Index them
as *assets for modes that do not exist* and let someone decide.

And the **eight vessel models referenced by nothing**, already documented in
`Docs/VESSEL_CONSTRUCTION.md`, including `dolphin_shapekey_with_animations` which carries a **real**
10,909-vertex morph and is referenced by nothing — a salvage candidate, not a deletion candidate.

## What to produce

**1 · `Docs/THIRD_PARTY_REGISTER.md`** — one row per third-party component: name, vendor, version if
discoverable, where it lives, licence file present or absent, **whether it ships in a player build
or is editor-only**, and the entitlement question a human has to answer.

That ships-or-not column is the one that decides urgency:

- **ParrelSync is editor-only and must never ship.** Confirm it is excluded from player builds —
  it is a dev tool and its presence in a shipped player would be a straightforward mistake.
- **PlayFabEditorExtensions** is editor tooling; `PlayFabSDK` is runtime code that is inert but may
  still be compiled in.
- **Wwise** ships nothing (it has no files) but is still *in the repository*, and Wwise licensing is
  per-title. Worth a line even though the answer is probably "remove it".
- **FMOD** is free below a revenue threshold but has tiers and an attribution requirement. Record
  which tier applies and whether the attribution is present in-game.
- **DOTween** — free and DOTween **Pro** are different products with different licences. Determine
  which is vendored.

**2 · `Docs/LAUNCH_BLOCKER_INDEX.md`** — every folder and file that should not be in a shipping
build, each with: what it is, what references it (measured, not assumed), what breaks if it goes,
and a **verdict of `remove` / `keep` / `salvage-first` / `needs-a-human`**.

**3 · A measured reference check for every deletion candidate.** The method matters — CLAUDE.md
records that `grep -rl | head -1` returns plausible false positives, because exactly one `.meta`
*owns* a guid and an FBX's `.meta` can carry an `externalObjects` remap into another FBX. Resolve
with `grep -c "^guid: $g"` per candidate and cross-check against something the prefab authored.
Two passes of Rhino jet work went onto the wrong hull by skipping this.

## Constraints

- **Delete nothing in this branch.** The deliverable is the index and the register. Removals are
  separate, reviewable changes, each with its own reference proof.
- **You cannot determine what was paid for.** The repository proves presence; invoices prove
  entitlement. Produce the *questions*, addressed to whoever holds the Asset Store account — never
  assert a licence status you cannot see.
- **Do not touch `MIgration_Prefabs (DELETE LATER)` beyond indexing it.** Its contents are the
  subject of an open decision (audit §02); the folder name is a trap.
- Flag anything that looks like a credential, key or token, **by location, never by value**.
- If a folder's purpose is genuinely unknowable from the repository, say *unknown* and name who
  would know. An invented explanation is worse than a gap.

## Definition of done

1. `Docs/THIRD_PARTY_REGISTER.md` covers every third-party component in `Assets/` and
   `Packages/manifest.json`, with a ships/editor-only column and an explicit entitlement question
   per commercial product.
2. `Docs/LAUNCH_BLOCKER_INDEX.md` gives every candidate a verdict backed by a measured reference
   check.
3. ParrelSync's exclusion from player builds is confirmed or flagged.
4. Nothing is deleted, and no licence status is asserted without a document behind it.
5. `Docs/STEAM_RELEASE_TASKS.md` R14 is ticked, with any licence exposure raised to a human
   immediately rather than waiting for the next checkpoint.
