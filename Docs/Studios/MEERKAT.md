# The Meerkat: the wormhole vessel (design studio, round 8, 2026-10-27)

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
- **The tunnel is an umbrella handle** (round 7): a straight shaft, then one bend of fixed radius `bendRadius`. Its
  throat (`throatDiameter`) and its entry and exit (`mouthDiameter`) are set at edit time and never change in flight.
- **LT** sets how far the shaft runs before the bend: `shaftMin` released, up to `shaftMax` squeezed. On keys and touch
  it ramps to full in `keyRampSeconds`.
- **The right stick** sets the bend's direction (left, right, up, down or between) and how far it turns, up to
  `bendMaxDegrees` (150°) with the stick fully over: 90°, 120° and 150° are all there.
- **RT** forms the tunnel and you dive into it. It ripples out of its centre to both mouths over `formSeconds`, never
  longer than your dive to it.
- **The ride** starts and ends at your entry speed and rushes to `rideMidMultiplier` x it at the throat. You come out with
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

**Round 8 (Yash, 2026-10-27: "Stoat and Meerkat are the two vessels I really wanna complete myself ... all four
abilities ... the abilities I wanna test first in artifact rather than inside game directly ... then we will see whether
to add them or not in Unity"): the full four-ability kit, as PROPOSALS.** Nothing in this round is approved for Unity
(`/vessel` §3's design gate is Yash's call): every new key is lab-only, tagged "lab" under its slider, and is not in
`MeerkatWormholeConfig` or `Tools/Build/studio_to_unity/meerkat.json`. The kit follows the fleet's element language
(Time = speed, Mass = making, Space = destroying, Charge = pilots and crystals) and PvP-is-petals-only: no ability moves
or damages an opposing vessel.

| Element · ability (button) | What it does | Its sliders | L5 upgrade |
|---|---|---|---|
| Charge · **Lookout** (Y, key E) | A pulse: for `lookoutSeconds` every crystal within `lookoutReach` is marked, and you see every rival's reticle and its next ring, through anything (drawn on the HUD) | `lookoutSeconds` 6, `lookoutReach` 700, `lookoutCooldownSeconds` 12 (from the marks' end) | **Alarm Call**: crystals inside the reach are drawn to you (`alarmPullSpeed` 90 u/s); team-mates see the marks too |
| Mass · **Hardened Bridge** (LB, key Q) | LB **arms** the next tunnel (press again to disarm). Its bridge sets as a lattice of prisms, a tube lasting `wallSeconds` that no other team can cross through its wall: the move into it is taken away and the rest kept, so they slide along and go round. Its maker's team is never stopped | `wallSeconds` 20, `wallPrismDensity` 1 (a ring every 20 u, a prism every 10 u round), `hardenCooldownSeconds` 8 | **Shielded Bridge**: shielded prisms, immune to Cave-in, drawn pale |
| Space · **Cave-in** (B, key F) | Your standing tunnel (not one you ride) implodes to its centre line now; every prism within `caveInReach` of the tube along its whole length is destroyed (field prisms and unshielded hardened prisms, yours included) | `caveInReach` 30, `caveInCooldownSeconds` 8 | **Burial**: a rival inside or touching the tunnel takes a scored hit, a petal lost and its abilities silenced (harmless) for `burialSilenceSeconds` 4; never pushed |
| Time · **the wormhole** (RT) | Rounds 1-7, unchanged | (Play Style Config) | **Chain Dig**: from `chainShare` (0.7) of a ride, RT digs the next tunnel from the far mouth (shaped by the sticks as a reticle would be) and the ride holds `rideMidMultiplier` through the join; the tunnel behind collapses |

- **Why LB toggles rather than LB held with RT:** RT is pulled while the right thumb bends the reticle, so a chord asks
  both index fingers to land in the same instant (and on touch RT is a drag handle). A toggle is one press any time
  before, and the Mass icon says ARMED.
- **The controls:** Vessel Config, its Section dropdown: one section per ability (Charge, Mass, Space, Time), each with
  an on/off switch, its element's level L0-L10 (the card says when the L5 upgrade is unlocked; scaling an ability by its
  level is not modelled) and its sliders, each with an ⓘ preview. They ride the page's saved state and its section
  profiles. AI Config has **AI uses the new abilities** (on).
- **The HUD:** the four-icon row, lower right (bottom centre in phone play), in the locked order Charge, Mass, Space,
  Time: key glyph, cooldown sweep and seconds, the level and an L5+ marker, white glyphs on neutral plates (elements have
  no colour), the accent for what is live (marking, ARMED, a Chain Dig window: DIG!). Phone play has Y, LB and B buttons
  under the RT handle.
