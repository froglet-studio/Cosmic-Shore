---
name: EndGameConditions
description: Use when setting, changing, or asking where the end-game / win-condition COUNTS live for the domain modes — HexRace crystal count, Joust joust count, Crystal Capture crystal count (how many crystals/jousts end a turn), and Maelstrom/Tournament win target (placement points to win the whole shuffle, "race to N"). These are now authored ONLY through the FrogletTools > Game Modes > End Game Conditions editor window (backed by Resources/EndConditionOverrides.asset), never via per-scene inspector fields. Trigger when editing CrystalCollisionTurnMonitor / JoustCollisionTurnMonitor / NetworkCrystalCollisionTurnMonitor / NetworkJoustCollisionTurnMonitor, TournamentDataSO / TournamentController, EndConditionOverridesSO, or when someone wants to make a mode end sooner/later.
---

# End Game Conditions — how the modes' win counts are set

The per-mode end-game **count** — how many crystals (HexRace, Crystal Capture) or jousts
(Joust) end a turn, and how many placement points a domain needs to win a whole **Maelstrom /
Tournament** ("race to N") — is set in **one place** and one place only:

> **FrogletTools ▸ Game Modes ▸ End Game Conditions**

That window edits the single config asset **`Assets/Resources/EndConditionOverrides.asset`**
(`EndConditionOverridesSO`). The turn monitors (and `TournamentController` for Maelstrom) load it
at runtime via `Resources.Load`.

## The rule (do not break this)

- **There are NO per-scene inspector fields for these counts.** The old
  `CrystalCollisionTurnMonitor.CrystalCollisions` and `JoustCollisionTurnMonitor.collisionsNeeded`
  `[SerializeField]`s were removed on purpose. **Do not re-add `[SerializeField]` to them** — they
  are now plain internal fields that just hold the *resolved* value. If you want a count changed,
  change it in the tool, not in a scene.
- **`0` = auto/default, `> 0` = explicit count** (same semantic the old field had, moved into the tool):
  - **HexRace / Crystal Capture** — `0` → auto-calc from the track waypoints (then 39). The
    auto-calc is `waypoints × laps`, where laps come from the monitor's `lapsPerIntensity`
    list (index 0 = intensity 1) and fall back to its scalar `optionalLaps`. Those two ARE
    legitimate per-scene fields — they are *inputs* to the auto-calc, not the resolved count.
    To change how long a race runs you can either retune laps there or set an explicit count
    here; setting a count here wins outright.
  - **Joust** — `0` → `EndConditionOverridesSO.DefaultJoustCount` (3).
  - **Brood Rush (Nucleus Rush)** — `0` → `EndConditionOverridesSO.DefaultNucleusRushWaveTarget` (3):
    claimed fauna waves a domain needs to win (resolved by `NucleusRushWaveTurnMonitor.StartMonitor`
    → `GameDataSO.GoalTargetCount`, synced by NetworkVariable).
  - **Rampage** — `0` → `EndConditionOverridesSO.DefaultRampagePrismTarget` (2000): hostile prisms
    (another domain's mass) a domain must destroy to win (resolved by
    `RampagePrismTurnMonitor.StartMonitor` → `GameDataSO.PrismTargetCount`, synced by NetworkVariable).
  - **Maelstrom** — `0` → `EndConditionOverridesSO.DefaultMaelstromWinTarget` (6). This is the
    "race to N" win target: the first DOMAIN whose cumulative `{2,1,0}` placement points reach it
    wins the shuffle. NOT a per-turn count — it ends the whole tournament.
- The setting **applies wherever the mode runs** — standalone arcade *and* inside a Tournament.
- HexRace and Crystal Capture share the same monitor class, so the SO keys the count by
  `gameData.GameMode` (HexRace 33 vs MultiplayerCrystalCapture 35). Keep that switch in
  `EndConditionOverridesSO.GetCrystalCount`.

## How it flows at runtime

