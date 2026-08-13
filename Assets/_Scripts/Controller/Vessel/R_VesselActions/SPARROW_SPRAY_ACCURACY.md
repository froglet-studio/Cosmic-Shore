# Sparrow — Spray Accuracy (the walking gun)

> **The rule, in one line:** *hold the trigger and the cone opens — the danger zone grows and
> your hands feel it; let go for an instant and you are pin-accurate again.*

The Sparrow's cannons are a **saturation** weapon, not a marksman's rifle. The design that makes
that legible is a two-mode gun with one control:

| you do | you get |
|---|---|
| tap the trigger (≤ 0.12 s) | a perfectly accurate burst — a scalpel, at a quarter of the volume |
| hold it down | a cone that opens over ~1.4 s to a 4° cap, filled at **120 rounds/s** — nothing in it survives, and the buzz in your hands climbs the whole way |
| release and re-pull | full accuracy back, instantly. This is the "3-shot burst" the design asks for |

The cone is the *point*, not a penalty. At 120 rounds/s a widening group means a **growing
volume you are saturating**, so a held burst is easier to land on something moving than a
pin-accurate line is — right up to the cap, which sits just past where the spread would start
costing you the target you actually wanted.

---

## The three moving parts

### 1. Rate of fire — 30 → **60 volleys/s** (120 rounds/s across two muzzles)

Round 6 of the turret pass shrank the bullet's hit sphere 8× (world diameter 12 → 1.65) after
discovering nothing had ever *authored* the 12 — it fell out of the tracer mesh's ×20 z-stretch
leaking into a `SphereCollider` radius. That was the right fix, and it deliberately made the guns
tighter. `SPARROW_TURRET_STANCE.md` round 6 says so explicitly: *"the guns feel tighter is the
intended outcome, not a regression to fix by inflating the sphere again."*

This pass is the sanctioned other half of that trade: the aim forgiveness comes back as **volume
of fire × cone coverage**, not as a bigger invisible ball around each round.

### 2. The fire loops are now frame-rate independent — and this was load-bearing

Both fire loops used `await UniTask.Delay(1 / rate)`. A frame-quantized delay can never produce
more than **one volley per frame**, so the authored rate was silently `min(rate, framerate)`:

| authored rate | 60 fps | 30 fps |
|---|---|---|
| 30 volleys/s (old) | 30 ✔ (33 ms ≈ 2 frames, correct by luck) | 30 ✔ |
| 60 volleys/s (naive) | **60**, but only exactly — 16.7 ms *is* one frame | **30** ✘ — half rate |

So raising `firingRate` past ~30 without fixing the loop would have handed 60 fps players double
the fire rate (and, in Dog Fight, double the scoring rate) of 30 fps players.

Both loops now **owe fire in seconds and pay it off in whole volleys** (`owed += Time.deltaTime`,
fire `floor(owed / interval)`), capped at `MaxVolleysPerTick = 4` with the excess *dropped* rather
than carried — after a hitch the gun resumes firing, it does not discharge the stall as a burst.
At 60 volleys/s a 60 fps client fires 1 volley per frame and a 30 fps client fires 2; both put the
same rounds downrange. The cap sustains the full rate down to 15 fps.

### 3. The cone

`GunSpreadMath.HalfAngleDegrees` — flat zero through the onset window, then linear, then hard
capped:

```
half-angle(t) = clamp( (t − onset) × growth , 0 , max )
```

`GunSpreadMath.Perturb` deflects each round to a point inside that cone, sampling the deflection
as `max × u^bias`. At the shipped **bias 0.5** that is *uniform over the cone's disc*: the whole
danger zone saturates evenly rather than piling every round in the middle. (Bias 1.0 is the
authored alternative — a dense core with a thin halo, so the thing you are aiming at still soaks
most of the fire. It is one field if the even fill reads as too loose in play.)

**One roll per ROUND, not per volley** — the two muzzles scatter independently, which is what
makes the stream a widening cone rather than two widening lines.

