# Desktop mouse + keyboard for the ONE-THUMB vessels

> **The rule, in one line:** *on a vessel that flies on a single stick, the mouse **is** that
> stick — how fast you move it is how hard the vessel turns.*

`SingleStickMouseInputStrategy` is the desktop flight scheme for every hull whose transformer
sets `IsSingleStickControls`, i.e. the vessels that read `EasedLeftJoystickPosition` and nothing
else:

| Vessel | Transformer |
|---|---|
| Sparrow, Serpent, Grizzly, Termite, Falcon, Shrike | `SingleStickVesselTransformer` |
| Scarab | `ScarabVesselTransformer` (single-stick steering, its own throttle integrator) |

## 1. Why these vessels needed their own scheme

The desktop default, `KeyboardInputStrategy`, is a **dual-stick** layout: WASD and P/;/L/' are two
digital sticks, mixed through `DualStickMix` into yaw / pitch / speed / roll (`XSum` / `YSum` /
`XDiff` / `YDiff`).

A one-thumb vessel reads **none of that mix**. `SingleStickVesselTransformer` takes pitch and yaw
straight off `EasedLeftJoystickPosition`, derives the bank into the turn from the same stick's `x`,
and `ComputeThrottleTarget` ignores `XDiff` entirely because full throttle is implicit. So on those
hulls the whole right hand was dead keys, and the only steering left was four digital WASD
directions with no magnitude between *centred* and *hard over*. Playable; not aiming — on the
vessel the shooter genre is built on.

The legacy `KeyboardMouseInputStrategy` did not help and could not have: it puts the mouse on the
**right** stick, which is exactly the one these vessels do not read, and it writes raw mouse delta
into `XSum`/`YSum` as a rate rather than holding a deflection. It remains unreferenced dead code —
a known audit trap, noted in `SCARAB.md §3.1`.

## 2. The control model

The mouse hands us a **delta**; the vessel asks for a **position**, and answers with a **turn
rate**. `MouseVirtualStick.Step` is the bridge, and its model is a **proportional spring, always
on** — the spring a physical thumbstick has and a mouse does not.

Under a sustained drag of `v` px/s the deflection settles at

```
deflection = v × stickUnitsPerPixel / springPerSecond          (MouseVirtualStick.SustainedDeflection)
```

so a slow careful drag is a gentle turn and a hard sweep is a hard turn; release and it decays with
time constant `1 / springPerSecond` until the dead zone lands it on exactly centred. The step is
the **closed-form** solution of `ds/dt = v·k − spring·s` over the frame rather than a per-frame
approximation, which is what makes that curve exact at any frame rate.

Shipped numbers (`Resources/MouseFlightConfig`, `MouseFlightConfigSO`) — **playtest dials, not
measurements**, and the two are not independent, so retune them as a pair against the curve above:

