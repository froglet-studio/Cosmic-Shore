# Third-party decisions — what we buy, what we replace, what we delete

The **decision** layer over [`THIRD_PARTY_REGISTER.md`](THIRD_PARTY_REGISTER.md). The register records
what is *present*; this records what the studio has decided to *do about it*, who owns each item, and
which prompt executes it.

**Owner decisions recorded 12 Sep 2026** (Shombith). **Measurements re-taken the same day** against
`claude/zealous-davinci-vjwev0`; every count below is measured, not estimated.

> **Nothing has been deleted or moved on this branch.** Each removal is its own reviewable change with
> its own reference proof — that is what the prompts are for.

---

## §1 · The decisions, as given

| # | Item | Owner's call | Executes as |
|---|---|---|---|
| **1** | FMOD in-game credit | **Build it** | [`CREDITS_SCREEN_PROMPT.md`](prompts/CREDITS_SCREEN_PROMPT.md) |
| **2** | FMOD licence tier | **Buy if the thresholds say so** | §2 below — money, not code |
| **3** | Obvious SOAP | **Owner is adding the licence file** | §2 — nothing to build |
| **4** | NiceVibrations plugin | **Replace with a placeholder** | [`HAPTICS_VENDOR_INDEPENDENCE_PROMPT.md`](prompts/HAPTICS_VENDOR_INDEPENDENCE_PROMPT.md) |
| **5** | NiceVibrations demo art in the shipped menu | **Replace with a placeholder** | [`NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md`](prompts/NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md) |
| **6** | Shift Sci-Fi UI + PrimitivePlus | **Replace with a placeholder** | ✅ **DONE 12 Sep 2026** — both replaced first-party and removed; see §4 row 6 and `THIRD_PARTY_REGISTER.md` §2.1 |
| **7** | "Effects Library" provenance | **Owner will look into it** | [`EFFECTS_LIBRARY_PROVENANCE_PROMPT.md`](prompts/EFFECTS_LIBRARY_PROVENANCE_PROMPT.md) |
| **8** | Wwise | **Owner will see to it** | [`WWISE_AND_DEAD_SDK_REMOVAL_PROMPT.md`](prompts/WWISE_AND_DEAD_SDK_REMOVAL_PROMPT.md) |

Then, once 4–8 have landed: [`THIRD_PARTY_FOLDER_HYGIENE_PROMPT.md`](prompts/THIRD_PARTY_FOLDER_HYGIENE_PROMPT.md).

---

## §2 · The money list — everything that needs a receipt or a purchase

**This is the only list on which inaction costs money or exposure.** Everything else on this page is
engineering work.

| Item | Vendor | Licence doc in tree | What has to be bought or produced | If we do not |
|---|---|---|---|---|
| **FMOD Studio** | Firelight Technologies | ✅ EULA present | **A tier decision, not necessarily a purchase.** Free commercial use is capped at **dev budget < $600k USD** *and* **gross revenue + funding < $200k USD/yr**. Froglet Inc. is a Delaware C-corp shipping paid Steam EA — confirm both, buy the Indie/Basic tier if either is exceeded. | Distributing without a tier that covers us. Independent of this, the **credit line is required on every tier** (§1 row 1). |
| **Obvious SOAP 2.7.0** | Obvious Games | ❌ **none** | **The receipt.** The README's *"Thanks for purchasing Soap :)"* shows someone bought it; the tree carries no EULA. Owner is adding it. | **Not removable** — 128 first-party files use it; it is the project's primary architecture. This is a receipt problem only. |
| **NiceVibrations 4.1.1** | Lofelt / More Mountains | ❌ **no product EULA** (the two licence-looking files are a Rust crate list and a CC audio notice) | **Nothing — we are replacing it.** Buy a seat *only* if mobile pattern haptics are wanted back before launch. | Nothing, once row 4 lands. |
| ~~**Shift — Complete Sci-Fi UI**~~ | Michsky | ❌ none | ✅ **Nothing — replaced and removed 12 Sep 2026.** No purchase needed. | — |
| ~~**PrimitivePlus**~~ | unknown (ns `PrimitivePlus`) | ❌ none | ✅ **Nothing — replaced and removed 12 Sep 2026.** No purchase needed, which is just as well: the vendor was never identifiable from the tree. | — |
| **DOTween (free tier)** | Demigiant | ❌ none | **No purchase.** Demigiant documents the free tier as usable commercially; the licence TEXT is simply not vendored. **Fetch `LICENSE.txt` from the DOTween distribution and commit it beside the DLL.** (DOTween **Pro** is a separate paid product and is *not* what is in the tree.) | A shipped dependency with no licence document, in 44 first-party files. |
| **NativeShare** | yasirkula | ❌ none | **No purchase** — MIT upstream. **Vendor the MIT notice.** | MIT's notice-preservation term unmet. |
| **UniTask / Reflex / ParrelSync** | Cysharp / G. Santos / VeriorPies | ❌ not vendored (git packages) | **No purchase** — MIT. Their notices ride the credits screen (row 1). | Same as above, ×3. |
| **Wwise** | Audiokinetic | — | **No purchase. Confirm no evaluation licence was ever signed, then delete.** 84 KB, **0 asset files**, 14 orphan `.meta`. | A licensed-audio-middleware question that never needed to exist. |
| **"Effects Library"** | ❓ unknown | ❌ none | **Identify the vendor.** 8.2 MB; exactly **one** prefab is live. | An unattributable 8.2 MB pack in a shipping build. |
| **SerializeInterface** | ❓ unknown | ❌ none, no namespace, no header | **No purchase — rewrite it.** 290 lines total (48 runtime / 242 editor), 7 first-party consumers. | An unattributable shipping code drop. |

