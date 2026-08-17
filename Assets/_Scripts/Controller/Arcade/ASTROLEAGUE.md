# Astro League Game Mode — Technical Documentation

## Overview

Astro League is hypersea soccer **played with a sword** — the spirit of Rocket League
translated to Cosmic Shore, rebuilt on the multiplayer domain-games stack. Two domains
(Jade defends -Z, Ruby defends +Z) fight to slam a glowing billiard-physics payload
through the opposing goal portal inside a wireframe arena suspended in the HyperSea.
1-6 players (2v2/3v3 with AI backfill) through the same single unified Netcode scene as
HexRace / Joust / Crystal Capture — solo play is just a party of one plus AI backfill.

**It is Rhino-only.** The Rhino's ForceFieldSkimmer capsule is a real, analog-trigger-
puppeteered sword (`R_VesselActions/RHINO_SHIELD_SWIPE.md`), and the ball resolves a
contact through it ON THE BLADE: the bounce normal comes off the point of the sword that
touched, and the strike speed is that point's TRUE velocity from `SkimmerSwingKinematics`
— so a swung tip, tens of units out on the lever arm and travelling many times faster than
the hull, sends the payload screaming, while a parked sword just deflects it. Tip strikes
carry an extra arcade pop on top. Ramming with the hull still works and is the fallback for
any vessel without a swinging skimmer.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameAstroLeague.unity` (single
  unified scene — no separate singleplayer variant)
- **GameMode enum**: `GameModes.AstroLeague = 37`
- **Controller**: `AstroLeagueController : MultiplayerDomainGamesController`
- **Scoring**: `AstroLeagueScoringRuleSO` (`metric = ScoringMetric.Goals`, points not
  golf), assigned to `GameDataSO.ScoringRule` in `OnNetworkSpawn`
- **Config**: every gameplay number lives in `AstroLeagueSettingsSO`
  (`Assets/_SO_Assets/Games/AstroLeagueSettings.asset`) — match rules, kickoff
  pacing, billiard physics, replication smoothing, juice, AI tuning, arena palette
- **Domains**: exactly two. `SO_ArcadeGame.MinDomainsAllowed = MaxDomainsAllowed = 2`
  pins the configure modal's DC stepper, so the standard pipeline (DomainAssigner →
  `ServerPlayerVesselInitializerWithAI` balancing) always produces Jade vs Ruby
- **Vessels**: **Rhino only** (`SO_ArcadeGame.Vessels` = [Rhino]). Restricting the arcade
  card's list is the whole mechanism — `GameDataSO.ClampVesselToGame` is applied on BOTH
  the human path (`ResolveSpawnVesselType`) and the AI path
  (`ServerPlayerVesselInitializerWithAI`), so AI can never field an illegal hull. Same
  pattern as Ribcage. Do not add a mode-local vessel check.

## Class Inventory (`_Scripts/Controller/Arcade/AstroLeague/`)

| Class | Role |
|---|---|
| `AstroLeagueController` | Match director (server-authoritative): kickoffs, goal attribution, celebrations, golden-goal overtime, winner banner, AI striker arming, final-score sync (HexRace/Joust/CC `SyncFinalResults_ClientRpc (shared MultiplayerDomainGamesController tail)` pattern) |
| `AstroLeagueBall` | Server-simulated billiard payload (`NetworkBehaviour`). Server owns a real non-kinematic rigidbody with full **angular dynamics**; clients dead-reckon from replicated position + velocity + **angular velocity** NetworkVariables (the kinematic replica free-spins so the faceted icosphere's tumble shows everywhere). Vessel hits are a **momentum-conserving elastic bounce off the moving hull** (off-center → spin) and the ball can never clip a vessel. Carries the **last-striker's domain** (`n_LastHitDomain`) which drives the ball tint and the selective prism interaction (own color → pass through + shield; opposing unshielded → slow by mass + destroy; opposing shielded → unshield + leave). The ball bounces elastically only off the **court boundary (`AstroLeagueBoundary`) and vessels**, never off prisms. Strike velocity comes from server-side per-vessel transform sampling (vessels are transform-driven, so rigidbody velocity and remote `VesselStatus.Speed` are useless) — EXCEPT through a swinging skimmer, where the contact resolves on the BLADE and takes that point's true velocity from `SkimmerSwingKinematics` (the Rhino's sword). Impact juice replicates via ClientRpc, including the **vessel-strike beat** (`Strike_ClientRpc`: flash + visual-child pop + burst + striker-emphasised shake + audio) that the mode previously had no equivalent of at all |
| `AstroLeagueMatchMonitor` | `TurnMonitor` match clock, server-authoritative ("M:SS"/"OT" pushed by ClientRpc on the shared display channel). Pauses during celebrations; the controller decides full-time vs overtime; turn ends only on `ForceEnd()` |
| `AstroLeagueGoal` | Accurate goal detector (server-gated): per-tick polls the ball for a genuine INWARD crossing of the goal-line plane WITHIN the mouth circle (no fat-trigger false positives, teleport-guarded); reports to `AstroLeagueController.HandleGoalServer` — attribution lives in the controller |
| `AstroLeagueArena` | Runtime **gameplay-only** HyperSea stadium, built identically on every peer (no networking): a **court play boundary that IS the Cell's nucleus** (the arena builds an `AstroLeagueBoundary` at `settings`-driven scaled dimensions and the ball reflects off its walls — no collider; this replaced six invisible BoxCollider walls, −6 colliders), portal goal rings with ball-proximity anticipation flare, and a midfield/kickoff center ring. The boundary **shape is pluggable per intensity** (see "Court Geometry" below) — flat polytope faces BANK the ball; the legacy sphere focuses it. **Owns no environment dressing** — the boundary read is the Cell's `MembranePrefab`, the drifting motes are the Cell's `CytoplasmPrefab`, and the boundary/core is the Cell's `NucleusPrefab` morphed to the court shape (a bespoke edge cage + plankton particle system were removed; see `Docs/ECOSYSTEM_MASTERPLAN.md §5.1`) |
| `AstroLeagueBoundary` | The court geometry the ball bounces off (plain C# object, built per intensity on every peer). Every ricochet polytope (box, octagonal/hexagonal prism, beveled box, octahedron) is a **convex polytope = a list of inward face-planes**, so one generic `Contain` reflects the ball off each violated plane — flat faces preserve the wall-PARALLEL velocity (the bank), only the perpendicular flips, exactly like billiards/air-hockey/Rocket-League boards. Sphere + Cylinder keep an analytic curved branch (Sphere = the legacy center-focusing baseline). **NotchedRing** layers a central torus OBSTACLE (the ball bounces off its OUTSIDE, with an angular notch) inside a chosen outer court — a center choke point. The same geometry drives `BuildVisualMesh()` (outer hull + notched torus, double-sided), so the nucleus **cage silhouette IS the wall the ball hits** |
| `AstroLeagueGoalReplay` | Per-peer goal replay: records the (replicated) ball flight into a ring buffer every FixedUpdate; on a goal plays a visual-only GHOST ball retracing the shot on the shared END camera (the "replay camera" — `CameraManager.BeginManualReplayCamera`) while the real arena resets behind it. BROADCAST framing: the camera holds a fixed vantage beside the whole flight (elevated, pulled back to fit the FOV) and PANS to the action — it never chases the ball. Ghost blooms in / shrinks out (continuity law); recording cleared at every kickoff GO. Added at runtime by the controller — no scene wiring |
| `AstroLeagueSettingsSO` | All tunables |
| `AstroLeagueScoringRuleSO` | Scoring strategy: mercy-rule end condition over per-domain `GoalsScored` sums, Score = personal goals, "WON BY N GOALS" reveal |

## Match Flow (server-authoritative)

```
SetupNewTurn            ball frozen at center, clock configured (server)
Ready (all humans) → shared 3-2-1 countdown = first kickoff count-in
OnCountdownTimerEnded   ParkVesselsForKickoff_ClientRpc (each peer parks vessels it
                        owns — vessels replicate owner-authoritatively) → players
                        active, clock runs, ball unfrozen, strikers armed, GO!
