# The rear view (look-back camera)

**One line:** the gameplay camera flips to the mirror of its own follow offset — the same
distance *ahead* of the vessel that it normally sits behind it — and keeps looking at the ship,
so the pilot sees their own nose against whatever is chasing them. Toggled by **C** on the
keyboard or **LB + RB together** on a pad. It replaces the picture-in-picture rear view.

---

## 1. What it replaces, and why that thing had to go

The old rear view was `Pip` + `PipUI` + `PipCamera.prefab`: a **second camera** bolted to the
hull at a hardcoded `(0, 0, 25)` with a 120° field of view, rendering into a shared
`PipRenderTexture`, shown in a 300×150 corner panel. Eight of eleven hulls carry one (Falcon,
Grizzly, Manta, Serpent, Shrike, Squirrel, Termite, Urchin).

Four things were wrong with it, and only the first is a matter of taste:

1. **It was a postage stamp.** A rear view exists so a pilot can react to what is behind them;
   at 300×150 in a corner, at a distance no vessel authored, it is decoration.
2. **The shared render texture made "who owns the panel" a rule to enforce.** One texture, one
   panel, so exactly one vessel in a match may drive it — which is why `Pip` carries an
   identity-guarded static owner, a local-pilot test, and a long comment about why it must not
   ask `AutoPilotEnabled`. All of that machinery exists to manage a resource the feature only
   needed because it was a second camera.
3. **Its frame was a lie.** `Pip.prefab`'s `border` points at a texture guid no asset carries any
   more, and a `RawImage` with no texture draws a **solid quad in its own tint** — so switching
   the panel on painted a 780×400 navy rectangle over half the screen. `PipUI` already carries a
   runtime guard that switches such a graphic off and names it.
4. **A second camera sits outside every camera-shaped platform system at once.** See §3.

---

## 2. What it does now

`VesselRearView` (`Assets/_Scripts/Utility/VesselRearView.cs`) is a static driver in the shape of
`VesselSpeedTunnel` and `PrismOcclusionCorridor`. When engaged, `CustomCameraController` poses
itself from `EffectiveOffset` — the authored `followOffset` with **z mirrored** — instead of
`followOffset` itself. The controller's existing look-at-the-target rotation then points the
camera back down the ship's forward axis for free.

| vessel | authored offset | rear vantage |
|---|---|---|
| Urchin | `(0, 0.83, −6.67)` | `(0, 0.83, +6.67)` |
| Squirrel | `(0, 0, −17)` | `(0, 0, +17)` |
| Dolphin | `(0, 0, −20)` | `(0, 0, +20)` |
| Manta | `(0, 0, −30)` | `(0, 0, +30)` |
| Sparrow | `(0, 10, −50)` | `(0, 10, +50)` |
| Scarab | `(0, 0, −50)` | `(0, 0, +50)` |
| Rhino | `(0, 0, −120)` | `(0, 0, +120)` |
| Serpent | `(0, 0, −250)` | `(0, 0, +250)` |

**Only z is mirrored — never x, never y.** The Sparrow rides high and behind at `y = +10`;
mirroring the whole vector would put its rear camera *under* the ship, a vantage no
`CameraSettingsSO` ever described. Mirroring z alone is exactly "the same distance, the same
height, the other side", which is what the feature was asked for.

The flip is a **cut, not a sweep**. The two vantages are `2 × ` the follow distance apart — 34
units on a Squirrel, 500 on a Serpent — and a dynamic-mode rig asked to travel that would
`SmoothDamp` straight through the ship. Every flip calls `SnapToTarget`.

---

## 3. Why there is no second camera

The rear view is the **same rig, read from the other side**. That is not only tidier; a second
live `Camera` would fall outside four systems at once, each silently:

| system | what it does | what a second camera would cost |
|---|---|---|
| Speed tunnel (`Docs/SPEED_TUNNEL.md`) | resolves `CameraManager`'s **active controller** and narrows its FOV with speed | keeps narrowing the camera the player is no longer looking through |
| `CameraManager.ApplyCameraGraphicsSettings` | pushes the player's FOV + anti-aliasing onto its **three managed cameras** | the rear view ignores the graphics settings panel |
| `ThemeManagerData.SetBackgroundColor` | applied **per camera** | wrong skybox/background in the rear view |
| `Camera.main` | returns the **first ENABLED camera tagged MainCamera** | two live gameplay cameras is a coin toss for everything that asks |

Reusing the rig inherits all four for free. This is the same argument
`CameraManager.BeginWindowedPlayerCamera` already records for the mode preview: *use the real
gameplay rig, because the platform laws are already bound to it.*

