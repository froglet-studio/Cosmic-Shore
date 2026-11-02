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
   verify rule (`CLAUDE.md` § "Unity CLI verification") or the hand test in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
3. **The session looks at frames, not at the whole video.** It reads at most 3-4 frames (start, middle, end, plus one
   where the change should show) to check the clip shows what it claims, then sends the file with `SendUserFile`. It
   never streams every frame into its own context.
4. **Videos are never committed.** They live in the session's scratchpad and reach the person through `SendUserFile`.
   Replays made for the person go the same way (they are machine-local, `Docs/REPLAYS.md` §3).
5. **A tool, not a re-discovery.** The steps below are the recipe; a session follows them instead of working them out
   again (the first run spent most of its tokens finding them).

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

1. **SDK and build.** `bash dotnet-install.sh --channel 10.0 --install-dir <scratch>/dotnet10`, then
   `DOTNET_ROOT=<scratch>/dotnet10 PATH=<scratch>/dotnet10:$PATH dotnet build Port/src/CosmicShore.Player`.
2. **Run windowed under xvfb, scripted, with the control port**:
   `xvfb-run -a -s "-screen 0 1280x720x24" .../bin/Debug/net10.0/CosmicShore --frames 2000000 --render-from 2000000000
   --size 1280x720 --control-port 47820` with `COSMIC_SHORE_PROFILE=<throwaway> COSMIC_SHORE_NET=off
   COSMIC_SHORE_AUDIO=off`. `--frames` makes every frame a fixed 1/60 s (the game plays at its real clock however
   slowly it draws); `--render-from` far ahead draws ONLY the frames you screenshot, so the rest run at CPU speed.
3. **Drive it like `Port/tools/amoebius_markers_run.py`** (its `Player` class): the first-run prompts for the throwaway
   profile (adult birth year, consent DECLINED, a fixed name), `do arcade <Mode>`, `do arcade intensity N`,
   `do arcade players N` (AI fill the rest), `do arcade start`, wait for the mode scene, wait for `ReadyButton` (the
   connecting screen comes first), `do key F7` (the development build's diagnostics overlay off), `do arcade ready`.
4. **Record**: repeat `wait 4` + `screenshot <dir>/fNNNNN.png`. Then
   `ffmpeg -framerate 15 -i f%05d.png -c:v libx264 -crf 28 -pix_fmt yuv420p out.mp4`.
   **Time the clip from the game, not from the frame count.** A screenshot costs the loop extra frames, so
   `wait 4` + `screenshot` measured about 6.9 game frames per picture on 2026-10-27, and a clip encoded at 15 fps
   played 1.7x fast. Encode at `pictures / game seconds` fps (`-framerate 8.7 ... -r 15`) and cut with `-t`, never
   `-frames:v` (it counts OUTPUT frames, and cut that clip at 26 of its 44 s).
   **Trim at the results screen.** A match can end inside the clip (the 2026-10-27 Skim Race, I2, four Squirrels,
   ended at 28 s), and everything after is a still. The results screen is bright and the race is dark, so the cut is
   the first frame whose mean brightness jumps (`ffmpeg ... -vf signalstats` `YAVG`, 31 -> 235 there); keep about 3 s
   after it. Finding it this way costs no frames read.
5. **The replay** the match records lands in `~/.local/share/CosmicShore-<profile>/Recordings/` (`.cstheater` +
   `.replay.json`, the same format the editor reads from `<repo>/Recordings/`). It is written the moment the end
   screen shows, with results (winner, pilots, the race rings); quitting before that saves it as "app closed",
   without results. The person drops both files into `<repo>/Recordings/` and the match is the first row in My
   Replays. Measured: 43 s of Skim Race with four pilots and the world recorded was 0.8 MB.

**All-AI matches need one engine verb that is not in the engine yet**: `do arcade autopilot`, which hands the local
seat to the AI through the game's own `PilotControl.TrySetAIFlies` (the Third Eye's You / AI toggle). The 2026-10-27
run used it as an uncommitted local patch to `Port/src/CosmicShore.Player/Inspector.cs`; adding it (and a committed
`Port/tools/amoebius_video.py` for steps 2-5) is engine tooling and goes through Shombith. Without it the local seat
idles and the camera watches an idle hull. With it, the 2026-10-27 run's handed-over seat finished with 0 crystals
while the three AI seats took 13, 9 and 8: the handed-over Squirrel flies, but whether it races like an AI seat is
`PilotControl`'s question (the Third Eye's toggle), not the video's.

## 4. What an Amoebius video does not show

- **The final look.** Amoebius's renderer is its own: shaders, post-processing and some VFX differ from Unity's
  (`Port/docs/PARITY.md`). Colour, glow, bloom and particle polish are judged in the Unity editor.
- **Feel.** Camera feel, input response and audio (runs are silent by default).
- **Multiplayer between machines.** Online play signs in to live UGS and needs the owner's go-ahead
  (`Port/CLAUDE.md`).
- Software rendering under xvfb: the clip plays at true game speed (one frame per 4 game frames), but it is not a
  frame-rate measurement.
