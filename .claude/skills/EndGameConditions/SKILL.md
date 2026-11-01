---
name: EndGameConditions
description: Use when setting, changing, or asking where the end-game / win-condition COUNTS live for the domain modes — SkimRace crystal count, Joust joust count, Crystal Capture crystal count (how many crystals/jousts end a turn), Maelstrom win target (placement points to win the whole shuffle, "race to N"), and the gate-race lengths (Skein rings, Headlong laps x rings, and the other GateRaceController modes). These are now authored ONLY through the FrogletTools > Game Modes > End Game Conditions editor window (backed by Resources/EndConditionOverrides.asset), never via per-scene inspector fields. Trigger when editing CrystalCollisionTurnMonitor / JoustCollisionTurnMonitor / NetworkCrystalCollisionTurnMonitor / NetworkJoustCollisionTurnMonitor, MaelstromDataSO / MaelstromController, EndConditionOverridesSO, EndConditionOverridesWindow, a GateRaceController subclass's AuthoredGateTarget (SkeinController, HeadlongController), or when someone wants to make a mode end sooner/later.
---

# End Game Conditions — how the modes' win counts are set

The per-mode end-game **count** — how many crystals (SkimRace, Crystal Capture) or jousts
(Joust) end a turn, and how many placement points a domain needs to win a whole **Maelstrom /
Maelstrom** ("race to N") — is set in **one place** and one place only:

> **FrogletTools ▸ Game Modes ▸ End Game Conditions**

That window edits the single config asset **`Assets/Resources/EndConditionOverrides.asset`**
(`EndConditionOverridesSO`). The turn monitors (and `MaelstromController` for Maelstrom) load it
at runtime via `Resources.Load`.

## The rule (do not break this)

- **There are NO per-scene inspector fields for these counts.** The old
  `CrystalCollisions` and `collisionsNeeded` on the crystal and joust turn monitors
  `[SerializeField]`s were removed on purpose. **Do not re-add `[SerializeField]` to them** — they
  are now plain internal fields that just hold the *resolved* value. If you want a count changed,
  change it in the tool, not in a scene.
- **`0` = auto/default, `> 0` = explicit count** (same semantic the old field had, moved into the tool):
  - **SkimRace / Crystal Capture** — `0` → auto-calc from the track waypoints (then 39). The
    auto-calc is `waypoints × laps`, where laps come from the monitor's `lapsPerIntensity`
    list (index 0 = intensity 1) and fall back to its scalar `optionalLaps`. Those two ARE
    legitimate per-scene fields — they are *inputs* to the auto-calc, not the resolved count.
    To change how long a race runs you can either retune laps there or set an explicit count
    here; setting a count here wins outright.
  - **Joust** — `0` → `EndConditionOverridesSO.DefaultJoustCount` (2).
  - **Brood Rush (Nucleus Rush)** — `0` → `EndConditionOverridesSO.DefaultBroodRushWaveTarget` (3):
    claimed fauna waves a domain needs to win (resolved by `BroodRushWaveTurnMonitor.StartMonitor`
    → `GameDataSO.GoalTargetCount`, synced by NetworkVariable).
  - **Rampage** — PER INTENSITY since 2026-10-26: `rampagePrismTargetByIntensity` (2000 / 1700 /
    1300 / 1000; element 0 is intensity 1), and a missing or `0` rung falls back to the scalar
    `rampagePrismTarget` → `DefaultRampagePrismTarget` (2000) - Cleave's pattern. Hostile prisms
    (another domain's mass) a domain must destroy to win (resolved on the server by
    `RampagePrismTurnMonitor.StartMonitor` from `SelectedIntensity` → `GameDataSO.PrismTargetCount`,
    synced by NetworkVariable).
  - **Dog Fight / Grizzly Charge** — both race on `DogFightPointTurnMonitor`, which picks the key by
    `gameData.GameMode`: `dogFightPointTarget` (default 40) for Dog Fight, `grizzlyChargePointTarget`
    (default 90) for Grizzly Charge. They shared Dog Fight's key until 2026-10-26.
  - **Skein** — `0` → `EndConditionOverridesSO.DefaultSkeinRingTarget` (24): rings in the
    Urchin cable course, which is both the finish line and the number of rings laid. See
    "Gate races" below for why this number is not free to change.
  - **Headlong** — `0` → `EndConditionOverridesSO.DefaultHeadlongGateTarget` (40): gate
    THREADINGS, i.e. laps x rings. See "Gate races" below.
  - **Maelstrom** — `0` → `EndConditionOverridesSO.DefaultMaelstromWinTarget` (6). This is the
    "race to N" win target: the first DOMAIN whose cumulative `{2,1,0}` placement points reach it
    wins the shuffle. NOT a per-turn count — it ends the whole tournament.
