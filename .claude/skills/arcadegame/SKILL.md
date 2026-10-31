---
name: arcadegame
description: Use for ANY new Cosmic Shore arcade game mode, or a change to how one is authored - a new GameModes member, a new MinigameXxx scene, a new MultiplayerDomainGamesController subclass, a new ScoringRuleSO / TurnMonitor pair, a new arcade card (SO_ArcadeGame), a per-mode intensity ladder (four CellConfigDataSOs), a Tools/Build/author_<mode>_assets.py generator, or a mode's toasts / preview / end-condition registration / card background (via /cardart). Loads the mode recipe (what is REUSED from the platform, what a mode genuinely OWNS, the eleven registrations every mode makes, the generator library, the gates) so a mode stops re-deriving the checklist and stops taking "the next free id". Trigger when editing Assets/_Scripts/Controller/Arcade/**, Assets/_SO_Assets/Games/**, Assets/_SO_Assets/Cell Configs/<Mode> Cell/**, EndConditionOverridesSO, GameModes.cs, or Tools/Build/author_*_assets.py.
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
| **Arena** | Reference an existing cell where the mode wants what it authors (Bends -> Rampage, Salvo -> Boneyard, Undertow -> Wildlife Liberation). Fork ONLY for a reason the doc can state (a different volume LADDER, a species the arena must grow, a court the forest must sit inside). Four `CellConfigDataSO`s via `CellTypeChoiceOptions.IntensityWise`, list order = intensity. A forked ladder is DERIVED (measured baseline x forest ratio), never typed. **A bigger membrane needs `CytoplasmShardDistance`**: `SnowChanger` fills the whole sphere at one shard per spacing³, so a radius-k×1,200 shell at the prefab's 120 builds k³ × 4,189 shards (Cleave's 3,600 shell built ~113,000 until 2026-10-26). Derive the spacing in the generator as 120 × R / 1,200 (`author_cleave_assets.py`); `python3 Tools/Build/check_cytoplasm_shards.py` fails any config above 3× a standard cell's count. |
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
| Arcade card | `_SO_Assets/Games/ArcadeGame<Mode>.asset` | `Mode`, `DisplayName`, `Description`, card art, `GolfScoring`, `SceneName`, `Vessels`, min/max players + domains + intensity, `ComebackRatePerScoreDeficit`. **Do not emit retired keys** (`CallToActionTargetType`, `PreviewClip`) - a generator is the second place a schema change has to land. `lib.card_errors` rejects both (and a non-render `CardBackground`); `Generator.finish` runs it, a standalone generator calls `lib.check_cards` itself. |
| Cell configs + spawn profiles + species forks | `_SO_Assets/Cell Configs/<Mode> Cell/` | only when forked (§1) |
| Toasts | `_SO_Assets/Game Toasts/GameToastConfig_<Mode>.asset` + `g.register_toast_config` | stat toasts (80-85) are free; the shared race beats (128-132: quarter, halfway, lead change, home stretch, final lap) come from ticking `DomainRaceToasts` in the controller (every `GateRaceController` mode already does; Rampage/Salvo/Hijack added it in #976); mode-specific milestones need the controller to post them |
| Preview | `_SO_Assets/Mode Previews/ModePreview_<Mode>.asset` + `g.register_preview` | `PreviewCellsByIntensity` when the arena differs per intensity; spawn block = the scene's (author_preview_spawns checks it) |
| Scene | `_Scenes/Multiplayer Scenes/Minigame<Mode>.unity` | CLONE the nearest sibling's scene and swap guids / blocks with `lib.swap_guid` / `lib.replace_block`, which assert the donor still matches - on FIRST bring-up. Wrap the clone in `lib.committed_scene(rel, build_clone, authored_blocks)`: once the scene is committed it is adopted as-is (the Editor owns its fileIDs and `GlobalObjectIdHash`es), the clone stands down even when its donor asserts fire, and every block the generator still authors must appear verbatim in the committed scene. Bends, Switchback, Hijack and Salvo use it. |
| Plus the registries | `g.register_arcade_card` (master roster + Arcade grid), `g.register_always_unlocked`, `g.register_build_scene`, `g.set_end_condition` | |
| **Card background** | `_Graphics/ARCADE/CardBackgrounds/<Mode>.png` + the card's `CardBackground` | NOT authored by the mode's generator and never hand-painted: **run the `/cardart` skill** once the card is on a roster. It renders the mode's own intensity-2 arena offline (the shipped generator COMPILED AND RUN, a course generator, or a model read off the controller's settings), imports it and rewires the card. The generator emits `lib.card_background("<Mode>")` for the field. A mode with an `EnvironmentPrefab` needs no code; anything else is one `recipe()` branch - an unknown card makes the renderer RAISE, so a new mode cannot ship on a legacy backdrop by omission. |
| **Genre petal** | `ModeGenre` (`_Scripts/Data/ModeGenre.cs`) | a mode's genre falls back to its METRIC, so most modes need nothing; add a mode row only when the mode is not the genre its metric implies (`Docs/HomeHub/ARCHITECTURE.md` §3.7) |

