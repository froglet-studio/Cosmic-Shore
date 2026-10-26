# The Omega crews: Warden, Bulwark, Tempest (vessels 25, 26, 27)

**Status: a Unity PROTOTYPE on `Ys-bleeding-edge` (2026-10-26), not editor-verified** (the Unity CLI was not available
in the session that wrote it; `Docs/UNITY_VERIFICATION_CHECKLIST.md` carries the hand test). The design and every
number come from the Vessel Studio's Omega Crews lab, `Docs/Studios/VesselStudio/omega.html` (the ONE artifact,
https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa, bay "Omega Crews"); the design record is
`Docs/Studios/CONCEPT_FLEET.md` §10. The artifact is the source and this is its clone (/artifact-to-unity): change a
number there, record it, and clone it here; never tune it here first.

Yash, 2026-10-26: "I wanna make a crew vessel, that stoat start will be a part of ... swap between each seats and
control the vessel in different way ... each vessel will have their own megaforms and then finally they will have their
omegaforms. They can combine." Then: "make this prototype in Ys-bleeding-edge, I wanna test there."

## 0. The design principle this is a test bed for (2026-10-27)

A Voltron-type vessel is designed from the ground up to be many separate vessels AND one big vessel at alternating
parts of its own game loop; a mechanic that combines characters designed as complete, autonomous vessels is the wrong
shape (the lab's round 1 showed it: an assembled omega has no reason to split). So the crews below, composed from
shipped vessels, are PLACEHOLDER parts standing in for a supervessel whose parts do not exist yet; what is reusable is
the plumbing (the formation, the seat swap, the AI seats, the attacker's turret, the defender's shield). The next round
designs that supervessel's parts and its loop in the artifact first: `Docs/Studios/CONCEPT_FLEET.md` §10.4.

## 1. What a crew is (round 2, Yash's test of 2026-10-26)

Three vessels of the shipped fleet, one hull class, one squad slot, one card (`SO_Class_<Crew>`, SeatCount 3), flying as
ONE MARCH FORMATION: an aligned flock of three whose relative distances never change. Every crew is three ROLES (Yash:
"defender, midfielder and attacker"): a MOBILITY vessel that leads and does the manoeuvring, an ATTACKER to its right
that does the shooting, a DEFENDER to its left that protects.

| Crew | Mobility (member 0) | Attack (member 1) | Defence (member 2) | The formation flies with |
|---|---|---|---|---|
| **Warden** (25), the field crew | Stoat (sling boost) | Sparrow (Pulsefire guns) | Rhino (shield; its plough on RT) | the Stoat: cruise 60, turn 110, roll 130 |
| **Bulwark** (26), the heavy crew | Scarab (cavitation dash) | Grizzly (charged cannon) | Butterfly (shield; a boost on RT) | the Scarab: 75 / 120 / 150 |
| **Tempest** (27), the fast crew | Squirrel (skim boost) | Thresher (wrecking ball) | Manta (shield; a boost on RT) | the Squirrel: 66 / 120 / 140 |

The compositions are proposals from the fleet's strengths (the best movers, the best shooters, the vessels with a
shield or a plough); Yash rearranges them in `OmegaMath.Crews` and the page's CREWS (the artifact first). The lab page
keeps the mega / omega COMBINE from round 1 (gather, dock, one omega body with Helm / Arms / Core, the finisher);
the game does not have it yet (§6).

## 2. How it plays

- **The formation.** The hull's `OmegaVesselTransformer` always flies with the MOBILITY vessel's cruise and turn rates,
  whichever member the person sits in. The other two members are bodies at fixed offsets in the leader's frame
  (`formationSpread` 18 u to each side, `formationBack` 10 u behind; `OmegaMath.FormationOffset`), drawn by
  `OmegaCrewView`. Nothing drifts apart.
- **Seats and the swap.** The person sits in one member; the HULL (the Unity vessel, with its camera) IS that member.
  The swap gesture (D-pad left / right, the arrow keys, a two-finger swipe) moves the hull to the chosen member's place
  in the formation (`OmegaCrew.HandOver`: the pose is the formation's, the speed the formation's), so the camera goes
  with you and the member you left becomes a body. An AI holds every seat you do not.

