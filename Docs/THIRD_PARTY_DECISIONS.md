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
| **1** | FMOD in-game credit | **Build it** | ✅ **DONE 12 Sep 2026** — [`CREDITS_SCREEN_PROMPT.md`](prompts/CREDITS_SCREEN_PROMPT.md) executed; evidence in register §7. Not yet opened in the editor. |
| **2** | FMOD licence tier | **Buy if the thresholds say so** | §2 below — money, not code |
| **3** | Obvious SOAP | **Owner is adding the licence file** | §2 — nothing to build |
| **4** | NiceVibrations plugin | **Replace with a placeholder** | ✅ **Landed 12 Sep** — [`HAPTICS_VENDOR_INDEPENDENCE_PROMPT.md`](prompts/HAPTICS_VENDOR_INDEPENDENCE_PROMPT.md) |
| **5** | NiceVibrations demo art in the shipped menu | **Replace with a placeholder** | ✅ **Landed 12 Sep** — [`NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md`](prompts/NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md) |
| **6** | Shift Sci-Fi UI + PrimitivePlus | **Replace with a placeholder** | [`VENDORED_UI_PACK_PLACEHOLDERS_PROMPT.md`](prompts/VENDORED_UI_PACK_PLACEHOLDERS_PROMPT.md) |
| **7** | "Effects Library" provenance | **Owner will look into it** | [`EFFECTS_LIBRARY_PROVENANCE_PROMPT.md`](prompts/EFFECTS_LIBRARY_PROVENANCE_PROMPT.md) |
| **8** | Wwise + Parse + SerializeInterface | **Owner will see to it** | ✅ **Executed 12 Sep 2026** — [`WWISE_AND_DEAD_SDK_REMOVAL_PROMPT.md`](prompts/WWISE_AND_DEAD_SDK_REMOVAL_PROMPT.md). Code side complete; **one human item open**, §2 below |

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
| **Shift — Complete Sci-Fi UI** | Michsky | ❌ none | **A seat (~$20–35) *or* nothing** — we are replacing it. It is 3 border textures and 1 switch prefab, with **zero first-party code coupling**. Cheapest honest outcome: buy the seat if anyone wants the art kept; otherwise replace. | Nothing, once row 6 lands. |
| **PrimitivePlus** | unknown (ns `PrimitivePlus`) | ❌ none | **A seat *or* nothing** — we are replacing it. 4 meshes and one 36-line component are live. **Vendor is not even identifiable from the tree**, so "buy it" may not be an available action. | Nothing, once row 6 lands. |
| **DOTween (free tier)** | Demigiant | ❌ none | **No purchase.** Demigiant documents the free tier as usable commercially; the licence TEXT is simply not vendored. **Fetch `LICENSE.txt` from the DOTween distribution and commit it beside the DLL.** (DOTween **Pro** is a separate paid product and is *not* what is in the tree.) | A shipped dependency with no licence document, in 44 first-party files. |
| **NativeShare** | yasirkula | ❌ none | **No purchase** — MIT upstream. **Vendor the MIT notice.** | MIT's notice-preservation term unmet. |
| **UniTask / Reflex / ParrelSync** | Cysharp / G. Santos / **Greg M; Ian and Contributors** | ❌ not vendored (git packages) | **No purchase** — MIT. ✅ Their notices now ride the credits screen (row 1); each `LICENSE` was fetched from the project, and all six MIT bodies are textually identical. | ParrelSync's holders are **not** "VeriorPies" — that is the GitHub org, not the copyright line. |
| **Wwise** | Audiokinetic | — | ⚠️ **STILL OPEN, and it is the only thing left on this row: confirm no Wwise evaluation or project licence was ever signed.** Audiokinetic licenses **per title**, so an evaluation agreement — if one exists — is an obligation attached to *Cosmic Shore*, and deleting the folder does not discharge it. **It must be recorded and closed out with Audiokinetic rather than left implicit.** No purchase is expected. **Asked, not yet answered** — see §2.1. The folder itself was **deleted 12 Sep 2026** without waiting on this, deliberately: it held 0 asset files and 0 references, so keeping it could not have preserved any evidence. | A licensed-audio-middleware obligation nobody can find, on a title that never used the middleware. |
| **"Effects Library"** | ❓ unknown | ❌ none | **Identify the vendor.** 8.2 MB; exactly **one** prefab is live. | An unattributable 8.2 MB pack in a shipping build. |
| ~~**SerializeInterface**~~ | ❓ unknown | ❌ none, no namespace, no header | ✅ **Done 12 Sep 2026 — no purchase, rewritten.** The live half (the `[RequireInterface]` attribute + its drawer) is now first-party at 48 + 219 lines; the dead half (`InterfaceReference<>`, its drawer, `InterfaceArgs` — **zero** consumers) was not reproduced. Folder deleted. | — closed |

