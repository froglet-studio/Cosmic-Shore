# Skim Race AI — Session Log, 2026-10-05 → 2026-10-07 (branch `claude/confident-pascal-w76l2o`)

A dated record of ONE working session: what was asked, what was decided, what was built, what was
measured, how it was verified, and what is still open — so a later "report on this work" can be
answered from the tree. The engineering content itself lives in `Docs/SKIM_RACE_AI.md` (sections are
cited below); this file is the narrative and the index of commits. Numbers here are copied from the
commits and doc sections they cite, not re-derived.

Branch state at the end: level with `bleeding-edge`, `Ys-bleeding-edge` and
`perf/performance-optimization` (0 commits behind each); 36 commits from this session; head `41e27e9d2`.
**Nothing in this session ran in the Unity editor** (no editor and no `unity` CLI in the cloud
session) — see §5 for what ran instead and §6 for what the editor still owes.

---

## 0. The asks, in order

1. **A middle-ground Skim Race AI** (the Squirrel): performant, good at the objective, simple difficulty
   knobs, on bleeding-edge's patterns — in small steps, with options to choose from and plain explanations.
   (The six-step plan whose Steps 3–6 ran in this session; Steps 1–2 were the earlier mapping/baselining.)
2. **Keep the branch current** with `bleeding-edge`, `claude/bold-fermi-54nlts` and `Bug_Hunt`.
3. **Diagnose an invite failure** ("Invade" could not enter another player's lobby on this branch; it
   worked on bold-fermi).
4. **Make the AI harder**: a human pair in co-op beat two AI almost every race "even though we were doing a
   lot of mistakes"; difficulty levels and their UI; how to test for better AI.
5. **Merge the latest bleeding-edge again and recheck the AI algorithm and the general AI architecture**,
   because "the performance is very low when I'm testing" Skim Race.
6. **Combine this branch with `perf/performance-optimization`** (the perf tricks worked on there) and take
   the latest from `Ys-bleeding-edge`; make sure the docs are well defined and routed from `CLAUDE.md` so a
   session can learn how performance, AI and multiplayer work are tackled.
7. This log.

---

## 1. Timeline and commits

All 36 commits carry this session's `Claude-Session` trailer
(`git log --grep='session_01ULRwYUDYKSHmDzCCo9Xa3V'`). Merges are listed because each was a request.

### 2026-10-05 — difficulty, mistakes, fingerprints, timers, team play, merges

| Commit | What |
|---|---|
| `cda05fde1` | Simulator: `tuneall` — one general policy tuned across every track at once |
| `b2fb41a85` | **Host-only AI difficulty picker** (Easy / Medium / Hard) on the Skim Race launch card; replicated via `LobbySnapshot.AIDifficulty`; remembered |
| `f3dc070d8` | **Easy and Medium make human-like mistakes** (`SkimRaceHandicap`: reaction delay, misjudged crystals); Hard is the unhandicapped pilot |
| `cc9c86b5e`, `8ea0d1745`, `2db9d8c66` | Medium tuned to a 95 s and Easy to a 120 s race at intensity 2; every difficulty checked on every track |
| `0201c9d8d` | `run.sh` parsed whole, so an edit cannot re-run a tune in flight |
| `cd466a408`, `3e35e15ad` | **A general AI policy that finishes every track** (`skimrace-v2-general`; I3 flies it); v1 vs v2 on the same I3 races |
| `f8fc72a73` | **Tuning files know their map**: `SkimRaceTrackFingerprint`, `SkimRaceAIConfigSO.TrackFingerprint`, `PolicyFor` falls back to the general policy and warns once; `skimrace_track_fingerprint.py --check`, `skimrace_retune.py <I>` |
| `9faf5bbc8` | A full-length retune run recorded |
| `16011bbd4`, `5c22c0e1c` | **Profiler timers** `SkimRace.Pilot.*` / `SkimRace.Driver.*` (same races, byte-identical); §12 of the AI doc |
| `f0d56df55`, `1e1f28c0d` | Track-window lookups without a remainder per segment (identical races); cost after |
| `2d0058308`, `01f16e75f`, `10cdd14be`, `82a60fe8e`, `af8224e45`, `edf009206` | Merges: `bleeding-edge` ×3, `bold-fermi` ×2, `Bug_Hunt` — the branch made current (ask 2) |
| `bd9ffbfc0` | **`NetworkConfig.ProtocolVersion` 8 → 9** — `LobbySnapshot` gained `AIDifficulty`; mixed builds read the lobby bytes out of step and the invite "did nothing" (ask 3) |
| `cac24b8f8` | Checklist: the branch's compile against real Unity references |
| `1abcc8b47` | **Team races in the simulator** (`ph.Team`, `ph.TeamRule`) — the co-op diagnosis measured |
| `f452ed38a` | **AI teammates split the crystals** (`SkimRaceTeamAssignment`, `SkimRaceTeamPlan`) at every difficulty |

