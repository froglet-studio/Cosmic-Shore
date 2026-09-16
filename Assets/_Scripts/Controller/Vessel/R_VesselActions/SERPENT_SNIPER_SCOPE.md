# Serpent — Scope (Space) + Sniper Shot (Charge)

Hold the **left trigger**: the view drops into the cockpit and magnifies with the trigger's own
depth. While it is up, the **right trigger** stops being the cloak and becomes a **sniper shot** —
one hitscan round down the scope's line that destroys what it hits, **including super-shielded
mass**, on a long cooldown.

Two of the Serpent's three open element slots are now filled. **Mass stays open.**

| Element | Ability | Input | Quantitative parameter | Level-5 upgrade |
|---|---|---|---|---|
| Charge | **Sniper Shot** | RT (`RightStickAction`) | cooldown: 12 s at rest → 5.4 s at Charge 10 | **Pierce** — the round carries through up to 3 prisms instead of stopping at the first |
| Mass | *(open design slot)* | — | — | — |
| Space | **Scope** | LT (`LeftStickAction`) | magnification: 22° FOV at full zoom at rest → 11° at Space 10, floored at 8° | **Steady Eye** — the zoom no longer bleeds off while turning |
| Time | Boost Duration | A / Space (`Button1Action`) | boost duration ×1.6 at L10 | — *(unchanged)* |

## Why these elements

**Space is reach/presence fleet-wide, and a scope's whole claim is reach** — so Space owns the
magnification, and the element and the mechanic are the same statement. **Charge is
threat/energy**, and on a weapon whose per-shot effect is already absolute (one round, one prism,
whatever its armour) the only honest axis left is *how often you get to use it* — so Charge owns
the recovery. Neither ability scales through the map's generic `MultiplierAtFullLevel`; both are
pinned to `1` there because the action SOs carry dedicated authored fields, per the
no-double-dipping rule.

## The right trigger is CONTEXTUAL, and neither ability knows about the other

The Serpent already bound RT to `CloakSeedWallAction`. Both actions are now bound to RT and
`R_VesselActionHandler` runs every action bound to an input, so **each one asks the scope whether
the context is its own**:

- `SniperShotActionSO` fires only while `SniperScopeActionExecutor.IsScoped`.
- `CloakSeedWallActionSO` declines to start while it is.

Keeping the question in the two SOs rather than in the handler means re-binding either ability
changes nothing about the rule, and the cloak stays safe on any hull:
`ActionExecutorRegistry.Get` falls back to a child search and returns null on a vessel with no
scope, which reads as "not scoped".

**`IsScoped` is maintained on EVERY peer**, not only where the camera is. The handler round-trips
every press and release through the server, so both executors already run on every machine, and
the sniper resolves its own shot on every machine. Had the flag been local-pilot-only, a remote
replica would have run the cloak *and* the shot on the same press.

## The camera

`VesselFirstPersonView` (`_Scripts/Utility/`) is a new platform driver and a deliberate sibling of
`VesselRearView` — same static shape, same `LateUpdate` push, same identity-guarded bind at the
four `IsLocalPilot` sites in `VesselController`, same `GetCloseCamera` identity test so a death or
replay camera is never seated.

- **No second camera.** The same rig, read from a different seat, for the four reasons
  `Docs/REAR_VIEW.md` records in full (the speed tunnel, `ApplyCameraGraphicsSettings`, per-camera
  background colour, and `Camera.main` all resolve one camera).
