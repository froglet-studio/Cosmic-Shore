# Music video generator: learnings (the memory the mapping improves from)

Read before every cut; add a row after every cut. A row says what was tried, what the scorecard and the
person said, and what changed in `Tools/Build/music_video/mapping.json` (or the tools) because of it.
`README.md` is the system; this file is why its numbers are what they are.

## Status

| Milestone | State | Where |
|---|---|---|
| M1: three ~5 s beats over ~15 s of one song | **two cuts made 2026-10-27, waiting on Garrett's feedback** (SKY FIRE 41.0-56.0 s: build → the drop at 46.37 → drop). Cut 1: Skim Race flyby → Joust chase → Wildlife chase. Cut 2 (closer cameras, bright bars in brighter arenas, the alive-subject rule): Wildlife chase → Joust chase → Skim Race chase. The scorecard prefers cut 2 on five of seven criteria (no dead frames, luminance now follows brightness, less stuck time); motion-vs-energy and motion-vs-onsets are still under their floors in both. | `milestones/M1-sky-fire-41-56/cut1`, `cut2` |
| M2: a whole song | not started (same pipeline, `--start 0 --end <duration> --beats <sections>`) | |
| M3: every song in `Assets/_Audio/Music` | not started | |
| M4: the skill and tools a cheaper model repeats for a new song | the skill exists (`/music-video`) and ran this cut end to end; it is proven when a session that is not this one makes M2 from it | |

## What the first cut taught (2026-10-27, M1)

### The engine as a camera department

- **Every frame is the real game.** Amoebius boots the shipped Menu_Main, opens the arcade card, seats the
  AI pilots and starts the match exactly as a player's hands would (`arcade MODE` / `players N` /
  `intensity I` / `start` / `ready` on the player's `--do`/control-port verbs). The look is the game's own:
  the Skim Race rails, the cell membrane, the Joust crystal cavern, the Wildlife cage.
- **Pose Camera.main with its rig stood down, not the replay camera, for the game's look.** The
  CameraManager's replay camera (`BeginManualReplayCamera`) renders a flat light-blue background (its
  own clear / post stack); the gameplay camera with its three rig behaviours disabled keeps the dark sea,
  the fog and the post. `mv camera main` is the default; `replay` stays for when the gameplay rig's
  corridor or speed-tunnel holds get in the way.
