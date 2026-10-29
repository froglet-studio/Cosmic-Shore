# Goby & Pistol: the first duo vessel

> **THE KIT AS IT IS NOW (Garrett, 2026-10-22) - read this first; it supersedes §3, §5, §6, §9-§10 and the earlier log
> entries wherever they disagree.** Built straight in Unity ("given that we already have the pair in the game we should
> be able to save the time and go straight to unity"); the lab (`goby.html`, round 13/14) is BEHIND the game until a lab
> round ports this kit, and `studio_to_unity` no longer maps the Goby (`clones.json` records the retirement).
>
> - "it should not have the squirrels abilities or be anything like the squirrel. it should be a goby with its own kit
>   that represents a goby and synergizes with the pistol and its kit" **[stated]**
> - "The goby should dart and perch similar to the way the shrike shoots itself" **[stated]**. **[read]:** the Goby is
>   CRUISES between perches, as the Shrike does between docks (2026-10-23; it was always pinned until then, which left it
>   "barely cruising around at all"): a person on the hull's own two-thumb flight, the seat's AI at `aiCruiseSpeed` 100.
>   Hold Dart to charge the distance (`dartMinRange` 140 -> `dartRange` 600 over 0.8 s), let go, and it flies there in
>   `dartSeconds` 1 (the Shrike's distance / 1 s), stopping on the first block on its line. The dart's end is a PERCH
>   (the end of the Pistol's next island) once it is `islandMinLength` 900 from the last perch, while fewer than
>   `islandQueue` 3 islands wait ahead of the Pistol; a longer gap is laid as islands of at most `islandMaxLength` 1800.
>   Otherwise the dart is a dash and it cruises on. Perched, it is pinned and aims for `perchSeconds` 1.5 (longer while a
>   dart charges), then lifts off (`GobyIslands`).
> - "the goby may shoot into open space and make its own perch" **[stated]**. **[read]:** a dart that reaches nothing
>   lays a small pad of its own blocks under the Goby there.
> - "the slope from one perch to another should be a fun downhill for the shrimp" **[stated]**. **[read]:** each dart
>   lays its trail as the Pistol's ground, and the Pistol rides it as **Tiny Wings** (Garrett, 2026-10-22: "the
>   reference for the pistol is the game tiny wings"; until 2026-10-24 a sinusoid with a swell on a 5 deg lean, the third and
>   fourth passes below). **Since 2026-10-24 the ride is Tiny Wings' own, measured from the game's remakes
>   (`Docs/PISTOL_TINY_WINGS_STUDY.md`, the fifth pass below):** the ground is random KEY POINTS, alternately a crest and a
>   trough, 10-15 u on and 3.6-7.2 u up or down, a half-cosine between (`PistolPlane.HillTrack`, seeded from the island's
>   perches), level (`slopeDegrees` 0), an island of 900-1800 u (36-73 hills) starting on a crest at the perch and ending
>   on a climb to the next; a frictionless body under `pGravity` 19, a dive x7.5, drag 0.05, a landing that keeps its
>   speed along the slope (`pHardKeep` 1; within `pGoodLand` 30 deg a perfect slide, `pSmoothBoost` 1.05), a floor of
>   `pMinSpeed` 6, the aimed jump off (`aimedJump`). The camera is the game's (`PistolPlane.TinyWingsView`): 29 u across
>   at rest with the Pistol 0.15 in from the left, zooming out by height to keep a fifth of headroom, to 150 u.
> - "the non-danger mass on either side of the pistol's plane (which no longer needs to bend at all) is pulled into that
>   plane based on the pair's space. Space L5 pulls in shielded prisms too." **[stated]** The plane is FLAT: the
>   unrolled ribbon and its warped view are retired. **[read]:** super-shielded and the pair's own prisms never move.
> - "once the shrimp gets to a perch it spends a moment resting and building a den of prisms and shielded prisms. if the
>   goby has moved on to a new perch the pistol can choose to travel backward to an old den and it will add prisms to a
>   den based on the distance traveled between perches and mass the pair has collected, L5 mass produces danger prisms
>   and a supershielded prisms." **[stated]** **[read]:** "mass the pair has collected" = the pair's Mass level; a rest
>   adds one prism per 40 u travelled since the last den, x1 -> x2.5 with Mass, at most 80 per den (`GobyDen`); the
>   Pistol's Back button turns it toward the older dens at a perch.
> - "The goby can lock on to vessels and fauna. if a locked vessel enters the pistols view, a fired vortex ring will be
>   fired at the locked target. the number of petals lost from a vortex ring hit scales with charge." **[stated]**
>   **[read]:** Lock takes the best target in the nose's cone (vessels first, then fauna, the Shrike's order); a ring
>   homes at 240 °/s; a hit is one scored PvP hit taking 1 -> 3 petals from each element with Charge.
> - "time is the cooldown on the pistol's jump. time L5 allows the goby to phase through prisms when darting."
>   **[stated]** (jump 9 s -> 3 s).
>   **[open] since 2026-10-22:** the aimed jump is off ("launching intentionally was wierd"), so Time's base card has
>   nothing to drive until Garrett names what Time does on the Pistol (or turns `aimedJump` back on).
> - "when the goby's trail is destroyed the shrimp should have to clear the destroyed section. if they fall they should
>   not hit phantom prisms. they should fall into a fun animation that brings them back to a den, but without the mass
>   creation." **[stated]** (2026-10-28). **[read]:** a destroyed ground block is a HOLE in the Pistol's ground
>   (`PistolGround`; a stolen block still stands, a restored one is ground again): the ride rolls off its edge, flies
>   over it if high and fast enough, and lands only on ground; sunk `pEdgeGrace` 1.2 u under the line it has fallen in and
>   lands on nothing after (no phantom ground). The AI floats or dives to land on ground when it can. (The fall's first
>   answer, a vortex ring carrying it back to the den it set off from, was replaced the next day: below.)
> - "there is an unfun state where the best paths are no longer crossable. so lets have falling still restore the path up
>   to what wasn't touched if the trail was still there, but once crossing where the trail should be they teleport to the
>   next section of undestroyed trail, leaving the rest of the gap and loosing all energy. this way they can keep going and
>   they restore some trail to try again on the next pass. this also still rewards them for how far they make it before
>   failing without taking the thrill out of succeeding." **[stated]** (2026-10-29). **[read]:** the moment it falls in
>   (`Events.FellIn`), the gap's blocks it touched, from the gap's start through the one it fell into, are restored
>   (`PistolGround.AfterGap`; at least one a fall, so no pass is wasted), its energy is lost, and it comes out of a
>   vortex-ring portal (`fallPopSeconds` 0.5, spinning upright) a quarter block into the next standing ground beyond the
>   gap, riding on at the speed it had; a gap that runs to the perch sets it at the perch, where it arrives with no energy
>   and builds nothing. A jump over gap blocks also restores them as it passes (the skip reward). Measured headless on the
>   shipped ride: the AI clears a 300 u gap in 3-9 passes and a 500 u gap in 5-9; a rider sloppy 30 % of the time, every
>   gap up to 150 u in 3-20 passes, and every pass shrinks it. The AI's "leave the island it fell on" is retired: it
>   keeps going.
> - "every time prisms are not ridden because the pistol is off trail then grow those prisms. if they are max size (which
>   should only grow out of plane.) then shield them. if they are destroyed, restore them. this reward the player for
>   making big jumps. the more prisms are skipped the more energy the pistol should collect. the amount the pistol builds
>   when reaching the den is based on energy collected. energy is lost when falling through a hole. that is why it doesn't
>   build, because it has no energy. the energy should be shown on its mass icon. crystals are also attracted to the plane
>   so pistol can collect them. when choosing paths in the den, the pistol should see the plane it should show one side of
>   the trail when traveling one way and pass on the other side when traveling the other direction. the two different
>   half planes available should signal their mass separately in a more 3d effect that obeys the no flat hypersea rule."
>   **[stated]** (2026-10-29). **[read]:** a block is SKIPPED when the Pistol flies over it (in the air at least `pMinAir`,
>   above the ground's line, not fallen in: `Events.Flew`). Each skipped block grows ACROSS the trail only (its x, out of
>   the plane; `skipGrowStep` 0.8 u to `skipGrowMax` 8 u); one already that wide is shielded; a destroyed one is restored
>   (so a jump over a hole heals it). Energy: the nth block of a flight gives 1 + 0.2 n (`energyFlightBonus`), up to
>   `energyMax` 640; a den takes one prism per 8 energy (`energyPerDenPrism`, x Mass) and keeps the rest; a fall zeroes it;
>   a teleport keeps it. Measured headless on the shipped ride: the Tiny Wings rider (the AI) flies over ~65 % of an
>   island's blocks and builds ~23 prisms on 900 u and ~48 on 1800 u (the old distance rule's 22 / 45), a sloppy rider 4-7,
>   one that never flies none. The Mass card's new gauge is the energy (Pistol seat; empty flying the Goby). Crystals beside
>   the plane drift in within the undertow's reach (`crystalPullSpeed` 30) and the Pistol takes one it touches, as the
>   Jacks' net does, on the hull's machine only. The HALF PLANE: riding an island ahead the Pistol plays the side its frame
>   calls +z (the far side from its camera), riding it back the other side; the undertow pulls mass only from that half,
>   and the path map scores and draws each half on its own: a lit, terraced wing of faceted blocks standing out of the
>   trail on its side, wider and in more tiers (1-4) and bluer -> limer as it holds more opposing prisms; the picked half
>   white; the map opens tilted `mapStartTilt` 35 deg so the two halves part on screen; a tap on a wing picks that half.
>   **[open]:** whether the crystals should also come from that half only; whether a block at full width that is already
>   shielded should go further (super-shield).
> - The primer, heat and flares are RETIRED (they were the Goby's old kit). Charge has no level-5 upgrade yet **[open]**.
> - Controls: Goby Dart = pad RT / X, key Q, phone Dart button (hold, let go); Lock = pad LT / Y, key F, phone Lock.
>   Pistol (since 2026-10-24, on the fleet's four labelled face buttons so each card's chip names its control from the
>   one glyph set): dive A / Space (hold), Ghost B / T, Snap X / Q, Back Y / F; the old keys still work (G ghost, E Snap,
>   the B key and LB back); with `aimedJump` on, the keyboard jump is J (it was F, which is now Back). Phone: its own
>   Ghost, Snap and Back buttons.
> - Each seat has its OWN four cards (Garrett, 2026-10-24: "give both of its seats separate UI ability icons, glyphs,
>   etc."): `GobyHUDVariant` + `GobyPistolHUDView` / `GobyPistolHUDController`, written by `Tools/Build/author_goby_hud.py`
>   and swapped onto `Goby.prefab` by `author_goby_assets.py`. Flying the Goby: Charge **Lock** (Y / F, lit while it holds
>   a lock), Mass the **perches** it has made (a count; flashes on each perch), Space the **undertow's reach** (a gauge),
>   Time **Dart** (X / Q; the gauge is the held charge, the veil its cooldown). Flying the Pistol: Charge **Snap** (X / Q,
>   the veil its cooldown), Mass the **den prisms** (a count) with **Back** (Y / F, lit while heading back), Space
>   **Ghost** (B / T, the veil its cooldown; the same reach gauge), Time the **dive** (A / Space; the gauge is its speed).
>   The icons are 128 px placeholders in `Assets/_Graphics/Icons/AbilityIcons/Goby/` for the art pass to replace 1:1,
>   drawn on the motifs of the seats' phone buttons. Time's card on the Pistol stays the dive until Time's open question
>   (below) is answered.
> - The four cards: Charge **Vortex Sting**, Mass **Dens** (L5 Fortress), Space **Undertow** (L5 Deep Undertow), Time
>   **Quick Jump** (L5 Phase Dart). `Tools/Build/author_goby_assets.py` writes and checks them.
> - "when the shrimp gets to a new perch it should be able to pick another perch to teleport to. teleporting to different
>   perches does not grow them. only traveling the distance using the dive should grow the den. the pistol should be able
>   to travel either direction from a den as well. the view that allows the pistol to find a den and direction of
>   interest should show which paths have collected the most opposing prisms for the vortex ring to destroy." **[stated]**
>   (2026-10-25). **[read]:** after its rest at a perch the Pistol's view pulls back to the PATH MAP: every perch framed,
>   looking along the islands' planes. Each island glows along its ground as a lit faceted bar, thicker and from the
>   neutral blue toward the call-to-action lime as it holds more OPPOSING prisms (not the pair's domain, breakable, not
>   super-shielded: what the vortex ring can catch) over it, within the undertow's reach of its plane and `pathScoreBand`
>   60 u of its ground. The options are every den, either way it has an island (`PistolPaths`); the head's "ahead" waits
>   for the Goby's next island. The pick is white, with a pointer at its den along the way it rides. Controls in the 2D
>   cameras' priority (Garrett, 2026-10-25: "the control priority for 2d characters is mobile, gamepad, mouse/keyboard ...
>   the player needs to be able to spin the map"): **touch** (the mouse is the same pointer) drags to spin the map, taps a
>   path's glowing bar to pick it (starting from the den at the end nearer the tap), taps the picked path again to ride
>   it, and the phone's Back button turns it round; a finger still down from the ride, held still 0.5 s, rides on
>   (`MapPointer`). **Gamepad**: right stick spins, left stick moves the pick to the next path that way on screen, A (or LT)
>   rides it (a fresh press, or held through the perch), Y turns it round. **Keyboard**: Space rides, A / D move the pick,
>   F turns round, Q / E spin. The spin is kept from perch to perch. Picking another den TELEPORTS there and rides from it:
>   no rest, no den growth (only the distance ridden grows the den the Pistol next arrives at). The AI takes the island
>   with the most opposing prisms (ties: nearest end), and its old way when every island is empty. Scores are kept one
>   island per 0.1 s, every frame while the map is open.
> - The Pistol has its own MODEL (Garrett, 2026-10-24: "give the pistol a model"): `Pistol.fbx`, a pistol-shrimp ship
>   built by `/vessel-model` (`specs/pistol.py`, `Docs/VesselModels/Pistol.md`): 19 floating parts, the big hammer claw
>   on its right (the side the Pistol's camera sees going forward), four element morphs. The seat draws it where the
>   ride puts it, nose along its travel and tipped with the slope or its flight, `pistolModelLength` 2.7 u long, wearing
>   the pair's domain on slot 0; the hammer shuts on a Snap and re-cocks (`snapClawShut` 75 deg, hold 0.08 s, re-cock
>   0.35 s) and the tail curls under on a dive (`diveTailCurl` 22 deg). Shrinks to 0.6 while ghosting, as the capsule did.
> - The vortex ring is no longer a flat faceted torus (Garrett, 2026-10-24: "make the vortex ring loose the flat effect
>   and replace it with a cool warping effect like the black holes use but convey a more subtle toroid"): each ring is a
>   `VortexRingLens`, a full-screen URP pass beside the black hole's that bends the light behind the ring toward its
>   core circle like a clear glass rod, with a faint 0x9fe8ff rim, a slight chromatic split and a slow swirl
>   (`Docs/BLACK_HOLE.md` §5.3; knobs on `Resources/VortexRingLens.mat`). It swells in over 0.25 s and fades over its
>   last 0.5 s; the prisms it carries still flow round it. The Goby's lock stays a lit ring (a fact in the world).
> - Still the Squirrel's: the hull MODEL's rig (the Goby's own hull, `Goby.fbx`, is built on the Squirrel's bones), the nested skimmer prefab (its
>   Squirrel effects are stripped through the Goby's own containers; it still collects crystals), and an AI-flown Goby
>   that the seat does not run on (backfill, other machines) flies the platform AIPilot without the kit. Team tasks.

## The ability matrix: both seats, every platform (2026-10-25)

Garrett, 2026-10-25: "ability matrix should now include controls like that. table with all three platforms. please give
me an ability matrix for both seats." Columns run in the 2D characters' control priority: **mobile, gamepad,
mouse/keyboard**. The four cards are the HUD's row (`GobyPistolHUDController`); each seat draws them its own way. Keep this
table in step with `GobyPistolDuo` (`HumanPistol`, `Choose`), the hull's bindings (`author_goby_assets.py`
`GOBY_BINDINGS`) and the phone buttons (`GobyPistolButtons`) in every change that moves a control.

**The Goby seat** (the 3D flier that lays the islands and perches):

| Card | Ability | What it does, and how its element scales it | Level 5 | Mobile | Gamepad | Mouse / keyboard |
|---|---|---|---|---|---|---|
| Charge | **Lock** (feeds the Pistol's Vortex Sting) | Locks the best target in the nose's cone, rival vessels first, then fauna; while the lock is on the Pistol's screen its rings home on it. Charge raises the petals a ring hit takes (1 -> 3). | none yet **[open]** | Lock button | Y or LT | F |
| Mass | **Perches** (the Pistol's dens) | Passive: a dart's end becomes a perch once it is 900 u from the last one; the Pistol builds its den there. The card counts them and flashes on each. | (Fortress, on the Pistol's dens) | passive | passive | passive |
| Space | **Undertow** (the Pistol's) | Passive: mass beside the Pistol's plane is pulled into it; the card's gauge is the reach (40 -> 200 u with Space). | (Deep Undertow) | passive | passive | passive |
| Time | **Dart** | Hold to charge the distance (140 -> 600 u over 0.8 s), let go to dart; it stops on the first block on its line. The gauge is the charge, the veil the cooldown. | **Phase Dart**: darts pass through prisms | Dart button: hold, let go | X or RT: hold, let go | Q: hold, let go |
| (flight) | the hull's own flight | Cruises; perched, it is pinned and aims. | | two thumbs | both sticks | the desktop flight scheme |

**The Pistol seat** (the 2D sidescroller that rides the islands, Tiny Wings style):

| Card | Ability | What it does, and how its element scales it | Level 5 | Mobile | Gamepad | Mouse / keyboard |
|---|---|---|---|---|---|---|
| Charge | **Snap** -> **Vortex Sting** | Fires a vortex ring (the toroidal lens) that gathers prisms round its core; with the Goby's lock on screen it homes, and a rival hull it passes through loses petals from each element (1 -> 3 with Charge). The veil is its cooldown. | none yet **[open]** | Snap button | X | Q (or E) |
| Mass | **Dens**, and **Back** | At each perch it rests and builds a den from its ENERGY (2026-10-29): one prism per 8 energy, x1 -> x2.5 with Mass, at most 80; what the den cannot take is kept. Energy comes from the blocks it flies over instead of riding (the nth block of a flight gives 1 + 0.2 n, so big jumps pay); a fall through a hole loses it all. Teleporting grows none. The card counts the den prisms and its gauge is the energy; Back, pressed while riding, turns the next perch's pick round. | **Fortress**: danger and super-shielded prisms too | Back button | Y (or LB) | F (or the B key) |
| Space | **Ghost** (and the Undertow, passive) | Ghost: a moment of passing through prisms, then a cooldown (the veil). The Undertow pulls mass into the plane from the HALF it plays this way (one side of the trail riding ahead, the other riding back), 40 -> 200 u with Space, and crystals from either side, which the Pistol collects on touch; the card's gauge is that reach. | **Deep Undertow**: shielded prisms pulled too | Ghost button | B | T (or G) |
| Time | **the dive** (and the aimed jump, off) | Hold to press into the hill; let go on an upslope and it throws the Pistol. The gauge is its speed. With `aimedJump` on, a dragged jump whose cooldown Time shortens (9 -> 3 s); off since 2026-10-22, so Time's card on the Pistol is **[open]**. | (Phase Dart is the Goby's) | finger held on the view (aimed jump: drag) | A or LT (aimed jump: RT + right stick) | Space, S, down, or mouse held (aimed jump: J) |

**The path map** (the Pistol seat, at a perch after its rest; picking another den teleports there):

| Move | Mobile | Gamepad | Mouse / keyboard |
|---|---|---|---|
| Spin the map | drag | right stick | drag, or Q / E |
| Pick a path (from the den at the nearer end) | tap its glowing bar | left stick (the next path that way on screen) | click its bar, or A / D |
| Ride it (or teleport, then ride) | tap the picked path again | A or LT | Space (or S, down), or click the picked path |
| Turn it round at its den | tap the den's other path (or the Back button) | Y | F |
| Ride on without stopping | keep the finger down, still | keep A or LT held | keep Space held |

The phone's Dart, Lock, Snap, Ghost and Back buttons are drawn by the seat (`GobyPistolButtons`, round the right thumb).
Since 2026-10-25 `/vessel` §4.ac3 (LOCKED, Garrett) says a vessel draws no controls of its own: each becomes its card
(the follow-ups below).

**Both seats:**

| Move | Mobile | Gamepad | Mouse / keyboard |
|---|---|---|---|
| Swap seats | two-finger swipe, or double tap the small view | D-pad left / right | left / right arrows |

**Status (2026-10-18):** in the game as a prototype, not yet flown in the editor. The Goby is vessel class 18 (card
"Goby and Pistol"), flyable from the Toybox's Vessel Changer, built from the shared crew pieces (SKILL.md "One notion
of a crew vessel"); its numbers are cloned from the lab (`studio_to_unity.py --vessel goby`). The editor playtest is
team task `T-20261018-8b32`. The decisions log below holds every round. **The lab** (§9):
`Docs/Studios/VesselStudio/goby.html`. Garrett prototypes it first in its own private artifact,
https://claude.ai/artifact/6hGNJz2AKFYY5gHvYJa9Up (his call, 2026-10-14). It joins the one Vessel Studio artifact later,
by a merge into `Ys-bleeding-edge`. Read that artifact's `decisions` collection at the start of each round.

- The brief was given by Garrett on 2026-10-14 as "the Flier & Bird".
- In round 2 (the same day) Garrett answered the open questions and asked for a symbiotic pair of
  clades to name it.
- **The Goby** is the front half (the user's "Flier"). **The Pistol** is the back half (the user's
  "Bird", a pistol shrimp).

Each line below is tagged:
- **[stated]**: Garrett said it. Do not change it without asking.
- **[read]**: an interpretation. Confirm it before anyone builds on it.
- **[open]**: not decided. Ask; never fill it in yourself (`/vessel` §3, the design-approval gate).
- **[proposed]**: Claude's proposal, made because Garrett asked for one. It stands until Garrett
  changes it.

## 0. The clades: goby and pistol shrimp [proposed]

The **watchman goby** (Gobiidae) and the **pistol, or snapping, shrimp** (Alpheidae) are one of
the sea's best-known mutualisms. They share a burrow, and each does what the other cannot:

| In the reef | In the game |
|---|---|
| The shrimp is **nearly blind**. The goby is its **eyes** and stands lookout at the burrow mouth. | The Pistol has **limited visibility**, a narrow band around the Goby's path. The Goby has the full 3D world. |
| When out of the burrow, the shrimp **keeps an antenna on the goby** and goes where it goes. | The Pistol is **bound to the Goby's path** and **magnetically pulled to its trail**. |
| The shrimp **bulldozes** sand and rubble to dig and keep the burrow. | The Pistol **breaks through every block** except the Goby's trail. |
| The snapping claw fires a **cavitation bubble**: a flash, a ~200 dB crack, and a shock that stuns prey. | The Pistol **explodes itself**. Its explosions **reach off the plane** into the depth layers. |
| The goby **flicks its tail** to warn, and points the way. | The Goby **primes** targets for the Pistol and lays the path it rides. |

Why it fits the fleet:
- The halves are sea animals, like the Manta, Dolphin, Urchin, Serpent and Thresher, which
  suits the HyperSea.
- Neither name collides with a vessel class or a fauna species (checked 2026-10-14).

Runners-up:
- **Honeyguide and honey badger.** The bird leads, and the badger breaks the hive open. But the
  bird is the guide, which is the wrong way round for our pair.
- **Oxpecker and rhino.** Collides with the Rhino.

## 1. References Garrett gave

- **Tiny Wings** (https://www.youtube.com/watch?v=nn_s051gdjQ). The Pistol's sidescroller feel:
  dive onto a downslope to gain speed, and release on the upslope to fly **[read]**.
- **"Whistling Birds"** fireworks (https://www.youtube.com/watch?v=MZ-mwLBlcbc). The look and
  sound of the Goby's heat-seeking flares: many small whistling projectiles fanning out, each to
  its own target **[read]**.

## 2. Why the Pistol exists

**The Pistol gives people who don't want to play a 3D game a way to contribute to their team and
experience the world** **[stated]**. It is a sidescroller made from the 3D match. Any trade-off
that makes it feel like an ordinary sidescroller wins over strict accuracy to the 3D world
**[stated]**, including distorting or misrepresenting prisms (§4.3).

## 3. The pair at a glance

| | Goby (front, "Flier") | Pistol (back, "Bird") |
|---|---|---|
| Role | Lays the path, primes targets, finishes hot targets | Rides the path and destroys things |
| Space | Full 3D, a **two-thumb flier like the Squirrel** **[stated, 2026-10-14; was "like the Serpent"]** | A **warped 2D world** with parallax depth (§4) **[stated]** |
| Collision | **Ignores collision** **[stated]** | Breaks every block **except the Goby's trail**. Friendly fire is ON **[stated]** |
| Trail | The main trail. Its blocks alternate their angles, and those angles are the control points of a spline **[stated]** | A tiny trail **[stated]** |
| Input | **Two-touch** flight on mobile **[stated]** | Sidescroller controls. No yaw and no off-plane movement **[stated]** |
| Camera | The Serpent-style chase camera **[read]** | A **far side camera beside it**, sidescroller-style **[stated]** |
| AI priorities | 1. Get the crystal. 2. Hunt flora and fauna. 3. Lay a path for the Pistol's destruction **[stated]** | **[open]** |

## 4. The Pistol's world (the 2.5D model)

### 4.1 The plane

**[stated]** The Pistol is constrained to the plane you get by **unrolling the yaw** of the
**parametric ribbon** defined by the **y and z directions of the Goby's path**. That makes a
**warped 2D world**, and the Pistol lives in it.

**[read]** As geometry:
- Take the Goby's path. Let **s** be the distance along it (its z direction) and **h** be height
  (its y direction). The ribbon is the surface those two directions sweep as the path goes.
- **Unrolling the yaw** straightens every left/right turn of the path and keeps every climb and
  dive. The ribbon then lies flat as a plane, with **screen x = s** and **screen y = h**.
- So the Goby's climbs and dives become the **hills** of the Pistol's world. The spline through the
  alternating-angle trail blocks (§5) is the **ground line** the Pistol rides, Tiny Wings style.
- A world point maps to (s, h, d). Here **d** is its signed distance across the ribbon, toward the
  inside or outside of the turn, i.e. into or out of the screen.

**[open]** Points to settle in the lab:
1. **Which way is "up".** World up, or a slowly filtered "up" of the Goby? Space has no gravity,
   and the Goby can roll.
2. **Loops and vertical climbs.** The ribbon folds onto itself when the Goby goes straight up or
   loops, and a world point near a tight turn can map to two values of s. Pick a rule, e.g.
   clamp the climb rate or take the nearest s ahead of the Pistol.

### 4.2 Depth

**[stated]** The world has depth: **warped offset ribbons**, layered **in front of and behind**
the Pistol's plane with **parallax**, so it feels like an ordinary sidescroller. Each layer is the
same unrolled ribbon at a fixed d.

### 4.3 Prisms in the warped world

**[stated]** Prisms may be **distorted or misrepresented** to preserve the sidescroller reality.
**[read]**
- Each prism within the Pistol's vision is drawn at its (s, h, d) on the layer nearest its d. It
  is bent or re-oriented to sit in the unrolled world.
- **Hits are judged in the same warped coordinates the Pistol sees**, so what looks like a hit is
  a hit.

### 4.4 What the Pistol can and cannot do

- **No yaw, and no movement off the plane** **[stated]**.
- It has **effects that extend off the plane** **[stated]**. Its **body is a cylinder whose length
  reaches just past its vision range** across the depth axis **[stated]**, so it hits every layer
  it can see at its position. Its explosions reach across the layers too **[read]**.
- **Limited visibility** **[stated]**: it sees only blocks **within a set distance of the main
  trail** across the depth axis **[stated]**. It also sees primed blocks at reduced opacity at any
  distance (§5).

### 4.5 Who sees it

**Only the Pistol's pilot** sees the warped world **[stated]**: the spline is invisible to everyone
else. It is rendered on that client only (SKILL.md §2.4). In 3D, everyone else sees an ordinary
craft riding the trail.

## 5. The Goby

- **Flight:** a two-thumb flier like the **Squirrel** **[stated, 2026-10-14; it replaced "like the Serpent"]**. **Ignores collision**
  **[stated]**.
- **Speed from closeness:** the closer the Pistol is, the faster the Goby goes **[stated]**. A
  **speed gauge** on the HUD shows the change **[stated]**. **[open]:** the curve and its range.
- **Straight-line boost:** a boost that is active while flying straight. It is like the Rhino's
  full-speed-when-straight, but weaker **[stated]**. **[open]:** how much weaker.
- **Trail:** blocks are placed at **alternating angles** that define the control points of a
  **spline**. The spline exists only within range of the Pistol's screen **[stated]**.
- **Primer ("oil bomb")** **[stated]**: shoot it at things to prime them for an explosion by the
  Pistol.
  - Primed blocks are **visible to the Pistol at reduced opacity, even outside its vision range**.
  - The prime **decays after a delay**. The delay is long enough for the Pistol to try **one
    recovery**.
  - When a prime is **detonated**, things just outside the prime range become **hot**.
  - Generous charges, short cooldown.
- **Heat-seeking flares** **[stated]**: they destroy **hot** blocks. One volley fires **one flare
  per hot block in the area**.
  - **Ships and flora are hot by default**.
  - Charges on a longer cooldown.

## 6. The Pistol's abilities

- "Angry Birds abilities" **[stated]**: **explode itself, dive, etc.** **[stated]**.
- The row as read **[read]: confirm in the lab**:
  1. **Launch.** Set a destination in the plane and fly there. Mobile: tap and drag. Controller:
     hold the right trigger and aim with the right stick **[stated]**. **[read]:** the aim is
     2D, since there is no off-plane movement.
  2. **Explode.** The snap: the Pistol detonates itself, and the blast reaches across the depth
     layers **[stated]**.
  3. **Dive.** Drop hard onto the ground line, the Tiny Wings dive that builds speed on a
     downslope **[stated]**.
  4. **Ghost.** No block collision, used to pass friendly blocks without breaking them
     **[stated]**.
- **Crystal bonus (the Pistol takes it):** ability **cooldowns reset**, and its **next impact is a
  free explosion** **[stated]**.

## 7. Shared by the pair

- **Crystals restock both halves fully.** That covers every charge on both, whichever half took
  the crystal **[stated]**. The Pistol's own bonus (§6) comes on top when the Pistol takes it.
- **One element meter for the pair** **[stated]**. Both halves' rows read it.
  - **[open]:** the element → ability mappings, through `/vessel` §3.
  - **Trap:** `PilotSwap` notes that element levels are simulated on the machine that owns a hull
    and do not replicate. A meter shared across two hulls, possibly owned by two machines, needs
    one authoritative home (ANCHORS.md §3).
- **Swap:** the **same D-pad left/right gesture as the arena pilot swap**, unified with it
  **[stated]**. See SKILL.md §2.2.

## 8. Open questions, collected

1. Clade names: Goby & Pistol **[proposed]**. Confirm, or name the pair's class.
2. "Up" for the ribbon, and how loops and vertical climbs unroll (§4.1).
3. The Pistol's four abilities as listed in §6.
4. The Goby's speed-from-closeness curve, and the straight-boost ceiling against the Rhino's.
5. The prime decay delay (time it from the Pistol's recovery in the lab), and the hot ring's
   width and duration.
6. The element mappings for the one shared meter.
7. The Pistol's AI priorities.
8. What the Pistol does before the spline exists (spawning or respawning away from the trail),
   and whether one half dying downs the pair.

### 8.1 Follow-ups left by `cece/cool-shannon-aulgpe` (shipped 2026-10-25)

1. **The phone buttons become the cards** (`/vessel` §4.ac3, LOCKED 2026-10-25: a vessel draws no controls of its own).
   `GobyPistolButtons` predates the rule and still draws Dart, Lock, Snap, Ghost and Back. The per-seat cards exist
   (`GobyPistolHUDController`), so each seat's card taps become its controls; the Pistol's cards need their inputs
   BOUND on `Goby.prefab` (Button1 and Button2 are not today: `GOBY_BINDINGS` in `author_goby_assets.py`) and the seat
   must hear taps while its flight input is paused (`JacksExecutor.ListenToCards` is the pattern). Then the matrix's
   mobile column changes. Measure first: `grep -n "GobyPistolButtons" -r Assets/_Scripts`.
2. **The path map's cost on a long chain is unmeasured.** `ScorePaths` queries spheres every 1.5 x max(reach, band)
   along one island per frame while the map is open; `AiPlanRoute` queries one sphere of the island's length round the
   last perch every rethink. Profile both with 20+ perches in a dense cell before tuning.
3. **Time's card on the Pistol** stays the dive while the aimed jump is off (open since 2026-10-22).
4. **The Charge level-5 upgrade** on either seat (open).

## 9. The lab comes first [stated]

**Prototyping both halves together in a lab is essential to the first steps** **[stated]**. Build
it as a page in the ONE Vessel Studio (`/vessel-studio`, `/labmaker`) before any Unity work. The
lab must have:

- **Both views at once:** the Goby's 3D chase view and the Pistol's warped sidescroller, side by
  side or picture-in-picture. Then the unrolling (§4.1) can be judged as one thing seen two ways.
- **The ribbon model as a pure function:** world point to (s, h, d) and back, with a headless test
  over loops, tight turns and vertical climbs. This is the lab's scorecard for §4.1's open points.
- **The swap** on the arena keys (left/right, plus 1/2), with an AI flying whichever half you are
  not.
- **The Goby's trail spline** as the Pistol's ground, the dive and the launch, the primer, hot
  blocks and flares, and the speed-from-closeness gauge.
- **Parallax depth layers** and the Pistol's vision band, with prisms distorted into them.

### 9.1 Round 1 (2026-10-14): what the lab models and what it found

**Built:**
- both views at once: the Goby in 3D and the Pistol's sidescroller, as main + inset or split;
- the arena swap's keys (D-pad left / right, 1 / 2, 0.75 s), with an AI on the half you are not flying;
- the three unrolls of §4.1 as a Scene Config choice: **World up** (an exact vertical curtain), **Arc length**, and
  **Goby's up** (the ribbon twists with the Goby's roll);
- the ground: a Catmull-Rom curve through every Nth trail block (a hinge), alternately lifted and dropped;
- parallax depth layers, prisms drawn where the unroll puts them, and primed blocks shown faintly off the band;
- the Pistol: dive, launch (drag, or RT + right stick), the snap that reaches off the plane, ghost, the crystal bonus;
- the Goby: primer, heat, heat-seeking flares, the straight boost and the closeness speed, with a gauge for both.

Everything without a game number is lab-only and says so on the page. The Unroll test and the Duo runs are the
scorecard. Tables: `Docs/Studios/VesselStudio/README.md` § "Goby & Pistol, round 1".

**Readings the lab had to make** (all [read], confirm in the lab):
- How hinges make hills: every 4th block is a hinge, alternately ±26 u, and the hinges are the ground's control points.
- The Pistol cannot pass the Goby: there is no ground ahead of it yet. This is one answer to open question 8.
- Only the Pistol detonates a prime; a flare takes only its own block (see Found 5).

**Found:**
1. The **Goby's-up** unroll held best on six of seven flights, and the pair scored most in it (124 blocks in 60 s,
   against 88 for Arc length and 42 for World up).
2. **World up** folds on an ordinary weave: the Goby's turn radius (~54 u) is close to the 45 u vision band, so the
   inside edge of the band crosses itself. A third of the test points came back somewhere else.
3. **Every unroll fails on a corkscrew** (81-99% ambiguous): a helix stacks its passes. Loops fold too (15-35%).
   This is a design question for Garrett: limit the Goby's flight, or accept a lossy Pistol world on those paths.
4. The **pair beats the Goby alone** in every unroll (42 / 88 / 124 against 32).
5. The first "No Pistol" baseline scored chain kills, because the Goby's flares set off primes. This is fixed in the
   lab. Keep it in mind for the design too: a half that can cash the other half's payoff alone erases the duo.
6. The **closeness speed hurt** the pair in World up (42 with it, 119 without): the Pistol pushes the Goby past its
   targets.
7. The AI levels do not separate yet. Rookie scores level with Hard in World up, so the Hard AIs are not yet skilled.

## 10. Where each mechanic starts in code

See `../ANCHORS.md`. In short:

- **Spline:** `Trail.Project` (Catmull-Rom through block centres). The Pistol's warped world needs
  a new render path; nothing like it exists.
- **Riding:** `TrailFollower` / `BlockscapeFollower`.
- **Pistol friendly fire:** ramming has no team check, so it is already the default; the work is
  exempting the Goby's trail.
- **Ghost:** `UrchinSlipActionExecutor`.
- **Launch aim:** `ThresherLaunchSight` for the stick. Drag-to-aim on mobile does not exist.
- **Per-viewer visibility:** the Serpent cloak's `IsLocalUser` alpha.
- **Goby flight:** the Squirrel's dual-stick `VesselTransformer` (`Squirrel.prefab`), not the Serpent's `SingleStickVesselTransformer`. **Straight boost:** `RampBoostActionExecutor` +
  `StraightLineGesture`.
- **Pistol far camera:** the Serpent's `SerpentCameraSettingsSO.asset` is a chase cam, not a side
  cam. The Pistol needs a new camera mode.
- **Swap:** `PilotSwap` + `PilotSwapGesture`.
- **Primer, hot, flares, speed gauge, proximity speed, shared cooldowns, the shared meter, the
  warped render:** none exist. See ANCHORS.md §3.

### 10.1 The Unity clone (2026-10-15)

The lab's arithmetic is in the game, line for line, held to the lab's own numbers:

| Piece | Unity | Proof |
|---|---|---|
| The unrolled world: map / unmap, frames, the hinge ground (all three unrolls) | `PistolRibbon` (`Assets/_Scripts/Controller/Vessel/Crew/GobyPistol/`) | `GobyPistolMathTests` (goldens from `Tools/Build/goby_pistol_goldens.cjs`, tests written by `goby_pistol_goldens_to_tests.py`) |
| The ride, launch, ghost, Snap trigger, the body test | `PistolBodyMath` | same |
| The vortex ring (capture, toroidal flow, collapse) | `VortexRing` | same |
| The Pistol AI and its levels, `mulberry32` bit for bit | `PistolAI` | same |
| 40 numbers | `GobyPistolConfigSO` / `GobyPistolConfig.asset` | `studio_to_unity.py --vessel goby --check` |
| Playing it: the Pistol rides your hull side on, on real prisms; the Goby's primer and flares; the arena's swap | `GobyPistolDuo` (a runtime component; **FrogletTools ▸ Vessels ▸ Goby and Pistol (Play mode)** adds it to any hull you fly) | Amoebius, 2026-10-15: spawned, bound, swapped both ways, fired a ring (below) |
| **The vessel: `VesselClassType.Goby` (18; 16 until 2026-10-18)**, flyable from the freestyle Vessel Changer | `Goby.prefab` (a Squirrel clone carrying `GobyPistolDuo`; since 2026-10-18 it draws its own hull, `Goby.fbx`), `SO_Class_Goby`, the container / network / class-list / fusion entries, `ElementalAbilityMaps/Goby` (the Squirrel's map), `ToyVesselRoster.Default` | `Tools/Build/author_goby_assets.py --check` / `--self-test` |

**Still lab-only:** the Goby's straight and closeness boosts (they need the duo vessel's own speed executor; the harness
cannot change a borrowed hull's speed), flora counting as hot, the crystal restock and the free Snap (crystal pickup is
the hull's), the world's seeding, the hinge tilt, the Goby AI's weave (in the game the Goby's AI is the vessel's own) and
`pParallax` (declared in the lab, read nowhere). The duo vessel class itself (one seat, two hulls, architecture A or B,
networking) is §8's open question and not started.

**The Pistol's view (2026-10-15, Garrett: "the green under the trail is not needed; it should perceive its surroundings
much more effectively; if the Goby spins around a prism the prism should stretch out").** Built in Unity first
(`GobyPistolDuo`'s side view; `PistolWarp`, `PistolWorldMesh`, `PistolWorld.shader`), the lab's side view being brought to the
same rules: (a) no floor or curtain — the Goby's own trail IS the ground, block by block on the hinge hills, and is never a
target; (b) every prism in a depth window of −½·pParallax … 3·pParallax is drawn with 2.5D parallax on an orthographic camera
(pParallax, the lab's own key, finally read: a layer that deep draws at half size), fogged beyond the band the body spans and
rimmed inside it, see-through in the foreground, a fogged FORECAST past the Goby (the drawing mapper extrapolates the ribbon's
last segment); (c) the stretch is the unroll's honest image: a prism's eight corners are mapped through `MapNear` in a window
sized from the path's curvature (`CornerWindow`), its mesh deformed trilinearly inside the warped box, so a prism at distance r
inside a lap of radius R draws R/r times as long and one holding the centre spans the lap (`PistolWorldMathTests`); (d) feel:
squash and puff on landing, a shake on a hard one, pops on hits, a flash on launch, the collapse ring, primed/hot/carried prisms
pulsing in their own colour, the tiny trail, the Pistol leaning along its velocity with a lit cap and a glowing eye.

**What the Goby is today (2026-10-15; its own model since 2026-10-18).** One hull, one Player: the Goby is a Squirrel clone (flight, its four
abilities and HUD, camera, containers), so architecture A/B (§8) is still open and the Pistol is not a second
networked object: `GobyPistolDuo` simulates it on the LOCAL pilot's machine only, wakes only while a human flies the
hull (not on an AI or remote copy, not under the menu's autopilot), and its prism breaks are local. Fine for freestyle;
not for a match until the duo is networked. The Goby's own element row (primer, flares, the Pistol's four) is an OPEN
design slot: its map is the Squirrel's, unchanged.

**Seen in Amoebius** (the repo's engine, which compiles the game live): with `Menu_Main`'s menu hull set to the Goby,
it spawned carrying `GobyPistolDuo`, idle under the menu autopilot; the Vessel Changer listed "Goby · flying"; in
freestyle the duo bound (side camera parked over the flat world at y -40 222), key 2 handed the Pistol to the human
(the Goby's AI on, its input paused, the side view full screen), Q fired a vortex ring, key 1 handed the Goby back
(AI off, input live). Amoebius does not yet composite a second camera's viewport or run `OnGUI`, so the side view
and the HUD text are not visible there: an engine row, not a game bug. Unity is the place to SEE them.

## 11. Decisions log

Add a dated line each time an **[open]**, **[read]** or **[proposed]** item is settled, and update
the line it settled.

- 2026-10-14: brief recorded as "Flier & Bird".
- 2026-10-14, round 2, Garrett:
  - The far camera is the Bird's, beside it, sidescroller-style.
  - The Bird is constrained to the unrolled ribbon, with warped parallax layers, and prisms may
    distort. The goal: non-3D players contribute and experience the world.
  - The Bird has no yaw and no off-plane movement; only off-plane effects and limited visibility.
  - The Bird's abilities are "explode itself, dive, etc."
  - A crystal restocks both halves.
  - One element meter for the pair.
  - The swap is the arena's D-pad left/right, unified.
  - A lab with both halves together comes first.
  - Claude proposed the clades: Goby (Flier) and Pistol shrimp (Bird).
- 2026-10-14, Garrett: the Goby is a **two-thumb flier like the Squirrel**, not the Serpent (the Serpent flies on one stick
  in the game, so "flies like the Serpent" and "two-touch flier" disagreed). The lab now flies it on `Squirrel.prefab`'s
  numbers and the Squirrel studio's stick mix. The Pistol must be playable in the lab: press and hold to dive, drag to
  launch, Snap / Ghost buttons. Found: the Squirrel's turn radius (~29 u) is inside the 45 u vision band, so the band must
  narrow or the Goby must turn wider while it lays path (README § Goby & Pistol, round 1b).
- 2026-10-14, Garrett: render the Pistol's prisms and surroundings with as much rendering parity as the warp allows (the
  lab now draws its world with the Goby view's own meshes, materials, lights and sky, warping only transforms); the
  pistol shrimp **shoots a vortex ring that pulls prisms into a toroidal motion** [stated] (in the lab: Q / pad Y; it
  carries what it catches and breaks it when it collapses [read]); and the world needs enough to interact with (the lab
  seeds clusters ahead of the Goby). **[open]:** the Pistol's row now has five candidates (launch, snap / explode,
  vortex ring, dive, ghost) for four slots.
- 2026-10-15, Garrett: **drop the self-explosion; the vortex ring IS the Snap.** The Pistol's row is now launch,
  Snap (vortex ring), dive, ghost (the four, closing the five-for-four question). The crystal's "free explosion on impact"
  becomes a free Snap: a vortex ring fired on the next hit, with no cooldown **[read]**. Then: push it to Unity.
  Done the same day (§10.1): the lab's maths, numbers and AI are cloned and playable in Play mode through a dev-only
  harness on the local vessel; the duo vessel class is not.
- 2026-10-15, Garrett: the Pistol's view is crude — no green under the trail; perceive the surroundings much more
  effectively; a prism the Goby spins round should stretch out; make the shrimp a thoughtful, enjoyable experience to test in
  Unity. Built (§10.1 "The Pistol's view"); the lab's side view follows the same rules so the artifact stays the source.
- 2026-10-15, a design review of that view (three designs, three judges, synthesized) corrected it, in both the lab and
  Unity: the parallax is centred on the PISTOL (camera-centred, the end-on body's caps drew 62 u apart); the corner window
  uses asin; a prism holding a lap's centre is drawn as one deterministic BAR (the run of path it is beside, with hysteresis
  and a grow-in) instead of corner-mapped slivers; a tear guard draws a prism rigid when a corner lands on another pass; the
  world draws in two passes (solid with depth, glass far-to-near); the lab's own marks (violet primed 0xb98bff, orange hot
  0xff6a2a, light-blue held 0x9fe8ff) and block tint (lerp to white 0.35) are the source — Unity had invented amber/red; the
  ground LINE is back (domain colour, warm on downslopes, the dive Tiny Wings is about), the tiles rigid on it at their own
  depth; the deep background (d > 3·pVision) is rigid boxes; a contact dash under an airborne Pistol; the ghost cue; the AI's
  aim as the lab's line; the sky turns with the frame at the Pistol. **[read] for Garrett:** the brief's §4.4 [stated] band-only
  visibility is replaced by limited REACH with fogged awareness of depth (reverting = the depth window to the band, one
  constant); the bar's half-lap → whole-lap jump as the Goby tightens a lap is the geometry, softened; `tAngle` is not cloned
  until the Goby's real trail alternates its angles; `pViewFog` / `pForeAlpha` / `pDepthView` are OFFERED as lab keys (not
  shipped) should the fog, foreground opacity or depth reach want tuning.
- 2026-10-14, round 1 of the lab built (§9.1). Nothing decided yet: Garrett picks the unroll in the lab (its
  Decisions tab writes to the shared log).
- 2026-10-15, the view's first runtime pass and an adversarial audit (four lenses, three refuters per finding; the Unity
  clone, not the design): Amoebius's scripted flight caught `PistolRibbon.FrameAt` reading `X(k + 1)` on the one-sample
  ribbon of the first frames after the Goby spawns — the lab's `ribFrameAt` had the same hole (JS gave NaN) — so both now
  answer with the one sample's heading, else the world's forward; the re-run flew clean. The audit's confirmed fixes: a
  teleport/respawn keeps the Pistol's old `_tail` (one strip streaked across the new screen); the bars' START times ran
  through `Expire` every 2 s (every bar on screen re-grew from half length); `showSideView` off also hid the 3D rings and
  flares; the torus's `LookRotation` with an exactly-up axis; a sample gap of 0 laid forever; the GobyUp frame swung past
  the ribbon's end (both sides now hold the last sample's frame, the centre still extrapolates); a re-bind (menu round
  trip) kept the old clock's primer / flare deadlines. Refuted and left: a prism on a Read/Write-off FBX draws as a unit
  box (no shipped prism is one); the actors are emitted after the prisms, so a near field that filled the 60 000-vertex
  mesh would drop the Pistol's own body first (the shipped meshes stay well under it; a reservation is one line should a
  bigger prism ever ship). **[read] for Garrett:** none of these change what the lab shows; the artifact (v7) carries
  the two lab-side guards.
- 2026-10-16, Garrett: **"make the shrimp launch have a significant cooldown; make the goby zip around crazy fast so it
  is almost impossible to catch up with."** Decided in the lab (private prototype v8) and cloned: `pLaunchCharges` 2 → 1,
  `pLaunchCd` 4 → 9 s (one launch is a commitment); `gCruise` 60 → 160, now the Goby's OWN prefab number (mapped to
  `Goby.prefab DefaultThrottleScaler`; the generator writes and checks it from the lab) — the first way the Goby departs
  from "the Squirrel's numbers" [stated, 2026-10-14; the turn rates still are]; the closeness speed (§5) is in the game at
  last (`gCloseMax` 2.4, `gCloseRange` 320) along with the AI Goby's easing (`aiMaxGap` 260), both as `GobyMath`, the lab's
  `closeness` / `aiGapThrottle` factored into named functions and held by a golden. The straight boost stays lab-only.
  **[read] for Garrett:** with 160 u/s and x2.4 the human Goby outruns the Pistol outright (35 u/s on the ground, 280 at
  launch): the Pistol's game is the world the Goby lays, not the chase; the AI Goby still waits beyond 260 u.
- 2026-10-18, Garrett: **"render the shrimp view as true to the game as possible; err on the side of being too true to
  the game."** The view is no longer a stylised re-rendering: a real perspective camera (its off-axis frustum IS the
  parallax about the Pistol, `PistolWarp.Frustum`), every prism / trail block / the Goby's hull drawn with their OWN
  meshes and materials under the scene's lights and sky (`PistolTrueDraw`), only positions and orientations unrolled
  (`FlatTransform`, `TileTransform`), a lapped prism's own mesh deformed into the bar. Dropped: fog, glass, depth fades,
  tints, the custom prism look; the marks (primed / hot / held, the ghost cue) are rings; the ground line, the Pistol's
  tube and trail, the aim and the pops stay as unlit marks. One concession: a foreground prism that would cover the
  Pistol is not drawn. The lab's side view follows the same principles (its 3D view's prism material, lit, the same
  camera; `frustumOf` held by a golden). **[read] for Garrett:** the brief's §4.3 "may distort" and the review's fog /
  glass awareness-of-depth are superseded by "true": depth is perspective alone now; if the near field needs a hint of
  distance back, a depth-of-field or the game's own fog is the true way, not a tint.
- 2026-10-18, Garrett: **"notice there is a new wrasse and grouper multiplayer vessel. learn what you can from how it
  renders the environment with the dither effect, uses both names in its card, etc. unify what you can so we have a
  consistent notion of multiplayer vessels. and apply it here."** Merged bleeding-edge, where the Wrasse and Grouper
  (`cece/compassionate-babbage-gc13io`) had landed: the Goby moved to `VesselClassType` 18 (16 is the Brittle Star), its
  card reads **Goby and Pistol** (`SO_Vessel.CrewNames`), and the code moved to `Controller/Vessel/Crew/GobyPistol/`
  beside the Wrasse's seat. The two seats now share one notion (SKILL.md "One notion of a crew vessel"): `ICrewSeat`;
  `CrewSeat` (wake, swap, hand-over - the Goby now pauses the player's `InputController`, as the Kite did);
  `CrewPictureInPicture` (the Pistol's view moved from a lower-RIGHT inset to the Wrasse's lower-left square, and
  taking the Pistol now lifts the Goby's view into that square); `CrewViewRender` (the side camera's render measures
  prism depth from the Pistol's plane and re-aims the game's occlusion corridor at the Pistol, so a foreground prism
  in front of it DITHERS - the round-before's "skip it" concession is gone) [2026-10-21: the Wrasse half of this
  unification was withdrawn in the next bleeding-edge merge; the Wrasse joins at KITE_NAVIGATOR.md's work order,
  step 2]; draws for the side camera only
  (`RenderParams.camera`); `check_crew_vessels.py` and `CrewVesselTests`. The Wrasse's behaviour is unchanged (its
  code now calls the same pieces). **[read] for Garrett:** the Goby still shows the Squirrel's HUD while the Wrasse
  has one that reads its seat; a Goby HUD that reads the Pistol (launch charge, snap, ghost) is the next consistency
  step. The lab's inset is still lower right (the layout was decided in Unity, as the Wrasse's round 4 was).
- 2026-10-18, ship pass: **the vortex ring and the flares are lit faceted volumes** (nothing in the world looks flat,
  LOCKED the same day, `Docs/claude/SHADERS_AND_PERFORMANCE.md`). In the lab (v11 of the prototype) and in Unity: the
  ring is a faceted torus (tube 0.16 of its radius, 24 x 6 facets, `PistolWarp.VortexRingFacets`, held by
  `PistolViewMathTests.VortexRing_IsAFacetedVolume_NotAFlatSheet` with a flattening negative control), the flares lit
  1.5 u boxes, and in the Pistol's view the ring is drawn on the lit solid pass, thinning as it fades. **[ask] for
  Garrett:** the Pistol's view still draws its other marks flat (the primed / hot / held rings round prisms, the aim
  ring and line, the contact dot, the pops). They read as the view's own markings, like a HUD drawn into the world; say
  whether they stay flat (the rule's owner's-call exception, recorded here) or become volumes too.
- 2026-10-18, **the Goby's own hull** (`/vessel-model`, `specs/goby.py`, `Assets/_Models/Vessel Models/Goby.fbx`): a
  spaceship inspired by a goby, never the fish. The lookout eyes are twin sensor turrets on the head; the gill covers
  are louvred cheek scoops; the pectoral fins are fans of three framed domain plates with a thruster pod at each tip; a
  swept sensor sail; a pelvic stub wing carrying the rear nacelles; a docking clamp under the chest where the Pistol
  rides; a long banded body ending in a rounded caudal fan. It is built on the Squirrel's rig and names, so the
  Squirrel's takes still drive it (the tail chain swims the body) and `Goby.prefab` binds it by a guid swap alone; the
  cards are rendered from it (`render_cards.py Goby`). Sheets: `Docs/VesselModels/sheets/goby_after.jpg`,
  `goby_cards.jpg`. **[read] for Garrett:** fly it; the bar is that it reads as the Goby from the chase camera and
  nothing floats or tears in a turn. The crystal-hull-fusion bakes read stale until re-baked in the editor.
- 2026-10-18, **round 12 in the lab** (Garrett: "work better on mobile ... pulling back and flinging ... make the shrimp
  very small and zoom out ... lower amplitude and higher frequency wiggles ... down more toward the goby ... warping only
  where necessary and as invisible as possible ... at their most manageable when the shrimp encounters them"). The lab,
  v12 of the prototype:
  - **Slingshot** (finger, mouse and pad alike): pull back anywhere and let go; the fling is opposite the pull, as far
    as the pull is long (`pSlingPull` of the screen's short side is a full `pLaunchRange`), with a rubber band, a power
    pouch and the flight's dotted arc. Phone: no thumb-stick rings or Goby gauges while you fly the Pistol, a faint
    ring round the tiny Pistol, the board and gauges shrink on a short screen.
  - **Scale**: `pBodyR` 5 -> 2.5, `pViewWidth` 460 -> 1050; slower true speeds (pull 140 -> 95, dive x3.2 -> 2.6, launch
    280 -> 160, drag 0.04 -> 0.35): the Pistol averages 76-86 u/s against 134 before, and breaks more (169 vs 145 over five
    seeds). Wiggles: hinge every 2 blocks at 10 u (was 4 at 26), the Goby AI's weave 18 u at 0.55 Hz (was 50 at 0.18).
  - **Downhill toward the Goby** (`pDownTilt` 16 deg): gravity leans toward the Goby and the side camera ROLLS by the same
    angle, so the pull reads straight down and the world runs downhill to the Goby. Forces and camera only; launch,
    aim, AI and touch all use the same leaning pull (`gravDir`, `launchVel`, `scr` / `unscr`, `rollClip`).
  - **The Settled unroll** (`soft`, now the default; the old ones stay selectable): the ground's sharp vertical curves
    (tighter than `pSoftRadius` 80 u) are eased over `pSoftReach` 48 u as a gentle height shear, settling while the Goby
    lays the path and FROZEN behind the window and three hinges ahead of the Pistol; prisms are drawn rigid and bent
    only where the unroll would stretch them past 12 % (full by 35 %). Measured: no settled sample ever moves; the ground
    under the Pistol never moves (except the Goby still laying its last span, in every mode); drawn-bent prisms on a weave
    98.6 % -> 48 %. Sideways smoothing of the spine was tried and measured worse (labmaker L-STU-117).
  - **The Goby's real hull** in both views (`models/goby-model.js`, generated from `Goby.fbx` by
    `Tools/Build/vessel_model/lab_mesh.py`, `--check`), in the fleet's colour scheme.
  **Unity is behind the lab** until Garrett has played it: `studio_to_unity.py --vessel goby --check` reports the 10
  changed numbers as drift, and the tilt / roll, the slingshot, the settled unroll and the rigid blend are lab-only.
- 2026-10-21, **round 13 in the lab: the Pistol is a 2.5D character, nothing is warped** (Garrett: "we have developed
  some 2.5 d characters recently that don't need to distort the world at all. this character need only bring the
  sidescroller plane to the tangent plane of the pistol and the goby path, then move the camera around minimally so we
  dont jerk things about any more than needed"). The lab, v13 of the prototype, a new default unroll **Tangent plane**
  (`plane`; Settled and the others stay selectable to compare):
  - **[stated] No world distortion.** The Pistol lives in the real 3D world. Its sidescroller is a plane through it that
    holds the Goby's path where the Pistol is (the path's tangent lies in it), seen by a real perspective camera from
    the plane's side. Prisms are their true boxes; out of the plane their faces part and fade (`pDepthSpread` 160 u),
    the game's `PrismDepthPlane` look that the Wrasse's kite and the Starlings use.
  - **[read] "Move the camera minimally"**, as three rules. The plane's normal is the path's own, carried along it
    without twisting, so bends within the plane are real hills and dips and the camera only yaws as far as the path
    leaves the plane (eased over `pPlaneEase` 0.12 s). "Along" follows the path's run round the Pistol (`pChord` 140 u,
    each tangent weighted by how much of it lies in the plane), eased slowly (`pRollEase` 0.9 s). The one fast roll is
    the needed one: past `pMaxLean` 40 deg the path under the Pistol would be a wall, so the camera rolls just enough.
  - **The Pistol's physics stay the 2D ones, in the plane** (down is the plane's down, leaning `pDownTilt` toward the
    Goby). The frame turns about the Pistol and carries its velocity; the plane's depth is pinned to the trail under the
    Pistol (it moves the plane, never the world). The ground is the real trail: hinges are lifted IN 3D along the path's
    up, and the Pistol rides their projection, exact where the plane touches the path. Where the projection folds, the
    ground is the stretch nearest the Pistol along the path; a step in it is a ledge the Pistol runs off, and a Pistol
    under its line is pulled back up by the same magnet.
  - **Measured** (60 s duo runs, Hard / Hard, intensity 2, seeds 11 / 22 / 33, against Settled): grounded time on ground
    steeper than 60 deg 7 / 5 / 9 % (Settled 29 / 53 / 17 %); breaks 188 / 286 / 202 (Settled 136 / 159 / 243); the
    grounded Pistol is within 15 u of its trail point (77 u once, after a fold). The camera turns 24-54 deg/s on
    average, mostly yaw (19-39 deg/s, the plane following the path out of plane, which tangency requires); roll 5-15.
    The Unroll test's new columns give the same per scripted flight. Every golden of the other unrolls is unchanged
    (`goby_pistol_goldens.cjs` before and after differ only by the five new SHIPPED keys).
  - **[open] for Garrett:** play v13. Is the yaw on a turning path acceptable, or should the plane lag more and let the
    path run into depth? Is the 1050 u view with the parallax camera right for a real-world view, or should it be
    orthographic like the Wrasse's kite? The trail's fine zigzag (round 12's hills) now reads as real 3D hills; keep it?
  **Unity is further behind**: the tangent plane, its five numbers (`--unmapped`) and the depth look are lab-only until
  Garrett has played it. Follow-ups that hang on his verdict: (1) the clone regenerates `GobyPistolMathTests.cs` from
  the lab's goldens (`goby_pistol_goldens.cjs` then `goby_pistol_goldens_to_tests.py`); today it encodes v11's numbers
  and a fresh generation differs only by round 12's tuned values; (2) if the tangent plane is kept, the unroll and warp
  machinery (lab: `ribMap*`, `classify`, `warpBox`, the Settled unroll; Unity: `PistolRibbon`'s maps, `PistolWarp`) has
  no reader left and can be retired in the same clone.
- 2026-10-21, **the Pistol's marks go into the world** (v14 of the prototype). Garrett's LOCKED rule of the same day
  ("always go for the 3d in world solution over the 2d flat text solution", `Docs/claude/SHADERS_AND_PERFORMANCE.md`)
  answers T-20261018-7c2d: the rings round primed, hot and held prisms and the ghost cue are now tints and pulses on the
  prisms themselves; an aimed fling lights the prisms it will break (its landing ring is gone; the dotted arc stays as
  the aim mark); a falling Pistol lights the trail tile under it (the contact dot is gone); flares are small lit
  volumes. **Unity still draws the rings** (`PistolWarp`, `PistolWorldMesh`) until the clone ports this.
- 2026-10-21, Garrett: "i can not switch to the pistol on mobile". The Unity duo read only the D-pad and keys 1 / 2
  (`CrewSeat.PollSwap`), which a phone has neither of, and the Pistol's own pointer read `Mouse.current`, which a phone
  does not have. Now a **double tap on the small view swaps**, as on the Wrasse, the Marlin and the Honeyguide (one
  shared detector, `CrewSmallViewDoubleTap` in `CrewViews.cs`, the Wrasse's numbers), on the arena cooldown; the Pistol
  reads a mouse or a finger, and a press in the small view is the swap's, never a dive. The lab's ⇄ Swap
  button stays the lab's stand-in. **[read] for Garrett:** on a phone, double tap the Pistol's square to fly it, double
  tap the Goby's square to come back; hold to dive and drag to launch on the Pistol.
- 2026-10-21, Garrett: "these should made consistent with other vessels. each seat has its own ui and controls. no need
  to use the dpad in a weird way". The Goby's primer and flares left keys 3 / 4 and the D-pad's up / down: they are the
  hull's own inputs now, bound like every hull ability (`CrewButtonActionSO`; Button3Action = pad X / key Q the primer,
  Button4Action = pad Y / key F the flares; `author_goby_assets.py` binds and checks them). On a phone each seat shows
  its own two buttons round the right thumb, only while the person is in it, as the Orca's gunners do
  (`GobyPistolButtons`): the Goby's Primer and Flares with their charges (they press the pilot's input, so the phone
  takes the same path as the pad), the Pistol's Ghost (held) and Snap, dimmed while cooling (they press the seat
  directly: the AI flies the hull meanwhile). The Pistol's finger is tracked by touch id, so a thumb on Ghost never
  steals the drag that launches. The Goby gets a `TouchControlProfiles` row (the Squirrel's lift glyphs). Icons:
  `touch_glyph_art.py` (TouchIcon_Primer / _Flares / _Ghost / _Snap). **[open], unchanged:** the duo's element row
  (§8 q6): the primer and flares are buttons, not cards, until the element mappings are decided. With a pad the buttons
  hide (the Orca's rule), so a pad shows no primer / flare count yet.
- 2026-10-22, Garrett: the Goby's own kit, straight in Unity (the summary at the top of this file). Unity:
  `GobyPistolDuo` rewritten (dart / perch / lock, the Goby's seat AI, the Pistol perch to perch on a flat plane, dens,
  the undertow, homing rings scored as PvP petal hits), `PistolPlane` and `GobyDen` new and pure, `PistolBodyMath`,
  `PistolAI` and `VortexRing` moved onto the flat frame; `PistolRibbon`, `PistolWarp`, `PistolWorldMesh`,
  `PistolTrueDraw`, `GobyMath`, `PistolTargets`, `PistolWorld.shader` and their golden tests retired with the
  unroll. `Goby.prefab` loses the Squirrel's tube and drift executors, its thumb-lift bindings and its impact
  containers (`GobyImpactorDataContainer`, `GobySkimmerImpactorDataContainer`); `GobyPistolTests` holds the rules.
  **[read] for Garrett:** fly it on a phone and a pad: perch, aim, dart (short tap vs full charge), dart into space,
  watch the Pistol ride down, rest and build, press Back to an old den, lock a rival and Snap when it is on the
  Pistol's screen.
- 2026-10-22, Garrett: "the pistol view needs updating. it was getting pretty fun, but with the recent change its camera
  is too far away, the number of wiggles in the path is too small. its needs a pass that preserves what is fun about the
  mechanics we are after and brings them back into the progress that we have made since they were tuned for fun."
  **[read]:** "tuned for fun" is the lab's rounds 12-13 (the ride numbers the kit already carries), against which the
  kit's flat plane lost two things. Measured:
  - **The camera.** The lab's and the first Unity side view's camera sat `pParallax` 160 u off the plane with an off-axis
    lens (`frustumOf` / `PistolWarp.Frustum`), so the world either side of the plane read in depth. The kit's sat
    `viewWidth / (2 tan(fov/2) aspect)` away at `viewFov` 40: 961 u in the small square, 541 u full screen on 16:9.
    Now `viewDistance` 160 again (replaces `viewFov`), the camera BESIDE the Pistol and the lens looking ahead
    (`viewPistolAt` -0.2: the Pistol a fifth of the width left of centre, the lab's), so the Pistol sits on the lens's
    axis and is never stretched; its height eases between the Pistol and the ground ahead (`PistolPlane.OffAxis`,
    `GobyPistolDuo.LateUpdate`). `viewWidth` 700 is kept.
  - **The wiggles.** The lab's ground had two scales: the fine zigzag of the alternating hinges (every second 16 u
    block, ±10 u: 64 u) and the rolls the Goby AI's height weave laid (18 u at 0.55 Hz behind a 160 u/s Goby: ~290 u),
    on top of the Goby's own climbs and dives. A dart is straight, so the kit kept only one wave (10 u at 70 u) and
    faded it to nothing over the dart (`sin(pi s / L)`), so the hills only stood up mid-dart and peaked at 10 u. Now
    both scales are laid along the Goby's up, each a whole number of half-waves per dart (`PistolPlane.HalfWaves`),
    so neither needs a fade and both meet each perch: a 380 u dart has 6 crests and hills to ~27 u (was 10); a dart so
    short that the hills would climb steeper than `MaxHillSlope` 1.5 gets lower ones, so the ground never folds
    (`slopeDegrees` is capped at 33 for the same reason). The ground's inverse is Newton's method now (exact to
    1e-4 u on every dart length measured, 6 to 600 u).
  - Kept as the kit has them: the ride (round 12's numbers), the 22 deg downhill, the dens, the undertow, the rings.
  `GobyPistolTests`: `Ground_KeepsTheLabsWiggles`, `Ground_NeverFolds_EvenOnAShortDart`,
  `SideLens_LooksAheadOfTheCameraBeside`. **[read] for Garrett:** fly the Pistol full screen and in the small square:
  is 160 u close enough (the lens is wide: 700 u across at 160 u is ~130 deg in the square), and do the rolls give a
  dive and a release on every dart?
- 2026-10-22, Garrett, on that pass: "we should zoom in, make the shrimp smaller and make more wiggles along the path.
  it should have a 15 to 30 second journey from perch to perch. it should not launch from the perch each time, and
  launching should only get big enough to clear a few humps that are a small fraction of the journey. launching
  intentionally was wierd." Built (`GobyPistolConfig`):
  - **Shrimp scale.** The ride's physics has no scale of its own (lengths and gravity scale together), so the Pistol's
    world is shrunk round the same darts: humps 10 u long, ±1.6 u (were 64 u, ±10), on rolls 60 u, ±3.5 u (were 290,
    ±18); trail blocks every 3 u, 1.4 x 0.4 x 2.8 (were 10 u, 4 x 0.9 x 9); the shrimp `pBodyR` 0.5 (was 2.5); the
    view `viewWidth` 120 u across (was 700), about twelve humps.
  - **The journey.** Tuned in a headless port of `PistolBodyMath.Step` against the Pistol AI's Tiny Wings rider:
    `pGravity` 30, `pDiveMul` 2.2, `pDrag` 0.45, `pMinSpeed` 4 (were 95 / 2.6 / 0.35 / 22). A rider who dives on the
    downslopes crosses 300 / 450 / 600 u in 16.5 / 24 / 30 s; one who never dives takes 20 / 27 / 37 s; the kit's
    first numbers took 4-9 s. **[read]:** a journey is 15-30 s only for darts of 300-600 u, so `dartMinRange` is
    300 (was 140: a tap now darts 300 u) and the Goby AI darts 450 (was 380) and waits up to 25 s for the Pistol
    (was 7).
  - **No launch off the perch.** Each stretch starts from a standstill at `pMinSpeed`; the Pistol used to carry its
    arrival speed into the first hump, and the Pistol AI aimed its jump at the den it had just built.
  - **Short flights.** Flights come only off the humps (let go of the dive on an upslope): ~5-9 a stretch, the longest
    ~29 u, three humps and 5-10 % of the journey.
  - **The aimed jump is off** (`aimedJump` false: no drag-to-aim, no RT + stick, no F, and the AI does not jump). With
    it on it reaches `pLaunchRange` 35 u (was 220). **[open] for Garrett:** Time's base card ("Quick Jump", the jump's
    cooldown) has nothing to drive; say what Time does on the Pistol, or turn the jump back on.
  - **[read] for Garrett, side effects:** the homing ring fires only when the locked rival is on the Pistol's screen,
    which is now 120 u across, so it homes far less often; and a 600 u dart lays 200 trail prisms (was 60). The class
    card's description ("flies the Goby ... on two thumbs", "launching into clusters") predates the kit and is stale.
  `GobyPistolTests.Ride_IsAFifteenToThirtySecondJourney_WithShortFlights` rides the SHIPPED asset over the shortest and
  the longest dart (12-32 s, longest flight under 15 % of the journey and six humps; control: the kit's first numbers
  under 12 s).
- 2026-10-22, Garrett: "the reference for the pistol is the game tiny wings. Tune the camera etc. to get us closer to that
  gameplay for the base movement of the pistol." Measured first, in a headless port of the ride (`PistolBodyMath.Step`)
  with Tiny Wings' loop as the scorecard (dive the slope, let go in the valley, the crest throws it, land on a back
  slope to slide faster, build speed over a run):
  - **The ride never flew.** Its liftoff compared one frame's free fall with the ground (`yb > yg + 0.05`), so it
    depended on the frame: at 120 steps a second the shrimp never left the ground, at 30 and 60 it left at random
    points. The same rider took 16.5 s at 60 fps and 19.9 s at 30. And **no landing was ever smooth** (0 %): it fell
    steeply onto the fronts of humps, so every flight cost 45 % of its speed. Tiny Wings' reward never happened.
  - **The physics now (`PistolBodyMath.Ride`).** A crest throws the Pistol when its speed needs more pull round the
    curve than gravity gives (v^2 times the curvature > g cos), so holding the dive keeps it down and letting go over a
    crest flies it. Every frame is ridden in 1/120 s steps (`Substep`): a never-diving and an always-diving rider are
    identical at 30 and 60 fps. A hop shorter than `pMinAir` 0.25 s is a skip (keeps its speed, earns nothing): before
    that, frame-long hops each counted as a smooth landing's x1.04.
  - **The hills (`PistolPlane.Hills`).** One train of smooth hills, heights swelling and falling, replaces rolls plus a
    fine zigzag (which left no slope a landing could be smooth on). The lean is 6 deg (was 22): Tiny Wings' hills are
    level, so speed is the rider's to build, and 22 deg of free speed drowned it.
  - **The ride's numbers**, chosen by sweeping lean, hill size, gravity, dive and drag against the scorecard and then
    checking 30 and 60 fps: `pGravity` 18, `pDiveMul` 4 (Tiny Wings' heavy dive), `pDrag` 0.15. Measured in the C#
    (300 / 600 u darts, 60 fps): the rider who dives the slopes and lets go up them takes 11.4 / 24.6 s, flies 11 / 22
    times, lands 10 / 17 of them as smooth slides, and its longest flight is ~36 u (two hills, 6-12 % of the journey);
    one who never dives takes 29.7 / 56.9 s and never flies; one who never lets go takes 17.3 / 34.2 s and flies 2 / 5.
  - **The Pistol AI** plays it the same way (`PistolAI.TinyWingsDive`): dive down slopes, let go up them, and in the
    air dive only when that lands it at a smaller angle (the back of a hill). Its old "dive when high" rule
    (`aiHighDive`) is gone.
  - **The camera (`PistolPlane.TinyWingsView`).** Tiny Wings frames the ground, not the bird: the ground under the
    Pistol sits `viewGroundedAt` 0.35 up the screen, the Pistol `viewPistolAt` a fifth left of centre (a third in);
    the view widens by `viewSpeedWiden` 1 u per u/s, and when the Pistol flies high it zooms out to keep `viewHeadroom`
    0.15 above it with the ground still on screen, up to `viewMaxWidth` 240 (then it rises with it). Out quickly, back
    in slowly. The camera pulls back with the zoom (`viewDistance` 160 at the rest width of 80), so the lens never
    changes (28 deg across: nearly flat, as Tiny Wings is 2D).
  - Kept: the shrimp (`pBodyR` 0.5), `dartMinRange` 300, no aimed jump, no launch off the perch.
  `GobyPistolTests`: `Ride_IsTinyWings_DiveLetGoFlySlide` (with the never-dive and never-let-go controls),
  `Ride_IsTheSame_AtThirtyAndSixtyFps`, `TinyWingsView_KeepsTheGroundOnScreen_AndZoomsOutToFly`,
  `Ground_IsATrainOfTinyWingsHills` (the control: no swell, hills alike). Their logic was run in .NET against the
  three pure files (all pass); the editor run is T-20261022-bc36. **[read] for Garrett:** the never-diving rider takes
  ~57 s on a full dart, Tiny Wings' penalty for not playing; at 30 fps a skilled run's longest flight reached 81 u once
  (4.5 hills, 14 % of the journey).
- 2026-10-23, Garrett: "it should feel much closer to tiny wings in relative scale of piston and hill height, apparent
  speed, length of level compared to perch distance. it doesn't feel there or even close in its tunings." The third pass
  fixed Tiny Wings' LOOP but not its PROPORTIONS. Measured in the Pistol's length (d), as Tiny Wings' are in the bird's:
  the screen showed 4.4 hills with the Pistol 1/80 of it, so the Pistol looked about 5x too small, the hills read as
  wiggles and the ride as slow (0.3 screens a second); a hill was only 3.6 d tall; a stretch was 17-33 hills.
  - **Tiny Wings' proportions, the targets** (**[read]**: Claude's estimates from the game, not measured from footage;
    correct any of them and the next pass re-tunes to it): a hill about 15 d crest to crest and 3-5 d tall (taller on
    later islands); the resting screen a little over one hill wide, the bird about 1/15-1/20 of its width, a quarter to
    a third in, the ground in the lower third; an island about 12-20 hills, crossed at about a hill a second (about a
    resting screen a second); a good flight clears 1-3 hills and peaks 1-3 hill heights up; the view zooms out about 4x
    in flight.
  - **Shipped** (`GobyPistolConfig`): `hillWavelength` 26, `hillHeight` 3.5 (a hill 15.3 d long, 4.1 d tall at the
    tallest; `swellWavelength` 156, six hills); `pBodyR` 0.85 (d = 1.7 u); `slopeDegrees` 5; `pGravity` 14, `pDiveMul` 4,
    `pDrag` 0.03; the view `viewWidth` 30 (1.15 hills, the Pistol 1/18 of it), `viewMaxWidth` 120, `viewSpeedWiden` 0.6,
    `viewDistance` 60 at rest (the same 28 deg lens). Chosen by a sweep of hill size, lean, gravity, dive and drag against
    those targets with the expert rider and both controls.
  - **Measured in the C#** (60 fps): a 300 u stretch is 11.5 hills, played in 13.8 s (0.83 hills/s), 6 flights, 4 of them
    smooth slides, the longest 59 u (2.3 hills); a 600 u stretch is 23 hills in 24.8 s (0.93 hills/s), 12 flights, 8
    smooth, the longest 64 u; at 30 fps 14.6 s and 25.3 s. A rider who never dives takes 29.3 / 57.1 s (1 smooth slide);
    one who never lets go 18.4 / 33.6 s (0 / 2). On screen the ride runs at about 0.9 resting screens a second (was 0.3).
  - **[read] for Garrett:** with taller hills a flight is about one in two hills, and the longest is ~20 % of the shortest
    (300 u) stretch: "a few humps" now means a few of Tiny Wings' hills, each about a twelfth of the journey. Flights
    peak about one hill height up (two at most): higher, Tiny Wings-at-full-speed flights need more speed than one
    stretch builds.
  `GobyPistolTests.Proportions_AreTinyWings` pins the ratios (control: the third pass's 18 u hills under an 80 u
  screen fail); `Ride_IsTinyWings_DiveLetGoFlySlide` now asks for a flight at least every three hills, flights under
  three hills and the controls sliding at most half as often. Logic run in .NET against the three pure files: all pass.
- 2026-10-23, Garrett: "the tiny wings character gets really floaty and can launch over many hills after just riding a
  few. it gets to quicker speeds launches higher falls slower. you gotta get a handle on their physics which is very
  forgiving and lets you ride about 3 times longer before the perch/den." The previous ride was the opposite: as heavy in
  the air as on the ground, a hard landing kept 55 % of the speed, drag held the speed flat, and a stretch was 11-23
  hills. The physics now (`PistolBodyMath.Ride`, four new numbers):
  - **Floaty** (`pAirGravity` 0.45): gravity in the air while the Pistol lets go is 0.45 of the slide's, so it launches
    higher and falls slower; the dive still pulls it down at x4. A crest throws it when its speed beats the AIR's
    gravity round the curve, so a floatier Pistol leaves crests sooner.
  - **Builds speed** (`pSmoothBoost` 1.16 per smooth landing, `pDrag` 0.01, `pMaxSpeed` 120, `pMinSpeed` 9): chained
    slides keep adding speed, so after a few hills it flies over many.
  - **Forgiving** (`pGoodLand` 65: a landing within 65 deg of the slope is smooth; `pHardKeep` 0.95: a hard one keeps
    95 % of its speed along the slope).
  - **3x longer** (`dartMinRange` 900, `dartRange` 1800, `aiDartLength` 1350; was 300 / 600 / 450): the Pistol's ground
    is the Goby's dart, so the darts are what got longer. Kept with it: `aiHomeRadius` 4000 (was 1600, so the Goby AI's
    1350 u darts have room), `aiWaitForPistol` 45, a trail block every 6 u (5.5 u long; was 3) so a full dart lays 300
    prisms, not 600. **[read] for Garrett:** a full dart now covers 1800 u in `dartSeconds` 1 - the Goby's own feel
    changed with it; and the island starts on SMALL hills by each perch, rising to tall ones between (`hillSwell` now
    lowers the hills at the perches), so nothing flies off the first crest.
  - The view zooms out to `viewMaxWidth` 180 (was 120) for the higher flights.
  - Chosen by a C# sweep (gravity, dive, float, boost, landing window and keep, floor and top speed, drag) over four
    dart lengths and three seeds, scored on: the first big flight after a few hills, flights over many hills, speed
    building, flight height, smooth landings, a sloppy rider (wrong a third of the time) not much slower, and a
    never-diver clearly slower. **Measured in the C#** (60 fps): 900 u (35 hills) in 20.2 s, the first 3-hill flight at
    hill 2.3, the longest 5.4 hills, speed 32 -> 92 u/s, 10 of 10 landings smooth; 1800 u (69 hills) in 30.4 s, first big
    flight at hill 4.1, the longest 9.4 hills, 58 -> 83 u/s, 12 of 12 smooth. The sloppy rider takes 20.0 / 44.9 s, the
    never-diver 61.9 / 60.5 s. At 30 fps one 1800 u run threw a 20-hill flight. About 1.7-2.3 hills a second.
  `GobyPistolTests.Ride_IsTinyWings_FloatyForgivingAndBuilding` replaces the dive-let-go test (the first big flight
  after 1.5 hills and within the first third, a flight of at least 4 hills, speed building x1.25+, 75 % smooth, the
  sloppy rider under x1.6, the never-diver over x1.4); `Proportions_AreTinyWings` now asks for 30-75 hills a stretch at
  1.2-3 hills a second. Logic run in .NET against the three pure files: all pass.
