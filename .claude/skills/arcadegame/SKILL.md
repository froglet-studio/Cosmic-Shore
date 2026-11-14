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
- **A gate race (any `GateRaceController` subclass) adds its line to `RaceCourseSource.For` and
  `GateRaceModes`** (and `RaceCourseSourceTests.Races`). The card's preview has no scene to read,
  so this switch is the ONLY way it learns the course: fifteen vessel Time games shipped without
  it, and every one of their cards previewed a bare shell that never changed with intensity
  (fixed 2026-11-10, `claude/great-meitner-72uziq`; `EveryGateRaceControllerIsRegistered` now fails).
- **A preview definition's cells are the SCENE's cells.** `PreviewCellsByIntensity: []` is not
  "authored": a generator that writes `[]` plus a donor's cell (hull_time_race.py wrote the Skim Race
  cell for every hull Time race) shows another mode's world. `author_preview_intensities.py --check`
  fails on that drift; run it after any scene cell change.

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
- `check_generated_assets.py` reported the Input System's default UI actions (guid `ca9f5fa9…`) on every arena scene
  cloned from another (11 references: m_ActionsAsset and its ten actions), a package ASSET its "not judged" rule
  missed. Fixed 2026-11-10 (Kelpwood): `PACKAGE_ASSET_GUIDS` names it, excused only while the base references it.

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

Waystation Prototype (the Tiger Moth's Drop Tower race, 2026-10-31, `claude/adoring-babbage-4jjppj`; folded into
Waystation on 2026-11-01, `WAYSTATION.md` "The Drop Tower") taught three, plus how a prototype is folded back in:
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

