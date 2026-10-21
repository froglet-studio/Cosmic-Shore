# Honeyguide patterns, abilities and passives

Lab round 3, 2026-10-21. Source of truth: `Docs/Studios/HoneyguideLab.html` on `claude/honeyguide-ratel` (the lab's Patterns tab draws every shape). This is the list the Unity port copies.

## How a pattern works

- The Dynamo is a hex board of 37 cells (radius 3). The Ratel moves and injects prisms to build the shapes on the three cards it holds.
- A shape matches in any of its six rotations. With the Mirror ability, mirror images match too.
- An empty injection port counts as a prism of its colour, but every match needs at least one real prism.
- Making a pattern costs 1 AP. One of its real prisms, drawn at random, goes to the battery as a slip charge of its domain. The pilot gets the card's passive.
- A pattern made for the third time is replaced by a random one. Every 4 patterns the Ratel gets a new random ability and a new port pattern. The spin clock stops for that: 3.5 s to read the ability, then 2 s to see the new ports, then the clock restarts.
- The port pattern turns 60° clockwise every 15 s, shown as a shrinking bar across the top. Each spin gives 3 AP (bank 3). The battery holds 3.

**Colours:** R = Ruby, J = Jade, G = Gold. The tabletop print's M, C and S map to R, J and G. **Cells** are axial hex coordinates (q, r), the same as `DynamoCore`: E is (+1, 0) and SE is (0, +1). Shapes are listed as printed; their position on the board does not matter.

**Pattern names** are the tabletop upgrade-card names. Each family hands out its passives in card order, so the pattern-to-passive table is a proposal.

## Ruby family (13): passives for the triggers

| # | Pattern | Prisms (R / J / G) | Passive | Cells (colour q,r) |
|---|---|---|---|---|
| 1 | **Juggernaut Plow** | 4 (2 / 1 / 1) | Rapid Fire | J 2,0 · R 1,1 · G 0,2 · R 0,3 |
| 2 | **Singularity Dock** | 5 (3 / 1 / 1) | Piercing | G 3,0 · R 2,1 · R 0,2 · J 1,2 · R 1,3 |
| 3 | **Amanita Furnace** | 5 (3 / 1 / 1) | Scatter | J 3,0 · R 2,1 · R 0,2 · G 1,2 · R 1,3 |
| 4 | **Plankter Sail** | 5 (3 / 1 / 1) | Long Barrel | R 3,0 · R 1,1 · G 2,1 · J 1,2 · R 0,3 |
| 5 | **Anomaly Skipper** | 4 (2 / 1 / 1) | Seeker | R 3,0 · G 2,1 · J 1,2 · R 0,3 |
| 6 | **Fugacity Discharger** | 5 (3 / 1 / 1) | Blast | G 2,0 · R 3,0 · R 1,1 · J 0,2 · R 0,3 |
| 7 | **Thruster Swarm** | 4 (2 / 1 / 1) | Rapid Fire | J 2,0 · R 3,0 · R 1,1 · G 0,2 |
| 8 | **Hard Light Lasso** | 4 (2 / 1 / 1) | Piercing | G 1,0 · R 2,0 · R 0,1 · J 1,1 |
| 9 | **Big Red Button** | 4 (2 / 1 / 1) | Scatter | R 2,0 · R 0,1 · J 1,1 · G 0,2 |
| 10 | **Eigen Booster** | 5 (3 / 1 / 1) | Long Barrel | R 2,0 · R 0,1 · J 1,1 · G 0,2 · R 1,2 |
| 11 | **Dragon Saddle** | 4 (2 / 1 / 1) | Seeker | R 2,0 · R 0,1 · G 1,1 · J 0,2 |
| 12 | **Wattslider** | 5 (3 / 1 / 1) | Blast | G 3,0 · R 4,0 · R 2,1 · R 0,2 · J 1,2 |
| 13 | **Eyelet Jet** | 5 (3 / 1 / 1) | Rapid Fire | R 2,0 · R 0,1 · G 1,1 · J 0,2 · R 1,2 |

## Jade family (13): passives for the hull (and Magnet for the slip)

| # | Pattern | Prisms (R / J / G) | Passive | Cells (colour q,r) |
|---|---|---|---|---|
| 14 | **Reality Detangler** | 4 (1 / 2 / 1) | Overdrive | J 2,0 · J 0,1 · G 1,1 · R 0,2 |
| 15 | **Epitaxial Condenser** | 4 (1 / 2 / 1) | Shield | R 1,0 · J 2,0 · J 0,1 · G 1,1 |
| 16 | **Sepia Drone** | 5 (1 / 3 / 1) | Agile | J 2,0 · J 0,1 · R 1,1 · G 0,2 · J 1,2 |
| 17 | **Zero Point Amplifier** | 4 (1 / 2 / 1) | Afterburner | G 2,0 · J 1,1 · R 0,2 · J 0,3 |
| 18 | **Noodley Appendages** | 5 (1 / 3 / 1) | Magnet | J 3,0 · J 1,1 · R 2,1 · G 1,2 · J 0,3 |
| 19 | **Triplesynth** | 5 (1 / 3 / 1) | Overdrive | R 3,0 · J 2,1 · J 0,2 · G 1,2 · J 1,3 |
| 20 | **Particle Harmonizer** | 4 (1 / 2 / 1) | Shield | R 2,0 · J 1,1 · G 0,2 · J 0,3 |
| 21 | **Genesis Lance** | 5 (1 / 3 / 1) | Agile | J 2,0 · J 0,1 · G 1,1 · R 0,2 · J 1,2 |
| 22 | **Universal Translator** | 4 (1 / 2 / 1) | Afterburner | J 2,0 · J 0,1 · R 1,1 · G 0,2 |
| 23 | **Anode Ray** | 5 (1 / 3 / 1) | Magnet | R 3,0 · J 4,0 · J 2,1 · J 0,2 · G 1,2 |
| 24 | **5D Printer** | 5 (1 / 3 / 1) | Overdrive | G 3,0 · J 2,1 · J 0,2 · R 1,2 · J 1,3 |
| 25 | **Square-Peg Hammer** | 4 (1 / 2 / 1) | Shield | J 3,0 · R 2,1 · G 1,2 · J 0,3 |
| 26 | **Reactor Cilia** | 5 (1 / 3 / 1) | Agile | G 2,0 · J 3,0 · J 1,1 · R 0,2 · J 0,3 |

## Gold family (13): passives for the slip (and Chroma Rounds for the triggers)

| # | Pattern | Prisms (R / J / G) | Passive | Cells (colour q,r) |
|---|---|---|---|---|
| 27 | **Chrono-Compressor** | 4 (1 / 1 / 2) | Long Slip | G 2,0 · G 0,1 · J 1,1 · R 0,2 |
| 28 | **Meta Machine** | 5 (1 / 1 / 3) | Echo | R 2,0 · G 3,0 · G 1,1 · J 0,2 · G 0,3 |
| 29 | **Mystic Eti** | 5 (1 / 1 / 3) | Wake | G 3,0 · G 1,1 · J 2,1 · R 1,2 · G 0,3 |
| 30 | **FTL Chromatool** | 4 (1 / 1 / 2) | Chroma Rounds | J 2,0 · G 1,1 · R 0,2 · G 0,3 |
| 31 | **Little Bang Grenade** | 4 (1 / 1 / 2) | Long Slip | R 2,0 · G 1,1 · J 0,2 · G 0,3 |
| 32 | **Vorticity Module** | 5 (1 / 1 / 3) | Echo | J 3,0 · G 4,0 · G 2,1 · G 0,2 · R 1,2 |
| 33 | **Gravity Engine** | 5 (1 / 1 / 3) | Wake | G 2,0 · G 0,1 · R 1,1 · J 0,2 · G 1,2 |
| 34 | **All Terrain Portals** | 5 (1 / 1 / 3) | Chroma Rounds | G 2,0 · G 0,1 · J 1,1 · R 0,2 · G 1,2 |
| 35 | **Rosen Bridge** | 5 (1 / 1 / 3) | Long Slip | J 3,0 · G 2,1 · G 0,2 · R 1,2 · G 1,3 |
| 36 | **Frame Dragger** | 4 (1 / 1 / 2) | Echo | J 1,0 · G 2,0 · G 0,1 · R 1,1 |
| 37 | **Void Jumper** | 5 (1 / 1 / 3) | Wake | R 3,0 · G 2,1 · G 0,2 · J 1,2 · G 1,3 |
| 38 | **Blink Drive** | 4 (1 / 1 / 2) | Chroma Rounds | G 2,0 · G 0,1 · R 1,1 · J 0,2 |
| 39 | **Monte Cannon** | 4 (1 / 1 / 2) | Long Slip | G 3,0 · J 2,1 · R 1,2 · G 0,3 |

## The passives (all proposals)

| Passive | Seat it upgrades | What it does (per level, stacks to III, 20 s) | Given by |
|---|---|---|---|
| **Rapid Fire** | triggers | Both triggers fire 50% faster a level. | Juggernaut Plow, Thruster Swarm, Eyelet Jet |
| **Piercing** | triggers | Each shot passes through 1 more prism a level. | Singularity Dock, Hard Light Lasso |
| **Scatter** | triggers | Bolt adds 2 side shots and Burst 2 pellets a level. | Amanita Furnace, Big Red Button |
| **Long Barrel** | triggers | Shots fly 40% farther and faster a level. | Plankter Sail, Eigen Booster |
| **Seeker** | triggers | Shots curve toward hostile prisms ahead. | Anomaly Skipper, Dragon Saddle |
| **Blast** | triggers | A hit destroys hostile prisms around it (4 u + 3 u a level). | Fugacity Discharger, Wattslider |
| **Overdrive** | hull | +15% speed a level. | Reality Detangler, Triplesynth, 5D Printer |
| **Shield** | hull | Hostile prisms slow you 40% less a level. | Epitaxial Condenser, Particle Harmonizer, Square-Peg Hammer |
| **Agile** | hull | Turn 30% faster a level. | Sepia Drone, Genesis Lance, Reactor Cilia |
| **Afterburner** | hull | Boost refills 0.4 s a second a level. | Zero Point Amplifier, Universal Translator |
| **Magnet** | slip | Slips catch trails 3 u farther away a level. | Noodley Appendages, Anode Ray |
| **Long Slip** | slip | Slip reach +35% a level. | Chrono-Compressor, Little Bang Grenade, Rosen Bridge, Monte Cannon |
| **Echo** | slip | The next slip keeps its charge (one a level). | Meta Machine, Vorticity Module, Frame Dragger |
| **Wake** | slip | Your trail is laid in your newest charge's domain: a slip lane behind you. | Mystic Eti, Gravity Engine, Void Jumper |
| **Chroma Rounds** | triggers | Shots turn hostile prisms to your newest charge's domain instead of destroying them. | FTL Chromatool, All Terrain Portals, Blink Drive |

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

- Should the patterns keep the tabletop card names, or get new names?
- The passive numbers and the pattern-to-passive table are proposals.
- With the 15 s spin and 3 AP per spin, the AI Ratel makes 4.1 patterns a minute (it made 6.6 with the 8 s spin). The lift to the race is unchanged at about +25%.