**It does not touch `UnityEngine.Random`.** The deflection is a pure integer-hash of a per-vessel
shot serial, for two reasons: the global RNG stream is shared state that deterministic systems
seed (`Random.InitState` for the HexRace track), and a gun drawing from it 120×/s would make
their output depend on how long someone held a trigger; and a hash keeps peers that agree on the
shot count agreeing on where the shot went, which matters for the turret's locally-spawned
prisms. The serial is **monotonic across the session** and deliberately *not* reset per hold —
resetting it would make every trigger pull replay the same deflection sequence, which is a
learnable pattern rather than a stochastic cone.

Note the cap is an **angle**, so miss distance scales with range. A Sparrow at SPACE 0 shoots
~72 u and groups within ~5 u; at SPACE 10 it shoots ~645 u and groups within ~45 u. That is
correct — you are shooting nine times further.

---

## Reset semantics, and the one subtlety in them

Releasing the trigger resets accuracy **completely**, so a release-and-re-pull always buys the
whole onset window back. The reset is deferred by exactly one frame (`GunSprayAccuracy.LateUpdate`)
for one specific reason:

> Toggling the Turret Stance mid-hold makes `SparrowModeSwitchingFireSO` **stop one fire action
> and start the other, synchronously, in the same call stack**. Without the deferral that internal
> hand-off is indistinguishable from a trigger release and would hand the pilot a free accuracy
> reset for flicking stance.

`ReleaseHold()` arms the reset; a `BeginHold()` arriving in the same frame disarms it. The fire
loops run at `PreLateUpdate`, so a real release still lands before the next volley — the deferral
is invisible in play. `BeginHold` is idempotent for the same reason: taking the hold over mid-press
refreshes the profile without restarting the clock.

## The turret stance sprays too — because a turret shot IS a bullet

`SPARROW_TURRET_STANCE.md`'s parity doctrine is unchanged and this pass extends it rather than
carving an exception: *"a turret shot is a bullet — you just see a prism flying, and where the
bullet would have been destroyed the prism stays."* Spread changes where the round goes, so the
prism goes there too. The turret authors **no cone of its own** — `FullAutoBlockShootActionSO.Spread`
forwards `bulletAction.Spread`, exactly as `FireRate`, `FlightTime` and `ResolveSpeed` already do.

The deflection is composed onto the muzzle **pose** (`Quaternion.FromToRotation`), never rebuilt
with `LookRotation`: a turret prism's long axis *is* the shot, so re-referencing roll to world up
would visibly twist every prism.

A held turret burst therefore lays a **scattered volume** of permanent mass instead of a line —
which is a better wall, and the same mechanic the bullets get.

## The fourth haptic feel

The escalating buzz is a deliberate exercise of `Docs/HAPTICS.md` ▸ "Adding / changing a feel"
(dedicated method + extended gate, never the silenced legacy API). It is the game's only
**continuous** feel, which is exactly why it sits at the bottom of the priority order:

```
alert  >  punish  >  skim  >  spray
```

Everything suppresses the spray; the spray suppresses nothing. Being interruptible costs it
nothing — the next pulse is milliseconds away — and it means adding a texture did not make the two
feels the policy is built around any less legible.

Both the **strength** (0.15 → 1.0) and the **cadence** (100 ms → 45 ms) climb with the cone, so it
reads as a gun winding up rather than a constant hum. Local human pilot only: remote players, AI
dogfighters and the Menu_Main autopilot all fire and none of them may buzz your device.

## Files

