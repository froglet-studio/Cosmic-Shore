# Crystal Run, the voyage HUD and the end-of-voyage scoreboard

Milestone 1 (Shombith, 2026-11-16): make Escort the Ark and one crew job voyage great. The crew job is **Crystal
Run** (`job.raid.crystals`). The look is Frogma Rev 113 (`Docs/MENU_REDESIGN_B.md` §209: boards R113Hud, R113Run,
R113Compare, R113Score). Not yet run in the Unity editor: `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 1. The rules (Shombith's answers, 2026-11-16)

- "Steal these many elemental crystals during the voyage; a few cells attack you, a few don't."
- The goal is **20 crystals**, between the whole squad. Any elemental crystal counts (Space, Time, Charge, Mass); an
  omni crystal does not.
- The leg is **4 cells: 1 calm, then 3 hostile** (`hostileCells` [false, true, true, true]). The breach line is
  **40%**: below it the Ark falls.
- Crystals count from the moment the Ark sets sail, through every cell, not only at the stop.
- **Any pilot can bring it**: the job is on every board (`always`), credited to the first seated pilot.
- At the stop the job resolves: met the goal, it succeeds and its pay is owed too; missed, it fails and only what was
  stolen is owed. Owed crystals are **at stake until the Ark docks home**, and lost with the Ark.
- **Finish** sails the Ark home with the squad escorting it; docking banks everything (`VoyageRules.Extract`) and
  ends the voyage. (Prompt 1 said Finish banks at once; Shombith's own answer, "the ark should sail home and players
  should accompany", wins.)
- **Venture further** sails the same work on from the stop to the next place of its kind: the goal **+10** and **one
  more cell** (`ventureGoal`, `ventureCells`), never straight back to where the run set out from.

Content: `Docs/Voyages/voyage_slice.json` (`always`, `countsUnderway`, `cells`, `ventureGoal`, `ventureCells`,
`hostileCells`, `breachHull`) through `Tools/Build/author_voyage_assets.py` (`--check`, `--self-test`). Rules:
`VoyageRaid` (pure, `CosmicShore.Data`): the board's always-offered work, the leg fitted to its cells, the tally by
seat and element, the stop, venturing. The run: `VoyageRun.Raid.cs`. The hostile cells' fauna and assaults are the
Ark-attack work (`VoyageRun.Assault.cs`, `Docs/Voyages/ARK_ESCORT.md` §4.2).

## 2. Toasts

Rev 113 Compare: a few seconds each, top centre, one at a time, a bold line and an optional line under it; an event
(white), a threat (red) or something good (green). They go through the run's one banner path
(`ArkwayRun.ShowBanner(line, seconds, mood, under)` → `ArkwayVoyageHud`), never a second toast system, and never one
per crystal. `VoyageToasts.Show` adds the mood's sound (empty slots on `Voyage.asset`).

| Toast | When | Mood |
|---|---|---|
| STEAL {goal} CRYSTALS | the run sets off | event |
| ENTERING CELL {n}. CALM / HOSTILE. | each new cell | event / threat |
| FINAL CELL: THE DESTINATION | the last cell | event |
| {goal} CRYSTALS. BRING THEM HOME. | the goal is met | good |
| the assault lines (`ArkEscort.asset`) | warned, started / repelled / passed | threat / good / event |
| ARK UNDER ATTACK | the hull starts losing plates (at most every 20 s) | threat |
| BREACH CLOSE | the hull within 10% of the breach line (once, until it climbs out) | threat |
| SAIL THE ARK HOME | Finish | event |
| the escort's own lines | depart, each world (red when hostile), through (green), fallen, overrun (red) | |

The alarm rule is `ArkAlarmToasts.Decide`, from the Ark's signals (`ArkVoyageSignals`).

## 3. The objective box

Top left, 600 wide, a thin left line, no glow (Rev 113 Hud and Run). It reads `VoyageObjective.Source` (the running
`VoyageRun` or `ArkEscortRun`); `VoyageObjectiveText` picks every line, so a test reads each choice.

- **Crystal Run**: CRYSTAL RUN, the goal line, the large `12 / 20` counter and its bar, the split by element with the
  petal flowers, the cells Calm / Hostile (the last named for the destination), the hull with its breach mark, one
  action line.
- **Escort the Ark**: ESCORT THE ARK - DIFFICULTY n, "Keep the Ark's hull above {breach}% to {last world}'s core", the
  worlds as stops (done, now, next, end; Hostile until pacified), the hull with its breach mark, "Ark bound for /
  inside {world}", one action line.
- **Sailing home**: SAILING HOME, what is aboard and at stake.
- The action line, first that applies: an assault under way (kills / needed), one announced (seconds), the hull
  losing plates, the Ark healing, the goal met, the cell hostile or calm.

## 4. The scoreboard

Over the dimmed sea (Rev 113 Score): the kicker and title, the Ark's hull against its breach line, a row per seat
(Crystals, Kills, Off the Ark, Score), the rewards (crystals stolen by element, the job's or escort's pay, at stake
or banked), and the ways out. The voyage scene waits while a board is open.

- Score per seat: crystals × 10 + kills × 5 + creatures felled near the Ark × 15 (`Voyage.asset`, "the scoreboard's
  score").
- Crystal Run at its stop: Finish (B) and Venture further (A).
- Escort through its last world: Finish (B: sail home, then bank) and Venture further (A: difficulty + 1).
- Docked home: one way out, Back to Voyages.
- Lost Ark (either voyage): marked lost, with what went down; Try again (A) and Back to Voyages (B).

No XP, rank bar or pilot trust on the rewards (Shombith, 2026-11-16: not needed).

## 4.1 The fauna attacking the Ark

Every hostile creature inside the Ark's guard radius (the escort's `CountThreats`, both voyages) wears a lit, faceted
red diamond hovering over it, sized to the creature. When any is off screen, a lit red arrow stands at the screen's
edge, in the world just in front of the camera, pointing at the nearest, with one pip per attacker off screen (up to
six). Both are 3D volumes, never a flat ring or a label (`Docs/claude/SHADERS_AND_PERFORMANCE.md`, "In the world, not
on the screen"). `ArkAttackerMarks`; the colour is Rev 113's breach red, traced.

## 5. The escort's pay

An escort that comes through owes `escortCrystalsPerDifficulty` (30) × difficulty crystals of
`escortRewardElement` (Omni), banked when the Ark docks home like everything else.
