# TERMITE — the commander queen

`VesselClassType.Termite = 8`. Restored September 2026 from the 2024 prototype: a
**Clash Royale–style commander** — you do not fly her, you point where she should go — whose
abilities are a **deck of six cards** paid for in **pheromone**. The card art is the
prototype's own (`_Graphics/VesselButtons/TermiteCard_{Front,Back}_*.png`).

| file | role |
|---|---|
| `TermiteHullForm.cs` / `TermiteHullBuilder.cs` | the procedural queen hull (pure geometry + builder) |
| `Animation/TermiteAnimation.cs` | wing fold/buzz, abdomen breath, the card pulse, procedural morphs |
| `TermiteCommandTransformer.cs` | commander flight (every machine that simulates her) |
| `TermiteCommander.cs` | the local pilot's pointing + orbit-camera input |
| `R_VesselActions/Executors/TermiteDeckExecutor.cs` | pheromone, the owner gate, the six cards |
| `TermiteDrone.cs` / `TermiteMound.cs` | the queen's workers/soldiers and her mounds |
| `Data Containers/TermiteCardActionSO.cs` / `TermitePheromoneActionSO.cs` | card slots, the Time clock |
| `UI/View/TermiteHUDView.cs` / `UI/Controller/TermiteHUDController.cs` | the deck on the HUD |
| `Editor/FrogletTools/TermiteVesselSetup.cs` | builds the prefab, HUD variant, container, registrations |
| `Tools/Build/termite_hull_harness/` | compile-and-RUN gate for the hull (winding included) |
| `Tools/Build/author_termite_pheromone_icon.py` | the Time card's placeholder icon (`--check`) |

---

## 1. Why it failed before, and what was kept

The prototype was a Manta-bodied prefab carrying `CommandVesselTransformer`, `QueenDronesVesselAction`,
`MoundDronesVesselAction`, `DeployDronesAction`, `RecallDronesAction`, a `BoidController` and **two
missing scripts**, registered in `DefaultNetworkPrefabs` and in **nothing else** — so it could not be
spawned by class. Its drone prefab (`_Prefabs/FloraAndFauna/TermiteDrone.prefab`) is built on a plain
`Prism`, not a `HealthPrism`, so the `Boid` it carries bails out; and a drone that WAS a fauna would
break the elemental economy (every lifeform drops a crystal — §5.3).

Kept: the **idea** (point-to-command flight; a deck of Autothysis / Team Crystal / Queen Drones /
Mound Drones / Teleport / New Mound) and the **card art**. Everything else is rebuilt on the fleet's
current platform. The legacy scripts are left in place, unreferenced by the new prefab; retiring them
is a `/refactor` proposal, not part of this change.

## 2. Design decisions (confirmed with the design owner)

- **Deck = element pairs.** Each ELEMENT slot of the four-icon row holds TWO cards that alternate:
  pressing plays the face-up card and turns the other over. Charge = **Autothysis ↔ Team Crystal**,
  Mass = **Queen Drones ↔ Mound Drones**, Space = **Teleport ↔ New Mound**, Time = the **pheromone
  clock** (passive).
- **Flight = the legacy commander, plus click-and-drag to change the perspective.**
- **New Mound = found a mound** (a structure of her own mass that workers build up).
- **Hull = a procedural queen** (the Scarab/Butterfly pattern).

The level-5 upgrades are **open design slots** (proposals in `Docs/ElementalAbilitySystem/FLEET_MAPS.md`).
Nothing was invented for them.

## 3. The hull

A queen termite: small head with mandibles and beaded antennae, thorax with six legs, a huge
segmented abdomen (plates on submesh 0, membrane bands on submesh 1 = the DOMAIN colour) and two
pairs of long wings (accent, double-sided). Four element extremes, each baked to per-vertex deltas:
Charge = mandibles and head, Mass = a fatter abdomen, Space = longer wings and antennae, Time =
leaner/longer legs and a lifted wing set.

