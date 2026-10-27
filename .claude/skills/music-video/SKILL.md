---
name: music-video
description: Make a Cosmic Shore gameplay music video from ANY song, rendered in Amoebius (the engine under Port/) - measure the song (beats, sections, loudness, brightness, onsets), compose a shot plan from the music -> gameplay mapping, shoot each beat in the real game with the engine-side director (the `mv` verb), lay the frames on the song, score the cut, and improve the mapping from feedback. Use on "/music-video", "make a music video", "gameplay footage for this song", "cut this track to gameplay", "trailer to music", "the drop needs a fight not a race" (feedback on a cut), or anything under Tools/Build/music_video/, Docs/MusicVideo/ or Port/src/CosmicShore.Player/MusicVideoDirector.cs.
---

# /music-video

The system: `Docs/MusicVideo/README.md` (read it once). The memory: `Docs/MusicVideo/LEARNINGS.md`
(read it EVERY time; add to it every time). The learned thing: `Tools/Build/music_video/mapping.json`.

The goal is a cut a viewer cannot tell from in-game footage, whose every moment fits the music, and that
spans the game's landscape. The person watches and gives feedback in a viewer's words; this skill turns
feedback into a mapping change, never into a one-off edit of a plan.

## 0. Setup (once per container)

```bash
export PATH=/opt/dotnet:$PATH DOTNET_ROOT=/opt/dotnet DOTNET_CLI_TELEMETRY_OPTOUT=1
ls /opt/dotnet/dotnet || (curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir /opt/dotnet)
python3 -c "import librosa, scipy, PIL" 2>/dev/null || pip install librosa scipy soundfile
dotnet build Port/src/CosmicShore.Player            # ~2 min cold; 0 errors or stop and fix
which xvfb-run ffmpeg                               # both needed
```

## 1. Measure the song

```bash
python3 Tools/Build/music_video/analyze_song.py "Assets/_Audio/Music/<song>" --brief
```

Read the sections and the candidate windows it prints. For a milestone segment pick a window with a
section boundary inside it (a build into a drop is the best 15 s a song has). For a whole song the
segment is 0 to the end and `--beats` is about one per section (see §6).

## 2. Compose

```bash
python3 Tools/Build/music_video/compose.py "Tools/Build/music_video/out/<song>.analysis.json" --start S --end E --beats N
```

Read what it prints: each beat's features, the archetype it chose and WHY. If a choice reads wrong before
shooting (the mapping, not the song, is wrong), fix `mapping.json` and re-compose; pinning a beat with
`--force i=archetype` is for a one-off experiment only and goes in LEARNINGS when it taught something.

## 3. Shoot

```bash
python3 Tools/Build/music_video/shoot.py Tools/Build/music_video/out/<plan dir>/plan.json --jobs 2
```

Two players at once on 4 cores. Each shot's `shoot.log` is the record: the engine answers every command
(`[mv] took Camera.main (3 rig behaviours stood down)`, `[mv] recording ... frames A-B every 2`). A shot
with 0 frames failed; read its log before re-running (`--only NAME`). Never run `pkill -f` with the
player's path in the pattern (it kills your shell): stop a stray player with its control port's `quit`
or by PID from `ps -eo pid,args | awk '/net10.0\/CosmicShore.dll/'`.

## 4. Assemble and look

```bash
python3 Tools/Build/music_video/assemble.py <plan.json> --captions   # review cut: shot names burnt in
python3 Tools/Build/music_video/assemble.py <plan.json>              # clean cut
```

Look at `video_sheet.png` first (first / middle / last frame of every shot): a shot whose frames are a
wall of prisms, an empty sky or a hull filling the frame is wrong before anyone hears a note. Then the
MP4. Hand the person the clean MP4 and the sheet (SendUserFile); never ask them to run anything.

## 5. Score, then record

```bash
python3 Tools/Build/music_video/score.py <plan.json>
```

The scorecard names what is below its floor. Copy `plan.json`, `scorecard.json` and the sheet into
`Docs/MusicVideo/milestones/<name>/` for a milestone cut. Add a row to LEARNINGS.md: what was tried, what
the score said, what the person said, what changed in the mapping.

## 6. From feedback to the mapping (the loop)

| The person says | Change in `mapping.json` |
|---|---|
| "the drop needs a fight, not a race" | raise the fight archetype's `target.energy` / lower the race's; or add an archetype |
| "it cuts too often / too rarely" | `--beats`, and the composer's cut snapping (section boundary > downbeat > beat) |
| "the camera is inside the wall" | that archetype's camera distance (orbit) or intensity; see LEARNINGS (Joust I3 orbit) |
| "nothing happens on the hits" | the archetype's `events` (boosts, blasts, converges, herds on `bar` / `strong` / `onset`); never a camera accent |
| "it jitters / it is not smooth" | the camera is a `mount` or a slow `orbit` (≤ 8 deg/s); `render.accum` 4 at `step_hz` 240 for motion blur; never pulses or shakes |
| "it looks cheap / like a prototype" | `render`: 1080p+, `msaa` 8, `render_scale` 2, `aniso` 16; compare a still with Unity before touching the engine |
| "too dark / too bright for the music" | `fov` range, or the archetype's `target.brightness` |
| "I want to see X (a kaiju, a crack drift, a ring joust)" | a new archetype: mode, players, intensity, camera, target; shoot a still of it first with the control port (`mv orbit name:Worm ...`) |

Every change gets a LEARNINGS row with the reason. The loss is a Pareto front with leaky-ReLU floors
(score.py's docstring): compare cuts criterion by criterion, and let a person's one named complaint be
the criterion that moves.

## 7. Whole song and new songs (M2, M3)

A whole song is the same pipeline with `--start 0 --end <duration>` and `--beats` ≈ the section count
(the composer cuts on section boundaries first). Shoot in batches of `--jobs 2`; each 5 s shot is 2-4
min of wall time. For a new song that is not in the project, pass its path to analyze_song.py; the
mapping is song-agnostic because every feature is normalised against the song itself.

## 8. The engine verb, when a shot needs a hand

With a player on a control port (`Port/CLAUDE.md` § The loop), `do "mv ..."`: `hud off|on`,
`autopilot on|off`, `camera main|replay`, `mount SUBJECT BACK UP SIDE [LEAD] [SMOOTH]`, `orbit SUBJECT DIST HEIGHT DEGPS`,
`dolly X,Y,Z X,Y,Z FRAMES [SUBJECT]`, `fov F`, `action SUBJECT INPUTEVENT [FRAMES]`, `aim SUBJECT SUBJECT2|X,Y,Z|clear`,
`converge A B`, `blast SUBJECT N [FORCE]`, `herd X,Y,Z DIST [NAME]` / `herd off`, `cue N CMD` (N frames after the
next record starts), `record DIR FRAMES [EVERY] [ACCUM]`, `in N CMD`, `release`, `status`. Subjects: `local`,
`ai0`.., `nearest`, `fastest`, `name:TEXT`. `vessels` prints every vessel's distance and screen position from the
camera; `screenshot` shows the shot. A camera straight behind a hull on its axis showed nothing at 23 u; sit
off-axis (behind, above and beside). Build a new archetype from stills before putting it in the mapping.
