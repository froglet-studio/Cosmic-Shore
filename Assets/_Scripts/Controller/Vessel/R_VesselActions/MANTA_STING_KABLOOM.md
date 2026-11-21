# Manta — Sting bombs, Kabloom, Soar, Yawstery turn trails

The Manta's spec remake (2026-08, approved design: "Destroyer → Nuke"). The vessel's whole kit
is **buttonless where it matters**: you arm bombs by flying (skimming), you plant them by flying
(grazing another vessel or a creature), and you detonate them by flying (touching a crystal).
The two inputs the vessel does hold are movement — Soar (dual-trigger analog boost) and Yawstery
(hard flat turns). This is the record of how the four abilities work, what the remake threw
away, and how to verify it in the editor.

The map is `Assets/Resources/ElementalAbilityMaps/Manta.asset` — **the asset is the record**
(authored by `Tools/Build/author_manta_kit_assets.py`, `--check` is the drift gate):

| Element | Ability | Quantitative (authoring home) | L5 upgrade |
|---|---|---|---|
| Charge | **Sting** (passive) | how long the shared fuse runs when a plant resets it: 20 s at rest → 30 s at Charge 10 (`fuseSeconds` × `fuseScale` 1 → 1.5, on `MantaStingConfig.asset`; §0a) | **Contagion** — anything caught in a bloom is itself bombed, free |
| Mass | **Decoy** (passive) | how long a decoy stands (`decoyLifetimeAtFullMass` 2×) | **Omni Decoy** — the omni crystal leaves a decoy too |
| Space | **Skim** (passive) | the skimmer's size (Manta.prefab `Skimmer.Scale`, 20 → 40) | **Shield Breaker** — skimming a shielded rival prism pops its shield and makes it explosive |
| Time | **Yawstery** (Input 12, the turn pair) | the Manta's speed: cruise (`VesselTransformer.ThrottleScalerMultiplier` 1.5× at Time 10) and soaring (`BoostSpeedMultiplier` 1.3×); the trail is a plain Squirrel-style trail, unscaled and unflared | **Soar** — the boost stays dark until Time 5 |

## 0. The 2026-11-07 overhaul (Garrett) — the rules as they stand

This section is the current design. The sections below it are the earlier record; where one
disagrees with this section, this section wins.

- **Energy (Space card).** Skimming pays `energyPerSkim` per prism (per-prism cooldown), up to
  `energyCapacity` (1: the loop is skim, plant, skim, plant; planted bombs have no cap). One energy plants one bomb. **Any hull contact with a prism sets off
  every planted bomb and loses all the energy** (`MantaDetonateByPrismEffectSO` in
  `vesselPrismEffects` → `MantaStingActionExecutor.OnPrismImpact`).
- **One fuse (Charge card).** Every bomb shares the bay's fuse (`FuseDeadline`); each new plant
  resets it, so all bombs always go off together. Charge sets how long the fuse runs when a plant
  resets it (§0a); the petals a bloom knocks out of a rival are flat. A bomb moved to a new carrier (a fauna heart
  crystal, a vessel that picked it up) keeps the running fuse. A fuse or prism-impact board blooms
  every bomb at once at `fuseBlastScale`, growing over `bloomGrowSeconds` (slow on purpose: many
  blooms at once read well and cost less per frame).
- **Bombs cannot be scraped off.** The old knock-off and own-fresh-trail graces are gone.
- **Omni crystal = cash out.** Every bomb goes off, nearest first, at `kabloomBlastScale ×
  OmniScaleMultiplier(count)` (`1 + omniScalePerExtraBomb × (n − 1)`, capped at
  `omniScaleMultiplierMax`), growing over `omniBloomGrowSeconds`. Press your luck: one more bomb
  is a bigger payout and one more chance to brush a prism and lose it.
