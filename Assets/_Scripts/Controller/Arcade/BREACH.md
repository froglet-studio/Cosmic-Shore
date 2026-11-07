# Breach - the Orca's Time race

> **Status.** Authored headless on 2026-11-07 (`claude/orca-convoy`), for Garrett's ask that every vessel get a Time
> game that suits it. The course, the AI's burn and their tests were compiled and RUN outside the editor (400 seeds per
> intensity for the tests, 200 for the clock sweep, `Tools/Build/breach_harness/run.sh`), against the Orca's shipped
> numbers read back off `Orca.prefab` and `OrcaCrewConfig.asset`. The mode compiles against Unity's reference
> assemblies. **Nothing here has been run in the Unity editor, and nobody has flown it.**

`GameModes.Breach = 83` is the **Orca's Time card**.

## 1. Why this race, for this hull

The Orca (`Crew/ORCA_CREW.md`, class 20) is a heavy four-seat crew hull. It cruises at **29.75 u/s** and turns at
about 46 degrees a second. The helm's Time ability is the **burn**: five times the speed (**114.75 u/s**) from a
**3 s tank** that refills at **0.25 s a second**, and only while the burn is let go. Its turn rate grows only a little
with speed (`speed x 0.055 + 44` deg/s), so a burning Orca turns round a circle about **3.5 times as wide** as a cruising
one (radius 131 u against 38).

So the Orca's clock is not its throttle. A full tank crosses 345 u against 90 at cruise, but it takes 12 s to refill,
so a race has about one full burn every 15 s. **The skill is where in each leg the helm spends it**: on the straights,
never into a turn too tight to make at speed, never into something that stops the hull. Breach builds each level on one
reason not to burn.

The vessel kit is untouched (the LOCKED mode rule). The mode's levers are the rings, the walls and the AI's coach.

## 2. The ladder (`BreachCourse.ForIntensity`, held by `BreachCourseTests`)

16 rings, flown once, every leg clear of the nucleus.

| | I1: open water | I2: hard turns | I3: walls | I4: breach |
|---|---|---|---|---|
| ordinary legs | 300-380 u, bend 15-40 | as I1 | as I1 | as I1 |
| hard turns | none | **every 2nd leg: 90-120 degrees, 160-220 u long** | none | every 3rd leg |
| walls | none | none | **every 2nd ring** (3, 5 ... 15) | rings 4, 7, 10, 13: the ring after each hard one |
| wall stands | - | - | 110 u before its ring, 16 u wider | 90 u before, 22 u wider |
| ring radius | 70 | 64 | 60 | 54 |

What each level asks:

- **I1, open water.** Gentle bends. Burn every straight and learn the tank: empty is fine, full is waste, and holding the
  burn on an empty tank keeps it empty (the tank refills only while the burn is let go).
- **I2, hard turns.** Every other leg turns 90-120 degrees and is short. A hull misses a ring when the leg is shorter
  than about `2r sin(turn)`, and r is 131 u burning against 38 at cruise: **burn into a hard turn and you overshoot the
  ring**. Coast the turn, burn out of it.
- **I3, walls.** A **wall** of danger prisms stands square across the line into every other ring, wider than the ring,
  so the hull has to dog-leg round its edge and back in. The wall is rounded on the **inside of the turn** (the side
  the previous leg comes from), so the shortest line is also the one that makes the dog-leg. A danger prism stops the
  hull dead and the throttle takes 3 s to recover. At cruise the dog-leg is easy; burning, the hull cannot turn back in
  before the ring. Or let the crew's guns breach it: the AI gunners shoot hostile prisms, so a wall met at cruise is
  often open by the time the hull gets there.
- **I4, breach.** Hard turns and walls in turn. A wall stands on the ring after each hard turn's, so the hull comes out
  of the hard turn, reads the wall down a full leg, and leaves it on an ordinary one (a test holds that a wall never
  meets a hard turn in or out). The smallest rings, and the widest, nearest walls.

Each wall is a cruise turn and not a burning one: a test holds that a hull at cruise can make the lateral swing round
the wall and back into the ring in the room the wall leaves, and that a burning hull cannot.

### What it measures (offline, 200 seeds, `run.sh --sweep`, the Orca's real numbers)

The clock flies a kinematic hull with the Orca's turn rates, a 0.3 s reaction lag and the exact `OrcaCrew` tank logic.
A wall-plane crossing inside a wall is a dead stop; a ring-plane crossing outside the ring is a miss (the hull turns
round and comes back). Four helms:

| | never burns | holds the burn | pulse (burns each full tank blind) | coach (the AI) | coach vs pulse |
|---|---|---|---|---|---|
| I1 | 178.3 s | 170.0 s | 112.0 s | 110.2 s | 1.6% |
| I2 | 155.9 s | 147.6 s | 107.3 s | 95.8 s | **10.7%** (wins 200 of 200) |
| I3 | 198.7 s | 190.3 s | 152.7 s (2.3 misses) | 136.7 s | **10.5%** |
| I4 | 174.9 s | 166.5 s | 146.9 s (4 misses) | 121.8 s | **17.1%** |

Read honestly:

- **Burning is the race.** Any helm that burns beats one that never does by 35-40%.
- **Holding the burn is a trap.** Holding it down drains the tank once and then keeps it empty: 5% faster than never
  burning. Letting go to refill is the first lesson, and I1 teaches it.
