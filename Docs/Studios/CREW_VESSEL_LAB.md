# Crew Vessel Lab

A `/labmaker` browser studio for the first **multiplayer vessel**: one hull, two seats. The question it
puts to the designer, from the brief ("Maverick and Goose"):

> Make a vessel that is meant to be played by several players. Start with a pilot and a navigator: a
> player who does not want to process a 3D environment plays a 2D puzzle while the other player flies.
> If there is no other player, an AI flies. The D-pad swaps players between flyer and puzzler, as if
> they had another squad mate.

The lab does not decide this. It puts four navigator consoles side by side against a solo pilot, lets
you sit in either seat with an AI squad mate in the other, and scores every console headless.

- **Source (the page is published from it):** `Docs/Studios/CrewVesselLab.html`. Open it in Chrome to use a
  gamepad (the claude.ai viewer can block the Gamepad API).
- **Live page:** https://claude.ai/artifact/JtQptDHQhNdC2CxPmspfct (private to the owner until shared from the page's
  Share menu; decisions recorded there land in the shared log Claude reads at the start of each round).
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/CrewVesselLab.html` (PASS, round 1).

## How to use it

- **Your seat** is in the header: **Fly**, **Navigate**, **Watch** (two AIs) or **Couch** (two people on one
  screen: keyboard or pad flies, mouse or touch navigates).
- **Swap seats** with the D-pad left/right, the 1 / 2 keys or the header button. It is the same gesture as the
  arena hull swap (`PilotSwapGesture`, `PilotSwap.CooldownSeconds` 0.75 s). The AI squad mate moves to the seat
  you leave and keeps its skill.
- **Crew tab:** pick the console under test and your squad mate's skill (Easy / Medium / Hard).
- **Pilot:** A / D or ← / → steer, S or ↓ brake, Space or Shift boost; pad left stick, LT brake, RT boost. On a
  phone, touch the pilot view left or right of centre, and use two fingers to boost.
- **Navigator:** mouse or touch on the console; on a pad, the right stick moves the cursor and A clicks.
- **Decisions tab:** record which console, which AI behaviour and which swap rule you want. Claude reads the
  shared log at the start of the next round.

The pilot view is a heading-up stand-in for the chase camera. Anything outside the sight cone (120°, 200 u)
is fogged out and its crystals are hidden, which is the limit a 3D chase view puts on the pilot. The navigator
console sits beside it (or below it on a phone).

## The four consoles

| Console | What the navigator does | Reads the world? | Economy |
|---|---|---|---|
| **Gem Feed** | Every skim drops a random gem and every crystal a gem of its element into a 6×6 board. Swap neighbours to line up 3+; a match buffs that element for 8 s. | No. Only the board. | Temporary buffs (`ApplyElementalEffect` with a duration) |
| **Scope Call** | A 360° heading-up radar (420 u). Tap a crystal to call it; it becomes the pilot's waypoint. | A 2D map | None: steering calls only |
| **Lock & Reel** | A short heading-up scope (100 u, grows with Space). Hold the reticle on a drifting blip for 1.5 s and the tractor reels it into the hull. | A moving 2D scope | Crystals caught as usual (one petal each) |
| **Power Split** | Six power pips to route between Charge, Mass, Space and Time. | The hull's gauges | Held levels, capped so nothing is held above 10 |

The empty seat (solo pilot) is the baseline every console is measured against.

## The model, and where its numbers come from

| Lab key | Value | Source |
|---|---|---|
| `minSpeed`, `throttleScaler` | 10, 25 u/s | `Sparrow.prefab` `DefaultMinimumSpeed`, `DefaultThrottleScaler` |
| `yawRate` | 80 °/s | `Sparrow.prefab` `YawScaler` |
| `recovery` | 0.02 /s | `ResourceSystem.elementalRecoveryRate` |
| `swapCooldown` | 0.75 s | `PilotSwap.CooldownSeconds` |
| one crystal = one petal | +1 level | `ResourceSystem.PetalNormalized`, `SkimmerAdjustElementLevelByCrystalEffectSO` |
| element bands | base [0,10], overcharge to 15 drains | `ResourceSystem` resting band |
| domain colours | Jade hull, Ruby rival | `OriginalColorSetSO.asset` via `studio-domains.js` |
| everything else | lab | marked `lab:` in the page's `SHIPPED` block |

Element effects follow the fleet's element conventions, as lab stand-ins: **Time** = rate → speed
(+50% at level 10), **Space** = reach → catch reach (+150%), **Charge** = energy → boost per skim (+100%),
**Mass** = size → shrugs off danger-prism slows (−80%).

The AI in both seats writes inputs only: the pilot AI writes `{steer, boost, throttle}`, the navigator AI
moves a cursor and clicks exactly as a mouse does. Difficulty changes what the AI believes and how fast it
reacts (bearing noise, reaction delay, cursor speed and jitter, blunder rate), never the stick
(`/vessel-ai`).

## Round 1 (2026-10-14)

### Scorecard

`runBatch()` defaults: 8 seeds × 120 s at 30 Hz, 8 crews per console. Petals/min is crystals caught per
minute. Percentages are against the solo pilot of the same skill. Hard + Hard unless the column says
otherwise. Re-run with the lab's own **Score every console** button (about 6 s).

| Console | Petals/min | Crew lift | Navigator skill | Pilot skill | AI crew | Rookie rescue | Swap cost | Control | Nav actions/min | Hits/min | ± SE |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Solo (empty seat) | 14.06 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 3.31 | 0.89 |
| Gem Feed | 15.94 | +13.3% | 9.8% | 56.9% | +7.0% | +7.6% | +8.2% | 0 | 24.5 | 3.81 | 1.21 |
| Scope Call | 16.00 | +13.8% | 31.1% | 44.4% | +7.4% | +32.2% | −1.0% | 0 | 18.6 | 5.56 | 1.11 |
| **Lock & Reel** | **30.75** | **+118.7%** | **40.9%** | 56.9% | **+88.2%** | **+208.5%** | −1.5% | 0 | 22.4 | 3.13 | 1.31 |
| Power Split | 16.94 | +20.4% | 4.0% | 61.3% | +14.4% | +12.7% | +2.7% | 0 | 202.9 | 4.13 | 0.68 |

Columns:
- **Crew lift:** Hard pilot + Hard navigator against the Hard pilot alone.
- **Navigator skill / Pilot skill:** Hard against Easy in that seat, as % of solo. "Does the play in this seat show?"
- **AI crew:** two Medium AIs against one Medium AI alone. This is the hull an AI squad mate gives you.
- **Rookie rescue:** an Easy pilot with a Hard navigator against the Easy pilot alone. This is the closest
  stand-in for the brief's own case: a weak flyer helped by a good puzzler.
- **Swap cost:** a seat swap every 15 s, with the human proxy paying a 1 s re-orientation each time, against the
  two seatings held (averaged).
- **Control:** the coupling cut (no gem feed, calls ignored, no lock, no pips). It must read 0, and it does on
  every console, so every lift above comes through the console and not through a side channel of the model.
- **Noise:** the standard error of Hard + Hard over the 8 seeds is about ±6–8% of the value. Differences under
  about 10% are a plateau, not a result.

Sweeps (same scorecard, one knob moved; full tables reproduced with `runBatch` after setting `P`):

| Sweep | Gem Feed lift | Scope Call lift | Lock & Reel lift | Power Split lift | Read |
|---|---|---|---|---|---|
| Shipped | +13.3% | +13.8% | +118.7% | +20.4% | |
| `gemBuff` 1 → 3 | +10.2% | (same) | (same) | (same) | Tripling the gem payout does not move Gem Feed. |
| `kTime` 1, `kSpace` 4, `kCharge` 2, `kMass` 1 | +18.1% (skill 8.6%) | (not run) | (not run) | +37.5% (skill 3.2%) | Stronger elements lift the levels consoles; their navigator skill stays small. Solo rises to 19.69. |
| `crystalCount` 10 → 4 (sparse cell) | −13.8% | +18.6% (rookie rescue +50.6%) | +75.9% | −0.7% | Seeing matters more when there is little to see. |
| `pilotFov` 120 → 60 (narrow chase view) | +2.7% | +24.9% (rookie rescue +61.9%) | +107.7% | +10.0% | Scope Call grows as the pilot sees less. |

### Found

1. **Lock & Reel is the only console that changes what a crew is.** It doubles the hull's catch rate, both seats'
   skill shows (navigator 41%, pilot 57%), and an Easy pilot with a good navigator catches three times as much.
   It works because it is a **second catching channel**: the navigator collects what the pilot cannot reach.
   That is the Goose-shaped job in the brief. It is also the strongest and the most likely to need a ceiling.
2. **A console that pays in element LEVELS is a weak lever, and the navigator's skill barely shows in it.** Gem
   Feed and Power Split both pay in levels. A Gem Feed board pays about 1 level of buff on average, spread over the
   four elements (measured mean 1.06 over a 120 s run, seed 3), which is worth a few percent of speed or reach.
   Tripling the payout leaves its lift inside the noise. Strengthening every element effect (`kTime` 1, `kSpace` 4,
   `kCharge` 2, `kMass` 1) lifts Gem Feed to +18% and Power Split to +38%, but navigator skill still shows only 9% and
   3%: the board's output, not the navigator's play, sets the number. There is a second ceiling over a longer match:
   the pilot's own crystals fill the [0,10] band (levels 7–10 by 120 s in that run), after which a buff can only reach
   the overcharge band, which drains. The element effects here are lab stand-ins, so a real hull's abilities could
   scale harder; the skill finding does not depend on that.
3. **Power Split is busy for nothing.** The Hard navigator clicks about 200 times a minute (the situation's best
   split flips every second) and the gain over an Easy navigator is 4%, inside the noise. Most of its +20% is the
   pips being lit at all. A seat that rewards attention this little is a spectator seat with a chore.
4. **A bad caller costs a good pilot.** Scope Call's navigator skill (31%) is larger than its lift (14%), so a
   Hard pilot following an Easy caller catches less than flying alone (about −17%). The lab's AI pilot obeys every
   call. Whether a call is an order or a suggestion is a design question.
5. **Scope Call is worth more when the pilot sees less.** It is +14% in the shipped cell, +25% with a 60° view,
   and its rookie rescue reaches +62%. In a 3D game, where the chase view really is narrow and behind is invisible,
   it may be worth more than this 2D stand-in shows.
6. **Swapping seats costs nothing measurable** at a swap every 15 s with a 1 s re-orientation (−1.5% to +8%,
   inside the noise on every console). The swap is free to keep as designed.
7. **Two lab bugs were found and fixed before any number above was taken.** The lab hull had no throttle, so the
   AI orbited any crystal inside its 25 u turn circle forever; and its "nothing in sight" search turned in place.
   Per-seed scores ran from 2 to 20 per minute. With the Sparrow's throttle and a roaming search point, a skilled
   pilot's runs sit within about ±15% of each other (L-STU-64).

**Open thread (not a result):** Gem Feed reads −14% in the sparse cell with a small standard error. The control
is 0, so the console causes it, but the mechanism is not shown. One candidate: Time buffs raise speed and with it
the turn radius, which costs catches when crystals are scattered. Check this before reading it as a finding.

### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/CrewVesselLab.html`: PASS (no console errors,
  desktop 1600×900 fits, phone 390 px does not scroll sideways, `__lab` hooks answer, every `SPEC` key in
  `SHIPPED` and inside its range, the manual clock advances, `runBatch` deterministic within a page and across
  two loads, stage not blank). Both screenshots were read; the first pass found the HUD line overlapping the
  console on a phone and the phone page laid out at 980 px (no viewport meta in the raw file). Both are fixed.
