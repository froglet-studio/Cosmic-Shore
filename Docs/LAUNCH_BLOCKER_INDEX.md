# Launch blocker index

Everything in `Assets/` that is a candidate for *"should not be in a build outsiders run"*, each
with **what references it (measured)**, **what breaks if it goes**, and a **verdict**.

**Measured 11 Sep 2026** against `claude/zealous-davinci-vjwev0`. Re-verify before acting.

> **Nothing in this document has been deleted, and nothing should be deleted on the strength of
> this document alone.** Each removal is its own reviewable change with its own reference proof.
> This follows the salvage-before-delete gate already established in
> [`VESSEL_CONSTRUCTION_FOLLOWUP.md`](VESSEL_CONSTRUCTION_FOLLOWUP.md): *"Referenced by nothing"
> means nothing is using it, not that it contains nothing.*

Companion: **[`THIRD_PARTY_REGISTER.md`](THIRD_PARTY_REGISTER.md)** — vendors, licences, and the
entitlement questions.

---

## Verdicts at a glance

| # | Candidate | Size | Verdict |
|---|---|---|---|
| **A1** | `Unity Assests/TextMesh Pro/Examples & Extras` | 7.6 MB | `remove` — **split the folder first** |
| **A2** | `YethGameDev/QuickScenePro/Resources` | 1.8 MB | `remove` — move under `Editor/` |
| **A3** | `NiceVibrations/Demo` | 7.4 MB | `salvage-first` — **art is in the shipped menu** |
| **A4** | `Wwise` | 84 KB | `remove` — zero files, zero references |
| **A5** | `Parse` | 76 KB | `remove` — zero references of any kind |
| **B1** | NiceVibrations demo art used by first-party UI | — | `needs-a-human` — **licence question** |
| **B2** | `PlayFabSDK` | 4.7 MB | `needs-a-human` — inert but **20 files** compile against it |
| **B3** | `MIgration_Prefabs (DELETE LATER)` | 3.4 MB | `needs-a-human` — **do not touch** (audit §02) |
| **B4** | 14 orphan arcade cards | small | `needs-a-human` — product decision |
| **B5** | 6 orphan arcade cards **still referenced** | small | `keep` until the referrers are cut |
| **C1** | `Unity Assests/TextMesh Pro/Resources` | 7.7 MB | **`keep` — load-bearing, invisible to refcheck** |
| **C2** | `Unity Assests/Adaptive Performance` | small | `keep` — wired into `ProjectSettings` |
| **C3** | `PrimitivePlus` | 1008 KB | `keep` — meshes used by projectiles/AOE |
| **C4** | `Shift - Complete Sci-Fi UI` | 584 KB | `keep` — textures used by 2 build scenes |
| **C5** | `Effects Library` | 8.2 MB | `keep` — nested in `VesselJet.prefab` |
| **C6** | `_Scripts/Game` | 288 KB | **`keep` — CLAUDE.md is wrong about this folder** |
| **C7** | `PlayFabEditorExtensions` | 4.9 MB | `keep` (editor-only, does not ship) |
| **D1** | 8 vessel model vestiges | ~large | `salvage-first` — already gated, see §D1 |

---

## Method, and the two things it cannot see

Inbound references were measured with a single pass that indexes every guid in every `.unity`,
`.prefab`, `.asset`, `.mat`, `.controller`, `.shadergraph` and `.meta` in `Assets/`,
`ProjectSettings/` and `Packages/`, then for each candidate asset:

1. reads that asset's **own** guid from its `.meta`,
2. asserts **ownership is unique** — `grep -c "^guid: $g"` across every `.meta`, per CLAUDE.md,
   because `grep -rl | head -1` returns plausible false positives,
3. counts referrers **outside** the candidate folder, so a folder that only references itself is
   correctly reported as self-contained.

That method reproduced the documented hazard live: **`RhinoModel.fbx` has 0 asset references but
2 `.meta` `externalObjects` remaps pointing into it** (from both `Vessel_Placeholder_*.fbx.meta`).
A `head -1` check would have mis-attributed it; a naive "0 references → delete" would have broken
two `.meta` files.

