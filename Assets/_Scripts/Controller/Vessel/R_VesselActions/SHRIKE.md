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
| `ShrikeActionSO` | The six controls: Trigger, Zoom, Mass, and the Space / Charge / Time mode buttons. |
| `ShrikeNestFlora` / `ShrikeMeshes` | The flora that grows missiles, and the missile, bud and stem meshes. |
| `ShrikeScopeOverlay` | The zoom's per-mode scope and the painted-target marker (local pilot only). |
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

Garrett redesigned the controls on 2026-10-15, after the first port. **LT is always zoom, and RT does the selected
mode's action.** The three modes are picked with face and shoulder buttons. Mass is its own button.

| Input | Control | What it does | Element dial |
|---|---|---|---|
| X / Q | **Space mode** | RT (held) fires the Void Lance. | Charge: radius 1.5 → 5. The Space upgrade "Void Pierce" lets it break super-shielded mass. |
| RB / E | **Charge mode** | RT (held) paints a target for the flora's missiles. | Space: lock cone 5° → 14° |
| B / R | **Time mode** | RT shoots the Void Sling. | Time: cooldown 6 → 2.5 s |
| A / Space | **Mass** | Docked: detach, leaving a nest behind. | Mass: 12 → 32 nest blocks; trail wavelength 8 → 4 |
| LT / RMB (held) | **Zoom** | Narrows the flight view and raises a scope in the mode's shape. | none |

The Space upgrade is the only designed level-5 upgrade. The other three slots are open.

