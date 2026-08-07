# Unity In-Editor Verification Checklist

**Purpose.** Some changes land on shared branches (`bleeding-edge` and the
per-feature branches) without ever being opened in the Unity Editor —
authored and committed by a session that **cannot run the editor**, so no
compile, no play-test, no prefab/asset inspection happened on the author's
side. Those changes are correct on paper but carry editor-side risk: a prefab
import that didn't take, a Variant override that didn't serialize, a rig weight
that reads differently in-scene than in code.

This doc is where that risk gets **recorded once** instead of being re-explained
at the start of every session. When you next open the project in Unity, work the
open items below, tick what you confirm, and delete (or move to "Verified") what
holds up. When you commit code that you could not editor-verify yourself, add an
entry here rather than leaving it in a PR body or a chat message that scrolls away.

**How to use it**
- One `### ` section per unverified change set, newest first.
- Each has: what landed, the concrete **verify in editor** steps, and any
  **first-pass tuning** numbers (these are starting points, expect a balancing
  pass once the thing is observable in context — they are *not* settled).
- Status markers: 🔴 unverified · 🟡 partially confirmed · 🟢 verified in editor.

---

### 🔴 Sparrow Turret Stance — the stance fired nothing; flight moved to the GPU clock (`claude/sparrow-prism-attack-hg6n78`)

Authored without a Unity compile or play-test, and this one touches **shader graphs**
(hand-synthesized JSON), **new ECS per-instance material properties**, two prefabs, two SO
assets, and a rewritten hot-path executor. Full mechanics + the 12-step procedure:
`_Scripts/Controller/Vessel/R_VesselActions/SPARROW_TURRET_STANCE.md` § "In-editor verification".

**The headline.** The stopped Sparrow was firing **invisible, intangible** prisms — the path
never called `Prism.Initialize`, so `IsCreationComplete` stayed false, `BeginGrowthAnimation`
early-returned, and every shot lived its whole life at `localScale` zero (no visual; its child
collider inherited `lossyScale` 0 so it could not register a hit either). Silently — the loop
never threw. That is fixed at the root: turret prisms now spawn through the documented
pool-spawn entry point.

**What else landed.** The shot is now the bullet in every authored respect (rate 30/s, speed
1500 × SPACE, the same eased flight, the same impact effect, and pierce on the **same SPACE-5
gate** — not always-on as the previous commit had it). The flight itself moved to the GPU clock
(`PRISM_ANIMATION.md` §5 C5, now SHIPPED): new `PrismFlightClock` HLSL + `_FlightStartTime` /
`_FlightDuration` / `_FlightVelocity` Hybrid-Per-Instance properties spliced into **both**
live-prism graphs. C4 was resolved by deleting `FireTrailBlockActionExecutor`/`SO` — unreachable
dead code carrying two racing `Destroy` timers on a visible prism.

**Verify in editor (the five most likely to be wrong):**

1. **The graphs import clean.** BlockGraph and ExplodingBlockGraph were edited out-of-editor;
   every block is schema-exact and machine-validated, but Unity has not imported them. Open each
   and confirm no import errors and that `FlightStartTime` / `FlightDuration` / `FlightVelocity`
   appear on the Blackboard. Recovery if not: `git checkout` the `.shadergraph` and run
   `FrogletTools > Ecology > Prism Animation > Auto-Wire Clock Properties`.
2. **Something comes out of the guns at all.** This is the bug the human reported. Stop, hold
   fire, watch prisms leave the muzzles.
3. **The flight is smooth, with no pop-in.** A prism that simply appears at maximum range means
   the flight stamp failed (look for `[PrismClock] flight:` in the console); one that vanishes
   and reappears partway means the `RenderBounds` envelope is wrong.
4. **Pierce follows SPACE 5, both modes.** Below it a shot stops at the first prism and leaves
   its prism there; at 5+ it destroys a line and leaves its prism at the far end.
5. **Asset re-serialize.** `FullAutoBlockShootAction.asset` YAML was hand-edited against a
   changed field set — confirm the inspector shows **Bullet Action** = `FullAutoAction` and no
   ghost fields. Also confirm `Sparrow Projectile Prism.prefab` shows `waitTime` **0** (at 0.5 the
   prism was still invisible when its 0.3 s flight ended).

