# Vessel Fleet — Status, Identity Spec & Completion Guide

Cosmic Shore ships 11 vessel classes (`VesselClassType`). Each is meant to be a self-contained,
*fun* experience defined by **five pillars**:

1. **Identity** — a clear genre/fantasy.
2. **Four abilities ↔ four dynamic icons** — abilities wired on the prefab's
   `R_VesselActionHandler._inputEventShipActions` (InputEvent → `ShipActionSO`); each ability's HUD
   icon conveys live state, not just a static button. **Every vessel shows exactly four ability
   icons** — a hard contract (see "The four-icon contract" below); a slot can bundle several kit
   parts or be a shallow passive, and unfilled slots show an obvious placeholder.
3. **Four elements ↔ four parameters** — `Charge / Mass / Space / Time` each scale a gameplay
   parameter.
4. **A distinct trail** — a per-vessel prism prefab / `TrailScaleProfileSO` / prism controller.
5. **Telemetry** — a `VesselTelemetry` subclass (nice-to-have).

**Squirrel** (racing/drift) is the reference build — fully realizes all five pillars. This document
is the fleet-wide gap map, the canonical identity spec, and the completion checklist for the other
ten, derived from a per-vessel audit.

---

## How each pillar is expressed — code vs. Unity asset

| Pillar | Mechanism | Editable in `.cs`? |
|---|---|---|
| Abilities (P2 wiring) | `R_VesselActionHandler._inputEventShipActions` in `<Vessel>.prefab` | ❌ prefab YAML |
| Ability icons (P2 visuals) | `VesselHUDController` subclass drives a `VesselHUDView` subclass; base toggles per-input `highlights`; subclass adds live state | ✅ controller/view code; ❌ the icon `Image` refs + `HighlightBinding`s live in the HUD prefab |
| Element → parameter (P3) | **executor reads `ResourceSystem` at use-time** via `ElementalScaling` (see below) | ✅ fully code |
| Trail (P4) | prism prefab + `TrailScaleProfileSO` + prism controller; `PrismFactory.PrismType` | ✅ scaler/controller code; ❌ prefab/pool/`prismType` |
| Telemetry (P5) | `VesselTelemetry` subclass + `VesselTelemetryBootstrapper` case + stat-event SOs | ✅ subclass; ❌ stat-event `.asset`s + bootstrapper wiring |

### Why P3 lives in the executor, not on an `ElementalFloat`

`ShipActionSO` assets are **shared and stateless** ("vessel context is passed in each call"). One
asset is referenced by every vessel of that class, so an `ElementalFloat` field on the asset cannot
hold per-vessel state — in multiplayer the last vessel to initialize would drive everyone's value.
Worse, the action-SO binder (`ElementalFloatBinder.BindAndClone`) is **commented out** in
`ShipActionSO.Initialize` **and** broken (it sets a non-existent `"Ship"` property and its clone
drops `element`/`Min`/`Max`). So element scaling on action SOs is dead today.

The fix (shipped this pass): **`ElementalScaling`** (`_Scripts/Controller/Vessel/ElementalScaling.cs`)
— a per-vessel-safe helper the executor calls at use-time:

```csharp
float mul = ElementalScaling.Multiplier(status, Element.Space, atFull: 1.6f);
// or
float scaled = ElementalScaling.Scale(status, Element.Mass, so.BaseValue, atFull: 1.5f);
```

- Reads the vessel's **own** `ResourceSystem.GetNormalizedLevel(element)`.
- **Anchored at `1.0` at the resting level (0):** the authored base value is unchanged at game
  start; elements only add/subtract power as crystals raise/lower levels — the intended elemental
  economy, with **no baseline-regression risk**.
- `Skimmer`-borne `ElementalFloat`s (which DO bind, via `ElementalShipComponent`) are unaffected.

**Element convention (fleet-wide):** `Space → reach/handling`, `Time → duration/cooldown/rate`,
`Charge → energy output`, `Mass → physical size`.