### 2026-10-06 — seating decision, frame rate, editor cost, stagger, retune

| Commit | What |
|---|---|
| `21f74d8ee` | Seating decision recorded: keep today's rule; the table of every 4-seat shape (§13) |
| `1122e1f3e` | Merge `bleeding-edge` (39 commits: AI boost policies, device tiers, trail cap, touch, toasts…) |
| `cd034077c` | **`ph.PhysicsStep`** — the game's 0.04 s contact step in the simulator; the **frame-rate findings** (§14.1–14.3) |
| `af6a3e8cf` | **`dts=`** — races spread over several frame times; `skimrace_retune.py` tunes across them; the AI's cost at the editor's Debug optimization (§14.4); a per-frame closure allocation removed from the editor race recorder |
| `4b871a5bd` | Planner re-plans on a fixed grid, odd lanes offset half a period (the stagger — later superseded, see 10-07) |
| `462f8f7c3` | **Retune I1, I2, I4 across frame rates** (`skimrace-v5-i1`, `skimrace-v3-i2`, `skimrace-v2-i4`) |

### 2026-10-07 — general retune, three merges, docs routing

| Commit | What |
|---|---|
| `2a96f238b` | **Retune the general policy** (`skimrace-v3-general`); §14.5–14.6; Easy/Medium re-checked; Unity checklist entry |
| `738185629` | Merge `bleeding-edge` (12 commits: the pilot's **objective refactor** — `CrystalTrackObjective` / `RegattaRingObjective`, Regatta's Squirrel AI flies the Skim Race pilot at rings; Urchin autopilot; ally hull picks) — the team plan re-slotted into `CrystalTrackObjective.Planned` |
| `55d9144fe` | **Merge `perf/performance-optimization` — best of both** (ask 6): their re-plan gate over this branch's grid; their float inner loops, `MathfNoAlloc`, cheaper obstacle gather, Mono simulator runtime, `prof` Burst flag, two allocation gates; this branch's team plan, handicap, retuned policies, `dts` / `PhysicsStep` |
| `992bfc9a1` | Merge `Ys-bleeding-edge` (the party layer's request discipline, review Phases 0–1, `UgsRequestPolicy`) |
| `41e27e9d2` | **Docs routed from `CLAUDE.md`**: the AI topic's table of every AI, the performance topic's record/instruments/gates, the multiplayer topic's UGS and package rows; §14 reconciled; `AI_BOOST.md` indexed |

---

## 2. What exists now (the AI as shipped on this branch)

- **The Skim Race pilot** (`Assets/_Scripts/Controller/AI/SkimRace/`): `SkimRacePilot` (lifecycle,
  sensing, actuation — input-only), `SkimRaceDriver` (the decision core, pure C#: racing line, crystal
  pass, lag-compensated steering, throttle, recovery, the I2 track planner, the laid-mass guard),
  `SkimRaceObjective` (`CrystalTrackObjective` for Skim Race, `RegattaRingObjective` for Regatta — the only
  mode-aware part), `SkimRaceTeamPlan` + `SkimRaceTeamAssignment` (AI teammates each fly a different
  crystal), `SkimRaceHandicap` + `SkimRaceDifficultySO` (Easy / Medium mistakes), `SkimRaceReplanGate`
  (one planner re-plan per frame across seats), `SkimRaceTrackFingerprint` (the map a policy was tuned on),
  `SkimRaceTargetTracker` (nearest valid crystal; the lone AI's rule and the teammates' fallback).
- **Policies** (`Assets/Resources/SkimRaceAIConfig*.asset`, authored by
  `Tools/Build/author_skimrace_ai_config.py`): `skimrace-v5-i1`, `skimrace-v3-i2`, `skimrace-v2-i4`,
  `skimrace-v3-general` (intensity 3 and any new intensity) — all tuned across 16/28/50 ms frames with the
  0.04 s contact step; each per-intensity file records its map fingerprint.
- **Difficulty**: `AIDifficulty` (Easy 1 / Medium 2 / Hard 3; default Medium), picked by the host on the
  Skim Race card, replicated in `LobbySnapshot.AIDifficulty`; `Resources/SkimRaceDifficulty.asset`
  (Easy: reaction 0.5 s, mistake chance 0.099; Medium: 0.25 s, 0.045; Hard: none).
- **Seating**: unchanged — `ServerPlayerVesselInitializerWithAI.GetBalancedDomain` (fewest pilots first);
  the host moves AI with **Add AI** on the launch panel.
- **Tools**: the offline simulator (`Tools/Build/skimrace_sim_harness/run.sh` — `eval`, `trace`, `tune`,
  `tuneall`, `handicap`, `fingerprint`; options `ph.Seats`, `ph.Team`, `ph.TeamRule`, `ph.PhysicsStep`,
  `dts=`, `SKIMRACE_RUNTIME=mono`), `skimrace_retune.py`, `skimrace_track_fingerprint.py`,
  the in-editor benchmark window (`FrogletTools > AI > Skim Race AI Benchmark`), the manual race recorder
  (`BenchmarkResults/SkimRaceAI/manual_I<n>_*.jsonl`, git-ignored), the gates
  `check_ai_no_state_writes.py`, `check_mathf_params_alloc.py`, `scan_perframe_allocations.py`.
- **Docs**: `Docs/SKIM_RACE_AI.md` (§3 architecture, §8.0e–i editor measurements and speed-ups, §10
  difficulty, §11 fingerprints/retune, §12 per-frame cost, §13 team races, §14 frame rate and editor cost),
  `Docs/UNITY_VERIFICATION_CHECKLIST.md` (the editor steps, top entries), the routing in
  `Docs/claude/FTUE_DIALOGUE_AI.md`, `SHADERS_AND_PERFORMANCE.md`, `MULTIPLAYER_AND_SOCIAL.md` and `CLAUDE.md`.

---

## 3. Findings, measured

Unless stated: the offline simulator (compiles the shipped driver), Hard, 2 AI seats, 28 ms frames ±50%,
the figure is each AI's median finish in seconds, 20 races per cell. Full tables and conditions are in
the cited sections.

### 3.1 Why the co-op pair won (§13)
- Skim Race finishes on a **team's summed crystals** against a target that does not grow with the team
  (`SkimRaceScoringRuleSO.IsObjectiveReached`); every player brings one crystal of their domain
  (`NetworkCrystalManager`), each walking the anchors on its own.
- With the default three teams, two humans on one team plus two AI puts **each AI alone on its own team** —
  each AI had to collect the whole target by itself (about twice as fast as each human to win).
- Two AI on one team both chased the **nearest** crystal (`SkimRaceTargetTracker`): the second to arrive
  swung round for a crystal that had just moved; pickups split 16/8, 5/19, 19/5… and the two flew through
  each other's trails.
- The difficulty was Medium (the default), which makes deliberate mistakes.

### 3.2 Team play (§13)

| Track | 1 AI alone | 2 AI on separate teams | 2-AI team, nearest rule (before) | 2-AI team, team plan (shipped) |
|---|---|---|---|---|
| I1 | 61.9 | 69.2 | 65.0 | **36.5** |
| I2 | 75.5 | 77.8 | 80.3 | **39.6** |
| I3 | 180.4 | 183.2 | 149.7 | **97.2** |
| I4 | 148.5 | 156.2 | 115.2 | **83.1** |

The team plan: the assignment of AI to crystals with the least total distance, kept until another is 15%
cheaper; AI only (a human teammate is never planned for); a lone AI is unchanged. A heading-aware cost was
tried and dropped (no gain).

### 3.3 Frame rate (§14.1–14.2)
Two things in the game depend on frame rate: contacts are tested on the **0.04 s fixed step**, and the
trail is laid **at most once per frame** (sparser at low fps). The policies were tuned at ~36 fps and raced
4–13% slower at 60–145 fps and 13–21% slower at 12 fps (Hard, I1: 77.0 / 70.7 / 67.9 / 66.6 / 77.0 s at
145 / 62 / 36 / 20 / 12 fps; I2: 84.6 / 82.6 / 75.8 / 78.6 / 91.4). Easy at I2 often overran the
simulator's cut (the game has none).

### 3.4 The AI's cost in the editor (§14.4)
- Unoptimized .NET as the stand-in for the editor's Debug optimization: I2, two AI together, **~11 ms in one
  frame of ten** (both re-planned on the same frames); I3's mass guard ~5 ms; optimized 2–3 ms at worst.
- The combined code (after the perf-branch merge) on **Mono in double precision — the editor's mode**:
  per AI per frame I1 0.16, I2 2.24, I3 1.13, I4 0.67 ms; I2 both AIs typical / worst-10% / worst-1%
  5.0 / **6.7** / 9.1 ms with no frame carrying two re-plans.
- The zero-code lever a tester has: editor **Code Optimization = Release** (about 5× on the planner).

### 3.5 The planner stagger — two branches, one kept (§8.0i, §14.4)
- This branch: a fixed grid of 1/TrackMpcHz with odd lanes offset half a period. At 16 ms frames the
  worst-10% frame fell 10.1 → 6.1 ms; at 28 ms ±50% it could not separate the seats (frames longer than
  half a period), and at ≤25 fps nothing on a grid can. (A first version that offset only the start was a
  no-op: the first shared frame re-locked the seats — found with a probe of the private schedule.)
- The perf branch: `SkimRaceReplanGate` — one re-plan claims a frame, the next seat waits one frame. It
  holds at every frame rate, so it was **kept at the merge and the grid retired**.

### 3.6 The retune across frame rates (§14.5)
Each policy re-fitted from its shipped numbers (`only=stated`), races spread over 16/28/50 ms frames with
the 0.04 s contact step, kept only if it beat the general policy on 20 fresh races; then shipped vs new at
120 / 62 / 36 / 20 / 12 fps:

| Track | Shipped → new | 120 | 62 | 36 | 20 | 12 fps |
|---|---|---|---|---|---|---|
| I1 | v4 → v5 (4 min) | 66.0 → 64.3 | 66.0 → 65.1 | 64.2 → 65.4 | 64.6 → 63.5 | 73.7 → **66.8** |
| I2 | v2 → v3 (22 min) | 82.1 → 80.6 | 79.9 → 79.3 | 78.6 → 78.3 | 78.0 → 79.3 | 88.0 → **84.3** |
| I4 | v1 → v2 (15 min) | 164.3 → **150.8** | 150.2 → 149.1 | 149.3 → 146.0 | 145.6 → 144.1 | 145.8 → 144.1 |
| I3 (general) | v2 → v3 (~2.5 h) | 188.5 → 184.6 | 185.3 → 183.4 | 184.5 → 182.1 | 185.0 → 181.0 | 193.6 → **185.7** |

- I1's one unfinished race at 120 fps was checked on 40 more: new 40/40 (median 73.9 s, 0.05 recoveries per
  race), shipped 39/40 (75.7 s, 0.55).
