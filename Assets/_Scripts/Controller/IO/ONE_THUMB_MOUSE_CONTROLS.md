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
| **Space** | — | `Button1Action` (pad A) | Mass ability | place switch |
| **R** | — | `Button2Action` (pad B) | Time ability | — |
| Q *or* MMB | — | `Button3Action` (pad X) | — | — |
| E | `Throttle` | `FlipAction` | — | — |

**The keys live in the QWER + Space cluster**, so one resting left hand reaches every one of them
without the right hand leaving the mouse. They are assigned in **reverse priority order**: the two
mouse buttons carry the highest-priority abilities, so only two more keys are needed — Space takes
the next, R the one after. Q is last and no one-thumb vessel binds it today.

`KeyboardInputStrategy` was moved onto the same three keys (it used B / N) rather than left where
it was. That is not tidying: `ControlGlyphSetSO` authors **one `keyboardLabel` per control**, so two
desktop schemes binding different keys would make the ability chip confidently wrong for whichever
one is live — the exact failure the derived-chip design exists to prevent. R and Q also sit
directly above that scheme's own WASD left stick, so the move suits both hands.
`ControlChipBindingTests.KeyboardActionKeysStayInTheQwerSpaceCluster` holds the line.

Four things about that table are load-bearing:

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

`InputController.SelectStrategy` picks this scheme when the player is **not currently using a
gamepad**, the device is not handheld, dual-mouse is not engaged, a mouse exists, and the local
pilot's current vessel reports `IsSingleStickControls`. The vessel is asked **live** rather than latched: `IsSingleStickControls`
is written by the transformer in `Initialize`, long after the `InputController` exists, and a
vessel swap can change the answer mid-session — `UpdateInputStrategy` already re-asks every frame,
so the hull arriving (or changing) hands flight over on its own.

`UseSingleStickMouse` also refuses for an AI or a remote replica. `Update()` already returns before
the strategy switch for those, but `SetInitialStrategy()` runs from `Initialize()` with no such
guard — and this strategy **locks the cursor** when it activates, so selecting it for a bot would
take the pointer away from a player who is not flying anything.

### 4.0 "Not using a pad", never "no pad connected"

The pad gate keys on the device family the player is **actually using**
(`InputDeviceActuation`), not on `Gamepad.current != null`. It used to key on presence, and the
consequence was reported from the first Sparrow playtest: a controller left plugged in took every
frame forever, so the ability chips correctly followed the player's keyboard and mouse while the
ship ignored both, and **unplugging the pad was the only way to fly**.

Neither system was wrong on its own. `InputDeviceIconSetSwitcher` had always detected by last
meaningful actuation; `SelectStrategy` had always detected by presence. *The defect was that one
question had two implementations* — and no test can catch a second detector by calling the first.
Both now read `InputDeviceActuation`, and `InputDeviceUnificationTests` asserts as a source law
that only that one file polls raw pad controls.

Two properties of the detector are deliberate: it counts **buttons, keys and clicks — never mouse
movement** (a bumped desk must not take the ship from a pad player, and stick noise must not take
it back), and it is **sticky** — it only answers on a real actuation, so nothing thrashes frame to
frame. Picking either device back up switches within one input.

### 4.1 There is no opt-out gesture (there was; it was removed)

The first version disengaged on **Escape** and re-engaged on a left click, mirroring dual-mouse.
That was wrong twice over: Escape is already the fullscreen toggle a few lines up in
`InputController.Update` and the reflexive *give me my cursor back* key in the Editor, so one press
turned the whole scheme off for the rest of the session with nothing on screen to say so and an
undiscoverable way back — and it was redundant, because the cursor is released on pause
(`OnPaused`) and on every strategy hand-over, which covers every moment a player actually needs the
pointer.

### 4.1.1 Escape is the OVERVIEW, and the cursor is never handed back early

