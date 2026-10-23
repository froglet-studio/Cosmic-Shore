# The Jacks (vessel class 22)

Two **jacks** that **herd the cell's real fauna**, flown by ONE player. Designed in the Swarm Vessel Lab
(`Docs/Studios/SWARM_VESSEL_LAB.md`, the "Shepherd" direction, artifact https://claude.ai/artifact/Q7BgQYrT9xq5kmNtqCudmw,
open `#shepherd`). Shipped on 2026-10-21 as "Marlin and Jacks" (a marlin pilot and the jacks, two seats); the same day
Garrett reduced it to the jacks:

> Lets reduce the "marlin and jacks" to just the jacks (if there is a better solo herder lets rename it). This allows us
> to bring in a rapier stabbing character at a later date and save the marlin as a proper swordsmen, not overshadowed by
> the jacks. The jacks don't need the marlin or a second seat because they can switch to 3d flight mode and use two thumb
> flight controls which will map very intuitively to flying two fish (move one fish up and the other down to roll, or
> move both together for pitch and yaw, and bring them apart to speed up, bring them together to slow down). then it has
> control of its own plane that it herds on.

**The name stays the Jacks.** A pair of jacks IS the vessel (two fish, two thumbs). The solo herders considered: the
pilot fish and the trevallies are jacks already (Carangidae), the billfish that herd bait balls are the marlin's own
family (saved for the swordsman), and the orca and the dolphin are taken. `VesselClassType.Marlin = 22` became
`VesselClassType.Jacks = 22` (the value is what is serialized, so nothing moved); the name `Marlin` is free for the
swordsman, and its model (`Marlin.fbx`, `specs/marlin.py`, `Docs/VesselModels/Marlin.md`) stays in the repo for it.

Code:

| Piece | File |
|---|---|
| The herd, the jacks, the two modes, the decoy, contacts, Mass 5 | `Executors/JacksExecutor.cs` (implements `IFaunaHerd`) |
| The lab's arithmetic and the flight formation, pure | `Controller/Vessel/JacksMath.cs` (held by `Tests/Editor/JacksMathTests.cs`) |
| The jacks' herding AI (Strömbom drive and collect) | `Executors/JacksAI.cs` |
| The herding view (Steady camera, small view, double-tap switch) | `Executors/JacksView.cs` |
| The flying jacks' buttons | `Data Containers/JacksActionSO.cs` (`JacksControl`: Call 0, Teleport 1, Decoy 2) |
| Every number | `Data Containers/JacksConfigSO.cs`, `Assets/_SO_Assets/VesselActions/Jacks/JacksConfig.asset` |
| The four cards | `UI/Controller/JacksHUDController.cs`, `Resources/ElementalAbilityMaps/Jacks.asset` |
| The herd's rule breaks (platform) | `Environment/FloraAndFauna/IFaunaHerd.cs`, read by `Fauna` |
| The bodies: two jacks drawn, swum and replicated; the owner's jacks on other peers; the spinning jack between them | `Controller/Vessel/Crew/JacksBodies.cs` |
| The model | `Assets/_Models/Vessel Models/Jack.fbx` (`/vessel-model`, `Tools/Build/vessel_model/specs/jack.py`; dossier `Docs/VesselModels/Jack.md`) |
| The prefab and its registrations | `Tools/Build/author_jacks_assets.py` (`--check`, `--self-test`); icons `author_jacks_icons.py`; card `render_cards.py Jacks` |

## 1. What the player does

