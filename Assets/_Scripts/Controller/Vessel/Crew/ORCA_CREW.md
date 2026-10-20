# The Orca: the 4-seat vessel

The Orca (`VesselClassType.Orca = 20`, card **Orca · 4 seat**) is a multiplayer vessel with four seats on one hull.
One **helm** flies it and three **gunners** fight from it. The seats are always full: a person takes one, and an AI
flies each of the others (Garrett, 2026-10-20: "this vessel always has 4 players (AI if not people)").

The design is the Crew of Four Lab, rounds 3 and 4: `Docs/Studios/CrewOfFourLab.html`, written up in
`Docs/Studios/CREW_OF_FOUR_LAB.md` (artifact https://claude.ai/artifact/UaJcpaXzgkza2eHSQCZwmb). Garrett asked for
the port on 2026-10-20: "stabilize the gunners guns as best you can. you can place them outside the vessel to do this.
then bring this into unity".

**Seats, not names.** "We can start referring to multiplayer vessels by their seats. this is a 4 seat vessel. that lets
players know it supports four players or brings in 4 AI" (Garrett, 2026-10-20). Two new `SO_Vessel` fields carry this:
- `SeatCount` (1 on every one-seat hull);
- `SeatLabelTemplate` (`{name} · {seats} seat`).

`CardName` uses them, so every card the Wrasse's crew names reached now reads **Orca · 4 seat**, and the Wrasse's reads
**Wrasse and Grouper · 2 seat**.

**Unverified:** nothing has been flown in the Unity Editor yet (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, and the team
task in "Try it"). The crew runs on one machine, as the Wrasse's seat does (see Follow-ups).

## The seats

| Seat | What it does | Controls |
|---|---|---|
| **Helm** (1) | Flies the hull with two thumbs: the Sparrow's hull on its base `VesselTransformer`, at the lab's share of the Sparrow's numbers (below). **Boost**: held on the right trigger while a tank lasts; the tank refills by itself. **Shield**: on the left trigger, danger prisms (`PrismKind.Danger`) stand on a shell round the hull and ride with it. The forward cone is left open so the helm can see and fly out. The prisms are in the asset's 12 × 12 count at a 50–70 u radius, `DangerHemisphereConfig`'s numbers. | Two sticks (or the keyboard); right trigger / right Shift to boost; left trigger / left Shift to shield |
| **Gunner 1–3** (2–4) | Each gunner has a first-person turret on an outrigger: G1 on a dorsal mast, G2 and G3 on port and starboard booms. The turrets are **gyro-stabilized** (lab round 4). **Release** launches a missile from the hull's **shared magazine** and hands the gunner its controls. The gunner flies the missile with one thumb. It bursts on contact, after 5 s, or on a second Release. A burst is the game's `AOEExplosion`, which spares our domain; its own vessel effect (`VesselCombatHitByMissileBlast`) takes a rival hull's petals and scores the hit. A missile **collects crystals** it flies through for the hull. Elemental crystals go through the hull's skimmer, as a hull contact does. Omni crystals go through the race ring's sweep entry and add 3 missiles (`OrcaOmniCrystalEffectSO`). | Turret: mouse look (the cursor locks), a pad's sticks, or W A S D. Release: Space, left mouse, A or the right trigger, or a tap. Missile: the left stick, W A S D, the mouse, or a drag |

**Changing seat.** Keys **1–4** pick a seat. The **D-pad** (the arena's `PilotSwapGesture`) steps through them, and so
does a **two-finger tap** on a touch screen. While the person holds a gunner seat:
- the hull's own `AIPilot` flies the hull;
- the person's flight input is paused (`InputController.SetPause`), as the Wrasse's navigator seat does;
- the camera looks out of the turret, or from behind the flying missile;
- the hull's chase camera shrinks to a square in the lower left, so the gunner always sees where the helm is going.

**The AI seats** are the lab's, and they press only inputs:
- **AI gunners** turn toward a target (`OrcaCrewCore.LeadAim`, with the skill's aim error and lead). They release
  once they are aligned within 15° and have held aim for the skill's reaction time. They fly the missile at the target
  and burst it close enough.
- **Targets**, re-picked every 0.4 s: an omni crystal when the magazine runs low, else the nearest rival hull or
  hostile prism, else any crystal.
- **The reserve:** AI gunners leave one missile in the magazine for a human gunner (`aiReserve`).
- **The AI helm** raises the shield when a rival Orca's missile closes on the hull.
- The default skill is **Hard** (`aiSkill`).

## How the guns are stabilized (lab round 4)

The order matters: each step removes what the one before leaves (`CREW_OF_FOUR_LAB.md` §4).
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

**Lab only** (no game source): the boost tank, shield timing and opening, missile speed, turn and fuse, magazine,
turret rates, cameras and AI skill. The flight formulas, the danger shell's count and size, the blast radius at rest and
the explosion's effect are the game's.

## Files

| File | What |
|---|---|
| `OrcaCrewCore.cs` | The rules with no engine: `OrcaFrame` (turn, follow), `OrcaTurret` (stabilized aim, held horizon, buffered Release), `OrcaMissile` (fly-by-wire step, fuse), `OrcaMagazine`, the shield's shape (Fibonacci sphere, forward cone open), the AI's stick and lead. Ported line for line from the lab, using `System.Numerics`. |
| `OrcaCrewConfigSO.cs` | Every number; each tooltip names its lab key. Asset: `Assets/_SO_Assets/VesselActions/OrcaCrew/OrcaCrewConfig.asset`. |
| `OrcaCrew.cs` | The crew on the hull: the seats and the swap, the helm's boost tank and shield (laid with `BoostRingBuilder.LayOne`, posed every frame by the prism mover contract, retired by the pooled-prism identity test), the gunners (input and AI), the missiles' contacts, crystal collection and bursts. |
| `OrcaCrewView.cs` | The gunner's turret and missile camera, the picture-in-picture chase view, and the faceted lit pods, struts and missiles (`Graphics.RenderMesh`, `Resources/OrcaCrewLit.mat`). |
| `UI/Controller/OrcaHUDController.cs` | The four cards: Charge counts the magazine, Space flashes on a burst, Mass shows the shield's recharge and flashes when it goes up, Time shows the tank. Plus the seat line (`seatLineTemplate`). |
| `ImpactEffects/.../OrcaOmniCrystalEffectSO.cs` | An Omni crystal refills the magazine (asset `OrcaOmniCrystalEffect`, in `OrcaImpactorDataContainer`). |
| `Assets/_Prefabs/Spacevessels/Orca.prefab`, `Tools/Build/author_orca_assets.py` | The hull (the Sparrow with no Sparrow kit, two sticks and this crew) and the tool that authors and registers it. The tool imports `author_wrasse_assets.py`'s helpers and never edits the Wrasse. |
| `Assets/_Scripts/Tests/Editor/OrcaCrewCoreTests.cs` | The core's rules, stated as sentences. |
| `Tools/Build/orca_crew_lab/parity.cjs` (+ `TraceDriver.cs`, `trace.sh`) | Compiles the shipped C# core and diffs it against the lab page's own functions step by step: 600 turret steps, 300 missile steps, 60 AI sticks, the shield. Also diffs the asset against the page. Planted controls must be caught. |

**Registrations:**
- `SO_Class_Orca`: owned from the start, `SeatCount` 4, in both class lists.
- The Vessel Prefab Container and `DefaultNetworkPrefabs`, with a fresh Netcode hash.
- `ElementalAbilityMaps/Orca`: four passive cards, no level-5 upgrades yet.
- Four `CrystalHullFusionConfig` entries, the Sparrow's.
- `ToyVesselRoster.Default` and `EnumIntegrityTests`, in code.

## Try it

**In the Toybox:**
1. Open the Toybox (menu freestyle) and fly into the **Vessel Changer**. Fly the **Orca · 4 seat** station.
2. You hold the **helm**. Fly with two thumbs. The right trigger (right Shift) boosts until the Time card's tank runs
   dry. The left trigger (left Shift) rings the hull in danger prisms, open ahead; the Mass card shows the recharge.
   Three AI gunners fire missiles at whatever is near: rival hulls, hostile prisms, and omni crystals when the magazine
   (the Charge card's number) runs low.
3. Press **2**, **3** or **4** (or the D-pad) to take a gunner seat. You look out of the turret, the hull's view drops
   to the lower left, and the AI flies the hull.
   - Aim with the mouse or a stick; turn the hull under you and the aim holds.
   - **Release** (Space, left click, A) launches a missile. Fly it with W A S D or the stick; Release again bursts it.
   - Fly it through an omni crystal and the magazine count goes up.
4. Press **1** to take the helm back.

## Where the game differs from the lab

- **The AI helm is the hull's own `AIPilot`**, not the lab's helm AI, so it flies the game's AI routes. The crew adds
  only the shield.
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
- **Sounds:** all five `EventReference` slots ship empty: launch, burst, shield, omni refill, empty magazine.
- **The HUD is the Sparrow's view under an Orca controller**, as the Wrasse's was. A prefab variant with the crew's four
  icons would replace the Sparrow's art.
- **The shield on a touch screen:** the left trigger has no touch equivalent yet. A tap on the Mass card would need
  that card to name an input (`AbilityCardPress`).
- **The duo-vessel system** (`cece/sharp-tesla-j0kd7z`, `/duo-vessel`) moves seat swapping into `CrewSeat`. Fold the
  Orca's seats into it when it lands.
- **`EnumIntegrityTests` count:** the Goby (`cece/sharp-tesla-j0kd7z`, 18) also raises the member count to 20. Whichever
  branch lands second makes it 21.
