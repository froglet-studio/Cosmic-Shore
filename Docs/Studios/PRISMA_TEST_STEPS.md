# Testing the Vessel Studio in Amoebius: step by step

There are two ways to test, and Amoebius gives you both from one page (**VESSEL STUDIO**, called STUDIOS before 2026-10-09):

| Button | What opens | What it tests |
|---|---|---|
| **OPEN IN AMOEBIUS** | The studio page (`Docs/Studios/VesselStudio/stoat.html`) in its own window: Edge's or Chrome's app mode, with no tabs or address bar, and Amoebius's own window profile | **The design:** the round-14 dipole sling, play styles, course ladder, AI sim lab, editor layout |
| **PLAY IN ENGINE** | The game itself in the Amoebius player: boot, menu, the **Slingshot** card, then Start | **What is built:** the game's own Stoat (the round-4 orbit sling the game ships today) |

The design in the studio is ahead of the game. The dipole sling, momentum and the five styles exist only in
the studio until they are ported (`STOAT_SIM_LAB_PLAN.md` §5, and the vessel-contract decision in
`STOAT_PLAY_STYLES.md` §4).

## 0. Get Amoebius onto this branch (once)

1. Pull `Ys-bleeding-edge` in GitHub Desktop.
2. Open Amoebius by either route:
   - In Unity: **FrogletTools ▸ Amoebius ▸ Vessel Studio Page**. This opens Amoebius straight on VESSEL STUDIO, building it first if needed.
     (**FrogletTools ▸ Vessels ▸ Vessel Studio** opens the studio home in Unity; a card's OPEN STUDIO opens that studio served by Amoebius, built from Unity's branch, with Sync, Ask and Decisions working.)
   - Or start `Prisma.exe` and click **VESSEL STUDIO** in the left rail.
3. The title bar shows the branch Amoebius plays. If it isn't `Ys-bleeding-edge`, pick it on the
   **GIT** page (or click **FOLLOW UNITY**). Without the studio folder on its branch, the VESSEL STUDIO page says so
   and names the branches that have it.

## 1. The studio in its own window (OPEN IN AMOEBIUS)

1. VESSEL STUDIO ▸ pick **STOAT** ▸ **OPEN IN AMOEBIUS**. A window opens with no browser chrome.
   - **Pass:** its address (hover the window title or press Ctrl+L in a browser) is `http://127.0.0.1:<port>/<token>/stoat.html#amoebius`,
     and the line under the card says **Serving <branch> @ <commit>**. The top bar reads **Running on PC (mouse / trackpad)**,
     exactly as on claude.ai and the live mirror (no host label, /vessel-studio D33).
   - With no Edge or Chrome installed, it opens in your default browser instead (same address).