One player, two thumbs, two modes. The switch is the seat swap: D-pad left / right, the left / right arrow keys (`Docs/CREW_STATIONS.md`: fly -> herd -> the next hull), or a double tap on
the small view (the Wrasse's gesture, `PilotSwapGesture`).

- **Herding.** The hull holds still (`IVesselStatus.IsStationary`, held every frame and given back as the round left it)
  and its x-z plane is the board. Each touch takes the nearest jack, which runs to the point under the thumb
  (`dogSpeed`). Lifting the thumb leaves the decoy where that jack stands (one decoy; a new one replaces the old). On a
  gamepad the left stick runs the left jack and the right stick the right one (screen-relative, `padDogSpeed`), and the
  bumper or trigger on that side is the lift. A stick never drives a jack off the screen. The flight input is paused: the
  sticks are the jacks'.
- **Flying.** The jacks take off: the hull jumps to halfway between them, facing the board's forward, and they swim into
  formation beside it (`flightFormationEase`). The hull flies on the fleet's own two-thumb flight (the Rhino's rig and
  ramp), and each jack sits under its thumb (`JacksMath.FlightOffset`: `flightGap` from the centre line, moved
  `flightReach` by its stick, in the hull's frame). So the fish ARE the controls: one up and the other down is a roll,
  both the same way a pitch or a yaw, apart is faster and together slower, which is exactly what the flight reads from the
  same sticks (YDiff, YSum, XSum, XDiff). Where they fly is where the board goes.
  - **Call** is A (Input 7): for `callTime` the herd is pulled to the jacks, which stop repelling; then `callCooldown`.
  - **Jump** to the decoy is Input 1 (a trigger, or a lifted thumb) and Input 4 (both thumbs lifted): the hull and the
    jacks jump there, keeping their heading; the decoy is spent. Nothing while herding.
  - The ramp boost stays on Input 0.
- **Landing.** Switch back: the jacks start herding where they flew, laid onto the hull's plane, and the hull holds there.
- **The AI** (an AI hull, or a human's hull the app shell parks on its AI) herds for `aiHerdSeconds` with the jacks'
  drive-and-collect AI (`JacksAI`, aim points only, as thumbs), then flies for `aiFlightSeconds` on the hull's own
  `AIPilot` (so the board moves with the fleet's targeting and the hull collects crystals), calling the herd as it takes
  off.
- **The views.** Herding, an orthographic camera looks straight down the board and never turns with the hull (the lab's
  Steady camera); the game camera is the small square. It is zoomed out until, on a full-size phone (`phoneShortMm` 71 mm,
  a 6.7-inch screen's short side), a big thumb (`thumbMm` 22) barely covers the smallest ring: the ring's diameter is
  `thumbCover` (0.9) of the thumb (Garrett, 2026-10-21; `JacksMath.ThumbViewSpan`, about 1,147 u across the short side at
  the shipped numbers). The span is the same on every device, so every player sees the same board. Flying, the game
  camera has the screen and the herding view is the small square, framed toward the hull (`flightHullWeight`). It draws
  the way the game camera does: post-processing (bloom), anti-aliasing, HDR and the same volumes, copied from the game
  camera every frame (`JacksView.AdoptPilotLook`).
- **Stability while herding** (Garrett, 2026-10-21: "we need to make things as stable for the jack player as we can"):
  the hull holds still, so the board does not move under the thumbs at all; the view frames the herd alone
  (`herdHullWeight` 0), or the jacks with no herd; the pan, once the herd leaves the slack box, eases at `viewPanEase`
  (1.2/s). The board's own ease (`herdPlaneEase` 0.25/s, `flightPlaneEase` 3/s) only settles the last turn of a landing.

## 2. Elements (Garrett, 2026-10-20)

| Element | Scales | Level 5 |
|---|---|---|
| Charge | the share of DANGER creatures: 0% at -5, 100% at 15. A danger creature that touches a rival hull stings it (a scored Strike through `CombatHitDrain`) and is spent | **Attack**: the herd goes for rivals within `attackRange` and every creature that touches one bites it (a Strike) |
| Mass | the herd's birth rate: a birth takes the species' own feeds at -5, a third of them at 15 | **Mass Clusters**: every `pooEvery` one creature lays `pooCount` prisms of our domain |
| Space | every herder's ring: the thumb floor at -5, +6% of it per level above | **Eat Shields**: the herd eats shielded prisms (never super-shielded) |
| Time | the herd's speed, +6% per level, floored at x0.4 | **Flying Decoy**: flying, X drops the decoy where the jacks are (was the Marlin Decoy) |

The herders are the two jacks and the decoy (the marlin was a third until 2026-10-21). The design rule "never stun
vessels" holds: no contact slows or stuns a hull. PvP is petals only, so every hit on a rival is a Strike.

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

Per creature per frame, the lab's `stepSheep` term for term (`JacksMath`): separation (1.6), alignment (0.5 /
cruise) and cohesion (0.6 / neighbour radius) over the herd; each herder's ring (3.2 x (1 - d/r), a cylinder read from
above, panic 1 - d/r); the call (2.6 toward the jacks, panic at least 0.5; while it lasts the jacks stop repelling); Charge 5's attack (2.2 toward the nearest
rival); grazing while calm (the nearest prism the creature itself can eat within `grazeSense`, never our own domain); the
wander; and the plane's spring (the terminal speed of the lab's `vn' = -h x planePull - 1.8 vn + 40 gn`). The result is a
wanted velocity; the creature swims it with its own steering (`IFaunaLeash.TrySteer`) and eats its ordered meal with its
own mouthful (`OrderedMeal`). The lab's lengths are about 2.5x the game's, so every length and speed is authored in lab
units and multiplied by `labScale` (0.4).