| Field | Value | What it buys |
|---|---|---|
| `stickUnitsPerPixel` | 0.011 | gain (close to `DualMouseInputStrategy`'s 0.013 — roughly where a mouse sweep stops feeling like a nudge) |
| `springPerSecond` | 3.5 | 0.29 s return; full deflection at a brisk ~318 px/s sweep |
| `deadZone` | 0.02 | what actually lands the exponential on centre |

`springPerSecond = 0` is the other school of mouse flight — no spring, so a push keeps the vessel
turning until you push back (what `DualMouseInputStrategy` effectively does). One field, not a
rewrite.

### 2.1 Two defects the model had, and how they were found

Both were caught by **running the shipped math** (`MouseVirtualStickTests` are the surviving
record), and neither is visible to the obvious "does it centre, does it clamp" checks:

1. **A spring that only ran while the mouse was still.** The first cut sprang back linearly and
   only on frames with no movement — so the spring was off *whenever you were actually steering*,
   any drag at all wound up pinned at full deflection, and no stable partial turn existed anywhere.
   A knife edge is not a control.
2. **A dead zone applied to the accumulator was a RATCHET.** At 60 fps a drag under ~110 px/s adds
   less than one dead zone per frame, so the state was zeroed every frame and could never
   accumulate: slow, careful movement — precisely what aiming is made of — did nothing at all, and
   the speed needed to escape scaled with frame rate. The dead zone now applies to the **published**
   value only; the state stays honest.

General shape worth keeping: *a control curve is a claim about the steady state under continuous
input, and a test that only pokes it with an impulse cannot see the claim at all.*

## 3. The mapping

The buttons mirror the **pad**, not the keyboard, because a one-thumb vessel's abilities are
authored against the pad and the pad's naming is what `InputHintBindingMap` and the ability
lockup's control chips already speak.

| Physical control | Publishes | Raises | Sparrow | Scarab |
|---|---|---|---|---|
| Mouse move | `EasedLeftJoystickPosition`, `LeftNormalizedJoystickPosition`, `XSum`/`YSum` | — | pitch / yaw / bank | pitch / yaw / bank |
| **LMB** *or* Right Shift | `RightTriggerAnalog` | `RightStickAction`, `OnlyRight…`, `BothSticks…` | guns | throttle |
| **RMB** *or* Left Shift | `LeftTriggerAnalog` | `LeftStickAction`, `OnlyLeft…`, `BothSticks…` | skybursts | drift |
| Space | — | `Button1Action` (pad A) | Mass ability | place switch |
| B | — | `Button2Action` (pad B) | Time ability | — |
| N *or* MMB | — | `Button3Action` (pad X) | — | — |
| E | `Throttle` | `FlipAction` | — | — |

Three things about that table are load-bearing:

- **Both sources per side are OR'd into ONE boolean before edge detection.** Two independent edge
  detectors on one logical trigger raise a release the moment *either* source lets go while the
  other is still held, which reads as the ability dropping out under your finger.
- **The shift keys are kept alongside the mouse buttons on purpose.** `InputHintBindingMap` maps
  the trigger sides to `KeyLeftShift`/`KeyRightShift` on keyboard, so every ability-lockup chip on
  a one-thumb HUD keeps naming a control that genuinely fires it. A mouse-button glyph would need
  that map to answer *which of two controls do I label*, which it has no way to decide today — and
  `InputDeviceIconSetSwitcher` already flips to the keyboard set on an LMB/RMB press, so a mouse
  player sees those labels. A truthful label now beats an ambiguous one later.
- **`XDiff` publishes 0.5 and `YDiff` 0** — the NEUTRAL value every other strategy produces with no
  throttle or roll axis deflected, not "full throttle". A one-thumb vessel reads neither, so the
  only consumer is the straight-line gesture pair; publishing 1 would make this scheme raise
  `FullSpeedStraightAction` on a hull where the pad never does, and *a gesture that fires on one
  device and not another for the same vessel is worse than a gesture that fires on neither*. It
  also makes `InvertThrottle` a genuine no-op here (0.5 is its own mirror image) rather than a
  silently broken one.

**Invert Y is applied at the source**, to the stick itself, rather than to `YSum` the way
`DualStickMix` does it — because a one-thumb vessel never reads `YSum`. Its pitch
(`SingleStickVesselTransformer.Pitch`), its hull puppetry (`VesselAnimation`) and its strafing roll
(`BarrelRollController`) all read the one stick, so inverting the stick is what makes every
consumer agree about which way the player just pushed.

> ⚠ **Known, pre-existing, NOT fixed here:** because `DualStickMix` applies `InvertY` only to
> `YSum`/`YDiff` and never to the eased sticks, the **Invert Y setting is dead on every one-thumb
> vessel on gamepad, keyboard and touch**. This scheme is correct on its own; the other three are
> not. The fix is one line in `SingleStickVesselTransformer.Pitch` and `ScarabVesselTransformer.Pitch`
> — but it must exclude autopilot, because `AIPilot` writes `EasedLeftJoystickPosition` as a
> *steering command*, not as a stick reading, and inverting it would fly the bot upside down
> whenever its owner happened to have the option on. Left out of this branch as a distinct defect
> rather than folded into a controls change.

## 4. Selection and engagement

`InputController.SelectStrategy` picks this scheme when there is **no gamepad**, the device is not
handheld, dual-mouse is not engaged, a mouse exists, and the local pilot's current vessel reports
`IsSingleStickControls`. The vessel is asked **live** rather than latched: `IsSingleStickControls`
is written by the transformer in `Initialize`, long after the `InputController` exists, and a
vessel swap can change the answer mid-session — `UpdateInputStrategy` already re-asks every frame,
so the hull arriving (or changing) hands flight over on its own.

It is the **default** for those vessels rather than an opt-in gesture: the mouse is the thumb they
fly on, so a scheme behind a secret handshake would be off for everyone who never learned it.

- **Escape** disengages (and releases the cursor). Flight falls back to `KeyboardInputStrategy`,
  where WASD still drives the one stick a single-stick vessel reads — nothing is taken away.
- **A full left click** re-engages. It is the click *release*, not the press: the strategy
  snapshots live button state on activation so a held control cannot raise a phantom press, and
  snapshotting a HELD left button would instead arm a release with no matching press. Clicking
  rather than pressing means there is no held button to snapshot at hand-over at all.

`UseSingleStickMouse` also refuses for an AI or a remote replica. `Update()` already returns before
the strategy switch for those, but `SetInitialStrategy()` runs from `Initialize()` with no such
guard — and this strategy **locks the cursor** when it activates, so selecting it for a bot would
take the pointer away from a player who is not flying anything.

## 5. What else had to know

`InputDeviceType.MouseKeyboard` is the new member. Anything that switches on that enum and treats
"not gamepad" as *binary triggers, needs easing* — `VesselTransformer.GetTriggerSum` and its two
ease sites — is already correct for it. Anything that maps a device to a per-trigger override table
must name it explicitly: `R_VesselActionHandler.GetActiveOverrides` routes it to the **gamepad**
overrides, for the same reason keyboard and dual-mouse already are — it raises the pad's trigger
events.

Deliberately **not** joined: `ShieldSwipeActionExecutor.IsLocalAnalogPilot` (Gamepad or DualMouse),
which wants a true analog trigger and is a Rhino ability — not a one-thumb vessel — and
`MantaAnalogTurnBoostExecutor`, likewise gated on a real pad.

**The Scarab's throttle is binary here.** Its integrator reads `RightTriggerAnalog`, which LMB
drives to 0 or 1 — the same thing Right Shift already did on the keyboard scheme. A vessel whose
throttle depth is the whole ability wants a pad; this makes it flyable, not equivalent.

## 6. Verification

- `MouseVirtualStickTests` (edit mode) covers the control curve at four frame rates, the perimeter
  contract `BarrelRollController` and `ScarabJukeController` depend on, the release, the no-spring
  option, and both regressions in §2.1.
- The same assertions were run against the **shipped** `MouseVirtualStick.cs` compiled off-editor
  against a minimal `UnityEngine` stub, so the math in this document is measured rather than
  claimed.
- **Not verified in the editor**: strategy selection, cursor lock/release, the engagement gesture,
  and how any of it actually feels. Every number in §2 is a starting point.