---

## The four-icon contract (`VesselAbilityBar`)

To the player, **every vessel presents exactly four ability icons** — a fixed expectation of "four
abilities," where the icons are the mnemonics. Under the hood an "ability" is decoupled from this:
one slot can be a shallow passive, or several kit parts hamfisted into one ability, because some
vessels are more complicated than others. It is a deliberate simplification of the game's surface.

Shipped this pass (code):

- **`VesselAbilitySetSO`** (`_Scripts/ScriptableObjects/`) — a per-vessel set of **exactly four**
  `VesselAbilitySlot`s (`Label`, `Description`, `Input`, `Icon`, `IsPlaceholder`). The four-slot size
  is enforced in `OnValidate` — you cannot author a set with more or fewer.
- **`VesselAbilityBar`** (`_Scripts/UI/Controller/`) — renders exactly four icons from the set, lights
  each icon while its `Input` is held, and **self-builds its icon row** if none is authored. Any slot
  without a real icon shows an **obvious code-generated placeholder** (`AbilityIconPlaceholder` —
  hazard-stripe tile, no asset needed), so a vessel *cannot* display fewer than four icons.
- **`VesselHUDController`** resolves and initializes a `VesselAbilityBar` under the HUD if present —
  **non-regressing**: existing HUDs are untouched until they adopt a bar.
- **`Tools > Cosmic Shore > Validate Vessel Ability Icons`** (`_Scripts/Editor/`) reports every
  vessel/HUD prefab as compliant / bar-without-set / missing-a-bar. A build gate exists
  (`EnforceOnBuild`, default **off**); flip it on once all vessels are migrated to make a
  non-compliant fleet fail the build — the hard "impossible to ship a vessel without four icons".

**Adopt per vessel (asset step):** add a `VesselAbilityBar` under the vessel's HUD (or let it
self-build), create a `VesselAbilitySetSO` (right-click ▸ *ScriptableObjects/Vessel/Ability Set (4
icons)*), fill the four slots with `Input` + `Label` and a real `Icon` where one exists (leave the
rest as placeholders), and assign the set to the bar. The rich per-vessel state widgets (boost fill,
overcharge dial, …) stay as they are — the bar is only the four-icon mnemonic layer.

---

## Fleet Status Matrix

| Vessel (ID) | Status | Identity (1 line) | HUD (ctrl/view/variant) | Elements mapped in code (C/M/S/T) | Trail | Spawnable? |
|---|---|---|---|---|---|---|
| **Squirrel** (6) | ✅ reference | Vaporwave drift racer | ✓/✓/✓ rich | (reference) | ✓ | ✓ |
| **Sparrow** (11) | ✅ complete | Dual-stance arcade gunship | ✓/✓/✓ rich | **M, T, C** (this pass) + Space via skimmer | ✓ boost-reactive | ✓ |
| **Manta** (1) | 🟡 half-baked | Reaper ray: harvests & chain-detonates enemy trails | ✓/✓/✓ | **C, M, S** (this pass) | ✓ wing prism | ✓ |
| **Dolphin** (2) | 🟡 half-baked | Charge-drift-blast racer | ✓/✓/✓ | **C, T** (this pass) | ✓ | ✓ |
| **Rhino** (3) | 🟡 half-baked | Heavyweight ram/forcefield bruiser | ✓/✓/✓ rich | **M** (this pass) | ✓ armored slab | ✓ |
| **Serpent** (7) | 🟡 half-baked | Stealthy one-thumb wall-weaver | ✓/✓/✓ | **T** (this pass) | ✓ tall slab + seed walls | ✓ |
| **Urchin** (4) | 🔴 stub | Attach-and-shoot sea-urchin turret | ✗/✗/✗ | none | borrows Dolphin prisms | ✗ |
| **Grizzly** (5) | 🔴 stub | Stop-and-fire gun emplacement | ✗/✗/✗ | none (empty `Resources`) | broken channel | ✗ |
| **Termite** (8) | 🔴 stub | Eusocial drone-commander | ✗/✗/✗ | none (empty `Resources`) | disabled | ✗ |
| **Falcon** (9) | 🔴 stub | *identity undefined* (Manta clone) | ✗/✗/✗ | none | none | ✗ |
| **Shrike** (10) | 🔴 stub | *identity undefined* (Manta clone) | ✗/✗/✗ | none | none | ✗ |