| File | Role |
|---|---|
| `_Scripts/Utility/GunSpreadMath.cs` | The pure cone math — ramp, hash-sampled deflection, roll-preserving `DeflectionOf`. No Unity state, no global RNG. |
| `R_VesselActions/Data Containers/GunSpreadProfile.cs` | The authored profile (cone + haptic ramp). Serialized on the bullet action. |
| `R_VesselActions/Executors/GunSprayAccuracy.cs` | Per-vessel hold state, the spread clock, the haptic ramp, and the deferred reset. |
| `R_VesselActions/Data Containers/FullAutoActionSO.cs` | Owns `Spread`; hands the accuracy component to its executor. |
| `R_VesselActions/Data Containers/FullAutoBlockShootActionSO.cs` | Adopts `bulletAction.Spread` — the turret authors no cone. |
| `R_VesselActions/Executors/FullAutoActionExecutor.cs` | Accumulator cadence + per-round deflection for the bullets. |
| `R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs` | Same for the turret, plus the roll-preserving shot rotation. |
| `Controller/Projectiles/Gun.cs` | `FireGun(..., aimDirection)` — the gun is *handed* a direction; it owns no spread policy and rolls no dice. |
| `Controller/IO/HapticController.cs` | `PlaySpray(strength01)` + the extended gate + the buzz clip. |
| `_Scripts/Tests/Editor/GunSpreadMathTests.cs` | Ramp, cap, cone containment, pole safety, determinism, distribution, roll preservation. |
| `_SO_Assets/VesselActions/Sparrow/FullAutoAction.asset` | The shipped numbers. |
| `_Prefabs/Spacevessels/Sparrow.prefab` | `GunSprayAccuracy` executor + resized pools. |

## Tuning knobs

Everything that moves **both** fire modes lives on `FullAutoAction.asset`:

| Knob | Shipped | Effect |
|---|---|---|
| `firingRate` | **60** | Volleys/s for guns **and** turret. The single lever for volume of fire — and for the turret's permanent-mass rate. |
| `spread.onsetSeconds` | **0.12** | Grace window of perfect accuracy at the start of every pull (~7 volleys / 14 rounds). Size it to the burst length that should stay surgical. |
| `spread.growthDegreesPerSecond` | **3.2** | How fast the cone opens. Full at `onset + max/growth` ≈ **1.37 s**. |
| `spread.maxHalfAngleDegrees` | **4** | The cap. Raise it and held fire starts missing what you aimed at; drop it to 0 to disable spread entirely (sanctioned opt-out). |
| `spread.distributionBias` | **0.5** | 0.5 = uniform over the disc (even saturation). 1.0 = dense core + thin halo. |
| `spread.hapticFloor01` | **0.15** | Buzz strength before any accuracy is lost — above zero so the gun is felt from round one. |
| `spread.hapticIntervalAtRest` / `AtMaxSpread` | **0.10 / 0.045** | Pulse cadence at each end of the ramp. Keep the max-spread value above ~0.04 s: NiceVibrations holds one clip at a time, so pulses closer than the clip just cut each other off. |

Pool sizes on `Sparrow.prefab` (resized for the doubled rate — the fire rate is the only reason
they are what they are):

| Pool | was | now | why |
|---|---|---|---|
| bullets (`ProjectilePoolManager`) | 25 / 100 / 25 | **60 / 240 / 60** | 120 rounds/s × 0.3 s lifetime ≈ 36 live at once. |
| turret prisms (`BlockProjectilePoolManager`) | 40 / 200 / 90 | **80 / 400 / 180** | Anchored prisms are **never returned**, so every shot past the buffer is a fresh `Instantiate`. |

## Costs this pass takes on, deliberately

- **Turret stance now lays ~120 prisms/s** of permanent world mass (was 60), at ~240 volume/s at
  base scale before the MASS stretch. That is the documented price of "the same rate as its
  bullets", and `firingRate` remains the single lever — **do not** add a turret-only divisor,
  which is exactly the drift the shared-cadence pass closed. Judge it against the host cell's
  phase ladder in play.
- **Dog Fight pace roughly doubles.** A bullet hit scores 1 against a 120-point target, so
  doubling rounds downrange roughly halves time-to-target for a pilot who is landing shots — and
  spread cuts the other way, so the net is genuinely a play-test question. The target is authored
  (FrogletTools ▸ Game Modes ▸ End Game Conditions, `GetDogFightPointTarget`), so retuning it is
  one field and does **not** need a code change.

## In-editor verification

Scene: `MinigameDogFight` (best — it has other pilots to shoot at) or `MinigameWildlifeLiberation`.
Sparrow, fire on input 1.

> **The haptic half needs a gamepad or a device.** On a bare desktop editor there are no motors,
> so "I feel nothing" carries no information about whether the ramp is wired. Connect a gamepad
> (the `GamepadRumble` path drives Input System motors) or run on device. The **cone** is fully
> visible on desktop — the tracers fan out — so the spread mechanic can be judged without one.

