# Third-party register

Every third-party component in `Assets/` and `Packages/manifest.json`, with **whether it reaches a
player build** and **the entitlement question a human has to answer**.

**Measured 11 Sep 2026** against `claude/zealous-davinci-vjwev0`. Sizes are `du -sh`; file counts
exclude `.meta`. Re-verify before acting — this drifts.

> **This register records what is PRESENT. It does not — and cannot — record what was PAID FOR.**
> The repository proves a folder exists. Only a purchase record proves we may distribute it. Every
> "entitlement question" below is addressed to **whoever holds the studio's Unity Asset Store
> account**; nothing here asserts a licence status that is not backed by a document in the tree.

Companion: **[`LAUNCH_BLOCKER_INDEX.md`](LAUNCH_BLOCKER_INDEX.md)** — what should not ship, with a
measured reference check and a verdict per candidate.

---

## How "ships / editor-only" was determined

Not by folder name. Unity's actual rules, applied in this order:

1. **Anything under a folder named `Editor/`** is compiled into `Assembly-CSharp-Editor` and is
   **never** in a player build.
2. **An `.asmdef` with `"includePlatforms": ["Editor"]`** is editor-only. An `.asmdef` with
   `"includePlatforms": []` compiles into the player.
3. **Code with no `.asmdef`** falls into `Assembly-CSharp` and **ships**.
4. **Any folder named `Resources/`** is packed into the player **whole, whether or not anything
   references it** — unless it sits under an `Editor/` folder, which excludes it.
5. **Everything else** ships only if a scene in `EditorBuildSettings` (29 enabled, measured)
   reaches it, directly or through a prefab.

Rule 4 is the one that produces defects nobody notices, and it produced two here.

---

## Register — `Assets/`

| Component | Vendor | Version | Path | Size / files | Licence doc in tree | Ships? |
|---|---|---|---|---|---|---|
| **FMOD for Unity** | Firelight Technologies | 2.03.x *(from bundled doc URL; no version constant found)* | `Plugins/FMOD` | 261 MB / 196 | ✅ `LICENSE.txt` | **Ships** — `FMODUnity`, `FMODUnityResonance`, `FMODUnityHaptics` are runtime asmdefs; `Resources/` ships |
| **NiceVibrations** | Lofelt / More Mountains | **4.1.1** (Lofelt Studio SDK 1.3.3) | `NiceVibrations` | 47 MB / 395 | ⚠️ **No product EULA** — see note below | **Ships** — incl. `Lofelt.NiceVibrations.Demo` (30 `.cs`, not editor-only) and `Scripts/Components/Resources/` |
| **TextMesh Pro** (imported assets) | Unity Technologies | bundled w/ Unity 6 | `Unity Assests/TextMesh Pro` | ~15 MB / ~190 | — (Unity UCL) | **Ships** — both `Resources/` folders; 34 example `.cs` in `Assembly-CSharp` |
| **Adaptive Performance settings** | Unity Technologies | pkg 5.1.6 | `Unity Assests/Adaptive Performance` | small / 7 | — (Unity UCL) | **Ships** — wired into `ProjectSettings.asset` |
| **PlayFab SDK** | Microsoft / PlayFab | not stated in tree | `PlayFabSDK` | 4.7 MB / 104 | ❌ none | **Ships** — `PlayFab.asmdef` has `includePlatforms: []`; `Shared/Public/Resources/` ships |
| **PlayFab Editor Extensions** | Microsoft / PlayFab | not stated | `PlayFabEditorExtensions` | 4.9 MB / 70 | ❌ none | **Editor-only** ✅ (`includePlatforms: ["Editor"]`) |
| **DOTween** (free — **not** Pro) | Demigiant (Daniele Giardini) | assembly `1.0.0.0`; no product version in tree | `Plugins/Demigiant` | 764 KB / 18 | ❌ none | **Ships** (`DOTween.dll`); `DOTweenEditor.dll` is editor |
| **Obvious SOAP** | Obvious Game | **2.7.0** (`package.json`) | `Plugins/Obvious` | 7.1 MB / 149 | ❌ none | **Ships** (`Obvious.Soap` runtime asmdef) |
| **NativeShare** | yasirkula | not stated in tree | `Plugins/NativeShare` | 148 KB / 10 | ❌ none | **Ships** (`NativeShare.Runtime`) |
| **ParrelSync** (settings asset only) | VeriorPies | — | `Plugins/ParrelSync` | 20 KB / 1 | ❌ none | **Does not ship** — see §ParrelSync |
| **QuickScene Pro** | yethgamedevv | © 2025 | `YethGameDev` | 4.4 MB / 10 | ✅ `LICENSE.md` (**MIT**, Indian jurisdiction clause) | **Editor code only**, but its **`Resources/` (1.8 MB) ships** |
| **PrimitivePlus** | *unknown* (namespace `PrimitivePlus`) | not stated | `PrimitivePlus` | 1008 KB / 53 | ❌ none | **Ships** — 5 runtime `.cs` + `Resources/` |
| **Shift — Complete Sci-Fi UI** | **Michsky** (namespace `Michsky.UI.Shift`) | not stated | `Shift - Complete Sci-Fi UI` | 584 KB / 15 | ❌ none | **Ships** — 4 runtime `.cs` + `Resources/` |
| **External Dependency Manager (EDM4U)** | Google | **1.2.183** | `ExternalDependencyManager` | 816 KB / 9 | ✅ `LICENSE` (Apache 2.0) | **Editor-only** ✅ (all under `Editor/`) |
| **Parse** (support DLLs) | Parse / Meta | not stated | `Parse` | 76 KB / 2 | ❌ none | **Ships if referenced** — nothing references it (see index) |
| **Wwise** | Audiokinetic | — | `Wwise` | 84 KB / **0 files** | — | **Ships nothing** — 14 `.meta` files, zero assets |
| **Shader Graph samples** | Unity Technologies | 12.1.6 | `Samples` | 972 KB / 14 | — (Unity UCL) | Ships if referenced |
| **Microsoft.Unity.Analyzers** | Microsoft | not stated | `Analyzers` | 256 KB / 2 | ✅ `README.md` *(not a licence)* | Roslyn analyzer — compile-time only |
| **"Effects Library"** | ⚠️ **unknown — see §Unknown provenance** | — | `Effects Library` | 8.2 MB / 25 | ❌ none | **Ships** — `vfx_Projectile_02` is nested in `VesselJet.prefab` |

