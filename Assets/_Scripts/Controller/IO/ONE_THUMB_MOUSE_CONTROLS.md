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

## 4. Selection, and why it is silent when it fails

`InputController.SelectStrategy` picks this scheme when there is **no gamepad**, the device is not
handheld, dual-mouse is not engaged, a mouse exists, and the local pilot's current vessel reports
`IsSingleStickControls`. The vessel is asked **live** rather than latched: `IsSingleStickControls`
is written by the transformer in `Initialize`, long after the `InputController` exists, and a
vessel swap can change the answer mid-session — `UpdateInputStrategy` already re-asks every frame,
so the hull arriving (or changing) hands flight over on its own.

`UseSingleStickMouse` also refuses for an AI or a remote replica. `Update()` already returns before
the strategy switch for those, but `SetInitialStrategy()` runs from `Initialize()` with no such
guard — and this strategy **locks the cursor** when it activates, so selecting it for a bot would
take the pointer away from a player who is not flying anything.

### 4.1 There is no opt-out gesture (there was; it was removed)

The first version disengaged on **Escape** and re-engaged on a left click, mirroring dual-mouse.
That was wrong twice over: Escape is already the fullscreen toggle a few lines up in
`InputController.Update` and the reflexive *give me my cursor back* key in the Editor, so one press
turned the whole scheme off for the rest of the session with nothing on screen to say so and an
undiscoverable way back — and it was redundant, because the cursor is released on pause
(`OnPaused`) and on every strategy hand-over, which covers every moment a player actually needs the
pointer.

### 4.2 The failure mode is SILENCE, so it reports itself

This is the finding worth carrying. When the scheme does not engage, the player is left on
`KeyboardInputStrategy` — which **still steers a one-thumb hull off WASD and still fires every
ability off the same keys**, because the two schemes share their button half. So "not engaged" and
"broken" are indistinguishable on screen, and the first playtest report was exactly that: *"I found
keys that used my abilities, but the mouse did not fly the vessel"*, with nothing in the console to
say which of five things had happened.

Two warn-once diagnostics close that, in the shape `PrismOcclusionDiagnostics` and
`VesselVisionDiagnostics` already use for a platform system that can silently fail to engage:

- **`MouseFlightDiagnostics`** names the reason `UseSingleStickMouse` declined — no mouse, not the
  local pilot, no vessel, autopilot, or a two-stick hull — once per reason, and logs the first
  frame the scheme *does* take over. Legitimate states never reach it: a connected pad, a handheld
  device and engaged dual-mouse all return earlier in `SelectStrategy`.
- **`SingleStickMouseInputStrategy.ReportIfMouseIsSilent`** covers the other half — engaged, but
  `Mouse.current.delta` reading exactly zero for four seconds, which is what a project set to
  *Process Events In Fixed Update* would produce. A player who is flying necessarily moves the
  mouse, so silence that long is the device not reaching us rather than them sitting still.

*General shape: when a system's failure mode is that another system quietly covers for it, the
system has to say so itself — nobody downstream can tell the difference.*

### 4.3 Known: Falcon and Shrike carry TWO transformers

`Falcon.prefab` and `Shrike.prefab` each have **both** a base `VesselTransformer` and a
`SingleStickVesselTransformer` component. `VesselStatus.VesselTransformer` resolves through
`GetOrAdd<VesselTransformer>()` → `TryGetComponent`, which returns whichever comes first in the
component list — so if it is the base one, those vessels fly the dual-stick model and never set
`IsSingleStickControls`, and mouse flight will refuse them (with `NotSingleStick` in the console).
Sparrow, Serpent, Grizzly and Termite are clean; the Scarab has its own transformer. Not fixed
here: both are unplayable *Planned* vessels, and removing a component from a prefab means editing
the component block **and** the GameObject's `m_Component` list, which is not worth the risk on a
controls branch.

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

## 6. The control chips were blank, and that was a separate, older bug

The same playtest reported *"no glyphs to label the abilities"*. That is not this scheme's doing —
**no vessel has ever shown a keyboard control chip**, on any input device, for two independent
reasons:

1. **The lookup asked for an address the asset does not use.** `InputHintBindingMap.BindingFor`
   answers a keyboard query with `KeyLeftShift` / `KeyRightShift`, while `Resources/ControlGlyphSet`
   authors the `keyboardLabel` on the **pad** rows (`PadLeftTrigger` / `PadRightTrigger`) — because
   one `Glyph` entry deliberately carries *both* representations of one logical control. `For()`
   found nothing and drew nothing. Fixed with `InputHintBindingMap.Canonical`, a one-way keyboard →
   pad alias that `ControlGlyphSetSO.For` falls back to *after* an exact match. One authored row
   per control stays the rule, so a label and its glyph cannot drift apart. Returning a pad-keyed
   entry to a keyboard player is safe: `AbilityLockupView.RefreshChip` picks `padGlyph` or
   `keyboardLabel` from the **device**, never from which binding matched.
2. **Half the row had no keyboard control at all.** The map had keyboard entries only for the two
   trigger sides, so every ability bound to a pad face button — the Sparrow's Mass and Time —
   returned `None` and was honestly, permanently blank. `KeySpace` / `KeyB` / `KeyN` are now mapped
   to `Button1/2/3Action`, matching what both desktop strategies actually raise, and the two face-
   button rows in the asset gained their `SPACE` and `B` labels.

The Sparrow's four chips now read **LSHIFT · RSHIFT · SPACE · B**. Those labels stay truthful under
mouse flight because this scheme honours the shift keys alongside LMB/RMB — see §3.

`ControlChipBindingTests` asserts the whole chain against the **shipped** asset, which is where the
break lived: neither the code nor the asset was wrong on its own.

> **Follow-up, not done here:** a mouse player's most natural controls are LMB/RMB, and the chip
> still says LSHIFT/RSHIFT. Labelling the mouse needs `AbilityLockupView.SetControlDevice`'s
> boolean to become three-valued (pad / keyboard / mouse) and `InputHintBindingMap` to answer
> "which of two controls do I label" — a real design question, not a wiring gap. Truthful now beats
> ambiguous later.

## 7. Verification

- `MouseVirtualStickTests` (edit mode) covers the control curve at four frame rates, the perimeter
  contract `BarrelRollController` and `ScarabJukeController` depend on, the release, the no-spring
  option, and both regressions in §2.1.
- The same assertions were run against the **shipped** `MouseVirtualStick.cs` compiled off-editor
  against a minimal `UnityEngine` stub, so the math in this document is measured rather than
  claimed.
- `ControlChipBindingTests` (edit mode) covers §6's chain against the shipped
  `Resources/ControlGlyphSet`.
- **Not verified in the editor**: strategy selection, cursor lock/release, the diagnostics' own
  wording, and how any of it actually feels. Every number in §2 is a starting point.