**The prism occlusion corridor needs no special handling and is the reason the view is readable.**
It is a camera-to-vessel corridor whose camera end is read on the GPU from
`_WorldSpaceCameraPos` (`Docs/PRISM_ANIMATION.md` §4.7), so it follows the camera to the front of
the ship and dissolves whatever the pilot is flying *into* while they are looking backwards.

---

## 4. The mirror is applied at the point of use, never written into the offset

`CustomCameraController.RearView` is a **flag**; `_followOffset` is never touched. This is the
load-bearing decision in the whole feature.

Several systems legitimately write that offset while the player is flying:

- the Manta/Rhino zoom-out abilities (`ZoomOutActionExecutor`, `CameraZoomFollowScaleProvider`,
  `ZoomOutAction`) call `SetCameraDistance`,
- adaptive zoom rides `NeutralOffsetZ`,
- the skimmer's camera-scaling prism effect scales the follow distance,
- a mid-flight vessel swap re-applies the new hull's `CameraSettingsSO` through
  `VesselCameraCustomizer.Configure`.

Had the toggle written a mirrored offset instead, **the first of those to fire would have written
a negative z back and silently dropped the pilot out of the rear view**, with nothing on screen or
in the console to explain it. With the mirror at the point of use, every one of them keeps working
and the rear view tracks them live — a look-back during a Rhino zoom-out is 120 units ahead if the
forward camera would have been 120 behind, and follows the zoom in real time.

`RearViewLawTests.RearViewFollowsALiveZoomInsteadOfFightingIt` is that property, asserted.

---

## 5. The gesture

`RearViewGesture` (`Assets/_Scripts/Controller/IO/RearViewGesture.cs`) is the sibling of
`OverviewGesture` and exists for the same reason: the view a pilot gets is a property of the
**platform**, not of a vessel or a mode, so the gesture must mean one thing everywhere and there
must be exactly one thing to read when asking what it is bound to.

```
keyboard : C
gamepad  : both shoulders down, and at least one of them going down THIS frame
```

**The pad chord is deliberately stateless.** That predicate is an exact rising edge whichever
button the player presses first, it cannot re-fire while the chord is held, and — unlike a
remembered `wasBothHeldLastFrame` flag — it carries nothing that could survive a scene load, a
device swap or an editor play-mode exit and desynchronise the toggle from what the player is
holding.

**It is polled by `InputController`, and only by `InputController`.** That is the one per-frame
pump already gated on exactly the conditions this needs: local *human* pilot only (an AI hull and
a remote replica both carry an `InputController` and must not move the local camera), and below
both pause gates, so the camera cannot be flipped from the overview or a modal. A second poller is
how one press comes to toggle twice and appear to do nothing;
`RearViewLawTests.OnlyInputControllerPollsTheGesture` forbids it.

It is deliberately **not** an `InputEvents` member and not in any `ElementalAbilityMapSO`: this
drives a camera, not a vessel, and routing it through the ability map would make it something a
hull could fail to author.

### Known overlap, stated rather than papered over

The right shoulder is **not a free button**. `GamepadInputStrategy` reads it as `Throttle` and
raises `FlipAction` from it, so completing the chord also boosts and flips on any vessel bound to
those. The left shoulder genuinely is free — its press/release handlers in that strategy are
commented out — which is what makes LB the half carrying the intent. This is the binding that was
asked for; if the overlap reads badly in play, the cheapest fixes in order are (a) move the chord
to LB + a face button, (b) make it LB + RB *held* for ~0.15 s so a boost tap cannot complete it,
or (c) give the rear view a shoulder of its own.

---

## 6. Where it is bound

In `VesselController.Initialize` **and** `VesselController.ChangePlayer`, under
`IPlayer.IsLocalPilot` — the same two sites the prism occlusion corridor, the speed tunnel and the
vessel vision band bind at, and for the same reasons:

- `Initialize` is the one method every vessel must call to become a player's vessel, on every
  spawn path (single-player, multiplayer, menu autopilot, runtime swap);
- `ChangePlayer` hands a **live** vessel to another player without ever reaching `Initialize`
  (the Cellular Duel round-boundary ownership swap);
- `IsLocalPilot`, not `IsLocalUser`, so the non-networked single-player spawn path is covered.

The release is **identity-guarded** (`ClearTarget(transform)`) so an outgoing vessel's teardown —
which runs *after* the incoming vessel's bind during a swap — cannot cancel the new binding.