Serpent Gulag (the Serpent's sniper duel, 2026-10-31, `claude/funny-fermat-9m75dr`, `SERPENTGULAG.md`) taught four:
- **A mode's own environment generator is cheap to PROVE before Unity**: write a Python twin of its counts and
  volumes in the generator (PhaseThresholds need them anyway), then run the shipped C# through
  `audit_prism_clipping.py <Spawnable> -v --json` and compare. Serpent Gulag's twin matched the C# prism for prism and
  volume to the unit on all three rungs, and the audit's 0-clipping verdict is the "no prism interpenetrates" law
  checked. Build a hollow shape flush by NESTING its faces (front/back full size, sides between them, caps between
  the sides) rather than overlapping corners. Read only serialized fields, never the intensity, or the bake refuses.
- **The .NET SDK is one `apt-get install -y dotnet-sdk-8.0` away, at `/usr/lib/dotnet`**, and the bake, clip-audit and
  card-art harnesses default to `~/.dotnet`: export `DOTNET_ROOT=/usr/lib/dotnet` or they fail with "no csc.dll".
  `unity_refcompile` then compiles the whole project against real Unity references in ~10 min cold.
- **Check the weapon's real rules before designing cover.** The Sniper Shot pierces everything and breaks
  super-shields (`pierceCount` 0), so a nest is concealment, not armour; a cover design that assumed the round stops
  at the first prism would have been wrong. Read the ability's executor, not its doc.
- **An AI's SKILL is the seat's AI difficulty, never the intensity** (`AIDifficulty.cs`: intensity is the map, difficulty the
  opponent; read each seat's own level with `GameDataSO.AIDifficultyFor(player)`). Serpent Gulag first keyed its ladders on
  the intensity, borrowed from a sibling that does; the generator now fails if the marksman reads `SelectedIntensity`. A
  STYLE can differ by level too: Legendary plays aggressively (shorter range, briefer holds) while Easy-Hard are patient.
- **A ranged AI that steers AT its rival reads as rushing**, however well it shoots. Give it a POST (a range from the rival,
  behind cover) and the vessel's own defensive verb at the post (the Serpent's stop, which grows its wall), and make it
  retreat, not fight, when a rival closes in.
- **The bake and card harnesses read primitive fields only**: a `Vector2` serialized field on a spawnable makes
  `bake_prism_layouts.py` refuse ("does not read rubbleRadii"). Two floats cost nothing; extending the shared reader is a
  tooling change (Shombith's).
- **A new mode beside a collaborator's unmerged one of the same idea gets its own id and touches nothing of theirs**
  (Garrett's Serpent Charge, 67, in flight since 2026-10-16): borrow the SHAPE of their design (the AI ladder) and
  credit it, never their class names, which would collide when both land.

- **Folding a prototype back in** (2026-11-01, Waystation Prototype -> Waystation): move the pure file (`git mv`
  keeps its guid), merge the subclass into the original controller rather than keeping two, have the ORIGINAL's
  generator author the prototype's words and fields, and make that generator RETIRE the prototype every run (its
  files on `Generator.stale`, its guids stripped from every registry, its id out of `ProgressionConfig`) so nothing
  can re-add them once the prototype's own generator is deleted. Retire the enum id and keep it reserved; rename a
  target whose MEANING changed (`waystationRingTarget` 15 rings -> `waystationClusterTarget` 5 clusters) instead of
  reusing the old name.

Lantern Run (the Angler's Time race, 2026-11-01, `claude/intelligent-lovelace-ynskrg`, `LANTERNRUN.md`) taught four:
- **Hang anything a mode places relative to a ring on the ring's AXIS, not on the chord from the previous ring.**
  `HeadlongCircuit` presents a ring up to ~76 degrees off that chord (`MaxPresentDegrees`), so a "near cluster before
  the ring, far cluster past it" pair laid on the chord drew a line that crossed the ring's plane up to FOUR mouths off
  centre at I4 (400 seeds); on `RaceGate.Axis` it threads the mouth by construction. Assert it per leg with a plane
  crossing (`LanternRunCourse.CrossesPlaneAt`), not by eye.
- **A pure course test runs in about a second outside Unity**: compile `card_art_harness/UnityShim.cs`,
  `HeadlongCircuit.cs`, `RaceCourseGeometry.cs`, the course and its `*CourseTests.cs` with csc against a ten-line NUnit
  stand-in (`[Test]`, the `Assert` members the test uses, a reflection `Main`). Negative-control it by compiling a
  mutated copy of the course (one constant changed) in place of the real one.
- **Renaming a hull_time_race mode before it lands is a regenerate, not a sweep**: every guid is derived from the
  member name, so `git rm` the generated assets, reverse the first commit's registry hunks
  (`git diff <c>~1 <c> -- <registries> | git apply -R`), `git mv` the sources, rename the tokens, and re-run the
  generator and `/cardart`. Hand-renaming the assets would keep the old name's guids under the new name.
- **No `dotnet-sdk-8.0` apt package in some containers**: the tarball from `builds.dotnet.microsoft.com` (e.g.
  `dotnet/Sdk/8.0.404/dotnet-sdk-8.0.404-linux-x64.tar.gz`) unpacked into `~/.dotnet` is what `unity_refcompile`, the
  card harness and the course runner look for by default.

Brittle Star Time's movement ladder (2026-11-06, `claude/brittle-stars-l5-updates-41j91p`, `BRITTLESTARTIME.md`) taught four:
- **A ring counts ANY pass through its plane inside the mouth, in either direction** (`RaceGateRing.PlaneCrossing`), so
  a course's difficulty is the line a pilot can fly, not the line through the centres. Measure every claim on three
  lines (through the centres, through the half-mouth line, brushing every rim) and assert the RIM line still asks the
  question: a weave whose amplitude is under the mouth radius is a straight line.
- **Sparse rings alias a periodic shape.** Eight rings round a whole-lap five-turn coil sample it at 225 degrees, and the
  smoothest line through them is a lazy three-turn spiral the other way: the corkscrew vanished. Sample a coil or a
  weave at most a quarter turn per ring (or Nyquist, alternating sides, for a weave), and hold it with a test plus a
  negative control that coarsens the step.
- **A rung can start the hull at an element level** without touching the vessel: `SO_ArcadeGame.StartingElements` rows
  are per intensity (`Intensity: 4`), and `hull_time_race.author(card_extra=...)` writes them where the card declares
  them. Brittle Star Time's I4 starts at Time 5 so the Speed Roll upgrade is live, and its test reads the row back.
- **A pure course and its shipped tests run offline in seconds** with a copy of `redline_course_harness`
  (`Tools/Build/brittle_star_time_course_harness/run.sh`, with `--sweep`, `--gates I S` for pictures, and `--self-test`
  negative controls). The card renderer drawn at each intensity (set `render_card_backgrounds.INTENSITY` from a scratch
  script) plus a plan view from `--gates` is how to show a person every rung without Unity. And if
  `check_generated_assets.py` dies on a missing file under a plugin bleeding-edge removed, the reference compiler's
  output folder (`/tmp/unity_refcompile_out/<repo>-*`) still holds the removed assembly's `.rsp`: delete that folder
  and re-run (a stale local cache, not a defect in the tree).

Hood (Serpent Gulag rebuilt in place, 2026-11-09, `claude/festive-ptolemy-mtm5mx`, `SERPENTHOOD.md`) taught five:
- **Renaming a LANDED mode in place keeps every guid's old SEED.** Lantern Run's "rename is a regenerate" is for a mode
  that has not landed; once rosters, previews, the build list and a scene point at it, `git mv` every file with its
  `.meta` and have the generator mint `guid(f"script/{OldName}")` under the new name, so no reference moves. The
  build-list path and the `EndConditionOverrides` key move as one-shot migrations in the generator that stand down once
  applied, plus `[FormerlySerializedAs]` on the C# field. A generated doc written by another system's sweep
  (`LEARNED_DRIVES_<Mode>.md`) is that sweep's output: leave its name, and say the analytics history keeps the old one.
- **A file you only READ can be deleted by someone else's unmerged branch with no conflict.** Hood builds from
  `SerpentineGate.cs` and never edits it; `claude/dazzling-ride-dpyvkx` deletes it. Before the PR, run
  `git merge-tree --write-tree --name-only HEAD origin/<in-flight branch>` for every live branch that touches the files
  you depend on (not only the ones you change). Moving the file into the mode that owns it (same guid) turned the
  silent delete into a rename/delete conflict.
- **A mode structure with a CORE RULE cannot be a cell environment.** A baked `CellEnvironmentSpawnableBase` lays
  anonymous prisms; a rule that watches which prisms are whose needs `PrismTrailBuilder.LayOne` with an owner id from
  the controller, on every peer, closed form (Serpentine's pattern). The cell configs then carry no environment, and
  their PhaseThresholds ride the census of what the controller lays.
- **Draw the ideas from the geometry code you ship, then make the mirror a parity gate.** The drawings came from a
  Python port; parity against the compiled C# (`serpent_hood_budget.py --parity`, every prism's position and axes)
  found a loop variable in the port shadowing the bar thickness, and the layout tests found the drawn I3/I4 shutters
  reaching into the crystal heart. Both were wrong in the drawings the person approved: say what moved and why.
- **Run `gates.sh` AFTER the first commit.** `check_generated_assets.py` audits `origin/bleeding-edge...HEAD`, so before
  anything is committed it audits nothing and passes; Hood's first ALL OK hid an unparseable toast config (an apostrophe,
  now escaped by `lib.toast`) and a renamed scene the audit read as new (it follows renames since). The compile half of
  the gates reads the working tree, so only the asset half was vacuous.
- **A warp of a shape bends its prisms' AXES too**: carry each part's forward and up through the warp's derivative
  (a central difference) and rebuild the rotation; the frame `Cross(axis, up)` is a reflection, so map the basis vectors
  and `LookRotation` them rather than composing quaternions.

Jackstraw (the Thresher's Charge game, 2026-11-09, `claude/ecstatic-heisenberg-21lpmu`, `JACKSTRAW.md`) taught four:
- **Draw the ideas from the generator you will ship, and the stills come free.** The three pitches were drawn from a
  Python mirror of each planned Spawnable; the approved one became `jackstraw_budget.py`, and the "approved vs built"
  sheet is the same projector run over the mirror, beside the card renderer's still per rung. Nothing was redrawn.
- **An EXACT mirror needs every draw to be `Hash01(integer key)`, never the shared `System.Random`.** Then a rejection
  loop (a stick that enters a clearing or touches another re-draws, up to 8 tries) is mirrored too, and the clip audit
  confirmed the C# laid the same 408 / 1,122 / 2,114 / 3,566 prisms and volumes. Float32 against float64 can still flip
  a draw that lands on a boundary: assert in the generator that every accepted piece clears its boundaries by a margin
  (`MIN_MARGIN`), so the census cannot silently describe a different heap.
- **Random sticks interpenetrate; give them a gap test and a knot.** A segment-segment distance check (Ericson 5.1.9,
  line for line in both languages) keeps rods apart, and a jack's three arms start at a shielded knot cube's faces, so
  the clip audit reads 0 without any fitting pass.
- **A charge mode can score a hull's existing hit with no vessel code at all.** Grep the executor for its `TryAdmit`
  class and the `onCombatHitLanded` channel its config wires (the generator asserts both), then pay that class in the
  rule. The AI can be steering only when the hull hits by contact; the hull's own boost policy keeps its presses.
Flatline (the Brittle Star's charge game, 2026-11-09, `claude/great-rubin-rur3xs`, `FLATLINE.md`) taught six:
- **An arena is something you fly ROUND, THROUGH and INTO, not a diagram.** The first Flatline crossed big planes at the
  centre (three on the axes, five pages round a spine): exact, clip-free and, in Shombith's words, "not fun". The
  re-lay scatters small structures (loose panes, open-ended tunnels, windowed cages) through the volume, rooms first,
  each the first of 64 hashed draws whose bounding sphere clears every earlier one by a fixed gap. Openness is then a
  property of construction (no carving, no clipping by design), the generator fails if anything finds no room, and the
  ladder grows by adding KINDS of place (panes, then tunnels, then cages), not by thickening one shape. Draw the field
  from a pilot's eye before building the geometry.
- **An EXACT twin is cheap when the geometry is integer first.** Decide which tiles exist on an integer grid
  (`a*a + b*b <= R*R`), keep every float comparison that can move a count in one place (the seam test), and compute that
  one in numpy float32 in the C#'s operation order (the scattered re-lay kept this: the placement hashes, cube-root
  radius and sphere test are float32 in order too). `flatline_budget.py` then matched the C# prism for prism and site for
  site on all four rungs at the first run. Put the geometry in a PURE static class (`FlatlinePanes`) so the tests, the
  twin and the Spawnable all call one thing.
- **A pure helper a Spawnable calls must join `render_card_backgrounds.SOURCES`**, or `bake_prism_layouts.py --check`,
  the clip audit and the card render all fail to compile (CS0246) - they share that list. When the harness fails on files
  you never touched, it is upstream drift (REFACTOR_BACKLOG S95, S112): list the missing helper or add the shim member
  (`PlatformShim.cs`); `--check` compares pixels, so a longer SOURCES list stales no committed card.
- **A hull that already runs its own autopilot gets a mode AI that only STEERS.** `BrittleStarExecutor.Autopilot` presses
  fire, modes and the ring itself; a second presser fights it every few seconds. `SetExternalTargetProvider` and nothing
  else, and the generator fails if the hunter calls `PerformShipControllerActions`.
- **An ability that reads the WORLD changes with the arena, and that is the arena's lever, not a vessel change.** The ring
  blast's disk is the cell's nucleus radius, falling back to `ringBlastFallbackRadius` when the cell has none, so a
  no-nucleus arena shrinks it from ~780 u to 400 u across. Read the executor for every world read before quoting an
  ability's size in a mode doc (the first FLATLINE.md quoted the nucleus number).
- **A per-intensity target is Cleave's shape** (scalar + `...ByIntensity` list, the `Build` twins, `SameInts` / `CopyInts`
  rows, the window's per-rung rows, the monitor resolving `SelectedIntensity` on the server). `set_end_condition`
  authors scalars only; the list rides beside it (`author_flatline_assets.py` §9).

Kelpwood (the Angler's Charge game, 2026-11-09, `claude/sweet-wozniak-tk48pb`, `KELPWOOD.md`) taught four:
- **A charge AI may press the hull's own trigger, and nothing else.** The Angler's lock is LT held while the beam
  meets a rival, so `KelpwoodHunter` writes `IInputStatus.LeftTriggerAnalog` on the AI hull (replicated, the channel a
  pad writes; the hull's own `AnglerLeapfrogAIPolicySO` writes RT the same way) and releases it after a short hold.
  The generator fails if it presses anything else or stops pressing it. Read the executor for how the verb is DECIDED
  (`UpdateBeamLock`: one lock per beam ENTRY, not per frame held) before choosing the hold.
- **Ids move twice in a day; check every in-flight branch, then again right before the PR.** Flatline went 84 -> 85 ->
  84 on its own branch while Jackstraw went 84 -> 85 on bleeding-edge, and both claimed toast 160 too. Take an id free
  on every branch (a 644-branch scan is about a minute), and re-read the enum after merging the base: the collision
  you flagged may have been settled for you.
- **The card harness's prism count is arena + staging.** Kelpwood read 881 against the mirror's 880 (its one beam box)
  and Jackstraw 1,140 against 1,122 (its 18 chain links): subtract your `accents()` pieces before calling a mismatch.
- **`generated_assets` audits the index, not the working tree.** It was green while the new scene was untracked and
  red once committed (the package-asset false positive, since fixed). Run the gates after `git add`.
- **A shared rule can be reused by subclassing, with the mode's words on its own asset**
  (`KelpwoodScoringRuleSO : JackstrawScoringRuleSO`): a Strike pays its points either way. The base's tooltips then
  name the wrong hull (REFACTOR_BACKLOG K1).

Corona (the Manta's Charge game, 2026-11-10, `claude/amazing-goldberg-7zn80e`, `CORONA.md`) taught five:
- **A mirror with NO random draws still has count-moving float comparisons; measure their margin and tune the authored
  value, not the code.** Corona's only count-changing tests are "inside a gate" and "inside a rim" (a dot product against
  a cosine). `corona_budget.build` reports every tile's least distance from both boundaries, the generator fails under
  1e-4, and quarter-degree gate angles (exact in float32) cleared every rung. Turning the shells' twist could not help:
  the tightest tile sat on the untwisted first shell. Search the authored values in the mirror, then commit them.
- **When the AI must route through the arena, have the Spawnable publish the geometry** (`SpawnableCorona.ShellRadius`,
  `GateDirection`), and the AI reads the same numbers the C# lays from. A second copy of the gate layout in the AI is a
  drift waiting for the next retune.
- **A passive kit means a steering-only AI.** Every Manta Charge verb is automatic (skim, graze, crystal) and the hull's
  own autopilot drive Soars, so `CoronaStinger` writes `SetExternalTargetProvider` and nothing else; the generator fails
  on `InputStatus` or `PerformShipControllerActions`. Read the verb's GATE before designing the encounter:
  `TryPlantOnVessel` plants only while the planter is at least as fast as the target, so a graze is an overtake.
- **A sibling mode's ability override is not precedent.** Bloomrush sets the Manta's fuse per intensity
  (`MantaBombRules.FuseSecondsOverride`); it predates the LOCKED vessel rule, so a new mode must not copy it. Corona's
  generator greps its own controller and AI for it.
- **The card renderer's full run can be blocked by another card** (`BurrowRun: no recipe`, REFACTOR_BACKLOG S95 / C4):
  render the new card by name, and know a named run skips the manifest. Add the new scene to
  `author_preview_spawns.SCENE_FOR_MODE` too: Kelpwood shipped without it and its preview went unchecked.

Glasshouse (the Angler's Mass game, 2026-11-10, `claude/peaceful-cerf-vpl3x8`, `GLASSHOUSE.md`) taught five:
- **Mass with no roster owner (flora, laid structure) is credited into the existing stat by DELTAS, never by
  assignment.** `StatsManager` moves `VolumeRemaining` by += / -= for every trail prism between your samples, so writing
  the garden's total over it erases the trail. Keep a book of what each pilot was credited (`GlasshouseGarden.ShareBook`)
  and add only the change; a test interleaves both producers and asserts "trail + garden".
- **A grazing arena needs a NUCLEUS to be fair.** Without one, a herbivore eats only what is not its own colour and the
  herds are born in the controlling (leading) colour, so the leader's mass is immune and the trailer's is eaten
  (Tapestry's reason for having no fauna). With one, everything outside it is edible whatever its colour
  (`Cell.IsPreyForHerbivore`), and the nucleus is a sanctuary, so count only mass outside it.
- **A species the cell must KNOW but never plant wild** (the Angler's seed reads `SpawnProfile.SupportedFloras`) is a
  mode-owned config copy with `SpawnProbability: 0`, no initial batch and no seeder floor: `AllowSpawn` is
  `Random.value < p`, so 0 never plants, and reproduction (`Flora.SpawnOffspring`, its parent's colour) still spreads it.
  A non-positive `FloraPopulationScale` is read as 1, so the scale cannot do this.
- **`ScoreReveal`'s fourth argument is `formatAsTime`, not "did win".** A points mode passes `false` on both sides;
  Tapestry and Bloomrush passed `true` on victory and so showed a volume as `MM:SS:CS` (fixed 2026-11-11,
  `claude/wonderful-franklin-x851ah`). `PointsRuleRevealTests.EveryShippedPointsRule_RevealsAWin_AsACount` now fails
  any scoring-rule asset with `golfRules: 0` whose win reveal formats as a time.
- **"The next free id" was taken twenty minutes before the push** (Harvest, the Thresher's Space game, on a sibling
  session of the same plan). Re-scan every branch for the enum value AND the toast id right before the first push,
  not only at design time. The generator mints guids from names, so a renumber moved only the ids and the
  always-unlocked row.
Harvest (the Thresher's Space game, 2026-11-10, `claude/keen-rubin-rw7p6q`, `HARVEST.md`) taught five:
- **Fly the vessel's own solver round the arena before laying the arena.** The sheaves were built, baked and gated on
  a premise nobody had flown: that a circling Thresher's chain sweeps a sheaf's stalks. Its chain TRAILS (21-60 u behind
  at cruise) and cuts only its own path, so the stalks starved the loop (0-11 cuts a lap). Compiling the shipped
  `ThresherChainSolver.cs` against `card_art_harness/UnityShim.cs` in a console project, with the dials read from the
  asset, answered it in an hour (`Tools/Build/harvest_stalk_lab/`). Do that at the brief, when the arena is still a
  drawing.
- **A destruction race on a field that never regrows must fit three domains' targets in the smallest hostile pool**,
  or the field can run dry with nobody at the target. Rampage restocks through flora; a baked field does not. Assert
  it per rung (`author_harvest_assets.py`); it cut Harvest's target from 1,500 to 750.
- **A colour rule over N sheaves must be fair at each rung's N.** "One in three" gave 2 / 1 / 1 among twelve; check
  the counts per domain in the mirror and pin them in a test.
- **A generator check that the BAKE equals the mirror deadlocks the run that changes the field** (second pass,
  2026-11-10). The bake reads the prefab on disk, the generator refuses to write the new prefab while the bake is stale,
  so neither can move first. Gate the comparison on the prefab being unchanged by this run (`g.read(rel) ==
  g.files[rel]`), print "re-bake, then re-run" otherwise, and let `--check` (which diffs the prefab) carry the rest.
  Order: generator, `bake_prism_layouts.py`, generator `--check`.
- **A mode's own creatures are its own COPIES of a species config** (Harvest I4 copies four, Serpentine's
  `_own_cell_assets` pattern): regex-replace only `m_Name` and the three population numbers, fail if any key is not
  found exactly once, and mint the guid from the new name. Editing the shared asset retunes every mode that uses it.

Shatterplane (the Brittle Star's Space game, 2026-11-10, `claude/fervent-edison-ol600q`, `SHATTERPLANE.md`) taught seven:
- **A mode may SIZE an ability that reads the world, through the world.** The ring blast is as wide as the nucleus of the
  cell it blows in, so "make the ring reach a little further" is an ARENA change: give the cell configs the standard
  `NucleusPrefab` (a cell with none cannot be sized, and the blast falls back to the vessel's own constant), then call
  `arenaCell.SetNucleusWorldRadius(r)` and `NucleusIsControlZone = false` on EVERY peer in `OnNetworkSpawn` (a radius asked
  for before the cell builds its nucleus is honoured when it does; Wrecking Ball's rule). The generator greps the executor
  for the read (`NucleusVisualWorldRadius`) so the lever fails loudly the day the vessel stops reading it.
- **Model the verb's tolerance before sizing the arena.** `shatterplane_budget.ring_yield` ran the hull's own disk test
  (`BrittleStarRules.DiskReaches`) against a plate at 0-45 degrees of tilt: on the planned 19-tile plates one degree already
  lost 18%, and the big plates did not all fit the field. The shipped plates are smaller and more numerous. The concept
  renders the person approved were the old size: say what moved and why.
- **`audit_prism_clipping.py` takes the Spawnable TYPE, not the prefab stem.** Given `SpawnableShatterplane1` it reported
  `0 generators on 0 prefabs ... clipping: 0`, which reads as a pass; `SpawnableShatterplane` ran 16 audits. Read the
  generator count before the clipping count.
- **`TemplateText` is `CosmicShore.UI`** (its file sits under `Data/`): a rule that subclasses `RampageScoringRuleSO` to
  move its literal results text into authored fields needs that using. The reference compile caught it; the stub-free
  using-directive gate passed once it was there.
- **Never `git stash -u` while a background reference compile reads the tree** (it hides the untracked new files mid-run);
  merge another branch from a clean tree or after the compile ends. And the card accent belongs on a structure the camera
  sees face-on: turning the `CAMERAS` row was faster than scoring structures by view.
- **Two sessions working ONE plan will take the same id, and only one of the two collisions conflicts.** Harvest (the
  Thresher's Space game, from the same `PLAN.md`) landed `GameModes` 88 and toast 163 an hour before this PR merged.
  `GameModes` conflicted; `GameToastSituation` auto-merged into two members worth 163 with NO marker. The loser renumbers
  (its own enum, generator `MODE_ID`, toast id, preview mapping, test case, docs; the generator re-emits the card, toast
  config, preview and unlock list), skipping ids a third in-flight branch claims (Glasshouse, 89 / 164). Run a duplicate-
  value check over every enum after the merge, and scan EVERY branch for the next free id right before the PR.
- **...and they get the same FOLLOW-UP too, so a platform hook gets built twice.** "I3 team crystals, I4 fauna" reached
  Harvest's and Shatterplane's sessions alike; each added its own team gate (`ElementalCrystalImpactor.RestrictTo` and a
  `TeamElementalCrystalImpactor` subclass). Harvest merged first, so Shatterplane adopted `RestrictTo` and deleted its
  class. Before adding a hook to a platform class, `git log origin/bleeding-edge -- <that class>` and read the sibling
  mode's newest commits; when two sessions work one plan, assume the other one is building the same thing.

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
- **A race (Time-genre) mode is a training ground.** Its hull's AI learns it from people's races where they
  play, and from self-play where they do not (`/compute`). The second needs an OFFLINE race model of the
  mode: a pure course generator (ported and parity-gated like `Tools/Compute/evaluators/course_port.py`)
  and the hull's drive sim. Without one, the mode shows up in `/compute-scout`'s coverage lens as a cell
  nothing can teach; say in `<MODE>.md` whether it has one.

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


## The design brief comes first (2026-11-07)

Before a new mode rule, track, environment or AI behaviour, write the one-page design brief (`Docs/claude/DESIGN_BRIEF.md`): the fundamental it serves,
the principles it obeys by id (`python3 Tools/Build/principles.py --area modes`; `Docs/claude/PRINCIPLES.md` holds
the owners' rules in their words, drafted on 2026-11-07 and waiting on each owner to confirm or strike), the one it
comes closest to breaking, the cheapest prototype, who judges it and where, and the retire condition. A design that
breaks a drafted principle is a proposal to that principle's owner first, not a build. A mode never changes the vessel (LOCKED, Shombith, 2026-10-31): the brief lists only track, environment and AI.