**Fleet-wide root cause:** the five stubs' prefabs predate the `ShipAction` MonoBehaviour →
`ShipActionSO` / `R_VesselActionHandler` migration and were left with empty action handlers, orphaned
legacy components, `vesselType = 0`, and absence from `Vessel Prefab Container.asset` — so they are
**unspawnable**. They cannot be made flyable from `.cs` alone; each needs a Unity-editor pass (below).

---

## What this session shipped (code, no Unity needed)

**Real runtime bug fixes** (`fix(vessels): repair real runtime bugs`):
- **Serpent** — boost was `Mathf.Pow(4, stacks)` = **256× at 4 charges** (uncontrollable). Now
  config-driven linear `BoostMultiplier.Value * stacks` → 4×..16×. Removed per-frame log spam; the
  boost SFX now fires only on a real consume, not no-op presses.
- **Dolphin** — `ShardToggleActionExecutor` dereferenced a null `Cell` → **NRE on every drift** in
  cell-less modes (Menu freestyle). Guarded.
- **Rhino** — HUD `Subscribe`/`Unsubscribe` weren't idempotent → **double-counted slow tallies** on
  menu vessel-swap and dead indicators after disable→enable. Added a guard + `OnEnable` re-subscribe.
- **Manta** — overcharge kept process-wide `static` state keyed by `SkimmerImpactor`; destroyed
  impactors leaked and carried state across scenes. Now prunes Unity-destroyed keys.
- **Sparrow** — removed three `Debug.Log` diagnostics from telemetry.

**Element → parameter mappings** (`feat(vessels): elements drive per-vessel parameters`) — via
`ElementalScaling`, anchored at 1× at resting level:

| Vessel | Element → parameter | Where |
|---|---|---|
| Manta | Space → Yawstery turn rate · Charge → overcharge blast · Mass → harvest capacity | `YawsteryActionExecutor`, `SkimmerOverchargeCollectPrismEffectSO` |
| Dolphin | Charge → charge-boost peak · Time → charge fill rate | `ChargeBoostActionExecutor` |
| Rhino | Mass → GrowTrail max slab size | `GrowTrailActionExecutor` |
| Serpent | Time → boost charge duration | `ConsumeBoostActionExecutor` |
| Sparrow | Mass → projectile size · Time → projectile lifetime · Charge → heat-decay rate | `FullAutoActionExecutor`, `OverheatingActionExecutor` |

---

## Canonical identities & the full intended 4×4 mapping

Element columns marked ✅ are wired in code this pass; ⬜ is the intended mapping still to author
(mostly asset-side or a further executor hook).

### Manta (1) — "The Reaper Ray"
An elegant ray that harvests opposing-domain trail mass with its skimmer and detonates it in a
distance-ordered chain — a predator that turns the enemy's own conserved mass against them.
- **Abilities ↔ icons:** Yawstery bank-turn (hold L/R, twin icon) · Boost · Skimmer Overcharge dial
  (count + radial fill + OVERCHARGED banner — built) · *(4th slot open — propose a manual overcharge
  release or an evasive wing-sweep).*
- **Elements:** Space→turn ✅ · Charge→blast ✅ · Mass→harvest capacity ✅ · Time→ramp/cooldown ⬜.
- **Trail:** distinct 20×1×5 wing prism (own mesh/mat/pool).