**Binding always lands forward-facing.** `SetTarget` disengages: a fresh vessel, a new round or a
hull swap must not inherit a rear view the pilot asked for on a ship they are no longer flying.

The driver also re-pushes the vantage every `LateUpdate`, because the camera under it can change
with nothing telling it: the death camera, the end camera and the manual replay camera all take
over through `CameraManager.SetActiveCamera`. A camera it stops driving is handed back
forward-facing, so cutting to the end camera mid-look-back cannot leave the player camera holding
a mirrored offset into the next round.

**Only the player rig is ever flipped, and "the active controller" is not a specific enough test
for that.** The death camera and the end/replay camera are `CustomCameraController`s too —
`CameraManager.GetOrFindCameraController` *adds* one if a scene has not — so an active-controller
test would mirror a death cam on the frame you die, and re-snap a hand-posed broadcast replay
underneath its own framing math (`AstroLeagueGoalReplay` reads that camera's FOV to fit the shot).
The driver identifies the player rig by identity against `CameraManager.GetCloseCamera()`.

---

## 7. Retiring the picture-in-picture

**Switched off, not deleted** — the project's standing preference (the goal-stack rings, the scene
skybox models, `ScarabBallForge.ForgeGate`).

- `VesselController` no longer calls `Pip.SetLocalPilot`, so no vessel ever claims the panel.
  `RearViewLawTests.NothingGrantsTheRetiredPipPanel` sweeps every first-party script for a
  re-grant.
- **`Pip.cs` is kept, and keeping it is load-bearing rather than sentimental.** Eight hulls still
  instance `PipCamera.prefab`, which ships **active and enabled**, and that component's `Awake`
  default-off is now the only thing standing the camera down. Deleting the component would hand
  every one of those vessels a permanent extra camera pass into a render texture nothing is
  showing — exactly the fault the default-off was written to prevent, arriving by the back door.
  Retiring it properly means removing the `PipCamera` child from those eight prefabs **first**.
- **The panel had to be switched off in two places, and the code half is the load-bearing one.**
  `Pip.prefab`'s own root ships inactive, but `CORE/GameCanvas.prefab` and
  `Panels/MiniGameHUD.prefab` each carried an `m_IsActive: 1` **override** on their nested
  instance — so the panel was on by default and was only ever switched off by a vessel announcing
  it was *not* the local pilot's. Removing the grant alone would therefore have left a dead panel
  showing a stale render texture in every mode. Both overrides are corrected, **and**
  `MiniGameHUD.HideRetiredPipPanel` stands it down at `Awake` — because fifteen scenes carry
  structural forks of that canvas (`Docs/GAMECANVAS.md` §9) and a fork's own copy is out of the
  prefab's reach.
- `MiniGameHUD.OnPipInitialized` stays wired to the `PipData` SOAP channel and is deliberately
  **deaf to what it is told**: it hides the panel whatever arrives. The listener lives in prefab
  YAML across every game-mode canvas, and a method that quietly does the right thing beats fifteen
  scenes' worth of dangling `UnityEvent` targets.

---

## 8. Verification

`RearViewLawTests` (`Assets/_Scripts/Tests/Editor/`) is in two halves.

**Geometry — real behaviour**, exercised against a live `CustomCameraController`:
the rear vantage is the same distance from the ship as the forward one and on the opposite side of
it; the camera looks back down the ship's forward axis; x and y survive the mirror and only z
flips; a live `SetCameraDistance` is tracked rather than fought; and with `RearView` off the pose
is bit-for-bit what it always was.

**Source laws** — the half you cannot detect by calling the code: one gesture bound to C and both
shoulders, stateless and edge-triggered; `InputController` the sole poller, below both gates;
`VesselRearView` bound at both ownership sites and released at both teardown sites; the driver
never writing the follow offset; both camera pose sites reading `EffectiveOffset`; nothing
granting the retired Pip; and the HUD never putting its panel back on screen.

### In the editor

1. Play any scene with a Squirrel (or any vessel). Fly forward.
2. Press **C** — the camera cuts to 17 units ahead, looking back at your own nose with your trail
   receding behind you. Press **C** again to cut back.
3. On a pad, press **LB and RB together**. Expect the throttle/flip overlap noted in §5.
4. Confirm the **PIP panel is gone** — no corner view, no navy rectangle.
5. In a Rhino or Manta, hold the zoom-out ability while in rear view: the rear camera should pull
   *ahead* as the forward camera would have pulled back.
6. Die (or trigger an end-game camera) while in rear view and return: the camera must come back
   forward-facing.
