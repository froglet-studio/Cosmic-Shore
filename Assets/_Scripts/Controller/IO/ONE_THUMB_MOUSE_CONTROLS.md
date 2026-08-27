# Desktop mouse + keyboard for the ONE-THUMB vessels

> **The rule, in one line:** *on a vessel that flies on a single stick, the mouse **is** that
> stick — near centre, how fast you move it is how hard the vessel turns; sweep it out to the
> **hold annulus** and the turn holds itself with the mouse dead still.*

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
rate**. `MouseVirtualStick.Step` is the bridge, and it has **two regimes**:

| Where the stick is | What it is | Why |
|---|---|---|
| inside `holdInner` (0.88) | a **rate** stick: spring live, deflection = `v · k / spring` | mouse speed is turn rate, and letting go straightens the vessel — the regime aiming and every ordinary flick live in |
| beyond `holdOuter` (0.97), **held there for `holdEngageSeconds` (0.25 s)** — the **annulus** | a **position** stick: spring exactly **0** | a *sustained* push parks it, and the vessel keeps turning **with the mouse still** |
| between them | the spring smoothsteps away | no step in feel, and no oscillation: the spring only ever gets *stronger* as the stick falls inward, so drift is monotone and the only stable places are centred and the annulus |

### Why the annulus exists

The scheme shipped first as a **pure spring**, and a pure spring makes deflection a function of
mouse *speed* — so holding the vessel hard over costs mouse travel **for as long as the turn
lasts**. Measured through the shipped code, driving both models with the same controller (push only
when the stick sags below the annulus):

| holding hard over for | pure spring | hold annulus |
|---|---|---|
| 3 s | 917 px | **228 px** |
| 6 s | 1,824 px | **228 px** |
| 12 s | 3,633 px | **228 px** |

The annulus cost is a **constant** — it is one sweep, and the rest is free. You run out of desk
long before the vessel runs out of turn otherwise. *A control curve can be exactly right and still
be unflyable because the DESK runs out* — which is why essentially every shipped mouse-flight game
uses a bounded cursor rather than a rate stick: Freelancer's clamped reticle, Elite Dangerous'
mouse widget (with its own optional decay), War Thunder's mouse aim, X4, Star Wars: Squadrons.
This is that model, with the spring kept live under `holdInner` so an aiming twitch still undoes
itself instead of permanently offsetting the heading.

**Near-centre feel is byte-for-byte what it was before the annulus**, because the gain and spring
are unchanged — only the band was added on top:

| flick (0.15 s) | deflection | vs pure spring | Sparrow turn | then |
|---|---|---|---|---|
| 30 px | 0.257 | identical | 7 °/s | centres |
| 60 px | 0.513 | identical | 26 °/s | centres |
| 100 px | 0.856 | identical | 67 °/s | centres |
| 120 px + | 1.000 | identical | 86 °/s | **holds** |

`EscapeSpeed` (**280 px/s**) is the sustained-drag threshold; a brief flick can cross it without
committing, because it does not last long enough to climb past `holdOuter` against the spring.
What commits is **saturating the stick** — about 120 px of travel — after which the turn holds
until you pull back.

**Committing takes TIME, not just distance**, and that gate is load-bearing rather than polish.
Hard over is only ~91 px at this gain, so *every* brisk aiming flick saturates the stick — an
annulus that engaged on contact latched on the first one and locked the vessel into a spin the
player never asked for. Position cannot tell a flick from a hold; only time can. The spring stays
at **full strength** for the whole dwell window, so staying out past `holdInner` means the player is
still pushing:

| gesture | result |
|---|---|
| any flick (0.15 s), 60–600 px | returns to centre — **identical to the pre-annulus scheme** |
| 500 px/s for 0.3 s | returns to centre |
| 500 px/s for 0.8 s | **parks in the annulus** |

Measured against the pre-branch code over 20,000 frames of a simulated hand, the worst divergence
is **0.035 of a stick unit**, and every bit of it is above `holdInner`. Without the dwell gate that
figure was **0.9995**, first diverging at frame 37 — the two schemes were simply different controls.

Shipped numbers (`Resources/MouseFlightConfig`, `MouseFlightConfigSO`) — **playtest dials, not
measurements**, and they are not independent:

| Field | Value | What it buys |
|---|---|---|
| `stickUnitsPerPixel` | 0.011 | gain. Reciprocal: **91 px** centre → hard over. **This is the dial a player reads as "responsiveness"** — see §2.1 #3 |
| `springPerSecond` | 3.5 | 0.29 s return inside the band |
| `deadZone` | 0.02 | what actually lands the exponential on centre |
| `holdInnerRadius` | 0.88 | where the spring starts fading — everything below is exactly the old scheme |
| `holdOuterRadius` | 0.97 | the annulus. **1 disables it** and restores the pure spring bit for bit |
| `holdEngageSeconds` | 0.25 | dwell before the spring lets go — what separates a flick from a hold. 0 engages on contact, which is the defect in §2.1 #4 |

