---
name: froglet-engine
description: Work on the Froglet Engine (the Cosmic Shore .NET port under Port/) - build it, run the real game, see it, drive it and inspect it live through the engine's MCP tools or control port. Use for anything under Port/, for "run the game in our engine", "take a screenshot of the port", "why does the port show X", or to check an Assets/_Scripts change in the engine without Unity.
---

# Froglet Engine

**Read `Port/CLAUDE.md` first.** It holds the map, the loop and the pitfalls; this skill is the
checklist.

1. **Tools.** If `mcp__froglet-engine__*` tools are available, use them. If not, either connect
   them (`claude mcp add froglet-engine -- dotnet run --project Port/src/CosmicShore.Mcp --`, or
   start with `--mcp-config Port/.mcp.json`) or use the control port with curl
   (`Port/CLAUDE.md` § The loop).
2. **Build before you run.** `engine_build` after every C# edit, in `Port/src` or in
   `Assets/_Scripts` (the engine compiles the game's own source live).
3. **See it, don't assume it.** `game_start`, then `game_screenshot` after every action that
   should change the screen. Coordinates are screenshot pixels from the top-left. Wait
   (`game_wait`) a few frames after input; under xvfb the game runs at a few frames per second.
4. **Inspect, don't guess.** `game_find` / `game_hierarchy` to locate objects, `game_get` to read
   live fields, `game_ui_at X,Y` for "what is that on screen", `game_dump_ui` for layout and
   anchors, `game_logs` for errors.
5. **Fix in the right place.** Engine gaps in `Port/src`; gameplay bugs in `Assets/_Scripts` on a
   separate Unity PR. Never edit `obj/live-src` (it is regenerated).
6. **Before committing**: `engine_test`, then `unity_isolation_check` - the port must not change
   anything Unity reads. `game_stop` when done.

Report what you saw (attach the screenshot path) and what you verified; say plainly if a check
did not run.