**Blind spot 1 — `Resources.Load` by name.** A guid check cannot see a runtime load by string.
`Unity Assests/TextMesh Pro/Resources/TMP Settings.asset` measures **zero inbound guid references**
and is nonetheless required by every text component in the game, because TMP loads it by name.
**A "0 references" result on anything inside a `Resources/` folder means nothing.** There are 16
name-based `Resources.Load` calls in first-party code; two of them (`ShapesFX_PACK`,
`Textures/Example`) resolve to no asset under `Assets/` at all and are presumed dead paths.

**Blind spot 2 — C# type references.** `using PlayFab;` creates no guid link. `PlayFabSDK` measures
0 inbound guid references and **20 first-party files reference it in code** (§B2).

Both blind spots were checked explicitly for every candidate below.

---

## A — ships today, should not

### A1 · `Assets/Unity Assests/TextMesh Pro/Examples & Extras` — 7.6 MB, 148 assets, 34 `.cs`

TMP's demo content. **3.5 MB of it ships unconditionally** because
`Examples & Extras/Resources/` is a `Resources` folder (rule 4), and the 34 example scripts compile
into `Assembly-CSharp` because the folder has no `.asmdef`.

**Measured references from first-party content — it is not cleanly removable:**

| Example asset | Referenced by |
|---|---|
| `Resources/Fonts & Materials/Electronic Highway Sign SDF.asset` | **`_Prefabs/Spacevessels/Manta.prefab`** |
| `Resources/Fonts & Materials/Bangers SDF.asset` | `_Prefabs/UI Elements/Main Menu Screens/QuestItemPrefab.prefab` |

Both verified unique-guid-owner. **Verdict `remove`, but split first:** move those two font assets
(and anything else a later sweep finds) into the real `TextMesh Pro/Resources/`, repoint nothing —
the guid travels with the file — then delete the rest of `Examples & Extras`. Deleting it wholesale
breaks a **shipped vessel prefab**.

### A2 · `Assets/YethGameDev/QuickScenePro/Resources` — 1.8 MB

QuickScene Pro is an **editor tool** and its code is correctly under `Editor/`. Its `Resources/`
folder is **not**, so three editor-tool icons (`QSP_Icon.png`, `icon_additive.png`,
`icon_single.png`) are packed into every player build.

Referenced only by its own three demo scenes, none of which is in `EditorBuildSettings`.

**Verdict `remove` from the player** — move `Resources/` to `Editor/Resources/` (the tool keeps
working; `Resources.Load` still resolves for editor code). Licence is MIT so there is no
entitlement problem, only a shipped-bloat one. `Demo/` (3 scenes) can go with it.

### A3 · `Assets/NiceVibrations/Demo` — 7.4 MB, 30 `.cs`

`Lofelt.NiceVibrations.Demo.asmdef` has `"includePlatforms": []`, so **30 demo scripts compile into
the player**. `NiceVibrationsDemo.unity` is not in the build list, so the scene itself does not ship.

**But the demo ART is load-bearing in shipped UI** — measured, unique-guid-owner:

| Demo asset | Referenced by |
|---|---|
| `DemoAssets/RegularPresetsDemo/Sprites/RegularPresetsIcons.png` | **`_Scenes/Menu_Main.unity`** (build scene), `Arcade Screen.prefab`, `ModalWindows.prefab` |
| `.../RegularPresetsIcons_8_Flipped_Vertically.asset` | **`_Scenes/Menu_Main.unity`**, `Records Screen.prefab` |
| `DemoAssets/CarDemo/Sprites/NVCar.png` | `_SO_Assets/Classes/SO_Class_Termite.asset` |
| `_Common/Sprites/NV7Dots.png` | `_SO_Assets/Classes/SO_Class_Termite.asset` |

**Verdict `salvage-first`.** Setting the Demo asmdef to `["Editor"]` is safe and removes 30 scripts
from the player. Deleting the folder is **not** safe — it would break the main menu. See §B1: the
right fix is probably to replace this art, and that is a licence decision before it is an art task.