No vessel turn rate changed: the Sparrow still turns at its authored 80 °/s scaler and still
triples it while stopped. Only the desk cost of *holding* that turn changed.

The step is the **closed-form** solution of `ds/dt = v·k − spring·s` over the frame rather than a
per-frame approximation, which is what makes the near-centre curve exact at any frame rate. The
spring *rate* is sampled from the radius at the start of the frame — the one approximation left,
and worth nothing a player could feel.

### 2.1 Six defects the model had, and how they were found

All four were caught by **running the shipped math** (`MouseVirtualStickTests` are the surviving
record), and none is visible to the obvious "does it centre, does it clamp" checks:

1. **A spring that only ran while the mouse was still.** The first cut sprang back linearly and
   only on frames with no movement — so the spring was off *whenever you were actually steering*,
   any drag at all wound up pinned at full deflection, and no stable partial turn existed anywhere.
   A knife edge is not a control.
2. **A dead zone applied to the accumulator was a RATCHET.** A 60 fps frame whose drag adds less
   than the dead zone gets zeroed every frame and can never accumulate: slow, careful movement —
   precisely what aiming is made of — did nothing at all, and the speed needed to escape scaled
   with frame rate. The dead zone now applies to the **published** value only.
3. **Tuning the steady state while never measuring the transient.** The annulus first shipped with
   the gain and spring *lowered* to 0.0045 / 1.5, picked so the annulus sat a "comfortable sweep"
   out. The reasoning checked the sustained curve — `v · k / spring`, near enough identical at 318
   vs 333 px/s for full deflection — and concluded feel was preserved. It was not: a **100 px flick
   went from 0.86 deflection to 0.40**, i.e. 67 °/s to 17 °/s on the Sparrow, and the scheme was
   reported as *not working at all*. **A control curve is a claim about the steady state; a flick is
   a claim about the transient, and a player judges responsiveness by the transient.** It is the
   exact mirror of #1's lesson, and it was made by someone who had just written that lesson down.
   `AFlickMustTurnTheVesselHard` is the guard.
   Its corollary: the fix for "holding costs too much travel" was **never** to reduce the gain —
   the complaint was *too much movement*, so the gain could only ever go up or stay.
4. **The annulus latched on ordinary flicks.** With the gain restored, hard over is ~91 px, so
   every brisk aiming flick saturates the stick — and an annulus keyed on POSITION alone engaged on
   the first one, leaving the vessel in a spin. Reported as *"the mouse movement does not fly the
   vessel"*, which is what an uncommanded permanent turn looks like from the cockpit. It was found
   by **measuring the shipped code against the pre-branch code** over a simulated hand rather than
   by reading either: worst divergence 0.9995 of a stick unit, first at frame 37. *When a change is
   meant to be additive, diff it against what it replaced under a realistic input stream — "it only
   affects the top of the range" is a claim, and the range is where the player spends their time.*
   The fix is the dwell gate; `NoFlickOfAnySizeMayCommitToAHeldTurn` is the guard.
5. **The widget vanished for a reason a desktop player could not connect to it.** It honoured
   `GameSetting.JoystickVisualsEnabled` — the setting that governs the TOUCH thumb rings — so a
   control the player had never touched, on a device they were not using, silently removed the
   mouse reticle. It now reads only its own `showWidget`. *A setting's SCOPE is part of its
   meaning, and inheriting one because it is nearby is how a UI acquires spooky action.*
6. **The widget could take flight input down with it.** `MouseFlightWidget.Report` was called from
   the middle of `Publish()`, above every `inputStatus` write, so any throw inside a **display**
   would have silently skipped every remaining input write and left the vessel dead with the
   reticle still on screen. It is now dispatched dead last and isolated, one fault retiring it for
   the session — the doctrine `ImpactorBase.RunEffectIsolated` already states for effect dispatch:
   *a picture must never be able to take down the thing it draws.*

General shape worth keeping: *a control curve is a claim about the steady state under continuous
input, and a test that only pokes it with an impulse cannot see the claim at all.* And its sequel,
learned the hard way: *the reverse is equally true, and a scheme needs both measurements before
either number moves.*

### 2.2 The stick is drawn, and that is not decoration

`MouseFlightWidget` — a centred reticle with the annulus drawn as a ring around the rim, plus a
dead-zone marker and a knob on a needle. The band and rim brighten and the knob grows the moment
the stick parks in the annulus.

