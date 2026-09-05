# Switchback — Technical Documentation

> **Naming.** `GameModes.Switchback = 45` is the code/data/enum identity, and the player-facing
> `DisplayName` on `ArcadeGameSwitchback.asset` is **"Switchback"** too. A switchback is a
> hairpin on a mountain road, and the mode is a course of them — it also contains the platform
> fundamental it is built on, the **switch**.

## Overview

Switchback is the **Dolphin-only gate race**. A course of **randomly placed and randomly
oriented** switch rings is scattered through the cell, and every pilot flies the **same course in
order**: thread your next gate, or turn around and go back for it. The first **DOMAIN** whose
**lead runner** threads the last gate wins.

It is the third Dolphin mode and the one that asks the vessel for nothing but flying. Rampage
points its cone at a forest and The Bends points it at a pilot; here there is no target at all,
and the Dolphin's kit is the racing itself — **skim** to bank energy, **drift** to carve a corner
that its 110°/s turn rate could not otherwise make, **boost** down the straight that follows.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameSwitchback.unity` — cloned from
  `MinigameRampage.unity` (whose AI roster is already four Dolphins and whose spawn ring is
  already cell-relative), then pointed at the **Skim Race cell**
- **GameMode enum**: `GameModes.Switchback = 45`
- **Controller**: `SwitchbackController : MultiplayerDomainGamesController` — 1 round / 1 turn,
  `HasEndGame = false`, server winner detection in `OnTurnEndedCustom`, snapshot
  `SyncFinalScores_ClientRpc`; plus the course build, its replication, and the crossing loop
- **Scoring**: `SwitchbackScoringRule.asset` (`SwitchbackScoringRuleSO`) — metric
  `ScoringMetric.SwitchesThreaded` (**9**, new), golf-timed
- **Turn monitor**: `SwitchbackGateTurnMonitor` — resolves the gate count from
  `EndConditionOverridesSO.GetSwitchbackGateTarget()` (default **20**, FrogletTools ▸ Game Modes
  ▸ End Game Conditions — never a per-scene field), syncs via NetworkVariable →
  `GameDataSO.SwitchTargetCount`
- **Players**: **2–4** with AI backfill. `MinDomainsAllowed = 2` (a race needs a rival)
- **Vessels**: **Dolphin only** — the single `Vessels` entry drives all three platform clamps
- **Comeback**: `ScoreDifferenceSource.SwitchesThreaded`, rate **0.5** (a quarter-of-course
  deficit ≈ 2.5 element levels)
- **Config**: `_SO_Assets/Games/ArcadeGameSwitchback.asset`, registered in
  `GameLists/OrganicRematchGames.asset` and `ProgressionConfig.alwaysUnlockedModes`

## The three ideas

### 1. Ordered gates make one replicated int carry the whole race

Because a pilot may only ever thread their **next** gate, `IRoundStats.SwitchesThreaded` is
simultaneously

- the **score** (gates threaded),
- the **progress bar** (`target − remaining`),
- the **index of the ring to test this frame**, and
- the **token the server validates a report against**.

There is no per-pilot bitmask of visited gates, no per-gate state, and detection is **one segment
test per pilot per frame** rather than pilots × gates. `SwitchThreadScoring.Credit` is the whole
validation: `gateIndex != stats.SwitchesThreaded` rejects both a client claiming gate 19 from the
starting line and a duplicate report of a gate already paid.

### 2. A domain's progress is its LEAD RUNNER, not its sum

Every pilot flies the same course, so summing teammates would give a two-pilot domain twice the
course, cross the target at half the gates, and beat a one-pilot domain that had actually flown
further. Switchback is therefore the first mode to fold a domain by **max**.

That needed one platform change, and it was made as a **seam rather than four overrides**:
`ScoringRuleSO.DomainValue(GameDataSO, Domains)` is a new virtual defaulting to
`ScoringMetrics.SumByDomain` — byte-for-byte the old behaviour for every existing mode — and
`SwitchbackScoringRuleSO` overrides it to the new `ScoringMetrics.BestByDomain`. Five readers go
through it: `Remaining`, `ResolveWinner`, `ResolvePlacementOrder`, `DomainDelta`, and
`MultiplayerDomainGamesController.SyncDomainSumsRoutine` (the HUD's own domain boxes).

**Why all five and not just the end condition:** a mode that overrode only `IsObjectiveReached`
would win on the lead runner while the score row above it showed the team's sum, and the comeback
system would compute a deficit against a third quantity. A domain's score is read in five places
and they must never disagree.

The design consequence is deliberate: **a teammate never adds to your score.** What a teammate can
do is run interference — and the Dolphin's blast cone debuffs a rival pilot in every mode since
The Bends wired it, so the ammunition is already on the course (see *Crystals*, below).

### 3. Randomly oriented, and still flyable

The course is a deterministic constructive walk (`SwitchbackCourse`, pure, no Unity randomness,
no scene access, unit-tested offline). Two properties hold **by construction**, not by tuning:

| property | how it is guaranteed |
|---|---|
| **Turn cap** — no corner sharper than `MaxTurnDegrees` | The heading only advances when a gate is PLACED, and every proposal — including the one that steers away from the shell wall — is clamped to the cap. When there is no legal escape the walk **backtracks** rather than bending the rule. |
| **Presentation cap** — no gate stands edge-on to the line you arrive on | A gate faces the **flow bisector** of its corner, which is already half the turn angle off each leg. The jitter that makes it "randomly oriented" is spent from what is LEFT of the cap: `presentation ≤ halfTurn + jitter ≤ MaxPresentDegrees`. |

The tempting shortcut for the first one is to let the heading rotate between failed attempts.
That is wrong: two 55° rotations compose into a 110° corner between two **placed** gates.

`SwitchbackCourseTests` sweeps **400 seeds × 4 intensities** and asserts the caps hold, every
course generates, every gate is inside the shell, gate 1 is on the pole, no two mouths come
within a ring diameter, and **every corner clears the Dolphin's turning circle at BOOST** — the
Dubins condition `leg > 2R·sin(turn)` at `R = 180.7u` (347 u/s over 110°/s), the state in which a
racer is least able to correct.

## The start is provably fair

Pilots spawn on an **equatorial ring** around the cell (`CellSpawnFormation.EquatorialRing`, which
the scene authors), and **gate 1 sits on that ring's POLE**. Every pilot is therefore exactly
`sqrt(spawnRadius² + d²)` from it. Under the donor's Symmetric (tetrahedral) formation no such
point exists and whoever spawned nearest gate 1 would start the race ahead.

`spawnDistanceOutsideNucleus` comes down from Rampage's **500** to **150**, because this cell's
nucleus is the full-size `Nucleus.prefab` (391.9u) rather than Rampage's `HalfNucleus` (196u) —
at 500 the ring would sit at 892u, three quarters of the way to the membrane.

## The course travels; the seed does not

The server generates the course and **broadcasts the geometry** (six floats per gate, interleaved
into one `float[]`; 20 gates is 480 bytes). A late-joining client pulls it with
`RequestCourse_ServerRpc`, mirroring `MultiplayerMiniGameControllerBase`'s config pull.

Generating locally from a shared seed would have worked — the generator is deterministic on
purpose — but it would rest on `Mathf.Sin`/`Acos` agreeing to the last bit across Mono and IL2CPP,
and a single flipped branch inside the walk yields a **completely different course** rather than a
slightly different one. The seed is kept only so a reported course can be reproduced.

## Detection: owner-detects / server-records

The platform's fourth use of the pattern, after `ReportFaunaKill`, `ReportCombatHit` and
`ReportEnvironmentPrismDestroyed`.

```
SwitchbackController.Update()                       [EVERY peer]
  └─ for each player where IPlayer.IsNetworkOwner   ← host: its human + ALL AI
     │                                                client: its own human only
     ├─ sample position, reject an implausible single-frame step
     ├─ test the segment against THAT PILOT'S next ring only
     └─ crossed?
         ├─ IsServer  → SwitchThreadScoring.Credit(stats, index)     [direct]
         └─ else      → Player.ReportSwitchThreaded_ServerRpc(index) [validated server-side]
