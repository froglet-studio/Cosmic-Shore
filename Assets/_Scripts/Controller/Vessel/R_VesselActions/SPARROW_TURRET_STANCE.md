# Sparrow — Turret Stance (MASS)

> **The rule, in one line:** *a turret prism is a bullet that always pierces and stops
> somewhere instead of disappearing.*

While the Sparrow is stopped (`IVesselStatus.IsTranslationRestricted`), its guns fire **prisms**
instead of bullets. Everything the two fire modes have in common is *adopted*, not copied — the
turret reads its fire rate, muzzle speed and flight time straight off the vessel's bullet action
asset. Exactly two things differ:

1. **Turret prisms always pierce.** A bullet is destroyed on its first prism impact unless the
   pilot has the SPACE-5 *Piercing Bullets* upgrade; a turret prism never is. It keeps damaging
   everything it crosses for the whole length of its flight.
2. **At the end of that flight the prism stays.** Where a bullet's lifetime simply expires, the
   turret prism decelerates to rest, re-enables its own colliders, registers with
   `PrismSpatialIndex`, and becomes permanent world mass of the shooter's domain.

Nothing else is meant to be different. If turret prisms ever feel slower, sparser, or weaker than
the guns, that is a bug, not tuning.

## Why the parity is structural

The obvious implementation — author a turret fire rate and a turret speed next to the gun's —
drifts the moment anyone retunes one of them, and it had: the turret shipped at **14 shots/s and
150 u/s** against guns firing **30 shots/s at 1500 u/s**, a 2× cadence gap and a 10× speed gap
that no single edit could close.

So `FullAutoBlockShootActionSO` no longer authors those numbers at all. It holds a reference to
the vessel's `FullAutoActionSO` and derives:

| Turret quantity | Comes from |
|---|---|
| Fire rate | `FullAutoActionSO.FiringRate` |
| Muzzle speed | `FullAutoActionSO.ResolveSpeed(status)` — the authored base × the live SPACE multiplier |
| Flight time / range | `FullAutoActionSO.ProjectileTime`, flown on the bullet's easing curve |

`ResolveSpeed` lives on the SO (not in the gun executor) precisely so both fire modes call the
same method. Retuning the cannons retunes the turret; there is no second number to remember.

The flight itself is the bullet's flight: `MoveAndAnchorAsync` steps by `cos(t·π/2T)` and yields
at `PreLateUpdate`, both lifted deliberately from `Projectile.MoveProjectileAsync`. A prism and a
bullet released at the same instant stay abreast for the whole flight and stop at the same range
(`speed · 2T/π` ≈ **286 u** at the shipped 1500 u/s × 0.3 s). The old random 90–120 u stop
distance is gone — *the end of the path* is the end of the bullet's life, not a separate dial.

## What made them stop on first contact

The flying prism carries a child `Projectile` + `ProjectileImpactor`, and its impact container
pointed at `DomainCheckProjectilePrismHitEffectSO`, which damaged the target **and then damaged
the shooter prism itself** — the exact opposite of pierce. It also read domain and attribution off
the carried prism rather than the pilot, because nothing ever called `Projectile.Initialize` on
it: `VesselStatus` was null, `OwnDomain` was the default, and the friendly-fire check in
`ProjectileImpactor.AcceptImpactee` could therefore never match.

Both are fixed at the source:

- The executor now calls `childProjectile.Initialize(...)` at fire time with the live domain and
  vessel status, and `stopOnFirstPrismImpact: **false**` unconditionally. Pierce is not an upgrade
  here — it is what the stance *is*.
- `SparrowPrismProjectileImpactContainer` now runs the **bullets'**
  `ProjectileDamagePrismEffect`, so "the same effect as its bullets" is literally the same asset.
  Ship-hit and mine-hit effects were already the same two assets as the guns'.
- `DomainCheckProjectilePrismHitEffectSO` and its asset are **deleted**. They had no other user,
  and leaving a live-looking "destroy the shooter's prism on contact" effect in the tree is how
  this comes back.

The projectile is initialized with a **null `ProjectileFactory`**, which is safe by construction:
it is part of a pooled *prism*, not a pooled projectile, and nothing on this flight can reach
`ReturnToFactory` — the pierce flag is off, the prism's container authors no end effects, and
movement is driven by the executor rather than `Projectile.LaunchProjectile`.

## MASS still owns the stance

Unchanged by this pass, and load-bearing:

- **MASS quantitative** stretches the fired prism's long axis (`blockScale.z × Multiplier(Mass)`),
  read live per shot. Volume is `x·y·z`, so the stretch feeds `Cell.LiveVolume` — *volume is the
  spine*.
- **MASS level-5 "Shielded Prisms"** snapshots `IsUpgradeActive(Element.Mass)` at fire time and
  engages a **regular** shield at anchor — after the collider re-enable and the spatial-index
  registration, in that order. Never SuperShield: shielded mass is still edible by fauna via
  devastate, so the food-web sink survives (`Docs/ECOSYSTEM.md` §16).
- Prisms bloom in from zero through `PrismScaleAnimator` during flight and anchor at full size —
  continuity of existence holds at the spawn end.

## Files

