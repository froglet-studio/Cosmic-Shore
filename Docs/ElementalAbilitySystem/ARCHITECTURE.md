# Elemental Ability Upgrades — Architecture (proposed)

**Status:** Phases 0–1 SHIPPED (gun repair, `ElementalScaling`, `ElementalAbilityMapSO`,
`R_VesselElementalAbilityHandler`, comeback modifier layer, Sparrow quantitative wiring, fleet
executor hunks). Phase 2 (level-5 qualitative unlocks + replication) is design. Two deltas from
the original design: the handler is **lazily self-initializing** via
`VesselStatus.ElementalAbilityHandler` (the ResourceSystem GetOrAdd pattern — zero prefab wiring)
rather than initialized from `VesselController.Initialize`, and maps are Resources-loaded by
class (`Resources/ElementalAbilityMaps/{VesselClassType}.asset`) rather than referenced from
`SO_Vessel`. `AUDIT.md` is the ground truth of what existed; `BACKLOG.md` sequences the work.

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
  (the ElementalBarsController/ElementalBars precedent). Not SOAP: the signal is vessel-internal, and
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
| **Space** | Gun range (projectile speed and/or lifetime; range = v·T·2/π) | `FullAutoActionExecutor` fire tick + `FireGunActionExecutor.Fire` — live `Multiplier(Space)` on speed/lifetime (authored `speedValue.Value` **375** with `MultiplierAtFullLevel` **9**, so SPACE 0 ≈ 72 u and SPACE 15 ≈ 931 u) | **Piercing bullets** — and ONLY that, on **both** fire modes (bullets and turret prism rounds). SPACE owns REACH; the armour on fired prisms is **MASS 5** (it spent 2026-08 rounds 4–6 here and was returned by sign-off on 2026-08-13) | Per-shot `piercing` flag through `Gun.FireGun → Projectile.Initialize`; prism-impact flow returns the projectile to the factory after the damage effect when not piercing. Must not reuse `DisableColliderNow` until the dud bug is fixed |
| **Time** | Boost speed, on an **indefinite** boost (no heat, no meter) | `VesselTransformer.CurrentBoostAmount()` — live `Multiplier(Time)` on top of `VesselStatus.BoostMultiplier`; the shared field is never mutated | **Elemental Ward**: while boosting, negative `ResourceSystem.ApplyElementalEffect` calls are dropped — buffs still land, live debuffs still decay, non-elemental danger punishments (slow, input mute) still apply | The general `ResourceSystem` immunity state + the shared `VesselElementalImmunity` driver (`WhileBoosting`, gated `Element.Time`, warding `ElementalDebuffSources.All` — a ward declares WHICH debuff classes it stops, and the Sparrow's stops every one; the Dolphin's Drift Ward stops `DangerPrism` alone). The **strafing roll is now BASE kit**, ungated, on `BarrelRollController` (left stick at perimeter + boost). Detail: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md` |
| **Mass** | Turret prism stretch (long z-axis) **+ in-flight round growth on BOTH fire modes** (rounds swell across their flight: 3× at resting Mass, 6× at Mass 10, linear in level over [-5, 15]) | `FullAutoBlockShootActionExecutor` — multiply `BlockScale.z` by `Multiplier(Mass)` at fire time, routed through `TargetScale` + `Prism.Initialize`. Growth: `FullAutoActionSO.ResolveGrowthFactor` → `Projectile.SetFlightGrowth`, scaling the drawn cross-section and the swept hit radius by the same factor every frame | **Shielded Prisms** — turret-fired prisms arrive with one-hit ablative octahedron armour and a wider hit sphere. Returned here from Space 5 by design sign-off (2026-08-13): **MASS owns the SUBSTANCE of what you fire, SPACE owns its REACH** | `FiredPrismState.ShieldedAtMass5` sets `prismProperties.IsShielded` before `Initialize`, off an `IsUpgradeActive(Mass)` snapshot taken per volley |
| **Charge** | Skyburst blast radius | `FireGunActionExecutor.Fire`: replace the literal `0` with `Clamp01(GetLevel(Charge)/10)`; author real min/max on the three skyburst effect assets (the `Lerp(MinScale, MaxScale, Charge)` pipe already exists in `ProjectileDetonatorSO`) | **Skybursts spare the shooter's own domain** | Gate the direct-hit damage in `SkyBurstProjectileDamagePrismEffectSO` on domain when unlocked (per-shot flag plumbed like piercing). **The AOE follows the same per-shot flag** since 2026-08: `ProjectileDetonatorSO` passes `AffectSelfOverride = !SpareOwnDomain`, because the prefabs' authored `affectSelf: 0` had the blast sparing own domain at EVERY level — half the upgrade was pre-unlocked. Prereq: wire steal → `PrismSpatialIndex.UpdateDomain` so "own domain" is live |

Presentation: `ElementalAbilityMaps/Sparrow.asset` re-authors the abandoned branch's verified
input map — Fire (1) / SkyBurst (2) / Turret Stance (6) / Afterburner (7, formerly "Overheat
Boost") — with the elements attached. The stale "Redirection" ability card is replaced by the
turret-stance ability.

## 6. Design-law compliance

- **Danger prisms**: the Charge-5 gate lives strictly in the explosion/projectile layer
  (Explosion→Prism), never in `Prism.Damage` and never in any `*ByDangerPrismEffectSO`
  (Prism→Vessel). No conflict with the locked invariant (AUDIT §4-Charge). The Sparrow's own
  overheat danger trail is gone with the overheat mechanic, and the Time-5 Elemental Ward is
  likewise NOT an exception to it: the ward denies only the elemental *drain*
  (`VesselElementalDebuffByDangerPrismEffectSO`), while the danger prism's speed slam and input
  mute still land on the warded pilot, own domain included.
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
- Ability icons — **the four-icon row has SHIPPED** (Squirrel first; the framework is fleet-wide).
  See §7.1.
- Per-upgrade state (e.g. roll armed) rides the existing per-vessel HUD controllers
  (`SparrowHUDController`) subscribing to the handler's event — same pattern as its current
  weapon-mode icon swap.

### 7.1 The four-icon ability row (LOCKED structure)

Every vessel HUD shows **exactly four ability icons in the lower right — one per ability** — and
their order is not a layout preference, it is the element contract made visible:

> **The icons run charge → mass → space → time, left to right — the same order as the element
> flowers above them.** Each icon sits under the element that upgrades that ability, so "which
> flower do I need to fill to upgrade this?" is answered by position alone.

`VesselHUDView.AbilityDisplayOrder` is the single source of that order (`VesselHUDController` seeds
its upgrade loop from the same array, and `ElementalBarsView` lays the flowers out the same way).
`OnValidate` keeps the `abilityIcons` list sorted into it, and
`VesselHUDView.ValidateAbilityIconRow()` — called once from `VesselHUDController.Initialize`, editor
only — warns when a HUD binds the wrong count, binds a slot out of order, or lays the icons out in
an order that contradicts the bindings.

**The upgrade signal.** `R_VesselElementalAbilityHandler.OnUpgradeStateChanged` →
`VesselHUDController.HandleUpgradeStateChanged` → `VesselHUDView.SetAbilityUpgraded(element, on)`,
which applies three independent layers so the signal survives any per-vessel presentation:

| Layer | What it does | When it is the load-bearing one |
|---|---|---|
| Authored sprite swap | `AbilityIconBinding.upgradedSprite` replaces the icon art, restored on re-lock | Whenever a vessel authors upgraded art (authored sprites only — never runtime-generated) |
| Element badge | That element's **petal**, in the level-5 **white**, blooms in at a corner of the icon (withers out on re-lock — nothing pops in or out) | Always. It is a *child* of the icon, so views that repaint the icon colour every frame cannot stomp it. Sprite + white come from `ElementalBarsConfigSO`, so level 5 reads as the same "all petals white" the flower shows |
| Tint + persistent scale bump | Icon tints to `upgradeHighlightColor` and rests at `upgradeHighlightScale`, with a one-shot unlock punch | Vessels whose icon colour is otherwise static. Set `tintIconOnUpgrade = false` where the icon colour is a live gameplay gauge |

**Vessels whose icons are live gauges** (the Squirrel: tube cooldown fill, drift lean, impact flash,
heat tint) must override `SetAbilityUpgraded` and re-anchor their own captured rest scales to
`AbilityIconRestScale(element)` — otherwise the view's own tweens settle back to the *pre-upgrade*
scale and wipe the bump. `SquirrelVesselHUDView` is the reference implementation.

**Where the row's geometry lives — read this before moving an icon.** A vessel HUD variant is
instantiated *inside the vessel prefab*, and that prefab instance can override the row's rects. The
Squirrel's did: `DriftButton` and `ShieldRingsButton` had their `m_Anchor*.x` / `m_AnchoredPosition.x`
/ `m_SizeDelta.x` overridden in `Assets/_Prefabs/Spacevessels/Squirrel.prefab`, while the other two
buttons' x came from the variant. Editing only the variant therefore moved half the row and left the
other half pinned — four icons collapsing onto two positions. **Every one of those overrides has been
removed**, so `SquirrelHUDVariant.prefab` is now the single source of truth for the row. When you
touch another vessel's row, resolve the effective value through the vessel prefab's
`m_Modifications` first; do not trust the variant alone.

The four buttons are now authored **identically** — same anchor span, `sizeDelta` and
`anchoredPosition` of zero, one shared y — with evenly spaced centres:

| | anchorMin.x | anchorMax.x | centre @1920 |
|---|---|---|---|
| charge | 0.68481258 | 0.76293758 | 1389.8 |
| mass | 0.75652886 | 0.83465386 | 1527.5 |
| space | 0.82824515 | 0.90637015 | 1665.2 |
| time | 0.89996143 | 0.97808643 | 1802.9 |

y is `0.027730448 .. 0.1665395` on all four. Because the CanvasScaler matches **height** (reference
1920×1080, `MatchWidthOrHeight = 1`), canvas *width* varies with aspect ratio — so a row that mixes
anchor-fraction sizing with fixed `sizeDelta` sizing, as this one did, renders unevenly on anything
that is not 16:9. Authoring all four the same way keeps the widths equal and the gaps equal at every
aspect ratio. The box scales with canvas width; the icon inside it does not (a fixed 80×80 child at
0.7 scale), so only the touch target changes size.

### 7.2 Control hints attach to the ability, not to a position

The `(LT)` / `(RT)` glyphs under the row used to be absolutely-positioned objects under
`XBOXRoot` / `PSRoot` / `PCRoot` with no link to what they labelled — so reordering the icons left
them behind, pointing at the wrong abilities. A label is now **bound to an ability**, and its
position is derived:

```
hint.binding (LT / RT / A / B …)          the physical control, authored on the hint
      │  InputHintBindingMap              mirrors what the input strategies raise
      ▼