### A4 · `Assets/Wwise` — 84 KB

**Zero non-`.meta` files** (14 `.meta`, 0 assets). **Zero** references — guid or code. No
`AkSoundEngine` / `AkAudioListener` / `AkBank` / `AkEvent` anywhere in first-party code. Audio is
FMOD (17 files use `FMODUnity`).

**Verdict `remove`** — it ships nothing today, so this is repository hygiene, not a build defect.
One caveat is not a code question: confirm no Wwise **per-title licence obligation** was incurred
when it was evaluated (register question 7).

### A5 · `Assets/Parse` — 76 KB

Two DLLs (`Unity.Tasks.dll`, `Unity.Compat.dll`) from the retired Parse backend. **0 guid
references and 0 code references** (`using Parse`, `ParseObject`, `ParseClient`, `Unity.Tasks`,
`Unity.Compat` — all zero hits in first-party code).

**Verdict `remove`.** The cleanest candidate in this document: nothing in the project can see it.

---

## B — needs a human

### B1 · Commercial demo art in the shipped main menu — **licence exposure, raise now**

The clearest exposure found. First-party UI draws sprites out of a **commercial asset's demo
folder**, and one of those references is in `Menu_Main.unity`, an enabled build scene (§A3 for the
table).

This is **not** a question about whether NiceVibrations is licensed — it is a question about whether
*demo content* is licensed for redistribution in a shipped product, which Asset Store EULAs treat
separately from the plugin. **Addressed to the Asset Store account holder.** If the answer is no,
the fix is to re-author four sprites, which is an art task with a lead time — hence "raise now"
rather than "raise at the next checkpoint".

### B2 · `Assets/PlayFabSDK` — 4.7 MB, ships

`PlayFab.asmdef` has `"includePlatforms": []`, and `Shared/Public/Resources/` is a shipping
`Resources` folder. So a legacy backend SDK is compiled into and packed into the player.

**It measures 0 inbound guid references — and that is the blind spot, not the answer.** 20
first-party files reference PlayFab **in code**, including `CatalogManager`, `DailyRewardHandler`,
`GroupController`, `AuthenticationManager`, `DailyChallengeSystem` and `ProfileModal`.

**Verdict `needs-a-human`.** Removing the SDK is a **code** change (delete or port those 20 call
sites) and is out of scope for an index. It intersects two live items: **R4** already de-scoped the
commerce surfaces, and the Hangar captain upgrade is PlayFab-catalog commerce. Worth scoping as its
own task; it is the largest single piece of dead weight that a build currently carries.

### B3 · `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)` — 3.4 MB, 9 prefabs

**Indexed only — deliberately not acted on.** R14 names this a trap and the contents are the
subject of audit §02 (the quest/progression chain), which `STEAM_RELEASE_TASKS.md` explicitly
excludes from the board.

Measured for the record: **0 inbound references** from outside the folder. Contents are
`ArcadeScreen`, `IAPManager`, `ModalWindows`, `NavBar`, `PlayerDataService`, `Screens`,
`ToastNotificationContainer`, `ToastNotificationManager`, `UGSStatsManager`. Note the folder is not
inert *internally* — `Screens.prefab` references a NiceVibrations demo sprite (§A3).

**The folder name is the trap:** "0 references" plus "DELETE LATER" reads like a safe delete, and
the open progression decision is exactly why it is not. **Verdict `needs-a-human`**, owner: whoever
owns audit §02.

### B4 · 14 orphan arcade cards — `SO_ArcadeGame` assets for modes with no scene

Measured: **0 inbound references each.** `ArcadeGameBotDuel`, `CatNMouse`, `CellularBrawl`,
`Curvatious`, `DashAndGrab`, `Denial`, `Distraction`, `KickinMass`, `MasterExploder`,
`ObstacleCourse`, `PumpNDump`, `RiskyDriftness`, `Soar`, `_ArcadeGameCosmicDrift`.