**Asset-only gates that should already pass** (run them first — they need no play mode):
`python3 Tools/Shaders/wire_prism_flight_clock.py --check` (OK on both graphs) and
`FrogletTools > Ecology > Prism Animation > Validate Clock Wiring`.

**Budget note (deliberate, flag if it hurts).** Matching the gun cadence roughly doubles the
permanent mass a held turret lays: **~60 anchored prisms/s**, ~600 in a ten-second hold. The
single lever is `FullAutoAction.firingRate` — and it moves the guns too, by design. Per-frame CPU
went *down*: the deleted `MoveAndAnchorAsync` was a per-frame write per live prism.

**First-pass tuning:**

| Knob | Value | Where it lives |
|---|---|---|
| Fire rate (both modes) | **30/s** | `FullAutoAction.asset` → `firingRate` |
| Muzzle speed base (both modes) | **1500** | `FullAutoAction.asset` → `speedValue.Value` |
| Flight time → range | **0.3 s** → ~286 u | `FullAutoAction.asset` → `projectileTime` |
| Prism shape before MASS stretch | **(0.8, 0.5, 5)** | `FullAutoBlockShootAction.asset` → `blockScale` |
| Creation delay | **0** (was 0.5) | `Sparrow Projectile Prism.prefab` → `waitTime` |

---

### 🔴 Sparrow stationary stance — roll works stopped, pitch/yaw 3× (`claude/sparrow-strafing-roll-stopped-d2yc7g`)

Authored without a Unity compile or play-test. **Code only — no prefab, scene or
SO asset was touched**, so the editor-side risk is a compile check plus feel.

**What landed.** The strafing roll (`BarrelRollController`) no longer bails out
while the Sparrow is in its stationary/turret stance. Stopped, the boost still
gives no speed, but the roll arms on the same boost press, triggers on the same
full stick deflection, spends the same charge pip, and **strafes the same
distance** — it is the stopped Sparrow's dodge. Rolling does not change the
stance.

- The displacement survives the restriction via a new per-modifier opt-in,
  `ShipVelocityModifier.ignoresTranslationRestriction` (default **false**; only
  the roll sets it, every other `ModifyVelocity` caller is untouched and stays
  fully held while restricted).
- `VesselTransformer` grew a restricted branch: `ApplyVelocityModifiers(
  translationRestricted: true)` + `MoveRestricted()`. It deliberately does not
  write `VesselStatus.Speed` or `Course`.
- Two incidental fixes fall out of that branch — velocity modifiers now **age**
  while restricted (previously they froze and lurched out on stance release),
  and the `StopFlareBody` material write is now edge-triggered instead of
  per-frame (it writes through `renderer.materials[0]`, which clones).
- The roll projects its nudge on current **facing** while stopped, because
  `Course` is stale there (`MoveShip` is what refreshes it).

**Also landed: pitch and yaw run at 3× while stopped.** New serialized
`VesselTransformer.restrictedTurnMultiplier` (default **3**), applied through a
shared `TurnScalar` property to the whole pitch/yaw rate whenever
`IsTranslationRestricted` is set. Applied in **`SingleStickVesselTransformer`**
as well as the base class — that subclass overrides `Pitch`/`Yaw` and is what
both the Sparrow and the Serpent actually run, so a base-only change would have
reached neither. Roll is deliberately unscaled (it is the bank into the turn,
not a turn rate). **The Serpent inherits the same default** — one inspector
field on `Serpent.prefab` to opt out.

**Verify in editor**

1. Project compiles with zero errors. Run the `CosmicShore.Tests.EditMode`
   suite — `ShipModifierTests` gained two cases pinning the new flag.
2. `MinigameFreestyleMultiplayer_Gameplay` (or Menu_Main freestyle), Sparrow.
   **Flying** roll first: boost + full left stick → rolls and strafes, once per
   press. This must be **unchanged** — it is the regression risk.
3. Toggle the stationary/turret stance. Boost + full left stick → **rolls and
   strafes**. Speed does not change. Still once per press (hold boost + hold the
   stick at max = exactly one roll). Charge ring arms and wipes as when flying.
4. After the stopped roll: still stopped, still in stationary fire mode, and no
   trail/bridging prisms were laid.
