# Manta — Sting bombs, Kabloom, Soar wake rings, Yastri turn trails

The Manta's spec remake (2026-08, approved design: "Destroyer → Nuke"). The vessel's whole kit
is **buttonless where it matters**: you arm bombs by flying (skimming), you plant them by flying
(grazing another vessel or a creature), and you detonate them by flying (touching a crystal).
The two inputs the vessel does hold are movement — Soar (dual-trigger analog boost) and Yastri
(hard flat turns). This is the record of how the four abilities work, what the remake threw
away, and how to verify it in the editor.

The map is `Assets/Resources/ElementalAbilityMaps/Manta.asset` — **the asset is the record**:

| Element | Ability | Quantitative (authoring home) | L5 upgrade |
|---|---|---|---|
| Charge | **Sting** (passive, no input) | bomb-bay capacity 3 → 5 at Charge 15 (`capacityPerChargeLevel`) AND skim-charge rate (`chargeRateAtFullCharge`), both on `MantaStingConfig.asset`; map multiplier pinned 1 | **Contagion** — anything caught in a bloom is itself bombed, free |
| Mass | **Yastri** (Input 12, the turn pair) | trail prism VOLUME (`VesselPrismController.trailVolume` on Manta.prefab, 1× → 2.5×, the Squirrel's Heavy Trail machinery); the turn RATE is deliberately unscaled (`turnRateElement: None`); map pinned 1 | **Shielded Turn Trails** — prisms laid during a hard turn come out shielded |
| Space | **Kabloom** (passive — it fires off crystal contact) | every bomb bloom's scale (`blastScaleAtFullSpace` 1.6× on `MantaStingConfig.asset`); map pinned 1 | **No Friendly Fire** — blooms spare allies and allied prisms |
| Time | **Soar** (Input 13, analog boost) | max soaring speed — the map multiplier (1.3 at full, 0.7 floor) IS the authoring home, read fleet-wide by `VesselTransformer.CurrentBoostAmount` | **Wake Highway** — wake rings come twice as often and allies can ride them |

All three pinned multipliers are the no-double-dip rule: a dedicated authored field carries the
scaling, so the map's generic multiplier must not scale the same parameter a second time.

## 1. What the remake threw away

The pre-remake Manta was the "Reaper Ray" overcharge kit — skim to build an overcharge meter,
detonate it as a resource-scaled crystal blast. All of it is **deleted**, not orphaned:

- `SkimmerOverchargeCollectPrismEffectSO` (+ its asset) — the overcharge accumulator.
- `UnstablePrismMaterial.mat` — the overcharge prism look.
- `MantaVesselExplosionByCrystalEffect.asset` — the old resource-scaled crystal blast. Left in
  place it would have fired BESIDE Kabloom on every crystal (a double blast), with its scale
  reading resource slot 0 — which the remake repurposed as the bomb bay mirror.
- `MantaAnimationTemp.cs` — a dead placeholder.
- The HUD's overcharge readouts (prism count, countdown) were **re-purposed, not deleted** —
  `[FormerlySerializedAs]` carries the prefab wiring onto the bomb-bay fields (§6).

The vessel's `Decoys` resource (slot 0) was renamed **Bombs** and is now a pure HUD mirror of
the bomb bay (initial 0; `MantaStingActionExecutor` writes it, nothing else reads it).

## 2. Sting — the buttonless bomb bay

`MantaStingActionExecutor` (on Manta.prefab, config wired directly — a passive ability is in no
input binding map, so `CollectBoundActions` could never find its SO; vessel-skill rule 20).

**Arming (skim-to-charge).** `MantaStingSkimPrismEffectSO` sits in
`MantaStingSkimmerImpactorDataContainer.skimmerPrismEffectsSO` (the renamed overcharge container,
guid preserved so the prefab's nested SkimmerImpactor override never moved). Every prism the
skimmer touches pays `chargePerSkim` into the bay, per-prism cooldown `perPrismChargeCooldown`
(1.5 s) so parking inside one prism doesn't pump the bay. Charge scales the rate
(`chargeRateAtFullCharge` 2× at Charge 10, `minChargeRateMultiplier` floor). A full unit of
charge = one armed bomb, up to capacity.

**Planting (graze/joust).** Two paths, one gate set:
- **Vessels**: `MantaStingPlantBombVesselEffectSO` in the same container's
  `vesselSkimmerEffectsSO` — a rival vessel inside the skimmer sphere gets
  `TryPlantOnVessel`.
- **Lifeforms**: the skim effect's prism branch — a `HealthPrism` contact resolves its owner
  `Fauna` and calls `TryPlantOnFauna`.

Gates, in order: the target is not own-domain; the Manta carries ≥1 armed bomb; the target is
not already bombed (`MantaBomb.IsBombed` — **one bomb per target**, so a bomb is also DENIAL: a
rival Manta tagging a creature first locks you out of it); the closing-speed margin
(`plantSpeedMargin`, 0 = any graze plants — raise it to demand a joust). A successful plant
spends one bomb and stamps a `MantaBombSnapshot` (§4) onto the target. **The target gets no
indication** — no HUD ping, no VFX on the victim's machine. That is the spec, not an oversight.

**The fuse.** `fuseSeconds` (25 s authored; Bloomrush overrides per intensity through the
static `MantaBombRules.FuseSecondsOverride`, reset on domain reload). A fuse expiry detonates at
`fuseBlastScale` — smaller than a crystal-cashed bloom by construction, which is what makes
"beat the fuse" score a fraction without any scoring special case.

**Knock-off (counterplay).** The bomb component watches trigger contacts on its carrier: being
driven through prism mass scrapes the bomb off (it sheds, unexploded — the carrier escaped).
Two exemptions: a grace window after planting (`knockOffGraceSeconds`), and the carrier's OWN
fresh trail (`prism.ownerID == carrierName` within `ownFreshTrailGraceSeconds` — the ribbon
still coming out of the victim's ship must not scrape the bomb, the same owner-scoped shape as
`SELF_TRAIL_CONTACT.md`, tested on `ownerID` so it survives a steal). A bombed creature that
dies before the fuse sheds its bomb (the wither/joust/consume pipelines are not detonations).

## 3. Kabloom — the crystal cash-out

`MantaKabloomByCrystalEffectSO` is the ONLY entry in
`MantaImpactorDataContainer.vesselCrystalEffects`. On crystal contact it:

1. **Detonates every planted bomb** at `kabloomBlastScale` (bigger than a fuse fizzle), via
   `MantaStingActionExecutor.DetonateAllPlanted()` — which returns the cashed count and credits
   `StatsManager.FusesBeaten` (server-direct or `Player.ReportFusesBeaten_ServerRpc`, the
   owner-detects → server-records round-trip StatsManager already uses for fauna kills).
2. Spawns the **extra domained blast** at the ship — `selfBlastPrefabs` = `AOEMantaBloom` +
   `AOEFlowerCreation` (the flower bloom at the Manta), with `AffectSelfOverride = false`
   (domained, always).

**Double-fire dedupe**: crystal effect dispatch is lockstep-broadcast
(`NetworkVesselImpactor` ServerRpc→ClientRpc plus the local fallback can both land), so the
effect keeps a static per-impactor 0.15 s cooldown — the Dolphin blast's exact pattern —
reset on domain reload.

**The bloom** (`AOEMantaBloom.prefab`, a flat copy of `AOEExplosion.prefab` with its stale
serialized effect keys replaced by a real container):
`MantaBloomExplosionImpactorDataContainer` carries `MantaBombDebuffByExplosionEffect` — a
**Mass + Space** decaying debuff on caught pilots (the spec's pick; the `elements` array on
`VesselElementalDebuffByExplosionEffectSO` defaults to all four so every existing asset is
unchanged) — plus the shared `VesselCombatHitByCrystalBlast` report, so a bloom's pilot hits
are COUNTED platform-wide and SCORED only where a mode's `ScoringRuleSO` pays for them (the
Dog Fight/Bends split).

## 4. Bombs are LOCAL objects — the snapshot + relay model

A bomb is a `MantaBomb` MonoBehaviour on the victim, spawned only on the **simulation
authority** for the planting Manta (`IsSimAuthority` = network owner, or always when no
NetworkManager is listening — the single-player fallback). Everything a bomb will ever need is
snapshotted at PLANT time into `MantaBombSnapshot`: config, planter name/domain/vessel,
`Contagion` (= `IsUpgradeActive(Charge)`), `AffectSelf` (= `!IsUpgradeActive(Space)` — No
Friendly Fire flips it), the Space blast multiplier, and the fuse (mode override applied here).
Per-use snapshot at plant is the vessel contract's replication rule: a bomb planted before an
upgrade landed behaves as planted.

Peers see the RESULT, not the bomb: `MantaBombNetworkRelay` (NetworkBehaviour on the Manta
root) broadcasts each bloom (position, scale, affectSelf) — ServerRpc → ClientRpc with the
originator skipped by `SenderClientId`, so the machine that simulated the bomb never
double-blooms. Wake rings ride the same relay. Scoring needs no extra networking: the bloom's
prism destruction is credited by whoever SIMULATES the attacker (`StatsManager.OwnsAttacker`,
the Rampage rule), and FusesBeaten rides its RPC.

**Contagion (Charge 5).** When a bloom resolves, the detonation sweeps its radius: vessels from
`GameDataSO.Players` by distance, creatures via `PrismSpatialIndex.QuerySphere` →
`HealthPrism` → owner `Fauna` — and plants a free bomb on every un-bombed, non-allied target
caught. One good route cascades through a whole pack. Uses the dying bomb's own snapshot, so a
contagion chain keeps the original planter's credit and upgrade state.

## 5. Soar + Yastri — the two held inputs

**Soar** (`MantaAnalogTurnBoostExecutor`): the dual-trigger analog boost. The remake widened
its device gate to **gamepad OR keyboard** — the keyboard Manta previously could neither boost
nor turn, which read as a broken vessel on desktop. Time's map multiplier (1.3) reaches it
through the fleet-shared `VesselTransformer.CurrentBoostAmount` path; nothing Manta-local
consumes it.

**Wake rings (Time 5 — "Wake Highway", plus the base behaviour).**
`MantaWakeRingActionExecutor` (passive, config wired directly) lays a boost ring behind the
Manta every `ringPeriodSeconds` **while boosting** (8 s base, 4 s at Time 5), through
`BoostRingBuilder.LayRing` — the Squirrel/Urchin lay path, so a ring is ordinary conserved
prism mass in the pilot's domain. Each ring carries a `MantaWakeRingSwitch`: a SphereCollider
trigger at **exactly `RingRadius`** (the Switch law — the ring IS the trigger volume, drawn at
its own radius), and threading it pays a velocity surge along the rider's course
(`VesselTransformer.ModifyVelocity`). Rider eligibility is snapshotted at LAY time: below Time 5
only the layer rides their own rings; at Time 5 any own-domain vessel does — the highway the
team can follow. The switch retires itself when its prisms are gone (checked by
`TimeCreated` identity, so a pooled prism reused elsewhere can't keep a dead switch alive), and
riding is gated on the rider's sim authority so a surge is applied exactly once, on the machine
that owns that vessel's motion.

> **Documented adaptation:** the spec asked wake rings to grant "meaningful boost refill". The
> Manta has no boost meter — Soar is a held analog trigger, not a charged resource — so the ring
> pays the thing a refill would have bought: speed, as a surge. If the Manta ever grows a
> metered boost, revisit.

**Yastri** (`YawsteryActionSO` / `YawsteryActionExecutor`, Input 12): the hard flat turn. The
remake moved its element read onto `turnRateElement` (default **None** — the turn rate is
deliberately unscaled; Space was re-scoped to Kabloom, and an absent key on the shipped assets
deserializes to None, which is why the field could be added without touching them). What the
turn now drives is the TRAIL: `driveTrailFlare: 1` on both `YawsteryAction-Left/Right.asset`
feeds turn intensity into `VesselPrismController.SetTurnTrail(amount01, turnSign)`, which
flares the OUTER-lane prism (`turnFlareMaxScale` 2× on the x axis, applied before the lane
shift) — the banked wall the spec draws. The executor clears the flare in its `finally` block
(a cancelled UniTask never runs its tail — the flare must not latch). **Shielded Turn Trails
(Mass 5)**: prisms laid while the flare is ≥ 0.5 come out shielded
(`turnUpgradeShieldsTrail` on Manta.prefab, gated per-spawn on `IsUpgradeActive(Mass)` — the
Squirrel Heavy Trail condition, one more OR term).

## 6. HUD

`MantaVesselHUDController` / `MantaVesselHUDView` were rewritten around the bomb bay. The old
overcharge fields carry over by `[FormerlySerializedAs]` so the prefab wiring survived the
rename: `bombChargeFill` (was `fillImage`) — the bay's charge gauge; `armedCountText` (was
`overchargePrismCount`) — armed bombs / capacity; `fuseContainer`/`fuseText` (were the
overcharge countdown pair) — the PLANTED board: how many bombs are out, and the shortest fuse
remaining (the controller polls `ShortestFuseRemaining`; a number that only ever counts down
needs no event channel). The controller binds the executor by serialized reference on the
vessel's own prefab (never `GetComponentInChildren` of another vessel's type — rule 14), with
the symmetric Rebind/Unbind pair, detach-first, pilot gate after the detach.

