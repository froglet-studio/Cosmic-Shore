# Grizzly Time — the Grizzly-only circuit race

> `GameModes.GrizzlyTime = 63`. A closed loop of switch rings is cut through the cell and every
> pilot flies **two laps** of it in order (16 gates; two since 2026-10-26); the first **domain** whose
> **lead runner** clears the last gate of the last lap wins, on finish **time** (golf). A **powder keg**
> stands beside every ring (2026-11-11, §0): open it with your own bomb, or Rush through it (2026-11-20,
> §0.1), and the gate is yours; a bomb blown from the keg's exit side throws you down the next leg. The
> race the Grizzly was asked for, alongside its arena mode **Grizzly Charge** (`62`).

## 0. The powder kegs (2026-11-11)

Spec, research and judge panel: `Docs/GrizzlyTime/POWDER_KEGS_SPEC.md`. Code: `GrizzlyTimeKegs` (pure),
`GrizzlyTimeController.Kegs.cs` (the partial that lays, breaks, regrows and credits them, the feedback),
`GrizzlyTimeController.AI.cs` (the AI's aim and its Rush).

**The loop.** Freeze a bomb in the keg, get round to the keg's EXIT side (you between the keg and the next ring), blow
it. One detonation opens the keg, clears the gate (a pilot within the credit radius of an OPEN keg clears its ring,
`ThreadsGate`) and throws the hull down the next leg, because the launch pushes AWAY from the bomb. Where you stand when
you blow it is your steering; the keg gives it a target. **The ring mouth still clears the gate**, so the old line is never closed
(Yash turned Thresher Time's nests off on 2026-11-07 for "just pass through waypoints"; here the kegs are a second way
in, and `kegsAtGates` turns them off).

| level | the new demand | core | credit | regrow | chalk |
|---|---|---|---|---|---|
| I1 KEGS | hit a target with your bomb: one bomb is key and engine | plain | 160 u | 3 s | none |
| I2 BANK SHOT | blow it from its exit side, so the push points at the next ring | plain | 140 u | 3 s | one rail |
| I3 TWO KEYS | strip the shield from range, then blow it from the exit (re-shields 2.5 s after) | shielded | 120 u | 2.5 s | one rail |
| I4 BREAK YOUR OWN | the tightest credit and the fastest regrow: a rival's open keg is gone first | shielded | 100 u | 2 s | one rail |

- **Placement** is closed form, zero random draws, the same on every peer: 140 u from the ring centre, in the ring's
  plane, toward the corner's inside (the legs' bisector); a corner under 10° picks its side by a hash of the ring. A
  keg is 3 core cubes and a 12-slab hoop at 16 u (15 prisms) - its nearest prism sits 124 u from the ring centre, past
  a full blast (100 u) fired in the mouth. **Mouths 96 / 84 / 74 / 66 u** (were 96 / 72 / 58 / 46): the mouth is the
  safe line, no step over 15%.
- **Chalk** (I2 up): 6 plain dots on the corner's inside of the exit lane, 70 u off it, 120-260 u down it - beyond a
  full blast at the keg, and at least 43 u from every ring-to-ring line over 400 seeds (the first cut's outside rail
  sat ON that line, median 3-10 u, and was dropped). A dot that would crowd a mouth is skipped.
- **Measured** (`grizzly_time_course_harness/run.sh --sweep`, 400 seeds, a model of the throw, not the hull;
  re-measured 2026-11-20 with the blow point on the exit side, §0.1): at corners over 60° a straight blow (fly on past
  the keg and blow it from there) lands a median **198 / 243 / 281 / 335 u** farther from the next ring than the bank
  shot at I1-I4 (never less than 39 u). A throw-chain race (one launch a gate, no carry bomb, straight lines) is a
  median 92 / 100 / 98 / 106 s for two laps, and 73 / 78 / 78 / 83 s with a Rush on every straight the Energy allows
  (13-15 Rushes a race) - both models with no turning; a carry bomb a leg brings either down.
