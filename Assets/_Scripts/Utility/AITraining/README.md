# AI Training Framework

**The intent, in one sentence:** press one button, walk away, and come back to
AI pilots that are *better, varied, and honest* — deployable at four difficulty
levels into any minigame, against or alongside any human, for any vessel the
game has or will ever have.

This document is the framework's memory. It records not just how it works but
the decisions and reversals that shaped it, so the next person (or the next
session) extends it instead of re-learning it.

---

## The spirit

Four values, in priority order:

1. **Honest AI.** The trainer only ever produces pilots that play the game the
   way a player could: reading the world, writing input. Nothing here teleports,
   nothing writes a transform, nothing reads privileged state to cheat an
   outcome. A trained pilot's advantage is *tuning*, and its handicap at lower
   intensities is *humanly-shaped imperfection* (reaction delay, hand noise,
   slower ability cadence) — never a lobotomy.
2. **Replayable and fun beats optimal.** A perfect pilot is a boring opponent.
   The archive keeps a **roster of behaviorally distinct personalities** per
   (vessel × mode × intensity) — an *Ace Rammer*, a *Steady Drifter*, a *Rookie
   Cruiser* — and deployment samples one per AI per match. Same training data,
   different night, different opponents.
3. **All skill levels.** Intensity 4 is the trained ceiling, bit-for-bit
   untouched (a unit test holds this). Intensities 1–3 are produced by
   dithering the ceiling: input degradation per frame plus tempo factors at
   apply time. One genome serves every player.
4. **Interrupt-anything durability.** Every completed match is persisted before
   the next one starts. Stop the tool, stop Unity, kill the process — you keep
   everything except the match that was in flight, and an in-flight match is
   never recorded (a partial fitness would poison the rolling mean).

## The architecture — and the decision that defines it

### The parallel-pilot trap (recorded reversal — do not repeat)

The first version of this framework shipped a `TrainingPilot`: a full
replacement brain (8 behavior policies, 3 world sensors, a blending layer) that
disabled the game's `AIPilot` and flew the vessel itself. It was well-built and
it was **the wrong thing**, for the same reason CLAUDE.md forbids mode-local
copies of Cell systems: *a bespoke feature that duplicates or bypasses an
existing fundamental is worse than either*.

While that pilot was being written, the real `AIPilot` grew an orbit-break
(Dubins reachability + empirical orbit detection, measured 326/400 → 400/400
objective completion), objective scoring with commitment hysteresis,
mass-cluster drift aim through the cell's Burst density grid, and a replicated
aim telegraph. The replacement pilot regressed **all of it**, and would have
rotted further with every upstream improvement.

**The pivot:** training now TUNES the shipped pilot through one surgical
upstream surface — `AIPilot.ApplyExternalTuning(ExternalTuning)` — where every
field is optional and null means "keep the authored value". The trainer's
floor is therefore *the game as shipped*: with no archive, no genome, or a
fully disabled genome, the AI is exactly the hand-authored pilot.

If you are ever tempted to reintroduce a replacement brain: the right move is
to grow `AIPilot` itself (or its tuning surface) and teach the genome the new
dial. The pilot is a platform fundamental; the trainer is its tuner.

### The pieces