5. **Stale-course check.** Stopped, rotate to aim well away from the heading you
   had when you stopped, then dodge. The strafe must go where the stick points
   relative to your **current** facing — a skew toward the old heading means the
   projection plane is wrong.
6. **No banked lurch.** Stopped, take a knockback (a Rhino ram, or clip a danger
   prism): you must not move. Release the stance: you must not lurch.
7. **Stopped turn rate.** Flying, time a full 180° yaw. Toggle the stance and
   repeat: roughly **a third** the time. Pitch likewise. Release the stance and
   the rate must drop straight back (the scalar is read per frame — a rate that
   stays fast means it got cached). The bank into the turn is unchanged by
   design, so the stopped turn reads flatter than a flying one.
7b. **Other vessels.** Serpent — stop into its weave stance, take a knockback,
   release: no movement while stopped, no lurch after. Note its pitch/yaw are
   **also 3×** while stopped (same transformer, same default); set
   `restrictedTurnMultiplier` to `1` on `Serpent.prefab` if that is unwanted.
   Any vessel: boosts/bounces/deviation nudges still displace normally while
   flying, and flying turn rates are unchanged everywhere.
8. **MPPM two clients.** Roll while stopped on client A; client B must see the
   same displacement (it replicates through the owner-authoritative
   NetworkTransform, same as the flying roll — no new networked state).

**First-pass tuning** (starting points, not settled)

| Knob | Where | Value |
|---|---|---|
| Dodge distance | `Sparrow.prefab` `BarrelRollController.nudgeSpeed` / `rollDurationSeconds` | 60 / 0.6 — **one number for both stances**. If the stopped dodge should reach further or less far than a flying strafe, that is a new serialized field, not a rescale of this one. |
| Stopped turn rate | `Sparrow.prefab` `VesselTransformer.restrictedTurnMultiplier` | 3 (pitch + yaw only). Sparrow authors PitchScaler/YawScaler 80 with RotationThrottleScaler 0.1, so ~82 °/s flying → ~247 °/s stopped. |

**Open question the author could not resolve** — whether a stopped dodge should
cover the same ground as a flying strafe. Shipped as identical (the simplest
reading of "the same way"); it is one field to split if it plays too strong in
turret stance.

---

### 🔴 Sparrow boost redesign — no overheat, base strafing roll, Elemental Ward (`claude/sparrow-ability-redesign-norbgz`)

Authored without a Unity compile or play-test. Touches a **platform** surface
(`ResourceSystem.ApplyElementalEffect`) plus two vessel prefabs edited as YAML,
so the editor-side risk is real: hand-written prefab documents, a removed
GameObject, a removed resource slot, and renamed serialized fields.

**What landed**

- The Sparrow's overheat mechanic is **deleted** — `OverheatingActionSO`,
  `OverheatingActionExecutor`, the legacy `OverheatingAction`, the
  `OverheatingAction.asset`, the `Heat` resource on the Sparrow prefab, and
  `VesselStatus.IsOverheating`. Input event 7 binds straight to the shared
  `BoostAction.asset`; boost is now unlimited in duration.
- The **strafing roll dropped to base kit** — `BarrelRollController` lost its
  `IsUpgradeActive(Element.Time)` gate. Still one roll per boost press.
- **TIME-5 is now "Elemental Ward"** — a general, source-keyed
  elemental-debuff immunity on `ResourceSystem`
  (`SetElementalDebuffImmunity` / `IsElementallyImmune`), gated in one place:
  the negative branch of `ApplyElementalEffect`. Driven declaratively by the new
  `VesselElementalImmunity` component: **Sparrow** `WhileBoosting` + Time gate,
  **Serpent** `WhileTranslationRestricted` ungated.
- The Sparrow boost icon's radial gauge became a **binary roll-charge pip**
  (`SparrowHUDView.SetRollCharge`), driven by
  `BarrelRollController.OnRollChargeChanged`.
- `SquirrelVesselHUDController` lost its `OverheatingActionExecutor` lookup — it
  compiled against a Sparrow-only component and always resolved to null on a
  Squirrel, so the Squirrel's heat gauge never moved. Pure dead-code removal.

**Verify in editor** (full steps + expected observables:
`_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md` §
"In-editor verification")