**Everything above that says "no purchase" resolves to either a text file or a prompt.** As of
12 Sep 2026 the only row that can cost money is **FMOD's tier**: Shift was the other one, and it is
now replaced and removed, so the choice it represented no longer exists.

---

## §3 · Disposition of every vendored SDK

`ships?` per Unity's real rules (§1 of the register): `Editor/` never ships, an `.asmdef`'s
`includePlatforms` decides, no-asmdef code lands in `Assembly-CSharp` and ships, and **any**
`Resources/` folder is packed whole.

| SDK | Ships? | First-party coupling (measured) | Disposition |
|---|---|---|---|
| **FMOD** | ships | the whole audio layer | **KEEP** — credit line + tier |
| **Obvious SOAP** | ships | **128** files | **KEEP** — receipt only |
| **NiceVibrations** | ships (incl. Demo, 30 `.cs`) | **1** file, **5** API symbols | **REPLACE** (row 4) |
| ├ demo sprites | ship | 6 usages in 5 shipped locations | **REPLACE** (row 5) |
| └ `HapticSamples/*.wav` | ship | 4 clips on legacy `AudioClip` fields | **REPLACE with FMOD events** (row 5) |
| ~~**Shift Sci-Fi UI**~~ | — | **0** code files; 3 textures + 1 prefab | ✅ **REPLACED + REMOVED** 12 Sep 2026 |
| ~~**PrimitivePlus**~~ | — | **0** code files; 1 component on 3 prefabs, 4 meshes on 16 prefabs + 1 shadergraph | ✅ **REPLACED + REMOVED** 12 Sep 2026 |
| **DOTween** | ships | **44** files | **KEEP** — vendor the licence |
| **NativeShare** | ships | **4** files | **KEEP** — vendor the licence |
| **PlayFabSDK** | ships | **20** files (inert backend) | **DEFER** — launch-blocker index §B2 |
| **PlayFabEditorExtensions** | editor-only ✅ | — | **KEEP** — costs the player nothing |
| **QuickScene Pro** | editor code, but `Resources/` (1.8 MB) ships | editor tool | **KEEP, move the `Resources/`** — index §A2 |
| **EDM4U** | editor-only ✅ | — | **KEEP** — Apache 2.0, licence present |
| **Microsoft.Unity.Analyzers** | compile-time | — | **KEEP** |
| **TextMesh Pro** | ships (both `Resources/`) | fleet-wide | **KEEP**, prune Examples & Extras — index §A1 |
| **Parse** | ships if referenced | **0** references, **0** code | **DELETE** (row 8 prompt) |
| **Wwise** | ships nothing (0 assets) | **0** | **DELETE** (row 8 prompt) |
| **SerializeInterface** | ships (no asmdef) | **7** files | **REWRITE first-party** (row 8 prompt) |
| **Effects Library** | ships | **1** prefab (`VesselJet`) | **SALVAGE + identify** (row 7) |
| **Shader Graph samples** | ships if referenced | — | **KEEP** — Unity UCL |
| **CC-BY vessel models** | not shipping (0 refs) | — | **KEEP** — attribution only if ever wired |

---

## §4 · What the replacements actually cost

Measured, so the "is a placeholder cheaper than a receipt?" question has a number behind it.

### Row 4 — NiceVibrations → first-party rumble

The dependency is **one file and five symbols**: `HapticController.Load(byte[], GamepadRumble)`,
`.outputLevel`, `.clipLevel`, `.Play()`, and the `GamepadRumble` struct.