```
Core/
  GeneSpec, GeneRegistry      — the search space, self-registered by modules
  TrainingGenome              — flat gene bag + enabled-module bits; JSON-portable
  TrainingPopulation          — GA: elites, tournament crossover, gaussian +
                                structural mutation, novelty-bonus selection
  PilotTuningGenes            — THE gene set: maps genome → AIPilot.ExternalTuning,
                                derives personality names
  IntensityDitherer           — flawless → 4 levels: input degradation (per frame)
                                + tempo factors (at apply)
  TrainingFitness             — per-episode weighted score with labeled breakdown
  EpisodeObservation          — the slim per-frame view fitness components read
Fitness/
  FitnessProfileSO            — per-mode recipe asset with 8 named presets
  FitnessComponents           — 20 components incl. the new-era stats
                                (CombatPoints, LifeformsKilled, GoalsScored,
                                HostilePrismsDestroyed)
Pilot/
  TrainingModulator           — genome → live vessel: applies tuning to AIPilot,
                                dithers its steering in LateUpdate, publishes
                                the observation
  TrainingDeploymentService   — auto-installed; samples a roster personality onto
                                every AI in NORMAL play (stands down in training)
Runner/
  TrainingScenarioSO          — what to train: mode, vessel, intensity, episode
                                bounds, early exits, fitness profile
  TrainingSessionStateSO      — the population + hall of fame; survives crashes
  TrainingSessionRunner       — the episode loop: checkout → apply → play →
                                harvest → evolve → persist. Does NOT restart the
                                match; an episode IS a turn
  TrainingMatchDriver         — PLAYS THE GAME with nobody at the keyboard: holds
                                every vessel on autopilot, presses Ready, presses
                                Play Again. The loop lives here
  TrainingAutoLauncher        — drives Bootstrap → Auth → Menu → game scene with
                                zero input, then stands up the driver + runner
Persistence/
  TrainingArchiveSO           — the deployable product: champion + personality
                                roster per (vessel × mode × intensity)
  TrainingControlSO           — the editor ↔ runtime handoff for the Learn button
  GenomeJson                  — git-shareable genome sidecars
Telemetry/  TrainingTelemetrySO — SOAP status surface for the window / any HUD
Editor/
  TrainingEditorWindow        — FrogletTools → AI Training: the Learn button,
                                Quick Setup, search-space browser, archive browser
  TrainingPlayModeHook        — spawns the auto-launcher on EnteredPlayMode when
                                the control asset says AutoStartOnPlay
```

### What the genome can express

- **PilotTuning** (always on): skill dial, throttle base/ramp, orbit-break
  geometry (approach run, away bias, capture radius), objective commitment.
- **PilotStyle** (structurally toggleable): ram, drift, prefer-approach-run
  objective choice. Module off = authored style kept.
- **AbilityTempo** (structurally toggleable): scales each authored ability's
  Duration/Cooldown from captured baselines — evolution finds a vessel's
  cadence without knowing what its abilities are.

Structural mutation flipping modules on/off is the honest version of "learning
new behaviors": bounded behavioral variety on top of a pilot that always flies
competently. The novelty bonus (hamming distance over behavior fingerprints)
keeps overnight runs from collapsing onto one local optimum — and those same
fingerprints are what qualify a genome for a roster seat.

## One-click training

1. `FrogletTools → AI Training`
2. Press **Learn**.

The button creates default assets if missing (idempotent Quick Setup), flips
`TrainingControlSO.AutoStartOnPlay`, and enters Play mode. The play-mode hook
spawns the auto-launcher, which:

- lets the app boot normally (Bootstrap → Auth → Menu_Main),
- waits for `gameData.OnClientReady` **in the menu scene** (the one signal that
  fires after `MainMenuController` has finished writing its own defaults —
  launching off `AppState.MainMenu` was tried and races the menu's Start; see
  "lessons" below),
- resolves the mode's own `SO_ArcadeGame` card and calls
  `gameData.SyncFromArcadeGame(card)` — the exact path the real launch button
  takes, so **a new minigame is trainable the moment its card asset exists** —
  then overrides player count / vessel / intensity and launches,
- ensures a `TrainingSessionRunner` in the game scene and flips the host's
  vessel onto autopilot so the match is fully AI vs AI (3 seats by default).

Matches then loop forever: play → harvest fitness from `RoundStats` → evolve →
deploy champions + roster contenders to the archive → mark assets dirty →
`ResetForReplay`. Stop any time; keep everything completed.

## Deployment: player vs (and with) trained AI

`TrainingDeploymentService` auto-installs at runtime and listens to
`OnPlayerPairInitialized`. In any normal match (`gameData.IsTraining == false`),
each AI player gets a `TrainingModulator` carrying a personality **sampled from
the archive roster** at the match's selected intensity. Console logs name who
showed up: `[Deploy] BotName flies trained personality 'Ace Drifter'`.

