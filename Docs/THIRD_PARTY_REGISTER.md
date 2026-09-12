# Third-party register — every SDK in the project

Every third-party SDK, library, plugin and package in `Assets/` **and** `Packages/`, with whether a
licence document is actually in the tree, whether it reaches a player build, and the entitlement
question a human has to answer.

**Measured 12 Sep 2026** against `claude/zealous-davinci-vjwev0`. Re-verify before acting.

> **This register records what is PRESENT. It cannot record what was PAID FOR.** The repository
> proves a folder exists; only a purchase record proves we may distribute it. Every entitlement
> question below is addressed to **whoever holds the studio's Unity Asset Store and middleware
> accounts**. Nothing here asserts a licence status that is not backed by a document in the tree.

Companions: **[`THIRD_PARTY_DECISIONS.md`](THIRD_PARTY_DECISIONS.md)** — what the studio has decided
to do about each item, the money list, and the prompt that executes it.
**[`LAUNCH_BLOCKER_INDEX.md`](LAUNCH_BLOCKER_INDEX.md)** — what should not ship, with a measured
reference check per candidate.

---

## §0 · The short answer — what a human has to resolve

Ordered by how much trouble it causes, not by size. **Owner decisions recorded 12 Sep 2026** — the
work each one implies is sequenced in **[`THIRD_PARTY_DECISIONS.md`](THIRD_PARTY_DECISIONS.md)**.

