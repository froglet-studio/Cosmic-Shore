# Marlin and Jacks (vessel class 22)

A marlin (its own model on the Rhino's flight rig: its two sticks and its ramp) and two **jacks** that **herd the cell's real fauna**. Designed
in the Swarm Vessel Lab (`Docs/Studios/SWARM_VESSEL_LAB.md`, the "Shepherd" direction, artifact
https://claude.ai/artifact/Q7BgQYrT9xq5kmNtqCudmw, open `#shepherd`). Named by Garrett on 2026-10-20 ("X and Ys", real
clades: a marlin and its jacks).

Code:

| Piece | File |
|---|---|
| The herd, the jacks, the decoy, the seats, contacts, Mass 5 | `Executors/MarlinJacksExecutor.cs` (implements `IFaunaHerd`) |
| The lab's arithmetic, pure | `Controller/Vessel/MarlinJacksMath.cs` (held by `Tests/Editor/MarlinJacksMathTests.cs`) |
| The jacks' AI (Strömbom drive and collect) | `Executors/MarlinJacksAI.cs` |
| The jacks' view (Steady camera, small view, double-tap swap) | `Executors/MarlinJacksView.cs` |
| The marlin's buttons | `Data Containers/MarlinJacksActionSO.cs` (`MarlinJacksControl`: Call 0, Teleport 1, Decoy 2) |
| Every number | `Data Containers/MarlinJacksConfigSO.cs`, `Assets/_SO_Assets/VesselActions/MarlinJacks/MarlinJacksConfig.asset` |
| The four cards | `UI/Controller/MarlinJacksHUDController.cs`, `Resources/ElementalAbilityMaps/Marlin.asset` |
| The herd's rule breaks (platform) | `Environment/FloraAndFauna/IFaunaHerd.cs`, read by `Fauna` |
| The bodies: the marlin and two jacks drawn, swum and replicated; the owner's crew on other peers | `Controller/Vessel/Crew/MarlinCrewBodies.cs` |
| The models | `Assets/_Models/Vessel Models/Marlin.fbx`, `Jack.fbx` (`/vessel-model`, `Tools/Build/vessel_model/specs/marlin.py`, `jack.py`; dossiers `Docs/VesselModels/Marlin.md`, `Jack.md`) |
| The prefab and its registrations | `Tools/Build/author_marlin_assets.py` (`--check`, `--self-test`) |

## 1. What the player does

Garrett, 2026-10-20 (the original ask, verbatim in the lab doc): a pilot and two thumb-run herders; the herd swims in 3D but
is strongly drawn to the pilot's x-z plane, where the herders run; every herder repels inside a ring "larger than a big
thumb's occlusion"; the pilot can CALL the herd on a long cooldown; lifting a thumb leaves ONE decoy (a new one replaces
the old); the pilot can teleport to it.

- **The jacks.** Each touch takes the nearest jack, which runs to the point under the thumb (`dogSpeed`). Lifting the thumb
  leaves the decoy where that jack stands. On a gamepad the left stick runs the left jack and the right stick the right one
  (screen-relative, `padDogSpeed`), and the bumper or trigger on that side is the lift. A stick never drives a jack off the
  screen.
- **The marlin** flies the Rhino's two sticks; its ramp boost stays on Input 0 ("the pilot has the rhino's full speed and
  straight ability to zoom forward"). **Call** is A (Input 7): for `callTime` the herd is pulled to the marlin, which stops
  repelling, then `callCooldown`. **Teleport** to the decoy is Input 1 (a trigger, or a lifted thumb) and Input 4 (both
  thumbs lifted); the decoy is spent.