The four-icon row is authored in Manta.prefab at the fleet-standard wirer bands
(charge → mass → space → time), bound in `AbilityDisplayOrder`. **Icon sprites are empty** —
art is the polish pass (see Follow-ups); the ability lockup renders the four cards regardless.

## 7. Files

| Role | File |
|---|---|
| Bomb bay config (all Sting/Kabloom tuning) | `R_VesselActions/Data Containers/MantaStingConfigSO.cs` → `_SO_Assets/VesselActions/Manta/MantaStingConfig.asset` |
| Bomb component + snapshot + bloom spawn | `Controller/Vessel/MantaBomb.cs` |
| Bay executor (charge, plant, detonate, registry) | `R_VesselActions/Executors/MantaStingActionExecutor.cs` |
| Bloom/ring relay (NetworkBehaviour, Manta root) | `Controller/Vessel/MantaBombNetworkRelay.cs` |
| Wake ring config / executor / switch | `.../MantaWakeRingConfigSO.cs`, `.../MantaWakeRingActionExecutor.cs` → `MantaWakeRingConfig.asset` |
| Skim-charge + fauna-plant effect | `EffectsSO/Skimmer Prism Effects/MantaStingSkimPrismEffectSO.cs` → `MantaStingSkimPrismEffect.asset` |
| Vessel-graze plant effect | `EffectsSO/Vessel Skimmer Effects/MantaStingPlantBombVesselEffectSO.cs` → `MantaStingPlantBombVesselEffect.asset` |
| Kabloom crystal effect | `EffectsSO/Vessel Crystal Effects/MantaKabloomByCrystalEffectSO.cs` → `MantaKabloomByCrystalEffect.asset` |
| The bloom + its container + Mass/Space debuff | `_Prefabs/Projectile/AOEMantaBloom.prefab`, `MantaBloomExplosionImpactorDataContainer.asset`, `MantaBombDebuffByExplosionEffect.asset` |
| Skimmer container (renamed, guid preserved) | `MantaStingSkimmerImpactorDataContainer.asset` |
| Vessel container (crystal slot → Kabloom) | `MantaImpactorDataContainer.asset` |
| The map | `Assets/Resources/ElementalAbilityMaps/Manta.asset` |
| FusesBeaten stat | `StatsManager.FusesBeaten`, `Player.ReportFusesBeaten_ServerRpc`, `IRoundStats`/`RoundStats.FusesBeaten` (full replicated-stat set) |
| Asset generator (idempotent, `--check`) | `Tools/Build/author_manta_kit_assets.py` |
| Turn-trail flare | `VesselPrismController.SetTurnTrail` + `turnFlareMaxScale`/`turnUpgradeShieldsTrail` (Manta.prefab) |