Because deployment only re-tunes the shipped pilot, its failure floor is the
unmodified game. Toggle globally via `TrainingControlSO.DeployArchiveInNormalPlay`.

**Co-op:** the same machinery serves AI *teammates*. The
`ApplyCoOpTeammateDefaults` fitness preset judges a partner on shared-objective
contribution with a heavy friendly-fire penalty and — deliberately — no time
pressure: a partner that rushes the match ends the human's fun. Train a
teammate bucket by pointing a scenario at `Multiplayer2v2CoOpVsAI` (or any
mode) with that profile; the mode's own spawn pipeline decides who is on whose
domain.

## Fitness recipes

`FitnessProfileSO` presets, selectable in one call or auto-picked by game mode
when a scenario has no profile assigned:

| Preset | Modes | Rewards |
|---|---|---|
| Racing | HexRace, default | crystals, score, boost time; time penalty, friendly-fire penalty |
| Joust | MultiplayerJoust | joust hits, enemy contact, survival |
| Gunnery | DogFight, Salvo | CombatPoints, demolition, survival |
| Hunt | WildlifeBlitz ×2, WildlifeLiberation | LifeformsKilled, hearts collected |
| Court | AstroLeague, ScarabScramble | GoalsScored, pace |
| Cellular | CrystalCapture, CellularDuel ×2, Rampage, Ribcage | volume built/restored/destroyed-hostile; heavy friendly-fire penalty |
| Co-op teammate | 2v2CoOpVsAI, any co-op | shared objective, zero friendly fire, no rush |
| Freestyle | MultiplayerFreestyle | distance, speed, ability expressiveness |

Fitness reads `IRoundStats` — the platform's own ledger — so what trains well
is what actually scores in the mode's own rules.

## Lessons this framework paid for (keep them)

1. **The parallel-pilot trap** (above). Tune the fundamental, don't fork it.
2. **Launch on `OnClientReady`, not `AppState.MainMenu`.** The state machine
   reaches MainMenu *before* the menu scene loads; configuring GameDataSO then
   gets stomped by `MainMenuController.ConfigureMenuGameData`, and
   `InvokeGameLaunch` can fire into a not-yet-listening SceneLoader. The
   12-second safety timeout remains for a menu that never reports ready.
3. **Never record an interrupted episode.** A partial fitness silently poisons
   the rolling mean; StopSession unwinds without harvesting.
4. **Resolve scenes through `SyncFromArcadeGame`.** The first version carried a
   hardcoded mode→scene table; it was stale within one upstream cycle. The
   platform's card assets are the single source of truth — the table survives
   only as a warn-loudly fallback.
5. **`IReadOnlyCollection<string>.Contains`** resolves through the span
   extension overload without LINQ in scope (CS7036). `GeneRegistry` exposes
   `IsDefaultEnabled()` instead.
6. **Scale ability timings from captured baselines**, never in place — a
   re-applied genome (new episode, new match) must not compound.
7. **Launching a match is not playing one.** The first version drove the flow
   as far as the game scene and stopped there, because every mode gates its
   turn behind the **Ready button** and nothing pressed it. *Getting to the
   scene is the easy half; a set-and-forget loop has to press every button a
   human would.* `TrainingMatchDriver` presses the REAL button, so every gate
   a human waits for — connecting panel, arena build, cinematic, per-round
   unlock — still holds; calling the controller is the announced fallback.
8. **A one-shot autopilot flip cannot survive the platform disagreeing.** The
   host's player is a HUMAN player, so `Player.StartPlayer` un-pauses its input
   at every countdown end and `EnsureLocalHumanCanMove` does it again — and an
   un-paused `InputController` writes the SAME `IInputStatus` the `AIPilot`
   writes, every frame. The AI *was* steering; a resting keyboard was
   overwriting it, which reads on screen as "there is no AI flying my ship."
   The fix is not a bigger hammer at spawn time, it is **re-assertion every
   frame** — cheap, and the only thing that holds against a rule the platform
   re-applies on its own schedule. (`StartAIPilot` itself is only called on the
   transition: it clears and restarts every ability coroutine, so per-frame
   calls would mean no ability ever completes.)