- **The world:** seeded crystals along the course line (six a leg; any hull within 14 u collects one; a tally only, so
  a race without the kit is the race it was), and the seeded prism field (on Warpline a sleeve 30-250 u round the
  course line; on the stand-in circuits the Stoat studio's band, bit for bit). Every race starts with the whole field
  and every crystal back.
- **The AI** (input-only, it presses the buttons a player has, behind the AI Config switch): Lookout whenever ready with
  a rival or 3 crystals in reach; Harden with a rival within 600 u; Cave-in on a tunnel it has left with a rival in it
  (L5) or 6 prisms in reach, never on its own unshielded wall; Chain Dig when a tunnel from the far mouth fits the next
  leg (the same planner as its reticle, from the far mouth). A pilot a wall stopped flies round its nearer end.
- **The course: Warpline**, the Meerkat's Time race (5 rings a lap, 2 laps), now the default; the 8-ring Circuit and
  Hairpins stay selectable (Game Config, Intensity). The page ports `HeadlongCircuit.Generate`'s recipe with
  `SlingshotCourse.ForIntensity`'s numbers (corner profile, angular spread, radial swing, out-of-plane offsets, ring
  radii 48/40/32/26, the legality relaxation, ring 0 on the pole, rings facing the bisector with the axis jitter); a
  seed's layout is the studio's own (mulberry32, not the game's Rng).

Measured (headless, the level scorecard: median of seeds 11, 23, 37; the AI flies your hull alone, so walls and Burial
do not come into it; "no WH" is the Hard pilot without the wormhole). Kit off = identical, race for race, to the page
before round 8 on Circuit and Hairpins. Every course finished at every level, kit on and off.

| Course | Kit off: Easy / Medium / Hard / no WH | Kit on: Easy / Medium / Hard / no WH |
|---|---|---|
| Warpline I1 | 93.0 / 91.1 / 90.0 / 110.9 | 82.1 / 79.9 / 78.4 / 110.9 |
| Warpline I2 | 90.2 / 88.5 / 86.5 / 110.5 | 82.4 / 79.3 / 80.7 / 110.5 |
| Warpline I3 | 95.0 / 93.4 / 92.0 / 122.0 | 89.0 / 83.6 / 83.2 / 122.0 |
| Warpline I4 | 93.2 / 91.4 / 89.0 / 111.5 | 82.4 / 79.5 / 79.6 / 111.5 |
| Circuit I1-I4 Hard | 102.5 / 101.3 / 106.8 / 106.3 | 102.5 / 95.9 / 99.0 / 95.4 |
| Hairpins I1-I4 Hard | 106.6 / 101.1 / 97.7 / 94.8 | 103.6 / 97.5 / 94.5 / 94.0 |

- Chain Dig is the kit's only speed: 5-9 chains a Warpline race take Hard 6-10 s faster.
- With the kit on, **Medium beats Hard on Warpline I2 and I4 and Circuit I2-I3, and Easy beats it on Circuit I3** (by 0.1-3.4 s): Chain Dig rewards
  whoever's line happens to leave a fitting leg, and the Hard pilot's one deterministic line does not always. The level
  order (D24) no longer holds with the kit on; a finding for Yash, not tuned away.
- Live races against three rivals (Hard, Medium, Easy) all ran to the end (all rivals 9-10 of 10 rings when the
  first pilot finished): 6-8 hardened tubes a race each, a pilot stopped by a wall for up to 1 s at a time, and Burial
  hit 2-19 times a race (the pack races one line, so a rival is often inside your tunnel): likely too strong as proposed.

**Round 20 (Yash, 2026-10-28: "remove the rings of spindle. From the trunk as well as the roots, so there should not be
any distinction between the wormhole and the trunk ... for the roots, we won't be traveling forward randomly. Rather, we
will also control going forward using our trigger inside the root ... use the left thumbstick to go forward, backward,
left, right, and on hitting the surfaces, we will bounce off a little bit ... the root need to be wide enough so our camera
view always stays inside the root ... In the root, it will be pretty more opaque ... It will be an underground world in
3D"). ARTIFACT ONLY.**
- **The trunk is the wormhole.** A formed tunnel draws no bridge (no spindle rings or ribs, no glass tube of its own)
  while its trunk shows. The trunk got a bore: the tunnel's wall is the trunk's inside, in the same wood. So it is one log
  you fly through. The bridge comes back only if the trunk is hidden (`trunkVisibility` 0). The reticle keeps its frame.
  - Unity still draws the bridge (its `bridge*` values are unchanged, 0 drift). Porting this is a later clone.
- **The roots draw no spindle frame** once dug (`labyrinthFrame`, `labyrinthRideFrame` and round 19's inner glow,
  `rootInside`, are gone).
- **Inside the roots you fly yourself** (`labTransit`; round 19's wall slide is gone, with `rootSlideSpeed`, `rootThrow`,
  `rootFriction`, `rootSteer`, `rootWallRide`, `forkBoost`, `forkLean`):
  - RT pushes you on along the root (`rootThrust` 90 u/s²).
  - The left stick pushes you on and back (up / down) and left and right; the right stick up and down (`rootMove` 55).
    Keys: W S A D, the arrows up and down.
  - Nothing held, you coast (`rootDrag` 0.45 /s), never faster than `rootMaxSpeed` 90 u/s. You carry your speed in, up to
    that.
  - A wall bounces you off with `rootBounce` (0.45) of your speed into it; the speed along the wall is kept.
  - `rootGravity` (0 = off) can draw you to the root's floor, square to the root so it never pulls you back down a climb.
  - Your nose follows the root you are in, the way you face along it; up is the network's up.
  - Past a fork you are in whichever root you steered into; back past a root's start you are in its parent again. Out of
    a tip you fly on along it; back out of the source you leave the way you came.
  - The AI holds RT and steers across the root (a spring on the sticks) for the root that leads nearest its ring.
- **The camera stays inside:** in the roots it sits 12 u back along the root's centreline (into the root behind you when
  that is behind), half your way out to the wall. The roots are wider: `labyrinthDiameter` 24 → 40 u, mouths 30 → 46 u,
  `rootTip` 0.5 → 0.6.
- **From inside, the roots are an underground world.** While you are in a network, its roots also draw their inside as
  an earthen wall (fibres along the root, knots, grit), drawn over everything outside:
  - `rootOpacity` 0.94 of the world outside is hidden;
  - your lamp lights the walls near you (`rootLamp`), and walls `rootFog` (160 u) off fade into the dark;
  - each root's ends stay open: an opening (a tip, the source) fully, a fork only a little, so you see the light at a tip.
  - From inside, the roots' space-time warp is off: they are earth, not bent space. From outside nothing changed.
- **Measured** (headless, seed 7, full Mass): coasting in, your speed dies from 64 to 28 u/s in 2 s; holding RT you reach
  90 u/s and go through two forks; holding the stick back slows you; you bump the walls in the bends; the camera sat 7 u
  off the centreline of a root 18.6 u in radius, inside it.
- **The scorecard (seed 11, I1-I4):** every level finishes every ladder. Hard 77.8 / 78.9 / 81.2 / 80.8 s (round 19's
  slide: 85.9 / 86.9 / 94.6 / 79.1 s); Easy 80.2 / 78.8 / 81.7 / 84.3 s; Medium 78.7 / 80.4 / 81.7 / 82.0 s.

**Round 19 (Yash, 2026-10-28: "it's non-navigable. What I want is to navigate the routes. So the speed of the vessel
inside the roots need to be little less. So that I can at least slide ... it will follow the inner wall of the root like a
ball flowing through the root canal. And it's friction will be very less ... with my thumbstick I can just make it a
little bit of direction control. Very less. Most of the time I will be just sliding through the walls of the inner
root"). ARTIFACT ONLY.**
- **Inside the roots you are a ball on the inner wall** (`labTransit`), `rootWallRide` (0.72) × the root's radius out
  from its centreline, at an angle round it. Your feet are on the wall (up = toward the centre).
  - The camera sits behind you on the same side of the wall, and rolls with you gently.
  - The speed tunnel's push-in stays at its normal strength in the roots.
- **Along the root you slide slower** than a wormhole ride: `rootSlideSpeed` (1.1) × your speed before the roots, plus
  `forkBoost` for every fork, easing to it. About 70-80 u/s against the wormhole's 160 or so.
- **Round the wall you sway:**
  - A bend throws you up its outside wall (`rootThrow` 0.3 × v² × the curvature), and `rootGravity` (45 u/s²) draws
    you back to the bottom (the network's down).
  - `rootFriction` (0.8 /s) slowly calms the sway. The swing is as slow as on a wall at least 10 u round (`SWING_R`),
    so it never whirls.
  - The curvature is read over a sample's length or more, because shorter steps read the sampled path's joints as kinks.
- **The left stick only nudges you round the wall** (`rootSteer` 35 u/s², under gravity's 45). It pushes toward a
  fixed side of the root (the network's up, and across), not the rolling hull's, so holding it right settles you on the
  right wall.
- **Where you ride on the wall chooses the root at a fork:** the one leaving on your side. The HUD and the 3D light it
  before the fork.
- **The first try failed, and why.** Before the fixes above, the ball whirled round the pipe at the swing-speed cap:
  - the thin roots' bends threw it several times harder than gravity;
  - the curvature was read over 2 u, so the sampled path's joints read as kinks;
  - the stick pushed in the hull's own rolling frame, so it drove the ball round for ever.
- **Measured** (`sim()` stepping, full Mass):
  - With no stick, the ball rides the bottom with a gentle sway (under 1 rad/s).
  - Holding right, it settles about 2.4 rad round and takes a different route.
  - Holding up, it takes a third route.
- **The AI** leans in the same frame toward the root that leads nearest its ring.
- **The scorecard (Hard, I1-I4, seed 11, before the kits merged in):** 85.9 / 86.9 / 94.6 / 79.1 s, against round 17's
  77.6 / 83.5 / 89.9 / 79.6 s. The slide is slower than the old root ride, as asked, so an omnicrystal no longer buys
  time on most ladders. Easy 83.9 / 93.4 / 84.8 / 81.4 s, Medium 81.9 / 92.9 / 90.0 / 78.3 s.
- **Merged over round 18 (the kits, another session):** this was built as round 18 alongside it, so it is round 19.
  The slide runs the same in every kit (Off, Garrett's, the Mix), including a ride into someone else's roots.
- **The walls show from inside.** The roots' bark lit only where it turns edge-on, and a pipe seen from inside faces
  you at about 56 degrees, so the walls you slide on were all but invisible: a flat haze about 13 brightness levels up.
  - The inner wall now has a glow of its own (`rootInside` 0.6, seen only from inside, so the outside stays faint),
    carrying the bark grain and a ring every 12 u streaming past.
  - The roots round you (the one you are in, the one behind, the ones after the next fork) show their spindle rings
    at `labyrinthRideFrame` (1.2 × the bridge), while the rest stay at `labyrinthFrame`.
  - Found on the way: the tube's triangles face outward, so "inside" is the back face. The first try had it backwards
    and lit the outside instead.
- **The roots are wider and the camera is closer**, so there is a wall to slide on:
  - `labyrinthDiameter` 12 → 24 u, `labyrinthMouthDiameter` 22 → 30 u.
  - `rootTip` 0.35 → 0.5: tips are half as wide as the source.
  - In the roots the chase camera sits 12 u behind you (`CAM_ROOT_BACK`), not 21, so it stays inside them in bends.

**Round 18 (Yash, 2026-10-27: "two variants, one with his kit only, one with a mix of his kit with the ones you
recommend"). ARTIFACT ONLY; lab-only keys, no Unity-mapped value changed.** The Kit dropdown (Garrett's kit, the Mix,
Round 8 archived, Off), built over round 17's Mass-sized trunks and roots. It was started as "round 17" on another branch
while round 17 landed on Ys-bleeding-edge, so it is round 18. Everything is in § "Two kits: Garrett's and the Mix" below.

**Round 17 (Yash, 2026-10-28: "our trunk size will be determined by ... our mass ... it is more mass we have, we create
bigger trunks, we move farther distance. But the trunks look need to be pretty subtle ... the bigger the trunk the bigger
the network of the branches will be ... the last trunk size will determine how big of a network you can get by getting
that omnicrystal. The max trunk size will give you a very large amount of network of roots ... Three-dimensional roots.
Spread out any direction from the omnicrystal's position"). ARTIFACT ONLY; no Unity-mapped value changed.**
- **Mass sizes the trunk.**
  - Your Mass is the Mass level (`massLevel`, Vessel Config) plus `massPerCrystal` (0.5) for every crystal you take
    this race, up to 10. It resets each race, and the HUD shows it while you shape a tunnel.
  - At Mass 0, LT reaches only `massLengthAtZero` (0.3) of its range and the trunk is `massDiameterAtZero` (0.6) as
    wide. At Mass 10 you get the whole range at full width; in between, in proportion.
  - Measured with LT squeezed and the tunnel straight:

    | Mass | Tunnel length |
    |---|---|
    | 5 | 409 u |
    | 10 | 598 u |

- **Subtle trunks**, because many will be on screen at once:
  - `trunkGlass` 0.04 → 0.012, `trunkRim` 0.55 → 0.28, `trunkGrain` 0.5 → 0.22, `trunkWarp` 0.25 → 0.1.
  - `trunkFresnel` 2.5 → 3 (a thinner outline).
  - `branchCount` 8 → 6.
- **The last trunk sizes the roots.**
  - Each trunk's size is its shaft against the longest a full Mass digs (0 to 1), and the last one is kept (`lastTrunk`).
  - The omnicrystal's network scales with it: forks `labyrinthBranchPointsMin`..`Max` (3..12), tips
    `labyrinthOpeningsMin`..`Max` (5..30), reach `labyrinthRadiusMin`..`Max` (90..420 u).
  - The round-16 keys were renamed to their `...Max`; the `...Min` keys are new.
  - Measured:

    | Last trunk | Roots |
    |---|---|
    | Smallest (60 u) | 8 channels, 5 tips |
    | Half (Mass 5, LT squeezed) | 26 channels, 18 tips |
    | Biggest (Mass 10, LT squeezed) | 42 channels, 30 tips |

- **The omnicrystal grows the roots at once**, from where you take it (`omniAtOnce` 1; 0 brings back round 16's "arms
  your next RT").
  - They spread every way (`labyrinthSpread` now 360). The first fork is close, and the centre sits just ahead of you.
  - You slide straight in and lean at the forks as before.
  - Taken while riding a tunnel, it arms your next RT instead.
  - **Give me an omnicrystal** grows them where you are.
- **They look like roots:**
  - They taper from the source to the tips (`rootTip` 0.35) and wander as they go (`rootGnarl` 0.3).
  - Every root wears the trunk's own bark shader, faint and root-brown (`rootGlass`, `rootRim`, `rootGrain`).
  - Their spindle wireframe is at `labyrinthFrame` (0.3) of the bridge's brightness, so a big network stays quiet.
  - The soil is kept but quieter (`soilVisibility` 1 → 0.45).
- **Measured** (the level scorecard, seed 11, Hard):

  | Course | Omnicrystals off | Omnicrystals on |
  |---|---|---|
  | 1 | 77.6 s | 77.6 s |
  | 2 | 79.3 s | 83.5 s |
  | 3 | 82.2 s | 89.9 s |
  | 4 | 80.9 s | 79.6 s |

  - Every level finishes every course.
  - With omnicrystals off, the race barely moved from round 16 (77.5-82.8 s). At the default Mass 5 a pilot digs
    shorter trunks, but the crystals it takes grow its Mass during the race.
  - Roots grown at once take the AI where they lead: on course 3 it is about 8 s slower. A finding for Yash.
  - Every pilot who flies through an omnicrystal gets roots, even with the AI's wormhole off. So the no-wormhole
    baseline moves too (110.9 s → 114.8 s on course 1).
- **Not decided:**
  - Whether the roots should also cost Mass (spend it to grow them).
  - Whether a rival's crystals should grow its own Mass in the game the same way. In the studio every pilot follows the
    same rule.

**Round 16 (Yash, 2026-10-28: "we are not going to make it into Gopher. So remove everything from Gopher and add them to
this Meerkat's Omnicrystal ability. So once it collects the Omnicrystal, it's going to rise up to a three-dimensional
branching from a single source ... 5 to 10 branch points with multiple openings, lots of openings ... openings all around
... configuration parameters to control the total how large volume the branching gonna spread up to, how many openings and
directions, randomness ... Wackadoodle labyrinth water slide park inside soil"). ARTIFACT ONLY. The Gopher studio is gone
(its page, card, preview and docs).**
- **The Omnicrystal ability: the LABYRINTH.**
  - **Omnicrystals:** `omniCount` (3) sit on the course line, half way between rings. They are bigger than crystals,
    gold-white and faceted. Fly through one and your next RT digs the labyrinth instead of a wormhole. The reticle turns
    into the whole labyrinth in gold, and the HUD says it is armed.
  - **Try it at once:** **Give me an omnicrystal** on the Omnicrystal · the labyrinth card.
- **Its shape: a 3D branching from one source.**
  - The source sits on your nose line, and the first fork comes soon after it.
  - It has `labyrinthBranchPoints` (8) forks, and each fork splits two to four ways.
  - It has `labyrinthOpenings` (16) ways out: at most 3 × forks + 1, at least forks + 1.
  - The openings lie `labyrinthRadius` (240 u) from its centre, spread over `labyrinthSpread` (300°, 360 = all round,
    back toward you included).
  - `labyrinthRandomness` (0.45) sets how wild it is. Every labyrinth is new.
  - **How it is built** (`labLayout`):
    - A random tree, grown shallow-first until it has that many forks; extra openings go to random forks.
    - The openings' directions are a Fibonacci spread over the cone, jittered. They are dealt to the subtrees by splitting
      along their long axis, so neighbouring openings share a branch.
    - Each fork sits toward its openings' mean direction, deeper forks further out.
  - **Channels:** `labyrinthDiameter` (12 u) wide, flaring to `labyrinthMouthDiameter` (22 u) at the source and every
    opening, dug rough (`labyrinthRoughness`).
  - **Digging:** it digs itself out from the source over `labyrinthFormSeconds` (0.8 s).
  - **Wireframe:** rings every `labyrinthRingSpacing` (12 u) of `labyrinthRingSides` (10) spindles, and `labyrinthRibs`
    (4) ribs.
- **The water slide.**
  - The ride multipliers apply, plus `forkBoost` (0.08) for every fork taken.
  - At a fork, lean the LEFT stick toward the channel you want (left, right, up, down or between) past `forkLean`
    (0.25). A lean held any time on the way counts. With no lean you take the straightest channel.
  - `forkHintSeconds` (0.9 s) before a fork, each channel is marked where it leads, with the one you would take lit, in
    the HUD and in the 3D.
  - Out of an opening you fly on along it. The labyrinth stays `staySeconds`, then falls in.
- **Inside soil.** The Gopher's soil, unchanged: strata walls, stones and roots (`soil*`, on the Omnicrystal · the soil
  card). Everyone sees it, the rider too.
- **The AI is input-only.**
  - With an omnicrystal and the ring ahead, at least half the labyrinth's radius away, it presses RT.
  - At each fork it leans toward the channel whose openings lie nearest the ring. A rookie sometimes leans the wrong way.
- **Measured** (the level scorecard, seed 11, Hard):

  | Course | Omnicrystals off | Omnicrystals on |
  |---|---|---|
  | 1 | 77.5 s | 79.5 s |
  | 2 | 79.9 s | 82.1 s |
  | 3 | 82.8 s | 82.8 s |
  | 4 | 81.5 s | 79.9 s |

  - Every level finishes every course.
  - The labyrinth is about time-neutral for the AI: it is a fun detour, not a shortcut, because openings all round can
    leave you facing away from the ring. A finding for Yash, not tuned away.
  - With it off, the race is exactly the race before.
  - Leaning left, right, up and down took four different routes through the same labyrinth.
- **Built on the Meerkat's tunnels.** Each channel is its own pair with a sampled path (`wh.poly`); `whAt`, `whFrame` and
  `whRadius` dispatch to it, so the drawing, glass, bent space and collapse work per channel. The tree is not drawn
  round labyrinth channels.
- **Cost:**
  - A default labyrinth is about 24 channels.
  - Wireframe: about 3,000 spindles (`BR_MAX` raised to 30,000).
  - Soil: a wall mesh per channel, built once.
  - Stones: up to 3,000, one instanced mesh.

**Round 15 (Yash, 2026-10-28: "I have added some config settings in the artifact ... It's named tree1. Can you check
those and then repolish the configurations to make it look more like a big branch through which this meerkat is
traveling"). ARTIFACT ONLY.**
- **`tree1` could not be read.** The shared `profiles` collection is empty, and so are `decisions` (nothing newer than
  2026-10-17) and `requests`. The profile was most likely kept in the browser that saved it: a section profile falls
  back to that browser when the viewer does not serve the shared db. These defaults are therefore a polish by look, not
  `tree1`'s numbers. To send them: Load `tree1`, then press **Record**, or Save it again while signed in to claude.ai.
- **A big branch:**
  - The limb is thicker (`trunkTop` 1.5, `trunkBase` 1.9 × the mouth) and more gnarled (`trunkBark` 0.22,
    `trunkRidges` 16, `trunkGnarl` 0.14).
  - **New: bark grain.** Thin fissures run along the wood, broken into lengths, on the trunk and every branch
    (`trunkGrain` 0.5, `trunkGrainLines` 22 round it). Riding through, they stream past along the walls.
  - The side branches now grow all along the limb (`branchZone` 0.85, `branchCount` 8, `branchLength` 2.4,
    `branchThickness` 0.28, `branchUp` 28°, `branchSymmetry` 0.45).
- **The burrowing version was tried as its own vessel, the Gopher**: a network of channels through soil, with a choice
  at every fork. It was removed the same day, and its network became the Meerkat's Omnicrystal ability (round 16).

**Round 14 (Yash, 2026-10-28: "the moment AI or player ... press their right trigger to create the tunnel, the tunnel
will automatically be created with the trunk, not separate from the trunk ... just extend the outer layer of the tube to
some tree-like structure with big similar symmetrical or asymmetrical but similar branches from the top half ... the
wormhole is actually a portion of the trunk of the dead tree ... the entire tree will be this watery glassy space-time
warp effect, like the wall"). ARTIFACT ONLY: not in Unity until Yash imports it.**
- **The problem:** round 13's trunk was hidden from whoever rode the tunnel. So watching an AI, its trunk appeared only
  as it came out, and its dissolve and branches ran on their own clock after that.
- **The tree is the tunnel's outer layer now**, made the frame RT forms the tunnel, and seen by everyone, its rider too.
  - It shows exactly the tunnel's window: it ripples out of the centre to both mouths with the tunnel (`formSeconds`)
    and falls back in with it as it collapses. The separate dissolve is gone.
  - Each branch grows over `branchGrowSeconds` (0.5 s) from the moment the forming tunnel reaches its root, and shrinks
    back as the collapse passes it.
- **Its look is the wall's**:
  - **Glass:** the same Fresnel glass as the tunnel, the maker's colour at the way in toward white at the way out
    (`trunkGlass` 0.03 face-on, `trunkRim` 0.6 at the silhouette).
  - **Bent space:** the tube's own warp, drawn into the same warp buffer, `trunkWarp` (0.25) strong, even with
    `warpStrength` at 0.
  - **Water:** both flow slowly from the top to the bottom. Above about 0.4 the warp melts the tree's shape into a blob.
- **The branches are a dead tree's**:
  - `branchCount` (6) bare branches out of the trunk's top `branchZone` (0.5, its top half), in pairs facing each other.
    Each pair is turned about 70° round the trunk, so they make a crown, not a fence.
  - `branchSymmetry` (0.7): 1 mirrors each pair exactly, lower moves each branch to its own place and angle.
  - Each is `branchLength` (2.2) × the mouth's radius long and `branchThickness` (0.3) × it thick, and tapers to a point.
  - They lean `branchUp` (40°) toward the top, curl up, are crooked, and fork `branchForks` (2) times on alternate sides.
- **Retired:** `trunkGrowSeconds`, `trunkDissolveEdge`, `branchDelaySeconds`. **Added:** `trunkWarp`, `branchZone`,
  `branchSymmetry`, `branchForks`.
- **Cost:** one static mesh per tunnel (about 4,500 vertices), built once, drawn twice (glass, warp). The window and the
  branch growth are uniforms. Suggested for Unity: the same, two passes in the field's existing warp buffer.

**Round 13 (Yash, 2026-10-28: "the moment I activate the tunnel, the trunk shape should be created. Like dissolve
shader ... slowly it will grow some random branches. Left and right ... the main tunnel's entry will be ... the cut
portion of the trunk above, and the other end will be the trunk's below cut portion ... a Fresnel effect ... trunk-like
... grow based on its length and will contain the wormhole inside"). ARTIFACT ONLY: not in Unity until Yash imports it.
It replaces round 12's plate tree (the crown, bark plates and roots and their `coat*` / `crown*` / `root*` keys are
gone).**
- **The trunk** (`drawTrunk`, the Wormhole · trunk card). Once a tunnel forms, one hollow trunk mesh wraps it from the
  way in to the way out, so it grows with the tunnel's length and holds the wormhole inside. The way in is the trunk's
  upper cut and the way out its lower cut: both ends are open annuli showing tree rings (`trunkRings` 6).
  - **Shape:** its radius is `trunkTop` (1.25) × the mouth's radius at the way in, widening to `trunkBase` (1.6) × at
    the way out, never inside the tunnel. Bark ridges run along it (`trunkRidges` 14, depth `trunkBark` 0.18) with a
    slow gnarl (`trunkGnarl` 0.1). Each tunnel has its own seed, so no two trunks match.
  - **Look:** a Fresnel shell, not a volume: nearly clear face-on (`trunkGlass` 0.03), bright at the silhouette
    (`trunkRim` 0.8, falloff `trunkFresnel` 2.5), in the maker's domain colour, drawn additively.
  - **Dissolve:** it dissolves in from the way in to the way out over `trunkGrowSeconds` (1.4 s), with a noisy bright
    front `trunkDissolveEdge` (0.06) wide. A collapsing tunnel dissolves its trunk back with it.
  - **Branches:** `trunkGrowSeconds` + `branchDelaySeconds` (0.3 s) after the tunnel forms, `branchCount` (6) branches
    grow out of it over `branchGrowSeconds` (2.5 s), one after another, alternating left and right at random points
    along it. Each is `branchLength` (1.6) × the mouth's radius long and `branchThickness` (0.14) × it thick at its
    base, leans `branchUp` (35°) toward the way in, curls and forks once.
  - Whoever rides the tunnel does not see the trunk: the ride stays the clear pipe. `trunkVisibility` 0 hides it.
- **Cost:** one mesh per tunnel (56 rings × 36 sides, about 2,300 vertices), built once when it forms and rebuilt
  only when a shape slider moves; the dissolve runs in its shader. Branches are about 100 spindles in the bridge's
  existing instanced mesh.
  - Suggested for Unity: the same: build the trunk mesh once per tunnel and drive the dissolve and Fresnel in one
    unlit additive shader, so a frame costs one draw a tunnel.
- **Fixed with this round:** round 12's tree card was missing from the published studio (v72): the page's tab layout
  lists its cards by id, and the new card was not on the list. The trunk card is on the Play Style Config tab.

**Round 12 (Yash, 2026-10-28: "the mouth diameter should have a min and max ... the wormhole will be larger while
traveling more distance ... an upper coating around the wormhole to be visible from a third perspective ... the pattern
of the omnicrystal ... like a tree trunk ... we use the upper part of the tree to get inside and the root area to get out
... Do it in artifact first"). ARTIFACT ONLY: not in Unity until Yash imports it.**
- **Size grows with length.** `throatDiameter` and `mouthDiameter` are now the shortest tunnel's size, and
  `throatDiameterMax` (18) and `mouthDiameterMax` (56) the longest's. A tunnel in between grows in proportion to its
  length (`shapeFor`). Measured: an 85 u tunnel is 8.4 / 27.1 u, a 730 u one 17.6 / 54.8 u.
- **The tree** (`drawTree`, the Wormhole · tree card). A formed tunnel is a tree to everyone watching from outside.
  Whoever rides it does not see it: the ride stays the clear pipe.
  - **Crown, at the way in:** `crownBranches` (7) limbs reach back `crownLength` (1.6) × the mouth's radius,
    `crownSpread` (60°) off the axis, forking twice.
  - **Trunk:** bark of the omni crystal's own four plate shapes (Mass triangles, Space squares, Time rhombi, Charge
    pentagons) spiralling round it, `coatPerRing` (14) a row at `coatPlateScale` (0.9). They stand `coatLift` (0.18) ×
    the radius off the wall, tilted `coatTilt` (22°) like scales, with a wave (`coatWave` 0.6, `coatWaveSpeed` 0.3)
    running crown to roots, as the omni crystal's flip wave does.
  - **Roots, at the way out:** `rootCount` (9) limbs splay `rootLength` (1.8) × the mouth's radius, curling
    `rootSpread` (50°), forking once.
  - Limbs are `coatLimbThickness` (0.15) × the mouth's radius at their base. `coatVisibility` 0 hides the tree.
  - Pieces near any camera shrink away.
- **Cost:** about 1,000 to 1,700 plates for a long tunnel (capped at 2,400 a shape), drawn as four instanced meshes,
  plus about 170 spindles in the bridge's existing instanced mesh.
  - Suggested for Unity: build each tunnel's bark once when it forms (it does not move) and run the wave in the plate
    shader, so a frame costs only the four draws.
- **Not decided:** whether the tree also shows its maker's element levels (each plate family is an element).

**Round 11 (Yash, 2026-10-28: "make the wormhole look very realistic ... not super calculative ... performant and
realistic, fun to travel without getting dizzy ... a pleasant water ride experience through a water pipe. Transparent
pipe ... the reticle ... just pure wireframe and it should be less lit in the front side and more lit back, so I can see
the bending of the other end"):**
- **The reticle** is still the bare wireframe. Its brightness now runs from `reticleNearLight` (0.25) × at its way in,
  by your nose, to full at its far end, so the bend stands out. The page and the game draw it the same way.
- **The formed tunnel** (Garrett's contour field, Unity only: the page does not draw the field) is retuned as a clear
  pipe of gentle glass. Three looks were rendered in the Tube Lens Lab, from outside and riding through: round 10's
  values, a water pipe, and a glassier pipe. Round 10's tore and swirled the world seen through the wall; the water pipe
  bent it gently, so pillars seen through it stayed whole with a slight water wave. Its numbers:
  - strength 6, pitch 0 (along the flow), drag 0.5, twist 0;
  - glass 1 at Δn 0.05, grazing 0.9 (the lab: low grazing makes the ride view swirl);
  - flow Even, outer 0.15, profile Smoother;
  - ribs 0.25 at sharpness 3, travelling 1.2 rings/s, 16 × 12.
- **Cost:** softer ribs let the march take larger steps (the rib term caps the step by 1 / (2 × sharpness + 4): 11.12
  to 3 is a cap about 2.6 times looser). The glass term still costs four field reads a step; `fieldGlass` 0 is the
  cheapest pipe if a profile shows it. Not profiled in Unity.

**Round 10 (Yash, 2026-10-28: "the cameras, why offset should always be zero, the vessel should be in the center of the
screen and this way we don't need to lerp the camera during entering into the tunnel. It is like a sparrow, it's always
center. Also, I want to have options to control the diameter of the entry exit pair ... and the inner diameter ...
configurable at edit time ... the entrance and the exit of the wormhole need to be a little bit bigger"):**
- **The camera.** Both cameras sit straight behind the hull with no height and no lag. In the game this is
  `MeerkatCameraSettingsSO`: offset (0, 0, -21), hard-attached. Riding, the camera is the same distance behind you on the
  tube's centreline, looking at you. It is a cut, not a blend: on the straight run in, the two coincide. Out of the far
  mouth it keeps to the tunnel's line until it has passed the mouth. Measured: the hull is exactly at screen centre,
  flying and riding. The camera is within 0.29 u of the line in the tube and past the exit, and its largest step at RT
  and at the exit is one normal frame. Retired: `rideCameraBack`, `rideCameraLookAhead`, `rideCameraBlendSeconds`.
- **The size.** `throatDiameter` (8 u) and `mouthDiameter` (26 u, the entry and exit a little bigger than round 7's
  20 u) replace the two radius keys. They are set at edit time; the sticks never change them.

**Round 9 (Yash, 2026-10-28: "the wormhole look and the reticle look can be the same, just the reticle can be just a
wireframe version of the actual wormhole look ... the reticles and the bridges geometry need to be same, and the same
position same way ... The reticle is the silhouette of the geometry"):** one geometry. The bridge's rings, spindles per
ring, ribs, thickness and shimmer (`bridge*`) draw both the formed tunnel and your reticle (the page's `drawFrame`, the
game's `MeerkatWormholeView.BridgeFrame`): the same rings at the same places along the tunnel, condensing out of its
centre the same way. The reticle is that frame bare, with no glass and no path. It keeps its own brightness
(`reticleIdleAlpha`) and its own colour, your domain colour toward white by `reticleWhite`; the bridge's spindles take
`bridgeWhite` the same way. Retired: `reticleGlass`, `reticleRings`, `reticleSpindlesPerRing`, `reticleRibs`,
`reticleSpindleThickness`, `reticlePath`, and in the game Garrett's `reticleLook` switch (there is one reticle now).
Measured on the page: the shaped reticle and the formed tunnel are the same 931 spindles (36 × 21 rings, 5 ribs).

**Round 8 (Yash, 2026-10-28: "the updated look I want. By merging Garrett's look with my reticles ... I don't want any
camera Y offset. I want the camera to go inside the tunnel"):** the look Yash tuned in Unity's `MeerkatWormholeConfig` is
the studio's SHIPPED look now. Before RT the reticle draws as Garrett's bridge (`reticleLook` Bridge), with the bridge
spindles tuned to the reticle's: 36 rings × 21 spindles, 5 ribs, 0.25 u thick, `bridgeVisibility` 0.163, glass 0.15.
The reticle keys are 30 × 21, 5 ribs, 0.25 u, idle 0.053, glass 0.048, path 0.333, smoothing 0.123 s, and `rippleEdge`
is 0.154. After RT it is Garrett's contour field, with glass 1.579 at Δn 0.078, grazing 0.883, profile Smoother, ribs
0.155 at sharpness 11.12, travel 1.95 and 9 rings. Those field numbers live only in the Unity asset; the page does not
draw the field. Riding, the camera sits ON the tube's centreline, `rideCameraBack` (18 u) behind you, looks
`rideCameraLookAhead` (40 u) along it, and eases in and out over `rideCameraBlendSeconds` (0.25 s). There is no height
offset. Measured on the page: once eased in, the camera stayed within 0.23 u of the centreline (throat 4 u) on every
frame, for a straight tunnel and for 114° and 150° bends on short and long shafts.

**Round 7 (Yash, 2026-10-27: "the diameter of entry can stay fixed, and we can then just use the bend to choose the distance from where it can turn 90 or 120 or 150 degrees ... it can be a long umbrella stick like geometry"):** the tunnel no longer grows shorter and fatter as it bends. It
is a straight shaft and then one bend of fixed radius `bendRadius` (60 u). LT sets the shaft (`shaftMin` 60 u released
to `shaftMax` 600 u squeezed), the right stick the bend's direction and angle (up to `bendMaxDegrees`, now 150°), RT
forms it. The throat (`throatRadius` 4 u) and mouths (`mouthRadius` 10 u) are narrow and fixed. The five old shape keys
(`lengthStraight`, `lengthBent`, `throatStraight`, `throatBent`, `mouthFlare`) are retired. The hull turns only on the
bend. The AI now picks the shaft and bend that reach its ring soonest (the ride at its mean speed, then the flight on,
then the turn left over at `pitchYaw`): it finishes every race, I3 104-108 s and I4 94-111 s over four seeds.

**Round 6 (Yash, 2026-10-27: "it should enter the wormhole with v and then it will speed up to v plus dv, and then while
exiting it will exit at little bit boosted"):** the ride is your speed before the tunnel times three multipliers. It is
`rideEntryMultiplier` (1) going in, rises to `rideMidMultiplier` (2.5, was `throatSpeed`) at the throat, and eases to
`rideExitMultiplier` (1.2) at the way out, then the hull eases back to cruise. Measured: in at 60 u/s with no slowdown,
about 150 at the throat, out at 72, back to 60 within about 1.5 s. The AI still finishes every race (98-103 s, seeds 1-4,
I1 and I3).

**Round 5 (Yash, 2026-10-27: "use 5x the normal value when going through the wormhole"):** riding a tunnel, the game's
speed tunnel (its dolly zoom: a narrower field of view and a relaxed Panini projection, `Docs/SPEED_TUNNEL.md`) is
`rideSpeedTunnelScale` (5) × its normal strength. The studio previews the field of view: 68° at the mouth, 23.8° at the
throat of a straight tunnel, then back to 68°.

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
| shape | `bendMaxDegrees` | 150° | The sharpest bend (right stick fully over) |
| shape | `shaftMin` | 60 u | The straight shaft before the bend, LT released |
| shape | `shaftMax` | 600 u | The shaft with LT fully squeezed |
| shape | `bendRadius` | 60 u | The bend's radius (fixed): a tunnel is shaft + `bendRadius` × its angle long |
| shape | `throatDiameter` | 8 u | The throat's diameter (fixed, set at edit time; round 10) |
| shape | `mouthDiameter` | 26 u | The entry and exit diameter at the shortest tunnel (round 10: was 20; round 12: the shortest's) |
| shape | `throatDiameterMax` | 18 u | The throat at the longest tunnel (round 12, artifact only so far) |
| shape | `mouthDiameterMax` | 56 u | The entry and exit at the longest tunnel (round 12, artifact only so far) |
| tree | `trunkVisibility` … `branchGrowSeconds` | see the page | The tunnel's outer layer, a dead tree in the wall's glass and bent space, formed with the tunnel (rounds 13-14, artifact only so far; replaced round 12's plate tree) |
| shape | `keyRampSeconds` | 0.5 s | LT on keys or touch reaches full after this |
| ride | `rideEntryMultiplier` | 1 | The ride going in, × your speed before the tunnel |
| ride | `rideMidMultiplier` | 2.5 | ...fastest, at the throat (was `throatSpeed`) |
| ride | `rideExitMultiplier` | 1.2 | ...coming out: a little boost, easing back to cruise |
| ride | `rideSpeedTunnelScale` | 5 | Riding: the game's speed-tunnel dolly zoom, × its normal strength |
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
| reticle | `reticleWhite` | 0.45 | Your reticle's colour: your domain colour toward white (round 9; its geometry is the bridge's) |
| reticle | `reticleNearLight` | 0.25 | Its brightness at the way in against its far end, so the bend stands out (round 11; 1 = even) |
| look | `bridgeVisibility` | 1 | The formed tunnel's bridge, for everyone (0 = hidden) |
| look | `bridgeGlass` | 0.6 | ...its glass surface |
| look | `bridgeRings` | 16 | ...rings of spindles along it |
| look | `bridgeSpindlesPerRing` | 12 | ...spindles round each ring |
| look | `bridgeRibs` | 6 | ...ribs of spindles along it (0 = none) |
| look | `bridgeSpindleThickness` | 1.6 u | ...spindle thickness at the middle |
| look | `bridgeShimmer` | 0.5 | ...a shimmer running way in to way out (0 = steady) |
| look | `bridgeWhite` | 0.45 | ...its spindles' colour: the domain colour toward white (round 9) |
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

## Before and after RT (Garrett, 2026-10-27)

Garrett picked the Meerkat's look in the Tube Lens Lab (https://claude.ai/artifact/PEScFn528pACfFq2KVNJRF):
- **Before RT:** the reticle looks like the bridge (spindles over glass in the pilot's domain colour).
- **After RT:** a formed tunnel is ONLY a contour-field lens: space bent in a thin shell on the wall, along its rings,
  graded to zero just off the surface. No bridge, no glass, no grid.

The game has it first (`R_VesselActions/MEERKAT.md` §2.1: `reticleLook`, `formedLook` and the 16 `field*` fields). This
page does not yet: its Wormhole look still shows round 4's bridge. The next round brings the lab's field onto this page
and maps the new keys in `studio_to_unity/meerkat.json`.

## Garrett's direction: the soliton kit (2026-10-27, for Yash to judge)

Garrett, 2026-10-27: *"the meerkat should be producing a new fundamental (solitons) and therefore a new pilot that
applies its vision power to them."* The design gate for the Meerkat's kit stays **Yash's** (round 8), so this section is
Garrett's direction beside Yash's round-8 proposals, to be flown and judged in the artifact. Nothing here is built.
Garrett finished the Sentinel and Mass rows in a second message the same day.

**The soliton** is a standing tunnel in space: the wormhole the Meerkat already makes, now a thing in the world anyone
can use. It is a new fundamental (`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md` § Soliton) with its own pilot sight
(FLUME, `Docs/Pilots/CALLSIGNS.md`).

| Element | Ability | What the element's level does | L5 upgrade | Source |
|---|---|---|---|---|
| **Space** | **Soliton**: the wormhole (RT, the sticks shape it, rounds 1-7) makes a soliton | Sets the soliton's **surface area** (below) | open | Garrett |
| **Time** | **Passive**: the Meerkat rides **every** soliton faster, its own or anyone's | Scales the ride's rush (today `throatSpeed` 2.5 × the entry speed) | **Boost out**: the Meerkat leaves every soliton with a boost | Garrett |
| **Charge** | **Sentinel**: left automatically at **every tunnel entrance**, an afterimage of the Meerkat that perches up like a real sentinel meerkat, guarding the way in. It **zaps enemy vessels**, knocking out their elements, and **zaps same-domain prisms**, turning them **shielded** | Proposal: the zap's reach and how often it fires | open | Garrett |
| **Mass** | **Passive**: the soliton **makes prisms at its far end** (the way out) | Their **size** | **Danger prisms**: at Mass L5 the prisms it makes are danger prisms | Garrett |

**What everyone sees and does with a soliton:**
- **The placement grid is the Meerkat's alone**: its reticle, already drawn only for its pilot.
- **Everyone else sees the soliton by its lensing alone** once it is made: the contour field (§ Before and after RT).
- **Any vessel can ride it**, not only its maker. A rider dives in at a mouth and rides it as the Meerkat does: in at its
  entry speed, rushing through the throat, out at the far mouth on its last heading.
- **It swallows prisms and fires them out of the other end.** Prisms near the way in are drawn in, run the tube and leave
  the way out along its last heading.
  - A fired prism is a MOVING prism (§ Prism movement): one that hits another prism destroys both, scored to the soliton's
    maker. A SHIELDED one bounces instead (Garrett, 2026-10-27, `Docs/PRISM_MOTION.md` 1a: built). Mass is conserved,
    because the prism is moved, never deleted.
  - Super-shielded mass is never moved. Whether shielded mass is drawn in is open.
  - A fired prism that reaches a vessel follows the shipped prism rules. PvP stays petals only.

**The Sentinel (Charge)**:
- **Enemy vessels:** a zap is a scored hit, so it stays inside PvP-is-petals-only. Knocking out elements is a ranged
  verb, so the petals are EJECTED as free crystals (`Docs/ELEMENTAL_ECONOMY.md`), and the hit silences the victim for
  what it is worth (`SILENCE.md`).
- **Same-domain prisms:** zapped into shielded prisms.
- **With the soliton:** shielded prisms now bounce instead of annihilating, so the Meerkat's own mass round its
  entrances becomes ammunition that survives. The soliton swallows it, fires it out, and it rebounds off what it hits.
- **Open:** its reach, how often it zaps, how many sentinels can stand at once (one per tunnel, so they collapse
  with it or outlast it), and whether a rival can knock one down.

**Mass (passive):**
- The soliton makes prisms at its way out on its own, for as long as it stands. Mass sets their size; at L5 they are
  danger prisms, which burn the petals of whoever touches them.
- They are the maker's domain, so a sentinel can zap them shielded.
- **Open:** the rate, whether they are fired along the exit heading (as moving prisms) or set down at the mouth, and
  a cap per soliton.

**Surface area (Space).** At a given Space level a soliton keeps one surface area A, so a longer tube is thinner and a
shorter one fatter. The wall is round 7's hyperboloid with the mouth a fixed multiple k of the throat (M = k·b), and its
area is

    A = 2π · L · b · g(k),    g(k) = ½ · (k + asinh(√(k²−1)) / √(k²−1))

so the throat is **b = A / (2π · L · g(k))**. LT sets the length L (shaft + bend, round 7), and the throat and mouth
follow from A. At round 7's numbers (throat 4, mouth 10, so k = 2.5 and g = 1.592):

| Length L | Today's area (throat fixed at 4) | Throat at A = 15,000 u² | Throat at A = 40,000 u² |
|---|---|---|---|
| 60 u (shortest shaft, no bend) | 2,401 u² | 25.0 (mouth 62.5) | 66.7 (mouth 166.6) |
| 200 u | 8,002 u² | 7.5 (mouth 18.7) | 20.0 (mouth 50.0) |
| 400 u | 16,004 u² | 3.7 (mouth 9.4) | 10.0 (mouth 25.0) |
| 757 u (longest shaft, 150° bend) | 30,287 u² | 2.0 (mouth 5.0) | 5.3 (mouth 13.2) |

The Space level would set A through an `ElementalFloat` (the two areas above are only examples). A shortest tube at
any useful area is very fat, so the throat may need a floor and a ceiling.

**Where this meets round 8 (for Yash):**
1. **The wormhole's element.** Round 8 puts it on Time, with Chain Dig as its L5. Garrett puts it on **Space**, with
   Time a passive and its L5 a boost out of every soliton.
2. **Charge and Mass.** Garrett's Sentinel and passive mouth prisms replace round 8's Lookout and Hardened Bridge.
3. **Space.** Round 8's Cave-in (destroy the prisms round your tunnel) has no slot in Garrett's kit. The soliton moves
   prisms instead of destroying them.
4. **LB.** Round 8 arms Hardened Bridge with **LB**, but LB is now the pilot's vision on every vessel
   (`Docs/Pilots/CALLSIGNS.md` §5). In Garrett's kit Mass and Charge are passive, so the kit needs no button beyond
   the wormhole's.
5. **Diameter.** Round 7 fixed the throat ("the diameter of entry can stay fixed"); Garrett's surface area couples it to
   the length again. A floor and ceiling on the throat is one way to keep both.

## Two kits: Garrett's and the Mix (round 18, 2026-10-28, for Yash to judge)

Yash, 2026-10-27: *"Make two variants, one with his kit only, one with a mix of his kit with the ones you recommend ...
Do this for every decision ... whatever his extra features there are, add those. And remove our extra features which he
already made."* The studio page (`Docs/Studios/VesselStudio/meerkat.html`, round 18) has a **Kit** dropdown at the top of
Play Style Config. It picks what every Meerkat in the race flies, and it is kept in the page's saved state and in
section profiles:

- **Garrett's kit**: his design as written. The source is his branch, not this one: `origin/cece/lucid-brown-ykcmhu`,
  `Docs/Studios/MEERKAT.md` § "Garrett's direction: the Meerkats and the soliton" (six messages). The section above is
  an earlier version of it. Where his design leaves a choice open, the page takes the simplest reading, and the
  tooltip says "open in Garrett's design".
- **The Mix (our recommendation)**: Garrett's kit with one change per decision (the table). Everything not in the
  table is exactly Garrett's.
- **Round 8 (archived)**: the round-8 proposals unchanged (Lookout, Hardened Bridge on LB, Cave-in, Chain Dig). They are
  kept to replay. **LB now conflicts with the fleet's pilot sight.**
- **Off**: no kit, the wormhole alone.

Round 8's Lookout and Hardened Bridge are not in Garrett's kit or the Mix: his Sentinel mode and exit portal cover them.

**These are prototypes for Yash to fly and judge.**
- Every kit number is lab-only. No `MeerkatWormholeConfig` field holds any of them, and none is in
  `Tools/Build/studio_to_unity/meerkat.json`.
- Nothing reaches Unity without Yash's say-so.
- Garrett's design is his to change.

**Byte-identical to round 17.** Off's headless races match the round-17 page (`origin/Ys-bleeding-edge`, a44f8f5835)
with round 8's abilities switched off and Mass left at 5; Round 8's races match that page as it ships. Both were checked
with `runBatch` (seed 11, every level and course).

**Round 17's Mass in the kits.** In Garrett's kit and the Mix the kit's one Mass level (`kMassLevel`) is the Mass that
round 17 sizes the trunk with (plus `massPerCrystal` a crystal this race), so one Mass sets both the trunk's reach and
the exit portal. Space still sets the throat by surface area (D5). Off and Round 8 keep Vessel Config's `massLevel`.

**The Omnicrystal's roots (rounds 16-17) are the fifth card (omni) in both kits, unchanged**: since round 17 they grow
at once where you take an omnicrystal, every way, sized by your last trunk (taken inside a tube, it arms your next RT).
The one choice per kit is who may slide into roots another pilot grew:
- **Garrett's kit**: anyone. This is his soliton rule, so rivals slide into yours.
- **The Mix**: the maker's team only.

In Garrett's kit, FLUME shows the roots' channels as solitons. Swallow-and-fire, Cave-in and sentinels do not act on
roots: that is not modelled.

**Sound.** Every kit event calls `VesselStudioAudio.play(name, sndAt(position))` through one guarded helper
(`kitSnd`): `sndAt` pans it to the camera's side and fades it to nothing by ~900 u, so a far rival's event is not heard.
The names: swallow, fire, shatter, bounce, boostOut (Garrett's Time L5), chainDig (the Mix's Time L5), sentinelPlace,
zap, shieldZap, sentinelLost, alarm (the Mix), hop and seatSwap (Garrett's), portal or dangerPortal (Mass L5), caveIn
(the Mix's B); FLUME loops (`loop('flume','flume',1)`) while LB is held and stops on release. The sounds only listen:
batch and headless runs play nothing, and the level batch is byte-identical with a stub audio object present and absent.
Measured live with a stub (90 s, Hard, Hard and Medium rivals): Garrett's kit fired zap 239, boostOut 101,
sentinelPlace 77, dangerPortal 76, swallow and fire 51, hop 15, shatter 13, bounce 6; the Mix fired zap 321, swallow
and fire 228, sentinelPlace 111, dangerPortal 109, shatter 55, chainDig 48, bounce 38, alarm 29, caveIn 28, shieldZap
22. Holding Q started the flume loop once and releasing it stopped it once. seatSwap needs a person (the AI never swaps).

| Decision | Garrett's kit | The Mix | Why the Mix differs (measured below) |
|---|---|---|---|
| **D1** the wormhole's element | Space: the soliton. Time: a passive faster ride through every soliton; L5 **Boost out** | The same, but Time L5 is round 8's **Chain Dig** (RT late in a ride digs the next tunnel from the far mouth) | Boost out is passive. Chain Dig keeps a racing decision at every tunnel: when, and which way, to dig on. The Mix's AI chained 7.4 times a field race |
| **D2** Charge | **Sentinel mode** on Y: pool 3, 6 at L5; zaps in sub-petal ticks (0.1 to 0.25 of a petal by Charge); shields its own prisms; the oldest retires with its soliton; **sentinels are seats** | Sentinel mode the same, **without seats**: every sentinel is AI-guarded. It also does Lookout's job: a rival entering any of your guards is **marked for you** for 4 s | In a one-person studio a seat costs the race. A person who sat in sentinels for 72 s lost the flyer twice (the element-holder rule) and finished in 1:32.6. The AI crew's field races took 1:03.5 to 1:23.0 (round 18). The alarm keeps round 8's "see the rival" job in a passive form: 20.4 alarms a field race |
| **D3** Mass | **Exit portal**: six small prisms on a hexagon at 1.5 x the exit radius, once per soliton, size by Mass; danger prisms at L5 | The same. **Hardened Bridge removed** | His portal is the minimal-mass version of the same idea, and it frees LB for the pilot sight |
| **D4** Space | **Swallow and fire**: prisms at the way in (shielded too, never super-shielded) run the tube and leave the way out as moving prisms (`Docs/PRISM_MOTION.md` 1 and 1a). L5 open | The same, **plus Cave-in on B** in the open Space L5 slot: your nearest soliton fires out everything it holds at once and collapses, and a rival riding it takes a scored hit (round 8's Burial; never pushed) | It gives Space an active verb and a reason to time the fire. 11.7 Cave-ins and 6.7 burials a field race |
| **D5** diameter | **Surface area** A = 2 pi L b g(k), so b = A / (2 pi L g(k)); unclamped but for a 0.5 u safety floor | Surface area, with the throat's diameter held between a **floor and ceiling** (sliders: 3 u and 25 u) | At Space L5 (A = 12,000 u2), Garrett's shortest tunnel has a 33 u throat and a 107 u mouth, and the longest a 2.6 u throat. The clamp keeps a long tunnel's mouth wide enough to swallow: 67.3 prisms fired a field race against 19.2 |
| **D6** dynamic seats | **The crew**: D-pad left / right (keys `[` `]`, phone SEAT or a two-finger swipe) steps the flyer, then each sentinel. A sentinel seat has a perched 2.5D view over its way in: click or tap a prism to shield it (any domain), or a rival to zap it (gamepad: right stick cursor, RT). **Y hops**: ride its soliton out as the flyer, while the old flyer becomes a sentinel at a new soliton its AI planner shapes. The meerkat you sit in holds the elements; any other dies of an element hit | **None** (D2). The flyer always holds the elements; sentinels still die of any element hit | See D2 |
| **Omni** (rounds 16-17) | The roots, unchanged; **anyone** may slide into yours | The roots, unchanged; **your team only** | The Mix keeps the roots a private shortcut. In Garrett's kit pilots slid into another's roots 2.6 times a field race (1.8 of them into yours) |
| **LB** | The pilot sight, **FLUME**: hold LB (Q; phone SIGHT) and every soliton is outlined through anything, with where it leads (the ring its way out heads at) and how long it is | The same | The fleet's rule (`Docs/Pilots/CALLSIGNS.md`) |

**Open in Garrett's design, and the reading the page takes:**
- the swallow zone: the guard, 1.5 x the opening;
- the carry, fire and glide speeds (150, 120 u/s, 3 s);
- the surface areas (6,000 to 18,000 u2 by Space);
- a danger prism's cost (0.5 of a petal);
- sentinels zap sentinels: a sentinel is a meerkat, so an enemy one in a guard is zapped, and it dies (it does not hold
  the elements);
- with no flyer, the AI hops for you after 3 s. Garrett's proposal is "the oldest sentinel's AI hops";
- the AI's own hop rule: an AI crew hops when that saves 1.5 s to its next ring. Garrett left the hop to people.

"Seated longest" is the person: the studio has one person and no teammates.

**Measured** (headless, seeded, 2026-10-28, after the merge with round 17, which changed the sim: Mass sizes the trunk
and the roots grow at once). Warpline, course seed 7, AI seeds 11, 23 and 37, omnicrystals on (the default). **Score
the kits** in AI Config makes these tables. No DNF in any of the 240 races, field races included; mass conserved in
every one.

*Race times, your hull alone* (the median of the 3 seeds, m:ss.s, Easy / Medium / Hard):

| Kit | I1 | I2 | I3 | I4 | Levels in order | No wormhole I1-I4 |
|---|---|---|---|---|---|---|
| Off | 1:31.5 / 1:36.1 / 1:35.0 | 1:27.5 / 1:26.5 / 1:26.2 | 1:44.3 / 1:43.4 / 1:40.1 | 1:32.8 / 1:28.2 / 1:25.4 | 3 of 4 | 1:50.6 · 1:52.7 · 2:01.7 · 1:52.1 |
| Garrett's | 1:22.2 / 1:18.9 / 1:24.1 | 1:21.1 / 1:23.6 / 1:03.5 | 1:32.8 / 1:21.9 / 1:30.2 | 1:21.8 / 1:17.8 / 1:16.5 | 1 of 4 | 1:50.1 · 1:52.6 · 2:03.8 · 1:51.1 |
| Mix | 1:25.2 / 1:20.4 / 1:27.3 | 1:23.8 / 1:23.5 / 1:25.6 | 1:24.2 / 1:25.5 / 1:17.0 | 1:20.0 / 1:16.2 / 1:10.2 | 1 of 4 | 1:50.5 · 1:52.5 · 2:02.0 · 1:51.7 |
| Round 8 | 1:28.7 / 1:21.4 / 1:20.3 | 1:31.5 / 1:23.5 / 1:25.0 | 1:26.5 / 1:29.6 / 1:32.3 | 1:21.8 / 1:19.5 / 1:17.2 | 2 of 4 | as Off |

("Levels in order" counts Off's I1 as out: Medium is slowest there.)

*The same with omnicrystals off* (round 17's roots-at-once taken out, everything else the same):

| Kit | I1 | I2 | I3 | I4 | Levels in order |
|---|---|---|---|---|---|
| Off | 1:31.3 / 1:30.1 / 1:28.7 | 1:26.4 / 1:25.5 / 1:24.3 | 1:34.4 / 1:33.2 / 1:31.2 | 1:30.2 / 1:28.6 / 1:26.0 | 4 of 4 |
| Garrett's | 1:22.2 / 1:21.1 / 1:19.4 | 1:17.3 / 1:15.0 / 1:06.7 | 1:25.2 / 1:21.9 / 1:22.3 | 1:21.8 / 1:21.2 / 1:19.2 | 3 of 4 |
| Mix | 1:22.8 / 1:18.7 / 1:16.4 | 1:19.9 / 1:15.8 / 1:13.2 | 1:22.0 / 1:20.0 / 1:15.4 | 1:19.6 / 1:15.2 / 1:10.2 | 4 of 4 |
| Round 8 | 1:23.2 / 1:18.7 / 1:17.6 | 1:22.3 / 1:18.8 / 1:19.3 | 1:25.5 / 1:23.3 / 1:22.2 | 1:21.8 / 1:18.5 / 1:20.9 | 2 of 4 |

So the roots grown at once scramble the level order in every kit (they take a pilot wherever they lead, and which
pilot meets an omnicrystal is luck of the line); without them the Mix and Off are in order on all four courses.

*In a field* (your hull at Hard against Hard, Medium and Easy rivals flying the same kit; I1 to I4 x 3 seeds = 12
races a kit; each number is the mean a race; omnicrystals on):

| | Garrett's | Mix |
|---|---|---|
| Your time (median by I1 / I2 / I3 / I4) | 1:07.1 / 1:07.0 / 1:23.0 / 1:16.3 | 1:26.3 / 1:15.9 / 1:19.5 / 1:10.2 |
| Zaps (all sentinels) · of them yours · petals knocked out | 120.2 · 84.2 · 20.3 | 137.1 · 69.5 · 22.3 |
| Own prisms zapped shielded | 0.4 | 10.0 |
| Prisms fired · destroyed · bounced | 19.2 · 10.8 · 7.7 | 67.3 · 38.5 · 23.1 |
| Rival rides through your solitons · rides into another's roots (into yours) | 20.0 · 2.6 (1.8) | 16.3 · 0 (team only) |
| Hops (the AI's rule) | 4.6 | none (no seats) |
| Cave-ins · burials | none (no Cave-in) | 11.7 · 6.7 |
| Sentinels lost (killed) · of them yours · retired | 8.5 · 1.6 · 2.6 | 15.8 · 1.8 · 3.3 |
| Alarms · Chain Digs | none | 20.4 · 7.4 |
| Solitons made · portal prisms set down | 28.9 · 173.5 | 46.8 · 281 |
| Mass conserved (moved, never deleted, but for a destroyed plain pair) | yes, every race | yes, every race |

Off makes no kit events; Round 8 makes its own (6.4 Cave-ins and 7.3 Chain Digs a field race).

*A person in a sentinel seat* (Garrett's kit, scripted, the earlier run): the AI flew 20 s, then a person took a seat
and stayed in seats for 72 s; the flyer died twice, zapped while the person held the elements in the seat. Each time
the AI hopped the person out after 3 s. The race finished in 1:32.6. Re-checked after the merge: Y toggles Sentinel
mode, the swap seats you in a sentinel and Y there hops you out; in the Mix the swap is refused (no seats).

**Our read (for Yash), measured:**
- **The Mix races better, and it is the one to fly first.** With the roots out of the picture its levels are in order
  on all four courses and its Hard is the fastest alone on I1, I3 and I4 (1:16.4, 1:15.4, 1:10.2). Its prisms do
  something: 3.5 times as many fired and destroyed as Garrett's.
- **Garrett's kit has the single fastest race** (I2 Hard, 1:03.5 with the roots, 1:06.7 without) **and the more original
  game**: rivals ride your solitons 20 times a race, your sentinels zap them 84 times, and the crew and the hop are new.
  Only a person can judge how they feel.
- **Garrett's weak spots**: a seat costs the race in a one-person studio (the flyer dies); unclamped surface area makes
  short tunnels huge (a 107 u mouth) and long ones pinholes, so little is swallowed (19 prisms a race); its levels are
  out of order even without the roots (I3).
- **The Mix's weak spots**: your own field time is worse on I1 (1:26.3 against Garrett's 1:07.1: Cave-ins and zaps cut
  both ways); sentinels kill each other on the shared race line (15.8 a race); mass piles up (281 portal prisms a race,
  none ever leaves).
- **Both, since round 17**: the roots grown at once undo the level order. A finding for Yash, not tuned away.
- The seat view, the hop and FLUME were driven by scripts here, not played.

**Not modelled:**
- a fired prism hitting a hull;
- teammates, so seats for another person and "seated longest" across people;
- the hull's trail being swallowed;
- danger prisms on the field's own prisms;
- the AI choosing another pilot's roots (a rival slides in only when its path crosses their source);
- swallow-and-fire, Cave-in and sentinels on the roots.

## What is not decided or not built

- **The hull.** The Meerkat flies the Stoat's model and its lope as a stand-in, in the studio and in the game. A real
  hull is `/vessel-model` work.
- **Its other three abilities and the element map** (`/vessel-studio` D37): proposed in round 8 (Lookout, Hardened
  Bridge, Cave-in, Chain Dig), for Yash to judge here. Not decided, not in Unity: the game's map still has four open
  slots and the wormhole is its one ability. Not modelled: a team-mate riding your hardened tunnel (only its maker
  rides a tunnel), scaling each ability by its element level, Garrett's after-RT look with a hardened tube (the page
  still draws round 4's bridge).
- **The bridge from the game's `Spindle` prefabs.** The game draws each spindle as a tapered mesh (a prefab per spindle
  would be ~280 objects per tunnel).
- **The game's Stoat still has round 18's straight warp gate on B** (`StoatModeAction` gate* fields, `StoatGateMath`).
  It is unlinked from the studio now. Removing it from the Stoat is Garrett's call (the Stoat's owner).
- **The kit** (round 18): Garrett's kit and the Mix are prototypes in the studio (§ Two kits), for Yash to judge.
  Neither is decided, and nothing of either is in Unity.
- **The play styles.** The Meerkat has none yet.
- **Learning.** The AI learning sweep records Burrow Run races; no learner reads the Meerkat's yet.

The parked black/white-hole look for tunnels is in `Docs/Studios/STOAT_PARKED_HOLE_LOOK.md`.

## Sound (2026-10-28, artifact only)

**On / off:** the **Sound: on / off** button at the top centre of the stage, the **M** key, or **Sound on** at the top
of **Vessel Config > Sound**. All three are one switch, remembered in this browser (`vessel-studio-sound-on`).

**The sound set (rebuilt the same day, Yash: "professional space sounds, not childish ones ... common good sounds").**
Every sound is made of what film and game space sound is made of: deep sub impacts and swells (a softly saturated
sine), airy filtered whooshes and risers, soft bell tones with long tails, and pads, all through one shared 4 s space
reverb and a gentle compressor. No square-wave beeps or arpeggios. The ride inside a tunnel is deep bass with a rare
high glint. Each sound keeps its Play, On, Volume, Pitch, Length, Brightness and Confirm / Redo.

| Group | Sounds | Wired in the page |
|---|---|---|
| Wormhole | reticle hum (loop, rises with the bend), tunnel forms, ride rush (loop, follows the ride speed), out of the tunnel, tunnel collapses | yes (rivals' tunnels by distance and side) |
| Omnicrystal: labyrinth | collected, armed, digs, slide in, take a fork (panned to the side), out of an opening, falls in | yes |
| Space: soliton | swallow, fire, annihilate, shielded bounce, surface area, Cave-in | yes, at the kit's own events (`kitSnd`: `sndAt`'s pan and distance) |
| Time | Boost out, Chain Dig | yes, at the kit's own events |
| Charge: sentinel | stands up, zap (rate-limited for ~5 a second), shields a prism, lost, alarm call, hop, seat swap | yes, at the kit's own events |
| Mass | exit portal, danger portal | yes, at the kit's own events |
| Pilot | FLUME sight (loop while LB is held) | yes, at the kit's own events |

The sounds watch the sim and never change it. They are the brief for the game's FMOD events, which ship empty until
sound design makes them (`MeerkatWormholeConfigSO.formEvent` / `collapseEvent` today); Unity plays none of these.
Measured headless: every recipe renders (none silent, none clipping), and an AI race with an omnicrystal fires every
wired sound with no page error.
