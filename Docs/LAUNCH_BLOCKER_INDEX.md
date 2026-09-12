# Launch blocker index

Everything in `Assets/` that is a candidate for *"should not be in a build outsiders run"*, each
with **what references it (measured)**, **what breaks if it goes**, and a **verdict**.

**Measured 11 Sep 2026**, extended with a build-reachability sweep **12 Sep 2026**, against
`claude/zealous-davinci-vjwev0`. Re-verify before acting.

> **§A1, §A2 and §A3 were EXECUTED on 12 Sep 2026** (branch `claude/eloquent-fermi-2gkh1a`), so
> every number below that mentions them is now historical. What actually happened, and what each
> row got wrong, is in those three sections. Headline: **6.8 MB less reaches a player build**
> (reached 434.0 MB → **427.2 MB**), **144 assets removed** (7,526 → **7,382**), **31 fewer
> unconditionally-packed `Resources/` roots** (165 → **134**), and **64 scripts out of
> `Assembly-CSharp`** (34 TMP examples deleted, 30 NiceVibrations demo scripts made editor-only).
> The §E folder table and totals are updated for it; nothing else in §B–§E was touched.
>
> **Verification status: NOT verified in the Unity editor.** The branch was executed in a headless
> remote container with no `unity` binary and no Editor running, so `/verify-unity` could not be
> run and *"Menu_Main, the arcade screen and a gameplay scene all render text correctly"* has **not
> been observed on screen**. What was proved instead, offline and in full:
>
> * `TMP Settings.asset` — the asset that gates *all* text — resolves every reference it holds
>   (default font `LiberationSans SDF`, `EmojiOne`, the style sheet, both line-breaking tables),
>   and it lives in `TextMesh Pro/Resources/`, which this branch does not touch (§C1).
> * **All 13** TMP font assets referenced by any enabled build scene, `_Prefabs`, `_SO_Assets`,
>   `Resources` or `FTUE` resolve to a file on disk — including the two relocated ones, whose
>   referrers (`Manta.prefab`, `QuestItemPrefab.prefab`) still carry their guids. The
>   most-referenced font in the game is `ALDRICH-REGULAR SDF` (64 referrers), which lives in the
>   untouched `TextMesh Pro/Resources/`.
> * **Every dynamic font in the project resolves its source TTF** — all 3 of them, which is the
>   two relocated here plus `LiberationSans SDF - Fallback` (untouched, and the font TMP falls
>   back to for a missing glyph). The other 18 of 21 are `STATIC` with baked glyphs and no
>   source-font reference at all.
> * Zero new dangling guid references project-wide (see each section's proof).
> * All five out-of-editor gates green: conditional-compilation, enum-member, switch-label,
>   self-referential-local. `check_using_directives` reports 18 problems, **identical at the
>   pre-change HEAD `e6481619`** and in files this branch does not touch.
>
> **What that leaves genuinely unchecked** is only what a reference graph cannot answer: that the
> importer re-imports the five relocated assets cleanly with no *missing script* / *missing
> reference* entries in the console, and that the three named scenes visibly draw their text. Both
> want one editor pass. Recorded for the `/qa-backlog` scan rather than as a section in
> `UNITY_VERIFICATION_CHECKLIST.md`, per that file's own banner.

> **§E is the one to read first.** The folder-by-folder pass below sizes the problem at ~17 MB; the
> reachability sweep sizes it at **662 MB** and finds a **360 MB unreferenced texture pack** nobody
> had catalogued.

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
| **A1** | `Unity Assests/TextMesh Pro/Examples & Extras` | 7.1 MB | ✅ **DONE** — split, then removed |
| **A2** | `YethGameDev/QuickScenePro/Resources` | 1.8 MB | ✅ **DONE** — moved under `Editor/` |
| **A3** | `NiceVibrations/Demo` | 7.4 MB | ✅ **DONE** — asmdef editor-only, and the art is unwired (B1) |
| **A4** | `Wwise` | 84 KB | `remove` — zero files, zero references |
| **A5** | `Parse` | 76 KB | `remove` — zero references of any kind |
| **B1** | NiceVibrations demo art used by first-party UI | — | ✅ **RESOLVED** — unwired, no licence answer needed |
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
| **E1** | `_Graphics/Texture/Noise Texture Collection (Angelo)` | **360.6 MB** | `needs-a-human` — **zero reachable, no licence, no vendor** |
| **E2** | `_Graphics/Video` | 165 MB | `salvage-first` — reached only via a **retired** serialized field |

### Which prompt executes which row

| Rows | Prompt |
|---|---|
| **A1, A2, A3** (shipped `Resources/` folders + the Demo asmdef) | [`SHIPPED_RESOURCES_PRUNE_PROMPT.md`](prompts/SHIPPED_RESOURCES_PRUNE_PROMPT.md) — **executed 12 Sep 2026**; A3's art half remains open |
| **A3 art, B1** (NiceVibrations demo sprites + audio) | [`NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md`](prompts/NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md) |
| **A4, A5** (Wwise, Parse) | [`WWISE_AND_DEAD_SDK_REMOVAL_PROMPT.md`](prompts/WWISE_AND_DEAD_SDK_REMOVAL_PROMPT.md) |
| **B2** (PlayFabSDK, 20 code call sites) | [`PLAYFAB_RETIREMENT_PROMPT.md`](prompts/PLAYFAB_RETIREMENT_PROMPT.md) |
| **C3, C4** (PrimitivePlus, Shift) | [`VENDORED_UI_PACK_PLACEHOLDERS_PROMPT.md`](prompts/VENDORED_UI_PACK_PLACEHOLDERS_PROMPT.md) — **their `keep` verdicts are superseded**: the studio's decision is to replace both |
| **C5** (Effects Library) | [`EFFECTS_LIBRARY_PROVENANCE_PROMPT.md`](prompts/EFFECTS_LIBRARY_PROVENANCE_PROMPT.md) |
| **E1, E2** (the 360 MB noise pack, the video folder) | [`UNREFERENCED_ART_SWEEP_PROMPT.md`](prompts/UNREFERENCED_ART_SWEEP_PROMPT.md) |

**Rows with no prompt, because they are decisions rather than work:** **B3**
`MIgration_Prefabs (DELETE LATER)` (owner: whoever owns audit §02 — untouched deliberately), **B4**
the 14 orphan arcade cards (a product call on whether a card for an unbuilt mode is worth keeping),
**B5** the 6 that are still referenced (`keep` until their referrers are cut), **C6**
`_Scripts/Game` (**the CLAUDE.md line that called it vestigial is now corrected**), and **D1** the
vessel-model vestiges (already gated by `VESSEL_CONSTRUCTION_FOLLOWUP.md`).

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

> **Tooling added for these rows.** `Tools/Build/measure_dangling_guid_references.py` answers the
> one question every removal in this document needs answered — *did taking this out leave a
> reference pointing at nothing?* — and there was no committed tool for it. It is a **READER**
> (prints; writes only a snapshot JSON you name), it carries a negative control
> (`--self-test` injects a dangling reference and requires the diff to report NOT CLEAN), and it
> is kept rather than retired because **§A4, §A5, §E1 and §E2 are all still-pending removals that
> need exactly this proof**. Usage is in its docstring; §A1's proof block below is a worked
> example.

### A1 · `Assets/Unity Assests/TextMesh Pro/Examples & Extras` — ✅ **REMOVED 12 Sep 2026**

TMP's demo content, on the wrong side of two rules at once: `Examples & Extras/Resources/` is a
folder named `Resources`, so Unity packed it whole whether anything referenced it or not (rule 4,
3.5 MB), and the folder had no `.asmdef`, so its 34 example scripts compiled into
`Assembly-CSharp` and shipped too (rule 3).

**Done in two commits** — `e166b4c9` moved what shipped content needs, `e25547bc` removed the rest
(131 files, 34 of them `.cs`, 7.1 MB). Net: `Unity Assests` reached **13.9 MB → 8.8 MB**, 144
assets out of the project, 28 fewer `Resources/` roots. `TextMesh Pro/` now measures **12 MB / 62
files** and holds ONE `Resources/`; register §2's row is updated to match (its previous
"~15 MB / ~190" understated the size — 12 MB remain and 7.1 MB were removed).

