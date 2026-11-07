# Slip Line - the Honeyguide's Time race

> **Status.** Authored headless on 2026-11-07 (`claude/honeyguide-slip-line`), for Garrett's ask that every vessel get a
> Time game that suits it. The course, the AI's rail choice and their tests were compiled and RUN outside the editor
> (400 seeds per intensity for the tests, 200 for the clock sweep, `Tools/Build/slip_line_harness/run.sh`), against the
> real Ratel AI (`DynamoCore.AIPlan`, its skills read off `HoneyguideConfig.asset`). The mode compiles against Unity's
> reference assemblies. **Nothing here has been run in the Unity editor, and nobody has flown it.**

`GameModes.SlipLine = 82` is the **Honeyguide's Time card**.

## 1. Why this race, for this hull

The Honeyguide (`Crew/HONEYGUIDE.md`, class 23) is a two-seat crew vessel. The pilot flies; the **Ratel** seat makes
patterns on the Dynamo, and each pattern puts a **charge** in the shared battery: a colour, Ruby, Jade or Gold, which the
Ratel does not choose. The battery holds 3; a fourth pushes the oldest out.

The pilot's Time ability is the **Slip**: touch a prism trail of a colour the battery holds, nose along it (inside
45 degrees, within 5.5 u), and the hull zips down it to its end at **480 u/s**, eight times its 60 u/s cruise, and the
charge is spent. A slip reaches at most 400 u.

So the Honeyguide's clock is not its throttle. It is **how many legs it crosses at the zip's speed**, and that is set by
two things the pilot does: **catching** a trail (getting onto its line, inside the aim cone) and **spending** charges on
the trails worth the most. Slip Line lays the trails itself, as **rails** down the legs between rings, and builds each
level on one of those two skills.

The vessel kit is untouched (the LOCKED mode rule). The mode's levers are the rings, the rails and the AI's coach.

## 2. The ladder (`SlipLineCourse.ForIntensity`, held by `SlipLineCourseTests`)

16 rings, flown once. Every rail ends 40 u past its ring, half a ring-radius off its centre, so a ride threads the ring
and lands just past it. Rails sit on the side of the ring away from where the next leg leaves, so the line flown ring
to ring never touches a rail (a test holds it: a rail you did not mean to catch is never in your way).

| | I1: catch | I2: spend | I3: lean | I4: chain |
|---|---|---|---|---|
| rails a leg | three, one of each colour | one | one | one |
| rail length | 260-370 u | half 260-370, half **stubs** 100-120 | as I2 | 40% stubs |
| rail line | along the leg | along the leg | **leans 15-25 degrees** off it | as I3 |
| chains | none | none | none | every 4th leg: **two rails, two colours, one ride for two rings** |
| ring radius | 72 | 66 | 60 | 54 |
| legs | 400-470 u | 400-480 | 400-500 | 400-520 (a chain's second leg 270-320) |

What each level asks:

- **I1, catch.** Every leg has a rail in every colour, so any charge rides. The lesson is the slip itself: fly onto a
  rail's line, nose along it, and feel the zip.
- **I2, spend.** One rail a leg, in one colour. Ride only when the battery holds that colour, and **a stub is worth
  about a second; a long rail about four**. Holding a charge past a stub for the long rail ahead is the decision.
- **I3, lean.** The rails swing out off the line by 15-25 degrees, so meeting one inside the aim cone means turning
  out early. A lazy approach misses the cone and flies the leg.
- **I4, chain.** Every fourth leg is a chain: the first rail's ride lands on the start of a second rail of another
  colour, which carries on through the next ring. Hold both colours and one ride takes two rings. The rings are the
  smallest.

### What it measures (offline, 200 seeds, `run.sh --sweep`, the real Ratel AI)

| | flier (never slips) | greedy rider | reader (the AI) | reader, Easy Ratel |
|---|---|---|---|---|
| I1 | 117.3 s | 87.7 s (6.9 rides) | 87.7 s | 95.9 s |
| I2 | 119.0 s | 103.7 s (5.8 rides) | 103.0 s (5.4 rides) | 106.1 s |
| I3 | 122.0 s | 107.3 s (6.0 rides) | 106.4 s (5.6 rides) | 110.0 s |
| I4 | 115.7 s | 98.9 s (5.2 rides) | 98.3 s (5.0 rides) | 101.9 s |

Read honestly:

- **Riding is the race.** A pilot who rides beats one who never slips by 13-25% at every level.
- **The Ratel matters.** A Hard Ratel (4.3 charges a minute) against an Easy one (3.0) is 3-9 s a race, so the second
  seat is a real part of the time.
- **Spending well is worth about 1%** (I2 1.1%, I3 0.6%, I4 0.9%, the reader winning about twice as many races as it
  loses). Charges are scarce (about one every 15 s against a rail every 7 s), and colours come at random, so a held
  charge often never meets the long rail it waited for. I tried three things to make it bigger: a 3-leg horizon, a
  rest-of-race "n-th best ride" rule (shipped), and shorter stubs (100-120 u, shipped). Each moved the gap by tenths
  of a percent. **The decision is real but small**, and making it big would mean more charges or chosen colours,
  which are the Ratel's kit, not the mode's.
- **What the clock does not model is the catch.** It assumes every planned ride is caught. A person's real time
  depends on getting onto the line inside the aim cone, which is where I3's lean and I4's chains bite. That is the
  skill the ladder is built on, and it can only be measured by flying it.

## 3. The rails

One trail per rail, laid by `SlipLineController.OnCourseRaised` from the broadcast rings: environment prisms
(`SpawnablePrism`), **super-shielded** so no hull or Peck breaks one mid-race, every 20 u along the rail, each 2.5 x 2.5
x 14 and turned along it (the slip's aim cone reads the prism's forward). Colours are the Dynamo's (Ruby, Jade, Gold:
`HoneyguideConfigSO.DomainOf`). Every peer lays the same rails: the colours and lengths hash each ring's exact axis,
which the broadcast carries bit for bit, so a rail does not move with the cell (a test holds it, and a negative
control that hashes on the position instead fails it).