InputEvents  { LeftStickAction, OnlyLeftStickAction }
      │  ElementalAbilityMapSO.Entries[].Input     (direct match)
      │  R_VesselActionHandler.CollectBoundActions (fallback: the ability's input and the
      │                                             control's input start the same action asset,
      ▼                                             for vessels whose touch/gamepad maps differ)
Element  →  VesselHUDView.TryGetAbilityIcon  →  the icon the label sits under
```

`InputDeviceIconSetSwitcher.BindHintsToAbilities` runs this once from
`VesselHUDController.Initialize` (after `ActionHandler.Initialize`, so the maps are populated) and
re-anchors each hint onto its ability icon plus `attachOffset`. It **does not reparent** — the hint
has to stay under its icon-set root so the Xbox/PS/keyboard switching still works — and it writes
the anchor as a fraction of the hint's own parent, so the placement survives resolution and aspect
changes the same way the row does. Placement retries until the canvas has laid out, and every set is
placed (including inactive ones) so switching devices later needs no extra work.

Two warnings close the loop in the editor: a hint whose control drives no ability on this vessel, and
an ability that *is* bound to an input but has no hint labelling it.

Reassigning an ability to a different input event in the action handler, or moving an icon in the
row, now carries the label along with no manual repositioning.

**Fleet status** (re-measured from the prefabs 2026-08-24). Squirrel, Sparrow, Dolphin **and Scarab**
author the row (four buttons, four bindings, uniform pitch and slot size, charge → mass → space →
time). Manta, Rhino and Serpent have HUD variants with **no** `abilityIcons` bindings; the **Urchin
has no HUD variant at all**, so its complete map has nowhere to draw. Nothing binds the row from a
*vessel* prefab — checked, since the Rhino's row was once missed by looking only at the HUD side.
See `COMPLETION_PUSH.md` for the full scorecard. The Dolphin runs with **both**
`tintIconOnUpgrade` and `showUpgradeBadge` off, because all four of its icons are live gauges — the
persistent scale bump is its only upgrade signal, and its Time-slot jaw tint is a *gauge* colour on
the jaw halves, not an upgrade tint on the (transparent) Time icon. It has no
`InputDeviceIconSetSwitcher`, so its hints do not bind. Sparrow's row was
**Mass, Space, Charge, Time** and is now
reordered; note two of its icons have their sprite driven by gameplay (`missileIcon` by ammo,
`weaponModeIcon` by weapon mode) and both start `enabled = false`, so the sprite-swap layer of the
upgrade signal is unavailable there and the element badge carries it. Sparrow's HUD is **not** a variant
of `VesselHUDPrefab` and has no `InputDeviceIconSetSwitcher`, so its four Xbox + four PlayStation
`ControllerIcon` glyphs are untoggled — both sets render at once — and hints cannot bind to abilities
until a switcher is added.

**Urchin** (revived 2026-08-15) is the inverse case and worth stating separately: its map is
**complete** — four named abilities, four `UpgradeLabel`s, all four L5 upgrades implemented and
gated on `IsUpgradeActive` — while its HUD row is **not yet authored**. So it is blocked on
*wiring*, not on design. Three specifics the auditor will report and that are correct rather than
oversights:

- **Trail Rider (MASS) carries `Input = 0` because it is PASSIVE**, contact-driven, bound to no
  input event. The map cannot distinguish "passive" from "unset", so the hint layer will find no
  control for it — and it should not. Charge/Space/Time carry `LeftStickAction(2)` /
  `RightStickAction(1)` / `Button2Action(7)` and do want hints.
- **LANDED** (this block previously read "nothing binds yet"; later commits on the same branch
  made that false): `Urchin.prefab` wires `R_VesselActionHandler._executors` to an
  `ActionExecutorRegistry`, and `RightStickAction(1)` / `LeftStickAction(2)` / `Button2Action(7)`
  are bound. The abilities are exercisable.
- **STILL OPEN — the HUD.** `UrchinVesselHUDController`/`UrchinVesselHUDView` exist and compile,
  but **no `UrchinHUDVariant.prefab` exists**, `Urchin.prefab` carries
  `vesselHUDController: {fileID: 0}`, and no asset references either script — so the pair is
  unreferenced code and the Urchin ships with **0/4 ability icons**. Every other vessel wires a
  `<Vessel>HUDVariant.prefab` into its vessel prefab (Dolphin 35 references, Sparrow 76). The
  view is designed to add an **ammo** fill and a deliberately **binary** riding indicator on top
  of the fleet-standard four-icon row (the base class owns the row, in charge → mass → space →
  time order). Authoring the prefab is the remaining work; **FrogletTools > Vessels > Wire Vessel
  Ability Row** creates and binds the row once a HUD prefab exists to run it against.

Mechanics for what those four icons will label: `_Scripts/Controller/Vessel/R_VesselActions/`
`URCHIN_CHAIN_SPIKES.md` and `URCHIN_TRAIL_RIDER.md`.

Manta, Rhino and Serpent are blocked on **design**: their maps are still `(open design slot)`
with `Input = 0` and no `UpgradeLabel`, and their HUDs carry 0–2 lower-right icons. Run
**FrogletTools > Vessels > Audit Vessel Ability Rows** (`VesselAbilityRowAuditor`) for the live table — it
checks map completeness, icon count and order, pitch/size uniformity and hint coverage across the whole
fleet from assets alone. At runtime a vessel with no row now warns once per class instead of failing
silently. The
remaining flyable HUDs (Manta, Rhino, Serpent, and the Urchin until its row is authored) have no
`abilityIcons` bindings and varied lower-right layouts; wiring them is per-vessel HUD work — the
framework above needs no further changes.

### 7.3 Gotcha: never write a control hint's SIZE

The glyph objects under `XBOXRoot` / `PSRoot` / `PCRoot` are authored as **pure stretch rects with a
`sizeDelta` of zero** — their entire size comes from the anchor span (an Xbox glyph is 0.185 × 4.479
of a 269 × 11 px root; a PC text is 0.290 × 1.000 of a 366 × 22 px root). So a placement routine that
collapses the anchors to a point and re-supplies the size from `rect.size` renders them at **zero
size**, because that read happens before any layout pass and, for the two inactive set roots, while
Unity is not updating their rects at all. The glyphs vanish, and collapsing the span has destroyed the
only thing that was giving them size, so nothing recovers them.

`PlaceOnAbilityIcon` therefore preserves the anchor **span** and `sizeDelta` exactly and moves only the
anchor **centre**. It never reads `rect.size` and never writes size. Placement also re-runs from
`ApplySet`, so a root that was inactive (and unmeasurable) when the hints were bound gets a correct
pass the moment its set is shown.

Two more traps in the same routine, both of which render the glyph invisible with no error:

- **`Mathf.InverseLerp` clamps to 0..1.** The hint roots are thin strips — `XBOXRoot` is ~11 px tall
  and sits at the very bottom of the canvas, while the ability row is at y ≈ 105 — so the honest
  anchor fraction is **7.3**, not something in 0..1. Clamping collapsed it to 1.0 and the negative
  `attachOffset` then pushed every glyph to negative Y, entirely below the screen. Use
  `InverseLerpUnclamped`; an anchor fraction far outside 0..1 is correct here, not a bug.
- **Verify against the function you actually called.** Both failed fixes were "verified" by a
  simulation that used unclamped arithmetic while the code called the clamping `Mathf.InverseLerp`.
  `WarnIfPlacedOffScreen` now checks the placed rect against the canvas rect and logs when a hint
  lands somewhere it cannot be seen — it would have caught all three failures on the first run.

### 7.4 Two fleet-wide traps this uncovered

**Reordering a row strands its labels unless a switcher runs there.** `BindHintsToAbilities` is a method
on `InputDeviceIconSetSwitcher`, so a HUD with no switcher gets no automatic placement — its glyphs stay
where they were authored. The Sparrow has four `ControllerIcon` glyphs per set but no switcher, so
reordering its row left every label beside the wrong ability. Its glyphs are now shifted onto their own
abilities (each keeping its authored offset), but that is a static fix: **add an
`InputDeviceIconSetSwitcher` to the Sparrow HUD** and the placement becomes automatic, and its Xbox and
PlayStation sets stop rendering simultaneously. Before touching any vessel's row, check whether that HUD
has a switcher.

**A HUD root that is not stretched to its canvas collapses the whole HUD to screen centre.** Every
vessel prefab overrides its HUD-instance root to `anchorMin (0,0) / anchorMax (1,1) / sizeDelta 0` so
screen-fraction anchors mean what they say. The Serpent's was `anchorMin (0.5,0.5) / anchorMax (0.5,0.5)
/ sizeDelta (100,100)` — a 100×100 box at the centre — so its control labels, silhouette and trail
display all clustered mid-screen, and someone had compensated by pushing `Boost Button` to
`anchoredPosition.x = +881`. The root is now stretched like the rest of the fleet and the boost button
re-tuned to hold its rendered position (verified: zero drift). If a vessel's HUD looks centre-clustered,
check the root override first — the children are probably fine.

Sparrow glyph art is separately wrong and needs an artist: the Xbox set uses `R1 Active` where the
control is the right TRIGGER and `R2 Active` where it is the LEFT trigger, and the PlayStation set uses
`triangle` where the control is ✕ and `square` where it is R2. `Buttons/XBOX/` has no left-trigger art
at all.