`termite_hull_harness/run.sh` compiles and RUNS the shipped form: topology AND pivots hold across all
four extremes, blend@1 == extreme, baked bounds contain every weight corner, the silhouette is a
queen (abdomen 59% of the body, each wing reaching past the tail), the abdomen closes at its tip,
and — **T8** — every closed-surface face is front-facing along its authored normal.

**T8 exists because the hull shipped inside-out on its first pass.** Unity's front face is the one
whose `Cross(v1 - v0, v2 - v0)` points at the viewer (proved against `OctahedronMeshGenerator`, the
shipped shield mesh), and `VesselGraph` draws front faces only (`m_RenderFace: 2`). Every harness
check passed on the inside-out hull, because none of them asked. **The Butterfly body has the same
defect** (0/224 faces front-facing, measured with the same test) — see §10.

## 4. Commander flight

**You do not fly the queen, you tell her where to go.**

| device | point (command) | orbit the view | zoom |
|---|---|---|---|
| mouse | left CLICK | left or right DRAG | scroll |
| touch | TAP | one-finger DRAG | pinch |
| pad | left stick steers the point (relative to the VIEW) | right stick | d-pad up/down |
| keyboard | WASD steers the point | arrow keys | `=` / `-` |

A click and a drag share the left button, told apart by distance (`dragThresholdPixels` 10). A click
resolves onto the plane through the queen facing the camera — "that place, at her depth" — so every
direction in 3D is reachable by orbiting first. A press that starts over UI belongs to the UI.

- **`TermiteCommandTransformer`** derives from `VesselTransformer`. While a local commander has a live
  command she flies there — accelerating (90 u/s²), braking into the arrival (`sqrt(2·70·d)`), and
  hovering inside 4 u — and PUBLISHES `Speed`/`Course` like any hull. **Without a command she is an
  ordinary stick hull**, which is what an AI pilot and the menu autopilot fly, with nothing
  mode-specific to wire. The danger-prism slow (`throttleMultiplier`) and knocks (`velocityShift`)
  reach a commanded queen exactly as they reach a flown one. TIME scales her commanded cruise
  (70 → 112 u/s, `commandCruiseSpeed`).
- **`TermiteCommander`** runs for the local human pilot only (edge-detected from `IsLocalPilot` and
  autopilot, so a swap, an AI takeover or the lava lamp releases everything). It drives the camera
  through **`CustomCameraController.CommanderFrame` / `CommanderZoom`** — a point-of-use override
  that replaces the follow target's rotation as the camera's frame and scales the follow offset,
  identical to the old behaviour when unset, and cleared only by the commander that set it.
- **She is a TWO-thumb hull.** The one-thumb desktop scheme makes the mouse a stick — the device a
  commander points with — so she left `OneThumbVesselCoverageTests`' roster and resolves the dual-stick
  strategy, whose sticks she reads as steering.

## 5. The deck

### 5.1 The gate — one decision, taken once, on the owner