- Under the gate (the retune ran under the grid), new I2: 79.1 / 77.1 / 76.3 / 79.0 / **89.1** — faster at
  36–120 fps, level at 20, slower at 12 fps (each seat then re-plans every other frame).
- Easy and Medium on the new policies, 21 races over 16/28/50 ms: I1 Medium 73.9 / Easy 88.9 s; I2 Medium
  94.9 / Easy 121.5 s — the ladder of §10 holds.
- A failure mode the retune did not touch: ~1 in 100 I2 races strands one AI at 20–22 of 30 crystals in a
  recovery loop, shipped and new alike.

### 3.7 What the merges changed for the AI (§14.3 and the merge commits)
- bleeding-edge 10-06: nothing in how the Squirrel races on desktop (the skim-tick limit is audio; the race
  trail cap is MobileLow only; the Squirrel AI boost policy is disabled in Skim Race). One side effect:
  `c13425ba5` ("drift changes", aradia1) moved the editor's QualitySettings to Very High with vSync and 4×
  MSAA and cleared the per-platform defaults — the user chose to leave it.
- bleeding-edge 10-07: the pilot's objective refactor; Regatta's Squirrel AI flies the Skim Race pilot at
  rings; the team plan applies to crystals only (rings are each pilot's own).
- `perf/performance-optimization`: everything in §1's `55d9144fe` row; its own editor measurement (§8.0h)
  was 35 → 55 fps on I2 with two AI.
- `Ys-bleeding-edge`: the party layer only (no AI change).

### 3.8 The invite failure (ask 3)
The reporter's local copy was behind (10 push / 157 pull: the pre-fresh-start history, preserved on
`claude/confident-pascal-w76l2o-backup`), and mixed builds read `LobbySnapshot` out of step after it gained
`AIDifficulty` without a protocol bump. `ProtocolVersion` 8 → 9 makes a mismatched build refuse at the
connection request instead of failing silently (`Docs/claude/MULTIPLAYER_AND_SOCIAL.md`, the protocol rule).
The user rechecks networking on their own branch.

