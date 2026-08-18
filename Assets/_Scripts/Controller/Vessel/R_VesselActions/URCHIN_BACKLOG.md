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
| U1 | **Cascade depth and reach are LOCAL element reads.** `ResolveGenerations` / `ResolveRangeScale` read the local `ResourceSystem`, so peers run different-depth, different-reach cascades and the prismscape diverges. Its sibling `ResolveRangeFalloff` is correct — it uses the replicated unlock BIT. | `UrchinSpikeActionSO` | Needs a replicated element-LEVEL surface; only unlock bits replicate today (`NetElementUnlocks`). See `Docs/ElementalAbilitySystem/ARCHITECTURE.md` §3.4. |
| U2 | **A steal by a remote client never debits the victim.** `StatsManager.PrismStolen`'s `OwnsAttacker` gate sits above the victim debit, so `PrismsRemaining` / `VolumeRemaining` inflate — and those feed cell control and volume scoring. | `StatsManager` | The credit half round-trips correctly via `Player.ReportPrismStolen_ServerRpc`; only the debit is stranded. Record the trade in `Docs/ScoringSystem/BUGS.md` — `Player.cs`'s docstring already claims it is there and it is not. |

## Gameplay gaps

| # | Item | Where | Note |
|---|---|---|---|
| U3 | **No HUD.** `UrchinVesselHUDController`/`View` are referenced by nothing; there is no `UrchinHUDVariant.prefab` and `Urchin.prefab` has `vesselHUDController: {fileID: 0}`. The vessel ships with **0/4 ability icons** against the LOCKED four-icon contract. | prefab authoring | Every other vessel wires a `<Vessel>HUDVariant.prefab` (Dolphin 35 refs, Sparrow 76). **FrogletTools > Vessels > Wire Vessel Ability Row** builds and binds the row once a HUD prefab exists. |
| U4 | **An embedded spike keeps stealing and chain-firing.** `EmbedAndRetire` halts the mover but defers `RaiseFlightEnded` by `dwellSeconds`, and `_flightEndRaised` is the swept loop's only exit — so the spike continues dispatching the rest of that frame's hits after it has visibly stopped. | `Projectile` | Give the sweep loop an `_embedded` exit alongside `_flightEndRaised`. |
| U5 | **Cell environments declare the wrong dimension.** All twelve `CellEnvironmentSpawnableBase` worlds lay their 30k+ prisms into one default-constructed `Trail`, i.e. `PrismscapeDimension.Trail` (1D). The branch declared `Surface` on the gyroid and Schwarz-P builders but missed the base. An Urchin attaching to a cell environment tries to rail-grind a 3D world. | `CellEnvironmentSpawnableBase` | Declare `Volume`, or leave the dimension unset and let `PrismscapeTopology`'s census resolve it. |
| U6 | **`armGunsOnAttach` writes nothing that is read.** `VesselAttachPrismEffectSO` sets `vesselStatus.GunsActive`, which no consumer reads — so "riding arms the guns" is not actually restored. | `VesselAttachPrismEffectSO` | Either wire a reader or drop the flag and the claim. |

## Robustness / correctness (minor)

| # | Item | Where |
|---|---|---|
| U7 | `BlockscapeFollower` never re-acquires ground after its prism is destroyed — the branch's `RideHasGround` fallback now hands the vessel back to free flight, but a re-acquire would keep the ride alive. | `BlockscapeFollower.RefreshGroundPrism` |
| U8 | `RefreshGroundPrism` does not validate the incumbent prism's liveness/identity, so a pooled prism that is reused elsewhere drags the rider with it. | `BlockscapeFollower` |
| U9 | `Microscene.RecycleAsync` re-runs `Prism.Initialize()` on belt prisms and never re-stamps `AssignTrail`, so the Wanderway belt loses trail membership on its first recycle — a lay site the `AssignTrail` sweep missed. | `Microscene` |
| U10 | `Trail.Project`'s park early-outs return a zero heading, which `RideTheTrail` writes into `VesselStatus.Course`. | `Trail` / `TrailFollower` |
| U11 | `HeadingAt` alone among the trail walks does not bridge holes; `IndexOrderHeading` then substitutes the world axis `Vector3.forward`. | `Trail` / `TrailFollower` |
| U12 | `SetDirection`'s terminal clamp is applied to LOOP trails too, where the correct re-expression of a flipped index is a modulo wrap. | `TrailFollower` |
| U13 | `MenuServerPlayerVesselInitializer` unlatches `_isSwapping` after the old vessel is despawned, so `RequestSwap` is reachable with `player.Vessel` pointing at a destroyed controller. | `MenuServerPlayerVesselInitializer` |
| U14 | `ApplyShipMaterialToSlots` round-trips `renderer.materials`, whose getter clones every slot — including the Body/Window materials it never writes. The `renderer.material` anti-pattern CLAUDE.md names, in new code. | `VesselHelper` |
| U15 | `RunEffectIsolated` is wired into `ProjectileImpactor` only; `VesselImpactor` and `SkimmerImpactor` still dispatch bare. It also allocates a closure per effect per contact. | `ImpactorBase` consumers |
| U16 | A cancelled `GhostAsync` runs its `finally` one frame late, after `Slip()` has re-armed a new ghost — the stale task re-solidifies the hull and kills the new ghost. | `UrchinSlipActionExecutor` |
| U17 | `pointsOverride <= 0` is the ship-volley-vs-chain-hop discriminator for the `energy--` decrement, so the tooltip's own advertised `barrageSpikeCount = 0` silently re-enables the decrement on the ship's volley. | `Gun.FireSpherical` |

## Not defects, but worth knowing

- `author_urchin_assets.py --check` only validates keys that are PRESENT. A key omitted from a
  body is invisible to it — `UrchinSpikeBarrageAction` omits `barrageSpikeCount` and relies on the
  C# initializer (36). Correct today, silently wrong the day that initializer moves.
- `VesselAbilityRowWirer` writes assets but neither records to `FrogletToolChangeLedger` nor draws
  `FrogletToolShipPanel`, which `Docs/TOOLING.md` requires of a writing tool. Pre-existing, upstream.
