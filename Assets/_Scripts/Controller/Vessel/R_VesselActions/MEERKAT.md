# The Meerkat: the wormhole vessel (`VesselClassType.Meerkat = 28`)

Cloned from the Vessel Studio (2026-10-27, `/artifact-to-unity`): the Meerkat page of the one Vessel Studio artifact
(https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa, source `Docs/Studios/VesselStudio/meerkat.html`, design
`Docs/Studios/MEERKAT.md`). The artifact is the source of truth: tune there, then clone (`studio_to_unity.py`).

Yash, 2026-10-27: *"This will be Meerkat, a new vessel ... with all its things similar to the Stoat. Just rename to
Meerkat and keep only one ability of the wormhole."* It is the Stoat studio's Gate mode, made its own vessel.

## 1. How it flies

**One thumb** (`MeerkatVesselTransformer : SingleStickVesselTransformer`). The left stick's Ease(2 × stick) yaws and
pitches at full throttle and banks into the turn, which leaves the right thumb always free. The studio's flight is on
the prefab:
- `PitchScaler` = `YawScaler` = 120 (`pitchYaw`);
- `RollScaler` 15.6 (`bankShare` 0.12 × `roll` 130);
- `DefaultThrottleScaler` 60 (`cruise`).

**The wormhole, its one ability** (`MeerkatWormholeExecutor`, numbers on `MeerkatWormholeConfig.asset`):

| Input | What it does |
|---|---|
| Right stick | The bend's direction (any way) and angle, as far as it is pushed, up to `bendMaxDegrees` (150°) |
| LT (`LeftStickAction`) | The straight shaft before the bend: `shaftMin` released to `shaftMax` squeezed. A pad's analog trigger is read live; a key or touch ramps to full in `keyRampSeconds` |
| RT (`RightStickAction`) | Forms the tunnel once the reticle is 60% grown, and dives in. The last tunnel collapses at once |
| X (`Button1Action`) | The stance (`StoatHoldAction`), kept from the Stoat; forming a tunnel ends it |

- **The shape** (round 22, Yash: *"it should always bend from the center rather than bending from one of the ends to a
  small knot"*): the tunnel is still S (LT's shaft) + `bendRadius` × θ (the stick's turn) long, but its centreline is ONE
  conic arc, symmetric about its middle, the conic's vertex where it turns sharpest (`MeerkatWormholeMath.Conic`,
  `OvalOf`, `At`, `BendAngle`).
  - How oval it is grows with its length: `ovalShortest` (0.2, nearly a circle) at the shortest tunnel to `ovalLongest`
    (1.1, a little hyperbola) at the longest.
  - Its middle never turns more than `ovalMaxSharpness` (8) × as sharply as its ends, so a long, sharply turning tunnel
    stays a smooth oval and never a knot at its centre; gentle long turns are hyperbolas, with nearly straight arms.
  - Round 7's straight shaft, then a hook at the far end, is gone. The reticle, the bridge, the tree, the warp tube, the
    ride, the AI's plan and the water pipe's lens all follow the same arc.
  - The throat (`throatDiameter`, 8 u) and the entry and exit (`mouthDiameter`, 26 u since round 10) are fixed: set on the
  config at edit time, never by the sticks.
- **The reticle** is always on the nose, `reticleAhead` ahead. Only its pilot sees it.
  - It eases toward what the right stick and LT ask for over `reticleSmoothSeconds`, so it glides rather than snaps.
  - It is the wormhole's own wireframe (round 9, Yash: *"the reticle is the silhouette of the geometry"*): the bridge's
    `bridgeRings` × `bridgeSpindlesPerRing`, `bridgeRibs` and `bridgeSpindleThickness`, in the same places, condensing
    the same way (`MeerkatWormholeView.BridgeFrame`), with no glass and no path.
  - It is dim at its way in, by your nose, and brightest at its far end, so the bend stands out
    (`reticleNearLight` 0.25, round 11).
  - Its own controls are its brightness (`reticleIdleAlpha` at rest, full while you shape it) and its colour: your
    domain colour toward white by `reticleWhite`. The bridge's spindles take `bridgeWhite` the same way.