1. **Tap accuracy.** Tap the trigger repeatedly. Every burst must be a tight line — no visible
   fan at all. This is the onset window; if short taps spread, `onsetSeconds` is too small.
2. **The cone opens.** Hold the trigger on a distant wall and watch the impacts: a point that
   grows into a widening circle over ~1.4 s, then **stops growing**. If it never stops, the cap
   is not being applied.
3. **Release resets.** Hold until fully open, release for a fraction of a second, re-pull. The
   first rounds of the new pull must be dead-on again.
4. **Stance flip does NOT reset.** Hold fire while flying, open the cone fully, then toggle
   Turret Stance (input 6) **without releasing the trigger**. The prisms must start laying at the
   *open* cone — if they come out in a tight line, the deferred-reset hand-off has regressed.
5. **Turret prisms scatter.** Stopped, hold fire: prisms must anchor in a scattered volume, not a
   line — and each one must still point along its own flight (no visible twist/roll on the long
   axis).
6. **Rate.** 120 rounds/s should read as a solid stream. Then **cap the editor to 30 fps**
   (Game view ▸ or `Application.targetFrameRate = 30`) and confirm the stream looks the same
   density — that is the accumulator working. Before this pass it would have halved.
7. **Haptic ramp** (gamepad/device). Hold the trigger: a light buzz from the first round that
   climbs in strength *and* rate for ~1.4 s, then holds steady at the cap. Release → silence.
8. **Haptics stay legible.** While spraying, ram a prism with the hull — the punish **thud** must
   cut cleanly through the buzz. Confirm the buzz never plays for a remote player's Sparrow, an
   AI dogfighter, or the Menu_Main autopilot.
9. **Settings.** Haptics off / level 0 in Settings → the buzz stops with everything else.
10. **No hitching under a long hold.** Both fire modes, 10+ second holds, profiler open. The pools
    were resized for this; if the turret still spikes, raise `bufferSizeTarget` / `maxAddsPerFrame`
    on the Sparrow's `BlockProjectilePoolManager` further.
11. **Console clean.** No `[FullAutoActionExecutor]` / `[FullAutoBlockShoot]` / `[PrismClock]`
    errors during a sustained hold.
12. **MPPM two-client.** Both clients see a spraying Sparrow. Turret prisms land in *approximately*
    the same places on both — see Follow-ups; exact agreement is not expected today.
13. **Asset import.** `FullAutoAction.asset` shows the new **Accuracy** foldout with the shipped
    numbers, and `Sparrow.prefab`'s `VesselActions` node has a **GunSprayAccuracy** child listed in
    `ActionExecutorRegistry._executors` (5 entries).

## Follow-ups

- **Cross-peer turret prism placement.** The deflection is deterministic in the shot serial, so
  peers agree exactly as long as their shot counts agree — but the loops run on each peer's own
  clock, so counts drift and the spread makes that drift *visible* (up to 4°) instead of
  sub-degree. This is the same open item `SPARROW_TURRET_STANCE.md` already records ("the turret's
  prism spawning is not networked at all today"); it settles when the stance becomes
  server-authoritative, not before.
- **No HUD readout.** The cone is visible in the tracers and audible in the hands, but there is no
  on-screen indicator. If one is wanted, the natural home is a ring on the **Space** ability icon
  (Pulsefire Cannons) — which makes it a live-gauge icon and pulls in the rule-9 obligations
  (`tintIconOnUpgrade = false` + a `SetAbilityUpgraded` override re-anchoring rest scales).
  Deliberately out of scope here.
- **`placementImmunitySeconds` is now doing more work again.** Round 6 noted 0.2 s was probably too
  long once the hit sphere shrank; at 120 shots/s the shot-vs-shot spacing it guards is tighter
  again. Re-judge it in play rather than assuming either direction.
- **`MaxVolleysPerTick = 4` is a code constant, not an authored field.** It only binds below
  15 fps at the shipped rate. If a rate above ~240 volleys/s is ever wanted it must move with it —
  hoist it onto `GunSpreadProfile` at that point rather than raising it blind.
