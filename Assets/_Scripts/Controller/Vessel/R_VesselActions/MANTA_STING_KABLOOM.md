# Manta — Sting bombs, Kabloom, Soar, Yastri turn trails

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
| Time | **Soar** (Input 13, analog boost) | max soaring speed — the map multiplier (1.3 at full, 0.7 floor) IS the authoring home, read fleet-wide by `VesselTransformer.CurrentBoostAmount` | *(open — Wake Highway was built and cut 2026-09; see §5)* |

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

**The skimmer draws nothing.** `Skimmer.prefab` (nested by eight vessels) carries a
`ForcefieldCrackleOverlay` whose shader composes `Alpha = fresnel + impact contributions`, and
`ForcefieldCrackleController` pushes an AMBIENT `fresnelRimIntensity` of **0.08** every frame
regardless of impacts — so the overlay is a permanently visible bubble on any vessel carrying
it. On the Manta that bubble is 20-40 units across and drove **nothing**: the crackle is a
skimmer PRISM effect and `MantaStingSkimmerImpactorDataContainer` never listed it (only the
Dolphin's and the Squirrel's containers do), so the rim was the whole of what it drew. The
Manta's nested skimmer instance now overrides `fresnelRimIntensity` to **0**, which zeroes
`Alpha` outright at `_ImpactCount <= 0`.

Chosen over disabling the overlay's `MeshRenderer` for one reason: it is non-destructive. Wire
the crackle effect into the Manta's container tomorrow and impact crackles draw normally; a
disabled renderer would have swallowed them silently, which is the shape of bug the vessel
skill's rule 22 is about. **Open, not fixed here:** Urchin, Grizzly, Falcon, Shrike and Termite
nest the same skimmer with no crackle effect wired either, so all five are still drawing an
ambient rim nothing drives.

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
double-blooms. Scoring needs no extra networking: the bloom's
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
consumes it. **An autopilot Soars too (2026-09, with Redline):** `AIPilot` writes only stick and
throttle, so an AI Manta could never boost. The executor now carries an autopilot drive — both
triggers held at a boost intent that is *how straight the stick is* (full under
`aiBoostStickBand` 0.35, fading linearly to nothing at full deflection), which mirrors the human
trade rather than inventing a policy. Gated on `AIPilot.AutoPilotEnabled`, not on the player
being an AI, so the lava-lamp Manta and a released companion fly the same kit a human does; no
net trigger, so no Yastri for a bot. `REDLINE.md` §5.

**Wake rings — built, then CUT (2026-09).** The remake shipped a Soar wake-ring layer (a
boost ring laid behind the Manta while boosting, threadable for a velocity surge; a "Wake
Highway" Time-5 upgrade let allies ride them). It was removed on design direction — "let's
not do the Soar wake rings; we will refine the kit, but right now we just need a race" — after
the first Bloomrush playtest, where a ring read as an unexplained booster launching the pilot
through the reef. The executor, its config SO/asset, the relay's ring RPCs and the prefab
component are deleted (not disabled); `BoostRingBuilder.LayRing` and the Switch law it used
are untouched platform capabilities. **Time's L5 slot is therefore OPEN** (`UpgradeLabel`
empty on the map — the audit reads 3/4 upgrades); the proposal lives in
`Docs/ElementalAbilitySystem/FLEET_MAPS.md`.

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
rename — and since the 2026-08-26 playtest pass every readout lives INSIDE the ability row
(the readouts' old floating homes read as a second UI, and the fuse panel popping mid-screen
read as messages outside the toast feed):

- `bombChargeFill` (was `fillImage`) is bound as the **Charge card's lockup gauge**
  (`AbilityIconBinding.gauge`), so `AdoptGauge` re-homes and restyles it and the view only
  writes `fillAmount`. The view never writes its colour — the lockup owns gauge styling.
- `armedCountText` (was `overchargePrismCount`) is a **corner badge on the Charge icon** and
  the "can I plant?" answer: highlighted whenever ≥1 bomb is armed.