- **The ride.** `MeerkatVesselTransformer` lets the tunnel fly the hull (`StepRide`). It dives straight to the mouth,
  then runs the shaft and the bend mouth to mouth, turning only on the bend. Its speed is the speed before the tunnel × `rideEntryMultiplier` going in (1), rising
  to `rideMidMultiplier` at the throat (2.5) and easing to `rideExitMultiplier` at the way out (1.2, round 6). It comes out
  on the tunnel's last heading, a little boosted, and eases back to cruise.
- **Out.** The reticle comes back after `reticleDelaySeconds`, growing out of its centre over `reticleGrowSeconds`.
- **The camera** (round 10, Yash: "the cameras, why offset should always be zero, the vessel should be in the center of
  the screen and this way we don't need to lerp the camera during entering into the tunnel. It is like a sparrow"):
  - `MeerkatCameraSettingsSO` (forked from the Stoat's, which is unchanged): offset (0, 0, -21), no look lift,
    `chaseEaseRate` 0 with mode 0, so `CustomCameraController` hard-attaches it. The hull is always at the centre of the
    screen. `author_meerkat_assets.py --check` fails if the asset gains a height offset or a lag.
  - Riding (`MeerkatWormholeExecutor.StepRideCamera`): the camera stays the follow camera's own distance behind the hull,
    but ON the tube's centreline, looking at the hull, through `CustomCameraController.Spectate` with no blend. On the
    straight run in it is where the follow camera already was, so the hand-over is a cut nobody sees. Round the bend it
    never cuts the chord out of the tube.
  - Out of the far mouth it keeps to the tunnel's line until it is past the mouth itself, so it does not jump sideways.
  - The page measured: the hull exactly at screen centre, flying and riding; the camera within 0.29 u of the line, in
    the tube and past the exit; its step at RT and at the exit no larger than a normal frame's.
  - Round 8's `rideCameraBack`, `rideCameraLookAhead` and `rideCameraBlendSeconds` are retired.
- **The dolly zoom.** While a tunnel flies the hull, the game's speed tunnel (`VesselSpeedTunnel`: the narrower field of
  view and relaxed Panini every vessel gets with speed) is `rideSpeedTunnelScale` (5) × its normal strength
  (`Docs/SPEED_TUNNEL.md` § The ride scale). The push-in peaks at the throat, where the ride is fastest.
- **Its life.** It forms out of its centre over `formSeconds` (never longer than 0.9 × the dive), stays `staySeconds`
  after its rider is out, then collapses to its centre over `collapseSeconds`. It then fades over 0.7 s.

## 2. What everyone sees

- **The bridge** (`MeerkatWormholeView`): rings of spindles round the throat (`bridgeRings` × `bridgeSpindlesPerRing`)
  and ribs along it (`bridgeRibs`), over a glass surface (`bridgeGlass`), in the maker's domain colour.
  - They condense in from the centre as the tunnel forms and evaporate as it collapses.
  - A shimmer runs from the way in to the way out (`bridgeShimmer`).
  - Spindles fade within 30 u of the camera.
  - `bridgeVisibility` 0 hides it.
  - A spindle is a six-sided bipyramid drawn additively. The studio's note that the game would build it from `Spindle`
    prefabs is not done: a prefab per spindle is ~280 objects per tunnel.
- **The bent space** (off since round 4: `warpStrength`, `dentStrength` and `rippleStrength` ship at 0, and the pass
  then never runs; `MeerkatWormholeLensPass`, `MeerkatWormholeLens.shader`, `MeerkatWormholeWarp.shader`):
  - Each tube's wall bends the world behind it (`warpStrength`).
  - Each mouth dents space as wide as the mouth (`dentStrength`), fading within 3 mouth radii of the camera.
  - A collapse ripples the screen's EDGES only (`rippleStrength`, from `rippleEdge` out past the corners, over
    `rippleSeconds`).
  - The pass is injected from script while a tunnel or ripple is live, the `BlackHoleLensPass` route, and draws nothing
    otherwise.
- **The network.** The owner publishes each tunnel it forms, and its rider's way out, through
  `MeerkatWormholeNetworkRelay` (one `NetworkVariable`, protocol version 18: round 7 added the shaft and the bend radius). Every peer runs the same life from
  there.

