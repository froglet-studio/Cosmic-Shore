# Switchback — Technical Documentation

> **Naming.** `GameModes.Switchback = 45` is the code/data/enum identity, and the player-facing
> `DisplayName` on `ArcadeGameSwitchback.asset` is **"Switchback"** too. A switchback is a
> hairpin on a mountain road, and the mode is a course of them — it also contains the platform
> fundamental it is built on, the **switch**.

## Overview

> **Current state (2026-11-01).** Every intensity is **Blast Run** ("Switchback is Blast Run" below): prism block
> fields, clock-driven movers, 100 fps budgets, fixed seeds and the seed picker; flora/fauna, worlds, sweepers, jaws and
> hunter packs are removed. The AI is the blast planner + prism sense + per-frame emergency sweep (below), watched in
> the in-game AI Diagnose panel. The overview that follows describes the original random-gate race; its course,
> scoring and replication rules still hold, but read the Blast Run sections for the layout.

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
- **Platform**: `GateRaceController` (`Controller/Arcade/Racing/`) — the shared gate-race base
  this mode was extracted into when Headlong landed: course broadcast, ring spawning, crossing
  detection, the owner-detects/server-records round trip, AI steering and the final-score
  snapshot all live there, and a subclass supplies its NAME, its COURSE and whether that course
  WRAPS. Switchback is the OPEN-chain case (`LapsPerRace = 1`); Headlong is the lapped one.
  `SwitchThreadScoring`, `RaceGateRing`, `RaceGateTurnMonitor` and `RaceGateObjectiveProvider`
  moved with it — same files, same guids, renamed off the mode.
- **Controller**: `SwitchbackController : GateRaceController` — 1 round / 1 turn,
  `HasEndGame = false`, server winner detection in `OnTurnEndedCustom`, snapshot
  `SyncFinalScores_ClientRpc`; plus the course build, its replication, and the crossing loop
- **Scoring**: `SwitchbackScoringRule.asset` (`GateRaceScoringRuleSO`, shared with
  Headlong) — metric
  `ScoringMetric.SwitchesThreaded` (**9**, new), golf-timed
- **Turn monitor**: `RaceGateTurnMonitor` — resolves the gate count from
  `EndConditionOverridesSO.GetSwitchbackGateTarget()` (default **20**, FrogletTools ▸ Game Modes
  ▸ End Game Conditions — never a per-scene field), syncs via NetworkVariable →
  `GameDataSO.SwitchTargetCount`