| Seat | Who flies | LT | RT |
|---|---|---|---|
| **Mobility** | you, with the sticks | - | the mobility vessel's move (§3: a boost, or the Scarab's dash) |
| **Attack** | the hull's AIPilot (your flight input is paused, the Orca's gunner rule); you aim the turret with the right stick, the mouse or W A S D | - | the attacker's weapon down the turret line (guns / cannon / the wrecking ball) |
| **Defence** | the hull's AIPilot; the sticks are free | the shield: every hostile prism within `shieldRadius` (40 u) of the formation dies while held; the meter drains `shieldDrain` | the ward burst: one sweep half again as wide, `wardCost` 0.2 of the meter, `wardCooldown` 3 s |

- **The AI seats** (input only): the attacker AI turns its turret onto the nearest mass ahead of ITS place and fires when
  aligned (a cannon holds until charged); the defender AI shields when `aiShieldMass` hostile prisms are about to meet
  the formation and fires the ward when `aiFinisherMass` are; the mobility AI is the hull's own AIPilot.
- **The meter** (the Mass card) starts at `startMeter` 0.5, regenerates `regen` 0.02/s always, fills by
  `meterPerCrystal` 0.22 per crystal (`OmegaCrystalEffectSO`), and pays for the shield and the ward.

## 3. What the Unity hull does with each member's move (the page's `megaMove`, folded)

The page has nine distinct moves. A hull here has one `OmegaMath.MoveKind` per member, kept honest in the table:

| Member | Page move | Here | Numbers |
|---|---|---|---|
| Stoat | Dipole sling | **Boost** burst | x2.0 for 1.2 s, cool 4 |
| Thresher | Wrecking ball | **Smash**: held, the mass within reach ahead (swinging side to side) is destroyed | reach 30, cool 0.4 per smash |
| Butterfly | Fold gate (a teleport pair) | **Boost** burst (the config's `foldBoost`, 1.5 for 1 s): the gate pair is round 2 | cool 6 |
| Rhino | Plough | **Smash**, timed sweep ahead, with its boost | reach 16, x1.4 for 1 s, cool 3 |
| Grizzly | Charged cannon | **Cannon**: hold to charge, release = a sphere smash and an `AOEExplosion` at `cannonRange` (140 u); the page flies the shell | blast 18 x (0.5 + charge), charge 1.5 s |
| Scarab | Cavitation dash | **Smash**, a timed dash (speed 260) that destroys what it passes | reach 14, 0.3 s, cool 2.5 |
| Squirrel | Skim boost | **Boost** burst | x1.5 for 1 s, cool 2.5 |
| Sparrow | Pulsefire | **Guns**: the nearest hostile prism within 6 u of the nose line | range 220, every 0.1 s |
| Manta | Boost tunnel (a ring others ride) | **Boost** burst (`foldBoost`): the ring is round 2 | cool 3 |

A smash is `PrismSpatialIndex.QuerySphere` + `Prism.Damage(devastate)` (a super-shielded prism has its shields
deactivated first), the Orca sniper's debris numbers. In the game a member's move runs from its PLACE in the formation along its aim: the mobility vessel's from the hull
(a boost is the whole formation's), the attacker's down its turret line, the defender's round the formation.

## 4. HUD (the Stoat's four gauges, `OmegaHUDController`)

Charge = the attacker's weapon (a cannon's charge while held, else how far its cooldown has run); Mass = the meter;
Space = the ward's cooldown running down, lit while the shield is up; Time = the seat you sit in (a third per seat:
mobility, attack, defence), lit in the mobility seat. No text: the formation is read off the world (§5).

## 5. The look (`OmegaCrewView`, placeholders)

