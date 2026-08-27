# Quest Graph Editor Tool — User Guide

How to author, edit, and **test** the FTUE / Main Quest with the visual tool. This is the
hands-on guide; the architecture/data-model reference lives in `QUEST_GRAPH_TOOL.md`.

The tool is the single control surface: every change you make in it (graphs, toggles, delays,
dialogue text) is written to the assets under `Assets/FTUE/DataContainer/` and **lands in git**.

---

## 1. Opening the tool

**`FrogletTools ▸ Quest Graph Editor`**

One-time setup menus (only needed on a fresh scene / fresh checkout):

| Menu | What it does |
|---|---|
| `FrogletTools ▸ Quest Graph ▸ Setup Runner In Scene` | Drops/wires `QuestGraphRunner` into the open scene (Menu_Main) and auto-resolves its references |
| `FrogletTools ▸ Quest Graph ▸ Wire Phase 0 UI (Menu_Main)` | Wires the hand-built UI: instruction sets, dialogue panel, toast notifier, nav buttons (Hangar + Profile lockable, Arcade allowed), progression service, and the **Quest Buttons** list (auto-registers the Episodes button under key `episodes`). Review the console mapping, then **save the scene** |
| `FrogletTools ▸ Quest Graph ▸ Create Main Quest (Default Content)` | Regenerates the whole Main Quest from the design map. ⚠ Overwrites generated content — your hand-edited phase assets are the source of truth, so only use this when you deliberately want a fresh graph |
| `FrogletTools ▸ Quest Graph ▸ Layout All Phases (Rows)` | Re-arranges **every** phase graph into venue rows (see §3) and saves. Safe to re-run any time |

## 2. Window tour

```
┌──────────────────────── Toolbar ────────────────────────┐
│ + Add Node · Frame (F) · Layout Rows · Node Colors ·    │
│ ◧ Quests · Inspector ◨ · zoom % · Save/Save*            │
├──────────┬──────────────────────────────┬───────────────┤
│ QUESTS   │           CANVAS             │  INSPECTOR    │
│ quest    │  nodes + edges of the        │  quest info + │
│ list,    │  selected phase graph        │  progress +   │
│ ordered  │                              │  phase info + │
│ phases   │                              │  node fields  │
└──────────┴──────────────────────────────┴───────────────┘
```

- **Left panel (◧ Quests)** — quests and their ordered phases. Add / reorder (▲▼) / remove (✕)
  phases; per-quest and per-phase **enable checkboxes** (the test harness). A **▶ marker** shows
  the phase the player's saved progress is currently in.
- **Canvas** — the selected phase's node graph. Node headers are color-coded by category
  (toggle the legend with **Node Colors**).
- **Right panel (Inspector ◨)** — from top to bottom: QUEST (enable, id, notes, rename,
  player progress, Force-Advance, live state, reset), PHASE (enable, name, notes),
  NODE (enable, display name, entry-node button, typed fields, connections), VALIDATION.
- Both sidebars hide/show from the toolbar and **resize by dragging their inner edge**;
  layout is remembered per user.

### Canvas controls

| Action | Input |
|---|---|
| Move a node | drag it |
| Connect | drag from an output port onto another node |
| Spawn-and-connect | drag from an output port, release on **empty canvas** → pick a node type |
| Add a node | right-click the canvas (or toolbar **+ Add Node**) |
| Delete a node | select + `Delete` |
| Zoom | mouse wheel (cursor-anchored) — click the toolbar **%** to reset |
| Pan | middle-drag or Alt-drag |
| Frame the graph | **F** |
| Re-arrange into rows | toolbar **Layout Rows** (undoable — press Save to keep it) |
| Tooltips | hover a node header or a port |

## 3. Editing a graph

1. **Pick the quest** in the left panel, then the **phase** — its graph loads on the canvas.
2. **Add nodes** (right-click) and **connect** them port → node. The flow starts at the
   **entry node** (set via *Set As Entry Node* in the node inspector) and follows `next` edges.
3. **Edit fields** on the selected node in the inspector — dialogue lines, gate targets,
   instruction text, funnel settings, etc. All content is authored **on the node**.
4. **Pace with edge delays** — in the node inspector's **Connections** section every connected
   port has a `↳ Delay (s)` field: a real-time pause before the next node runs. Delayed edges
   show a ⏱ label on the canvas.
5. **End the phase** — every path must reach a **PhaseEnd** node (or **End** on the final
   phase, which completes the whole quest). Validation flags dead-ends.
