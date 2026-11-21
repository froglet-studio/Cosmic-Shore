# Haptics — the two-feel policy (+ one rare alert, + two fenced textures)

Cosmic Shore ships **two everyday haptic feels**, both **local-human-pilot-only**, plus **one
rare alert** reserved for match-changing events and **one continuous texture** fenced to a held
full-auto trigger. Everything else
is deliberately silent. This is a design decision, not an omission: minimal, legible haptics that
never fight each other read as *intentional*; a buzz on every UI tap, drift, boost, joust, and
explosion reads as noise. Keep it this way — see "Adding/changing a feel" before touching it.

| Feel | What it is | Fires on |
|---|---|---|
| **Skim pulse** (reward) | Short (~70 ms), bright, sharp transient at high haptic frequency. Strength scales with how close the prism passed to the skimmer centre. Many in sequence read as a rapid, continuously rewarding pulse train. | Each prism entering a skimmer (Squirrel etc.) |
| **Punish thud** (mistake) | Heavy, **low**-frequency thud, the deliberate opposite of the bright skim, on a three-step **hull-hit ladder** read from the prism (Caleb 2026-11-12): **plain** ~240 ms; **shielded** ~300 ms with more high-motor grit; **heavy** (danger or super-shielded) a ~420 ms crunch, two slams with a dip between. A lighter hit never cuts a heavier thud short, and a heavier one lands at once inside the 250 ms spacing. Shielded and heavy play at **2x** their authored strength (`PunishGain`, Caleb 2026-11-12; the motors cap at 1, so the first slams cannot grow and the grit and tails double). Plain is **25% under** its doubled feel (low 0.75, high 0.375 then 0.225), authored straight into its clip. | The vessel **body** slamming a prism |
| **Alert shake** (event) | Long (~1.2 s) hard **rattle** — full-amplitude sawtooth at mid frequency, both gamepad motors out of phase. Unmistakably neither of the above, and long enough to read as "something happened" rather than "you hit something". | Cleave's progress-milestone rungs (25% / 50% of the win target) — **nothing else** |
| **Bind grind** (state) | Short (~80 ms) **low**-frequency (0.15) grind carried by the LOW motor with a little high-motor grit, no transient, repeated every ~100 ms. Heavy like the thud, repeating like the spray. | The Rhino's NON-energized blade held inside a super-shielded prism it cannot cut (`RHINO_ENERGY_SWORD.md` § "Binding") — the entry is a punish thud, this is the texture after it. **Nothing else** |
| **Drift rumble** (state) | Short (~90 ms) smooth, **low-mid** frequency (0.3) swell-and-fade, low motor leading but lighter than the bind, no transient. Repeats every ~100 ms while drifting; strength follows the **slip angle** like the drift loop's volume (linear from `quietSlipDegrees` to `fullSlipDegrees`), 0 to 0.3 - subtle by design (Garrett 2026-10-27). | Drifting on the local human pilot's vessel (`DriftAudioController`, Haptics header). **Nothing else** |
| **Drift press / let-go** (event) | Two one-shots. Press: a firm ~60 ms mid-frequency (0.6) tap on both motors. Let-go: a softer ~90 ms low-frequency (0.2) bump, low-motor led. Each sets a short busy window so the drift rumble can't cut it short. | The drift trigger engaging / releasing on the local human pilot's vessel (`DriftAudioController`). **Nothing else** |
| **Plant knock** (event) | Two short firm knocks (~40 ms, 40 ms gap, ~50 ms softer second), mid-low frequency (0.35), both motors with the low one leading. Two hits is its signature against the skim's one tick and the punish's one thud. Rate-limited to 150 ms; suppresses skim and the textures for its ~130 ms. | The Manta planting a bomb on a vessel or a creature (`MantaStingActionExecutor.SpendBomb`, local human pilot; Caleb 2026-11-11). **Nothing else** |
| **Spray buzz** (state) | Short (~50 ms) **mid**-frequency buzz with no transient (skim's signature) and both motors together (which is what reads as a buzz rather than a tick or a rumble). Repeats while the trigger is down, climbing in **both** strength (0.15 → 1.0) and cadence (100 ms → 45 ms) as the gun's accuracy decays, and holding flat once the cone reaches its sustainable cap — both channels are at their ceiling there, so a longer hold has nothing worse left to say. | Holding the Sparrow's full-auto trigger — bullets **or** turret stance. **Nothing else** |

**Priority, top to bottom: alert > punish (heavy > shielded > plain) > plant > skim > drift press/let-go > spray = bind = drift.** The textures share the
bottom rung and neither sets a busy window, so neither can suppress anything. The spray is the game's only
*continuous* feel and therefore the only one that sits below skim: everything suppresses it and
it suppresses nothing. Being interruptible costs it nothing (the next pulse is milliseconds
away) and it is what keeps the two feels the policy is built around fully legible — a thud still
cuts cleanly through a held burst.

## The universal rule (DECIDED, Caleb 2026-11-12)

Nothing in the docs set a fleet-wide rule for what the pad says on a collision, a skim or a joust before this; the
camera already had one (`Docs/CAMERA_FEEL.md`: every hull's hit weighed by the prism). Caleb: honour the universal
rule AND the spirit of each kit, "vessels that want contact should feel rewarded for doing the thing they want, not
punished. Not even punished lightly."

1. **The pad speaks for what happens to the pilot's own hull, or for a deliberate act landing.** Never for a
   background state that runs without the pilot doing something (a passive skimmer in dense mass was the Manta's
   minutes-long hum).
2. **A hull hit is a mistake, and plays the hull-hit ladder** (plain, shielded, heavy; above) on every hull whose
   kit does not want contact. No mode rescales it.
3. **A rammer is rewarded, never punished.** A hull whose verb IS contact (today: the Rhino, "a plough") carries
   `VesselRamRewardByPrismEffect` INSTEAD of the punish effect: the skim pulse at one fixed strength (0.7), spaced at
   least 100 ms (above the 70 ms clip, so a plough through a wall never becomes a hum). Whether a rammer's reward
   grows on shielded, danger or super-shielded prisms is open; if it does, it is a per-kit choice, like the skim.
   Candidates to review against their kits: the Shrike ("docks to whatever it touches"), the Urchin
   (`VesselAttachPrismEffect`), the Grizzly ("rush and ride your shockwaves").
4. **Skims are opt-in per kit**: a skim pulse only where skimming is the hull's core, skill-tested verb (the
   Squirrel's speed tubes, the Dolphin's needle-threading), never a hull whose skimmer is a big passive sphere (the
   Manta). Stronger skim pulses on armour or danger MAY be allowed per kit later.
5. **A kit's signature act gets at most one event feel**, fenced to it (the Manta's plant knock, the Sparrow's spray,
   the Rhino's bind, the drift kicks), and it must be distinguishable from the thud and the skim.
6. **Pilot-on-pilot moments** (a joust, a petal hit taken) are silent today; adding one is a fleet decision, not a kit's.

## Where it lives

| Role | File |
|---|---|
| Policy + gate + runtime clip factory | `_Scripts/Controller/IO/HapticController.cs` (`CosmicShore.Gameplay.HapticController`) |
| Skim hook (proximity-scaled) | `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerHapticsByPrismEffectSO.cs` |
| Punish hook | `_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Prism Effects/VesselHapticsByPrismEffectSO.cs` |
| Skimmer sphere radius (for proximity) | `_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs` (`SphereWorldRadius`) |
| Spray ramp driver (accuracy decay → strength + cadence) | `_Scripts/Controller/Vessel/R_VesselActions/Executors/GunSprayAccuracy.cs` (`DriveHaptics`); tuning on `GunSpreadProfile`, authored on `FullAutoAction.asset`. See `R_VesselActions/SPARROW_SPRAY_ACCURACY.md` |
| Gamepad rumble player (what actually vibrates) | `_Scripts/Controller/IO/GamepadRumblePlayer.cs` |
| The rumble envelope type | `_Scripts/Controller/IO/GamepadRumblePattern.cs` |

The two feels are ordinary **impact-effect SOs** wired into the standard effect containers, same as
every other `SkimmerPrismEffectSO` / `VesselPrismEffectSO`:

- **Skim**: `SkimmerHapticsByPrismEffect.asset` → each skimmer's `SkimmerImpactorDataContainerSO.SkimmerPrismEffects`
  (fired from `SkimmerImpactor.AcceptImpactee`, prism case). Wired into the Squirrel and
  Rhino-forcefield skimmer containers. **The Manta is the exception (2026-11-11):** its skimmer is
  20-40 units across, so a pulse per prism ENTRY never stopped in dense prisms (the Toybox cell, or its
  own trail waking inside the sphere when it flies slower than the radius per second). Pulses 30 ms apart
  on a 70 ms clip never let the clip end, so the pad sat at one steady level for minutes. Caleb's call:
  the Manta's skim has **no haptic** (sound and the HUD tell it). Its pad speaks only on a hull collision
  (the punish thud, below) and when it plants a bomb (the plant knock).
- **Punish**: `VesselHapticsByPrismEffect.asset` → each vessel's `VesselImpactorDataContainerSO.VesselPrismEffects`
  (fired from `VesselImpactor.AcceptImpactee`, prism case). Wired into all six playable vessel containers.

Both effects gate on `status.IsLocalUser && !status.AutoPilotEnabled` — remote players and AI/autopilot
(including the Menu_Main lava-lamp) never buzz this device. (The pre-existing `HapticSpec` helper only
checked `AutoPilotEnabled`, which leaked remote players' haptics; these effects no longer use it.)

## The gate (why it exists)

The motors carry **one pattern at a time** — every `GamepadRumblePlayer.Play()` replaces whatever is
playing. That was originally a property of the plugin; it is now a deliberate choice of ours, and it is
what makes the priority order expressible at all. So a tiny priority/rate-limit gate in
`HapticController` arbitrates the feels with a handful of timestamps (no metering, no per-category
tables):

- **Skim** is rate-limited to a rapid train (`SkimMinIntervalSec` ≥ 30 ms) and is **suppressed while a
  punish is playing** (`s_punishBusyUntil`), so the train can never cut a thud short.
- **Punish** is spaced out (`PunishMinIntervalSec` ≥ 250 ms) and **always loads over** whatever skim clip
  is playing — a thud always interrupts the train.
- **Spray** yields to all three (`s_alertBusyUntil`, `s_punishBusyUntil`, `s_skimBusyUntil`) and sets
  **no busy window of its own**, so it can never suppress anything. Its real cadence is owned by the
  caller — it tightens with the same accuracy decay that raises its strength — and
  `SprayMinIntervalSec` (35 ms) is only a backstop against a second caller.

Priority: **alert > punish > skim > spray**, always.

## Clip generation

The four feels are generated **once at runtime**, each as BOTH a `.haptic` JSON envelope and a
`GamepadRumblePattern`, then replayed per pulse through `HapticController.PlayPattern`. Decimal points in
the JSON are hard-coded so the strings are locale-independent. Skim = high frequency + high-frequency
motor (bright); punish = zero frequency + low-frequency motor (heavy).

Every number in those envelopes is **first-party data we authored** — which is the whole reason the
vendor swap below cost one file and no content.

`GameSetting.HapticsEnabled` / `HapticsLevel` are honoured on every play; disabled or zero-level →
nothing plays. The player's level and the per-pulse strength are multiplied into **one** gain before it
reaches a backend, so a backend cannot apply half of the scaling.

## What plays them (and what does not)

**Gamepad — the whole feature on the launch platform.** `GamepadRumblePlayer` steps a pattern's segments
through `UnityEngine.InputSystem`'s `Gamepad.SetMotorSpeeds`, on `Gamepad.current`. It replaced Lofelt
NiceVibrations, whose entire contribution in code was this player: **one file, five API symbols**
(`Load`, `outputLevel`, `clipLevel`, `Play`, and the `GamepadRumble` struct). No clip, pattern or number
came from the plugin, so nothing was re-authored and no feel changed. Details of the replacement decision:
`Docs/THIRD_PARTY_DECISIONS.md` §4.

Three properties of that player are load-bearing and should not be "simplified" away:

* **One pattern at a time.** A `Play` replaces what is playing. The gate above is written against this.
* **Position comes from ELAPSED TIME, not one segment per frame.** A frame is the finest resolution
  available (16.7 ms at 60 fps, 33 ms at 30) and several authored segments are shorter than that, so a
  slow frame *skips* the segments it slept through instead of stretching them. Measured: a 250 ms hitch
  mid-alert lands on the segment that owns t=250 ms; the alert ends at 1.067 s at 30 fps against its
  1.060 s of authored segments.
* **The motors are written once per SEGMENT, not once per frame, and never more than once a frame**:
  every `SetMotorSpeeds` is a command to the device, and on HID pads (DualShock / DualSense, Bluetooth above
  all) a blocking one. One `Flush` sends every command: it skips speeds the pad already holds, holds a
  second change in a frame for the driver's `LateUpdate`, and defers a pattern's end-of-clip silence there
  so the spray's next pulse overwrites the zero instead of paying a command for it (2026-11-02). Every
  command is inside the `Haptics.MotorWrite` profiler marker.

It also stops the motors on every way the game can go quiet (play-mode exit, scene teardown, quit,
pause, focus loss, and a pad unplugged mid-pattern). A sound left playing is something you can hear; a
motor left running is not, so each of those is explicit.

**Mobile — silent, deliberately.** Pattern haptics on a phone (iOS Core Haptics, Android
`VibrationEffect`) were the one thing the plugin bought that a gamepad cannot do, and the launch platform
is PC/Steam. Rather than fake it with `Handheld.Vibrate()` — a single fixed buzz with no amplitude and no
envelope, which cannot express any of the four feels and would be worse than nothing fired once per skim
— `HapticController.PlayMobilePattern` is an empty, documented seam. The `.haptic` JSON is still built
and still handed to it: it is the only portable record of the four envelopes and it is what a future
backend consumes. **Do not delete it to tidy up an unused parameter.** A pad connected to a phone *does*
rumble, which the plugin's gamepad path did not — it was compiled out on iOS and Android entirely.

## The cadence floor (for anything that repeats a feel)

The spray is the only repeating feel, and its interval is authored on `GunSpreadProfile`
(`hapticIntervalAtRest` 0.10 s → `hapticIntervalAtMaxSpread` 0.045 s). That floor used to be justified by
the plugin's one-clip behaviour; re-derived against our own player, over a simulated 1 s hold at 60 fps:

| cadence requested | pulses | device commands | duty | distinct motor levels |
|---|---|---|---|---|
| 100 ms (rest) | 10 | 30 | 37% | 20 |
| **45 ms (max spread — shipped)** | **22** | **55** | **82%** | **44** |
| 30 ms | 33 | 60 | 100% | 54 |
| 16 ms (one frame) | 62 | 62 | 100% | **1** |
| 8 ms | 123 | 123 | 100% | **1** |

**An authored interval is a REQUEST, delivered on the next frame boundary.** At 60 fps a 45 ms request
is issued every 50 ms — which happens to be exactly the spray clip's own length, so the shipped
max-spread buzz is back-to-back whole clips with a gap of at most one frame, and **both segments of
every pulse reach the motors**. That corrects the reasoning this floor used to carry ("pulses closer
than the clip just cut each other off"): at 60 fps, at the shipped number, nothing is being cut off.

The real floor is a **cliff one frame down**. Between the clip length and a frame, duty is already
saturated at 100%, so tightening buys no more intensity — only device commands. **At one frame it stops
working entirely**: every `Play` writes segment 0 and the next `Play` overwrites it before the driver can
advance, so 62 pulses produce **one** motor level and the texture becomes a flat hum. The shipped 45 ms
sits 2.7× above that. Keep any future repeating feel at or above its own clip length, and never within
about two frames of one.

## Everything else is silent

Every other haptic call site in the codebase routes through the legacy
`HapticController.PlayHaptic(HapticType)` / `PlayConstant(...)` entry points (UI button press,
boost, overtake, elemental debuffs, AstroLeague collisions, …). Both are now **no-ops**. There are no
`HapticSource` / `HapticReceiver` components placed in any scene or prefab, so those two methods plus the
four `Play*` feels above are the *only* haptic pathways. To silence a category, you don't need to touch
its call site — it's already silent.

## Adding / changing a feel

- **Do not** add a further feel or re-enable a legacy category without a deliberate decision — the whole
  point is that the set stays legible. If you must, route it through a new dedicated method on
  `HapticController` (never through the silenced `PlayHaptic`/`PlayConstant`) and extend the gate.
- **The drift rumble** (requested 2026-10-06) is the fourth exercise and the third texture:
  `HapticController.PlayDrift(strength01)`, driven by `DriftAudioController` every
  `driftHapticInterval` (0.1 s, above the 90 ms clip) while `IsDrifting`, local human pilot only.
  Strength follows drift depth. Same fence as spray/bind: bottom priority, no busy window, so a
  skim landed mid-drift still cuts through.
- **The bind grind** (requested for the Rhino, 2026-09-25) is the third exercise and the second
  texture: `HapticController.PlayBind(strength01)`, driven by `ShieldSkimmerScaleDriver` every
  `bindHapticIntervalSeconds` (0.1 s, above the 80 ms clip — the cadence floor below) while the
  blade is inside super-shielded mass, local human pilot only. It answers "am I still stuck in
  it?" and stops the frame the blade comes free. The ENTRY into the armour reuses the punish
  thud (it is a mistake-shaped event: you hit something you cannot cut), which the grind cannot
  cut short. Same fence as the spray: one mechanic, bottom priority, no busy window.
- **Two exercises of that clause preceded it.** Both were requested explicitly, both added a
  dedicated method with the gate extended, and both are fenced to exactly one thing.
- **The spray buzz** (requested for the Sparrow, 2026-08) is the second, and the only one that is
  CONTINUOUS: `HapticController.PlaySpray(strength01)` reports how far the full-auto gun's accuracy
  has decayed while the trigger is held. Because a texture that could cut off an event would make
  the two everyday feels *less* legible, it was placed at the BOTTOM of the priority order rather
  than given a busy window — alert, punish and skim all interrupt it and it interrupts none of
  them. It is fenced to a held full-auto trigger on the local human pilot's own vessel; do not hang
  it on anything else. The driver, ramp and tuning live with the mechanic
  (`R_VesselActions/SPARROW_SPRAY_ACCURACY.md`), not here, because the strength IS the gameplay
  quantity. Note it needs a **gamepad or a device** to be judged — a bare desktop editor has no
  motors, so "I feel nothing" there is not evidence either way.
- **The alert shake** (requested for Cleave, 2026-08) was the first: a
  third feel, added via a dedicated `HapticController.PlayAlert()` with the gate extended so it
  outranks BOTH other feels for its duration (`s_alertBusyUntil` suppresses skim *and* punish) and
  is rate-limited (`AlertMinIntervalSec` 1.5 s) so it can never stack into a drone. It is fenced to
  **rare, match-changing state changes** — currently only the two Cleave milestone rungs, which fire
  at most twice per match. Do NOT hang it on anything frequent: the policy exists because haptics
  stop meaning anything once they are common.
- **The bar for the next one is unchanged, and it is high.** Both additions cleared it the same way:
  each answers a question the pilot is actively asking ("did something just change?", "how much
  accuracy have I lost?"), each is fenced to a single mechanic, and neither weakened the two
  everyday feels — the alert by being rare, the spray by being outranked. A further *everyday*
  feel, or hanging either of these on a second call site, would still be a regression.
- Tuning the skim strength floor: `SkimmerHapticsByPrismEffectSO.minStrength` (SerializeField on the asset).
- Tuning the gate cadence / clip shape: constants + `EnsureClips()` in `HapticController.cs`. These are
  intentionally hard-coded (the feature was scoped to "no per-category tables, no editor tooling"). If the
  team later wants them designer-editable, hoist them into a `HapticConfigSO` per the Config-Separation
  pattern — see Follow-ups.

## In-editor verification (a human must do this — Unity can't run headless here)

1. **Assets import clean** (three were hand-edited as YAML): open `SquirrelImpactorDataContainer`,
   `SkimmerHapticsByPrismEffect` (should show `Min Strength = 0.35`), and `VesselHapticsByPrismEffect` in
   the inspector — no "missing script" / broken-reference warnings; the Squirrel vessel container lists the
   punish effect in its Vessel Prism Effects.
2. **Skim train** (device or connected gamepad): fly the Squirrel in freestyle and skim a trail — expect a
   rapid, bright pulse train that intensifies as you thread the skimmer centre over prisms.
3. **Punish thud**: crash the vessel **body** into a prism wall — expect a single heavy low thud that cuts
   through / interrupts the skim train, and no machine-gunning (≥250 ms apart).
4. **Spray ramp** (device or connected gamepad — a bare desktop editor has no motors, so it can
   only be judged with one): fly a Sparrow and hold the fire trigger. Expect a light buzz from the
   first round, flat through the gun's ~2 s grace window, climbing in **both** strength and rate
   over the ~2 s the cone takes to open, then holding steady for the rest of the hold — including
   the accuracy blow-out past 6 s, which is deliberately not on this channel
   (`R_VesselActions/SPARROW_SPRAY_ACCURACY.md` Round 6). Release → silence; re-pull → back to the
   light end. It must be immediately distinguishable from the
   skim's bright ticks and the punish's heavy thud.
5. **Spray yields, never suppresses**: while holding fire, ram a prism with the hull — the punish
   thud must cut cleanly through the buzz rather than being drowned by it.
6. **Silence**: confirm UI taps, boost, jousts, and explosions produce **no** haptics.
7. **Setting**: toggle Haptics off (and slide Haptics level) in Settings — every feel stops / scales.
7b. **Motors always stop**: while a feel is playing, exit play mode / alt-tab away / return to the main
   menu — the pad must go quiet immediately and stay quiet. Then unplug the pad mid-alert and re-plug it:
   it must not come back still buzzing.
8. **Not for autopilot/remote**: the Menu_Main lava-lamp autopilot and remote players must not buzz —
   including a remote or AI Sparrow holding down its guns.

## Follow-ups (not blockers)

- **The skim train can flatten into a hum on any vessel.** `SkimMinIntervalSec` (30 ms) is shorter than
  the skim clip (70 ms), so a stream of entries faster than about 14 a second re-starts the clip before it
  ends and the motors hold one level instead of pulsing (against this document's own cadence rule). It was
  the Manta's minutes-long rumble. Caleb and Garrett (2026-11-11) have not felt it on the Squirrel or the
  Rhino, so it stays as it is there; revisit if a dense skim ever reads as a hum.

- **Own-trail false-positives**: the punish fires on *any* body-into-prism, including the Squirrel clipping
  its own freshly-collider-enabled trail during a tight drift. The 250 ms gate caps the rate, but if it
  feels like false punishment in play, gate it (e.g. minimum impact angle/speed, or exclude own-domain
  environment prisms). Left as-is because "clipping a prism with your body is a mistake" is the intended
  reading — decide after feeling it.
- **Config-in-SO**: gate cadence and clip envelopes are hard-coded per the minimal scope; hoist to a
  `HapticConfigSO` if designers want to tune them.
- **The alert's two representations disagree on length, and always have.** Its gamepad segments sum to
  **1060 ms** while its `.haptic` envelope and the gate's `AlertDurationSec` both run to **1200 ms**
  (measured, pre-dates the vendor swap). Harmless in the safe direction — the busy window outlives the
  rumble by 140 ms, so the alert keeps outranking the other feels until slightly after the pad stops —
  but if the alert is ever retuned, make the three agree. Deliberately left alone here: changing it
  changes a shipped feel, which does not belong on a vendor-independence branch.
- **`PlayAlert` has four call sites, not one.** This document's table and CLAUDE.md both fence the alert
  to Cleave's milestone rungs; measured, it is also called by `WildlifeLiberationController`,
  `DogFightController` and `BendsController`. Either the fence moved without the docs, or three modes
  helped themselves to it. That is a **policy** question — the "Adding / changing a feel" bar above says
  the set stays legible only while each addition is fenced to one mechanic — so it is recorded here
  rather than silently resolved in either direction.