#### What the plan above got wrong: it is FOUR assets, not two

The two font assets really are the only things first-party content references — re-swept, 0 other
external referrers — but each one **also needs its source TTF at runtime**, and both TTFs lived in
`Examples & Extras/Fonts/`:

| Moved | To | Why |
|---|---|---|
| `Resources/Fonts & Materials/Electronic Highway Sign SDF.asset` | `TextMesh Pro/Resources/Fonts & Materials/` | **`_Prefabs/Spacevessels/Manta.prefab`** |
| `Resources/Fonts & Materials/Bangers SDF.asset` | `TextMesh Pro/Resources/Fonts & Materials/` | `_Prefabs/UI Elements/Main Menu Screens/QuestItemPrefab.prefab` |
| `Fonts/Electronic Highway Sign.TTF` | `TextMesh Pro/Fonts/` | **source font — live runtime dependency** |
| `Fonts/Bangers.ttf` | `TextMesh Pro/Fonts/` | **source font — live runtime dependency** |
| `Fonts/Bangers - OFL.txt` | `TextMesh Pro/Fonts/` | that font's required SIL OFL notice |

Both font assets are `m_AtlasPopulationMode: 1` (**DYNAMIC**) with `m_GlyphTable: []`,
`m_CharacterTable: []` and `m_ClearDynamicDataOnBuild: 1` — they carry **no baked glyphs** and
build their atlas at runtime from `m_SourceFontFile`. Moving only the two `.asset` files and
deleting the folder would have left a shipped vessel prefab and the quest UI rendering **no glyphs
at all**, with both font references still resolving perfectly.