---

## 4. Decisions the user made

| Date | Decision |
|---|---|
| 10-05 | Steps 3–6 as recommended: difficulty picker; Easy/Medium = Hard plus mistakes; map fingerprints with a check script and one retune command; Profiler timers with no fixed budget; same-races speed-ups applied, flying-changing ones decided after the Unity check |
| 10-05 | Merge bleeding-edge, bold-fermi and Bug_Hunt; bump the protocol version; recheck networking on their other branch |
| 10-05 | **AI teammates split the crystals at every difficulty** |
| 10-06 | **Keep today's AI seating rule** (after the table of every 4-seat shape); set 2-vs-2 up by hand with Add AI |
| 10-06 | "Test first, then decide" whether Hard must fly faster or an Expert level is added; the co-op race had been on Medium (the default) |
| 10-06 | The report was that the **game** runs slow (not that the AI races badly); **retune across frame rates**; **leave** the QualitySettings change |
| 10-07 | Combine with `perf/performance-optimization`, take `Ys-bleeding-edge`, route the docs from `CLAUDE.md` |

---

## 5. Verification record

What ran, every time C# changed:
- **Real-Unity-reference compile** (`Tools/Build/unity_refcompile/run.sh`): player config **0 errors in
  project code** at every commit; editor config 0 errors in changed files — the same 4 old errors in untouched
  files each time (`CameraSettingsSOEditor.cs`, `ResourceDisplay.cs`, `UniversalStatsProviderEditor.cs` ×2).
  After the package bump, 25 lines the tool cannot verify (missing types in files whose multiplayer packages
  are not fetchable offline) — by its own classification, not project errors.