- The control column reads 0 on every console in every sweep.
- Per-seed values were printed for every console × pilot skill (10 seeds × 240 s) before the scorecard was trusted.
- Power Split's AI was traced pip by pip over 25 s to confirm the 200 clicks/min is the situation flipping, not a
  stuck click (an earlier verb, "add, stealing from the biggest column", did loop; it now has add and free).

### What this lab does not model

- The pilot view is a 2D top-down stand-in for the 3D chase camera.
- Element effects are stand-ins that follow the element conventions. No real hull's four abilities are modelled,
  and no ability fires.
- One hull, one rival AI, no game mode and no scoring rule.
- The rival's trail recycles its oldest prism. The game conserves mass; this lab does not.
- Human players are proxied in the scorecard by AI skill levels, plus a guessed re-orientation time (`swapOrient`).
- Networking. See "What the game would need" below.

### Decisions needed (designer)

1. **Which console** does the first multiplayer vessel carry? Lock & Reel is the only one that clearly changes the
   crew; Scope Call is the purest "read a map, call the shot" Goose; Gem Feed is the only one with no spatial
   reading at all. A hybrid is possible (radar calls plus a tractor).
2. **Is a navigator's call an order or a suggestion?** (Finding 4.)
3. **Should a levels console stay in the running?** It is weak and skill-blind (Finding 2). Making it pay more
   means larger or longer buffs, and over a long match it meets the [0,10] band. That band is part of the LOCKED
   elemental economy (buffs are temporary; no sustained mechanism holds above 10), so changing it for a crew vessel
   is a question for you, not a tuning change.
