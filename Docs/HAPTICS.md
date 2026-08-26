# Haptics — the two-feel policy (+ one rare alert, + one held-trigger texture)

Cosmic Shore ships **two everyday haptic feels**, both **local-human-pilot-only**, plus **one
rare alert** reserved for match-changing events and **one continuous texture** fenced to a held
full-auto trigger. Everything else
is deliberately silent. This is a design decision, not an omission: minimal, legible haptics that
never fight each other read as *intentional*; a buzz on every UI tap, drift, boost, joust, and
explosion reads as noise. Keep it this way — see "Adding/changing a feel" before touching it.

| Feel | What it is | Fires on |
|---|---|---|
| **Skim pulse** (reward) | Short (~70 ms), bright, sharp transient at high haptic frequency. Strength scales with how close the prism passed to the skimmer centre. Many in sequence read as a rapid, continuously rewarding pulse train. | Each prism entering a skimmer (Squirrel etc.) |
| **Punish thud** (mistake) | Short (~200 ms), heavy, **low**-frequency thud — the deliberate opposite of the bright skim. | The vessel **body** slamming a prism |
| **Alert shake** (event) | Long (~1.2 s) hard **rattle** — full-amplitude sawtooth at mid frequency, both gamepad motors out of phase. Unmistakably neither of the above, and long enough to read as "something happened" rather than "you hit something". | Ribcage's progress-milestone rungs (25% / 50% of the win target) — **nothing else** |
| **Spray buzz** (state) | Short (~50 ms) **mid**-frequency buzz with no transient (skim's signature) and both motors together (which is what reads as a buzz rather than a tick or a rumble). Repeats while the trigger is down, climbing in **both** strength (0.15 → 1.0) and cadence (100 ms → 45 ms) as the gun's accuracy decays, and holding flat once the cone reaches its sustainable cap — both channels are at their ceiling there, so a longer hold has nothing worse left to say. | Holding the Sparrow's full-auto trigger — bullets **or** turret stance. **Nothing else** |

**Priority, top to bottom: alert > punish > skim > spray.** The spray is the game's only
*continuous* feel and therefore the only one that sits below skim: everything suppresses it and
it suppresses nothing. Being interruptible costs it nothing (the next pulse is milliseconds
away) and it is what keeps the two feels the policy is built around fully legible — a thud still
cuts cleanly through a held burst.

## Where it lives

| Role | File |
|---|---|
| Policy + gate + runtime clip factory | `_Scripts/Controller/IO/HapticController.cs` (`CosmicShore.Gameplay.HapticController`) |
| Skim hook (proximity-scaled) | `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerHapticsByPrismEffectSO.cs` |
| Punish hook | `_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Prism Effects/VesselHapticsByPrismEffectSO.cs` |
| Skimmer sphere radius (for proximity) | `_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs` (`SphereWorldRadius`) |
| Spray ramp driver (accuracy decay → strength + cadence) | `_Scripts/Controller/Vessel/R_VesselActions/Executors/GunSprayAccuracy.cs` (`DriveHaptics`); tuning on `GunSpreadProfile`, authored on `FullAutoAction.asset`. See `R_VesselActions/SPARROW_SPRAY_ACCURACY.md` |
| Plugin | `Assets/NiceVibrations/` (Lofelt NiceVibrations) |

The two feels are ordinary **impact-effect SOs** wired into the standard effect containers, same as
every other `SkimmerPrismEffectSO` / `VesselPrismEffectSO`:

- **Skim**: `SkimmerHapticsByPrismEffect.asset` → each skimmer's `SkimmerImpactorDataContainerSO.SkimmerPrismEffects`
  (fired from `SkimmerImpactor.AcceptImpactee`, prism case). Wired into the Squirrel / Manta-overcharge /
  Rhino-forcefield skimmer containers.
- **Punish**: `VesselHapticsByPrismEffect.asset` → each vessel's `VesselImpactorDataContainerSO.VesselPrismEffects`
  (fired from `VesselImpactor.AcceptImpactee`, prism case). Wired into all six playable vessel containers.

Both effects gate on `status.IsLocalUser && !status.AutoPilotEnabled` — remote players and AI/autopilot
(including the Menu_Main lava-lamp) never buzz this device. (The pre-existing `HapticSpec` helper only
checked `AutoPilotEnabled`, which leaked remote players' haptics; these effects no longer use it.)

## The gate (why it exists)

NiceVibrations holds **one loaded clip at a time** — every `HapticController.Load()` evicts whatever is
playing. So a tiny priority/rate-limit gate in `HapticController` arbitrates the feels with a handful of
timestamps (no metering, no per-category tables):

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

The clips are generated **once at runtime** as `.haptic` JSON (iOS/Android) **and** a `GamepadRumble`
(gamepads), then reloaded per pulse via `HapticController.Load(byte[] json, GamepadRumble rumble)` — the
same approach NiceVibrations' own `HapticPatterns` use, so iOS, Android, and gamepads all work with one
code path. The JSON matches the plugin's `nv-*-template.txt` schema; decimal points are hard-coded so the
strings are locale-independent. Skim = high frequency + high-frequency motor (bright); punish = zero
frequency + low-frequency motor (heavy). Proximity strength is applied via `clipLevel` after `Load`,
which scales both the iOS clip amplitude and the gamepad motor speeds.

`GameSetting.HapticsEnabled` / `HapticsLevel` are honoured on every play (`HapticsLevel` becomes the
NiceVibrations `outputLevel`); disabled or zero-level → nothing plays.

## Everything else is silent

Every other haptic call site in the codebase routes through the legacy
`HapticController.PlayHaptic(HapticType)` / `PlayConstant(...)` entry points (UI button press, drift,
boost, overtake, elemental debuffs, AstroLeague collisions, …). Both are now **no-ops**. There are no
`HapticSource` / `HapticReceiver` components placed in any scene or prefab, so those two methods plus the
four `Play*` feels above are the *only* haptic pathways. To silence a category, you don't need to touch
its call site — it's already silent.

## Adding / changing a feel

- **Do not** add a further feel or re-enable a legacy category without a deliberate decision — the whole
  point is that the set stays legible. If you must, route it through a new dedicated method on
  `HapticController` (never through the silenced `PlayHaptic`/`PlayConstant`) and extend the gate.
- **Two exercises of that clause exist so far.** Both were requested explicitly, both added a
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
- **The alert shake** (requested for Ribcage, 2026-08) was the first: a
  third feel, added via a dedicated `HapticController.PlayAlert()` with the gate extended so it
  outranks BOTH other feels for its duration (`s_alertBusyUntil` suppresses skim *and* punish) and
  is rate-limited (`AlertMinIntervalSec` 1.5 s) so it can never stack into a drone. It is fenced to
  **rare, match-changing state changes** — currently only the two Ribcage milestone rungs, which fire
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
6. **Silence**: confirm UI taps, boost, drift, jousts, and explosions produce **no** haptics.
7. **Setting**: toggle Haptics off (and slide Haptics level) in Settings — every feel stops / scales.
8. **Not for autopilot/remote**: the Menu_Main lava-lamp autopilot and remote players must not buzz —
   including a remote or AI Sparrow holding down its guns.

## Follow-ups (not blockers)

- **Own-trail false-positives**: the punish fires on *any* body-into-prism, including the Squirrel clipping
  its own freshly-collider-enabled trail during a tight drift. The 250 ms gate caps the rate, but if it
  feels like false punishment in play, gate it (e.g. minimum impact angle/speed, or exclude own-domain
  environment prisms). Left as-is because "clipping a prism with your body is a mistake" is the intended
  reading — decide after feeling it.
- **Config-in-SO**: gate cadence and clip envelopes are hard-coded per the minimal scope; hoist to a
  `HapticConfigSO` if designers want to tune them.