**Every haptic the game plays is already first-party data.** `HapticController.EnsureClips()` builds
all four feels — skim, punish, alert, spray — in code, as `.haptic` JSON strings and
`GamepadRumble` arrays *we authored*. NiceVibrations contributes **no content**, only a player.

On **gamepad** that player is replaceable outright by `UnityEngine.InputSystem.Gamepad.SetMotorSpeeds`
(Input System **1.14.2**, already a direct dependency): step the existing `durationsMs` /
`lowFrequencyMotorSpeeds` / `highFrequencyMotorSpeeds` arrays. **On PC/Steam — the launch platform —
that is the whole feature**, at zero feel loss. Mobile pattern haptics (iOS Core Haptics / Android
`VibrationEffect`) are the only thing the plugin was buying, and they are not on the launch path.

**Cost: ~1 file rewritten, 0 assets, 0 art. Saves 47 MB and a missing EULA.**

### Row 5 — the demo assets

Two separate problems that happen to share a folder.

**The sprites (4 distinct, 6 usages).** `RegularPresetsIcons.png` (a sheet — 4 sub-sprites in
`Menu_Main.unity`, `Arcade Screen.prefab`, `ModalWindows.prefab` ×2),
`RegularPresetsIcons_8_Flipped_Vertically.asset` (`Menu_Main.unity`, `Records Screen.prefab`), and
`NVCar.png` / `NV7Dots.png` — which are the **Termite class icons** (`SO_Class_Termite.IconActive` /
`IconInactive`). Termite is a *planned, unimplemented* vessel, so those two are already placeholders
standing in for art that does not exist.

**The audio, which is the more serious half and is a NEW finding.** Four clips under
`NiceVibrations/HapticSamples/` are wired into shipped UI as plain `AudioClip` fields —
`Beep1.wav` → `CountdownTimer.prefab.countdownBeep`, `Cash5.wav` → `onTriggerClip`,
`Coins2.wav` → `targetReachedClip`, `Keyboard3.wav` → `Profile`/`ModalWindows`/`Menu_Main`
`TypingAudio`. Their bundled notice, `HapticPackCClicense.txt`, **opens by declaring the pack
"licensed under CCBYNC 3.0"** — *non-commercial* — while its 100-entry source list is almost entirely
CC0 with one Attribution item, and **names freesound sources rather than the shipped filenames**, so
no shipped clip can be mapped to a licence term. Either reading demands the same action, and the
project has already decided what that action is: CLAUDE.md's audio convention says the
`AudioClip` + `AudioSource` path is legacy and **new SFX is FMOD**. Replacing these four is not a
licence workaround — it is finishing a migration the project already committed to.

**Cost: 4 sprite swaps + 4 FMOD events. Art lead time on the sprites; no engineering risk.**

### Row 6 — Shift + PrimitivePlus

| | Shift Sci-Fi UI | PrimitivePlus |
|---|---|---|
| Size | 584 KB / 15 files | 1008 KB / 53 files |
| First-party **code** references | **0** | **0** |
| Live assets | 3 border textures, `Switch.prefab` | `PrimitivePlusMaterial.cs`, `Sphere`, `Cone`, `Cube`, `CylinderTube` |
| Live in | `Menu_Main`, `Authentication`, `ModalWindows`, `OptionsMenuContent` | 16 prefabs + `LaserGraph.shadergraph` |
| `Resources/` that ships regardless | `Shift UI Manager.asset` | **47 meshes + 1 material** |
| Replacement | 3 sprites re-authored (9-sliced frames) + a first-party toggle | 4 meshes baked first-party + a 36-line component rewritten |

Two hazards the prompt had to respect. `Cone.asset` is on **`AOEConicExplosion`** and
**`AOEConicSkyBurst`** — the Dolphin's crystal blast and the Sparrow's skyburst, both
gameplay-critical; and `Switch.prefab` carries **two `m_Script` guids owned by no `.meta` under
`Assets/`** (almost certainly package scripts — unverifiable here, because `Library/PackageCache/`
is not committed), so it had to be rebuilt rather than edited in place.

#### ✅ Landed 12 Sep 2026 — and three things worth carrying

Full record, with every measurement: [`THIRD_PARTY_REGISTER.md` §2.1](THIRD_PARTY_REGISTER.md).
Five commits: author the replacements, re-point, remove each pack (one commit each, each carrying
its own per-asset guid proof), update the docs.

