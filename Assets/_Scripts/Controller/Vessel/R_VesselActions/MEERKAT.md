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
| Right stick | Bends the reticle any way, as far as it is pushed, up to `bendMaxDegrees`. The further it bends, the shorter (`lengthStraight` to `lengthBent`) and fatter (`throatStraight` to `throatBent`) it gets |
| LT (`LeftStickAction`) | Shortens a straighter reticle. A pad's analog trigger is read live; a key or touch ramps to full in `keyRampSeconds` |
| RT (`RightStickAction`) | Forms the tunnel once the reticle is 60% grown, and dives in. The last tunnel collapses at once |
| X (`Button1Action`) | The stance (`StoatHoldAction`), kept from the Stoat; forming a tunnel ends it |

- **The reticle** is always on the nose, `reticleAhead` ahead. Only its pilot sees it (round 3):
  - It eases toward what the right stick and LT ask for over `reticleSmoothSeconds`, so it glides rather than snaps.
  - It is a see-through frame of thin spindles: `reticleRings` × `reticleSpindlesPerRing` round its throat and
    `reticleRibs` along it, `reticleSpindleThickness` thick, cyan to white, over a faint glass (`reticleGlass`).
  - The lime line you will fly runs through it (`reticlePath`).
- **The ride.** `MeerkatVesselTransformer` lets the tunnel fly the hull (`StepRide`). It dives straight to the mouth,
  then runs the arc mouth to mouth. Its speed starts and ends at the entry speed and rushes to `throatSpeed` × it at the
  throat. It comes out with the entry speed, on the tunnel's last heading.
- **Out.** The reticle comes back after `reticleDelaySeconds`, growing out of its centre over `reticleGrowSeconds`.
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
  `MeerkatWormholeNetworkRelay` (one `NetworkVariable`, protocol version 17). Every peer runs the same life from
  there.

## 3. The AI (input only, `/vessel-ai`)

The platform `AIPilot` flies the line. `MeerkatWormholeExecutor.Autopilot` adds the studio's `aiInput` / `aiGatePlan`,
line for line:
1. It waits until its reticle is back and its ring is more than `aiBentMinDistance` away. Only a bent tunnel is planned
   inside `aiStraightMinDistance`.
2. It tries 21 bends × 9 LT squeezes, and keeps the one whose way out heads within 0.26 rad of the ring with 60 u to
   spare, straighter preferred.
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
| Look | `MeerkatWormholeView.cs`, `MeerkatWormholeLensPass.cs`, `_Graphics/Materials/Graphs/MeerkatWormhole*.shader`, `Resources/MeerkatWormhole*.mat` |
| HUD | `UI/Controller/MeerkatHUDController.cs` |
| Prefab and registrations | `Tools/Build/author_meerkat_assets.py` (`--check`, `--self-test`) |
| Studio map | `Tools/Build/studio_to_unity/meerkat.json` (42 keys, `--check` 0 drift) |