The hull wears the Stoat's model whichever member you sit in (so the Stoat shape moves with you; the member you left
shows as its placeholder). The other two members are faceted bodies in the page's proportions (plates, ball, wings,
wedge, box, disc, cone, fighter, delta) in the hull's domain colour on the Orca's lit material
(`Resources/OrcaCrewLit.mat`), the colour set on the material itself (a property block on the instanced template drew
them BLACK in Yash's first test, 2026-10-26). The attacker's aim is a thin bar along its turret line, weapon fire a bar
to the strike, the shield eight shards orbiting the formation, the ward a shell that fades. Real member models are the
next round (`/vessel-model`).

## 6. Not in the game yet (round 3)

- **The combine** (the lab's mega / omega forms: gather on LT, the dock, one omega body with Helm / Arms / Core, the
  finisher that splits the crew) is on the page and in `OmegaMath` (slots, dock ease, the finisher cone, held by
  `OmegaMathTests`), not in `OmegaCrew`: Yash's test asked for the formation with roles first ("for now, just make
  this much").
- **Multiplayer swapping** (a second person taking the attacker or the defender from another machine): `HostsRiders`
  is false; the Orca's rider seats are the pattern.
- **The Omega Run** mini-game (walls only a crew weapon opens): `/arcadegame`.
- **Skims do not fill the meter** (`meterPerSkim` is carried but no per-skim hook reaches the crew).
- **An AI crew** (a rival the spawner adds) flies the formation with every seat on AI (the attacker fires, the defender
  shields) on the authority only.
- **No per-member camera**: the hull's chase camera moves with you because the hull does; the members have no cameras
  of their own.
- **The gate pair and the tunnel ring** are boosts here (§3). A defender whose move is a boost (Butterfly, Manta) boosts
  the whole formation on RT; the ward and the shield are the same for every defender.

## 7. Elements (open slots)

`Assets/Resources/ElementalAbilityMaps/<Crew>.asset` has four OPEN slots carrying the page's proposals as text: Charge =
Weapon power (x1 to x1.6, L5 "Overload"), Mass = Ward (shield drain x1 to x0.4, "Bulwark"), Space = Finisher reach (x1 to
x1.5, "Horizon"), Time = Quick combine (x1 to x0.5, "Instant"). `OmegaMath.ElementMults` carries the arithmetic; nothing
reads it until the slots are signed off (vessel skill §3).

## 8. Files

| What | Where |
|---|---|
| The arithmetic (pure) + goldens | `Crew/OmegaMath.cs`, `Tests/Editor/OmegaMathTests.cs` (goldens from the page's own functions, 2026-10-26) |
| The crew (forms, seats, meter, moves, weapon, finisher, AI seats) | `Crew/OmegaCrew.cs` (`ICrewTriggers`, `ICrewStations`, `IHullParkAware`; the Orca's lifecycle) |
| The look | `Crew/OmegaCrewView.cs` |
| The numbers | `Crew/OmegaCrewConfigSO.cs`; assets `Assets/_SO_Assets/VesselActions/Omega/Omega<Crew>Config.asset` (seeded) |
| The flight | `Vessel/OmegaVesselTransformer.cs` (cruise and turn rates switched per form, a boost channel) |
| The meter's crystals | `ImpactEffects/.../OmegaCrystalEffectSO.cs` in `OmegaImpactorDataContainer.asset` (the Stoat's, minus its Overclock) |
| The HUD | `UI/Controller/OmegaHUDController.cs` on the Stoat HUD view |
| The generator and gate | `Tools/Build/author_omega_assets.py` (`--check`, `--self-test`) |
| The page | `Docs/Studios/VesselStudio/omega.html` + `omega-studio.js` |

## 9. Hand test (Garrett's workflow: artifact to the Toybox's Vessel Changer)

Menu_Main > freestyle > Toy Box > Vessel Changer > **Warden** (then Bulwark, Tempest). Fly: the Stoat hull leads, two
bodies in your domain colour ride 18 u to each side and 10 u behind, and stay there through every turn and boost. RT:
the Stoat's sling boost (the whole formation speeds up). D-pad right: you are the Sparrow; the hull jumps to the right
place, the camera with it, the AI now steers the formation and your sticks turn a thin bar; RT fires its guns down it,
the nearest prism on that line dies. D-pad right again: the Rhino; LT holds the shield (shards orbit the formation,
mass inside it dies, the Mass card drains), RT fires the ward (a shell swells and fades, mass around the formation
dies). D-pad left twice: back in the Stoat, your sticks fly again. Swap to another vessel: nothing is left behind.
