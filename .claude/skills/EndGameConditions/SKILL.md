---
name: EndGameConditions
description: Use when setting, changing, or asking where the end-game / win-condition COUNTS live for the three domain modes — HexRace crystal count, Joust joust count, Crystal Capture crystal count (how many crystals/jousts end a turn). These are now authored ONLY through the Tools > Cosmic Shore > End Game Conditions editor window (backed by Resources/EndConditionOverrides.asset), never via per-scene inspector fields. Trigger when editing CrystalCollisionTurnMonitor / JoustCollisionTurnMonitor / NetworkCrystalCollisionTurnMonitor / NetworkJoustCollisionTurnMonitor, EndConditionOverridesSO, or when someone wants to make a mode end sooner/later.
---

# End Game Conditions — how the three modes' win counts are set

The per-mode end-game **count** — how many crystals (HexRace, Crystal Capture) or jousts
(Joust) end a turn — is set in **one place** and one place only:

> **Tools ▸ Cosmic Shore ▸ End Game Conditions**

That window edits the single config asset **`Assets/Resources/EndConditionOverrides.asset`**
(`EndConditionOverridesSO`). The turn monitors load it at runtime via `Resources.Load`.

## The rule (do not break this)

- **There are NO per-scene inspector fields for these counts.** The old
  `CrystalCollisionTurnMonitor.CrystalCollisions` and `JoustCollisionTurnMonitor.collisionsNeeded`
  `[SerializeField]`s were removed on purpose. **Do not re-add `[SerializeField]` to them** — they
  are now plain internal fields that just hold the *resolved* value. If you want a count changed,
  change it in the tool, not in a scene.
- **`0` = auto/default, `> 0` = explicit count** (same semantic the old field had, moved into the tool):
  - **HexRace / Crystal Capture** — `0` → auto-calc from the track waypoints (then 39).
  - **Joust** — `0` → `EndConditionOverridesSO.DefaultJoustCount` (3).
- The setting **applies wherever the mode runs** — standalone arcade *and* inside a Tournament.
- HexRace and Crystal Capture share the same monitor class, so the SO keys the count by
  `gameData.GameMode` (HexRace 33 vs MultiplayerCrystalCapture 35). Keep that switch in
  `EndConditionOverridesSO.GetCrystalCount`.

## How it flows at runtime

```
EndConditionOverridesSO (Resources/EndConditionOverrides.asset)   ← edited by the Tools window
        │  GetCrystalCount(mode, autoCalc) / GetJoustCount()
        ▼
CrystalCollisionTurnMonitor.GetCrystalCollisionCount()   → resolves CrystalCollisions (HexRace, Crystal Capture)
JoustCollisionTurnMonitor.StartMonitor()                 → resolves collisionsNeeded (Joust)
        │  (0 in the tool → waypoint auto-calc / default 3)
        ▼
NetworkCrystalCollisionTurnMonitor → gameData.CrystalTargetCount   (synced to clients)
NetworkJoustCollisionTurnMonitor   → gameData.JoustTargetCount
        ▼
the mode's ScoringRuleSO.IsObjectiveReached(...) ends the turn on that target
```

## To change a count

1. Unity → **Tools ▸ Cosmic Shore ▸ End Game Conditions** (auto-creates the asset on first open).
2. Set **HexRace — Crystal Count**, **Crystal Capture — Crystal Count**, and/or **Joust — Joust Count**.
   Leave `0` for auto/default. The window shows the effective value and saves on edit.
3. Commit `Assets/Resources/EndConditionOverrides.asset` (and its `.meta`).

Defaults shipped (match the pre-tool scene values, so behavior is unchanged until edited):
HexRace `0` (auto), Crystal Capture `20`, Joust `3`.

## Live vs. Build values (don't ship a test config)

The asset stores **two** sets of counts:

- **Live values** (`hexRaceCrystalCount` / `crystalCaptureCrystalCount` / `joustCount`) — what the
  turn monitors actually read at runtime. Lower these to end a mode quickly while testing.
- **Build values** (`*Build` fields) — the values a shipping build must use. They are *not* read at
  runtime; they're the documented baseline + restore target so a tester knows exactly what to undo.

The window warns (yellow banner) whenever any Live value drifts from its Build value, listing each
diff, with a **"Restore build values → live"** button (disabled when already in sync). **"Save live
values as build"** (with a confirm dialog) re-baselines the Build values when production counts
genuinely change.

A build-time check (`EndConditionBuildCheck`, `IPreprocessBuildWithReport`) logs a **non-blocking
warning** if a build starts while Live ≠ Build, so a test configuration isn't shipped by accident.
`EndConditionOverridesSO.LiveMatchesBuild` / `DescribeDrift()` back both the window banner and the
build check (one phrasing for both). Keep the committed asset's `*Build` fields equal to its Live
fields so a clean checkout reports no drift.

Testing workflow: drop a Live count → test → **Restore build values → live** → commit with Live ==
Build. If you forget, the build warning reminds you.

## Files

| Role | File |
|---|---|
| Config SO (single source of truth) | `Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs` (Live + Build fields, `LiveMatchesBuild`, `DescribeDrift`) |
| Config asset (committed) | `Assets/Resources/EndConditionOverrides.asset` |
| Editor window (the menu) | `Assets/_Scripts/Editor/EndConditionOverridesWindow.cs` |
| Build-time drift warning | `Assets/_Scripts/Editor/EndConditionBuildCheck.cs` |
| Crystal modes read it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs` (`GetCrystalCollisionCount`) |
| Joust reads it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/JoustCollisionTurnMonitor.cs` (`StartMonitor`) |
| Network sync (unchanged) | `NetworkCrystalCollisionTurnMonitor.cs` (`CrystalTargetCount`), `NetworkJoustCollisionTurnMonitor.cs` (`JoustTargetCount`) |

## When adding a new count-based mode

Add a Live field **and** its `*Build` counterpart + a `case` in `EndConditionOverridesSO`, include
both in `LiveMatchesBuild` / `DescribeDrift`, add the Live + Build rows in the editor window, then
have that mode's turn monitor resolve its count through the SO — **never** with a new per-scene
`[SerializeField]`.
