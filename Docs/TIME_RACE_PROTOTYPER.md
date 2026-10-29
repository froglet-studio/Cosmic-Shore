# Time Race Prototyper

**FrogletTools > Game Modes > Time Race Prototyper** (`Assets/_Scripts/Editor/TimeRacePrototyperWindow.cs`). Owner: Yash.

Yash, 2026-10-27: *"Stoat will use Slingshot and Meerkat will use Warpline. These will be two time game modes of these
two vessels. So let's create prototypes with various configuration options that I can click and see and also save
buttons ... Build it inside Unity directly."*

## What it does

It shapes the courses of the three time races cut from Slingshot's circuit:

| Race | Vessel | Shipped |
|---|---|---|
| Slingshot | Stoat (its Time race since 2026-10-27) | 2 laps x 8 rings |
| Warpline | Meerkat (since 2026-10-27; the Stoat's before) | 2 laps x 5 rings |
| Burrow Run | Meerkat | 2 laps x 8 rings |

For the race you pick:
- **Use this course in the game**: off, the race flies its shipped course; on (after Save), it flies yours.
- **The race**: laps and rings a lap (the race is laps x rings threadings long).
- **Intensity I1-I4**, one circuit each: ring mouth radius, circuit radius, the corner turns of the lap, the tightest
  corner allowed, corner swing, uneven legs, climb and dive, ring twist, the most a ring may face away. Every field
  has a tooltip. **Reset I_n to shipped**, **Copy I_n to all**, **Reset race to shipped**.
- **The course**: the game's own generator (`HeadlongCircuit.Generate`) drawn from above on the circuit's plane, with the
  race shell (480 and 1080 u), every ring at its mouth size and the start ring in lime, the shipped course faint under
  it, and a climb-and-dive strip round the lap. **Seed** / **New seed** roll courses on the same settings. Under it: the
  lap length, the shortest and longest legs, the sharpest turn, and the lap and race times at the vessel's cruise and
  its top speed (straight-line distance only, so a floor, not a prediction).
- **Presets**: name (and note) the live values and **Save as preset**; **Load** or **Delete** any saved one. Only the
  live values race.
- **Save** writes `Assets/Resources/TimeRaceCourseConfig.asset`; **Save and Play** saves and enters Play mode (through
  Bootstrap): open Arcade, pick the race and the intensity.
- **Validate & Push** at the bottom commits the saved asset (the tool-output contract, `Docs/TOOLING.md`).

## How the game reads it

`TimeRaceCourseConfigSO` (`Assets/_Scripts/Controller/Arcade/Racing/`), loaded from `Resources/TimeRaceCourseConfig`.
`SlingshotCourseSource`, `WarplineCourseSource` and `BurrowRunCourseSource` cut their circuit through
`TimeRaceCourseConfigSO.Circuit(mode, intensity)` and take their laps and race length from `LapsFor` / `GateTargetFor`.
With no asset, no entry for the race, or the course switched off, each returns the shipped value, so nothing changes until
someone switches a course on. The server builds the course and broadcasts it (`GateRaceController`), so the host's copy
decides a match.

Tests: `TimeRaceCourseConfigTests` (a course made from the shipped race is the shipped race, ring for ring; presets
round-trip; an untouched race flies its shipped course; the Warpline source builds through the config).

## What to know

- **A reshaped course is a different race for the AI learner.** It tells courses apart by ring radius only in
  `PER_INTENSITY_MODES` (Switchback). Slingshot and Warpline are not in it, so races on a prototyped course and on the
  shipped one are learned from together (`Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md` §8).
- **The preview's shell is the fallback** (480..1080 u). The match measures the race cell's nucleus, which is close to it
  in the barren race cell.
- **The end-condition window** still owns the shipped race lengths (`EndConditionOverridesSO`): the prototyper's laps x
  rings replaces them only while its course is on.
- **Not run in the Unity editor yet** (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).