**(1) The replacement had to be measured while the thing it replaced still existed.** Both authoring
tools carry a `--verify-vendor` mode that compares against the pack, and those numbers are the only
evidence that will ever exist — after deletion the mode correctly stands down with "vendor asset is
gone" rather than aborting. All four meshes and all three frames came back surface-identical /
0-pixels-off; the Cone, the one that could have changed a blast, matched to a max UV delta of
**0.000001** and now has *cleaner* bounds than the vendor's (exactly ±0.5 against ±0.5000004).

**(2) Two of the three things the prompt asked us to build turned out not to need building**, and
saying so was worth more than building them. `PrimitivePlusMaterial` is an editor-authoring helper
whose only two callers are the vendor's own inspector — at runtime it caches a `MeshRenderer` into a
private field nobody reads — so a first-party rewrite would have been a no-op compiled into
`Assembly-CSharp` and carried on three prefabs; the component was deleted instead. The Shift
**switch** was the same shape of answer for a different reason: its one instance shipped
`m_IsActive: 0`, inside a prefab (`_Prefabs/UI Elements/ModalWindows.prefab`) that **nothing
references**, while the live settings panel expresses all seven of its on/off rows as a
`GameSettingsPanelController.OnOffControl`. There was no toggle to rebuild. *The `ModalWindows` that
first-party code does name is `ScreenSwitcher.ModalWindows`, an enum — not that prefab.*

**(3) Fit the geometry, do not read it off.** Both replacements were first derived by reading alpha
ramps and vertex dumps by hand, and both hand-derivations were WRONG in ways that looked right: the
frame's bottom-right chamfer was 3 px out (a printed row index misread), and the outline band was
built as a coordinate inset when the art is a true perpendicular offset — 7.4 % too heavy. Solving
each constant numerically against the asset found both in seconds. The same pass caught a defect in
the *replacement* nobody would have seen otherwise: the generated cube's ±Z faces were wound
backwards, which a comparison by triangle winding reported and an eyeball would not have.

### Row 7 — Effects Library

**8.2 MB, of which one prefab is load-bearing.** `vfx_Projectile_02.prefab` → `VesselJet.prefab`,
pulling exactly two materials (`Flare00_AB_2.mat`, `DistortedFlare01_AB.mat`) and their textures.
Measured: the four `FE_*` render-pipeline settings assets — which look like they might be the
project's live URP configuration — have **zero external references** and are not the live pipeline;
`Mountains01.fbx` (860 KB) has zero references; four of the five `vfx_*` prefabs have zero references.

### Row 8 — the dead drops

`Wwise` (84 KB, **0 asset files**, 14 orphan `.meta`), `Parse` (76 KB, **0** references, **0** code),
and `SerializeInterface` (290 lines, **7** consumers, no vendor, no licence, ships via
`Assembly-CSharp`).

---

## §5 · Folder reorganisation — after, never before

Sequenced last on purpose: moving a folder rewrites paths in every `.meta`-adjacent tool and in the
`Resources/` rules, and doing it while items are still being deleted means doing it twice.

Current state, measured:

* **17 third-party folders sit at the `Assets/` root**, interleaved with the first-party `_`-prefixed
  ones — `NiceVibrations`, `Effects Library`, `PlayFabSDK`, `PlayFabEditorExtensions`, `YethGameDev`,
  `PrimitivePlus`, `Samples`, `ExternalDependencyManager`, `Shift - Complete Sci-Fi UI`, `Analyzers`,
  `Wwise`, `Parse`, `SerializeInterface`, `Unity Assests` *(sic)*, `Adaptive Performance`,
  `Resolvers`, `Environment`.
* **`Assets/Environment/` is empty.**
* **`Assets/ArcadeDPadNav.cs`** — a first-party script — sits loose at the `Assets/` root.
* **`Assets/.DS_Store` is tracked in git** (2 `.DS_Store` files are).
* `Assets/Unity Assests/` is a **typo** that is now load-bearing in every guid-free path reference.

Target shape and the rules that make the move safe are in
[`THIRD_PARTY_FOLDER_HYGIENE_PROMPT.md`](prompts/THIRD_PARTY_FOLDER_HYGIENE_PROMPT.md). The one that
matters most: **a move is `git mv` of the file *and* its `.meta`, never a rename** — guids survive a
move and do not survive a re-import, and a `Resources/` folder changes meaning the moment its path
changes.

---

## §6 · What this page does not decide

* **Whether any of it was paid for.** The repository proves presence; invoices prove entitlement.
* **The launch-blocker removals.** Those live in [`LAUNCH_BLOCKER_INDEX.md`](LAUNCH_BLOCKER_INDEX.md)
  and are a separate pass.
* **A content-level secret scan.** §8 of the register was filenames and known settings keys only.
