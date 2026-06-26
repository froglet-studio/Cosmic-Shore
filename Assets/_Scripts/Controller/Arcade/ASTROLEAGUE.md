# Astro League Game Mode - Technical Documentation

## Overview

Astro League is hypersea soccer - the spirit of Rocket League translated to Cosmic
Shore, rebuilt on the multiplayer domain-games stack. Two domains (Jade defends -Z,
Ruby defends +Z) fight to slam a glowing billiard-physics payload through the
opposing goal portal inside a wireframe arena suspended in the HyperSea. 1-6 players
(2v2/3v3 with AI backfill) through the same single unified Netcode scene as HexRace /
Joust / Crystal Capture - solo play is just a party of one plus AI backfill.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameAstroLeague.unity` (single
  unified scene - no separate singleplayer variant)
- **GameMode enum**: `GameModes.AstroLeague = 37`
- **Controller**: `AstroLeagueController : MultiplayerDomainGamesController`
- **Scoring**: `AstroLeagueScoringRuleSO` (`metric = ScoringMetric.Goals`, points not
  golf), assigned to `GameDataSO.ScoringRule` in `OnNetworkSpawn`
- **Config**: every gameplay number lives in `AstroLeagueSettingsSO`
  (`Assets/_SO_Assets/Games/AstroLeagueSettings.asset`) - match rules, kickoff
  pacing, billiard physics, replication smoothing, juice, AI tuning, arena palette
- **Domains**: exactly two. `SO_ArcadeGame.MinDomainsAllowed = MaxDomainsAllowed = 2`
  pins the configure modal's DC stepper, so the standard pipeline (DomainAssigner ->
  `ServerPlayerVesselInitializerWithAI` balancing) always produces Jade vs Ruby
- **Vessels**: all six playable ships are selectable (`SO_ArcadeGame.Vessels` =
  Squirrel, Manta, Dolphin, Rhino, Serpent, Sparrow - Squirrel is the default).
  AI picks randomly among them. All six are registered in the Vessel Prefab Container.

## Class Inventory (`_Scripts/Controller/Arcade/AstroLeague/`)

| Class | Role |
|---|---|
| `AstroLeagueController` | Match director (server-authoritative): kickoffs, goal attribution, celebrations, golden-goal overtime, winner banner, AI striker arming, final-score sync (HexRace/Joust/CC `SyncFinalScores_ClientRpc` pattern) |
| `AstroLeagueBall` | Server-simulated billiard payload (`NetworkBehaviour`). Server owns a real non-kinematic rigidbody with full **angular dynamics**; clients dead-reckon from replicated position + velocity + **angular velocity** NetworkVariables (the kinematic replica free-spins so the faceted icosphere's tumble shows everywhere). Vessel hits are a **momentum-conserving elastic bounce off the moving hull** (off-center -> spin) and the ball can never clip a vessel. Carries the **last-striker's domain** (`n_LastHitDomain`) which drives the ball tint and the selective prism interaction (own color -> pass through + shield; opposing unshielded -> slow by mass + destroy; opposing shielded -> unshield + leave). The ball bounces elastically only off the **spherical nucleus boundary and vessels**, never off prisms. Strike velocity comes from server-side per-vessel transform sampling (vessels are transform-driven, so rigidbody velocity and remote `VesselStatus.Speed` are useless). Impact juice replicates via ClientRpc |
| `AstroLeagueMatchMonitor` | `TurnMonitor` match clock, server-authoritative ("M:SS"/"OT" pushed by ClientRpc on the shared display channel). Pauses during celebrations; the controller decides full-time vs overtime; turn ends only on `ForceEnd()` |
| `AstroLeagueGoal` | Accurate goal detector (server-gated): per-tick polls the ball for a genuine INWARD crossing of the goal-line plane WITHIN the mouth circle (no fat-trigger false positives, teleport-guarded); reports to `AstroLeagueController.HandleGoalServer` - attribution lives in the controller |
| `AstroLeagueArena` | Runtime **gameplay-only** HyperSea stadium, built identically on every peer (no networking): a **spherical play boundary that IS the Cell's nucleus** (the arena scales the nucleus to `settings.boundaryRadius`xintensity and the ball bounces elastically off its inner surface via a radial reflect - no collider; this replaced six invisible BoxCollider walls, -6 colliders), portal goal rings with ball-proximity anticipation flare, and a midfield/kickoff center ring. **Owns no environment dressing** - the boundary read is the Cell's `MembranePrefab`, the drifting motes are the Cell's `CytoplasmPrefab`, and the boundary sphere is the Cell's `NucleusPrefab` (a bespoke edge cage + plankton particle system were removed; see `Docs/ECOSYSTEM_MASTERPLAN.md Sec 5.1`) |
| `AstroLeagueSettingsSO` | All tunables |
| `AstroLeagueScoringRuleSO` | Scoring strategy: mercy-rule end condition over per-domain `GoalsScored` sums, Score = personal goals, "WON BY N GOALS" reveal |

## Match Flow (server-authoritative)

```
SetupNewTurn            ball frozen at center, clock configured (server)
Ready (all humans) -> shared 3-2-1 countdown = first kickoff count-in
OnCountdownTimerEnded   ParkVesselsForKickoff_ClientRpc (each peer parks vessels it
                        owns - vessels replicate owner-authoritatively) -> players
                        active, clock runs, ball unfrozen, strikers armed, GO!