### `Packages/manifest.json` — 77 dependencies

Clean by comparison. **Three non-Unity**, all from git, all permissive, no scoped registries:

| Package | Vendor | Licence (upstream) | Ships? |
|---|---|---|---|
| `com.cysharp.unitask` | Cysharp | MIT | Ships (runtime) |
| `com.gustavopsantos.reflex` **14.1.0** | Gustavo Santos | MIT | Ships (runtime) |
| `com.veriorpies.parrelsync` | VeriorPies | MIT | **Editor-only upstream — unverifiable here**, see §ParrelSync |

The other 74 are Unity first-party (`com.unity.*`), covered by the Unity Companion / UCL terms that
come with the Editor licence. One is worth a line: **`com.unity.pipeline` 0.5.0-exp.1** is the CLI
pipeline package and is already hard-gated out of release builds by
`Assets/_Scripts/Editor/Build/UnityPipelineReleaseGuard.cs`.

---

## ⚠️ Entitlement questions — for whoever holds the Asset Store account

These are the only questions that matter before an outside build. Each names what is in the tree so
the answer can be checked against a purchase record.

1. **NiceVibrations 4.1.1 (47 MB, Lofelt / More Mountains).** Is there a purchase on the studio
   account, and does the seat cover distribution in a commercial Steam build?
   **The two files that look like a licence are not one:** `3RD-PARTY-LICENSES.md` is the *Rust
   crate list for the Lofelt SDK*, and `HapticSamples/HapticPackCClicense.txt` covers the *haptic
   sample clips*. **Neither is the product EULA.** *(The starting inventory recorded this component
   as "licence file present"; measured, the entitlement document is absent.)*
2. **NiceVibrations — the demo art specifically.** `Demo/` art is **in the shipped main menu**
   (measured: `RegularPresetsIcons.png` → `Menu_Main.unity`, a build scene). Asset Store demo
   content is not always licensed for redistribution in a shipped product on the same terms as the
   plugin. **Does the licence cover shipping the demo art?** If not, this is an art task, not a
   delete task — see index §B1.
3. **Shift — Complete Sci-Fi UI (Michsky).** Purchased on the studio account? Its border textures
   are used in `Menu_Main.unity` and `Authentication.unity`.
4. **PrimitivePlus.** Purchased? Its meshes are load-bearing in the projectile and AOE prefabs.
5. **"Effects Library" (8.2 MB).** **Whose is it?** See §Unknown provenance — this is the one where
   the repository cannot answer and the answer matters.