| # | Item | State | What has to happen | Decision |
|---|---|---|---|---|
| **1** | **FMOD in-game credit** | ✅ **Built 12 Sep 2026** | Clause 3 of the bundled EULA: *"All Products require an in game credit line which must include the words 'FMOD' and 'Firelight Technologies Pty Ltd.'"* — **all tiers, no exemption.** An in-game credits screen now carries it (`Assets/Resources/CreditsManifest.asset` → `MIDDLEWARE`), reachable from the settings panel, and **two gates keep it there**: `CreditsReleaseGuard` fails any non-development build without it, and `Tools/Build/check_credits_manifest.py --check` answers offline. ⚠️ **Not yet opened in the editor** — the data is proved, the rendering is not. Evidence: §7. | ✅ **Done** — `prompts/CREDITS_SCREEN_PROMPT.md` |
| **2** | **FMOD licence tier** | ⚠️ Threshold question | Free commercial use requires **dev budget < $600k USD** *and* **gross revenue/funding < $200k USD/yr**. Froglet Inc. is a C-corp shipping paid Steam EA — a human must confirm which side of both thresholds applies, and buy the tier if not. | 💰 **Buy if the thresholds say so** |
| **3** | **Obvious SOAP 2.7.0** | ⚠️ **Commercial, no EULA in tree** | Its own README ends *"Thanks for purchasing Soap :)"* — so it was bought, by someone, at some point. **No licence file exists anywhere in `Assets/Plugins/Obvious/`.** It is the project's **primary architecture** (128 first-party `using Obvious…`), so this is not removable — it needs the receipt. | ✅ **Owner is adding the licence file** |
| **4** | **NiceVibrations 4.1.1** | ⚠️ Commercial, **no product EULA** | The two files that look like a licence are not one — see §2. Seat must cover commercial distribution. | ✅ **Code coupling REMOVED** (12 Sep) — `HapticController` now drives `UnityEngine.InputSystem.Gamepad.SetMotorSpeeds` through first-party `GamepadRumblePlayer` / `GamepadRumblePattern`; **zero** `Lofelt` references remain in `Assets/_Scripts`. Every clip was already first-party data, so no feel changed. The folder still ships, but **nothing first-party reaches it** now that rows 4b/5 have landed too; deleting `Assets/NiceVibrations/` is a separate final commit with its own reference proof (`Tools/Build/check_vendor_tree_references.py`). |
| **4b** | **NiceVibrations `HapticSamples/*.wav` in shipped UI** | ⚠️ **Non-commercial licence claim** | **New finding, 12 Sep.** Four clips are wired into shipped UI as legacy `AudioClip` fields — `Beep1` (`CountdownTimer.countdownBeep`), `Cash5` (`onTriggerClip`), `Coins2` (`targetReachedClip`), `Keyboard3` (`TypingAudio` on `Profile`/`ModalWindows`/`Menu_Main`). Their notice **opens by declaring the pack "licensed under CCBYNC 3.0"** — *non-commercial* — while its source list is almost all CC0 and **names freesound titles, not shipped filenames**, so no clip maps to a term. | ✅ **Done 12 Sep** — all four migrated to FMOD `EventReference` fields on the components that make them (`CountdownTimer.countdownBeepEvent`, `ProfileModal.typingAudioEvent`, `IconEmitter.onTriggerEvent`/`targetReachedEvent`). **Shipped empty, so all four are silent** until the audio owner authors the events (`AudioSystem/CHARLES_TASKS.md` C9). `IconEmitter` also stopped calling `AudioSource.PlayOneShot`, so these now respect the SFX slider. Zero shipped references remain. |
| **5** | **NiceVibrations demo ART is in the shipped menu** | ⚠️ Distribution question | `RegularPresetsIcons.png` → `Menu_Main.unity` (a build scene). Asset Store EULAs treat demo content separately from the plugin. **4 distinct sprites, 6 usages** — two of them (`NVCar`, `NV7Dots`) are the **Termite class icons**, i.e. already placeholders for an unimplemented vessel. | ✅ **Done 12 Sep** — re-pointed to first-party glyphs under `_Graphics/UI/Chrome/`, authored by `Tools/Build/author_ui_chrome_icons.py` (`--check`), which shares its drawing engine with the objective-icon set so the two cannot drift. **New assets under a first-party PATH, not an in-place edit of the vendor PNG** — an in-place edit works and hides itself, leaving the path still saying `NiceVibrations/Demo/…` for the next audit to re-report and the next plugin re-import to revert. The two Termite slots take a neutral placeholder; real art is a design task. Zero shipped references remain. |
| **6** | **Shift Sci-Fi UI (Michsky)**, **PrimitivePlus** | ✅ **RESOLVED — both removed 12 Sep 2026** | Replaced first-party and deleted, in two commits each carrying a per-asset guid-ownership proof (`0` inbound references, `0` ambiguous owners, counted over `Assets/` + `ProjectSettings/` + `Packages/`). Shift's 3 frames → `Assets/_Graphics/UI/Frames/` (`Tools/Build/author_ui_frame_sprites.py`); PrimitivePlus's 4 meshes → `Assets/_Models/Primitives/` (`Tools/Build/author_primitive_meshes.py`). Both replacements were **measured against the packs before deletion** — see §2.1. | ✅ **Done** |
| **7** | **"Effects Library"** | ❓ Unknown vendor | Contains only `Froglet Stuff/`, but carries an `FE_` import prefix. Whose is it? **Measured: 8.2 MB of which exactly ONE prefab is live** (`vfx_Projectile_02` → `VesselJet`); the four `FE_*` render-pipeline assets are **not** the live pipeline (0 references). | 🔍 **Owner will identify** — `prompts/EFFECTS_LIBRARY_PROVENANCE_PROMPT.md` |
| **8** | **Wwise**, **Parse**, **SerializeInterface** | ✅ **Done — all three gone from the tree** | **Landed 12 Sep 2026.** `Assets/Wwise/` (0 asset files, 14 orphan `.meta`) and `Assets/Parse/` (2 DLLs, 0 references) **deleted**, each with its own guid-ownership + inbound-holder + type-reference + `Resources.Load`-by-name proof re-measured at deletion time. `SerializeInterface` **rewritten first-party** into `CosmicShore.Utility` / `CosmicShore.Editor`, all seven `[RequireInterface]` lines byte-identical. **Five prose residue sites** were swept, not the two the brief predicted — and the two extra **never name the vendor**: `VesselAbilityRowWirer`'s alias-convention comment cited the deleted `InterfaceReference.cs`, and this register's own sweeper tool offered `--tree Assets/Wwise` as its usage example. *A grep for the vendor's name finds only the residue that mentions it; sweep the deleted paths and type names too.* ⚠️ **One human item is still OPEN and it is NOT a code question:** whether a Wwise **evaluation or project licence** was ever signed during that evaluation — see §2 of [`THIRD_PARTY_DECISIONS.md`](THIRD_PARTY_DECISIONS.md). Deleting the folder does not settle it, and did not wait on it. | 🗑/🔄 **Executed** |
| **9** | **CC-BY 4.0 models** | ✅ Not shipping today | Two Sketchfab models by **VertaScan** under CC-BY 4.0 (`_Models/Vessel Models/Placeholder/`). Measured **0 references** — they do not ship, and they are **deliberately absent from the credits screen**, because crediting an asset the build does not contain is a false statement about the build. | ⏸ Watch — the trigger is recorded in `VESSEL_CONSTRUCTION_FOLLOWUP.md`; wiring one makes the attribution mandatory and the credits screen is where it goes |
| **10** | **EmojiOne**, **Roboto**, **Liberation Sans** | ✅ **Credited 12 Sep 2026** | All three sat under a `Resources/` folder, so they **shipped whether referenced or not** (Roboto's TMP asset has since gone with `Examples & Extras`, index §A1 — the entry stays earned by NiceVibrations' `RobotoMono-*.ttf`) — the register previously said *"ships if used"* for the first two and did not list Liberation Sans (TMP's default font) at all. Each now has its own entry: EmojiOne's own terms, Roboto under Apache 2.0, Liberation Sans under SIL OFL 1.1. | ✅ **Done** — see §7 |
| **11** | **Bangers**, **Electronic Highway Sign** | ❌ **Ship, not credited** | **New finding, 12 Sep.** Pruning TMP's `Examples & Extras` found two fonts reachable from shipped prefabs — `Bangers SDF` ← `QuestItemPrefab`, `Electronic Highway Sign SDF` ← `Manta.prefab` — both **DYNAMIC**, so each needs its TTF at runtime and both were relocated rather than deleted (index §A1). Bangers is SIL OFL 1.1 and its notice was preserved beside it; **Electronic Highway Sign has no licence document anywhere in the repository.** | ➕ **Add a Bangers entry to `CreditsManifest.asset`** (an asset edit). 🔍 **Owner: where did Electronic Highway Sign come from?** — nothing can be credited until that is answered |

---

## §1 · How "ships / editor-only" was determined

Not by folder name. Unity's actual rules, applied in order:

1. Anything under a folder named **`Editor/`** compiles to `Assembly-CSharp-Editor` and **never** ships.
2. An **`.asmdef`** with `"includePlatforms": ["Editor"]` is editor-only; `[]` compiles into the player.
3. **Code with no `.asmdef`** falls into `Assembly-CSharp` and **ships**.
4. **Any folder named `Resources/`** is packed into the player **whole, referenced or not** — unless it sits under an `Editor/` folder.
5. Everything else ships only if one of the **29 enabled build scenes** reaches it.

Rule 4 is the one that produces defects nobody notices; it produced two (index §A1, §A2). **Both were fixed on 12 Sep 2026** — §A1's folder removed, §A2's `Resources/` moved under `Editor/` — and rule 2 was fixed with them (§A3's Demo asmdef). A third rule-4 folder survives and is untouched: `NiceVibrations/Scripts/Components/Resources`.

---

## §2 · Vendored SDKs under `Assets/`

Sizes are `du -sh`; file counts exclude `.meta`.

| SDK | Vendor | Version | Path | Size / files | Licence in tree | Ships? |
|---|---|---|---|---|---|---|
| **FMOD for Unity** | Firelight Technologies | **2.03.x** *(from bundled doc URL)* | `Plugins/FMOD` | 261 MB / 196 | ✅ `LICENSE.txt` (**EULA — see §0.1, §0.2**) | **Ships** — 3 runtime asmdefs + `Resources/` |
| ├ **Resonance Audio** | **Google** (bundled inside FMOD) | not stated | `Plugins/FMOD/addons/ResonanceAudio` | — | ❌ **none for the addon** | **Ships** (`FMODUnityResonance` runtime asmdef) |
| └ **FMOD Haptics addon** | Firelight | not stated | `Plugins/FMOD/addons/Haptics` | — | ❌ none for the addon | **Ships** (`FMODUnityHaptics` runtime asmdef) |
| **Obvious SOAP** ⚠️ | **Obvious Games** (`com.obvious.soap`) | **2.7.0** | `Plugins/Obvious` | 7.1 MB / 149 | ❌ **none** — README says *"Thanks for purchasing"* | **Ships** (`Obvious.Soap` runtime asmdef) |
| **NiceVibrations** ⚠️ | Lofelt / More Mountains | **4.1.1** (Lofelt SDK 1.3.3) | `NiceVibrations` | 47 MB / 395 | ⚠️ **not the product EULA** — see below | **Ships**, but nothing first-party reaches it any more (12 Sep). The **Demo**'s 30 `.cs` were made **editor-only** (index §A3); no first-party CODE calls the plugin (row 4); and no first-party ASSET references it (rows 4b/5) — measured by sweeping all **411** guids owned under `Demo/` and `HapticSamples/` against every shipped scene, prefab and asset. The only holders left are in `_Prefabs/MIgration_Prefabs (DELETE LATER)/`, which ships nothing and is an open decision (`LAUNCH_BLOCKER_INDEX.md` §B3). Still shipping regardless of references: the plugin's own assemblies and `Scripts/Components/Resources`. **The folder is now deletable on its own evidence.** |
| **DOTween** (free — **not** Pro) | Demigiant / D. Giardini | asm `1.0.0.0`; no product version in tree | `Plugins/Demigiant` | 764 KB / 18 | ❌ none | **Ships** (`DOTween.dll`) |
| **NativeShare** | yasirkula | not stated | `Plugins/NativeShare` | 148 KB / 10 | ❌ none | **Ships** (`NativeShare.Runtime`) |
| **PlayFab SDK** | Microsoft | not stated | `PlayFabSDK` | 4.7 MB / 104 | ❌ none | **Ships** — `PlayFab.asmdef` `includePlatforms: []` + `Resources/` |
| **PlayFab Editor Extensions** | Microsoft | not stated | `PlayFabEditorExtensions` | 4.9 MB / 70 | ❌ none | **Editor-only** ✅ |
| ├ **Microsoft.Identity.Client (MSAL)** | Microsoft | not stated | `…/Editor/Resources/` | — | ❌ none | Editor-only |
| ├ **Microsoft.IdentityModel** (`.JsonWebTokens`, `.Logging`, `.Tokens`) | Microsoft | not stated | `…/Editor/Resources/` | — | ❌ none | Editor-only |
| └ **System.IdentityModel.Tokens.Jwt** | Microsoft | not stated | `…/Editor/Resources/` | — | ❌ none | Editor-only |
| **QuickScene Pro** | yethgamedevv | © 2025 | `YethGameDev` | 4.4 MB / 10 | ✅ `LICENSE.md` — **MIT** (+ Indian jurisdiction clause) | **Editor-only since 12 Sep 2026** — `Resources/` and `Demo/` moved under `Editor/` (index §A2), so none of it now reaches a build |
| ~~**PrimitivePlus**~~ | *unknown* (ns `PrimitivePlus`) | not stated | ~~`PrimitivePlus`~~ | ~~1008 KB / 53~~ | ❌ none | ✅ **REMOVED 12 Sep 2026** — see §2.1 |
| ~~**Shift — Complete Sci-Fi UI**~~ | **Michsky** (ns `Michsky.UI.Shift`) | not stated | ~~`Shift - Complete Sci-Fi UI`~~ | ~~584 KB / 15~~ | ❌ none | ✅ **REMOVED 12 Sep 2026** — see §2.1 |
| **External Dependency Manager (EDM4U)** | **Google** | **1.2.183** | `ExternalDependencyManager` | 816 KB / 9 | ✅ `LICENSE` (**Apache 2.0**) | **Editor-only** ✅ |
| **Microsoft.Unity.Analyzers** | Microsoft | not stated | `Analyzers` | 256 KB / 2 | ❌ none (`README.md` only) | Roslyn analyzer — compile-time |
| **TextMesh Pro** (imported assets) | Unity | bundled | `Unity Assests/TextMesh Pro` | **12 MB / 62** | — (Unity UCL) | **Ships** — now ONE `Resources/`; `Examples & Extras` (incl. its second `Resources/` and 34 `.cs`) removed 12 Sep 2026, index §A1 |
| ├ **EmojiOne** sample sprites | EmojiOne | — | `…/TextMesh Pro/Sprites` | — | ⚠️ `EmojiOne Attribution.txt` — defers to their terms | Ships if used |
| └ **Bangers** + **Electronic Highway Sign** fonts | Google Fonts / unknown | — | `…/TextMesh Pro/Fonts` | — | ✅ `Bangers - OFL.txt` · ❌ **none for Electronic Highway Sign** | **Ship** — both are DYNAMIC TMP fonts referenced by shipped prefabs, so each carries its TTF. Relocated here 12 Sep 2026 from `Examples & Extras`, whose **Roboto** went with the folder (index §A1) |
| ~~**Parse** (support DLLs)~~ | Parse / Meta, via **Google EDM4U** (`gvh_version-9.4.0`, .NET 3.5 — recovered from the `.meta` labels) | not stated | ~~`Parse`~~ | ~~76 KB / 2~~ | ❌ none | **DELETED 12 Sep 2026.** Was 0 references; both DLLs were additionally importer-disabled (`Any: enabled: 0`, `Editor: enabled: 0`) so they reached no target at all |
| ~~**Wwise**~~ | Audiokinetic | — | ~~`Wwise`~~ | ~~84 KB / **0 files**~~ | — | **DELETED 12 Sep 2026.** Shipped nothing (14 `.meta`, 0 assets). The per-title licence question is §0 row 8 and is a human item, not a tree item |
| **SerializeInterface** → **first-party** | ~~unknown~~ → **Froglet** | — | `_Scripts/Utility/RequireInterfaceAttribute.cs` + `_Scripts/Editor/RequireInterfaceDrawer.cs` | 48 + 219 lines | n/a — first-party | **Ships** (the attribute only; the drawer is under `Editor/`). Rewritten 12 Sep 2026; `Assets/SerializeInterface/` deleted |
| **Shader Graph samples** | Unity | 12.1.6 | `Samples` | 972 KB / 14 | — (Unity UCL) | Ships if referenced |
| **"Effects Library"** ❓ | **unknown — §5** | — | `Effects Library` | 8.2 MB / 25 | ❌ none | **Ships** — nested in `VesselJet.prefab` |
| **CC-BY vessel models** | **VertaScan** (Sketchfab) | — | `_Models/Vessel Models/Placeholder` | 11.6 MB / 2 | ✅ `CC_Attribution_…txt` — **CC-BY 4.0** | **Not shipping** (0 refs) — see §0.9 |

**The NiceVibrations licence trap, stated precisely.** Two files look like a licence and neither is
the product EULA:
- `3RD-PARTY-LICENSES.md` is the **Rust crate list** for the Lofelt Studio SDK (addr2line, adler, aho-corasick, …).
- `HapticSamples/HapticPackCClicense.txt` covers the **haptic sample clips** only.

The entitlement document for the plugin itself is **absent**. *(The 10 Sep starting inventory
recorded this component as "licence file present" — measured, it is not.)*

**And `HapticPackCClicense.txt` is itself a problem, not just a non-licence.** It opens *"licensed
under CCBYNC 3.0"* — **non-commercial** — over a pack whose four clips (`Beep1`, `Cash5`, `Coins2`,
`Keyboard3`) are wired into **shipped UI** as plain `AudioClip` fields. Its ~100-entry source list is
almost entirely CC0 with one Attribution item, so the file contradicts its own header; and it names
**freesound source titles rather than shipped filenames**, so no clip in the tree can be mapped to a
term. There is no reading under which a paid release is provably clear. Disposition: §0 row 4b.

✅ **Resolved 12 Sep — and the finding stays on this page deliberately.** All four clips are
unwired; zero shipped assets reference anything under `HapticSamples/`. The file is still in the
tree (the plugin folder is deleted separately), so the trap is still *present* even though nothing
is exposed to it any more, and a finding deleted the moment it stops biting is one the next audit
re-discovers from scratch. **The fix was not a licence argument**: CLAUDE.md's audio convention
already says `AudioClip` + `AudioSource` is legacy and new SFX is FMOD, so the four call sites moved
to FMOD `EventReference` fields — finishing a migration the project had already committed to.
The events ship **empty and silent** on purpose (`CHARLES_TASKS.md` C9); pointing them at a
borrowed event to keep a noise would have been an invisible TODO.

---

## §2.1 · Removed — Shift Sci-Fi UI and PrimitivePlus (12 Sep 2026)

Both packs are **gone from the tree**. Neither had a licence document, a vendor identifiable from
the tree, or a recoverable purchase record, and — the part that made replacing cheaper than chasing
a receipt — both shipped a `Resources/` folder into the player whole, so **44 PrimitivePlus meshes
shipped for the 4 that were used** and Shift's `Shift UI Manager.asset` shipped for nothing.

**What replaced what, and what it cost.** Neither pack had a single first-party code reference, so
the whole dependency was art:

| was | is | authored by |
|---|---|---|
| `PrimitivePlus/Resources/Meshes/{Cone,Sphere,Cube,CylinderTube}.asset` | `Assets/_Models/Primitives/Primitive{Cone,Sphere,Cube,CylinderTube}.asset` | `Tools/Build/author_primitive_meshes.py` |
| `Shift …/Cut Frame Big - 6px (200ppu).png` | `Assets/_Graphics/UI/Frames/frame_cut_outline_200.png` | `Tools/Build/author_ui_frame_sprites.py` |
| `Shift …/Cut Frame Filled Big (200ppu).png` | `…/frame_cut_filled_200.png` | ″ |
| `Shift …/Cut Frame Filled Big (300ppu).png` | `…/frame_cut_filled_300.png` | ″ |
| `PrimitivePlusMaterial` on 3 prefabs | *removed* — an editor-authoring helper, inert at runtime | — |
| `Switch.prefab` instance in `ModalWindows.prefab` | *removed* — inactive, in an unreferenced prefab | — |

**The replacements were MEASURED against the packs before deletion**, which is the only window in
which that can be done — both tools carry a `--verify-vendor` mode that now stands down with
"vendor asset is gone" rather than aborting, and a `--self-test` that proved the comparison FAILS on
a mesh or a frame that really differs.

| replacement | result |
|---|---|
| `PrimitiveCone` | surface **IDENTICAL**, max normal delta 0.0159°, **max UV delta 0.000001** |
| `PrimitiveSphere` | surface **IDENTICAL**, 0.4429°, 0.003906 (= 1/256) |
| `PrimitiveCube` | surface **IDENTICAL**, 0.0000°, 0.000000 |
| `PrimitiveCylinderTube` | surface **IDENTICAL**, 0.0175°, UVs not reproduced *(see below)* |
| `frame_cut_filled_200/300` | coverage **−0.008 %**, **0** of 16384 px off by >0.25, max \|alpha\| 0.022 |
| `frame_cut_outline_200` | coverage **−0.159 %**, **0** of 16384 px off by >0.25, max \|alpha\| 0.103 |

"Surface IDENTICAL" means the triangle coverage and winding agree exactly, compared per supporting
plane so a free triangulation choice does not read as a difference while a curved quad's diagonal
still has to match; vertex clusters agree within 1.2e-06 world units. The two non-zero UV deltas are
deliberate and neither reaches a shader: the **Sphere**'s vendor UVs sit on a k/256 grid (an export
quantiser upstream of an otherwise Float32 asset) and the first-party asset emits the analytic
values those are a rounding of; the **CylinderTube**'s are a modelled non-uniform unwrap that is not
reproduced, which nothing can see because its one consumer wires a **null material** and also uses
the mesh as a non-convex `MeshCollider`.