- **A scripted run is frame-exact.** `--frames N` steps every clock by 1/60 s, so a 5.37 s beat is 322
  frames whatever the renderer's wall speed, and accents scheduled with `mv cue N` land on their frame.
  `--render-from 999999999` skips drawing everything but recorded frames and screenshots, so the menu,
  the card and the settle cost simulation only (the Joust settle of 420 frames with 7 vessels still takes
  about two minutes of wall time; Wildlife Liberation's 600-frame settle about the same).
- **The local pilot must be on autopilot** (`mv autopilot on` = `VesselController.ToggleAIPilot(true)` +
  input paused), or the scripted "hold W" flies it off the course into the dark while the AI pilots race.
  With it on, `local` is a usable subject like any AI.
- **Subjects are the AI pilots.** `ai0`… are the non-local vessels in spawn order; `nearest` is the one
  closest to the local pilot. In Skim Race at I2 all four are Squirrels with cyan wakes; in Joust the
  hulls carry magenta and cyan domain wakes.
- **The HUD is nine canvases in a race, seventeen in Joust**; `mv hud off` disables every Canvas and
  remembers them. The game's own diagnostics panel (F7) starts visible in the engine: `key F7` once after
  the menu builds.
- **`arcade players 12` seats six AI.** "11 placed AI exceed the 6 replicated slots - truncating": seven
  vessels is the Joust ceiling in the engine today. Still the busiest arena; the "multiple squads" shot
  of the brief needs either the replicated-slot cap lifted (Shombith's area: UGS / netcode) or ghosts.
- **The Wildlife Liberation kaiju did not show.** `find Worm` after a 600-frame settle at I3 found only
  the WormholeView; the Worm Colony (`Assets/_Prefabs/FloraAndFauna/WormColony.prefab`, the kaiju of
  `WILDLIFE_LIBERATION.md`) is on the roster but had not spawned by then (or spawns under another name).
  The cage itself (orange / blue / purple lattice with brittlestar swarms) is a strong location on its
  own; the kaiju fight is M2's first new archetype: find when it spawns (`find` on `Head`, a longer
  settle, I4), frame it with `mv orbit name:<its object> …`, keep a still in `milestones/`.

### Cameras (numbers that were wrong the first time)

- **A Squirrel is about 6 u long; cameras 40-90 u away make it a dot.** Chase at back 38 / up 10 reads
  as "a glow with a wake"; the flyby at ahead 160 opens on an empty rail for two seconds before the pass.
  Second pass: chase back 22 / up 6, orbit dist 35 / height 10, flyby ahead 80 / side 14. The pass itself
  (frame ~60 of the flyby: rail, rings, wakes crossing) is the best picture of the cut.
- **Chase smoothing must not lag the position.** Low-passing the camera's world position at 6 e-folds/s
  left it 20 u behind a vessel doing 120 u/s, with the subject in a top corner. The director now rides
  the subject's position exactly and low-passes only its heading.
- **Joust at I3+: an orbit at 70 u sits inside the prism mass.** The cavern is solid at that distance;
  the drop rides a hull (chase) or orbits at ≤ 36 u where the air around the jouster is open.
- **A world-up orbit on a Skim Race vessel is fine; the first try missed because the subject had just
  respawned** (ai0 at 651 u). Prefer `nearest`, which is always near the local pilot, when a subject can
  respawn.

### The pipeline

- **Spaces in the song name broke the `mv record` path** ("SKY FIRE_41-56"). The verb now takes the
  trailing numeric words as FRAMES [EVERY] and the rest as the path, and the composer names plan
  directories with `-` instead of spaces.
- **Accents must be relative to the recording's first frame** (`mv cue`), not to "now" (`mv in`): the
  control port's commands land a few frames apart, which is 50-100 ms of drift on a beat.
- **Analysis is cheap and reusable:** 20-30 s per song with librosa; the JSON is 1-2 MB with the 43 Hz
  curves and is kept under `out/` (regenerated, git-ignored).
- **Section labels from energy percentiles are usable but coarse:** SKY FIRE's 44.47-46.37 "breakdown"
  is the two-bar pre-drop pause, which is exactly the cut the composer wanted; SPACE GLIDING's opening
  splits into four "breakdown" slivers. A beat shorter than a bar should merge into its neighbour
  (compose.py `min_len` = 3 s does this for cuts; the analyzer's section list keeps them for the reader).

### The mapping (why the first numbers are what they are)

- Feature weights: energy 1.0 > onset density 0.8 > bass 0.6 > brightness 0.5 > flatness 0.3. Loudness
  is what a viewer feels first; hits second; the arena's density (bass) third.
- `same_mode_penalty` 0.9 > `diversity_penalty` 0.6: the brief's secondary goal is to SPAN the landscape,
  so a repeated mode costs more than a repeated camera.
- Intensity from energy at 0.3 / 0.55 / 0.8: I1 for quiet bars, I4 only for the loudest fifth of a song.
  Archetypes may pin an intensity where the arena only reads at one (Joust I3, Wildlife I3).
- FOV 52-74 by brightness: a bright bar opens the frame.
- Downbeat pulses from energy 0.35 (5° per unit beat strength over 8 frames); every-beat pulses only
  above 0.6; shakes only above 0.7 and on the top-quartile onsets. Untested on a viewer; the first cut's
  drop beats carry them.

### The loss (score.py)

Pareto front over seven criteria with leaky-ReLU floors (steep below the floor, a small reward above).
Floors on the first cut: motion-vs-energy correlation 0.3, motion-vs-onsets 0.15, cut alignment 0.15
beats, dead frames 2 %, stuck frames 10 %, diversity 0.9. **These floors are guesses until a cut a
person liked has been scored**; the first scored cut is in `milestones/M1-sky-fire-41-56/scorecard.json`,
and the floors should move to sit just under a liked cut's numbers.

### What cut 2 taught (same day)

- **The Pareto read worked as intended.** Cut 2 beat cut 1 on five criteria and tied or barely lost on
  two (motion-vs-onsets 0.031 → 0.029, cut alignment equal), so it is the better cut without a scalar
  argument; the scalar (3.44 → 0.70) agrees. The two criteria still below floor are both about MOTION
  tracking the music, which no camera number fixes: that needs beat-driven camera moves (a dolly kick, a
  cut, a boost) on the beats, not just FOV pulses. First thing to try for cut 3: `mv cue` a `shake` or a
  short `dolly` on every downbeat, and compare the motion-vs-onsets number.
- **A chase in a dense arena clips through prisms.** The Wildlife chase at 24 u behind ends with a prism
  across half the frame; the Joust chase opens inside the prism mass. `mean_detail` does not catch a
  wall (a wall has texture); a "camera inside mass" detector needs depth, or the director should test the
  line from camera to subject against prism colliders and pull in. Noted as the next director feature.
- **`nearest` and `fastest` can switch subjects mid-shot.** The Skim Race chase ended on a different
  Squirrel than it started on (the nearest to the local pilot changed). For a chase the subject should
  be held for the whole shot unless it dies; the alive rule already does the dying half. Make `nearest`
  and `fastest` resolve once at the first frame of a shot.
- **Measured luminance per arena is now in the mapping** (`luminance` on each archetype) and the
  brightness targets follow it; luminance-vs-brightness went from -1.0 to +0.5.

## Log

| Date | Cut | Score said | Person said | Changed |
|---|---|---|---|---|
| 2026-10-27 | M1 cut 1: SKY FIRE 41.0-56.0, flyby (Skim Race I2) → chase (Joust I3) → chase (Wildlife I3) | loss 3.44; dead frames 12 % (the flyby's empty opening), stuck 17 % (the Joust subject died), luminance-vs-brightness -1.0 | waiting | cameras moved closer; brightness targets follow measured luminance; `fastest` subject + the alive rule; `cue` scheduling; `camera main` default |
| 2026-10-27 | M1 cut 2: chase (Wildlife I3) → chase (Joust I3, `fastest`) → chase (Skim Race I3, `nearest`) | loss 0.70; dead 0 %, stuck 10 %, luminance-vs-brightness +0.5; motion-vs-energy 0.19 and motion-vs-onsets 0.03 still under floor | waiting | nothing yet: next are beat-driven camera moves and a camera-in-mass guard (above) |
