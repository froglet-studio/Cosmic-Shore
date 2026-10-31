# Serpent Time: the Serpent's dirty race

The Serpent's **Time** game, prototyped in the Vessel Studio before the game changes (artifact → Unity, `/vessel-studio`
D37). Studio page: `Docs/Studios/VesselStudio/serpent.html`, in the one Vessel Studio artifact
(https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa). Started 2026-10-27 by Shombith; he will explain more as it goes, so
this file grows a round at a time.

**Where to fly it.** On its own (private, Shombith's; share it from its Share menu):
https://claude.ai/artifact/5TbEm7vjqTDWVhXBiNPAZK, built from the repo page minus the hub's Sync tag and links (as the
Shrike's standalone is). The hub card is on this branch (`claude/dazzling-ride-dpyvkx`, based on `Ys-bleeding-edge`), but
the one hub artifact belongs to an account outside the Froglet organisation, so this session could not publish to it: its
owner (Yash drives the Vessel Studio) republishes from this branch, or merges it into `Ys-bleeding-edge` with the Sync panel,
and the Serpent card appears there.

## 1. The brief (Shombith, 2026-10-27, his words)

> "we are gonna make the serpent and its time game the best game. This needs to be an dirty time game. I think there can
> be multiple rings in one place and they see it but serpents can spawn walls infront of them so others have to shoot it
> down or find another way to complete the race, this is a dirty game the ai needs to learn to play dirty as well in this
> game mode. The game mode I think is already there we will reshape it thats all. Also in order to collect sniper bullets
> they have to wonder to collect more crystsals as well. The cloak option make it cool as well."

Read as five requirements:

| # | Requirement | How round 1 reads it |
|---|---|---|
| R1 | A **Time** game: a race | Thread one ring of every gate, in order, two laps; first home wins (a `<Hull><Element>` mode, like Grizzly Time and Thresher Time) |
| R2 | **Several rings in one place**, all visible | Every gate is a cluster of 3 rings (I1, I2) or 2 (I3, I4); any one of them counts |
| R3 | **Serpents spawn walls** in front of the others; the others **shoot them down or find another way** | The shipped Seed Wall: cloak just past a ring (or stop in it) and a wall grows from the loose mass round the gate. The others shoot it with the Sniper Shot, ram it, or take another ring of the gate |
| R4 | **Sniper rounds cost crystals**, and the crystals make you wander | A round per crystal (1 at the start, 3 at most); crystals sit 150 to 350 u off the racing line |
| R5 | The **AI plays dirty** too, and **learns** to | Four AI play styles that wall, stop, shoot and snipe (hand-written brains, round 1). Learning comes with the Unity port: the race is recorded and learnt from automatically (CLAUDE.md, "AI learning is automatic") |
| R6 | The **cloak** stays and is part of it | RT unscoped: you vanish for 15 s and leave a super-shielded seed (and a decoy of your hull) behind you |

## 2. What round 1 models

**The Serpent is the shipped one** (every number cites its asset; `python3 Tools/Build/element_ability_table.py Serpent`):

| Element | Ability | Pad | Keys | Phone | Scales with the element | L5 |
|---|---|---|---|---|---|---|
| Space | Scope (the eyepiece) | LT, depth = zoom | Left Shift | SCOPE latch, full zoom | 22° → 11° at full zoom (floor 8°) | Deep Focus (× 1.6 zoom) |
| Charge | Sniper Shot | RT while scoped | Right Shift / F | RT while the scope is on | 12 s → 5.4 s recovery | Pierce (breaks super-shielded mass) |
| Mass | Seed Wall | B (stop) or RT unscoped (cloak) | T / Right Shift | B / RT | bricks × 1 → × 2 | Lockdown (a crystal refills your walls' holes) |
| Time | Solid Fuel Pellets | A | Space | A | burn 3 s → 4.8 s | none |

The round goes the full 3,000 u through every hostile prism in its 1.5° cone (radius floored at 6 u), never your own,
and skips super-shielded mass until Pierce: `SniperShotActionExecutor.IsValidTarget`, which is the record where the docs
disagree (the scope doc and the ability map describe Pierce differently).

**The race is a design (lab-only, every number a proposal on the page's Game Config tab):**

- **Courses.** I1 flat loop, 8 gates of 3 rings; I2 tilted loop, 10 of 3; I3 saddle, 12 of 2; I4 knot, 12 of 2 (smaller
  rings). Two laps. The mode has no scene yet, so these are not game data.
- **Wall fuel.** 48 loose debris prisms round each gate (they grow back) plus the trail every hull lays (a prism every 8 u,
  gone after 30 s). A wall steals from both, so a gate everybody flies through builds the wall that plugs it.
- **What a wall does.** A bare brick costs half your speed; a shielded brick or a seed bounces you to 10 u/s. Your own
  walls let you through. A wall lives 45 s and holds 160 bricks at most.
- **Snipes.** A rival in the cone loses one level of every element (`vesselStripPerElement` 0.1); no stun by default.
- **The AI.** Input-only (sticks, triggers, A, B). It picks the ring of each gate it can get through cheapest, threads a gap
  in a part-built wall, routes round a wall across its path, backs off to get range for a shot, and rams through bricks
  (never at a seed) when it has no round. Dirty: just past a ring a rival still needs it cloaks to leave a seed there; a
  Bricklayer that leads stops in the gate it just threaded. Play styles: **Dirty** (5/5/5/5, every L5), **Racer** (Time 10),
  **Bricklayer** (Mass 10, Space 8), **Marksman** (Charge 10), **Shipped** (all 0).
- **Levels.** Hard is the skilled pilot; Medium and Easy add the Skim Race levels' two mistakes on every gate (notice it
  0.25 / 0.5 s late; misjudge 4.5% / 9.9% of gates, believing the ring two radii to one side) and on every wall (notice it
  late; believe a walled ring is open), plus an aim error (Easy 2.0°, Medium 1.2°, Hard 0.5°).

## 3. Round 1 scorecard (2026-10-27, measured headless in the page; times in seconds)

| Course | Hard · Medium · Easy (one Dirty pilot vs three Hard, 8 seeds, median) | Dirty field (all four, median finish) | No walls (control) | Walls / race | Wall shots | Crashes | Snipes | Style wins Dirty / Racer / Bricklayer / Marksman |
|---|---|---|---|---|---|---|---|---|
| I1 | 87.3 · 90.1 · 101.5 | 87.5 | 82.6 | 7.3 | 1.3 | 10.7 | 0.7 | 4 / 0 / 0 / 0 |
| I2 | 96.1 · 102.3 · 112.8 | 97.9 | 95.6 | 13.0 | 0.7 | 7.0 | 3.3 | 0 / 4 / 0 / 0 |
| I3 | 124.5 · 127.1 · 131.5 | 121.4 | 104.3 | 10.7 | 3.3 | 10.7 | 3.7 | 4 / 0 / 0 / 0 |
| I4 | 175.9 · 175.3 · 184.8 | 196.3 | 176.6 | 13.3 | 3.3 | 27.7 | 1.0 | 1 / 3 / 0 / 0 |

What it says:

1. **Walls matter, and more on the hard courses.** The dirty field finishes 5 s slower than the no-walls control on I1, 17 s on
   I3 and 20 s on I4. The leader lays the walls, so the field pays for them, not the winner.
2. **The level ladder holds on I1 to I3** (Hard < Medium < Easy). On I4, Hard and Medium tie. The gaps are smaller than the
   game's Skim Race (Medium +2 to 6%, Easy +6 to 17% here, against 14 to 19% and 23 to 54% there). Four seeds were not
   enough to order Hard and Medium, so the column uses eight.
3. **Only Dirty and Racer ever win.** Racer wins the two longer courses on its 4.8 s pellet burns; the Bricklayer's stance
   costs more time than its walls take from others, and the Marksman's detours for rounds cost more than its snipes. For the
   designer to call: is that the balance we want, or should walling or sniping pay more?
4. **Sniping pilots is rare** (1 to 4 a race) and only strips levels. If the dirty game should hurt more, the stun row
   (Game Config, "Stun on a sniped pilot") is the lever to try.

## 4. Open decisions (Shombith's to make, with the vessel's owner where it touches the Serpent)

**Rule (Shombith, 2026-10-31): the vessel kit as designed always stays the same; the mode is made interesting by its
track, environment and AI, never by changing what an ability does.** That settles D-ST2, D-ST3 and D-ST5 below: no round
economy, a wall does to a hull what the shipped Seed Wall does, and a snipe does what the shipped Sniper Shot does. The
kit is Garrett's (#1642, merged 2026-10-31: Scope on Charge, Cloak / Sniper Shot on Space, Hunter's Sight, Fifth Pellet).

- **D-ST1 Ring rule.** Any ring of a gate counts (round 1), or each ring is worth something different (a far ring pays
  more)?
- **D-ST2 Round economy.** *Settled 2026-10-31: none (vessel rule above).* A crystal is one round (round 1). Should crystals also raise Charge (a faster rifle), as Serpent
  Charge's waves do?
- **D-ST3 What a wall does to a hull.** *Settled 2026-10-31: what the shipped wall does.* Bounce or half speed (round 1), or something else (a stun, a trail cut)?
- **D-ST4 Own walls.** Let their owner through (round 1), or block everyone (riskier dirty play)?
- **D-ST5 Snipe on a pilot.** *Settled 2026-10-31: the shipped rule.* Strip only (the game's rule), or add a stun in this mode?
- **D-ST6 The arena.** The page's four loops are placeholders. In Unity, a real scene: reuse a gate race's course
  (`GateRaceController` modes) or the cell the Serpent Charge branch cloned?

## 5. Bringing it to Unity (round 1 built, 2026-10-27, not yet flown in the editor)

"The game mode I think is already there": it was. **Serpent Time** (`GameModes.SerpentTime = 73`, the Serpent's Time
card, a lapped circuit on Headlong's solver) was authored the same day on `cece/confident-goodall-6cpxan` (Affects:
Garrett, who merges that branch). This branch merges it and reshapes it; no new mode id was claimed. Serpent Charge
(67, Garrett's sniper duel on `cece/vibrant-archimedes-n3xn3x`) is untouched.

What the game now does (details: `Assets/_Scripts/Controller/Arcade/SERPENTTIME.md`, "The dirty race"):

| Requirement | In the game |
|---|---|
| R1 a Time race | Serpent Time's circuit: 8 gates, 2 laps, golf |
| R2 several rings at a gate | Ring clusters, 3 at I1/I2, 2 at I3/I4, any one counts (`SerpentTimeCluster`) |
| R3 walls, shoot or go round | The shipped cloak lays a Seed Wall, which does what it does everywhere; the rifle clears it; another ring of the gate is open |
| R4 rounds cost crystals | **Retired 2026-10-31** (the mode never changes the vessel): the rifle's shipped cooldown is its only limit |
| R5 a dirty AI | Picks the open ring, shoots walls when its rifle is ready, cloaks just past a ring a rival still needs |
| R6 the cloak | Unchanged (the shipped `CloakSeedWallActionSO`) |

The gate debris came over too (48 loose prisms round each gate, one grown back per gate every 1.5 s, kept 20 u clear of
other gates). What the studio models and the game does not yet: the snipe's level strip on a pilot as a mode rule (the rifle's own hit rules apply),
and the four AI play styles (the game's AI is one dirty pilot at the lobby's difficulty).

## 6. Round log

| Round | Date | What changed | What it taught |
|---|---|---|---|
| 1 | 2026-10-27 | The page: the shipped Serpent, the race (R1 to R6), four AI styles, three levels, the scorecard above | (a) A cloak seed laid on top of a rival traps it bouncing in place: the seed is the Serpent's latest trail prism, behind it, and the AI routes round any wall across its path. (b) An AI that only aims for the ring circles it: the game's stick mix pins the hull at 25 u/s at full yaw (an 18 u turn), so the approach must line up on the ring's axis from far enough out, scaled by how far off the axis it is. (c) A sniper that starts lining up 280 u out and wants a 0.35° aim never fires: fire when the believed aim sits in the inner half of the cone at that distance, ease off the throttle while lining up, and back off for range. (d) Belief mistakes only on walls do not separate the levels, because few rings are walled; the Skim Race mistakes belong on every gate. (e) An InstancedMesh's colour attribute must exist before its first render, or the walls draw white (three r128). |
| 2 | 2026-10-27 | Unity: Serpent Time (73) reshaped into the dirty race (§5) | The gate race needed four hooks to stand several rings at a gate (detection, the AI's ring, the lit ring, a per-pilot crystal budget); the course's own ring is kept, hidden, because the objective arrow, the race toasts and the AI's commit all read it |
| 3 | 2026-10-31 | Unity: the round economy and the wall's speed cost retired; the three vessel files back to bleeding-edge; the AI shoots on the rifle's own cooldown and calls a ring walled by any rival prism in its mouth | A mode that rations an ability or adds a contact effect is changing the vessel. The track and the AI are the mode's levers. **The studio page (`serpent.html`) still models the round economy and the wall bounce; its next round removes them** |
| 4 | 2026-10-31 | Unity: rings grown over with danger prisms (1/2/1/2 covered per gate by intensity, regrowing), and the race moved into Rampage's four living cells (reversed: sparse at I1, dense at I4). Shombith: "rings get covered by prisms we shoot and find our way"; no glass gates, nothing added to the pellets | A plain prism costs a Serpent nothing to ram (it is destroyed, the hull keeps its speed), so a cover only matters as danger; the cell's own Mass and Charge flora are already the obstacles a race wants |
| 5 | 2026-10-31 | The cover mixes plain and danger slabs, 15 / 30 / 50 / 70 % danger at I1-I4, scattered (Shombith: "it can be both, keep the element of surprise, on lower intensities it is mostly normal prisms") | A ring's cost is now read only up close, which is what makes the scope worth raising |
| 6 | 2026-10-31 | The studio page (round 2 of `serpent.html`) matches the game: Garrett's recut kit (#1642: Charge Scope, Space Cloak / Sniper Shot, L5s Hunter's Sight / Lockdown / Shield Steal / Fifth Pellet, scope 60 to 12 deg, the round always pierces), no rounds (the rifle's cooldown is its only limit), a bare brick breaks with no speed lost, and the covered rings ported with the game's own hashes (1/2/1/2 covered, 15/30/50/70 % danger, regrow 1.5 s; a danger slab debuffs every element a level for 4 s). Headless, seed 7: every course finishes; danger hits 0 / 1 / 8 / 37 at I1-I4; at I4 the Racer (most danger hits, its burns shortened) hit the 260 s limit at gate 21 of 25 in one seed of four | The page now drifts from the game only where it says so (its placeholder courses, no Rampage forest, Hunter's Sight / Lockdown / Shield Steal not modelled) |
| 7 | 2026-10-31 | Unity: the slab cover is gone ("it won't make a hole big enough"); a gate stands 2 rings at I1-I2 and 1 at I3-I4, and one ring of each gate is guarded by a living Borromean flora (Time plain, Mass shielded, Charge danger, rising with intensity). Back in the Skim Race cell for frame time; markers added in the mode and the wall | The obstacle should be a lifeform, not a grid: flora already carry the plain / armoured / danger ladder by element. **The studio page still models the slab cover and the old ring counts; its next round moves to guards** |
| 8 | 2026-10-31 | Unity: hit a guard's heart and the whole plant falls (Shombith: "if I hit the middle of it the whole flora destroys clearing the way"). A hull within 9 u of the heart, or a sniper round through it, bursts every plate outward from the heart and the plant dies the ordinary flora death. The AI shoots a Mass or Charge guard's heart (one-ring gates included) and flies through a Time guard's | A plant's middle is its heart, the one place every element's membrane leaves open; the rifle and the hull are unchanged, the plant answers being hit there. The studio page still lacks guards |
| 9 | 2026-10-31 | Named **Serpentine** (display only). Guards stand before, beside, or either side of (a door) their ring, not in it; the start is a ring behind gate 0 pointed through it; the cytoplasm's shards point along the race line, a current through the cell; the race runs in its own copy of the Skim Race cell with half the grazers. Picked by Shombith from four options each before any of it was built | The track is shown by the cell itself, not by a drawn line. The studio page still lacks guards and the current |
| 10 | 2026-10-31 | Unity: **no fauna** in Serpentine's cell (Shombith: "very non performative, lets remove the fauna"); the half-fauna cut of round 9 was not enough. Nothing grazes the guards or the walls now | Frame time first; a capture with the markers is still the way to find what else costs |
| 11 | 2026-10-31 | Unity: **one ring a gate at every intensity**, sized for a large hull: 65 / 55 / 48 / 40 u (was 54 / 41 u cluster rings at I1-I2, 52 at I3, 40 at I4). With no open ring beside it, **every gate has a guard** (before, beside or a door). Both picked by Shombith from options | I4's 40 u was the right size; the early rings were too small for the Serpent |
| 12 | 2026-10-31 | Unity: the flora guards are gone ("rather than using this flora boring one"). Every ring (now Breakwater's 72 / 60 / 50 / 42 u) is closed by a mode-laid shape after Breakwater's stations (`SerpentineGate`): a plain shell flaring back, a danger weave across the mouth with 2-3 exits for the Serpent, a gold core. Coil at I1-I2, Maw at I3, Iris at I4; Breakwater's danger rule; bigger rings - all picked by Shombith from a sketch of three shapes. Break a core block and the whole shape disintegrates; it grows back 25 s later | A shape the mode lays is cheaper than a plant (no spindles, growth or grazing), the same on every peer, and can be designed; a plant's middle was a heart only the Squirrel could take |
| 13 | 2026-10-31 | Unity: each intensity a different place - its own cell config, creatures (at most 25 live) and tint: I1 Shallows, I2 Grazing Grounds, I3 Hunting Grounds, I4 The Deep - and the core shrinks (7 x 6 u, 3 x 5 u, 1 x 5 u, 1 x 3.5 u) so the shot gets harder. All three picked by Shombith from options ("intensities are not changing enough") | An intensity is a place, not a number; the fauna is back, capped |