### 2.1 Before and after RT (Garrett, 2026-10-27, from the Tube Lens Lab)

**One-click swap** (Yash, 2026-10-27): the config's Inspector has **YASH'S LOOK** (`formedLook` Bridge: the spindle
bridge after RT) and **GARRETT'S LOOK** (ContourField), from `Editor/Studios/MeerkatWormholeConfigSOEditor.cs`. They set
that one switch. Round 9 retired `reticleLook`: the reticle is always the wormhole's wireframe. The contour field
measures the tunnel's centreline (`MeerkatWormholeLens.shader` `MWTube`; round 22: 25 points along the oval arc, with circle
arcs between them, within 0.07 u of the exact arc).

Garrett flew the Tube Lens Lab (https://claude.ai/artifact/PEScFn528pACfFq2KVNJRF, source
`Docs/Studios/StoatTubeLensLab.html`) and picked a look: *"this looks so good it doesn't need the grid or anything else,
lets the lensing alone sell the effect."* Two switches on the config hold it, and both old looks are one setting away:
- **Before RT** (his `reticleLook` Bridge; since round 9 the only reticle): the reticle draws as the formed tunnel's
  bridge: its spindles (`bridge*` counts and thickness) and shimmer, in its pilot's domain colour. It condenses out of its
  centre as it emerges, at the reticle's own brightness. No lime path. Round 9 also took its glass away (a bare
  wireframe).
- **After RT, `formedLook` = ContourField:** a formed tunnel draws nothing: no bridge, no glass. It is only bent space.
  `MeerkatWormholeLensPass` ray-marches each pixel through a thin shell on the wall, from `fieldInner` × r inside to
  `fieldOuter` × r outside, graded to zero at its edge (`fieldProfile`). Inside the shell the ray is turned by a field
  whose lines on the wall are the tube's own contours: `fieldPitch` 0° runs along the long lines, 90° round the rings.
  - `fieldDrag` pulls rays along the lines, `fieldTwist` turns them round, and `fieldGlass` × `fieldGlassIndex` refracts.
  - `fieldStrength` is about the bend, in degrees, of a head-on wall crossing; `fieldGrazing` evens out skimming rays.
  - `fieldFlow` In then out bends in at the way in and out at the way out.
  - `fieldRibs` gathers the field on `fieldRibRings` × `fieldRibLines` grid lines, which travel toward the exit at
    `fieldRibTravel` rings a second.
  - The tube is measured over its LIVE window, so the field ripples in from the centre and collapses with the tunnel.
  - The four tunnels nearest the camera take part. A vessel in front of the first shell a ray meets is not bent.
  `Bridge` brings back round 3's bridge and glass.