**`Cone.asset` was the gameplay-critical one** — it is the mesh on `AOEConicExplosion` (the
Dolphin's crystal blast) and `AOEConicSkyBurst` (the Sparrow's skyburst), and its material's shader
`LaserGraph.shadergraph` carries a `UVNode` feeding two `SampleTexture2DNode`s, so UV0 really does
reach the screen. Measured before → after:

    bounds   (−0.5000001, −0.5000001, −0.5000004) → (−0.5, −0.5, −0.5)
             (+0.4999999, +0.5000001, +0.4999997) → (+0.5, +0.5, +0.5)
    pivot    +1e−07 off the bounds centre          → exactly the bounds centre
    axis     base centre → apex = +Y (to 6e−07)    → +Y exactly
    apex (0, +0.5, 0) · base plane y = −0.5 · ring radius 0.5 · 20 segments — unchanged

**Proof of removal**, per asset, by the guid-ownership method (a unique owner asserted per `.meta`,
never `grep -rl | head -1`), counted over `Assets/` + `ProjectSettings/` + `Packages/`:

    Assets/PrimitivePlus                 53 assets · all UNIQUE · 0 inbound · 0 ambiguous
    Assets/Shift - Complete Sci-Fi UI    15 assets · all UNIQUE · 0 inbound · 0 ambiguous