## 8. Tuning knobs (`MantaStingConfig.asset` unless noted)

| Knob | Shipped | Meaning |
|---|---|---|
| `baseCapacity` / `capacityPerChargeLevel` / `maxCapacity` | 3 / 0.2 / 5 | bay = 3 + 0.2×Charge, so 5 at Charge 15 (spec) |
| `chargePerSkim` / `perPrismChargeCooldown` | 0.34 / 1.5 | ~3 distinct prisms arm one bomb |
| `chargeRateAtFullCharge` / `minChargeRateMultiplier` | 2 / 0.25 | Charge scales arming speed |
| `fuseSeconds` | 25 | mode-overridable via `MantaBombRules.FuseSecondsOverride` |
| `plantSpeedMargin` | 0 | closing speed required to plant; 0 = any graze |
| `knockOffGraceSeconds` / `ownFreshTrailGraceSeconds` | 1 / 6 | scrape-off exemptions |
| `kabloomBlastScale` / `fuseBlastScale` / `blastScaleAtFullSpace` | (asset) | cashed vs fizzle vs Space growth — keep cashed > fizzle or "beat the fuse" stops meaning anything |
| `contagionRadiusFraction` | (asset) | how far a bloom re-plants |
| `MantaWakeRingConfig.asset` | — | ring period (base/Time-5), radius, surge strength/seconds |