- `fuseText` (was the overcharge countdown) is the compact **fuse board above the Space
  (Kabloom) card** — `"{planted}x {seconds}s"`, hidden while nothing is planted
  (`fuseContainer` now points at that text's own GO). The controller polls
  `ShortestFuseRemaining`; a number that only ever counts down needs no event channel.

The controller binds the executor by serialized reference on the vessel's own prefab (never
`GetComponentInChildren` of another vessel's type — rule 14), with the symmetric
Rebind/Unbind pair, detach-first, pilot gate after the detach.

The old overcharge cluster (`OuterContainer`/`InnerRing`/`OverchargeText`), a duplicate added
`TrailContainer` carrying a Missing script, and the Manta-local **`ToastHolder` nested
instance are DELETED from Manta.prefab** — the readout re-homes above spared those branches
from the lockup's retire sweep (a referenced branch survives it by design), and a per-vessel
toast surface competes with the game's one dedicated toast feed in the upper right. Messages
go through `GameToastAPI` or not at all.

The four-icon row is authored in Manta.prefab at the fleet-standard wirer bands
(charge → mass → space → time), bound in `AbilityDisplayOrder`, with **generated placeholder
silhouettes** (bomb / turn arrow / bloom / chevrons —
`Tools/Build/author_manta_icon_placeholders.py`, 128 px white-on-transparent at
`_Graphics/Icons/AbilityIcons/Manta/`) so the cards read before the art pass replaces them 1:1.

## 6a. The feel of the loop (the 2026-08-26 juice pass)

The loop was mechanically complete and read as nothing happening: skims paid into an
invisible number, a plant was silent on both ends, fuses burned where nobody could see them,
and a cashed board went off as one flat bang. Every beat now answers, in four channels.

| Beat | What it does |
|---|---|
| **Skim pays charge** | Charge card flashes (`VesselHUDView.PlayAbilityFlash`), gauge climbs, `skimChargeEvent`. The skim-pulse haptic was already wired. |
| **A bomb finishes arming** | Charge card flashes again + `bombArmedEvent`, and the armed badge lights (≥1 bomb = highlight colour). This is the "you may plant" moment. |
| **A bomb is planted** | Charge card flashes, badge decrements, `bombPlantedEvent`, and the **fuse marker blooms in** on the target. |
| **A fuse burns down** | The marker crosses calm → critical and its pulse quickens from `markerCalmPulseHz` to `markerCriticalPulseHz`; the HUD board counts the shortest fuse. |
| **A fuse runs out** | `fuseExpiredEvent` (its own cue — the opposite outcome to a cashed bloom) then the smaller fizzle blast. |
| **Crystal → Kabloom** | Space card flashes, `kabloomEvent`, a `BloomrushKabloom` toast in the dedicated feed, and the board **cascades** rather than detonating at once. |

**The fuse marker** (`MantaBombMarker`) is the instrument that answers "where are my bombs and
how long have I got?". It reuses the Echo Sight halo outright (`Resources/EchoSightHalo`,
`ZTest Always`, billboarded in the vertex shader, constant angular size) so a bomb stays
findable *through* the reef and at any range, at the cost of one shared material and a
per-renderer MaterialPropertyBlock. It is **planter-local for free**: bombs only exist on the
machine simulating their planter, and the extra gate (`LocalHumanPlanter`, snapshotted at
plant time) is only that the planter is the local human — the same predicate the two haptic
feels use, so a host does not see its own bots' markers. **The target still gets nothing**;
the spec's silence is intact.

**The cascade** is the mode's payoff, so it is staggered rather than simultaneous
(`cascadeStaggerSeconds` 0.09, compressed to fit `cascadeMaxSeconds` 1.2 on a big board) and
ordered **nearest-first from the ship**, so the chain reads as a wave rolling outward from the
pilot who set it off. Every committed bomb holds its marker at full critical while it waits —
that is what "watch the fuses turn into explosions" actually looks like. Two ordering rules
make it safe: the whole board is `CommitToCascade`'d on the crystal frame (so a fuse cannot
expire mid-cascade and pay the small blast by accident), and **FusesBeaten is credited on
COMMIT, not on bloom** — the pilot beat those fuses the moment they touched the crystal, and a
round that ends mid-cascade must still pay for them. The third is the corollary of the first
two: a bomb stays in the planted list until it actually blooms, and a cascade outlasts the
Kabloom cooldown by an order of magnitude, so **an already-cascading bomb is excluded from a
second cash-out** — otherwise touching two crystals a beat apart credits the same fuses twice.

**Audio ships SILENT and that is the policy, not an omission.** All six cues are
inspector-exposed `EventReference` fields on `MantaStingConfig.asset` (`skimChargeEvent`,
`bombArmedEvent`, `bombPlantedEvent`, `fuseExpiredEvent`, `kabloomEvent`,
`cascadeBloomEvent`). An empty reference is a clean no-op; a borrowed "temp" event is what the
audio law forbids, because it survives to release disguised as an intentional sound. The audio
owner fills them — one field per beat is exactly so they can be tuned independently.

**No new haptic was added, deliberately.** `Docs/HAPTICS.md` is a locked two-feel policy
(+ one rare alert, + one held-trigger texture) and the Manta already carries both everyday
feels — the skim pulse on its skimmer container and the punish thud on its vessel container.
A buzz per fuse expiry is precisely the "do NOT hang it on anything frequent" case the policy
names, and hanging the rare alert on a bomb would weaken it everywhere it already means
something. If a fuse-expiry haptic is wanted anyway it needs a deliberate decision and a
dedicated `HapticController` method with the gate extended — the doc's stated bar.

## 6b. Jousting a plant plants a bomb