- **Gates**: using directives, enum member references, switch-label collisions, duplicate attributes,
  abstract members, console logging, conditional compilation, persistent-listener injection, AI
  no-state-writes, mathf-params-alloc, self-referential locals, `author_skimrace_ai_config.py --check`,
  `skimrace_track_fingerprint.py --check` / `--self-test`, the mode generators' `--check` after each merge.
- **Simulator identity**: every "same races" change proven line-for-line identical on 6 races per track
  (the timers, the track-window lookup, `ph.Team=0`, `ph.PhysicsStep=0`, no `dts=`); every behaviour change
  measured on fresh seeds against the previous build.
- **Offline tests**: `SkimRaceTeamAssignmentTests` 8/8 under an NUnit stand-in, with three deliberate breaks
  each caught by its test; the fingerprint golden values agree between C# and Python.
- **Mono** (6.8.0.105) was installed in the session for the editor-mode cost numbers.

What did NOT run: `/verify-unity` and anything in the Unity editor. Every change is filed in
`Docs/UNITY_VERIFICATION_CHECKLIST.md` (top entries, 2026-10-05 → 10-07).

---

## 6. Open items and next steps

1. **The editor owes every check** in the checklist's top entries — in particular: compile, the Skim Race
   tests (including the four gate tests), the Profiler showing one `SkimRace.Driver.TrackMpc` call per frame
   with two AI on I2, and hand-played races on Hard at I1–I4 **with their frame time**
   (`manual_I<n>_*.jsonl` records both).
