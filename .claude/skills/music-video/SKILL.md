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
python3 Tools/Build/music_video/compose.py "Tools/Build/music_video/out/<song>.analysis.json" --start S --end E
```

`--beats auto` (the default) makes one shot per SECTION of the song, 6-16 s each (`mapping.json`
`shots`): a short section merges into its neighbour, a long one splits on downbeats, and a shot that
ends where a drop begins runs two bars into the drop so its payoff lands on it. Every shot is a
**phrase**: a setup over its bars and a payoff on the section boundary inside it (else the downbeat
near 70 % through). Read what it prints: each shot's length and section, the archetype and its
setup/payoff line, the payoff time and WHY. If a choice reads wrong before shooting (the mapping, not
the song, is wrong), fix `mapping.json` and re-compose; `--force i=archetype` pins a shot for a one-off
experiment and goes in LEARNINGS when it taught something. `--beats N` is the old equal split for a
15 s milestone.

## 3. Shoot

```bash
python3 Tools/Build/music_video/shoot.py Tools/Build/music_video/out/<plan dir>/plan.json --jobs 2
```

Two players at once on 4 cores. Each shot's `shoot.log` is the record: the engine answers every command
(`[mv] took Camera.main (3 rig behaviours stood down)`, `[mv] planted 3/3 Borromean flora`, `[mv] released
Manta (Jade)`, `[mv] recording ... frames A-B every 4`). Each shot lands as `shot.mp4` (encoded as it records; the PNGs are deleted, a minute of them is 20 GB) with
`frames.csv`. A shot with 0 frames failed; a `failed:` or
`unknown command` answer in the log means a stage command or a cue did nothing (read it before
re-running with `--only NAME`). A populated 12 s shot at 1080p / 1.5x is about 40-60 min of wall time;
budget a 60 s cut at two to three hours. Never run `pkill -f` with the player's path in the pattern (it
kills your shell): stop a stray player with its control port's `quit` or by PID from
`ps -eo pid,args | awk '/net10.0\/CosmicShore.dll/'`.

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
| "the drop needs a fight, not a race" | raise the fight archetype's `target.energy` / lower the race's, or add `drop` to its `sections`; or add an archetype |
| "it cuts too often / too rarely", "longer shots" | `shots.min_seconds` / `max_seconds` (section-aligned since v3), `payoff_tail_bars` |
| "there is no payoff / nothing builds" | the archetype's `events`: `setup_bar` templates that accumulate (abilities 1, 2, 3; a `circle` loop; `aim all local`), a `payoff` that spends them (ability 4 + boost + `blast`; `action all`; a herd scatter) |
| "the camera is inside the wall" | that archetype's camera distance (mount back/up/side, orbit distance) |
| "nothing happens on the hits" | the archetype's `events` (boosts, blasts, converges, herds on `bar` / `strong` / `onset`); never a camera accent |
| "erratic camera / it makes people sick" | `camera`: every camera is a mount with smooth ≥ 1.6 or an orbit ≤ 4 deg/s; no dolly; the horizon leans a quarter; FOV 56-66 |
| "it jitters / it is not smooth" | `render.accum` 2-4 at `step_hz` 240 for motion blur; never pulses or shakes |
| "there is no skybox / it is too austere" | the game's quality preset: `render.prefs` keeps `gfx.Preset` 5 and `gfx.Adaptive` 0 |
| "the small FX smear into streaks" | `render.accum` 2 (a 90-degree shutter) instead of 4 |
| "it looks cheap / like a prototype" | `render`: 1080p+, `msaa` 8, `render_scale` 2 (1.5 for a cut over `long_seconds`), `aniso` 16 |
| "too dark / too bright for the music" | `fov` range, or the archetype's `target.brightness` |
| "too sparse / populate it" | the archetype's `stage_commands`: 60-100 fauna in shoals, 6-12 flora of 2-4 species, 6-21 hulls (`spawn vessel all 21`); `mv lifeforms` lists every species |
| "the scenes all look the same" | a new archetype in a cell not yet used (`mv cells` lists 61 configs); `same_cell_penalty` keeps a cut from reusing one |
| "I want to see X (a kaiju, a loop, a joust)" | a new archetype: cell, population, camera, phrase, target, sections; shoot a still of it first with the control port |

Every change gets a LEARNINGS row with the reason. The loss is a Pareto front with leaky-ReLU floors
(score.py's docstring): compare cuts criterion by criterion, and let a person's one named complaint be
the criterion that moves.

## 7. A minute, a whole song, new songs (M2, M3)

A minute or a whole song is the same pipeline with `--start S --end E` (the composer cuts on sections).
Pick the window from the sections `analyze_song.py --brief` prints: a build into a drop and the drop's
first bars is the best minute a song has. Shoot in `--jobs 2`; a 60 s cut is 2-3 hours, so shoot two
songs side by side (two `shoot.py` runs, different `--port` and `--profile`). For a new song that is not
in the project, pass its path to analyze_song.py; the mapping is song-agnostic because every feature is
normalised against the song itself. A different vibe picks itself: a quiet song's shots fall to the
shoal, loop and drift archetypes; a loud one's to the rallies, sieges and jousts.

## 8. The engine verb, when a shot needs a hand

With a player on a control port (`Port/CLAUDE.md` § The loop), `do "mv ..."`: `hud off|on`,
`autopilot on|off`, `camera main|replay`, `mount SUBJECT BACK UP SIDE [LEAD] [SMOOTH]`, `orbit SUBJECT DIST HEIGHT DEGPS`,
`dolly X,Y,Z X,Y,Z FRAMES [SUBJECT]`, `fov F`, `action SUBJECT|all INPUTEVENT [FRAMES]` (Button1Action..Button4Action
are the hull's four abilities, FullSpeedStraightAction the boost), `aim SUBJECT|all SUBJECT2|X,Y,Z|clear`,
`circle SUBJECT RADIUS SECONDS [CENTRE]` (the pilot flies one loop that closes after SECONDS), `converge A B`,
`blast SUBJECT N [FORCE]`, `herd X,Y,Z DIST [NAME]` / `herd follow SUBJECT DIST` / `herd off`, `domain Ruby|Jade|Blue|Gold`,
`cell NAME` (swap the scene's cell to any cell config; `cells` lists them), `spawn vessel CLASS|all N [DOMAIN|cycle] [R]`
(AI pilots of the roster's 21 classes, Menu_Main only), `spawn fauna|flora NAME N [ELEMENT]` (multi-word names work;
`lifeforms` lists the Toy Box catalogue), `cue N CMD` (N frames after the next record starts), `record DIR FRAMES
[EVERY] [ACCUM]`, `in N CMD`, `release`, `status`. Subjects: `local`, `ai0`.., `all` (actions and aims), `nearest`,
`fastest`, `fleet` (the AI fleet's centre), `flora` / `fauna` (the nearest life), `name:TEXT`. `vessels` prints every
vessel's distance and screen position from the camera; `screenshot` shows the shot. A camera straight behind a hull
on its axis showed nothing at 23 u; sit off-axis (behind, above and beside). Build a new archetype from stills
before putting it in the mapping.
