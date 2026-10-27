# Videos and previews from a session: when to ask, what they cost, how they are made

Yash, 2026-10-27: "Let me know if it's more expensive to ask you for short videos rather than me going in the game
and checking ... unless it's too expensive, I would like you to give me videos in future. Prototypes and feature
development." This file is the answer. It is measured from the first run (an all-AI Skim Race in Amoebius, its replay
kept, a 60 s video), not estimated.

## 1. The rule

1. **A session that changed how something LOOKS or MOVES shows it**, when it can do so cheaply (section 3), instead of
   handing the person a "go and check" step. Default to a **still** (one screenshot, or a contact sheet of 4-6 frames);
   make a **video** when the change is about motion or order of events (a race, a replay, an ability, an AI's line, a
   menu flow). A clip is at most **60 s at 15 fps, 1280x720, H.264** (a few MB).
2. **The person still judges in Unity whatever Unity alone can show** (section 4), and the session says so in the
   same message: "this video is Amoebius; the final look is the editor's". A session video never replaces the
   `/verify-unity` gate or the hand test in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
3. **The session looks at frames, not at the whole video.** It reads at most 3-4 frames (start, middle, end, plus one
   where the change should show) to check the clip shows what it claims, then sends the file with `SendUserFile`. It
   never streams every frame into its own context.
4. **Videos are never committed.** They live in the session's scratchpad and reach the person through `SendUserFile`.
   Replays made for the person go the same way (they are machine-local, `Docs/REPLAYS.md` §3).
5. **A tool, not a re-discovery.** `Port/tools/amoebius_video.py` (section 3) is the recipe; a session runs it instead
   of working the steps out again (the first run spent most of its tokens finding them).

## 2. What it costs (measured 2026-10-27, cloud session, 4 cores, software rendering)

| Step | Wall time | Session tokens |
|---|---|---|
| Install the .NET 10 SDK into the scratchpad (once per session) | ~1 min | ~1k |
| Build the Amoebius player (`dotnet build Port/src/CosmicShore.Player`) | ~1.3 min | ~1k |
| Boot, first-run prompts, Arcade, launch the mode (Skim Race) | 60 s on a new profile, 35 s on a known one | ~1k |
| Record: one rendered frame every 4 game frames (15 fps of game time) | 0.24 s a frame: **900 frames (60 s of game) in 3.6 min** | ~0 (the driver runs alone; progress lines only) |
| Let the match finish unrendered so the replay carries results | seconds to a few minutes | ~0 |
| Encode with ffmpeg | seconds | ~0.5k |
| The session checking 3-4 frames | - | **~1.5k a 1280x720 frame, ~6k** |
| **Total, once the recipe is known** | **~8-10 min** (the 2026-10-27 clip: 5 min from launch to a sent file) | **~10-15k** |
| First run, discovering the recipe (this file now replaces it) | ~45 min | tens of thousands |

Against that: the person opening Unity, playing to the point and looking costs no tokens and a few minutes of their
time, and shows the real renderer. So:

- **Worth a video:** the session works in the cloud (no Unity), the person is away or on a phone, the question is
  "does it happen, in the right order" (an AI races, a replay plays the whole match, a menu reaches the screen), or
  the same check repeats every iteration of a prototype.
- **Not worth one:** the person is at the editor anyway, the change is one value they will tune by feel, or the
  question is the final look (section 4). A still or one sentence is enough for a text or layout change.
- **Artifacts (labs, studios, web prototypes):** a Playwright screenshot or frame capture of the page is cheaper still
  (Chromium is preinstalled; `/labmaker`'s `verify_lab.cjs` and `/vessel-studio`'s `check_studio.cjs` already write
  screenshots). The page IS the prototype, so the picture is exact.

## 3. How (Amoebius, any session with the repo)

