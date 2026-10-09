---
name: prisma
description: Work on Amoebius (formerly Amoebius; the Cosmic Shore .NET port under Port/) - build it, run the real game, see it, drive it and inspect it live through the engine's MCP tools or control port. Use for anything under Port/, for "run the game in our engine", "take a screenshot of the port", "why does the port show X", or to check an Assets/_Scripts change in the engine without Unity.
---

# Amoebius (formerly Amoebius)

**Read `Port/CLAUDE.md` first.** It holds the map, the loop and the pitfalls; this skill is the
checklist.

1. **Tools.** If `mcp__prisma__*` tools are available, use them. If not, either connect
   them (`claude mcp add prisma -- dotnet run --project Port/src/CosmicShore.Mcp --`, or
   start with `--mcp-config Port/.mcp.json`) or use the control port with curl
   (`Port/CLAUDE.md` § The loop).
2. **Build before you run.** `engine_build` after every C# edit, in `Port/src` or in
   `Assets/_Scripts` (the engine compiles the game's own source live).
3. **See it, don't assume it.** `game_start`, then `game_screenshot` after every action that
   should change the screen. Coordinates are screenshot pixels from the top-left. Wait
   (`game_wait`) a few frames after input; under xvfb the game runs at a few frames per second.
4. **Start from Amoebius's memory.** `prisma_tracks` has every recorded run's problems, performance
   and audio; `prisma_board` the open bugs and tasks. Suggest what you find but won't fix now with
   `prisma_board_suggest`, always with a `criterion`: the check that will prove it done. A card's
   "done when" is its acceptance test: run it and show the result before calling the work done.
5. **Multiplayer: several players at once.** `net_players action=start players=N` (1-4, a party
   is four) starts them over one session folder; `net_input` / `net_command` drive any of them,
   `net_sim` gives each a bad line (latency, loss, a pulled cable), `net_fault` breaks the session
   service, `net_stats` reads traffic and RTT. `relay=local` sends them through Froglet's relay
   server as an internet game would; `relay=ugs` signs them in to the game's LIVE UGS project, so
   only when the owner asks. `Docs/MULTIPLAYER_START_HERE.md` first, then `Port/docs/MULTIPLAYER.md`.
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
   original `Assets/` file and line. Treat a `PRISMA001` warning (an RPC Amoebius cannot intercept)
   as a bug.
9. **Before committing**: `engine_test`, then `unity_isolation_check` - the port must not change
   anything Unity reads. `game_stop` when done.

Report what you saw (attach the screenshot path) and what you verified; say plainly if a check
did not run.
