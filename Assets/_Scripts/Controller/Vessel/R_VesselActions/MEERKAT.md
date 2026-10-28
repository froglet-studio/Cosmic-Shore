# The Meerkat: the wormhole vessel (`VesselClassType.Meerkat = 28`)

Cloned from the Vessel Studio (2026-10-27, `/artifact-to-unity`): the Meerkat page of the one Vessel Studio artifact
(https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa, source `Docs/Studios/VesselStudio/meerkat.html`, design
`Docs/Studios/MEERKAT.md`). The artifact is the source of truth: tune there, then clone (`studio_to_unity.py`).

Yash, 2026-10-27: *"This will be Meerkat, a new vessel ... with all its things similar to the Stoat. Just rename to
Meerkat and keep only one ability of the wormhole."* It is the Stoat studio's Gate mode, made its own vessel.

**Its four abilities are Garrett's kit** (§9, with §8's surface area and swallow, 2026-10-28; Garrett's design, approved by Yash 2026-10-28: *"Garrett has
already defined all the elemental abilities, so we're gonna implement those, and it also has the Space ability, which
pulls prisms inside the tube"*): Space **Soliton** (the wormhole, RT), Time **Faster Ride** (passive), Mass **Exit
Portal** (passive), Charge **Sentinel Mode** (Y).

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
- **HUD:** the Stoat's `StoatHUDVariant` with `MeerkatHUDController` (the LOCKED four-icon row, charge → mass → space →
  time): Charge, the sentinels standing out of the pool; Mass, the last exit portal's prisms still standing (of six);
  Space, the reticle growing back (full once RT would form); Time, the ride's progress through any soliton (in the ready
  colour while riding). The Stoat's view has no ready colour on its Charge and Space gauges.
- **The element map** (`ElementalAbilityMaps/Meerkat.asset`): Garrett's four rows (§9; Space's area and swallow are §8), written by
  `author_meerkat_assets.py`. `element_ability_table.py Meerkat`: 4/4 abilities, scaling 4/4, L5 gates 4/4.
- **The ability icons** are the Stoat's placeholders (`StoatHUDVariant`); the Meerkat's own four are `/ability-icon` work.
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
| Studio map | `Tools/Build/studio_to_unity/meerkat.json` (110 keys, `--check` 0 drift) |
| Garrett's kit (§9) | `Executors/MeerkatSolitonKit.cs` (the kit, its four L5 gates, the zap), `MeerkatSentinels.cs`, `MeerkatExitPortal.cs`, `MeerkatSolitonMath.cs` + `Tests/Editor/MeerkatSolitonMathTests.cs` (run outside Unity: `Tools/Build/meerkat_kit_harness/run.sh --self-test`), `Data Containers/MeerkatSentinelModeActionSO.cs`, `_SO_Assets/VesselActions/Meerkat/MeerkatSentinelModeAction.asset`; Space L5 Steal hooks `MeerkatSwallow` |

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

## 9. Garrett's kit: the four abilities (2026-10-28)

