# Impact Effects & Audio (FMOD)

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

### Impact Effects Architecture

The collision/impact system (`Assets/_Scripts/Controller/ImpactEffects/`) uses a matrix of impactors and effect SOs:

**Impactor types** (all extend `ImpactorBase`): `VesselImpactor`, `NetworkVesselImpactor`, `PrismImpactor`, `ProjectileImpactor`, `SkimmerImpactor`, `MineImpactor`, `ExplosionImpactor`, `CrystalImpactor`, `ElementalCrystalImpactor`, `OmniCrystalImpactor`, `TeamCrystalImpactor`

**Effect SO pattern**: `[Impactor][Target]EffectSO` — e.g., `VesselExplosionByCrystalEffectSO`, `SkimmerAlignPrismEffectSO`, `SparrowDebuffByRhinoDangerPrismEffectSO`. Per-vessel effect asset instances exist for each vessel class. Organized into subdirectories: `Vessel Crystal Effects/`, `Vessel Prism Effects/`, `Vessel Explosion Effects/`, `Vessel Projectile Effects/`, `Vessel Skimmer Effects/`, `Skimmer Prism Effects/`, `Projectile Crystal Effects/`, `Projectile Prism Effects/`, `Projectile Mine Effects/`, `Projectile End Effects/`.

Key interfaces: `IImpactor` / `IImpactCollider`

**An EMPTY slot in a serialized effect array names itself — dispatch it through `ImpactorBase.IsEffectSlotEmpty`.** `DoesEffectExist` only gates on length, so a hole *inside* the list reached `effect.Execute(...)` and threw a bare `NullReferenceException` at the call site, naming neither the container nor the index. That is survivable in a PhysX callback and is not once the **shell tier** owns the pair: `PrismShellContactManager` dispatches from `Update`, so one bad slot threw **once per frame** for the life of the contact, and each throw aborted the rest of that frame's shell contacts *and* skipped `SweepStalePairs`. `IsEffectSlotEmpty` reports container + field + index **once** (`CSDebug.LogError`, keyed so it can't spam) and returns true so the caller skips that slot and the sibling effects still run — the missing effect cannot be invented, but nothing else in the chain needs to die with it. Wired through every dispatch loop in `VesselImpactor` and `SkimmerImpactor`, the two impactors registered as shell probe owners (`RegisterProbeOwner`) and therefore the two whose dispatch left the callback and became a per-frame path. Route any new effect-dispatch loop through it** rather than dereferencing the slot directly. This is not a fail-soft exception to the fail-loud policy: it fails loud *once, with the offender's address*, instead of anonymously forever.

**Its companion is `ImpactorBase.RunEffectIsolated` — for a slot that is FILLED but THROWS.** An exception inside an effect's `Execute` is reported once per (effect, impactor type) with its stack, and the rest of the contact's list still runs. Same doctrine, opposite failure: a hole vs. a thrower. The Urchin forced it — its spike container is `[Embed, Steal, ChainFire]` in a load-bearing order, so one throwing effect silently killed both the steal and the cascade while the embed had already visibly landed, and the weapon read as dead with nothing in the console. Wired into `ProjectileImpactor`; `VesselImpactor` and `SkimmerImpactor` still dispatch bare and are the open item. Route a new dispatch loop through BOTH helpers.

**ONE BLAST PAYS A VICTIM ONCE, and the per-blast ledger is what enforces it.** `VesselCombatHitLatch` dedupes by `(shooter, victim, class)` over a window sized for the gap between two separate SHOTS — 0.5 s on all three missile reporters — while a blast is a trigger that keeps GROWING for its whole life (`AOEExplosion.ExplosionDuration` 3 s). So a pilot swept up by a detonation, thrown clear, and turning back into it re-enters the SAME explosion, raises `OnTriggerEnter` again past the latch, and used to be paid and drained a second time for one shot. `ExplosionImpactor._vesselsHit` — already there as the tally behind the Dolphin's `BlastTally` — now **gates** the vessel-effect dispatch as well as counting it. The three missile TIERS are a different question and are a separate blast instance each: the latch folds them onto one key and pays only the closest (`CombatHitScoring.Credit` subtracts the superseded tier's price; `CombatHitDrain.Apply` nets the drain the same way), so a rocket whose shockwave, blast and direct hit all reach one pilot pays **30 and 3 petals**, never 60 and 6. General rule: **a dedupe window sized for the gap between two EVENTS cannot dedupe one event that outlives the window** — ask how long the thing being deduped lives, and prefer a per-instance ledger that GATES over one that only counts. `SPARROW_SKYBURST_BAY.md`.