6. **FMOD for Unity 2.03.** Which tier applies at the studio's revenue, and **is the required FMOD
   attribution present in-game?** No in-game credits/attribution screen was found in this pass; the
   only FMOD attribution in the tree is `Plugins/FMOD/LICENSE.txt`, which a player never sees.
7. **Wwise.** Licensing is per-title, and the folder contains **zero** assets (14 `.meta` files) and
   **zero** first-party references (no `AkSoundEngine` anywhere — audio is FMOD). Confirm no Wwise
   licence obligation was ever incurred, then it can be removed as a repository-hygiene item.
8. **QuickScene Pro** is **MIT** — distributable — but MIT requires the copyright notice to travel
   with "copies or substantial portions". Its 1.8 MB `Resources/` currently ships into the player
   (index §A2). Cleanest answer is to stop shipping it rather than to add an attribution.

---

## ParrelSync — confirmed for `Assets/`, **flagged** for the package

Required by R14's definition of done, so stated precisely.

**Confirmed, measured:** the `Assets/` side **cannot ship**.
`Assets/Plugins/ParrelSync/` holds exactly one file, `ParrelSyncProjectSettings.asset`. It has
**0 inbound references** (guid `aa4d7717a7cbf7a4bbbc940124c221ab`, checked across every `.unity`,
`.prefab` and `.asset` in the project), and it is in neither a `Resources/` nor an `Editor/` folder
— so rule 5 applies and nothing pulls it into a build.

**Flagged, not confirmed:** the **package** side is **unverifiable in this repository**.
`com.veriorpies.parrelsync` is consumed from git and `Library/` is not committed, so the package's
`.asmdef` is not present in the tree and I cannot read whether it is `includePlatforms: ["Editor"]`.
ParrelSync is editor-only by upstream design, but **this pass did not see that file and does not
assert it.**

**To close it**, one of:
- read the resolved package's `.asmdef` on a machine where the package has been restored, or
- grep a release `Player.log` / the IL2CPP link output from **R1** for `ParrelSync`, or
- extend the existing guard. `UnityPipelineReleaseGuard` already implements exactly this pattern —
  an `IPreprocessBuildWithReport` that fails a non-development build carrying a dev-tool's runtime
  surface — for `com.unity.pipeline`. **There is no equivalent guard for ParrelSync.** A second
  guard, or a generalisation of that one, would make this a build-time guarantee instead of a
  recurring manual check.

---

## Unknown provenance — the repository cannot answer these

Per R14's constraint, these are recorded as *unknown* rather than guessed.

**`Assets/Effects Library/` (8.2 MB).** Its only child is `Froglet Stuff/`, which reads as
first-party — but the parent folder is named like an imported VFX pack, and the render-pipeline
assets inside carry an `FE_` prefix (`FE_ForwardRenderer`, `FE_UniversalRP-HighQuality`,
`FE__GlobalVolumeProfile`) that looks like an import prefix rather than a Froglet one. It is
load-bearing either way (`vfx_Projectile_02.prefab` is nested in `VesselJet.prefab`, documented in
`Docs/VESSEL_TAIL_AND_JETS.md`). **Unknown:** whether this is studio-authored or a re-foldered
commercial pack.
**Who would know:** whoever added the vessel jet FX, and the holder of the Asset Store account.

**`Assets/PrimitivePlus/`, `Assets/Shift - Complete Sci-Fi UI/`, `Assets/Unity Assests/`.** No
vendor, version, invoice or import record is recoverable. **Git history cannot supply it:** this
clone is **shallow** (`.git/shallow` present, 741 commits, history begins 2026-08-12), so
`git log --diff-filter=A` reports the graft-boundary commit for every one of these folders rather
than the commit that introduced them. A **full clone** would answer "when and by whom"; it would
still not answer "was it paid for".

---

## Credentials

Swept by **location and filename only — no values were read or printed.**

Searched `Assets/`, `ProjectSettings/`, `Tools/` for `*.keystore`, `*.jks`, `*.p12`, `*.pem`,
`*.pfx`, `*.mobileprovision`, `.env*`, `google-services.json`, `GoogleService-Info.plist`,
`*credential*`, `*secret*`.

**Nothing found.** The only filename hits were false positives — a music track named
"SECRET HIDE AWAY" and its SO. `ProjectSettings.asset` carries no Android keystore or alias fields.

This is a filename-and-known-location sweep, **not** a content scan for embedded API keys. A
secret-scanning pass over file *contents* is a separate job and is not claimed here.
