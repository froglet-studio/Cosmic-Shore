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

### Frame what was BUILT — the radius a cell reports is its BOUNDARY

The framing above was the arcade card's — **1.95 × r** back at 60° FOV — and once the membrane
started spawning in time to be measured, that turned out to be exactly wrong for this panel. At
1.95 × r a sphere of radius r subtends `atan(1/1.95) = 27.1°` against a 30° half-FOV, so the
**membrane fills the frame by construction**. The mass actually being laid sits far inside that
shell, so the shot was a wall of membrane shards with the arena a speck in the middle of it.

The two cards want different shots because they answer different questions. The arcade card is
showing you a **world**, so it frames the whole thing from outside with air around it. This panel is
showing you a **build**, so it belongs in the room the build is happening in: **0.7 × r** back,
**0.22 × r** up, at **45°** FOV — the zoom spent half on distance and half optically. Orbit stays at
6°/s: the subject is the world appearing, and a fast orbit competes with it.

> General: **a cell's reported radius is its playfield boundary, not the extent of what is in it.**
> Framing a build on it frames the shell.

And zooming was only half of it, because `r` was still the wrong number — Scurry's arena is a small
fraction of its membrane, so *any* factor times the membrane radius is a shot of the membrane. The
extent is now **measured from the build itself**: `PrismTrailBuilder` keeps a running world AABB of
everything laid since the hold began (`TryGetLaidBounds`), and the camera frames that. The cell's
own size is only the fallback — during the dwell, before a prism exists, and for an authored
`EnvironmentPrefab` that is instantiated wholesale rather than laid.

Three things make the measurement cheap and the shot stable:

- **It costs nothing on the hot path.** The pose is read straight off the lay plan (a float compare
  per prism, no transform resolve, no world-matrix recompute) into a LOCAL AABB, which is pushed
  into the shared world AABB once per clone batch — 8 `TransformPoint`s per 256 prisms. All eight
  **corners**, never just min/max: a rotated or scaled parent maps a box's corners to a box neither
  original corner sits on, and taking two of them silently under-measures the arena.
- **The framing only ever grows, and it is eased** (`framingSmoothing`, 1.2 s). The extent arrives
  one batch at a time, so tracking it raw would jitter the shot every 256 prisms, and letting it
  shrink would dolly the camera *in* while the world was getting bigger. Monotone + eased reads as
  one slow pull-back that follows the arena out.
- **It is reset with the hold**, or the next load opens on the previous match's arena.

### The camera never leaves the cell

`insideCellMargin` (**0.9**) clamps the camera's distance from the **cell centre** — not the
arena's, since the membrane is centred on the cell and an arena measured off its own mass can sit
well off-centre inside it. A camera outside the membrane is looking at the arena *through* its own
boundary shell, which is the wall-of-membrane shot this whole pass exists to remove; it is also the
only place a preview can put something outside the playfield, and nothing belongs out there.

### Every scene gets its own camera

`previewCamera` is authored **empty** in the prefab, which is what makes the preview create and own
a dedicated camera per scene (destroyed with the panel). `ReserveCamera` enforces it if a scene ever
wires the panel's backdrop camera there — see below.

> **Serialized values beat field initializers.** The prefab carries `framingFactor` /
> `liftFactor` / `fieldOfView` explicitly, so changing the C# defaults alone changes nothing on
> screen. The prefab is patched with them; if the framing is ever retuned, retune the asset.

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
> preview; the numbers above bound what the *preview* adds, and do not make the load fast. That is
> the build tempo below, not a panel cost.

---

## 3. A WATCHED hold pays for the view

The load gate runs the build at a deliberately brutal tempo — **a 250 ms lay slice per frame**
(`PrismTrailBuilder.LoadGateLayBudgetMs`) and **512 prism creation completions per frame**
(`Prism.LoadGateCreationCompletionsPerFrame`) — and both numbers are justified in their own comments
by the same premise: *the screen is covered, so there is no visible frame to protect.* Between them
that is ~4 build frames a second, by design.

**That premise stopped being true the moment this panel started showing the arena being built.** A
live preview, an orbiting camera and a moving bar are a view, and a view has to be read at a frame
rate. So a hold that shows the build states its own slices:

| Dial | Unwatched | Watched (this panel) |
|---|---|---|
| `PrismTrailBuilder.LoadGateLayBudgetOverrideMs` | 250 ms | **25 ms** |
| `PrismTrailBuilder.LoadGateCreationBudgetMsOverride` | 512 completions | **18 ms** |

Three things about this are worth keeping:

- **Both dials are work-conserving.** The same prisms are laid and the same prisms are created
  either way; the slice only decides how that fixed work is spread over frames. The cost is the
  extra per-frame overhead of finishing over more frames — a load that is somewhat longer — and the
  purchase is a frame rate the view can be read at. It is a real trade, not a free win.
- **The creation budget is stated in MILLISECONDS, not as a count.** Per-prism completion cost
  varies with scene size and collider density, so a count cannot hold a frame budget on two
  different machines — exactly the argument `LayBudgetedAsync` already makes for laying. `Prism`
  keeps its completion count for every unwatched path and switches to the time slice only while a
  watched override is set.