Inert — they name scenes that do not exist and nothing points at them.
**Verdict `needs-a-human`:** whether a card for an unbuilt mode is a backlog artefact worth keeping
is a product call, not an engineering one.

### B5 · 6 orphan arcade cards that are **still referenced** — `keep` for now

**This corrects the starting inventory.** R14 records all 20 as inert on the strength of
`check_gamelist_scenes.py` passing — but that checker validates **`SO_GameList` rosters only**, and
six cards are reachable by a different path it does not cover:

| Card | Still referenced by |
|---|---|
| `ArcadeGameBlockBandit` | `SO_Class_Manta`, `SO_Class_Serpent`, `SO_Class_Squirrel`, `SO_Class_Termite`, `SO_TrainingGame_BlockBandit` |
| `ArcadeGameElimination` | `SO_Class_Grizzly`, `SO_Class_Rhino`, `SO_Class_Urchin` |
| `ArcadeGameDolphinDarts` | `SO_Class_Dolphin`, `SO_TrainingGame_DolphinDarts` |
| `_ArcadeGameShootingGallery` | `SO_Class_Grizzly`, `SO_Class_Urchin` |
| `ArcadeGameMazeRun` | `SO_TrainingGame_MazeRun` |
| `ArcadeGameSlipNStride` | `SO_TrainingGame_SlipAndStride` |

Deleting these dangles a reference in a **vessel class** SO. **Verdict `keep`** until the referrer
is cut. General point worth carrying: *a roster checker passing proves the rosters are clean, not
that an asset is unreferenced.*

---

## C — keep, and why (the "worse day" cases)

### C1 · `Assets/Unity Assests/TextMesh Pro/Resources` — 7.7 MB — **the load-bearing one**

Holds **`TMP Settings.asset`**, which TMP loads at runtime by `Resources.Load` **name**. It measures
**zero inbound guid references**.

**Deleting `Assets/Unity Assests/` on a reference check alone would break every piece of text in the
game.** The typo'd folder name makes it look like an unattributed grab-bag; it is TMP's essential
resources, Adaptive Performance's settings, and TMP's examples, in one folder. **Verdict `keep`.**
Only §A1's `Examples & Extras` subtree is a removal candidate, and only after the split.

### C2 · `Assets/Unity Assests/Adaptive Performance` — `keep`

`AdaptivePerformanceGeneralSettings.asset` and the Samsung/Simulator provider settings are
referenced by **`ProjectSettings/ProjectSettings.asset`** and `EditorBuildSettings.asset`.

### C3 · `Assets/PrimitivePlus` — `keep`

Its `Resources/Meshes/` are used across shipped gameplay: `Sphere.asset` → `Projectile.prefab`,
`FalconProjectile`, `AxeBubble`, `AOEExplosionRed`; `Cone.asset` → `AOEConicExplosion`,
`AOEConicSkyBurst`, `LaserGraph.shadergraph`; `Cube.asset` → the FX crackle prefabs;
`CylinderTube.asset` → `oldWallFlora`. Also `PrimitivePlusMaterial.cs` → 3 projectile prefabs.
**16 distinct external referrers.** Entitlement question stands (register Q4).

### C4 · `Assets/Shift - Complete Sci-Fi UI` — `keep`

Border textures used by **`Menu_Main.unity`** and **`Authentication.unity`** (both build scenes),
plus `ModalWindows.prefab` and `OptionsMenuContent.prefab`; `Switch.prefab` → `ModalWindows.prefab`.
Entitlement question stands (register Q3).

### C5 · `Assets/Effects Library` — `keep`

`Froglet Stuff/Prefabs/vfx_Projectile_02.prefab` is nested inside
`_Prefabs/Spacevessels/Components/Jet/VesselJet.prefab` — the vessel jet plume documented in
`Docs/VESSEL_TAIL_AND_JETS.md`. Provenance unresolved (register §Unknown provenance).

### C6 · `Assets/_Scripts/Game` — `keep`, **and CLAUDE.md is wrong about it**