## 9. In-editor verification (not yet run — no Unity CLI in the authoring session)

1. Open Manta.prefab: `MantaStingActionExecutor` + `MantaWakeRingActionExecutor` on the actions
   object with configs wired; `MantaBombNetworkRelay` on the root; HUD view's four icons bound;
   `trailVolume` enabled (Mass 1→2.5); no Missing (Mono Script) rows.
2. Play Freestyle as Manta: skim the cell's mass — bay gauge fills, a full unit ticks the armed
   count. Graze a creature — armed count drops by one, fuse board shows the countdown. Touch a
   crystal — every planted bomb blooms, the flower + domained blast fire at the ship, the spent
   crystal plays its payoff (the old Manta-only mute on `OmniCrystalImpactor` is removed).
3. Let a fuse expire — visibly smaller bloom.
4. Ram a bombed AI through dense mass — the bomb scrapes off (no bloom).
5. Two-client party: plant on the remote pilot — NO indication on their screen; the bloom
   appears on both machines once (no double blast on the owner).
6. Time 5: boost continuously — rings every 4 s; a same-domain second pilot threads one and
   surges. Below Time 5 the ally passes through inert.
7. Mass 5: hold a hard Yastri turn — outer-lane prisms visibly flared AND shielded.
8. FrogletTools > Vessels > Audit Vessel Ability Rows / Audit Ability Lockups / Audit Vessel
   Skimmers — Manta rows green (skimmer audit: the sting container holds prism + vessel
   effects).

## 10. Follow-ups

- **Icon art**: the four ability icons ship with empty sprites (structure only). Art polish
  pass owns the sprites per the spec's ownership note.
- **AI Manta lays no wake rings** — `AIPilot` never drives the analog boost, so `IsBoosting`
  never latches for a bot. Harmless (rings are additive), but an AI teammate contributes no
  highway. Needs an AI boost policy before Wake Highway matters in solo play.
- **Pre-existing dangling refs** on Manta.prefab's `ElementalBarsController` (config/view) —
  present at the branch base, not introduced here; the view falls back to Resources.
- **Prismatic Relay** (the spec's second minigame sketch) is not built; Bloomrush is minigame 1.
- **`SO_Class_Manta` description/abilities meta-layer** (hangar copy) still describes the old
  kit — text-only, no gameplay read.
- **`AOEFlowerCreation` in the Kabloom self-blast** needs an eyes-on check that the flower
  reads at the shipped scale beside the domained blast.
