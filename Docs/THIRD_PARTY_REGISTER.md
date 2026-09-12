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
| **1** | **FMOD in-game credit** | ❌ **Required and absent — verified** | The bundled EULA, Clause 3: *"All Products require an in game credit line which must include the words 'FMOD' and 'Firelight Technologies Pty Ltd.'"* **All tiers, no exemption.** Measured: **zero** occurrences of `Firelight`, `Made with FMOD` or `fmod.com` anywhere outside `Plugins/FMOD` itself, and **the game has no credits screen at all.** Add one before any outside build. | ✅ **Build it** — `prompts/CREDITS_SCREEN_PROMPT.md` |
| **2** | **FMOD licence tier** | ⚠️ Threshold question | Free commercial use requires **dev budget < $600k USD** *and* **gross revenue/funding < $200k USD/yr**. Froglet Inc. is a C-corp shipping paid Steam EA — a human must confirm which side of both thresholds applies, and buy the tier if not. | 💰 **Buy if the thresholds say so** |
| **3** | **Obvious SOAP 2.7.0** | ⚠️ **Commercial, no EULA in tree** | Its own README ends *"Thanks for purchasing Soap :)"* — so it was bought, by someone, at some point. **No licence file exists anywhere in `Assets/Plugins/Obvious/`.** It is the project's **primary architecture** (128 first-party `using Obvious…`), so this is not removable — it needs the receipt. | ✅ **Owner is adding the licence file** |
| **4** | **NiceVibrations 4.1.1** | ⚠️ Commercial, **no product EULA** | The two files that look like a licence are not one — see §2. Seat must cover commercial distribution. | 🔄 **Replace** — the whole dependency is **1 file, 5 API symbols**; every clip is already first-party data. `prompts/HAPTICS_VENDOR_INDEPENDENCE_PROMPT.md` |
| **4b** | **NiceVibrations `HapticSamples/*.wav` in shipped UI** | ⚠️ **Non-commercial licence claim** | **New finding, 12 Sep.** Four clips are wired into shipped UI as legacy `AudioClip` fields — `Beep1` (`CountdownTimer.countdownBeep`), `Cash5` (`onTriggerClip`), `Coins2` (`targetReachedClip`), `Keyboard3` (`TypingAudio` on `Profile`/`ModalWindows`/`Menu_Main`). Their notice **opens by declaring the pack "licensed under CCBYNC 3.0"** — *non-commercial* — while its source list is almost all CC0 and **names freesound titles, not shipped filenames**, so no clip maps to a term. | 🔄 **Replace with FMOD events** — which is the migration CLAUDE.md already mandates for new SFX. `prompts/NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md` |
| **5** | **NiceVibrations demo ART is in the shipped menu** | ⚠️ Distribution question | `RegularPresetsIcons.png` → `Menu_Main.unity` (a build scene). Asset Store EULAs treat demo content separately from the plugin. **4 distinct sprites, 6 usages** — two of them (`NVCar`, `NV7Dots`) are the **Termite class icons**, i.e. already placeholders for an unimplemented vessel. | 🔄 **Replace** — same prompt as 4b |
| **6** | **Shift Sci-Fi UI (Michsky)**, **PrimitivePlus** | ⚠️ Commercial, no licence | Both load-bearing in shipped scenes. Purchase records needed. **Measured: neither has a single first-party code reference** — Shift is 3 textures + 1 prefab, PrimitivePlus is 4 meshes + one 36-line component (with **47** meshes shipping via `Resources/`). | 🔄 **Replace** — `prompts/VENDORED_UI_PACK_PLACEHOLDERS_PROMPT.md` |
| **7** | **"Effects Library"** | ❓ Unknown vendor | Contains only `Froglet Stuff/`, but carries an `FE_` import prefix. Whose is it? **Measured: 8.2 MB of which exactly ONE prefab is live** (`vfx_Projectile_02` → `VesselJet`); the four `FE_*` render-pipeline assets are **not** the live pipeline (0 references). | 🔍 **Owner will identify** — `prompts/EFFECTS_LIBRARY_PROVENANCE_PROMPT.md` |
| **8** | **Wwise** | ⚠️ Per-title licensing | Zero files, zero references — but was it ever licensed during evaluation? Then remove. | 🗑 **Owner will confirm, then delete** — with `Parse` (0 refs) and a first-party rewrite of `SerializeInterface`. `prompts/WWISE_AND_DEAD_SDK_REMOVAL_PROMPT.md` |
| **9** | **CC-BY 4.0 models** | ✅ Not shipping today | Two Sketchfab models by **VertaScan** under CC-BY 4.0 (`_Models/Vessel Models/Placeholder/`). Measured **0 references** — they do not ship. **If the Rhino rig work ever wires one, attribution becomes mandatory.** | ⏸ Watch — credits screen covers it if ever wired |
| **10** | **EmojiOne** | ⚠️ By reference only | TMP's sample emoji sprites; the bundled note defers to EmojiOne's own terms without stating them. Ships if a TMP sprite asset is used. | ⏸ Credits screen |