Validate in the script before `g.finish`: the comeback assert, every donor guid gone from the
clone and every new one present, the AI template count and hull, the intensity list, anything
the mode's own numbers promise (a band inside a court, a ladder ordered and monotone, a pilot
worth more than a creature). Then run it, run it again with `--check` (must pass), and **watch
it fail once**: mutate an authored file, `--check` must name it, re-run to restore. A gate
nobody has watched fail is a gate nobody should trust.

**The card's `CardBackground` is owned by `/cardart`, not by the mode generator** - once the renderer has rewired a card, a generator that re-emits the whole card with `arcade_mode_lib.CARD_ART`'s legacy placeholder silently puts the old backdrop back on every re-run, and `render_card_backgrounds.py --check` then fails on a card nobody touched. Emit `arcade_mode_lib.card_background("<Mode>")`: it returns the guid of the mode's `/cardart` PNG `.meta` (`CardBackgrounds/<Mode>.png`), and falls back to the placeholder only on first bring-up, before `/cardart` has run. The value comes from the RENDER, not from the card, so a card that drifts back to the placeholder fails `--check` instead of being copied forward - the older approach (read `EXISTING['CardBackground']` back off the card; `butterfly_games_common.card_background`, now unused) copied a drifted card forward, and twelve generators that skipped even that emitted the placeholder outright until #969 (2026-10); Waystation clobbered its rendered card the first time its generator ran after `/cardart`. Tollway and Wildlife Liberation are standalone and pin their render guid in `EXISTING['CardBackground']`; prefer the helper in anything new. General rule: *a generator that rewrites a whole asset owns every field in it unless it derives the ones another tool owns from that tool's output.*

**A donor's NEW field leaks into every clone.** When the donor scene's controller gains a field after a mode was
cloned from it, the next generator run copies it into the clone, and `--check` goes red on a mode nobody touched
(2026-10-26: Switchback's `featuresPrefab` reached Waystation; Waystation already stripped `firstGateDistance` the
same way). Strip donor-only fields with a pattern that also matches when the field is ABSENT (so the generator still
passes against an older donor), and add each donor-only asset guid to the "every donor guid gone from the clone"
validation, so the leak fails by name instead of as a drift diff.

**A spent one-shot must STAND DOWN, not abort** - a scene clone that asserts on its donor's
exact text is right today and is the `author_dogfight_assets.py` trap the day the donor moves;
when the scene is committed and the donor drifts, guard the clone and keep the checks below it live.
`lib.committed_scene` is that guard; Wildlife Liberation (standalone) carries its own equivalent.
**State of the family, measured 2026-10-06 on bleeding-edge:** all 25 `author_*_assets.py`
scripts (21 mode generators + the Borromean, Mandelbulb, Manta-kit and Urchin kits) pass
`--check`, after #969 (shared card/scene/drift helpers, 17 generators repaired) and #967 (Wildlife
Liberation's spent clone guarded, Tollway's flora families). `author_urchin_assets.py --check`
validates but does not diff against disk - read its output before calling it a drift gate.

**The PREVIEW definition is a COPY of scene values, so it goes stale on a scene edit, not only at
bring-up.** `ModePreview_<Mode>.asset` mirrors the scene's `ServerPlayerVesselInitializer` spawn
block, and nothing re-runs `author_preview_spawns.py` for you: Cleave moved its spawn-ring floor
576 -> 1050 -> 3150 across two envelope passes and the preview kept saying 576, which would have
opened the card's preview INSIDE the arena the floor exists to keep pilots out of. **Re-run
`author_preview_spawns.py --check` whenever a mode's spawn ring, formation or distance moves** -
it reports every definition that would change, so it costs nothing to run and is invisible if you
do not. It mirrors the per-intensity floor list (`spawnRingRadiusFloorByIntensity` ->
`SpawnRingRadiusFloorByIntensity`) as well as the scalar, and `ModePreviewDefinitionSO.
ResolveSpawnRingRadiusFloor` resolves it by the server's rule, so a mode with a per-intensity
ring (Cleave) previews on each rung's own ring. A scene with no list writes no list, so the
other previews are unchanged; `ModePreviewSpawnFloorTests` holds the rule.

