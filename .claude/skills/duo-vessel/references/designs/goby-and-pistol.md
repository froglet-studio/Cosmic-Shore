# Goby & Pistol: the first duo vessel

**Status:** design brief, round 2. Nothing is in the game. **The lab is built** (round 1, §9):
`Docs/Studios/VesselStudio/goby.html`, a page of the one Vessel Studio artifact.

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
| Space | Full 3D, flies like the **Serpent** **[stated]** | A **warped 2D world** with parallax depth (§4) **[stated]** |
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

- **Flight:** like the Serpent, with two-touch controls **[stated]**. **Ignores collision**
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
- **Goby flight:** `SingleStickVesselTransformer`. **Straight boost:** `RampBoostActionExecutor` +
  `StraightLineGesture`.
- **Pistol far camera:** the Serpent's `SerpentCameraSettingsSO.asset` is a chase cam, not a side
  cam. The Pistol needs a new camera mode.
- **Swap:** `PilotSwap` + `PilotSwapGesture`.
- **Primer, hot, flares, speed gauge, proximity speed, shared cooldowns, the shared meter, the
  warped render:** none exist. See ANCHORS.md §3.

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
- 2026-10-14, round 1 of the lab built (§9.1). Nothing decided yet: Garrett picks the unroll in the lab (its
  Decisions tab writes to the shared log).
