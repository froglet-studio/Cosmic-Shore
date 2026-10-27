# The Meerkat: the wormhole vessel (design studio, round 1, 2026-10-27)

**Studio:** `Docs/Studios/VesselStudio/meerkat.html` (its own source of truth; no copy script), a page of the one Vessel
Studio artifact (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa). Test hook `window.__meerkatStudio` (and `__lab`).
**Owner:** Yash (the Vessel Studio). **Status:** design only. There is no Meerkat in the game yet.

## Where it came from

Yash, 2026-10-27: *"a big change is this new wormhole ability with all these stored features ... This will be Meerkat,
a new vessel. The Stoat will have that field and orbit for now untouched. Just take away all this wormhole kind of
ability of the Stoat and create a Meerkat, a new vessel, with all its things similar to the Stoat. Just rename to
Meerkat and keep only one ability of the wormhole. Do not add field trajectory or orbit into Meerkat."*

The Meerkat is the Stoat studio's **Gate mode** (rounds 19-26, Garrett and Yash, `STOAT_DIPOLE.md` §1g-§1n), made its
own vessel. The Stoat studio keeps Mass (the field trajectory) and Time (the catch sling) exactly as they were: both
race the same times as before the split (Mass 59.6 s, Time 53.8 s on seed 7, the AI at Hard).

The same request changed three things on the way over:
- **The ripple is subtle, circular, and at the screen's edges only.** Round 26 sent a gravitational wave through space
  when a tunnel collapsed. Passing the camera, it stretched the whole view, the centre included, and read as
  distorting the player's eyesight. The Meerkat's ripple is a screen-space ring instead. It runs from `rippleEdge` out
  past the corners, and nothing inside that circle moves.
- **No grid on a formed tunnel.** The grid is only the reticle you shape. Once RT forms the tunnel, the reticle fades
  over the forming time and the tunnel has no grid. Everyone, you included, sees it only as bent space-time.
- **Simpler parameters, named as the game will name them.** Every key is its future field on
  `MeerkatWormholeConfig` (below). The round-18 gate keys, the warp-boost option, the dent width, the dent's camera fade,
  the grid sheets and the grid-on-ride switch are gone.

## How it flies

One thumb flies, as the game's one-thumb hulls do (`SingleStickVesselTransformer`):
- The left stick gives yaw and pitch through Ease(2 x stick), at full throttle, banking `bankShare` x the roll rate
  into a turn. This leaves the right thumb always free.
- **The reticle** is always on your nose, `reticleAhead` ahead. It shows the tunnel RT would form now. You see it, and
  no one else does.
- **The right stick** bends the reticle any way, as far as it is pushed, up to `bendMaxDegrees`. The further it bends,
  the shorter (`lengthStraight` to `lengthBent`) and fatter (`throatStraight` to `throatBent`) it gets.
- **LT** shortens a straighter tunnel. On keys and touch it ramps to full in `keyRampSeconds`.
- **RT** forms the tunnel and you dive into it. It ripples out of its centre to both mouths over `formSeconds`, never
  longer than your dive to it.
- **The ride** starts and ends at your entry speed and rushes to `throatSpeed` x it at the throat. You come out with
  the speed and momentum you went in with, on the tunnel's last heading. While you ride, the camera runs down the
  tube's centreline.
- **Out**, the reticle comes back after `reticleDelaySeconds` and grows out of its centre over `reticleGrowSeconds`.
- **The tunnel's end:** it stays `staySeconds`, then collapses to its centre over `collapseSeconds`, and its collapse
  ripples the screen's edges. Forming a new tunnel collapses the last one at once.

What everyone sees: the space round the tube is bent away from it (`warpStrength`), with its centre line kept clear, and
each mouth is a dent as wide as the mouth (`dentStrength`, fading as the camera nears it).

## The parameters (the artifact's keys ARE the game's fields)