### Dolphin (2) — "Dolphin Darts"
Thread narrow gaps at max speed to build charge, then drift into a crystal to release one of the
biggest blasts in the HyperSea.
- **Abilities ↔ icons:** Drift(+DriftTrail) · ChargeBoost (11-step charge bar — built) · DeployTeamCrystal · ShardToggle. *(De-bundle ShardToggle off the drift input — asset.)*
- **Elements:** Charge→peak boost ✅ · Time→charge rate ✅ · Space→drift handling ⬜ · Mass→trail size ⬜.
- **Trail:** dedicated Dolphin prism + drift banking. Give the signature blast a bespoke
  `dolphinCrystalExplosionEvent` (`VesselExplosionByCrystalEffectSO` currently `case Dolphin: break;`).

### Rhino (3) — "The Bulldozer"
An armored bruiser that charges straight to boost and fatten its slab into a bulldozing wall, wrapped
in a large forcefield skimmer that spins/slows/danger-blocks enemies.
- **Abilities ↔ icons:** Charge/Boost (passive) · GrowTrail (passive) · **add** Grow-Skimmer button
  (wire the orphaned `GrowSkimmerAction` + executor) · **add** a Ram/Slam burst. Rich reactive HUD
  (skimmer-scale, slowed count, danger-line flash, debuff timer) already excellent.
- **Elements:** Mass→slab size ✅ · Space→skimmer max size ⬜ · Charge→boost/skimmer growth ⬜ · Time→skimmer hold/decay ⬜.
- **Trail:** distinct Mass-scaled armored slab.

### Serpent (7) — "The Wall-Weaver"
A stealthy, territorial one-thumb pilot that plants seeds stealing ALL local blocks into a shieldable
wall to disrupt enemy volume and navigation.
- **Abilities ↔ icons:** Consume Boost (4-pip magazine — built) · Toggle Stationary/Seed-Wall · Cloak+Seed Wall · **Button3 slot empty** — add a Shield-Wall / Burrow ability + a cloak-active/cooldown icon.
- **Elements:** Time→boost duration ✅ · Space→turn/skimmer range ⬜ · Mass→wall/prism size ⬜ · Charge→seed bonding ⬜.
- **Trail:** distinct tall slab + seed-wall/gyroid assembler.

