# The Scarab's steering at speed: the AI nose lead (2026-11-06)

**For:** Garrett (asked), Yash (owns the vessel AIs and Regatta's AI). **Plan step:** `time-arcade` (brief 7 of the Regatta
balancing work). **Status:** built and measured in an offline MODEL only. Nothing here has raced in Unity. The new
steering is OFF everywhere; switching it on for the Scarab is Yash's call after a real-game check.

## The problem

The Scarab is held off Regatta's card (`author_regatta_assets.py` `NOT_REGATTA_READY`) because its AI misses 6-20 ring
mouths a race. At the card's Time 10 (324 u/s) it tracks its line 60-100 u wide (Regatta Hull Benchmark, real game, I1,
2026-11-03, `REGATTA_AI.md`).

**Why.** An AI hull's nose follows the rotation its stick COMMANDS through a lag (`VesselTransformer.LERP_AMOUNT`,
1.5/s, a 0.67 s time constant; human pilots lost it on 2026-10-21, AI hulls kept it). `AIPilot` judges its steering
error from the NOSE. Through the lag it keeps winding the command on while the nose is still catching up, so the command
overshoots and then the nose does. At 324 u/s, 0.67 s is 216 u of travel. On a 60 deg swing the nose overshoots by
about 18 deg and takes about 4.5 s to settle inside 3 deg.

The older models (`regatta_line_sim.py`, `scarab_drive_sim.py`) did not show this, because:
- they run with the lag off by default;
- they turn the command TOWARD the aim and stop it there, which the game's stick does not do: `Pitch`/`Yaw` rotate it by
  the stick's rate every frame.

## The fix: the nose lead (`AIPilot.NoseLeadGain`)

With a gain g above 0, the pilot places the command where the lagging nose has to be pulled from. That is the nose
turned toward the aim by g times its error, past the aim when g is over 1, and at most `NoseLeadMaxExtraDegrees`
(default 40) past it. The pilot then steers the command onto that point, judged from the command. Under a first-order
follow the nose closes g times faster instead of overshooting: on the same 60 deg swing at g = 2.5 it overshoots by
about 1.5 deg and settles in about 1.1 s.

What the fix is and is not:
- It is input only. The vessel's turn rate, lag, speed and Time levels are untouched, so it is not a vessel change.
- The gain is stated at the fleet lag and scaled to the hull's own rate (`EffectiveNoseLeadGain`). A faster nose needs
  less lead, and a nose with no lag gets none.
- It is off while drifting.
- Regatta sets it per hull at arming, from the hull's race line style (`RaceLineStyleSO.NoseLeadGain`, 0 on every
  shipped asset).
- To switch it on, add `"NoseLeadGain": 2.5` to the Scarab's row in `Tools/Build/author_race_lines.py` `HULLS` and run
  it. That rewrites only `Scarab_Regatta.asset` (`--check` guards it).

## The model's numbers

**These are model numbers, not game numbers.** The model is `Tools/Build/scarab_nose_lead_sim.py`:
- regatta_line_sim's race line, stick law, orbit break-off, miss rule and `AimThroughRing`;
- the nose lag on, and the game's command update;
- a constant 324 u/s;
- no traffic and no rail rams;
- every seat raced (3 lanes x 3 risks), with a DNF priced at 300 s.

Mean seconds of nine seats and missed mouths a race. Full grid:
`/mnt/project-files/regatta-training/results/scarab-steering/nose_lead_grid.json`.

| Line | Steering | I1 | I2 | I3 | I4 | I1-I4 |
|---|---|---:|---:|---:|---:|---:|
| bleeding-edge (share 0.788, 0.35 / 0.8 s) | today | 135.4 s, 28.1 miss | 195.7, 59.3 | 246.3, 83.7 (8/9 finish) | 299.1, 118.7 (2/9) | 876.5 |
| bleeding-edge | lead 2.5 | 44.5, 0.0 | 56.7, 1.7 | 59.6, 1.8 | 59.8, 1.4 | **220.6** |
| overnight (share cap 0.35, 0.6 / 1.0 s) | today | 50.1, 2.1 | 108.3, 24.2 | 188.6, 61.8 | 215.5, 78.2 | 562.5 |
| overnight | lead 2.5 | 43.3, 0.0 | 53.9, 0.4 | 57.5, 0.0 | 57.5, 0.4 | **212.2** |

What else was tried:
- **A longer look-ahead alone** (x1.5) halves today's losses (bleeding-edge 876 -> 595 s, overnight 562 -> 390 s). It
  adds nothing to the lead (216-231 s).
- **Gains 2-3** all land within 5% of each other; 1.5 is too little (386 s on bleeding-edge). A 20 deg cap on the extra
  is worse (276 s); 80 deg is the same as 40.
- **Judging from where the nose will be** in s seconds ("pred") is not shipped. At s = 0.3 it nearly matches the lead
  on the overnight line (210 s, no miss), but it still misses 16 mouths a race at I3 on bleeding-edge's line. At s = 1
  (judging from the command) it is little better than today.

**Model against the real game.** At I1, today's model on the overnight line (2.1 misses, 50 s) is kinder than the
benchmark's real races (6-20 misses, 67-122 s). The model likely flatters today, so the lead's real-game gain may be
larger or smaller than shown: only an editor race settles it.

## What is not verified

- **No Unity run.** The reference compile and the ship gates passed (0 errors in the changed files; the 21 errors it
  reports are in files this change does not touch). `NoseLeadTests` has not been run (no editor in the cloud).
- **Real-game checks for later (a PC session):**
  1. Regatta Hull Benchmark for the Scarab at I1-I4 (Legendary, alone), with `NoseLeadGain` 0 and then 2.5 on
     `Scarab_Regatta`.
  2. Then a six-AI grid.
  3. The card change (`NOT_REGATTA_READY`) waits for those.
- **Other hulls.** The lead is general. The Manta (Redline nose-lag brief) and the Rhino fly the same lag and may gain
  too. Not modelled here.

## Files

- `Assets/_Scripts/Controller/AI/AIPilot.cs`: `NoseLeadGain`, `NoseLeadMaxExtraDegrees`, `NoseLeadDirection`,
  `EffectiveNoseLeadGain`, and the steering judged through them (off at 0).
- `Assets/_Scripts/Controller/AI/Racing/RaceLineStyleSO.cs`: the two per-hull fields (0 / 40).
- `Assets/_Scripts/Controller/Arcade/Regatta/RegattaController.cs`: `OnRacerArmed` sets them per hull.
- `Assets/_Scripts/Tests/Editor/NoseLeadTests.cs`: the geometry, the gain scaling, the closed-loop swing (with today as
  the negative control) and "no shipped style turns it on".
- `Tools/Build/author_race_lines.py`: the optional fields, written only when a hull sets them.
- `Tools/Build/scarab_nose_lead_sim.py`: the model (`--self-test`, with negative controls).