**Docking.**
- Any block the hull touches docks it. The test is the hull radius against the block's oriented box.
- Your own trail younger than 1 s never docks you.
- Docked, translation is pinned. The nose still aims, faster, because docking uses the fleet's restricted stance.
- Docked, cooldowns run ×1.6. In a nest or on super-shielded mass they run ×2.5.
- Docked, the local pilot's chase camera eases back to 1.8× its authored distance. It eases home on leaving.
  - The camera is written only while this hull is its follow target.
  - It is handed back only if nobody else moved it in the meantime (ThresherExecutor's rule).
- A docked or slinging hull's spawner pen is up.

**Mass (A).**
- Docked, Mass detaches the hull.
- On the nest cooldown (8 s), it also leaves a **nest** behind:
  - a cup of super-shielded blocks round where you were, with the nose's way out open;
  - crowned with a **nest flora carrying one missile**.
- The hull leaves through the mouth, and for 2 s the nest it just left cannot catch it.
- Inside one of your own nests, Mass only detaches.
- Flying, Mass does nothing.
- At most three nests. A fourth retires the oldest.
- **No block clips another.** The cup's radius grows until its two closest blocks are more than a block's bounding
  diameter (s√3) × 1.1 apart, so no two cubes can intersect whatever their orientation (`ShrikeRules.NestRadiusFor`).
  - 12 blocks sit on a radius of about 12 u; 32 blocks on about 20 u. Mass makes a bigger nest, not smaller blocks.
  - Each block faces outward.
  - `ShrikeTests.Nest_NoTwoBlocksClip_AtAnyCount` holds it for every count from 4 to 40.

**Void Lance (Space mode, RT held).**
- A 2400 u hitscan that breaks every block it passes, tested by ray against the block's box.
- **Super-shielded mass only with the Space upgrade.** Below it, the beam passes super-shielded blocks
  (`ShrikeRules.LanceBreaks`; the Serpent's Pierce gate, `IsUpgradeActive(Element.Space)`).
- It skips the block you sit on and the nest you are docked in.
- A rival hull on the line is a scored petal hit.

**Void Sling (Time mode, RT).**
- The tether flies at 2400 u/s for up to 1600 u and anchors on the first block it meets.
- The hull then flies there at distance / 1 s, swept, laying thick 2.6 u blocks.
- It docks on the first block it touches, or on the anchor.
- A crystal resets the sling.

**Targeting Array (Charge mode, RT held).**
- Lock priority is **vessels, then fauna, then flora, then prisms**, by class first and then nearest the aim
  (`ShrikeRules.BetterLock`). A held lock stays to 1.6× the cone until something of a higher class enters the cone.
  - Vessels: rival hulls.
  - Fauna: `Cell.LiveFauna` across the active cells.
  - Flora: `FloraHeartRegistry.Live`, by its heart.
  - Prisms: the centre of the blocks within 35 u of the one nearest the aim. Your own blocks are never the target.
- While painted, **every flora with a ready missile launches one every 0.8 s**.
- Missiles home while you paint, swerve round blocks that are not part of the target, and blow on contact in an 18 u
  blast. A shield takes the hit first, and a rival hull inside the blast is a petal hit.
- **Painted-target indicator:** a spinning, pulsing diamond with four ticks sits on the target's screen position.
  It shows for the local pilot only.

### 3.4 Zoom and the scope

- LT eases the flight view to 24° over 0.2 s, through `VesselSpeedTunnel.SetHomeFieldOfViewOverride`. That tunnel is
  the gameplay camera's ONE FOV writer, so letting go hands the player's own FOV back.
- A scope fades in, its shape set by the mode (`ShrikeScopeOverlay`, built at runtime, local pilot only):
  - **Space:** a sniper's ring with a split crosshair.
  - **Charge:** four lock brackets round a diamond.
  - **Time:** a tether line with chevrons running up it.
- The Serpent chose a picture-in-picture over magnifying the flight view, because a magnified flight view magnifies
  every motion (`VesselSpeedTunnel` § the override). Garrett asked for a zoom, and the Shrike mostly zooms while docked
  and still, so the flight view is magnified here. If it reads as nauseating in flight, the Serpent's window is the
  fallback.

### 3.5 Crystals and nest flora

- Every crystal kind becomes a **nest flora** where it was (`ShrikeNestFlora`, at the crystal's collect pose so every
  peer agrees), and resets the sling.
- A flora is a stem and a spinning bud with **large missiles (4 u) standing round it like petals**. It grows one every
  6 s (`turretRegen`), twice as fast while you sit in its nest, up to 4.
  - The growing missile swells visibly.
  - Ready ones breathe.
- At most 8 flora; a new one withers the oldest.
- **Missiles in flight** are the same 4 u body in the domain's material, with a bright domain-coloured trail 1.8 u wide.

### 3.6 Trail

- The cruising trail is thin long blocks: 0.6 × 0.6 × 7, one per wavelength (8 u, 4 u at Mass 10).
- Cruise is the design ship's 18 u/s.

## 4. HUD

The four cards follow the element map: Charge (Targeting Array) · Mass (Nest) · Space (Void Lance) · Time (Void Sling).
The **selected mode's card** turns the ready colour.

| Card | Shows | Ready colour when |
|---|---|---|
| Charge | **The count of missiles left across every flora**, a number generated into the card at runtime (the Squirrel's steal count is the pattern). The gauge is full while any missile is ready. | Charge is selected |
| Mass | The nest cooldown | docked |
| Space | The lance cooldown | Space is selected |
| Time | The sling cooldown | Time is selected |

## 5. Networking

Like the Brittle Star, the executor runs on every peer, because the presses replicate.

- Every machine holds the same docks, nests, tethers and missiles.
- Prism destruction is per peer.
- Only the hull's **motion** is written by its owner: the sling's flight, the undock push and the snap onto a surface.
- Scored hits are decided on the shooter's owner.
- **Not verified in multiplayer.** Docking is decided on each peer from its own copy of the hull's position, which a
  network-interpolated hull can disagree with near a block edge.

## 6. What differs from the page, and why

The game is now AHEAD of the Shrike Studio. The studio still has the first design: a nest built on A, and turret health
blocks. The 2026-10-15 control scheme and flora live in Unity only, until the studio is updated.

- **Nests do not turn with the docked hull.** The game's prisms are registered in the spatial index where they were
  laid.
- **Flora and turrets cannot be destroyed.** A flora is the owning executor's own object, and no other vessel can query
  it.
- **A missile blast strips one tier of a super-shield and then stops.** The game's `DeactivateShields` drops every tier.
- **Not ported:** the Ark range and the minigames' targets (asteroids).

## 7. Open

- **Level-5 upgrades:** Space has "Void Pierce" (the lance breaks super-shielded mass). Charge, Mass and Time are open
  design slots.
- **FMOD:** every event slot on `ShrikeConfig.asset` ships empty.
- **Element minigames.** The Shrike's two studio minigames do not yet follow labmaker §1.5, one minigame per element:
  - Time: race;
  - Space: destruction;
  - Mass: creation;
  - Charge: dogfight.

  See `Docs/Studios/SHRIKE.md` §6.
