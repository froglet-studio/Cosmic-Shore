# Mode Preview — the live diorama and the in-menu Test Flight

Replaces the arcade card's pre-rendered preview video with two things: a **live scale model of
the arena the mode actually builds**, shown in the configure modal, and a **Test Flight** — a
short, single-player, full-screen taste of the mode played inside Menu_Main.

---

## 0. What was there, and what actually changed

`SO_Game.PreviewClip` is a `VideoPlayer` **prefab** reference, instantiated by
`ArcadeGameConfigureModal.InitializeGameMetaView` into `selectedGamePreviewWindow`. So the preview
was never on the card face — the grid paints a static `CardBackground` sprite and only the
*selected* game shows a clip. **One live preview at a time is all this feature ever needs**, which
is what makes a real-time diorama affordable at all.

That path is not deleted. A mode with no `ModePreviewDefinitionSO` still plays its video, so this
rolls out one mode at a time rather than as a flag day — and it has to, because the arcade lists
**42** `SO_ArcadeGame` assets while only ~15 have a scene on disk.

---

## 1. The shape, and why it is this shape

The request was "press a card and play a mini version of that game, rendered into the card's
preview image". Three things in that are load-bearing and one is not.

**A preview cannot be playable inside the thumbnail.** Input ownership on this platform is
*exclusive*: `ScreenSwitcher.HandleEnterFreestyle` sets `EventSystem.sendNavigationEvents = false`
so the pad flies the ship instead of double-driving the UI. A vessel under player control behind a
live, interactive card grid is two consumers of one stick — and a 300 px window is not a viewport.
So the two halves were split: the **diorama** is what you look at while choosing, the **Test
Flight** takes the whole screen when you commit.

**The preview must not stand a second world up beside the menu.** Menu_Main boots the Lattice cell
— the largest collider budget of any cell, ~42,840 grown prisms at cap. So a Test Flight
**replaces** the menu world through `Cell.RequestCellSwap`, the one sanctioned runtime world-swap
door, exactly as the Wanderway run and the Cell Selector toy already do. Collider budget stays
flat, and there is never any ambiguity about which cell the vessel is in.

