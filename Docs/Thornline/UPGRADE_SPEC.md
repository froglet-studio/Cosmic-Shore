# Thornline upgrade: "the honest line" (2026-11-17)

The Shrike's Time game (`GameModes.Thornline = 79`, #1800) upgraded in place for Yash's round 5 ask ("make every
vessel's Time minigame super fun"). Garrett owns the Shrike; nothing here changes the vessel or its kit numbers, and
every kit-side finding is a proposal to him (§6). Mode doc: `Assets/_Scripts/Controller/Arcade/THORNLINE.md`.

Fun estimate **5.7 -> 6.7** (Verb 9, Flow 8, Ladder 8, Feedback 6, Rivals 5, Fairness 4). Nothing was run in Unity.

## 1. How this spec was made

Five readers (the Shrike kit; rounds 3-5 of the Time-game upgrades; the fun bar and recorded play; the `/arcadegame`
in-place recipe; what a course can be built from), three designs (verb first "Briar Run", spectacle first
"Gunsight", rivals and fairness first "Pack Line"), and three judges (player, engineer, rules and AI). The engineer
and the rules judge put Pack Line first (7.5) with Gunsight's sight grafted on; the player put Briar Run first and
asked for its power curve later. Every claim below was then measured in the offline harness, and the ones the numbers
called fake were dropped (§5).

## 2. What the readers found (the floors' real causes)

1. **The sling catches a 12 u disc, not the thorn.** `ShrikeExecutor.FindSlingAnchor` takes a block only when its
   CENTRE lies within `dockQueryReach` 12 u of the ray (its own tooltip says that reach "must cover the largest block's
   half size"); thorns are 32-84 u. A person aiming at the visible edge presses and nothing happens.
2. **The I2/I3 rim crystal anchored on nothing.** Its aim point sat 0.375 x the thorn's side off the centre (17 / 15 u
   at I2 / I3), outside the 12 u. The rung's one new idea did not work.
3. **The AI could not race.** `TryOverrideAim` returns before any difficulty code runs, so every AI flew alike; with
   no nose lead, AIPilot's cubic stick (114.6 e^3) closes the last degree onto a 12 u disc at 1,000 u very slowly. In
   the replica (§4) the shipped law finishes 14 of 20 I1 races (median 216 s) and 4 of 20 at I4, wasting 9-14 s per
   sling. This answers the open question from round 5: `AIPilot` cannot hold the aim Thornline asks for, unaided.
4. **No equal start.** The start thorn sat on the pole and the seats on the platform's equatorial ring; whether that
   was inside sling range was "assumed, not measured".
5. **Soft-lock paths.** A broken thorn regrew only once no hull was within `size + 30` u (a hull hanging at the face
   held it off), and the AI could aim at a broken thorn.
6. The 17 / 29 / 36 % "reading the course saves" figure was not the strict metric: aim time was not counted and
   taking a snipe or crystal costs nothing, so "always take everything" equals the reader.

## 3. The design (built)

| Feature | Rung | What it is | Proved by (offline) |
|---|---|---|---|
| **Honest rim mark** | I2+ | The crystal sits 0.9 down its leg on the line to a point 8 u off the thorn's centre (inside the 12 u), so the shot through it anchors even from a dock up to ~30 u off the last thorn's centre; the centre shot passes it by 7.2 u. | `The_shot_through_a_crystal_anchors_and_the_centre_shot_misses_it`; controls: rim 17 u (the old geometry) and rim 0 u both fail |
| **Equal start** | all | `ThornlineController : IPlayerSpawnLine`. Every seat 640 u from the start thorn, 30 deg below it, facing it: one sling for everyone at the gun. The walk keeps every later thorn and ring off the seat cone. | `Every_seat_is_the_same_sling_from_the_start_thorn_and_its_line_is_clear` (0/400 walk failures at every rung, 128 attempts); control: the cone guard removed fails |
| **AI lanes** | all | The AI aims at one of 8 lane points 6.5 u off the thorn's centre: inside the 12 u it anchors on, outside the 3.5 u the autopilot counts toward its lance (Charge 0), spread by seat so two AIs fly different lines. | `Every_lane_anchors_and_keeps_the_target_off_the_lance_line`, `Two_seats_on_one_leg_fly_different_lines`; controls: lane 2 u and lane 13 u fail; pack centre-aim breaks the ordering |
| **AI plan** | all | `ThornlineAIPlan` (pure, shared with the replica): snipe (Hard+), crystal shot (Legendary), next thorn, back to the leg's start, the stall rescue; never a broken thorn. | `ThornlineAIPlanTests` (every aim anchors on the thorn it means at every rung and level; never a broken thorn) |
| **AI aim by difficulty** | all | A nose lead set on each AI at arming: 2.5 / 3 / 8 / 16 (Easy..Legendary), capped 10 deg past the aim, the fleet nose lag. Difficulty is aim and reading, never a delay (§5). | `run.sh --ai --check`: 100/100 finish every rung at every difficulty, solo and in a pack of four, ordered; controls: no lead, and gain 1, both fail |
| **Exact regrow** | all | A broken thorn waits only for a hull INSIDE its box, and a regrown thorn is a live ribbon (never holds the arena-ready gate). | `A_broken_thorn_waits_only_for_a_hull_inside_it`; control: the old sphere fails |
| **The sight** | all | A faceted dome on the next thorn's face, sized to the catching disc as seen from the hull (never bigger), grey and growing while the sling reloads, Lime when ready. | Unity only (checklist) |
| **Rival glow** | all | The thorn a rival sits on glows faintly in that rival's colour (PrismLit, restricted to the Blue thorns). | Unity only |
| **Beats** | all | Toasts 420-426: hint, snipe, crystal reset, "{0} took the crystal", caught short on a trail, "Past {0}", "{0} slung past you". Nine FMOD slots, all empty. | generator `--check` (enum values, defaults, exposed EventReferences) |

### Measured (offline, `Tools/Build/thornline_course_harness/run.sh --ai --seeds 100`, 2026-11-17)

Median race, seconds (solo / a pack of four):

| | Easy | Medium | Hard | Legendary |
|---|---|---|---|---|
| I1 | 64.5 / 68.4 | 56.4 / 59.0 | 49.3 / 49.5 | 49.3 / 49.4 |
| I2 | 72.0 / 89.6 | 62.7 / 82.2 | 57.8 / 70.0 | 52.7 / 58.1 |
| I3 | 72.8 / 87.3 | 63.0 / 80.6 | 54.6 / 59.5 | 49.3 / 54.5 |
| I4 | 88.2 / 103.0 | 73.4 / 94.2 | 57.9 / 70.0 | 53.5 / 59.2 |

Every seat finishes every rung (100/100). Aim overhead beyond the reload: 0.9 / 0.4 / 0.03 / 0.03 s per sling. In a
pack the slow aimers lance rival trails crossing their swing (Easy 13-16 s a race, Legendary under 2). The match
lands at 50-100 s against the 90 s aim; a first race by a person is unmeasured.

Reading the course, with the aim modelled (`run.sh --read`, Legendary's aim, 100 seeds): taking snipes and crystals
saves **9 / 15 / 19 %** at I2-I4 over docking every thorn. The strict metric (against the best fixed habit) is **0 %
by construction**: a seen snipe or crystal is never a loss, so "always take everything" is the reader. The skill the
course rewards is the shot, not the choice.

## 4. The replica (what it is and is not)

`RaceReplica.cs` flies whole races through the SHIPPED `ThornlineAIPlan` and `NoseLeadSteering`, with the Shrike's
sling, reload, dock bonus, crystal reset, Whipcrack, detach (the undock push, the dock grace, contact docking only
within reach), the lance count on the nose line and rival trails. `run.sh --self-test` greps the prefab, the config
and AIPilot for the numbers it copies. **Not modelled:** AIPilot's orbit break-off and emergency steering,
Charge-mode paint and missile diversions, the nest a detach leaves, network latency, a crystal tunnelling at sling
speed, and what a hull drifting into a thorn's collider does. Those are the editor checklist's.

## 5. Tried and dropped by the numbers

- **A reaction delay per difficulty** (d3's hesitation, d2's reaction): the autopilot detaches the moment it is
  reloaded after `aiDockSeconds` 3 s, and a plain thorn's reload is 3.1 s, so any delay after the reload detaches at
  every plain thorn and the AI then aims at a third of the turn rate. A delay applied only after the reload did
  nothing at all (the nose is on the thorn that frame and the autopilot slings). Difficulty is the nose lead instead.
- **Lanes at 8.5 u** (d3): quieter lance, but 3.5 u of outward slack cost Easy and Medium 25-40 % of their race. 6.5 u
  measured best overall.
- **A nose-follow change by difficulty** (d2 follow 6-8): a faster follow divides the effective lead
  (`EffectiveNoseLeadGain`): lead 4 with follow 6 finished 1-6 of 20 races, and no lead with no lag 2-12 of 20.
- **The stall rescue's claim**: its negative control does not fire offline (no race in the replica stalls 15 s). It is
  kept as unproven insurance for what the replica does not model, and labelled so.
- **From the designs, not built** (deferred, per the engineer judge): Briar Run's briars, spurs and guard thorns, a
  card start of Time 4 for Whipcrack, 24 rings; Gunsight's lock and blocked sight states, beat streaks and finish
  chase; Pack Line's contested-crystal catch-up claim (rests on in-flight pickup at 25 Hz physics, unprovable
  headless). Dropped as false premises: a "course domain" (Blue is never a racer's domain, so the thorns already count
  the same for everyone), and a 290 u hold point exploiting the autopilot's 300 u gate.

## 6. Proposals for Garrett (the Shrike's owner; none built)

1. **`dockQueryReach` 12 against 32-84 u blocks.** Its tooltip says it must cover the largest block's half size. The
   mode is built on 12 (the sight, the lanes, the rim mark) and `Course_constants_match_the_shipped_Shrike` pins it, so
   a change shows at once. Raising it (or testing the box) would make the drawn thorn honest for every Shrike mode.
2. **The autopilot's detach.** It presses Mass at 3 s docked once reloaded, aimed or not, which is what made every
   per-difficulty delay fake. Detaching only when no target lies inside its fire cone (or an `IShrikeRaceCoach` seam,
   the Jacks/Goby/Orca shape) would let a race set a reaction.
3. **Whipcrack (Time L5, 0.6 s slings) is out of reach in Thornline** (the card seeds no Time; three crystals reach
   Time 3). A card `StartingElements` of Time (Redline's pattern) is a mode-side option; it was not measured here.
4. Charge-mode diversions (a crystal grows a flora with missiles; the autopilot then paints any rival in its 8 deg
   cone) are unmeasured. Yash: no learner reads the Shrike (`Shrike.prefab` carries `SparrowAIBoostPolicy`).

## 7. Owed to the editor

`Docs/UNITY_VERIFICATION_CHECKLIST.md`, "Thornline upgrade (2026-11-17)".
