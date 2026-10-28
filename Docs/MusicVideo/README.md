# The music video generator: any song → Cosmic Shore gameplay footage, rendered in Amoebius

Started 2026-10-27 (Garrett's brief, branch `cece/stoic-faraday-dlabpf`). **Primary goal:** the best
visuals for the music, moment by moment. **Secondary goal:** span the game's landscape, now and planned
(a kaiju against squads, a skim race drift into a ring joust), all on the beat. **The repeatable part is
the point:** every step is a tool a cheaper model runs, every decision is a number in a mapping table a
person can change after watching, and every cut is scored so the mapping improves with use.

Milestones: **M1** three ~5 s beats over ~15 s of one song (five cuts, each judged by Garrett) → **M2** a minute of
two songs with different vibes (cut 6: SKY FIRE 30-90 s and SPACE GLIDING 20-80 s), then a whole song → **M3** every song in `Assets/_Audio/Music` → **M4** the
skill and tools a cheaper model repeats for a new song. Status and the lessons per milestone:
[`LEARNINGS.md`](LEARNINGS.md).

## The pipeline (four tools, one engine verb)

All under `Tools/Build/music_video/`; every tool prints what it decided and why, and writes JSON the
next one reads. A session (or `/music-video`) runs them in order:

| Step | Tool | Reads | Writes |
|---|---|---|---|
| 1. Measure the song | `analyze_song.py SONG` | the audio (anything ffmpeg decodes) | `out/<song>.analysis.json`: tempo, beats, downbeats, onsets, sections labelled intro / build / drop / breakdown / verse / outro, per-frame curves (43 Hz: loudness, spectral centroid = brightness, flatness, bass share, highs share, onset strength), and the best 15 s windows ranked by how much changes inside them |
| 2. Compose | `compose.py ANALYSIS --start S --end E [--beats auto]` | the analysis + [`mapping.json`](../../Tools/Build/music_video/mapping.json) | `out/<song>_S-E/plan.json`: one **shot per section** of the song (6-16 s: short sections merged, long ones split on downbeats, a build carried two bars into its drop), each shot a **phrase** - its archetype (a cell, a population, a steady camera), its **payoff** time (the section boundary inside it, else the downbeat near 70 % through) and the **orchestration events** at exact song times: the setup over the bars before the payoff (abilities 1, 2, 3; a loop being laid; a fleet converging; a shoal gathering), the payoff (ability 4 with a boost and a burst; the loop closing; every hull boosting in; the shoal scattering), the release after it - plus the shot's features and the reason for every choice; the `render` block (60 fps from 240 Hz sub-frames averaged 2:1, 1080p, 8x MSAA, 2x supersampling, 1.5x for a cut over 20 s) |
| 3. Shoot | `shoot.py PLAN [--jobs 2]` | the plan, the built player | `frames/<shot>/f*.png` + `frames.csv` (game time per frame) + `shoot.log` per shot |
| 4. Assemble | `assemble.py PLAN [--captions]` | frames + the song | `video.mp4` at true game speed with the song segment, and `video_sheet.png` (first / middle / last frame of every shot) |
| 5. Score | `score.py PLAN` | frames + analysis | `scorecard.json`: the criteria, the leaky-ReLU penalties, the loss, what is below its floor |

The engine side is one verb, **`mv`**, on the Amoebius player (`Port/src/CosmicShore.Player/MusicVideoDirector.cs`,
reachable from `--do` and the control port): it hides the HUD, puts the local pilot on autopilot, takes the
gameplay camera (its rig stood down, so the look is the game's own) and poses it AFTER every simulation tick
as a steady **mount** on a subject (`local`, `ai0`…, `fleet`, `flora`, `name:WormHead`) or a slow orbit (≤ 4 deg/s;
nothing faster, no dollies, no pulses: the camera is never what makes a viewer sick); it **stages** the scene in
the menu's lava-lamp cell (`cell` swaps to any of the 61 cell configs, `spawn` releases the roster's 21 hull
classes as AI pilots and every Toy Box species of fauna and flora); it **orchestrates the game** (`action` fires a
hull's ability or boost, `all` at once; `aim` / `converge` / `circle` steer AI pilots; `blast` devastates the prisms
around a subject with the game's own damage; `herd` draws every swarm creature toward a subject or a direction;
`domain` shifts the pilot's colour) at frames the song chose (`cue`); and it starts a `FrameRecorder` range from
the next frame, averaging sub-frames in linear light for motion blur. Because a scripted run steps every clock by
a fixed step (1/240 s for a cut), a beat computed from the song lands on an exact frame whatever the software
renderer's speed. Nothing is rendered in real time: every sub-frame is drawn at full quality (`--step-hz`,
`--msaa`, `--render-scale`, `--aniso`, `--size`).

The `render` block also carries the game's own **graphics prefs** for the shoot's profile (`prefs`: Ultra
preset, adaptive performance off, Lush ecosystem, 8x MSAA, full VFX). Presets below High replace the
procedural HyperSea skybox with a baked panorama and cut cytoplasm, the membrane and HDR; adaptive
performance lowers the preset at the software renderer's frame rate. The runner writes them into the
profile's `prefs.json` before every player starts, so a cut is always shot at the game's best.

## Why it is shaped this way

- **The real game, not a mock-up.** Every frame is the shipped game's own mode, arena, vessels, AI pilots
  and effects as Amoebius runs them from `Assets/`. That is what "indistinguishable from in-game footage"
  means here; the director only chooses where to look and when.
- **Measure, map, decide, score: four separate files.** A new mapping never re-measures audio; a new
  song never changes the mapping; a scorecard names which criterion got worse. A cheaper model edits one
  JSON number, not a pipeline.
- **The mapping is the learned thing.** `mapping.json` holds the **archetypes** (a scene, its mode, its
  camera mount, its orchestration event templates and the feature target it is best for), the feature
  weights, the diversity penalties, the intensity ladder, the FOV range and the render block. Feedback
  from a cut becomes a change there, with the reason in `LEARNINGS.md`.
- **The music is answered by the game, never by the camera** (Garrett, 2026-10-27): the camera is a steady
  mount; the beat lands as a boost, a burst of prisms, a joust pass, a swarm turning.
- **Every shot is a phrase with a setup and a payoff** (Garrett, 2026-10-28: "longer shots where you could build
  up a narrative and some payoff... a set up and payoff from a combo or loop completion within a kit"): shots
  follow the song's sections, and each archetype says what accumulates over its bars and what spends it on the
  payoff - the kit combo (abilities 1, 2, 3, then 4 with a boost), the loop (`circle` closes exactly on the
  payoff), the siege (the fleet converges, then boosts in at once), the bloom (a flora per bar, then a shoal
  released), the joust (head-on, then the meeting).
- **Populated and diverse** (Garrett, 2026-10-28: "5 times as diverse and 10 times as populated"): twelve
  archetypes in twelve cells, each with 60-100 fauna, 6-12 flora and 6-21 hulls; a cut never reuses a cell.
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
python3 Tools/Build/music_video/compose.py "Tools/Build/music_video/out/SKY FIRE.analysis.json" --start 30 --end 90
python3 Tools/Build/music_video/shoot.py Tools/Build/music_video/out/SKY-FIRE_30-90/plan.json --jobs 2
python3 Tools/Build/music_video/assemble.py Tools/Build/music_video/out/SKY-FIRE_30-90/plan.json
python3 Tools/Build/music_video/score.py Tools/Build/music_video/out/SKY-FIRE_30-90/plan.json
```

The shoot needs `xvfb-run` (a server without a display) and a profile that has answered the game's
first-run prompts; `shoot.py` answers them itself on a fresh profile (birth year, data collection
**declined**, pilot name "Director"). Two players at once on a 4-core box; a populated 12 s shot at 1080p takes about an hour (the menu and the
settle are simulated without drawing; only recorded sub-frames are rendered), so a minute of a song is two to
three hours and two songs shoot side by side.