A rail in a colour you do not hold is a plain prism wall to the hull, as any trail is. The ring-to-ring line never
touches one.

## 4. The vessel change (HoneyguideCrew; the Honeyguide is Garrett's)

One, for the AI only: **`HoneyguideCrew.RaceCoach`** (`IHoneyguideRaceCoach`). The Honeyguide AI's slip guard
(`AISlipHelps`) takes a slip only when the landing is nearer where the AI is going, and "where it is going" was the
pilot's objective. In this race the coach steers the AI onto its rail, so the objective is a point on the rail and every
ride looked like a step backwards. The coach now answers "going": the ring after the rail it rides. A person's slips
never ask it, and with no coach set the AI is exactly as shipped.

`BatteryCap` and `RaceHull` were added as read-only accessors for the coach.

## 5. AI

The AI flies the gate race's own steering, except while it means to ride:

1. At the start of each leg it asks `SlipLineCourse.ChooseRail` (once per leg, from where the hull is): ride a rail of
   a colour the battery holds when the ride saves at least 1 s and at least 70% of what that charge would buy later
   (with n charges of that colour held, the n-th best ride of that colour left before the finish; none left: spend it).
   A full battery always rides (the oldest charge is about to be lost). A chain's first rail counts its second.
2. It steers at a lead point 40 u down the rail past its foot on the line (`RailPursuit`, `aiRailLead`), so it closes
   onto the rail already running along it, inside the aim cone.
3. The Honeyguide's own slip catches the rail and spends the charge. The coach only says where.
4. If the charge goes (a Ratel card spent it) or the hull runs past the rail without catching it, the AI flies the leg.

The Ratel seat is the shipped `DynamoCore.AIPlan`; its difficulty (`engineerLevel`) is the vessel's.

## 6. Numbers, and where they are authored

| Knob | Where | Shipped |
|---|---|---|
| race length (rings) | FrogletTools > Game Modes > End Game Conditions, "Slip Line - Gate Target" (`slipLineGateTarget`) | **16** |
| the ladder | `SlipLineCourse.ForIntensity` | §2 |
| Honeyguide numbers the cut depends on | `SlipLineCourse` constants, read back off `HoneyguideConfig.asset` and the prefab by `The_course_is_cut_for_the_shipped_honeyguide` | reach 400, zip 480, ramp 0.15, exit 8, contact 5.5, cone 45, min trail 6, cruise 60 |
| the AI's lead | scene > `aiRailLead` | **40** |
| detection clamp | scene > `maxPlausibleSpeed` | **1400** (a 480 u/s zip plus its exit boost, twice, with room) |
| comeback rate | `ArcadeGameSlipLine.asset` | **0.25** (a Time level does not change a ride; the Slip is base kit) |

## 7. Assets

All authored by `Tools/Build/author_slip_line_assets.py` (on `hull_time_race.py`, Thresher Time's scene as the donor):
the card, the scoring rule (`GateRaceScoringRuleSO`), the scene, the preview, the toasts, the registrations. `--check`
is the drift gate. Card art: `render_card_backgrounds.py SlipLine` (an I4 course with its rails).

## 8. In-editor verification (owed)

1. Open the scene once and save it (it is a text clone); then `author_slip_line_assets.py --check`.
2. I1, one Honeyguide with an AI Ratel: three rails a leg in Ruby, Jade and Gold. With a charge, fly onto the rail of
   that colour nose along it: the hull zips to just past the ring, the ring counts, the charge goes.
3. Fly ring to ring without slipping: the hull never touches a rail.
4. I2: one rail a leg; stubs are visibly short. A rail you hold no charge for is a wall if you fly into it.
5. I3: the rails lean off the line; a straight approach misses the aim cone, a turn out early catches it.
6. I4: a chain's first ride lands on the second rail and rides on through the next ring when the battery holds both
   colours.
7. Two AI Honeyguides: they ride rails and finish; note any leg where one circles a rail without catching it (the lead
   `aiRailLead` is the knob).
8. MPPM two clients: both see the same rails in the same colours.

## 9. Known limitations

- **The spending decision is small (about 1%).** See §2. It is real, the AI makes it, and a person can learn it, but
  the race is mostly the catch.
- **The clock assumes every planned ride is caught.** The catch is the skill the levels build on and it is unmeasured
  until someone flies it.
- **A person playing the Ratel** changes the charge rate; the clock uses the AI Ratel. A good human Ratel should beat
  the Hard AI's 4.3 a minute.
- **Ratel cards that spend or recolour charges** (Void Jumper, Reality Detangler, Plankter Sail) are the kit and are
  left on; they change what a leg can ride.