```
EndConditionOverridesSO (Resources/EndConditionOverrides.asset)   ← edited by the Tools window
        │  GetCrystalCount(mode, autoCalc) / GetJoustCount() / GetMaelstromWinTarget()
        ▼
CrystalCollisionTurnMonitor.GetCrystalCollisionCount()   → resolves CrystalCollisions (HexRace, Crystal Capture)
JoustCollisionTurnMonitor.StartMonitor()                 → resolves collisionsNeeded (Joust)
TournamentController.StartTournamentInternal()           → TournamentDataSO.ResolveWinTarget(...) (Maelstrom)
        │  (0 in the tool → waypoint auto-calc / default 3 / default 6)
        ▼
NetworkCrystalCollisionTurnMonitor → gameData.CrystalTargetCount   (synced to clients)
NetworkJoustCollisionTurnMonitor   → gameData.JoustTargetCount
TournamentDataSO.EffectiveWinTarget → IsShuffleComplete + the lobby/summary race-rule text
        ▼
the mode's ScoringRuleSO.IsObjectiveReached(...) ends the turn on that target;
for Maelstrom, the first DOMAIN to reach EffectiveWinTarget cumulative points ends the shuffle
```

The Maelstrom path differs from the per-turn modes: there is no turn monitor. The win target is
resolved **once per shuffle start** in `TournamentController.StartTournamentInternal` (mirroring how
the monitors resolve at `StartMonitor`) and stamped onto `TournamentDataSO` via `ResolveWinTarget`.
`TournamentDataSO.EffectiveWinTarget` returns that resolved value, falling back to the asset's
serialized `WinTarget` only when the tool asset is missing (or in pure unit tests). Every peer
resolves from the same committed asset, so `IsShuffleComplete` stays deterministic with no extra
networking.

## To change a count

1. Unity → **FrogletTools ▸ Game Modes ▸ End Game Conditions** (auto-creates the asset on first open).
2. Set **HexRace — Crystal Count**, **Crystal Capture — Crystal Count**, **Joust — Joust Count**,
   **Maelstrom — Win Target (points)**, **Rampage — Prism Target** (hostile prisms a domain must
   destroy to win), and/or **Brood Rush — Wave Target** (fauna waves a domain
   must claim to win Nucleus Rush; one wave per 30s spawn cycle). Leave `0` for auto/default. The window shows the
   effective value and saves on edit.
3. Commit `Assets/Resources/EndConditionOverrides.asset` (and its `.meta`).

Defaults shipped (match the pre-tool scene/asset values, so behavior is unchanged until edited):
HexRace `0` (auto), Crystal Capture `20`, Joust `3`, Maelstrom `6`, Brood Rush `3`, Rampage `2000`.

## Live vs. Build values (don't ship a test config)

The asset stores **two** sets of counts:

- **Live values** (`hexRaceCrystalCount` / `crystalCaptureCrystalCount` / `joustCount` /
  `maelstromWinTarget`) — what the turn monitors (and `TournamentController` for Maelstrom) actually
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
| Crystal modes read it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs` (`GetCrystalCollisionCount`) |
| Joust reads it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/JoustCollisionTurnMonitor.cs` (`StartMonitor`) |
| Maelstrom resolves it here | `Assets/_Scripts/Controller/Arcade/Tournament/TournamentController.cs` (`StartTournamentInternal` → `ResolveWinTarget`) |
| Maelstrom reads it here | `Assets/_Scripts/Utility/DataContainers/Tournament/TournamentDataSO.cs` (`EffectiveWinTarget`, `IsShuffleComplete`) |
| Network sync (unchanged) | `NetworkCrystalCollisionTurnMonitor.cs` (`CrystalTargetCount`), `NetworkJoustCollisionTurnMonitor.cs` (`JoustTargetCount`) |

## When adding a new count-based mode

Add a Live field **and** its `*Build` counterpart (+ a `case`/getter) in `EndConditionOverridesSO`,
include both in `LiveMatchesBuild` / `ApplyBuildValues` / `CaptureBuildValues`, add the Live input
row + the Build-baseline display line in the editor window, then have that mode resolve its count
through the SO — **never** with a new per-scene `[SerializeField]`.

- **Per-turn modes** (crystal/joust): the turn monitor resolves the count at `StartMonitor`.
- **Session-level modes** (Maelstrom/Tournament): there is no turn monitor — resolve once at the
  session start. Maelstrom's `TournamentController.StartTournamentInternal` reads
  `GetMaelstromWinTarget()` and stamps it via `TournamentDataSO.ResolveWinTarget`; everything reads
  `TournamentDataSO.EffectiveWinTarget` (which falls back to the serialized `WinTarget` when the tool
  asset is missing / in unit tests). Resolving once per start — instead of calling
  `EndConditionOverridesSO.Instance` from a data-container getter — keeps the pure-logic unit tests
  decoupled from the committed Resources asset.