- **Numbers, round 11** (Yash, 2026-10-28: "a pleasant water ride experience through a water pipe. Transparent pipe
  ... I should not feel too much of warping ... performant"): a clear pipe of gentle glass, rendered against round 10
  in the Tube Lens Lab before choosing (`Docs/Studios/MEERKAT.md` Round 11).
  - strength 6, pitch 0 (along the flow, not round it), drag 0.5, twist 0;
  - glass 1 at Δn 0.05, grazing 0.9 (a ray skimming the wall from inside bends little, so the ride does not swirl);
  - flow Even (no sign flip mid-ride);
  - inner 0.1, outer 0.15, profile Smoother;
  - ribs 0.25 at sharpness 3 (soft ripples that travel toward the exit at 1.2 rings/s, 16 × 12): softer ribs also
    let the march take larger steps.
  - Garrett's pick was strength 18, pitch 90, drag 1, glass 0.4 at Δn 0.04, grazing 0.6, flow In then out, outer 0.12,
    ribs 0.9 at sharpness 8. Yash's round-8 tune was glass 1.579 at Δn 0.078, grazing 0.883, ribs 0.155 at 11.12,
    9 rings.
- **Not in the studio yet.** These 18 fields are not on the Meerkat page and not in `studio_to_unity/meerkat.json`, so
  `--check` neither maps nor drifts them. Bringing them onto the page (a Before/After panel) and mapping them is Yash's
  next step; the lab's GLSL (`tube`, `prof`, `ribF`, `fieldDir`, `trace`) is what `MeerkatWormholeLens.shader` ports.
- **Cost.** Up to 256 march steps per covered pixel, at full resolution, in the existing lens draw. While a tunnel is
  ridden, every pixel is covered. Not profiled yet: profile before tuning (`/perf-hunt`), and move the march to a
  half-resolution offset target (as the tube warp does) if it shows.

## 3. The AI (input only, `/vessel-ai`)

The platform `AIPilot` flies the line. `MeerkatWormholeExecutor.Autopilot` adds the studio's `aiInput` / `aiGatePlan`,
line for line:
1. It waits until its reticle is back and its ring is more than `aiBentMinDistance` away. Only a bent tunnel is planned
   inside `aiStraightMinDistance`.
2. It tries 21 bends × 9 shafts (LT), and among those whose way out heads within 0.26 rad of the ring with 60 u to
   spare keeps the one that reaches the ring soonest: the ride at its mean speed, the flight on, and the turn left at
   the hull's `YawScaler` (round 7).
3. It holds its line (no left stick) while the right stick bends the reticle and LT squeezes for `aiShapeSeconds`.
4. It waits for its eased reticle to catch up (within 0.03, 1 s at most).
5. It presses RT through the REPLICATED press path.

`aiNoise` bends it off the mark (0, Hard, by default). Easy and Medium race on the gate race's own handicap
(`GateRaceHandicap`) in Burrow Run. `aiWormhole` off flies without tunnels.

## 4. Where it is offered

- **The Toybox Vessel Changer (lava lamp):** `ToyVesselRoster.Default` + `Vessel Prefab Container`.
- **Its Time mini-game:** `GameModes.BurrowRun = 71` (`Arcade/BURROWRUN.md`), in the arcade list.
- **Its class card** (`SO_Class_Meerkat`, the Stoat's icons as placeholders) is in `SO_Classlist_All` / `_Classes`.

## 5. Stand-ins and open slots

- **Hull:** the Stoat's model and lope, as in the studio. A real Meerkat hull is `/vessel-model` work.
- **HUD:** the Stoat's `StoatHUDVariant` with `MeerkatHUDController`: the Time card fills as the reticle grows back
  (ready once RT would form) and empties along the ride.
- **The element map** (`ElementalAbilityMaps/Meerkat.asset`): four OPEN design slots. The tunnel's size already reads
  the hull's Space level (§8, Garrett's surface area), but no map row declares it. Which element the wormhole is,
  the other three abilities and the level-5 upgrades are not designed (`/vessel` §3: never invented).
- **No `[CliCommand]`, no Unity tuning window:** tune in the artifact and clone. In play mode the
  `MeerkatWormholeConfig` asset's inspector changes are read live, for a quick look.
- **Touch:** no per-hull phone profile yet (`TouchInputStrategy` raises RT/LT the fleet's way; the right stick on a phone
  is not mapped for this hull).

## 6. Files

| Piece | File |
|---|---|
| Arithmetic (held to the page's goldens) | `MeerkatWormholeMath.cs`, `Tests/Editor/MeerkatWormholeMathTests.cs` |
| Config, actions | `Data Containers/MeerkatWormholeConfigSO.cs`, `MeerkatWormholeActionSO.cs` |
| Executor (reticle, form, ride, life, AI) | `Executors/MeerkatWormholeExecutor.cs` |
| Flight and ride | `Controller/Vessel/MeerkatVesselTransformer.cs` |
| Network | `Controller/Vessel/MeerkatWormholeNetworkRelay.cs` |
| Look | `MeerkatWormholeView.cs`, `MeerkatWormholeLensPass.cs` (with the contour field, §2.1), `_Graphics/Materials/Graphs/MeerkatWormhole*.shader`, `Resources/MeerkatWormhole*.mat` |
| The contour field's lab | `Docs/Studios/StoatTubeLensLab.html` (https://claude.ai/artifact/PEScFn528pACfFq2KVNJRF) |
| HUD | `UI/Controller/MeerkatHUDController.cs` |
| Prefab and registrations | `Tools/Build/author_meerkat_assets.py` (`--check`, `--self-test`) |
| The roots (§7) | `MeerkatRootMath.cs`, `MeerkatRootNetwork.cs`, `MeerkatOmnicrystalField.cs`, `Tests/Editor/MeerkatRootMathTests.cs` |
| The tree and the roots' look (§7) | `MeerkatTreeMesh.cs`, `MeerkatTreeView.cs`, `_Graphics/Materials/Graphs/MeerkatTree.shader`, `MeerkatRootInside.shader`, `Resources/MeerkatTree.mat`, `Resources/MeerkatRootInside.mat` |
| The swallow (§8) | `Executors/MeerkatSwallow.cs` (run by the executor; its arithmetic is `MeerkatWormholeMath.PullVelocity` / `CarryDistortion` / `SurfaceAreaThroat`) |
| Studio map | `Tools/Build/studio_to_unity/meerkat.json` (104 keys, `--check` 0 drift) |

## 7. The tree and the roots (cloned 2026-10-28 from the artifact's rounds 13-21)

Yash, 2026-10-28: *"in our YS bleeding edge, we already have a effect created. That's the very subtle watery tube. So that
one you need to keep in the project. Then the second one is this tree and this root ... the tree is just you get to travel
faster, but the OmniCrystal collection will actually trigger the root creation, the network of roots, just like in the
artifact. And we can navigate the root ... it need to be easily navigable. Our controls inside the root will be controlled
by left thumbstick."*

- **The water pipe stays.** `formedLook` ContourField and its round-11 numbers (§2.1) are unchanged: a formed tunnel is
  still the subtle watery tube, and the bridge is still one switch away.
- **The tree** (the artifact's rounds 13-15 and 20; `MeerkatTreeMesh.BuildTree`, `MeerkatTreeView`, `MeerkatTree.shader`):
  - Every formed tunnel, everyone's, wears a dead tree: a trunk round the whole tunnel, `trunkTop` × the mouth at the way
    in (its top cut) to `trunkBase` × at the way out (its bottom cut), ridged (`trunkBark`, `trunkRidges`) and gnarled
    (`trunkGnarl`).
  - Its bore is the tunnel's own wall (round 20: the trunk is the wormhole): a smooth sheen with no lines, a little
    denser than the bark (`tubeInner`). Its cut faces carry `trunkRings` tree rings.
  - Up close (15-90 u from the camera) the bark's lines fade, so the ride is calm (Yash: "the outer part can be very rough
    and edgy, the inner tube need to be very soft ... calming, inviting").
  - `branchCount` bare branches grow out of its top `branchZone`, in pairs (`branchSymmetry`), leaning `branchUp`,
    `branchLength` × the mouth long, forking `branchForks` times.
  - It is drawn in the wall's watery glass (`trunkGlass` face-on, `trunkRim` at the `trunkFresnel` silhouette, the bark's
    fissures `trunkGrain` × `trunkGrainLines`), the maker's domain colour toward white, flowing slowly.
  - It shows exactly the tunnel's live window, so it ripples out with the tunnel and collapses with it. Each branch grows
    over `branchGrowSeconds` once the tunnel's front reaches its root. `trunkVisibility` 0 hides it.
  - The ride is unchanged: the tree is the tunnel's look, and the tunnel is how you travel faster.
  - Not ported: `trunkWarp` (the tree bending space would need the lens pass).
- **The Omnicrystal ability, the roots** (rounds 16-21; `MeerkatRootMath`, `MeerkatRootNetwork`, `MeerkatOmnicrystalField`,
  the executor's roots half):
  - **Omnicrystals** (`omniCount`, gold; `omniOn` 0 switches the ability off):
    - In Burrow Run, half way between the rings of a lap (`BurrowRunController.OnCourseRaised`).
    - In a scene with no course (the Toybox's Vessel Changer), the local Meerkat lays them ahead of itself, 300 u on and
      200 u apart. It lays them again 6 s after the last is taken.
    - Fly within 18 u of one and it is yours.
  - **The roots grow at once** from the hull, spreading every way (`labyrinthSpread`). Taken inside a tunnel, they grow
    the moment you are out (round 21, the artifact does the same).
  - **Their size is your last trunk's**: its shaft against the longest LT digs. The smallest trunk gives
    `labyrinthBranchPointsMin` forks, `labyrinthOpeningsMin` tips and `labyrinthRadiusMin` reach; the biggest gives the
    `...Max` numbers.
  - The roots are `labyrinthDiameter` wide, flaring to `labyrinthMouthDiameter` at the source and the tips, and taper to
    `rootTip` × toward the tips. They wander (`rootGnarl`) and dig out from the source over `labyrinthFormSeconds`.
  - They stay `staySeconds` once you are out, then collapse like a tunnel. A new omnicrystal's roots, or a new tunnel,
    collapse the last ones.
  - **Inside you fly on the left stick** (`MeerkatVesselTransformer` hands the move to `MeerkatWormholeExecutor.StepRoots`):
    - you cruise along the root on your own (`rootCruise` 45 u/s), your nose following it;
    - the left stick moves you round the root, the way it turns your nose outside (right is right; up is nose-up, so
      Invert Y applies), by `rootMove`, and `rootDrag` calms the drift;
    - RT pushes you faster (`rootThrust`, up to `rootMaxSpeed` 90);
    - the walls bounce you off (`rootBounce`); `rootGravity` (0 = off) can draw you to the floor;
    - move into a root at a fork to go down it; fly back past a fork and you are in the root before it;
    - out of a tip you fly on along it at your entry speed × `rideExitMultiplier`; back out of the source you leave
      facing out.
  - **The camera** rides the root's centreline 12 u behind you (into the root behind you when that is behind), half your
    way out to the wall, so it is always inside the root.
  - **From inside, a soft inner tube** (`MeerkatRootInside.shader`; Yash, 2026-10-28: "there should not be any black
    colored fill"): while you are in a network, its roots draw their inside as one slow warm tone, a little dense
    (`rootOpacity` 0.32), denser at its silhouette, lit by your lamp (`rootLamp`). The world outside still shows through,
    and far walls fade to clear `rootFog` (240) u off. Each root's ends stay open. From outside the roots are bark in the
    watery glass (`rootGlass`, `rootRim`, `rootGrain`).
  - **The AI** (input only): in the roots it holds RT and steers across the root with the left stick, toward the middle,
    or at a fork toward the side of the root that leads nearest its ring. A rookie (`aiNoise`) sometimes picks wrong.
  - **The network**: the owner publishes each network (seed, trunk size, the hull's frame, the omnicrystal taken) and its
    rider's way out through `MeerkatWormholeNetworkRelay.n_roots` (protocol version 22). Every peer grows the same roots,
    because `MeerkatRootMath` is double precision and ported from the page bit for bit (its `rng` included).
  - Not ported: the soil (`soil*`: strata walls, stones, hanging roots), `omniAtOnce` 0 (an omnicrystal arming the next
    RT), the labyrinth reticle's wireframe (`labyrinthRing*`, `labyrinthRibs`, `labyrinthRoughness`), and the fork call-out
    (`forkHintSeconds`, a HUD line). No HUD text was added (player-facing text needs authored fields first). The roots'
    sound is the empty `rootsEvent` slot.
- **The shaders** are a recipe now: `Docs/Shaders/WATERY_GLASS.md`.
- **Tests**: `Tests/Editor/MeerkatRootMathTests.cs` holds the rng, four layouts, the built roots and a 500-step flight
  (forks and bounces) to the page's own numbers. All 40 Meerkat math tests pass outside Unity (dotnet, with Unity
  stubs). The tree, the field, the camera and the look have not been opened in the Editor yet
  (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).

## 8. Garrett's soliton: the surface area and the swallow (cloned 2026-10-28 from the artifact's round 23)

Yash, 2026-10-28: *"first we're gonna revert the mass ability as Garrett has said. So the width of the tubes shell will be
dependent on this mass ability. The longer the tube, the thinner the tube gonna be ... make sure you follow Garrett's rules
... we also need to make so that the prism gets sucked in from this one side and gets sucked out on the other. And the
prisms will be distorted while traveling through this wormhole. The prisms and all the environmental materials that are
destructible or interactable ... sucked in. From a certain radius. And sucked out from the other side."*

**The size: Garrett's surface area** (`Docs/Studios/MEERKAT.md` § "Garrett's direction: the soliton kit"). At a given
**Space** level a soliton keeps one surface area A, so a longer tube is thinner and a shorter one fatter. Garrett puts the
area on Space and gives Mass the exit portal, so Mass no longer sizes the tunnel in the artifact's kits; round 17's
Mass sizing was never ported here, so in the game "revert" is only the area.
- `MeerkatWormholeMath.SurfaceAreaThroat` (the page's `kitThroat`): b = A / (2π L g(k)), g(k) = (k + asinh(√(k²−1)) /
  √(k²−1)) / 2, with k the authored mouth / throat (26 / 8 = 3.25), a 0.25 u safety floor and the mouth k·b.
- A = `areaL0` (6,000 u²) to `areaL10` (18,000 u²) by the hull's Space level (`ResourceSystem.GetLevel(Element.Space)`,
  read every frame the reticle is shaped, so a crystal taken mid-flight resizes the next tunnel; the AI plans with it
  too). A hull with no element levels keeps the authored fixed size.
- At Space 5 (A = 12,000 u²): the shortest tunnel (60 u) has a 33 u throat and a 108 u mouth; a 200 u tunnel 9.9 u and
  32 u; the longest (757 u) 2.6 u and 8.5 u. Remote peers get the owner's b and M with the tunnel, so everyone draws the
  same size.

**The swallow** (`MeerkatSwallow`, run by `MeerkatWormholeExecutor` for its tunnels, on every peer):

| Stage | What happens | Keys |
|---|---|---|
| Pull | Every prism (shielded too, never super-shielded) and every crystal (an omni crystal, or an elemental one not yet collected) within `suckRadius` of a standing tunnel's way in is drawn to it: `suckSpeed` × `suckRadius` / distance, up to `carrySpeed`, swirling round the axis at a third of that. Four looks a second. A plant's prism is plucked first (`HealthPrism.LeaveAsSkeleton`); a creature's body, a prism still growing in and our own fresh trail are left alone | `suckRadius` 90, `suckSpeed` 30 |
| Swallow | Within `swallowShare` × the opening radius it enters, its place across the mouth kept as a share of the radius | `swallowShare` 1.5 |
| Carry | Along the centreline at `carrySpeed`, held to the wall's narrowing, stretched along the tube to 1 + `carryStretch` at the middle (none at the mouths), squeezed across by 1 / √ of that so its volume is kept, and twisted `carryTwist` turns mouth to mouth (`CarryDistortion`) | `carrySpeed` 150, `carryStretch` 2, `carryTwist` 0.5 |
| Fire | Out of the way out along its last heading at `fireSpeed` (a crystal at half), its own scale back, gliding to rest over `fireGlideSeconds`. A tunnel that collapses fires out all it holds at once | `fireSpeed` 120, `fireGlideSeconds` 3 |

- **A moving prism** (`Docs/PRISM_MOTION.md`): a pulled or gliding prism that runs into a plain prism destroys both,
  scored to the tunnel's maker (`PrismMotion.Annihilate`); a shielded collision bounces (`PrismBounce`, the shared
  `shieldBounceRestitution` 0.6 = the page's `bounceRestitution`). Everything one Meerkat moves is a mover of the same
  step (`PrismMotion.MoverSteps`), so what it pulls in together never breaks itself, as the Jacks' net's catch does not.
  Carried prisms are not contact-tested (they are inside the tube).
- **Crystals** move on each machine, as the Jacks' net carries them; the machine that decides the hull relocates the
  cell's omni crystal where it comes to rest (`CrystalManager.RelocateCrystal`). A crystal another hull picks up or
  something else moves (more than 15 u from where the swallow left it) is let go.
- **Caps:** `swallowPrismCap` (256, a performance cap, not in the artifact) prisms per Meerkat; the prism-motion ladder
  picks boxes or spheres by the count.
- **Not modelled:** the Meerkat's own omnicrystals (`MeerkatOmnicrystalField`) are not swallowed; a carried prism is not
  hit by a rider in the same tube.

