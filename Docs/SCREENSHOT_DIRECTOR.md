# The screenshot director

Press **0** — the number row's zero — (or the pad's **Select / View / Share**) in flight. A UI-free photograph of your
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

## What a capture actually is, pixel for pixel

| | shipped | where it comes from |
|---|---|---|
| height | **2160** (4K-tall) | `captureHeight`, `[Range(480, 4320)]` — so 4320 is available |
| width | `height x the window's aspect` | **3840x2160 on a 16:9 window, and not otherwise** |
| encoding | **PNG, lossless** | `EncodeToPNG` |
| colour | 8 bits/channel, `RGBA32`, sRGB | the readback texture; the RT is sRGB read/write |
| post, AA mode, shadows | the gameplay camera's | `AdoptUrpSettings`, gated on `matchGameQuality` |
| MSAA | **the URP asset's** (4x today) | the capture RT's `antiAliasing`, then a resolve blit |

Two of those are worth stating plainly rather than being read off the word "4K".

**The width is a function of the window, not a constant.** `captureHeight` sets the height and the
width follows the window's aspect, deliberately — a capture then frames *exactly* what is on
screen. On a 16:9 window that is 3840x2160. On an ultrawide or a windowed editor it is 2160 tall
and whatever that aspect implies, which is the intent, not a shortfall; if a specific pixel size is
needed, set the Game view to that aspect first.

**MSAA had to be asked for.** `RenderTexture.GetTemporary`'s `antiAliasing` parameter **defaults to
1**, so for its first few days this feature rendered every capture with no MSAA at all while the
game beside it ran the URP asset's 4x — post-process AA (FXAA/SMAA/TAA) was correctly adopted the
whole time, which is exactly why it was not obvious: the shots were anti-aliased, just less than
the screen they were taken from, and stair-stepping on a prism edge reads as "the renderer" rather
than as a missing argument. The capture now requests `QualitySettings.antiAliasing` (the value URP
syncs from its own asset), snapped down to a legal 1/2/4/8, and **resolves it with a blit** — a
multisampled target cannot be `ReadPixels`'d directly, that is undefined on several backends. One
extra full-frame copy, on a key the player pressed.

*General shape: a default-valued optional parameter is a decision nobody made, and a quality
default of "none" fails by looking slightly worse rather than by failing.*

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

Shipped solo library: Over the Shoulder, Sidecar (starboard and port), Oncoming, Low Chase, Top
Down, Static Tracking Cam, Establishing. Plus three PAIR concepts — see below.

**Each solo band is a UNION, not a window.** The first roll of real captures came back too tight,
so every band was scaled 1.5x — which moved the near edge out along with the far one and quietly
*deleted* the close shots instead of adding to them. Each band now runs from the tight cut's floor
to the roomy cut's ceiling, so a single concept rolls the whole range it has ever been able to
frame and the library gets its variety from the roll rather than from a decision made once at
authoring time:

| concept | tight cut | 1.5x cut | **shipped (union)** |
|---|---|---|---|
| Over the Shoulder | 12-26 | 18-39 | **12-39** |
| Sidecar (both) | 14-34 | 21-51 | **14-51** |
| Oncoming | 18-45 | 27-67.5 | **18-67.5** |
| Low Chase | 8-18 | 12-27 | **8-27** |
| Top Down | 28-70 | 42-105 | **28-105** |
| Static Tracking Cam | 35-110 | 52.5-165 | **35-150** (capped) |
| Establishing | 220-520 | — | **220-520** (unchanged) |

Two numbers are not free scales, and both are forced by the **vessel vision band** rather than by
taste: **Static Tracking Cam's ceiling is held at 150**, not its arithmetic 165, because 150 is
where the band starts re-shading a hull into a flat silhouette and every concept but one is
supposed to sit inside it; and **Establishing is left alone**, since it is already the wide shot
and is deliberately past that edge. *A ratio applied to a list of numbers is not a decision until
you check what each number was up against.* `Defaults_KeepEverySoloConceptInsideTheVisionBand…`
holds both, reading the edge off the **shipped** `VesselVisionShadingConfig` asset rather than off
the C# field initializer — which is the trap `Docs/VESSEL_VISION.md` records against itself.

### Lead room is measured in frames, never in metres

`aimLeadSeconds × speed` is a WORLD distance, so the same 0.35 s that frames a 110u static tracking
shot beautifully is **105 units of lead** on a vessel doing 300 u/s — from the 8u Low Chase camera,
which puts the ship completely outside the frame and photographs empty space. The solve clamps the
lead to `MaxLeadFraction` (0.4) of the camera's own distance, so the subject sits at most ~22° off
the optical axis: near the edge of frame on a long lens, which is the composition, and never
outside it.

This was found by the test suite, not by looking at the code. It is the general trap: **a
composition parameter expressed in units of the SUBJECT is unbounded relative to the FRAME.**

## The two-shot: two vessels, framed identically

When two vessels are between **10 and 30 units** apart (`pairSeparation`), a capture has an
**85%** chance (`pairChance`) of being a two-shot instead of a solo — both ships at exactly the
same distance from the lens, laid out across the frame, symmetric about its centre. It is
deliberately the high-priority branch: two ships that close is the rarer and more interesting
moment, and it falls back to a solo shot whenever there is no pair, no usable Pair concept, or the
roll goes the other way.