6. **Save** — the toolbar button reads **Save\*** when there are unsaved edits; press it (or
   Ctrl+S). Enable-toggle changes save to disk automatically so they always show up in git.

### Canvas layout — one row per place the player is standing

Graphs are arranged in **rows, not columns**. The flow reads left→right along a row, and a new
row starts wherever the beat moves the player between the **app shell** (menus, arcade, profile)
and **gameplay** (freestyle flight, a launched match) — so you can see at a glance where the
player physically is at every point of a track, and every row break is a real context switch.

```
row 0  gameplay   enter freestyle → …flight-school beats…
row 1  app shell  exit freestyle → lock nav → dialogue → funnel → CTAs
row 2  gameplay   wait for the match to be played          (the away trip)
row 3  app shell  …everything that greets them on the way back… → Phase Complete
```

**Layout Rows** (toolbar) re-arranges the open phase this way — undoable, and it takes effect in
git once you press **Save**. `FrogletTools ▸ Quest Graph ▸ Layout All Phases (Rows)` does the
whole quest at once. You are free to drag nodes anywhere afterwards; Layout Rows just puts the
canonical arrangement back.

Where the breaks land is declared **on the node type** (`Venue` / `VenueAfter` in code), not
guessed from the layout — the enter/exit-freestyle nodes, the game-launch/played gates, and the
intensity milestone gates are the only ones that move the player. If you add a new node type
that takes the player in or out of gameplay, override `Venue` on it or its row break won't
appear. Details: `QUEST_GRAPH_TOOL.md` § "Canvas layout — venue rows".

### Enable toggles (mute anything without unwiring it)

| Level | Where | Runner behavior |
|---|---|---|
| Quest | left panel + quest inspector | never starts a disabled quest |
| Phase | left panel + phase inspector | skips disabled phases |
| Node | node header, node inspector, node context menu | passes straight through (follows its `next` edge) |

## 4. Node reference

| Category | Node | What it does |
|---|---|---|
| Flow | **Wait** | Fixed real-time delay |
| Presentation | **ShowInstruction** | Instruction overlay text (+ optional keyed panel + haptic); persists while gates hold |
| Presentation | **Dialogue** | Captain dialogue panel; lines authored on the node; Next advances, last line closes |
| Gameplay | **EnterFreestyle** | Forces the menu vessel into player control |
| Gameplay | **ExitFreestyle** | Waits for the player's own volume-button exit (`forceExit` drives it instead) |
| Gameplay | **Navigate** | Force-navigates to a screen / the arcade modal |
| Gameplay | **LockModes** | One-shot card lock (prefer SetArcadeConstraints) |
| Gameplay | **SetArcadeConstraints** | The arcade funnel: one clickable card, one intensity, player/domain defaults. Persists across scene loads AND play sessions — **every phase that applies it must also clear it** (Clear mode) |
| Gameplay | **LockNavigation** | Disables the Hangar/Profile nav buttons (Arcade stays available); always restored on teardown |
| Gameplay | **SetButtonInteractable** | Flips a runner-registered scene Button by key (e.g. `episodes`) — used to unlock UI the FTUE gates |
| Gate | **WaitForInput** | A specific accepted control input |
| Gate | **WaitForDrift** | Sustained drift (LT+RT) with hold time |
| Gate | **WaitForSkim** | N prisms skimmed; live "n / target" counter on the instruction text |
| Gate | **WaitForGameLaunch** | A game is launched |
| Gate | **WaitForGamePlayed** | A game is finished (mode + min-intensity filters; survives scene round-trips) |
| Gate | **WaitForIntensity** | A mode reaches an intensity tier |
| Gate | **WaitForModeUnlocked** | The player claims/unlocks a mode on the quest track |
| Gate | **WaitForUserAction** | Generic `UserActionType` gate (ViewProfileMenu=500, ViewEpisodeMenu=600, UnlockVessel, …) |
| Guidance | **HighlightCTA** | Lights a CTA breadcrumb and waits for its completion action; one-shot (retracts when the beat completes by any means) |
| Progression | **UnlockMode** | Direct mode unlock via the progression service |
| Terminal | **PhaseEnd** | Ends the phase (also clears any active arcade funnel) |
| Terminal | **End** | Completes the whole quest |

New node types added in code appear in the right-click palette automatically.

## 5. Testing workflow (Play mode)

Everything below lives in the **QUEST** section of the inspector.

### Where am I? — the checkpoint view