## 5. Models and networking

- **The jacks are the vessel.** Two `Jack.fbx` (a deep body, a forked tail, domain scutes and a saddle on the back so the
  top-down view finds them) under the root, drawn by `JacksBodies`: herding, on the board where they run, facing the way
  they swim, the spine waving with their speed, at `jackModelScale` (2.5: 40 units long, sized for the herding view);
  flying, beside the hull under the thumbs, with the hull's heading and roll, at `flightModelScale` (1: 16 units, sized for
  the chase camera), the swim beating with the hull's speed. The size eases between the two (`modelScaleEase`).
- **The hull** is still the Rhino's flight rig: its five colliders, its two sticks, its ramp, its `RhinoAnimation`. Its
  rig is switched off (the puppetry still turns its bones), and the ship geometry is its fuselage part (the collider
  `GhostAction` turns off). Nothing is drawn at the hull itself: flying, the jacks flank it; herding, it is parked where
  they landed, colliders and all (a look and feel call for the playtest: see §6).
- **The marlin's model is out of the prefab** (`author_jacks_assets.py` `drop_marlin`), and its puppetry (tail, fins,
  sail) went with `MarlinCrewBodies`. `Marlin.fbx` stays for the swordsman.
- **The jack between the fish** (Garrett, 2026-10-23: "instead of a sword (1 axis) make it all three orthogonal axes
  (like a jack in the game of jacks). then if it was a coordinate system, spin it about the (1,1,1) axis and place that
  axis dynamically so it the same axis that draws a line between the two fish"). The Rhino's sword capsule (the nested
  `ForceFieldSkimmer`) is still the hull's skimmer; `JacksBodies` makes it a jack: three round arms along its own x, y
  and z (drawn in the blade's material; the blade's own renderer and crackle overlay stand down), its (1,1,1) diagonal
  laid on the line from jack A to jack B, centred halfway between them and spun about that line
  (`toyJackSpinDegreesPerSecond`, 120). The twist at zero spin follows the hull's up (the board's while herding), so it
  never flips (`JacksMath.JackRotation`). Each arm is long enough that its tip turns in the plane a fish swims in
  (`JacksMath.JackArm`: sqrt(3)/2 x the gap x `toyJackReach`; about 24 u at the flight formation), clamped to
  `toyJackMinArm`..`toyJackMaxArm` (4..40 u). The prefab carries the three trigger capsules (the sword's own Y one plus X
  and Z, `author_jacks_assets.py` `jack_retrofit`), so the impactor's probe set holds all three; the jack owns the
  skimmer's scale (`Skimmer.HasExternalScaleDriver`), so Space no longer lengthens a blade. It is posed on every peer
  from the drawn jacks. While herding it is drawn between the fish on the board, but its colliders are off (and the
  impactor bounced, so it forgets its overlaps): a parked hull does not skim or vacuum crystals in the middle of the
  board (`toyJackSkimsWhileHerding`). The sword's look (`RhinoSwordFXController`) and its five streaks
  (`RhinoSwordBladeTracer0..4`) are stripped.
- **No jets.** The Rhino's vestigial jets are stripped (Garrett, 2026-10-23: "the vestigal jets from the rhino should be
  gone"): the ten `VesselJet`s (two pods, eight body nozzles), the pods' `JetFX`, the arc lightning, `JetTest` and the two
  legacy jet particle systems. `VesselTailAndJets` stays on the root for the tail.