2. **The human-vs-AI question is still open**: the user's plan is to race 2 humans vs a 2-AI team on Hard
   and then decide between making Hard faster (within the input-only rule) and adding an Expert level.
3. The gate at **12 fps** makes each seat re-plan every other frame (I2 84 → 89 s there); a refinement
   nobody has measured: let the gate stand down when the frame is longer than half the re-plan period.
4. **I3's mass guard** dominates its cost in dense traffic (editor-mode worst-10% 5.7 ms for two AI);
   untouched by either branch.
5. The **1-in-100 stranded I2 seat** (recovery loop) predates this work.
6. **Burst the planner rollouts** (the perf branch's lever 3) — the largest remaining AI cost win, not done.
7. The QualitySettings change (Very High + vSync as the editor default) was left as is; raise with its author
   if the editor's frame rate matters for testing.
8. Networking: the user retests on `Ys-bleeding-edge`; this branch carries its Phases 0–1 and the package bump.

---

## 7. Where to look

| Need | Where |
|---|---|
| The AI's design, numbers, commands | `Docs/SKIM_RACE_AI.md` (§3, §8, §10–14) |
| What the editor still has to verify | `Docs/UNITY_VERIFICATION_CHECKLIST.md`, top entries |
| Every AI in the project and its doc | `Docs/claude/FTUE_DIALOGUE_AI.md` § AI Opponent System |
| Performance record, instruments, gates | `Docs/claude/SHADERS_AND_PERFORMANCE.md` § Performance Standards → `Docs/PERFORMANCE_OPTIMIZATION.md` |
| Multiplayer routing (UGS calls, packages, protocol rule) | `Docs/claude/MULTIPLAYER_AND_SOCIAL.md` § Party docs table |
| Policy source of truth | `Tools/Build/author_skimrace_ai_config.py` (`--check`) |
| Re-tune after a map or code change | `python3 Tools/Build/skimrace_retune.py <I>`; general policy: `run.sh tuneall 1,2,3,4 4 16 sigma=0.15 final=20 only=stated … ph.PhysicsStep=0.04 dts=0.016,0.028,0.05` |
| Measure the AI's cost the editor's way | `SKIMRACE_RUNTIME=mono bash Tools/Build/skimrace_sim_harness/run.sh eval <I> 10 … ph.Seats=2 ph.PhysicsStep=0.04` |
| This session's commits | `git log --grep='session_01ULRwYUDYKSHmDzCCo9Xa3V'` |

The simulator's per-run outputs lived in the session's scratch space and are not kept; the numbers they
produced are in the sections cited above and in the commit messages.

---

## 8. How the work was done (the method, for the next session)

- **Simulator first.** Every AI question was put to the offline simulator before any game code moved: it
  compiles the shipped driver, so what it measures is what ships. Behaviour changes were A/B'd on the same
  seeds; "same races" changes were proven line-for-line identical.
- **A measurement per claim, a negative control per gate.** Tests were broken on purpose to prove they
  catch the break; the first stagger's no-op was found by probing the private schedule, not by trusting the
  diff.
- **Small steps, the user's options.** Each step ended with the numbers in plain language and 2–4 options
  with a recommendation; the user's picks are in §4.
- **Compile against real Unity references and run every gate before every commit**, and say in the
  commit what did not run (`/verify-unity`), filing the editor steps in the checklist.
- **Docs with the code.** Every step landed its section in `SKIM_RACE_AI.md`, its checklist entry and,
  at the end, its route from `CLAUDE.md`.
