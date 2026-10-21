# The Orca: the 4-seat vessel

The Orca (`VesselClassType.Orca = 20`, card **Orca · 4 seat**) is a multiplayer vessel with four seats on one hull.
One **helm** flies it and three **gunners** fight from it. The seats are always full: a person takes one, and an AI
flies each of the others (Garrett, 2026-10-20: "this vessel always has 4 players (AI if not people)").

The design is the Crew of Four Lab, rounds 3 to 5: `Docs/Studios/CrewOfFourLab.html`, written up in
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

**Unverified:** nothing has been flown in the Unity Editor yet (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, and the team
task in "Try it"). The crew runs on one machine, as the Wrasse's seat does (see Follow-ups).

## The seats

| Seat | What it does | Controls |
|---|---|---|
| **Helm** (1) | Flies the hull with two thumbs: the Sparrow's hull on its base `VesselTransformer`, at the lab's share of the Sparrow's numbers (below). **Boost**: held on the right trigger while a tank lasts; the tank refills by itself. **Shield**: 144 prisms (`DangerHemisphereConfig`'s 12 × 12) stand on a 50–70 u shell round the hull, with the forward cone left open so the helm can see and fly out. It works in two phases (round 5): (1) **carried**, the prisms ride with the hull as **super-shielded** prisms for `shieldSeconds` (4 s × Mass), a wall that only a sniper breaks; (2) **dropped**, each turns into a **danger prism where it stands** (`PrismKinds.Retheme`) and stays for `shieldLinger` (20 s). | Two sticks (or the keyboard); right trigger / right Shift to boost; left trigger / left Shift, E or pad B to shield |
| **Gunner 1–3** (2–4) | Each gunner has a first-person turret on an outrigger: G1 on a dorsal mast, G2 and G3 on port and starboard booms. The turrets are **gyro-stabilized** (lab round 4). Each seat has **a gun** (the table below) **and its own magazine** of missiles: 2 to start, 4 at most, and an omni crystal gives every seat one (`OrcaOmniCrystalEffectSO`). **Missile** launches one and hands the gunner its controls; the gunner flies it with one thumb. It bursts on contact, after 5 s, or on a second Missile press. A burst is the game's `AOEExplosion`, which spares our domain; its own vessel effect (`VesselCombatHitByMissileBlast`) takes a rival hull's petals and scores the hit. A missile **collects crystals** it flies through for the hull: elemental crystals through the hull's skimmer, as a hull contact does, and omni crystals through the race ring's sweep entry. | Turret: mouse look (the cursor locks), a pad's sticks, or W A S D. **Fire** (the gun): left mouse, Space, RT, or a finger down. **Missile**: right mouse, F, A, or a double tap. In flight: the left stick, W A S D, the mouse, or a drag; Missile again (or a tap) bursts it |

### The three guns (round 5)

