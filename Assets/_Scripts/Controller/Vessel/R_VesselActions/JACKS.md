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
| The jacks' herding AI (Strömbom drive and collect) | `Controller/AI/Jacks/JacksAI.cs` (moved 2026-10-25) |
| The herding view (Steady camera, small view, double-tap switch) | `Executors/JacksView.cs` |
| The netting jacks' buttons | `Data Containers/JacksActionSO.cs` (`JacksControl`: Call 0, Teleport 1, Decoy 2 (retired), TeleportLeft 3, TeleportRight 4, Wide 5) |
| Every number | `Data Containers/JacksConfigSO.cs`, `Assets/_SO_Assets/VesselActions/Jacks/JacksConfig.asset` |
| The four cards | `UI/Controller/JacksHUDController.cs`, `Resources/ElementalAbilityMaps/Jacks.asset` |
| The herd's rule breaks (platform) | `Environment/FloraAndFauna/IFaunaHerd.cs`, read by `Fauna` |
| The bodies: two jacks drawn, swum and replicated; the owner's jacks on other peers; the net drawn between them | `Controller/Vessel/Crew/JacksBodies.cs` |
| The net: what it catches, carries, steals, shields and takes | `Executors/JacksNet.cs` (geometry `JacksMath.NetFrame`) |
| The omni crystal's worm | `Vessel Crystal Effects/VesselSpawnFaunaByCrystalEffectSO.cs`, `Assets/_SO_Assets/Effects/Vessel Crystal Effects/JacksOmniWormByCrystalEffect.asset` |
| The model | `Assets/_Models/Vessel Models/Jack.fbx` (`/vessel-model`, `Tools/Build/vessel_model/specs/jack.py`; dossier `Docs/VesselModels/Jack.md`) |
| The prefab and its registrations | `Tools/Build/author_jacks_assets.py` (`--check`, `--self-test`); icons `author_jacks_icons.py`; card `render_cards.py Jacks` |

## 1. What the player does