### Sparrow (11) — "The Gunship" (reference-complete)
Dual-stance arcade shooter: full-auto stream ↔ turret block-launcher, crystal-gated SkyBurst heavy
cannon, overheat-boost that turns your own trail into a danger hazard.
- **Abilities ↔ icons:** all 4 wired with rich dynamic icons (weapon-mode swap, missile ammo, heat gauge).
- **Elements:** Mass→projectile size ✅ · Time→projectile lifetime ✅ · Charge→heat-decay rate ✅ · Space→projectile speed/skimmer ⬜ (Space works via the Skimmer's own `ElementalFloat` when enabled).
- **Trail:** distinct + boost-reactive `SparrowPrismController`.

### Urchin (4) — "The Sea-Urchin Turret"
A defensive turret that **attaches** to a prism, fires **twin guns**, and can go **intangible (ghost)**
to escape — stationary area denial. Abilities: Attach/Detach · Twin-Gun Fire · Barrage/Energize · Ghost.
Elements: Charge→gun energy · Mass→projectile size · Space→attach range · Time→ghost/cooldown.

### Grizzly (5) — "The Gun Emplacement"
A heavy gunner that stops to become a **turret** (2× resource gain), charge-fires scaled shots,
detonates live projectiles, and flat-spins to reposition. Abilities: Charged Fire · Toggle Turret ·
Detonate · Spin-Around. Elements: Charge→charge rate · Mass→projectile scale · Space→turn · Time→gain/cooldown.

### Termite (8) — "The Colony"
A drone-commander spawning **queen** (swarm) and **mound** (build-to-crystal) drones and transferring
workers between roles to shrink opposing mass — composes with Flora/Fauna + Cells + Mass. Abilities:
Queen Drones · Mound Drones · Deploy · Recall. Elements: Mass→drone count · Charge→drone speed ·
Space→swarm radius · Time→drone lifetime.

### Falcon (9) — **identity undefined — needs sign-off**
Proposed **"The Stoop"**: a bird-of-prey diver — build altitude/speed, commit to a high-velocity dive
that converts speed into a piercing `FalconProjectile` lance + shockwave. (Orphaned `Gun`/`GunTransformer`
+ `FalconProjectile.prefab` support a dive-and-strike gunship.)

### Shrike (10) — **identity undefined — needs sign-off**
Proposed **"The Impaler"** (the real shrike impales prey on thorns): launch spike-prisms that pin
enemy vessels/mass in place, then harvest the pinned mass — a control/immobilize gunship distinct from
Falcon's dive and Sparrow's stream.

---

## Track B — Unity-editor checklist (per vessel)

Nothing here can be done in `.cs`. Ranked by ROI.

### Finish the half-baked four (highest ROI — working cores)
- **Manta:** author 2–3 ability icon `Image`s in the embedded `MantaVesselHUDView`; replace the single
  orphan `HighlightBinding` (input 2 → nothing) with three bindings on inputs **11/12/13**. *(Headline
  visual bug: no yaw/boost icon ever lights.)*
- **Dolphin:** in `_inputEventShipActions`, move **ShardToggle** off the LeftStick drift input to its
  own button; give ChargeBoost/DeployTeamCrystal proper buttons; add 4 `HighlightBinding`s + icon
  `Image`s in `DolphinHUDVariant.prefab`; remove the null executor slot.
- **Rhino:** wire the orphaned `GrowSkimmerAction` as a button ability (+ its icon); resolve the
  double-drive vs `ShieldSkimmerScaleDriver`.
- **Serpent:** author a 4th `ShipActionSO` `.asset` + `_inputEventShipActions` entry on Button3(8);
  add a cloak-active/cooldown `Image` + RightStick highlight to `SerpentHUDVariant`.
- **All four + Sparrow:** wire `SilhouetteController.elementBars` (run *Tools > Cosmic Shore > Wire
  Elemental Petal Bars*) so the elemental flower shows.

### Resurrect the stubs (each is a multi-step editor project)
For **Urchin, Grizzly, Termite, Falcon, Shrike**:
1. Set `VesselStatus.vesselType` to the correct enum (4/5/8/9/10 — all currently 0) and add the prefab
   to `Vessel Prefab Container.asset._shipPrefabs` (all five absent → **unspawnable**).
2. Author 4 ability `.asset`s (port the legacy `VesselActions/*.cs` logic into new `ShipActionSO` +
   executor pairs that mirror `DeployTeamCrystalActionSO`/`Executor`), assign an
   `ActionExecutorRegistry`, populate `_inputEventShipActions` + the button SOAP events + `AIPilot.abilities`.
3. Build the `HUDVariant.prefab` and assign `VesselStatus.vesselHUDController`.
4. Populate `ResourceSystem.Resources` with the 4 element tracks (**Grizzly & Termite have none**).
5. Create the prism pool + prefab, wire the null `_onPrismSpawnedEventChannel`, set `prismType`; fix
   Termite's `startDelay: 100000`.
6. Wire skimmers, `VesselCustomization._shipGeometries` (empty → hull won't theme / Ghost has nothing
   to phase), enable `SilhouetteController`.
7. Fix copy-pasted class metadata: `SO_Class_Urchin.Abilities` = Serpent's; `SO_Class_Grizzly.Abilities`
   = Serpent's; `SO_Class_Termite.Abilities` = Manta's. Promote Falcon/Shrike class SOs out of `_TEMP`.

### Design decision needed first
Sign off Falcon ("The Stoop") and Shrike ("The Impaler") identities above, or reassign — everything
downstream of P1 depends on it.

---

*Audit + this pass: `claude/vessels-review-completion-5weidk`. Element scaling anchored at 1× at
resting level, so every mapping is additive and cannot regress a vessel's baseline feel.*