GOAL (server plane-cross) attribution: most recent striker NOT on the defending domain
                        (own goals credit the opponent; unattributed -> kickoff, no
                        score) -> scorer.RoundStats.GoalsScored++ (NetworkVariable)
                        -> ball detonates (ClientRpc juice) -> celebration -> kickoff
Mercy / golden goal     rule.IsObjectiveReached (domain goal sum >= GoalTargetCount),
                        or any goal during overtime -> FinishMatch
Clock expires           tied + goldenGoalOvertime -> OVERTIME (sudden death, "OT")
                        else -> FinishMatch(rule.ResolveWinner)
FinishMatch             winner banner (real time) -> matchMonitor.ForceEnd()
                        -> OnTurnEndedCustom (server): AssignScores + Sort +
                        CalculateDomainStats -> SyncFinalScores_ClientRpc
                        -> WinnerName/WinnerDomain/Results on every peer
                        -> InvokeWinnerCalculated + InvokeMiniGameEnd -> shared
                        end-game cinematic + scoreboard
```

## Scoring & Stats

- **Metric**: `ScoringMetric.Goals = 4` reading the new `IRoundStats.GoalsScored`
  (full `RoundStats` NetworkVariable pattern, same as `JoustCollisions`).
- **Domain aggregation**: the base `MultiplayerDomainGamesController` domain-sum
  NetworkVariables replicate per-domain goal sums to every peer - the in-game score is
  shown by the **shared `MultiplayerHUD` domain boxes** (the existing system, reading
  `GameDataSO.GetDomainMetricSum`), so it can never diverge from the host. Astro League adds
  no bespoke score UI.
- **Goal target**: `GameDataSO.GoalTargetCount` (mercy rule), published by the
  controller from `AstroLeagueSettingsSO.goalLimit` and synced by ClientRpc.
- **Final scores**: every player's `Score` = personal `GoalsScored`; the winning
  DOMAIN is the highest goal sum (golden goal guarantees no tie when enabled; with
  overtime disabled, full-time ties break by `ActiveDomains` order).
- **Comeback**: `ElementalComebackSystem` with `ScoreDifferenceSource.Goals` - buffs
  scale with the TEAM goal deficit (Elementals are the buff fundamental; no bespoke
  rubber-banding).

## Networking Model

| Concern | Owner | Mechanism |
|---|---|---|
| Ball physics + strikes | Server | Rigidbody sim (linear + angular); elastic vessel bounce via `OnCollision`/`OnTrigger` Enter+Stay; spherical-boundary containment via per-tick radial reflect (`ContainWithinBoundary`); prisms resolved by a per-tick `QuerySphere` scan (every peer), not collisions |
| Ball position/velocity/spin | Server -> all | `NetworkVariable<Vector3>` x3 (pos, linear vel, angular vel), client dead reckoning + smoothing + free-spin |
| Ball last-hit domain (color/interaction) | Server -> all | `NetworkVariable<Domains> n_LastHitDomain` (Blue = neutral) |
| Ball frozen/hidden | Server -> all | `NetworkVariable<bool>` x2 |
| Strike velocity | Server | Per-vessel transform sampling each FixedUpdate (`gameData.Vessels`) - correct for host, remote and AI vessels alike |
| Goal detection + attribution | Server | Per-tick goal-plane crossing within the mouth (teleport-guarded) + last-striker ring buffer |
| GoalsScored | Server -> all | `RoundStats.n_GoalsScored` NetworkVariable |
| Match phase / clock | Server | Controller fields + monitor; display via ClientRpc |
| Kickoff parking | Owning peer | `ParkVesselsForKickoff_ClientRpc`; deterministic slots (domain members sorted by name) |
| Announcer beats / juice | All peers | ClientRpcs -> shared `AudioSystem` cues (kickoff GO, goal, overtime, game-end) + ball-impact camera shake / haptics |
| Hitstop & celebration slow-mo | Solo sessions ONLY | Local `Time.timeScale` desyncs connected peers (see `MenuCrystalClickHandler` precedent) - gated on `ConnectedClientsIds.Count <= 1` |

## AI Striker

`AIPilot.SetExternalTargetProvider(Func<Vector3>)` (sampled once per frame in
`Update`, overrides crystal/player seeking). The controller arms every AI on the
server with billiard thinking:

- **Attack**: when on the own-goal side of the ball, aim `strikerApproachLead`
  behind the ball along the ball->enemy-goal line, so contact drives it goalward.
- **Recover**: when caught on the wrong side, swing wide around the ball
  (`strikerRecoverDistance`, perpendicular offset) instead of own-goaling.
- **Kickoff hold**: while the ball is frozen, hold at the team's kickoff anchor.

## Ball Physics Notes

> **The feel we're building: Rocket League in the HyperSea.** The ball is a real,
> momentum-carrying payload. It bounces *elastically* off only two things - the spherical
> **nucleus boundary** and the **vessels** - and everything else is about the ball's DOMAIN (the team
> color of whoever struck it last) interacting with the colored mass of the prismscape:
> it glides through friendly trail (shielding it), eats enemy trail (slowing as it plows),
> and pops enemy shields. There is no friction and no scripted strike - speed is gained
> from vessel hits and lost only by plowing enemy mass, so a well-placed shot screams
> across the arena and a defender can wall it off with their own trail. Fauna are spawned for
> the **controlling domain** - the cell-ecosystem food web layered onto the arena so the
> dominant team's mass grows a living (ambient) defense (see "Cell ecosystem" below).
>
> The HUD **objective indicator always points at the ball** (`AstroLeagueObjectiveProvider`,
> auto-wired by `MiniGameHUD` for `GameModes.AstroLeague`), hiding while the ball is hidden
> during a goal celebration / kickoff reset.

- Vessels move via `transform.position +=` (`VesselTransformer`), so neither
  `collision.rigidbody` velocity nor (for remote vessels) `VesselStatus.Speed` is
  trustworthy on the server. `AstroLeagueBall` samples every vessel root's position
  per physics tick and uses the delta as strike velocity (`Course * Speed` is the
  first-tick fallback).
- **Vessel hit = momentum-conserving elastic bounce off a moving paddle.** Vessels are
  transform-driven, so the hull is modeled as an infinite-mass moving paddle: in the
  vessel's frame the *approaching* component of the ball's velocity reflects about the
  contact normal (restitution `ballBounciness`), and adding the vessel velocity back gives
  the ball up to ~2x the vessel's speed on a head-on hit (the kick). A *stationary* vessel
  still reflects the ball's own velocity, so the ball always bounces off a ship - never
  sticks. `hitBoostMultiplier` adds an arcade pop biased from the contact normal toward the
  pilot's heading (`directionalBias`) for aim. The OFF-CENTER contact also injects torque
  (`tau = (contact - COM) x impulse`, applied as the split form of `AddForceAtPosition`:
  direct linear + `AddTorque`) so the ball picks up real **spin**, clamped by
  `maxAngularSpeed` and persisting under low `ballAngularDamping`.
- **Strike detection spans both collider paths, Enter AND Stay.** Vessel contact is
  handled in `OnCollisionEnter`/`OnCollisionStay` (physics hull) AND
  `OnTriggerEnter`/`OnTriggerStay` - Serpent and Sparrow have NO non-trigger collider, so
  without the trigger paths they'd pass straight through. All four route to `VesselContact`.
  The elastic bounce only fires when the ball is moving INTO the vessel (`approach < 0`),
  which makes it **self-limiting** (a ball that already bounced away stops approaching) and
  **self-deduping** (the second collider path the same frame sees the ball separating).
- **The ball can NEVER clip a vessel; re-hits always register.** `VesselContact` runs on
  *every* contact frame and ALWAYS depenetrates first - `EjectBallFromVessel` pushes the
  ball so its center is >= `ball radius + vesselClearRadius` from the vessel root
  (`rb.position` republished to `n_Position`), independent of any cooldown. So even a pilot
  driving straight into the ball, or a trigger-only ship with no physics depenetration,
  cannot pass through it - and because the elastic bounce re-fires every approaching frame,
  every fresh approach is a fresh collision. The *deliberate-strike extras* (the arcade
  pop, the vessel **recoil**, hitstop) are rate-limited per vessel by `vesselStrikeCooldown`
  and gated on `minimumHitSpeed`, so a fast committed hit pops + recoils while continuous
  dribble contact doesn't spam. The recoil (the vessel "bouncing off" too) is the
  controller's `RecoilVessel_ClientRpc` - owner-authoritative vessels move only where
  `IsNetworkOwner`, so it's keyed by vessel `NetworkObjectId`.
- **Intensity scales the whole playfield.** The controller computes a scale factor that
  steps evenly with intensity - **1x / 2x / 3x / 4x** for intensities 1-4 (`lerp(1,
  intensityScaleAtMax=4, (i-1)/(maxIntensityLevel-1))`) - and broadcasts it in
  `SyncMatchConfig_ClientRpc` so every peer applies the same scale. (4x is the playable
  ceiling; the earlier 10x max was too big.) `AstroLeagueArena.Build(scale)` rebuilds the
  stadium at the scaled dimensions, `AstroLeagueBall.SetSizeScale(scale)` resizes the ball
  (visual + collider) on top of its authored base, and the controller pushes the goals +
  team spawns out to the scaled goal lines. Vessels stay normal-size, so a high-intensity
  match is a bigger playfield with a bigger ball. Players reset to the scaled team positions
  on every kickoff (`ComputeKickoffPose` reads the scaled spawns and scales the lateral
  teammate spacing too).
- **Faceted icosphere - rotation you can see.** The mesh is a medium-poly **flat-shaded
  icosphere** generated at runtime (`IcosphereMeshGenerator`, default 2 subdivisions =
  320 tris) and assigned to the MeshFilter in `SetupVisuals` (the SphereCollider is kept,
  so the inertia tensor stays a uniform sphere -> free rotation about any axis). The
  authored Unity Sphere mesh is replaced at load; the icosphere is radius 0.5 to match
  the collider, so the visual hull tracks the physics hull at every intensity scale.
  Flat facets are what make the spin legible: each facet catches the fresnel rim
  differently as the ball tumbles, instead of a uniform glowing ring.
- **3D fresnel look + domain color.** The ball clones `PrismMaterial.mat` (the prism
  `BlockGraph` fresnel shader) at runtime, so it renders solid with a bright
  view-dependent rim exactly like trail prisms, driving the fresnel `_BrightColor` (HDR
  rim, flash-capped) with a dim `_DarkColor` base. **The hue keys to the last striker's
  domain** (`n_LastHitDomain`): a Jade striker tints the ball Jade (`jadeGoalColor`), a
  Ruby striker Ruby (`rubyGoalColor`), matching the arena palette. Before any strike the
  ball is **neutral** (`Domains.Blue`) and runs the original three-way rainbow cycle so it
  reads as "unclaimed"; it resets to neutral on every kickoff. Falls back to
  `Shader.Find("Shader Graphs/BlockGraph")`, then URP/Lit, if the material is unwired.
  Scaled to world radius ~ 7 for a chunky billiard feel and a precise strike target.
- **The ball is a FIRST-CLASS entity: it resolves prisms by a per-tick spatial scan, NOT by
  physics collisions.** This is the crux. Prism colliders are LOD-culled away from vessels,
  and a fast ball tunnels past tiny box colliders - so collision-driven prism interaction
  only fired near vessels and the map cluttered up. Instead, the ball's collider **excludes
  the `TrailBlocks` layer entirely** (`sphereCol.excludeLayers`, set in `Awake`), so the
  solver never bounces or snags it on a prism, and every physics tick - on **every peer** -
  `ProcessPrismInteractions` sweeps `PrismSpatialIndex.QuerySphere` over the segment the ball
  just travelled (radius = `BallWorldRadius x prismScanRadiusFactor`, multi-sampled so a fast
  ball skips nothing) and resolves the prisms it overlaps. The ball clears/shields trail
  consistently along its **whole path**, exactly like a player vessel senses its surroundings:
  - **Same color** (own trail) -> **shield** it (`prism.ActivateShield()`, skipped if already
    shielded). No speed change.
  - **Opposing + unshielded** (or a **neutral** ball, which has no color yet) -> **eat it**:
    destroy via the canonical animated `Prism.Damage`, and **slow the ball by the prism's
    MASS** - `speed x= ballMass / (ballMass + prismDragMassScale * prismVolume)`, direction
    preserved, never reversed. Plowing a thick enemy wall brakes hard; a thin one barely.
    **This is the ONLY thing that slows the ball** (no friction; walls + same-color +
    shielded passes are all lossless; vessel hits re-energize; `maxSpeed` caps the top).
  - **Opposing + shielded** -> **unshield** it (`prism.DeactivateShields()`) and **LEAVE it
    standing this visit** - the shield absorbs the pass. The prism is held in a
    `_shieldPoppedThisVisit` set so it isn't eaten the same overlap; once it leaves scan range
    the protection drops, so a later visit eats the now-unshielded prism.
- **Per-peer, no broadcast.** Prisms are per-peer GameObjects (laid by `VesselPrismController`
  on every peer, not shared NetworkObjects). Each peer runs the scan over its OWN local copies
  around the ball's local (replicated/dead-reckoned) position using the replicated
  `n_LastHitDomain`, so each peer's view clears/shields its own trail consistently - no
  per-tick RPC. Only the **server** applies the eaten-mass speed drag; clients get the slowed
  velocity via `n_Velocity` replication and just mirror the shield/destroy on their copies.
  Mass is conserved (animated explode-out via spatial-index release + VFX, never raw `Destroy`).
- Layers: the ball is on `Default` (0); prisms run on `TrailBlocks` (11). The ball
  **excludes layer 11** so it passes through all prisms, and still collides with vessels
  (Ships, layer 8) for its elastic strike bounces. The **arena boundary is no longer a
  collider** - containment is a server-side radial reflect off the nucleus sphere
  (`ContainWithinBoundary`), so there are no wall colliders on any layer. (Because prism
  interaction no longer needs colliders, the ball is NOT a `PrismColliderLodManager` focus -
  no extra collider-budget cost.)
- Replay is a full scene reload (the standard domain-games replay path), which clears
  accumulated trail mass with the scene - not a decay mechanism.

## Replay

`UseSceneReloadForReplay = true` - Play Again performs a full network scene reload
(HexRace/CC pattern). All match state, ball, arena, and accumulated trail mass are
destroyed with the scene and re-initialized fresh via `OnNetworkSpawn`.

## Shared-Code Touchpoints (added for this mode)

| Change | File |
|---|---|
| `AstroLeague = 37` | `_Scripts/Data/Enums/GameModes.cs` |
| `Goals = 4` metric | `_Scripts/Data/Enums/ScoringMetric.cs` + `Scoring/ScoringMetrics.cs` |
| `GoalsScored` stat (+ event, Cleanup, NetworkVariable) | `_Scripts/Data/Enums/IRoundStats.cs`, `RoundStats.cs` |
| `GameDataSO.GoalTargetCount` | `_Scripts/Utility/DataContainers/GameDataSO.cs` |
| `AIPilot.SetExternalTargetProvider` | `_Scripts/Controller/AI/AIPilot.cs` |
| `IcosphereMeshGenerator` (runtime faceted icosphere for the ball mesh) | `_Scripts/Utility/IcosphereMeshGenerator.cs` |
| `CustomCameraController.Shake` | `_Scripts/Controller/Camera/CustomCameraController.cs` |
| `SO_ArcadeGame.Min/MaxDomainsAllowed` (+ modal DC bounds) | `_Scripts/ScriptableObjects/SO_ArcadeGame.cs`, `_Scripts/UI/Modals/ArcadeGameConfigureModal.cs` |
| `ScoreDifferenceSource.Goals` | `_Scripts/Controller/Arcade/ElementalComebackSystem.cs` |

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameAstroLeague.asset` (registered in `GameLists/OrganicRematchGames.asset`) |
| Settings | `_SO_Assets/Games/AstroLeagueSettings.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/AstroLeagueScoringRule.asset` |
| Comeback profile | `_SO_Assets/ComebackProfiles/AstroLeagueComebackProfile.asset` |
| End-game cinematic | `_SO_Assets/Cinematics/MinigameAstroLeagueCinematicDefinition.asset` (registered in `SceneCinematicLibrary.asset`) |
| Cell config (biome) | `_SO_Assets/Cell Configs/Astro League Cell/Astro League Cell Config.asset` |
| Spawn profile (food web) | `_SO_Assets/Cell Configs/Astro League Cell/Astro League Spawn Profile.asset` |