- **Where you burn is worth 10-17% from I2 up.** A blind burner wastes tanks into hard turns and walls and misses rings.
  That is the decision the ladder is built on, and it grows with each level. At I1 there is nowhere bad to burn, so the
  gap is small (1.6%) by design.
- **What the clock does not model:** the AI gunners breaching walls on their own (so the I3-I4 wall cost is an upper
  bound), a person's line (the clock flies the round point perfectly, which is why it shows misses and no wall hits),
  and the shield.

## 3. The walls

Laid by `BreachController.OnCourseRaised` from the broadcast rings: environment prisms (`SpawnablePrism`) in the **Blue
danger** domain, a hex disc of them every 9 u in the wall's plane, each 7 x 7 x 1.5 and turned square to the ring's axis.
A broken wall prism grows back **6 s** later, once no hull is at its wall, so every racer meets the wall and a crew that
breaches it breaches it for itself. Which rings are walled is the ring's index, and the wall stands on the ring's axis, so
every peer lays the same walls from the broadcast rings wherever the cell is (a test holds it). Nothing new is replicated.

## 4. The vessel change (OrcaCrew; the Orca is Garrett's)

One, for the AI only: **`OrcaCrew.RaceCoach`** (`IOrcaRaceCoach`). **The shipped AI helm never burns**: `wantBoost` was
set only when a person held the helm, so an AI Orca flew every race at cruise. The coach is asked only when no person
holds the helm: `wantBoost = RaceCoach != null && RaceCoach.HelmBurns(this)`. With no coach set the AI is exactly as
shipped, and a person's burn never asks it.

`BoostTankSeconds` and `Boosting` were added as read-only accessors for the coach.

**Proposed, not done (Garrett's call):** every other Orca mode has the same AI helm that never burns. Breach's
`BreachCourse.BurnWanted` is general (burn when lined up, coast hard turns and walls); a default coach for the gate races
would give the Orca's AI its Time card everywhere.

## 5. AI

The AI flies the gate race's own steering, with two things from the coach:

1. **Aim.** Into a walled ring, it aims at the **round point** (just past the wall's inside edge) until it is past the
   wall's plane, then at the ring (`AimPoint`, `TryOverrideAim`).
2. **Burn.** `BurnWanted` in order: no burn on a tank under 1 s unless already burning (so it lets go and refills);
   none with the nose more than 8 degrees off the aim; none within 150 u of a hard turn's ring; none into a walled ring
   before the wall is passed, or within 180 u of it.

The Orca's own helm spends the tank. The gunners are the shipped AI gunners.

## 6. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (rings) | FrogletTools > Game Modes > End Game Conditions, "Breach - Gate Target" (`breachGateTarget`) | **16** |
| the ladder | `BreachCourse.ForIntensity` | §2 |
| Orca numbers the cut depends on | `BreachCourse` constants, read back off `Orca.prefab` and `OrcaCrewConfig.asset` by `The_course_is_cut_for_the_shipped_orca` | min speed 8.5, throttle 21.25, boost x5, turn 44 + 0.055/u, tank 3, refill 0.25, danger recovery 3 |
| the burn's thresholds | `BreachCourse` (`BurnAlignDeg`, `HardTurnStop`, `WallStop`, `MinBurnStart`) | 8 deg, 150 u, 180 u, 1 s |
| wall regrow | scene > `wallRegrowSeconds`, `wallRegrowPerFrame` | **6 s**, 40 |
| detection clamp | scene > `maxPlausibleSpeed` | **500** (a 115 u/s burn, twice, with room) |
| comeback rate | `ArcadeGameBreach.asset` | **0.25** |

## 7. Assets

All authored by `Tools/Build/author_breach_assets.py` (on `hull_time_race.py`, Thresher Time's scene as the donor): the
card, the scoring rule (`GateRaceScoringRuleSO`), the scene, the preview, the toasts, the registrations. `--check` is the
drift gate. Card art: `render_card_backgrounds.py Breach` (an I4 course with its walls).

## 8. In-editor verification (owed)

1. Open the scene once and save it (it is a text clone); then `author_breach_assets.py --check`.
2. I1, one Orca with AI crew: hold the burn and watch the tank empty and stay empty; let go and it refills.
3. I2: burn into a hard turn and the hull swings wide of the ring; coast it and the ring is easy.
4. I3: the walls stand before every other ring, rounded on the inside of the turn. Fly into one: the hull stops dead.
   Watch the gunners shoot a breach; the wall grows back 6 s after.
5. I4: a wall on the ring after each hard turn; neither ever meets the other.
6. Two AI Orcas: they burn the straights, coast the turns and walls, and finish. Note any wall an AI hits.
7. MPPM two clients: both see the same walls.

## 9. Known limitations

- **A breach is local.** The crew's prism destruction does not replicate between machines, so a wall one client's gunners
  broke can still stand on another's. The walls are laid identically on every peer, and regrow on every peer.
- **The gunners are unmodelled.** The AI gunners breach walls on their own, so the measured wall cost is an upper bound.
- **The clock flies a perfect line.** A person's real cost at the walls is the line round them, and is unmeasured until
  someone flies it.
- **Every other Orca mode's AI still never burns** (§4).
