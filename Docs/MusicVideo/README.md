# The music video generator: any song → Cosmic Shore gameplay footage, rendered in Amoebius

Started 2026-10-27 (Garrett's brief, branch `cece/stoic-faraday-dlabpf`). **Primary goal:** the best
visuals for the music, moment by moment. **Secondary goal:** span the game's landscape, now and planned
(a kaiju against squads, a skim race drift into a ring joust), all on the beat. **The repeatable part is
the point:** every step is a tool a cheaper model runs, every decision is a number in a mapping table a
person can change after watching, and every cut is scored so the mapping improves with use.

Milestones: **M1** three ~5 s beats over ~15 s of one song (this document's first cut, waiting on
Garrett's feedback) → **M2** a whole song → **M3** every song in `Assets/_Audio/Music` → **M4** the
skill and tools a cheaper model repeats for a new song. Status and the lessons per milestone:
[`LEARNINGS.md`](LEARNINGS.md).

## The pipeline (four tools, one engine verb)

All under `Tools/Build/music_video/`; every tool prints what it decided and why, and writes JSON the
next one reads. A session (or `/music-video`) runs them in order:

| Step | Tool | Reads | Writes |
|---|---|---|---|
| 1. Measure the song | `analyze_song.py SONG` | the audio (anything ffmpeg decodes) | `out/<song>.analysis.json`: tempo, beats, downbeats, onsets, sections labelled intro / build / drop / breakdown / verse / outro, per-frame curves (43 Hz: loudness, spectral centroid = brightness, flatness, bass share, highs share, onset strength), and the best 15 s windows ranked by how much changes inside them |
| 2. Compose | `compose.py ANALYSIS --start S --end E [--beats N]` | the analysis + [`mapping.json`](../../Tools/Build/music_video/mapping.json) | `out/<song>_S-E/plan.json`: the cut points (snapped to a section boundary, else a downbeat, else a beat), one **shot** per beat with its mode, player count, intensity, camera program, field of view and the accents (FOV pulses on downbeats, shakes on hard onsets) at exact song times, plus the beat's features and the reason for every choice |
| 3. Shoot | `shoot.py PLAN [--jobs 2]` | the plan, the built player | `frames/<shot>/f*.png` + `frames.csv` (game time per frame) + `shoot.log` per shot |
| 4. Assemble | `assemble.py PLAN [--captions]` | frames + the song | `video.mp4` at true game speed with the song segment, and `video_sheet.png` (first / middle / last frame of every shot) |
| 5. Score | `score.py PLAN` | frames + analysis | `scorecard.json`: the criteria, the leaky-ReLU penalties, the loss, what is below its floor |

The engine side is one verb, **`mv`**, on the Amoebius player (`Port/src/CosmicShore.Player/MusicVideoDirector.cs`,
reachable from `--do` and the control port): it hides the HUD, puts the local pilot on autopilot, takes the
gameplay camera (its rig stood down, so the look is the game's own) or the CameraManager's replay camera,
poses it every frame on a **subject** (`local`, `ai0`…, `nearest`, `name:Worm`) as an orbit, chase, dolly or
flyby, pulses the field of view or shakes on a schedule, and starts a `FrameRecorder` range from the next
frame. Because a scripted run steps every clock by exactly 1/60 s, frame 300 is always game second 5, so a
beat computed from the song lands on an exact frame whatever the software renderer's speed.

## Why it is shaped this way

- **The real game, not a mock-up.** Every frame is the shipped game's own mode, arena, vessels, AI pilots
  and effects as Amoebius runs them from `Assets/`. That is what "indistinguishable from in-game footage"
  means here; the director only chooses where to look and when.
- **Measure, map, decide, score: four separate files.** A new mapping never re-measures audio; a new
  song never changes the mapping; a scorecard names which criterion got worse. A cheaper model edits one
  JSON number, not a pipeline.
- **The mapping is the learned thing.** `mapping.json` holds the **archetypes** (a scene, its mode and
  camera program, and the feature target it is best for), the feature weights, the diversity penalties,
  the intensity ladder, the FOV range and the accent rules. Feedback from a cut becomes a change there,
  with the reason in `LEARNINGS.md`.
- **The loss is a Pareto front with leaky-ReLU floors, not one scalar.** Each criterion (motion tracks
  loudness, accents land on onsets, cuts sit on beats, bright bars read bright, no dead frames, no stuck
  frames, modes are diverse) has a floor; below it the penalty is steep, above it a small slope still
  rewards doing better. Two cuts are compared criterion by criterion first; the summed loss breaks ties.
  A person's feedback usually names one thing, and the scorecard must be able to show that one thing.

## What the features mean to the director (the mapping's vocabulary)

Every feature is normalised against the whole song (0..1), so a quiet song's loudest bar still reads as
its drop. **energy** (loudness) picks intensity and camera speed and gates accents; **onset_density**
(hits per second against the song's own rate) asks for a subject that moves and for cuts; **bass**
leans to the dense, heavy arenas (the Joust cavern, the Wildlife cage); **brightness** (spectral
centroid) widens the field of view and leans to the open, airy arenas (the Skim Race rails);
**flatness** (noisy, textural sound) leans to texture shots; **trend** says whether a beat rises or
falls; **section** (build / drop / breakdown…) and **position** in the song are written next to each
shot for the archetype rules and the reader.

## The feedback loop (how it improves with use)

1. Watch `video.mp4` (or read `video_sheet.png` and `scorecard.json` first).
2. Say what is wrong in the viewer's words ("the drop needs a fight, not a race", "the cut is late").
3. Translate to the mapping: an archetype's target, a weight, a floor, a camera number, a new archetype.
4. Record the change and the reason in `LEARNINGS.md` (the table there is the memory of what worked).
5. Re-compose, re-shoot only the changed shots (`shoot.py --only NAME`), re-assemble, re-score.

A session never asks the person to run a command; it runs the pipeline and brings back the MP4 and the
sheet (CLAUDE.md § AI learning is automatic).

## Where things are

- Tools: `Tools/Build/music_video/` (`analyze_song.py`, `compose.py`, `shoot.py`, `assemble.py`,
  `score.py`, `mapping.json`). Outputs under `Tools/Build/music_video/out/` (git-ignored; large).
- Engine: `Port/src/CosmicShore.Player/MusicVideoDirector.cs` (+ the `mv` case in `InputScript.cs`, the
  tick in `PlayerWindow.cs` / `Program.cs`).
- Skill: `.claude/skills/music-video/SKILL.md`.
- Milestone cuts: `Docs/MusicVideo/milestones/` (each cut's plan, scorecard and contact sheet; the MP4 is
  handed to the person, not committed).
- Songs: `Assets/_Audio/Music/` (SKY FIRE, SEVERITY, SPACE GLIDING, DISORIENTED, SECRET HIDE AWAY,
  I've waited so long, the cosmic shore WAVs).

## Running it (a cheaper model's checklist is the skill; this is the shape)

```bash
export PATH=/opt/dotnet:$PATH DOTNET_ROOT=/opt/dotnet            # .NET 10 SDK (Port/CLAUDE.md item 13)
dotnet build Port/src/CosmicShore.Player                          # the engine, with the mv verb
python3 Tools/Build/music_video/analyze_song.py "Assets/_Audio/Music/SKY FIRE.ogg"
python3 Tools/Build/music_video/compose.py "Tools/Build/music_video/out/SKY FIRE.analysis.json" --start 41 --end 56 --beats 3
python3 Tools/Build/music_video/shoot.py Tools/Build/music_video/out/SKY-FIRE_41-56/plan.json --jobs 2
python3 Tools/Build/music_video/assemble.py Tools/Build/music_video/out/SKY-FIRE_41-56/plan.json
python3 Tools/Build/music_video/score.py Tools/Build/music_video/out/SKY-FIRE_41-56/plan.json
```

The shoot needs `xvfb-run` (a server without a display) and a profile that has answered the game's
first-run prompts; `shoot.py` answers them itself on a fresh profile (birth year, data collection
**declined**, pilot name "Director"). Two players at once on a 4-core box; a 5 s shot takes about two to
four minutes (the menu and the settle are simulated without drawing; only recorded frames are rendered).