Amoebius (`Port/`, Shombith's engine) compiles the game's own `Assets/_Scripts` and renders it with its own OpenGL
renderer, so it plays the real modes, AIs, HUD and replay system with no Unity. Read `Port/CLAUDE.md` first.

1. **SDK.** `bash dotnet-install.sh --channel 10.0 --install-dir <scratch>/dotnet10`, then
   `export DOTNET_ROOT=<scratch>/dotnet10 PATH=<scratch>/dotnet10:$PATH` (the cloud container has no .NET).
2. **One command:**

   ```bash
   python3 Port/tools/amoebius_video.py --mode SkimRace --intensity 2 --players 4 --seconds 60 --out <scratch>/video
   ```

   It builds the Debug player, plays the match with every seat flown by the AI and writes
   `<out>/<Mode>_I<n>.mp4` (1280x720, H.264, 15 fps of game time, so it plays at game speed) plus the match's replay
   pair (`.cstheater` + `.replay.json`) beside it. `--every N` draws one frame per N game frames (default 4),
   `--keep-frames` keeps the PNGs, `--no-build` skips the build, `--profile` picks the throwaway save slot. Run it in
   the background; it prints a progress line every 5 s of game.
3. **Check 3-4 frames** (`--keep-frames`, or `ffmpeg -ss <t> -i <mp4> -frames:v 1 f.png`) and send the mp4 with
   `SendUserFile`. The person drops the replay pair into `<repo>/Recordings/` and the match is the first row in My
   Replays.

What the tool does, so a session never re-derives it:

- **Windowed under xvfb, scripted, with the control port**: `--frames 2000000 --render-from 2000000000 --size 1280x720
  --control-port N`, with `COSMIC_SHORE_PROFILE=<throwaway> COSMIC_SHORE_NET=off COSMIC_SHORE_AUDIO=off`. `--frames`
  makes every frame a fixed 1/60 s (the game plays at its real clock however slowly it draws); `--render-from` far
  ahead draws ONLY the frames it screenshots, so the rest run at CPU speed.
- **Drives it like `Port/tools/amoebius_markers_run.py`** (whose `Player` class it reuses): the first-run prompts for
  the throwaway profile (adult birth year, consent DECLINED, a fixed name), `do arcade <Mode>`, `do arcade intensity
  N`, `do arcade players N` (AI fill the rest), `do arcade start`, the mode scene, then the `ReadyButton` (the
  connecting screen comes first), `do key F7` (the development build's diagnostics overlay off), `do arcade ready`,
  and **`do arcade autopilot`**: the engine verb that hands every local human seat to the AI through the game's own
  `PilotControl.TrySetAIFlies` (the Third Eye's You / AI toggle; `Port/src/CosmicShore.Player/Inspector.cs`).
  `PilotControl` compiles into development builds only, so the verb works in the Debug player and says so in Release.
  The handed-over seat flies, but whether it races like an AI seat is `PilotControl`'s question, not the video's (the
  2026-10-27 run's handed-over Squirrel took 0 crystals while the three AI seats took 13, 9 and 8).
- **Trims at the results screen.** A match can end inside the clip (the 2026-10-27 Skim Race, I2, four Squirrels,
  ended at 28 s), and everything after is a still. The race is dark and the results bright (mean luma 31 -> 235
  there), so the cut is the first frame whose brightness jumps (`--jump`, default 60 over the race's median), plus
  `--tail` seconds (default 3). No frame is read into the session to find it.
- **The replay** lands in `~/.local/share/CosmicShore-<profile>/Recordings/` (the same format the editor reads from
  `<repo>/Recordings/`). It is written the moment the end screen shows, with results (winner, pilots, the race rings);
  quitting before that saves it as "app closed", without results. So a match that outlasts `--seconds` is left to
  finish undrawn (up to `--finish-timeout`) before the player quits. Measured: 43 s of Skim Race with four pilots and
  the world recorded was 0.8 MB.

## 4. What an Amoebius video does not show

- **The final look.** Amoebius's renderer is its own: shaders, post-processing and some VFX differ from Unity's
  (`Port/docs/PARITY.md`). Colour, glow, bloom and particle polish are judged in the Unity editor.
- **Feel.** Camera feel, input response and audio (runs are silent by default).
- **Multiplayer between machines.** Online play signs in to live UGS and needs the owner's go-ahead
  (`Port/CLAUDE.md`).
- Software rendering under xvfb: the clip plays at true game speed (one frame per 4 game frames), but it is not a
  frame-rate measurement.