GOAL (server plane-cross) attribution: most recent striker NOT on the defending domain
                        (own goals credit the opponent; unattributed → kickoff, no
                        score) → scorer.RoundStats.GoalsScored++ (NetworkVariable)
                        → ball detonates (ClientRpc juice) → celebration → kickoff
                        Celebrate_ClientRpc additionally runs the ON-GOAL ARENA RESET on
                        every peer: vessels re-park on their kickoff lines with speed
                        ZEROED, the accumulated field prisms sweep clean (staggered
                        center-out animated Damage — skips the super-shielded edge
                        lining + fauna bodies), and the GOAL REPLAY plays on the replay
                        camera through the celebration + kickoff-freeze window
                        (AstroLeagueGoalReplay; gameplay camera restored at kickoff GO)
Mercy / golden goal     rule.IsObjectiveReached (domain goal sum ≥ GoalTargetCount),
                        or any goal during overtime → FinishMatch
Clock expires           tied + goldenGoalOvertime → OVERTIME (sudden death, "OT")
                        else → FinishMatch(rule.ResolveWinner)
FinishMatch             winner banner (real time) → matchMonitor.ForceEnd()
                        → OnTurnEndedCustom (server): AssignScores + Sort +
                        CalculateDomainStats → SyncFinalResults_ClientRpc (shared MultiplayerDomainGamesController tail)
                        → WinnerName/WinnerDomain/Results on every peer
                        → InvokeWinnerCalculated + InvokeMiniGameEnd → shared
                        end-game cinematic + scoreboard
