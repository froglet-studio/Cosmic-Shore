---
name: amoebius
description: Work on Amoebius (the Cosmic Shore .NET port under Port/) - build it, run the real game, see it, drive it and inspect it live through the engine's MCP tools or control port. Use for anything under Port/, for "run the game in our engine", "take a screenshot of the port", "why does the port show X", or to check an Assets/_Scripts change in the engine without Unity.
---

# Amoebius

**Read `Port/CLAUDE.md` first.** It holds the map, the loop and the pitfalls; this skill is the
checklist.

1. **Tools.** If `mcp__amoebius__*` tools are available, use them. If not, either connect
   them (`claude mcp add amoebius -- dotnet run --project Port/src/CosmicShore.Mcp --`, or
   start with `--mcp-config Port/.mcp.json`) or use the control port with curl
   (`Port/CLAUDE.md` § The loop).
2. **Build before you run.** `engine_build` after every C# edit, in `Port/src` or in
   `Assets/_Scripts` (the engine compiles the game's own source live).
3. **See it, don't assume it.** `game_start`, then `game_screenshot` after every action that
   should change the screen. Coordinates are screenshot pixels from the top-left. Wait
   (`game_wait`) a few frames after input; under xvfb the game runs at a few frames per second.
4. **Start from Amoebius's memory.** `amoebius_tracks` has every recorded run's problems, performance
   and audio; `amoebius_board` the open bugs and tasks. Suggest what you find but won't fix now with
   `amoebius_board_suggest`, always with a `criterion`: the check that will prove it done. A card's
   "done when" is its acceptance test: run it and show the result before calling the work done.
5. **Multiplayer is UGS only.** START ONLINE (`game_start` `online: true`) signs in to the game's LIVE
   UGS project and plays through UGS Lobby and Relay, as the Unity build does; START is offline. There
   is no LAN play, local server or multi-player harness any more, so only start an online game when the
   owner asks. Engine-side work is tested against stand-ins (`UgsMultiplayerTests`, `NetStackTests`).
   `Docs/MULTIPLAYER_START_HERE.md` first, then `Port/docs/MULTIPLAYER.md` §6.10.
6. **Inspect, don't guess.** `game_find` / `game_hierarchy` to locate objects, `game_get` to read
   live fields, `game_ui_at X,Y` for "what is that on screen", `game_dump_ui` for layout and
   anchors, `game_logs` for errors.
7. **Data, models and editor tools without Unity.** `asset_datasets` / `asset_dataset` read the
   ScriptableObject data sets (fields with Unity's labels, ranges, enums, references, stale keys);
   change one with `cs-asset set <file> &<fileId> <path> <value>`. `asset_model` and
   `asset_model_preview` show an FBX as Unity imports it, with a picture. `asset_froglet_tools`
   lists the FrogletTools with their source - read a tool's source before doing its job. Scene and
   hierarchy edits go through `cs-asset` (create, add, delete, overrides, apply), never by hand.
8. **Fix in the right place.** Engine gaps in `Port/src`; gameplay bugs in `Assets/_Scripts` on a
   separate Unity PR. Never edit `obj/live-src` (it is regenerated); errors there already name the
   original `Assets/` file and line. Treat a `AMOEBIUS001` warning (an RPC Amoebius cannot intercept)
   as a bug.
9. **Before committing**: `engine_test`, then `unity_isolation_check` - the port must not change
   anything Unity reads. `game_stop` when done.
10. **UI bugs: test at the user's size, not the reference size.** The launcher opens the game at
   1600x900, and windows get resized. At 1920x1080 (the canvases' reference resolution) the scale
   factor is exactly 1, so scale- and resize-dependent layout bugs disappear. Reproduce at
   `game_start` size 1600x900, then `resize 1280x720` through the control port, before you call a
   menu correct. Three traps cost a whole session (2026-10-13):
   - **Unity saves DRIVEN RectTransform values as zero.** A slider's fill anchors, a scroll bar's
     handle and a layout child's size read 0 in the scene YAML. The component that drives them
     must run, or the bar draws empty. An all-zero rect in a scene usually means "driven", not
     "broken data".
   - **Never write a RectTransform's `localPosition` before it has its real parent.** The setter
     back-solves `anchoredPosition` against the current parent rect, and a zero rect bakes the
     anchor point into it. Stretched children then sit off their anchors (cloned cards).
   - **Copy uGUI semantics, not approximations.** The old ScrollRect said vertical position 0 was
     the top; Unity's 0 is the bottom, and the game relies on that (`verticalNormalizedPosition = 0`
     means scroll a log to the newest line). When a component misbehaves, port Unity's rule for
     rule (ugui 2.0), then fix the tests that pinned the approximation.
   Drive scrolling with `game_input` `scroll X,Y N` (wheel notches) and `drag X,Y X,Y F`.

11. **Multiplayer runs two games, online, with the owner's go-ahead.** Give each its own `profile`
   (each is its own UGS player) and `online: true`; both sign in to UGS and meet in its Lobby. In
   Menu_Main: ARCADE, the party panel's "+", then Invite (accept it in the other game's REQUESTS) or Join
   on the row. Both press START GAME; the match begins when every player presses the HUD's GO (the
   ReadyButton) - a GO that stays on screen is waiting for a press. Check replication with
   `get "Manta(Clone)" NetworkObject OwnerClientId` and `Transform position` on both games (`get` prints
   every match). Stop a stray game by PID (`pgrep -x CosmicShore`), never `pkill -f 'CosmicShore ...'`:
   the pattern also matches the shell running your command, so it kills that shell too.

12. **Traps from the UGS-online session (2026-10-14).**
   - **Amoebius.exe and the game update separately.** START pulls and builds the GAME from the branch;
     the launcher itself only changes through its own UPDATE (rail badge, or SETTINGS > ABOUT > CHECK /
     INSTALL). A launcher feature you just pushed is invisible to a user who only pressed START: tell them
     to update Amoebius, and read the PLAY page's own buttons in their screenshot to see which build they run.
   - **A file the player window uses must also be linked into `CosmicShore.Mobile.csproj`.** The phone build
     compiles the shared player sources by link; `MobileLinkTests` fails when one is missing (it caught
     `XInputRumble.cs` and `PlayerDiagnostics.cs`). Add the `<Compile Include=... Link=...>` with the file.
   - **Launcher screenshots under xvfb show a tooltip** wherever the pointer sits (the screen's centre).
     Use a virtual screen much larger than the window (`xvfb-run -s "-screen 0 3400x2200x24"`) so the
     pointer is off it.
   - **Never assume the user's workspace path.** `%LOCALAPPDATA%\Amoebius\workspace` is only the default;
     BESIDE MY CLONE puts it next to their clone. Prefer a launcher button over hand-run PowerShell.
   - **F3 is Amoebius's diagnostics overlay** (`PlayerDiagnostics.cs`, ARCHITECTURE §11.3): read it in a
     user's screenshot for fps, sign-in, lobby and ping before guessing. The game's own top-left panel (F7)
     reported 60 fps while the game ran at 5-9, so trust F3's numbers.

Report what you saw (attach the screenshot path) and what you verified; say plainly if a check
did not run.