1. Project compiles with zero errors; no new console warnings on Sparrow or
   Serpent spawn. **Known pre-existing, not from this branch:** the Sparrow's
   `ElementalBarsController.view` reference (`fileID 7416581124810081342`) is
   already dangling on `bleeding-edge`.
2. **Prefab integrity is the top risk.** Open `Sparrow.prefab` and confirm: no
   missing-script slots; the `OverheatingBoostActionExecutor` child is gone; the
   `ResourceSystem` list reads Missiles / FullAuto / ExhaustBarrage (3 entries,
   no Heat); `SparrowHUDController.barrelRollController` points at the root's
   `BarrelRollController`; `VesselElementalImmunity` is on the root reading
   `WhileBoosting` + `Time`. Then `Serpent.prefab`: `VesselElementalImmunity`
   on the root reading `WhileTranslationRestricted` + `None`.
3. Hold boost 60 s — no force-release, no danger trail, no self-slam.
4. Time at 0: boost + full stick deflection rolls **once** per press.
5. The boost (rightmost) ability icon's ring: full on press, wipes empty with a
   punch on roll, empty until the next press. Never a partial fill.
6. Time ≥ 5 (`ResourceSystem.TimeTestHarness = 0.5`): danger prism **while
   boosting** → element flowers do not dip; **not boosting** → they dip. Slow
   and input-mute land either way (by design).
7. Serpent stopped + danger prism → no flower dip, at any Time level.
8. **MPPM two clients**, both Sparrows, one at Time 5: both machines must agree
   on who resists the drain. This is the replicated-`NetElementUnlocks` path —
   a local level read would pass step 6 and fail here.
9. `FrogletTools > Vessels > Audit Vessel Ability Rows` — Sparrow still 4/4 in
   charge → mass → space → time.

**First-pass tuning** (starting points, not settled)

| Knob | Where | Value |
|---|---|---|
| Boost speed at Time 10 | `Sparrow.asset` Time `MultiplierAtFullLevel` | 1.5 (unchanged — but the hold is now unbounded, so this is the first balance lever) |
| Immunity window | `Sparrow.prefab` `VesselElementalImmunity.condition` | `WhileBoosting` (`Always` = passive ward at Time 5, one field) |
| Roll pip colours | `SparrowHUDVariant.prefab` | armed cyan `0.55/0.9/1`, spent dim grey `0.35/0.4/0.45 @ a 0.5` |
| Roll wipe / punch | same | 0.15 s / 0.3 |