CLAUDE.md:745 states the folder holds *"only non-code assets … All C# code has been reorganized"*.
**Measured, it holds 3 `.cs` files and two of them are live:**

| Asset | Referenced by |
|---|---|
| `Environment/CapsuleMembrane.cs` | `_Prefabs/Environment/CapsuleMembrane.prefab` |
| `Environment/CapsuleMembraneAnimationSO.cs` | `_SO_Assets/Cell Data/CapsuleMembraneAnimation.asset` |
| `Vessel/Animation/JetMaterial.mat` | **`_Prefabs/Spacevessels/Rhino.prefab`** |
| `Vessel/TrailPassives/ScoutTrailPrismConfig.asset` | **`_Prefabs/Spacevessels/Manta.prefab`** |

(`IO/_Input Mapping/InputActionsAsset.cs` has 0 serialized referrers — it is the generated wrapper
for `InputActionsAsset.inputactions`.)

**Verdict `keep`.** The doc line should be corrected; "vestigial" invites exactly the delete this
index exists to prevent. Flagged for the next doc-drift pass (**R8**'s successor) rather than
edited here, since this branch is an index.

### C7 · `Assets/PlayFabEditorExtensions` — `keep`

`includePlatforms: ["Editor"]`, all under `Editor/`. **Does not ship.** 4.9 MB of repository weight
only — a concern for **R15**, not for a build outsiders run.

---

## D — already gated elsewhere

### D1 · The eight vessel model vestiges

Fully documented in [`VESSEL_CONSTRUCTION.md` §4](VESSEL_CONSTRUCTION.md) with a salvage order in
[`VESSEL_CONSTRUCTION_FOLLOWUP.md`](VESSEL_CONSTRUCTION_FOLLOWUP.md). **Not re-derived here** — that
doc's Phase 0 asks for re-verification before acting, so this pass re-ran the referrer counts only:

| Model | Owners | Asset refs | `.meta` remaps |
|---|---|---|---|
| `dolphin_shapekey_with_animations.fbx` | 1 | 0 | 0 |
| `rhino_shapekey_with_animations.fbx` | 1 | 0 | 0 |
| `urchan_shapekey_with_animations.fbx` | 1 | **5** | 0 |
| `Vessel_Placeholder_1.fbx` | 1 | 0 | 0 |
| `Vessel_Placeholder_2.fbx` | 1 | 0 | 0 |
| `RhinoModel.fbx` | 1 | 0 | **2** |
| `Riptide.fbx` / `Dolphin_split.fbx` / `Hammerhead_split.fbx` | 1 | 0 | 0 |

Counts agree with `VESSEL_CONSTRUCTION.md`, so the tree has not moved. Two of the eight are **not**
"referenced by nothing", which the summary phrasing elides: `urchan_shapekey` is referenced by four
animator controllers plus one more (its takes are used by other vessels), and `RhinoModel` is
reached through `externalObjects` remaps in both placeholder `.meta` files.

**Verdict `salvage-first`**, unchanged — `dolphin_shapekey_with_animations` carries the Dolphin's
only real hull morph (10,909 verts) and is a salvage candidate, not a deletion candidate.

---

## What this index does not cover

Stated so the gaps are not mistaken for clean results.

- **Content-level secret scanning.** The credential sweep was filenames and known settings keys
  only (register §Credentials). No file *contents* were scanned for embedded keys.
- **Per-asset build-size attribution.** "Ships" here is derived from Unity's inclusion rules, not
  from a build report. A real `Editor.log` build breakdown from **R1** would give byte-accurate
  numbers and may find more.
- **Texture/audio import settings.** Oversized import settings on shipped art are a build-size
  concern this pass did not measure.
- **`Assets/_Graphics` (635 MB) and `Assets/_Audio` (253 MB)** — the two largest folders in the
  project. Both are first-party and out of scope for a third-party register, but they are where the
  build size actually is, and neither has been audited for duplicates or unused content.
- **The ParrelSync package asmdef** — see register §ParrelSync; unverifiable in this repository.