| Seat | Gun | Copies | Numbers |
|---|---|---|---|
| Gunner 1 (dorsal mast) | **Sniper** | The Serpent's sniper shot (`SniperShotActionExecutor.ResolveShot`), from the turret along its aim. | **Everything hostile in the cone dies**: a 1.5° cone at least 6 u wide, 3000 u long, with unlimited pierce. **Super-shielded prisms included** (`DeactivateShields`, then a devastating `Damage`, the sanctioned order). Every rival hull in the cone loses 0.1 of each element, ejected as crystals (`ElementalTransfer.ApplyAllAuthoritative`), a scored hit (`CombatHitDrain`, `onCombatHitLanded`). The tracer is `SniperBeam.Fire`. The cooldown is **12 s**, the Serpent's. |
| Gunner 2 (port boom) | **Fast gun** | The Brittle Star's rounds: a real `Gun` firing the Sparrow's projectile from its own pool (`OrcaFastGun` on the prefab). | **30 rounds/s** while held (the Brittle Star's gun fires 90; the lab uses 30). Rounds fly at 135 u/s plus the hull's velocity for 4.5 s, slowing as they go (387 u range), and grow ×3 in flight. The round's own impact container damages prisms and scores bullet hits; our own domain's prisms are spared (`spareOwnDomain`). It **overheats after 6 s** of fire and is locked out for 5 s (`OrcaFastGun`). |
| Gunner 3 (starboard boom) | **Bomb** | The Grizzly's trigger bomb: a real `Gun` firing `GrizzlyBomb.prefab` from its own pool (`OrcaBombGun`), cruising (`Projectile.Cruises`), with `GrizzlyBombVisual.Arm`. | 90 u/s plus the hull's velocity. It bursts on a rival hull (`VesselStruck`, frozen there), on a hostile prism, on a second Fire press, or at a **4 s fuse**. The blast is `AOEGrizzlyExplosion.prefab`, radius **50 u** at rest × Space; its own vessel effect takes a rival's petals. One bomb at a time, 2.5 s between throws. **It never knocks our own hull about**: the Grizzly's self-launch is `GrizzlyTriggerBombExecutor.LaunchSelf`, which the Orca never calls, and the blast spares its owner (`AffectSelfOverride` off). |

**Changing seat.** Keys **1–4** pick a seat. The **D-pad** (the arena's `PilotSwapGesture`) steps through them, and so
does a **two-finger tap** on a touch screen. While the person holds a gunner seat:
- the hull's own `AIPilot` flies the hull;
- the person's flight input is paused (`InputController.SetPause`), as the Wrasse's navigator seat does;
- the camera looks out of the turret, or from behind the flying missile;
- the hull's chase camera shrinks to a square in the lower left, so the gunner always sees where the helm is going.

**The AI seats** are the lab's, and they press only inputs:
- **AI gunners** turn toward a target (`OrcaCrewCore.LeadAim`, with the skill's aim error and lead at the chosen
  weapon's speed).
  - They fire the **gun** when the target is within its range and the gun is ready: aligned within 4° for the sniper,
    8° for the fast gun and 15° for the bomb. A sniper steadies its aim error to 0.4 ×.
  - Otherwise they launch a **missile**: always for a crystal (only a missile collects), and beyond 60% of the gun's
    range.
  - They fly the missile at the target and burst it close enough.
- **Targets**, re-picked every 0.4 s: an omni crystal when the seat's magazine is down to one, else the nearest rival
  hull or hostile prism, else any crystal.
- **The AI helm** raises the shield when a rival Orca's missile closes on the hull.
- The default skill is **Hard** (`aiSkill`).

## How the guns are stabilized (lab round 4)

The order matters: each step removes what the one before leaves (`CREW_OF_FOUR_LAB.md` §4a).
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
per-seat magazines, the fast gun's rate (30) and heat, the bomb's fuse and cooldown, turret rates, cameras and AI skill. The flight formulas, the danger shell's count and size, the blast radius at rest and
the explosion's effect are the game's.

## Files

| File | What |
|---|---|
| `OrcaCrewCore.cs` | The rules with no engine: `OrcaFrame` (turn, follow), `OrcaTurret` (stabilized aim, held horizon, buffered Release), `OrcaMissile` (fly-by-wire step, fuse), `OrcaMagazine` (one per seat), `OrcaFastGun` (rate and heat, the lab's `rapidStep`), the shield's shape (Fibonacci sphere, forward cone open), the AI's stick and lead. Ported line for line from the lab, using `System.Numerics`. |
| `OrcaCrewConfigSO.cs` | Every number; each tooltip names its lab key. Asset: `Assets/_SO_Assets/VesselActions/OrcaCrew/OrcaCrewConfig.asset`. |
| `OrcaCrew.cs` | The crew on the hull. The seats and the swap. The helm's boost tank and shield: laid super-shielded with `BoostRingBuilder.LayOne`, posed every frame by the prism mover contract, re-themed to danger at the drop, retired by the pooled-prism identity test. The gunners' input and AI, the three guns, the missiles' contacts, crystal collection and bursts. |
| `OrcaCrewView.cs` | The gunner's turret and missile camera (with the game camera's post-processing, `OffscreenCameraSetup.AdoptGameCameraImage`), the picture-in-picture chase view, and the faceted lit pods, struts and missiles (`Graphics.RenderMesh`, `Resources/OrcaCrewLit.mat`). Each gun's pod has its own shape. |
| `UI/Controller/OrcaHUDController.cs` | The four cards: Charge counts missiles (your seat's, or all three at the helm), Space flashes on a burst, Mass shows the shield's recharge and flashes when it goes up, Time shows the tank. Plus the seat line (`seatLineTemplate`: the seat, its gun and state, its missiles; rebuilt only when one changes). |
| `ImpactEffects/.../OrcaOmniCrystalEffectSO.cs` | An Omni crystal gives every seat a missile (asset `OrcaOmniCrystalEffect`, in `OrcaImpactorDataContainer`). |
| `Assets/_Prefabs/Spacevessels/Orca.prefab`, `Tools/Build/author_orca_assets.py` | The hull and the tool that authors and registers it. The hull is the Sparrow with no Sparrow kit, two sticks and this crew, plus `OrcaFastGun` and `OrcaBombGun`: a `Gun`, `ProjectileFactory` and `ProjectilePoolManager` each, cloned from the Brittle Star's and the Grizzly's. Pools: 160 / 320 rounds (rate × flight = 135 alive) and 2 / 4 bombs. The tool imports `author_wrasse_assets.py`'s helpers and never edits the Wrasse. |
| `Assets/_Scripts/Tests/Editor/OrcaCrewCoreTests.cs` | The core's rules, stated as sentences. |
| `Tools/Build/orca_crew_lab/parity.cjs` (+ `TraceDriver.cs`, `trace.sh`) | Compiles the shipped C# core and diffs it against the lab page's own functions step by step: 600 turret steps, 300 missile steps, 60 AI sticks, the shield, 1500 fast-gun steps. Also diffs the asset against the page. Planted controls must be caught. |

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
   - **Fire** (left click, Space, RT) shoots your seat's gun:
     - seat 2, the sniper: a long beam, then 12 s of cooldown on the seat line;
     - seat 3, the fast gun: hold it until "overheated";
     - seat 4, the bomb: press to throw, press again to burst.
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
  double tap also throws a bomb. This needs a phone playtest; the cleaner shape is a card tap, which needs the hull to
  bind an action to the card's input (`R_VesselActionHandler.HasAction`).
- **Super-shielded prisms are 3× their box** (`/vessel` 4.x): the carried shell's 144 prisms look denser and wider
  than the danger shell they become. If it reads as too solid, shrink `shieldBlockScale` (a lab dial).
- **The HUD is the Sparrow's view under an Orca controller**, as the Wrasse's was. A prefab variant with the crew's four
  icons would replace the Sparrow's art.
- **The shield on a touch screen:** the left trigger has no touch equivalent yet. A tap on the Mass card would need
  that card to name an input AND the hull to bind an action to it (`AbilityCardPress` presses only bound inputs).
- **The duo-vessel system** (`cece/sharp-tesla-j0kd7z`, `/duo-vessel`) moves seat swapping into `CrewSeat`. Fold the
  Orca's seats into it when it lands.
- **`EnumIntegrityTests` count:** the Goby (`cece/sharp-tesla-j0kd7z`, 18) also raises the member count to 20. Whichever
  branch lands second makes it 21.