A guid sweep has two blind spots (`LAUNCH_BLOCKER_INDEX.md` §"blind spots"): it cannot see
`Resources.Load` **by name**, and it cannot see a **C# type** reference. Both were closed by hand —
the project's complete `Resources.Load` literal list is 14 names, none of them either pack's, and no
first-party `.cs` mentions `PrimitivePlus`, `Michsky`, `SwitchManager`, `UIManagerImage` or
`UIElementSound`. Nothing outside the packs named them by path, and neither appeared in preloaded
assets or the always-included shader list.

**One thing did NOT become recoverable.** `Switch.prefab` carried two `m_Script` guids owned by no
`.meta` under `Assets/` — `fe87c0e1cc204ed48ad3b37840f39efc` and `4e29b1a8efbd4b44bb3f3716e73f07ff`
— almost certainly package scripts, unverifiable in this clone because `Library/PackageCache/` is
not committed (§3.2). That is why the toggle was **rebuilt-or-retired rather than edited in place**;
in the event it was retired, because the instance shipped `m_IsActive: 0` inside a prefab nothing
references, and the live settings panel expresses all seven of its on/off rows as a
`GameSettingsPanelController.OnOffControl` (an ON button + an OFF button) rather than a switch.

### §2.1.1 · Verification status — ⚠ NOT opened in the Unity editor