Every press is replayed on every peer (owner → ServerRpc → ClientRpc → `StartAction` everywhere).
A card that refused itself inside `StartAction` would be played where the pheromone happened to be a
frame ahead and refused elsewhere. So the executor registers
**`R_VesselActionHandler.OwnerPressGate`** — a new, generic, null-by-default seam consulted only where
a press ORIGINATES (the owner's input and `PerformShipControllerActionsReplicated`), never where it is
replayed. It either refuses (the press is never sent; the deny sound plays on the owner) or admits
and **reserves** the cost immediately, so two presses inside one round trip cannot both be admitted
against pheromone that pays for one. `Play` consumes the reservation; a replica pays on receipt,
clamped at 0. Every machine plays the same cards in the same order and flips the same slots.
`R_VesselActionHandler.TryGetActionOn<T>(input)` was added beside `TryGetBoundAction<T>` so the gate
judges the press it is asked about.

### 5.2 Pheromone (TIME)

A 10-point tank, 5 at spawn, refilling at **0.357/s at rest (Clash Royale's one per 2.8 s) → 0.714/s
at Time 10**, evaluated at the REPLICATED level so every peer's clock agrees. Time buys tempo, never a
bigger hand.

### 5.3 Drones — vessel-owned agents, never lifeforms

**Deliberately not `Fauna`.** Every lifeform drops exactly one elemental crystal and lifeform
spawning is the economy's only SOURCE, so a card that spawned lifeforms would mint petals on demand.
A drone is the queen's hand: no heart, no body prisms, **no collider**, credited to her name and
domain. Cap 36 live drones (a drone card is refused at the cap; nothing is recycled on a count).

- **Queen Drones (Mass, 3 pheromone)** — soldiers. Escort the queen and **destroy** opposing mass
  within 70 u of her through `Prism.Damage` (a shield sheds, a super-shield deflects). 6 at rest, 12
  at Mass 10.
- **Mound Drones (Mass, 4 pheromone, needs a mound)** — workers. **Graze** opposing mass
  (`Prism.Consume`, suction toward the worker) and carry up to 3 home, where the mound lays them back
  down as the queen's prisms. **One prism in, one prism out**: a worker moves mass between domains and
  never mints it. A worker whose mound is full idles rather than eat mass it cannot store. 5 at rest.
- Prey is opposing mass (Blue counts as opposing, as for every weapon) and **never shielded or
  super-shielded mass** — a drone led to armour would hold forever (the fauna's rule, applied to the
  queen's hands). Claims stop two drones chasing one prism.

### 5.4 Mounds (SPACE — New Mound, 5 pheromone)

A cathedral spire of her own prisms founded 28 u behind her, laid through `BoostRingBuilder.LayOne`
(full-size collider from frame 0, trail membership stamped after Initialize, `Trail.Dimension =
Volume`). Rings are derived from a 3.4 u spacing and grow bottom-up; the seed is 24 prisms at rest,
48 at Space 10, and the spire holds ~150. **No health, no timer, no decay**: a mound is alive while
any of its prisms stands and falls when an active force removes the last one; its workers then
re-home or become escorts. Cap 3.

### 5.5 The other three cards

- **Autothysis (Charge, 3)** — every soldier within 260 u of the queen ruptures (≤ 12 blasts per
  play) in `AOETermiteAutothysis.prefab`: a copy of the fleet sphere blast carrying
  `TermiteAutothysisExplosionImpactorDataContainer`, whose one vessel effect is the **shared Debuff-class
  drain** `ScarabCavitationDebuffByExplosionEffect` (same verb, same price as the Dolphin cone and the
  Scarab plate — so no new drain asset and no change to `author_combat_debuff_magnitudes.py`). With no
  soldier near her she bursts herself at 0.7×. Charge is the blast diameter (60 → 120).
- **Team Crystal (Charge, 2)** — plants a crystal only her domain may collect, ahead of her. OWNER
  ONLY (`TeamCrystal.prefab` has no NetworkObject — the Dolphin seeding precedent and scope). Cap 3.
- **Teleport (Space, 2)** — she jumps to the point she was last commanded to (snapping onto her own
  mound within 120 u), clamped to her reach (220 → 440 u); with no point, straight ahead. OWNER ONLY,
  through `IVessel.SetPose`, which replicates from the owner.

## 6. HUD

The three card slots show the **face-up card's front art**, swapped when the card is played (the
flash is the face changing, the same fact the deck records). The fleet's clockwise depleting veil is
the **elixir wait** — it depletes as pheromone fills toward the card's cost — and a card blocked by a
cap (or Mound Drones with no mound) is fully veiled. The Time plate is the **pheromone tank**, a
vertical fill bound both as the view's field and as the Time icon's lockup gauge. Row placement uses
`VesselAbilityRowWirer.WireRow` (made `internal` so the setup tool runs the same code as the menu item).

## 7. Invariants touched

- **Mass is conserved** — drones remove mass only by active force (Damage/Consume, credited to the
  queen); workers deposit exactly what they grazed; a mound's seed is placed by an ability like any
  structure; nothing decays; mounds and drones are returned only at a turn boundary (the Urchin track's
  precedent) — never on a clock.
- **Continuity of existence** — drones bloom in and wither out; the command marker fades; mound prisms
  grow in through the Boost pool's bloom.
- **Every lifeform drops a crystal / economy source** — drones are not lifeforms (§5.3).
- **No vessel may move or take the controls of another** — Autothysis drains elements (a transfer),
  never a shove or a mute.
- **Shielded mass is never food** — drone prey excludes both shield tiers.

## 8. What runs where (accepted divergence)

Drones, mounds and blasts run on **every peer** (the press is replayed everywhere); each machine flies
its own drones, which can pick different targets — the same per-peer divergence the client-local food
web already accepts. Mounds are founded at the same pose everywhere; their growth can diverge with the
workers. Teleport and Team Crystal run on the owning machine only.

## 9. Collider budget

- Drones: **0** colliders.
- Mounds: ≤ 3 × ~150 ordinary Boost-pool prisms per queen (LOD-cullable like trail mass) — worst case
  ~450 per queen, reached only by play.
- Trail: a narrow pheromone trail (2.2 × 0.8 × 5 at wavelength 10).
- Crystals: ≤ 3 team crystals per queen (one sphere collider each, collected by vessels).

## 10. Follow-ups

- **Butterfly body winding** (out of scope here, measured): `ButterflyHullForm` core + antennae are
  inside-out (0/224 faces). Flip `a,c,b / b,c,d` to `a,b,c / b,d,c` and add the T8 check to
  `butterfly_hull_harness`. `ButterflyAnimation` also flaps with `-side * beat`; `Euler(0,0,θ)` sends
  +x toward +y, so check whether its "positive = up" comments hold (the Termite uses `+side * beat`).
- **L5 upgrades**: open slots, proposals in `FLEET_MAPS.md`.
- **Legacy scripts** (`CommandVesselTransformer`, `QueenDronesVesselAction`, `MoundDronesVesselAction`,
  `DeployDronesAction`, `RecallDronesAction`, `BoidController`, `TermiteDrone.prefab`) are unreferenced
  by the new prefab — a `/refactor` salvage-then-delete proposal.
- **Arcade preview window**: the commander resolves the gameplay camera; inside a card's preview window
  a click maps onto the full screen, not the window. Point-to-command there is untested.
- **AI** plays a random affordable card every 5–9 s through the same gate; it never commands (it flies
  on the stick) and it never aims a Teleport.
- Card **SFX** fields ship empty (silence, by the audio convention).

## 11. Verification status

Authored headless; **not run in the editor**. Proven offline: the hull harness (8 check groups incl.
winding); a Roslyn type-check of every new runtime file against a stub surface, with a negative
control; the standing gates; `check_elemental_economy` now holds the Termite to the contract.
**Needs the editor**: run **FrogletTools ▸ Vessels ▸ Create Termite Vessel**, then read its report
(everything UNWIRED is a real gap), then:

1. Menu freestyle: swap to the Termite from the Vessel Changer toy; the queen renders right-side-out,
   wings fold at rest and buzz when moving.
2. Click a point → she flies there and hovers; drag → the view orbits; scroll → zoom.
3. RT with >= 3 pheromone -> with no soldiers yet the queen bursts herself; the Charge card turns
   over to Team Crystal. LT -> six soldiers bloom in and destroy nearby opposing mass; the Mass card
   turns to Mound Drones (veiled: no mound yet). Watch the pheromone gauge fall and refill and the
   veils deplete as it does.
4. X -> Teleport (to the last clicked point); X again -> New Mound behind her. Now LT -> Mound
   Drones: workers graze and the spire grows. RT (Autothysis, once flipped back) ruptures soldiers.
5. Party of two: plays appear on both machines; a refused press (empty tank) appears on neither.
6. Re-run `python3 Tools/Build/element_ability_table.py Termite` after the prefab rebuild.