- **I4 was twin kegs** (inside and outside, pick one). The throw-chain sweep found the inside keg right at 99% of
  gates and reading the choice worth 0.1%, so the twins were dropped as the spec's own bar said.
- **Rivalry** is the fleet's: a blast that catches a rival is a MissileBlast hit (20 points, 8 s silence), and a
  silenced pilot's blasts deflect off every prism, so it threads the mouths until it recovers.
- **AI** (input only, no vessel change; `GrizzlyTimeController.AI.cs`): commits from the approach point
  (`AICommitFromApproachPoint`; Redline went 10/24 → 24/24 offline on it); passes BESIDE a plain keg down its exit
  lane (the autopilot's bombs open it) and gives it up for the mouth if it still stands; flies to any OPEN keg within
  600 u; threads the mouth past a shielded keg; after 18 s without a gate it is steered to a run-up 300 u behind its
  ring; and it Rushes on a straight (§0.1). **Since 2026-11-21 it no longer passes beside a standing keg for its bombs
  to open; it RAMS a standing keg open with Rush** when the line through the core to the next ring is no longer than
  the line through the mouth (`aiRamsKegs`, `aiKegRamSlack` 0; `GrizzlyTimeKegs.AIRamPays` / `AIShouldRam`: Rush within
  100 u, nose within 12 deg, give up only inside 8 u of a core still standing). About 9 rams in 10 open the keg
  (next bullet).
- **The ladder after the 20-80 blast (2026-11-21).** With the smaller blast and its retrained Time AI the ladder ran
  backwards (bleeding-edge, 12 courses: I1 133 s, I2 136, I3 82, I4 92). The cause was the AI, not the courses: at
  I1-I2 it detoured to every standing plain keg, whose core now needs a bomb frozen within 40 u, and opened about one
  a race. Threading the mouths past standing kegs (`AIKegGiveUp` retired) gave **I1 77 s, I2 82, I3 80, I4 92**
  (`regatta_judge.py --mode GrizzlyTime`, card start, 24 fresh courses), I2 and I3 level. Garrett (card, 2026-11-21):
  teach the AI to open kegs. With the ram (same 24 courses): **I1 70 s, I2 72, I3 82, I4 83**. The shielded cores of
  I3-I4 are now the step: a ram opens them too, but (not traced yet) a hull meeting a shielded prism bounces (PRISM_MOTION 1a),
  so a ram there costs more than at I1-I2. I1/I2 and I3/I4 are pairs for the AI (chalk, credit radius and regrow do
  not touch a ram). A course pass was tried and dropped: squeeze, profile, mouth size and the out-of-plane swing barely move this AI;
  only course length does (an I4 ring of 950 u gave 114 s), and the 480-1080 cell shell then cuts legs under a
  launch and a half. Runs: `/mnt/project-files/regatta-flywheel/night3/grizzly_ladder/` (`be_*`, `fx*`).
- **Feedback** (in the world, every peer): the keg's burst (doubled speed on your final lap) and its regrow as a live
  ribbon, the chalk lane, a crown of debris off each ring you clear (local). Toasts `GameToastConfig_GrizzlyTime`:
  the shared race beats 129-132 + 30, the hint (300, once), "Bank shot ({0})" (301, first and every 4th) and "Rammed
  open ({0})" (302, §0.1). Five sound slots: `kegBurstEvent`, `kegStrippedEvent`, `bankShotEvent`, `crownBurstEvent`
  (each holds the recycled placeholder the ledger lists for it since 2026-11-16,
  `Docs/AudioSystem/sound_placeholders.json`) and `kegRammedEvent` (new, EMPTY).
- **Cut from the spec:** the finish demolition wave (bursting every keg on the local finish would open them for the
  AIs still racing on the host), the I4 twins (above).

### 0.1 The fun pass (2026-11-20): the bank shot fixed, Rush in the race

Design, three judged designs, the skeptics' findings and a 13-finding review: this session (Yash). Nothing here was
played. What ran: the offline harness (33 tests, 13 negative controls), and the open editor compiled the final code,
runtime and tests, and `GrizzlyTimeKegsTests` (19) and `GrizzlyTimeCourseTests` (14) pass in it (EditMode, 2026-11-20
22:10).

- **The bank shot stood on the wrong side.** `GrizzlyTimeKegs.BlowPoint` was `Centre - ExitDir x 60`, the ENTRY side.
  A Grizzly is launched away from its bomb (`LaunchSelf`: direction = hull - bomb), so a bomb in the keg threw a hull
  standing there away from the next ring. It is `Centre + ExitDir x BlowStandOff` now. The test that guarded it measured
  only the distance, so it passed on either side; it now asserts the launch is the exit lane. The numbers above, the
  hint toast, the card text and the checklist's hand test described the wrong side and are corrected. The in-game
  bank-shot reading (`IsBankShot`) always read the real launch, so it was never wrong.
- **Rush is a key.** The Grizzly's Time ability is a ram since 2026-11-16: for one second every prism within 16 u of
  the hull is smashed, shielded ones included. A Rush through a keg's core opens it in one pass at every level (at
  I3-I4 that replaces two bombs). The vessel's ram runs only where the vessel has authority and prism deaths are not
  replicated, so every peer hears each hull's Rush (the executor's public `OnRam`) and opens ITS OWN copy of a keg
  whose core that hull is within `RamReach` of (`GrizzlyTimeKegs.RamOpens`, `SyncRamWatches`, `TryRammer`). `RamReach`
  is 19 u: the ram's 16 plus half a core cube, a stated tolerance for reading a replica's position once a frame. A hull
  this machine does not fly gets 0.2 s more on the end of its window (`RamLatencyGrace`), because its replica trails
  its owner. A silenced pilot's ram opens nothing, as on the vessel. The vessel is read, never changed;
  `Ram_constants_match_the_shipped_Rush` holds the two restated numbers to the asset.