**`/verify-unity` did not run.** This work was done in a remote container with no `unity` binary,
no editor attached, and no `Library/` — so the project has never been imported against these
changes. Everything below was verified **out of editor**, and the gap is stated rather than papered
over.

What *was* verified, mechanically:

* **Reference integrity.** The two packs owned **88** guids between them, of which exactly **9** had
  a referrer outside their own trees (the 7 re-pointed assets, plus `PrimitivePlusMaterial` and
  `Switch.prefab`). All 9 now resolve to **0 files** across `Assets/`, `ProjectSettings/` and
  `Packages/`; all 7 replacement guids resolve to exactly the referrer counts the originals had
  (Cone 3, Sphere 7, Cube 5, CylinderTube 1, outline 2, filled-200 1, filled-300 2) — re-derived at
  ship time against the merge base, which also corrected this line's earlier count of 10.
* **No orphaned reference, in the project's own tool.** `measure_dangling_guid_references.py`
  (which arrived on `bleeding-edge` while this branch was in flight, written for exactly this class
  of removal) differenced a snapshot of the merge base against one of this branch:
  **0 new unowned guids, 0 new edges, and 0 of the 7 removed edges had a referrer outside the two
  removed trees** — the second line being the one that matters. Its `--removed-under` took a single
  path, which reported each pack's own internal referrers as losses from the other, so this branch
  made the flag repeatable; its `--self-test` negative control still fires.
* **No dangling YAML.** A *regression* comparison over all 20 re-pointed files: exactly 4 anchors
  removed, every referrer to each gone, **no new dangling fileID**. (The naive "every local fileID
  has an anchor" check was tried first and is useless here — it cannot tell a dangling id from a
  reference into a nested prefab or a built-in, and flagged all 20 files.)
* **Asset integrity.** Every emitted PNG decodes with correct per-chunk CRCs at 128×128 RGBA8; every
  emitted mesh's `m_DataSize`, vertex stride (48 B), index-buffer length and 14-channel block are
  self-consistent; all 7 new guids are unique project-wide (0 duplicate guids anywhere in `Assets/`).
* **Serialization shape.** The mesh `.asset` is line-for-line identical in structure to the file it
  replaces, and both `.meta` shapes are byte-identical to Unity's own formatting (see 96a319e6 —
  the empty-scalar trailing space, which would otherwise have made `--check` report phantom drift
  after the first import).
* **Standing gates.** `check_conditional_compilation`, `check_enum_member_references`,
  `check_switch_label_collisions`, `check_self_referential_locals` all pass.
  `check_using_directives` reports **OK, 0 files in scope** — which is the honest answer rather
  than a clean bill of health: it scopes to CHANGED `.cs`, and every `.cs` this work touched is a
  DELETION (the 11 vendor scripts), so there is nothing left for it to read. This work changed
  **zero** first-party `.cs` files. (An earlier pass recorded 18 problems in 12 files here; those
  were the whole-tree pre-existing findings, reproduced identically at the commit before this work,
  in files it never touched.)

**What still needs a human in the editor**, in rough order of what would hurt most if wrong:

1. **The Dolphin's crystal blast** and **the Sparrow's skyburst** — the cone should read exactly as
   before: same gape, same reach, same texture flow along it. This is the one place a wrong mesh
   would change gameplay *feel* without changing the blast VOLUME, since the trigger `BoxCollider`,
   the Burst sweep job and `ExplosionImpactor` all keep their own numbers.
2. **`Menu_Main`** — the party panel (`ArcadeLobbyList`) and the friends panel (`FriendListPanel`):
   the cut-corner plates, at the right corner size. These are the 200-vs-300 PPU pair, so a
   swapped-over import would show as corners ~1.5× too big on one of them.
3. **`Authentication`** — the username field's plate.
4. **The options panel** (`SettingsModal` → `OptionsMenuContent`) — the four tab-button outlines.
5. **`ModalWindows.prefab`** opens with no missing-script warning and no missing nested prefab.
6. **The projectile and FX prefabs** — the sphere-bodied rounds and the crackle FX emit as before.
7. **A console with no import errors**, which is the one check that covers everything above at once.

A re-import will also be the first time Unity assigns the generated sprites their `21300000`
sub-asset fileID. That is the universal convention for a `spriteMode: 1` texture and is the value
the vendor references already used, but it is an assumption until an import confirms it — if a
frame renders as a white box, that is where to look first.

---

## §3 · `Packages/` — **104 resolved packages, not 77**

`manifest.json` lists **77 direct** dependencies. `packages-lock.json` — which is committed and is
the authority on what actually resolves — carries **104**, so **27 arrive transitively** and appear
in no hand-maintained list.

### §3.1 Non-Unity packages (3, all git, all direct)