---

## §1 · How "ships / editor-only" was determined

Not by folder name. Unity's actual rules, applied in order:

1. Anything under a folder named **`Editor/`** compiles to `Assembly-CSharp-Editor` and **never** ships.
2. An **`.asmdef`** with `"includePlatforms": ["Editor"]` is editor-only; `[]` compiles into the player.
3. **Code with no `.asmdef`** falls into `Assembly-CSharp` and **ships**.
4. **Any folder named `Resources/`** is packed into the player **whole, referenced or not** — unless it sits under an `Editor/` folder.
5. Everything else ships only if one of the **29 enabled build scenes** reaches it.

Rule 4 is the one that produces defects nobody notices; it produced two (index §A1, §A2).

---

## §2 · Vendored SDKs under `Assets/`

Sizes are `du -sh`; file counts exclude `.meta`.

| SDK | Vendor | Version | Path | Size / files | Licence in tree | Ships? |
|---|---|---|---|---|---|---|
| **FMOD for Unity** | Firelight Technologies | **2.03.x** *(from bundled doc URL)* | `Plugins/FMOD` | 261 MB / 196 | ✅ `LICENSE.txt` (**EULA — see §0.1, §0.2**) | **Ships** — 3 runtime asmdefs + `Resources/` |
| ├ **Resonance Audio** | **Google** (bundled inside FMOD) | not stated | `Plugins/FMOD/addons/ResonanceAudio` | — | ❌ **none for the addon** | **Ships** (`FMODUnityResonance` runtime asmdef) |
| └ **FMOD Haptics addon** | Firelight | not stated | `Plugins/FMOD/addons/Haptics` | — | ❌ none for the addon | **Ships** (`FMODUnityHaptics` runtime asmdef) |
| **Obvious SOAP** ⚠️ | **Obvious Games** (`com.obvious.soap`) | **2.7.0** | `Plugins/Obvious` | 7.1 MB / 149 | ❌ **none** — README says *"Thanks for purchasing"* | **Ships** (`Obvious.Soap` runtime asmdef) |
| **NiceVibrations** ⚠️ | Lofelt / More Mountains | **4.1.1** (Lofelt SDK 1.3.3) | `NiceVibrations` | 47 MB / 395 | ⚠️ **not the product EULA** — see below | **Ships**, incl. the **Demo** (30 `.cs`, not editor-only) + `Resources/` |
| **DOTween** (free — **not** Pro) | Demigiant / D. Giardini | asm `1.0.0.0`; no product version in tree | `Plugins/Demigiant` | 764 KB / 18 | ❌ none | **Ships** (`DOTween.dll`) |
| **NativeShare** | yasirkula | not stated | `Plugins/NativeShare` | 148 KB / 10 | ❌ none | **Ships** (`NativeShare.Runtime`) |
| **PlayFab SDK** | Microsoft | not stated | `PlayFabSDK` | 4.7 MB / 104 | ❌ none | **Ships** — `PlayFab.asmdef` `includePlatforms: []` + `Resources/` |
| **PlayFab Editor Extensions** | Microsoft | not stated | `PlayFabEditorExtensions` | 4.9 MB / 70 | ❌ none | **Editor-only** ✅ |
| ├ **Microsoft.Identity.Client (MSAL)** | Microsoft | not stated | `…/Editor/Resources/` | — | ❌ none | Editor-only |
| ├ **Microsoft.IdentityModel** (`.JsonWebTokens`, `.Logging`, `.Tokens`) | Microsoft | not stated | `…/Editor/Resources/` | — | ❌ none | Editor-only |
| └ **System.IdentityModel.Tokens.Jwt** | Microsoft | not stated | `…/Editor/Resources/` | — | ❌ none | Editor-only |
| **QuickScene Pro** | yethgamedevv | © 2025 | `YethGameDev` | 4.4 MB / 10 | ✅ `LICENSE.md` — **MIT** (+ Indian jurisdiction clause) | Editor code only, but its **`Resources/` (1.8 MB) ships** |
| **PrimitivePlus** ⚠️ | *unknown* (ns `PrimitivePlus`) | not stated | `PrimitivePlus` | 1008 KB / 53 | ❌ none | **Ships** — 5 runtime `.cs` + `Resources/` |
| **Shift — Complete Sci-Fi UI** ⚠️ | **Michsky** (ns `Michsky.UI.Shift`) | not stated | `Shift - Complete Sci-Fi UI` | 584 KB / 15 | ❌ none | **Ships** — 4 runtime `.cs` + `Resources/` |
| **External Dependency Manager (EDM4U)** | **Google** | **1.2.183** | `ExternalDependencyManager` | 816 KB / 9 | ✅ `LICENSE` (**Apache 2.0**) | **Editor-only** ✅ |
| **Microsoft.Unity.Analyzers** | Microsoft | not stated | `Analyzers` | 256 KB / 2 | ❌ none (`README.md` only) | Roslyn analyzer — compile-time |
| **TextMesh Pro** (imported assets) | Unity | bundled | `Unity Assests/TextMesh Pro` | ~15 MB / ~190 | — (Unity UCL) | **Ships** — both `Resources/` |
| ├ **EmojiOne** sample sprites | EmojiOne | — | `…/TextMesh Pro/Sprites` | — | ⚠️ `EmojiOne Attribution.txt` — defers to their terms | Ships if used |
| └ **Roboto** font | Google | — | `…/Examples & Extras/Fonts` | — | ✅ `Roboto-Bold - License.txt` | Ships if used |
| **Parse** (support DLLs) | Parse / Meta | not stated | `Parse` | 76 KB / 2 | ❌ none | Ships if referenced — **nothing references it** |
| **Wwise** ⚠️ | Audiokinetic | — | `Wwise` | 84 KB / **0 files** | — | **Ships nothing** (14 `.meta`, 0 assets) |
| **SerializeInterface** | ❓ **unknown** | — | `SerializeInterface` | 56 KB / 5 | ❌ none, no namespace, no header | **Ships** (no asmdef → `Assembly-CSharp`) |
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
repository**, because `Library/PackageCache/` is not committed.