**Everything above that says "no purchase" resolves to either a text file or a prompt.** The only two
rows that can cost money are **FMOD's tier** and — optionally, only if the art is wanted — **Shift**.

---

### §2.1 · The Wwise evaluation-licence question — asked, OPEN

**Asked 12 Sep 2026, as part of deleting the folder. Not answered here, because nothing in this
repository can answer it.**

> Was a Wwise **evaluation** or **project** licence ever signed with Audiokinetic during the
> middleware evaluation that left `Assets/Wwise/` behind?

**Why it cannot be answered from the tree, stated so nobody re-runs the search:** the folder held
**zero asset files** — 14 `.meta` describing directories whose contents were already gone — so there
was never a licence document, a version string, a project GUID or an `AkWwiseProjectData` asset to
read. There is no `Wwise` entry in `ProjectSettings` or `Packages` either. And `git log
--diff-filter=A` cannot date the import: this clone is **shallow**, so it reports the graft-boundary
commit (register §6). A **full clone** would give *when and by whom*; it would still not give *was
anything signed*.

**Who can answer:** whoever ran the evaluation, and the Audiokinetic account holder — the same person
as the Asset Store account holder for the §2 entitlement rows above.

**What to do with each answer:**

| Answer | Action |
|---|---|
| **No licence was ever signed** | Record that here and close the row. Nothing further is owed. |
| **An evaluation licence was signed** | Close it out with Audiokinetic in writing. An evaluation agreement is per-title and does not lapse just because the integration was removed; the integration's removal is the evidence that no Wwise runtime ships, which is what makes closing it cheap. |
| **A project licence was signed** | Same, plus check whether anything was paid and whether it is recoverable or transferable. |

**This row does not block anything.** It is paperwork, it was never a code question, and it is
recorded as outstanding rather than quietly dropped so that it cannot be mistaken — by the absence of
the folder — for something that was settled.

---

## §3 · Disposition of every vendored SDK

`ships?` per Unity's real rules (§1 of the register): `Editor/` never ships, an `.asmdef`'s
`includePlatforms` decides, no-asmdef code lands in `Assembly-CSharp` and ships, and **any**
`Resources/` folder is packed whole.

| SDK | Ships? | First-party coupling (measured) | Disposition |
|---|---|---|---|
| **FMOD** | ships | the whole audio layer | **KEEP** — credit line + tier |
| **Obvious SOAP** | ships | **128** files | **KEEP** — receipt only |
| **NiceVibrations** | ships (incl. Demo, 30 `.cs`) | ~~**1** file, **5** API symbols~~ → **0** | ✅ **REPLACED** (row 4). Folder still ships for rows 4b/5 |
| ├ demo sprites | ship | ~~6 usages in 5 shipped locations~~ → **0** | ✅ **REPLACED** (row 5) — `_Graphics/UI/Chrome/` |
| └ `HapticSamples/*.wav` | ship | ~~4 clips on legacy `AudioClip` fields~~ → **0** | ✅ **REPLACED** (row 5) — FMOD `EventReference`s, shipped empty |
| **Shift Sci-Fi UI** | ships (+ `Resources/`) | **0** code files; 3 textures + 1 prefab | **REPLACE** (row 6) |
| **PrimitivePlus** | ships (+ 47-mesh `Resources/`) | **0** code files; 1 component on 3 prefabs, 4 meshes on 16 prefabs + 1 shadergraph | **REPLACE** (row 6) |
| **DOTween** | ships | **44** files | **KEEP** — vendor the licence |
| **NativeShare** | ships | **4** files | **KEEP** — vendor the licence |
| **PlayFabSDK** | ships | **20** files (inert backend) | **DEFER** — launch-blocker index §B2 |
| **PlayFabEditorExtensions** | editor-only ✅ | — | **KEEP** — costs the player nothing |
| **QuickScene Pro** | editor code, but `Resources/` (1.8 MB) ships | editor tool | **KEEP, move the `Resources/`** — index §A2 |
| **EDM4U** | editor-only ✅ | — | **KEEP** — Apache 2.0, licence present |
| **Microsoft.Unity.Analyzers** | compile-time | — | **KEEP** |
| **TextMesh Pro** | ships (ONE `Resources/`) | fleet-wide | **KEEP**. Examples & Extras ✅ **pruned 12 Sep 2026** — index §A1 |
| ~~**Parse**~~ | — | **0** references, **0** code | ✅ **DELETED** 12 Sep 2026 |
| ~~**Wwise**~~ | — | **0** | ✅ **DELETED** 12 Sep 2026 (licence question open — §2.1) |
| ~~**SerializeInterface**~~ | the attribute ships; the drawer is `Editor/` | **6** live files (+1 commented out) | ✅ **REWRITTEN first-party** 12 Sep 2026 |
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

