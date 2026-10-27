# The Meerkat: the wormhole vessel (design studio, round 2, 2026-10-27)

**Studio:** `Docs/Studios/VesselStudio/meerkat.html` (its own source of truth; no copy script), a page of the one Vessel
Studio artifact (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa). Test hook `window.__meerkatStudio` (and `__lab`).
**Owner:** Yash (the Vessel Studio). **Status:** in the game since 2026-10-27 (see "In the game" below).

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
  over the forming time and the tunnel has no grid. Round 2 gave the formed tunnel a geometry of its own, which
  everyone sees: the spindle bridge (below).
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

What everyone sees:
- the space round the tube, bent away from it (`warpStrength`), with its centre line kept clear;
- each mouth as a dent as wide as the mouth (`dentStrength`, fading as the camera nears it);
- **the bridge** (round 2): the formed tunnel's geometry.

The bridge (Yash, 2026-10-27: *"show the geometry of the wormhole ... I will create this wormhole bridge with the
spindles in our game ... the spindles will show the wireframe structure of the wormhole, while the actual curvature of
space-time will be the shader"*):
- **What it is made of:** rings of spindles round the throat (`bridgeRings` x `bridgeSpindlesPerRing`) and ribs of
  spindles along it (`bridgeRibs`), over a glass surface (`bridgeGlass`).
- **Colour:** its maker's domain colour.
- **Life:** the rings sit at fixed places along the tunnel. As it forms they condense in one by one from its centre, and
  as it collapses they evaporate the same way, like the game's Spindle condense / evaporate fades.
- **Motion:** a shimmer runs through them from the way in to the way out (`bridgeShimmer`).
- **Near the camera:** spindles fade out between 30 u and 4 u.
- `bridgeVisibility` 0 hides the bridge and leaves only the bent space.

In the studio a spindle is a tapered rod. In the game, the bridge is built from the `Spindle` prefabs, and the
space-time bend stays the shader's.

**The panels:** all the wormhole cards are in **Play Style Config**, in this order: Reticle (feel and look, yours
alone), Wormhole look (the bridge), Space look (the warp, the dents, the ripple), Shape, Ride.

**Round 4 (Yash, 2026-10-27: "Turn off all lensing"):** the tube's warp, the mouths' dents and the collapse's ripple ship
at 0. The wormhole's look is the bridge and your reticle. The Space look sliders stay, to bring any of them back; at 0
the game's lens pass does not run at all.

**Round 3 (Yash, 2026-10-27: "I want it to be a little smoothy ... pretty transparent, just maybe wireframe looking with
just spindles. Tiny thinning spindles"):**
- **The reticle eases** toward what the right stick and LT ask for over `reticleSmoothSeconds` (0.15 s: 63% of the way
  in 0.15 s). RT forms the tunnel you see.
- **The AI waits for its reticle to catch up** with its sticks (within 0.03) before it pulls RT, 1 s at most. Measured,
  seeds 1-4 at I1 and I3: every race finished, in 101-104 s (94-104 s without the easing).
- **The reticle is a see-through frame of thin spindles**: 10 rings of 16 spindles and 8 ribs, 0.35 u thick, cyan to
  white, over a faint glass. These are the same spindles as the bridge, and it replaces the line grid. The lime line you
  will fly runs through it at half brightness.
- **The screen effects (warp, dents, ripple) have their own card**, Space look.

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
| space | `warpStrength` | 0 (off, round 4) | Space bent along the tube (everyone sees it) |
| space | `dentStrength` | 0 (off, round 4) | Space bent in at each mouth (everyone sees it) |
| space | `rippleStrength` | 0 (off, round 4) | The collapse's ripple at the screen's edges (0 = none) |
| look | `rippleEdge` | 0.45 | ...from this share of the way from the centre to a corner |
| look | `rippleSeconds` | 1.2 s | ...running out and fading over this long |
| reticle | `reticleSmoothSeconds` | 0.15 s | Your reticle eases toward the sticks over this long (0 = at once) |
| reticle | `reticleIdleAlpha` | 0.4 | Your reticle's brightness with the right stick centred |
| reticle | `reticleGlass` | 0.15 | The faint lit surface under your reticle's spindles |
| reticle | `reticleRings` | 10 | Rings of spindles round your reticle |
| reticle | `reticleSpindlesPerRing` | 16 | ...spindles round each ring |
| reticle | `reticleRibs` | 8 | ...ribs of spindles along it (0 = none) |
| reticle | `reticleSpindleThickness` | 0.35 u | ...spindle thickness |
| reticle | `reticlePath` | 0.5 | The lime line you will fly through it (0 = none) |
| look | `bridgeVisibility` | 1 | The formed tunnel's bridge, for everyone (0 = hidden) |
| look | `bridgeGlass` | 0.6 | ...its glass surface |
| look | `bridgeRings` | 16 | ...rings of spindles along it |
| look | `bridgeSpindlesPerRing` | 12 | ...spindles round each ring |
| look | `bridgeRibs` | 6 | ...ribs of spindles along it (0 = none) |
| look | `bridgeSpindleThickness` | 1.6 u | ...spindle thickness at the middle |
| look | `bridgeShimmer` | 0.5 | ...a shimmer running way in to way out (0 = steady) |
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

## In the game (2026-10-27)

The Meerkat is in Unity as `VesselClassType.Meerkat = 28` (`Assets/_Scripts/Controller/Vessel/R_VesselActions/MEERKAT.md`):
- **Where:** flown from the Toybox's Vessel Changer, and raced in its Time mini-game **Burrow Run**
  (`GameModes.BurrowRun = 71`, eight rings, two laps).
- **Numbers:** every key in the table above is a `MeerkatWormholeConfig` field at its shipped value.
  `studio_to_unity/meerkat.json` maps 36 keys, 0 drift.
- **The port:** the page's arithmetic, ported line for line and held to its numbers (`MeerkatWormholeMathTests`).
- **Lab-only:** `aiTau`, `roll` (the one-thumb hull only banks: `bankShare` × `roll` is the prefab's `RollScaler`) and
  the `lope*` keys.
- **Verification:** not yet verified in the Editor (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).

## What is not decided or not built

- **The hull.** The Meerkat flies the Stoat's model and its lope as a stand-in, in the studio and in the game. A real
  hull is `/vessel-model` work.
- **Its other three abilities and the element map** (`/vessel-studio` D37) are not designed yet. The game's map has
  four open slots; the wormhole is its one ability.
- **The bridge from the game's `Spindle` prefabs.** The game draws each spindle as a tapered mesh (a prefab per spindle
  would be ~280 objects per tunnel).
- **The game's Stoat still has round 18's straight warp gate on B** (`StoatModeAction` gate* fields, `StoatGateMath`).
  It is unlinked from the studio now. Removing it from the Stoat is Garrett's call (the Stoat's owner).
- **The play styles.** The Meerkat has none yet.
- **Learning.** The AI learning sweep records Burrow Run races; no learner reads the Meerkat's yet.

The parked black/white-hole look for tunnels is in `Docs/Studios/STOAT_PARKED_HOLE_LOOK.md`.
