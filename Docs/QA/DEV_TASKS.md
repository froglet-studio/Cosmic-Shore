# Dev tasks from QA failures

Every entry here was created by a `FAIL` in a submitted `Docs/QA/RESULTS/` file.
Written by the `/qa-backlog` skill — add detail freely, but do not delete an entry
by hand: it closes when its QA item passes on a later run.

**Definition of done for every task below:** the named QA item passes on a build
that contains the fix. Nothing here is done because the code "looks right" — that
is exactly how these items got onto the QA list in the first place.

Status: 🔵 open · 🟠 in progress (branch named) · 🟢 fixed, awaiting retest.

<!-- qa-dev-tasks -->



## DT-001 — The menu-return/loading veil does not hide the build: prisms are visibly **popping in duri 🔵
- **QA item:** QA-MENU-VEIL-PAUSE
- **Failed on:** bleeding-edge @ 5663cc4b3, 6000.3.17f1, Editor, 2026-10-08 by akouroshm
- **Observed:** The menu-return/loading veil does not hide the build: prisms are visibly **popping in during loading**, and on entering a game the player sees a **"metal seal" opening up** — an animation that appears to be a remnant of an old loading-screen concept, not the current veil. The teardown/build is not being covered. (Observed on 71d67ba9b.)
- **Source of the change:** PRs #672, #693, #698 (the menu-return veil hold + the prewarmed pause menu). Design reference: `Docs/CONNECTING_PANEL.md`.
- **Likely files:** two distinct sub-symptoms —
  1. *Prisms popping in during loading* (the veil lifts, or never covers, while the arena is still building): `Assets/_Scripts/Controller/Environment/Spawning/EnvironmentLoadVeil.cs` and the load-gate tempo/hold it rides (`Docs/CONNECTING_PANEL.md`). Check that the veil holds until the build reports ready rather than lifting on a timer.
  2. *The "metal seal" opening animation on game entry* (suspected vestige of an old loading screen): `Assets/_Prefabs/UI Elements/In Game/ConnectingPanel.prefab`, `Assets/_Scripts/UI/ConnectingPanelController.cs`, `Assets/_Scripts/UI/Elements/ConnectingPanel.cs`. Confirm whether that open-animation is intended or leftover art/behaviour to remove.
- **Done when:** QA-MENU-VEIL-PAUSE passes.

## DT-002 — Auditors ran without throwing, but several report beyond the known exceptions 🔵
- **QA item:** QA-AUDIT-TOOLS
- **Failed on:** bleeding-edge @ 88478a6d, 6000.3.17f1, Editor, 2026-10-09 by akouroshm
- **Observed:** Auditors ran without throwing, but several report beyond the known exceptions. Audit Vessel Skimmers: **6 faults** (known exception is Serpent only). Validate Lifeform Crystals: **34 warnings**. Measure Cell Environment Baselines: **1 warning**. Game Mode Prefab Kit Validate: **75 warnings**. Pending Tool Changes: **~11 uncommitted files**. Ability Rows / Elemental Morphs / Speed Tunnel / Corridor Radii / Cell-Owned Visuals / Occlusion Corridor: ran clean. NOTE: the item's step 10 is STALE — End Game Conditions correctly shows Wildlife Liberation **30** (the mode was re-targeted 250 to 30) and Dog Fight 120; 30 is right, so that line is a doc bug in this item, not a tool fault (fixed in the item body).
- **Triage, per auditor** (each is a separate investigation — this task is a cluster, not one bug):
  - *Audit Vessel Skimmers — 6 faults:* only Serpent is a known-good failure. The other 5 are either newly-added hulls with unauthored skimmers (Butterfly, Scarab, arena newcomers) or real wiring regressions. List the six by name and decide per hull: author the skimmer, or add to the known-exception set. Owner surface: `FrogletTools ▸ Vessels ▸ Audit Vessel Skimmers`, the vessel prefabs under `Assets/_Prefabs/Spacevessels/`.
  - *Validate Lifeform Crystals — 34 warnings:* dump the list; the new flora families (Borromean, Mandelbulb) and recently-added fauna are the likely source. `FrogletTools ▸ Ecology ▸ Validate Lifeform Crystals`.
  - *Game Mode Prefab Kit — 75 warnings:* most likely the GameCanvas / shared-prefab drift this repo tracks (`Docs/GAMECANVAS.md`); confirm whether they are the known fork-scene overrides or new.
  - *Pending Tool Changes — ~11 uncommitted files:* an editor tool wrote assets that were never committed (the "tool output is a deliverable" trap). Identify which tool and land its output (`/ship-tools`).
  - *Measure Cell Environment Baselines — 1 warning:* a single cell's PhaseThresholds drifted from its measured baseline; name the cell.
- **Note:** the known-exception lists in the QA item itself may also be stale (it predates the Butterfly vessel and the arena hulls); refresh them as hulls are triaged so this stops reading as a blanket FAIL.
- **Done when:** QA-AUDIT-TOOLS passes (every auditor clean or only genuinely-known exceptions, with the exception list brought current).

<!-- /qa-dev-tasks -->

---

### Entry format (for reference)

```
## DT-NNN — <one-line symptom> 🔵
- **QA item:** QA-PRISM-OCCLUSION (step 1)
- **Failed on:** bleeding-edge @ 2e2d3aaf, Unity 6000.0.x, Editor/Windows, 2026-08-06 by <tester>
- **Observed:** <verbatim console text / description>
- **Source of the change:** PR #661 (`claude/transparent-prism-occlusion-3fwjky`)
- **Likely files:** `_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl`, `PrismOcclusionCorridor.cs`
- **Done when:** QA-PRISM-OCCLUSION passes.
```