**Landed 12 Sep 2026** at that estimate, near enough: one file rewritten
(`HapticController.cs`), two small ones added (`GamepadRumblePattern` — the vendor struct's shape,
`GamepadRumblePlayer` — the segment stepper), and **zero** assets, art or authored numbers touched.
`HapticController`'s public surface is byte-for-byte unchanged, so none of the 16 call sites moved.
Three prose sites that named the vendor while stating a real constraint were re-derived against the new
player rather than re-worded (`GunSpreadProfile`, `GunSprayAccuracy`, `SPARROW_SPRAY_ACCURACY.md`), and
the spray's 45 ms cadence floor survives the re-derivation unchanged — see `Docs/HAPTICS.md`
§ "The cadence floor" for the measured table behind it.

Two things the swap **gained**, neither asked for: a pad connected to a phone now rumbles (the plugin
compiled its gamepad path out on iOS and Android entirely), and the motors are now explicitly stopped on
play-mode exit, quit, pause, focus loss and mid-clip unplug. One thing it **dropped**, deliberately and
on the record: mobile *device* pattern haptics are silent rather than faked with `Handheld.Vibrate()`.
The `.haptic` JSON is retained as the portable authored source for a future backend.

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

**Landed 12 Sep 2026.** Both halves done, and the sprite count came out at **five glyphs for six
references** rather than four for six: `CancelButton` (Menu_Main) and `Close Button` (Arcade Screen)
wore two DIFFERENT vendor glyphs while meaning the same thing, and the Style Foundation's icon list
names one *"X Button"*, so they now share `chrome_close`. That is the swap's one intentional look
change; everything else keeps its predecessor's read (a down triangle stays a down triangle). The
Termite pair collapses to one placeholder for the same reason — both slots were already standing in
for art that does not exist, and selection is signalled by the border, not by the glyph.

Three method notes worth carrying to rows 6-8, which are the same shape of job:

* **The replacements live under a first-party PATH.** Editing the vendor PNG in place and keeping
  its guid is a one-line change that works — and hides itself: the asset path still says
  `NiceVibrations/Demo/…`, so the next audit re-reports it and the next `git clean` or plugin
  re-import silently restores demo art.
* **A sub-sprite reference is `{fileID, guid}` and the fileID is load-bearing.** The four shipped
  `RegularPresetsIcons.png` usages point at four DIFFERENT sub-sprites of one sheet, so a guid-only
  swap would have silently re-pointed all four at whatever sub-sprite happened to carry that
  fileID. Both fields were rewritten together (and `type: 2` → `type: 3` for the standalone
  `.asset` sprite, which is a different importer).
* **The proof is a whole-TREE sweep, not a check of the eight named assets.** All **411** guids
  owned under `Demo/` and `HapticSamples/` were counted against every shipped scene, prefab and
  asset: zero holders outside `MIgration_Prefabs (DELETE LATER)`, which was left untouched on
  purpose (`LAUNCH_BLOCKER_INDEX.md` §B3). Checking only the eight would have proved the eight.
  It is a tool, not a shell one-liner — **`Tools/Build/check_vendor_tree_references.py`**, a
  READER, which asserts guid OWNERSHIP per `.meta` before counting holders (a `.meta` can remap
  into another asset, so a `grep -rl | head -1` returns plausible false positives) and carries a
  `--self-test` negative control, because a gate nobody has watched fail is a gate nobody should
  trust. **Rows 6-8 and the final `Assets/NiceVibrations/` deletion each need this same evidence:
  add the tree to its `ROSTER` and run it.**

### Row 6 — Shift + PrimitivePlus

