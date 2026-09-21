# The screenshot director

Press **P** (or the pad's **Select / View / Share**) in flight. A UI-free photograph of your
vessel lands in your clone's own `Recordings/` folder, shot from a camera angle drawn at random
from a library of capture concepts. No setup, no scene wiring, no pause.

- Runtime: `Assets/_Scripts/Utility/ScreenShots/` — `ScreenshotDirector`, `ScreenshotFraming`,
  `ScreenshotConcept`, `ScreenshotGesture`
- Config: `ScreenshotDirectorConfigSO`, asset at `Assets/Resources/ScreenshotDirectorConfig.asset`
- Tests: `Assets/_Scripts/Tests/Editor/ScreenshotDirectorTests.cs`

## Where captures go, and why they are never pushed

`ScreenshotDirectorConfig` ▸ **Output Folder**. Leave it EMPTY and captures go to
**`<repo>/Recordings`** — resolved per machine, not hardcoded: in the Editor
`Application.dataPath` is `<repo>/Assets`, so its parent is whatever your own clone lives in. One
default is therefore `C:\Users\Will\source\repos\Cosmic-Shore\Recordings` on one machine and
the equivalent on the next, with no per-user setting to get wrong.

**Nobody else carries your screenshots.** `/Recordings` is already in `.gitignore` (line 77), so
the folder is private to the machine that made the shots — which is the design: captures are for
the person who took them to triage and decide what to do with, not repository content. Anything
that moves this default must move that ignore rule with it, and the regression test
`ResolveOutputFolder_DefaultsToTheRepositorysOwnGitIgnoredRecordingsFolder` is what makes that
pairing fail loudly rather than quietly.

A player build has no repository, and the folder beside a shipped executable is routinely
unwritable (Program Files), so a build falls through to the persistent data path. An absolute path
is used as given; a relative one hangs off that same default root, so `Runs/Tuesday` is
`<repo>/Recordings/Runs/Tuesday`. A folder the OS refuses falls back to the persistent data path
with a warning rather than losing the shot.

Filenames are `CosmicShore_<Concept>_<timestamp>.png`. **The concept name is in the filename on
purpose**: after a session you can see at a glance which concepts are producing keepers and retune
their weights, which is the only way the library gets better.

## Why it is UI-free, and why that is structural

The director renders **its own camera into a RenderTexture, never to the screen**. A screen-space
canvas is composited straight to the display and never reaches a RenderTexture at all, so the HUD
is excluded *by construction* rather than by a toggle that can be left in the wrong state after a
crash. Only WORLD-space UI needs excluding, and `excludedLayers` (UI + 3D UI) does that.

This is also what buys the other two properties: a capture can be **larger than the window**
(`captureHeight`, default 2160, width follows the window's aspect so framing matches what you see)
and it can be posed **somewhere the player's camera is not**, which is the whole feature.

`ScreenCapture.CaptureScreenshot` — what the older `CaptureScreenShot.cs` uses — can do none of
this: it grabs the composited frame, UI included, from the camera you already have.

## A concept is only ranges

There is deliberately **no enum of shot types and no per-concept camera code**. Every shot is one
solve — a spherical offset around the subject, an aim point, a lens — and the only structural
difference between "over the shoulder" and "static tracking cam" is `worldAligned`: whether azimuth
is measured from the vessel's own course (the shot follows it through a turn) or from world north
(a vantage planted in the arena that the vessel flies past).

| field | what it does |
|---|---|
| `azimuthDegrees` | 0 = directly ahead of the subject, 180 = directly behind |
| `elevationDegrees` | + is above the subject's horizon |
| `distance` | world units (see the vision-band note below) |
| `fieldOfView` | low = long lens, picks the subject out; high = wide, subject inside its world |
| `rollDegrees` | dutch tilt |
| `aimLeadSeconds` | aims ahead of the subject, leaving the space it is flying into |
| `framingPitchDegrees` | drops the subject off-centre for some sky |
| `weight` | relative odds; 0 retires a concept without deleting it |

Adding a shot type is a **row in a list**, which is the point — a shot type expressed as a subclass
is one nobody can author without a programmer.

Shipped library: Over the Shoulder, Sidecar (starboard and port), Oncoming, Low Chase, Top Down,
Static Tracking Cam, Establishing.

### Lead room is measured in frames, never in metres

`aimLeadSeconds × speed` is a WORLD distance, so the same 0.35 s that frames a 110u static tracking
shot beautifully is **105 units of lead** on a vessel doing 300 u/s — from the 8u Low Chase camera,
which puts the ship completely outside the frame and photographs empty space. The solve clamps the
lead to `MaxLeadFraction` (0.4) of the camera's own distance, so the subject sits at most ~22° off
the optical axis: near the edge of frame on a long lens, which is the composition, and never
outside it.

This was found by the test suite, not by looking at the code. It is the general trap: **a
composition parameter expressed in units of the SUBJECT is unbounded relative to the FRAME.**

## The three platform laws it meets

**Prism occlusion corridor — held, for the capture only.** The corridor dissolves mass along the
line from the camera to the local ship so a pilot can always see their own hull
(`Docs/PRISM_ANIMATION.md` §4.7). A camera posed somewhere the pilot is not would cut that hole
through unrelated mass — in a photograph, straight through the trail the shot exists to show. The
director therefore takes the same narrow, symmetric hold `CameraManager.BeginManualReplayCamera`
takes, for the same stated reason, and this doc is the record that the corridor now has **two**
sanctioned holders, both of them manually-posed vantages that are not the pilot's eye. It is a
hold, not an opt-out: the vessel binding stays, the lift is unconditional in a `finally`, it is
identity-guarded so a replay camera's own hold is never lifted by us, and it lasts two frames.
Switch it off per-config with `holdOcclusionCorridor` if you want the corridor in your shots.

**Vessel vision band — untouched, and it shapes the library.** Hulls are progressively re-shaded
into flat domain-coloured silhouettes as a function of distance from the camera drawing them, with
no suppression by design (`Docs/VESSEL_VISION.md`). So a capture camera past ~150u photographs a
silhouette, not a ship. Every concept in the shipped library sits inside that near edge **except
"Establishing (banded hull)", which breaks it deliberately** and says so in its name: it is the one
concept that photographs the world rather than the vessel.

**Speed tunnel — untouched.** It is bound to the gameplay camera and stays bound; the capture
camera is a second, disabled camera stepped by hand, so it keeps its concept's authored FOV. A
capture therefore does not inherit the speed-tunnel's FOV narrowing. If a shot should look *fast*,
that is a follow-up: read `SpeedTunnelConfigSO.Effect01(speed)` and fold it into the concept's lens.

## Notes for whoever touches it next

- **It installs itself.** `[RuntimeInitializeOnLoadMethod]` + `SingletonPersistent`, the same
  zero-wiring shape as `DisplayGraphicsSettings`. Nothing in any scene references it.
- **It is therefore never injected.** Reflex injects scene objects via a `ContainerScope`; an
  object created at runtime gets none, so an `[Inject] GameDataSO` here would be permanently null.
  The subject is read from `PrismOcclusionCorridor.Target` / `.TargetRadius` instead — the platform
  already maintains exactly that handle (the local pilot's hull and its measured radius, rebound on
  every spawn path including a mid-match vessel swap) because a law depends on it being right.
- **A bare `AddComponent<Camera>` comes up with URP's defaults, not the project's** — an un-adopted
  capture camera photographs a flat, bloom-free version of a world the game shows lit, which reads
  as the feature being broken. `AdoptUrpSettings` copies post/AA/shadows/volume mask from
  `Camera.main`. `ConnectingArenaPreview` records the same finding from the same trap.
- **The capture costs one frame.** `EncodeToPNG` at 4K is main-thread-only and takes a couple of
  hundred milliseconds; the file write is synchronous because this project builds against the .NET
  Framework 4.8 profile, which has **no `File.WriteAllBytesAsync`**. Moving the encode off the main
  thread (`AsyncGPUReadback` + a worker) is the obvious follow-up if the hitch ever matters.
- **F12 is deliberately not the key.** It is Steam's own screenshot key, so binding it would fire
  two captures, one of which has the UI in it. F5–F9 are the diagnostics/benchmark overlays and F11
  is fullscreen.
- **P is not free either, and that is stated rather than hidden.** `KeyboardInputStrategy` — the
  dual-WASD desktop scheme every two-stick hull flies on — reads `pKey` as the RIGHT STICK's
  vertical axis (`WASD` left, `P`/`;`/`L`/`'` right, `KeyboardInputStrategy.cs:90`). So on those
  hulls a capture press also feeds the vessel one frame of stick, and a held P keeps feeding it —
  a small nudge to the very framing the system exists to produce. The one-thumb hulls are
  unaffected: `SingleStickMouseInputStrategy` reads only the left stick, so P reaches nothing
  there. It is bound anyway because it is the key that was asked for and the nudge is minor; the
  clean fix is to move the keyboard scheme's right-stick-up off `P`, which is a player-facing
  rebind (and a CLAUDE.md edit) and therefore a separate decision, not one to take in passing.
  The general shape: **a key is only "free" against the keys some OTHER system is reading, and an
  input scheme that consumes raw keys advertises none of them.**