**And these were the only dynamic fonts in the folder being deleted.** Measured over all 21 TMP
font assets on disk (whole-file scan — a first pass that read only each asset's leading 4 KB
missed 5 of them and undercounted): **18 are `STATIC`** with baked glyphs and no source-font
reference of any kind, and **3 are DYNAMIC** — these two, plus `LiberationSans SDF - Fallback`,
which lives in `TextMesh Pro/Resources/` and was never at risk. So every font in the game that
cannot render without a TTF that this plan would have touched, it would have broken; the third
one it never reached. All 3 resolve their TTF today.

> **General rule this row now carries: a reference check tells you who points AT an asset, never
> what that asset needs to FUNCTION.** A guid sweep proved the two fonts were needed and said
> nothing about the four files needed to honour that. Before removing a folder, ask what the
> salvaged assets themselves depend on — read the asset, not just the graph around it.

#### The path meaning that did not change

A `Resources.Load` key is the path relative to the `Resources/` folder. Both fonts were at
`…/Resources/Fonts & Materials/<name>` and land at `…/Resources/Fonts & Materials/<name>`, so the
key `Fonts & Materials/<name>` is **byte-identical either side** — which is also the path
`TMP Settings.asset`'s `m_defaultFontAssetPath: Fonts & Materials/` uses to resolve a
`<font="X">` tag by name. Destination held no file of either name, so no collision and no
shadowing.

#### Both blind spots of a guid check, closed by hand

* **C# TYPE references.** All 34 class names (`Benchmark01`–`04`, `CameraController`,
  `ChatController`, `TeleType`, `VertexJitter`, `WarpTextExample`, the `TMP_*` helpers, …) grepped
  word-boundary against `_Scripts` and `FTUE`: **zero hits**. No first-party prefab or scene
  carried a demo script's guid either.
* **LOAD BY NAME.** Every `Resources`-relative key in the folder searched as a literal string
  across `_Scripts`, `FTUE`, `_Prefabs`, `_Scenes`, `_SO_Assets` and `Assets/Resources`: **zero
  hits**. The project *does* contain `<font="Anton SDF">` and
  `<sprite="DropCap Numbers" index=13>` tags — and **all of them were inside this folder's own
  example scenes**, which went with it.

#### Guid-count proof