1b. **Sync, Ask, Decisions work here** (D33).
   - **Sync** (bottom right) ▸ the top line reads **Showing froglet-studio/cosmic-shore @ <branch> · <commit>**; the session box
     already holds `amoebius-local`. Under Merge, From `vessel-studio`, Into `Ys-bleeding-edge`, **Compare**. **Pass:** the
     console prints "a vs b: N ahead, M behind" and the commits (needs Python 3). Merge into `Ys-bleeding-edge` is refused
     (a shared base branch); never test Merge or Delete on a branch you care about.
   - **Decisions**: type a line, **Record decision**. **Pass:** it appears in the list at once with your name; it is still
     there after closing and reopening the window (stored under Amoebius's data folder, `studio/db/decisions.json`).
   - Hub (ALL STUDIOS ▸ OPEN IN AMOEBIUS) ▸ **Ask the studio agent**: ask a question. **Pass:** the answer streams in
     (needs Claude Code signed in on Amoebius's AGENT page). **File it as a request**: it shows in the list below.
2. **Layout.**
   - The stage fills the middle.
   - The right dock has tabs: Sling, Dipole, Sim lab, Styles, Black hole, White hole, Life, Course, Lope, Archive.
   - The bottom dock has tabs: Runs, Scorecard, Controls, Decisions, About.
   - **Pass:** nothing scrolls the window.
3. **Pop out.** In any tab, press **⧉ Pop out**.
   - **Pass:** the tab becomes a floating mini window you can drag by its title bar and resize from its corner.
   - **⇲ Dock** puts it back.
   - Close the studio window and open it again: tabs, sizes and open windows come back.
4. **Course ladder.** Course tab ▸ buttons **1–4**.
   - **Pass:** 1 is a flat plain circle; 2 has tilted rings; 3 climbs, with sharper tilts; 4 has side-on and dive rings.
   - The note under the buttons describes each level.
5. **Fly it** (gamepad or keyboard; the Controls tab lists the keys). Squeeze LT/RT to lay a pair, hold to go round, release to sling.
   - **Pass:** the HUD shows **momentum +N** after a few slings, and your speed climbs with each one.
6. **AI flies, you watch.** Sim lab tab ▸ tick **AI flies the Stoat**.
   - Switch the camera between Chase, Follow (wide) and Free (drag to orbit, wheel to zoom), and the speed between 1×, 2× and 4×.
   - **Pass:** the caption at the bottom names the style, and the AI restarts each race on its own.
7. **Scorecard.** Sim lab ▸ **Score all five styles**.
   - **Pass:** after a few seconds the **Scorecard** opens as its own window, and each style wins its own column, in its colour:
     - Comet: average speed;
     - Flare: top speed;
     - Needle: ring error;
     - Anchor: rookie catch rate;
     - Maelstrom: prisms per sling.
   - **Show scorecard** reopens it.
8. **Phone layout preview.** Top bar ▸ **Layout: Phone**.
   - **Pass:** the touch sticks and trigger handles appear.
   - **Layout: Auto** returns to PC.

## 2. The game's own Stoat (PLAY IN ENGINE)

1. VESSEL STUDIO ▸ STOAT card ▸ **PLAY IN ENGINE**.
   - Amoebius builds the game (the first build takes a few minutes; the bar at the bottom shows progress) and starts it.
2. A **new profile** answers three first-run prompts: birth year, the data-collection choice, then a username.
   - After that it goes on by itself.
   - A profile you have used before skips them (Amoebius's PLAY ▸ profile field).
3. **Pass (automatic):** the game reaches the main menu, opens the **Slingshot** card and presses **Start GAME**.
   - The CONSOLE page shows:
     - `[arcade] --arcade Slingshot: opening the card`
     - `[arcade] card pressed: Slingshot`
     - `[arcade] start pressed`
4. In the race, press **Ready** (or the ready button on the HUD). **GO** shows, and the Stoat flies.
5. Sling: hold **Right Shift** (RT) or **Left Shift** (LT), or the triggers on a gamepad.
   - **Pass:** the diagnostics panel's **BlackHole** row changes from `none` to `N live, … bodies, … stretching`.
   - The prisms near the hole stretch.
6. Fly the rings: the **THREAD SWITCHES 0/16** counter at the top left counts them.

**Known in Amoebius today:**

- **The black hole's lens is not drawn.** Its render pass runs through URP's render graph, which the engine
  compiles but does not execute yet (`Port/src/CosmicShore.Engine/Rendering/RenderGraph.cs`). The hole's
  physics, the tides on prisms and the HUD all run.
- **`CrystalFlipWave.LateUpdate` throws a NullReferenceException every frame.** This happens in the menu and
  in races, and is the same on bleeding-edge (the file is identical). It is not from this branch; it is
  logged for the engine/crystal owners. The game keeps running.
- **Under software rendering** (a server without a GPU) the game runs at about 10 fps. On a Windows PC with a GPU it is normal speed.

## 3. Phone

1. Open the published Vessel Studio in the phone's browser (https://yskhan61.github.io/vessel-studio/, or the claude.ai artifact).
2. Pick **Stoat**. It opens straight into the touch layout.
3. Hold the phone sideways:
   - thumbs on the two sticks;
   - index fingers drag the LT/RT handles down to squeeze.
4. ✕ returns to the settings tabs.

## 4. What was checked before this was handed over (Linux, headless and xvfb)

| Check | Result |
|---|---|
| The game on this branch compiles in Amoebius | It did not before. It failed on engine API gaps from this branch's code, all now in the engine:

  - the black-hole field's transform jobs (`TransformAccessArray`, `IJobParallelForTransform`);
  - its lens pass (URP's render graph) and sky capture (`CommandBuffer`);
  - the black-hole tool's `typeof(SerializeField)`;
  - the mouse camera's `InputSystem.Controls.ButtonControl`. |
| Engine tests (`Port/tests/CosmicShore.Tests`) | 1,913 / 1,913 pass after merging both bleeding-edges (1,691 before), including the new GLSL port of `PrismGravityWarpDeform` |
| Launcher tests | 30 / 30 pass, including 3 new studio-catalog tests |
| The game's edit-mode tests on Amoebius | 352 / 352 pass (before and after the merges) |
| `--arcade Slingshot` (headless) | Bootstrap → Authentication → Menu_Main → card → Start → `MinigameSlingshot` at frame 258 (re-run after the merges: same) |
| Live run (xvfb, control port) | Menu, the Slingshot card, Ready, GO, the Stoat flying; one RT sling gave "2 live, 34 bodies, 2 stretching" |
| VESSEL STUDIO page (screenshot) | One picker (SQUIRREL · STOAT · ALL STUDIOS) and one card with OPEN IN AMOEBIUS · OPEN IN BROWSER · PLAY IN ENGINE · AGENT · DOCS and its engine note. |
| `stoat.html#prisma` (before D33) | Read "Amoebius · PC"; since D33 every surface reads "PC (...)", the page is served by Amoebius from the build |

**Not checked here:** a real Windows PC. Edge's app window, a GPU, and a gamepad through the app window all
need your first run.
