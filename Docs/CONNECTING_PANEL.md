# The Connecting Panel

The screen every game scene shows while its arena is built — `ConnectingPanelController`, under the
MiniGameHUD. It holds for a short dwell and then until `PrismTrailBuilder.PollArenaReady()` says the
arena is complete, so the player never sees the structure lay or bloom in.

It answers one question — *is this doing anything?* — in three ways, deliberately different in kind:

| Surface | What it is |
|---|---|
| **Status text** | Words: the phase and its raw counts (`BUILDING ARENA 42% (8,412 / 20,153)`). |
| **Progress bar** | A number: one monotonic 0..1 across the whole load. |
| **Arena preview** | The thing itself: the world being built, live, in a small window. |

The bar and the text read the same counters. The preview does not read a counter at all — it is the
arena — which is why a load that has genuinely stalled looks different from one that is merely long.

---

## 1. The progress bar

`ArenaLoadProgress` (plain C#, `UI/Elements/`) folds the build's phases into one value the slider
shows. `ConnectingPanelController` eases the drawn value toward it, because the phases *step* and a
bar that steps with them reads as broken.

### It is MONOTONIC by construction

The underlying signals genuinely go backwards:

- `PrismTrailBuilder.LayProgress` reads **1 while idle** and drops to 0 the moment a batch starts.
- A second arena build re-queues the lay counters from scratch.
- `GrowRemainingCount` climbs before it falls, as prisms are still being queued while others settle.

A bar that followed them faithfully would run backwards mid-load, which turns *"this is taking a
while"* into *"something is wrong"*. So the model **never lowers its own output**; a phase reporting
less than what is already shown is simply not shown. `Reset()` is the only way down.

### Bands, and the creep between them

| Span | Band |
|---|---|
| Dwell (nothing announced yet) | 0 → **0.05**, by creep |
| Laying | **0.05 → 0.60**, from `LayProgress` |
| Growing | **0.60 → 0.95**, from `1 − remaining / peak` |
| Arena ready | **1.0** |

Two spans have no denominator: the opening dwell, and any gap between phases. Sitting still there
reads as a hang; jumping ahead lies. The model eases toward the current phase's ceiling at a rate
that never reaches it, so the bar always moves and never overtakes real progress.

Three details are load-bearing, and each is a test:

- **The creep's ceiling is a function of the furthest phase ENTERED**, not of the current value.
  Inferring it from the value (*"we are past the lay band, so creep toward the grow ceiling"*) relies
  on a lerp never quite reaching its target — a float accident standing in for a decision. The day it
  does reach it, the dwell creeps to 60% before a single prism is laid.
- **The grow denominator is the PEAK count seen this load.** Taking the first reading as the total
  shows 0% forever on a build whose prisms are still being queued as others settle.
- **It finishes at exactly 1**, latched off the hold predicate. A bar that vanishes at 0.9 reads as
  an abandoned load rather than a completed one.

### The slider is a READOUT, not a control

`StyleProgressBar` forces `interactable = false`, `Transition.None`, `Navigation.Mode.None`, and
**removes the handle** — a handle is the affordance that says *drag me*, and there is nothing to
drag. Done in code rather than left to the prefab because every one of those is a way for a stock
UGUI slider to look and behave like a control.

The LOOK is authored **in the prefab**, not in code, so an art pass never has to come back through
C#: two **9-sliced white capsules** from `Tools/Build/author_loading_bar_sprites.py` (`--check`) in
`_Graphics/UI/Loading/`, tinted there — a blue fill in a dark channel — on a 14 px bar. Nine-slicing is not decoration: a progress bar is
stretched to whatever width the panel gives it, and a capsule stretched without a border turns its
round caps into ellipses, which is the single thing that makes a bar look cheap. The border is the
corner radius exactly, and the script asserts the stretch region is constant per row (or the bar
bands as it grows) and that the corners are empty (or the 9-slice caps square off).

**UGUI's own slider template fights this.** A stock slider insets its Fill Area by a handle's width
so a handle never overhangs the track (`anchoredPosition.x −5`, `sizeDelta.x −20`, and another
`+10` on the Fill itself) and anchors both track and fill to the middle half of the bar's height.
With no handle, all of that only stops the fill from reaching the ends of its own channel and leaves
the capsule drawn at half height. The prefab now zeroes the insets and spans both to `0..1`.

---

## 2. The arena preview

`ConnectingArenaPreview` (`UI/Elements/`) renders the cell being built into the panel's RawImage —
the same idea as the arcade card's preview window, pointed at the world *this* scene is standing up.

- **It renders into a RenderTexture, never to the screen.** A camera with a `targetTexture` never
  draws to the display, so it cannot fight the panel's own backdrop camera or the gameplay camera
  behind it. There is no depth ordering to get right.
- **The camera is created at runtime when none is wired.** The interesting half is *where it looks*,
  not which prefab object it is. Wire one only to pin a specific culling mask.
- **A runtime-made camera excludes the UI layer**, or the preview draws the panel inside its own
  window, one frame stale, forever.
- **It comes down with the panel**, on every exit including a cancelled load. Left running it is both
  a GPU allocation nobody frees and a second camera rendering the world for the whole match.

### Framing: the same bug the arcade preview already recorded

The first version showed the skybox and a few distant slivers instead of the arena. Two causes,
compounding, and `ModePreviewArena.FramingRadius` had already written up the first one:

- **`Cell.MembraneRadius` returns 0 until the membrane has actually spawned**, so a camera placed
  against it parks at a fallback distance with the arena's real size unknown. The framing now falls
  back membrane → `ExpectedNucleusWorldRadius × 3` → **1200** (the menu cell's own membrane, a sane
  arena size), and is **re-read every tick** so it corrects itself the moment the membrane appears.
- **The clip planes were copied from the template camera.** The panel's backdrop camera is posed a
  few units from a backdrop and its far plane is sized for that; a preview inheriting it clips the
  entire arena away and shows — again — the skybox. They are now derived from the shot:
  `far = distance + 2r`, `near = distance / 200`.

The camera sits at **1.95 × r** back and **0.35 × r** up, the arcade preview's own factors, which at
60° FOV puts the arena's full radius just inside the frame with air around it. Orbit is 6°/s: the
subject is the world appearing, and a fast orbit competes with it.

### Never wire it to the panel's own backdrop camera

`previewCamera` left **empty** is the authored state; the panel's backdrop camera goes in
`settingsTemplate`. Wiring the backdrop camera as the preview camera is an easy and completely
silent mistake — the preview retargets it to a RenderTexture (so it stops drawing to the screen)
and re-poses it (so it stops looking at what it was posed at), and the panel's backdrop just
disappears with nothing in the console. `ConnectingPanelController` therefore hands the preview its
reserved camera before `Begin` (`ReserveCamera`), which detects the collision, demotes it to the
template, makes a dedicated camera, and says so once.

### Cost: it renders ON DEMAND, not every frame

The preview runs during the heaviest frames in the game. An enabled camera would take a second full
pass over an arena of ~50,000 prisms **every frame**, roughly doubling the render cost of the load it
is reporting on. So the camera is left **disabled** and stepped by hand at `renderHz` (**8**), with
post-processing, shadows and anti-aliasing off and a 288 px render height. A world growing in reads
perfectly well at 8 Hz.

> **The load itself is still the expensive thing.** Laying 49,856 prisms is heavy with or without a
> preview; the numbers above bound what the *preview* adds, and do not make the load fast. If frame
> rate during the build is the problem to solve, that is a lay-budget question, not a panel one.

---

## 3. Wiring

The controller degrades cleanly — every new field is optional, and a panel with none of them wired
behaves exactly as it did before. To light them up:

| Field | Wire to |
|---|---|
| `progressSlider` | the panel's Slider |
| `arenaPreview` | a `ConnectingArenaPreview` on the panel |
| …its `surface` | the panel's Level Preview RawImage |
| …its `settingsTemplate` | the panel's existing `connectingCamera`, for clear flags and culling mask |
| …its `previewCamera` | **nothing** — see above |

`ConnectingArenaPreview.previewCamera` is left **empty** unless a scene needs a specific one.