4. **Where does the navigator play?** On the same screen (Couch), or on their own device such as a phone or tablet.
   The lab only models the same screen.
5. **Which hull** carries the first crew? The lab flies the Sparrow's speed and turn rate as a stand-in for the
   fleet's fighter.

## What the game would need (recorded for when a pick graduates; nothing built yet)

- **Two players on one hull does not exist.** Today one `Player` flies one vessel, and the arena's
  `PilotSwap` keeps one pilot per hull by only ever exchanging hulls. A crew vessel needs a seat layer: a hull
  with a pilot seat and a navigator seat, each held by a `Player`.
- **The D-pad is already taken in arenas.** `PilotSwapGesture` reads D-pad left/right (and 1 / 2) for the hull
  swap. A crew vessel's seat swap should extend that ring (every seat in every team hull) rather than add a second
  binding for the same gesture.
- **The console's consequences must run on every peer.** Element levels are simulated per peer and the hull's
  owner simulates its own (`PilotSwap`'s stated limit). A navigator on another machine must send inputs that the
  owner applies and that replicate, never write the hull's state directly (`/vessel` rule 48).
- **Both AIs must stay input-only.** `AIPilot` already fits the pilot seat. The navigator seat needs its own
  input-only console AI, as the lab's does.
- **Score credit for two pilots in one hull** is undefined. `RoundStats` rides the `Player` and hits are credited
  through `VesselStatus.Player`.

## Publishing

The repo copy is the source. Publish it as its own artifact (not inside the Vessel Studio: this is a lab for a
vessel that does not exist yet), with `capabilities: {db: {}, user: {}}` for the shared decision log.
Round 1 is published at https://claude.ai/artifact/JtQptDHQhNdC2CxPmspfct; republish to that URL so the log stays one log.