**The preview must not be the mode.** Every mode is a scene-placed `MiniGameControllerBase`
(`NetworkBehaviour`) expecting turn monitors, a countdown, a scoreboard, an end-game sequencer and
a replicated `GameDataSO`. Standing one up inside the menu would fight `MainMenuController`'s state
machine and spawn `NetworkObject`s onto every party member. So the preview reuses the two things
that actually make a mode look and feel like itself — **its `CellConfigDataSO`** (the Cell owns the
environment, so the mode's own cell config *is* its arena) and **its vessel** — and nothing else.

---

## 2. The pieces

| Piece | Location | Job |
|---|---|---|
| `ModePreviewDefinitionSO` | `_Scripts/ScriptableObjects/` | Per-mode: preview cell, optional structure prop, vessel, objective metric/target, duration, spawn standoff, diorama settings |
| `ModePreviewLibrarySO` | `_Scripts/ScriptableObjects/` | Mode → definition lookup. `Resources/ModePreviewLibrary`. Excludes Tournament in code |
| `ModePreviewSession` | `_Scripts/Controller/Arcade/Preview/` | Owns a Test Flight: enter, exit, and the ONE way out |
| `ModePreviewRunner` | `_Scripts/Controller/Arcade/Preview/` | Watches one stat and a clock. Plain MonoBehaviour |
| `ModePreviewDiorama` | `_Scripts/UI/View/` | The modal's live scale model: private stage, one culled camera, one RenderTexture |
| `ModePreviewHUD` | `_Scripts/UI/View/` | Objective, progress, timer, exit |
| `ModePreviewSetupTool` | `_Scripts/Editor/` | `FrogletTools > Scene Setup > Setup Mode Preview` — wires the modal prefab and Menu_Main |

Assets: `Assets/_SO_Assets/Mode Previews/` (definitions), `Assets/Resources/ModePreviewLibrary.asset`.

---

## 3. The diorama

`CellMiniatureBuilder` — the same builder the Cell Selector toy uses — strides the environment
generator's own output (`SpawnableBase.GetTrailData` + `CellEnvironmentSpawnableBase.CachedLays`)
into **one mesh with a submesh per domain**, spawning **no prisms**. Generation is pure math; the
~97%-of-cost part of a real build is the per-prism `Instantiate`, which never happens.

Three rules keep it off the frame budget, and all three are load-bearing:

1. **A private layer.** The stage lives on `ModePreview` (layer 19) and the preview camera's
   `cullingMask` is *that layer alone*. This camera therefore never renders the menu world. Without
   the layer the feature would be a second full pass over the Lattice cell, which is the one thing
   it must not be — so `ModePreviewDiorama.EnsureStage` **fails loud and refuses to render** when
   the layer is missing rather than falling back to `Everything`.
2. **Distance.** The stage sits 50,000 units up, well beyond every gameplay camera's far clip
   (8,000 in Menu_Main; `CameraSettingsSO` defaults to 1,000). So no game camera can see it even if
   somebody later widens a culling mask. Float spacing at that distance is ~0.004 units against a
   50-unit model — invisible.
3. **Lifetime.** The camera and its RenderTexture (384×216) are enabled only while the modal is
   open. `Hide()` is called from the modal's `OnDisable` **and** from `CloseAndNotifyClients`,
   because `ModalWindowOut` hides via CanvasGroup and does not deactivate the object.

The stage light also carries a `cullingMask` — lights ignore layers unless told to, and a stage
light falling on the whole menu world would be a subtle, hard-to-attribute bug.

Models are built **one frame after the modal opens** (so opening is never gated on a generation)
and cached per cell config, with the generator's lay data released immediately after sampling.
Meshes are owned here and destroyed in `OnDestroy`, the same contract `CellSelectorToy` follows.

---

## 4. The Test Flight

Every step is an existing, shipped path:

```
OnTestFlightClicked
 └─ session.SetModeVessel(mode's own hull from SO_ArcadeGame.Vessels)
 └─ session.TryBegin(definition)
     ├─ MenuCrystalClickHandler.ToggleTransition()      chrome fade, camera blend, input gate
     ├─ MenuServerPlayerVesselInitializer.RequestSwap() the mode's hull (pose/speed/domain kept)
     ├─ Cell.RequestCellSwap(definition.PreviewCell)    menu world suctions out, mode world blooms in
     ├─ CellSpawnFormation.Build(1, …)                  opens on the framing the real mode opens on
     ├─ Instantiate(StructurePrefab)                    local prop only, if the mode needs one
     └─ ModePreviewRunner.Begin(...)                    watch one stat, watch the clock
 └─ CloseAndNotifyClients()
```

Exit reverses it and reopens the card the player left from.

### 4.1 One way out

Every exit funnels through the single idempotent `ModePreviewSession.End`:

| Route | How it reaches `End` |
|---|---|
| HUD **LEAVE** button | `RequestExit()` |
| Gamepad **Start**, on-screen Volume/Pause | freestyle drops → `Update` sees it |
| Objective reached / timer expired | `ModePreviewRunner` callback |
| Launching the real game | `GameDataSO.OnLaunchGame` → `AbortHard` |
| Scene teardown / destroy | `OnDestroy` → `AbortHard` |

The freestyle check is a **state test, not a falling edge**, so a drop that lands mid-entry (before
the watcher ever saw a `true`) is caught too. `End` is a no-op while `Idle` or `Exiting`, so the
exit it triggers — dropping freestyle — can never feed back in as a second exit.

This discipline is copied deliberately from `WanderwayRun`: a "leave the world and come back"
feature dies of the exit path nobody remembered.

### 4.2 The invariants it respects

- **Local only.** Nothing carries a `NetworkObject`; `SpawnStructure` refuses a prefab that does,
  loudly. Menu_Main runs a live host, so a networked prop would land on every party member. A party
  member keeps flying the menu world while you fly the preview — the same thing that already
  happens when you pick a different world with the Cell Selector toy.
- **`GameDataSO` is never written.** It is the real launch config and it syncs to clients.
- **Mass is conserved.** A cell swap is an explicit, player-initiated world change — the same class
  of event as a scene load, and `Docs/ECOSYSTEM.md §19`'s sanctioned removal. Nothing here runs on
  a clock, ages a prism out, or culls a population. The preview's `DurationSeconds` ends the
  **flight**, not the world.
- **Continuity of existence.** Both swaps suction out and bloom in behind the standard
  `EnvironmentLoadVeil`; the diorama model grows in from zero; the HUD fades.
- **The objective reads the mode's own metric.** `ScoringMetrics.Read` against the same
  `ScoringMetric` the mode scores on — but **relative to a baseline** taken at `Begin`, because
  `RoundStats` live on the persistent Player object and have been accumulating for the whole menu
  session.

---

## 5. Authoring a preview

1. Create a `ModePreviewDefinitionSO` (`ScriptableObjects > Game > Mode Preview`).
2. Point `PreviewCell` at **the mode's own shipped `CellConfigDataSO`**. That is the default and it
   is why most modes need no new assets.
3. Leave `Vessel` at `Any` — a vessel-locked mode already declares its hull on its `SO_ArcadeGame`,
   and the modal passes it down.
4. Pick an objective metric the mode already scores on, and a **small** target. This is a taste.
5. Set `SpawnDistanceOutsideNucleus`. **A cell with no nucleus reports radius 0**, so this field
   carries the whole standoff — Ribcage and the Boneyard both need a large value or the preview
   opens *inside* the arena.
6. Add it to `Resources/ModePreviewLibrary`.
7. Run `FrogletTools > Scene Setup > Setup Mode Preview` if the wiring is not already in place.

### 5.1 Shipped definitions

| Mode | Cell | Objective | Diorama | Notes |
|---|---|---|---|---|
| Rampage | Rampage Cell Config 1 | 150 prisms destroyed / 90 s | ✗ | Arena is GROWN flora — see §5.2 |
| Ribcage | Ribcage Cell Config 1 | 200 prisms destroyed / 90 s | ✓ | Spawn 500 — no nucleus, cage outer radius 360 |
| Wildlife Liberation | WL Cell Config 1 | 5 lifeforms killed / 90 s | ✓ | Spawn 1200 — outside the 1050 outer cage |
| Dog Fight | Boneyard Cell Config 1 | open-ended / 60 s | ✓ | Scores on gunnery; solo has nobody to shoot |
| Scarab Scramble | Scarab Scramble Cell Config | open-ended / 60 s | ✗ | Court IS the nucleus; hoops controller-built, see §6 |

### 5.2 A grown arena has no diorama, and that is not a gap

`CellMiniatureBuilder` samples a **generator's** output, so a cell only has a scale model if it
authors an `EnvironmentPrefab`. Two of the five shipped modes do not: **Rampage**'s arena is a
flora forest seeded by its `SpawnProfile`, and **Scarab Scramble**'s court *is* the nucleus.
`ModePreviewDefinitionSO.HasDiorama` is the gate, and the modal falls those modes back to the
legacy video while **still** offering a Test Flight — which is the right split, because the
flight shows a grown arena perfectly and only the static model cannot. Taking the diorama branch
for them would render an empty black window, which reads as broken rather than as absent.

Modelling grown mass (flora populations, the nucleus) is the obvious extension, and it is the
same question the Cell Selector's mini-cells already have.

**Maelstrom (Tournament) is excluded in code**, not by omission: it draws *other* modes per round,
so it has no arena of its own to shrink.

---

## 6. Known limitations

- **A mode whose gameplay structure is built by its controller previews as an empty arena.**
  Scarab's hoops, Astro League's goals and HexRace's track are built by the controller from a
  settings SO with `NetworkVariable`s, not by the cell. `StructurePrefab` is the hook for a local,
  `NetworkObject`-free stand-in prop; nothing authors one yet. The better long-term fix is to
  extract each of those arena builders so the controller and the preview call the same code — the
  preview should never grow its own copy of a mode's geometry.
- **A preview reuses the mode's shipped cell config at intensity 1.** Nothing is scaled down. If a
  mode's arena turns out to be too heavy to build inside the menu, author a lighter variant config
  and **re-measure its `PhaseThresholds`** (`FrogletTools > Ecology > Measure Cell Environment
  Baselines`) — a small world inheriting a big world's volume ladder pins at Frenzy immediately.
  Prefer `SpawnProfileSO.FloraPopulationScale` / `FaunaPopulationScale` / `FloraPlantBudgetScale`
  over forking per-species assets.
- **A stat channel that only fires in a real match reads 0 here.** The runner treats that as a
  flight with no counter rather than as an error, which is the right failure: the arena is most of
  the value.
- **In a party, a Test Flight is visually solo.** Your world swaps, theirs does not; the vessels
  still replicate, so you see each other in what look like different places. Identical to the
  existing Cell Selector behaviour.
- **The generated UI is functional, not designed.** The setup tool places a Test Flight button and
  a HUD panel at sane coordinates; both want a layout pass.
- **Not verified in the Editor.** The C# is Roslyn syntax-clean and `Tools/CI/validate_project.py`
  passes, but no Unity compile or play-mode run has happened on this branch — see §7.

---

## 7. Verification still owed

- `/verify-unity` (Editor compile + load) — not run; no Unity in the authoring environment.
- Play-mode: open the arcade, select each of the five shipped modes, confirm the diorama renders
  and turns, and that the main camera never shows the stage.
- Test Flight each of the five: entry, objective counting, all five exits (button, Start, timer,
  target, launch), and that the menu world comes back intact each time.
- Frame cost with the modal open, on the Lattice cell, on a mobile target.
