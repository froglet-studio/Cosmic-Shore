# Elemental Ability Upgrades — Architecture (proposed)

**Status:** DESIGN — nothing in this document is implemented yet. `AUDIT.md` is the ground truth of
what exists; `BACKLOG.md` sequences the work.

---

## 1. What this is

A new subsystem of the **Elementals** fundamental (CLAUDE.md: "the single system that governs ALL
buffing and debuffing"). Today elementals are quantitative: four per-vessel levels (Mass, Charge,
Space, Time; integer −5…15) scale parameters continuously. This subsystem adds the **qualitative
tier**: at **level 5** in an element (normalized 0.5 — the all-five-petals-white flower), the
vessel ability mapped to that element gains a discrete upgrade.

The contract every vessel is expected to satisfy:

1. **Four abilities**, bound as `InputEvents → ShipActionSO` in the vessel prefab's
   `R_VesselActionHandler` (this is already the R_ system's shape).
2. **Four element mappings** — each element quantitatively scales a parameter of (ideally) one of
   those abilities, with the element assignment following the fleet convention:
   **Space = reach/presence · Time = rate/mobility · Charge = threat/energy output ·
   Mass = physical size/volume production.**
3. **Four level-5 upgrades** — one qualitative change per element, enhancing the mapped ability.

This is an application of "Favor Emergent Systems": the upgrade layer rides the existing element
economy (crystals, debuffs, comeback) rather than adding a parallel progression system. Danger
prisms, mass conservation, and continuity-of-existence constraints are addressed in §6.

## 2. Architectural constraints (derived from the codebase — see AUDIT §1–2)

- **(a) `ShipActionSO` assets are shared and must stay stateless.** No unlock state, no bound
  ElementalFloats, no subscriptions on SOs. (The dead `ElementalFloatBinder` path is *not*
  resurrected; the shared-asset state bugs in AUDIT §2#4 are the cautionary tale.)
- **(b) Per-vessel state lives in executors / vessel-root MonoBehaviours.**
- **(c) The observation channel is `ResourceSystem.OnElementLevelChange`** — a per-vessel C# event
  (the SilhouetteController/ElementalBars precedent). Not SOAP: the signal is vessel-internal, and
  a global channel would recreate the `stationaryModeChanged` cross-talk bug. SOAP is reserved for
  cross-system observers (toasts, analytics, game feed).
- **(d) Config lives in SOs** (CLAUDE.md config separation). The abandoned branch's scattered
  `atFull: 1.5f` call-site literals get hoisted.
- **(e) Do not fork the action system.** Upgrades flow through the same `ShipActionSO → executor`
  path, so AI pilots (which drive the same executors via cloned SOs) and remote clients get them
  for free.
- **(f) Outcome-affecting unlocks must be replicated.** Element levels are local (AUDIT §1.2#4);
  piercing/shielding/domain-sparing change conserved world state, so the unlock bits need an
  authoritative, replicated home.

## 3. Components

### 3.1 `ElementalScaling` (static helper) — cherry-pick + extend

From the abandoned branch (`Assets/_Scripts/Controller/Vessel/ElementalScaling.cs`):
`Level01(status, element)`, `Multiplier(status, element, atFull, minMul)` — live executor-side
reads, anchored at 1× at resting level, `LerpUnclamped` so the debuff band genuinely weakens,
null-safe (no ResourceSystem → 1×). Extended with the tier layer:

```csharp
public static bool IsQualitativeUnlocked(IVesselStatus status, Element element, int threshold = 5)
    => status?.ResourceSystem?.GetLevel(element) >= threshold;   // local read — HUD/cosmetic only
```

Gameplay-outcome checks go through the handler (§3.3), which reads the replicated state.

### 3.2 `ElementalAbilityMapSO` — the per-vessel data home

`Assets/_Scripts/ScriptableObjects/ElementalAbilityMapSO.cs`, namespace
`CosmicShore.ScriptableObjects`, `[CreateAssetMenu("ScriptableObjects/Vessel/Elemental Ability
Map")]`; assets at `Assets/_SO_Assets/ElementalAbilityMaps/{Vessel}.asset`, referenced by
`SO_Vessel` (and readable by Hangar/HUD/codex UI).

Exactly four entries (`OnValidate`-enforced, the `VesselAbilitySetSO` pattern), each:

| Field | Purpose |
|---|---|
| `Element element` | which element owns this slot |
| `ShipActionSO ability` | backlink to the driving action (UI/meta; executors don't need it) |
| `InputEvents input` | HUD binding (absorbed from the branch's `VesselAbilitySetSO` slots) |
| `string label / description` + icons | presentation (authored sprites, never runtime-generated) |
| `float multiplierAtFullLevel`, `float minMultiplier` | quantitative tuning (hoisted `atFull`) |
| `int unlockLevel = 5`, `UnlockLatchPolicy latch` | qualitative tier config |
| `string upgradeLabel / upgradeDescription` (+ optional unlocked-state icon) | what the player is told at L5 |

One asset declares both "which element scales me" and "what I gain at level 5" — the mapping can't
drift between a gameplay SO and a parallel presentation asset (branch lesson #6).

### 3.3 `R_VesselElementalAbilityHandler` — the per-vessel state home

MonoBehaviour on the vessel root (sibling of `R_VesselActionHandler`), initialized from
`VesselController.Initialize` beside `ActionHandler.Initialize`, exposed via a new
`IVesselStatus.ElementalAbilityHandler` property (executors cache it in their `Initialize`, the
same way they cache `ResourceSystem`).

Responsibilities:

- Subscribe `ResourceSystem.OnElementLevelChange` (idempotent `-=`/`+=`; detach in `OnDestroy` —
  the Rhino-HUD lesson).
- Apply the **latch policy** from the map config (§4) to derive the four unlock bits.
- **Owner/server**: write the bits into the replicated `NetworkVariable` (§3.4).
  **All peers**: read unlock state *from the replicated value*, never from the local
  `ResourceSystem`, for anything outcome-affecting.
- Public API: `float Multiplier(Element)` (delegates to `ElementalScaling` with map config),
  `bool IsUpgradeActive(Element)`, C# event `OnUpgradeStateChanged(Element, bool)` (HUD flare,
  audio sting, arming the barrel roll).

### 3.4 Replication

`NetworkVariable<byte> NetElementUnlocks` on `VesselStatus` (already a NetworkBehaviour;
`ResourceSystem` is a RequireComponent sibling) — one bit per element, single-writer
(server-write, mirroring the `Player.NetDomain` discipline; the server derives from the
owner's level events or the server-side copy). Remote execution paths (input actions run on ALL
clients via the ServerRpc→ClientRpc broadcast) resolve piercing/shielding/domain-sparing from the
replicated bits, so every peer destroys the same prisms.

Level *display* (petals) stays local as today; only the four unlock bits replicate.

### 3.5 Detection point

`ResourceSystem.EmitElementLevel` is already the deduped choke point for integer-level
transitions. Either (a) the handler derives crossings from the existing event (preferred — zero
core change), or (b) if more systems need it later, add a sibling
`OnElementThresholdCrossed(Element, int threshold, bool nowAtOrAbove)` beside it. Start with (a).

## 4. Latch policy (recommended default)

`OnElementLevelChange` carries the **effective** level (base + decaying modifiers), so a danger
prism's −0.5×4 s debuff can drop a level-10 vessel below 5 and back. Existing quantitative
consumers scale symmetrically down through debuffs — codebase-consistent behavior is that
**upgrades re-lock while the effective level is < threshold**. To avoid feel-bad flicker:

- **Hysteresis**: unlock at `level ≥ 5`, re-lock at `level < 4` (both configurable per entry).
- **No mid-action interruption**: an in-flight barrel roll completes; a shielded prism stays
  shielded (prism state is independent once granted); already-fired projectiles keep their flags
  (all flags are per-shot snapshots at fire time — which also makes replication timing benign).
- `UnlockLatchPolicy { Relock (default), LatchForTurn }` in the map config for modes that prefer
  earned-is-kept.

## 5. The Sparrow mapping (pilot vessel)

| Element | Quantitative (continuous) | Attach point | Level-5 upgrade | Attach point |
|---|---|---|---|---|
| **Space** | Gun range (projectile speed and/or lifetime; range = v·T·2/π) | `FullAutoActionExecutor` fire tick + `FireGunActionExecutor.Fire` — live `Multiplier(Space)` on speed/lifetime (the authored `speedValue` Min 1500→Max 4000 becomes the tuning range) | **Piercing bullets** (new default below L5: destroy on first prism impact — today's bullets already pierce, see AUDIT §4) | Per-shot `piercing` flag through `Gun.FireGun → Projectile.Initialize`; prism-impact flow returns the projectile to the factory after the damage effect when not piercing. Must not reuse `DisableColliderNow` until the dud bug is fixed |
| **Time** | Boost speed | Boost path: scale the effective boost multiplier in the overheat/boost executor (`Multiplier(Time)` on top of `VesselStatus.BoostMultiplier`) — do not mutate the shared `BoostMultiplier` field | **Barrel roll**: right stick at perimeter + boost → roll (CW right half / CCW left half); visual roll on OrientationHandle/Animator; orthogonal displacement via `ModifyVelocity` (left stick picks the normal direction); **bridging prisms oriented along actual travel** via a `blockRotation` override for the roll duration (replicates via `n_BlockRotation`) | New `BarrelRollActionExecutor` on the vessel (polls the newly-published `RightNormalizedJoystickPosition` + `IsBoosting`, the `MantaAnalogTurnBoostExecutor` pattern), gated on `IsUpgradeActive(Time)`. AI: synthesize the trigger in the executor for `AutoPilotEnabled` vessels (AI never runs input strategies) |
| **Mass** | Turret prism stretch (long z-axis) | `FullAutoBlockShootActionExecutor` — multiply `BlockScale.z` by `Multiplier(Mass)` at fire time, routed through `TargetScale` + `Prism.Initialize` (prereq fix), curve in the SO | **Shielded turret prisms** (regular shield, never SuperShield — fauna must keep their devastate sink) | `prismProperties.IsShielded = true` before `Initialize` (the trail-spawner pattern), gated on `IsUpgradeActive(Mass)` |
| **Charge** | Skyburst blast radius | `FireGunActionExecutor.Fire`: replace the literal `0` with `Clamp01(GetLevel(Charge)/10)`; author real min/max on the three skyburst effect assets (the `Lerp(MinScale, MaxScale, Charge)` pipe already exists in `ProjectileDetonatorSO`) | **Skybursts spare the shooter's own domain** | Gate the direct-hit damage in `SkyBurstProjectileDamagePrismEffectSO` on domain when unlocked (per-shot flag plumbed like piercing); AOE already spares own domain via `affectSelf:0`. Prereq: wire steal → `PrismSpatialIndex.UpdateDomain` so "own domain" is live |

Presentation: `ElementalAbilityMaps/Sparrow.asset` re-authors the abandoned branch's verified
input map — Fire (1) / SkyBurst (2) / Turret Stance (6) / Overheat Boost (7) — with the elements
attached. The stale "Redirection" ability card is replaced by the turret-stance ability.

## 6. Design-law compliance

- **Danger prisms**: the Charge-5 gate lives strictly in the explosion/projectile layer
  (Explosion→Prism), never in `Prism.Damage` and never in any `*ByDangerPrismEffectSO`
  (Prism→Vessel). A L5 Sparrow is still slammed by its own overheat danger trail. No conflict
  with the locked invariant (AUDIT §4-Charge).
- **Mass conservation**: no new sinks or timers. Piercing *reduces* per-prism destruction odds per
  shot; domain-sparing *reduces* destruction; stretch adds volume through the normal spawn
  channel; shields convert destruction into shield-pop. Turret prisms remain permanent
  (never pool-returned) — the pipeline fix (Initialize/registration) actually brings their mass
  *into* the ecosystem's books for the first time.
- **Continuity of existence**: bridging prisms and stretched turret prisms bloom in via the
  standard `Prism.Initialize` path (the current turret pop-in is fixed as a prerequisite);
  shield engage/disengage already animate.
- **Collider budget**: MASS-5 shielded prisms swap Box→convex Mesh 1:1 (count-neutral), but shield
  colliders are currently exempt from collider-LOD — either extend `SetColliderCulledByLod` to the
  shield collider or accept and state the budget line (BACKLOG 2.3). Piercing raises concurrent
  live projectiles; revisit the full-auto pool `bufferSizeTarget`. The barrel roll adds zero
  colliders.
- **Universality**: nothing here is Sparrow-special in the framework — the map SO + handler +
  executor-side reads are the same for all 11 vessels; Sparrow is simply the first authored map.
  The other five flyable vessels' quantitative hunks from the abandoned branch drop into the same
  structure.

## 7. HUD surface

- Element levels: the existing petal flowers (fix the Sparrow's broken `elementBars` wiring
  first — AUDIT §5#1). The all-white state IS the unlock telegraph; add a one-shot bloom/flare via
  `OnUpgradeStateChanged` (juice config in `ElementalBarsConfigSO`, per its single-source rule).
- Ability icons: adopt the branch's **final** view-binding shape only
  (`VesselHUDView.GetAbilitySlotImage`) if/when the four-icon row ships; authored sprites only.
  An unlocked slot swaps to its authored unlocked-state icon.
- Per-upgrade state (e.g. roll armed) rides the existing per-vessel HUD controllers
  (`SparrowHUDController`) subscribing to the handler's event — same pattern as its current
  weapon-mode icon swap.