**A blast with `affectsPrisms` OFF reaches prisms only through `ExplosionImpactor.SweepPrismEffects`.** Such a blast never starts the Burst prism pass and its trigger declines prisms, so before 2026-10 its container's `explosionPrismEffects` could never run. The sweep (spherical frames only) queries `PrismSpatialIndex.QuerySphere` over each frame's wavefront, hands each prism ONCE to the container's `explosionPrismEffects` and to every `IExplosionPrismPayload` component on the blast prefab, at most 48 per frame, draining the rest after the visual with a `TimeCreated` identity check. A blast that DOES affect prisms is not swept (the batch pass already decided its mass), and the Burst batch path still never runs `explosionPrismEffects`. First user: the Butterfly's omni-crystal bloom (`ButterflyBloomDust`, `R_VesselActions/BUTTERFLY.md §3.3a`), which applies the Dust-mode capsule's own `SkimmerScaleDustPrismEffectSO.Apply`. `ExplosionImpactor.PrismEffectsReached` / `PrismEffectsDispatched` separate "found nothing" from "found prisms, ran nothing".

**A vessel and its own skimmer never impact each other.** `SkimmerImpactor` and `VesselImpactor` carry mirrored self-guards on their vessel<->skimmer dispatch — required because the Rhino's sword capsule permanently overlaps its own hull, which otherwise ran the full victim-effect chain against the pilot (when that was written, muting their own `RightStickAction` via `VesselDamageBySkimmerEffect`, since removed with the control-theft tier; impact-SFX spam, still). The guard's reason is the OVERLAP, so it outlives whichever effects the container happens to carry. Skimmer-vs-own-PRISM handling is separate and stays flag-controlled (`Skimmer.AffectSelf`). See `_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md`.