Reproduce with **`Tools/Build/measure_dangling_guid_references.py`** (added by this branch, a
READER — see §A's tooling note below), snapshotting the merge base and this branch:

```
guids owned on disk        7526 -> 7382     (144 assets removed)
distinct unowned guids      444 ->  419
reference edges            2129 -> 1974

NEW unowned guids introduced ............ 0
NEW (guid -> referrer) edges ............ 0
edges removed ........................... 155
  of which the referrer was NOT under 'Examples & Extras': 0

VERDICT: clean - nothing outside the change lost a reference
```

The last line is the one that carries the proof: **every one of the 155 lost edges had its referrer
inside the folder being deleted**, so nothing outside it lost a reference. A falling total would
not have shown that on its own — a new dangle can hide behind a larger number of removals, which
is why the tool differences the SETS rather than comparing counts.

Two properties of that measurement are worth knowing before quoting it. **The absolute count is
not a defect count**: 444 is dominated by package guids that have no `.meta` in a clone
(`Library/PackageCache` is not checked in) — `TMP_FontAsset` is `71c1514a…`, shared by 21 font
assets including the keeper `LiberationSans SDF`, resolving from the builtin `com.unity.ugui`
2.0.0 that bundles TMP in Unity 6. Only the **delta** is the signal. And **an edge is keyed by the
referrer's own guid, never its path**, so a MOVE contributes no edge change at all — which is why
`NEW edges` is 0 here despite five assets changing path. A first, path-keyed cut of the tool
reported 24 phantom "lost" references for three QuickScene Pro demo scenes that had simply moved
one folder deeper.

#### Licence side effect, in the good direction

`Roboto-Bold SDF.asset` lived in that `Resources/` folder, so **Roboto shipped unconditionally
whether or not anything used it** — register §7's "ships if the font is used" was too generous for
that copy. It is gone, along with Anton, Oswald and Unity.ttf. `Bangers` still ships (via
`QuestItemPrefab`) and its OFL notice was deliberately preserved; `Electronic Highway Sign.TTF`
ships with **no notice in the tree**, which is pre-existing and register §7's problem.

### A2 · `Assets/YethGameDev/QuickScenePro/Resources` — ✅ **MOVED UNDER `Editor/` 12 Sep 2026**

QuickScene Pro is an **editor tool** and its code was already correctly under `Editor/`. Its
`Resources/` folder was not, so three editor-tool PNGs (`QSP_Icon.png`, `Icons/icon_additive.png`,
`Icons/icon_single.png`) were packed into every player build (rule 4).

**Done in `815c5890`** — `Resources/` → `Editor/Resources/` and `Demo/` → `Editor/Demo/`. Nothing
deleted. Result: `YethGameDev` reached **1.7 MB → 0.0 MB** with the asset count unchanged, so the
whole 4.4 MB vendored tool now reaches a player build **not at all**. The 3 assets that stopped
being reachable are exactly the 3 that were `Resources/` roots. Licence is MIT with the notice
present, so this was shipped bloat and never an entitlement problem.

#### Correction: the stated reason was the wrong reason

This row justified the move with *"the tool keeps working; `Resources.Load` still resolves for
editor code"*. That mechanism is real but **unused**: `QuickScenePro.cs` is the tool's only script
and contains **zero** occurrences of `Resources.Load`, `Texture`, or `icon` — it loads no image and
draws no icon. Its only asset access is `AssetDatabase.FindAssets("t:Scene …")` and
`GUIDToAssetPath`.

So the move is **safer** than this row claimed, for a different reason: nothing can break because
nothing loads them. Measured referrers — `QSP_Icon.png` is an `m_Sprite` on a UI Image in the three
demo scenes; the two `Icons/*.png` are referenced by **nothing anywhere in the project**. The
executing prompt's acceptance criterion ("the tool still opens and draws its icons") describes
behaviour the tool has never had.

> **General rule: a vendored tool's `Resources/` folder is not evidence the tool uses
> `Resources.Load`.** Asset Store tools ship one because it is the conventional place to put
> icons, not because anything loads from it — so check the call before writing the migration note
> that depends on it.

**Proof.** All three guids still uniquely owned; `DemoScene_1/2/3.unity` each still carry
`8efa9943…` because the guid travelled with the `.meta`. Project-wide dangling counts unchanged:
7382 guids owned, 419 distinct unowned, 1972 edges — identical before and after, because a move
that carries the `.meta` changes nothing about the graph.

### A3 · `Assets/NiceVibrations/Demo` — 7.4 MB, 30 `.cs` — ◑ **asmdef DONE 12 Sep 2026**

`Lofelt.NiceVibrations.Demo.asmdef` had `"includePlatforms": []`, so **30 demo scripts compiled
into the player**. `NiceVibrationsDemo.unity` is not in the build list, so the scene itself does not ship.

**But the demo ART is load-bearing in shipped UI** — measured, unique-guid-owner:

| Demo asset | Referenced by |
|---|---|
| `DemoAssets/RegularPresetsDemo/Sprites/RegularPresetsIcons.png` | **`_Scenes/Menu_Main.unity`** (build scene), `Arcade Screen.prefab`, `ModalWindows.prefab` |
| `.../RegularPresetsIcons_8_Flipped_Vertically.asset` | **`_Scenes/Menu_Main.unity`**, `Records Screen.prefab` |
| `DemoAssets/CarDemo/Sprites/NVCar.png` | `_SO_Assets/Classes/SO_Class_Termite.asset` |
| `_Common/Sprites/NV7Dots.png` | `_SO_Assets/Classes/SO_Class_Termite.asset` |

**Verdict `salvage-first`. The asmdef half is ◑ DONE (`1abf5066`, 12 Sep 2026); the art half is
still open.**

`Lofelt.NiceVibrations.Demo.asmdef` now reads `"includePlatforms": ["Editor"]` — a one-key change,
reversible, and the exact shape the same vendor already uses one folder over in
`Scripts/Editor/Lofelt.NiceVibrations.Editor.asmdef`. 30 demo scripts out of the player; all 30
remain on disk. The two sibling assemblies are untouched, including the shipping
`Lofelt.NiceVibrations`, which is the plugin the game actually uses.

Three ways the Demo assembly could have had a consumer, all measured **zero**: no `.asmdef` in
`Assets` or `Packages` names `Lofelt.NiceVibrations.Demo` or its guid `75b32e98…` in `references`;
no asset outside the Demo folder carries any of the 30 scripts' guids; no first-party `.cs` names
that namespace. `NiceVibrationsDemo.unity` is not in `EditorBuildSettings`, so only the code ever
shipped.

One caveat that came with it: that demo scene now sits **outside** `Editor/` while its components
compile into an editor-only assembly. Fine as things stand — it binds normally in the editor and
cannot reach a build — but the scene must not be added to `EditorBuildSettings`, or its scripts go
missing. The sibling `Lofelt.NiceVibrations.Editor` assembly has always had the same property.

**The art is untouched and re-verified as still load-bearing** (see the table above; all four
referrers confirmed present as of `1abf5066`). Deleting the folder is **not** safe — it would break
the main menu. See §B1: the right fix is probably to replace this art, and that is a licence
decision before it is an art task
([`NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md`](prompts/NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md));
retiring the plugin is
[`HAPTICS_VENDOR_INDEPENDENCE_PROMPT.md`](prompts/HAPTICS_VENDOR_INDEPENDENCE_PROMPT.md).

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

### B1 · Commercial demo art in the shipped main menu — ✅ **RESOLVED 12 Sep 2026, no answer needed**

The clearest exposure found: first-party UI drew sprites out of a **commercial asset's demo
folder**, one of them from `Menu_Main.unity`, an enabled build scene. It was **not** a question
about whether NiceVibrations is licensed but about whether *demo content* is licensed for
redistribution, which Asset Store EULAs treat separately from the plugin — a question for the
account holder, with an art lead time behind it.

**It was closed by removing the exposure instead of answering the question**, which is the cheaper
half of that pair and needed no legal input: the six references now point at first-party glyphs
under `_Graphics/UI/Chrome/` (`THIRD_PARTY_DECISIONS.md` row 5). A **seventh** exposure of the same
shape was found in the same pass and is worse — four `HapticSamples/*.wav` were wired into shipped
UI under a notice claiming **CC-BY-NC**, i.e. non-commercial (register row 4b); those are unwired
too. Zero shipped assets now reference anything under `Demo/` or `HapticSamples/`, proved over all
**411** guids those trees own by `Tools/Build/check_vendor_tree_references.py`.

**One holder remains and is deliberate:** `_Prefabs/MIgration_Prefabs (DELETE LATER)/` keeps 4 audio
and 1 sprite reference. It ships nothing and is §B3's open decision, so it was left untouched.

*The general rule this leaves: when a licence question has an expensive answer and a cheap
avoidance, price the avoidance first — replacing four sprites and four sounds cost one branch, and
the question no longer has to be asked.*

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

CLAUDE.md's `Project Structure` note on `_Scripts/Game/` (`:745` — a hint; re-grep before trusting
the number) stated the folder holds *"only non-code assets … All C# code has been reorganized"*.
**Corrected on this branch**; the paragraph below is the measurement behind that correction.
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