**And `PreviewCellsByIntensity` outlives a deleted cell config as a DANGLING guid.** Shortening a
ladder means pruning that list by hand - Unity keeps an unresolvable reference silently, and the
list is one of the few places a retired intensity can still be pointed at. When a ladder's length
changes, diff the list's entry count against the mode's intensity count.

### 3a. Designing a race COURSE offline (waypoint tracks)

Worked example: Skim Race I4 "Relativity", `Tools/Build/author_skimrace_relativity_track.py`
(`SKIMRACE.md` §5a). It went through three review rounds; each one is a trap below.

- **Validate on the LAID track, not the curve you designed.** The runtime re-splines your waypoints
  (uniform Catmull-Rom, a prism every 12 u), and that measures TIGHTER than the analytic curve: 190 u
  laid against 214 u designed, at a lobe joint. Port the layout rule into the generator (`lay()`)
  and run every clearance/turn-radius assert on its output.
- **Symmetric = repetitive.** An S6-symmetric knot (six identical lobes) was rejected for
  "repetition of curvature". Reward variety as a LADDER (sort the lobes' turn radii and reaches and
  pull them onto target rungs). An std/ptp reward is satisfied by one outlier: four of six lobes sat
  on the same radius while it scored well.
- **"Through the centre" must not mean ONE point.** Six passes within 64 u of the centre read as a
  pile. Offset each pass 130-185 u and tilt it, so the chords cross the cage at different places,
  and assert a minimum centre miss.
- **Measure turning in the PILOT's frame** (ribbon up = floor; yaw = curvature · (up × forward)).
  Every lobe of a petal knot turns the same way, and a world-space reference cannot show it. A
  counter-turn (a pass bowing the other way) fixes it; assert a minimum counter-turn run.
- **A marker block means "a crystal appears here".** On a dense spline, mark only the waypoints at
  crystal anchors (`SpawnableWaypointTrack.markedWaypoints`). Lay the waypoints with a knot AT every
  anchor, so each anchor is a waypoint. Fewer markers also cut the AI's track strikes by half.
- **Lengthening a pass shrinks its neighbouring lobes' room to turn**, which tightens their joints.
  Grow those lobes' reach by the same amount.
- **Random search over free 3D directions almost never yields a collision-free knot.** Deform a
  known-good arrangement (a tensegrity strut set) with a cross-entropy search over offsets, tilts
  and shape parameters instead. Each candidate costs ~0.2 s in numpy.
- **The AI simulator re-implements the layout** (`skimrace_sim_harness/Sim.cs`): any field you add
  to the track (ribbon normals, crystals per lap, markers) must be exported by `run.sh` and honoured
  there, or the AI trains on a track the game does not lay.
- **YAML patch regexes: never `(?s)` with `.*`.** A DOTALL `.*\n` in a list-removal regex eats to the
  end of the document on the SECOND run (the first run has nothing to remove). Use `[^\n]*`, and
  run the generator twice: write, then `--check`.

### 3b. A mode prototyped in a vessel studio

When the mode was first raced in a vessel studio (the Stoat's Slingshot / Warpline, the Squirrel's Skim
Race), the studio's numbers come over through generators: the course and target through this skill's
generator, the HULL's numbers (ability config, chase camera, AI tuning) through the vessel's own generator
(`/vessel` §4.ad). The studio's AI levels are the lobby's `AIDifficulty` (Skim Race levels via
`SkimRaceDifficultySO`; a gate race applies them with `GateRaceHandicap`). Two studio behaviours the mode
must keep: an AI that presses an input releases it on hand-back (`/vessel` §4.ad.3), and a hull nobody is
flying still heads for its next target (the studio's idle takeover, `/vessel-studio` D31; in the game a
human seat with no input simply flies straight, so a mode that can leave a seat unattended needs an AI
backfill, not a drifting hull).