| File | Role |
|---|---|
| `R_VesselActions/Data Containers/FullAutoActionSO.cs` | The bullets — and the single authored home of cadence/speed/flight time. `ResolveSpeed` is shared with the turret. |
| `R_VesselActions/Data Containers/FullAutoBlockShootActionSO.cs` | Turret stance config. Adopts the bullet action; authors only prism shape + pool. |
| `R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs` | The fire loop, the bullet-eased flight, and the anchor. |
| `R_VesselActions/Executors/FullAutoActionExecutor.cs` | The gun loop; now resolves speed through the SO. |
| `R_VesselActions/Data Containers/SparrowModeSwitchingFireSO.cs` | Picks bullets vs turret off `IsTranslationRestricted`, per vessel. Unchanged. |
| `_SO_Assets/VesselActions/Sparrow/FullAutoBlockShootAction.asset` | Wires `bulletAction` → `FullAutoAction.asset`. |
| `_SO_Assets/Effects/Effect Containers/Projectile Containers/SparrowPrismProjectileImpactContainer.asset` | Turret prism impact chain — now the bullets' prism effect. |
| `_Prefabs/Trails/Prisms With Pools/Sparrow Projectile Prism.prefab` | The pooled prism the turret fires. |
| `_Prefabs/Spacevessels/Sparrow.prefab` | `spawnVisibilityDelay` → 0. |

## Tuning knobs

Everything that moves both fire modes lives on **`FullAutoAction.asset`**:

| Knob | Value | Effect |
|---|---|---|
| `firingRate` | **30** | Volleys/s for guns **and** turret. |
| `speedValue.Value` | **1500** | Muzzle speed base for guns **and** turret, before the SPACE multiplier (0.4×–2.5×). |
| `projectileTime` | **0.3** | Flight time; with the easing curve → ~286 u of range at base speed. |

Turret-only, on **`FullAutoBlockShootAction.asset`**:

| Knob | Value | Effect |
|---|---|---|
| `blockScale` | **(0.8, 0.5, 5)** | Prism dimensions before the MASS stretch on z. |
| `prismType` | `Sparrow` | Which pool the anchored prism comes from. |
| `disableCollidersOnLaunch` | **true** | Only the projectile trigger registers hits in flight. |

`spawnVisibilityDelay` on the executor (Sparrow prefab) is **0** and should stay there — at bullet
speed every 0.1 s of it is ~150 u of invisible flight.

## Collider / mass budget — read before retuning

The cadence change is the expensive part of this pass, and it is deliberate:

| | Before | After |
|---|---|---|
| Volleys/s | 14 | **30** |
| Muzzles | 2 | 2 |
| **Anchored prisms/s** | 28 | **60** |
| Volume/s (base scale, MASS ×1) | ~56 | **~120** |

A held turret burst therefore lays permanent mass at **~2.1× the previous rate**, and every
anchored prism is a spatial-index registration plus a collider subject to the usual collider-LOD.
Ten seconds of held fire is ~600 prisms. This is what "the same rate as its bullets" costs; the
single lever that moves it is `FullAutoAction.firingRate`, and moving it also retunes the guns.

No new per-frame CPU: the flight loop is one transform write per live prism (projectile motion,
not prism *animation* — the grow-in is still a clock stamp), and it ends after 0.3 s.

## In-editor verification

Scene: any Sparrow-playable multiplayer scene (`MinigameWildlifeLiberation` or
`MinigameFreestyleMultiplayer_Gameplay`). Fly the Sparrow, hold the stationary-mode input
(input 6) to stop, then hold fire (input 1).

1. **Cadence parity.** Fire on the move, then stopped. The audible/visual rate must be
   indistinguishable — both are 30 volleys/s from 2 muzzles. Before this pass the stopped rate was
   visibly less than half.
2. **Speed parity.** Prisms must leave the muzzle as fast as bullets do and travel ~286 u before
   stopping (roughly a bullet's visible reach), not the old lazy 90–120 u lob.
3. **Pierce.** Aim down a line of enemy prisms and hold. A single turret prism must destroy
   **more than one** prism on its way through and keep going to the end of its path. Before, it
   destroyed one and killed itself.
4. **Anchor.** At the end of the flight the prism stops, becomes solid, and stays. Fly into it —
   it should behave as ordinary world mass (it is now in `PrismSpatialIndex`, so AOE and fauna
   see it).
5. **Own-domain pass-through.** Fire through your own anchored prisms — they must not be damaged
   (`ProjectileImpactor` skips own-domain prisms now that `OwnDomain` is actually set).
6. **Attribution.** Prisms you destroy in turret mode must credit **you** on the scoreboard, and
   `SparrowVesselTelemetry.PrismBlocksShot` must tick once per prism fired.
7. **MASS quantitative.** Collect Mass crystals; the fired prisms must visibly lengthen.
8. **MASS level-5.** At Mass 5+, anchored prisms arrive **shielded** (octahedron), and shielded
   ones must still be destructible/edible rather than invulnerable.
9. **Console clean.** No `[FullAutoBlockShoot]` errors, and specifically no
   "No player found to deal damage to prism!" — that message means `Projectile.Initialize` was
   skipped again.
10. **MPPM two-client.** With two clients, one stopped and firing: prisms must appear at the same
    places on both, and the SPACE-scaled speed must be read from the shooter's own map (the
    upgrade bits replicate through `R_VesselActionHandler.NetElementUnlocks`).

## Follow-ups

- **Tunneling.** Both bullets and turret prisms are discrete triggers moved by transform writes
  (`m_CollisionDetection: 0`), so at 1500 u/s they advance ~25 u/frame and can pass through a thin
  prism without registering. The turret prism is the *less* affected of the two (its collider is
  ~5 u long along travel, up to ~12.9 u stretched), but "pierce and keep destroying" would read
  more reliably with a swept test. The right home for that is a segment query on
  `PrismSpatialIndex` (`Docs/SPATIAL_INDEX.md`), not CCD — a transform teleport bypasses CCD
  anyway. Not done here; it would change bullet behaviour too, and parity is the point of this pass.
- **Anchored-mass rate.** If 60 prisms/s proves too much for a cell's phase ladder in practice,
  the fix is `FullAutoAction.firingRate` (both modes) — not a turret-only divisor, which would
  re-open the drift this pass closed.