### It is one geometric fact, not a search

The set of points **equidistant from A and B is the perpendicular bisector plane** of the segment
joining them — the plane through their midpoint whose normal is the separation direction. Put the
camera anywhere on that plane, aim it at the midpoint, and three properties fall out *together*:

| property | why it follows |
|---|---|
| both ships the same distance from the lens | that is what the plane *is* — so the same apparent size |
| the pair is broadside, at equal depth | the separation is the plane's normal, so it is perpendicular to the optical axis |
| symmetric either side of frame centre | the midpoint is on the axis and they are mirrored about it |

Choosing the camera's **right** axis to be the separation direction then lays them out level, which
is what `rollDegrees` tilts off horizontal. So the vantage is a single angle sweeping that plane —
`azimuthDegrees` on a Pair concept is **the angle around the line joining the two ships**, and a
0-360 range is a free orbit in which *every* angle keeps the promise.

Verified numerically over 200,000 random pairs before it was written: worst-case equidistance error
**8.9e-15** relative, worst symmetry error **3.7e-13** units, both subjects always in front of the
lens and always inside the frame with ~5 degrees of margin to spare. `ScreenshotPairFramingTests`
asserts the same properties over randomized pairs in edit mode.

### Two authored fields are ignored, and that is structural

- **`elevationDegrees`** would push the camera OFF the bisector plane, which is the single move
  that breaks equidistance. The in-plane angle already reaches every vantage the guarantee permits.
- **`aimLeadSeconds`** would swing the aim off the midpoint. It cannot change either distance —
  those are fixed by where the camera *is*, not where it looks — but it slides both ships toward
  one edge and loses the symmetry the shot exists for.

The solve does not read them, rather than relying on the shipped concepts authoring zeros. **A
promise you can author your way out of is not a promise**, and the alternative fails silently: the
photograph still comes out, just not framed the way the concept claims.

### Distance is a floor, not a setting

A Pair concept's `distance` is the **closest** the camera may be; the solve pushes further back
whenever that is what it takes to fit both hulls, using each ship's own measured radius
(`PrismOcclusionCorridor.MeasureCircumscribedRadius`, so a new vessel needs nothing authored) and
the real viewport aspect, since the pair lies across the frame. `Duo Close Pass` authors `0` — "as
close as they will both fit" — which is why `ScreenshotConcept.IsUsable` exempts Pair concepts from
the minimum-distance test that would otherwise retire it.

### Where the candidates come from

`VesselVisionShading.CollectStampedVessels` — the vision band's own roster, which every vessel
joins through `VesselHelper.SetShipProperties` on every spawn, vessel swap and replicated domain
change. Same argument this system already makes for reading `PrismOcclusionCorridor.Target`: **a
platform law maintains the handle because the law depends on it being right**, so reading it is
free and cannot drift from what is on screen. It beats `FindObjectsByType` on correctness rather
than on speed — `StampDisplayModel` deliberately does not join that roster, so a toy matrix's mini
hulls can never be mistaken for pilots.

Pairs containing the **local** ship win ties (the photograph is nominally of your own flight);
among equals the closest pair wins. A tail chase — one ship directly behind another, so the flow is
parallel to the separation — is the degenerate case that actually happens in a dogfight, and it is
handled rather than guarded: the pair genuinely defines no "ahead", because ahead is along the line
joining them, which is the one direction the camera may not occupy, so the reference falls back
through world up and world forward. The resulting shot lays pursuer and pursued across the frame,
which is the shot you want of a chase anyway.

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
- **The key is a DIGIT because every letter is already spoken for, and that turned out to be the
  better answer anyway.** `KeyboardInputStrategy` — the dual-WASD desktop scheme every two-stick
  hull flies on — consumes `WASD` (left stick), `P`/`;`/`L`/`'` (right stick), `QWER` + Space (the
  ability keys, shared verbatim with `SingleStickMouseInputStrategy`) and both Shifts (the
  triggers). **`P` in particular is right-stick-UP**, so capture bound there would have fed the
  vessel a frame of stick on every press and kept feeding it on a hold — a nudge to the very
  framing this system exists to produce, on exactly the hulls a third-person photograph is worth
  taking of. Moving the flight binding to free `P` up was tried and **reverted**: a capture is a
  deliberate, occasional act rather than a flight control, so a key *away* from the hands' resting
  clusters is one you cannot fat-finger mid-manoeuvre, and leaving the flight scheme untouched is
  what a photograph *of that flight* actually needs. The digits are entirely unclaimed — `0`-`9`
  are read by nothing in `_Scripts`.
  The rule it records: **a key is only "free" against the keys some OTHER system is reading, and
  an input scheme that consumes raw `Keyboard.current` reads advertises none of them** — grep the
  reads, do not reason from what is "normally" bound. Its corollary is about where to look for
  room: **when every ergonomic key is taken, ask whether the new binding actually wants an
  ergonomic key** — a control you press mid-manoeuvre and one you press between them have
  opposite requirements, and the second is happier on a shelf the first can never reach.