- The setting **applies wherever the mode runs** — standalone arcade *and* inside a Maelstrom.
- SkimRace and Crystal Capture share the same monitor class, so the SO keys the count by
  `gameData.GameMode` (SkimRace 33 vs Scurry 35). Keep that switch in
  `EndConditionOverridesSO.GetCrystalCount`.

## How it flows at runtime

```
EndConditionOverridesSO (Resources/EndConditionOverrides.asset)   ← edited by the Tools window
        │  GetCrystalCount(mode, autoCalc) / GetJoustCount() / GetMaelstromWinTarget()
        ▼
NetworkCrystalCollisionTurnMonitor.GetCrystalCollisionCount() → resolves CrystalCollisions (SkimRace, Crystal Capture)
NetworkJoustCollisionTurnMonitor.StartMonitor()               → resolves collisionsNeeded (Joust)
MaelstromController.StartMaelstromInternal()           → MaelstromDataSO.ResolveWinTarget(...) (Maelstrom)
        │  (0 in the tool → waypoint auto-calc / default 3 / default 6)
        ▼
  …then the same monitor     → gameData.CrystalTargetCount   (synced to clients)
  …then the same monitor     → gameData.JoustTargetCount
MaelstromDataSO.EffectiveWinTarget → IsShuffleComplete + the lobby/summary race-rule text
        ▼
the mode's ScoringRuleSO.IsObjectiveReached(...) ends the turn on that target;
for Maelstrom, the first DOMAIN to reach EffectiveWinTarget cumulative points ends the shuffle
```

The Maelstrom path differs from the per-turn modes: there is no turn monitor. The win target is
resolved **once per shuffle start** in `MaelstromController.StartMaelstromInternal` (mirroring how
the monitors resolve at `StartMonitor`) and stamped onto `MaelstromDataSO` via `ResolveWinTarget`.
`MaelstromDataSO.EffectiveWinTarget` returns that resolved value, falling back to the asset's
serialized `WinTarget` only when the tool asset is missing (or in pure unit tests). Every peer
resolves from the same committed asset, so `IsShuffleComplete` stays deterministic with no extra
networking.

## To change a count

1. Unity → **FrogletTools ▸ Game Modes ▸ End Game Conditions** (auto-creates the asset on first open).
2. Set **SkimRace — Crystal Count**, **Crystal Capture — Crystal Count**, **Joust — Joust Count**,
   **Maelstrom — Win Target (points)**, **Rampage — Prism Target** (hostile prisms a domain must
   destroy to win), and/or **Brood Rush — Wave Target** (fauna waves a domain
   must claim to win Nucleus Rush; one wave per 30s spawn cycle). Leave `0` for auto/default. The window shows the
   effective value and saves on edit.
3. Commit `Assets/Resources/EndConditionOverrides.asset` (and its `.meta`).

Defaults shipped (re-aimed at an experienced ~90 s match on 2026-10-26 - the measurements, every
before/after value and what is still an estimate are in `Docs/ARCADE_MATCH_LENGTH.md`; read it before
moving a target): SkimRace `0` (auto; scene laps 3/3/1/1), Crystal Capture `14`, Joust `2`,
Maelstrom `6`, Brood Rush `3`, Rampage `2000/1700/1300/1000`, Cleave `950/950/1000/1000`, Dog Fight
`40`, Grizzly Charge `90`, Skein `24`, Headlong `40`. The window draws a row for every Live field on
the SO; the asset itself is the authority for the rest of the modes' shipped values.

**A target is a FUNCTION the comeback reads.** `bonusLevels = deficit x rate`, so a mode's
`ComebackRatePerScoreDeficit` is sized against its target; when a target moves, move the rate so a
quarter-of-target deficit buys the same element levels (each generator records that number and
asserts at least one level). And a mode with an `author_<mode>_assets.py` owns its target: change the
generator constant, the SO `Default*` and initialiser, and the asset's Live and `*Build` values
together, then run the generator - changing the asset alone is reverted by the next run.

## Retuning a target for match LENGTH (learned 2026-10-26, `Docs/ARCADE_MATCH_LENGTH.md`)