| Group | Key = `MeerkatWormholeConfig` field | Shipped | What it does |
|---|---|---|---|
| shape | `reticleAhead` | 30 u | The reticle rides this far ahead of the nose |
| shape | `bendMaxDegrees` | 120° | The sharpest bend (right stick fully over) |
| shape | `lengthStraight` | 600 u | A straight tunnel's length |
| shape | `lengthBent` | 120 u | The sharpest bend's length |
| shape | `throatStraight` | 3 u | Throat radius, straight |
| shape | `throatBent` | 40 u | Throat radius, sharpest bend |
| shape | `mouthFlare` | 2 | Mouth radius as a multiple of the throat (+6 u) |
| shape | `keyRampSeconds` | 0.5 s | LT on keys or touch reaches full after this |
| ride | `throatSpeed` | 2.5 | Fastest at the throat, as a multiple of your entry speed |
| ride | `reticleDelaySeconds` | 0.5 s | The reticle comes back this long after you are out |
| ride | `reticleGrowSeconds` | 0.8 s | ...growing out of its centre over this long |
| ride | `formSeconds` | 0.4 s | RT: the tunnel forms over this long |
| ride | `staySeconds` | 10 s | It stays this long after you are out |
| ride | `collapseSeconds` | 0.8 s | ...then collapses to its centre over this long |
| look | `warpStrength` | 0.8 | Space bent along the tube (everyone sees it) |
| look | `dentStrength` | 0.6 | Space bent in at each mouth (everyone sees it) |
| look | `rippleStrength` | 0.3 | The collapse's ripple at the screen's edges (0 = none) |
| look | `rippleEdge` | 0.45 | ...from this share of the way from the centre to a corner |
| look | `rippleSeconds` | 1.2 s | ...running out and fading over this long |
| look | `reticleIdleAlpha` | 0.4 | Your reticle's brightness with the right stick centred |
| look | `reticleGlass` | 0.6 | The faint lit surface under your reticle's grid |
| ai | `aiStraightMinDistance` | 300 u | The AI forms a straight tunnel only with its ring this far away |
| ai | `aiBentMinDistance` | 120 u | ...a bending one from this far |
| ai | `aiShapeSeconds` | 0.3 s | The AI shapes its reticle this long before RT |
| flight | `bankShare` | 0.12 | One-thumb bank into a turn, as a share of the roll rate |
| flight | `cruise`, `pitchYaw`, `roll` | 60, 120, 130 | `Meerkat.prefab VesselTransformer` (the Stoat's numbers for now) |

These are lab-only (studio only, never in the game): `aiWormhole` (the sim lab's switch), `aiTau`, `aiNoise` (the AI
levels' judgement), and the stand-in hull's `lope*`.

**Fixed in the studio, not parameters:** the dent fades within 3 mouth radii of the camera (`DENT_NEAR`, round 24's fix
for the view flipping as you fly in). A faded tunnel is gone after 0.7 s (`FADE_S`). The ripple is 0.012 screen units
at strength 1, at full strength within 300 u of the collapse.

`Tools/Build/studio_to_unity/meerkat.json` is its map to Unity. It is empty until the fields exist;
`studio_to_unity.py --unmapped --vessel meerkat` lists every key.

## Measured (headless, 2026-10-27)

- **One tunnel, flown by hand:** the reticle was fully grown after 1.25 s. Formed at a 0.3 stick bend, the tunnel
  showed no grid while ridden (0 wireframe vertices from the end of forming). You come out at your entry speed
  (58.2 in, 58.2 out). It stayed 10 s and collapsed with one ripple.
- **The ripple:** its front runs from 0.45 to 1.25 of the way to the corners. At the default strength, its peak is
  about 2 px at the ring.
  - Same frame, ripple on and off: the centre (r < 0.2) and the middle (0.2-0.6) are unchanged. The difference stays
    in the outer bins (up to 0.4 of a level on average).
  - The centre's residual differs by the same amount with the ripple untouched, so it is not the ripple.
- **AI (Hard, seed 7):** 100 s for 15 of 16 rings, 17 tunnels formed and ridden (the Stoat's Gate AI raced 98-101 s on
  circuits).

## What is not decided or not built

- **The hull.** The Meerkat flies the Stoat's model and its lope as a stand-in. A real hull is `/vessel-model` work.
- **The game.** There is no `VesselClassType` value, prefab, `MeerkatWormholeConfig` SO or action yet. Claiming the enum
  value needs a check of `bleeding-edge` and the in-flight branches (`CLAUDE.md`, team rules).
  `/artifact-to-unity` brings the Meerkat into the Toybox's Vessel Changer.
- **The game's Stoat still has round 18's straight warp gate on B** (`StoatModeAction` gate* fields, `StoatGateMath`).
  It is unlinked from the studio now (its rows left `stoat.json`). Removing it from the Stoat is part of the same port.
- **Its mini-game and its other three abilities** (the element map, `/vessel-studio` D37) are not designed yet. The
  wormhole is its one ability so far.
- **The play styles** are the Stoat's (they leaned its slings and holes). The Meerkat has none yet.

The parked black/white-hole look for tunnels is in `Docs/Studios/STOAT_PARKED_HOLE_LOOK.md`.
