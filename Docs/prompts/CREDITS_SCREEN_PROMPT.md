# Prompt — build the in-game credits screen (the FMOD credit line, and everything it carries with it)

> **STATUS: EXECUTED 12 Sep 2026.** What was built, and the evidence for each obligation, is in
> `Docs/THIRD_PARTY_REGISTER.md` §7. Two things this prompt asked for were **not** delivered as
> written and are recorded there: the screen has **not been opened in the Unity editor** (no compile,
> no play mode — the gates prove the DATA, not the rendering), and the settings-panel row is
> **generated as a fallback** rather than authored in `SettingsModal.prefab`, because authoring a
> prefab needs an editor. Wiring `creditsButton` stands the fallback down. Kept for the record.

Paste everything below into a fresh session.

---

**Cosmic Shore has no credits screen of any kind, and shipping without one breaches the FMOD EULA.**

`Assets/Plugins/FMOD/LICENSE.txt`, clause 3, verbatim (lines 86–90):

> **3. CREDITS**
>
> All Products require an in game credit line which must include the words "FMOD"
> and "Firelight Technologies Pty Ltd." Refer to www.fmod.com/attribution for
> examples.

**This applies on every tier — free, Indie and Basic alike.** It is not contingent on the licence
tier question (which is a separate, money-side decision). Measured on `claude/zealous-davinci-vjwev0`:
the strings `Firelight`, `Made with FMOD` and `fmod.com` appear **nowhere** in the repository outside
`Assets/Plugins/FMOD` itself, and there is no credits screen, modal, panel or scene.

Read `Docs/THIRD_PARTY_REGISTER.md` §7 and `Docs/THIRD_PARTY_DECISIONS.md` §1 before writing anything.

## Why this is one job and not seven

§7 of the register collects **every** in-product attribution the tree can prove is owed. They are all
outstanding for the same reason — there is nowhere to put them:

| Source | Obligation |
|---|---|
| **FMOD** (EULA cl. 3) | An in-game credit containing **"FMOD"** and **"Firelight Technologies Pty Ltd."** |
| **UniTask** (Cysharp), **Reflex** (Gustavo Santos), **ParrelSync** (VeriorPies) | MIT — the notice must travel with the software |
| **Newtonsoft.Json** (James Newton-King), **Mono.Cecil** (Jb Evain), **NUnit** | MIT, via `com.unity.nuget.*` / `com.unity.ext.nunit` |
| **OpenImageIO** | BSD-3-Clause, via `com.unity.bindings.openimageio` |
| **QuickScene Pro** (yethgamedevv) | MIT — notice with "substantial portions" |
| **Roboto** (Google) | Apache 2.0 notice — ships if the font is used |
| **EmojiOne** | Per EmojiOne's own terms — ships if a TMP sprite asset is used |
| **VertaScan** Sketchfab models | CC-BY 4.0 — **only if** a placeholder vessel model is ever wired (0 references today) |

**One screen discharges all of it.** Build the screen once, with a data layer that makes adding the
next notice an asset edit.

## What to build

### 1. The data — `CreditsManifestSO`

Per the project's config-separation rule, the credit text is **authored data, not string literals in
code**. One ScriptableObject at `Assets/Resources/CreditsManifest.asset` so the runtime loads it with
no per-scene wiring — the same shape as `ControlGlyphSet`, `SpeedTunnelConfig`,
`PrismOcclusionConfig` and every other fleet-wide config in that folder.

Shape it as ordered **sections**, each with a heading and a list of entries (name, role/one-line,
optional notice body). Two sections are structural rather than authored taste:

* a **middleware** section, which is where the FMOD line lives;
* a **third-party notices** section carrying the MIT/BSD/Apache bodies.

Do not model licences as an enum. A notice is prose; a typed licence field forces the UI to carry a
formatter per licence kind, which is the same trap `Docs/CODEX.md` records for stats.

### 2. The screen

`Menu_Main` is the only place this has to be reachable from. The project already has the machinery:

