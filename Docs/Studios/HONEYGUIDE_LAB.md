# Honeyguide Lab

A `/labmaker` browser studio for the **Honeyguide and Ratel**, the two-seat crew vessel that carries *Slipstream* (the
tabletop game Garrett and friends designed and played from 2019 to 2022). The question it puts to the designer:

> Is the handoff fun? The **Ratel** (the Engineer seat) plays a hex pattern-building puzzle, the **Dynamo**, whose
> injection ports turn 60 degrees every spin. Making a pattern sends one of its prisms, drawn at random, to the battery,
> where its domain is a slip charge, and fires that tabletop card's own effect for the **Honeyguide** (the Pilot seat). The
> Honeyguide is not a Sparrow (round 6): RT is always Peck, a weak attack, and LT always Flit, a short move; upgrade cards take a trigger over for a while. Its Time ability is the **Slip**: touch a trail of a domain you hold with the
> nose near the trail's line, and the hull jumps to the end of the trail it is pointing at.

**The acceptance gate for phase 1:** Garrett plays the Engineer on a phone and records *Sign-off* ("this is fun") on
the Decisions tab. The Unity port (phase 2) starts only after that.

- **Source (the page is published from it):** `Docs/Studios/HoneyguideLab.html`. Open it in Chrome to use a gamepad
  (the claude.ai viewer can block the Gamepad API).
- **Live page:** https://claude.ai/artifact/4B7oZwb5aYxFJR7XKyWx7v (private to the owner until shared; decisions recorded
  there land in the shared log Claude reads at the start of each round).