| | Shift Sci-Fi UI | PrimitivePlus |
|---|---|---|
| Size | 584 KB / 15 files | 1008 KB / 53 files |
| First-party **code** references | **0** | **0** |
| Live assets | 3 border textures, `Switch.prefab` | `PrimitivePlusMaterial.cs`, `Sphere`, `Cone`, `Cube`, `CylinderTube` |
| Live in | `Menu_Main`, `Authentication`, `ModalWindows`, `OptionsMenuContent` | 16 prefabs + `LaserGraph.shadergraph` |
| `Resources/` that ships regardless | `Shift UI Manager.asset` | **47 meshes + 1 material** |
| Replacement | 3 sprites re-authored (9-sliced frames) + a first-party toggle | 4 meshes baked first-party + a 36-line component rewritten |

Two hazards the prompt has to respect. `Cone.asset` is on **`AOEConicExplosion`** and
**`AOEConicSkyBurst`** — the Dolphin's crystal blast and the Sparrow's skyburst, both
gameplay-critical; and `Switch.prefab` carries **two `m_Script` guids owned by no `.meta` under
`Assets/`** (almost certainly package scripts — unverifiable here, because `Library/PackageCache/`
is not committed), so it must be rebuilt rather than edited in place.

### Row 7 — Effects Library

**8.2 MB, of which one prefab is load-bearing.** `vfx_Projectile_02.prefab` → `VesselJet.prefab`,
pulling exactly two materials (`Flare00_AB_2.mat`, `DistortedFlare01_AB.mat`) and their textures.
Measured: the four `FE_*` render-pipeline settings assets — which look like they might be the
project's live URP configuration — have **zero external references** and are not the live pipeline;
`Mountains01.fbx` (860 KB) has zero references; four of the five `vfx_*` prefabs have zero references.

### Row 8 — the dead drops ✅ **DONE 12 Sep 2026**

`Wwise` (84 KB, **0 asset files**, 14 orphan `.meta`) and `Parse` (76 KB, **0** references, **0**
code) are **deleted**; `SerializeInterface` (290 lines, no vendor, no licence, shipped via
`Assembly-CSharp`) is **rewritten first-party**. One human item survives, §2.1.

**Two of the measurements this row was planned from were wrong, and both corrections are worth
keeping:**

* **Six live consumers, not seven.** `AIGunner.cs`'s `[RequireInterface]` is inside a `/* */` block
  along with the `Start()` that read it, so no inspector field ever existed and the file does not
  reference the attribute at compile time. *A grep for an attribute's name counts the comments too* —
  the commented usage was indistinguishable from the six live ones in the inventory that planned this.
* **Only 141 of the 290 lines were load-bearing.** `InterfaceReference<>`, `InterfaceReferenceDrawer`
  and `InterfaceArgs` had **zero** consumers project-wide, so "rewrite 290 lines" would have meant
  writing 149 lines of unused first-party code the studio then owns and maintains. The rewrite is 48
  runtime + 219 editor lines, the larger editor figure being doc comments and the four small fixes
  recorded in the commit. *When the plan is to rewrite a drop rather than delete it, measure which
  half of it anything actually uses first — the dead half costs the same to write and more to keep.*

The cheapness of the call held up, though, and for the stated reason: the behaviour was fully
specified by the call sites, so nothing had to be reverse-engineered from the vendored code.

---

## §5 · Folder reorganisation — after, never before

Sequenced last on purpose: moving a folder rewrites paths in every `.meta`-adjacent tool and in the
`Resources/` rules, and doing it while items are still being deleted means doing it twice.

Current state, measured:

* **14 third-party folders sit at the `Assets/` root** (17 before row 8 landed), interleaved with the
  first-party `_`-prefixed ones — `NiceVibrations`, `Effects Library`, `PlayFabSDK`,
  `PlayFabEditorExtensions`, `YethGameDev`, `PrimitivePlus`, `Samples`,
  `ExternalDependencyManager`, `Shift - Complete Sci-Fi UI`, `Analyzers`, `Unity Assests` *(sic)*,
  `Adaptive Performance`, `Resolvers`, `Environment`. ~~`Wwise`~~, ~~`Parse`~~ and
  ~~`SerializeInterface`~~ were deleted 12 Sep 2026 — so three of the moves this section was
  sequenced to protect no longer have to happen at all, which is the cheapest way a folder gets
  tidied.
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
