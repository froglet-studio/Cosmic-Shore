# Honeyguide patterns and abilities

Lab round 6, 2026-10-21; the triggers are round 8 (2026-11-01). Source of truth: `Docs/Studios/HoneyguideLab.html` on `claude/honeyguide-ratel` (the lab's Patterns tab draws every shape). This is the list the Unity port copies. The Unity build is still round 4; the rows below marked RT or LT changed in rounds 5 and 6.

## How a pattern works

- The Dynamo is a hex board of 37 cells (radius 3). The Ratel moves and injects prisms to build the shapes on the three cards it holds.
- A shape matches in any of its six rotations. With the Mirror ability, mirror images match too.
- An empty injection port counts as a prism of its colour, but every match needs at least one real prism.
- Making a pattern costs 1 AP. One of its real prisms, drawn at random, goes to the battery as a slip charge of its domain, and the pattern fires its own effect.
- A pattern made for the third time is replaced by a random one. Every 4 patterns the Ratel is offered three abilities and picks one (round 7, Garrett 2026-10-29), then gets a new port pattern. The spin clock stops for that: up to 10 s to pick (then the first offer is taken), then 2 s to see the new ports, then the clock restarts.
- The port pattern turns 60° clockwise every 15 s, shown as a shrinking bar across the top. Each spin gives 3 AP; unspent AP are kept up to 6 (round 7; was 3). The battery holds 3.

**Colours:** R = Ruby, J = Jade, G = Gold. The tabletop print's M, C and S map to R, J and G. **Cells** are axial hex coordinates (q, r), the same as `DynamoCore`: E is (+1, 0) and SE is (0, +1). Shapes are listed as printed; their position on the board does not matter.

**Pattern names** are the tabletop upgrade-card names (Garrett, 2026-10-21: keep them). Each pattern's effect is converted from its card: from the card's text where it has a real-time reading, otherwise from its name. Mutons in the card text are now prisms.

## The triggers (round 8, 2026-11-01)

Round 8 replaces the round 6 trigger rules below (kept for the record). Garrett: RT is a Space active that always cuts
opposing prism volume, LT a Charge active that always strikes rival vessels and creatures; both are pulled to preview
and released to fire, never analog; each upgrade card banks a use (a tick) on its trigger, the newest banked ability is
current and fires first. Time is the Slip; Mass is the passive hive. The full list (each ability's preview and release)
is in `Docs/Studios/HONEYGUIDE_LAB.md`, round 8.

| Trigger | Base | Upgrades (banked, newest first) |
|---|---|---|
| **RT, Space** | Peck: pull the aim line, release three shots | Genesis Lance, Little Bang Grenade, Particle Harmonizer, Fugacity Discharger, Sepia Drone, Thruster Swarm, Hexapod Eyelet Jet |
| **LT, Charge** | Flit: pull an 18 u dart, release it to strike what it passes | Hard Light Lasso, Blink Drive, All Terrain Portals, Rosen Bridge, FTL Chromatool, Monte Cannon |

## The triggers (round 6, superseded by round 8)

Garrett, 2026-10-21: "the right trigger should always be a weak attack power that gets upgraded sometimes. the left should
be a movement power that works similar. the 39 patterns should still be unique." The Honeyguide has no guns.

| Trigger | Base power (always there) | Upgraded by |
|---|---|---|
| **RT** (E or K) | **Peck**: a weak shot while held, 2.5 a second, 45 u. | 6 attack cards: Hard Light Lasso, Genesis Lance, Little Bang Grenade, Sepia Drone, Particle Harmonizer, Fugacity Discharger |
| **LT** (Q or J) | **Flit**: an 18 u hop ahead, every 2 s. | 7 move cards: Eyelet Jet, Thruster Swarm, Blink Drive, All Terrain Portals, Rosen Bridge, FTL Chromatool, Monte Cannon |

- An upgrade takes its trigger over for **20 s**, its time running down the bottom of the trigger's card, then Peck or
  Flit comes back.
- Making the same card again refreshes it and raises its level (reach +25% a level, up to III). A different upgrade for
  the same trigger replaces the one there.
- Every upgrade is one button, with no combos. It is used one of three ways:
  - **press**;
  - **hold** (Particle Harmonizer);
  - **hold to charge and let go** (Fugacity Discharger).
- The other 26 cards keep their own timed passives and one-off effects, so every one of the 39 is unique.
- With the hard AI crew, an upgrade is on a trigger about a third of the time, about 1.5 a minute. Move upgrades come up
  about half as often as attack upgrades: five of the seven are Gold-family shapes, which are harder to make.

## How the effects work

- **RT upgrade / LT upgrade**: takes that trigger for 20 s (above).
- **Timed, stacks**: the effect lasts 20 s. Making the pattern again refreshes it and raises its level, up to III.
- **Once**: the effect happens when the pattern is made.
- Every jump is a teleport through `SetPose`. In round 4 the jumps were stored on the Slip card; since round 6 they are LT upgrades.
- **Seat** says who an effect helps. *Ratel* effects change the Dynamo itself.
- **Lift alone** is the race speed when every pattern fires that one effect, against patterns that fire nothing. It is measured with the AI pilot and AI Ratel over 12 seeds × 180 s, with an SE of about ±3%, so a difference under about 6% is noise. Zero Point Amplifier and Meta Machine copy other effects, so they have no number of their own. For the 13 upgrades, the number is from rounds 5 and 6: every pattern made deals that upgrade. RT upgrades are measured against no triggers at all (round 5, 4.11 legs a minute). LT upgrades are measured against Peck and Flit only (round 6, 5.11).

## Ruby family (13)

| # | Pattern | Tabletop card | Effect now | Kind | Seat | Lift alone |
|---|---|---|---|---|---|---|
| 1 | **Juggernaut Plow** | Boost 4, pushing any ships or prisms one space out of the way. | Plough through hostile prisms: they break on contact and do not slow you. +1 s of boost now. | Timed, stacks | Hull | +22% |
| 2 | **Singularity Dock** | Move your ship up to 6 spaces and end adjacent to a prism or ship. | Slips catch trails 6 u farther away a level: the dock pulls you in. | Timed, stacks | Slip | +1% |
| 3 | **Amanita Furnace** | Destroy 1 prism in your Engine and Boost up to 6. | Burns one more prism on the Dynamo for 3 s of boost. | Once | Hull | +34% |
| 4 | **Plankter Sail** | Slip up to 4 spaces then propel. (You do not need prisms in your battery to take this action) | Your next slip needs no charge and rides a trail of any domain, at half reach, then 1 s of boost. | Once | Slip | +2% |
| 5 | **Anomaly Skipper** | Move up to 5 spaces, each movement must change what color space your ship is in | Touching a prism of a different domain from the last gives 0.4 s of boost a level. | Timed, stacks | Hull | +17% |
| 6 | **Fugacity Discharger** | Move 2 for each prism and Ship in your sector. | Hold RT to charge, let go to discharge: every hostile prism within 12 to 40 u (by charge) shatters, and each gives 0.05 s of boost (up to 1.5 s). | RT upgrade | RT (attack) | +44% |
| 7 | **Thruster Swarm** | Move your ship or map prisms 1 space 4 times | LT: a quick 20 u thrust, again every 0.4 s, shattering what it crosses. | LT upgrade | LT (move) | +26% |
| 8 | **Hard Light Lasso** | Move one prism on the map, You may then move your ship up to 3 spaces closer to that prism. | RT: a rope of light hooks the nearest hostile prism ahead (90 u, 30 degrees) and reels you to it at speed. It and every hostile prism within 10 u shatter when you arrive. | RT upgrade | RT (attack) | +31% |
| 9 | **Big Red Button** | Destroy any prisms of 1 color in your engine then move 2 spaces for each prism destroyed. | Destroys every other Dynamo prism of the battery prism's colour; 0.5 s of boost for each. | Once | Hull | +2% |
| 10 | **Eigen Booster** | Make up to 3 diagonal movements. | Turn 60% faster a level. | Timed, stacks | Hull | +12% |
| 11 | **Dragon Saddle** | Drain any number of prisms and boost 3 per prism | Drains the whole battery into boost: 1.5 s for each charge. | Once | Hull | +22% |
| 12 | **Wattslider** | Freely move diagonally, while maintaining distance to a prism or ship. | Flying alongside a trail (within 14 u, heading within 30 degrees of it): +30% speed a level. | Timed, stacks | Hull | +2% |
| 13 | **Eyelet Jet** | Move 3 for each direction you can not propel. | LT: jet 50 u ahead in a quarter of a second, shattering every hostile prism on the way. | LT upgrade | LT (move) | +23% |

## Jade family (13)

| # | Pattern | Tabletop card | Effect now | Kind | Seat | Lift alone |
|---|---|---|---|---|---|---|
| 14 | **Reality Detangler** | Change the color of any 3 prisms. | The next 3 prisms you touch turn your newest charge's domain: new slip lanes, and they do not slow you. | Once | Slip | -4% |
| 15 | **Epitaxial Condenser** | Create a 5 prism chain of the same color by placing 4 prisms. | Lays a 6-prism trail of your newest charge's domain ahead of you. | Once | Slip | -8% |
| 16 | **Sepia Drone** | Draw two ship cards and attach either to Sepia Drone. | RT: launch a drone that circles you for 8 s and zaps the nearest hostile prism within 50 u three times a second. | RT upgrade | RT (attack) | −5% |
| 17 | **Zero Point Amplifier** | Activate one of your other upgrades and drain a prism. | Fires the effect of another pattern in the Ratel's hand, and drains your oldest charge into 1.5 s of boost. | Once | Ratel | copies another |
| 18 | **Noodley Appendages** | Freely move up to 5 prisms in adjacent sectors. | The next 5 hostile prisms in your path are pushed aside instead of hitting you. | Once | Hull | -1% |
| 19 | **Triplesynth** | Create a connected group of each color prism in your engine. | Places a touching Ruby, Jade and Gold prism on empty Dynamo cells. | Once | Ratel | -3% |
| 20 | **Particle Harmonizer** | Create a connected group of 3 prisms anywhere on the map. | Hold RT: a beam turns hostile prisms in a narrow cone up to 55 u ahead to your newest charge's domain, 8 a second: new slip lanes. | RT upgrade | RT (attack) | +13% |
| 21 | **Genesis Lance** | Create 2 prisms in a nearby sector. | RT: throw a lance 120 u straight ahead. It shatters every hostile prism on its line and leaves a trail of your newest charge's domain along it: fly on and slip. | RT upgrade | RT (attack) | +8% |
| 22 | **Universal Translator** | Move a prism from anywhere to anywhere. You may then change its color. | The Ratel gets a free prism of any colour: tap an empty cell. | Once | Ratel | -5% |
| 23 | **Anode Ray** | Create 3 prisms in your battery | +1 charge of each colour. | Once | Slip | +2% |
| 24 | **5D Printer** | Create 2 prisms in your Engine. | The Ratel gets 2 free prisms of any colour. | Once | Ratel | -4% |
| 25 | **Square-Peg Hammer** | Create 3 prisms in nearby void spaces | Drops 3 prisms of random colours on empty Dynamo cells. | Once | Ratel | -2% |
| 26 | **Reactor Cilia** | Store 3 / Take a bonus action in your engine phase whenever you remove a prism from this card. (Limit 6) | The Ratel banks 3 extra AP. | Once | Ratel | -2% |

## Gold family (13)

| # | Pattern | Tabletop card | Effect now | Kind | Seat | Lift alone |
|---|---|---|---|---|---|---|
| 27 | **Chrono-Compressor** | Store 1. / In your Pilot phase, remove prisms from this card as a free action, take a bonus action whenever you remove a prism. (Limit 1 prism) | The Ratel banks 1 extra AP and the spin bar winds back 5 s. | Once | Ratel | -3% |
| 28 | **Meta Machine** | Activate a face up card in any upgrade discard pile. | Fires the effect of the last pattern replaced (or else the last other pattern made). | Once | Ratel | copies another |
| 29 | **Mystic Eti** | Create an stable sector, charging any overlapped prisms. Place any number of prisms from your battery onto the new tile. | +1 charge for each domain with a trail within 40 u of you. | Once | Slip | -4% |
| 30 | **FTL Chromatool** | Swap with a prism up to 4 spaces away, and place that prism on this upgrade / As a free action you may remove a prism of each color to teleport adjacent to a void in an adjacent sector | LT: swap places with the nearest prism ahead within 60 u; it becomes a charge of its domain. | LT upgrade | LT (move) | −1% |
| 31 | **Little Bang Grenade** | Create a new sector along any edge of the board -- and -- You may teleport 4 into the new sector if able | RT: lob a grenade 55 u ahead. It bursts 0.6 s later and shatters every hostile prism within 22 u. | RT upgrade | RT (attack) | +12% |
| 32 | **Vorticity Module** | Rotate a sector tile. | The injection ports turn back 60 degrees and the spin bar refills. | Once | Ratel | -4% |
| 33 | **Gravity Engine** | Drag a sector 2 and boost into it | Slips catch trails 3 u farther away and end with 1 s of boost, a level. | Timed, stacks | Slip | +29% |
| 34 | **All Terrain Portals** | Teleport 2. If you jumped void space you may teleport 2 again. | LT: teleport 30 u ahead; land in empty space and you go 30 u again. | LT upgrade | LT (move) | +21% |
| 35 | **Rosen Bridge** | Teleport your ship to a matching color space in the same sector. | LT: teleport to the farthest prism of your newest charge's domain within 300 u and 35 degrees of your nose. | LT upgrade | LT (move) | +16% |
| 36 | **Frame Dragger** | Drag sector tiles up to 3 spaces. | Your trail is laid in your newest charge's domain: a slip lane behind you. | Timed, stacks | Slip | -5% |
| 37 | **Void Jumper** | Orbit a void | Slip on trails of any domain (each slip spends your oldest charge). | Timed, stacks | Slip | -6% |
| 38 | **Blink Drive** | Teleport 3 spaces. | LT: blink 60 u ahead. | LT upgrade | LT (move) | +13% |
| 39 | **Monte Cannon** | Swap with a ship or prism up to 3 spaces away. / Use this ability up to 3 times per activation. | LT: three hops, each to the farthest prism ahead within 40 u. | LT upgrade | LT (move) | 0% |

## The Ratel's abilities (one held at a time; proposals)

| Ability | What it does |
|---|---|
| **Overclock** | +1 AP every spin; the bank holds 1 more. |
| **Long Reach** | Drag a prism to any empty cell you can reach through empty cells, for 1 AP. |
| **Printer** | A free prism of any colour every spin (hold 2): tap an empty cell. |
| **Swap** | Drag a prism onto a neighbouring prism to swap them, for 1 AP. |
| **Free Fire** | Making a pattern costs no AP. |
| **Wild Core** | The centre cell counts as any colour. |
| **Capacitor** | The battery holds 2 more charges. |
| **Mirror** | Patterns also match mirrored. |
| **Refund** | Each pattern made banks 1 extra AP (hold 2). |

In the lab, Printer doubles the Ratel's pattern rate. Mirror and Swap made no measurable difference (round 2, 24 seeds).

## The injection port patterns

These are the four designed engines. Every one is three-fold colour symmetric: a 120° turn puts each port on a port, and each colour moves to the next colour. The lab plays each engine in its 6 rotations × 6 colourings. That gives 12 distinct port patterns per engine, 48 in all. The random scatters from round 2 are gone. `__lab.portCheck()` checks the symmetry.

**2020-22 player board**

```
   · · · ·
  · R J J ·
 · G · · J ·
· G · + · G ·
 · G · · R ·
  · J R R ·
   · · · ·
```

**Heimdall-13**

```
   · · · ·
  · G G G ·
 · J · · R ·
· R · + · J ·
 · R · · J ·
  · R G J ·
   · · · ·
```

**Claw that rends the sky (fixed: the print was missing the Jade port at 1,-1)**

```
   · R · ·
  · R · · ·
 · R · J G ·
· · G + · G ·
 · · · R · G
  J J J · ·
   · · · ·
```

**Apr 2021 co-op engine**

```
   J · · ·
  · · J · ·
 · J · R G ·
· · G + · · G
 · R · J G ·
  · · R · ·
   R · · ·
```

`+` is the centre and `·` an empty cell.

## Open for the port

- [ ] The effects and their numbers are proposals. Play them and mark the ones to change.
- [ ] Boost is worth more than slips in the lab. Amanita Furnace (+34%) and Dragon Saddle (+22%) turn charges into boost and beat every slip effect. The lab's boost is the Sparrow's: 5× the throttle term.
- [ ] Two effects lay trails ahead of the hull or turn the prisms it touches: Epitaxial Condenser and Reality Detangler. The AI pilot dodges those trails instead of riding them, so their measured lift (−8% and −4%) undersells them. A person should judge these.
- [ ] Peck and Flit numbers (2.5 shots a second over 45 u; an 18 u hop every 2 s), and the 20 s upgrade life.
- [ ] Monte Cannon, FTL Chromatool and Sepia Drone do little in the lab's race. The two jumps need prisms lined up ahead; the drone's kills are not scored. Play them before cutting them.
- [ ] Move upgrades come up half as often as attack upgrades. Weight the Gold family in the Ratel's draw?
- [ ] Port round 6 to Unity. The Unity build still has the Sparrow's guns and round 4's stored jumps.
- [ ] AP per spin: with the 15 s spin and 3 AP, the AI Ratel makes about 4.5 patterns a minute. With 6 AP it makes 7.5, and the lift rises from +11% to +23%.