- **Round 1** (the brief's rules) is kept as `Docs/Studios/HoneyguideLab.round1.html`.
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/HoneyguideLab.html` (PASS, round 7).
- **Parity:** `node Tools/Build/dynamo_lab/parity.cjs` holds `DynamoCore.cs` to the lab's core, the Engineer AI
  included (PASS 2026-10-29, round 7: 16,824 steps over 24 rule sets, 1,649 AI moves chosen alike, 72 ability offers,
  48 port sets, 400 slips).
- **In Unity (phase 2, started 2026-10-21 on Garrett's "lets play it in unity"):** `VesselClassType.Honeyguide = 23` (21 until the bleeding-edge merge gave it to the Starling),
  offered by the Toybox's Vessel Changer. See `Assets/_Scripts/Controller/Vessel/Crew/HONEYGUIDE.md`.

Why a lab of its own and not a fourth round of the Crew Vessel Lab: that page is the Wrasse's signed-off and graduated
record with an open catch-up task (`T-20261018-3247`), and one decision log per vessel keeps each sign-off readable.
The Orca took the same route (`CrewOfFourLab.html`, `cece/happy-noether-m54hz6`). The shell (seats, swap, AI squad
mate, decision log, platform detection) is the Crew Vessel Lab's, rebuilt around a 2D arena.

## How to use it

- **Your seat** is in the header: **Engineer** (you play the Ratel; an AI flies), **Fly** (you fly; an AI plays the
  Dynamo), **Watch** (two AIs) or **Couch** (two people: the keyboard flies, the mouse or a finger plays the Dynamo).
  Swap with the D-pad left/right, 1 / 2, the Swap button, or a double-tap on the small view on a phone. It is the
  arena's gesture (`PilotSwapGesture`, `PilotSwap.CooldownSeconds` 0.75 s).
- **The Ratel (Engineer).** Drag a prism to move it (1 AP a cell). Tap an empty injection port to inject a prism of its
  colour (1 AP). An empty port already counts as a prism of its colour in a pattern, but every pattern needs at least
  one real prism. When a card lights, tap it (1 AP), then tap one of the outlined prisms: it goes to the battery. The
  ports turn 60 degrees every spin; the countdown is top right, and in the last 1.5 s rings show where they will land.
  Each card names the passive the pilot gets, and its pips count the makes before it is replaced. Your ability is under
  the AP row and changes, with the port pattern, every 4 patterns.
- **The Honeyguide (Pilot).** A/D or ←/→ steer; W or Space boosts when you have boost. RT (E or K) is Peck or its upgrade, LT
  (Q or J) is Flit or its upgrade (round 6). On touch, hold a finger on the flight view and the hull turns toward it (two
  fingers boost); the four cards bottom right (RT, LT, Boost, Slip) are the buttons. The Slip arms itself: a grey wedge on the nose
  shows the 45-degree cone, it lights in the trail's colour when a slip is armed, and a dashed line shows where it
  would land.
- **Rules tab:** Garrett's round 2 rules, each a box you can turn off (a control). **Patterns tab:** all 39 patterns and
  the passive each gives.

## 2026-10-31: the spin bar shrinks from both sides to the centre

Garrett: "including the way the timer subtract from both sides to the center" (the Ratel Raid Lab's bar). The page's
spin bar and Unity's (`HoneyguideDynamoView`) now shrink toward the centre. `verify_lab.cjs` PASS.

## Round 7 (2026-10-29): the bank holds 6, and the Ratel picks one of three abilities

Garrett, 2026-10-29, on the proposal `Docs/Studios/ONE_THUMB_SEAT_ARCS.md` §3: "lets have the ap cap go to 6. lets do the 3
ability pick as well. those will go in for sure, but show me the raid in an artifact." The raid is its own lab
(`Docs/Studios/HONEYGUIDE_RAID_LAB.md`); this round is the two numbers that go in.

### What changed

- **`apBank` 3 → 6.** A spin still gives 3 AP; what is not spent is kept, up to 6. Past 6 a spin's AP are wasted, as before.
- **A pick of three** (`abilityChoices` 3, `pickSeconds` 10). Every 4 patterns the draw OFFERS three abilities: distinct,
  never the one held, never a retired one (Printer). The board dims and shows them as three cards; tap one. The spin clock
  stays stopped while the Ratel picks; after `pickSeconds` the first offer is taken (0 waits for the pick). Then the new port
  pattern is shown for `revealPorts` and the clock restarts. The new input is `pick` (slot = the offer); the new event is
  `offer`; the `shuffle` event now follows the pick, so the toast still names the ability taken. `abilityChoices` 1 is the
  old random draw.
- **The AI Ratel picks** the first offer in `AI_PICK_ORDER` (Overclock, Refund, Free Fire, Long Reach, Capacitor, Wild Core,
  Swap, Mirror, Printer): AP first. A proposal; the order is a constant in the core, held to the C# by the parity gate.
- Three scorecard variants: **Round 6: bank 3, random ability**, **Bank 6, random ability** and **Bank 3, pick of three**.

### Scorecard (12 seeds × 180 s, `window.__lab.runBatch`, hard pilot)

| Variant | Patterns / min | Charges / min | New abilities / min | Pilot waiting % | Lift |
|---|---|---|---|---|---|
| **Round 7 (bank 6, pick of three), hard Ratel** | **5.11** | 5.42 | 1.11 | 20.6 | +15.0% |
| Round 6: bank 3, random ability | 4.53 | 5.25 | 0.97 | 25.2 | +15.7% |
| Bank 6, random ability | 4.53 | 5.25 | 0.97 | 25.2 | +15.7% |
| Bank 3, pick of three | 5.11 | 5.42 | 1.11 | 20.6 | +15.0% |
| Round 7, easy Ratel | 3.17 | 3.25 | 0.72 | 42.9 | +13.0% |
| Round 7, idle Ratel (control) | 0 | 0 | 0 | 100 | 0 |

SE of the lift about ±0.16 legs a minute, so the lift columns are the same within noise.

### Found (round 7)

- **The bank of 6 changes nothing for the AI Ratel**: the bank-3 and bank-6 rows are identical. The AI spends every AP the
  moment it has one, so it never banks. The bank is for a person: room to skip a spin and think, or to save for a big
  turn. Only a person can measure that. A planner that saves AP for a later spin would be a new AI, not a tuning.
- **The pick of three makes the AI Ratel about 13% faster** (4.53 → 5.11 patterns a minute) because it takes Overclock or
  Refund when offered. The race lift does not move (the pilot is the limit there).

### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/HoneyguideLab.html`: PASS (desktop and iPhone 13).
- `node Tools/Build/dynamo_lab/parity.cjs`: PASS, 16,824 steps over 24 rule sets (the pick of 1 to 4, a 10 s, 1.2 s and no
  time limit), 72 offers, 60 AI picks, every step's digest and events alike; 13 planted controls caught, three of them new
  (a timed-out pick takes the last offer; the AI's order reversed; offers may repeat the held ability).
- The new edit-mode tests' bodies were run against the shipped `DynamoCore.cs` with .NET 8 (outside Unity, settings built
  from the lab's tables): the pick, the timeout, the AI pick, the bank of 6 and the board copied mid-pick all pass.
- `python3 Tools/Build/author_honeyguide_assets.py --check` and `--self-test`: ok.
- `Tools/Build/unity_refcompile/run.sh`, player and editor configs: 0 errors in project code. Not opened in the Editor
  (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).

### Decisions needed (Garrett)

- The AI's pick order, and whether 10 s is the right time to pick (0 = no limit).

## 2026-10-22: Printer retired from the draw; the Ratel's board in portrait on a phone

Garrett: "the ratel dynamo should assume the player is holding the phone in portrait mode. it should size text and
buttons appropriate to mobile. for now remove the engineer ability that gives players a free prism at the start of the
spin, the experience is too confusing to players on mobile."

- **Printer is out of the random draw.** The core takes the pool a draw picks from (`S.abilities`; `abilityPool`, null =
  all nine), and the page's shipped settings pass every ability but `RETIRED_ABILITIES` (`['printer']`). With the full pool
  the draw is the same arithmetic as before, so every old seed plays the same. A fixed ability (the Rules tab's
  "Ratel ability") can still pick Printer, and the cards that grant free prisms still do. The game's config carries the
  same list (`retiredAbilities`), held to the page by `author_honeyguide_assets.py --check`. `parity.cjs` plays rule sets
  with the full pool, the shipped pool, two abilities and one, and catches a core that ignores the pool (a new negative
  control).
- **Portrait is a Unity-side layout only.** The game's screen is locked to landscape, so the board is drawn a quarter turn
  round with millimetre-sized text and cards (`HoneyguideDynamoView`, `Assets/_Scripts/Controller/Vessel/Crew/HONEYGUIDE.md`
  step 4). The page's own board is unchanged.

## Round 6 (2026-10-21): RT always attacks, LT always moves, and every card is its own

Garrett, after round 5: "the right trigger should always be a weak attack power that gets upgraded sometimes. the left
should be a movement power that works similar. the 39 patterns should still be unique. and grant some temporary fun in
the form of actives and passives."

### What changed

- **RT is always Peck**, a weak attack: one shot while held, 2.5 a second, 45 u. **LT is always Flit**, a short move: an
  18 u hop every 2 s. Both are hull-intrinsic, so the solo hull has them too.
- **Thirteen cards upgrade a trigger** for `activeSeconds` (20 s). Then Peck or Flit comes back. The same card again
  refreshes the upgrade and raises its level; a different upgrade for the same trigger replaces it.
  - **RT (attack), the six from round 5:** Hard Light Lasso, Genesis Lance, Little Bang Grenade, Sepia Drone, Particle
    Harmonizer (hold), Fugacity Discharger (hold, let go).
  - **LT (move), seven:**
    - Eyelet Jet (50 u, ploughing).
    - Thruster Swarm (a 20 u thrust every 0.4 s, ploughing).
    - Five that used to be stored jumps on the Slip card: Blink Drive, All Terrain Portals, Rosen Bridge, FTL Chromatool
      and Monte Cannon. These press LT; they have cooldowns of 2.5 to 4 s.
- **Every card is its own.** The round 5 random deal is gone; it is kept as a control. The other 26 cards keep their
  round 4 timed passives and one-off effects. The Slip card no longer stores jumps.
- **The pilot AI** pecks at a hostile prism dead ahead. It flits when the destination is ahead. It takes a move upgrade
  when the landing is 20 u closer to the destination, and uses the attack upgrades as in round 5.
- **Page.**
  - A Triggers slider tab: Peck, Flit and the upgrade life.
  - Round 4's gun sliders moved to "Guns (r4)".
  - The HUD cards read Peck / Flit or the upgrade's name, with its time bar and cooldown.
  - `__lab.made(seeds, seconds)` counts which cards the crew makes.
  - The localStorage prefix is `r6`.
- **The Dynamo core did not change.** Parity still PASSES.

### Scorecard (round 6, 24 seeds × 180 s, hard pilot)

The solo hull now has Peck and Flit, so solo is 4.40, not round 5's 3.06. The row with nothing on the triggers is the
old baseline.

| Variant | Legs/min | An upgrade on a trigger % | Upgrades/min | Uses/min | Shattered/min |
|---|---|---|---|---|---|
| Solo (Peck and Flit, no Ratel) | 4.40 ± 0.16 | 0 | 0 | 0 | 11 |
| **Candidate** (hard Ratel) | **5.21 ± 0.12** | 34 | 1.53 | 6.0 | 21 |
| Candidate, rookie pilot | 5.06 ± 0.13 | 34 | 1.53 | 8.5 | 17 |
| Candidate, easy Ratel | 5.19 ± 0.12 | 27 | 1.04 | 5.6 | 21 |
| No upgrades (Peck and Flit only) | 4.98 ± 0.13 | 0 | 0 | 0 | 10 |
| Upgrades only, no Peck or Flit | 4.73 ± 0.15 | 34 | 1.53 | 5.8 | 16 |
| Nothing on the triggers | 4.05 ± 0.17 | 0 | 0 | 0 | 3 |
| Round 4: the guns | 5.03 ± 0.13 | 0 | 0 | 0 | 46 |
| Round 5: every pattern also deals an upgrade | 5.51 ± 0.24 | 85 | 4.63 | 19.3 | 42 |
| Upgrades last 10 s | 5.04 ± 0.10 | 20 | 1.53 | 4.3 | 18 |
| Upgrades last 40 s | 5.23 ± 0.14 | 49 | 1.53 | 8.8 | 33 |

**Each move upgrade alone** (12 seeds × 180 s, dealt on every pattern; Peck and Flit only is 5.11 on these seeds):

| Thruster Swarm | Eyelet Jet | All Terrain Portals | Rosen Bridge | Blink Drive | Monte Cannon | FTL Chromatool |
|---|---|---|---|---|---|---|
| 6.42 | 6.26 | 6.16 | 5.93 | 5.76 | 5.09 | 5.05 |

**How often each upgrade comes up.** These are the cards the hard crew made over 24 seeds × 180 s (374 makes):
- Attack upgrades, 68 makes: Lasso 17, Grenade 15, Harmonizer 13, Discharger 9, Lance 7, Drone 7.
- Move upgrades, 37 makes: Eyelet 10, Monte 9, FTL 6, Thruster 4, Portals 3, Blink 3, Rosen 2.

### Checked

- `verify_lab.cjs`: PASS.
- `parity.cjs`: PASS, all nine controls caught.
- I looked at the desktop screenshot: the HUD reads Peck / Flit / Boost / Slip.

### Found (round 6)

1. **Flit is worth a lot.** Peck and Flit alone lift the race from 4.05 to 4.98 (+23%). With the upgrades on top, the
   candidate reaches 5.21 (+29%). That is above round 4's guns (5.03).
2. **"Upgraded sometimes" is what it measures.** An upgrade holds a trigger 34% of the time, about 1.5 a minute. Round
   5's random deal had one up 85% of the time and scored a little higher (5.51). Keeping every card unique costs about
   0.3 legs a minute.
3. **Move upgrades are rarer than attack upgrades** (37 makes against 68). Five of the seven are Gold-family cards,
   whose shapes are harder to make.
4. **The base powers matter more than the upgrades in the race.** Without Peck and Flit, the upgrades alone give 4.73.
   Without the upgrades, Peck and Flit give 4.98.
5. **A rookie pilot is close to the skilled one** (5.06 against 5.21): the triggers are still easy to use.
6. **Three upgrades do little alone.** Monte Cannon and FTL Chromatool are about zero; they need prisms ahead, and the
   AI seldom has a target lined up. The round 5 finding stands for the Drone and the Lance.

### Decisions needed (Garrett), on the Decisions tab

1. **Peck and Flit numbers.** Peck is 2.5/s over 45 u; Flit is 18 u every 2 s.
2. **Should move upgrades come up more often?** For example, by weighting the Gold-family cards in the Ratel's draw.
3. **Sign-off,** then port round 6 to Unity.

## Round 5 (2026-10-21): timed actives on the open buttons, no guns

Garrett, after flying the Unity port: "lets not have a sparrow clone. lets make a vessel that is super fun to play because
you never know what buffs are coming next", then "lets do a lab round, but lets have actives too. so it is gaining fun
abilities that are not so hard to use like a dolphin combo but fun like a rhino sword but a different weapon. we can stick
an ability on an open button. and the abilities time out and then go away. and new ones come." The decision log was
empty at the start of the round.

### What changed

- **The guns are gone.** The candidate has no Bolt or Burst. LT and RT are the open buttons. Round 4's guns stay only as
  a control (`opts.guns`, the "Round 4" row).
- **Seven actives, one per weapon card.** These are the seven cards that were parked in the port because they upgraded
  the guns. Each is now a weapon of its own (`ACTIVES` in the page), and each is one button with no combo:

  | Card | Active | Use |
  |---|---|---|
  | Hard Light Lasso | A rope hooks the nearest hostile prism ahead (90 u, 30°) and reels the hull to it at 220 u/s. The hull ploughs through what it crosses, and the prism and everything hostile within 10 u shatter on arrival. | press |
  | Genesis Lance | A lance 120 u straight ahead shatters every hostile prism on its line. It leaves a trail of the newest charge's domain along it to slip on. | press |
  | Little Bang Grenade | Lobbed 55 u ahead. It bursts 0.6 s later and shatters everything hostile within 22 u. | press |
  | Eyelet Jet | Jets 50 u ahead in 0.25 s, ploughing through what it crosses. | press |
  | Sepia Drone | Circles the hull for 8 s, zapping the nearest hostile prism within 50 u three times a second. | press |
  | Particle Harmonizer | A beam turns hostile prisms in a 40° cone up to 55 u ahead to the newest charge's domain, 8 a second. This makes slip lanes from rivals' trails. | hold |
  | Fugacity Discharger | Hold to charge for up to 1.2 s, then let go. Everything hostile within 12 to 40 u shatters, and each prism gives 0.05 s of boost (up to 1.5 s). | hold, let go |

- **Each active stays on its button for `activeSeconds` (20 s), then goes away.** Its time runs down the card's bottom
  edge and flashes for the last 4 s. A new active takes the first open button. When both buttons are full, it replaces
  the one with the least time left. The same active again refreshes it and raises its level (reach +25% a level, up to
  III).
- **"New ones come": every pattern made also deals a random active** (`opts.deal` = `every`, the candidate). The seven
  weapon cards deal their own. Dealing only on the weapon cards (`cards`) leaves an active on a button 27% of the time
  (table), so the buttons would mostly sit empty.
- **The pilot AI uses actives through its buttons.** Press actives get one step down, then up. It holds the Harmonizer
  and holds the Discharger to 1 s of charge, then lets go. A skilled pilot (`actSense` 1) judges the moment:
  - a target ahead lying toward the destination (Lasso);
  - the destination ahead (Lance, Jet);
  - hostile prisms where the grenade lands (Grenade), near the hull (Drone, Discharger), or in the beam (Harmonizer).

  It also uses a press active with 2 s left rather than lose it. A rookie (`actSense` 0.35) often just presses whatever
  is ready.
- **Page.**
  - New Actives slider group (`activeSlots`, `activeSeconds`, `activeLevelGain`).
  - New rule boxes.
  - The four HUD cards are RT, LT, Boost and Slip.
  - New effects: rope, lance beam, grenade arc, drone, harmonizer cone, discharge ring.
  - `__lab.grant(key)` and `state().actives` are the test hooks.
  - The localStorage prefix is `r5`.
- **The Dynamo core did not change.** Parity still PASSES (16,824 steps, 1,540 AI moves alike, all controls caught).

### Scorecard (round 5, 24 seeds × 180 s)

| Variant | Legs/min | Lift | Active up % | Uses/min | Shattered/min |
|---|---|---|---|---|---|
| Solo | 3.06 ± 0.12 | 0 | 0 | 0 | 0 |
| **Candidate** (hard Ratel, hard pilot) | **5.15 ± 0.16** | **+68%** | 85 | 19.4 | 67 |
| Candidate, rookie pilot | 5.15 ± 0.10 | +68% | 85 | 25.2 | 50 |
| Candidate, easy Ratel | 4.67 ± 0.13 | +53% | 71 | 14.0 | 49 |
| Actives off (no guns either) | 4.05 ± 0.17 | +32% | 0 | 0 | 3 |
| Round 4: the guns, no actives | 5.03 ± 0.13 | +64% | 0 | 0 | 46 |
| Only the 7 weapon cards deal an active | 4.61 ± 0.15 | +51% | 27 | 4.6 | 19 |
| One open button | 4.95 ± 0.13 | +62% | 85 | 13.1 | 48 |
| Actives last 10 s | 4.78 ± 0.13 | +56% | 55 | 14.2 | 45 |
| Actives last 40 s | 5.45 ± 0.15 | +78% | 96 | 22.0 | 87 |
| Spin 15 s, 6 AP | 6.21 ± 0.16 | +103% | 95 | 25.9 | 83 |

The 24-seed solo baseline is 3.06; the round 4 table's 4.49 was the older arena's. Lifts here are against 3.06.

**Each active alone** (every deal is that active; 12 seeds × 180 s; the actives-off row is 4.11 on these seeds):

| Active | Legs/min | Uses/min | Shattered/min |
|---|---|---|---|
| Fugacity Discharger (after the boost cut below) | 5.92 | 11.4 | 133 |
| Eyelet Jet | 6.15 | 18.6 | 10 |
| Hard Light Lasso | 5.40 | 15.3 | 60 |
| Particle Harmonizer | 4.66 | 10.9 | 2 (24 turned) |
| Little Bang Grenade | 4.60 | 19.3 | 114 |
| Genesis Lance | 4.42 | 4.1 | 9 |
| Sepia Drone | 3.90 | 5.7 | 96 |

### Checked

- `verify_lab.cjs`: PASS (desktop and phone; deterministic batch).
- `parity.cjs`: PASS (the Dynamo core is unchanged).
- I looked at screenshots of the HUD with two actives on the buttons (time bar, level pip, banner "Fugacity Discharger on
  RT") and of the phone layout.

### Found (round 5)

1. **Removing the guns cost nothing.** The candidate (+68%) matches round 4's guns (+64%) within the error. It shatters
   more (67 a minute against 46).
2. **The actives are easy to use.** A rookie pilot who often presses whatever is ready scores the same as the skilled AI
   (5.15 both). The skill shows only in how much it shatters (50 against 67).
3. **"New ones come" needs every pattern to deal.** With only the seven weapon cards dealing, an active is on a button
   27% of the time, about one a minute. With every pattern dealing, an active is on a button 85% of the time, 4.6 a
   minute.
4. **20 s is about right.** At 10 s the buttons are empty half the time (+56%). 40 s scores more (+78%), but the buttons
   are almost never open, so fewer new ones are seen.
5. **The first Fugacity Discharger was too strong** (+171% alone, from 0.15 s of boost a prism). I cut it to 0.05 s a
   prism (up to 1.5 s), which gives +105%. The boost lesson from round 4 holds: boost carries the race.
6. **Sepia Drone and Genesis Lance score low in the race.** The drone shatters 96 a minute, but the race does not value
   that. The lance's lane is in the newest charge's domain. After the hull has slipped down it, the lane is hostile and
   slows it (hits rise to 10 a minute). Both would read better in a mode that scores destruction, or with the lane in the
   hull's own domain.
7. The second button matters: one open button drops uses from 19 to 13 a minute (+62% against +68%).

### What round 5 does not model

These are on the page's About tab.
- The actives only shatter, recolour or lay prisms, in 2D.
- There is no aiming in 3D, no camera, no sound, and no effects beyond lines and rings.
- A shattered prism simply vanishes.
- The reaches, cooldowns and the 20 s life are proposals.

### Decisions needed (Garrett), on the Decisions tab

1. **The actives.** Should every pattern deal one (the candidate), or only the seven weapon cards?
2. **Two open buttons, and a life of 20 s.** Are these right?
3. **Which actives feel like the Rhino's sword.** Cut or rework the ones that don't (the Drone and the Lance score lowest).
4. **Sign-off,** then port round 5 to Unity.

## Round 4 (2026-10-21): every pattern has its own card's effect

Garrett: "Keep the tabletop names and try to make abilities that convert from the old, are inspired by the name, etc."
The 15 generic passives are gone. Each of the 39 patterns now has its own effect, in `EFFECTS` and `CARD_EFFECTS`. An
effect is converted from the card's text where that text has a real-time reading, and from the card's name otherwise.
The full table, with each card's text, the effect and its measured lift, is `HONEYGUIDE_PATTERNS.md`.

- **Three kinds.** A *timed* effect lasts `passiveSeconds` (20 s), and a repeat raises its level to III. A *once*
  effect happens when the pattern is made. A *stored jump* waits on the Slip card, which then reads Jump. The card
  stores up to 3 jumps, and the R key or pad Y takes one. Jumps teleport through `setPose`.
- **Ratel-side effects** change the Dynamo through the core's new `boardEffect(st, S, kind, arg)`. Its kinds are burn,
  purge, free, bonus, rewind, unspin, trio, scatter, charge, drain and drainOne. They run on the core's seeded stream,
  so the C# port can match them.
- **The effect fires on the `pick` event**, when the battery prism is taken. Big Red Button uses that prism's colour.
- **The AI pilot** takes a stored jump when it lands at least 20 u closer to the destination. It taps boost for each
  Thruster Swarm dash and fires ahead while Genesis Lance runs. It plans slips over any domain while Void Jumper or a
  Plankter Sail allows it.
- The slot cards and the HUD chips show the pattern's name. The Patterns tab gives each effect's text. `opts.only`
  forces one effect for every pattern, which is how the per-effect lifts were measured.

### Scorecard (round 4, 24 seeds × 180 s, hard pilot AI)

| Variant | Legs/min | Lift |
|---|---|---|
| Solo | 4.49 ± 0.06 | 0 |
| **Candidate** (hard Ratel) | 4.99 ± 0.12 | +11% |
| Pattern effects off (slip only) | 4.53 ± 0.11 | +1% |
| Slip off (effects only) | 4.92 ± 0.11 | +10% |
| Spin 15 s, 6 AP | 5.54 ± 0.13 | +23% |
| Hold to arm | 5.37 ± 0.11 | +20% |

### Found (round 4)

1. The card effects together lift the race +10%. The round 3 generic passives gave +24%. The generic set was all speed,
   fire rate and reach, so every pattern helped. Most card effects do something narrower.
2. Boost carries the race. Amanita Furnace (+34% alone), Gravity Engine (+29%), Dragon Saddle and Juggernaut Plow (+22%)
   and Anomaly Skipper (+17%) all pay out in boost or in not being slowed. In the lab, a second of boost is worth more
   than a slip.
3. Ratel-side effects (free prisms, AP, Triplesynth, Vorticity) measure as noise. They speed the Dynamo only a little
   at 3 AP a spin.
4. The AI cannot use Epitaxial Condenser, Genesis Lance or Reality Detangler. It dodges the lanes they lay (−11% to
   −4%), so a person has to judge those three.
5. Doubling AP to 6 a spin lifts the race from +11% to +23%. The 15 s spin is what slows the effects down.

## Round 3 (2026-10-21): Garrett's second set of notes

| Garrett's note | What the lab does |
|---|---|
| Only port patterns that are three-fold colour symmetric; one was close but missing an injection point | `symmetricPorts()` keeps a port set only if a 120° turn maps ports onto ports and each colour onto the next. All four designed engines pass. *Claw that rends the sky* had 11 ports: the Jade port at (1, −1) was missing (now added, so 4 / 4 / 4). The 72 random scatters failed the test and are gone. 48 distinct port patterns remain (each engine's 6 turns × 6 colourings, de-duplicated). `__lab.portCheck()` reports each engine. |
| When the pattern and engineer change, stop the time; first the ability with time to read it, then the pattern with a couple more seconds, then start anew | The core enters `st.reveal`. Stage 1 lasts `revealAbility` (3.5 s): the board dims, a card names the new ability and what it does, and the header's ability flashes. Stage 2 lasts `revealPorts` (2 s): the new ports grow in under a NEW INJECTION PORTS label. Then the spin clock restarts at a full 15 s. The spin clock stands still and Ratel inputs are refused throughout. The pilot keeps flying. |
| 15 seconds per spin | `tickSeconds` 15 (was 8). AP per spin is unchanged at 3. |
| No numerical time; a bar that shrinks, across the top | A 5 px bar runs across the top of the Dynamo panel and shrinks to the spin. It turns amber in the last 1.5 s and grey while a reveal holds it. The number and the ring round the board are gone. |
| Don't have them pick the slip prism; take one at random | `activate` draws one of the made pattern's real prisms with the core's own seeded stream. The pick step, `pickTimeout` and the AI's pick are gone. |

The full list of patterns, passives, abilities and engines is `HONEYGUIDE_PATTERNS.md`. Garrett asked for it on
2026-10-21 ahead of the port.

### Scorecard (round 3, 24 seeds × 180 s, hard pilot AI)

| Variant | Legs/min | Lift | Patterns/min | Hit rate |
|---|---|---|---|---|
| Solo (no Engineer) | 4.49 ± 0.06 | 0 | 0 | 0 |
| **Candidate** (hard Ratel) | 5.62 ± 0.24 | +25% | 4.1 | 74% |
| Spin every 8 s (round 2's clock) | 5.54 ± 0.25 | +23% | 6.6 | 65% |
| Spin 15 s, 6 AP (round 2's AP rate) | 5.33 ± 0.17 | +19% | 6.8 | 57% |
| No reveal pause | 5.54 ± 0.28 | +23% | 4.3 | 78% |
| Hold to arm | 5.98 ± 0.26 | +33% | 4.1 | 82% |
| Passives off (slip only) | 4.53 ± 0.11 | +1% | 4.1 | 75% |
| Slip off (passives only) | 5.57 ± 0.33 | +24% | 4.1 | 0 |

### Found (round 3)

1. The 15 s spin drops the AI Ratel from 6.6 to 4.1 patterns a minute (same 3 AP a spin), but the race lift is the
   same within noise (+25% against +23%). Fewer charges are made, and more of them get used: the hit rate rises from
   65% to 74%. Giving 6 AP a spin restores the pattern rate and does not raise the lift. AP per spin is open.
2. The reveal pause costs nothing measurable (+25% with it, +23% without).
3. A random battery prism leaves the lift where the AI's chosen prism had it. In round 2 the AI chose the colour with
   the best slip toward the destination, and that choice was worth less than the noise.
4. The slip alone is still worth about nothing under axis arming (+1%); the passives carry the race (+24%). Hold to arm
   is again the best arming (+33%).

## Round 2 (2026-10-21): Garrett's rules from his first play (superseded by round 3 where they differ)

Garrett's notes after playing round 1 (thread, 2026-10-21), all built:

| Note | What the lab does now |
|---|---|
| The slip arms on its own when the nose is within 45° of the prism's z axis, either way, and the prism is part of a 1-D prismscape | `arming: 'axis'`. A prism's z axis is its trail's direction there (the prisms either side). "1-D" is measured locally: a trail prism with at most two other prisms within 1.5 prism spacings (10.5 u). The arena now has 2-D beds of prisms (7 flat beds of 20, 6 u apart) that slow and can be shot but never arm a slip; a crossing of two trails is 2-D where they cross |
| The pilot's Time ability is the slip | The pilot HUD is the four-card row (`Docs/claude/VESSEL_HUD_AND_ELEMENTS.md`): Charge = Bolt (RT), Mass = Burst (LT), Space = Boost, Time = Slip (auto, shows the charges) |
| No letter labels; if letters, J R G | No letters. A box on the Rules tab shows R J G for anyone who wants them |
| Patterns register at any orientation | Six rotations (mirror images only with the Mirror ability) |
| The spin is animated, 60° holding the same injection pattern | The whole port pattern turns 60° clockwise about the centre as one piece, animated over 0.6 s; prisms stay put |
| The Ratel sees how long until the spin | "spin 4.2 s" top right, the ring round the board, and rings where the ports will land in the last 1.5 s |
| An injection point counts as a prism for completing patterns, but a real prism still goes to the battery | `shows(cell)` = its prism, or its port's colour when empty. A match needs at least one real prism; only real prisms can be picked |
| Mutons are now prisms | Renamed everywhere on the page |
| A random Ratel ability at the start, a new one every 4 patterns with a new port pattern | Nine abilities (table below); 216 port patterns: the four designed engines in 6 turns × 6 colourings, and 72 random scatters of 12 |
| A pattern made for the third time is replaced by a random one | Per slot; the pips on the card count the makes |
| Each pattern grants the pilot's vessel a passive | 39 patterns → 15 passives (table below), 20 s each; a repeat refreshes it and raises its level (max 3) |
| Two versatile weapons on the triggers that take upgrades as patterns complete | RT Bolt (5/s, 160 u/s, 110 u) and LT Burst (1.5/s, 5 pellets in 40°, 60 u). Shots destroy prisms not of your domain. Every weapon passive shapes both |

**The Ratel's abilities** (one at a time): Overclock (+1 AP a spin, bank +1), Long Reach (a drag of any length through
empty cells is 1 AP), Printer (a free prism of any colour each spin, hold 2; retired from the draw 2026-10-22), Swap (drag a prism onto a neighbour to swap,
1 AP), Free Fire (making a pattern is free), Wild Core (the centre counts as any colour), Capacitor (battery +2), Mirror
(patterns also match mirrored), Refund (each pattern banks 1 AP, hold 2).

**The passives** (by pattern family; the card's place in its family picks the row). Ruby family, the triggers: Rapid
Fire, Piercing, Scatter, Long Barrel, Seeker, Blast. Jade family, the hull: Overdrive (+15% speed a level), Shield,
Agile, Afterburner (boost refills), Magnet (slips catch from farther). Gold family, the slip: Long Slip (+35% reach a
level), Echo (the next slip keeps its charge), Wake (your trail is laid in your newest charge's domain), Chroma Rounds
(shots turn hostile prisms to your newest charge's domain instead of destroying them).

### Scorecard (round 2)

24 seeds × 120 s at 30 Hz, AI Hard pilot, AI Hard Ratel unless named. ±SE is Legs/min's standard error (about ±5%,
so lift differences under about 12 points are not findings).

| Variant | Legs/min | ±SE | Lift % | Slips/min | Slip gain (u) | Bad slips % | Patterns/min | Kills/min | Hostile hits/min |
|---|---|---|---|---|---|---|---|---|---|
| Solo (no Ratel) | 4.55 | 0.08 | 0 | 0 | 0 | 0 | 0 | 51.9 | 1.8 |
| **Candidate · hard Ratel** | **5.70** | 0.28 | **+25** | 5.2 | 128 | 15 | 7.8 | 40.2 | 6.2 |
| Candidate · easy Ratel | 5.62 | 0.26 | +24 | 3.9 | 139 | 17 | 5.7 | 42.3 | 5.5 |
| Candidate · idle Ratel | 4.55 | 0.08 | 0 | 0 | 0 | 0 | 0 | 51.9 | 1.8 |
| Passives off (slip only) | 4.74 | 0.12 | +4 | 4.3 | 138 | 19 | 7.8 | 27.9 | 6.3 |
| Slip off (passives only) | 5.92 | 0.36 | +30 | 0 | 0 | 0 | 8.0 | 22.4 | 8.0 |
| Triggers never fired | 5.41 | 0.25 | +19 | 5.3 | 112 | 22 | 8.0 | 0 | 8.2 |
| Ports do not count | 5.42 | 0.23 | +19 | 5.4 | 114 | 19 | 8.1 | 50.3 | 5.7 |
| Patterns never replaced | 5.37 | 0.26 | +18 | 4.9 | 131 | 17 | 8.8 | 43.9 | 6.5 |
| No new ability or ports | 5.59 | 0.25 | +23 | 5.5 | 116 | 17 | 8.0 | 48.0 | 5.7 |
| Patterns as printed (no rotation) | 5.65 | 0.27 | +24 | 4.7 | 123 | 15 | 6.6 | 48.5 | 6.2 |
| Hold to arm (round 1 rule) | 6.29 | 0.23 | +38 | 6.0 | 130 | 16 | 7.8 | 45.8 | 5.1 |
| Always armed (the brief) | 5.40 | 0.27 | +19 | 6.9 | 57 | 35 | 7.9 | 60.1 | 3.6 |
| Arming cone 30° | 5.88 | 0.30 | +29 | 4.6 | 155 | 14 | 7.8 | 37.2 | 7.6 |
| Spin every 6 s | 5.78 | 0.27 | +27 | 6.4 | 107 | 20 | 10.3 | 44.5 | 7.1 |
| Spin every 12 s | 5.79 | 0.31 | +27 | 4.3 | 130 | 13 | 5.5 | 52.3 | 5.6 |

The Dynamo alone (12 seeds × 10 min, the Ratel AI only), patterns a minute by ability: Printer 12.7, Refund 7.9,
Overclock 7.7, Free Fire 7.6, Long Reach 6.8, Wild Core 6.5, Mirror 5.9, Capacitor 5.8 (no Dynamo effect: the
baseline), Swap 5.7. Ports counting 7.2 against 7.4 without.

### Found (round 2)

1. **The passives carry the race now; the auto-armed slip barely pays for its set-up.** Passives alone lift the race
   30%, the slip alone 4%, both 25%. A slip still takes 128-138 u off the way, but under the axis rule the AI pilot has
   to swing round to come in along the trail, and it flies into hostile prisms on the way in (6 hits a minute against
   1.8 solo). Under hold-to-arm, the same slips lift the race +15% alone and +38% with the passives. A wider cone does
   not help (60°: +4%, 75°: -2%, as brushes turn into wrong-way slips). This is the AI's line-up, not yours: your
   play is the test of whether lining up along a trail feels good. The race also flatters the hull passives
   (Overdrive is +15% speed a level), so read "passives +30%" as "the passives work", not as their worth against the slip.
2. **Auto-arming fixed the brief's problem without a button.** Always-armed: 35% of slips go the wrong way (gain 57 u).
   The axis rule: 15% (gain 128 u), the same as hold-to-arm (16%).
3. **The Ratel's ability matters more than the other new rules.** Printer doubles the Dynamo's rate (12.7 patterns a
   minute against 5.8); Mirror and Swap add nothing the AI can use. With a random draw every 4 patterns, Printer turns
   up about one draw in nine.
4. **Ports counting as prisms did not speed the AI up** (7.2 against 7.4 patterns a minute). At first it made it
   slower (4.8): the AI built on empty ports and the spin turned them away. It now injects at the ports it is relying
   on when the pattern cannot be finished before the spin. A person sees the ports and the countdown; whether counting
   ports feels easier is a play question.
5. **The triggers are worth about 6 points of race** (+25 against +19 with them never fired), inside the noise. They
   destroy 40-60 hostile prisms a minute; nothing in the lab scores kills yet.
6. Replacement after 3 makes, the new ability and ports every 4, rotation and the spin rate all land within the noise
   of the candidate for the AI pair. They are rules about variety and feel; the scorecard cannot judge them.

### Checked (round 2)

- `verify_lab.cjs` PASS (the `__lab` contract, the scorecard batch, phone and desktop screenshots).
- The Ratel AI: no input it returns is refused; it uses every AP (0 wasted over 10 minutes); it no longer moves the same
  prism back and forth (it keeps the placement it is building unless another is 1.5 AP cheaper).
- Screenshots at an iPhone 13 viewport as the Engineer (the board's hexes 44 × 51 px), mid-spin (the port pattern
  turning as one piece), and as the pilot (the four cards, the passive chips and the arming wedge).

### What round 2 does not model

Everything round 1 did not, plus: kills score nothing; the triggers have no ammo, heat or element cost; the passive
numbers, the nine abilities and the pattern-to-passive table are proposals; 1-D is a local test in a flat world.

### Decisions needed (Garrett), on the Decisions tab

1. **Sign-off** on a phone, as the Engineer.
2. **Slip arming:** keep the axis rule (Found 1 says it costs the AI its slips' worth; your play decides), or go back to
   hold-to-arm.
3. **The passives and abilities:** which feel worth chasing; Printer is far stronger than the rest.
4. **The two triggers:** Bolt and Burst as built, and whether kills should score.

## Round 1 (2026-10-20): the Dynamo, the Slip and the starter twelve (superseded; `HoneyguideLab.round1.html`)

### The model

| Part | What the lab does | Source |
|---|---|---|
| Dynamo grid | 37 pointy-top cells, axial (q, r), r down, centre (0,0) | brief §3.2 |
| Injection ports | 2020-22 board: ring 2, clockwise from top-left M C C C S M M M C S S S; Heimdall-13, Claw and the 2021 co-op engine as variants | Appendix B |
| Rotation | every tick (8 s) each port steps one cell clockwise round its own ring; mutons stay. Variant: the whole engine turns 60° | brief §3.2 |
| Actions | 3 AP a tick, bank 3. Tinker, Refuel, Activate cost 1; Research 2 | the board; Research cost is a proposal |
| Matching | a translated copy of the card's printed shape (orientation fixed; variant: 6 rotations). First copy in a fixed scan order | Appendix C |
| Battery | 3 charges; a new one replaces the oldest | "LIMIT 3 MUTONS" |
| Pick | after Activate, 4 s to pick the battery muton, then the Dynamo picks one of the family colour | proposal |
| Domains | Mass→Ruby, Charge→Jade, Space-Time→Gold (a table; variant: own domain is always M). Mutons are drawn in their domain's colour | brief §3.2, palette from `studio-domains.js` |
| Pilot hull | the Sparrow's numbers: cruise 35 u/s, boost ×5 on the throttle term, yaw 80°/s, a prism every 7 u | `Sparrow.prefab`, as the Crew Vessel Lab |
| Own-trail grace | own prisms younger than 1 s are ignored | `SelfTrailContactConfig.hullGraceSeconds` |
| The Slip | contact with a prism of a held domain on a trail of at least 6 prisms; land 8 u past the end the nose points at more (larger dot product), aligned to the trail; reach cap 400 u (else the farthest block within reach); burn 1 charge; the trail just ridden is ignored for 1 s | brief §3.3 |
| Arena | a 420 u disc, three AI hulls laying Ruby, Gold and Jade trails (110 prisms each), and the Honeyguide racing from one destination ring to the next | lab |
| AIs | input-only. Engineer: picks the cheapest placement to complete (a breadth-first path for each missing cell), activates as soon as a slot lights, and picks the battery muton by scoring each domain's best slip toward the destination within reach. Pilot: flies for the destination, steers for a slip when one of a held domain gains enough, avoids hostile prisms (and, under always-armed, held-domain trails it does not want) | `/vessel-ai` |

**The starter twelve** (proposals, all in "Slipstream points": one point of boost = 0.5 s):

| Card | Here |
|---|---|
| Amanita Furnace (M) | burns one more muton of the pattern; 6 points of boost |
| Big Red Button (M) | destroys every other muton of the picked colour; 2 points of boost per muton |
| Dragon Saddle (M) | drains the battery into boost, 3 points a charge |
| Thruster Swarm (M) | four 24 u dashes on the pilot's button |
| Reality Detangler (C) | the next 3 prisms the hull touches turn the picked domain (new slip paths) |
| Epitaxial Condenser (C) | lays a chain of the picked domain ahead (6 prisms, the slip minimum, not the card's 5) |
| Anode Ray (C) | +1 charge of each colour |
| 5D Printer (C) | two free mutons, any colour, on any empty cell |
| Blink Drive (S) | a 60 u forward jump |
| Rosen Bridge (S) | teleport to the farthest prism of the picked domain in a ±35° cone within 300 u |
| Monte Cannon (S) | three swaps with a prism ahead within 120 u, on the pilot's button |
| Chrono-Compressor (S) | banks one extra Engineer action |

Every card holds one muton of each other colour, so every activation offers all three domains: the domain choice is
always real. The other 27 cards fire with no effect yet; the seven about sector tiles, planets or initiative are
parked (brief §3.4).

### Scorecard

`runBatch()` defaults: 6 seeds × 120 s at 30 Hz, AI Hard pilot. **Legs/min** is the race: destinations reached a
minute plus the share of the last leg flown. Every other column is about the handoff. Re-run with **Score every
variant** (about 4 s).

| Variant | Legs/min | Lift % | Slips/min | Mean slip (u) | Slip gain (u) | Bad slips % | Patterns/min | Waiting % | Hit rate % | ± SE |
|---|---|---|---|---|---|---|---|---|---|---|
| Solo (no Engineer) | 3.97 | 0 | 0 | | | | 0 | 100 | | 0.16 |
| **Candidate · Hard Engineer** | **4.97** | **+25.2** | 4.33 | 253 | 151 | 5 | 6.17 | 9.6 | 70.8 | 0.15 |
| Candidate · Easy Engineer | 4.49 | +13.1 | 1.92 | 252 | 141 | 12 | 2.75 | 43.2 | 69.7 | 0.34 |
| Candidate · idle Engineer (control) | 3.97 | 0 | 0 | | | | 0 | 100 | | 0.16 |
| Slip off (abilities only) | 4.25 | +7.1 | 0 | | | | 6.58 | 7.4 | | 0.13 |
| Always armed (the brief's default) | 4.05 | +2.0 | 5.92 | 217 | 61 | 32 | 6.33 | 33.1 | 93.8 | 0.25 |
| Aim-gated arming | 4.61 | +16.1 | 3.67 | 227 | 159 | 10 | 6.83 | 9.4 | 54.0 | 0.37 |
| Whole engine turns | 5.15 | +29.7 | 2.83 | 229 | 158 | 6 | 3.33 | 52.8 | 84.5 | 0.27 |
| Rotatable patterns | 5.37 | +35.3 | 4.92 | 216 | 142 | 11 | 7.08 | 13.4 | 69.3 | 0.13 |
| Exit keeps facing | 4.73 | +19.1 | 4.75 | 238 | 112 | 20 | 6.67 | 17.2 | 72.5 | 0.16 |
| Tick 6 s | 5.70 | +43.6 | 5.42 | 249 | 133 | 13 | 9.00 | 6.1 | 59.9 | 0.31 |
| Tick 12 s | 4.67 | +17.6 | 3.50 | 267 | 167 | 16 | 4.50 | 18.7 | 78.8 | 0.30 |
| Battery 6 | 5.02 | +26.4 | 4.67 | 258 | 139 | 8 | 6.42 | 9.6 | 73.5 | 0.15 |
| Own domain is M | 5.43 | +36.8 | 4.50 | 264 | 190 | 5 | 6.50 | 11.4 | 69.2 | 0.36 |
| Heimdall-13 engine | 5.59 | +40.8 | 5.33 | 219 | 136 | 15 | 7.08 | 15.0 | 75.3 | 0.20 |
| Claw engine | 5.71 | +43.8 | 5.42 | 226 | 144 | 11 | 6.42 | 23.2 | 85.3 | 0.22 |
| Co-op engine | 5.56 | +40.1 | 5.08 | 238 | 145 | 9 | 6.83 | 14.1 | 75.0 | 0.23 |
| Loadout: Mass (boost) | 5.36 | +35.0 | 1.00 | 292 | 197 | 4 | 6.00 | 83.5 | 17.1 | 0.36 |
| Loadout: Space-Time | 5.73 | +44.3 | 4.75 | 217 | 112 | 21 | 7.25 | 11.6 | 65.3 | 0.17 |
| Loadout: Engineer | 5.24 | +32.0 | 5.42 | 221 | 130 | 18 | 8.75 | 17.1 | 48.0 | 0.17 |
| Loadout: Charge (trails) | 4.92 | +23.9 | 4.42 | 225 | 97 | 21 | 6.92 | 9.8 | 63.8 | 0.34 |

Columns not in the table are on the page: Aimed % (100 under hold- and aim-gated arming), Aimed gain, Clipped %,
Charges/min, Eng idle % (0 for every AI Engineer: it never lets a tick's AP go to waste) and Hostile hits/min.

**Noise.** The standard error of Legs/min is 0.13–0.37 on about 5, so read differences under about 15% as a plateau.

### Found

1. **Always-armed slips are worth almost nothing to the race (+2%), and hold-to-arm makes them worth +25%.** With
   every touch of a held domain's trail armed, about a third of the slips were brushes the pilot did not steer for, and
   a third of all slips carried the hull away from its destination (32% bad slips; mean gain 61 u against 151 u). The
   Hard pilot was taught to avoid unwanted held-domain trails the way it avoids hostile prisms, and that only brought
   the bad share down from 43% to 32%. Under hold-to-arm, every slip is one the pilot asked for (5% still land worse,
   when the pilot's read of which end it points at is off). Aim-gated arming, a lab proposal that needs no button (a
   slip fires only when one of the trail's ends lies within 45° of the nose), sits between them (+16%). The playtests of
   2020 called Slip "powerful" and easy to visualise; a slip that fires by accident would make the Engineer's choice
   look random. **The lab's candidate is hold-to-arm**, changed from the brief's default because of this.
   **Decision needed (Garrett).**
2. **The Slip carries the handoff.** The abilities alone (Slip off) lift the race 7%; the candidate lifts it 25%. An
   idle Engineer lifts it 0, exactly the solo hull (the control). An Easy Engineer, who completes a third as many
   patterns, still gets +13%: the handoff degrades gracefully with skill.
3. **The Engineer's domain choice lands about 70% of the time** (hit rate: charges slipped on, of charges made). The
   rest are replaced in a full battery or still held at the end. Under always-armed it reads 94%, but those charges were
   spent on brushes, not on choices.
4. **A faster tick is the strongest single lever** (6 s: +44%, 9 patterns a minute; 12 s: +18%). Time scales the tick
   in the four-ability proposal, so this is the Time element's reach. A tick of 8 s leaves the battery empty 10% of the
   time; 12 s, 19%.
5. **The other engines beat the 2020-22 board** (Heimdall-13 +41%, Claw +44%, co-op +40%, against +25%), at about the
   same patterns a minute, with SEs of about 0.2. The lab does not yet show why; one candidate is where each engine
   puts its starting mutons relative to the starter cards' shapes. **Open thread**, not a result.
6. **Rotatable patterns (+35%) and own-domain-is-M (+37%) both read above the candidate** by more than the noise. The
   first makes patterns easier; the second makes Ruby (most hostile trails here are not Jade) cheaper to reach. Both
   are design choices for Garrett, not tuning.
7. **The Mass loadout boosts instead of slipping**: Dragon Saddle drains the battery into boost (1 slip a minute,
   battery empty 84% of the time) and still reads +35%. Boost is a strong substitute for slips in a race; worth
   remembering when sizing boost-for-points.
8. **Exit keeps facing is worse than aligning to the trail** (+19%, and 20% bad slips): the hull lands pointing where
   it came from rather than along the trail it just rode.

**Fixed in the lab before any number was taken:**
- a whole-leg yardstick (destinations a minute) moved in steps of one destination, so the first table could not see a
  slip; the race now counts the share of the last leg flown, and every slip records its signed gain (L-STU-114);
- the AI pilot armed every contact; it now arms only when it is steering for a slip.

### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/HoneyguideLab.html`: PASS (no console errors, desktop
  1600×900 fits, the phone does not scroll sideways, `__lab` hooks, `SPEC` ⊂ `SHIPPED` within range, the manual clock,
  `runBatch` deterministic within a page and across loads, stage not blank).
- `node Tools/Build/dynamo_lab/parity.cjs`: PASS. 21,624 steps over 24 scenarios (four engines × ports or whole engine ×
  three rule sets, one with synthetic one- and two-cell patterns so several copies are on the board at once): 1,302
  activations, 385 picks, 1,134 ticks, 47 research offers and 4,278 illegal inputs refused alike, every digest and event
  equal. 400 slip destinations equal to 1e-9. Four planted controls were each caught: ports turning counter-clockwise,
  the placement scan reversed (only once the synthetic patterns were added: L-STU-115), a tick of 8.25 s, the slip
  taking the other end.
- Mid-game screenshots at 40 s (desktop 1600 × 900 and an iPhone 13) were read. On the phone the Dynamo takes the lower
  68% of the stage in the Engineer seat, and cells are 43 × 50 px; the first layout had 36 px cells and slot cards
  below the fold, both fixed.
- On the iPhone 13 viewport, a drag through the real pointer path moved a muton one cell (1 AP) and a tap on the
  emptied port refuelled it (1 AP), with no page errors.

### What round 1 does not model

- The world is a flat 2D disc, not the 3D cell; trails are lines of dots. No crystals, elements, petals or rivals to
  fight; a hostile prism only slows the hull for a second.
- The race between destination rings is the lab's yardstick for "a slip shortened the route"; no game mode.
- Trails never loop and have no holes, so `IsLoop` and `IsRidable` are not exercised.
- Time, Space, Mass and Charge scaling and the L5 upgrades are fixed numbers here (brief §3.5).
- 27 of the 39 cards have no effect yet; the twelve effects are proposals.
- Two players on one device only; no networking.
- The Engineer AI never researches. A real thumb tires and misses; the AIs only have speed, noise and reaction time.

### Decisions needed (Garrett), on the Decisions tab

1. **Sign-off:** is the Engineer seat fun on your phone? (the acceptance gate)
2. **Slip arming** (question 3): hold-to-arm (the lab's candidate), always armed (the brief) or aim-gated (Found 1).
3. **What rotates** (1): ports only, or the whole engine.
4. **Pattern orientation** (2): fixed as printed, or rotatable (Found 6).
5. **Exit facing** (4): align to the trail (Found 8).
6. **Domain map** (5): M→Ruby, C→Jade, S→Gold, or own domain is M (Found 6).
7. **Tick, AP, battery** (6): 8 s / 3 AP / 3 (Found 4).
8. **Research** (7): 2 AP mid-match, plus the loadout of 3 at launch.
9. **Starter set and loadout** (8): which three cards fill the slots at launch.
10. **Engine at launch** (9): the 2020-22 board, Heimdall-13, Claw or co-op (Found 5).

## The port (phase 2, 2026-10-21)

Garrett asked for it before a written sign-off ("lets play it in unity"), so the round 4 lab is what was ported.
- **`VesselClassType.Honeyguide = 23`** (19 is the Wrasse, 20 the Orca, 21 the Starling, 22 the Marlin). It is in `ToyVesselRoster.Default` and
  `EndGameSequencer`'s Sparrow-hull rule.
- **`DynamoCore.cs`** is re-ported from the round 4 core, with `aiPlan` as `DynamoCore.AIPlan`. The parity gate points
  at `HoneyguideLab.html`.
- **The seat** is `HoneyguideCrew`, which follows the Wrasse's seat plumbing. The generic duo-vessel system
  (`CrewSeat`) had not landed on bleeding-edge by 2026-10-21.
- **The assets** are written from the page by `Tools/Build/author_honeyguide_assets.py` (`--check` holds the cards,
  shapes, effects, engines and ability words to the page).
- **Tests:** `HoneyguideTests.cs` carries a golden game from the page's own core.
- The design choices made in the port, and the 8 parked trigger-upgrade cards, are listed in `HONEYGUIDE.md`.

## Parity toolchain note

`trace.sh` needs a per-user .NET 8 under `$HOME/.dotnet`. Where `builds.dotnet.microsoft.com` is blocked (it was in this
session), the same layout can be assembled from `api.nuget.org/v3-flatcontainer`: `runtime.linux-x64.Microsoft.NETCore.DotNetHost`
(the `dotnet` host), `runtime.linux-x64.Microsoft.NETCore.DotNetHostResolver` (`host/fxr/<v>/libhostfxr.so`),
`Microsoft.NETCore.App.Runtime.linux-x64` (`shared/Microsoft.NETCore.App/<v>/`), `Microsoft.NETCore.App.Ref`
(`packs/Microsoft.NETCore.App.Ref/<v>/ref/net8.0`) and `Microsoft.Net.Compilers.Toolset`
(`sdk/<v>/Roslyn/bincore/csc.dll`). Both this gate and the Kite's pass on that layout.

## Publishing

The repo copy is the source. It is published as its own artifact with `capabilities: {db: {}, user: {}}` for the shared
decision log. Republish to the same URL so the log stays one log.