* `ScreenSwitcher` owns the modal stack, the return-to-modal preference and the close sweeps
  (`ScreenSwitcher.ModalWindows`, `OpenModal`). **Add a modal type; do not call `ModalWindowIn`
  yourself** — `Docs/HomeHub/ARCHITECTURE.md` records why a second authority over a modal's lifecycle
  is the wrong shape.
* `ModalWindowManager` is the base class for a modal window (open/close animation, audio, stack
  integration).
* `GameSettingsPanelController` is the 4-tab options panel and already routes rows to
  `Application.OpenURL` for links — **a CREDITS row there is the cheapest correct entry point.**

Make it a **scrolling** panel and read `Docs/README.md`'s ScrollRect warning first: a ScrollRect's
Content is a *scroll extent*, not a layout frame, and a section below the reachable range is clipped
**and eats the press**. Measure the content height after populating, do not author it.

Do not invent art. Reuse the existing modal chrome; the ability lockup's `TrapezoidGraphic` and
`LockupBloom` are the house surfaces if a header plate is wanted (`Docs/ABILITY_LOCKUP.md`).

### 3. The guard — this is the part that makes it stay fixed

**A credits screen that can be silently emptied is worth very little.** The project already has
exactly the right precedent: `Assets/_Scripts/Editor/Build/UnityPipelineReleaseGuard.cs` is an
`IPreprocessBuildWithReport` that throws `BuildFailedException` on any **non-development** build whose
shipped content would include a dev-only runtime component.

Write the sibling: a build preprocessor that **fails a non-development build** when

* `Assets/Resources/CreditsManifest.asset` is absent, **or**
* its authored text does not contain both `FMOD` **and** `Firelight Technologies Pty Ltd.`

Development builds stay exempt, exactly as the pipeline guard does it. Match that file's structure and
error-message style rather than inventing a new one.

Add an offline gate too — `python3 Tools/Build/check_credits_manifest.py --check`, no Unity needed,
parsing the asset YAML for the same two strings — so the answer is available in CI and in a session
that cannot open the editor. Give it a `--self-test` that proves it FAILS on a manifest with the FMOD
line removed; per this project's tooling rule, *a gate nobody has watched fail is a gate nobody should
trust*.

## Constraints

* **Quote FMOD's required words exactly.** "FMOD" and "Firelight Technologies Pty Ltd." — including
  the trailing period, which is inside the quoted requirement. `www.fmod.com/attribution` shows
  accepted forms; the conventional one is *"Made with FMOD Studio by Firelight Technologies Pty Ltd."*
* **Do not assert a licence status the tree cannot prove.** For the git packages (UniTask, Reflex,
  ParrelSync) the licence text is *not vendored* — fetch each project's actual `LICENSE` and paste the
  real notice; do not write "MIT" from memory and move on.
* **Leave the CC-BY models out until they are wired.** `_Models/Vessel Models/Placeholder/` measures
  **0 references**, so it does not ship and crediting it would be a false statement about the build.
  Add a line to `Docs/VESSEL_CONSTRUCTION_FOLLOWUP.md` instead: wiring one makes the attribution
  mandatory.
* **Do not put the credits only on the Steam store page.** The clause says *in game*.
* **Reachability is part of the deliverable.** A screen no build scene can open does not discharge
  anything. Verify from `Menu_Main` in the editor and say so.

## Definition of done

1. `CreditsManifestSO` + `Assets/Resources/CreditsManifest.asset`, authored with every §7 obligation.
2. A credits modal reachable from `Menu_Main` (settings row or home-hub entry), scrolling correctly to
   its last line.
3. The build guard, refusing a release build with no FMOD line, plus the offline `--check` with a
   passing `--self-test`.
4. `Docs/THIRD_PARTY_REGISTER.md` §0 row 1 and §7 updated from ❌ to ✅ **with the evidence** — not
   the claim.
5. Verified in the editor (`/verify-unity`), or an explicit statement that it was not, per CLAUDE.md.
