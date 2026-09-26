---
name: arcadegame
description: Use for ANY new Cosmic Shore arcade game mode, or a change to how one is authored - a new GameModes member, a new MinigameXxx scene, a new MultiplayerDomainGamesController subclass, a new ScoringRuleSO / TurnMonitor pair, a new arcade card (SO_ArcadeGame), a per-mode intensity ladder (four CellConfigDataSOs), a Tools/Build/author_<mode>_assets.py generator, or a mode's toasts / preview / end-condition registration. Loads the mode recipe (what is REUSED from the platform, what a mode genuinely OWNS, the eleven registrations every mode makes, the generator library, the gates) so a mode stops re-deriving the checklist and stops taking "the next free id". Trigger when editing Assets/_Scripts/Controller/Arcade/**, Assets/_SO_Assets/Games/**, Assets/_SO_Assets/Cell Configs/<Mode> Cell/**, EndConditionOverridesSO, GameModes.cs, or Tools/Build/author_*_assets.py.
---

# Arcade Game Mode Protocol

You are adding or changing an **arcade game mode**. Every shipped mode is the same shape, and
the shape is what lets a mode be small: a mode is a **scoring rule** over a **metric the platform
already counts**, a **turn monitor** that reads an **end-condition target**, a **controller** that
arranges the arena and the AI, and an **arcade card**. Everything else - the vessel, the cell, the
crystals, the food web, the HUD, the toasts, the comeback, the scoreboard - is the platform's and
is REFERENCED. A mode that builds its own copy of any of those is the mistake CLAUDE.md names
("the Cell owns the environment - minigames don't build parallel systems").

## 0. Read first

- `CLAUDE.md` § "Game Modes & Controllers" (the roster and what each mode contributed) and
  § "Controller Hierarchy".
- The two nearest siblings' docs under `Assets/_Scripts/Controller/Arcade/*.md`. Pick by the
  SHAPE of the race, not the hull: a **destruction race** (Rampage / Cleave / Salvo /
  Wrecking Ball), a **vessel-vs-vessel duel** (Dog Fight / The Bends / Undertow), a **goal
  race** (Astro League / Scarab Scramble / Tollway), a **gate race** (Switchback / Headlong /
  Breakwater / Skein / Redline - the `GateRaceController` platform), a **timed highest-score**
  (Bloomrush), an **ecology race** (Wildlife Liberation / Brood Rush).
- The vessel's own doc if the mode is hull-locked (`/vessel` skill, `R_VesselActions/<HULL>.md`).
- If the mode touches flora/fauna/cells: the `/ecology` skill FIRST. Its invariants are locked.

## 1. Decide what the mode OWNS - and prove the rest is reused

Write these down before any file. Each is a decision the sibling docs record a trap for.

| Decision | The rule |
|---|---|
| **Metric** | One `ScoringMetric` member, already counted platform-wide. New metric only when the SOURCE of the score is new (gunnery, ecology, ownership each earned one). A mode may re-WEIGHT (`ScoringRuleSO.PointsForCombatHit`) or re-FOLD (`DomainValue` - the Switchback seam; Undertow folds two stats through it) without a new metric. |
| **Target** | One `EndConditionOverridesSO` field + `Build` twin + getter + `TryGetAuthoredTurnTarget` row + `LiveMatchesBuild` / `ApplyBuildValues` / `CaptureBuildValues` rows + the editor window's field, label and build-summary rows. Never a per-scene field (`/EndGameConditions`). |
| **Comeback rate** | `ComebackRatePerScoreDeficit` is a FUNCTION OF THE TARGET (`bonusLevels = deficit x rate`). The generator ASSERTS a quarter-of-target deficit buys >= 1 element level. This trap has bitten seven modes. |
| **Arena** | Reference an existing cell where the mode wants what it authors (Bends -> Rampage, Salvo -> Boneyard, Undertow -> Wildlife Liberation). Fork ONLY for a reason the doc can state (a different volume LADDER, a species the arena must grow, a court the forest must sit inside). Four `CellConfigDataSO`s via `CellTypeChoiceOptions.IntensityWise`, list order = intensity. A forked ladder is DERIVED (measured baseline x forest ratio), never typed. |
| **Vessel lock** | The card's `Vessels` list. Nothing mode-local: three platform layers clamp off it. A card that lists SEVERAL hulls is an ARENA card - use the `/arenagame` skill on top of this one (the fleet facts, the per-hull `StartingElements` table, the `ArenaGames` roster). |
| **Team race** | Always a DOMAIN race. A per-player winner was tried (Wildlife Liberation) and reverted - three playable domains, four seats. |
| **AI** | The narrowest hook that works: nothing (Rampage - crystal seeking IS the loop), `SetDriftLookTargetProvider` (Bends), `SetExternalTargetProvider` (Scramble). An AI's PLACED or FIRED thing must go through the replicated path (`PerformShipControllerActionsReplicated`, `ScarabJukeController.TryAutopilotDash`) - an AI runs server-only, and an all-AI domain that cannot play is a defect (the Tollway rule). |
| **Min players / domains** | A RULE, not a preference: 2/2 for a duel (you cannot hit a teammate), 1/2 for a race against the arena. |