## Cell Ecosystem (the environment IS the Cell)

Astro League runs the **standard cell ecosystem** - zero bespoke ecology code, and (post-audit)
zero bespoke *environment* code. A `Cell` GameObject sits at the arena center (origin), parented
under the scene's **`Environment`** container, and self-initializes on `OnInitializeGame` like
every other biome. Vessel-trail prisms inside the cell's sense radius auto-bind to it via
`PrismSpatialIndex`, so the cell's per-domain volume/count climbs as trails accumulate and falls
as the ball + fauna eat them - no controller wiring.

**Spawner bootstrap - the cell needs a crystal (this was the "no fauna" bug).** `Cell.cs` only
calls `StartSpawnerForMode()` from `InitilizePostFirstCellItem()`, which runs on the **first
`runtime.OnCellItemsUpdated` raise** - and that is raised **only when a crystal registers**
(`CellRuntimeDataSO.AddCrystalToList` <- a crystal manager). A cell with no crystal never starts
its spawner, so no amount of phase-threshold tuning produces fauna. Astro League therefore wires a
**`NetworkCrystalManager`** onto the `Game` controller GameObject (sharing its existing
`NetworkObject`), configured for a **single** neutral anchor crystal: `cellData` = the shared
`Runtime Cell Data` SO (same asset the Cell uses), `crystalPrefab` = `Crystal.prefab`,
`crystalCountMode = FixedCount`, `fixedCrystalCount = 1`, `spawnCrystalWithPlayerDomain = false`,
`spawnOnClientReady = true`. On client-ready the server writes one crystal slot to its `n_Slots`
`NetworkList`; the slot replicates and **every peer instantiates its own local crystal** and calls
`AddCrystalToList` locally - bootstrapping each peer's client-local cell. The crystal doubles as a
midfield elemental power-up; it is **not** a scoring/turn objective (Astro League ends on the
time-based `AstroLeagueMatchMonitor` + goal scoring, never on crystals). *(Joust has the identical
latent "cell but no crystal manager -> spawner never starts" gap - fix it the same way when its
ecology is wired.)*

