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

The art is two **9-sliced white capsules** authored by
`Tools/Build/author_loading_bar_sprites.py` (`--check`) into `_Graphics/UI/Loading/`, tinted at
runtime — a cool blue fill in a dark channel. Nine-slicing is not decoration: a progress bar is
stretched to whatever width the panel gives it, and a capsule stretched without a border turns its
round caps into ellipses, which is the single thing that makes a bar look cheap. The border is the
corner radius exactly, and the script asserts the stretch region is constant per row (or the bar
bands as it grows) and that the corners are empty (or the 9-slice caps square off).

---

## 2. The arena preview

`ConnectingArenaPreview` (`UI/Elements/`) renders the cell being built into the panel's RawImage —
the same idea as the arcade card's preview window, pointed at the world *this* scene is standing up.

- **It renders into a RenderTexture, never to the screen.** A camera with a `targetTexture` never
  draws to the display, so it cannot fight the panel's own backdrop camera or the gameplay camera
  behind it. There is no depth ordering to get right.
- **The camera is created at runtime when none is wired.** The interesting half is *where it looks*,
  not which prefab object it is. Wire one only to pin a specific culling mask or post-processing.
- **The aim is re-resolved every frame.** The cell does not exist when the panel comes up — that is
  the whole point of the panel — so a one-shot lookup frames empty space for the entire load. It
  orbits `Cell.FindNearestActiveCell` at `MembraneRadius × 1.35`, slowly (6°/s: the subject is the
  arena appearing, and a fast orbit competes with it).
- **A runtime-made camera excludes the UI layer**, or the preview draws the panel inside its own
  window, one frame stale, forever.
- **It comes down with the panel**, on every exit including a cancelled load. Left running it is both
  a GPU allocation nobody frees and a second camera rendering the world for the whole match.

Render height defaults to a modest 320 px. This runs during the heaviest frames of the session and
the surface is a small panel inset; there is nothing to gain from more.

---

## 3. Wiring

The controller degrades cleanly — every new field is optional, and a panel with none of them wired
behaves exactly as it did before. To light them up:

| Field | Wire to |
|---|---|
| `progressSlider` | the panel's Slider |
| `trackSprite` / `fillSprite` | `_Graphics/UI/Loading/loading_bar_track.png` / `…_fill.png` |
| `arenaPreview` | a `ConnectingArenaPreview` on the panel |
| …its `surface` | the panel's Level Preview RawImage |
| …its `settingsTemplate` | the panel's existing `connectingCamera`, so the preview matches its clear flags and culling mask |

`ConnectingArenaPreview.previewCamera` is left **empty** unless a scene needs a specific one.
