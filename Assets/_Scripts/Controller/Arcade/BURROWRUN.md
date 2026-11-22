# Burrow Run (`GameModes.BurrowRun = 71`)

The **Jewel Wasp's time race**. A closed loop of **eight** switch rings is cut through the barren race cell; every
pilot flies **two laps** of it in order, and the first **DOMAIN** whose **LEAD RUNNER** threads the last ring of the
last lap wins (golf: finish time).

**Re-seated 2026-11-22 (Yash):** the mode was built as the Meerkat's race (the Vessel Studio's Meerkat race). The
Meerkat keeps one Time game, Warpline, and Burrow Run moved to a hull that had none. The name, id, scene, circuit,
target and scoring did not change; the card, the preview, the AI numbers and the Meerkat-only parts did (section 5).

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameBurrowRun.unity` (cloned from `MinigameWarpline`) |
| Controller | `BurrowRunController : GateRaceController` |
| Course | `BurrowRunCourseSource`: `SlingshotCourse.ForIntensity` (the 600 u eight-ring circle and its intensity ladder) through the shared `HeadlongCircuit` |
| Metric | `ScoringMetric.SwitchesThreaded` (9), **reused**, golf |
| Target | `EndConditionOverridesSO.burrowRunGateTarget` = **16** (2 laps × 8 rings) - FrogletTools ▸ Game Modes ▸ End Game Conditions |
| Card | `_SO_Assets/Games/ArcadeGameBurrowRun.asset` - Vessels: `SO_Class_JewelWasp` only |
| Generator | `Tools/Build/author_burrowrun_assets.py` (`--check`) |
| Vessel | `R_VesselActions/JEWEL_WASP.md` |

## 1. What the mode is asking today

**The line through eight rings.** The wasp flies free like any hull (55 u/s cruise) and threads the rings in order. A
ring counts the hull's own position, so a wasp nested in a creature would thread a ring by riding through it; the
barren race cell has no creature, so today nobody rides.

**The race spawns no crystal (2026-11-23).** The cell's spawn profile has no creature, but that alone did not keep the
cell barren: the scene was cloned from Warpline's, whose crystal manager spawns players + 1 omni crystals in the
nucleus, and an omni crystal hatches a predator under a Jewel Wasp (`JewelWaspHatchByCrystalEffectSO`,
`JewelWaspExecutor.HatchPredator`). Measured with the mode smoke test at I1 with three AI wasps: no creature for 40
game seconds, 16 at 42 s, 17 to 49 after; every wasp nested in one in 86-88% of samples; one ring or two threaded and
no finisher in 600 game seconds. The scene's crystal manager is now FixedCount with a count of 0 (Redline's setting),
written by the generator, which fails if the scene can spawn a crystal. With it: 0 creatures alive for the whole race
and game over at 230 game seconds. The wasp itself is unchanged, so a person on the wasp has no omni crystal to hatch
from in this race either.

## 2. Open: the wasp's Time ability is not in the race yet

The wasp's Time ability is the burst while it rides a creature (x1.5 to x3 the host's top speed, `JEWEL_WASP.md`
section 2). The race cell is barren, so the ability has nothing to act on and the mode does not yet pass the elemental
loop's step 1 (abilities and game together). The fix is the mode's to make, through its environment: creatures along
the circuit for the wasp to sting and ride from ring to ring. It is not built because of the AI:

- An AI wasp stings a creature whenever the cell has one (`JewelWaspExecutor.StepAIFree`), then flies the host at
  the nearest rival, not at a ring (`StepAIHost`), and leaves after 14 s. With creatures in the cell no AI wasp would
  race.
- A mode AI may only steer the free hull (`WaspSeats`); what the wasp does in a host is the vessel's own code, which a
  mode never changes (the LOCKED vessel rule). An AI that rides for a ring needs a way to tell the wasp's AI where to
  ride, and that is a change on the wasp, for its owner (Garrett).

The generator fails if the cell stops being the barren one while the wasp's AI still flies a host at a rival, so
creatures cannot be added without this being settled. It also fails if the scene's crystal manager can spawn a
crystal (section 1): a hatched predator is a creature in the cell like any other.

## 3. Reused, not built

Everything but the cut is the platform's, exactly as Warpline: `GateRaceController` (rings, ordering, laps, the lead-
runner rule, the AI approach and the Regatta steering), `RaceGateTurnMonitor`, `RaceGateObjectiveProvider` (registered
in `MiniGameHUD`), `GateRaceScoringRuleSO` (one more asset), the barren race cell, the equatorial spawn ring, the shared
race toasts, the comeback.

## 4. AI

The platform's `AIPilot` flies the line (the gate approach, `AICommitFromApproachPoint`) and the mode presses nothing.
The approach numbers are Warpline's (commit 160, lead 180, through 120): no search has flown a Jewel Wasp here. Easy
and Medium race on `GateRaceHandicap` (`AIDifficultyRules.HasOwnHandicap`).

## 5. What the re-seat changed (2026-11-22)

- **Card:** seats `SO_Class_JewelWasp`; the words describe the ring race, not the tunnel.
- **Preview:** flies the Jewel Wasp (class 31); its objective line is the ring race's.
- **AI approach:** the Meerkat's searched genes (commit 42, lead 702, through 74, run1 2026-11-17) and its nose row
  (follow 7.97/s) were that hull's and left with it; the scene went back to Warpline's numbers through the generator.
- **Omnicrystals:** the controller no longer lays the Meerkat's omnicrystals between the rings
  (`MeerkatOmnicrystalField.ForCourse`). They are the Meerkat's ability and belong to no other hull's race.
- **Generator checks:** the Meerkat's wormhole checks went; in their place the wasp's prefab flies under `AIPilot`,
  the cell is barren while the wasp's AI chases rivals in a host, the detection clamp covers the wasp's sting flight,
  the controller names nothing of the Meerkat and the card does not seat it.
- **Not changed:** the card background (the circuit is the same, so `/cardart`'s render still shows this arena), the
  target, the scoring rule, the scene's Netcode hashes, any vessel file.

## 6. Status

Authored by the generator, `--check` green with two negative controls watched (the card re-seated by hand, the
controller naming the Meerkat). **Not compiled and not run in the Editor** on 2026-11-22: `unity_refcompile` does not run
on the machine that made the change and Unity was not opened (`Docs/UNITY_VERIFICATION_CHECKLIST.md`). No offline race
model flies a Jewel Wasp here, so `/compute` cannot teach its AI this mode yet.

**2026-11-23, first runs in Unity** (batch mode, the mode smoke test, `Docs/LAUNCH/SMOKE_TEST.md`; I1, three AI wasps,
difficulty Legendary, time scale 4). The tree compiles (0 `error CS`). Before the no-crystal change the race never
ended (three runs, 600 game seconds each, no finisher). After it, one run: the scene loads, three AI hulls move, the
lead wasp threads a ring every 10 to 18 game seconds, the game-over event fires at 230 game seconds with a winner
calculated, 0 creatures alive throughout, no error in the match. Warpline in the same run finishes at 107 game seconds,
as before. The wasps fly a median 27-33 u/s there, under the 55 u/s cruise named above; why is not looked at. Not
run: I2-I4, a person on the wasp, a second client, a built player. The elemental loop's step 1 is still open (section
2): the wasp's Time ability is not in the race.