A mouse pilot needs the pointer for exactly one thing — the on-screen **Volume / Pause** button —
so rather than releasing the cursor and hoping they find it, that button has a key.
`OverviewGesture` (**Escape**, or the pad's **Start**) is asked by both HUDs, and each answers by
invoking *its own* volume/pause button: the key does not reimplement the overview, it presses the
button, so whatever the button is authored to do in that scene is exactly what the key does and
the two cannot drift. In `MiniGameHUD` that opens the pause panel; in `MenuMiniGameHUD` it exits
freestyle. The menu loop is therefore **click the screen to fly, Escape to come back**, and it
reads the same in a game scene.

The cursor follows from that with no special casing: it is locked while the strategy is live and
released when the overview actually opens, because both paths pause the input controller
(`PauseMenu.TogglePlayerPauseWithDelay` in a game scene, `ToggleTransition` in the menu) and
`OnPaused` unlocks it. Nothing releases it speculatively.

**Escape no longer toggles fullscreen** — that was `InputController`'s previous binding and it both
took the key and left keyboard players with no way to reach the overview. Fullscreen moved to
**F11** rather than being dropped, so a windowed build is not a trap.

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
  local pilot, no vessel, autopilot, or a two-stick hull — once per reason, as an unconditional
  **warning**. Legitimate states never reach it: a connected pad, a handheld device and engaged
  dual-mouse all return earlier in `SelectStrategy`. It also reports the first frame the scheme
  *does* take over, but that one is bring-up telemetry rather than a fault, so it sits on the
  `CSLogChannel.MouseFlight` channel (off by default, toggled in FrogletTools ▸ Toolbox ▸
  Logging). Turn it on when you need to tell "engaged" from "a pad is in use" — both are silent
  otherwise. *Loud when it fails, quiet when it works.*
- **`SingleStickMouseInputStrategy.ReportIfMouseIsSilent`** covers the other half — engaged, but
  `Mouse.current.delta` reading exactly zero for four seconds, which is what a project set to
  *Process Events In Fixed Update* would produce. A player who is flying necessarily moves the
  mouse, so silence that long is the device not reaching us rather than them sitting still.

*General shape: when a system's failure mode is that another system quietly covers for it, the
system has to say so itself — nobody downstream can tell the difference.*

### 4.3 Every one-thumb hull qualifies STRUCTURALLY, not by luck

A vessel gets this scheme because `IsSingleStickControls` is true, and that flag is written by
whichever `VesselTransformer` the vessel resolves — through
`VesselStatus.VesselTransformer` → `GetOrAdd<VesselTransformer>()` → `TryGetComponent`, i.e. **by
component order**. So a prefab carrying two transformers has its flight model, and therefore its
whole control scheme, decided by a list nobody maintains deliberately.

`Falcon.prefab` and `Shrike.prefab` carried exactly that: a `SingleStickVesselTransformer` at
index 2 and a base `VesselTransformer` at index 3. **They were not broken** — the single-stick one
won on order, and the base was inert anyway (`m_Enabled: 0`, and `Update` early-outs on
`!isActive`, which only the resolved transformer ever clears). An earlier revision of this document
claimed they might be flying the dual-stick model; that was wrong, and the correction matters,
because "correct because of the order two components happen to sit in" is a different claim from
"correct". A re-serialize or an inspector reorder would have silently handed those hulls the
dual-stick model with `IsSingleStickControls` never set — and the symptom would have been this
scheme quietly refusing them, which §4.2 exists because nobody can see.

Both duplicates are excised. The roster now reads:

| Vessel | Transformer | One-thumb |
|---|---|---|
| Sparrow, Serpent, Grizzly, Termite, Falcon, Shrike | `SingleStickVesselTransformer` | ✅ |
| Scarab | `ScarabVesselTransformer` | ✅ |
| Dolphin, Manta, Rhino, Squirrel | `VesselTransformer` | — |
| Urchin | `GunVesselTransformer` | — |

`OneThumbVesselCoverageTests` holds all three halves of that as source laws — exactly one
transformer per vessel, every rostered hull carrying a single-stick one, and no two-stick hull
quietly gaining one (which would start locking a player's cursor with nobody having decided it).
It reads the prefab TEXT rather than going through the asset database on purpose: the question is
about the serialized component list itself, and a `GetComponents` sweep reports the resolved
winner while saying nothing about the duplicate behind it.

> Unity fileIDs are **signed**, and a `&(\d+)` census regex silently skips every document with a
> negative anchor — which is how the first pass of this audit lost the Grizzly entirely and
> reported six hulls instead of seven. Match `&(-?\d+)`.

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
   button rows in the asset gained their `SPACE` and `R` labels. (`KeyB` / `KeyN` existed for one
   commit and are gone; `KeyR` replaces them and `KeyQ` was already declared.)

The Sparrow's four chips now read **LSHIFT · RSHIFT · SPACE · R**. Those labels stay truthful under
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
- `InputDeviceUnificationTests` and `OneThumbVesselCoverageTests` (edit mode) hold §4.0, §4.1.1
  and §4.3 as source laws. Both suites were **executed** against the real files outside the editor
  (compiled against NUnit stubs, driven by reflection from the project root) — 9 passed — and each
  gate was proven to FAIL by injecting its defect: the duplicate transformer restored, the
  Sparrow's single-stick transformer swapped for the base, and the pad-PRESENCE gate reinstated.
- **Not verified in the editor**: cursor lock/release across the pause and freestyle transitions,
  the device hand-over itself, the diagnostics' own wording, and how any of it actually feels.
  Every number in §2 is a starting point.