- **✓** on every node the player has completed (canvas overlay).
- Amber **▶ NEXT** banner on the node the quest is currently waiting at / will resume from.
- **▶** on the current phase in the left panel.
- **PLAYER PROGRESS** box: phase, resume node, completed-node count.
- Updates live (~2×/s) while playing; also correct in edit mode from the saved local progress.

### ▶ Force-Advance Current Node

Completes the **▶ NEXT** node as if the player did it — and applies the **real** state it was
waiting for (tier unlocks write real intensity, mode gates really unlock the mode, CTA gates
fire the real user action). Progress persists exactly like a real advance, so you can chain
Force-Advance through an entire run without replaying every game.

### LIVE STATE box

Answers "why is this card locked / this intensity blocked?" at a glance:

- **Arcade funnel** — active/inactive, allowed mode, forced intensity/players/domains,
  plus a **Clear Arcade Funnel Now** escape hatch.
- **Unlocked modes** — each with its max unlocked tier.

### Reset ALL Player Progress

- **In Play mode:** full reset — quest cursor + completed nodes, arcade funnel, played-game
  records, mode/intensity progression, vessel unlocks (Squirrel re-unlocked as the starter).
- **In Edit mode:** clears the local quest mirror only (with the backend gate closed that IS
  the full reset — progression is session-local and starts fresh every play).

### Runner debug fields (on the `QuestGraphRunner` component)

| Field | Use |
|---|---|
| `debugDisable` | Hard off switch — quest never runs in this scene |
| `debugForceRun` | Run from the start, ignoring completion + saved progress |
| `debugPhaseOverride` | Run ONE phase graph, no persistence writes |

## 6. Persistence (current: LOCAL-ONLY)

`ProgressionBackendGate.CloudEnabled` is **false**: quest progress lives in PlayerPrefs,
and mode/intensity/vessel progression resets fresh each play session (a starter normalization
runs at sign-in: Squirrel unlocked, everything else locked). Flip the gate to `true` to restore
UGS cloud sync once the FTUE is signed off — no other changes needed.

## 7. Common recipes

| I want to… | Do this |
|---|---|
| Change dialogue/instruction text | Select the node → edit lines in the inspector → Save. Node text always wins over whatever is in the scene TMP |
| Add a pause between two beats | Select the upstream node → Connections → `↳ Delay (s)` |
| Skip a beat for a test run | Untick the node's enable checkbox (auto-saves) |
| Test one phase in isolation | Assign it to the runner's `debugPhaseOverride`, enter Play mode |
| Jump forward during a run | Spam **▶ Force-Advance** — it writes real progression state as it goes |
| Restart testing from zero | Play mode → **Reset ALL Player Progress** → restart Play mode |
| Funnel the arcade for a beat | **SetArcadeConstraints** (Apply) … your beats … **SetArcadeConstraints** (Clear). Validation warns if a phase applies without clearing; PhaseEnd clears as a safety net |
| Let a quest node enable a scene button | Register the Button on the runner's **Quest Buttons** list (or re-run the Phase 0 wirer), then use **SetButtonInteractable** with that key |
| Rename the quest asset | QUEST section → type the name → **Rename Asset** |
| Tidy a graph after adding beats | Toolbar **Layout Rows** → **Save**. For every phase at once: `FrogletTools ▸ Quest Graph ▸ Layout All Phases (Rows)` |

## 8. Troubleshooting

| Symptom | Check |
|---|---|
| Quest never starts | Quest/phase enabled? Not already COMPLETED (see PLAYER PROGRESS — reset to replay)? `debugDisable` off? Runner in the scene with the quest assigned? |
| Stuck — nothing advances | Find the **▶ NEXT** node on the canvas: that's the gate the quest is waiting on. The Console `[Quest]` lines log every node enter/advance |
| A card/intensity is locked unexpectedly | LIVE STATE box — if the funnel is active, **Clear Arcade Funnel Now**; otherwise it's real progression (unlock via Force-Advance or play) |
| A CTA glows but tapping does nothing | The target button may be authored non-interactable — precede the CTA with **SetButtonInteractable** |
| `SetButtonInteractable: no button registered` warning | The runner's Quest Buttons list is missing that key — re-run the Phase 0 wirer or add the entry manually |
| My tool edits aren't in git | Toolbar shows **Save\*** → press Save. Toggles save automatically |
| Validation warnings | Fix before shipping: unreachable nodes, dangling edges, missing terminal, funnel applied but never cleared |