Serpent Time (2026-10-27, `claude/dazzling-ride-dpyvkx`, `SERPENTTIME.md`) taught three more:
- **Mode-laid prism mass is laid on EVERY peer from a seed the course gives.** A Seed Wall assembles from each
  peer's own prisms, so debris laid on the server alone would not exist where the wall is built. Seed each gate
  from its index and broadcast position (`SerpentTimeController.DebrisSeed`). Lay the first cohort with
  `PrismTrailBuilder.LayOne(..., watchForReveal: true)` behind the connecting panel. Anything regrown mid-race takes
  `watchForReveal: false` and `CompletesAsLiveRibbon = true`, or the arena-ready gate can never all-clear. Then
  MEASURE the shell against the other gates on the real circuits: uncapped, a 120 u debris band reached into a
  neighbour's rings on I2 and I3.
- **A field of the mode's OWN controller enters a committed scene through the generator**, not by hand:
  `hull_time_race.author(new_field_lines=...)` appends it to the authored controller block with a one-shot migration
  that stands down once applied. Watch `--check` name the scene before the write.
- **Standing several rings at one gate keeps the course's ring**, hidden (`RaceGateRing.SetVisualHidden`): the
  objective arrow, the race toasts and the AI's commit all read it. The mode answers `ThreadsGate` and
  `ChooseAIGate` (`GateRaceController`), and the cluster's centre lies OUTSIDE every ring of a 3-ring cluster, so an
  AI left on the course's ring never threads.
- `check_generated_assets.py` reports the Input System's default UI actions (guid `ca9f5fa9…`) on every arena scene
  cloned this session: a package ASSET, which its "not judged" rule (package SCRIPTS only) does not cover. The shipped
  Thresher Time and Headlong scenes carry the same 11 references.

Serpentine (Serpent Time reshaped, 2026-10-31, same branch) taught four more:
- **A start line behind gate 0 for a SEEDED course** (`IPlayerSpawnLine` is asked during the spawn chain, before the
  course exists): override `PickCourseSeed` to latch ONE seed for whichever asks first, and in `TryBuildSpawnPoses`
  build the course with `CourseSource.Build` from that seed, the same `ResolveShell` and `AuthoredGateTarget`. Decline
  (return false) when the cell cannot answer yet, rather than guess a shell the real build would not use. Release the
  latch when the course is raised. `SerpentTimeController.TryBuildSpawnPoses`.
- **Show the course with the cell, not a drawn track**: `SnowChanger.SetFlow(IFlow)` points the cytoplasm's shards
  along whatever a mode answers per shard (Serpentine: the nearest leg of the lap, leaning toward the line), and
  null restores the crystal law. A cell with no crystal never built its shards; `SetFlow` builds them.