The Manta reaches a living lifeform's heart the same way the Squirrel does — the
`VesselLifeformCrystalEffects` surface on its own impactor container — and does the **opposite
thing** with it: `MantaPlantBombByLifeformJoustEffectSO` plants a bomb on the lifeform and
leaves it alive. Rooted flora sit at `CurrentSpeed` 0, so they are trivially joustable, which
makes the reef itself a board to tag; the same effect covers fauna the hull reaches.

The Manta's container deliberately does **not** carry the Squirrel's
`VesselWitherLifeformByCrystalEffectSO` — a withering joust would destroy the very target the
bomb is riding. That is a per-vessel container question (rule 22), not a platform behaviour,
which is why the two vessels can share one surface and disagree about the outcome.

`MantaBomb`'s carrier is therefore `ILifeFormEntity` rather than `Fauna`: flora and fauna are
separate class hierarchies that meet at that interface, and a bomb does not care what kind of
life it rides. The fauna-only body-prism liveness test is kept for fauna carriers (a creature
that dies takes its bomb with it); a plant that dies destroys the component with its
GameObject, which `OnDestroy` already handles.

## 7. Files

| Role | File |
|---|---|
| Bomb bay config (all Sting/Kabloom tuning) | `R_VesselActions/Data Containers/MantaStingConfigSO.cs` → `_SO_Assets/VesselActions/Manta/MantaStingConfig.asset` |
| Bomb component + snapshot + bloom spawn | `Controller/Vessel/MantaBomb.cs` |
| Fuse marker (planter-local halo) | `Controller/Vessel/MantaBombMarker.cs` |
| Joust-plants on flora/fauna | `EffectsSO/Vessel Crystal Effects/MantaPlantBombByLifeformJoustEffectSO.cs` → `MantaPlantBombByLifeformJoustEffect.asset` |
| Bay executor (charge, plant, detonate, registry) | `R_VesselActions/Executors/MantaStingActionExecutor.cs` |
| Bloom relay (NetworkBehaviour, Manta root) | `Controller/Vessel/MantaBombNetworkRelay.cs` |
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
| `cascadeStaggerSeconds` / `cascadeMaxSeconds` | 0.09 / 1.2 | the chain-reaction beat; 0 = simultaneous |
| `markerRadius` / `markerCalmColor` / `markerCriticalColor` | 14 / cyan / hot orange | the fuse marker's look |
| `markerCriticalSeconds` | 6 | when "cash in NOW" becomes the read |
| `markerCalmPulseHz` / `markerCriticalPulseHz` | 0.9 / 5 | the quickening that IS the fuse state |
| six `*Event` FMOD slots | EMPTY | one per beat, for the audio owner |

## 9. In-editor verification (not yet run — no Unity CLI in the authoring session)

1. Open Manta.prefab: `MantaStingActionExecutor` on the actions object with its config wired; `MantaBombNetworkRelay` on the root; HUD view's four icons bound;
   `trailVolume` enabled (Mass 1→2.5); no Missing (Mono Script) rows.
2. Play Freestyle as Manta: skim the cell's mass — bay gauge fills, a full unit ticks the armed
   count. Graze a creature — armed count drops by one, fuse board shows the countdown. Touch a
   crystal — every planted bomb blooms, the flower + domained blast fire at the ship, the spent
   crystal plays its payoff (the old Manta-only mute on `OmniCrystalImpactor` is removed).
3. Let a fuse expire — visibly smaller bloom.
4. Ram a bombed AI through dense mass — the bomb scrapes off (no bloom).
5. Two-client party: plant on the remote pilot — NO indication on their screen; the bloom
   appears on both machines once (no double blast on the owner).
6. Mass 5: hold a hard Yastri turn — outer-lane prisms visibly flared AND shielded.
7. FrogletTools > Vessels > Audit Vessel Ability Rows / Audit Ability Lockups / Audit Vessel
   Skimmers — Manta rows green (skimmer audit: the sting container holds prism + vessel
   effects); the ability-row audit reads 3/4 upgrades (Time L5 open, by design).

## 10. Follow-ups

- **Icon art**: the four ability icons ship with GENERATED placeholder silhouettes
  (`author_manta_icon_placeholders.py`). The art polish pass owns the real sprites per the
  spec's ownership note — replace the four PNGs 1:1 (same guids) or rewire the icon Images.
- **Time L5 is open** — Wake Highway was cut (§5); the next proposal goes through FLEET_MAPS.
- **Pre-existing dangling refs** on Manta.prefab's `ElementalBarsController` (config/view) —
  present at the branch base, not introduced here; the view falls back to Resources.
- **Prismatic Relay** (the spec's second minigame sketch) is not built; Bloomrush is minigame 1.
- **`SO_Class_Manta` description/abilities meta-layer** (hangar copy) still describes the old
  kit — text-only, no gameplay read.
- **`AOEFlowerCreation` in the Kabloom self-blast** needs an eyes-on check that the flower
  reads at the shipped scale beside the domained blast.
