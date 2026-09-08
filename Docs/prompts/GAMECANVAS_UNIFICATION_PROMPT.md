# Prompt — retire the GameCanvas fork, one canvas, tool-driven

Paste everything below into a fresh session.

---

Retire `Assets/_Prefabs/GameCanvas-SkimRace.prefab`. The project must end with **one** in-game
canvas prefab — `Assets/_Prefabs/CORE/GameCanvas.prefab` — and no scene carrying a wall of
overrides on it. An in-game UI redesign could start any week, and today "just restyle the prefab"
is impossible for 15 scenes because a scene override always beats the prefab.

**Build a tool that does this, and drive the scenes through it.** I will run it per scene in the
editor. Minimise what I have to do by hand; anything you can decide by measurement, decide in code.

Read `Docs/GAMECANVAS.md` and `Docs/UI_ARCHITECTURE_AUDIT.md` §0.3 F1 + §5.1 first.

## What is already measured (2026-09-07, re-verify before acting — these will drift)

Parsed from scene YAML, so treat as a starting point, not gospel.

**Scope has grown since `Docs/GAMECANVAS.md` was written — it says 6 scenes, it is now 15.**
Every mode added since (Drumfire, Hijack, Switchback, Salvo, Scarab Scramble, Dog Fight, Bends,
Wildlife Liberation, Peel the Cage, Rampage, Brood Rush, Astro League…) was cloned from a
fork scene and inherited the whole override blob.

| | |
|---|---|
| On `GameCanvas-SkimRace` | **15** scenes, ~1,766–1,774 overrides each |
| On `CORE/GameCanvas` | 10 scenes (2v2 Co-op, Cellular Duel ×2, Freestyle MP, Maelstrom, WildlifeBlitz ×2, Benchmark, 2 Recording Studio), 76–658 overrides |
| Distinct override keys across the 15 | **1,790** |
| → present in EVERY scene, **byte-identical** | **1,729** — these belong in the prefab |
| → present in every scene, **values differ** | **20** |
| → present in only some scenes | **41** |

**The 20 differing keys are mostly not per-mode config.** Itemised:

- **8 are objectReference wiring**: `gameController`, `hexRaceController`, `multiplayerController`,
  `playerCardContainer`, `replayButton`, `statsToTrack.Array.data[0..2]`. Note `Docs/GAMECANVAS.md`
  records that the Ready button and the Scoreboard already resolve the scene's controller
  themselves — so some of these are hand-wiring of something the canvas can find, which the rules
  forbid. Check each before preserving it.
- **`statsToTrack.Array.size`** — `3` in 14 scenes, `5` in one. This is the **one genuinely
  per-mode row** the audit identified. It survives.
- **`m_OnClick…m_TargetAssemblyTypeName`** on fileID `3870071580335586427` — 10 scenes serialize
  `CosmicShore.Gameplay.RampageController`, one `AstroLeagueController`, one `BroodRushController`,
  one `JoustController`. A UnityEvent bound to a **concrete controller subclass**, which CLAUDE.md
  explicitly forbids, and 10 of them are clone artifacts naming a mode that scene is not. Unity
  resolves the call from the live target's type, not this string, so it is cosmetic *and* it is
  exactly the drift the rule exists to stop. Re-bind to the base or let the canvas self-resolve.
- **4 keys on fileID `8859118550345700281`** (the in-game toast feed rect) — 12 scenes agree,
  Joust is the outlier at `m_AnchoredPosition (-1416.3756, -463.03168)` against everyone else's
  `(-314.4, 90)`. Almost certainly off-screen. Normalise, do not preserve.
- **`m_MatchWidthOrHeight`** — `0` in 14 scenes, `1` in one. A real canvas-scaler difference; find
  out which is intended before flattening it.
- **3 more anchored positions** where 14 scenes agree and one drifted.

So the honest bill is: **~1,729 overrides to push into the prefab, ~1 row of real per-mode config
to preserve, and ~19 drifted values to normalise or deliberately keep.**

## The structural blocker — do this before any guid swap

The fork is a **superset**. A naive guid swap silently deletes UI.

- `CORE/GameCanvas.prefab` — 129 GameObjects
- `GameCanvas-SkimRace.prefab` — 178 GameObjects
- Only in the fork (**19**): `AllyDomainContainer`, `DomainScoreBar`, `TeamScorecard` ×3,
  `Player1ScoreCard`, `MultiplayerPlayerScoreCard`, `Player (1)`, `Player (2)`, `ScoreBG`,
  `TeamNameBG`, `CrystalDisplayBG`, `XPDisplayBG`, `XPEarned`, `XPEarnedText`, `XPIcon`,
  `Continue`, `Continue Text`, `GameCanvas-SkimRace` (the root)
- Only in CORE (**3**): `NotificationUI`, `PlayerFour`, `GameCanvas` (the root)

**CORE/GameCanvas must absorb the fork's 19 objects first**, so the swap is lossless. Then the
15 scenes can be re-pointed. Nothing in runtime C# resolves the canvas by name (verified — the
only string hit is a tooltip in `CanvasUpgraderWindow`), so renaming the root is safe.

