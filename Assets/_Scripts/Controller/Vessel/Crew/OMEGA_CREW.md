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

## 1. What a crew is

Three vessels of the shipped fleet, one hull class, one squad slot, one card (`SO_Class_<Crew>`, SeatCount 3):

| Crew | Members (page `MEMBERS`) | Omega weapon (Arms) | Core shield / finisher | Omega flight |
|---|---|---|---|---|
| **Warden** (25), the field crew | Stoat, Thresher, Butterfly | Ripple whip: 60 u sweep, cool 0.6 s | Fold ward / Annihilation | turn x0.6, speed x1.15 |
| **Bulwark** (26), the heavy crew | Rhino, Grizzly, Scarab | Siege cannon: blast 30 x (0.5 + charge), charge 1.6 s | Switch wall / Breaker | turn x0.5, speed x1.1 |
| **Tempest** (27), the fast crew | Squirrel, Sparrow, Manta | Pulsefire battery: range 260, every 0.08 s | Afterburner veil / Kabloom | turn x0.75, speed x1.3 |

The omega flight is the members' average cruise / turn / roll times the crew's multipliers (`OmegaMath.OmegaFlight`):
Warden 70.9 u/s, turn 64, roll 74; Bulwark 67.8 / 49.2 / 56.7; Tempest 89.3 / 82.5 / 105.

## 2. How it plays (the page's controls, on the game's inputs)

| Form | LT | RT | Swap gesture (D-pad left / right, arrow keys, two-finger swipe) |
|---|---|---|---|
| **Apart (mega)** | hold = call the gather; with the meter at `combineAt` (0.3) and every member within `gatherRange` (140 u) the dock starts | the hull member's move (§3) | the hull becomes another member: it takes that member's pose and flight, the member left behind takes the hull's |
| **Gathering** | - | - | waits for the omega |
| **Omega, Helm** (seat 0) | hold `splitSeconds` (0.5 s) = split | - | to Arms / Core: the hull's AIPilot flies, your flight input is paused (the Orca's gunner rule) |
| **Omega, Arms** (seat 1) | - | fire the crew weapon down the turret line (right stick / mouse / W A S D aim; the arrows stay the seat cycle) | |
| **Omega, Core** (seat 2) | hold = the shield (extra meter drain, mass touched is destroyed) | the finisher when the meter is at `finisherAt` (0.9): everything hostile in a cone `finisherCone` (180 u) ahead, a blast, then the split | |

The meter (`OmegaCrew.Meter01`, the Mass card): starts at 0.5; apart it regenerates `regen` (0.02/s) and a crystal adds
`meterPerCrystal` (0.22); combined it drains `drain` (0.05/s, plus `shieldDrain` 0.06/s while shielded) and a crystal
pays `combinedCrystal` (half); the finisher costs `finisherCost` (0.7); empty, the crew splits. The AI fills every seat
the person does not hold: Arms turns the turret onto the nearest mass ahead and fires when aligned (a cannon holds until
charged); Core shields when `aiShieldMass` prisms are about to hit and fires the finisher when the cone holds
`aiFinisherMass`; the AI Helm is the hull's AIPilot and never splits on purpose (the page's AI helm splits when nothing
locked lies ahead; there are no walls here yet, §6).

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
deactivated first), the Orca sniper's debris numbers. The AI members do not use their moves here (the page's AI
members do); they fly the formation and the gather.

## 4. HUD (the Stoat's four gauges, `OmegaHUDController`)

Charge = the weapon (a cannon's charge while held, else how far its cooldown has run); Mass = the meter; Space = apart,
how close the farthest member is to gather range (full when the combine can start), the dock while gathering, full
combined; Time = the finisher, lit when the meter can pay for it. No text: the form is read off the world (§5).

## 5. The look (`OmegaCrewView`, placeholders)

The hull wears the Stoat's model. The other two members are faceted bodies in the page's proportions (plates, ball,
wings, wedge, box, disc, cone, fighter, delta) in the hull's domain colour on the Orca's lit material
(`Resources/OrcaCrewLit.mat`), flying beside the hull apart, docked on their page slots (9 up / 4 back, 8 down / 6
back) with struts combined. Weapon fire is a lit bar to the strike, the Arms aim a thin bar, the finisher a cone that
fades over 0.6 s. Real member models are the next round (`/vessel-model`); so is a body for the omega form.

## 6. Not on the page's side yet, or not here yet (round 2)

- **The Omega Run** (the page's mini-game: shielded walls lock every third crystal and only the omega's weapon opens
  them) is not a mode yet: `/arcadegame`. Without walls the AI Helm never splits on purpose, and the Warden's omega has
  no reason to split (the page's round-1 finding; decision `omega-round1-purpose` in the artifact's log).
- **Skims do not fill the meter** (`meterPerSkim` is carried but no per-skim hook reaches the crew).
- **One person per crew**: `HostsRiders` is false; the Orca's rider seats are the pattern when a second person should
  take Arms or Core from another machine.
- **An AI hull** (a rival crew the spawner adds) flies as its first member and never combines.
- **No omega camera**: the hull's chase camera stays in every seat; the Arms aim shows as a bar in the world.
- **The gate pair and the tunnel ring** are boosts here (§3).
- **Networking the crew's effects** (what a remote peer sees of the bodies and the finisher) is the Orca's follow-up too.

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

Menu_Main > freestyle > Toy Box > Vessel Changer > **Warden** (then Bulwark, Tempest). Fly: the Stoat hull with two
faceted bodies beside it. RT: the Stoat's sling boost. D-pad right: you are the Thresher (the hull jumps to its pose);
RT held: mass ahead is smashed. Hold LT with the Mass card past a third: the two bodies dock onto the hull over 1.6 s;
the hull is now the omega (faster, wider turns), bulldozing mass. D-pad right: Arms; the right stick aims a bar, RT
whips. D-pad right: Core; LT holds the shield (the Mass card drains faster), RT with the Mass card near full fires the
finisher: a cone of mass dies, a blast, the crew splits round you. Hold LT as Helm for half a second: split.