- **An obstacle a hit can CLEAR is a shape the MODE lays, with a core.** A plant felled through its heart worked (a
  joust leaves a plant's plates standing, so the mode had to damage them itself) but read as "boring flora" and cost a
  plant's spindles and growth; Serpentine replaced it the same day with `SerpentineGate`: closed-form prisms after
  Breakwater's stations (plain shell, danger weave with exits carved for the hull, a plain core), laid on every peer from
  the course. The mode watches the core's prisms (`Holds`: alive and still its owner id) and, when one goes, damages the
  rest (devastating, core outward, a capped number a frame) and regrows it later. Exits carved by TRIMMING each bar at
  the exit's edge, not dropping whole chords, keep the weave closed right up to the hole; a geometry test samples the
  mouth for any hull-sized hole outside the exits.
- **A colon in card copy breaks the WHOLE card, silently, and it happened twice to one mode.** The generator writes
  `Description:` as a plain YAML scalar, so `rises:` at a line end (or `fuel: tap`) starts a new key: Unity keeps the
  fields above it and drops everything after - the card shows its name with white art, no controls, and the mode does
  not load (its `SceneName` is gone). `hull_time_race.plain_scalar_errors` and `yaml_parse_errors` now fail `--check` on
  it; write ' - ' instead of a colon. Any other generator writing free text into YAML has the same exposure.
- **A mode that wants fewer creatures gets its OWN copy of the cell**, generated from the shared one each run
  (`hull_time_race.author(extra_assets=...)`, Serpentine's `_own_cell_assets`): a cell config and spawn profile are
  shared by ~10 modes, so editing them in place re-tunes every one.

Waystation Prototype (the Tiger Moth's Drop Tower race, 2026-10-31, `claude/adoring-babbage-4jjppj`,
`WaystationPrototype/WAYSTATION_PROTOTYPE.md`) taught three:
- **A prototype BESIDE a shipped mode reuses it without editing it**: a new `GameModes` id whose controller
  INHERITS the original's, a course source that CALLS the original generator and post-processes its gates, a scene
  cloned from the original's committed scene through `lib.committed_scene`. Where the original's AI must stand down
  on the prototype's new pieces, raise its serialized threshold in the prototype's scene (Waystation's
  `aiFoldMinDistance` 420 -> 700) and prove every new link is under it in the offline model, rather than adding a
  seam to the original's file.
- **An apostrophe in a single-quoted scalar breaks the asset exactly like the colon above, and `--check` cannot
  see it**: `lib.toast` and a hand-written `Notes: '...'` wrap text in `'...'` without doubling `'`, so "The tower's
  rings" ended the scalar. Double them (`text.replace("'", "''")`) and parse every authored `.asset`
  in the generator; `check_generated_assets.py` (run by `/ship`'s gates) is what caught it (`REFACTOR_BACKLOG` S93).
- **A per-peer mode structure is a pure function of the BROADCAST course**: clients receive gate positions, not the
  seed (`GateRaceController.BroadcastCourse`), so walls, plates or debris derived on every peer must be computed from
  the gates alone. Tag what a peer must tell apart in a field the broadcast already carries (the prototype's tower
  rings use a radius no cluster ring uses) instead of widening the RPC.

## 4. Gates (no Unity needed; run them all)

```
python3 Tools/Build/author_<mode>_assets.py --check
python3 Tools/Build/arcade_mode_lib.py --self-test        # the shared card / scene / drift checks still fire
python3 Tools/Build/check_switch_label_collisions.py
python3 Tools/Build/check_enum_member_references.py
python3 Tools/Build/check_console_logging.py
python3 Tools/Build/check_conditional_compilation.py
python3 Tools/Build/check_self_referential_locals.py
python3 Tools/Build/check_using_directives.py
python3 Tools/Build/check_gamelist_scenes.py
python3 Tools/Build/author_preview_spawns.py --check
python3 Tools/Build/render_card_backgrounds.py --check    # the card's background is its CURRENT arena
python3 Tools/Build/bake_prism_layouts.py --check       # a CellEnvironmentSpawnableBase arena loads from its bake (ECOSYSTEM §62.2b)
python3 Tools/Build/author_card_backgrounds.py --strict   # every live card wears its own
```

**After the generator runs, the open Editor has NOT seen the build-scene registration.**
`ProjectSettings/EditorBuildSettings.asset` is outside the AssetDatabase, so Unity reads it once
at project open and never again. The card then renders normally and fails at the moment a player
commits to it - *"has not been added to the build settings scenes in build list"*, about a list
that on disk contains the scene - and the next project-settings save writes the stale in-memory
list back over the file and deletes the registration. `BuildSceneListReconciler` repairs it on
every domain reload, so pulling the branch or touching a script is enough; **FrogletTools > Game
Modes > Reconcile Build Scene List** is the same check run deliberately, with a report. General rule: *a settings file
outside the AssetDatabase is one the Editor owns for the whole session; an external write to it is
not a change, it is a change that has not happened yet.*

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

- **Change the vessel (LOCKED, Shombith, 2026-10-31).** "The vessel kit as designed always stays the same, we just make
  the game mode interesting." A mode never rations an ability (rounds bought with crystals), vetoes a press, adds a
  contact effect (a wall that costs speed), rescales an ability or edits a vessel file (`R_VesselActions/**`,
  `R_VesselActionHandler`, `Assemblers/**`, a vessel prefab or action SO) for its own sake. Its levers are the track, the
  environment (prisms, debris, flora, hazards), the course ladder and its AI's choices. Serpent Time's round economy and
  wall cost were built and retired for this (`SERPENTTIME.md`). A kit change is a vessel change: it goes to the vessel's
  owner, never through a mode.

- Build a parallel arena/spawner/culler the Cell already owns; add decay, timers or caps to mass.
- Score from a stat nothing counts, or count a stat in a way only one mode can see (`StatsManager`
  counts platform-wide; a mode's rule PAYS).
- Put a rule on one producer that another producer bypasses (the forge-time ball cap).
- Assume an AI can use a human's input (a stick-driven ability is inert under autopilot).
- Hand-edit a generated asset. Re-run the generator.
- Take an enum id from memory. Read the file.