**Garrett's design, approved by Yash 2026-10-28.** The design is Garrett's (`Docs/Studios/MEERKAT.md` § "Garrett's
direction: the Meerkats and the soliton", on `cece/lucid-brown-ykcmhu`); the artifact models it under the Kit dropdown's
"Garrett's kit" (`Docs/Studios/VesselStudio/meerkat.html`, round 18), and this is a port of the page's kit functions and
numbers (`kitOverrides`, `kitAddSentinel`, `kitZaps`, `kitPortal`, `kitTryRide`; `kitThroat` and the swallow are §8's
round-23 port). The arithmetic is `MeerkatSolitonMath` (6 tests on the page's own numbers); the kit is
`MeerkatSolitonKit`, a component beside the wormhole executor, which (with `MeerkatSwallow`) carries only small hooks
marked `[Garrett's kit hook]`.

| Element | Ability | Input | Scales with the level | L5 |
|---|---|---|---|---|
| Space | Soliton | RT (`RightStickAction`) | the surface area `areaL0` / `areaL10` 6,000 → 18,000 u² (§8) | Steal |
| Time | Faster Ride | passive | the rush `rush` 1 → 1.4 | Boost Out |
| Mass | Exit Portal | passive | the portal's size `portalSize` 0.4 → 0.9 | Danger Portal |
| Charge | Sentinel Mode | Y (`Button4Action`, F on a keyboard) | the petals a zap takes `zapPetals` 0.1 → 0.25 | Six Sentinels |

Every number is the page's shipped value, by the page's own key (`MeerkatWormholeConfig`, its "Garrett's kit" headers):
the plain keys are mapped in `studio_to_unity/meerkat.json`; each ElementalFloat is the page's L0 / L10 pair.

### 9.1 Space: the Soliton (RT)

- **The wormhole IS the soliton**: a standing tunnel in space that anyone may ride.
- **Its surface area (the Space level) and its swallow and fire are §8** (round 23: `MeerkatWormholeMath.SurfaceAreaThroat`,
  `MeerkatSwallow`). This kit's own surface-area function and swallow (`MeerkatSolitonPrisms`) were removed in the merge
  with round 23, which is the one port: the numbers were the same (6,000 → 18,000 u², the 0.25 u floor, at least 10 u), and
  round 23 reads the integer Space level (0-10) where the kit read an ElementalFloat.
- **L5 Steal** (`MeerkatSolitonKit.Stamp` sets the tunnel's steal bit, published with it; `MeerkatSwallow.Swallow` steals):
  each prism the soliton swallows is stolen into the Meerkat's domain (`Prism.Steal`) the moment its run through the tube
  starts, so it leaves the way out already theirs. Super-shielded mass is never moved, so it is never stolen.
- **Shielded prisms bounce** (`PrismBounce`, rule 1a): on Ys-bleeding-edge since commit e6b5c988ec (2026-10-27).
- **Anyone rides it** (the page's `kitTryRide`, `MeerkatWormholeExecutor.TryRideAnySoliton`): a Meerkat, its maker or any
  other, that flies into a standing soliton's way in (1 u before the mouth to 10 u past it, inside the mouth, the nose
  within 72° of the tunnel) is carried through it at the speed it went in with, out on its last heading. A rider leaves
  another's soliton untouched (only the maker's ride starts its stay). Not re-entered within 1.5 s. **Only Meerkat hulls
  can ride**: the ride is flown by `MeerkatVesselTransformer`, and giving every other hull a ride needs a hook in the
  fleet's `VesselTransformer`, which is not contained (a follow-up for the transformer's owners).
- A fired prism plays `prismFiredEvent` (§9.6), hooked on `MeerkatSwallow`'s fire.

### 9.2 Time: Faster Ride (passive)

- **The ride through every soliton** (its own or anyone's) rushes faster: the throat's multiplier `rideMidMultiplier`
  (2.5) × `rush` (1 at Time 0, 1.2 at 5, 1.4 at 10), the page's `kitOverrides`.
- **L5 Boost Out**: every soliton is left at `boostOut` (1.5) × the exit multiplier (1.2 → 1.8 × the speed before the
  tunnel), and the hull eases back to cruise from there.

### 9.3 Charge: Sentinel Mode (Y)

- **Y toggles it** (`MeerkatSentinelModeActionSO`, a press-and-forget toggle: release does nothing, vessel rule 37).
  Off, no new sentinels; the standing ones stay.
- **While on, each new soliton leaves a sentinel** just back from its way in: an AFTERIMAGE of the hull, standing up (its
  nose to the sky, its back to the way in), baked once from the hull's own renderers and drawn with its materials, with no
  collider (a simple visual, as asked).
- **The pool is `sentinelPool` (3)**; with it used up the oldest sentinel retires, and its soliton with it.
- **A soliton with a sentinel lasts as long as the sentinel**: it is HELD (its stay never runs out, and a new tunnel or a
  root network does not collapse it).
- **The guard is a sphere of `guardShare` (1.5) × the opening radius** round the way in.
- **It zaps** at `aiZapRate` (5 a second; every sentinel is AI-guarded, seats are not built): the nearest RIVAL hull in
  the guard, or failing that the nearest of its own domain's unshielded prisms there, which it SHIELDS (so the soliton
  fires them out as bouncing ammunition). Nothing in the guard, it looks again in 0.2 s.
- **A zap on a rival hull** is a scored hit inside PvP-is-petals-only (`MeerkatSolitonKit.ZapVessel`): decided on the
  Meerkat's owner (`ElementalTransfer.IsDecidedHere`), admitted through `CombatHitDrain.TryAdmit` as a Bullet
  (`zapCooldownSeconds` 0, so every zap is admitted; a warded victim is neither scored on nor robbed; a silenced Meerkat's
  zaps are refused; the admitted hit SILENCES the victim, `SILENCE.md`), then `zapPetals` (0.1 at Charge 0 → 0.25 at 10)
  of a petal on each element through the economy's accrual (`ElementalTransfer.ApplyAllAuthoritative`, Eject:
  `ResourceSystem.AccrueElementalLoss` settles only whole petals), EJECTED as free crystals, and the hit on
  `Event_CombatHitStats`. The Sparrow's bullets take the same path at 0.1 of a petal. The zap is priced as a Bullet
  (1 point), so its score and its silence are a bullet's while its take grows with Charge.