It is load-bearing because the two regimes **fly completely differently and nothing else on screen
tells them apart**: inside the annulus the turn holds itself, a hair outside it the spring is
already pulling you back. Every shipped bounded-cursor scheme draws its stick for this reason. It
also answers this scheme's older failure mode (§4.2): the mouse path can decline silently while the
vessel still flies on WASD, and a visible stick makes "am I on the mouse?" a glance rather than a
bug report.

**Do not tidy `MouseFlightWidget.Ensure`.** Its body is empirically known-good and was broken once
by an "improvement": dropping the explicit `typeof(CanvasRenderer)` to lean on `[RequireComponent]`,
and moving the graphic from the GameObject constructor to an `AddComponent` after parenting — a
tidier construction order, reasoned from how `Graphic.OnEnable` caches its canvas, which made the
widget stop drawing entirely. It is restored verbatim and now self-checks at install
(`VerifyInstall` names a missing CanvasRenderer / Canvas ancestor / disabled graphic, once).
*When something works and you cannot run it, do not refactor it for elegance.*

Mechanics: generated geometry, never sprited (the ring radii are live functions of the hold band,
so art would need re-exporting every time a dial moved), one mesh so the whole widget is one draw
call, antialiasing baked in as zero-alpha feather rings because a canvas gives a generated circle
none — the same call `TrapezoidGraphic` makes. It **self-installs** on first report and auto-hides
when the reports stop, so pause, alt-tab, a vessel swap onto a two-stick hull and a scene load all
put it away with nothing to remember to call (the `VesselSpeedTunnel` / `PrismOcclusionCorridor`
driver precedent). It draws in the config's `widgetColor`, neutral by default: **domain colour
means TEAM everywhere else in the game** (`Docs/PALETTE.md`), so an instrument wearing one is
making a claim it does not mean. `showWidget` is its **only** switch — see §2.1 #5. It respects the existing **joystick-visuals** setting, since that
is exactly the setting this is, and `showWidget` on the config turns it off for capture.

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

- `MouseVirtualStickTests` (edit mode, 23 tests) covers both regimes: the near-centre control
  curve at four frame rates, the annulus holding with zero input for ten seconds, `EscapeSpeed`
  separating a stable partial turn from a committed one, the perimeter contract
  `BarrelRollController` and `ScarabJukeController` depend on, that a long sweep banks no
  deflection the player must unwind, that `holdOuter = 1` reproduces the pure spring **bit for
  bit**, the flick response (§2.1 #3), the dwell gate in both directions (§2.1 #4), and every
  regression in §2.1.
- Every one of those assertions was **executed** against the **shipped** `MouseVirtualStick.cs`,
  `MouseFlightConfigSO.cs` and `MouseFlightWidget.cs`, compiled off-editor (dotnet 8 / Roslyn)
  against a minimal `UnityEngine` stub and driven by reflection — 23 passed. Every number in §2 is
  measured from that run, not claimed.
- The shipped integrator was **diffed against the pre-branch one** over 20,000 frames of a
  simulated hand at the shipped config: worst divergence 0.035 of a stick unit, all of it above
  `holdInner`. That measurement is what found §2.1 #4, and it is the check to repeat before any
  future change claims to be additive.
- The scheme also now reports a dead flight path itself, unconditionally and once
  (`WatchForDeadFlight`): after 3 s active with nothing published it names which link is dead — no
  delta arriving, delta under the dead zone, or delta arriving that the integrator will not
  accumulate. A diagnostic behind a channel you must enable first is one nobody has when the fault
  happens.
- `MouseFlightWidget`'s mesh was **rasterised** from the shipped `OnPopulateMesh` at three stick
  positions to confirm the rings land on the radii the config asks for (dead zone, annulus inner
  edge at 0.9, rim at 1.0) and that the held state is unmistakable.
- `ControlChipBindingTests` (edit mode) covers §6's chain against the shipped
  `Resources/ControlGlyphSet`.
- `InputDeviceUnificationTests` and `OneThumbVesselCoverageTests` (edit mode) hold §4.0, §4.1.1
  and §4.3 as source laws. Both suites were **executed** against the real files outside the editor
  (compiled against NUnit stubs, driven by reflection from the project root) — 9 passed — and each
  gate was proven to FAIL by injecting its defect: the duplicate transformer restored, the
  Sparrow's single-stick transformer swapped for the base, and the pad-PRESENCE gate reinstated.
- **Not verified in the editor**: cursor lock/release across the pause and freestyle transitions,
  the device hand-over itself, the widget's on-screen size and legibility over live gameplay, the
  diagnostics' own wording, and how any of it actually feels. Every number in §2 is a starting
  point — see §2's tables for which dial moves which property.
