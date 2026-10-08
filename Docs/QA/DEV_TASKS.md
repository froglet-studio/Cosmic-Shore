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