**The Cell owns the environment, not the arena** (`Docs/ECOSYSTEM_MASTERPLAN.md Sec 5.1`,
`CLAUDE.md > "The Cell owns the environment"`). The arena builds only gameplay-bearing structure
(the spherical boundary's ball physics, goal portals, midfield ring). Everything
atmospheric/territorial - including the boundary sphere itself - lives on the `CellConfigDataSO`:

| Need | Cell field | (removed bespoke duplicate) |
|---|---|---|
| Playfield boundary read | `MembranePrefab` | ~~`AstroLeagueArena.BuildEdgeCage` + `settings.edgeColor`~~ |
| Drifting hypersea motes | `CytoplasmPrefab` (`SnowChanger`) | ~~`AstroLeagueArena.BuildPlankton` + `settings.planktonColor/planktonCount`~~ |
| **Spherical play boundary** (ball bounces off it) | `NucleusPrefab`, scaled to `settings.boundaryRadius` by `AstroLeagueArena` via `Cell.SetNucleusWorldRadius` | ~~6 `BoxCollider` arena walls~~ |

- **Biome = `Astro League Cell Config`** (cloned from the Skim Race trail-grazing biome -
  the no-flora "fauna eat AI trail obstacles" template):
  - `SupportedFloras = []` - **no flora**. Mass is purely vessel trails; nothing is planted.
  - **Food web = `Astro League Spawn Profile`** -> two herbivore foragers
    (`Astro League Tadpole`/`Brittlestar` fauna configs; both `FaunaDiet.Herbivore`, no apex
    predator - predators thin foragers, counterproductive to trail cleanup). These are
    Astro-League-specific **low-population** copies of the Skim Race foragers - Tadpole
    `PopulationSize 4`/`MaxLivePopulation 8`, Brittlestar `2`/`4` (~ **12 live fauna** cap, 1/5 of the
    Skim Race 40+16=56 swarm) - so the arena reads as an ambient ecosystem, not a cloud. They are
    separate assets because the originals are still live in HexRace (via the Skim Race cell) and
    must keep their large populations. Churn is also gentled on the profile:
    `BaseFaunaSpawnTime 30`, `FaunaSpawnIntervalSeconds 2`, `InitialFaunaSpawnWaitTime 8`. Diet is opposing
    prism mass; the **phase/aggression ladder** decides reach: at **Restless/L1** they hunt the
    nearest opposing-color trail; at **Frenzy/L2** they converge on the densest **ANY-domain**
    region and graze even the controlling color - the requested "frenzy eats same-domain mass."
  - `SenseRadiusOverride = 1000` - a fixed sphere that covers the arena at every intensity
    (the 4x arena's farthest corner is ~ 748 from center; 1000 has margin). Decoupled from
    the visual membrane, exactly like Skim Race's 3000 over the HexRace track.
  - **Phase thresholds - authored in VOLUME, tuned for Squirrel's low-volume prisms.** A mature
    Squirrel trail prism is only **~ 3.1 volume** (~1/5 the nominal-leaf 16), so the legacy
    countx16 derivation (`RestlessEnter 500`->8000 vol, `FrenzyEnter 1500`->24000 vol) set the
    ladder ~5-8x too high: the gauge barely moved and fauna never left Calm. The config now sets
    **explicit volume** fields: `RestlessEnterVolume 400` / `Exit 300` (~130 prisms of total cell
    mass -> fauna start hunting opposing trail **early**), `FrenzyEnterVolume 1500` / `Exit 1200`
    (~485 prisms -> Frenzy graze-everything cleanup). The **count** fields (`Restless 500`,
    `Frenzy 1500`) remain only as the perf backstop (a runaway prism *count* forces Frenzy even at
    low volume). `SpawnProfile.FaunaFoodFloor = 5` (nominal prisms -> 80 prey-volume ~ 26 opposing
    Squirrel prisms) so herbivores actually seed against the thinner prey. The
    `DomainVolumeIndicator` hex gauge reads the same `FrenzyEnterVolume`, so the ladder and the
    gauge are tuned together.
- **Controlling color is emergent** - the cell's `DominantDomain` is whichever domain holds
  the most trail mass; fauna spawn in that color and hunt the opposition (no domain
  asymmetry, no manual assignment).
- **Collider budget:** the cell adds **no new prism colliders** - fauna senses ride
  `PrismSpatialIndex.QuerySphere` (not `Physics.OverlapSphere`), and prism colliders are
  already proximity-LOD'd by vessel foci. The only added cost is the forager bodies
  (bounded by each species' `MaxLivePopulation` perf cap) and the ~0.25s volume recompute.
  Removing the plankton `ParticleSystem` and the 12-segment edge-cage LineRenderers
  *reduces* the per-peer draw/particle cost. The ball excludes the `TrailBlocks` layer, so it
  never collides with the prisms the fauna graze - the two systems are orthogonal (ball eats via
  `Prism.Damage`, fauna via `Consume`, both idempotent on the shared `prism.destroyed` flag).
- **Mass is conserved / continuity:** fauna wither-to-crystal on death (sealed in
  `Fauna.Die`); the only prism sinks are the ball (an active force) and fauna consumption.
  No decay, no lifespan.
- **Networking:** fauna + trail prisms are client-local (no `NetworkObject`) - each peer
  runs its own cell/spawner over its own trail copies, matching the Skim Race/HexRace model.
  Phase can diverge slightly across clients (a known ecosystem caveat); fine for this
  per-client environmental layer.