| Package | Actual upstream project | Commonly licensed *(unverified here)* | Direct? |
|---|---|---|---|
| `com.unity.nuget.newtonsoft-json` **3.2.1** | **Newtonsoft.Json** (James Newton-King) | MIT | direct |
| `com.unity.nuget.mono-cecil` **1.11.6** | **Mono.Cecil** (Jb Evain) | MIT | **transitive** |
| `com.unity.ext.nunit` **2.0.5** | **NUnit** | MIT | **transitive** |
| `com.unity.bindings.openimageio` **1.0.2** | **OpenImageIO** | BSD-3-Clause | **transitive** |
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

**`Assets/SerializeInterface/` (5 `.cs`).** No namespace, no header comment, no licence, no vendor.
It is the well-known community `[RequireInterface]` pattern, which exists in several public repos
under different licences. It **ships** (no asmdef).
**Who would know:** whoever added `[RequireInterface]` to the project.

**`Assets/PrimitivePlus/`, `Assets/Shift - Complete Sci-Fi UI/`, `Assets/Unity Assests/`.** No
vendor, version or import record recoverable. **Git cannot supply it:** this clone is **shallow**
(`.git/shallow` present, 741 commits, history begins 2026-08-12), so `git log --diff-filter=A`
reports the graft-boundary commit for every one of them. A **full clone** would answer *when and by
whom*; it would still not answer *was it paid for*.

---

## §7 · Attribution obligations, collected

Every in-product attribution this tree can prove is owed. **The game currently has no credits screen
of any kind**, so all of these are outstanding.

| Source | Obligation | Status |
|---|---|---|
| **FMOD** (EULA cl. 3) | In-game credit containing **"FMOD"** and **"Firelight Technologies Pty Ltd."** — **all tiers** | ❌ **absent — verified project-wide** |
| **CC-BY 4.0 models** (VertaScan) | Attribution if distributed | ✅ not shipping today (0 refs) |
| **EmojiOne** | Per EmojiOne's own terms | ⚠️ ships if a TMP sprite asset is used |
| **Roboto** | Apache 2.0 notice | ⚠️ ships if the font is used |
| **Google EDM4U** (Apache 2.0) | NOTICE preservation | editor-only — does not ship |
| **QuickScene Pro** (MIT) | Copyright notice with "substantial portions" | 1.8 MB of it currently ships (index §A2) |
| **Every MIT package** (UniTask, Reflex, ParrelSync, Newtonsoft, Cecil, NUnit) | MIT requires the notice to travel with the software | ❌ no aggregated notice exists |

**The cheapest complete fix is one credits screen** listing FMOD's required line plus a third-party
notices block. That single screen discharges FMOD cl. 3, every MIT notice, EmojiOne, Roboto, and
CC-BY if a placeholder model is ever wired.

---

## §8 · Credentials

Swept by **location and filename only — no values were read or printed.**

Searched `Assets/`, `ProjectSettings/`, `Tools/` for `*.keystore`, `*.jks`, `*.p12`, `*.pem`,
`*.pfx`, `*.mobileprovision`, `.env*`, `google-services.json`, `GoogleService-Info.plist`,
`*credential*`, `*secret*`. **Nothing found** — the only hits were a music track named
"SECRET HIDE AWAY" and its SO. `ProjectSettings.asset` carries no Android keystore or alias fields.

This is a filename-and-known-location sweep, **not** a content scan for embedded API keys. Note that
PlayFab title IDs and UGS project IDs are configuration rather than secrets, and were not audited.
A content-level secret scan is a separate job and is not claimed here.