- **Players**: **2–4** with AI backfill. `MinDomainsAllowed = 2` (a race needs a rival)
- **Vessels**: **Dolphin only** — the single `Vessels` entry drives all three platform clamps
- **Comeback**: the rule's `DomainValue` (`SwitchesThreaded`), rate **0.5** (a quarter-of-course
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
`GateRaceScoringRuleSO` overrides it to the new `ScoringMetrics.BestByDomain`. Five readers go
through it: `Remaining`, `ResolveWinner`, `ResolvePlacementOrder`, `DomainDelta`, and
`MultiplayerDomainGamesController.SyncDomainSumsRoutine` (the HUD's own domain boxes).

**Why all five and not just the end condition:** a mode that overrode only `IsObjectiveReached`
would win on the lead runner while the score row above it showed the team's sum, and the comeback
system would compute a deficit against a third quantity. A domain's score is read in five places
and they must never disagree.

**Its mirror image is a second seam, because a PILOT's own readouts must not show the domain
fold.** With the domain folded by its best pilot, a trailing teammate's goal row would read the
ace's "12/20" while their objective arrow pointed at gate 4, and their scoreboard row would sit
3 gates flown beside 8 left of a 20-gate course. `ScoringRuleSO.RemainingForPlayer(GameDataSO,
IRoundStats)` is that seam, and it defaults to the pilot's DOMAIN remaining — which is the right
answer in every other mode, where a pilot's objective genuinely IS the team's shared pile.
Switchback overrides it; the turn monitor's display channel, the scoreboard's "N Gates Left" and
the defeat reveal read it. `Remaining` stays domain-folded and is what the end condition, the
loser sentinel and `DomainDelta` read — those are questions about the race, not about one pilot.

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
racer is least able to correct. The hairpins of intensity 2-4 are the exception by design: they
are checked against their own band instead, because a drift is how they are flown.

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
(`RaceGateObjectiveProvider`). Repainting the next gate in the pilot's domain colour was the
obvious alternative and is wrong twice — it spends the reserved domain colour on something that
grants no domain, and it makes two pilots flying side by side see different worlds.

## Intensity is the COURSE, not the arena

One cell at every level. Since 2026-10-26 (Yash, `Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md` §3.3) each level
adds ONE demand on the Dolphin's three verbs - drift, skim, discharge - instead of only shrinking the mouth:

| intensity | name | mouth radius (⌀) | legs | ordinary corner | hairpins | course features (2026-10-26; replaced by Blast Run 2026-10-29) |
|---|---|---|---|---|---|---|
| 1 | Learn to fly | 72 (144) | 420–620 | ≤ 40° | none | none |
| 2 | Carve | 48 (96) | 400–650 | ≤ 45° | every 2nd corner, 90–150° | none |
| 3 | Charge economy | 32 (64) | 460–720 | ≤ 50° | every 3rd corner, 90–130° | **skim walls** on every 2nd leg |
| 4 | Gauntlet | 20 (40) | 360–520 | ≤ 55° | every 2nd corner, 110–150° | **danger prisms** just off every leg's line |

Axis jitter climbs 15 / 25 / 30 / 40° and the presentation cap 40 / 50 / 55 / 60°. The numbers live in
`SwitchbackCourseSettings.ForIntensity` and `SwitchbackCourseSettings.RingRadiusForIntensity`.

- **I1 teaches steering** through gates. 30° corners were tried first and failed all 400 seeds: a course that
  gentle cannot stay inside the 480–1080 shell. 40° with 420–620 u legs generates every seed.
- **I2 teaches the drift.** A hairpin (`SwitchbackCourse.IsHairpinCorner`) is a corner the Dolphin's 110°/s cannot
  make at boost; only a drift carves it. The hairpin is drawn inside its band rather than steered back off the shell
  wall, and its gate faces the leg you arrive on (a hairpin's flow bisector would stand the gate edge-on to it).
  Hairpins are exempt from the boost-circle test, which is the point of them.
> **2026-10-29:** the skim walls and danger prisms below are RETIRED. The course ladder (mouths, legs, corners,
> hairpins) is kept unchanged; what sits on it at every intensity is now Blast Run's doors and block fields (see
> "Switchback is Blast Run" and "Blast Run" below).

- **I3 teaches charge economy**: a wall of plain prisms 26 u beside the line over the middle half of every second
  leg - skim along it to bank charge, spend it down the straight after.
- **I4 is risk against reward**: three danger prisms 12 u off the straight line at the middle of every leg, on
  alternating sides. The shortest line brushes them; a wider one is safe and slower.

The mouths keep the same order without the old cliff (the geometric ladder went 72 → 28 → 11 → 4.3, and the I4 mouth
was 1.5 hull radii - no room to drift through it). The tightest is now 7 hull radii; `SwitchbackCourseTests` asserts it
stays between 5 and 10 and that no step tightens by more than 0.6×.

**The course features are a pure function of the course.** (Historical: now `SwitchbackBlastRun.Layout(course,
intensity, centre, clearRadius)`.) `SwitchbackFeatures.Layout(course, intensity, centre, clearRadius)` returned the prisms, `SpawnableSwitchbackFeatures` (prefab `_Prefabs/Spawnables/SpawnableSwitchbackFeatures`,
`featuresPrefab` on the controller) lays them in `OnCourseRaised` on every peer, from the course every peer received -
Breakwater's stations are the same shape. Everything is `Domains.Blue`. The course is random, so another leg or gate can
pass anywhere near a feature: a prism is dropped when it would sit within 30 u of a gate's rim, closer to ANY leg's line
than 0.9 of its own offset, or inside the nucleus plus 30 u. Over 400 seeds: I3 lays 165–219 wall prisms (mean 190),
I4 48–54 danger prisms. `SwitchbackFeaturesTests` held all of it (deleted 2026-10-29 with the walls).

Gate **count** is deliberately constant, because it is the end-game target and is authored in one
place — so a match is the same length at every level and the four are comparable. Same reasoning as
Rampage, where the forest is identical at all four and only the pressure changes.

**The crystal supply is flat too, and that is the half of this rule that is easy to lose.** The
scene is cloned from Rampage, where intensity IS crystal scarcity (`crystalCountMode:
IntensityScaled`, 2×players down to exactly 1). Inheriting that ladder would have made intensity
mean two contradictory things at once, and would have made this mode's whole interference layer
~8× rarer at exactly the level the gates get tighter. The clone is set to
`PlayerCountPlusExtra` at **+1** and the four-row ladder is flattened to the same answer rather
than deleted, so a future flip back to `IntensityScaled` still says "the arena does not change".
The generator asserts both.

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
Cleave and Dog Fight record. Which side is "behind" is **latched** when the gate changes,
not recomputed: a pilot that drifts just past the plane without threading would otherwise see the
sides swap and swing away (Dog Fight's break-off lesson).

**Since 2026-10-26 the AI flies Regatta's gate steering**: see "The Regatta steering, on since 2026-10-26" below.

Switchback is **not** in `ServerPlayerVesselInitializerWithAI`'s seek-players set. Installing the
steering hook is correct here — unlike Rampage and Salvo, whose objective IS a crystal and whose
AI must keep the platform's crystal seeking.

> **Retired 2026-10-26 (Yash):** *"while playing any racing game mode, AI should not go for crystals. It should only
> focus on waypoints. If the crystals are the waypoints, then go for crystals."* No gate race detours for a crystal now:
> `GateRaceController.AIDetoursForCrystals` is false in code, so the scene's `aiCrystalDetourSlack` is read only by a
> mode that opts in (none does). Test: `RaceAIWaypointsOnlyTests`. The paragraph below is the history.

**But the hook still disarms the blast, so the AI gets a crystal detour.** Installing an external
target provider *replaces* `AIPilot`'s own crystal seeking outright — the trap The Bends records —
and a crystal is the Dolphin's only blast trigger, so a racing AI would never once fire the
interference layer below and it would be human-only in every match with backfilled bots. The
detour is bounded by a **distance budget, never a radius**: a managed crystal is taken only when
flying through it costs less than `aiCrystalDetourSlack` (220u) of extra distance between here and
the next gate, so it can never pull a pilot off the course. It is re-tested every frame rather
than latched — unlike the approach side, where a latch is what stops an oscillation — because "on
the way" is monotone in progress and stops holding the instant the pilot is past the crystal, so
the detour cannot become an orbit. The registry scan is throttled to `aiCrystalScanSeconds`
(0.5s) and filters to `Crystal.CrystalManager != null`, the same filter `RampageObjectiveProvider`
records: `Crystal.Active` also holds every lifeform heart the food web drops.

### The Regatta steering, on since 2026-10-26 (the Dolphin's training minigame)

`SwitchbackController` overrides `AICommitFromApproachPoint` to true (the scene switch stays as it was). In people's
races the AI Dolphins threaded 0-2 of 20 rings, and two things caused it:
- **The approach point sat outside the commit radius.** On any leg over 650 u the point behind the ring is the full
  `aiApproachLead` (300 u) out, beyond `aiCommitDistance` (260 u). A pilot reaching it was still "approaching" and
  steered at the point it sat on. The Dolphin's 35 u turning circle kept it 265-335 u out, so it never committed: it
  circled gate 0 for the whole race. With the switch on, `GateRaceController.ApproachLeadFor` keeps the approach point
  at most 0.8 of the commit radius.
- **A drift committed off-axis missed the mouth.** The far-side point on the ring's axis is off the line from where the
  Dolphin commits, and a commit drift locks its course on it. The switch puts the through point on the line from the
  pilot through the ring's CENTRE, and re-attacks at once after a missed mouth (`Arcade/REGATTA.md` §6, 2026-10-17).

Offline on the six recorded courses (`Tools/Build/switchback_dolphin_sim.py`): 0 rings on every one before, 20 of 20
on every one after. The Dolphin's charge drive in Switchback uses numbers learned from Yash's races
(`DolphinAIBoostPolicy_Switchback`, `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`); the plan is
`Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md`. Garrett's other gate races keep their scene switch.

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
lay.

**The COURSE is the authority, though, not the override.** A shell too tight for the authored gate
count makes generation back off — it halves the ask until the walk succeeds, floor 2 — and a
target naming a gate that does not exist is unreachable, which is a match that cannot end. So the
monitor publishes `SwitchbackController.AuthoritativeGateCount` (the course's own length) and
falls back to the override only before the course exists, warning when the two differ. Both knobs
that can cause the shortfall are authorable in the shipped editor: that window's gate target, and
this component's shell fields. Generation failing at 2 gates is the one case left, and it logs an
error with the numbers that have to change rather than hanging the match.

**And the connecting panel holds until the course lands.** This mode lays no prisms, so the
arena-ready gate has nothing to observe and would release the moment it opened — releasing a pilot
who then flies gate 1 (which sits straight ahead of every spawn point) and is credited nothing,
permanently, with nothing on screen to say why. `OnNetworkSpawn` opens a
`PrismTrailBuilder.BeginArenaBuild()` bracket that `ApplyCourse` closes, the same one-line shape
SkimRace uses through its seed wait; despawn and a failed generation close it too, so it can never
wedge on a build that will not happen.

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
| Course features (Blast Run doors and block fields) | `_Prefabs/Spawnables/SpawnableSwitchbackFeatures.prefab` |
| The AI's boost numbers in this mode | `Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset` (written by `learn_drives_from_flights.py --mode Switchback`) |

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
| `MiniGameHUD` | Switchback and Headlong → `RaceGateObjectiveProvider` |
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
11. **Intensity (the 2026-10-26 ladder).** I1: wide rings, gentle corners, nothing else. I2: every
    second corner is a hairpin you need a drift for. Every level also carries Blast Run's doors and block
    fields (since 2026-10-29; see "Blast Run"). The gate count is 20 at every level, and the console is silent (a
    missing `featuresPrefab` logs `[Switchback] No features prefab assigned`).
16. **The AI Dolphins race.** At I2 with two Medium AI Dolphins, they thread most of the 20 rings
    (they threaded 0–2 before the steering fix) and drift through the hairpins.
12. **Interference.** Collect a seeded omni crystal and put the blast cone on a rival: they take
    the all-element debuff for 4s. It does not change either pilot's gate count.
13. **Regression — Rampage unchanged.** Launch Rampage: cactus forest, four intensity configs,
    prisms-destroyed scoring, Symmetric spawn. The two modes share a donor scene, not assets.
14. **The next gate is LIME, and only yours.** Your next ring reads lime green while every other
    ring reads neutral blue; thread it and the lime moves to the following ring. In a real lobby,
    confirm the two pilots see the lime on DIFFERENT rings when their counts differ — the signal is
    local and never replicated, so a shared lime would mean it is being set on the wrong side.
15. **The arcade shows all thirteen cards.** Open the arcade with **no favourites set**. Scroll to
    the bottom: a fourth row exists, every card is fully drawn, the scroll does not spring back,
    and the last card OPENS its launch panel. Then favourite a mode and repeat — the card that
    moves into last place must also open. The console must be silent: any
    `ArcadeExploreView - the … card sits N units past …` error means the fit did not settle.

## Switchback is Blast Run (2026-10-29)

Yash, 2026-10-29: "Keep Blast Run, remove all other intensity types. We will go with Blast Run." Every Switchback
match at I1-I4 - Arcade, the weekly challenge, the tutorial quest funnel, a replay, every guest - flies Blast Run (below).
There is no fun-type picker and no `SwitchbackFunType` enum any more.

**Retired** (2026-10-28 to 2026-10-29, a test surface): the CLASSIC (`Baseline`, the shipped I3 skim walls / I4 danger
prisms), LEVEL SHAPES (`Plan1_LevelShapes`: corner patterns, needle rings), LIVE FEATURES (`Plan2_LiveFeatures`: charge
ribbons, shortcut rings, moving danger clusters), PAR & MEDALS (`Plan3_ParMedals`: `SwitchbackPar`, split/medal toasts,
personal bests) and COMBINED fun types, with their code (`SwitchbackLiveFeatures`, `SwitchbackPar`, the Plan 1 course
fields, `SwitchbackFeatures.Layout`'s walls/danger, `SwitchbackFunTypeTests`, `SwitchbackFeaturesTests`) and the generic
`GateRaceController` hooks only they used (`PrepareCourseSource`, `CourseVariant` / `ReceivedCourseVariant`,
`ThreadsAlternate`). The Classic course LADDER stays: Blast Run's course is it, bit-identical.

**How it travels.** The server builds the course (`SwitchbackCourseSource`, `SwitchbackCourseSettings.ForIntensity(i)`)
and `SyncCourse_ClientRpc` carries every gate; each peer then lays the same Blast Run layout from it
(`SwitchbackController.OnCourseRaised`). The only Switchback launch-panel control left is `SEED: RANDOM / FIXED`
(`ArcadeGameConfigureModal.SwitchbackSeedPicker.cs`, host only, PlayerPrefs `Switchback.FixedSeed`). A weekly challenge
and the quest funnel hide it and fly a random seed. Stale PlayerPrefs from the picker (`Switchback.FunType`,
`Switchback.PB.<type>.I<n>`) are deleted the first time the panel loads.

**Every run logs one line** (all builds, `CSDebug.Log`):
`[Switchback] RUN blastRun intensity=... seed=... time=... gates=... prismsDestroyed=... stations=... keys=... movers=...`
(a client's line has `seed=0`: the seed is not broadcast; the course is). No par, medal or personal best.

**Flight recordings** name the family: a Switchback flight has `"funType":"BlastRun"`; one without the field flew the
retired Classic ladder, and `learn_drives_from_flights.py` (`SHIPPED_FAMILY = {"Switchback": "BlastRun"}`) learns the
Dolphin's Switchback drives from Blast Run races only.

## Blast Run (2026-10-29) - NOT editor-verified

Yash, 2026-10-29: obstacles that disturb the race and hurt the Dolphin, and environmental mass the Dolphin has to BLAST
through to reach its rings - "like the Sparrow's time game, just in the Dolphin way". Revised the same day: "3D grids
of prisms with movable area that are challenging, cubes, cuboids, spheres, big and small objects, remove flora and
faunas for now ... keep the things that are easy to sync in multiplayer ... target 100fps". So the Garden / Maze /
Membrane / Menger worlds, the sweepers, the snap-trap jaws and the hunter packs (`BlastRunHunters`, its
`NetworkList`, toasts and tests) are GONE; what remains is block fields whose only motion is a pure function of the
shared clock. The course is the Classic ladder unchanged (`SwitchbackCourseSettings.ForIntensity(i)`); what changes is what sits on it.

**A blocked station** (`SwitchbackBlastRun.IsBlocked`, `Layout`): on the leg INTO a blocked gate -

1. a **key crystal** on the leg line, `KeyLeadFor(leg)` = 0.62 x the leg clamped to 230-400 u upstream of the ring
   (inside the explosion's 400 u minimum length, so a blast fired at the key reaches the door). Keys are ordinary
   `Domains.Blue` crystals (any domain may take them) laid by the server's `CrystalManager.SetCourseKeys`; they
   replace the passive crystal field for the match (`RetireCrystalsBeyondSlots`), and `ClearCourseKeys` runs at
   despawn. A key's explosion core is 1.25 x (`BlastRunKeyCrystals.KeyCoreMultiplier`, matched within 4 u of the
   explosion origin in `VesselExplosionByCrystalEffectSO` - CODE only; the shared
   `DolphinVesselExplosionByCrystalEffect` asset is untouched, so every other crystal is unchanged).
2. a **door**: `BreakwaterStationBuilder.BuildDoor` - danger rakes round an always-open 18 u eye (`PortFor(i)`),
   8 u upstream of the ring plane; I4 adds a second door 8 u downstream.
3. a **block field** (`BlastRunWorld.Blocks` family, `EmitField`) between the door's keepout and 40 u short of the key:
   a 3D grid of 24 u cells in the leg's frame (`Frame`: s1, s2 across, z upstream), `CellsHalf` cells each side
   across and up to `CellsDeep` cells deep (a field under 2 cells deep is skipped). Every cell is filled with
   probability `Fill(i)` (hash of the gate's rounded position and the station) by a weighted shape:
   small cubes (8-14 u, jittered), 22 u blocks, 6 x 6 x 22 pillars, 22 x 22 x 4 slabs, small voxel spheres (19 x 6.2 u),
   46 u big cubes (2x2x2 cells), big voxel spheres (26 x 8.4 u, I2+), 46 x 46 x 4 panels, 70 u long pillars along the
   leg, and 70/94 u beams across it (I3+). A **bore** of `BoreRadius(i)` round the leg's line is never entered by any
   field prism laid or moving, so the eye is always reachable straight down the leg (the AI's lane); the challenge is
   that the bore narrows and the field round it thickens. Field prisms keep off every other leg's line, the keys, the
   doors and other stations' fields (sampled spheres), and a spatial-hash counter (`Density`) refuses anything that
   would push the 200 u neighbourhood over `ColliderCap`. `DangerShare(i)` of the small/thin shapes are danger.

**Movers** (`BlastRunMover`, laid FIRST in each field so they get the cleanest cells; posed every frame on every peer
by `SwitchbackBlastRunRuntime.TickMovers` from `NetworkManager.ServerTime`):

- **Slider** (I2+): 4 x 10 u cubes in a 2 x 2 block, sliding +-24 u along a 3-cell track across the leg
  (`SlidePeriod` 4 / 3.2 / 2.6 s). The whole track box is reserved, so nothing static sits in its way.
- **Opposed slider pair** (I4): two adjacent tracks half a cycle apart - a closing gate.
- **Rotor** (I3+): a 6 u hub and four 16 x 4 x 4 danger arms (5-21 u out) spinning about the leg's direction in a
  2 x 2-cell disc (90 / 135 deg/s, either sense, hashed phase); swept radius 21.1 u, reserved.

`PoseAt(t)` is closed form (slide `A (sin(wt + phi) - sin phi)`, spin `AngleAxis(repeat(wt, 2 pi))`), so t = 0 is the
laid pose and any peer, late joiner or replay gets the same pose from the clock alone. There is NO per-prism network
state, no physics and nothing reacts to players; a blasted mover prism simply stops being posed.

| Intensity | Bore | Cells (across x deep) | Fill | Shapes added | Movers |
|---|---|---|---|---|---|
| I1 | 30 u | 7 x 7 x 5 | 30 % | cubes, blocks, pillars, slabs, small spheres, big cubes, long pillars | none (static) |
| I2 | 26 u | 7 x 7 x 6 | 40 % | + big spheres, panels | sliders (<= 10 groups / 40 prisms) |
| I3 | 22 u | 9 x 9 x 7 | 50 % | + beams | + rotors (<= 20 / 100) |
| I4 | 18 u | 9 x 9 x 8 | 60 % | (more beams and panels) | + opposed slider pairs (<= 28 / 140), double door |

Stations closer than `StationSpacing` = 280 u door to door are skipped (hairpins fold the course back on itself).

**100 fps budgets** (10 ms a frame; CHOSEN from code reasoning and Breakwater's headless counts - NOTHING was measured
in Unity; `BudgetsHold` asserts them on 60 seeds + the four fixed seeds per intensity):

| | I1 | I2 | I3 | I4 | why |
|---|---|---|---|---|---|
| `ColliderCap` (prisms within 200 u of any prism) | 300 | 340 | 370 | 400 | the physics/trail-collision cost near a Dolphin; all under Breakwater I1's shipped worst 568 (Breakwater 568 / 464 / 322 / 276) |
| `TotalCap` (prisms laid) | 1800 | 2400 | 3000 | 3400 | instanced draw + spatial index memory; below the old Blast Run caps (4600-3000) and `LayCapacity` 4096 |
| `StationCap` (field prisms a gate) | 150 | 170 | 190 | 210 | one blast clears one station's mass |
| `MovingPrismCap` / `MovingGroupCap` | 0 / 0 | 40 / 10 | 100 / 20 | 140 / 28 | per-frame CPU: one closed-form pose per group, then a transform write + `NotifyPositionChanged` (spatial index, shell, render matrix) per prism - ~140 cheap writes at most, no physics bodies, no allocation |

Network cost per frame: zero for the fields (static prisms are deterministic from the course seed; movers from the
clock). Only the key crystals (existing `CrystalManager` path) and prism destruction (existing path) replicate.

**The AI** detours for crystals in Switchback (`AIDetoursForCrystals => true`, the scene's 220 u slack), so an AI
Dolphin takes the keys. Regatta and the other gate races keep the waypoints-only rule.

**Fixed seeds** (`SEED: FIXED`, `SwitchbackBlastRun.FixedSeed(i)` = 45_500 + i): 45_501-45_504. On the shipped shell (20 gates, 480-1080 u) those lay (sb_harness,
headless):

| Seed | Stations (keys) | Prisms | Field | Danger in field | Colliders / 200 u | Movers (slide / spin) | Moving prisms |
|---|---|---|---|---|---|---|---|
| 45_501 (I1) | 6 | 1173 | 597 | 9 | 250 | 0 | 0 |
| 45_502 (I2) | 9 | 1998 | 1404 | 21 | 339 | 10 (10 / 0) | 40 |
| 45_503 (I3) | 17 | 2988 | 2070 | 76 | 325 | 20 (12 / 8) | 88 |
| 45_504 (I4) | 12 | 3349 | 2341 | 140 | 400 | 28 (16 / 12) | 124 |

Averages over seeds 1-60 + 45_501-45_504: stations 5.8 / 8.2 / 17.0 / 13.1, prisms 1226 / 1757 / 2990 / 3239,
moving groups 0 / 9.9 / 20 / 27.9. I3 and I4 are `TotalCap`-bound; I4 trades a few gates (station spacing, double
doors) for thicker fields.

**Logs:** `[Switchback] BLAST RUN intensity N: G gates, S blocked gates, M movers, X prisms.` (verbose,
ArcadeMatch), and the RUN line above. A scene with no
CrystalManager logs `[Switchback] BLAST RUN: no CrystalManager in the scene ...` as an error and lays no keys.
`SpawnableSwitchbackFeatures`' layout revision is 5 (it lays Blast Run only; older caches are invalid).

**Held headless:** `SwitchbackBlastRunTests` (EditMode; 60 seeds + the fixed seed per intensity, plus all four fixed
seeds for budgets/clipping): Classic course, determinism (positions, scales, kinds, movers), the ladder (per station
and `EveryStepUpTheLadderIsHarder`), keys inside the blast, budgets (total, colliders/200 u, moving groups and prisms,
danger <= 15 % of the field), nothing in a mouth or the nucleus, the bore clear at 8 clock samples, movers as pure
functions of time (t = 0 laid, periodic, on track / constant radius), no prism clips another by SAT laid AND at 16
times through the movers' cycle, the shape mix, the key multiplier. `audit_prism_clipping.py` POSES
`SpawnableSwitchbackFeatures` as Blast Run at 45_501-45_504 (laid pose; flush, 0 clipping).

**Minimal / not done:** at I1-I3 nothing stops a pilot threading a blocked ring from the downstream side; the AI does
not aim its blast at doors (it takes the key and the blast fires down its heading); key crystals replace the passive
crystal field for the match; the movers use `ServerTime.TimeAsFloat` (float precision is fine for hours, not days);
nothing about frame time was measured in Unity.

## AI: the blast planner (2026-10-29) - NOT editor-verified

An AI Dolphin plays a blocked gate the way a good player does. Each gate has three points: the **key crystal** (on the
leg line, 230-400 u before the ring), the **obstacle** (the door and its block field), and the **ring**. At every
blocked gate the AI picks one of three plans:

| Plan | When | What it flies |
|---|---|---|
| **Direct** | the gate has no obstacle, or 95%+ of it (all but 2 prisms) is already blasted | the gate race's ring steering |
| **Thread** | threading the eye is cheaper, or the key is gone (a rival took it), behind, too far, or something stands in the way to it | the ring steering through the centre: the bore, which the layout never fills |
| **Blast** | taking the key and blasting is cheaper | approach the key on the fire line, with the nose (drift look) on the obstacle at contact, then fly the lane it cleared |

**Code.** `Switchback/SwitchbackBlastPlanner.cs` is pure C#: the plan, the costs and the aim, all headless-tested.
`Switchback/SwitchbackBlastAI.cs` is the server-only executor. `SwitchbackController` wires it into the gate race's
hooks: `TryOverrideAim` for the approach to the key, and the new `GateRaceController.TryOverrideDriftLook` for the nose
at contact. With the planner on, `AICrystalDetourSlack` is 0, so the planner decides which key to take. The planner is
on by default; untick **`aiBlastPlanner`** on the controller to fall back to the blind crystal detour.

**Costs, in seconds.**
- **Thread** = ETA × (1 + the eye's slow-down: 0.35 at an 18 u eye, scaled by 18/eye, plus 0.15 for a double door)
  \+ 0.6 s for each standing prism within 8 u of the line (1.5 s for a Danger prism). Movers are posed with the
  closed-form `BlastRunMover.PoseAt` at the shared clock, at ETA ± the skill's timing error.
- **Blast** = (ETA + the detour to the key + drift alignment (turn beyond 15° at 110°/s, + 0.6 s if over 45°) + 0.2 s
  of contact + 0.6 s for each lane prism within 20 u that the planned slot does **not** cover) × the skill's blast bias.

**The aim.** The Dolphin's blast is a cone along the nose, stretched along the vessel's up axis into a slot.
- The nose points from the key to the door.
- The gape (up) lies along the standing obstacle's principal axis across the leg, so a slab, beam or slider track is
  cut along its length.
- The planner's slot model (`SwitchbackBlastSlot.Default`: reach 400 u, core tan 0.12, gape tan 0.22) is deliberately
  narrower than the asset's cone, so a planned blast under-promises.

**Replanning.** The AI plans when it starts a gate and every `ReplanSeconds` (not once its strike has begun). It replans
at once when its key vanishes; if that happens within 30 u of the key, it took the key itself. It reads the key from
`Crystal.Active` (managed crystals within 4 u of the key) at 10 Hz. It reads whether a prism still stands from
`SwitchbackBlastRunRuntime.PrismStanding`: on the server, every layout prism is matched to its spawned prism by laid
position through a position hash. An unmatched prism reads as standing, so the planner can over-count an obstacle but
never under-count it.

**Difficulty** (`SwitchbackAISkill.ForLevel`, by the lobby's `AIDifficulty` per pilot; override per level on the
controller with `aiSkillOverride` / `aiSkills`):

| Lobby level | Plays as | Decision quality | Blast bias | Aim error | Roll error | Timing error | Replan | Key search radius |
|---|---|---|---|---|---|---|---|---|
| Easy | rarely blasts | 0.30 | 1.60 | 14° | 70° | 0.60 s | 3 s | 170 u |
| Medium (default) | blasts when it is convenient | 0.60 | 1.15 | 8° | 35° | 0.35 s | 1.5 s | 360 u |
| Hard ("medium-hard") | good cost choices | 0.85 | 1.05 | 4° | 15° | 0.15 s | 0.75 s | 520 u |
| Legendary ("hard") | near-perfect | 1.00 | 1.00 | 0.75° | 3° | 0.03 s | 0.25 s | 700 u |

What the columns mean:
- **Decision quality** is the chance the AI takes the cheapest plan; otherwise it falls back on its habit, threading.
- **Blast bias** multiplies the blast's cost: above 1, the pilot undervalues the blast.
- **Aim and roll error** are seeded per pilot, gate and plan, so they are deterministic.

Headless, on the four fixed-seed courses (12-17 stations each, pilots at 60-400 u before each key, 8 seats), the AI
chooses to blast on 1% / 12% / 38% / 73% of these decisions. When it blasts, its slot covers 94% / 88% / 94% / 97% of
the lane. Easy's figure is high because it only blasts the easiest, best-covered shots. Legendary never picks a costlier
plan.

**Known limits.**
- **Roll is planned, not flown.** `AIPilot` has no roll hook. The gape axis is logged and drawn, but the blast fires
  along the hull's actual up axis. The nose is flown, through drift look.
- The slot model is an approximation of `AOEConicExplosion`.
- Movers are only sampled at the ETA, so a fast rotor can still clip a threading pilot.

**Debug.**
- With the controller selected, gizmos show each AI's plan: magenta to its key, a yellow fire line to the aim point, a
  cyan gape axis; orange means Thread and green means Direct.
- Verbose ArcadeMatch log lines are written when a plan is made or changes, and when the pilot leaves the gate:
  `[Switchback AI] gate=7 plan=Blast crystal=212u aimErr=3.9 roll=-11 cover=94% cost thread=5.41 blast=4.37 why=cheapest result=planned pilot=3`
  then `[Switchback AI] gate=7 plan=Blast crystal=taken aimErr=3.9 result=blasted standing 170->41 pilot=3`
  (other results: `missed-key`, `threaded`, `rival-took-key threaded`).

**Network and performance.** It runs on the server only and adds no networked data: the pilots it steers are server
AIs, and the movers and keys are read off the shared clock and the crystal manager. It plans per gate, on a replan, or
when a key changes. A decision scans that station's obstacle (about 150-300 prisms) and a box-culled pass over the
layout for the way to the key. Per frame it does only a dictionary lookup and a few vector operations, with no
allocation.

**Tests.** `Tests/Editor/SwitchbackBlastPlannerTests.cs` (24 cases, headless) covers:
- open gate → Direct;
- key close → Blast, or an honest refusal, on at least half the stations;
- key far → Thread; way to the key blocked → Thread;
- already blasted → Direct; rival took the key → Thread;
- Legendary's erred slot covers the door, and the lane as well as a perfect aim does;
- the roll solve lies along a slab's long axis;
- the difficulty ladder (more blasts, less regret, better aim at each step; Easy rarely blasts; Legendary is never suboptimal);
- determinism; movers posed at the pilot's time; unset difficulty → Medium.

## AI: any prism in the way (2026-10-29) - NOT editor-verified

Yash's report: AI Dolphins kept flying into RIVAL trails at the same spots, as if following the exact path the rival flew.

**Root cause.** Every AI aimed at the same points. `GateRaceController.ArmRacer` hands each AI the ring centre
(`ringAt = gate.Position`), then the approach point `ringAt + gate.Axis * side * approachLead` and, inside commit range,
`throughPoint = ringAt + (ringAt - self).normalized * aiThroughDistance`. No per-pilot term, so every Dolphin converges
on one line per leg, and the Dolphin ahead has just laid its trail down that line. Nothing saw it: `AIPilot`'s
`CornerBehaviors` raycast avoidance is declared and built but never read (dead code), and the blast planner only knows
the course's own layout prisms (`ISwitchbackAIWorld.PrismStanding`), never trails.

**Fix (server only, `SwitchbackBlastAI.Sense` + pure `SwitchbackPrismAvoidance`).**
- *Lanes:* new `GateRaceController.AILaneOffset` hook (zero by default). Switchback gives each AI a fixed, seeded point
  inside the mouth of every OPEN ring (`LaneShare` of the radius; zero at blocked gates, where the eye/blast lane rules).
- *Sense:* every `ReactionSeconds` (jittered per pilot) one `Physics.OverlapCapsuleNonAlloc` down the line to the goal
  on the `TrailBlocks` layer (every prism: course blocks, rival trails, own trail), into static 128-slot buffers. Bounds
  centre + extent (capped 12 u) per hit. No allocation per look; strings only on a decision.
- *Decide:* line clear -> nothing. Blocked -> `ChooseCorridor` (rings x 8 directions round the goal, max air for least
  turn). If that dodge is poor (air < Clearance or turn > 25 deg) and a managed crystal ahead and nearer than the blocker
  has a slot (fired at the blocker, gape along its widest spread) covering >= `BlastMinCoverage` of it -> **Blast**: fly
  through the crystal with the drift-look on the blocker (same execution as the gate blast). Else **Avoid**.
- Not used while a gate blast is on its strike (the obstacle is the point).

| Level | SenseRange | Reaction | Clearance | Rings | BlastMinCoverage | Crystal range | LaneShare |
|---|---|---|---|---|---|---|---|
| Easy | 70 | 0.45 s | 5 | 1 | never | - | 0.35 |
| Medium | 110 | 0.30 s | 7 | 2 | 0.70 | 110 | 0.40 |
| Hard | 150 | 0.18 s | 9 | 3 | 0.55 | 160 | 0.45 |
| Legendary | 200 | 0.10 s | 10 | 4 | 0.45 | 220 | 0.50 |

Log: `[Switchback AI] prism gate=.. action=Avoid|Blast|Clear ahead=..u blocking=.. ... avoids=.. blasts=.. pilot=..`.
Headless (point mass, 100 u/s, 110 deg/s): straight down a rival trail 90/90 prisms hit without the sense, 0 at every
level; a weaving trail 8 -> 0. Real hull/turn dynamics are not modelled - verify in editor.

## Known limitations / follow-ups

- **The arcade grid had to grow to show this card, and growing it takes TWO changes.**
  `ArcadeExploreView`'s grid is AUTHORED at a fixed size — 3 rows × 4 = 12 slots in Menu_Main —
  and the populate loop was bounded by it, so a roster larger than the grid truncated **silently**:
  the alphabetically-last modes simply stopped existing in the arcade, with no error and no gap in
  the grid to notice. Menu_Main was sitting at exactly 12 renderable cards (13 games minus the
  Maelstrom, which the grid deliberately excludes), so adding Switchback made 13 and pushed
  Wildlife Liberation off the end. `EnsureGridCapacity` clones the last authored row until the
  roster fits, and the loop's third bound — `GameList.Games.Count`, a ceiling on a *different*
  list — is removed. Arithmetic is asserted in `ArcadeGridCapacityTests` rather than eyeballed,
  because an off-by-one there does not throw, it hides a game mode.

  **Adding the row is only half of it**, and the missing half reads as three unrelated bugs. The
  grid lives in a `ScrollRect` whose Content has a HARDCODED height (1104) and no
  `ContentSizeFitter` — nobody had noticed, because the 3×4 grid was never scrolled to its end. A
  card that ends up below the reachable range is clipped by the viewport's `Mask`, and that `Mask`
  does two things to it: it cuts the drawing off (you see the top of a card and nothing under it)
  **and**, being an `ICanvasRaycastFilter` that rejects any point outside its own rect, it eats the
  PRESS. The ScrollRect meanwhile stops at the authored height, so dragging further springs back
  (MovementType is Elastic). *Half a card, a scroll that snaps back, and a dead button are one
  cause* — which is also why favouriting a mode "fixed" it: that only moved it out of the last
  slot and moved something else in.

  **Getting the height right took FOUR passes, and the three failures are the useful part —
  the third was wrong about the CAUSE while looking like it worked.**

  1. *Increment.* Grow Content by what the new rows cost (`rowHeight + gridSpacing` — the grid's
     spacing is **negative** in Menu_Main, the rows deliberately overlap). This assumes Content
     previously contained its children exactly, and it did not: the authored 1104 was already
     short of the three authored rows, whose lowest edge sits at −1835.78 under the grid's own
     stretched height. The increment landed short and the card stayed out of reach.
  2. *Measure once.* Replace the increment with
     `RectTransformUtility.CalculateRelativeRectTransformBounds`, which reads the real extent of
     every **active** descendant at runtime. Still short.
  3. *Iterate to a "fixed point", blamed on `ChildForceExpandHeight`.* **Wrong diagnosis, and it
     shipped a second bug.** Content's `VerticalLayoutGroup` is `m_Enabled: 0` — it has never
     laid anything out, so force-expand was never doing anything and switching it off changed
     nothing. The real cause was one line up the hierarchy.
  4. *Pin the children, then measure once.* **Content's layout is pure ANCHORS, and two of its
     three children are anchored to a FRACTION of its height** — `MelstromMode` spans y
     0.761→1.0 (0.239 × H) and `GameGrid` spans 0.218→0.843 (0.625 × H); only `WeeklyChallenge`
     is point-anchored, which is why it alone never moved. So every unit added to Content
     stretched the grid by 0.625 — the grid's bottom receded as fast as the content grew, which
     is why no amount of iterating reached it — **and stretched the Maelstrom banner by 0.239**,
     which is the too-tall card that pass 3 shipped (264 → 584 at the height it settled on).
     `PinVerticalAnchorsToTop` re-anchors each child to Content's top at the height it is
     drawing right now — arithmetically a no-op, verified against both children's authored
     values — after which Content's height is a pure scroll extent, the grid is sized to its
     rows once, and the fit settles in ONE pass at 1409.5 with the banner still 264.

  **The rule pass 3 got wrong is worth more than the fix: a ScrollRect's Content is a SCROLL
  EXTENT, not a layout frame.** Anything anchored to a fraction of it is resized by every change
  to that extent, so "make the content taller" silently resizes the page. And the reason the
  wrong diagnosis survived a round of review is that *a disabled component reads exactly like an
  enabled one* in a YAML dump unless you look for `m_Enabled` — the layout group's fields were
  all there, all plausible, and all inert.

  Three details that look like polish and are not: the needed height is
  `max(bounds.size.y, -bounds.min.y)` because both rects are TOP-pivoted, so what has to be
  covered is how far the lowest child reaches *below the origin* rather than the bounds' total
  height; the pin runs **before** the measurement, or the measurement is of a layout it is about
  to move; and the fit runs **last and unconditionally** in `PopulateGameSelectionList`, because
  `CalculateRelativeRectTransformBounds` skips inactive objects and the pre-existing shortfall
  wants repairing whether or not a row was added this time. Only the VERTICAL anchors are
  pinned — the Maelstrom banner is deliberately anchored wider than the content (x 0.474→1.715)
  so it runs past the scroll view's right edge, and normalising that would move it on screen.
  Deliberately **not** a `ContentSizeFitter`, which would re-derive the authored rows' height
  from their preferred sizes rather than from the scene's anchors.

  **And it now says so when it fails.** Every version of this bug was silent — a truncated roster
  left no gap, and a clipped row looked like a scroll that had reached its end, so both read as
  "that mode is not shipped yet". `ReportUnreachableCards` logs an error naming any mode with no
  slot and any card whose lowest edge sits past the content's own height. Beside it,
  `ReportCardPressability` states the LAST card's press path every repopulate whether or not it
  is broken — its `Button`'s interactable flag, whether a `SelectGame` listener was attached, and
  what a real `EventSystem` raycast at the card's centre actually lands on — and `SelectGame`
  itself logs the press. **That slot has been reported dead twice**, and on screen a swallowed
  press looks identical whether the geometry, the button, the lock or the modal ate it; reasoning
  could not separate them from the scene alone, so the view is made to say which.

  Two details there are not incidental. **Whether `SelectGame` was wired is RECORDED at the
  decision, never read back off the `Button`** — `onClick` can report its PERSISTENT count and
  nothing else, and `SelectGame` is added at runtime, so the count reads identically on a wired
  card and an unwired one; `_lockedSlots` is the record. And the press is logged in `SelectGame`
  because that is the OTHER half of the measurement: everything the card-side report can see is
  about the card, and "the click never arrived" and "the click arrived and the modal declined to
  open" are the same observation on screen with nothing in common underneath — one is the grid's
  problem, the other the modal's.

  **And the last slot's dead press was none of those things — it was DEPENDENCY INJECTION.**
  `MenuAudio.PlayAudio` is the ONE persistent `onClick` listener every `GameCard`'s Button
  carries, and it dereferences an `[Inject] AudioSystem`. Reflex populates `[Inject]` for objects
  present at SCENE LOAD; `EnsureGridCapacity` creates its row at RUNTIME, so nothing injected it
  and that field was null on all four of its cards. **`UnityEvent.Invoke` runs PERSISTENT
  listeners BEFORE runtime ones** (`InvokableCallList.PrepareInvoke` builds
  `m_ExecutingCalls = m_PersistentCalls + m_RuntimeCalls`) and guards neither, so the
  `NullReferenceException` from `PlayAudio` aborted the invoke list before reaching the runtime
  listener this view attaches — `() => SelectGame(game)`. The card rendered with the right title
  and art, reported `interactable = true`, passed an `EventSystem` raycast, played no sound, and
  opened nothing.

  That is exactly why it read as POSITIONAL: only cards in a CLONED row are un-injected, and at
  13 modes only the first of them is ever active. At 14 modes the second would have died too.
  Favouriting "fixed" a mode by moving it onto one of the twelve AUTHORED cards, which the scene's
  `ContainerScope` had injected at load.

  The fix is in two layers, and the second is the one that generalises past this grid.
  **(1) Inject at the creating site.** `EnsureGridCapacity` now holds `[Inject] Container
  _container` and calls `GameObjectInjector.InjectRecursive(row.gameObject, _container)` on every
  row it clones — which covers not just `MenuAudio` but any `[Inject]` field any future card
  component grows. This was the **third** outing of one bug class, and the first two had
  already written the fix down: `ProfileIconSelectView.cs:167-170` and
  `ProjectilePoolManager.cs:27-33` each carry the identical call with a comment naming the
  identical failure (*"an un-injected projectile NREs on its null AudioSystem in LaunchProjectile
  and every shot from that instance is a dud"*). **A comment written twice for other subsystems
  did not stop it a third time**, which is why the fix is not only two code changes but a gate:
  `Tools/Build/audit_persistent_listener_injection.py --check` resolves every persistent
  `UnityEvent` listener in every scene and prefab to its target's script and fails on any
  `(class, method)` whose class declares an `[Inject]` field and is not reviewed. It RATCHETS —
  its pairs are frozen as an explicitly UNREVIEWED baseline, so it passes now and fails on
  anything new. (That baseline was **28** when this was written and is **56** since 12 Sep 2026,
  when the auditor's own parser was fixed: it had been reading 279 of the project's 992 persistent
  calls. Not one of the added rows is new debt — see CLAUDE.md's entry on this gate for the three
  rules that came out of it.) **(2) Fail safe on the persistent listener.** Layer 1 has to be remembered once per spawn
  site, forever; layer 2 holds everywhere at once. `MenuAudio` now falls back to
  `AudioSystem.Instance` and warns ONCE per component, so the next runtime-created UI object that
  nobody injects loses a SOUND rather than a BUTTON. `MenuAudioResilienceTests` pins it.

  **Four investigations missed it because every one of them was looking at authored state.** The
  scene YAML is byte-identical for all twelve cards; the clone is a faithful copy of one of them;
  the difference exists only at runtime and only in a field no serialized data mentions. For the
  record, all of these were ruled out and none was the cause: nothing is drawn over the grid
  (`GameSelectScrollView` is `Explore`'s last child, and the panel's later siblings
  `ArcadeLobbyList` and `CloseButton` miss it — the scroll view is inset 600px from the panel's
  left edge); the rows do not overlap in a way that matters (spacing −142.08 against 456-tall rows
  holding 202.72-tall cards, so the CARDS never overlap, and the clone is a later sibling anyway,
  so it raycasts first); `GameCard.SetLocked` is symmetric and `_originalBgColor` initialises to
  `Color.white`; (the `CallToActionIndicator` corner badge this line described was retired
  with the call-to-action surface, 2026-09-08 — `Docs/UI_ARCHITECTURE_AUDIT.md §2.13.1`);
  `MinigameLaunchPanel.Handles` accepts every non-Maelstrom card and is not a `HostModal`, so
  `OpenFor` always calls `ModalWindowIn`; `ArcadeDPadNav` never writes `interactable`; and the
  progression chain cannot single a slot out, since ten of the thirteen roster modes — Switchback
  among them — are absent from `GameModeQuestList` entirely. **The lesson is the search, not the
  list: when a card is authored identically to eleven working ones and still does nothing, stop
  auditing the asset and ask what is only true of it at RUNTIME.**

  **The general rule for the next mode:** *adding a card is adding a ROW, and a row is only
  reachable if the scroll content was measured after it — never assume an authored content height
  contains what the scene authored into it.* The three moving parts are the roster bound (fixed),
  the grid's capacity (`EnsureGridCapacity`, general) and the content's height (`FitScrollContent`,
  general), so mode 46 needs none of this repeated.

- **20 gates is unmeasured.** Chosen from the arithmetic (≈9.4k units of course; 2–3 minutes at
  realistic Dolphin speeds), not from a playtest. It is one editor field.
- **No toasts.** No `GameToastConfigSO`, so no "GATE 12/20" or lead-change announcement. The
  shared config still covers join/ready/disconnect. Rampage and Salvo also ship this way (Dog
  Fight and The Bends have since gained `GameToastConfig_*` assets); the enum's next free block is 70+.
- **The mode preview has no course in it.** `ModePreview_Switchback.asset` exists (authored by
  `Tools/Build/author_mode_previews.py`, spawn block by `author_preview_spawns.py`), but the gate
  course is generated by the controller at match start, so the preview arena shows only the
  Barren cell and the Dolphin's drift-and-boost, with no rings to thread. A `StructurePrefab` of a
  few standing rings is the recorded gap.
- **No laps.** The course is one pass. A looping course (last gate leads back to the first) would
  let the target be gates × laps, as Skim Race does.
- **The gate rings are not in the Codex.** They are neither flora, fauna, crystal nor toy, so no
  kingdom currently fits them.
- **The course is generated about the origin and then offset to the cell.** `SwitchbackCourse`
  is a pure function of its shell and knows nothing about where the arena is;
  `GenerateAndBroadcastCourse` adds `ResolveCellCentre()` to every gate before broadcasting, so
  world positions travel and moving or nesting the Cell keeps the course on the arena. The
  fairness argument (gate 1 on the equatorial ring's pole) depends on that offset.
- **~~Three sibling scenes carry the same stale-donor comeback source this mode's clone did~~ — FIXED 2026-09 by
  retiring the setting**: `ElementalComebackSystem` now reads `ScoringRuleSO.DomainValue`, so no
  scene can author a comeback stat at all (eight scenes were affected, not three). Historical note follows:
  (`MinigameDogFight`, `MinigameBends` and `MinigameWildlifeLiberation` all serialized
  `differenceSource: 3` while `DefaultSourceFor` named `CombatPoints`/`LifeformsKilled`).
  Not fixed here — it is not this branch's diff — but it is
  the same defect and worth a ticket.
- **The Skim Race cell's `PhaseThresholds` are count-based** (600/480, 2000/1600) with no volume
  keys, inherited from a mode whose vessel lays a different trail. A Dolphin trail here has not
  been measured against that ladder; if fauna never hunt, or the cell pins at Frenzy, that is
  where to look.
  *(Note 2026-11-01: Blast Run spawns no flora/fauna, so this only applies if they come back.)*

### AI: the emergency sweep (2026-10-29, "dolphins eat prisms stupidly")

Playtest of 9ff5d38b5: the sense ran but the hulls still flew into prisms. Root causes:

1. **A drift locks the course.** `AIPilot.Update` keeps a commit drift while `Dot(desired, VesselStatus.Course) >= .9`
   (any dodge under ~26° changes nothing - the hull keeps travelling the locked course while only the nose turns), and
   during that drift `desiredDirection` is replaced by the drift LOOK. The Dolphin drifts most of the race.
2. **The sense looked down the line to the goal, not the velocity**, on a 0.10-0.45 s tick with a fixed 70-200 u range
   (under a second of warning at boost speed).
3. **No sense at all on a course without a blocked gate**: `_blastAI` was only built when `layout.Stations.Count > 0`.
4. **The strike ignored everything** ("on the strike the obstacle ahead IS the point") - rival trails included.
5. The boost driver's `HoldsLine` (`CentreStick`) and `RotationLimit` could flatten a dodge; the orbit break replaced
   the target.

Fix: `AIPilot.SetEmergencySteerProvider` - asked every frame with the real velocity; a hit lets the drift go, beats the
orbit break, and stands the boost driver's line-hold down. `SwitchbackBlastAI.Emergency` sweeps
`SphereCastNonAlloc` at hull radius (3 u + 0.3 x clearance) down the velocity, reach = speed x 0.35/0.5/0.7/0.9 s
(Easy..Legendary, min 25 u), plus the nose while turning; every other frame (staggered) when cold, every frame for 0.6 s
after a hit; the dodge is held 0.18 s. On a strike only hits before the crystal count. The sense range is now
max(SenseRange, speed x (1.2 + reaction)). The sense runs on every course. Controller `aiSenseDebug` draws it
(sense tube green/yellow, sweep cyan/red, hit magenta, dodge white, avoid orange) and logs dodges at Info level.
Startup: `[Switchback AI] sense active mask=2048 layer=11 stations=N`.

Tuning: the hull radius is `SwitchbackPrismAvoidance.ShipRadius = 3f`; the sweep radius is ShipRadius + 0.3 x the
difficulty's clearance. Raise it if hulls still clip prism edges, lower it if they dodge gaps they fit through.

### Inspector flags (SwitchbackController)

| Flag | Default | Does |
|---|---|---|
| `aiBlastPlanner` | on | Server AIs use the Direct / Thread / Blast planner (off = the generic gate AI) |
| `aiSkillOverride` + `aiSkills` | off | Replace the intensity-to-skill mapping with four hand-set skills (I1-I4) |
| `aiPlanGizmos` | on | Scene-view plan lines (select the controller) |
| `aiSenseDebug` | off | Sense / sweep / dodge lines and Info-level dodge logs |

## Offline: the Blast Run simulator (2026-11-01)

`Tools/Build/switchback_sim_harness/run.sh` (eval / trace / selftest) compiles the SHIPPED `SwitchbackCourse`,
`SwitchbackBlastRun` (keys, doors, block fields, movers posed off the clock), `SwitchbackBlastPlanner` and
`SwitchbackPrismAvoidance` against the Skim Race harness's Unity shim, and races one AI Dolphin through them with the
executor ported from `SwitchbackBlastAI` (plan, setup / strike, drift look, the sense, the emergency sweep),
`GateRaceController` (the Regatta steering, lanes at open gates), `AIPilot` (commit drift, orbit break, nose lag) and
`ChargeBoostAIPolicySO` (the `DolphinAIBoostPolicy_Switchback_I<n>` numbers). Prism hits are what
`DolphinImpactorDataContainer` applies: a plain prism x0.5 easing back over 1 s, a Danger prism a dead stop easing back
over 3 s, the charge halved, the prism destroyed, no bounce. A key is taken within 12 u (the 10 u skimmer); its blast
destroys what `SwitchbackBlastSlot.Default` covers from the key down the nose, gape along the hull's up (roll is not
flown). 20 gates on the shipped shell; seeds 45501-45504 (the fixed seeds) and 1-8.

**Not modelled:** rivals and trails (the AI is alone), the danger prism's elemental debuff, the asset's real cone (the
planner's narrower slot is used; `cone=1` widens it), physics contact shapes (a 3 u hull sphere against each prism's box).
It is a model: Unity decides.

**What it found (one AI alone, median seconds over 12 courses; 2026-11-01, the AI as shipped that day):**

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Shipped AI: Easy / Medium / Hard / Legendary | 93 / 89 / 95 / 99 | 146 / 160 / 192 / 183 | 198 / 201 / 245 / 254 | 257 / 210 / 411 (10 of 12 finish) / 541 (4 of 12) |
| Sense and emergency sweep off (Easy / Legendary) | 74 / 80 | 121 / 128 | 133 / 135 | 128 / 144 |
| Planner, sense and sweep all off (Easy / Legendary) | 74 / 77 | 121 / 124 | 133 / 135 | 128 / 120 |
| Sense and sweep see only Danger prisms (Easy / Legendary) | 71 / 94 | 124 / 137 | 156 / 176 | 141 / 232 |

1. **The dodging costs the most, and more the harder the AI.** Each dodge pulls the Dolphin off its line; it then
   misses the ring (18 u eye at a blocked gate) and loops back. A harder level senses further and dodges more, so it is
   SLOWER: at I4 Legendary 1,084 emergency dodges and 598 missed rings over 12 races.
2. **A plain prism hit is cheap** (about 0.25 s of lost distance), a Danger one about 1.5 s. Flying straight through a
   field costs less than going round it.
3. **The blast, as flown, does not pay yet.** With dodging off, the planner on and off finish within a few seconds
   of each other: the blasts remove prisms (fewer hits), and the setup turn behind the key costs about as much. The
   planner's `HitPenalty` (0.6 s a prism) is over twice the measured plain-hit cost, which makes it blast and dodge more
   than it should.

## Watching the AI: the AI Diagnose panel (2026-11-01)

The old FrogletTools ▸ AI ▸ Third Eye window (a second camera, rendered twice) is gone. The code still lives in
`Controller/Camera/ThirdEye/`, but the tool is now the in-game **AI Diagnose panel** (editor + development builds only):

- **F2 is the only hotkey**: open / close. Everything else is a panel button or a console command (`aidiag cam on`,
  `aidiag fly`, `aidiag next`, `aidiag overlay ...`; `help` lists them).
- Cameras (Chase / Follow / Free fly / Orbit) drive the MAIN camera only while the panel is open. Free fly is
  `FreeFlyCamera`, the same class Lavalamp god mode uses. The default gameplay chase camera is unchanged and is what
  every match starts on.
- Switchback registers two overlays: **AI plan (Switchback)** (= `aiPlanGizmos` in the Game view) and **AI sense
  (Switchback)** (turns on `aiSenseDebug` lines and logs).
- Closing the panel (F2, scene unload at match end, quit, Play stop) restores the default camera and turns every
  overlay off. Only slider tuning persists; nothing else survives a session or domain reload. Full controls:
  `/vessel-ai` §4.1.

## Performance (2026-10-30 .. 11-01, "make Switchback very performant")

Targets for a Release capture of Switchback I2 / Blast Run I4 (`perf_hunt.py` scenarios `Switchback`,
`SwitchbackBlast`): **CPU busy under 10 ms, about 300 draw calls, GC under 50 KB** per capture window.

What was found:
- **The Third Eye window rendered the world a second time** (post, shadows, AA). Fixed by moving it into the game
  (it drives the main camera; no second render).
- **Editor GC** is the editor's, not the game's: Debug code optimization, deep profile, a visible Scene view and
  inspector repaint all allocate. Judge GC only from a clean capture.
- **Background creatures** (flora/fauna) cost CPU every frame for nothing the race uses; Blast Run spawns none.
- Code: the mover scan matches prisms through a laid-position hash and a reused buffer, with profiler markers on
  `TickMovers` and the scan (414581980); AI log strings are built only while ArcadeMatch verbose is on, through
  `CSDebug` (d9816f400).

**Clean capture:** Code Optimization = Release; Profiler deep profile off; no visible Scene view; Game view <= 1440p
and focused; AI Diagnose panel closed (camera and overlays off); `aiSenseDebug` off; fps uncapped; 2 AI at I2 (or
3 AI at I4), 90 s, and let the editor auto-capture. Then `python3 Tools/Build/perf_capture_report.py`.