9. **`EditorApplication.isPlaying = true` plays the OPEN scene**, not the first
   build scene. Pressing Learn from a game or tool scene skipped AppManager
   entirely — no DI, no auth, no `OnClientReady` — and the launcher waited for
   a menu that was never coming. A tool must put the editor into the state its
   flow assumes rather than assume it.
10. **Two replay paths racing is worse than none.** The runner used to call
   `gameData.ResetForReplay()` (a data-only reset) while the driver asked the
   controller for a real, networked replay. Game flow has exactly one owner:
   the driver. The runner's job ends at "the episode is banked."
11. **An episode is a TURN, not "the moment vessels exist."** Players are in the
   roster through the arena build and the Ready gate; starting the fitness clock
   there charges every genome for time it spent sitting on the start line — the
   easiest possible way to make a fitness function measure the loading screen.
12. **`GameDataSO.IsTraining` already means something else** — the legacy
   single-player *training game*, set true by `Arcade.LaunchTrainingGame` for
   ordinary arcade launches. Reading it as "an AI-training run is active" would
   have disabled deployment in exactly the sessions a player opens to fly
   against the trained AI. `TrainingSession.IsActive` is ours, and it resets at
   `SubsystemRegistration` because a leaked static outlives play mode.
13. **A finished match must LATCH.** Skim Race ends with its objective still
   satisfied — stats only zero at the next countdown — so a turn restarted on a
   finished match ends on its first frame. Press GO into that and the countdown
   replays every couple of seconds forever, going nowhere. `_gameOver` makes
   replay the driver's only remaining action in a scene, and pressing GO again
   structurally impossible.
14. **A press interval shorter than the thing it triggers is an infinite loop.**
   The pre-turn countdown is ~4s and `CountdownTimer.BeginCountdown` KILLS and
   restarts its DOTween sequence, so a 1.5s re-press could never let it finish —
   the number just fell back to 3. Wait for the EFFECT (`IsTurnRunning`), not a
   cooldown.
15. **A networked replay can be refused, and refusal is silent.** It is a scene
   load behind an async sequence; one swallowed failure ends the night. Retry on
   an interval, announce every attempt, and say plainly when it is stuck.


## Roadmap (in rough order of value)

- **Difficulty calibration from human outcomes.** Record human-vs-AI match
  results per intensity into the archive and nudge dither settings toward a
  target win rate (~55% for the human at their chosen intensity). The data
  hook is cheap; the loop closes itself overnight.
- **Per-mode dither profiles.** A racing "hand" errs differently from a
  gunnery "hand"; `IntensityDitherer.LevelsByIntensity` is serializable and
  ready to be lifted into a per-scenario asset when tuning demands it.
- **Scenario rotation.** The Schedule tab is UI-only today; the runner already
  supports `targetEpisodes`, so chaining scenarios (HexRace tonight, Joust
  tomorrow) is a small editor loop away.
- **New tuning dials.** When `AIPilot` (or a vessel action) grows a knob worth
  searching, add a gene to `PilotTuningGenes` and it joins every future run —
  old genomes stay valid (missing genes read registry defaults).
- **True in-situ co-op fitness** (assists, proximity support, revive-style
  events) once RoundStats grows team-attributed stats.

## Constraints (unchanged, non-negotiable)

- Input only. No transform writes, no physics writes, no state cheats.
- Training and deployment share one code path (`TrainingModulator`), so what
  you trained is exactly what ships.
- Intensity 4 is the identity transform. Tested.
- Everything survives an interrupt except the in-flight match. Tested by design
  review; the persistence calls sit before `ResetForReplay` in the episode loop.