- **Seats.** D-pad left / right, keys 1 / 2, or a double tap on the small view move the local human between the marlin and
  the jacks (the Wrasse's gesture, `PilotSwapGesture`). The empty seat is an AI: the hull's own `AIPilot` for the marlin, or
  `MarlinJacksAI` for the jacks, which plays through aim points only, as thumbs do.
- **The view.** Running the jacks, an orthographic camera looks straight down the marlin's plane and never turns with
  the marlin (the lab's Steady camera); the game camera is the small square. It is zoomed out until, on a full-size phone
  (`phoneShortMm` 71 mm, a 6.7-inch screen's short side), a big thumb (`thumbMm` 22) barely covers the smallest ring: the
  ring's diameter is `thumbCover` (0.9) of the thumb (Garrett, 2026-10-21: "zoomed out enough that on a full size mobile
  device an above average sized thumb barely covers the smallest jack repulsion radius";
  `MarlinJacksMath.ThumbViewSpan`). At the shipped numbers that is about 1,147 u across the short side (the lab's view
  was 720 u, where the ring was 1.25 thumbs on a smaller phone). The ring's size in the world does not change with the
  view: `viewSpan` (the lab's) still sizes it. The span is the same on every device, so every player sees the same board.
  Flying the marlin, the game camera has the screen and the jacks' view is the small square. It draws the way the game
  camera does: post-processing (bloom), anti-aliasing, HDR and the same volumes, copied from the game camera every frame
  (`MarlinJacksView.AdoptPilotLook`).
- **Stability for the jacks' player** (Garrett, 2026-10-21: "the marlin makes the jacks player sick with its movement. we
  need to make things as stable for the jack player as we can"). While a human runs the jacks:
  - the board (the jacks' plane, and what the view looks down) turns toward the marlin's plane at `jackSeatPlaneEase`
    (0.25/s, against `pilotSeatPlaneEase` 3/s when the jacks are the AI), so the marlin's roll and pitch barely tilt the view;
    0 holds it still;
  - the view frames the herd alone (`jackSeatMarlinWeight` 0), or the jacks with no herd, so the marlin's flight never pans it;
  - the pan, once the herd leaves the slack box, eases at `viewPanEase` (1.2/s, the lab's 1.6).
  The marlin's height above or below the plane never moves the picture: the view is orthographic along the plane's normal.

## 2. Elements (Garrett, 2026-10-20)

| Element | Scales | Level 5 |
|---|---|---|
| Charge | the share of DANGER creatures: 0% at -5, 100% at 15. A danger creature that touches a rival hull stings it (a scored Strike through `CombatHitDrain`) and is spent | **Attack**: the herd goes for rivals within `attackRange` and every creature that touches one bites it (a Strike) |
| Mass | the herd's birth rate: a birth takes the species' own feeds at -5, a third of them at 15 | **Mass Clusters**: every `pooEvery` one creature lays `pooCount` prisms of our domain |
| Space | every herder's ring: the thumb floor at -5, +6% of it per level above | **Eat Shields**: the herd eats shielded prisms (never super-shielded) |
| Time | the herd's speed, +6% per level, floored at x0.4 | **Marlin Decoy**: X drops the decoy where the marlin is |

The design rule "never stun vessels" holds: no contact slows or stuns a hull. PvP is petals only, so every hit on a rival
is a Strike.

## 3. The herd is a special swarm (Garrett, 2026-10-21)

"These are a special swarm that can be upgraded to break rules and should only starve down to a minimum size and break
other rules if need be. In general shielded prisms should be food during a frenzy when the cell has too many prism."

- **Size** (Garrett, 2026-10-21: "make the swarm way bigger"): the floor is 30 (was 12), born 0.12 s apart (was 0.25),
  and the herd holds up to 150 (was 60).
- **The mark** (Garrett, 2026-10-21: "distinguish them by giving their spindles a slight hue shift in your domain. do the
  same thing for other tamer classes like the angler"): a herded creature's spindles lean `herdHueLean` (0.35) of the way
  round the hue circle toward our domain's hue. It is the leash's mark (`IFaunaLeash.TryGetLeashMark`), so every tamer
  wears it (the Angler's school too, `tameHueLean`): `Fauna.ApplyLeashMark` leans each `Spindle` onto a shared material
  per mark (it still batches, and the LOD tiers copy it), `FaunaNetworkSync` replicates it (protocol 11), it comes off
  when a creature is let go and stays on through its wither. The creature's domain, body prisms and diet are the cell's.
- **Who joins.** Wild leashable herbivores that come inside any herder's ring join the herd, up to `herdCap`; a herded
  creature's offspring joins it (`IFaunaHerd.Born`).
- **The floor.** The first `minHerd` creatures (in join order) do not starve (`IFaunaLeash.Sustains`), and an EMPTY herd has
  `minHerd` born to it, one every `floorBirthSeconds`, through the platform's one fauna spawn seam in the cell's colour:
  the cell's first leashable species, or `companionFauna`. Predation, jousts and stings still kill. This is the second
  sign-off on the Angler keeper's exception (`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`, "No imposed death").
- **Rule breaks are methods on `IFaunaHerd`**, each with one reader in `Fauna`: `EatsShields` (Space 5), and
  `FeedsPerOffspringScale` (Mass). Everything else is the creature's own: its diet (`LeashCanEat`), its mouthful, its
  stomach, its death.
- **Platform rule, not the herd's:** shielded mass is food for every grazer in a cell in `CellPhase.Frenzy`
  (`Cell.ShieldedMassIsFood`; `Docs/ECOSYSTEM.md` §61.8).

## 4. How the herd is steered

Per creature per frame, the lab's `stepSheep` term for term (`MarlinJacksMath`): separation (1.6), alignment (0.5 /
cruise) and cohesion (0.6 / neighbour radius) over the herd; each herder's ring (3.2 x (1 - d/r), a cylinder read from
above, panic 1 - d/r); the call (2.6 toward the marlin, panic at least 0.5); Charge 5's attack (2.2 toward the nearest
rival); grazing while calm (the nearest prism the creature itself can eat within `grazeSense`, never our own domain); the
wander; and the plane's spring (the terminal speed of the lab's `vn' = -h x planePull - 1.8 vn + 40 gn`). The result is a
wanted velocity; the creature swims it with its own steering (`IFaunaLeash.TrySteer`) and eats its ordered meal with its
own mouthful (`OrderedMeal`). The lab's lengths are about 2.5x the game's, so every length and speed is authored in lab
units and multiplied by `labScale` (0.4).

## 5. Models and networking (Garrett, 2026-10-21: "lets give it some models and get it ready to ship")

- **The marlin** is `Marlin.fbx`: a spear, a tall sail, sickle pectoral fins, a crescent tail and eight domain bars down
  each flank (the striped marlin's). It is nested under the Rhino's fuselage part, so every turn `RhinoAnimation` gives the
  hull carries it, and it is the ship geometry (the domain paint lands on it). The Rhino's rig stays in the prefab,
  inactive: the puppetry still turns its bones, and the hull's five colliders are the Rhino's parts' own.
  `MarlinCrewBodies` puppets it from the hull's measured motion on every peer: the tail beats with speed and bends out
  of a turn, the fins follow the Rhino's front plates (`finFollow`), the sail folds back at boost and STANDS UP while the
  marlin calls (the call is replicated, so rivals see the herd being called).
- **The jacks** are two `Jack.fbx` (a deep body, a forked tail, domain scutes and a saddle on the back so the top-down
  view finds them), drawn where the jacks run, facing the way they swim, the spine waving with their speed, scaled by
  `jackModelScale` (2.5: 40 units long). The rings and dots stay as the local player's instruments. Each view draws its
  own set for its own camera (`AnglerWorldMarks.Begin(cam, onlyThisCamera)`): the jacks' view at full strength, the
  marlin's view much more subtle (Garrett, 2026-10-21: "make the dots that form the radius much more subtle to the
  marlin"): 24 dots a ring instead of 48, half the size, down to 1.5 px, at a fifth of the alpha (`pilotRing*` on the
  executor).
- **Networking.** The machine that owns the hull runs the jacks, the decoy and the call and publishes them on
  `MarlinCrewBodies` (offsets from the hull, quantized to 0.25 u, owner-write NetworkVariables). Every other peer draws
  them, and its executor HERDS with them (`MarlinJacksExecutor.FollowRemoteCrew`) instead of running its own AI jacks, so
  the host, where the creatures are simulated, steers the herd with the client's real jacks and decoy.
- **Own camera** (`MarlinCameraSettingsSO`, the Rhino's numbers at first) and **telemetry** (`DefaultVesselTelemetry` with
  its stat channels wired, so the bootstrapper's unwired fallback does not run).
- **The class card**, hangar preview, squad image and inactive icon are rendered from `Marlin.fbx`
  (`render_cards.py Marlin`; `author_marlin_assets.py` owns the same guids). The ability row's four icons are the
  marlin's (`Tools/Build/author_marlin_icons.py`: Danger Share, Breeding, Herding Ring, Herd Speed), over the Rhino's
  sword placeholders the HUD variant draws.

## 6. Follow-ups

- **Danger stings on a client-owned marlin.** A contact is decided on the hull's owner (`ElementalTransfer.IsDecidedHere`)
  while the herd is leashed on the host; whether the owner sees the creatures close enough to score is for the
  host-client test (checklist step 10).
- **Crystal hull fusion** still bakes the Rhino's hull shape (the four Marlin entries are the Rhino's) until a bake from
  `Marlin.fbx` is made in the editor.
- `element_ability_table.py Marlin` reads the Rhino's ForceFieldSkimmer for Space and the ramp for Time; the marlin's real
  bindings are in code (`MarlinJacksExecutor.Ring`, `MarlinJacksMath.SpeedMultiplier`).
- **Hull-radius sweeps see the jacks.** The two jack models are children of the vessel root flown up to the herd ring
  away from the hull, and `PrismOcclusionCorridor.MeasureCircumscribedRadius` (scope pip, Echo Sight, occlusion
  corridor, crystal ejector) counts every active child renderer, so a measure taken while the crew is seated reads the
  ring, not the hull. The Wrasse's kite has the same shape today; the fix is one shared crew-exclusion rule in the
  sweep, for the owner of that utility, not a Marlin special case. Vision shading and Echo Sight tinting the jacks
  with the hull is intended.
- **The herd's step is O(n²) over its members** (round 4, `cece/sharp-faraday-gq7m65`). `StepHerd`'s neighbour loop
  visits every pair each frame: 150 x 149 = 22,350 pair tests at the new cap (3,540 at the old 60). Not measured in
  Unity yet (checklist step 13). If it shows in a capture, bucket the herd on the board (a cell grid of
  `neighbourRadius`) rather than lowering the cap.
- **The jack models were sized for the old view.** `jackModelScale` 2.5 (40 u long) was chosen to read at 720 u across;
  the thumb-framed view is about 1,147 u, so a jack now draws about 63% as large on screen. A look call: raise the scale
  (rivals see the bigger fish too) or leave it, since a thumb covers the jack it runs anyway.
- **`AnglerWorldMarks` is now shared** by the Angler and Marlin and Jacks, under the Angler's name. A rename to a
  vessel-neutral name is a guid-safe `git mv` plus two call sites; not done here, to keep this branch to its subject.