- **What "every peer" does not promise.** A peer hears a Rush only when ITS copy of the hull accepted the press, and
  each copy keeps its own charges, cooldown clock and Energy (none is replicated; Energy gains once a second on each
  peer's own phase). A press made the instant the Energy or the cooldown allows can be refused on a peer a tick behind;
  that peer's keg then stands (stripped, at I3-I4) while it is open elsewhere, until it regrows. The rammer's own gate
  is decided on its owner and is never lost. Before this pass a rammed shielded keg opened on the rammer's machine and
  the host only, so this is better, not whole. The whole fix is to decide the open once, on the hull's owner, and send
  it: a wire change (a protocol-version bump, `Docs/claude/MULTIPLAYER_AND_SOCIAL.md`), left as a decision.
- **A standing keg mends its hoop.** A ram that only grazes a keg breaks hoop slabs where the vessel has authority and
  nowhere else, and a blast that strips a shielded core takes the hoop with it. A standing, unstripped keg now lays
  its lost slabs back every `chalkRegrowSeconds` once no hull is within 110 u, so every peer's keg is whole again.
- **The order needs no chord.** Rush through the keg (it opens, the gate is yours), then an ordinary bomb-jump down
  the lane. The bank shot is read for `bankShotWindowSeconds` (1.5 s, was 0.3) after a keg breaks beside you, so that
  order counts as one; a Rush's own push (at most 120 u/s) never does (`BankShotMinPush` 150).
- **Feedback.** Toast 302 "Rammed open ({0})" to the pilot whose ram it was (first and every 4th), the
  `kegRammedEvent` slot at the keg on every peer that opens it, and the hint (300) and card now say "bomb it, or Rush
  through it", then "freeze a bomb in the keg, fly just past it toward the next ring and let go".
- **The AI Rushes for pace, never to break** (Yash, 2026-11-17: an AI breaks only what it cannot pass, and the mouth
  is always open). One pure rule for every difficulty, `GrizzlyTimeKegs.AIShouldRush`: nose within 8° of the point it
  is STEERING at, 150 u of straight left, no launch pushing it, the Energy there with one gain tick to spare (so a peer
  a tick behind still accepts the press), its own last press cooled down. The steering point is the keg aim when the
  mode answered it, else the autopilot's own last target (the approach point, then the point through the mouth), with
  the run cut at the ring; never the ring's centre, which the nose is 13-23° off for the whole approach. It does not
  Rush while a difficulty mistake hides or moves its ring. Pressed and let go in one call through the replicated press
  on the hull's own bound control (`TryGetBoundAction<GrizzlyRushActionSO>`), by the machine that flies the hull.
  `aiRushes` on the scene's controller turns it off. Plan step: `time-arcade` (hand-set rules, a seed).
- **A guest-flown AI hull is no longer "rescued" every 18 s.** The gate clock was stamped in `OnServerTick`, which only
  the server runs; it is kept in `TryOverrideAim` now, which runs on whichever machine flies the hull.

**What the pass found and did NOT fix** (each is somebody's decision):
- **Time does not change how often a Grizzly can Rush in a race.** Energy starts at 0, gains 0.05/s, and a Rush costs
  0.25: one Rush per 5 s. The three banked charges cover the 6 s refill at Time 0 for about 70 s, and from Time 3 up
  the refill is already quicker than the Energy. So a Time crystal, the comeback's Time lift and the I2-I4 start at
  Time 5 buy only Vector Control (the ram follows the nose). Fixing it is a change to the vessel (Garrett's kit), not
  the mode.
- **At I3-I4 the kegs may not be the fast line.** A straight-line model with no turning or threading cost put
  bomb-jumping through the mouths 12-25% ahead of every keg line there (the legs are longer and a shielded core
  costs two bombs or a Rush). The model cannot see the cost of threading a 66 u mouth at 350 u/s, so only play can
  settle it.
- **Match length.** The models above put two laps at 73-106 s; the recorded hand-flown races (5 flown, 2 finished,
  `Docs/AI_SYSTEM/VESSEL_MODE_CHART.md`) have a best of 106 s and a median of 192 s for 16 gates. Laps were left at two.

### 0.2 Ruins at I4 (2026-11-24, Yash): `GrizzlyTimeRuins`, `GrizzlyTimeController.Ruins.cs`

The environment update Spearline got (`SPEARLINE.md` §2a), on this circuit. **Not compiled in Unity, not played.**

- **What stands.** `SpearlineRuins.Lay` is CALLED with this course's rings (not copied, no Spearline file changed): one
  ruin on each leg of 380 u or more except the leg into ring 0, the seven kinds in turn (pergola, flower head, vines,
  wall with a doorway, tunnel, maze, turning wheel), every prism at least 8 u thick, a 60 u lane down the ring-to-ring
  line, nothing within 120 u of a ring's mouth. I4 only; I1-I3 lay none.
- **Cut back for the Grizzly.** A Grizzly is mostly not on the ring-to-ring line: it flies keg to keg and a bank shot
  throws it from a keg at the next ring. `GrizzlyTimeRuins.Lay` therefore also drops every standing prism within 60 u
  of the throw lane (keg to next ring), of the keg line (keg to next keg) and of the approach (ring to next keg), and
  within 110 u (`HullClear`) of a keg, so the chalk's start and the keg's regrow space stay open. A wheel is kept whole
  unless its 150 u sweep would reach a keg's space.
- **Measured** (`grizzly_time_course_harness/run.sh --sweep`, 400 seeds at I4): Spearline lays a median 762 prisms a
  lap here; 572 are kept (min 432, max 707); 7.0 ruins a lap (never fewer than 6); 190 of 192 wheels kept.
- **Colour.** Each ruin is one domain colour, Jade, Ruby, Gold in turn, so the ruins read apart from the kegs and chalk
  (Blue). `ruinDomains` on the controller; empty = Blue.
- **Decoration only.** Plain prisms laid with the kegs' prefab under their own owner id, with the game's own prism
  rules: a hull, a bomb or a Rush breaks them and nothing grows back. Nothing reads or writes a vessel. The wheel is
  posed every frame from the network clock, so every peer sees the same angle.
- **Held by** `GrizzlyTimeRuinsTests` (8, in the same harness) and four negative controls.
- **Open, for play to settle.** (1) The wheel sweeps the lanes, as in Spearline, and a bank shot arrives at up to
  350 u/s: it may read as unfair here. (2) The AI does not know the ruins are there; it has not been watched at I4
  with them, and a stall there would hurt step 3. (3) A ruin in a pilot's own domain colour is that pilot's own prism
  to the game. (4) About 570 more prisms on screen at I4: frame rate not measured.

## 1. What the mode is asking

The Grizzly is the fleet's slowest, tightest hull: it cruises at **50 u/s** and turns at
`50 × 0.1 + 90` = **95 °/s** — a **30 u** circle. Everything past cruise is **riding its own
blasts** (`R_VesselActions/GRIZZLY_TRIGGER_BOMBS.md`): LT and RT each fire a bomb, a second pull
freezes it, the release detonates it, and a Grizzly inside its own blast is **thrown away from
the bomb** — so the race is fire, freeze, fly past, blow it behind you. A full squeeze's blast
(scale 200) hands over `200 / 1.2 s × 4.5` = 750 u/s at the bomb, eased 1.5 → 0.5 over a second
and clamped by the launch's own **300 u/s** ceiling (`selfLaunchCeiling` — three times the 100 u/s
every other shove shares; the launch was tripled on 2026-10-08) — so a full launch ridden close
sits on that ceiling for its whole second and carries the hull **~300 u**. Launching flat out:
**350 u/s**, seven times cruise.

That push is a **world-space** velocity (`VesselTransformer.velocityShift`). It keeps going the
way it was thrown when the bomb went off, and the turn rate never sees it. So a corner is one
question:

> **How much launch is this corner worth?**

| launch held | speed | circle it holds | reads as |
|---|---|---|---|
| full | **350 u/s** | **211 u** | riding a full-squeeze blast |
| ~half | ~200 | ~120 | a launch bleeding off |
| none | 50 | **30 u** | coasting — a pivot, once the last launch has carried its ~300 u |

A launch taken into a corner throws you wide of it; a launch saved for after the corner costs the
time it takes to fire, freeze and ride the next bomb. **Ammo, not the ceiling, sets the sustained
pace:** a full launch costs 0.35 of a pool that refills at 0.15/s, so a pilot who launches as
often as the pool allows averages ~180 u/s (cruise plus ~130 u/s of launch carry) and bursts to 350
— the opening full pool is worth three launches. That is the Manta's Soar trade in Redline, with
the boost meter replaced by a magazine.

**Intensity is how many corners a lap asks that question at.** Measured over 400 seeds, the
median lap (`GrizzlyTimeCourseTests`):

| intensity | corners that cost launch | hardest corner | 2nd | 3rd | mouth radius | lap |
|---|---|---|---|---|---|---|
| 1 | **0** of 8 | 321 u · 100% | 387 u · 100% | 487 u · 100% | 96 | ~4.7 k u |
| 2 | **1** of 8 | 161 u · **76%** | 299 u · 100% | 353 u · 100% | 72 | ~4.9 k u |
| 3 | **2** of 8 | 136 u · **64%** | 149 u · 71% | 306 u · 100% | 58 | ~5.1 k u |
| 4 | **3** of 8 | 98 u · **47%** | 115 u · 54% | 190 u · 90% | 46 | ~5.2 k u |

Percentages are of the 350 u/s top speed via `FastestSpeedForCorner`. A two-lap race is
~9.4-10.4 k u. Pace has two limits: recorded flights are CONTROL-bound (the ammo meter sat at
0.66-0.81 - pilots were not spending it), and a pilot who launches as often as the pool allows is
AMMO-bound at about 4.7 s a gate, ~75 s a race. The mouth radii in the table are the first cut's;
since 2026-11-11 they are 96 / 84 / 74 / 66 (§0). The first cut (2026-10-06) was fourteen gates on a 560 u ring against a
150 u/s top speed; tripling the launch (2026-10-08) made every one of those legs shorter than a
single launch's carry, so the circuit was re-cut (§3).

## 2. What the mode does NOT add

No new weapon, no new ability, no cell of its own, no new scoring metric, and **no circuit
generator**. It reuses:

- `ScoringMetric.SwitchesThreaded` (9), the `BestByDomain` fold, the goal-stack row, the objective
  icon — all keyed on the metric.
- `GateRaceScoringRuleSO` — one class, one asset per mode (`GrizzlyTimeScoringRule.asset`).
- `GateRaceController` and the whole gate-race platform (course broadcast, rings, crossing
  detection, owner-detects/server-records, AI steering, final scores).
- **`HeadlongCircuit`**, the shared closed-circuit solver. `GrizzlyTimeCourse` supplies the
  Grizzly's settings exactly as `RedlineCourse` supplies the Manta's; the solver is untouched
  (Redline's 14-test suite still passes against it).
- The Grizzly's shipped kit for the racing: the trigger bombs are the throttle; **Rush** (its Time
  ability, a ram since 2026-11-16) is a burst of about 76 u that smashes the prisms the hull ploughs
  into, with three banked charges, each paid for in Energy (it starts at 0, so the first Rush comes
  about 5 s in, and then one per 5 s); **Dig In** is a hard stop that braces the cannon; and a bomb
  blown across a rival's trail breaks it (the blast spares only your own domain). None of that is
  scored; the race is. The mode reads the kit and never changes it (§0.1: a Rush opens a keg).

## 3. The cut — Headlong's octagon, against the 211 u circle

A corner's radius on this metric is `(shorter leg / 2) / tan(turn / 2)`. With the launch tripled,
the Grizzly's full-launch circle is **211 u** and a full launch carries **~300 u** — the Rhino's
territory, so the cut is Headlong's, re-measured with the shipped solver compiled out of editor:

- **Eight gates a lap** on Headlong's **800 u** base circle, two laps a race (16 gates). Legs of 475–610 u at the median: one launch's carry and half again on every leg
  (`Legs_are_long_enough_to_launch_down`). The old fourteen gates on 560 u left 250–420 u legs —
  shorter than one launch now carries — and no shorter lap fits a hairpin with long enough legs
  inside the 480–1080 shell.
- **The reach dial is `AngularSpread`** — REDLINE.md §4's finding, again. Profiles were swept over
  four spreads per level and picked so each level's hardest corner costs ≥ 8 points more than the
  last: level 2 asks one 110° corner and needs spread 4 to make it cost anything; level 3 asks
  150° + 135° half a lap apart, which pull the ring into a lens at spread 2 on their own; level 4 is
  140° + 135° + 70° at spread 4 — a triangle with three braking zones.
- **`RadialSwing` 0.42**, Headlong's.

The safety floor is stated in absolute Grizzly units (`CornerFloorRadius` 169 / 106 / 95 / 89 u),
never below the 30 u cruise pivot. Presentation caps (50 / 70 / 72 / 76°) each sit over the
level's measured worst half-turn (44 / 68 / 68 / 73°). Mouths were Headlong's (96 / 72 / 58 / 46)
until the kegs; they are 96 / 84 / 74 / 66 since 2026-11-11 (§0).

## 4. The AI bomb-jumps — a vessel change, not a mode one

`AIPilot` writes the stick and the throttle and nothing else, so an AI Grizzly can never press a
trigger. In a race cut around the full-launch circle that would make every bot a 50 u/s obstacle —
and an all-AI domain that cannot play is a defect. `GrizzlyTriggerBombExecutor` carries an
**autopilot drive** (Redline's autopilot Soar, REDLINE.md §5, on the Grizzly's kit): while
`AIPilot.AutoPilotEnabled` it fires a full bomb (about every 2.3 s), freezes it 18 u ahead, flies past it and detonates it 10 u behind — thrown forward, away
from it — through the REPLICATED press/release, so every peer runs the same bomb. Gated on the pilot being an autopilot,
so the menu's lava-lamp Grizzly and a released companion bomb-jump too. `0` disables the drive.

Two prefab defects had to go for any of that to matter (both on `Grizzly.prefab`, both from the
pre-restoration prefab):

- **`AIPilot` was serialized disabled** (`m_Enabled: 0`). `StartAIPilot` does not enable the
  component and steering runs in `Update`, so an AI Grizzly never steered — in Grizzly Charge too.
  Every other hull ships it enabled. Now enabled; the generator asserts it.
- **The stick band does not gate the AI.** `aiFireStickBand` (0.35) reads `input.XSum/YSum`, which
  `AIPilot` never writes for a single-stick hull, so the AI fires mid-corner too (proposal P1 in
  POWDER_KEGS_SPEC.md §5, for the vessel's owner). The AI's throttle does not set its speed either:
  `SingleStickVesselTransformer` ignores it.

AI approach numbers are Redline's ratios on the Grizzly's 211 u circle: commit **380** / lead
**420** / through **285** (Redline: 420 / 480 / 320 against 237 u).

## 5. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (laps × rings) | `Resources/EndConditionOverrides` → `grizzlyTimeGateTarget` | **16** |
| laps | `MinigameGrizzlyTime.unity` → `GrizzlyTimeController.laps` | **2** |
| powder kegs (on/off, prism, burst, regrow, crown, toasts, sounds) | scene → `GrizzlyTimeController` keg block | §0 |
| keg ladder (credit, regrow, core, chalk) and geometry | `GrizzlyTimeKegs` | §0 |
| rings per lap / base circle | `GrizzlyTimeCourse.GatesPerLap` / `.BaseRadius` | **8** / **800** |
| corner profile, spread, floor, mouth, presentation | `GrizzlyTimeCourse.ForIntensity` | §1, §3 |
| AI commit / lead / through | scene → `GateRaceController` | 380 / 420 / 285 |
| detection clamp | scene → `maxPlausibleSpeed` | **1400** (4× the 350 top speed) |
| autopilot bomb-jump | `GrizzlyTriggerBombConfig.asset` → `aiFireStickBand` / `aiFreezeDistance` | 0.35 / 18 |
| launch strength / ceiling | `GrizzlyTriggerBombConfig.asset` → `selfLaunchMultiplier` / `selfLaunchCeiling`; `maxBlastScale` | 4.5 / 300; 200 |
| ammo (the sustained pace) | `Grizzly.prefab` Ammo `resourceGainRate`; `maxAmmoCost` | 0.15/s; 0.35 |
| comeback rate | `ArcadeGameGrizzlyTime.asset` | **0.45** (4 gates behind buys 1.8 levels) |
| course shell | scene → `courseOuterRadius` / `courseInnerRadiusFallback` | 1080 / 480 |

Every vessel number the cut depends on is restated as a constant on `GrizzlyTimeCourse` and
**read back off `Grizzly.prefab`, `GrizzlyTriggerBombConfig.asset` and
`AOEGrizzlyExplosion.prefab`** by
`Course_constants_match_the_shipped_Grizzly_and_its_launch` — retune the hull, the bombs or the
launch and the test names the constant that moved.

## 6. Assets — all authored by `Tools/Build/author_grizzly_time_assets.py`

The scene is cloned once from `MinigameRedline` (controller script, field block, fresh project-
unique `GlobalObjectIdHash`es) and then adopted as committed (`arcade_mode_lib.committed_scene`).
The generator authors the card, the scoring rule, the preview (`ModePreview_GrizzlyTime`,
registered in `Resources/ModePreviewLibrary`), the build-scene row, the always-unlocked row, both
rosters (`OrganicRematchGames` + `ArcadeGames`), the end-condition target, the toast config
(`GameToastConfig_GrizzlyTime`) and the controller's keg block in the scene, and asserts the
keg ladder and mouths against the C#, the toast ids,
comeback rate, the lap arithmetic, the constants it shares with the C#, the AI prefab state, the
autopilot bomb-jump (on, and freezing inside its own blast) and the toybox roster. `--check` diffs every file.

## 7. In-editor verification

1. **Arcade card.** Menu → Arcade: **Grizzly Time** and **Grizzly Charge** both appear and are
   clickable on a fresh account; Grizzly Time's launch panel pins the vessel to **Grizzly**.
2. **Launch at intensity 1, 2 players.** Eight rings bloom in a closed loop; your next gate is
   lit and the objective arrow points at it.
3. **Bomb-jump a lap.** Fire, freeze just ahead, ride the blast; alternate LT/RT. Each launch
   should throw you to ~350 for about a second and carry you ~300 u. At intensity 1 every corner
   can be taken with a launch running.
4. **Lap wrap.** After gate 8 the lit ring returns to gate 1 and the goal row reads 8/16.
5. **Intensity 4.** A median lap is a triangle: two near-hairpins that punish launching into
   them (you swing wide of the next mouth) and a third you can hold with most of a launch.
6. **AI.** Add AI Grizzlies: they must **steer** (the `AIPilot` enable), **fire, freeze and ride
   bombs on the straights** (the autopilot bomb-jump), and complete more than one lap.
7. **MPPM two clients.** Both see the same circuit; a client's gate reports are credited once;
   each peer sees the other's bombs at matching sizes.
8. **Regressions.** Redline and Headlong unchanged; the menu lava-lamp Grizzly now bomb-jumps —
   if that reads too busy, the dial is `aiFireStickBand` (0 disables).
9. **The kegs** (the `Docs/UNITY_VERIFICATION_CHECKLIST.md` entry for 2026-11-11 lists each step):
   a full blast opens a plain keg and clears its gate; at I3 the first blast only strips the core and
   it re-shields 2.5 s later; the chalk reads as a lane; the bank-shot toast fires on a blow from
   the exit side (you between the keg and the next ring); AI Grizzlies open I1-I2 kegs and finish at
   I1-I4. The 2026-11-20 entry adds the Rush: a ram opens a keg on every peer, and the AI Rushes.

## 8. Known limitations / follow-ups

- **Not editor-verified.** Authored headless: the course and its test suite were compiled and
  run out of editor (Roslyn, against the card-art harness's Unity shim) over 400 seeds × 4
  intensities, and the suite was watched failing under a mutated course. Nobody has flown it.
- **The launch curve is the steady-state circle.** A launch keeps the direction it was thrown in
  when the bomb went off, so a turn taken mid-launch slides wide of 211 u; the level-4 corners sit
  well inside it (≤ 115 u at the median), but level 2's single corner (161 u) is closer to the
  line, so a play-test may want it tighter.
- **The launch cannot be steered.** At 300 u/s of world-space push, a launch aimed badly carries
  the hull ~300 u the wrong way. The mouths are Headlong's for that reason; if threading still
  reads as luck, the dials are `RingRadius` or `selfLaunchSeconds` (shorter launch, same peak).
- **Ammo sets the sustained pace (~180 u/s), and it is a first-pass number.** If races feel like
  coasting between launches, the dial is the Ammo `resourceGainRate` on `Grizzly.prefab` (0.15/s)
  or `maxAmmoCost` (0.35); neither moves the corner ladder, which is cut against the ceiling.
- **Card art** is the `/cardart` COURSE-tier render (`CardBackgrounds/GrizzlyTime.png`): the
  shipped `GrizzlyTimeCourse` at intensity 2 through the card-art harness, the `GrizzlyTime` branch
  of Redline's recipe in `render_card_backgrounds.py`. Judged at card size; not yet seen in the grid.
- **Toasts** are `GameToastConfig_GrizzlyTime` (since 2026-11-11): the race beats 129-132 + 30, the
  hint (300), the bank shot (301) and the rammed-open keg (302, since 2026-11-20).
- **The AI Rushes on a straight** (since 2026-11-20, §0.1) and never uses the charged cannon; its bombs are always full-size.