- **Measure first, from the flights.** Every editor match is on `origin/bugledger/*` as
  `flight_<Mode>_*.json` (`Tools/Build/learn_from_flights.py` `flights_on_branches()`): `raceSeconds`,
  `finished`, `raceTarget`, the end-of-match `result` table (winner time, what the others had left) and a
  10 Hz `rings` column, which gives time-to-first-gate plus seconds per gate even for an unfinished race.
  `time ~= lead-in + (N - 1) x pace` is how a gate count is sized.
- **In a circuit race the length lever is LAPS.** Headlong, Redline, Grizzly Time, Thresher Time, Slingshot,
  Warpline and Wrasse Run lay `ceil(target / laps)` rings on a fixed-radius loop, so lowering the target thins
  rings and barely moves the time. Move the generator's `LAPS`, `<Mode>CourseSource.DefaultLaps`, the scene
  controller's `laps:` and the SO default together. Switchback, Waystation and Breakwater get longer with more
  gates; Skein does neither (see below).
- **A generator that stands down on a committed scene does NOT patch it.** `committed_scene()` only checks that
  its authored blocks are present, so a moved constant (`laps:`, a monitor's `duration:`) fails `--check` with
  "authored block no longer in the committed scene" until you edit that one scene line by hand. That failure is
  the gate working.
- **SkimRace laps are part of the Skim Race AI's track fingerprint.** Changing `lapsPerIntensity` for an
  intensity with a matching tuned policy puts the game on the general policy until
  `Tools/Build/skimrace_retune.py <I>` runs (needs dotnet). Run `skimrace_track_fingerprint.py --check` before
  and after.
- **Two modes can share a monitor and a key.** Grizzly Charge runs `DogFightPointTurnMonitor`; it now has its
  own key, but check the scene's monitor script before assuming a key is one mode's.
- **Weekly Challenge objectives must stay at or under the target** (`WeeklyChallengeTests`); read
  `Resources/WeeklyChallengeCatalog.asset` before lowering one.

## Gate races (Skein, Headlong, and the other GateRaceController modes)

A gate race has no per-mode turn monitor. `RaceGateTurnMonitor` asks the scene's
`GateRaceController.AuthoredGateTarget()` for the target, and the controller reads its own key on
this SO, so the monitor never knows which key its mode uses. The same number sizes the course, and
once the course exists its real length (`AuthoritativeGateCount`) wins, so a target can never name
a gate that is not there. Each mode's key and what the one number means:

| Mode | Key | Means | Shipped |
|---|---|---|---|
| Skein | `skeinRingTarget` | rings laid on the cable AND rings to thread | 24 |
| Breakwater | `breakwaterStationTarget` + `breakwaterLaps` | crossings = 1 + (stations - 1) x laps: a start gate, then the circuit every lap. TWO authored inputs, one derived target (`GetBreakwaterCrossingTarget`); the window has a row for each and shows the derived crossings under "Effective now" | 15 stations, 2 laps = 29 |
| Headlong | `headlongGateTarget` | threadings = laps x rings; rings per lap = ceil(target / laps), laps = `HeadlongController.laps` in `MinigameHeadlong.unity` (5) | 40 (5 x 8) |

Both are ONE count each, so each has one row in the window ("Skein - Ring Target", "Headlong -
Gate Target (laps x rings)"). Headlong's lap count is a per-scene input to the course shape, the
same way SkimRace's `lapsPerIntensity` is an input to its auto-calc; it is not a second end
condition, and the race length does not change when it does (only rings per lap does, rounded up
to a whole lap).

**Skein's ring count is load-bearing geometry, not just a length.** The rings must close on the
finish collar after a whole number of cable laps, so the count sets the ring SPACING
(`laps x L / (count - 1)`), and `Tools/Build/skein_budget.py` proves the pinned-ring spacing and the
intensity-1 "next ring is on screen" promise at `GATE_COUNT = 24` only. The arena
(`SpawnableSkein`) builds its cable with `SkeinCourseSettings.ForIntensity`'s own `GateCount`
(24), and `BuildAll` rejects a seed whose walk lays a different ring count, so the count decides
which re-roll a cable lands on. `SkeinController.BuildCourse` therefore picks the seed with the
ARENA's count and walks the authored count on that same seed: at 24 that is the one pass it always
was; at any other count the rings stay on rails the arena really laid, and if that seed cannot carry
the authored count the course fails loudly with the fix in the message (set it back to 24, or
re-measure the budget). Change Skein's count only together with a `skein_budget.py` run at the new
count.

## Live vs. Build values (don't ship a test config)

The asset stores **two** sets of counts:

- **Live values** (`hexRaceCrystalCount` / `crystalCaptureCrystalCount` / `joustCount` /
  `maelstromWinTarget`) — what the turn monitors (and `MaelstromController` for Maelstrom) actually
  read at runtime. Lower these to end a mode quickly while testing.
- **Build baseline** (`*Build` fields) — the values a shipping build must use. They are *not* read at
  runtime; they're the restore target. You don't type them in — click **"Set Build Values"** in the
  window to snapshot the current Live values as the baseline (shown read-only above the button). Do
  this once, when the Live values are the real production counts.

The safety net is the toggle **"Auto-restore build values before build"** (`autoRestoreBuildValuesBeforeBuild`,
**on by default**). When on, `EndConditionBuildRestore` (`IPreprocessBuildWithReport`) copies the
Build baseline onto the Live counts and saves the asset at build start — no warning, no block, it
just restores — so test values can't ship. Turn it off to build with the current Live values.

`EndConditionOverridesSO.ApplyBuildValues()` (build → live, used by the build restore),
`CaptureBuildValues()` (live → build, used by "Set Build Values"), and `LiveMatchesBuild` (skip if
already in sync) are the shared helpers. Keep the committed asset's `*Build` fields equal to its Live
fields so a clean checkout is already in sync.

Testing workflow: (one time) set production counts → **Set Build Values** → commit. Then: drop a Live
count → test → build (auto-restore puts the baseline back) — or click nothing and just rely on the
build restore.

## Files

| Role | File |
|---|---|
| Config SO (single source of truth) | `Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs` (Live + Build fields, `autoRestoreBuildValuesBeforeBuild`, `ApplyBuildValues`/`CaptureBuildValues`, `LiveMatchesBuild`) |
| Config asset (committed) | `Assets/Resources/EndConditionOverrides.asset` |
| Editor window (the menu) | `Assets/_Scripts/Editor/EndConditionOverridesWindow.cs` |
| Build-time auto-restore | `Assets/_Scripts/Editor/EndConditionBuildRestore.cs` |
| Crystal modes read it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs` (`GetCrystalCollisionCount`; its local-only base was folded in on 2026-10-25) |
| Joust reads it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkJoustCollisionTurnMonitor.cs` (`StartMonitor`; its local-only base was folded in on 2026-10-25) |
| Maelstrom resolves it here | `Assets/_Scripts/Controller/Arcade/Maelstrom/MaelstromController.cs` (`StartMaelstromInternal` → `ResolveWinTarget`) |
| Maelstrom reads it here | `Assets/_Scripts/Utility/DataContainers/Maelstrom/MaelstromDataSO.cs` (`EffectiveWinTarget`, `IsShuffleComplete`) |
| Gate races resolve it here | `Assets/_Scripts/Controller/Arcade/Racing/RaceGateTurnMonitor.cs` (`StartMonitor` → `GateRaceController.AuthoredGateTarget`) |
| Skein / Headlong keys read here | `Skein/SkeinController.cs`, `Headlong/HeadlongController.cs` (`AuthoredGateTarget`, `BuildCourse`) |
| Network sync (the same two files) | `NetworkCrystalCollisionTurnMonitor.cs` (`CrystalTargetCount`), `NetworkJoustCollisionTurnMonitor.cs` (`JoustTargetCount`) |

## When adding a new count-based mode

Add a Live field **and** its `*Build` counterpart (+ a `case`/getter, + a `TryGetAuthoredTurnTarget`
row) in `EndConditionOverridesSO`, include both in `LiveMatchesBuild` / `ApplyBuildValues` /
`CaptureBuildValues`, and add ALL FOUR window pieces in `EndConditionOverridesWindow`: the help-text
bullet, the Live input row (plus its assignment inside the `Persist` block), the "Effective now"
row, and the Build-baseline line in `DescribeBuildValues`. Skein and Headlong shipped with every SO
row and none of the window rows, which left the asset as the only way to change them - check the
window, not just the SO, then have that mode resolve its count
through the SO — **never** with a new per-scene `[SerializeField]`.

- **Per-turn modes** (crystal/joust): the turn monitor resolves the count at `StartMonitor`.
- **Session-level modes** (Maelstrom): there is no turn monitor — resolve once at the
  session start. Maelstrom's `MaelstromController.StartMaelstromInternal` reads
  `GetMaelstromWinTarget()` and stamps it via `MaelstromDataSO.ResolveWinTarget`; everything reads
  `MaelstromDataSO.EffectiveWinTarget` (which falls back to the serialized `WinTarget` when the tool
  asset is missing / in unit tests). Resolving once per start — instead of calling
  `EndConditionOverridesSO.Instance` from a data-container getter — keeps the pure-logic unit tests
  decoupled from the committed Resources asset.