## 2. The C# (four files, namespace `CosmicShore.Gameplay`)

Copy the nearest sibling; do not write from scratch.

1. `Arcade/<Mode>/<Mode>Controller.cs : MultiplayerDomainGamesController` - 1 round / 1 turn,
   `HasEndGame => false`, `UseSceneReloadForReplay => true`, server winner detection in
   `OnTurnEndedCustom`, a `SyncFinalScores_ClientRpc` snapshot carrying the metric AND its
   breakdown (a client that only replicated the total shows every loser's breakdown as 0),
   `SetupNewRound` suppressed after the whistle, AI hooks armed in `OnCountdownTimerEnded` and
   cleared on despawn / end / replay. Field names matter: if the donor scene's controller block
   is carried over, name your fields as the donor does (`settings` / `rule` / `arenaCell`).
2. `Arcade/<Mode>/<Mode>SettingsSO.cs` - every tunable number except the target and the point
   values (config separation). Or Bends-style serialized fields on the controller for a small mode.
3. `Arcade/<Mode>/<Mode>ScoringRuleSO.cs : ScoringRuleSO` (or a sibling rule to inherit
   wording from) - `TargetCount`, `IsObjectiveReached`, `AssignScores`, `BuildResults`,
   `BuildReveal`; `PointsForCombatHit` / `DomainValue` only when the mode re-weights or re-folds.
4. `Arcade/TurnMonitors/<Mode>XxxTurnMonitor.cs : TurnMonitor` (or `CombatPointTurnMonitorBase`
   / `RaceGateTurnMonitor` where the family has a base) - resolves the target server-side,
   replicates it, publishes it to `GameDataSO`, ends the turn through the rule.

Then the platform rows, in this order:
- `GameModes.cs`: **take the highest id + 1 and re-run `check_switch_label_collisions.py`
  after every merge** - 7, 31 and 47 are reserved forever; parallel branches have collided on
  "the next free id" five times. Bump `EnumIntegrityTests.GameModes_HasExpectedMemberCount`.
- `EndConditionOverridesSO` + `EndConditionOverridesWindow` (all rows in §1).
- (nothing for the comeback: `ElementalComebackSystem` reads the mode's `ScoringRuleSO.DomainValue`, so publishing the rule IS the registration. The per-mode `DefaultSourceFor` table and the scene-authored `differenceSource` were retired 2026-09 after eight cloned scenes shipped reading their donor's stat.)
- `MiniGameHUD.CreateObjectiveProviderForGameMode` - reuse a provider when the arrow answers
  the same question (Salvo/Bloomrush -> Rampage's, Undertow -> Bends', Wrecking Ball -> Scramble's).
- `GameToastSituation` - new per-mode situations at the next free block (100+ is the lobby).
- `Tools/Build/author_preview_spawns.py` `SCENE_FOR_MODE`.
  **Every playable card MUST have a `ModePreview_<Mode>.asset` registered in
  `Resources/ModePreviewLibrary`** - a card without one falls back to its static background and
  offers no Test Flight, silently. Eight modes shipped that way because their generators pre-dated
  `register_preview`; `Tools/Build/author_mode_previews.py --check` now holds those eight, and a
  new mode registers its own through the library.

## 3. The generator (`Tools/Build/author_<mode>_assets.py`)

Every serialized asset a mode adds is AUTHORED BY A SCRIPT, never by hand, so re-tuning is one
edit plus a re-run and the whole result is validated before a byte is written. Import
`Tools/Build/arcade_mode_lib.py` - it owns the shape (deterministic guids, the asset preamble,
`.meta` writers, the eleven registrations, the collision sweep, `--check` with a per-file diff
line). `author_wrecking_ball_assets.py` and `author_undertow_assets.py` are the two worked examples
on it; the older generators are standalone copies of the same shape.

What the script authors, and where:

| Asset | Path | Note |
|---|---|---|
| `.cs.meta` for every new script | beside the script | `g.script_meta(path, guid("script/<Name>"))`; folders too (`g.folder_meta`) |
| Scoring rule | `_SO_Assets/Scoring Rules/<Mode>ScoringRule.asset` | `metric:` + `golfRules:` + the rule's own fields |
| Settings | `_SO_Assets/Games/<Mode>Settings.asset` | |
| Arcade card | `_SO_Assets/Games/ArcadeGame<Mode>.asset` | `Mode`, `DisplayName`, `Description`, card art, `GolfScoring`, `SceneName`, `Vessels`, min/max players + domains + intensity, `ComebackRatePerScoreDeficit`. **Do not emit retired keys** (`CallToActionTargetType`, `PreviewClip`) - a generator is the second place a schema change has to land, and the older ones still emit them. |
| Cell configs + spawn profiles + species forks | `_SO_Assets/Cell Configs/<Mode> Cell/` | only when forked (§1) |
| Toasts | `_SO_Assets/Game Toasts/GameToastConfig_<Mode>.asset` + `g.register_toast_config` | stat toasts (80-84) are free; milestones need a controller to post them |
| Preview | `_SO_Assets/Mode Previews/ModePreview_<Mode>.asset` + `g.register_preview` | `PreviewCellsByIntensity` when the arena differs per intensity; spawn block = the scene's (author_preview_spawns checks it) |
| Scene | `_Scenes/Multiplayer Scenes/Minigame<Mode>.unity` | CLONE the nearest sibling's scene and swap guids / blocks with `lib.swap_guid` / `lib.replace_block`, which assert the donor still matches. A donor that moves fails the generator LOUDLY instead of producing a half-wired scene. |
| Plus the registries | `g.register_arcade_card` (master roster + Arcade grid), `g.register_always_unlocked`, `g.register_build_scene`, `g.set_end_condition` | |

Validate in the script before `g.finish`: the comeback assert, every donor guid gone from the
clone and every new one present, the AI template count and hull, the intensity list, anything
the mode's own numbers promise (a band inside a court, a ladder ordered and monotone, a pilot
worth more than a creature). Then run it, run it again with `--check` (must pass), and **watch
it fail once**: mutate an authored file, `--check` must name it, re-run to restore. A gate
nobody has watched fail is a gate nobody should trust.

**A spent one-shot must STAND DOWN, not abort** - a scene clone that asserts on its donor's
exact text is right today and is the `author_dogfight_assets.py` trap the day the donor moves;
when the scene is committed and the donor drifts, guard the clone and keep the checks below it live.

**The PREVIEW definition is a COPY of scene values, so it goes stale on a scene edit, not only at
bring-up.** `ModePreview_<Mode>.asset` mirrors the scene's `ServerPlayerVesselInitializer` spawn
block, and nothing re-runs `author_preview_spawns.py` for you: Cleave moved its spawn-ring floor
576 -> 1050 -> 3150 across two envelope passes and the preview kept saying 576, which would have
opened the card's preview INSIDE the arena the floor exists to keep pilots out of. **Re-run
`author_preview_spawns.py --check` whenever a mode's spawn ring, formation or distance moves** -
it reports every definition that would change, so it costs nothing to run and is invisible if you
do not. Its blind spot is worth stating too: it mirrors the SCALAR floor, so a mode with a
per-intensity ring gets its scalar on every rung.

**And `PreviewCellsByIntensity` outlives a deleted cell config as a DANGLING guid.** Shortening a
ladder means pruning that list by hand - Unity keeps an unresolvable reference silently, and the
list is one of the few places a retired intensity can still be pointed at. When a ladder's length
changes, diff the list's entry count against the mode's intensity count.

## 4. Gates (no Unity needed; run them all)

```
python3 Tools/Build/author_<mode>_assets.py --check
python3 Tools/Build/check_switch_label_collisions.py
python3 Tools/Build/check_enum_member_references.py
python3 Tools/Build/check_console_logging.py
python3 Tools/Build/check_conditional_compilation.py
python3 Tools/Build/check_self_referential_locals.py
python3 Tools/Build/check_using_directives.py
python3 Tools/Build/check_gamelist_scenes.py
python3 Tools/Build/author_preview_spawns.py --check
```

These are syntax-level. What stays editor-only: a member that does not exist, an override whose
signature drifted, an argument mismatch - and everything about how the mode PLAYS. Say so.

**Run `check_using_directives.py` AFTER the last new file is written, not once mid-branch.** It
is scoped to changed files, so it sees a file only once the file exists - and a Roslyn stub
harness does NOT stand in for it: a stub declares every type in one namespace, so a real
`GameDataSO` (`CosmicShore.Utility`) referenced from a `CosmicShore.Gameplay` file with no using
type-checks clean against the stubs and fails in the Editor (CS0246, the first thing the author
saw when opening Wrecking Ball). The gate names the exact using to add; it only has to be run.

## 5. Docs (the mode is not done without them)

- `Assets/_Scripts/Controller/Arcade/<MODE>.md` - overview, the loop in the vessel's own terms,
  what is reused vs forked and WHY, the platform changes it made (with the general rule each
  one records), the intensity ladder, AI, assets, verification status (honestly: "authored
  headless, not yet run in the editor"), known limitations.
- `CLAUDE.md`: the `GameModes` list line, the mode paragraph, the controller hierarchy row, the
  Documentation Index row; and any platform rule the mode changed, where that rule lives.
- `Docs/SCENES.md` (two tables + the turn-monitor table), `GAME_TOASTS.md` (situations + config
  rows), the hull's `R_VesselActions/<HULL>.md` if the mode touched the vessel.

## 6. What a mode must NEVER do

- Build a parallel arena/spawner/culler the Cell already owns; add decay, timers or caps to mass.
- Score from a stat nothing counts, or count a stat in a way only one mode can see (`StatsManager`
  counts platform-wide; a mode's rule PAYS).
- Put a rule on one producer that another producer bypasses (the forge-time ball cap).
- Assume an AI can use a human's input (a stick-driven ability is inert under autopilot).
- Hand-edit a generated asset. Re-run the generator.
- Take an enum id from memory. Read the file.