```

## Scoring & Stats

- **Metric**: `ScoringMetric.Goals = 4` reading the new `IRoundStats.GoalsScored`
  (full `RoundStats` NetworkVariable pattern, same as `JoustCollisions`).
- **Domain aggregation**: the base `MultiplayerDomainGamesController` domain-sum
  NetworkVariables replicate per-domain goal sums to every peer — the in-game score is
  shown by the **shared `MultiplayerHUD` domain boxes** (the existing system, reading
  `GameDataSO.GetDomainMetricSum`), so it can never diverge from the host. Astro League adds
  no bespoke score UI.
- **Goal target**: `GameDataSO.GoalTargetCount` (mercy rule), published by the
  controller from `AstroLeagueSettingsSO.goalLimit` and synced by ClientRpc.
- **Final scores**: every player's `Score` = personal `GoalsScored`; the winning
  DOMAIN is the highest goal sum (golden goal guarantees no tie when enabled; with
  overtime disabled, full-time ties break by `ActiveDomains` order).
- **Comeback**: `ElementalComebackSystem` with `ScoreDifferenceSource.Goals` — buffs
  scale with the TEAM goal deficit (Elementals are the buff fundamental; no bespoke
  rubber-banding).

## Networking Model

| Concern | Owner | Mechanism |
|---|---|---|
| Ball physics + strikes | Server | Rigidbody sim (linear + angular); elastic vessel bounce via `OnCollision`/`OnTrigger` Enter+Stay; court-boundary containment via per-tick wall reflect (`ContainWithinBoundary` → `AstroLeagueBoundary.Contain`); prisms resolved by a per-tick `QuerySphere` scan (every peer), not collisions |
| Ball position/velocity/spin | Server → all | `NetworkVariable<Vector3>` ×3 (pos, linear vel, angular vel), client dead reckoning + smoothing + free-spin |
| Ball last-hit domain (color/interaction) | Server → all | `NetworkVariable<Domains> n_LastHitDomain` (Blue = neutral) |
| Ball frozen/hidden | Server → all | `NetworkVariable<bool>` ×2 |
| Strike velocity | Server | Per-vessel transform sampling each FixedUpdate (`gameData.Vessels`) — correct for host, remote and AI vessels alike |
| Goal detection + attribution | Server | Per-tick goal-plane crossing within the mouth (teleport-guarded) + last-striker ring buffer |
| GoalsScored | Server → all | `RoundStats.n_GoalsScored` NetworkVariable |
| Match phase / clock | Server | Controller fields + monitor; display via ClientRpc |
| Kickoff parking | Owning peer | `ParkVesselsForKickoff_ClientRpc`; deterministic slots (domain members sorted by name) |
| Announcer beats / juice | All peers | ClientRpcs → shared `AudioSystem` cues (kickoff GO, goal, overtime, game-end) + ball-impact camera shake / haptics |
| Hitstop & celebration slow-mo | Solo sessions ONLY | Local `Time.timeScale` desyncs connected peers (see `MenuCrystalClickHandler` precedent) — gated on `ConnectedClientsIds.Count <= 1` |

## AI Striker

`AIPilot.SetExternalTargetProvider(Func<Vector3>)` (sampled once per frame in
`Update`, overrides crystal/player seeking) is the ONLY lever — the AI has no throttle or
ability API — so "smarter AI" here means a better target point. The controller arms every AI
on the server with billiard thinking:

- **Intercept, not chase** (`strikerClosingSpeedEstimate`, `strikerMaxLeadSeconds`): the AI
  extrapolates the ball over its own travel time (`distance / closing speed`, capped) and plays
  the PREDICTED position. Without this it ran at where the ball used to be and was permanently a
  step behind a moving payload. The cap matters: an uncapped lead extrapolates a distant ball
  straight through the wall it is about to bounce off, and the AI sprints at empty space.
- **Role split** (`strikerDefenderFraction`, `defenderStandoffFraction`, `defenderClearDistance`):
  each domain's AI are sorted by distance to the ball every frame; the nearest attack, the farthest
  drop back and hold `Lerp(own goal mouth, ball, standoff)`. A domain with one AI always attacks.
  A defender abandons the post and clears once the ball is inside `defenderClearDistance` of its
  own goal. This is the biggest single fix — previously every AI chased the ball, so a domain was
  never home and anything past the pack was a goal.
- **Own-goal refusal** (`strikerOwnGoalGuardDegrees`): the ball leaves roughly along the direction
  the AI drives through it, so an approach heading within the guard angle of its OWN net is refused
  outright and the AI peels off to re-approach. The old striker only tested which SIDE of the ball
  it was on, which let it shepherd the payload into its own goal from a wide angle.
- **Attack**: aim `strikerApproachLead` (scaled with the arena) behind the predicted ball along the
  ball→enemy-goal line, so contact drives it goalward.
- **Recover**: caught on the wrong side, or refused for an own goal, swing wide around the ball
  (`strikerRecoverDistance`, perpendicular offset) and come back onto the shot line.
- **Kickoff hold**: while the ball is frozen, hold at the team's kickoff anchor.

Roles and the own-goal guard are **end-goal concepts** and are skipped in the central shared-goal
layout (intensity 4), where both detectors sit at the arena centre: there is no "our end" to fall
back to, and own-goaling is a matter of pass DIRECTION, already handled by aiming past centre.

## Ball Physics Notes

> **The feel we're building: Rocket League in the HyperSea.** The ball is a real,
> momentum-carrying payload. It bounces *elastically* off only two things — the **nucleus court
> boundary** (`AstroLeagueBoundary` — flat-walled by default so it BANKS like billiards; Sphere is
> the legacy center-focusing baseline) and the **vessels** — and everything else is about the ball's DOMAIN (the team
> color of whoever struck it last) interacting with the colored mass of the prismscape:
> it glides through friendly trail (shielding it), eats enemy trail (slowing as it plows),
> and pops enemy shields. There is no scripted strike — speed is gained from vessel hits, so a
> well-placed shot screams across the arena and a defender can wall it off with their own trail.
> The ball **settles**: it bleeds speed on a slow exponential drag (`ballDrag`), loses energy on
> every wall carom (`wallRestitution` < 1) and snaps to rest below `ballRestSpeed`, on top of the
> mass drag from plowing enemy trail. It was originally frictionless with perfectly elastic walls,
> and in a large court that read as **pong** — the ball ricocheted forever and nobody could settle
> a possession. A resting payload is something players contest; a permanently moving one is
> something they dodge. Vessel strikes stay fully elastic, so the sword still fires it. Fauna are spawned for
> the **controlling domain** — the cell-ecosystem food web layered onto the arena so the
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
  the ball up to ~2× the vessel's speed on a head-on hit (the kick). A *stationary* vessel
  still reflects the ball's own velocity, so the ball always bounces off a ship — never
  sticks. `hitBoostMultiplier` adds an arcade pop biased from the contact normal toward the
  pilot's heading (`directionalBias`) for aim. The OFF-CENTER contact also injects torque
  (`τ = (contact − COM) × impulse`, applied as the split form of `AddForceAtPosition`:
  direct linear + `AddTorque`) so the ball picks up real **spin**, clamped by
  `maxAngularSpeed` and persisting under low `ballAngularDamping`.
- **A blade contact is resolved ON THE BLADE (the Rhino's sword).** A swinging skimmer is a
  rigid SEGMENT, so neither "the vessel's position" nor "the vessel's speed" describes the hit.
  When the contact collider resolves to a `Skimmer` with a ready `SkimmerSwingKinematics`
  (`settings.bladeAwareStrikes`), the ball takes `ClosestBladePoint(ballCenter)` as the contact
  and `VelocityAt(contact)` as the striker velocity — the same model
  `PrismEffectHelper.ContactVelocity` feeds prism impacts. Everything downstream (bounce normal,
  the arcade pop's aim, the recoil, the feedback intensity) then describes the SWING rather than
  the hull. `bladeTipStrikeBonus` lerps an extra pop from hilt (1×) to tip.
  **Two traps this had to solve:**
  1. *Reach.* The Rhino's skimmer carries TWO volumes — the sword's thin non-trigger
     `CapsuleCollider` (~2.4 × 59 world units) and the inherited SPHERE trigger that is its skim
     field (radius = half the blade length, 15-60 units). Both reach the contact handler. A
     contact further than `ball radius + bladeClearRadius` from the blade CENTRELINE is the skim
     field, not the sword, and is dropped — otherwise the Rhino bats the payload from meters away
     with an invisible aura.
  2. *Anti-clip.* `EjectBallFromVessel` measures from the vessel ROOT, which cannot protect a
     30-120 unit sword: at a tip strike the ball is already well outside the hull's clear radius,
     so the check no-ops while the blade sweeps through it. A blade hit depenetrates off the blade
     SEGMENT instead (`EjectBallFromPoint`, `bladeClearRadius`).
  Vessels with no swinging skimmer are untouched — the model reports not-ready and the hull path
  runs exactly as before.
- **Every strike broadcasts feedback (`Strike_ClientRpc`).** This did not exist: a vessel
  connecting with the ball produced **nothing at all** — no flash, no burst, no shake, no sound —
  and the only evidence you had hit the payload was that it changed direction. That is the single
  largest reason the mode read as unresponsive. Four layers, each covering a different distance:
  emission **flash** + a volume-safe **pop** (a scale pulse on the ball's VISUAL CHILD, never the
  root — the root's lossyScale is the ball's physical size, read by the collider, the goal-line
  threshold, the prism scan radius and the depenetration clearance); a particle **burst** at the
  contact; distance-scaled camera **shake**, multiplied by `strikerShakeEmphasis` for the pilot
  who actually connected (resolved by NetworkObjectId against `GameDataSO.LocalPlayer`, not by
  ownership — AI vessels are server-owned, so an ownership test would hand the host every AI's
  emphasis); and an **audio cue**, heavier above `bigHitSpeedFraction` or on a tip strike.
  **No haptic**: `Docs/HAPTICS.md` ships two everyday feels (skim reward / prism punish) plus one
  rare alert, and a ball strike is none of them — the mode's legacy `HapticController.PlayHaptic`
  calls are already no-ops by that policy.
- **Strike detection spans both collider paths, Enter AND Stay.** Vessel contact is
  handled in `OnCollisionEnter`/`OnCollisionStay` (physics hull) AND
  `OnTriggerEnter`/`OnTriggerStay` — Serpent and Sparrow have NO non-trigger collider, so
  without the trigger paths they'd pass straight through. All four route to `VesselContact`.
  The elastic bounce only fires when the ball is moving INTO the vessel (`approach < 0`),
  which makes it **self-limiting** (a ball that already bounced away stops approaching) and
  **self-deduping** (the second collider path the same frame sees the ball separating).
- **The ball can NEVER clip a vessel; re-hits always register.** `VesselContact` runs on
  *every* contact frame and ALWAYS depenetrates first — `EjectBallFromVessel` pushes the
  ball so its center is ≥ `ball radius + vesselClearRadius` from the vessel root
  (`rb.position` republished to `n_Position`), independent of any cooldown. So even a pilot
  driving straight into the ball, or a trigger-only ship with no physics depenetration,
  cannot pass through it — and because the elastic bounce re-fires every approaching frame,
  every fresh approach is a fresh collision. The *deliberate-strike extras* (the arcade
  pop, the vessel **recoil**, hitstop) are rate-limited per vessel by `vesselStrikeCooldown`
  and gated on `minimumHitSpeed`, so a fast committed hit pops + recoils while continuous
  dribble contact doesn't spam. The recoil (the vessel "bouncing off" too) is the
  controller's `RecoilVessel_ClientRpc` — owner-authoritative vessels move only where
  `IsNetworkOwner`, so it's keyed by vessel `NetworkObjectId`. **`vesselRecoilSpeed` DEFAULTS
  TO 0 (OFF)**: anti-clip is already guaranteed by the ball's own depenetration, so any recoil
  only fights player control — a frictionless ball that keeps bouncing back into a vessel
  re-fires it every cooldown, stacking toward `VesselTransformer.velocityModifierMax` (100) and
  throwing the vessel back "like crazy" (the reported runaway-throwback feel). Dial it up only
  for a deliberate subtle bounce; `AstroLeagueController.ApplyVesselRecoil` early-outs entirely
  when it's ≤ 0.
- **The whole playfield is authored on ONE asset.** Base court dimensions
  (`settings.arenaLength/Width/Height`, default **400 × 320 × 240**), the goal mouth
  (`settings.goalMouthRadius`, default **62**) and the kickoff line
  (`settings.kickoffLineDistance`) live on `AstroLeagueSettingsSO`, not on the scene. The
  controller DERIVES the goal transforms (±length/2 on the goal axis) and the team-spawn
  transforms from them, and hands the same mouth radius to every `AstroLeagueGoal` — so
  resizing the pitch is a one-number edit with no scene drag, and the portal ring you aim at
  can no longer disagree with the mouth that scores (they used to be two independently
  authored fields). `AstroLeagueArena`'s serialized dimensions and `AstroLeagueGoal.mouthRadius`
  survive only as fallbacks for a scene with no settings asset wired. The court is also much
  BIGGER than it shipped: height went 100 → 240 (it was a pizza box in a 3D flight game) and
  the scale table starts at 1.8×, so an intensity-1 court is 720 × 576 × 432 against the
  shipped 600 × 400 × 200. **Sizing history — do not re-derive it:** the original court was far
  too small to fly in, a first pass took the table to 3-4.5× and overshot (a big box plus a
  frictionless ball is pong), and the table is that pass cut by **40%**. `shakeFalloffRadius`
  (180 → 450) and `maxSpeed` (220 → 300) moved with it; at the old falloff nearly every event
  was silent for nearly everybody.
- **Intensity scales the whole playfield AND picks the court shape.** The controller reads a
  **per-intensity scale table** (`settings.arenaScaleByIntensity`, default **1.8× / 2.1× / 2.4× / 2.7×**
  for intensities 1-4; the legacy even
  `lerp(1, intensityScaleAtMax=2, (i-1)/(maxIntensityLevel-1))` ramp remains the fallback when the
  table is empty) — and a **court shape** + **central-goal flag** per intensity
  (`settings.boundaryShapesByIntensity` / `centralGoalByIntensity`, default BeveledBox / Hex / Cylinder
  / Sphere-with-central-goal). All are published as **NetworkVariables** (`n_IntensityScale`,
  `n_BoundaryShape`, `n_CentralGoal`, `n_GoalTarget`) so every peer — including a client that
  spawns AFTER the server set them — applies the same scale + geometry (a one-shot ClientRpc used to
  miss late joiners, leaving them with no visible arena). `AstroLeagueArena.Build(scale, shape, centralGoal)`
  rebuilds the stadium boundary at the scaled dimensions, `AstroLeagueBall.SetSizeScale(scale)`
  resizes the ball (visual + collider) on top of its authored base, the controller pushes the goals +
  team spawns out to the scaled goal lines, and the nucleus is morphed to the boundary (mesh for
  polytopes/cylinder/ring, radius for sphere). Vessels stay normal-size. Players reset to the scaled
  team positions on every kickoff (`ComputeKickoffPose` reads the scaled spawns and scales the lateral
  teammate spacing too).
- **Faceted icosphere — rotation you can see.** The mesh is a medium-poly **flat-shaded
  icosphere** generated at runtime (`IcosphereMeshGenerator`, default 2 subdivisions =
  320 tris) and assigned to the MeshFilter in `SetupVisuals` (the SphereCollider is kept,
  so the inertia tensor stays a uniform sphere → free rotation about any axis). The
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
  Scaled to world radius ≈ 7 for a chunky billiard feel and a precise strike target.
- **The ball is a FIRST-CLASS entity: it resolves prisms by a per-tick spatial scan, NOT by
  physics collisions.** This is the crux. Prism colliders are LOD-culled away from vessels,
  and a fast ball tunnels past tiny box colliders — so collision-driven prism interaction
  only fired near vessels and the map cluttered up. Instead, the ball's collider **excludes
  the `TrailBlocks` layer entirely** (`sphereCol.excludeLayers`, set in `Awake`), so the
  solver never bounces or snags it on a prism, and every physics tick — on **every peer** —
  `ProcessPrismInteractions` sweeps `PrismSpatialIndex.QuerySphere` over the segment the ball
  just travelled (radius = `BallWorldRadius × prismScanRadiusFactor`, multi-sampled so a fast
  ball skips nothing) and resolves the prisms it overlaps. The ball clears/shields trail
  consistently along its **whole path**, exactly like a player vessel senses its surroundings:
  - **Same color** (own trail) → **shield** it (`prism.ActivateShield()`, skipped if already
    shielded). No speed change.
  - **Opposing + unshielded** (or a **neutral** ball, which has no color yet) → **eat it**:
    destroy via the canonical animated `Prism.Damage`, and **slow the ball by the prism's
    MASS** — `speed ×= ballMass / (ballMass + prismDragMassScale · prismVolume)`, direction
    preserved, never reversed. Plowing a thick enemy wall brakes hard; a thin one barely.
    This is the only thing that slows the ball **by domain interaction**; a wall carom also costs
    `wallRestitution` and the coast bleeds on `ballDrag` (see "the ball settles" above). Same-color
    and shielded passes remain free; vessel hits re-energize; `maxSpeed` caps the top.
  - **Opposing + shielded** → **unshield** it (`prism.DeactivateShields()`) and **LEAVE it
    standing this visit** — the shield absorbs the pass. The prism is held in a
    `_shieldPoppedThisVisit` set so it isn't eaten the same overlap; once it leaves scan range
    the protection drops, so a later visit eats the now-unshielded prism.
  - **Super-shielded (any domain)** → **untouched.** Fully invulnerable structure — the arena's
    edge lining. Never popped, never eaten, no speed cost; the ball glides straight through.
- **Per-peer, no broadcast.** Prisms are per-peer GameObjects (laid by `VesselPrismController`
  on every peer, not shared NetworkObjects). Each peer runs the scan over its OWN local copies
  around the ball's local (replicated/dead-reckoned) position using the replicated
  `n_LastHitDomain`, so each peer's view clears/shields its own trail consistently — no
  per-tick RPC. Only the **server** applies the eaten-mass speed drag; clients get the slowed
  velocity via `n_Velocity` replication and just mirror the shield/destroy on their copies.
  Mass is conserved (animated explode-out via spatial-index release + VFX, never raw `Destroy`).
- Layers: the ball is on `Default` (0); prisms run on `TrailBlocks` (11). The ball
  **excludes layer 11** so it passes through all prisms, and still collides with vessels
  (Ships, layer 8) for its elastic strike bounces. The **arena boundary is no longer a
  collider** — containment is a server-side reflect off the `AstroLeagueBoundary` walls
  (`ContainWithinBoundary`), so there are no wall colliders on any layer. (Because prism
  interaction no longer needs colliders, the ball is NOT a `PrismColliderLodManager` focus —
  no extra collider-budget cost.)
- Replay is a full scene reload (the standard domain-games replay path), which clears
  accumulated trail mass with the scene — not a decay mechanism.

## Court Geometry (ricochet boundary)

**Why this exists.** The original boundary was a SPHERE: the ball reflected about the sphere's
radial normal, and on a sphere every surface normal points at the center — so every bounce carried
the ball back toward the middle (a whispering-gallery focusing effect), never a tangential *bank*.
Billiards, air hockey and Rocket League are fun *because* of FLAT walls: a flat plane preserves the
wall-parallel velocity and only flips the perpendicular component, so a glancing shot banks along
the boards instead of being thrown back at center.

**The model — convex polytope = a list of inward face-planes.** Box, octagonal/hexagonal prism,
beveled box and octahedron are all intersections of flat half-spaces, so `AstroLeagueBoundary` stores
each as a `List<(normal, offset)>` and `Contain(ref pos, ref vel, ballRadius, restitution, …)`
reflects the ball off every plane it has poked through (two passes to resolve corners cleanly) and
clamps it to kiss the wall. Sphere and Cylinder keep an analytic curved branch. The **same plane list
drives the visual**: `BuildVisualMesh()` solves every plane triple, keeps the points inside all
planes (the polytope vertices), fan-triangulates each face, and double-sides it (players fly *inside*
the boundary, so it must render through `CageMaterial`'s back-cull). So the glowing nucleus cage IS
the wall the ball hits.

**Shapes** (`AstroLeagueBoundaryShape`):

| Shape | Feel | Notes |
|---|---|---|
| `BeveledBox` | Rocket-League-style | Box with every edge + corner chamfered — flat faces + angled ramps that redirect instead of trap. **(default i1)** |
| `HexagonalPrism` | Tighter 6-wall arena | Elongated hexagon cross-section extruded along the goal axis, flat caps. **(default i2)** |
| `Cylinder` | Banks lengthwise, focuses across | Flat goal caps + curved barrel. **(default i3)** |
| `Sphere` | Center-focusing — pairs with the central goal | Legacy radial reflect; the focusing that's bad for banking is GOOD for the central shared goal (it funnels the ball back through the center). **(default i4, central goal)** |

| `NotchedRing` | Center choke point, lots of bounces | A central **torus ring obstacle** (axis = goal axis) inside an outer court (default Cylinder). The ball bounces off the ring's OUTSIDE; the central hole + an angular **notch** stay open as shooting lanes. |
| `Box` | Pool / air-hockey — sharpest banks | 6 flat walls, 90° corners (can trap). Flat goal caps backboard missed shots. |
| `OctagonalPrism` | "Cage" arena, varied bank angles | Box with its 4 goal-axis edges chamfered → octagon cross-section, 135° corners, flat caps. |
| `Octahedron` | Diamond, every wall banks | 8 angled faces; very different, more chaotic. |

**Every shape wears the arena cover.** The glowing faceted "cover" over the court is the
boundary's own `BuildVisualMesh()` rendered as the cell nucleus through its `CageMaterial` — the
surface you see IS the surface the ball reflects off. The **Sphere** shape used to be the one
exception: it contributed no hull mesh and was merely resized (`Cell.SetNucleusWorldRadius`),
keeping the nucleus prefab's plain sphere, which is why intensity 4 looked bare next to intensity
1. `AppendSphereMesh` now builds a flat-shaded icosphere hull (`IcosphereMeshGenerator`, 3
subdivisions) at the court radius, and the controller feeds the generated mesh to the nucleus for
**every** shape, falling back to a radius only for a degenerate boundary that yields no mesh.

**Central shared goal** (`centralGoalByIntensity[]`, default ON only for intensity 4). Instead of two
end goals, the two `AstroLeagueGoal` detectors move to the **arena center**, facing OPPOSITE ways along
the goal axis (±Z), so there is **ONE shared goal where the pass DIRECTION decides the scorer**: push
the ball +Z (toward the Ruby cone) → Ruby scores; -Z (toward the Jade cone) → Jade scores (own-goal
rules still apply — a team driving it the wrong way feeds the opponent). It is a **pass-through** goal
(scores on the ball's CENTER crossing the plane within the mouth disk, no solid back wall), and the
ball **spawns off-center** in the goal's plane (`centralBallSpawnOffset` along X) so it doesn't start
sitting in the goal. The arena draws a back-to-back Ruby(+Z)/Jade(-Z) cone at center; the AI aims PAST
center along its scoring direction (so it drives the ball THROUGH the disk the right way, not into an
own goal). The central hole + sphere focusing make this read like a "core" you shoot the ball into.

**Per-intensity test harness.** `AstroLeagueSettingsSO.boundaryShapesByIntensity[]` maps one shape to
each intensity (default 1-4 = **BeveledBox / Hex / Cylinder / Sphere+central-goal**). The server reads
`settings.ShapeForIntensity(intensity)` + `CentralGoalForIntensity(intensity)`, publishes them + the
scale via NetworkVariables, and every peer's `ApplyIntensityScale(scale, shape, centralGoal)` rebuilds
the boundary + morphs the nucleus + lays out the goals. Re-map freely in the inspector — pure data.
Tunables: `octagonBevelFraction` / `beveledBoxBevelFraction` (chamfer depth); `boundaryRadius` (sphere
only); `centralBallSpawnOffset`; and the **NotchedRing** ring — `notchedRingOuterShape` (default
Cylinder), `ringMajorRadiusFraction` / `ringTubeRadiusFraction` (ring size, as fractions of the court
cross-section radius — the central hole = major − tube must clear the ball), and `notchCenterDegrees` /
`notchHalfWidthDegrees` (the gap). Polytope/cylinder/ring outer walls derive
from the arena's `arenaLength/width/height` (the goal axis is Z, so the flat ±length/2 caps sit on the
goal lines and "backboard" missed shots — `AstroLeagueGoal`'s plane-cross-within-the-mouth detection
is shape-agnostic and unchanged).

## Super-Shielded Edge Lining

Every court, at every intensity, is rimmed with a dense lining of **SUPER-SHIELDED neutral
prisms** — invulnerable structure marking the arena's edges, each wearing the **stellated
octahedron** (Stella Octangula — the Skim Race track look; `PrismStateManager.ActivateSuperShield`
now engages `PrismStellatedOctahedronShield` with the opaque team material, and `DeactivateShields`
reverses it). `AstroLeagueBoundary.CollectEdgePaths` derives the edge geometry from the same source
as the walls and the cage mesh (polytope hull edges; cylinder cap rims; three latitude rings for the
edge-less sphere; NotchedRing uses its outer court), and `AstroLeagueArena.RebuildEdgeLining` walks
the summed edge length laying a **fixed total count** (`settings.edgePrismCount`, default 240 —
≈20-unit spacing on the doubled intensity-1 box) of `PrismKind.SuperShielded`, `Domains.Blue` prisms
through the standard `BoostRingBuilder.LayOne` pooled path (`prismSpawnChannel` — the PrismFactory
channel, wired in-scene). Long axis along the edge, inset `edgePrismInset × scale` toward the play side.

- **Deterministic volume budget.** Count and prism scale are FIXED across shapes/intensities
  (spacing scales with the arena), so lining volume = `240 × vol(2.5·2.5·10) = 15000` exactly. The
  Astro League Cell Config's phase-volume thresholds are raised by that budget (Restless
  15400/15300, Frenzy 16500/16200) — change either side and retune the other (`Docs/ECOSYSTEM.md §14`).
- **Volume-only cell binding.** Super-shielded structure binds to the cell like fauna bodies:
  counted in `LiveVolume`, excluded from targeting grids / per-domain counts / `DominantDomain` /
  prey signals (`PrismSpatialIndex.ComputeEnvironmentMass`; re-filed on shield transitions via
  `UpdateShieldState`). A permanent neutral lining can never sway node control or bait fauna.
- **Ball + fauna ignore it.** The ball's prism scan skips super-shielded prisms entirely (never
  popped, never eaten, no drag); fauna already skip shielded prey. Vessels DO collide with the
  lining's stellated shields — the rim is physically real.
- **Collider budget:** +240 always-on convex MeshColliders per peer (the engaged stellated shield
  swaps off the LOD-cullable BoxCollider). Static, bounded by `edgePrismCount`; precedent: the Skim
  Race track super-shields its entire spawned track the same way. Zero new physics queries.
- **Continuity/mass:** lining prisms bloom in via the pooled spawn; the only removal is the
  animated `Damage` teardown on an arena rebuild (late-arriving match config on a client).

## Goal Reset & Goal Replay (every non-final goal)

`Celebrate_ClientRpc` now carries the full **on-goal arena reset**, running on every peer
(`settings.goalResetsArena`):

1. **Vessels re-park immediately** on their team's kickoff lines with **speed zeroed**
   (`ParkOwnedVesselsForKickoff` — same owner-authoritative parking as kickoff, plus
   `IVessel.SetInitialSpeed(0)`; the pre-GO kickoff re-park is idempotent on top).
2. **The field sweeps clean**: `ClearFieldPrismsAsync` queries `PrismSpatialIndex.QuerySphere`
   over the court and destroys the accumulated prisms with the canonical animated `Damage` path
   (never a raw `Destroy` — continuity law), staggered center-out over `goalPrismClearSeconds`
   so it reads as a wave washing outward. It skips the super-shielded edge lining (invulnerable)
   and fauna body prisms (the food web is not part of the pitch reset — no imposed death).
   Prisms are per-peer local copies, so each peer clears its own — no RPC, no sync drift.
3. **The goal replay plays on the replay camera** (`settings.goalReplayEnabled`):
   `AstroLeagueGoalReplay` continuously records the replicated ball flight (ring buffer,
   `goalReplayRecordSeconds`, recording gated off while hidden/frozen) and on the goal spawns a
   visual-only ghost ball (`AstroLeagueBall.DressReplayGhost` — same icosphere, same material +
   frozen scorer-tint property block, matching trail) that retraces the shot. **Broadcast
   framing**: `CameraManager.BeginManualReplayCamera` hands the end-camera rig over with no
   follow target, and the replay poses it at a FIXED vantage beside the flight — perpendicular
   to the shot line, elevated by `goalReplayVantageElevation`, pulled back so the whole flight
   fits the FOV × `goalReplayFramingMargin` — then only PANS (`goalReplayPanSpeed`-smoothed
   look-at) to track the ghost, like a stadium camera operator; it never chases the ball at a
   fixed distance. Playback speed is fitted to the celebration + kickoff-freeze window
   (`goalReplayWindowFraction`, floored by `goalReplayMinPlaybackSpeed` — short recordings play
   in slow-mo). The ghost blooms in and shrinks out (continuity), and the gameplay camera is
   restored when playback ends, at kickoff GO, or at match end — whichever lands first. The
   recording is cleared at every kickoff GO so a replay never crosses a reset.

The final (mercy/golden) goal skips all of this — the match-end flow (winner banner →
scoreboard) owns that moment.

## Replay

`UseSceneReloadForReplay = true` — Play Again performs a full network scene reload
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
| `Cell.NucleusIsControlZone` (nucleus as play geometry, not a claim) | `_Scripts/Controller/Environment/Cell.cs` |
| `Cell.FaunaExclusionRadius` (the pen's inner wall) + pen-aware birth position | `_Scripts/Controller/Environment/Cell.cs`, `CellLifeSpawnerBase.cs`, `FloraAndFauna/Fauna.cs` |

## Vessel-Feel Fixes (cross-cutting — why the non-AstroLeague files are in this branch)

Playtesting Astro League with agile vessels (Manta, Rhino) surfaced three feel bugs that were
**not** AstroLeague-specific — they live in shared systems, so the fixes ship in shared files. A
merge reviewer will see these three files in the diff; here's why:

- **`CustomCameraController.cs` — Manta follow-cam jitter.** The gameplay follow-cam previously
  *hard-snapped* position+rotation when the ship's lateral motion exceeded its forward motion and
  *SmoothDamped* otherwise. On a banking vessel whose lateral ≈ forward speed, that binary flipped
  every few frames → alternating instant/lagged transform = visible high-frequency jitter,
  independent of Astro League. Replaced with a **continuous low-pass blend** of the responsiveness
  (`_lateralDominance`, snappier on strafes, smoother on forward — no discontinuity to stutter on),
  plus a **teleport guard** (a fresh spawn / kickoff park jumps the follow target far in one frame;
  snap into place instead of SmoothDamping a wild swing — the "wonky, jittery start"), and a
  **softer ~10 Hz Perlin** camera shake (was ~25 Hz random, which read as buzz).
- **`AstroLeagueBall.cs` — continuous wall-bounce shake after touching the ball.** A frictionless
  ball skimming tangentially along a curved wall bounces every tick with ~0 perpendicular speed, so
  the old wall-juice fired camera shake continuously (the "high-frequency jitter persists after I
  collide with the ball"). `HandleWallBounce` now gates on the **perpendicular** into-wall speed
  (`wallJuiceMinIntensity`) and a **cooldown** (`wallJuiceCooldown` ≥ `strikeShakeDuration`), so only
  genuine banks juice.
- **`VesselChangeSpeedByPrismEffectSO.cs` — hard-brake on your own shielded prism.** The volume-scaled
  slow effect fired on a vessel's *own* trail, which is normally harmless (own trail isn't collidable)
  — but the Astro League ball *shields* friendly trail, making it collidable, so flying into a large
  own prism hard-braked the pilot and oscillating at its edge stuttered. The effect now **skips
  same-domain non-danger prisms** (danger prisms stay friendly-fire per locked design). This is a
  general impact-effect correctness fix; Astro League's shield-your-own-trail rule just made it
  reproducible.

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
| Cleanup-crew species | `_SO_Assets/Cell Configs/Astro League Cell/Astro League Piranha Fauna Config Data.asset` |

## Cell Ecosystem (the environment IS the Cell)

Astro League runs the **standard cell ecosystem** — zero bespoke ecology code, and (post-audit)
zero bespoke *environment* code. A `Cell` GameObject sits at the arena center (origin), parented
under the scene's **`Environment`** container, and self-initializes on `OnInitializeGame` like
every other biome. Vessel-trail prisms inside the cell's sense radius auto-bind to it via
`PrismSpatialIndex`, so the cell's per-domain volume/count climbs as trails accumulate and falls
as the ball + fauna eat them — no controller wiring.

**Spawner bootstrap — the cell needs a crystal (this was the "no fauna" bug).** `Cell.cs` only
calls `StartSpawnerForMode()` from `InitilizePostFirstCellItem()`, which runs on the **first
`runtime.OnCellItemsUpdated` raise** — and that is raised **only when a crystal registers**
(`CellRuntimeDataSO.AddCrystalToList` ← a crystal manager). A cell with no crystal never starts
its spawner, so no amount of phase-threshold tuning produces fauna. Astro League therefore wires a
**`NetworkCrystalManager`** onto the `Game` controller GameObject (sharing its existing
`NetworkObject`), configured for a **single** neutral anchor crystal: `cellData` = the shared
`Runtime Cell Data` SO (same asset the Cell uses), `crystalPrefab` = `Crystal.prefab`,
`crystalCountMode = FixedCount`, `fixedCrystalCount = 1`, `spawnCrystalWithPlayerDomain = false`,
`spawnOnClientReady = true`. On client-ready the server writes one crystal slot to its `n_Slots`
`NetworkList`; the slot replicates and **every peer instantiates its own local crystal** and calls
`AddCrystalToList` locally — bootstrapping each peer's client-local cell. The crystal doubles as a
midfield elemental power-up; it is **not** a scoring/turn objective (Astro League ends on the
time-based `AstroLeagueMatchMonitor` + goal scoring, never on crystals). *(Joust has the identical
latent "cell but no crystal manager → spawner never starts" gap — fix it the same way when its
ecology is wired.)*

**The nucleus here is a WALL, not a claim — and getting that wrong disabled the whole food web.**
`Cell.IsPreyForHerbivore` (and `Boid.IsEdibleForForager`) refuse to feed anything inside the
nucleus, because in a normal cell the nucleus interior is the territorial claim and a fauna
sanctuary. Astro League has no node control and morphed the nucleus into its ricochet court, so
`RefreshNucleusControlRadius` made the control radius the COURT's circumscribing radius: every
prism in the match read as "inside the nucleus" and **nothing on the pitch was ever food**. No
amount of phase/food-floor/population tuning could reach it — the diet predicate returned false
first. `AstroLeagueController.ApplyIntensityScale` now sets **`cell.NucleusIsControlZone = false`**
(after morphing the nucleus; the setter re-measures), which collapses the control zone and returns
the cell to whole-cell semantics — the same state a cell with no `NucleusPrefab` is in. Full
ruling: `Docs/ECOSYSTEM.md §25.1`. **Any mode that repurposes a Cell-owned visual should check what
SEMANTICS it borrowed along with the geometry.**

**The cleanup crew waits outside until the pitch silts up.** The third species,
`Astro League Piranha Fauna Config Data`, is the tadpole prefab shrunk to `BaseBodyScale 0.22`
with `Forager` on (any-domain diet), speed 45-70, a 60-unit graze radius, a 0.6 s behaviour tick
and `StarvationSeconds 40` — small, fast and always hungry, which is what makes it aggressive
without one line of bespoke behaviour. Seed floor 8, cap 22, `CenterFocusBias 0.35`.

It is held OUT of the court by **`Cell.FaunaExclusionRadius`** — the mirror of the existing
`FaunaContainmentRadius`, added for this mode (`Docs/ECOSYSTEM.md §25.2`): a spatial DIET +
STEERING rule, never a wall. `AstroLeagueController.UpdateFaunaExclusion` sets it to the court's
`MaxExtent` while `Cell.Phase == Calm` and to 0 at Restless or above, sweeping over
`faunaExclusionSweepSeconds` so the swarm visibly pours over the wall. **"The arena is getting
crowded" is read from the spine** — `LiveVolume` crossing `RestlessEnterVolume` — not from a
signal invented for the mode, and the ladder's own Enter/Exit hysteresis debounces the edge for
free. It ticks on every peer (fauna and trail prisms are per-peer local objects, like the
goal-reset sweep) — no RPC.

**The Cell owns the environment, not the arena** (`Docs/ECOSYSTEM_MASTERPLAN.md §5.1`,
`CLAUDE.md ▸ "The Cell owns the environment"`). The arena builds only gameplay-bearing structure
(the court boundary's ball physics, goal portals, midfield ring). Everything
atmospheric/territorial — including the boundary surface itself — lives on the `CellConfigDataSO`:

| Need | Cell field | (removed bespoke duplicate) |
|---|---|---|
| Playfield boundary read | `MembranePrefab` | ~~`AstroLeagueArena.BuildEdgeCage` + `settings.edgeColor`~~ |
| Drifting hypersea motes | `CytoplasmPrefab` (`SnowChanger`) | ~~`AstroLeagueArena.BuildPlankton` + `settings.planktonColor/planktonCount`~~ |
| **Court play boundary** (ball bounces off it) | `NucleusPrefab`, morphed to the per-intensity court shape by `AstroLeagueArena` via `Cell.SetNucleusMesh` (polytopes, keeps `CageMaterial`) / `Cell.SetNucleusWorldRadius` (sphere) | ~~6 `BoxCollider` arena walls~~ |

- **Biome = `Astro League Cell Config`** (cloned from the Skim Race trail-grazing biome —
  the no-flora "fauna eat AI trail obstacles" template):
  - `SupportedFloras = []` — **no flora**. Mass is purely vessel trails; nothing is planted.
  - **Food web = `Astro League Spawn Profile`** → three herbivores
    (`Astro League Tadpole`/`Brittlestar` fauna configs; both `FaunaDiet.Herbivore`, no apex
    predator — predators thin foragers, counterproductive to trail cleanup). These are
    Astro-League-specific **low-population** copies of the Skim Race foragers — Tadpole
    `PopulationSize 4`/`MaxLivePopulation 8`, Brittlestar `2`/`4` (≈ **12 live fauna** cap, ⅕ of the
    Skim Race 40+16=56 swarm) — so the arena reads as an ambient ecosystem, not a cloud. They are
    separate assets because the originals are still live in HexRace (via the Skim Race cell) and
    must keep their large populations. Churn is also gentled on the profile:
    `BaseFaunaSpawnTime 30`, `FaunaSpawnIntervalSeconds 2`, `InitialFaunaSpawnWaitTime 8`. Diet is opposing
    prism mass; the **phase/aggression ladder** decides reach: at **Restless/L1** they hunt the
    nearest opposing-color trail; at **Frenzy/L2** they converge on the densest **ANY-domain**
    region and graze even the controlling color — the requested "frenzy eats same-domain mass."
  - `SenseRadiusOverride = 2000` — a fixed sphere that covers the arena at every intensity
    (the intensity-4 court's farthest corner is ≈ 1280 from center; 2000 has margin). Decoupled from
    the visual membrane, exactly like Skim Race's 3000 over the HexRace track.
  - **Phase thresholds — authored in VOLUME, now tuned for RHINO trail, riding a structural
    floor.** A Rhino trail prism is only **≈ 0.75 volume** (`BaseScale (3,3,0.5)`, `Gap 2` → a
    `(0.5, 3, 0.5)` sliver), and it lays two per spawn. The gameplay window is **Restless +600 /
    Frenzy +2000 volume of trail mass** (≈ 800 / 2700 Rhino prisms on the pitch) — but the
    super-shielded edge lining is a permanent **structural floor of exactly 30000 volume**
    (`edgePrismCount 480 × vol(2.5·2.5·10)`; it counts in `LiveVolume` per "volume is the spine"
    while binding volume-only for every other signal, see `Docs/ECOSYSTEM.md §14`), so the config
    sets `RestlessEnterVolume 30600` / `Exit 30450` and `FrenzyEnterVolume 32000` / `Exit 31600`.
    **Change the lining budget and these thresholds together** — the lining doubled from 240 with
    the court's size, and the floor doubled with it. Restless is also the gate that RELEASES the
    fauna into the court (see the cleanup crew above), so this is the mode's primary ecology pacing
    dial and the first thing to move after a playtest. The **count** fields (`Restless 900`,
    `Frenzy 3000`) remain the perf backstop (the volume-only lining never enters `LiveBlockCount`).
    `SpawnProfile.FaunaFoodFloor = 5` (nominal prisms → 80 prey-volume) so herbivores seed against
    the thin prey. The `DomainVolumeIndicator` hex gauge reads the same `FrenzyEnterVolume`, so a
    gauge in this biome sits mostly lit at rest — the accepted cost of keeping the spine's measure
    pure (prompter-confirmed July 2026).
- **Controlling color is emergent** — the cell's `DominantDomain` is whichever domain holds
  the most trail mass; fauna spawn in that color and hunt the opposition (no domain
  asymmetry, no manual assignment).
- **Collider budget:** the cell adds **no new prism colliders** — fauna senses ride
  `PrismSpatialIndex.QuerySphere` (not `Physics.OverlapSphere`), and prism colliders are
  already proximity-LOD'd by vessel foci. The only added cost is the forager bodies
  (bounded by each species' `MaxLivePopulation` perf cap) and the ~0.25s volume recompute.
  Removing the plankton `ParticleSystem` and the 12-segment edge-cage LineRenderers
  *reduces* the per-peer draw/particle cost. The ball excludes the `TrailBlocks` layer, so it
  never collides with the prisms the fauna graze — the two systems are orthogonal (ball eats via
  `Prism.Damage`, fauna via `Consume`, both idempotent on the shared `prism.destroyed` flag).
- **Mass is conserved / continuity:** fauna wither-to-crystal on death (sealed in
  `Fauna.Die`); the only prism sinks are the ball (an active force) and fauna consumption.
  No decay, no lifespan.
- **Networking:** fauna + trail prisms are client-local (no `NetworkObject`) — each peer
  runs its own cell/spawner over its own trail copies, matching the Skim Race/HexRace model.
  Phase can diverge slightly across clients (a known ecosystem caveat); fine for this
  per-client environmental layer.