**Every control is an ability card** (Garrett, 2026-10-25, round 10: "on PC the gamepad view should only every have the 5
standard ability icons and any additional active controls should be to the left of those (like the squirrel's drift) and
it should be labeled with a glyph. this is always true for vessels"). The herding view draws no buttons of its own (rounds
2-9 put a zoom and a call button top right; they are gone). The three active abilities are the elemental cards, the same
in both modes, each chip naming its control:

| Card | Control | Herding | Netting |
|---|---|---|---|
| **Mass: The Call** | B / T (Button2, Input 7) | calls the herd to the point between the jacks (`callTime`, then `callCooldown` on the card's veil) | toggles whether the herd is drawn to the plane (lit while it is) |
| **Space: Wide** | Y / F (Button4, Input 14) | eases the view out to the whole cell and back | nothing (round 11, 2026-10-25: "remove the space ability in net mode"; round 10's bigger net, `wideNetScale`, is retired) |
| **Time: Decoys** | the triggers / L and R shift (Inputs 2 and 1; the chip is the two-trigger glyph, Input 13) | a pull lays that trigger's decoy at the jack on that side of the screen (a phone: lift a thumb) | a pull FLIES the hull fast to that trigger's decoy (a phone: lift a thumb, or both for the one placed last, Input 4) |

Without Time 5 there is one decoy and either trigger lays it or flies to it; with it each trigger has its own. A card
tapped on screen presses its control in either mode: netting through the flight's buttons, herding through
`JacksExecutor.OnCardPress` (the flight's buttons are paused while the jacks herd).

**The fast travel** (round 11, Garrett, 2026-10-25: "don't teleport, move quickly, so the player has a sense of where they
travelled to"): the hull flies to the decoy at `travelSpeed` (900 u/s) on top of its own flight, never quicker than
`travelMinSeconds` (0.35 s) or slower than `travelMaxSeconds` (1.2 s), keeping its heading, and the chase camera rides
along. It is one velocity displacement on the hull's transformer (the Angler's dash, `VesselTransformer.ModifyVelocity`),
less the distance the hull's own flight covers meanwhile, so it arrives at the decoy; what the net carries comes with it.
The next one waits for this one to land plus `teleportGuard`.

**The decoy is a third jack** (Garrett, 2026-10-25: "Let's have the decoy look like a third jack"): each decoy down is drawn
as a copy of the jacks' model (`JacksBodies.UpdateDecoys`; a fourth with Time 5's second), painted in the domain's ship
material, at the jacks' own size for the mode, swimming in place on the board and facing the way the jack that laid it
faces - and turning as that jack turns (Garrett, 2026-10-25: "when the fish is laid it should match the orientation of the
laid fish. as that fish reorients so should the decoy"; `JacksExecutor.DecoySource`, published as `n_decoyFrom`). The one
laid between the jacks on landing faces the way both do on average. It grows in where it is laid, and a re-laid decoy shrinks out of its old place and grows into the new one
(`decoyGrowRate` 5/s; a move over `decoyRelayDistance` 8 u is a re-lay, a smaller one is followed). Every peer draws it
from the published decoys. The copy has no collider. Its 2D mark is a jack's dot; its ring keeps the decoy's colour.

One player, two thumbs, two modes. The switch is the seat swap: D-pad left / right, the left / right arrow keys (`Docs/CREW_STATIONS.md`: fly -> herd -> the next hull), or a double tap on
the small view (the Wrasse's gesture, `PilotSwapGesture`).

- **Herding.** The hull holds still (`IVesselStatus.IsStationary`, held every frame and given back as the round left it)
  and its x-z plane is the board. Each touch takes the nearest jack, which runs to the point under the thumb
  (`dogSpeed`). Lifting the thumb leaves the decoy where that jack stands (a phone's way; a mouse let go lays nothing,
  the triggers lay it on PC). On a gamepad the left stick runs the left jack and the right stick the right one
  (screen-relative, `padDogSpeed`); the trigger on that side lays that side's decoy. A stick never drives a jack off the
  screen. The flight input is paused: the sticks are the jacks'. The herd is always drawn to the board's plane. **Call**
  (the Mass card; B or T, read straight off the device because the flight's buttons are paused): for `callTime` the herd is pulled to
  the point between the jacks, which stop repelling; then `callCooldown` (Garrett, 2026-10-24: "in herding mode it
  attracts fauna to the point between the jacks, with the plane attraction always on"). A jack that touches a crystal
  collects it, measured across the board as the top-down view shows it (`jackCrystalReach` 32 u, up to `jackCrystalHeight`
  200 u above or below the board; round 1 measured in 3D and missed the crystals the net left off the board), so the
  crystals the net let go of are the jacks' to pick up.
- **Netting** (the flying mode; Garrett, 2026-10-24). The jacks take off: the hull jumps to halfway between them, facing the way they face on average (2026-10-25: "the new orientation should be the average orientation of the two fish. same for location"; the board's forward if they face opposite ways), and they swim into
  formation beside it (`flightFormationEase`). The hull flies on the fleet's own two-thumb flight (the Rhino's rig and
  ramp), and each jack sits under its thumb (`JacksMath.FlightOffset`: `flightGap` from the centre line, moved
  `flightReach` by its stick, in the hull's frame). So the fish ARE the controls: one up and the other down is a roll,
  both the same way a pitch or a yaw, apart is faster and together slower, which is exactly what the flight reads from the
  same sticks (YDiff, YSum, XSum, XDiff). Where they fly is where the board goes. Sticks apart also swim the fish up to
  `netSpread` further apart, so the **net** between them opens wider and taller (§1.1).
  - **Call** is Button2 (B, T; Input 7): it TOGGLES whether the herd is drawn to the plane (Garrett, 2026-10-24: "in
    netting mode it toggles plane attraction for fauna of your domain"). On at take-off; the herd glows while it is on.
  - **Fast travel** to a decoy is a trigger (Input 2 the left's, Input 1 the right's, or a lifted thumb) and Input 4
    (both thumbs lifted: the one placed last): the hull and the jacks fly there fast, keeping their heading; the decoy
    stays.
  - **Wide** (Button4, Input 14) does nothing while netting.
  - No ramp boost (Garrett, 2026-10-25: "lets remove the rhino speed buff at full speed and straight"): Input 0 is
    unbound and the Rhino's `RampBoostActionRegistry` is stripped, so the AI's boost policy cannot arm it either. The hull
    flies at the Rhino rig's plain speed.
- **Landing.** Switch back: the jacks start herding where they flew, laid onto the hull's plane, and the hull holds there.
- **The AI** (an AI hull, or a human's hull the app shell parks on its AI) herds for `aiHerdSeconds` with the jacks'
  drive-and-collect AI (`JacksAI`, aim points only, as thumbs), then nets for `aiFlightSeconds` on the hull's own
  `AIPilot` (so the board moves with the fleet's targeting and the net sweeps what it flies through), its herd drawn to
  the plane so it follows.
- **The views.** Herding, an orthographic camera looks straight down the board and never turns with the hull (the lab's
  Steady camera); the game camera is the small square. It is zoomed out until, on a full-size phone (`phoneShortMm` 71 mm,
  a 6.7-inch screen's short side), a big thumb (`thumbMm` 22) barely covers the smallest ring: the ring's diameter is
  `thumbCover` (0.9) of the thumb (Garrett, 2026-10-21; `JacksMath.ThumbViewSpan`, about 1,147 u across the short side at
  the shipped numbers). The span is the same on every device, so every player sees the same board. Flying, the game
  camera has the screen and the herding view is the small square, framed toward the hull (`flightHullWeight`). It draws
  the way the game camera does: post-processing (bloom), anti-aliasing, HDR and the same volumes, copied from the game
  camera every frame (`JacksView.AdoptPilotLook`).
- **The herding view, 2026-10-24** (Garrett: "the herding view should have a view that is zoomed in a bit more and
  follows the jacks. the player can use a button to zoom out to the whole cell if they want to have a view of more
  fauna"): the span is `herdViewZoom` (0.75) of the thumb-framed one, and the framing follows the jacks
  (`herdFollowJacks` 1; 0 frames the herd as before). Wide (the Space card; Y, F) eases out to the whole cell
  (`cellViewMargin` x its diameter) and back (`viewZoomEase`); thumbs still aim the jacks at any zoom.
- **One view at a time** (Garrett, 2026-10-24, round 2: "we can remove the pip from both modes"): herding, the jacks'
  view has the whole screen and the game camera sleeps under it; netting, the game camera alone. The small view and its
  double tap are gone; the swap is the D-pad, the arrow keys or the two-finger swipe. Herding, the view also zooms out as
  the jacks spread ("as the sharks fly apart in herding mode the camera should zoom out so they never collide with the
  screen perimeter"): both jacks, with `herdJacksMargin` (40 u) round each, stay inside `herdJacksFill` (0.8) of the view,
  out quickly and calmly back in.
- **The netting view** (Garrett, 2026-10-24: "the jacks and anything in their net should be in the camera view while
  netting"): the chase camera backs off until both fish, the bag and every netted crystal fit in `netCameraFill` (0.8)
  of the view (`JacksMath.ChaseDistanceFor`), never closer than its authored distance, easing at `netCameraEase`, and goes
  home on landing (the Shrike's camera rule: written only while the hull is the camera's target, handed back only if
  nobody else wrote it).
- **Stability while herding** (Garrett, 2026-10-21: "we need to make things as stable for the jack player as we can"):
  the hull holds still, so the board does not move under the thumbs at all; the view frames the herd alone
  (`herdHullWeight` 0), or the jacks with no herd; the pan, once the herd leaves the slack box, eases at `viewPanEase`
  (1.2/s). The board's own ease (`herdPlaneEase` 0.25/s, `flightPlaneEase` 3/s) only settles the last turn of a landing.

### 1.1 The net (Garrett, 2026-10-24)

> the spinning jack in the middle doesn't work. lets make it a net that opens wider with xdiff. if an opposing team's
> fauna is netted it steals it and then goes through the net shielding or lighting its prisms like any other fauna of
> the player's domain. all five crystals can be relocated using the net. prisms are collected by the net but when they
> collide they are destroyed like always. when the jacks switch to hearding mode from netting mode the net goes away and
> they can collect the crystals netted ... if an opposing vessel is netted it should remove some of their crystals which
> remain in the net.

The net hangs between the fish while they net (`JacksMath.NetFrame`): its mouth is the line from one fish to the other,
`netMinHeight`..`netMaxHeight` tall as it opens, and its bag trails `netDepth` behind, deepest in the middle. How far it
is open is read back from how far apart the fish are (`NetOpenFromGap`), so every peer nets and draws the same net from
the published jacks; it opens only once the fish are in formation (`JacksNet.Formed`), so a take-off from a wide board
never strings a net across it. Its bag hangs straight back from the hull's nose and SWINGS OUT against the hull's turn
(`JacksMath.SwingBag`: `netSwingSeconds` 0.6 of the nose's turn rate, eased at `netTrailEase`, leaning at most
`netSwingMax` 1.2, about 50 degrees): turning up it swings down, turning left it swings right, at any angle between, for
as long as the turn lasts (Garrett, 2026-10-25: "i would expect this to work in any direction (including combinations like
30 degrees vs 70 degrees) and it should work whether changing or maintaining directions"). The bag may lean along the fish
line too (`NetFrame.Bag`, a skewed bag; the mouth stays between the fish). Rounds 1-4 hung it against the net's averaged
path, which leans INTO a held turn and could not lean left or right at all. `JacksBodies` draws it as faceted strand tubes, wound outward and in the
jacks' own domain material (round 1 wound them inward on the sword's Fresnel blade and the net vanished from some angles),
that grow open and shrink away (`netGrowRate`). What crosses the mouth (`JacksNet`, run on every peer for what that peer simulates):

- **Crystals, all five**: caught and carried, settling `netSettle` into the bag. While netting the hull's own touch
  leaves crystals alone (Garrett, 2026-10-25: "lets allow them to net the omicrystal and move it around"): the Jacks'
  root is an `IHullContactGate` (`JacksBodies.TakesCrystalsByContact` and `TakesPrismsByContact`, off while netting, read on every peer from the
  replicated mode), and both halves of a crystal contact honour it (`VesselImpactor`'s omni case, `OmniCrystalImpactor`
  and `ElementalCrystalImpactor` on the server and every peer), so the omni crystal the hull flies into is netted, not
  collected. A deliberate pickup (a sweep: the herding jacks, the race rings) is never gated.
- **The jacks collect, the net relocates** (Garrett, 2026-10-25: "if either jack hits the omnicrystal it should still
  produce the serpent, but if it nets the omnicrystal it should move it. same with the elemental crystals the hulls can
  collect in either mode, but the net allows them to be relocated"): in EITHER mode a fish that touches a crystal collects
  it (`JacksNet.CollectByJacks`: herding across the board, `jackCrystalReach`; netting round each flying fish,
  `jackNetCrystalReach` 10 u), so an omni crystal a jack hits still bears the worm. Netting, the fish take first and the
  net carries what crosses its mouth between them. On landing the net goes away and they
  stay where they were; the cell's omni crystal is put there on every machine by the hull's owner
  (`CrystalManager.RelocateCrystal`, new: a slot write on the network manager, no second bloom where it already is).
- **Our own prisms** (Garrett, 2026-10-25: "when prisms of the same domain are netted then they should be briefly lit not
  moved or destroyed"): lit (`Prism.SetColorLift`, `netLitLift` 0.6, fading in and out over `netLitFadeSeconds`) while the
  net crosses them and `netLitSeconds` after; never moved. A look on each machine; nothing reads it.
- **Prisms** (Garrett, 2026-10-25: "lets have prisms move and be dragged by the net. they should only be destroyed when
  they collide with other prisms"): caught and DRAGGED where the net holds them (up to `netPrismCap`, 48), each step a move
  of the prism-motion fundamental (`Docs/PRISM_MOTION.md`, the third mover): a dragged prism that runs into another prism
  destroys both, scored to us (`PrismMotion.Annihilate`), and the net's own prisms are movers of the same step, so the
  catch never breaks itself. A netted prism sinks into the mesh (`netPrismSettle` 1, at `netSettleEase`) and turns to lie
  flat against it, its length along the fish line and its face to the mouth (`netPrismTurnEase` 6/s;
  `JacksMath.NetFrame.SurfaceAt`; 2026-10-25: "when prisms are stuck in the net their orientation should match the
  expectation of being stuck in the net"). **Owner-less mass is netted like any other** (round 10, 2026-10-25: "netting mode is still
  having no interaction with prisms when it should be dragging them like the crystals"). Rounds 5-9 skipped every
  owner-less plain prism (`Prism.IsEnvironmentOwned`, "no player name") as a mode's fixed track - and in the Toybox's boot
  cell, Garland, the whole bough the player flies through is ~4,300 owner-less `SpawnablePrism`s laid by its
  `SpawnableGarland` (`PrismTrailBuilder.ConfigureLaid` names no player), while its plants are 13. So the net touched
  nothing in all three rounds. The hull breaks those prisms in every mode; an indestructible track (Skim Race's) is
  super-shielded, which the net never moves.
  A PLANT's prism is plucked and dragged too (2026-10-25: "the net did not interact with any
  prisms. it should be dragging them just like the crystals"; round 5 skipped all living mass, and a cell's prisms are
  mostly its plants'): `HealthPrism.LeaveAsSkeleton` frees it from its plant as ordinary cell mass (it stops swaying, the
  plant forgets it, the mass stays), then it is a mover like any other. A creature's body is never dragged (the creature
  is netted), nor super-shielded mass, a prism still growing in, an environment-owned track, or our own fresh trail. Once
  `netPrismCap` is full, a prism crossing the mouth meets the dragged ones and both break: a big plant can fill it. On landing they stay where they are. (Round 1 destroyed every prism the net
  touched; round 3's drag never showed, because the invisible hull in the middle of the mouth rammed every prism first:
  the hull's prism contact is gated too while netting, `IHullContactGate.TakesPrismsByContact`, on both halves of the
  contact (`VesselImpactor`, `PrismImpactor`), so the netting hull neither rams prisms nor is hurt by them.)
- **Creatures**: one of another domain is STOLEN (`Fauna.StealInto`: its domain and body prisms become ours, replicated by
  `FaunaNetworkSync`'s stolen domain; only species that recolour their whole body, `Fauna.CanBeStolen`, the light fauna).
  Then, like any creature of ours that goes through the net, its plain body prisms are shielded (`Fauna.ShieldBody`,
  replicated by an RPC; danger prisms stay danger) and it joins the herd, so it wears the herd's mark and its glow
  ("lighting"). This is the one-colour law's third sign-off (`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`).
- **Rival hulls**: a rival hull in the net loses `netRivalTake` of each element (a crystal each), at most once per
  `netRivalCooldown`, knocked loose backward into the bag through `ElementalTransfer.ApplyAllAuthoritative` (the networked
  Eject: PvP is petals only), decided where our hull is; the net then catches the crystals.
- **The omni crystal's worm** (Garrett, 2026-10-24: "lets have the omnicrystal produce an omnivorous worm fauna in the
  player's domain"): collecting an omni crystal (with the hull, or a jack while herding) bears the worm colony (it eats
  prisms and creatures) in our domain through the platform's one spawn seam (`VesselSpawnFaunaByCrystalEffectSO`, in the
  Jacks' crystal effects). The worm colony is not network-synced, so each peer bears its own, as every local species is.

The near-field skimmer (the Rhino's sword capsule) is hidden and made a small sphere at the hull (`skimmerRadius` 4); the
jack it was for a day (2026-10-23) is retired, and its two arm colliders came off the prefab (`net_retrofit`). Since
2026-10-25 it carries the fleet's EMPTY skimmer container (`NoEffectsSkimmerImpactorDataContainer`; Garrett: "remove the
rhino skimmer effect"): the Rhino's container was still giving it the sword's prism damage (its Space-5 Breaker), the
sword's combat hit and its steal on rival hulls. It only collects elemental crystals now (their effects are the
crystal's own). The invisible hull's TOUCH leaves crystals and prisms alone in both modes once the jacks are seated
(`IHullContactGate`): the fish collect, the net carries, drags and lights. Its five colliders still meet other HULLS
(jousts, rams), which is a call for the playtest.

## 2. Elements (Garrett, 2026-10-20)

| Element | Scales | Level 5 |
|---|---|---|
| Charge | the share of DANGER creatures: 0% at -5, 100% at 15. A danger creature that touches a rival hull stings it (a scored Strike through `CombatHitDrain`) and is spent | **Attack**: the herd goes for rivals within `attackRange` and every creature that touches one bites it (a Strike) |
| Mass | **The Call** (round 10: "mass should be the herd calling ability in herding mode and the plane attraction toggle in net mode"; B, T). Scales the herd's birth rate: a birth takes the species' own feeds at -5, a third of them at 15 | **Mass Clusters**: every `pooEvery` one creature lays `pooCount` prisms of our domain |
| Space | **Wide** (round 10: "Space be the zoomed out view in herding mode"; Y, F; herding only since round 11: "remove the space ability in net mode"). Scales every herder's ring: the thumb floor at -5, +6% of it per level above | **Eat Shields**: the herd eats shielded prisms (never super-shielded) |
| Time | **Decoys** (round 10, 2026-10-25: "in herding mode the triggers should drop decoys. in netting mode the triggers should fast travel to the decoy"): herding, a trigger lays the decoy at the jack on that side (a phone: a lifted thumb); netting, a trigger flies the hull fast to it (round 11: "don't teleport, move quickly"). There is ALWAYS one decoy (2026-10-25: "there should always be at least one decoy available"): laid between the jacks when they land and never spent by the travel. Time scales the herd's speed, +6% per level, floored at x0.4. (Rounds 8-9 made Time a herding DASH of both jacks to the decoy; retired with `dashSpeed`, `dashGap`, `decoyPlaceHold` and `decoyTapPixels`.) | **Second Decoy** (2026-10-25: "lets have the upgrade give the player a second decoy controlled by the second trigger (in placement and which one you fly to)"): each trigger has its own decoy - the left trigger lays the left one (at the jack further left on the herding view) and flies to it, the right likewise; a phone's both-thumbs lift flies to the one placed last. It replaced the Flying Decoy (netting, A dropped the decoy; Input 6 is unbound) |

Each upgrade is its own gate with its element written out (`JacksExecutor.ChargeUpgraded` .. `TimeUpgraded`), so
`/element-ability-table` reads all four as live (2026-10-25; through one `Upgraded(e)` helper it read three as prose). The
table still reads no scaling for Charge, Mass and Time, and Space as the hidden skimmer's 30 -> 50: the real curves are the
lab's formulas in `JacksMath` (`DangerShare`, `FeedsPerOffspringScale`, `Ring`, `SpeedMultiplier`), which no channel of
the tool reads, and the skimmer's curve is switched off on the Jacks (`Scale.Enabled` 0, an override of the nested
variant the tool reads past). The map's words are rewritten by the tool whenever they still carry an older round's words
(`STALE_MAP_WORDS`). On the HUD (`JacksHUDController`): the Charge card's gauge is the danger share; the Mass card is the
call - herding, lit while it pulls the herd, its cooldown on the veil; netting, lit while the plane's pull is on - with
the herd's size on it and its fill on the gauge; the Space card is lit while the herding view is Wide; the Time card flashes
(`decoyFlashSeconds`) when a decoy is laid and is lit for the whole of a fast travel.

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
- **The jack between the fish** (2026-10-23) was retired the next day for the net (§1.1).
- **No jets.** The Rhino's vestigial jets are stripped (Garrett, 2026-10-23: "the vestigal jets from the rhino should be
  gone"): the ten `VesselJet`s (two pods, eight body nozzles), the pods' `JetFX`, the arc lightning, `JetTest` and the two
  legacy jet particle systems. `VesselTailAndJets` stays on the root for the tail.
- **The herd lights up** (Garrett, 2026-10-23: "fauna that get herded should light up when in herding mode. fauna that
  get called should light up when flying"): while it is drawn to the plane - always while herding, and while netting when
  the call has the plane's pull on (2026-10-24: the netting call is that toggle) - every herded
  creature's spindle colours are multiplied by 1 + `herdGlow` (1.5, HDR, so the bloom catches them). It rides the
  leash's mark (`IFaunaLeash.TryGetLeashMark`'s `glow`; `Spindle.SetHueLean` mints a shared material per mark and glow,
  so a lit herd still batches); the executor calls `Fauna.RefreshLeashMark` on each member when the state changes
  (`JacksExecutor.SetLit`, on the peer that simulates the creatures; on the host the mode of a client-owned hull comes
  from `JacksBodies`' flying and plane flags). The mark goes to puppets in one `uint` (domain, glow, lean; protocol 13). The
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

- **Playtest the net** (2026-10-24, no playtest yet): its size against crystals and the chase camera (`netSpread`,
  `netMinHeight`/`netMaxHeight`, `netDepth`), whether prisms should only shed shields rather than be destroyed (today a
  hull contact), the rival take (`netRivalTake` 0.1, `netRivalCooldown` 2), the herding zoom (`herdViewZoom` 0.75), and
  the worm colony as the omni crystal's creature (the shipped "Worm Colony Kaiju" config; a smaller worm may read better).
  Round 10: a dragged bough prism destroys whatever it runs into, which in Garland's packed bough may read as the net
  chewing through it - watch it in the playtest; `netPrismCap` and `wideNetScale` are the levers.
- **A netted crystal's place on other peers.** Every peer carries the crystals its own net catches; a peer whose copy of
  the jacks lags may catch a crystal a frame later or not at all. The owner's release puts the omni crystal right for
  everyone; a loose elemental crystal is local to each machine already.
- **Playtest the two modes** (the decisions made without one, 2026-10-21): the flight formation (`flightGap` 14,
  `flightReach` 10, `flightModelScale` 1) against the Rhino's chase camera; whether the take-off jump to the jacks reads
  well; whether the parked hull's colliders (invisible, where the jacks landed) should switch off while herding; the AI's
  14 s / 6 s rhythm. All are numbers on `JacksConfig` / `JacksBodies` except the colliders, which would be code.
- **The card shows one jack.** `render_cards.py` renders one FBX; a pair would read as the vessel better.
- **Danger stings on a client-owned hull.** A contact is decided on the hull's owner (`ElementalTransfer.IsDecidedHere`)
  while the herd is leashed on the host; whether the owner sees the creatures close enough to score is for the
  host-client test.
- **Crystal hull fusion** still bakes the Rhino's hull shape (the four Jacks entries are the Rhino's).
- `element_ability_table.py Jacks` reads the four level-5 gates (2026-10-25) but not the scaling (§2): the curves are
  `JacksMath`'s lab formulas. Teaching the tool a channel for them, or moving the curves onto authored endpoints, is open.
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