Some of those 19 are known dead and should be dropped rather than migrated — confirm each, but
per the audit: `TeamScorecard` ×3 (`Populate` is never called; they render static authored
content), the `Scoreboard/SinglePlayerView` subtree, the `PlayerOne…PlayerFour` rows, and the
`RematchRequestButton`s. Also delete `Assets/_Prefabs/UI Elements/Panels/R_GameOverPanel.prefab`
(referenced by **0** assets; `GameOverPanel.prefab` is the live one) and resolve the dangling
override that points at `MiniGameHUD.prefab`, an asset never instantiated anywhere.

## Do not write a new tool — extend the one that exists

`Assets/_Scripts/Editor/FrogletTools/` already has everything but the fork retirement:

- **`PrefabInstanceSceneScanner.cs`** (354 lines) — reads overrides straight out of scene YAML,
  read-only, no scenes opened. Already has `ClassifyOverrides` → `(uniform, divergent)` and
  `MeaningfulOverrideCount`.
- **`PrefabDriftFixer.cs`** (258 lines) — every write through `PrefabUtility` on a properly loaded
  scene: `RevertInstance`, `ApplyInstance`, and
  `ConsolidateUniform(prefabGuid, scenePaths, uniformKeys, donorScenePath)` — which is exactly the
  operation this job needs and is already implemented.
- **`GameModePrefabKitWindow.cs`** (468 lines) — the `FrogletTools/Game Modes/Game Mode Prefab Kit`
  window, with per-entry Validate / Add to Scene / Open Prefab and a Validate All pass.

What is missing is the **fork-retirement flow**: absorb the delta, re-point an instance from one
prefab guid to another while preserving a named set of values, and a per-scene "fix this scene"
button with a dry-run report. Add that; reuse the rest.

## Suggested order

1. **Report only.** A read-only pass over all 25 canvas-bearing scenes that reproduces the table
   above from the current tree. Ship this first and show it to me — if your numbers disagree with
   mine, your numbers win, but say so.
2. **Absorb the delta** into `CORE/GameCanvas.prefab` (minus the dead objects), so it is a true
   superset. One commit, no scene touched.
3. **Consolidate the uniform 1,729** into the prefab from a designated donor scene, then revert
   them across all 15. `ConsolidateUniform` already does this.
4. **Re-point** the 15 scenes to the CORE guid, preserving only the reviewed survivors.
5. **Delete the fork** and the dead prefabs.

Do **one scene end-to-end first** — I will play-test it — and only then the other 14.

## Traps that will cost you a pass each

- **`- target:` wraps across two lines in scene YAML**, so a one-line regex reports **zero**
  overrides and a 1,774-override instance reads as clean. I hit this in the session that produced
  these numbers, and CLAUDE.md records it under the ability-lockup trap. Normalise the wrap before
  parsing, and sanity-check your parser against a known count.
- **Never `ApplyPrefabInstance` a scene before classifying.** Applying a scene with 1,774
  overrides pushes that scene's *drift* into the shared prefab — Joust's off-screen toast feed and
  the 5-stat `statsToTrack` would become everyone's. Classify, revert the divergent deliberately,
  then apply.
- **Never hand-edit scene or prefab YAML to "apply" an override.** Reads go through
  `PrefabInstanceSceneScanner`, writes go through `PrefabDriftFixer` / `PrefabUtility`. This is a
  standing rule in CLAUDE.md's tooling section.
- **Fail-loud SOAP references.** Project policy is no null guards on serialized `ScriptableEvent`
  fields — a re-pointed instance that loses one **throws**. `MiniGameHUD` alone subscribes to ~10
  SOAP channels via serialized SO references. Audit §5.5: a mistyped serialized reference on the
  pause prefab broke the Windows build twice.
- **CanvasGroup-alpha visibility is load-bearing.** Several components stay active at alpha 0 so
  their subscriptions survive. Do not convert any of them to `SetActive` toggling.

## The rules in force (CLAUDE.md § "Shared prefabs are single sources of truth")

One canvas asset. A variant, never a copy. Never leave scene overrides. Never hand-wire what the
canvas can find itself. Never bind a UnityEvent to a concrete controller subclass. Run
`FrogletTools ▸ Game Modes ▸ Game Mode Prefab Kit ▸ Validate` before committing any scene.

## Shipping — the part that is easy to get wrong

This tool **writes assets**, so `Docs/TOOLING.md` § "Tool output is a deliverable" applies in
full: `FrogletToolChangeLedger.Record(ToolName, path)` in the same block that writes each scene,
and draw `FrogletToolShipPanel.Draw(Ship, this)`. If the tool merges and its scene output does
not, the canvas is broken on every other machine with nothing in the diff to explain it. Use
`/ship-tools` at the end, and `/ship-deep` for the branch — this is 15 scenes of hand-authored
asset YAML.

Verify with `Tools/CI/validate_project.py`, `Tools/Build/audit_persistent_listener_injection.py
--check`, and a dangling-scene-local-reference sweep over every scene you touch. If no Unity
editor / `unity` CLI is available, say so plainly in the commit and PR rather than claiming a
verification that did not run.