## E — what ACTUALLY ships, measured (12 Sep 2026)

The first pass of this index worked folder by folder. That answers *"is this folder referenced?"*
and not *"does this reach a build"*, so it sized the problem at **~17 MB**. A transitive
reachability sweep from the real build roots sizes it at **662 MB**, and finds two items that
outrank everything in §A–§D.

### The method

Measured by **`Tools/Build/measure_build_reachability.py`** (`--self-test` asserts the model reaches
four assets that must be reachable; `--list <path>` enumerates what a folder leaves unreached).
**That self-test is negative-controlled**, because a green gate is only evidence if you can name a
failure it produced: removing the `Resources/` root class (196 roots → 31) makes the `TMP Settings.asset`
probe fail, which is the one probe that exists to prove the load-by-name blind spot is closed rather
than dodged. Its roots are Unity's real inclusion rules (register §1):

1. every **enabled** scene in `EditorBuildSettings.asset` — **29 of 31**;
2. every asset under any folder named `Resources/` **not** under an `Editor/` folder — **165 assets**
   (this is what makes `Resources.Load` by name a non-issue: the whole folder is a root);
3. **preloaded assets** in `ProjectSettings.asset` — 2.

Then guid references are followed transitively. Result: **2,948 of 7,526 assets reachable**.
Over every asset that is **441.4 MB reachable against 925.3 MB not**; excluding the two documented
false-positive classes below (code, native plugins) it is **434.0 MB against 662.3 MB**. The second
pair is the honest headline.

