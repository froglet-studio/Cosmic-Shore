# AI training: from the artifact prototype to AIs that learn from players in Unity

**The goal (Yash, 2026-10-15).** Every vessel's AI trained in Unity, in three ways:

- **AIs train each other** by racing.
- **AIs learn from players.** Anyone who plays is asked "Would you like to train the AIs with your own moves?", and
  their flying teaches the AI.
- **People watch it happen** from a Third Eye view in a simulation scene, with the data drawn on screen.

The artifacts are the prototype. Unity is where it has to live. Regatta comes first; every hull is in scope.

This doc is the plan. It builds on what exists (section 2) and does not replace it: the genetic trainer is the work of
studyholic, Garrett and Shombith (`who_works_where.py query Assets/_Scripts/Utility/AITraining`), and any change to it
is agreed with them first.

**Decision (Yash, 2026-10-16): training data comes from playing in the Unity editor only, and tests happen in the
editor too.** The artifact below was the prototype; it was **deleted on 2026-10-16** with `Docs/Studios/RegattaAI/`
(git history keeps it). Section 1 is kept as a record of what it showed (`SITUATION_RECORDING_PLAN.md`).

## 1. The prototype: Training AI for Regatta (the artifact)

Deleted 2026-10-16 (it was https://claude.ai/artifact/FCC7UWbs2wC6LXYMVW4KA4, repo `Docs/Studios/RegattaAI/`). It was renamed from "Regatta AI" on
2026-10-15.

- **Every vessel class.** All 16 hulls race. The 8 card hulls fly their modelled speed verbs; the other 8 fly a
  **generic drive**, labelled as such: cruise, plus their prefab's boost held on straights. Their flight numbers are
  **generated** from the prefabs (`export_fleet.py` writes `fleet.js` with `regatta_balance.py`'s own reader), the
  same way `course.js` is generated from the course code.
- **AIs train each other** (Training tab). A population of one hull's AIs races on the ticked intensities, each AI
  with different steering and drive settings. The fastest quarter breed the next generation (a cross-entropy search),
  and the champion races in every generation, so a new AI only takes over by beating it.
  - Measured: the Rhino on I1 + I2, 10 AIs × 6 generations, went from **62.7 s to 28.5 s** a race.
  - In a live race the trained Rhino beat the untrained one, **20.2 s to 41.5 s**.
- **Learn from me.** Fly a hull yourself for a lap and your line is kept (one per hull and intensity). Training then
  also scores each AI by how far it strays from your line (the *Learn from me* weight).
- **Watch it.** *Race trained vs untrained* seats both AIs in a live race. The Chase, Follow and Free cameras, trails
  and the planned-route line all show what each one does.
- **Hand-off.** *Copy JSON* exports the trained AI as `cosmic-shore.ai-tuning/1`: the steering knobs, with the Unity
  field each one maps to; the hull's drive knobs; the race time; and where it was learned from. Section 4 T2 imports it.

What the prototype does not prove: it is a simplified flight model on Hard with a fixed seed. Unity has to re-measure
everything (section 3, the noise warning).

## 2. What Unity has today (inventory, 2026-10-15)

| Piece | State | Where | The gap |
|---|---|---|---|
| Third Eye: Chase / Follow / Free cameras on any pilot, trails, AI aim lines, per-AI phase readout | Built, **editor only** | `Assets/_Scripts/Editor/AI/ThirdEye*.cs` | Not in a player build; read-only (no time scale, no spawning, no hull picking); no graphs |
| Genetic AI trainer: genome population, novelty bonus, hall of fame, fitness recipes per mode, overnight schedule, deploys through `TrainingArchiveSO` | Built (~8,100 lines) | `Assets/_Scripts/Utility/AITraining/` (README), FrogletTools ▸ AI ▸ AI Training | One archive entry (Skim Race Squirrel, generation 0), and it is unused in play. No Regatta fitness recipe or steering genes. Deploys only in the editor. Amoebius's tournament found its best genome flies worse than a random one: selection is noise-dominated |
| Skim Race AI Benchmark: N races through the real launch flow, a JSON record per race, CSV traces | Built | `Editor/AI/SkimRaceBenchmarkWindow.cs`, `Controller/AI/SkimRace/SkimRace*Recorder.cs` | Skim Race only |
| Time scale above 1 | Built, ×8 cap | `TrainingAutoLauncher.cs` | Only inside the trainer |
| AI on your own seat, spectator camera | Built | `Vessel.ToggleAIPilot`, `SpectatorController.cs` | — |
| Offline simulators: the Skim Race sim (cross-entropy tuning that writes the AI config assets), the Regatta course harness, the Regatta balance model | Built | `Tools/Build/skimrace_sim_harness/`, `skimrace_retune.py`, `regatta_course_harness/`, `regatta_balance.py` | No Regatta AI simulator in C# |
| Amoebius headless trainer (`--train` / `replay` / `eval`) | Built, **under evaluation** (`Docs/TOOLING_DECISIONS.md`) | `Port/docs/AI_TRAINING.md` | Not to be grown here |
| AI simulation scene, Test Range | **Planned only** | `Docs/Studios/VESSEL_TEST_RANGE_PLAN.md` (R1–R6 unbuilt) | — |
| Recording a player's flight; learning the Regatta Squirrel's numbers from it | **Built 2026-10-15** (L3 steps 1 and 4A, your own flights, editor and dev builds) | `HumanFlightRecorder.cs`, `Tools/Build/learn_from_flights.py`, `Docs/SKIM_RACE_AI.md` §16 | Ghosts, behaviour cloning, players' flights (consent) |
| Situations: what the pilot faced, did and meant, per moment; learning the commit distance, the skim choice and the boost angle from them | **Built 2026-10-16** (in `situation_features.py` over editor flights; the recorder in Unity) | `SITUATION_RECORDING_PLAN.md`, `situation_features.json`, `Tools/Build/situation_features.py`, `HumanFlightRecorder` flight/3 | The learned knobs are not in Unity's steering yet |
| Neural inference | Package only | `com.unity.ai.inference` 2.6.1 (Sentis) is in `Packages/manifest.json`; no code or model uses it | — |
| Consent, analytics, Cloud Save | Built | `AnalyticsServiceFacade.cs` (opt-in consent, COPPA age gate), `UI/Privacy/PrivacyConsentOverlay.cs` (text from `PrivacyConsentConfigSO`), `UGSDataService.cs` (Cloud Save 3.4.0, key-value only) | No AI-training consent; no store for flight files |

## 3. The system, in four layers

Each layer is useful on its own, and each is built on the layer before it.

### L1 — Watch: the AI Lab scene (the Test Range, made the training lab)

One dev-only harness that installs into the real mode scene. This is `VESSEL_TEST_RANGE_PLAN.md`'s Test Range, built
rather than a second one. Its parts:

- **Seats:** any hull on any seat, each with its own difficulty and its trained or untrained AI (as in the artifact's
  *Race trained vs untrained*).
- **Time:** 0.25×–8×, pause, single step (the trainer's ×8 cap, reused).
- **The Third Eye, in the lab at runtime.** The window stays editor-only, but its rig, trails and readout move to a
  runtime assembly so a development build shows them too. Data on screen:
  - a speed graph per pilot;
  - a phase timeline (approach / through / break-off / missed mouth);
  - rings missed;
  - the planned route line (approach point, ring, point past it);
  - the race table.
- **A scorecard file per run**, in the Skim Race benchmark's JSON shape, so Unity, the artifact and the trainer
  compare the same numbers.

### L2 — Train: AIs train each other (the genetic trainer, extended for Regatta)

- **A Regatta fitness recipe** (`FitnessProfileSO`): race time, rings threaded, missed mouths and break-offs, with a
  DNF costed like the artifact's (the limit plus 10 s a ring short).
- **Steering genes** registered with `GeneRegistry` from `GateRaceController`'s knobs:
  - the shipped ones: `aiApproachLead`, `aiCommitDistance`, `aiThroughDistance`, the lead cap;
  - the lab ones, once ported: go straight when lined up, exit toward the next ring, break-off distance.
- **Drive genes per hull**, from each hull's boost policy (`AIBoostPolicySO` subclasses).
- **Self-play already exists in the trainer** (population plus hall of fame). What it lacks is honest evaluation.
  Amoebius measured that one race per genome is noise, so each candidate races several seeds, seat-balanced, and a
  champion only changes when it wins by more than the noise.
- **The artifact JSON importer.** A menu item reads `cosmic-shore.ai-tuning/1` and writes the hull's genome (or its
  steering asset). Prototype findings then reach Unity in one step, through review.
- **Deploying in a player build.** Today `TrainingDeploymentService` finds assets through `AssetDatabase` in the
  editor only; it needs to read the archive through `Resources` or a direct reference.

### L3 — Learn from players

**Record the situation, not just the sticks:** what to record, compute and learn so the AI knows *why* a player did
something (target, line-up, obstacles, skims taken and skipped, outcome) is planned in `SITUATION_RECORDING_PLAN.md`.

1. **The flight recorder** (runtime, new). While a human races, it samples about 10 times a second:
   - position, velocity and heading;
   - the sticks and triggers (the same input the AI writes; the input-only contract makes the two comparable);
   - ring index;
   - the speed verb's state.

   It is compact (a few KB a race) and starts local-only, in development builds.
2. **Consent**, its own opt-in, never bundled with analytics: "Would you like to train the AIs with your own moves?"
   - It reuses `PrivacyConsentOverlay` and the consent storage.
   - Its text is authored in an SO (the LOCKED player-text rule).
   - It is off by default, with a Settings toggle, and is never asked of a player under the age gate.
   - It sends no personal data, and a player can delete what they sent.
3. **Upload.** Cloud Save is key-value only, so flights need a file store. Options:
   - Cloud Save player files, if this Cloud Save version has them;
   - the R2 bucket the toybox already publishes to;
   - our own endpoint.

   This is a team decision; Shombith owns the UGS layer.
4. **Learning, cheap and explainable first.**
   - *Step A:* the trainer's fitness gains the artifact's *Learn from me* term: how far an AI strays from the recorded
     lines of the best players. The AI keeps its hand-written structure and its numbers move toward how good players
     fly.
   - *Step B*, only once step A shows the data is worth it: **behaviour cloning**. A small network from state to
     sticks is trained offline on the recorded flights, exported as ONNX and run by Sentis (already in the manifest) as
     a "learned drive" behind the same input-only contract.
   - Ghosts (a top player's line as a race opponent) come almost free from the recorder.

### L4 — Ship: nothing deploys itself

A trained AI reaches players only after a person:

- reads its diff against the current AI;
- watches it in the Lab against the current one;
- sees it win by more than the noise.

Easy and Medium stay the same pilot with belief handicaps (`GateRaceHandicap`), so training never makes Easy harder.

## 4. Order of work

| Step | What | Where | Depends on |
|---|---|---|---|
| **T0** | Training AI for Regatta: all 16 hulls, AIs racing each other, learn from me, trained vs untrained, JSON hand-off | Artifact | Done 2026-10-15; **deleted 2026-10-16** (editor only) |
| **T1** | The AI Lab scene: Test Range R1–R3 (seats, time, runtime Third Eye panels, scorecard file) in `MinigameRegatta` first | Unity, dev-only | — |
| **T2** | Regatta in the genetic trainer (fitness recipe, steering and drive genes, multi-seed evaluation) and the artifact JSON importer | Unity, `AITraining/` (with its authors) | T1 for watching |
| **T3** | The flight recorder (local), and *Learn from me* in the Unity trainer's fitness | Unity runtime | T2 |
| **T4** | The consent popup, the upload and the server-side store | Unity and UGS (Shombith) | T3; the team's privacy call |
| **T5** | Behaviour cloning with Sentis | Offline training, Unity inference | T4's data |

Everything lands on `Ys-bleeding-edge` for testing; nothing goes to `bleeding-edge` until Yash has tested it
(`BRANCH_WORKFLOW.md`).

## 5. Decisions for the team

1. **Extend the existing genetic trainer** (recommended) or build a second one. This plan assumes extending it, with
   its authors' agreement.
2. **Where player flights are stored** (T4), and the privacy wording. The core four decide; Shombith owns UGS.
3. **Whether a network-driven AI (T5) may ship**, or whether learning only moves the numbers of hand-written AIs.
4. **Which hulls get real drives next.** The generic drive is a stand-in, and a hull's own abilities are modelled when
   its AI is built (`REGATTA_AI_ROADMAP.md`).