- **L5 Six Sentinels**: the pool is `sentinelPoolL5` (6).
- **The AI** presses Y once (through the replicated press path, as a person would) to turn Sentinel mode on, and leaves
  it on.
- **Not built, and why:** the sentinel SEATS (taking a sentinel with the D-pad, the perched 2.5D view, tap to shield or
  zap) and the HOP (the sentinel and the flyer change places) need dynamic crew seats, which the crew system does not have
  yet (`Docs/CREW_STATIONS.md`); "one meerkat holds the elements, any other dies of an element hit" rides on those seats
  too. A sentinel zapping another Meerkat's sentinel (the page's "sentinels zap sentinels") and danger prisms killing
  sentinels are not ported either: sentinels end only by retiring or with their soliton.

### 9.4 Mass: Exit Portal (passive)

- **Once per soliton, when it forms**, it sets down six prisms on a regular hexagon square to the exit heading, its
  corners `portalShare` (1.5) × the exit radius out, each at its edge's midpoint and laid along the edge, so the six close
  into one outline (the page's `kitPortal`, `perpBasis`).
- **Their size** is the hull's own trail prism (x halved, as the fleet's single trail lays it) × `portalSize` (0.4 at
  Mass 0 → 0.9 at 10, read at the replicated level). They are the Meerkat's domain and its pilot's.
- **L5 Danger Portal**: they are danger prisms (a silenced Meerkat's come out plain). A rival touching one takes what the
  platform's danger-prism effects take (the page's `dangerPetals` is lab-only).
- Mass is conserved: a portal is never taken back.

### 9.5 The network

- The owner decides each soliton's kit when it forms it (`MeerkatSolitonKit.Stamp`: a sentinel or not, the Charge / Mass /
  Space L5 bits, the portal's size) and publishes it with the tunnel: `MeerkatWormholeNetworkRelay.TunnelState.Kit` and
  `PortalSize` (**protocol version 23**). The throat and mouth were already published.
- Every peer then lays the same portal, stands the same sentinels (and holds the same solitons) and steals the same
  swallowed prisms from the same tunnels, on its own prisms: prisms are per-peer, as every mover of the prism-motion fundamental is
  (`Docs/PRISM_MOTION.md` §4), so two peers can disagree on a collision at the edge of a contact.
- A zap's petals are decided on the Meerkat's owner and settled on the victim's owner (`ElementalTransfer`'s relay); a
  peer's own copy of a sentinel only plays the zap's sound and shields its own prisms.
- The ride (anyone's) and Time's two numbers run on the rider's simulating machine; the hull's pose replicates as every
  vessel's does.

### 9.6 Sound

Five empty `EventReference` slots on `MeerkatWormholeConfig` (the FMOD rule: shipped EMPTY until sound design wires
them): `zapEvent` (a zap on a rival, about five a second at most), `sentinelPlacedEvent`, `sentinelLostEvent` (lost or
retired), `portalEvent` (at the way out) and `prismFiredEvent` (one per tube fired at once). The artifact's synthesized
sounds are their brief.

### 9.7 Testing

- `Tests/Editor/MeerkatSolitonMathTests.cs` (6 tests): the ride's rush and Boost Out, the way in, the sentinel pool retiring the oldest, the zap's petals per Charge and its clock, the hexagon (three
  page goldens, the edges closing). The surface area's goldens are §8's (`MeerkatWormholeMathTests`). `Tools/Build/meerkat_kit_harness/run.sh --self-test` runs them under
  dotnet and proves three wrong ports fail.
- Hand tests: `Docs/UNITY_VERIFICATION_CHECKLIST.md`, "Meerkat: Garrett's kit".

### 9.8 Follow-ups

- The sentinel seats and the hop (dynamic crew seats), and the element-holder rule that rides on them.
- A non-Meerkat hull riding a soliton (a ride hook in `VesselTransformer`).
- The Meerkat's own HUD view and four icons (`/ability-icon`); a ready colour on the Charge and Space cards.
- A zap's look in the world (today its cues are the victim's ejected crystals and the empty `zapEvent`).
- The AI does not yet seek a soliton to ride (the page's `kitAiSoliton`); it only rides one it flies into.