- **The slices belong to the HOLD, not to the builder.** `SetLoadGateHolding(false)` now clears
  both, so an aborted or cancelled hold cannot leak one holder's tempo into the next load. The
  panel clears them again on `Hide` for the same reason.

`EnvironmentLoadVeil` reached this shape first from the other direction — Menu_Main's veil runs at
**80 ms** because the services under it (Netcode heartbeats, Relay, audio) must keep breathing. Two
different reasons, one dial: *the full tempo is only correct when nothing else needs the frame.*

---

## 4. The pilot roster, and waiting for the other machine

The panel shows one chip per **human** pilot: that player's avatar over their domain colour. A chip
has two states and they say one thing — **greyed** means that pilot's machine is still building its
arena, **lit** (avatar at full colour, domain colour up, a slow glow behind it) means they are done.
The status line only names what the row already shows: `WAITING FOR PLAYERS… 1 / 2 READY`.

### Loaded is PER-MACHINE, so it needs a report

The arena is built independently on every peer — each runs its own spawner off its own clock — so
"the arena is ready" is not a server fact and cannot be derived from one. It is the same
owner-detects / server-records round trip the platform already uses for client-local facts
(`ReportFaunaKill_ServerRpc`, `ReportCombatHit_ServerRpc`,
`ReportEnvironmentPrismDestroyed_ServerRpc`): `Player.ReportArenaReady()` → server →
`Player.NetArenaReady` (server-write, everyone-read) → every peer can see who is still loading.

Four details are each load-bearing:

- **It is reset per scene.** `PrepareForNewScene` clears it on the server. A stale `true` would let
  the next match's panel release before that machine had laid a prism — the panel's whole job,
  skipped, with nothing to show for it.
- **An AI is ready by construction** and is marked so at spawn. It has no machine of its own to
  finish loading, so nothing may ever wait on one — which is also why AI are absent from the row: a
  row listing them would show chips that can never change, reading as players who never arrive.
- **A player that is not network-spawned is trivially ready** (`IsArenaReady => !IsSpawned || …`),
  so the legacy single-player spawn path is unaffected.
- **A mode with no connecting panel still reports.** `MiniGameHUD`'s no-panel branch calls
  `ReportArenaReady` after its own gate passes, because a *peer's* panel is waiting on that answer
  and a mode that happens not to wire a panel must not pin everyone else to a loading screen.

### The wait is bounded, and it releases loud

`peerWaitTimeoutSeconds` (**45**) caps it. A player who crashed or dropped during the load cannot
hold the rest of the lobby on a loading screen forever, and the release logs a warning naming how
many never reported — a timeout here means somebody is about to start a match a player short, which
is worth saying.

### The row is structural, and finds its own pieces

`ConnectingPanelController` **ensures** a `ConnectingPlayerRoster` at `Awake` and hands it its
sources, the same way it ensures its `CanvasGroup` — waiting for a teammate's arena is a property of
the load, not of how a particular panel prefab was authored, and a prefab carrying the art but not
the component would show a row that never lights up.

The roster finds its own container (a descendant named `PlayerIcons`, else itself) and its own chip
template (the container's first authored child, which it hides and clones — so an art pass is
honoured rather than overwritten; with no children it builds a plain chip). Within a chip the pieces
are found by name — a child whose name contains *domain*, *avatar* / *icon* / *profile* /
*portrait*, *glow* — with anything missing built. The alternative is a serialized reference per
chip on a row whose length is not known until the match starts.

Avatar art comes from the **HUD's own** `SO_ProfileIconList` (`MiniGameHUD.ProfileIcons`), so the
faces on the connecting screen and the faces on the scoreboard cannot drift apart. Domain colour is
`SO_ColorSet.GetDomainSignalColor` — the accessor the top bar and the Echo Sight already read — and
it is read **live** every tick, never snapshotted, because a domain can change right up to the
launch.

The lit state arrives over `litRiseSeconds` (**0.35**) rather than snapping: continuity of existence
applies to UI, and a chip that pops on reads as a chip that was replaced.

---

## 5. Wiring

The controller degrades cleanly — every new field is optional, and a panel with none of them wired
behaves exactly as it did before. To light them up:

| Field | Wire to |
|---|---|
| `progressSlider` | the panel's Slider |
| `arenaPreview` | a `ConnectingArenaPreview` on the panel |
| …its `surface` | the panel's Level Preview RawImage |
| …its `settingsTemplate` | the panel's existing `connectingCamera`, for clear flags and culling mask |
| …its `previewCamera` | **nothing** — the preview creates and owns one per scene |
| `playerRoster` | nothing — ensured at `Awake`; wire one only to author its references |
| …its `container` | nothing — a descendant named `PlayerIcons` is adopted |
| …its `entryTemplate` | nothing — the container's first child is adopted (and hidden) |

`ConnectingArenaPreview.previewCamera` is left **empty** unless a scene needs a specific one.