**Validated before use.** Six assets that must be reachable all are, including the two the method
section names as blind spots: `TMP Settings.asset` (reached *as a Resources root*, which is the
point), `Manta.prefab`, `Sparrow.prefab`, `GameCanvas.prefab`, `VesselGraph.shadergraph`,
`ControlGlyphSet.asset`.

### Three limits, stated so the number is not over-read

| Limit | Consequence |
|---|---|
| **Code ships regardless of references** | A `.cs` with no guid referrer still compiles into `Assembly-CSharp`. `_Scripts`' 1,306 "unreached" files are **not** build weight — they may be dead *code*, which is a different audit. |
| **Native plugins ship by platform importer settings, not by guid** | `Plugins`' 252 MB "unreached" is FMOD's per-platform binaries. Only the target platform's ship, and no guid reaches any of them. **Not a finding.** |
| **A RETIRED serialized key still greps as a reference** | The sweep reads YAML text, so a field the script no longer declares still looks like a live edge. Unity drops it at import. This over-reports — see §E2, where it pulled 110 MB of video into "ships" through a field that no longer exists. |

Also unmodelled: **Always Included Shaders** in `GraphicsSettings`, and per-platform texture
compression (a `.png`'s bytes on disk are not its bytes in the build).

### Reachability by top-level folder

| Folder | Reachable | Not reached | Unreached files |
|---|---|---|---|
| `_Graphics` | 139.9 MB | **483.1 MB** | 856 |
| `_Prefabs` | 7.9 MB | 44.8 MB | 178 |
| `_Audio` | 208.3 MB | 44.0 MB | 24 |
| `NiceVibrations` | 0.3 MB | 43.3 MB | 384 |
| `_Models` | 47.9 MB | 19.6 MB | 63 |
| `Effects Library` | 1.1 MB | 6.8 MB | 20 |
| `FTUE` | 0.0 MB | 4.5 MB | 36 |
| `PlayFabEditorExtensions` | 0.0 MB | 4.4 MB | 70 |
| `PlayFabSDK` | 0.0 MB | 3.8 MB | 102 |
| `Unity Assests` | 13.9 MB → **8.8 MB** | 2.9 MB → **1.8 MB** | 108 → **26** |
| `YethGameDev` | 1.7 MB → **0.0 MB** | 2.5 MB → **4.2 MB** | 7 → **10** |

`Plugins` and `_Scripts` are omitted per the limits above.

**Re-measured 12 Sep 2026 after §A1–§A3.** The two arrowed rows are the only ones this branch
moved. Project totals: **2,948 of 7,526 assets reachable → 2,896 of 7,382**; **434.0 MB reached →
427.2 MB**; `Resources/` roots **165 → 134**. (Re-run after merging `bleeding-edge`, which landed
the credits screen: **2,898 of 7,386** — the credits manifest is itself a `Resources/` root, so it
and what it reaches are the +2. *A measurement quoted into a doc is a derived value: this branch's
own numbers moved once between measuring and shipping.*) Unreached rose 662.3 → 663.0 MB *because
QuickScene Pro moved out of "reached" rather than out of the project* — a move, not a deletion, so
its bytes changed column. `--self-test` green throughout, including the `TMP Settings.asset`
Resources-root probe, which is the one that proves the load-by-name blind spot is still closed.

### E1 · `Assets/_Graphics/Texture/Noise Texture Collection (Angelo)` — **360.6 MB, 109 files, ZERO reachable**

**The largest single item in the project, and nothing can reach any of it.** 109 PNGs (4K noise
tiles: Cells, Vines, Swirls, Waves, Geometric, Boxes), **360.6 MB**, of which **0 files** are
reached from any build root. It is **26% of `Assets/`** — more than twenty times the entire §A
section of this index.

It is also a **third-party register gap**: no licence file, no readme, no vendor, and a folder name
naming a person. The register never caught it because it scoped `_Graphics` out as first-party.

**Verdict `needs-a-human`, then almost certainly `remove`.** Two questions, in order: *who is
Angelo and what were the terms?* (provenance — the same question §6 of the register asks of
`Effects Library`), and *is anyone about to use it?* An unreferenced 360 MB texture pack is a
repository-weight problem rather than a build-size one, but it is the one worth asking about first.

### E2 · `Assets/_Graphics/Video` — 165 MB reached only through a **RETIRED** field

The sweep reported 110.7 MB of video as shipping. **It is not**, and the reason is worth more than
the number.

40 `SO_ArcadeGame` assets still carry a serialized **`PreviewClip:`** key pointing at a
`*Preview_Prefab.prefab`. **`SO_ArcadeGame` no longer declares that field** — it declares
`PreviewVideo` — and `SO_Game.PreviewClip` was
deleted when the arcade preview became a live satellite arena (`Docs/ModePreview/ARCHITECTURE.md`,
which states the window *"must never fall back to a video"*). Unity never prunes an unresolvable
serialized key, so the YAML still names the guid and a text-based sweep still follows it.

What is actually live: **one** card (`ArcadeGameMaelstrom`) wires a non-null `PreviewVideo`, read by
the single consumer `MaelstromLaunchPanel`. The other 39 `PreviewVideo` fields are null. A separate
path — `SO_VesselAbility.PreviewClip`, a **`VideoPlayer`** reference read by `HangarAbilitiesView` —
covers the per-ability videos and is **not** verified here: **24 `SO_VesselAbility` assets carry a
live one** (plus 1 `SO_Mission` asset whose type declares neither, a third dead key). So the field
NAME is not dead anywhere in the project — only its use on `SO_ArcadeGame` is, which is why the
check has to be *which type owns the asset*, not *does this identifier exist*.

**Verdict `salvage-first`.** Establish which clips the Hangar path still needs, keep those and the
Maelstrom clip, and remove the rest along with the dead `PreviewClip:` keys. General rule:
**a retired serialized field is invisible to the compiler, invisible to the inspector, and still
visible to every text-based tool** — including this index's own sweep.

### What the reachability sweep does NOT license

It says an asset is unreachable, not that it is unwanted. The salvage-before-delete gate at the top
of this document applies to every row above exactly as it does to §A–§D, and `_Graphics`/`_Audio`
are first-party content where "nobody references it yet" is a normal state for work in progress.
**Ask before removing first-party art.**

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