| Package | Vendor | Upstream licence | Licence in tree | Ships? |
|---|---|---|---|---|
| `com.cysharp.unitask` | Cysharp | MIT *(upstream)* | ❌ not vendored | **Ships** — 134 first-party `using Cysharp…` |
| `com.gustavopsantos.reflex` **14.1.0** | Gustavo Santos | MIT *(upstream)* | ❌ not vendored | **Ships** — 183 first-party `using Reflex…` |
| `com.veriorpies.parrelsync` | VeriorPies | MIT *(upstream)* | ❌ not vendored | **Editor-only upstream — unverifiable here**, §4 |

No scoped registries. All three are pulled from GitHub at their default branch or a tag.

### §3.2 Third-party OSS that Unity **redistributes** (the set a "they're all Unity packages" reading misses)

These are `com.unity.*` by package name and **not Unity's code**. Each ships its own
`LICENSE.md` / `Third Party Notices.md` inside its package folder — which **cannot be read from this
repository**, because `Library/PackageCache/` is not committed. The "commonly licensed" column below
is therefore a *guess about the upstream project*, and **the OpenImageIO row proves the guess can be
wrong in two directions at once**: the notices were fetched from Unity's package registry on
12 Sep 2026 while authoring the credits screen, and

* the **package's own** licence is the **Unity Companion License / Unity Package Distribution
  License**, not BSD — `com.unity.bindings.openimageio copyright © 2024 Unity Technologies`;
* the bundled **oiio 2.4.14.0** is the **Modified BSD** licence of *Larry Gritz et al.*, and the
  package bundles **five more** projects nothing in this repository named: **OpenEXR 2.1.0**,
  **libpng 1.6.38**, **libTIFF 4.3.0**, **boost 1.71.0**, **JPEG-9e**.

General rule this leaves: **an SDK can bundle other SDKs, and the wrapper's licence says nothing
about theirs.** Fetch the package's own `Third Party Notices.md` before writing a licence down.

| Package | Actual upstream project | Commonly licensed *(unverified here)* | Direct? |
|---|---|---|---|
| `com.unity.nuget.newtonsoft-json` **3.2.1** | **Newtonsoft.Json** (James Newton-King) | MIT | direct |
| `com.unity.nuget.mono-cecil` **1.11.6** | **Mono.Cecil** (Jb Evain) | MIT | **transitive** |
| `com.unity.ext.nunit` **2.0.5** | **NUnit** | MIT | **transitive** |
| `com.unity.bindings.openimageio` **1.0.2** | **OpenImageIO** + OpenEXR, libpng, libTIFF, boost, JPEG | ~~BSD-3-Clause~~ → **package: Unity Companion/UPDL; oiio: Modified BSD (Larry Gritz et al.)** — *fetched & verified 12 Sep 2026* | **transitive** |
| `com.unity.burst` **1.8.29** | Unity (bundles LLVM) | Unity + LLVM notices | **transitive** |

### §3.3 The remaining ~96 `com.unity.*` packages

Unity first-party, covered by the **Unity Companion License / Unity Package Distribution terms** that
come with the Editor licence. Two are worth a line each:

| Package | Note |
|---|---|
| **`com.unity.pipeline` 0.5.0-exp.1** | The CLI pipeline package — a dev tool with a remote command surface. Already hard-gated out of release builds by `Assets/_Scripts/Editor/Build/UnityPipelineReleaseGuard.cs`. |
| **UGS services** (`analytics`, `cloudsave`, `leaderboards`, `multiplayer`, `friends`, `core`, plus transitive `authentication`, `qos`, `wire`, `deployment`) | Governed by the **Unity Gaming Services Terms of Service**, not just the package licence — a separate agreement a human accepts on the UGS dashboard, with its own data-processing obligations. Relevant to the privacy/analytics work in `Docs/Analytics/`. |
| **`com.unity.purchasing` 4.12.2 / `com.unity.ads`** | Commerce and ads SDKs carry additional platform terms. Note **R4 de-scoped the commerce surfaces**, so Purchasing is present but gated. |

---

## §4 · ParrelSync — confirmed for `Assets/`, **flagged** for the package

**Confirmed:** the `Assets/` side **cannot ship**. `Assets/Plugins/ParrelSync/` holds exactly one
file, `ParrelSyncProjectSettings.asset` — **0 inbound references** (guid
`aa4d7717a7cbf7a4bbbc940124c221ab`, checked across every `.unity`, `.prefab` and `.asset`), and it
is in neither a `Resources/` nor an `Editor/` folder.

**Flagged, not confirmed:** the **package** side is **unverifiable in this repository** —
`Library/` is not committed, so the package's `.asmdef` is not present and I cannot read whether it
is `includePlatforms: ["Editor"]`. ParrelSync is editor-only by upstream design; **this pass did not
see that file and does not assert it.**

To close it: read the resolved package's `.asmdef` on a machine where packages are restored, grep a
release build's IL2CPP output for `ParrelSync`, or extend the guard —
**`UnityPipelineReleaseGuard` already implements exactly this pattern** for `com.unity.pipeline`, and
**there is no equivalent for ParrelSync.**

---

## §5 · Native binaries

**38 managed DLLs** and **56 native libraries** (25 `.so`, 22 `.a`, 4 `.bundle`, 2 `.aar`, 1 `.jar`,
1 `.dylib`, 1 `.framework`). Ownership:

| Vendor | Native libs | Note |
|---|---|---|
| **FMOD** | 52 | Every shipping platform (Android, iOS, Linux, macOS, Windows, UWP) + `resonanceaudio` + `fmod_haptics` |
| **NiceVibrations / Lofelt** | 2 + 1 editor DLL | Android `.aar`, macOS bundle, `nice_vibrations_editor_plugin.dll` |
| **NativeShare** | 1 | Android `.aar` |

A native binary carries its vendor's licence whether or not a text file sits beside it; none of the
three vendors above ships a per-binary notice in this tree.

---

## §6 · Unknown provenance — the repository cannot answer these

Recorded as *unknown* rather than guessed.

**`Assets/Effects Library/` (8.2 MB).** Its only child is `Froglet Stuff/`, which reads as
first-party — but the parent is named like an imported pack and the render assets inside carry an
`FE_` prefix (`FE_ForwardRenderer`, `FE_UniversalRP-HighQuality`, `FE__GlobalVolumeProfile`) that
looks like an import prefix rather than a Froglet one. Load-bearing either way
(`vfx_Projectile_02.prefab` → `VesselJet.prefab`).
**Who would know:** whoever added the vessel jet FX; the Asset Store account holder.

