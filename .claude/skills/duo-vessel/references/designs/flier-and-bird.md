# Flier & Bird (working name): the first duo vessel

**Status:** design brief. Nothing is built. Given by Garrett on 2026-10-14. The two halves are
called **the Flier** (front) and **the Bird** (back) until they have class names.

Each line below is tagged:
- **[stated]**: the user said it. Do not change it without asking.
- **[read]**: an interpretation of what the user said. Confirm it before anyone builds on it.
- **[open]**: not decided. Ask; never fill it in yourself (`/vessel` §3, the design-approval gate).

## 0. References the user gave

- **Tiny Wings** (https://www.youtube.com/watch?v=nn_s051gdjQ, a world-record run). The Bird's
  feel. In Tiny Wings the bird dives onto the downslope of a hill to gain speed and lets go on the
  upslope to fly. Here the hills are the spline through the Flier's trail. **[read]**
- **"Whistling Birds"** fireworks (https://www.youtube.com/watch?v=MZ-mwLBlcbc). How the Flier's
  heat-seeking flares look and sound: many small whistling projectiles fanning out, each to its own
  target. **[read]**

## 1. The pair

| | Flier (front) | Bird (back) |
|---|---|---|
| Role | Makes the path, primes targets, finishes hot targets | Rides the path and destroys things |
| Space | Full 3D, flies like the **Serpent** **[stated]** | **2.5D**, warped from 3D **[stated]** |
| Collision | **Ignores collision** **[stated]** | Breaks through every block **except the Flier's trail**. Friendly fire is ON **[stated]** |
| Trail | The main trail. Its blocks alternate their angles, and those angles are the control points of a spline **[stated]** | A tiny trail **[stated]** |
| Input | **Two-touch** flier on mobile **[stated]** | Launch aim: drag on mobile, right trigger + right stick on controller **[stated]** |
| Camera | **[open]**: "far cam like serpent" is in the Bird's paragraph, so it most likely means the Bird | Far camera like the Serpent's **[stated, owner ambiguous]** |
| AI priorities | 1. Get the crystal. 2. Hunt flora and fauna. 3. Lay a path for the Bird's destruction **[stated]** | **[open]** |

## 2. The Flier

- **Flight:** like the Serpent, two-touch **[stated]**. Ignores collision **[stated]**.
- **Speed from closeness:** the closer the Bird is, the faster the Flier goes **[stated]**. A
  **speed gauge** on the HUD shows the change **[stated]**. **[open]:** the distance-to-speed curve
  and its range.
- **Straight-line boost:** a speed boost that is active while flying straight. It is like the
  Rhino's full-speed-when-straight, but weaker **[stated]**. **[open]:** how much weaker.
- **Trail:** blocks are placed at **alternating angles**, and the alternation defines the control
  points of a **spline curve** **[stated]**. The spline:
  - exists only within range of the Bird's screen **[stated]**. So it is built and kept only near
    the Bird, never for the whole trail.
  - is **invisible to everyone but the Bird** **[stated]**. That is per-viewer rendering: in a
    network game only the client piloting the Bird draws it.
- **Primer ("oil bomb")** **[stated]**: shoot it at things to **prime** them for an explosion by
  the Bird.
  - Primed blocks are **visible to the Bird at reduced opacity, even outside its vision range**.
  - The prime **decays after a delay**. The delay is long enough for the Bird to try **one
    recovery**, i.e. one go-around.
  - When a prime is **detonated**, things just outside the prime range become **hot**.
  - Generous charges, short cooldown.
- **Heat-seeking flares** **[stated]**: they destroy **hot** blocks. One volley fires **one flare
  per hot block in the area**.
  - **Ships and flora are hot by default**, so the flares always have something to hunt.
  - Charges on a longer cooldown than the Primer.
- **Swap:** see §5.

## 3. The Bird

- **2.5D** **[stated]**. **Yaw is warped** to turn 3D into 2.5D **[stated]**. **[read]:** the Bird
  flies a strip that follows the Flier's trail. Along the trail and up/down are the two real axes,
  like Tiny Wings' side view. Yaw stops being a free 3D heading: it is bent so the Bird keeps
  facing along the trail and cannot turn off the strip. **[open]:** what yaw input does after the
  warp (nothing, a lean, a lateral slide within the vision range?).
- **Body:** a **cylinder whose length reaches just past the vision range** **[stated]**.
  **[read]:** the cylinder lies across the strip, so the Bird sweeps the whole visible width at its
  position along the trail. That is how a 3D world is played in 2D without the depth axis ever
  needing to be aimed.
- **Vision:** besides the main trail, the Bird sees only blocks **within a set distance left or
  right of the main trail** **[stated]**, plus primed blocks at reduced opacity at any distance
  (§2).
- **Magnetic attraction to the trail** **[stated]**. **[read]:** this is the Tiny Wings hill. The
  spline pulls the Bird down onto it, a dive onto a downslope builds speed, and leaving an upslope
  throws the Bird into the air.
- **Collision:** breaks through **every block except the Flier's trail**. **Friendly fire is ON**,
  so it also breaks its own team's blocks **[stated]**. Ghosting is how it avoids friendly blocks
  **[stated]**.
- **Abilities:** "Angry Birds abilities" **[stated]**. Named so far:
  - **Launch:** set a destination and fly there. Mobile: tap and drag. Controller: hold the right
    trigger and aim with the right stick **[stated]**.
  - **Explosion on impact:** implied by "a free explosion on impact" from the crystal, so it is
    normally an ability with a cost **[read]**.
  - **Ghosting / no block collision:** "4th ability is no block collision" **[stated]**, and
    "ghosting to avoid friendly blocks" **[stated]**. **[read]:** these are the same ability.
  - **[open]:** the remaining slot, and "the below 2" in the brief. Read literally, it means the
    two abilities described after that sentence (Launch and the impact explosion?). Confirm.
- **Crystal (Bird only):** **resets the Bird's ability cooldowns** and gives it **a free explosion
  on its next impact** **[stated]**.

## 4. Crystals (both halves)

**Crystals fully restock every charge of everything** **[stated]**. That covers the Primer, the
flares and the Bird's abilities. **[open]:** does a crystal taken by either half restock both, or
only the half that took it? (The Bird-only bonus in §3 comes on top.)

## 5. Who flies what (the duo contract applied)

- **One squad slot, balanced as one vessel** **[stated]**.
- **Solo:** a **phone flip** swaps between the Flier and the Bird **[stated]**. The D-pad swaps on
  a controller (the duo convention, SKILL.md §2). The half you are not flying is flown by its AI.
- **Two players:** a phone flip **offers** a swap to the other player. It does not force one
  **[stated]**. **[read]:** the D-pad works the same way, and the swap happens only when the other
  player accepts.
- **Human + AI:** the AI flies the other half. The Flier's AI uses the priorities in §1.

## 6. The elements (open)

**[open]: none of the eight element mappings exist** (four per half, or four for the pair; see
SKILL.md §3.3). Candidates the brief suggests, for Garrett to approve or reject. These are
proposals, not design:

- Space (reach): the Bird's vision width / cylinder length, or the prime radius.
- Time (rate): the prime decay delay, or the Primer / flare cooldowns.
- Charge (threat): the flares per volley, or the explosion radius.
- Mass (size): the Bird's cylinder radius, or the trail block size.

## 7. Open questions, collected

1. Class names for the pair and each half; the `VesselClassType` value(s) (SKILL.md §3.1).
2. Who owns the far cam: the Bird, the Flier, or both.
3. The Bird's yaw after the warp (§3).
4. The Bird's full four-ability list and what "the below 2" means (§3).
5. The distance-to-speed curve for the Flier, and how much weaker the straight boost is than the
   Rhino's.
6. The prime decay delay in seconds ("one recovery": time it from the Bird's go-around in the
   studio).
7. The "hot" ring: how far beyond the prime range, and how long things stay hot.
8. Whether a crystal restocks both halves (§4).
9. The element maps (§6).
10. What the Bird does when the Flier is far ahead and the spline has not been built yet (e.g. the
    Bird spawns or respawns away from the trail).
11. The Bird's AI priorities.

## 8. Decisions log

Add a dated line each time an **[open]** or **[read]** item is settled, and update the line it
settled. Never delete the original tag. Strike it through and point at the decision.

- 2026-10-14: brief recorded (this file).
