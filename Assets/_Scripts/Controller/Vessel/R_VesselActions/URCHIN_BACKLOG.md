# Urchin — open items after the ship-deep pass (2026-08-18)

Every entry below is a **confirmed** finding from the branch's adversarial review (14 agents,
41 findings confirmed, 1 refuted) that was NOT fixed on the branch. They are recorded here rather
than in a commit message so the next session starts from the real state.

Fixed on the branch, for contrast: the `FireSingle` non-uniform-scale blocker, the swept-detection
reentrancy, `Trail.GetBlock`/`AttachedPrism` null safety, the ride's `RideHasGround` fallback, the
camera-ownership gate, the `points - 1` NaN, the "Overcharge" no-op, the missing
`ClientNetworkTransform`, the asset-generator drift, and the Slip ghost's empty collider set.

## Blocking a multiplayer ship (not blocking a merge)

| # | Item | Where | Note |
|---|---|---|---|
| U1 | ~~**Cascade depth and reach are LOCAL element reads.**~~ **FIXED 2026-10-08 (#1005)** - `ResolveGenerations` reads Charge through `R_VesselElementalAbilityHandler.ReplicatedLevel` (the owner-published `NetElementLevels` nibble) and `ResolveRangeScale` evaluates its multiplier with `ElementalFloat.EvaluateReplicated`, so every peer re-executing a volley runs the same depth and reach. Cost, stated in the code: the replicated level is clamped to 0..15, so a Charge DEFICIT now reads as level 0 (resting depth, x1 reach) rather than the extrapolated shallower/shorter cascade. Editor-unverified (two-peer). | `UrchinSpikeActionSO` | Was blocked on a replicated level surface; `NetElementLevels` landed since. |
| U2 | ~~**A steal by a remote client never debits the victim.**~~ **FIXED 2026-10-08 (#1005)** - `Player.ReportPrismStolen_ServerRpc(float volume, FixedString64Bytes victimName)` now carries the victim and the server debits it through `StatsManager.DebitPrismSteal` (the twin of `CreditPrismSteal`, which the server-local path now also uses). The trade - the victim's name is client-supplied - is recorded as `Docs/ScoringSystem/BUGS.md` B19. Editor-unverified (two-peer). | `StatsManager`, `Player` | |

## Gameplay gaps

| # | Item | Where | Note |
|---|---|---|---|
| U3 | ~~**No HUD.**~~ **SHIPPED 2026-10-06** — `UrchinHUDVariant.prefab` (a variant of `VesselHUDPrefab`) binds 4/4 placeholder icons (Chain Spikes / Trail Rider / Track Projector / Slip), the ammo fill as the Charge card's gauge and the binary riding indicator as the Mass card's; the controller pushes the Track recharge veil onto Space and (2026-10, #973 follow-up) the Chain Spikes hold-to-charge progress onto a `SpikeChargeRing` around the Charge icon, read off the executor's `IsChargeArmed` / `ChargeProgress01` (`URCHIN_CHAIN_SPIKES.md` "The charge is READABLE"). `Urchin.prefab` nests it and points `vesselHUDController` at `UrchinVesselHUDController`. All authored by `Tools/Build/author_urchin_hud.py` (`--check`). Editor-unverified. | prefab authoring | Art pass replaces the four placeholder sprites (and `Urchin_ChargeRing.png`) 1:1 (same guids). |
| U4 | ~~**An embedded spike keeps stealing and chain-firing.**~~ **FIXED 2026-10-08 (#1005)** - every sweep exit (the three in the flight loop and the per-hit exits inside `SweepPrismsAlong` / `SweepVesselsAlong`) tests `FlightHalted` = `_flightEndRaised || _embedded`, so an embedded spike stops dispatching, stays where it struck instead of stepping to the segment end, and skips the fuze test. | `Projectile` | |
| U5 | ~~**Cell environments declare the wrong dimension.**~~ **FIXED 2026-10-08 (#1005)** - the base lay declares `PrismscapeDimension.Volume`, so a rider routes to the boundary (face) ride instead of rail-grinding a 3D world in index order. "Leave it unset" was not available: `Trail.Dimension` is non-nullable and `DimensionOf` reads a container's dimension before any census. The worlds that lay real ribbons (Skein, Regatta rails, Switchyard, Breakwater) override the lay and keep their own per-trail dimensions. Editor-unverified. | `CellEnvironmentSpawnableBase` | |
| U6 | ~~**`armGunsOnAttach` writes nothing that is read.**~~ **FIXED 2026-10-08 (#1005)** - the flag and its claim are dropped (no asset serialized the key; nothing reads `GunsActive` for firing). `URCHIN_TRAIL_RIDER.md` "Attaching arms the guns" and its tuning-table row still describe the flag and need the same cut (outside this PR's files). | `VesselAttachPrismEffectSO` | |

## Robustness / correctness (minor)

| # | Item | Where |
|---|---|---|
| U7 | `BlockscapeFollower` never re-acquires ground after its prism is destroyed — the branch's `RideHasGround` fallback now hands the vessel back to free flight, but a re-acquire would keep the ride alive. | `BlockscapeFollower.RefreshGroundPrism` |
| U8 | `RefreshGroundPrism` does not validate the incumbent prism's liveness/identity, so a pooled prism that is reused elsewhere drags the rider with it. | `BlockscapeFollower` |
| U9 | `Microscene.RecycleAsync` re-runs `Prism.Initialize()` on belt prisms and never re-stamps `AssignTrail`, so the Wanderway belt loses trail membership on its first recycle — a lay site the `AssignTrail` sweep missed. | `Microscene` |
| U10 | `Trail.Project`'s park early-outs return a zero heading, which `RideTheTrail` writes into `VesselStatus.Course`. | `Trail` / `TrailFollower` |
| U11 | `HeadingAt` alone among the trail walks does not bridge holes; `IndexOrderHeading` then substitutes the world axis `Vector3.forward`. | `Trail` / `TrailFollower` |
| U12 | `SetDirection`'s terminal clamp is applied to LOOP trails too, where the correct re-expression of a flipped index is a modulo wrap. | `TrailFollower` |
| U13 | ~~`MenuServerPlayerVesselInitializer` unlatches `_isSwapping` after the old vessel is despawned~~ **FIXED 2026-10-08 (#1005)** - `IsSwapping` now also holds on a requesting CLIENT until its player is bound to a different, live vessel (5 s deadline for a swap the server refuses), and `RequestSwap` / the server swap test the vessel with `IsAlive()` instead of an interface `== null`. Editor-unverified (two-peer). | `MenuServerPlayerVesselInitializer` |
| U14 | ~~`ApplyShipMaterialToSlots` round-trips `renderer.materials`~~ **FIXED 2026-10-08 (#1005)** - reads and writes `sharedMaterials`; nothing is set per renderer, so no MaterialPropertyBlock is needed. | `VesselHelper` |
| U15 | `RunEffectIsolated` is wired into `ProjectileImpactor` only; `VesselImpactor` and `SkimmerImpactor` still dispatch bare. It also allocates a closure per effect per contact. | `ImpactorBase` consumers |
| U16 | ~~A cancelled `GhostAsync` runs its `finally` one frame late~~ **FIXED 2026-10-08 (#1005)** - the ghost window carries a generation and only the current one restores the hull. Editor-unverified. | `UrchinSlipActionExecutor` |
| U17 | `pointsOverride <= 0` is the ship-volley-vs-chain-hop discriminator for the `energy--` decrement, so the tooltip's own advertised `barrageSpikeCount = 0` silently re-enables the decrement on the ship's volley. Still live after the 2026-08-18 merge: the charged release passes `ChargedSpikeCount`, which is clamped `>= 1`, so the shipped path is safe — but a Spherical ability authored with 0 points is still mis-tiered. | `Gun.FireSpherical` |

## Not defects, but worth knowing

- `author_urchin_assets.py --check` only validates keys that are PRESENT. A key omitted from a
  body is invisible to it. (The instance that made this concrete — `UrchinSpikeBarrageAction`
  omitting `barrageSpikeCount` — went away with that asset in the 2026-08-18 trigger merge, but
  the gap in the checker did not.)
- `VesselAbilityRowWirer` writes assets but neither records to `FrogletToolChangeLedger` nor draws
  `FrogletToolShipPanel`, which `Docs/TOOLING.md` requires of a writing tool. Pre-existing, upstream.