- **The pose is applied at the POINT OF USE.** `CustomCameraController.EffectiveOffset`
  substitutes the cockpit while `FirstPerson` is set; `_followOffset` is untouched, so the four
  systems that legitimately move the camera mid-flight (zoom-out abilities, adaptive zoom, the
  skimmer's camera-scaling prism effect, a vessel swap re-applying its `CameraSettingsSO`) keep
  working and cannot eject the pilot from the cockpit.
- **First person beats rear view** rather than composing with it: the z-mirror of a cockpit offset
  is another point inside the same hull.
- **It is a rigid attachment, and both halves matter.** The camera aims along the *target's*
  forward, because the usual look vector (target position − camera position) is ~zero in the
  cockpit and `SafeLookRotation` would decline it, freezing the view. And it does not SmoothDamp:
  any lag at all puts the camera inside the hull it is trying to see past.
- **The eye is MEASURED**, at `1.05 ×` the vessel's own circumscribing hull radius
  (`PrismOcclusionCorridor.MeasureCircumscribedRadius`, the rotation-invariant hull-only
  measurement the occlusion corridor already sizes itself from). The Serpent sits 250 units behind
  its camera in third person; a constant could not serve both it and a Squirrel.
- **The occlusion corridor needs no special handling and is why the view is usable.** It reads
  `_WorldSpaceCameraPos` on the GPU, so in the cockpit it collapses to almost nothing — correct,
  since there is no longer any mass *between* the camera and the ship, and a scoped pilot wants the
  arena undissolved.

## The zoom goes through the speed tunnel's HOME, never through the camera

`VesselSpeedTunnel` owns the gameplay camera's field of view fleet-wide and is its only writer
(`Docs/SPEED_TUNNEL.md`). A direct `Camera.fieldOfView` write fails two ways, both silent: while
the tunnel is engaged it is overwritten every frame, and when the tunnel *engages* it captures
whatever FOV it finds as the home to restore later — **baking the zoom in permanently**.

So the tunnel grew one sanctioned surface:

```csharp
VesselSpeedTunnel.SetHomeFieldOfViewOverride(float fov, Transform key);
VesselSpeedTunnel.ClearHomeFieldOfViewOverride(Transform key);   // swap-guarded, like ClearTarget
VesselSpeedTunnel.HasHomeFieldOfViewOverride;
VesselSpeedTunnel.HomeFieldOfView;                               // the PLAYER's own value
```

Three properties make it safe:

1. **One writer survives.** The tunnel still narrows for speed, it just narrows *from* the scoped
   base — so a scoped Serpent that accelerates still reads its speed in the optics instead of the
   two effects fighting over one number.
2. **`RestoreFov` deliberately does NOT read the override.** Releasing always hands the camera back
   the player's own value.
3. **An active override engages the law on its own** (`Tick`), because the zoom has to hold at a
   standstill where the speed term is zero and nothing else would be writing FOV at all.

The scope's unscoped endpoint is read **live** off the camera (or off `HomeFieldOfView` while an
override is already held, or it would ratchet a little tighter every frame), so the scope respects
the player's own FOV setting — a scope that snapped to 90° would be a zoom *out* for anyone playing
at 70°.

> Rule 21 of the `/vessel` contract says to check an ability still earns the FOV surface without
> the zoom; the Dolphin's Echo Sight did, and its surface was reverted. **A scope without
> magnification is not a scope**, which is why this one earns it.

### Two traps this hit, both found by re-reading the diff rather than by any check

- **A keyed release must happen BEFORE the key moves.** `SetTarget` originally rebound
  `_targetKey` and *then* released, so an outgoing vessel's FOV override stayed keyed to a
  transform nobody would ever pass again — a zoom stranded on the camera for the rest of the
  session, on exactly the path (a mid-match hull swap while scoped) that is hardest to notice.
  The swap guard that matters is upstream in `ClearTarget(Transform)`; once this view has *decided*
  to release, the override is its own to drop outright.