**Open design question the author could not resolve** — whether the ward should
hold `WhileBoosting` (shipped, mirrors the Serpent's stopped stance) or `Always`
at Time 5. With an indefinite boost, `WhileBoosting` means a pilot willing to
fly permanently full-throttle is permanently warded. One inspector field either
way; no code change.

### 🔴 Dolphin elemental pass — skim feedback, drift boost, cone blast (`claude/dolphin-energy-crystal-cooldown-zpvc07`)

Authored without a Unity compile or play-test. Garrett play-tested the HUD/boost
rounds mid-branch, but **the final skim-feedback fix is unconfirmed** — the last
report was still "no skimming indication", after which the branch found (a) the
crackle needs three pieces the Dolphin had none of, and (b) all three skim signals
are individually invisible on desktop. Nobody has yet seen a Dolphin skim work.
Mechanics + full knob list: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md`.

**Verify in editor (highest risk first):**

1. **Run `FrogletTools > Vessels > Audit Vessel Skimmers`.** Expect
   `Dolphin  NearFieldSkimmer: 'EnergySkimmer' OK`. This is the branch's headline fix —
   `VesselStatus._nearFieldSkimmer` pointed at a DISABLED legacy skimmer, so
   `Skimmer.Initialize` never reached the object whose trigger fires and
   `SkimmerImpactor` dropped every contact silently. (Serpent is expected to FAIL —
   known, untouched.)
2. **Skim in Menu_Main freestyle.** Fly the Dolphin through cell mass: crackle arcs
   should sweep the skimmer sphere per prism, the HUD jaw icon should punch per skim,
   and the gape (icon + the model's own jaws) should widen toward 18.4° per side as
   energy fills. Watch the console — an unauthored `Prism.ParticleEffect` now logs one
   named warning per prefab instead of throwing per contact.
3. **The boost loop.** Hold drift → the ring steps up; release → speed rises and decays
   as it drains. Flying straight must NOT fill the ring (the passive `resourceGainRate`
   is gone). Drift → release → drift again must return to normal speed (the interrupted
   discharge used to leave `BoostMultiplier` stuck).
4. **Crystal impact.** The cone fires, energy empties, the jaws snap shut, and the Space
   icon flashes with a prism count. At Space L5 the cone must stop damaging your own
   domain's prisms.
5. **Charge L5.** A second crystal pip appears and two team crystals can be planted back
   to back. The deploy preview must be tinted your domain, and bloom/wither rather than
   pop (continuity of existence).
6. **MPPM two-client:** the L5 upgrade effects are gated on the replicated
   `IsUpgradeActive`, so confirm both peers agree on Clean Blast and Twin Seed.

**Hand-authored assets that have never had an editor import round-trip:** the Dolphin
HUD variant's four-icon row, the Dolphin prefab's crackle overlay + controller, and
`DolphinSkimmerChangeResourceByPrismEffect.asset`. Their YAML keys were machine-checked
against the scripts' serialized field sets, but Unity has not re-serialized them.

---

### 🔴 Fauna consumption v3 + shark jaw rig (fauna-consumption-behavior branch, merged)

Landed via PR #614 (`claude/fauna-consumption-behavior-*`) plus the shark-jaw
commit `438070a2`. None of it had a Unity compile or play-test from the author —
it is on the shared branch unverified. Design + mechanics reference:
`Docs/ECOSYSTEM.md` §7 / §7.3 (intentional consumption, the mouth-driven
predator, tiger-shark territoriality, centre focus).

**Verify in editor (the three things most likely to be wrong):**

1. **Jaw prefab import.** Open `Assets/_Models/Fauna/MassSharkFauna.prefab`.
   Confirm `SharkJawDriver` (`_Scripts/Controller/Environment/FloraAndFauna/SharkJawDriver.cs`)
   sits on `Shark_model` alongside the `Animator` + `RigBuilder`, that the two
   mouth `MultiAimConstraint`s and the `MawTarget` it aims at are all present and
   wired, and that weight `0` = FBX swim pose (mouth closed) / weight `1` = aimed
   at `MawTarget` (mouth open). Danger prisms are parented to the jaw bones — check
   the teeth actually gape with the mouth in a play-test (`NotifyBodyPrismsMoved`
   should keep their spatial-index positions honest as the jaw moves).

2. **Elemental Variant on the tadpole config.** Confirm the tadpole's
   `FaunaConfigurationSO` / prefab Variant carries its intended elemental setup
   (that the Variant override actually serialized and points at the creature
   prefab's `Boid`, not the dead `*Population`/manager prefab — see the §7 warning
   that the live spawn path is the cell config, not the scene-placed populations).

3. **Two feeding models coexist.** Confirm both consume paths still compile and
   run side by side without one having been collapsed into the other:
   `LightFauna` (brittlestar/shark) has **no** `_pendingMeals` grazing queue
   (intentional-feeding: approach → face → suction), while `Boid`'s **drone**
   path keeps its `_pendingMeals` burst-pacer (combat). Do not re-add the
   burst-pacer to the forager/intentional types or strip it from the drone path
   (`Docs/ECOSYSTEM.md` §7.3 explains why they differ).

**First-pass tuning (expect a balancing pass — observe in context first):**

| Knob | Value | Where it lives |
|---|---|---|
| Hunt pulse (window / cycle) | **10s open / 20s interval** | `LightFaunaDataSO.huntDurationSeconds` / `huntIntervalSeconds` |
| Tiger-shark territory radius | **r = 600** | `LightFaunaDataSO.territoryRadius` (+ `territoryAnchorDistance`) |
| Jaw open / close | **0.6s open / 1.8s close** | `SharkJawDriver` (open notably faster than close) |
| Herbivore/forager centre focus | **0.35** | `FaunaConfigurationSO.CenterFocusBias` (per-deployment) |

These four are the ones the author flagged as guesses. The jaw transition is
~2.4s total per 20s hunt cycle; the driver early-outs on a single float compare
whenever the mouth is settled, so re-tuning the timings has no perf cost.
