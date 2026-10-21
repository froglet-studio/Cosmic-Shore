# The Orca: the 4-seat vessel

The Orca (`VesselClassType.Orca = 20`, card **Orca · 4 seat**) is a multiplayer vessel with four seats on one hull.
One **helm** flies it and three **gunners** fight from it. The seats are always full: a person takes one, and an AI
flies each of the others (Garrett, 2026-10-20: "this vessel always has 4 players (AI if not people)").

The design is the Crew of Four Lab, rounds 3 to 7: `Docs/Studios/CrewOfFourLab.html`, written up in
`Docs/Studios/CREW_OF_FOUR_LAB.md` (artifact https://claude.ai/artifact/UaJcpaXzgkza2eHSQCZwmb). Garrett asked for
the port on 2026-10-20: "stabilize the gunners guns as best you can. you can place them outside the vessel to do this.
then bring this into unity".

**Seats, not names.** "We can start referring to multiplayer vessels by their seats. this is a 4 seat vessel. that lets
players know it supports four players or brings in 4 AI" (Garrett, 2026-10-20). Two new `SO_Vessel` fields carry this:
- `SeatCount` (1 on every one-seat hull);
- `SeatLabelTemplate` (`{name} · {seats} seat`).

`CardName` uses them, so every card the Wrasse's crew names reached now reads **Orca · 4 seat**, and the Wrasse's reads
**Wrasse and Grouper · 2 seat**.

**Round 5 (Garrett, 2026-10-21):**
- Every gunner seat has **a gun and its own missiles**. Each gun is different: a long-cooldown sniper that kills
  super-shielded prisms, a fast gun like the Brittle Star's, and a bomb like the Grizzly's that never knocks the hull
  about.
- An omni crystal gives **each** seat a missile.
- The helm's shield now **rides with the hull as super-shielded prisms, then drops in place as danger prisms**.
- The gunner camera (and the Wrasse's kite camera) now draws with the game camera's post-processing (bloom, tonemap,
  volume, anti-aliasing, shadows).

**Round 6 (Garrett, 2026-10-21):**
- **Every seat is an element**, and the person's seat **lights its ability icon**: the helm is Time, the sniper Space,
  the fast gun Mass, the bomb Charge (`seatElements`). Each gunner icon shows **its own seat's missiles**; its veil is
  its seat's recharge (the boost tank, the sniper's cooldown, the fast gun's heat; the bomb has none).
- **The sniper is always scoped** (`sniperScopeFov` 22, the Serpent's full zoom).
- **The bomb has no cooldown, one at a time**: a press throws it, the next press bursts it (the AI bursts its own when a
  rival is inside the blast or the bomb has reached or passed its target). The fuse is only a 10 s safety now.
- **The fast gun writes prisms like a pen** ("like a sparrow does when stopped but less than a sparrow does, and with a
  clean precision of calligraphy"): see *The pen* below.
- **Better gunner views**: the fast gun and the bomb look **over their turret** (behind and above it, at the gun's line
  180 u out), so the rounds stream away instead of filling the eye.
- **Bespoke crosshairs**: a scope, a pen nib, a blast ring (`OrcaCrosshairGraphic`).
- **Its own hull** ("use the vessel modeling tools and really step up the vessel"): `Orca.fbx`, built by `/vessel-model`
  (see *The model* below). It replaces the drawn Sparrow and the faceted pods.

**Round 7 (Garrett, 2026-10-21, after flying round 6):**
- **Every shot flies where it is aimed.** Round 6 gave each round and bomb the hull's whole velocity, so at the Orca's
  speeds the shots slewed off the barrel and the crosshair. A shot now keeps only the hull's velocity along its aim
  (`OrcaCrewCore.ShotVelocity`, `shotInherit`): it flies exactly down the aimed line, and a forward shot still outruns
  the hull.
- **The model, the crosshair and the shot are one line.** The gunners aim and fire in `LateUpdate`, after the hull has
  moved that frame. Each turret is posed the moment it fires (`OrcaHullRig.PoseSeat`). Rounds, bombs and the sniper's
  beam leave the barrel tip, missiles leave the missile tube, and the crosshair is projected from the barrel tip along
  the aim.
- **The sniper looks over its turret** like the other gunners, at the normal field of view, with the **Serpent's pip
  scope always open** in the upper left (`SniperScopeOverlay`, the eyepiece a Serpent opens with the left trigger). The
  scope looks from the barrel tip down the aim at 22°, and its cone reticle also sits over the main view.

**Round 8 (Garrett, 2026-10-21, after flying round 7):**
- **The minigun's odd angle was a rest-pose bug.** `OrcaHullRig` took each turret's rest from its bones as found, so
  after a re-bind (an AI crew at spawn, then a person boarding) it took an AIMED turret as rest and drew it at that
  angle off its aim for good. Rest now comes from the skin's bind poses, which no pose can change, and the bones go
  back to rest when the crew lets go.
- **The model is a hovering assembly** ("free floating parts that are associated and animated together but clearly
  distinct parts of a hovering assembly"): the core, a floating dorsal fin, a chain of floating outrigger plates out to
  each side turret, floating engines and flukes, every part on its own bone (see *The model*). The rig animates them
  together: the outrigger plates swing as a chain between the hull and the lagged turret frame, every part bobs on its
  own phase, and the engines lean into turns.
- **The jets burn from the model's engine mouths.** The prefab's six `VesselJet`s name the hidden Sparrow's tail bones;
  the rig remounts each on the mouth on its side (`VesselJet.Remount`, new), keeping where its plume points.
- **How vessel models are made changed with it** (`/vessel-model` SKILL 0.5, LESSONS 49, gate
  `assembly_report.py --floating`). Every from-scratch hull welds its parts; their rebuild is `T-20261021-ab48`.

**Unverified:** nothing has been flown in the Unity Editor yet (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, and the team
task in "Try it"). The crew runs on one machine, as the Wrasse's seat does (see Follow-ups).

## The seats

| Seat | What it does | Controls |
|---|---|---|
| **Helm** (1) | Flies the hull with two thumbs: the Sparrow's hull on its base `VesselTransformer`, at the lab's share of the Sparrow's numbers (below). **Boost**: held on the right trigger while a tank lasts; the tank refills by itself. **Shield**: 144 prisms (`DangerHemisphereConfig`'s 12 × 12) stand on a 50–70 u shell round the hull, with the forward cone left open so the helm can see and fly out. It works in two phases (round 5): (1) **carried**, the prisms ride with the hull as **super-shielded** prisms for `shieldSeconds` (4 s × Mass), a wall that only a sniper breaks; (2) **dropped**, each turns into a **danger prism where it stands** (`PrismKinds.Retheme`) and stays for `shieldLinger` (20 s). | Two sticks (or the keyboard); right trigger / right Shift to boost; left trigger / left Shift, E or pad B to shield |
| **Sniper, Fast gun, Bomb** (2–4) | Each gunner has a turret on an outrigger: the sniper (Space) on a dorsal mast, the fast gun (Mass) on the port boom and the bomb (Charge) on the starboard boom. The turrets are **gyro-stabilized** (lab round 4). Each seat has **a gun** (the table below) **and its own magazine** of missiles: 2 to start, 4 at most, and an omni crystal gives every seat one (`OrcaOmniCrystalEffectSO`). **Missile** launches one and hands the gunner its controls; the gunner flies it with one thumb. It bursts on contact, after 5 s, or on a second Missile press. A burst is the game's `AOEExplosion`, which spares our domain; its own vessel effect (`VesselCombatHitByMissileBlast`) takes a rival hull's petals and scores the hit. A missile **collects crystals** it flies through for the hull: elemental crystals through the hull's skimmer, as a hull contact does, and omni crystals through the race ring's sweep entry. | Turret: mouse look (the cursor locks), a pad's sticks, or W A S D. **Fire** (the gun): left mouse, Space, RT, or a finger down. **Missile**: right mouse, F, A, or a double tap. In flight: the left stick, W A S D, the mouse, or a drag; Missile again (or a tap) bursts it |

### The three guns (round 5; round 6 made each an element)

| Seat | Gun | Copies | Numbers |
|---|---|---|---|
| Sniper · Space (dorsal mast) | **Sniper**, with the pip scope always open | The Serpent's sniper shot (`SniperShotActionExecutor.ResolveShot`), from the turret along its aim. | **Everything hostile in the cone dies**: a 1.5° cone at least 6 u wide, 3000 u long, with unlimited pierce. **Super-shielded prisms included** (`DeactivateShields`, then a devastating `Damage`, the sanctioned order). Every rival hull in the cone loses 0.1 of each element, ejected as crystals (`ElementalTransfer.ApplyAllAuthoritative`), a scored hit (`CombatHitDrain`, `onCombatHitLanded`). The tracer is `SniperBeam.Fire`. The cooldown is **12 s**, the Serpent's, on the Space icon's veil and as a ring round the scope. |
| Fast gun · Mass (port boom) | **Fast gun**, and its pen | The Brittle Star's rounds: a real `Gun` firing the Sparrow's projectile from its own pool (`OrcaFastGun` on the prefab). | **30 rounds/s** while held (the Brittle Star's gun fires 90; the lab uses 30). Rounds fly at 135 u/s plus the hull's velocity for 4.5 s, slowing as they go (387 u range), and grow ×3 in flight. The round's own impact container damages prisms and scores bullet hits; our own domain's prisms are spared (`spareOwnDomain`). It **overheats after 6 s** of fire and is locked out for 5 s (`OrcaFastGun`); the heat is the Mass icon's veil. Up to 10 rounds a second also **lay prisms like a pen** (below). |
| Bomb · Charge (starboard boom) | **Bomb**, one at a time | The Grizzly's trigger bomb: a real `Gun` firing `GrizzlyBomb.prefab` from its own pool (`OrcaBombGun`), cruising (`Projectile.Cruises`), with `GrizzlyBombVisual.Arm`. | 90 u/s plus the hull's velocity. **No cooldown, one bomb at a time** (round 6): a Fire press throws it and the next press bursts it, so the gunner must burst a bomb to throw another. It also bursts on a rival hull (`VesselStruck`, frozen there) or a hostile prism, and at a 10 s safety. The blast is `AOEGrizzlyExplosion.prefab`, radius **50 u** at rest × Space; its own vessel effect takes a rival's petals. **It never knocks our own hull about**: the Grizzly's self-launch is `GrizzlyTriggerBombExecutor.LaunchSelf`, which the Orca never calls, and the blast spares its owner (`AffectSelfOverride` off). |

### The pen (round 6)

The fast gun "shoots prisms like a Sparrow does when stopped, but less, and with a clean precision of calligraphy". The
Sparrow's turret stance makes every round a prism that stops where its flight ends (`FullAutoBlockShootActionSO`). The
Orca's pen makes **at most 10 rounds a second** (`fastGunPenRate`; a third of the rounds) leave a prism where they reach
the pen's **paper**, 120 u out (`fastGunPenReach`), and lays those prisms as **strokes** (`OrcaPen`, the lab's `penLay`):
- one prism every **5 u** along the stroke (`fastGunPenSpacing`, the Sparrow turret prism's length), each laid **along**
  the stroke, so a sweep reads as one unbroken line. Prisms are 0.8 × 0.5 × 4.6 (the Sparrow's turret prism);
- a stroke's first landing waits for the second, so it lies along the stroke too;
- a landing nearer than the spacing lays nothing (a pen never blots), and a quick sweep fills up to 2 prisms per landing;
- a longer jump, or a 0.35 s pause, lifts the pen. A stroke with one landing is a dot, laid at the nib's 45°;
- the hull keeps 150 pen prisms (`fastGunPenCap`); the oldest goes first.

Where and when a pen round lays is fixed at the press, on the round's own eased path (`OrcaPen.Landing`), so the stroke
is the gunner's sweep whatever the round meets. The prisms are ordinary prisms of our domain, one `Trail` per stroke.
Measured in the lab (12 seeds, I2, Hard): 105–187 pen prisms a minute.

**Why the paper is 120 u, not the round's end.** At 387 u a turret sweep moves a landing ~100 u between pen rounds, so
every landing started a new stroke and the pen drew loose sticks. At 120 u a deliberate sweep is a steady stroke.

### The views and the crosshairs (round 6)

| Seat | View | Crosshair |
|---|---|---|
| Sniper | Over the turret: 12 u behind, 6 u up (round 7), plus the Serpent's **pip scope always open** in the upper left (`sniperScopeFov` 22, from the barrel tip) | The Serpent's own two reticles (`SniperScopeOverlay`): the cone at its true size with posts and a dot inside the pip, the recharge ring round it, and the same cone over the main view at the shot's point |
| Fast gun | Over the turret: 14 u behind, 8 u up, looking at the gun's line 180 u out | A pen nib: a dot, a slanted 45° stroke, a lime tip while the next round is a pen round, the heat as an arc under it |
| Bomb | Over the turret: 22 u behind, 10 u up | A dashed ring the size of the blast where the view meets the gun's line; with a bomb in the air, the ring round the live bomb, pulsing, and "BURST" |
| Any, flying a missile | Behind the missile | A small ring |

The crosshair sits where the gun's line meets the view: projected from the barrel tip along the aim, not the screen
centre (round 7), on a screen-space canvas under the HUD. Its words and colours are config fields.

**Changing seat.** Keys **1–4** pick a seat. The **D-pad** (the arena's `PilotSwapGesture`) steps through them, and so
does a **two-finger tap** on a touch screen. While the person holds a gunner seat:
- the hull's own `AIPilot` flies the hull;
- the person's flight input is paused (`InputController.SetPause`), as the Wrasse's navigator seat does;
- the camera looks down the scope or over the turret, or from behind the flying missile;
- the hull's chase camera shrinks to a square in the lower left, so the gunner always sees where the helm is going.

**The AI seats** are the lab's, and they press only inputs:
- **AI gunners** turn toward a target (`OrcaCrewCore.LeadAim`, with the skill's aim error and lead at the chosen
  weapon's speed).
  - They fire the **gun** when the target is within its range and the gun is ready: aligned within 4° for the sniper,
    8° for the fast gun and 15° for the bomb. A sniper steadies its aim error to 0.4 ×. The bomb seat bursts its bomb
    when a rival hull is inside the blast, or the bomb has reached (half the blast radius) or passed its target.
  - Otherwise they launch a **missile**: always for a crystal (only a missile collects), and beyond 60% of the gun's
    range.
  - They fly the missile at the target and burst it close enough.
- **Targets**, re-picked every 0.4 s: an omni crystal when the seat's magazine is down to one, else the nearest rival
  hull or hostile prism, else any crystal.
- **The AI helm** raises the shield when a rival Orca's missile closes on the hull.
- The default skill is **Hard** (`aiSkill`).

## The model (round 6)

`Assets/_Models/Vessel Models/Orca.fbx`, built by `/vessel-model` from `Tools/Build/vessel_model/specs/orca.py` (dossier
`Docs/VesselModels/Orca.md`, contact sheets `Docs/VesselModels/sheets/orca_after.jpg` and `orca_turrets.jpg`). A
spaceship inspired by the killer whale, built (round 8) as a **hovering assembly** like the artist hulls: a deep
gunmetal hull core with domain armour plates and the eye patches as sensor lenses; a dorsal fin blade hovering 1.6 u
over the back with the sniper hovering above it; on each side a chain of three floating paddle plates out to a pod
nacelle, with the fast gun (port) or the bomb (starboard) hovering over its pod; two engine pods with glowing bells
between two floating fluke blades. Every part is its own closed shell on its own bone; nothing is mated. 17 / 17 parts
float 1.55-2.26 % of the diagonal clear (`assembly_report.py --floating`). 49 × 24 × 37 u, 16,280 triangles.
- **Three bespoke turrets**, each a YAW / PITCH / MUZZLE bone chain at exactly the code's mounts
  (`OrcaCrewCore.Mounts` × `hullRadius`): the sniper a long railgun with coil rings and a scope, the fast gun a
  six-barrel rotary "pen", the bomb a fat mortar drum. Each also carries a missile tube.
- **The floating parts' bones** (all children of `hull`): `fin`, `wing.L.1..3` / `wing.R.1..3`, `pod.L` / `pod.R`,
  `engine.L` / `engine.R` (with `jet.L.mouth` / `jet.R.mouth`), `fluke.L` / `fluke.R`. The turret yaw bones are
  children of `hull` too.
- **Four element morphs**: Charge 4.6% (the turrets swell), Mass 4.3% (the hull bulks; the plate chain rides out so its
  gaps stay open), Space 9.3% (the barrels and flukes reach), Time 5.6% (the nose sharpens, the chain sweeps back).
  Slots: Domain, Body, Window, Engine.
- **Nested by `author_orca_assets.py`** (`model_retrofit`, the Grouper's pattern) under the root as `Orca`: it is the
  ship geometry the domain paints (`_shipGeometries`, slot 0), and the Sparrow's mesh is switched off. The Sparrow's
  bones, jets, colliders and Animator stay, so the hull still flies on them.
- **`OrcaHullRig`** finds the turret bones by name. Every frame it stands each turret at its lagged mount and points it
  along its seat's stabilized aim, relative to the bone's rest. Rounds, bombs and the sniper's beam leave from the
  barrel's tip; the scope looks from it. Without the bones the view draws its faceted pods, so the crew never depends on
  the art.

## How the guns are stabilized (lab round 4)

The order matters: each step removes what the one before leaves (`CREW_OF_FOUR_LAB.md` §4c).
1. **The aim lives in the world** (`OrcaTurret.Aim`). The gunner's input turns it; the hull's turns never reach it.
2. **The horizon is held, then eased** (`OrcaTurret.HoldHorizon`). The view keeps its own up, square to the aim, and
   drifts to the hull's up over `horizonSmooth` (3 s). A roll does not spin the picture, and a long inverted run still
   ends with the gunner's up matching the ship's.
3. **The mounts sit outside the hull**, on outriggers (`OrcaCrewCore.Mounts`, in hull radii). This keeps the view clear
   of the ship and the launch point clear of it.
4. **The outriggers ride a lagged frame** (`OrcaFrame.Follow`, `mountSmooth` 0.35 s), so a snap turn swings the eye
   point through a soft arc. The aim is not lagged.

Measured in the lab, 12 seeds, every game: a gunner's view sways **3–6 °/s** unsteered, against **47–54 °/s** for the
round-3 hull-locked turrets. The AI's points are unchanged.

## Where the numbers come from

**The lab owns the numbers.** `OrcaCrewConfig.asset` was seeded once from the lab's SHIPPED block by
`Tools/Build/author_orca_assets.py`, and `Tools/Build/orca_crew_lab/parity.cjs` holds it there key by key.
- To change a number, tune it in the lab, record the decision, then re-seed. Never tune these fields in Unity.
- The flight and the shield shell are the game's own numbers.

**The element channels.** The lab scales four numbers by an element: an element's effect at level 15 is 1 + k. That is
the same straight line an `ElementalFloat` draws through its rest value and its level-10 value, with
level-10 = rest × (1 + 2k/3). So each channel is an `ElementalFloat`, read at use time:

| Lab | Field | Rest → level 10 |
|---|---|---|
| `boostRefill` × `kTime` 0.6 | `boostRefill` (Time) | 0.25 → 0.35 tank s/s |
| `bombBlastR` × `kSpace` 0.8 | `bombBlastRadius` (Space) | 50 → 76.7 u |
| `shieldTime` × `kMass` 0.8 | `shieldSeconds` (Mass) | 4 → 6.13 s |
| `blastR` × `kSpace` 0.8 | `blastRadius` (Space) | 30 → 46 u (`AOEExplosion` MaxScale twice it) |
| `kCharge` 0.6 | `missileScale` (Charge), on missile speed and turn | 1 → 1.4 |

**The hull** is the lab's share of the Sparrow's numbers. `author_orca_assets.py` writes these values once, over the
Sparrow's untouched ones, so a value tuned in Unity afterwards stays:

| Field | Sparrow | Orca (lab) |
|---|---|---|
| `DefaultMinimumSpeed` | 10 | 8.5 (`hullSpeedMul` 0.85) |
| `DefaultThrottleScaler` | 25 | 21.25 |
| `PitchScaler`, `YawScaler` | 80 | 44 (`hullTurnMul` 0.55) |
| `RollScaler` | 30 | 16.5 |
| `RotationThrottleScaler` | 0.1 | 0.055 |

The roll will feel slow: the Wrasse found 30 "especially slow" on two sticks and went to 120. That is a feel question
for the first playtest.

**From the game's own weapons** (the lab took them from there): the sniper's cooldown, cone, range, strip and beam
(`SniperShotAction.asset`); the fast gun's round speed, flight time and growth (`BrittleStarConfig`); the bomb's speed and
blast (`GrizzlyTriggerBombConfig`, mid-charge). The parity gate also checks `sniperStripPerElement` × 40 = the lab's
`sniperPetals` (4).

**Lab only** (no game source): the boost tank, shield timing, opening and linger, missile speed, turn and fuse, the
per-seat magazines, the fast gun's rate (30), heat and pen, the bomb's safety fuse, turret rates, cameras, crosshairs and AI skill. The flight formulas, the danger shell's count and size, the blast radius at rest and
the explosion's effect are the game's.

## Files

| File | What |
|---|---|
| `OrcaCrewCore.cs` | The rules with no engine: `OrcaFrame` (turn, follow), `OrcaTurret` (stabilized aim, held horizon, buffered Release), `OrcaMissile` (fly-by-wire step, fuse), `OrcaMagazine` (one per seat), `OrcaFastGun` (rate and heat, the lab's `rapidStep`), `OrcaPen` (the pen's strokes and where a pen round lands, the lab's `penLay` / `penFlush` / `penLanding`), the shield's shape (Fibonacci sphere, forward cone open), the AI's stick and lead. Ported line for line from the lab, using `System.Numerics`. |
| `OrcaCrewConfigSO.cs` | Every number; each tooltip names its lab key. Asset: `Assets/_SO_Assets/VesselActions/OrcaCrew/OrcaCrewConfig.asset`. |
| `OrcaCrew.cs` | The crew on the hull. The seats and the swap. The helm's boost tank and shield: laid super-shielded with `BoostRingBuilder.LayOne`, posed every frame by the prism mover contract, re-themed to danger at the drop, retired by the pooled-prism identity test. The gunners' input and AI, the three guns (the bomb with no cooldown and the AI's burst; the pen's queued landings, laid with `BoostRingBuilder.LayOne`), the missiles' contacts, crystal collection and bursts. Readers for the HUD: `SeatElement`, `SeatVeil01`, `BurstAt`, `MissilesAt`, `PenReady`. |
| `OrcaCrewView.cs` | The gunner's camera (the scoped sniper, the over-the-turret fast gun and bomb, the missile) and the crosshair canvas (with the game camera's post-processing, `OffscreenCameraSetup.AdoptGameCameraImage`), the picture-in-picture chase view, and the faceted lit pods, struts and missiles (`Graphics.RenderMesh`, `Resources/OrcaCrewLit.mat`). Each gun's pod has its own shape. |
| `OrcaHullRig.cs` | The model posed by the crew: the three turrets aimed (rest read from the skin's bind poses, stood at the lagged mounts), the floating plates swung as a chain between the hull and the lagged turret frame, every part's hover bob, the engines' lean, and the prefab's jets remounted on the engine mouths. Barrel tips and missile tubes are where the guns fire from. |
| `Assets/_Models/Vessel Models/Orca.fbx`, `Tools/Build/vessel_model/specs/orca.py` | The hull model and its spec (`build.py specs/orca.py --check` is its drift gate). |
| `UI/View/OrcaCrosshairGraphic.cs` | The bespoke crosshairs as generated uGUI geometry (nib, bomb ring, missile ring), feathered and haloed. The sniper's is the Serpent's `SniperScopeOverlay`, which gained an explicit-eye `Tick` (eye, rotation, flight camera) for it. |
| `UI/Controller/OrcaHUDController.cs` | The four cards are the four seats (round 6): the person's seat's card is lit (re-asserted every frame), each card's veil is its seat's recharge, each gunner card counts its own seat's missiles, and a card flashes when its seat's missile or bomb bursts (Time also when the shield goes up). Plus the seat line (`seatLineTemplate`: the seat, its gun and state, its missiles; rebuilt only when one changes). |
| `ImpactEffects/.../OrcaOmniCrystalEffectSO.cs` | An Omni crystal gives every seat a missile (asset `OrcaOmniCrystalEffect`, in `OrcaImpactorDataContainer`). |
| `Assets/_Prefabs/Spacevessels/Orca.prefab`, `Tools/Build/author_orca_assets.py` | The hull and the tool that authors and registers it. The hull is the Sparrow with no Sparrow kit, two sticks and this crew, plus `OrcaFastGun` and `OrcaBombGun`: a `Gun`, `ProjectileFactory` and `ProjectilePoolManager` each, cloned from the Brittle Star's and the Grizzly's. Pools: 160 / 320 rounds (rate × flight = 135 alive) and 2 / 4 bombs. The tool imports `author_wrasse_assets.py`'s helpers and never edits the Wrasse. |
| `Assets/_Scripts/Tests/Editor/OrcaCrewCoreTests.cs` | The core's rules, stated as sentences. |
| `Tools/Build/orca_crew_lab/parity.cjs` (+ `TraceDriver.cs`, `trace.sh`) | Compiles the shipped C# core and diffs it against the lab page's own functions step by step: 600 turret steps, 300 missile steps, 60 AI sticks, the shield, 1500 fast-gun steps, 400 pen landings, 21 pen reaches and 40 shot velocities. Also diffs the asset against the page. Planted controls must be caught. |

**Registrations:**
- `SO_Class_Orca`: owned from the start, `SeatCount` 4, in both class lists.
- The Vessel Prefab Container and `DefaultNetworkPrefabs`, with a fresh Netcode hash.
- `ElementalAbilityMaps/Orca`: four passive cards, no level-5 upgrades yet.
- Four `CrystalHullFusionConfig` entries, the Sparrow's.
- `ToyVesselRoster.Default` and `EnumIntegrityTests`, in code.

## Try it

**In the Toybox:**
1. Open the Toybox (menu freestyle) and fly into the **Vessel Changer**. Fly the **Orca · 4 seat** station.
2. You hold the **helm**. Fly with two thumbs.
   - The right trigger (right Shift) boosts until the Time card's tank runs dry.
   - The left trigger (left Shift, E, pad B) rings the hull in white super-shielded prisms, open ahead. After ~4 s they
     turn into danger prisms where they stand and stay ~20 s. The Mass card shows the recharge.
   - Three AI gunners fire their guns and missiles at whatever is near.
3. Press **2**, **3** or **4** (or the D-pad) to take a gunner seat. You look out of the turret, the hull's view drops
   to the lower left, and the AI flies the hull.
   - Aim with the mouse or a stick; turn the hull under you and the aim holds.
   - Your seat's icon in the ability row lights (2 Space, 3 Mass, 4 Charge) and shows your missiles.
   - **Fire** (left click, Space, RT) shoots your seat's gun:
     - seat 2, the sniper: you look over the turret, and the pip scope stays open in the upper left; a long beam, then
       12 s of cooldown round the scope and on the Space icon;
     - seat 3, the fast gun: you look over the turret; hold it and sweep slowly: a clean line of prisms draws itself
       120 u out. Hold until "overheated";
     - seat 4, the bomb: press to throw, watch the ring round the bomb, press again to burst it, then throw the next.
   - **Missile** (right click, F, A) launches one of your seat's missiles. Fly it with W A S D or the stick; Missile
     again bursts it.
   - Fly a missile through an omni crystal: every seat's count goes up by one.
4. Press **1** to take the helm back.

## Where the game differs from the lab

- **The AI helm is the hull's own `AIPilot`**, not the lab's helm AI, so it flies the game's AI routes. The crew adds
  only the shield. (The lab's helm AI gained a throttle in round 5; the game's `AIPilot` has its own.)
- **The fast gun's and the bomb's rounds are the game's real projectiles**, so their contacts, damage and scoring are the
  game's: the lab modelled a bullet as 0.4 of a petal and a bomb as bursting on any hostile prism. In the game a bomb
  also bursts on a hostile prism (the crew sweeps its path, as for missiles).
- **A sniper hit on a hull** is the Serpent's strip: 0.1 of every element ejected. The lab counts that as 4 petals.
- **Missiles collide with rival hulls, hostile prisms (`PrismSpatialIndex.QuerySegment`), and rival Orcas' missiles.**
  The lab also burst missiles on rival shields; here a rival's shield is hostile prisms, so the same rule holds.
- **The burst is the real `AOEExplosion`**, timed by the lab's `blastSeconds` (0.5 s). It decides what a caught hull
  loses (`VesselCombatHitByMissileBlast`), not the lab's 2 petals.
- **The hull keeps two Sparrow element channels:** Space scales the trail (`Scale` 20 → 40) and Time the boost speed
  (`BoostSpeedMultiplier` 1 → 1.5). `element_ability_table.py Orca` lists them beside the crew's.

## Gates

```
node Tools/Build/orca_crew_lab/parity.cjs          # core vs page, asset vs page, planted controls
python3 Tools/Build/author_orca_assets.py --check  # the hull, the crew's assets, the registrations
python3 Tools/Build/author_orca_assets.py --self-test
python3 Tools/Build/vessel_model/build.py Tools/Build/vessel_model/specs/orca.py --check   # the model's drift gate
python3 Tools/Build/vessel_model/assembly_report.py "Assets/_Models/Vessel Models/Orca.fbx" --floating fin,wing.L.1,wing.L.2,wing.L.3,wing.R.1,wing.R.2,wing.R.3,pod.L,pod.R,engine.L+jet.L.mouth,engine.R+jet.R.mouth,fluke.L,fluke.R,SniperYaw+SniperPitch+SniperMuzzle+SniperMissile,FastGunYaw+FastGunPitch+FastGunMuzzle+FastGunMissile,BombYaw+BombPitch+BombMuzzle+BombMissile
bash Tools/Build/unity_refcompile/run.sh           # the player compile
python3 Tools/Build/element_ability_table.py Orca  # four cards, four channels
```

## Follow-ups

- **Networking.** The crew runs on one machine: the local person's hull, or an AI hull on the authority. Missiles, the
  shield's poses and the AI gunners are that machine's. The burst is an `AOEExplosion` spawned there only, and prism
  destruction does not replicate. A second human on another machine needs the seats replicated, plus a decision on
  which machine decides the bursts (the Wrasse's `T-20261017-b06e` is the same question).
- **Level-5 upgrades:** none is designed. The map's four `UpgradeLabel`s are empty, waiting on Garrett (vessel skill §3,
  the design-approval gate).
- **Sounds:** all eight `EventReference` slots ship empty: launch, burst, shield, omni refill, empty magazine, sniper
  shot, bomb throw, bomb burst. (The fast gun's rounds and the bomb carry their prefabs' own sounds, if any.)
- **Touch:** a finger down fires the gun and a double tap launches a missile. On the bomb seat, the first tap of a
  double tap also throws (or bursts) a bomb. This needs a phone playtest; the cleaner shape is a card tap, which needs the hull to
  bind an action to the card's input (`R_VesselActionHandler.HasAction`).
- **Super-shielded prisms are 3× their box** (`/vessel` 4.x): the carried shell's 144 prisms look denser and wider
  than the danger shell they become. If it reads as too solid, shrink `shieldBlockScale` (a lab dial).
- **The HUD is the Sparrow's view under an Orca controller**, as the Wrasse's was. A prefab variant with the crew's four
  icons would replace the Sparrow's art (the seats are elements now, so each card could carry its gun's own art).
- **Fit the hit box, jets, skimmer and chase camera to the model.** They are still the Sparrow's, sized for a 12 u hull;
  the Orca is 36 u long and 49 u across. The jets burn inside the hull (the model's engine mouths are bones
  `jet.L.mouth` / `jet.R.mouth` at z -14.75), and the convention puts the chase camera ~113 u back, against the lab's 50
  (a lab dial, `camBack`). The Grouper has the same task (`T-20261020-a9f5`).
- **Space moves the barrel tips** (the sniper's 6.1 u at full Space) but the muzzle bones stay at rest, so at high Space a
  round leaves from inside the longer barrel. `BrittleStarHullModel.ReadMouthTravel` reads that offset if it matters.
- **Anchorage scores lower in round 6** (73 ± 16 against round 5's 119.5 ± 7.6, 12 seeds): the no-cooldown bomb trades
  about twice the hits both ways, and every crew's blasts break the other's trail. Mass-game balance is a lab question.
- **The shield on a touch screen:** the left trigger has no touch equivalent yet. A tap on the Mass card would need
  that card to name an input AND the hull to bind an action to it (`AbilityCardPress` presses only bound inputs).
- **The duo-vessel system** (`cece/sharp-tesla-j0kd7z`, `/duo-vessel`) moves seat swapping into `CrewSeat`. Fold the
  Orca's seats into it when it lands.
- **`EnumIntegrityTests` count:** the Goby (`cece/sharp-tesla-j0kd7z`, 18) also raises the member count to 20. Whichever
  branch lands second makes it 21.