- **The herd lights up** (Garrett, 2026-10-23: "fauna that get herded should light up when in herding mode. fauna that
  get called should light up when flying"): while the jacks herd, and while a call lasts in flight, every herded
  creature's spindle colours are multiplied by 1 + `herdGlow` (1.5, HDR, so the bloom catches them). It rides the
  leash's mark (`IFaunaLeash.TryGetLeashMark`'s `glow`; `Spindle.SetHueLean` mints a shared material per mark and glow,
  so a lit herd still batches); the executor calls `Fauna.RefreshLeashMark` on each member when the state changes
  (`JacksExecutor.SetLit`, on the peer that simulates the creatures; on the host the mode of a client-owned hull comes
  from `JacksBodies`' flying flag). The mark goes to puppets in one `uint` (domain, glow, lean; protocol 13). The
  Angler's school does not glow (`glow` 0).
- **The rings and dots** are the local player's instruments. Each view draws its own set for its own camera
  (`AnglerWorldMarks.Begin(cam, onlyThisCamera)`): the herding view at full strength, the flight view much more subtle
  (Garrett, 2026-10-21: "make the dots that form the radius much more subtle"): 24 dots a ring instead of 48, half the
  size, down to 1.5 px, at a fifth of the alpha (`flightRing*` on the executor). No ring is drawn round a jack while the
  call lasts (it does not repel then).
- **Networking.** The machine that owns the hull runs the jacks, the decoy, the call and the mode, and publishes them on
  `JacksBodies` (owner-write NetworkVariables, quantized to 0.25 u): herding, offsets from the hull in world axes; flying
  (a `FlyingBit`), places in the hull's own frame, so the formation turns with the hull between ticks. Every other peer
  draws them, and its executor HERDS with them (`JacksExecutor.FollowRemoteCrew`) instead of running its own AI jacks, so
  the host, where the creatures are simulated, steers the herd with the client's real jacks and decoy. The hold and the
  take-off jump are written only where the hull is decided (`ElementalTransfer.IsDecidedHere`); the transform replicates.
- **Own camera** (`JacksCameraSettingsSO`, the Rhino's numbers at first) and **telemetry** (`DefaultVesselTelemetry` with
  its stat channels wired, so the bootstrapper's unwired fallback does not run).
- **The class card**, hangar preview, squad image and inactive icon are rendered from `Jack.fbx` (`render_cards.py Jacks`;
  `author_jacks_assets.py` owns the same guids). The ability row's four icons are the herd's
  (`Tools/Build/author_jacks_icons.py`: Danger Share, Breeding, Herding Ring, Herd Speed), over the Rhino's sword
  placeholders the HUD variant draws.

## 6. Follow-ups

- **Playtest the jack and the glow** (2026-10-23, no playtest yet): the jack's size and spin against the chase camera
  and in the herding view (`toyJack*` on `JacksBodies`), whether the herd glow (`herdGlow` 1.5) reads without washing
  the herd out, and whether the jack should skim while herding. The skim region moved: the sword reached 60 u ahead of
  the nose; the jack surrounds the hull, so the Jacks' skim and crystal-vacuum feel changes.
- **Playtest the two modes** (the decisions made without one, 2026-10-21): the flight formation (`flightGap` 14,
  `flightReach` 10, `flightModelScale` 1) against the Rhino's chase camera; whether the take-off jump to the jacks reads
  well; whether the parked hull's colliders (invisible, where the jacks landed) should switch off while herding; the AI's
  14 s / 6 s rhythm. All are numbers on `JacksConfig` / `JacksBodies` except the colliders, which would be code.
- **The card shows one jack.** `render_cards.py` renders one FBX; a pair would read as the vessel better.
- **Danger stings on a client-owned hull.** A contact is decided on the hull's owner (`ElementalTransfer.IsDecidedHere`)
  while the herd is leashed on the host; whether the owner sees the creatures close enough to score is for the
  host-client test.
- **Crystal hull fusion** still bakes the Rhino's hull shape (the four Jacks entries are the Rhino's).
- `element_ability_table.py Jacks` reads the Rhino's ForceFieldSkimmer for Space and the ramp for Time; the jacks' real
  bindings are in code (`JacksExecutor.Ring`, `JacksMath.SpeedMultiplier`).
- **Hull-radius sweeps see the jacks.** The two jack models are children of the vessel root, up to the herd ring away
  from the hull while herding, and `PrismOcclusionCorridor.MeasureCircumscribedRadius` (scope pip, Echo Sight, occlusion
  corridor, crystal ejector) counts every active child renderer. The Wrasse's kite has the same shape; the fix is one
  shared crew-exclusion rule in the sweep, for the owner of that utility, not a Jacks special case.
- **The herd's step is O(n²) over its members.** `StepHerd`'s neighbour loop visits every pair each frame: 150 x 149 =
  22,350 pair tests at the cap. Not measured in Unity yet. If it shows in a capture, bucket the herd on the board (a cell
  grid of `neighbourRadius`) rather than lowering the cap.
- **`AnglerWorldMarks` is shared** by the Angler and the Jacks under the Angler's name. A rename to a vessel-neutral name
  is a guid-safe `git mv` plus two call sites.
- **A player who saved "Marlin" as a string** (a cloud record keyed by the enum's name) would not parse after the rename.
  The class shipped the same day, so none is expected; the enum's VALUE, which Unity serializes, did not change.