```

**`IsNetworkOwner`, never `IsLocalUser`** — the host owns every AI Player, and the narrower test
would silently never advance one. That is the gate The Bends records for combat hits, reached here
from the other direction.

**Segment crossing, never a trigger volume**: a boosted Dolphin covers ~14 units per physics tick,
so a ring can be flown through between two samples. The plane test cannot miss one. It is
direction-agnostic, like `ScarabSwitch`'s — a gate threaded backwards is still threaded, which is
the honest reading for a race, since you still had to fly there.

**The optimistic counter** (`PilotRun.Optimistic`) exists because on a client the authoritative
count lags by a round trip, during which a boosted pilot can reach the next gate. It reconciles
both ways: it adopts the server's value when that catches up, and falls **back** to it when a
report goes unacknowledged for `reportResyncSeconds`, so a dropped or rejected report cannot
strand a pilot testing a gate they will never be credited for.

## The gates

A gate is a **switch** (CLAUDE.md, "Switch"): a ring you thread, drawn in the prism shader at the
radius its own crossing test uses, via the one builder every switch in the game comes from
(`ToyFactory.AddSwitchRing`).

- **Neutral, painted `Domains.Blue`.** Threading one hands nobody a domain, so the reserved
  domain colours stay with the switches that do. No `ToySwitchSignal.Domain` appears anywhere in
  this mode, so `ToySwitchVocabularyTests` has nothing to allow-list.
- **A marker, not mass.** One renderer, **zero colliders**, a shared 144-triangle mesh. A ring of
  prisms would be ~8 prisms and 8 colliders each, which at 20 gates is 160 colliders against a
  cell budget already running 3–4k. Nothing here is conserved mass and nothing is removed by a
  clock: a gate stands for the whole match and comes down with the course.
- **Continuity of existence holds at both ends.** The ring blooms from zero
  (`ToyFactory.ScaleInFromZero`) and withers on teardown (`ScaleOutAndDestroy`). Detection is live
  at the **full** mouth from frame one and only the drawing grows into it — a ring drawn *smaller*
  than its trigger is the legal direction, because a crossing still always fires; drawing one
  *larger* would be the lie the switch law forbids.

**Every gate looks the same, on purpose.** Which ring is *yours* next is a per-pilot fact, and the
platform already has a per-viewer answer: the **objective arrow**
(`SwitchbackObjectiveProvider`). Repainting the next gate in the pilot's domain colour was the
obvious alternative and is wrong twice — it spends the reserved domain colour on something that
grants no domain, and it makes two pilots flying side by side see different worlds.

## Intensity is the COURSE, not the arena

One cell at every level. What climbs is how hard the gates are to fly:

| intensity | ring radius | leg length | max corner | axis jitter | presentation cap |
|---|---|---|---|---|---|
| 1 | 72 | 420–680 | 45° | 30° | 50° |
| 2 | 60 | 400–650 | 50° | 40° | 55° |
| 3 | 50 | 380–620 | 55° | 50° | 60° |
| 4 | 42 | 360–580 | 60° | 60° | 65° |

Gate **count** is deliberately constant, because it is the end-game target and is authored in one
place — so a match is the same length at every level and the four are comparable. Same reasoning as
Rampage, where the forest is identical at all four and only the pressure changes.

The ring band is anchored to the shipped fly-through vocabulary: the Scarab's placed switch is 24u,
Astro League's goal mouth 62u, Scarab Scramble's hoops 60/54/48/42. A racer arrives far faster than
a ball, so level 1 opens wider than any of them and level 4 lands on Scramble's tightest.

`MinStep` never drops below **360u**, which is the AI's approach-run floor (`2R − c` at boost) —
below it an AI would peel away and re-attack between every pair of gates.

## AI

`ArmRacers` installs a per-pilot `AIPilot.SetExternalTargetProvider` at
`OnCountdownTimerEnded` (server only). Each AI is pointed at its own next gate as **two
waypoints**:

- far out → a point **behind** the ring on its own axis, which lines the approach up with the mouth;
- inside `aiCommitDistance` → a point **beyond** it, which flies the pilot through.

`AIPilot` has no arrive-and-stop behaviour — it steers at its target forever and passes through on
arrival — so handing it the ring's centre produces a pilot orbiting the hoop, the defect both
PeelTheCage and Dog Fight record. Which side is "behind" is **latched** when the gate changes,
not recomputed: a pilot that drifts just past the plane without threading would otherwise see the
sides swap and swing away (Dog Fight's break-off lesson).

Switchback is **not** in `ServerPlayerVesselInitializerWithAI`'s seek-players set. Installing the
steering hook is correct here — unlike Rampage and Salvo, whose objective IS a crystal and whose
AI must keep the platform's crystal seeking.

## Crystals, and the interference they buy

The mode authors no crystal behaviour. Two shipped systems put some on the course anyway, and both
are kept deliberately:

1. The cloned scene keeps the donor's `NetworkCrystalManager`, so a handful of omni crystals
   respawn inside the nucleus.
2. **The Dolphin seeds its own.** `DeployTeamCrystalActionExecutor` has no game-mode gate: the
   local human's Dolphin plants one every 30s in the cytoplasm band, up to 8 live, in *any* scene.

Below Mass 5 those are free-for-all omni crystals, so a rival can take one and fire the blast
cone — which since The Bends debuffs every element of a pilot it engulfs, for four seconds. That is
the mode's interference layer and it cost zero new code. It is also why a teammate is worth having
even though they cannot add to the score.

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.switchbackGateTarget`, 0 = default **20**). The same getter is read
twice on purpose — by the turn monitor for the target and by the controller for how many gates to
lay — so the course a pilot flies and the number their goal row counts to are one authority and
cannot drift.

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameSwitchback.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/SwitchbackScoringRule.asset` |
| Scene | `_Scenes/Multiplayer Scenes/MinigameSwitchback.unity` (in `EditorBuildSettings`) |
| End conditions | `Assets/Resources/EndConditionOverrides.asset` (`switchbackGateTarget`) |
| Goal-stack icon + label | `Assets/Resources/ObjectiveIconSet.asset` metric 9, "Thread switches" |
| Launch-panel objective icon | `Assets/Resources/ModeControlsLibrary.asset` metric 9 |
| Objective glyph | `_Graphics/UI/Objectives/objective_switches_threaded.png` |
| Cell / spawn profile | The Skim Race cell's assets, referenced verbatim — **not** forked |

Every Switchback-owned asset is authored by `Tools/Build/author_switchback_assets.py`
(deterministic GUIDs, idempotent, validates before writing). **Re-tune there and re-run** rather
than hand-editing YAML. The glyph is authored by `Tools/Build/author_objective_icons.py`, which
mints the same deterministic GUID the switchback generator writes into the catalogue — and the
switchback generator asserts the two match.

## Shared-code touchpoints (added for this mode)

| Site | Change |
|---|---|
| `GameModes` | `Switchback = 45` |
| `EnumIntegrityTests` | member-count tripwire 43 → 44 |
| `ScoringMetric` | `SwitchesThreaded = 9` |
| `IRoundStats` / `RoundStats` | the stat, its event, its Server-write NetworkVariable, its mirror, its `Cleanup()` |
| `ScoringMetrics` | **`BestByDomain`** — the new max fold |
| `ScoringRuleSO` | **`DomainValue`** virtual (default = the old sum) + its five readers |
| `MultiplayerDomainGamesController` | HUD domain boxes routed through `DomainValue` |
| `GameDataSO` | `SwitchTargetCount` + both resets |
| `Player` | `ReportSwitchThreaded_ServerRpc` |
| `ElementalComebackSystem` | `SwitchesThreaded` source (max-folded), default for the mode, direction |
| `MiniGameHUD` | Switchback → `SwitchbackObjectiveProvider` |
| `EndConditionOverridesSO` (+ window + asset) | `switchbackGateTarget` live/build/getter, default 20 |
| `author_objective_icons.py` | the metric-9 glyph |

No new impact effects, no vessel edits, no cell edits, no new ability. The mode is a composition
of shipped systems plus one course generator.

## In-editor verification (authored headless — NOT yet run)

The C# was compiled headless against Unity-type stubs and the course generator was **executed**
over 400 seeds × 4 intensities (all contracts hold); nothing below has been run in the editor.

1. **Open** `MinigameSwitchback.unity`. Every script reference resolves; the controller's inspector
   shows `rule` = SwitchbackScoringRule, `cellData` = Runtime Cell Data, the course/AI/detection
   fields at their authored values; the Cell shows ONE config (Skim Race) on Random; the spawner
   shows `spawnFormation` = EquatorialRing, `spawnDistanceOutsideNucleus` = 150.
2. **THE COURSE EXISTS — the load-bearing check.** Enter play. Twenty blue rings bloom in,
   scattered between the nucleus and the membrane, each facing a different way. Gate 1 is directly
   "above" the cell centre and every pilot is the same distance from it.
3. **THREADING SCORES.** Fly through gate 1: the goal row ticks 1/20 and the objective arrow moves
   to gate 2. Fly through gate 3 *without* threading 2 — nothing happens. Go back through 2, then
   3: both count.
4. **Backwards counts.** Thread a gate from the far side: it still counts.
5. **Missing the mouth does not count.** Fly past a ring just outside its rim: no tick.
6. **Client detection.** In a real lobby, the CLIENT threads gates and its own count rises, on both
   machines. Reverse it: the HOST threads, the client's scoreboard follows.
7. **The lead-runner fold — the headline check.** Two pilots on one domain. Pilot A threads 5
   gates, pilot B threads 0. The domain's HUD box reads **5**, not 5. Then B threads 3: the box
   still reads 5 (A's), not 8. The goal row shows 5/20.
8. **Win + scoreboard.** First domain to put ONE pilot through gate 20 ends the turn; winners show
   "VICTORY" + course time, losers "N Gates Left"; each player's secondary line is their OWN gate
   count. Teammates share the win. Replay (scene reload) resets everything to 0/20.
9. **AI flies the course.** AI Dolphins thread gates in order rather than orbiting a ring. Watch
   one approach: it should line up along the gate's axis, pass through, and turn for the next.
   Their domain's score climbs.
10. **Comeback.** Let one domain fall ~5 gates behind: the trailing pilots' element flowers fill
    ~2 levels.
11. **Intensity.** Compare 1 and 4: the rings are visibly smaller and the course visibly twistier
    at 4, and the gate count is the same 20 at both.
12. **Interference.** Collect a seeded omni crystal and put the blast cone on a rival: they take
    the all-element debuff for 4s. It does not change either pilot's gate count.
13. **Regression — Rampage unchanged.** Launch Rampage: cactus forest, four intensity configs,
    prisms-destroyed scoring, Symmetric spawn. The two modes share a donor scene, not assets.

## Known limitations / follow-ups

- **20 gates is unmeasured.** Chosen from the arithmetic (≈9.4k units of course; 2–3 minutes at
  realistic Dolphin speeds), not from a playtest. It is one editor field.
- **No toasts.** No `GameToastConfigSO`, so no "GATE 12/20" or lead-change announcement. The
  shared config still covers join/ready/disconnect. Rampage, Dog Fight, The Bends and Salvo all
  ship this way; the enum's next free block is 70+.
- **No mode preview.** No `ModePreview_Switchback.asset`, so the arcade card falls back to its
  `CardBackground` and hides Test Flight — the same state Salvo ships in. The preview would be
  worth authoring here, since the course is the thing worth looking at.
- **No laps.** The course is one pass. A looping course (last gate leads back to the first) would
  let the target be gates × laps, as Skim Race does.
- **The gate rings are not in the Codex.** They are neither flora, fauna, crystal nor toy, so no
  kingdom currently fits them.
- **The Skim Race cell's `PhaseThresholds` are count-based** (600/480, 2000/1600) with no volume
  keys, inherited from a mode whose vessel lays a different trail. A Dolphin trail here has not
  been measured against that ladder; if fauna never hunt, or the cell pins at Frenzy, that is
  where to look.