- **Blooms never hurt the planter's side.** No Friendly Fire was the old Space upgrade; it is now
  the base rule (contagion skips the planter's domain too).
- **A bombed fauna's crystal stays bombed.** When the creature dies the bomb moves onto its heart
  crystal; whoever collects that crystal (any vessel, the Manta's own side included, which blooms
  never hurt) carries the bomb away on the same fuse.
- **Yawstery and Soar are Time.** Shielded Turn Trails is dropped. The trail is toned down to a
  Squirrel-style trail (2026-11-07 follow-up): the Squirrel's prism size (20 × 0.75 × 5.5), no
  element growing its volume, no turn flare. Time makes the Manta faster: cruise speed 1.5× at
  Time 10 (`ThrottleScalerMultiplier`, the Scarab's shape), on top of the soaring speed.
- **The turn is called Yawstery** (Caleb, 2026-11-11). It was "Yastri"; the name changed everywhere
  (the ability map's `AbilityLabel`, the HUD fields, the icon sprites, the AI's `YawsteryStickBand`, docs and tools).
  A name only: what the ability does is unchanged. Renamed serialized fields carry `[FormerlySerializedAs]`.

## 0b. The trail is shaped by speed, never by what is near it (Caleb, 2026-11-11)

Caleb's pick in the Manta Trail Lab (https://claude.ai/artifact/UcrAbovUrX7NPCx1mLoB9k, "Free curves";
`Docs/Manta/POLISH.md`, round "speed-shaped trail prisms"). `ScoutTrailPrismScaler`, which sized the trail's width
from a probe of nearby prisms, is off the Manta. `VesselPrismController.speedShape` (`TrailSpeedShape.cs`) shapes
every two-rail prism instead:
- **X** is one rail's width, 8 m at every speed.
- **Z** keeps 5.5 m up to 162 u/s and grows smoothly to 10 m at Soar (600 u/s). It never passes 0.9 of the gap to
  the next prism on its rail; at Soar that gap is speed × frame time, because the lay is one prism per frame.
- **Y** is 0.75 m at cruise, up to 4× below 60 u/s, and +80 % at a full Yawstery turn.
- **In a turn** the inside rail shortens and the outside lengthens by the ratio of each rail's arc to the vessel's,
  and the outside loses up to 25 % of its height. Y is held between 0.1 and 3.5 m.

`TrailSpeedShapeTests` pins the lab's numbers and the prefab. The rest of §0's Squirrel-size note still holds for
the other hulls: the shape is off unless a prism controller authors it.

## 0a. Charge is the fuse, not the petals (Garrett, 2026-11-10)

*"I want to move away from charge being coupled to elemental petals. It is too coarse to adjust. The manta charge
parameter should be the length of time on the fuse when it resets."*

- **The fuse.** Each plant resets the shared fuse to `fuseSeconds` × `fuseScale`, read at the planter's live Charge at
  that moment (`MantaStingConfigSO.FuseSecondsFor`, called by `MantaStingActionExecutor.ResetFuse`). `fuseScale` is an
  `ElementalFloat` on Charge: ×1 at rest, ×1.5 at Charge 10, floored at ×0.5. So 20 s at rest, 25 s at Charge 5, 30 s
  at Charge 10, 15 s at Charge −5 and 35 s at Charge 15, and every tenth of a level in between moves it. A plant at a
  higher Charge buys the whole board the longer fuse; a bomb that only moves (a heart crystal, a pickup) keeps the
  running fuse, as before.
- **Why the continuous level.** Only the machine that simulates the bombs runs the fuse (§4), so nothing else has to
  agree with it, and `EvaluateLive` reads the level between petals. That is the whole point of the change.
- **The bloom's bite is flat again.** `magnitudeAtFullCharge` on `MantaBombDebuffByExplosionEffect.asset` is back to 1:
  every bloom ejects the priced 1.2 petals (0.12, the drain table's Debuff class) from each of Mass and Space, at any
  Charge. From 2026-11-07 to 2026-11-10 Charge doubled it (2.4 at Charge 10).
- **Bloomrush.** Its per-intensity ladder (`MantaBombRules.FuseSecondsOverride`, 30/25/20/20) replaces the RESTING
  length only; Charge still scales it, so an intensity-1 Charge 10 Manta runs a 45 s fuse. Whether a mode may set the
  fuse at all is still the open owners' call in `Docs/REFACTOR_BACKLOG.md` C3 (the LOCKED rule that a mode never
  changes the vessel postdates the ladder).
- **Tune it** on `MantaStingConfig.asset` through `Tools/Build/author_manta_kit_assets.py` (it writes the whole file;
  `--check` is the drift gate and now also fails on a file that does not parse). Tests: `MantaFuseChargeTests`.

## 3a. Explosive prisms

Skimming an opposing-domain prism (never neutral Blue) marks it **explosive**
(`Prism.MakeExplosive`, a colour lift is the tell; danger-suppressing silence blocks it). An
explosive prism's death (`Prism.SetupDestruction`, the one funnel) fires `PrismExplosiveChain`:
after a short beat it destroys its prismscape neighbours (the same trail when it has one, a
`PrismSpatialIndex` sphere of 1.6 prism extents otherwise), and any of those that were explosive
fire their own beat, so chains roll outward. The work is spread at most 24 hits per frame. A
shielded prism needs the Space 5 upgrade, which pops the shield and primes it.

## 4a. Decoy crystals (the Mass ability)

`MantaDecoyByCrystalEffectSO` sits in all four elemental crystal lists and in the omni list of
`MantaImpactorDataContainer`. A collected elemental crystal leaves an elemental decoy; at Mass 5 a
collected omni crystal leaves an omni decoy too. The bay keeps one of each and pops the older one.

`MantaDecoyCrystal` copies the real prefab under an inactive holder, paints it, and strips every
gameplay part (Crystal, impactors, colliders, rigidbodies, network components), so it can never be
collected, scored or targeted. **The tell:** one effect driver is frozen (an elemental decoy its
own element; an omni decoy one of four, picked by a seed every peer shares). A rival vessel or
skimmer that touches it springs it into `decoyMorphPrefabs` (the danger-block hemisphere) laid
along the rival's heading. Decoys spawn on every peer; the morph's prism layout is each peer's own.

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

**The fuse.** `fuseSeconds` (20 s at rest, × Charge's `fuseScale` since 2026-11-10, §0a; Bloomrush overrides the
resting length per intensity through the static `MantaBombRules.FuseSecondsOverride`, reset on domain reload). A fuse expiry detonates at
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

## 5. Soar + Yawstery — the two held inputs

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
net trigger, so no Yawstery for a bot. `REDLINE.md` §5.

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

**Yawstery** (`YawsteryActionSO` / `YawsteryActionExecutor`, Input 12): the hard flat turn. The
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

### On a phone (2026-10-21)

Garrett, 2026-10-21: *"the manta can fly ultra fast when you lift both thumbs and then steer with the gyro."*

- **Lift both thumbs = Soar.** `Manta.prefab` `_touchActionOverrides`: `IdleAction (4)` → `BoostAction`
  (`8346bd7c…`, the asset the shared `BothSticksAction` entry runs). `TouchInputStrategy` presses `IdleAction` once
  when the last thumb leaves the glass (only after the pilot has touched at least once, so a Manta never soars off the
  start line) and releases it when a thumb lands. `BoostActionSO.StartAction` sets `IsBoosting` (+ the boost SFX),
  `StopAction` clears it.
- **Why `BoostAction` and not `MantaAnalogTurnBoostAction`.** The pad's Soar is `MantaAnalogTurnBoostExecutor`, and
  `MantaAnalogTurnBoostAction` is a deliberate NO-OP SO: the executor reads the analog triggers per frame and returns
  early on any device but Gamepad/Keyboard, so on a phone binding it does nothing. Top speed is the same either way:
  both triggers flat on a pad set `BoostMultiplier` to the executor's base (`boostMultiplierOverride` 0 → the hull's
  `boostMultiplier` 4), and `BoostAction` boosts at that same multiplier (`decayBoost` is off). With no thumbs the
  Manta's throttle reads FULL (`TouchControlProfile.idleFullThrottle`; every other hull reads a released pad's
  neutral 0.5), so the phone Soar is 1 × 180 × 4 = **720 u/s** - "ultra fast", the pad's flat-out Soar with the sticks
  spread - against 90 u/s at neutral thumbs. Time's boost multiplier applies on both.