**`Assets/_Graphics/Texture/Noise Texture Collection (Angelo)/` (360.6 MB, 109 PNGs).** **Added
12 Sep — the register's first pass did not catch it**, because it scoped `_Graphics` out as
first-party. No licence, no readme, no vendor; the folder name names a person. 4K noise tiles
(Cells, Vines, Swirls, Waves, Geometric, Boxes) and **zero of them are reachable from any build
root** (`Docs/LAUNCH_BLOCKER_INDEX.md` §E1), so nothing ships — this is repository weight and an
unanswered provenance question, not a build defect. It is **26% of `Assets/`**.
**Who would know:** whoever commissioned or downloaded it; the art lead. General lesson: *a
first-party folder name is not evidence that everything inside it is first-party.*

**`Assets/SerializeInterface/` — RESOLVED 12 Sep 2026, by rewrite rather than by answer.** It had
no namespace, no header comment, no licence and no vendor, and it shipped (no asmdef). Nobody needed
to remember where it came from: it is the well-known community `[RequireInterface]` pattern, and what
the project actually depends on is fully specified by its own call sites, so it was **rewritten
first-party** (`CosmicShore.Utility.RequireInterfaceAttribute` + `CosmicShore.Editor.RequireInterfaceDrawer`)
and the folder deleted. **Half of it turned out to be dead** — `InterfaceReference<>`, its drawer and
the `InterfaceArgs` struct had zero consumers — and that half was deliberately not reproduced.
*General lesson: an unattributable code drop small enough to re-derive from its call sites does not
need its provenance answered — it needs replacing. Ask the provenance question only where the thing
is too large to rewrite, as with the three below.*

**`Assets/PrimitivePlus/`, `Assets/Shift - Complete Sci-Fi UI/`** *(both since removed — §2.1)*
**and `Assets/Unity Assests/`.** No vendor, version or import record recoverable. **Git cannot supply it:** this clone is **shallow**
(`.git/shallow` present, 741 commits, history begins 2026-08-12), so `git log --diff-filter=A`
reports the graft-boundary commit for every one of them. A **full clone** would answer *when and by
whom*; it would still not answer *was it paid for*.

---

## §7 · Attribution obligations, collected

Every in-product attribution this tree can prove is owed. **They were all outstanding for one
reason — there was nowhere to put them.** There is now: an in-game **credits screen**, built
12 Sep 2026.

### §7.1 What was built

| Artefact | Path | What it does |
|---|---|---|
| Data | `Assets/Resources/CreditsManifest.asset` (7,055 bytes) | The authored credit text. Under `Resources/`, so the runtime loads it with no per-scene wiring — and so it **ships whether or not anything references it** (§1). |
| Schema | `Assets/_Scripts/ScriptableObjects/CreditsManifestSO.cs` | Ordered sections → entries (name / role / notice). A notice is **prose, never a typed licence enum** — QuickScene Pro ships *MIT with an added Indian jurisdiction clause*, which no enum can express. |
| Screen | `Assets/_Scripts/UI/Modals/CreditsModal.cs` | A `ModalWindowManager` whose rows are **generated from the manifest**, so adding the next notice is an asset edit. Its scroll height is **measured** after the rows exist (`VerticalLayoutGroup` + `ContentSizeFitter` + `ForceRebuildLayoutImmediate`), never authored — the ScrollRect trap CLAUDE.md records, where content past the reachable range is clipped *and eats the press*. |
| Reachability | `ScreenSwitcher.ModalWindows.CREDITS` (= 18) + `ScreenSwitcher.EnsureCreditsModal()` + `GameSettingsPanelController.OpenCredits()` / `EnsureCreditsRow()` | A **Credits** row in the settings panel names the modal TYPE and lets the switcher find it. Both the window and the row are **ensured in code** when unauthored, and both stand down the moment a human authors the real thing. |
| Build gate | `Assets/_Scripts/Editor/Build/CreditsReleaseGuard.cs` | `IPreprocessBuildWithReport`, `callbackOrder −9999`, modelled on `UnityPipelineReleaseGuard`. Throws `BuildFailedException` on any **non-development** build whose manifest is absent or has lost either required string. Loads through the **runtime `Resources` path**, not an asset path — that is what proves the shipped game can find it. |
| Offline gate | `Tools/Build/check_credits_manifest.py` | The same question with no editor, for CI. `--self-test` carries **four negative controls** (company name removed, every `FMOD` removed, the trailing period dropped, manifest absent) — *a gate nobody has watched fail is a gate nobody should trust*. |

### §7.2 The obligations, and where each is discharged