- 2026-10-23, Garrett: "ok now take a pass at tuning the two together. the pistol is a bit overtuned now but much closer.
  the goby is barely cruising around at all. I coudn't get the goby to work and the AI did only a little better. there
  is a fun pair here but they both need a lot of work lets start by tuning their movement". Measured first:
  - **The Goby never cruised.** The kit pinned it always ([read] above), so it moved only during a dart, and a dart
    stops on the first prism within 9 u of its line - in a cell of flora usually a few dozen u out. With the 900-1800 u
    darts of the floaty pass that meant short islands and a Goby that hardly moved. Its AI aimed one long dart, then waited
    up to 45 s (`aiWaitForPistol`) for the Pistol, which rides an island in 20-35 s.
  - **The Goby now cruises** (`GobyPistolDuo`, `GobyIslands`): pinned only while perched or darting; a person flies it on
    the hull's own two-thumb flight (`DefaultThrottleScaler` 160), the seat's AI cruises at `aiCruiseSpeed` 100, turning
    at `aiTurnRate` 90 deg/s and re-choosing its heading every `aiRethinkSeconds` 2.5 (at a locked rival in reach, back
    toward the last perch while the Pistol's islands are full, home when it strays, else a wander off the nose). The dart
    is the Goby's burst again (140-600 u, `aiDartLength` 300); its end is a perch only when the island would be
    `islandMinLength` 900 or more and fewer than `islandQueue` 3 islands wait for the Pistol; a gap longer than
    `islandMaxLength` 1800 is laid as several islands, each ending in a perch of its own. Perched it aims for
    `perchSeconds` 1.5, then lifts off. `aiWaitForPistol` is retired.
  - **Together:** the wandering AI (simulated over 200 seeds: net progress median 0.88 of what it flies, one run in ten
    0.56) makes a 1350 u island in about 22 s; the Pistol AI rides one in about 33 s. So the Pistol never waits at the
    head perch, and the queue keeps the Goby from running away: it circles the last perch until the Pistol catches up.
  - **The Pistol, a notch back** ("a bit overtuned"): `pAirGravity` 0.45 -> 0.5, `pSmoothBoost` 1.16 -> 1.12,
    `pMaxSpeed` 120 -> 100; the forgiveness (`pGoodLand` 65, `pHardKeep` 0.95) kept, since a version without it made a
    sloppy rider twice as slow on a long island. Measured: 900 u in 19.3 s (19.3 s at 30 fps too), first big flight at hill
    8, longest 6.5 hills, speed 41 -> 76 u/s; 1800 u in 33.6 s, longest 7.3 hills; sloppy rider x1.3 / x1.2 (averaged over
    four phases: one frame's shift moved a single run by half again), never-diver x2.7 / x2.2.
  - **[read] for Garrett:** a person's Goby has no in-world cue yet for "this dart will perch" (900 u from the last perch,
    queue not full): a dart that falls short just dashes. Say if it wants one (the LOCKED world-not-screen rule: a light or
    shape in the world).
  `GobyPistolTests`: `Islands_AreLaidAtTheirLength_AndKeepThePairTogether`, `Pair_TheGobyKeepsAheadOfThePistol` (control:
  the waiting Goby), the ride tests on island lengths with the sloppy rider over four phases. Logic run in .NET against
  the pure files: all pass.
- 2026-10-24, Garrett: "i feel like you could really make this class work but you need to understand tiny wings better,
  and really put the effort into making the pistol feel great and satisfying. it should be properly scaled, have the
  right sinusoidal curve, speed, accelerations, camera, everything. find the other games in the genre. what works and
  what doesn't. make a really fun to play pistol and goby." The four passes before tuned from memory; this one from the
  game. **The study is `Docs/PISTOL_TINY_WINGS_STUDY.md`** (sources: the MIT haqu remake, the Kodeco tutorial, two clones,
  the guides; Wavespark, Dune!, Alto's, Ski Safari for the genre). What it corrected:
  - **The ground is not a sinusoid.** Random key points, alternately crest and trough, 160-240 pt on and 60-120 pt up or
    down for a 14 pt bird, a half-cosine between: every hill a different size, every crest and trough level. Rebuilt as
    `PistolPlane.HillTrack` in the Pistol's length (half-hills 10-15 u, rises 3.6-7.2 u, a +-9 u band, +8 % taller an
    island to x1.5), seeded from the island's two perches so the rider's machine builds the same ground; the island
    starts on a crest (the first half-hill is the dive) and ends on a climb (the ramp onto the perch). `Hills` (the
    sinusoid and swell) is retired.
  - **The dive is ~7 g, not 4, and the air is not light.** Gravity 9.8 m/s^2 plus a 40 N dive on a 0.6 kg body; drag 5 %/s;
    no friction, no bounce, no cap, a floor on forward speed. Now `pGravity` 19, `pDiveMul` 7.5, `pAirGravity` 1,
    `pDrag` 0.05, `pMinSpeed` 6, `pMaxSpeed` 90.
  - **A landing loses only the speed into the slope** (frictionless, no bounce): `pHardKeep` 1, and the perfect slide is
    the game's scored event, not a speed bonus (`pSmoothBoost` 1.05 kept as a touch, `pGoodLand` 30 deg).
  - **Level** (`slopeDegrees` 0): the game's hills are level; every u/s is the rider's.
  - **The camera**: the bird an eighth of the screen from the left and the zoom by height only, keeping it under four
    fifths (`viewPistolAt` -0.35, `viewHeadroom` 0.2, `viewSpeedWiden` 0.15, `viewMaxWidth` 150).
  - **Measured** (60 fps, two seeds): a 36-hill island in 20-21 s at 1.6-1.8 screens a second, 14 flights (13-14 perfect
    slides), the longest 4.3 hills and 3 hill heights up, speed 23 -> 33 d/s with a top of 51; a 73-hill island in 40 s.
    A rider wrong a third of the time takes twice as long (so would the game's); never diving dawdles at the floor speed
    (93 s; the game's nightfall is what ends that); never letting go stays glued (43 s, one flight).
  - **[read] for Garrett:** the Pistol AI is in the air 77-81 % of a ride (it dives in the air only when that lands it
    flatter). And the study's §4, not built: fever for the pair (three perfect slides -> the den or the ring), speed coins
    in the troughs, juice on the slide and the launch, a clock.
  `GobyPistolTests`: `Ground_IsTinyWingsKeyPointHills` (lengths, rises, alternation, level keys, the dive start and the
  ramp end, determinism; controls: flat, growth), `Ride_IsTinyWings` (pace in screens a second, flights, perfect slides,
  the top speed in bird lengths; the never-dive, never-let-go and sloppy controls), `Proportions_AreTinyWings` on the
  game's ranges, the view test on the game's framing. Their logic run in .NET against the pure files: all pass.
- 2026-10-24, Garrett: "this vessel needs a lot of work but this is big step. now give the pistol a model, give both of
  its seats separate UI ability icons, glyphs, etc. and make the vortex ring loose the flat effect and replace it with a
  cool warping effect like the black holes use but convey a more subtle toroid instead of a spherical singularity."
  - The Pistol's model: `Pistol.fbx` (three rounds; a wasp, then a shrimp side-on, then its paint and fan fixed), drawn
    by the seat in place of the capsule, with the claw snap and the dive's tail curl. **[proposed]** its length 2.7 u
    (the capsule's) and the claw on the camera's side going forward.
  - Each seat's own HUD: `GobyHUDVariant`, eight placeholder icons, per-seat chips, veils, gauges and counts (top
    section). **[proposed]** the Pistol's controls on the fleet's four labelled face buttons (old keys kept), so every
    chip is honest; the aimed jump's key J.
  - The toroidal lens: `VortexRingLens` (top section). **[proposed]** dropping the faceted ring mesh entirely (the lens
    alone carries the ring); the rim tint stays the ring's old 0x9fe8ff rather than the domain colour.
- 2026-10-25, Garrett: the path map, teleport between perches, either direction from any den, den growth only from the
  distance ridden (top section). **[proposed]** the map's look (side-on to the islands, lit bars by score, a white pick
  with a pointer), its controls (A / D or left stick, F / Y turns round, the dive or a tap rides), "opposing" = any domain
  but the pair's, and the AI's pick (the richest island, else its old way).
- 2026-10-25, Garrett: "the control priority for 2d characters is mobile, gamepad, mouse/keyboard. so make sure the
  controls work best in that priority. the player needs to be able to spin the map to decide where they want to go when
  choosing the den to teleport to." The map's controls reworked touch first (drag spins, tap picks, tap again rides),
  then the pad (right stick spins, left stick picks, A rides), then the keyboard (top section). **[proposed]** tap-again
  to ride on touch, and the spin kept between perches.
- 2026-10-25, Garrett: "ability matrix should now include controls like that. table with all three platforms. please give
  me an ability matrix for both seats. the goby AI was doing a bad job of taking paths that would give the shrimp plenty
  to destroy." The ability matrix above (both seats, mobile / gamepad / mouse-keyboard). The Goby AI now plans each
  island through the opposing prisms (`PistolPaths.BestHeading`): of 26 fixed headings and a heading through each of up
  to 64 sampled opposing prisms, the one whose island passes the most within the Pistol's reach, its end inside the home
  radius; Easy and Medium misread the counts (up to +-50 % / +-22 %, rolled per island). It chases a rival only when no
  opposing prism is in reach. **[proposed]** the corridor (the undertow's reach, or `pathScoreBand` when wider).