**A pilot does not interact with their own trail while they are MAKING it** — `SelfTrailContactConfigSO` (`Resources/SelfTrailContactConfig`), asked by both `VesselImpactor` and `SkimmerImpactor` at the top of their prism branch. A trail prism is laid a fixed offset behind the vessel and the spawner assumes the vessel then leaves it; a **drift** slides the hull sideways across the ribbon it is extruding, **MASS scaling** stretches the prism further back than the clearance delay was sized for, and a **skimmer sphere** (15–30 u on the Squirrel) outlasts the hull by a long way. So a Squirrel fed itself skim energy off the ribbon it was laying, a Dolphin *rammed* its own fresh trail and lost **half its banked skim energy and half its charged boost** (`VesselChangeResourceByPrismEffectSO` / `VesselChangeBoostByPrismEffectSO`, neither of which carries a self-guard). **The gate is OWNER-scoped and TIME-boxed, deliberately not domain-scoped**: `Skimmer.AffectSelf` compares DOMAINS (so switching it off also blinds a vessel to its teammates' trails) and is evaluated AFTER the skimmer effect loop, where it gates only the skim bookkeeping — it changes nothing for effects. The test is `prism.ownerID == vessel.PlayerName` within the grace since `prismProperties.TimeCreated`, using `ownerID` (which records who LAID it and survives a steal) rather than `PlayerName`, and excluding `IsEnvironmentOwned` mass outright. Consequently **another player's trail — and a teammate's — is skimmable from the frame it appears**, so a trailing Squirrel still farms an opposing ribbon all the way into joust range, and a pilot's own older trail is ordinary mass again. Both guards sit ABOVE the shell-ownership check so the Squirrel's MASS-5 shielded drift armour is covered on the analytic tier too. Nothing is culled, decayed, or hidden — the mass is live for the whole world from the frame it is laid; one vessel declines to act on it, so conserved mass is intact. Its companion fix: `VesselPrismController.CreateBlock`'s `waitTillOutsideSkimmer` delay measured `TrailZScale` (= `BaseScale.z`), which omits BOTH `ZScaler` and the MASS volume multiplier applied a few lines above it, so an upgraded vessel's collider came on while the prism was still inside the ship — it now measures the length actually being laid (`scale.z`), which is identical for un-upgraded vessels and only ever lengthens. That delay hides the prism from EVERYONE, which is exactly why it can never be the lever for an owner-scoped rule. Full record: `_Scripts/Controller/ImpactEffects/SELF_TRAIL_CONTACT.md`.

**A skimmer only skims if `VesselStatus` points AT it — and the failure is silent.**
`VesselController.Initialize` initializes **only** `VesselStatus.NearFieldSkimmer` /
`FarFieldSkimmer`, and `SkimmerImpactor` drops every contact while `skimmer.IsInitialized` is
false. So a vessel can carry a perfectly wired skimmer — trigger sphere, kinematic rigidbody,
`ImpactCollider`, effect container, layer 7 — and skim **nothing at all**, with no error
anywhere, because the reference points at a different (or disabled) skimmer object. The Dolphin
shipped that way for its whole life: an active `EnergySkimmer` doing the physics and a disabled
legacy nested `Skimmer.prefab` holding the reference. **Audit it, don't infer it from feel:**
`FrogletTools > Vessels > Audit Vessel Skimmers` checks assignment, active state up the whole
ancestor chain, the components the trigger path needs, and whether the container holds any
prism effects — asset-only, no play mode. *(Serpent currently fails it.)* Note that a skim's
feedback signals are each individually invisible — the haptic is a **no-op on desktop**,
the legacy beam VFX (`SkimmerFXPrismEffectSO`, `[Obsolete]`) is per-vessel wiring that only
draws when the container asks for it AND the skimmed prism authors a `ParticleEffect`, and a
gauge that moves a tenth of its range per skim reads as nothing — so "I feel no skimming" is
not evidence about the wiring in either direction. **The crackle is meant to be a vessel's ONLY
skim visual**: the beam is the effect it replaced, so a container holding both draws a beam to
every prism in the sphere *on top of* the crackle. The Dolphin ran both for three hours of
branch history and now wires the crackle alone; the Squirrel's beam was retired too (2026-10-06,
owner's call), so no live container holds the beam. The forcefield crackle needs **three** pieces to be
present or `SkimmerForcefieldCracklePrismEffectSO.Execute` returns silently: the effect in the
container, a `ForcefieldCrackleController` on the impactor's own GameObject, and an overlay
`MeshRenderer` assigned to it (vessels whose skimmer IS `Skimmer.prefab` get the last two free;
standalone skimmer objects do not). Detail:
`_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md` §5.

**Danger prisms are not safe to their own domain (locked design).** `IsDangerous` effects apply to every vessel that touches the prism, regardless of domain — friendly fire included (the fire-trail action literally sets `IsDangerous` from a `FriendlyFire` flag). Danger-prism effect SOs must not gate on domain. **Danger is mutually exclusive with BOTH shield tiers**: `PrismStateManager.MakeDangerous` clears `IsShielded` AND `IsSuperShielded` (and disengages the shield visuals), just as `ActivateSuperShield` clears `IsDangerous` — a danger prism carrying a stale super-shield flag is invulnerable and kills any AOE explosion that touches it. `Prism.ResetState` also clears `IsSuperShielded` on pool reuse (no spawner requests super-shield pre-`Initialize`; it is always engaged post-spawn). This is what makes danger trails a risk/reward surface: a danger trail grants 10x skim energy (`SkimmerBoostPrismEffect.dangerEnergyMultiplier`, gated behind the skimming vessel's Charge level-5 "Live Wire" upgrade — below it danger skims pay base energy) but slams its owner on contact — volume-independent full-stop slow at the danger max (`VesselChangeSpeedByPrismEffectSO`: `maxSlowStrength * dangerSlowMultiplier`), all-element elemental hit (`VesselElementalDebuffByDangerPrismEffectSO`), and boost reset. **That elemental hit is now THE ECONOMY'S ONLY SINK, and the locked no-domain-gate rule is intact because it is not a gate**: both branches run for every vessel that touches the prism, and the prism's domain only chooses HOW the loss lands — an **opposing-domain** danger prism (someone else's trap, a creature's rods, neutral `Domains.Blue` environment mass) **BURNS** the petals permanently, while **your own** danger trail keeps the 4s decaying debuff it always had. It falls that way round because burning is an act of the WORLD against a pilot and a pilot's own trail is not the world — and because a self-inflicted permanent burn would turn Live Wire into a trap that destroys the levels it pays out. Note this makes the Dolphin's Drift Ward a ward against the SINK, and note the emergent asymmetry flagged for playtest in `Docs/ELEMENTAL_ECONOMY.md` §4: fauna spawn in the cell's CONTROLLING colour, so a creature's danger rods burn the pilots who do not hold that cell and merely sting the ones who do. (The Sparrow's own overheat danger trail was retired with its overheat mechanic; the `EnableDangerMode` machinery survives for a future caller. The one thing that can deny a danger prism's bite is the general **elemental-debuff immunity** state — `ResourceSystem.IsImmuneTo(ElementalDebuffSources.DangerPrism)`, held by the Sparrow while boosting at Time 5, by the Serpent while stopped, and by the Dolphin while drifting at Time 5 — and it denies ONLY the elemental drain: the slow, the input mute and the boost reset still land. It is a gate inside `ApplyElementalEffect`, not a domain exception, so the locked law is intact. A ward is held against a MASK of debuff SOURCE CLASSES, not as a bare bool: the Sparrow's and Serpent's cover everything, while the **Dolphin's covers `DangerPrism` alone** — it is a ward against the ARENA, so an opposing pilot's blast still debuffs a drifting Dolphin (which is what keeps The Bends scoreable; see §"The Bends" and `SPARROW_AFTERBURNER.md` §1.1).)
**The slow half of that punishment is PER-VESSEL WIRING, not a platform given** — it only happens
if the vessel's `VesselImpactorDataContainerSO.vesselPrismEffects` actually contains a
`VesselChangeSpeedByPrismEffectSO`, and for most of the fleet's life most vessels did not. The
Dolphin had an authored `DolphinVesselChangeSpeedByPrism` asset referenced by **no** container, and
the Sparrow — the only vessel Dog Fight flies — had neither asset nor entry, so neither took a speed
penalty from any prism, danger ribs included. Three shipped docs asserted the slow anyway
(`DOLPHIN_ENERGY_ECONOMY.md`'s drift-hold clause, `SPARROW_AFTERBURNER.md`'s ward step, and
`DOGFIGHT.md`'s danger-rib paragraph) because a vessel that simply never slows reads as a vessel
that is fast, and because a correctly-designed passthrough — the immunity gate really does leave
`ModifyThrottle` alone — looks verified even when nothing is being pushed through it. **Wiring
status is therefore a thing to CHECK, never to assume**: Squirrel / Dolphin / Sparrow / Manta carry
it and are pinned to one shared tuning (`speedModifierDuration 1`, `massScaling 0.1`,
`maxSlowStrength 0.5`, `dangerSlowMultiplier 3`, `dangerSlowDurationMultiplier 3` — a prism should
read the same whichever hull hits it, so moving one asset off these numbers un-shares the fleet's
collision read); **Rhino and Serpent still have no speed effect at all** and are the open item.
Note also that a *name* is not evidence of a slow: `SparrowDebuffByRhinoDangerPrismEffectSO` carries
a `vesselSlowedByRhinoDangerPrismEvent` field and a "Slow Viewer Integration" header, and only ever
muted an input.

**A prism's DEATH VISUAL wears the palette of the TIER it was wearing, never just its domain.** The dying prism's `PrismKind` rides `PrismEventData.Kind` — stamped by `Prism.Explode`/`Implode` from `PrismKinds.Of` *before* the destruction pass — and the batched debris path (Grow still uses `ConfigureForTeam`) tints from `SO_ColorSet.GetPrismKindColors`, the ONE composition `ThemeManager` also paints the live block materials with (`ThemeManager.PaintPrismTier`). Before this, debris was tinted from the domain alone at the PLAIN tier, so a danger prism — a frosty shielded base under the hot domain-independent danger rim — shattered into ordinary domain-coloured debris and read as a plain prism dying; shielded/super-shielded mass had the same defect on a devastating hit. **Never re-inline a tier's colour pair** at either consumer, and never fix a debris colour on the per-domain `SO_MaterialSet.ExplodingBlockMaterial` copies — nothing draws with those (`PrismDebris` reads mesh+material off the pool prefab and overrides colour PER ENTITY, which is also why a mixed-tier burst is still ONE batch and one draw: the tier must never become a reason to split a batch or swap a material). Danger alone also detonates HARDER — `PrismExplosion.DetonationGain`, authored as `dangerDetonationMultiplier` on `PrismExplosion.prefab` (1.6, set 1 for palette-only) — and that gain scales debris speed, shatter rate and the clamp band as ONE quantity, per the AOE-impulse contract above. Detail: `Docs/PALETTE.md §2.1`, `Docs/PRISM_ANIMATION.md §4.6`.

**AOE blast impulse — `Inertia` only reaches the screen with a ceiling of its own.** Every
explosion entry point (`ExplosionImpactor.ProcessBatchFrame` / `ProcessBatchConeFrame` /
`DrainPendingBatchFrame` → `PrismSpatialIndex.ProcessExplosionFrame` / `ProcessExplosionConeFrame`
/ `DrainPendingExplosionDamage`) takes ONE `ExplosionImpulse`
(`_Scripts/Controller/Projectiles/ExplosionImpulse.cs`) instead of a loose `(speed, inertia)` pair,
because debris speed is `min(Speed * Inertia, ceiling)` and the ceiling is the third number that
cannot travel separately. With no ceiling of its own a blast falls back to
`PrismExplosion.prefab`'s authored `maxSpeed` (**33.33 u/s**) — a guard sized for the legacy
`impactVector / volume` gain, not a physical bound — and **every** AOE magnitude sits far above it
(the Dolphin cone's wavefront is `height / (duration * 4)` ≈ 222 u/s, 6.7x over), so every blast
saturates to the same speed and `Inertia` is dead tuning. `AOEExplosion.proportionalDebris` opts a
blast onto the true-velocity contract `PrismEffectHelper.DamageProportional` already defines: the
vector IS the debris velocity (`speed * debrisRestitution * Inertia`) and the blast passes a matching
ceiling. Off by default; **on** for `AOEConicExplosion.prefab` (the Dolphin crystal blast) at
`debrisRestitution 1/3 x Inertia 1.8 = 0.6`, and for `AOEScarabCavitation.prefab` (the Scarab's
swept-plate dash blast, `AOECylindricalExplosion`) at `1/3 x 3 = 1.0` — the product is deliberately
**1** there because that blast's whole read is "the wall goes the way you dashed": at 1.0 the
debris velocity IS the plate's sweep velocity, so a prism leaves at exactly the speed the blast
crossed it. That is the third AOE shape (sphere / cone / swept cylinder), and a new shape must
carry the ceiling through BOTH prism paths and supply a matching narrowphase — a squat cylinder's
bounding sphere reaches ~43u BEHIND the pilot, which is harmless for prisms (they are tested
exactly) and NOT harmless for the crystal sweep, which SPENDS what it touches. Debris speed and **shatter rate are one number** on this
contract (`PrismExplosion.TriggerExplosion` re-reads `Speed` off the clamped velocity when an
override is supplied — otherwise raising the ceiling finishes the shatter in a frame while the debris
crawls), so `Inertia` scales both together; do not split them. Both prism paths carry the ceiling —
the Burst resolve and the Physics-trigger fallback (`ExecuteCommonPrismCommands`) — so a blast throws
mass at the same speed with or without the spatial index. Detail: `Docs/SPATIAL_INDEX.md` § "Impulse".

**Forcefield Crackle (Skimmer)**: `SkimmerForcefieldCracklePrismEffectSO` (at `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/`) is a shader-driven alternative to `SkimmerFXPrismEffectSO` that visualizes the Skimmer's invisible sphere collider on prism impacts. It computes the impact point via `Collider.ClosestPoint` between the prism box and skimmer sphere, projects it onto the sphere surface, and forwards the event (position + duration + intensity + radius) to a `ForcefieldCrackleController` MonoBehaviour on the vessel (`_Scripts/Controller/Vessel/ForcefieldCrackleController.cs`). The controller owns all visual parameters (colors, arc density/sharpness, ring thickness, ripple speed, fresnel) as serialized fields and feeds a ring buffer of up to 16 simultaneous impacts to the shader via MaterialPropertyBlock arrays each frame. `[ExecuteAlways]` allows edit-mode preview via `ForcefieldCrackleControllerEditor` (at `_Scripts/Editor/`). The shader's custom-function HLSL file `ForcefieldCrackle.hlsl` (at `Assets/Materials/Graphs/`) uses FBM-based electrical arcs with expanding wavefronts on a geodesic distance metric so arcs follow the sphere's curvature. All three code files use the `CosmicShore.Gameplay` namespace.

### Audio (FMOD) — every sound is an exposed, editable field (LOCKED convention)

FMOD Studio is the audio middleware (`FMODUnity`, `Assets/Plugins/FMOD`). The rule below is not a
style preference — it is what makes the game's audio *authorable by whoever owns audio*, without a
programmer, a recompile, or a merge.

> **Every noise anything makes must be an inspector-exposed `EventReference` on the prefab/component
> (or SO) that makes it.** If a sound exists, an audio designer must be able to find it in the
> component view of the thing that produces it, and swap it — without touching code, and without
> hunting for which shared category it borrowed.

**Corollaries — all three are load-bearing:**

1. **Never plug in a "temp" event.** Do not point a new sound at a borrowed/placeholder FMOD event
   just to hear something. Ship the `[SerializeField] EventReference` **empty** and let it be
   silent — an empty slot is a visible, greppable TODO in the inspector; a temp event is an
   invisible one that survives to release and gets mistaken for an intentional sound. FMOD's
   `EventReference.IsNull` makes an empty slot a clean no-op, and `AudioSystem` already warns once
   per unwired category (`warnOnUnwiredCategory`) rather than failing. Follow that pattern: check
   `IsNull`, return, optionally warn once — never substitute another event.
2. **Every ship ability gets its own dedicated FMOD event field** — boost, gun fire, drift, shield,
   turret, missile, ability start/stop, whatever. One field per ability per distinct sound (a
   start/stop or charge/release ability gets a field for each). Do **not** route a new ability
   through an existing `GameplaySFXCategory` because it is "close enough" — sharing a category means
   two abilities can never be tuned independently, which is exactly what the audio owner needs.
3. **The sound is a trigger's payload, not an implicit side effect.** When something should sound on
   contact, the collider/trigger that detects the contact is where the `EventReference` lives and is
   played from. Same for a state change: the component that owns the state plays its own field.

**How to add a sound (the shape to copy):**

```csharp
[Header("Audio")]
[SerializeField, Tooltip("FMOD event played when this ability fires. Leave empty for silence.")]
EventReference fireEvent;

// at the trigger / state change:
if (!fireEvent.IsNull)
    audioSystem.PlaySFXEvent(fireEvent, transform.position);   // spatialized
```

Play through `AudioSystem` (`PlaySFXEvent` / `PlaySFXEventAttached`) or
`FMODOneShotVolumeHelper` — **never** `RuntimeManager.PlayOneShot` directly, which has no
per-instance volume and therefore ignores the SFX slider when the bus fails to resolve
(`_Scripts/Controller/FX/FMODOneShotVolumeHelper.cs` documents why). For a **looping/continuous**
sound (engine, drift, ambient, creature loop) use a `StudioEventEmitter` on the prefab — again with
the event exposed — or a small controller that owns its own `EventReference` field, like
`ShipAudioController`, `DriftAudioController`, `ProximityBoostAudioController`,
`FloraAmbientAudioController`.

**Create, attach and release through `FmodSafe`; resolve volume through `AudioSystem`.**
`RuntimeManager.CreateInstance` THROWS for an event no loaded bank knows (renamed, deleted, stale
GUID, banks still loading) and for a system that failed to initialise; a controller that retried it
every frame turned one bad reference into an exception per frame. And `RuntimeManager.Instance` —
which `Attach`/`Detach` go through — RE-CREATES the manager and re-initialises FMOD if the old one
is already gone, which is exactly the state during quit; FMOD's own emitter guards that with an
`isQuitting` flag and ours did not. `FmodSafe.TryCreateInstance` (reports once, then silent),
`FmodSafe.Attach/Detach` (no-ops during teardown) and `FmodSafe.StopAndRelease` are the seams.
Volume is ONE mapping — `AudioVolumeMath`, read through `AudioSystem.ResolveSfxInstanceVolume(trim)`
/ `ResolveMusicInstanceVolume(trim)` — never a per-component copy of "mute → 0, slider × trim":
the FMOD project's `vca:/SFX` / `vca:/Music` control no bus today, so the slider is applied per
instance, and the day they do (`AudioSystem.driveFmodVcas`) every resolver collapses to its trim so
the slider is never applied twice. A UI slider talks only to `GameSetting` (`AudioLevelSlider`),
never to FMOD. Record: `Docs/AudioSystem/FMOD_AUDIT.md`; audio-owner tasks: `CHARLES_TASKS.md`.

**The two tiers, and which to use:**

| Tier | What it is | Use when |
|---|---|---|
| **Per-prefab field** (preferred) | `[SerializeField] EventReference` on the component that makes the noise; edited in that prefab's component view | The sound belongs to a *specific thing* — a vessel ability, a projectile, a trigger volume, a creature, a toy, a UI widget with its own voice |
| **Central category** | `AudioSystem.PlayGameplaySFX(GameplaySFXCategory.X)` / `PlayMenuAudio(MenuAudioCategory.X)`, wired once on the AudioSystem GameObject | The sound is genuinely *shared platform-wide* and must stay identical everywhere — prism destruction, crystal collect, generic vessel impact, menu clicks |

Both tiers keep the event in an inspector slot; they differ only in *where* the slot lives. If you
find yourself adding a `GameplaySFXCategory` member for one vessel's one ability, that is the signal
you wanted a per-prefab field instead. **Existing ability call sites that pass a shared category
(`BoostActionSO` → `BoostActivate`, `DriftActionSO` → `DriftStart`/`DriftEnd`) are the legacy shape**
— when you touch one, give it its own `EventReference` field (falling back to the category only when
the field is empty) rather than adding another category consumer.

Data-driven variants override the same way — a config SO carries the `EventReference` and stamps it
onto the emitter at spawn (`FaunaConfigurationSO.OverrideAudio` + `AudioLoopEvent` →
`Fauna`'s `StudioEventEmitter`), so a species can be re-voiced or silenced from its asset.

**Anti-patterns:**

- A hardcoded `RuntimeManager.PlayOneShot("event:/some path")` or any string event path in code
  (first-party code is currently clean of this — keep it that way)
- A new sound wired to an unrelated existing event "for now"
- A sound that can only be changed by editing C# or by finding one shared enum member
- An `AudioClip` + `AudioSource` for a *new* gameplay sound — the Unity AudioSource path is legacy
  (music, plus stragglers like `IconEmitter` and `AudioSystem.PlaySFXClip`) and is being retired;
  new SFX is FMOD
- Adding an if-null-guard *fallback to another event*. Guard for silence, never for substitution