- **"The camera's current FOV" is only the player's FOV while nothing is writing it.** The scope's
  unscoped endpoint read `controller.Camera.fieldOfView` directly, which the speed tunnel has
  already narrowed whenever the pilot is moving — so scoping at speed lerped from the narrowed
  value *and then locked it in as the base*, ratcheting a little tighter every frame. It now asks
  `VesselSpeedTunnel.IsActive` first and takes the tunnel's own `HomeFieldOfView` when the law is
  engaged, falling back to the live camera only when genuinely nothing is writing it (where the
  tunnel's `_homeFov` would instead be stale from some other camera in some other scene).

## The shot

- **Hitscan**, not a projectile: `PrismSpatialIndex.QuerySegment(origin, origin + forward × range,
  pathRadius, …)`.
- **The line is the CAMERA's line** — the vessel's forward axis, which is exactly what the cockpit
  camera is aimed down, so the shot lands where the view is pointing. Deriving it from `Course`
  would put the round somewhere the pilot is not looking whenever the vessel is sliding.
- **The path is a capsule, not a ray.** `QuerySegment` tests a prism's *centre* and a prism is
  several units across, so a mathematical line threaded through a lattice of centres misses almost
  everything it visually passes through. `pathRadius` (4 u) is what makes the round hit what the
  reticle covers.
- **`QuerySegment`'s snapshot is unordered** (it walks buckets), so hits are sorted along the ray
  before anything is destroyed — a sniper round has to stop at the *first* thing it reaches.
- **Own-domain mass is skipped.** The Serpent's identity is the wall it weaves; a rifle that cut
  through it would make the two abilities fight. `Domains.Blue` is the neutral sentinel and stays
  hostile to everyone.

### Breaking a super-shield uses the ONE sanctioned sequence

Super-shielded mass is invulnerable to `Prism.Damage` outright — it early-returns through
`AbsorbSuperShieldHit`. The sanctioned teardown drops the shields **first** and then devastates,
which is precisely what the Rhino's energised blade does
(`RhinoSkimmerDamagePrismEffectSO.PopSuperShield`):

```csharp
prism.DeactivateShields(impact, debrisSpeedLimit);          // sheds the stellation as debris
prism.Damage(impact, domain, name, devastate: true, debrisSpeedLimit: …);
```

Both calls get the same impact vector and the same true-velocity ceiling, so the stellation's
shards and the prism's own pieces fly on identical terms — one effect, two meshes
(`Docs/PRISM_ANIMATION.md §4.8.1`). Nothing pops out of existence and **mass is conserved**: this
is an ACTIVE force with a long cooldown and a scope you have to hold, which is what keeps it clear
of the no-imposed-decay law.

**This is the fleet's second force that can break a super-shield**, after the Rhino's energised
sword. Anything relying on "only the Rhino can open super-shielded mass" — an arena whose rails or
lining are super-shielded for exactly that reason — should be re-checked against a Serpent.

## Tuning knobs

`Assets/_SO_Assets/VesselActions/Serpent/SniperScopeAction.asset`

| Field | Ships at | What it does |
|---|---|---|
| `fieldOfViewAtFullZoom` | 22° | FOV at full zoom at the resting Space level |
| `zoomDepthAtFullSpace` | 2 | zoom depth multiplier at Space 10 (divides the angle → 11°) |
| `minFieldOfView` | 8° | floor, so no element level makes the scope a soda straw |
| `zoomResponse` | 6 /s | how fast the applied zoom chases the trigger — this is what ramps a BINARY trigger (mouse/keyboard) smoothly |
| `zoomDeadzone` | 0.08 | dead travel at the top of the trigger, so a resting pad cannot creep the view in |

`SniperScopeActionExecutor.steadyZoomFloor` (0.35) — fraction of the zoom surviving at full stick
deflection **before** Space 5.

`Assets/_SO_Assets/VesselActions/Serpent/SniperShotAction.asset`

| Field | Ships at | What it does |
|---|---|---|
| `cooldownSeconds` | 12 | wait between shots at resting Charge — the ability's whole cost |
| `cooldownMultiplierAtFullCharge` | 0.45 | → 5.4 s at Charge 10 |
| `rangeUnits` | 3000 | the whole flight; there is no projectile to outrun |
| `pathRadius` | 4 | capsule radius of the hitscan (see above) |
| `pierceCount` | 3 | prisms a PIERCING round takes; 0 = unlimited |
| `debrisSpeed` / `debrisSpeedLimit` | 90 / 120 | true-velocity debris and its ceiling |
| `shakeIntensity` / `shakeDuration` | 0.6 / 0.18 s | the report, local pilot only |

## Files

| File | Role |
|---|---|
| `_Scripts/Utility/VesselFirstPersonView.cs` | **new** — the cockpit platform driver |
| `_Scripts/Utility/VesselSpeedTunnel.cs` | **+** the sanctioned home-FOV override surface |
| `_Scripts/Controller/Camera/CustomCameraController.cs` | **+** `FirstPerson` / `FirstPersonOffset`, the rigid-attach branch, `ApplyShake` extracted |
| `_Scripts/Controller/Vessel/VesselController.cs` | **+** binds the view at the four `IsLocalPilot` sites |
| `…/R_VesselActions/Data Containers/SniperScopeActionSO.cs` | **new** — Space ability config |
| `…/R_VesselActions/Data Containers/SniperShotActionSO.cs` | **new** — Charge ability config |
| `…/R_VesselActions/Executors/SniperScopeActionExecutor.cs` | **new** — scope state, zoom drive, `IsScoped` |
| `…/R_VesselActions/Executors/SniperShotActionExecutor.cs` | **new** — hitscan, cooldown, super-shield teardown |
| `_Scripts/UI/View/CloakSeedWallActionSO.cs` | **+** declines while scoped |
| `_Scripts/UI/Controller/SerpentVesselHUDController.cs` | **+** drives the Charge card's cooldown veil |
| `Assets/Resources/ElementalAbilityMaps/Serpent.asset` | Charge + Space entries authored; Time's `Input` corrected |
| `Assets/_SO_Assets/VesselActions/Serpent/SniperScopeAction.asset` | **new** |
| `Assets/_SO_Assets/VesselActions/Serpent/SniperShotAction.asset` | **new** |
| `Assets/_Prefabs/Spacevessels/Serpent.prefab` | two executor children, registry entries, LT + RT bindings |

## Drive-by corrections

- **`Serpent.asset`'s Time entry had `Input: 0`** (`FullSpeedStraightAction`) while
  `ConsumeBoostAction` actually rides `Button1Action`. That field is not documentation: the ability
  lockup DRAWS each card's control chip from it (`map entry → InputHintBindingMap.BindingFor`), so
  the Serpent's Time card would have drawn the wrong glyph. Corrected to `6`; the ability table now
  reports `A / Space`.
- **`SniperShotActionExecutor` resolves its registry with `GetComponentInParent`, not
  `GetComponent`.** On every shipped vessel the registry lives on the `ShipActions` container and
  each executor sits on a **child** of it, so a same-object lookup returns null and every fallback
  below it is silently dead. `ToggleTranslationModeActionExecutor` carries that bug today and is
  deliberately left alone — it is another ability's play-tested behaviour.

## Checked and clear

- **Rule 26 (re-scoping an L5).** Space 5 on this hull could have granted two upgrades:
  `SerpentVesselExplosionByCrystalEffect.asset` reads `IsUpgradeActive(Element.Space)` for the
  blast's "spares allies" upgrade. It does **not** fire — the asset serializes no
  `_spaceUpgradeSparesAllies`, whose field initializer is `false`, and its
  `_heightMultiplierAtFullSpace` / `_coreMultiplierAtFullCharge` are both `1`, which the code
  skips outright. Nothing to switch off. (Confirmed by
  `Tools/Build/element_ability_table.py Serpent`, which labels both reads *inert — authored x1*.)

## Known limitations

- **The cooldown's element scaling is owner-local.** Element levels never replicate, so a remote
  replica cannot derive the owner's Charge-scaled cooldown. The *press* does replicate and the
  owner is the only machine that can produce one, so the owner's cooldown is the only one that can
  be authoritative: a non-owner therefore enforces only a **floor** (the fastest the ability can
  ever recover), which can never refuse a shot the owner admitted while still bounding a duplicated
  or replayed press. The exact fix, if it is ever needed, is to replicate the resolved cooldown the
  way `R_VesselActionHandler.NetEchoSightShape` replicates the Dolphin's cone — deliberately not
  paid for here.
- **No ability ICONS.** The Serpent binds 0/4 icons, so the ability lockup renders four LOCKED
  cards (its designed state for an un-iconed vessel) and the Charge card's cooldown veil now moves
  on one of them. Wiring real icons needs art plus an in-editor pass with
  **FrogletTools ▸ Vessels ▸ Wire Vessel Ability Row**.
- **The shot has no FMOD event.** `fireEvent` ships **empty**, which is silence — never a borrowed
  event (CLAUDE.md's audio convention). It is an inspector-visible TODO on the
  `SniperShotActionExecutor` component.
- **No AI uses either ability.** `AIPilot` presses nothing here, and `OnButtonPressed` early-returns
  under autopilot, so an AI Serpent flies exactly as it does today.
- **Nothing has been run in the editor.** See below.

## In-editor verification

Everything below is unverified — the branch was authored headlessly. The C# was type-checked
against a Roslyn stub harness whose 34 stub signatures were each grepped out of the real sources
(and negative-controlled), and the six out-of-editor gates pass, but neither can see a camera.

1. **Scene:** any Serpent-capable scene (Menu_Main freestyle is enough; swap to the Serpent with
   the vessel-changer toy). Confirm no console errors on spawn.
2. **Scope:** hold **LT** (or **Left Shift** on keyboard, **LMB** on the one-thumb mouse scheme).
   Expect: the camera cuts to just past the nose, aims where the ship points, and the FOV narrows.
   Ease the trigger — on a pad the zoom should track the depth continuously; on mouse/keyboard it
   should ramp in smoothly over ~1/6 s rather than snapping.
3. **Release:** the camera cuts back to 250 u behind and the FOV returns to **the player's own
   setting** (check Settings ▸ FOV, set it to something non-default first — this is the assertion
   that the speed tunnel's home was not clobbered).
4. **Compose with speed:** scope, then accelerate. The view should narrow *further* with speed and
   widen back to the scoped value, not fight itself. Then unscope at speed — FOV should land on
   the speed-tunnel value for that speed, not on the scoped one.
5. **Steady Eye:** below Space 5, hold the scope and haul the stick over — the zoom should ease
   back out to ~35 % and return when you centre. Raise Space to 5 and it should hold through the
   same manoeuvre.
6. **Shot:** scoped, press **RT**. Expect a camera kick, the prism under the reticle destroyed with
   ordinary animated debris, and the **Charge** card's veil sweeping clockwise over ~12 s. Press
   again during the veil — nothing should happen.
7. **Super-shield:** point at Skim Race track mass (`SegmentSpawner` super-shields it) or an Astro
   League rail and fire. Expect the stella octangula to shed as debris *and* the prism to die —
   both, in one shot. Anything less (a deflect wobble, or the shield popping with the prism
   surviving) means the two-step sequence regressed.
8. **Pierce:** raise Charge to 5 and fire down a line of 3+ prisms — expect 3 destroyed, not 1.
9. **The contextual trigger:** unscoped, press RT — the **cloak** should engage exactly as it does
   today. Scoped, press RT — the cloak must **not** engage.
10. **Teardown:** while scoped, open the overview / pause (or let a turn end). The camera must
    return to third person and the FOV to the player's setting — this exercises
    `R_VesselActionHandler.ReleaseHeldInputs` and the executor's `OnDisable`.
11. **Vessel swap while scoped:** scope, then swap hulls with the vessel-changer toy. The new hull
    must arrive in third person at its own FOV, with no stranded zoom.
12. **MPPM, two clients:** scope and fire on client A. On client B the same prisms must die. Then
    check B's own camera never moved — `IsScoped` is shared, the camera is not.

## Follow-ups

- Wire the four ability icons (art + `Wire Vessel Ability Row`), so the Charge veil and the Space
  card have marks to sit on.
- Author the FMOD event for the shot.
- Fill the **Mass** slot — the last open Serpent design slot. `Docs/ElementalAbilitySystem/FLEET_MAPS.md`
  §2 still proposes *wall prism scale* / **Fortified Wall**, which does not collide with either
  ability added here.
- Consider whether any arena that super-shields structure for the Rhino's sword alone needs
  re-checking now that a second hull can open it.
