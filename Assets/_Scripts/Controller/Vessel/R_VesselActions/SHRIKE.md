# Shrike (VesselClassType.Shrike = 10)

The Shrike is a slow sniper. It docks to the mass it touches, builds nests, slings itself across the cell on a tether
and calls missiles down from its nests' turrets.

- **Design:** the Notion page "Class - Shrike".
- **Source of every number and mechanic:** the Shrike Studio. Its repo page is `Docs/Studios/VesselStudio/shrike.html`,
  published as its own artifact (https://claude.ai/artifact/JD17sQMeuEUETp6AUPqMmy). The studio's record is
  `Docs/Studios/SHRIKE.md`.
- **How it got here:** this port is the first `/artifact-to-unity` clone (`Tools/Build/studio_to_unity/clones.json`).

## 1. Where everything is

| Piece | What it is |
|---|---|
| `ShrikeRules` | Pure rules, ported line for line from the page. Covers the nest cup, the turret, the block count, the dock-bonus clock, the sling's speed, the box and ray maths, missile turning, turret regen and the one-missile-per-paint rule. |
| `ShrikeHullForm` / `ShrikeHullBuilder` | The procedural hull: the page's `makeHull`, with Unity's +Z nose. |
| `ShrikeExecutor` | The whole kit, ported from the page's `stepPilot`, `fireLance`, `stepTether`, `stepSling`, `updateLock`, `launchMissiles`, `stepMissiles`, `detonate` and `stepNests`. Also holds the input-only autopilot. |
| `ShrikeActionSO` | The four controls. |
| `ShrikeConfigSO` | `ShrikeConfig.asset`. Field names are the studio's SHIPPED keys and tooltips are its labels. |
| `ShrikePrismController` | The cruising trail. Mass shortens its wavelength. |
| `ShrikeAnimation` | The flame, the bank and the element morphs. |
| `ShrikeHUDController` / `ShrikeHUDView` | The four gauges. |
| `ShrikeCrystalEffectSO` | A crystal resets the sling and adds nest health. |
| `ShrikeTests` | The pure rules with golden numbers from the page, run outside Unity by `Tools/Build/shrike_harness/run.sh`. |
| `ShrikeConfigTests` | The elemental ranges against SHIPPED. |
| `Tools/Build/author_shrike_assets.py` | Builds the prefab, the assets and the registrations (`--check`, `--self-test`). |
| `Tools/Build/studio_to_unity/shrike.json` | The number map. |
| `author_shrike_icons.py` / `render_shrike_card_icon.py` | The four HUD icons and the card. |

## 2. Hull

The hull is the page's `makeHull`, turned so its nose is Unity's +Z:

- a six-sided fuselage and a dark sensor head;
- the Void Lance's barrel slung under the nose;
- two forward-swept wings with domain stripes;
- a tail fin;
- the engine flame.

Submesh 1 holds the accents, and `ShipHelper` paints the domain colour onto it.

The element morphs follow the class document's mapping:

| Element | What it changes |
|---|---|
| Charge | thickens the barrel |
| Mass | fattens the fuselage |
| Space | lengthens the barrel |
| Time | spreads the wings and lengthens the flame |

### 2.2 Animation

- The flame is an ember while docked, cruise length in flight, and four times as long while slinging.
- The hull leans into the turn.

## 3. Controls and kit

The bindings are shared by every device. They are lab-only choices: the document names none.

| Input | Control | Element |
|---|---|---|
| RT / LMB (held) | **Void Lance** | Charge: radius 1.5 → 5 |
| A / Space | **Nest / Dock** | Mass: 12 → 32 blocks; trail wavelength 8 → 4 |
| LT / RMB (held) | **Targeting Array** | Space: lock cone 5° → 14° |
| B / R | **Void Sling** | Time: cooldown 6 → 2.5 s |

**Docking.**
- Any block the hull touches docks it. The test is the hull radius against the block's oriented box.
- Your own trail younger than 1 s never docks you.
- Docked, translation is pinned. The nose still aims, faster, because docking uses the fleet's restricted stance.
- Docked, cooldowns run ×1.6. In a nest or on super-shielded mass they run ×2.5.
- A docked or slinging hull's spawner pen is up.

**Nest (A).**
- Docked, A undocks with a push of 8 u/s off the surface.
- Flying, A builds a cup of super-shielded blocks round the hull, with the nose's way out open, then docks in it.
  - The blocks come from `BoostRingBuilder.LayOne`.
  - The turret bud sits on top.
- At most three nests. A fourth retires the oldest.
- Touching one of your own nests snaps the hull to its centre.

**Void Lance (RT).**
- A 2400 u hitscan. Each block it passes is tested by ray against the block's box, thickened by the beam's radius.
- It breaks every block it passes, super-shielded included, using `SniperShotActionExecutor`'s sanctioned teardown.
- It skips the block you sit on and the nest you are docked in.
- A rival hull on the line is a scored petal hit.

**Void Sling (B).**
- The tether flies at 2400 u/s for up to 1600 u and anchors on the first block it meets.
- The hull then flies there at distance / 1 s, swept every frame against the spatial index.
- It docks on the first block it touches, or on the anchor when it arrives.
- On the way it lays thick 2.6 u blocks, one per wavelength, with a per-frame cap.
- A crystal resets the sling.

**Targeting Array (LT).**
- The lock goes to a rival hull inside the cone first, and holds to 1.6× the cone.
- Otherwise it takes the centre of the mass within 35 u of the block nearest the aim.
- Each nest launches one homing missile per paint while its turret has a health block to spare.
- A missile:
  - swerves round blocks that are not part of the target's mass;
  - blows on contact or after 8 s, in an 18 u blast;
  - in the blast, a shield takes the hit first, and every rival hull inside it is a petal hit.

**PvP is petals only (LOCKED).** Nothing moves, slows or disables another pilot.

### 3.5 Crystals

Every crystal kind does two things:

- resets the sling;
- gives each nest 3 health blocks, up to 12.

### 3.6 Trail

- The cruising trail is thin long blocks: 0.6 × 0.6 × 7, one per wavelength (8 u, 4 u at Mass 10).
- Cruise is the design ship's 18 u/s.

## 4. HUD

The four cards are Lance · Nest · Array · Sling, in charge → mass → space → time order.

| Card | Gauge fills with | Turns the ready colour when |
|---|---|---|
| Lance | its cooldown | docked |
| Nest | its cooldown | in a nest |
| Array | full while a nest can fire | painting a lock |
| Sling | its cooldown | slinging |

## 5. Networking

Like the Brittle Star, the executor runs on every peer, because the presses replicate.

- Every machine holds the same docks, nests, tethers and missiles.
- Prism destruction is per peer.
- Only the hull's **motion** is written by its owner: the sling's flight, the undock push and the snap onto a surface.
- Scored hits are decided on the shooter's owner.
- **Not verified in multiplayer.** Docking is decided on each peer from its own copy of the hull's position, which a
  network-interpolated hull can disagree with near a block edge.

## 6. What differs from the page, and why

- **Nests do not turn with the docked hull.** The page re-poses the nest every frame ("rotates with the shrike"). The
  game's prisms are registered in the spatial index where they were laid, so a nest is laid in the hull's orientation
  at the moment it is built.
- **Turrets cannot be destroyed, and they drop no crystal.** The page's lances and missiles kill turrets. In the game a
  turret is the owning executor's own object, and no other vessel can query it.
- **A missile blast strips one tier of a super-shield and then stops.** The page counts shield tiers. The game's
  `DeactivateShields` drops every tier, so a super-shielded block takes two blasts here, not three.
- **The lock does not target fauna or asteroids.** Those are the minigames' targets. In freestyle the lock takes rival
  hulls, then mass.
- **The Ark range is not ported.** It is the minigames' arena, which is lab-only.
- **Own-trail rule for the lock.** The mass lock ignores your own blocks (your nests and your trail). The page ignores
  every trail.

## 7. Open

- **Level-5 upgrades** are open design slots. `ElementalAbilityMaps/Shrike.asset` leaves them empty, because the class
  document names none.
- **FMOD:** every event slot on `ShrikeConfig.asset` ships empty.
- **Element minigames.** The Shrike's two studio minigames do not yet follow labmaker §1.5, one minigame per element:
  - Time: race;
  - Space: destruction;
  - Mass: creation;
  - Charge: dogfight.

  See `Docs/Studios/SHRIKE.md` §6.
