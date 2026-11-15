# The game kit roster: every missing elemental arcade game (2026-11-15)

Garrett, 2026-11-15, after the vessel polish pass (#1994): *"Ok now build any missing games. it is ok if we have a
game like the dolphin mass game missing because with its kit a mass game doesn't make sence. but it has a space game
so that is fine. similarly if a vessel has a great mass game then it is ok if we decide that we don't need to struggle
giving it a space game. but four games (one for each element is ideal) make all the missing games you can"*

This page is step 0 of the loop ([`PLAYBOOK.md`](PLAYBOOK.md)) for the whole fleet at once: which games are missing,
which ones each kit supports, which ones are skipped and why, and the **ids reserved for each game** so that sessions
building them in parallel never take the same number (the 2026-10-13 and 2026-11-10 collisions).

Measured on bleeding-edge @ `996e4788` with `python3 Tools/Build/elemental_loop.py` (the board reads the shipped
cards). Kits read from `Docs/FleetCodex/abilities.json` and each hull's `R_VesselActions/<HULL>*.md`.

## 1. What each element's game is

`ModeGenre.TryElementsFor` decides it from the card (`Docs/MassAndSpaceGames/PLAN.md` section 1): **Time** a race,
**Charge** working other pilots over (`Jousts`, `CombatPoints`), **Mass** making or taking mass and holding it
(`PrismsRemaining`, `PrismsStolen`, `VolumeRemaining`), **Space** taking mass off the board (`PrismsDestroyed`,
`VolumeDestroyed`, `LifeformsKilled`). A game turns the hull's own ability (or its omni crystal) for that element into
the score, and never changes the vessel (LOCKED, Shombith 2026-10-31).

A **joust** is the platform's overtake (a faster skimmer sweeping past a slower rival, `JOUST.md`), but only a hull
that carries `VesselExplosionBySkimmerEffect` can score one: today the Squirrel, Stoat, Shrike, Brittle Star and
Thresher. A hull whose Charge ability never touches a rival hull (Rhino, Urchin) gets a Charge game built on what its
kit really lands on a rival, not on a joust it cannot score.

## 2. The roster and the reserved ids

`GameModes` ids 91-127 and hint-toast ids 166-202 are reserved here, one each per game, in this order. A session
building a game takes ITS row's numbers, never "the next free id". The working names are placeholders: rename the game
freely before it lands (the generator mints guids from the name), but keep the ids.

| # | Vessel | Element | Working name | GameModes | Hint toast | Built on |
|---|---|---|---|---|---|---|
| 1 | Angler | Space | Chum | 91 | 166 | Paint: half the school balls up, let go to throw it at the painted prisms and eat them |
| 2 | Brittle Star | Mass | Starweave | 92 | 167 | Creation Mode: five trails from the ring's corners, the slowest mode |
| 3 | Thresher | Mass | Haul | 93 | 168 | Mass Mode: the ball steals rival prisms, shields yours and lays your trail |
| 4 | Serpent | Mass | Hedgerow | 94 | 169 | Seed Wall: stop dead, the seed pulls loose prisms into a wall, stealing rivals' |
| 5 | Serpent | Space | Longshot | 95 | 170 | Sniper Shot: one hitscan round breaks every hostile prism on its line |
| 6 | Swordfish | Time | Spearline | 96 | 171 | Ramp Spool: wind up in a straight line, the hull breaks what it rams |
| 7 | Swordfish | Charge | Riposte | 97 | 172 | Energize: the lit sword knocks a rival's petals loose |
| 8 | Swordfish | Mass | Palisade | 98 | 173 | Afterimage: the sword leaves prism copies of itself, walls you swing into place |
| 9 | Orca | Charge | Pod Hunt | 99 | 174 | Flown Missiles: the gunners steer missiles into rival hulls (crew vessel) |
| 10 | Orca | Mass | Ice Shelf | 100 | 175 | Danger Shield: a ring of super-shielded prisms that drops as lingering danger mass |
| 11 | Orca | Space | Depth Charge | 101 | 176 | Burst: missiles and the bomb burst on contact, wrecking rival prisms |
| 12 | Shrike | Charge | Larder | 102 | 177 | Targeting Array: paint a rival and every nest launches its missile |
| 13 | Shrike | Mass | Thornbush | 103 | 178 | Nest: dock, detach, leave a super-shielded cup crowned with a missile plant |
| 14 | Shrike | Space | Lancefall | 104 | 179 | Void Lance: a hitscan beam breaks every block it passes |
| 15 | Meerkat | Charge | Sentry | 105 | 180 | Sentinel Mode: each tunnel leaves a sentinel that zaps rivals |
| 16 | Meerkat | Mass | Warren | 106 | 181 | Exit Portal: each tunnel sets a hexagon of your prisms round its mouth |
| 17 | Meerkat | Space | Cave-in | 107 | 182 | Soliton: the tunnel sucks in prisms and fires them out to smash others |
| 18 | Honeyguide | Charge | Flitstrike | 108 | 183 | Flit: dart along the line, striking every rival ship on it |
| 19 | Honeyguide | Mass | Honeycomb | 109 | 184 | Hive: fly into a hive, the Ratel's hive puzzle pays out prisms |
| 20 | Honeyguide | Space | Woodpecker | 110 | 185 | Peck: three quick shots chip rival prisms ahead |
| 21 | Wrasse | Charge | Cleaner Ram | 111 | 186 | Kite Ram: a Charge-mote loop powers a ram that knocks petals loose (crew vessel) |
| 22 | Wrasse | Mass | Reefbuild | 112 | 187 | Mass Cluster: a Mass-mote loop lays a cluster of your prisms |
| 23 | Wrasse | Space | Blastline | 113 | 188 | Space Blast: a Space-mote loop blasts rival prisms where it closed |
| 24 | Goby | Charge | Snap | 114 | 189 | Vortex Sting: the Pistol's ring takes petals from a rival hull (crew vessel) |
| 25 | Goby | Mass | Den Builder | 115 | 190 | Dens: at each perch the Pistol builds a den, bigger for the energy it earned |
| 26 | Goby | Space | Riptide | 116 | 191 | Undertow pulls mass into the Pistol's plane, the vortex ring smashes it |
| 27 | Jacks | Charge | Stingherd | 117 | 192 | Danger Share: danger creatures of your herd sting rival hulls |
| 28 | Jacks | Space | Trawl | 118 | 193 | The net drags prisms; a dragged prism that hits another destroys both |
| 29 | Stoat | Charge | Shockfront | 119 | 194 | Ripple: a collapsing hole pair strips a petal of every element from each rival |
| 30 | Stoat | Space | Fulmen | 120 | 195 | Omni crystal: a branching bolt sets prisms burning; a crystal it reaches bolts again |
| 31 | Rhino | Charge | Bullrush | 121 | 196 | Charge on Ramp Spool's top speed (the plough never touches a hull; the Rhino has no joust effect, so its own thread picks what scores) |
| 32 | Rhino | Mass | Windrow | 122 | 197 | Blade and Berm: shove the field's prisms into your zone and hold them |
| 33 | Rhino | Space | Avalanche | 123 | 198 | Carry: a thrown prism smashes the next one; chain the field apart |
| 34 | Urchin | Charge | Railrush | 124 | 199 | Chain-spike hits off the rail (PR #1997) |
| 35 | Grizzly | Charge | Standoff | 125 | 200 | Weapon Systems and the Charged Cannon on rival hulls, Dig In to hold a post |
| 36-37 | Jewel Wasp | Time, Charge, Mass or Space | (after PR #1993 lands) | 126-127 | 201-202 | its reworked kit; designed last |

## 3. Skipped, and why

| Vessel | Element | Why no game |
|---|---|---|
| Dolphin | Mass | no mass-making ability (Garrett's own example); Rampage is its Space game. Already excused in `loop.json`. |
| Squirrel | Space | already excused in `loop.json`; Scurry is its Mass game. |
| Grizzly | Mass | Weapon Systems only destroys; there is nothing the Grizzly makes to score. Its Space game is Grizzly Charge (scores as Space). |
| Jacks | Mass | its Mass ability (The Call) moves the herd, and a herd is not mass; Trawl is its Space game. |
| Stoat | Mass | Heavy Launch is a harder kick out of the orbit and touches no mass; Fulmen is its Space game. |
| Urchin | Space | Track Projector lays trail (mass) and the spikes steal; nothing destroys. Hijack is its Mass game. |
| Starling | every element | its kit is not settled (`element_ability_table.py --gaps` still lists it) and it was left as it is on purpose (2026-11-07, 2026-11-15). Games wait for its kit. |

Each skip is recorded in `loop.json` `no_game_ok` (Mass and Space only; the tool requires a Time and a Charge game).
A skip is reversible: if a kit later gains the ability, the game is built and the excuse removed.

## 4. How each game is built

One branch per vessel, through `/arcadegame` (and `/duo-vessel` for the Orca, the Wrasse and the Goby, `/ecology`
first for the Angler and the Jacks). The nearest shipped siblings: Charge - Jackstraw, Kelpwood, Corona, Flatline;
Mass - Glasshouse, Tapestry, Scurry; Space - Harvest, Shatterplane, Rampage; Time - `hull_time_race.py` (Perch Run,
Lantern Run). Each game lands with its card art (`/cardart`), its doc, its row here marked built, and nothing run in
Unity is claimed: every one of these is authored headless until a person flies it.

## 5. Status

| # | Game | Branch / PR | State |
|---|---|---|---|
| 1 | Chum (Angler Space) | `claude/angler-space-chum` | built headless, not run in Unity (2026-11-15) |
| 2 | Starweave (Brittle Star Mass) | `claude/brittlestar-mass` | built headless, not run in Unity (2026-11-15) |
| 3 | Haul (Thresher Mass) | `claude/thresher-mass-haul` | built headless, not run in Unity (2026-11-15) |
| 4 | Hedgerow (Serpent Mass) | `claude/serpent-mass-hedgerow` | built headless, not run in Unity (2026-11-15) |
| 5 | Longshot (Serpent Space) | `claude/serpent-space-longshot` | built headless, not run in Unity (2026-11-15) |
| 1-37 | all | | not started (2026-11-15) |
| 24-26 | Snap, Den Builder, Riptide | `claude/goby-games`, #2003 | built and merged headless, not run in Unity (2026-11-15) |
