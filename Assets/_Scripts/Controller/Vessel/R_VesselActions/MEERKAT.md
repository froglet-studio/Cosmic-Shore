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

- **The shape** (round 7, the umbrella handle): a straight shaft of S, then one bend of radius `bendRadius` through the
  stick's angle θ, so the tunnel is S + `bendRadius` × θ long (`MeerkatWormholeMath.ShapeFor` / `At` / `BendAngle`). The
  throat (`throatDiameter`, 8 u) and the entry and exit (`mouthDiameter`, 26 u since round 10) are fixed: set on the
  config at edit time, never by the sticks.
- **The reticle** is always on the nose, `reticleAhead` ahead. Only its pilot sees it.
  - It eases toward what the right stick and LT ask for over `reticleSmoothSeconds`, so it glides rather than snaps.
  - It is the wormhole's own wireframe (round 9, Yash: *"the reticle is the silhouette of the geometry"*): the bridge's
    `bridgeRings` × `bridgeSpindlesPerRing`, `bridgeRibs` and `bridgeSpindleThickness`, in the same places, condensing
    the same way (`MeerkatWormholeView.BridgeFrame`), with no glass and no path.
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
measures round 7's tunnel: a straight shaft, then the bend (`MeerkatWormholeLens.shader` `MWTube`).

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
- **Numbers:** the lab's export, Garrett's pick: strength 18, pitch 90, drag 1, twist 0, glass 0.4 at Δn 0.04,
  grazing 0.6, flow In then out, inner 0.1, outer 0.12, profile Smooth, ribs 0.9, sharpness 8, travel 1.5.
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
- **The element map** (`ElementalAbilityMaps/Meerkat.asset`): four OPEN design slots. Which element the wormhole is,
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
| Studio map | `Tools/Build/studio_to_unity/meerkat.json` (42 keys, `--check` 0 drift) |