- **Steer with the gyro.** With no thumb down, phone tilt is the stick (`TouchControlProfile.tiltSteersWhileIdle`):
  wheel-turn yaws, tipping forward/back pitches.
- **The lifts are unchanged**: one thumb lifted is still Yawstery (`OnlyLeft/OnlyRightStickAction` → the shared
  `YawsteryAction-Left/Right`). Lifting the second thumb releases the Yawstery, then presses the Soar.
- An autopilot Manta Soars through `MantaAnalogTurnBoostExecutor`'s autopilot drive (checked before the device gate,
  so on any device), never through a press, so it is unaffected by this binding.

## 6. HUD

`MantaHUDController` / `MantaVesselHUDView` were rewritten around the bomb bay. The old
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

**The 2026-11-07 icon pass** replaced the placeholders with kit parts (`Tools/Build/icon_kit/specs/manta.py`,
`Docs/Icons/ABILITY_ICON_SKILL_PLAN.md` §8): Sting draws a rival wearing a bomb per plant on the shared-fuse plate, Decoy
the decoy crystal and its trap, Skim the explosive prisms over the energy plate (`energyFill` is now the Space card's
gauge), Yawstery the hard turn with Soar's chevrons at Time 5; the omni card is the cash-out. The view tints the parts
(the Mantas in the domain colour, rivals and the trap in danger) and shows them from the executor's state.

## 6a. The feel of the loop (the 2026-08-26 juice pass)

The loop was mechanically complete and read as nothing happening: skims paid into an
invisible number, a plant was silent on both ends, fuses burned where nobody could see them,
and a cashed board went off as one flat bang. Every beat now answers, in four channels.

| Beat | What it does |
|---|---|
| **Skim pays charge** | Charge card flashes (`VesselHUDView.PlayAbilityFlash`), gauge climbs, `skimChargeEvent`. **No haptic** (Caleb, 2026-11-11): the per-entry skim pulse held the pad at a steady hum in dense prisms, so skimming is told by sound and the HUD; the Manta's pad speaks only on a hull hit (punish thud) and a plant (`HapticController.PlayPlant`, `Docs/HAPTICS.md`). |
| **A bomb finishes arming** | Charge card flashes again + `bombArmedEvent`, and the armed badge lights (≥1 bomb = highlight colour). This is the "you may plant" moment. |
| **A bomb is planted** | Charge card flashes, badge decrements, `bombPlantedEvent`, the **fuse marker blooms in** on the target, and the pad gives the **plant knock** (two quick knocks, local human pilot). |
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
| `fuseSeconds` / `fuseScale` | 20 / ×1 → ×1.5 (floor ×0.5) | the fuse a plant resets: 20 s at rest, 30 s at Charge 10 (§0a); the resting length is mode-overridable via `MantaBombRules.FuseSecondsOverride` |
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
6. Mass 5: hold a hard Yawstery turn — outer-lane prisms visibly flared AND shielded.
7. FrogletTools > Vessels > Audits > Audit Vessel Ability Rows / Audit Ability Lockups / Audit Vessel
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