| Source | Obligation | Status |
|---|---|---|
| **FMOD** (EULA cl. 3) | In-game credit containing **"FMOD"** and **"Firelight Technologies Pty Ltd."** — **all tiers** | ✅ **`MIDDLEWARE` → "Made with FMOD Studio by Firelight Technologies Pty Ltd."** — the conventional form from `www.fmod.com/attribution`, trailing period included. Enforced by both gates. |
| **UniTask** (Yoshifumi Kawai / Cysharp), **Reflex** (Gustavo Santos), **ParrelSync** (Greg M; Ian and Contributors) | MIT — the notice must travel with the software | ✅ `THIRD-PARTY NOTICES` → *MIT-licensed components*. Each project's real `LICENSE` was **fetched**, not written from memory; all six MIT bodies are **textually identical**, so one verbatim body carries six copyright lines. |
| **Newtonsoft.Json** (James Newton-King), **Mono.Cecil** (Jb Evain), **NUnit** | MIT, via `com.unity.nuget.*` / `com.unity.ext.nunit` | ✅ same entry |
| **QuickScene Pro** (yethgamedevv) | MIT **with an added jurisdiction clause** — notice with "substantial portions" | ✅ own entry, verbatim including the added clause — and since 12 Sep 2026 it **no longer ships** either (`Resources/` and `Demo/` moved under `Editor/`, `Docs/LAUNCH_BLOCKER_INDEX.md` §A2). The MIT obligation attaches to *distribution*; the credit is kept because the entry costs nothing and the editor tool is still vendored. |
| **OpenImageIO** + OpenEXR / libpng / libTIFF / boost / JPEG | Modified BSD (oiio) and the bundled projects' own notices | ✅ own entry. **Correction:** the register previously said BSD-3-Clause for the *package*; see §3.2. |
| **Liberation Sans** | SIL Open Font License 1.1 | ✅ own entry. **This was missing from this section entirely** — it is TMP's default font asset, under `Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials/`, i.e. a `Resources/` folder, so it **ships**. |
| **Roboto** (Google) | Apache 2.0 notice | ✅ own entry. **Two corrections, in order.** This row once read *"ships if the font is used"*; §7 corrected that (`Roboto-Bold SDF.asset` sat in a `Resources/` folder, so it shipped whether used or not). That asset has since been **removed with TMP's Examples & Extras** (`Docs/LAUNCH_BLOCKER_INDEX.md` §A1), so no Roboto reaches the player from TMP at all. NiceVibrations' four `RobotoMono-*.ttf` remain in its Demo folder, so the entry stays earned. |
| **EmojiOne** | Per EmojiOne's own terms | ✅ own entry. **Correction:** same as Roboto — `EmojiOne.asset` is under `Resources/`. |
| **Bangers** (SIL OFL 1.1) | Reserved-Font-Name notice | ❌ **NOT in the manifest.** It **ships** — `Bangers SDF.asset` is referenced by `QuestItemPrefab.prefab`, and being a DYNAMIC TMP font it carries `Bangers.ttf` with it. Both were relocated out of Examples & Extras rather than deleted, and `Bangers - OFL.txt` was preserved beside them (`Docs/LAUNCH_BLOCKER_INDEX.md` §A1). Needs a §7 entry. |
| **Electronic Highway Sign** | unknown — **no notice anywhere in the tree** | ❌ **NOT in the manifest**, and cannot be until somebody answers the provenance question. It **ships** — `Electronic Highway Sign SDF.asset` is referenced by `Manta.prefab` and is likewise DYNAMIC, so the TTF ships too. Pre-existing; the prune only made it visible. |
| **Google EDM4U** (Apache 2.0) | NOTICE preservation | editor-only — does not ship, deliberately **not** credited |
| **CC-BY 4.0 models** (VertaScan) | Attribution if distributed | ⏸ **deliberately absent** — 0 references, so they do not ship, and crediting them would be a *false statement about the build*. `Docs/VESSEL_CONSTRUCTION_FOLLOWUP.md` carries the trigger: wiring one makes the attribution mandatory. |

### §7.3 In-editor verification — the steps a human must run

Nothing below has been opened in Unity. These are the checks, in order; each names what a
failure looks like so it cannot be mistaken for working.

1. **Open `Menu_Main` and press Play.** The credits window is BUILT at
   `ScreenSwitcher.Start()`, so it should exist in the hierarchy as `CreditsModal (built)`
   under the root canvas, **invisible** (CanvasGroup alpha 0). *A visible credits window on
   the menu's first frame means the alpha-0-at-build step did not run.*
2. **Settings → the General tab.** A `Credits` row should sit one row-height below
   `Privacy Policy`, same size and style (it is a runtime clone of it). *If it is missing,
   `EnsureCreditsRow` found no `privacyPolicyButton`; if it is ON TOP of Privacy Policy, the
   measured offset did not apply.*
3. **Press it.** The window opens. **Scroll to the very bottom** — the last line must be the
   EmojiOne notice, reachable. *A list that stops short is the ScrollRect trap (CLAUDE.md, the
   arcade grid): content past the reachable range is clipped AND eats the press.*
4. **Confirm the FMOD line is legible on screen**, under `MIDDLEWARE`: *"Made with FMOD Studio
   by Firelight Technologies Pty Ltd."* This is the one line with legal weight.
5. **Close with the CLOSE button, then re-open and close with gamepad B.** Both must return to
   Settings rather than closing both windows. *If B closes both, the modal is not on the
   switcher's stack — `AttachScreenSwitcher` did not run.*
6. **Watch the console.** One warning naming `ModalWindowManager … has no AudioSystem` means
   the injection at the creating site did not run; everything else still works, the open/close
   sting is silent.

**To retire both fallbacks** (optional, and the better end state): author a CREDITS modal in
`Menu_Main` with the house chrome and add it to `ScreenSwitcher.Modals`, and wire a real
`creditsButton` in `SettingsModal.prefab`. Each ensure checks for the authored thing first and
does nothing when it finds it, so neither needs a code change to stand down.

### §7.4 What is NOT proved

* **That the screen renders.** Both gates check the **data**, so the modal's layout, the generated
  row's placement in the settings panel, and the scroll reaching the last line are **unverified** —
  §7.3 is the checklist that closes them. Nothing has been opened in the Unity editor and no play
  mode has run.
* **What IS proved about the code:** every new and edited file **type-checks under real Roslyn**
  (dotnet-sdk 8.0 installed per-user, `-langversion:9.0`, against a stub harness carrying only the
  API surface these files touch, plus the two edited files' new methods extracted verbatim into a
  faithful harness). Zero errors; the only warnings are `CS0649` on `[SerializeField]` fields, which
  every Unity project has. That is a TYPE check, not a behaviour check — it says the calls exist
  with those signatures, not that the window scrolls. "It compiles by inspection" was the earlier
  claim and it was weaker than what this environment can actually do
  (`.claude/skills/asset-surgery` §4).
* **Entitlement.** The register proves *presence*; §0 rows 2–8 are still the money-side questions,
  and a credits line is not a licence.

## §8 · Credentials

Swept by **location and filename only — no values were read or printed.**

Searched `Assets/`, `ProjectSettings/`, `Tools/` for `*.keystore`, `*.jks`, `*.p12`, `*.pem`,
`*.pfx`, `*.mobileprovision`, `.env*`, `google-services.json`, `GoogleService-Info.plist`,
`*credential*`, `*secret*`. **Nothing found** — the only hits were a music track named
"SECRET HIDE AWAY" and its SO. `ProjectSettings.asset` carries no Android keystore or alias fields.

This is a filename-and-known-location sweep, **not** a content scan for embedded API keys. Note that
PlayFab title IDs and UGS project IDs are configuration rather than secrets, and were not audited.
A content-level secret scan is a separate job and is not claimed here.
