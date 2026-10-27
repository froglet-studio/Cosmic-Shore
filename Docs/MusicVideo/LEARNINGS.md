# Music video generator: learnings (the memory the mapping improves from)

Read before every cut; add a row after every cut. A row says what was tried, what the scorecard and the
person said, and what changed in `Tools/Build/music_video/mapping.json` (or the tools) because of it.
`README.md` is the system; this file is why its numbers are what they are.

## Status

| Milestone | State | Where |
|---|---|---|
| M1: three ~5 s beats over ~15 s of one song | **cuts 1-2 judged by Garrett (prototype look, jitter, camera dances - see below); cut 3 judged by Garrett (sky missing, too austere, the Sparrow rounds smeared); cut 4 on the Ultra preset with the procedural sky, Lush ecosystem and a 90-degree shutter is the current cut.** Cuts 1-2 (2026-10-27) (SKY FIRE 41.0-56.0 s: build → the drop at 46.37 → drop). Cut 1: Skim Race flyby → Joust chase → Wildlife chase. Cut 2 (closer cameras, bright bars in brighter arenas, the alive-subject rule): Wildlife chase → Joust chase → Skim Race chase. The scorecard prefers cut 2 on five of seven criteria (no dead frames, luminance now follows brightness, less stuck time); motion-vs-energy and motion-vs-onsets are still under their floors in both. | `milestones/M1-sky-fire-41-56/cut1`, `cut2` |
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

### Garrett's feedback on cuts 1 and 2 (2026-10-27) and what changed for cut 3

Garrett: *"they both have the same fundamental issues. this looks like a prototype rendering. it is not
smooth. it is the opposite at times with an eye-straining jitter. we are not rendering in real time. we
don't have frame rate constraints. we should be cheating and calculating one beautiful screenshot-worthy
frame at a time. this didn't look like cosmic shore. it looked like a cheap imitation. it should be the
cinematic over-the-top version if it makes a mistake. ideally people watching still believe it is real
unity gameplay but it should be better or equal in every way. never less than."* And: *"keep it to
orchestrated gameplay not camera dances. keep things as smooth and inviting as you can, and orchestrate
the fights, the timing of explosions, the directions that swarms move etc."*

What was wrong, measured:
- **The jitter was the camera, not the frame pacing.** frames.csv showed every output frame exactly
  1/30 s apart. The strain came from (1) a FOV pulse on EVERY beat above energy 0.6 (one every 0.49 s at
  123 bpm) plus shakes on hard onsets, (2) the camera posed BEFORE the simulation tick, so it was always
  one frame behind the hull it followed, (3) a chase that re-resolved `nearest`/`fastest` every frame,
  and (4) 30 fps with no motion blur at racing speed.
- **The look was the renderer at 1600x900, 4x MSAA, scale 1.** The engine's post stack is exactly what
  the game's `GamePlay PostProcessing Profile` turns on (Bloom + Panini; Vignette, White Balance and DoF
  are authored but inactive), so the gap is resolution, anti-aliasing and motion, not a missing effect.
- **A subject 23 u from the camera on its forward axis rendered nothing** while a 3/4 view from 30 u showed
  the full Squirrel and wake: cameras sit off-axis (behind, above AND beside).

What cut 3 does instead (all in the mapping's `render` block and the archetypes' `events`):
- **One beautiful frame at a time.** 1920x1080, 8x MSAA, 2x supersampling, 16x anisotropy; the run
  steps at 240 Hz and every 60 fps output frame is the linear-light average of 4 sub-frames (a 180-degree
  shutter: real motion blur). About 0.5 s per sub-frame in the software renderer, so a 5 s shot is
  about 10 minutes of wall time; two shots at once on 4 cores.
- **The camera is a mount, posed after the tick.** `mv mount SUBJECT back up side lead smooth`: it rides the
  subject exactly, off-axis, with only the heading low-passed (2 e-folds/s); orbits are 5-8 deg/s. No
  pulses, no shakes, no flybys. The subject is resolved once per shot and held.
- **The music is answered by the game.** `events` per archetype: boosts (`action … FullSpeedStraightAction`)
  on downbeats and strong beats, prisms devastated around the pilot (`blast`, the game's own
  `Prism.Damage(devastate)`) on downbeats and hard onsets, AI jousters steered at the pilot and at each
  other (`aim`, `converge`, through `AIPilot.SetExternalTargetProvider`), the brittlestar swarm herded in a
  new direction every bar (`herd`, through `Fauna.Goal`, which the creatures re-resolve only every 1-5 s so
  a per-frame write wins).

- **A control-port `wait` ends after 15 minutes** whatever N it was given: cut 3's first shoot stopped
  every shot at frame ~124 / 74 of 322 / 230 after exactly 15:00. `shoot.py` now waits in chunks of
  32 sim frames and reports the frame count; a 5 s shot at 1080p / 2x / 8x MSAA with two players sharing
  four cores takes 20-25 minutes.

### Garrett's feedback on cut 3 and what changed for cut 4 (2026-10-27)

Garrett: *"this is much better but the sparrow's guns have FX that look better than the white stream.
these shots are too austere. look at what we are capable of with swarms and other cells in the cell
selector. there is no skybox. these scenes should have all the subtle details of our game and more."*

What was wrong, measured:
- **"No skybox" was the game's own quality cut, not the engine's sky.** Every scene authors the
  procedural `HyperSeaSkybox` (stars, galactic band, core, nebulae, the cell overlay), but at runtime
  `RenderSettings.skybox` read `StaticHyperSeaSkybox`: quality presets Very Low to Medium replace it with
  a baked panorama (`QualityPresetCuts.asset`, `bakedSky: 1`), and the shoot profile's `prefs.json` was
  on **`gfx.Preset: 1` (Low)** because adaptive performance (`gfx.Adaptive: 1`, "Balanced" on the F7
  panel) had lowered it at the software renderer's frame rate. Those presets also cut cytoplasm to a
  quarter (`cytoplasmDensity 0.25`), cap the membrane's subdivisions at 3 and turn HDR off: the
  austerity. The Desktop platform profile itself cuts nothing.
- **The engine could not draw the baked sky** (`SkyboxPass.CanDraw` knew only the procedural shader),
  so on those presets Amoebius shows the camera's flat navy. Fixed in the engine: `SkyboxPass` now
  samples `CosmicShore/StaticSkyPanorama` with the shader's own equirectangular mapping (an engine
  gap every Amoebius player below High was seeing).
- **The "white stream" is a Sparrow round under a 180-degree shutter.** The round is a bright
  Fresnel sphere (`SparrowProjectileMaterial`, 0.5/0.7/1.2 HDR blue) with its charge-field arcs (both
  hand-translated in the engine); four averaged sub-frames at racing speed smear it into a white dash
  and bloom blows the core out. Cut 4 uses a 90-degree shutter (`accum` 2 of 4) so a round stays a
  glowing sphere; the material itself was not changed.
- **Wildlife Liberation at I4 is the richest cell**: 99 brittlestars, sharks and clawfish on one band
  (I3 had 40). The kaiju still has not spawned in any run (`find Worm` finds only the WormholeView).

What cut 4 does (mapping `render.prefs`, applied by shoot.py to the profile's prefs.json before each
player starts): **Ultra preset, adaptive performance off, Lush ecosystem, 8x MSAA, full VFX** - the
settings a player on a strong PC has, which keep the procedural sky, full cytoplasm, the full membrane
and HDR. `accum` 2. Wildlife at I4.

- **Two players sharing one profile must never have its prefs.json written while they boot.** The first
  cut-4 shoot wrote the cinematic prefs per shot, while the other shot's player was reading the file;
  that player booted as a first-time user (no birth year, no consent) and then saved its empty answers
  over the shared file, so every later player sat at the "Before you fly" prompt forever (the runner's
  first-run clicks were measured at 1600x900 and missed at 1920x1080). shoot.py now writes prefs ONCE,
  atomically, before any player starts, and scales the first-run clicks with the window.
- **The engine's panorama sky is verified**: with `gfx.Preset` 1 the menu renders the baked HyperSea
  panorama (galactic band, core, stars) behind the lava-lamp cell
  (`milestones/M1-sky-fire-41-56/engine-panorama-sky-on-low-preset.png`).
- **`pkill`/`kill` by a pattern that appears in your own command line kills your own shell** (exit 144
  here, mid-command, with nothing after it run). Stop players with their control port's `quit`, and
  match a pattern only the player's command line carries (`[c]ontrol-port 478`, bracketed).

### Garrett's feedback on cut 4 and what cut 5 does (2026-10-27)

Garrett: *"again these scenes are too sparse. too austere. these should be populated environments rich in
flora, fauna, multiple vessels and their various mass effect and trails, these environments should feel
like we are being taken through something with a past and a future full of dynamic activity. use all the
cells in the cell selector for inspiration. use lattice flora, borromean flora, sharks works, etc. compose
scenes like an orchestra."* And: *"use all 21 vessels if you can and all of their abilities if you can."*

The arcade arenas are purpose-built and sparse. The populated world is the **lava-lamp cell behind the
menu** (Menu_Main, the Toy Box's home): the cell selector swaps it to any cell (Arboretum, Lattice,
Garland, Crystal Wormhole…), the spawn matrix releases every species of fauna and flora and AI-piloted
vessels of all 21 roster classes through the menu's own networked pipeline. Cut 5 stages every shot
there, with new director verbs built on the game's own calls:
- `mv cell NAME` (`Cell.RequestCellSwap`): the scene's own list first, then any of the 61
  `CellConfigDataSO` assets, loaded by their `Assets/` path (the engine's `Resources.Load` now takes a full
  path as a port-only convenience; Unity would return null, and no game code passes one).
- `mv spawn vessel CLASS|all N [DOMAIN] [RADIUS]` (`MenuServerPlayerVesselInitializer.RequestSpawnAiCompanion`,
  the hangar station's call): a ring of AI pilots ahead of the local vessel, cycling the roster's 21
  classes - Manta, Dolphin, Rhino, Squirrel, Serpent, Sparrow, Urchin, Scarab, Butterfly, Grizzly, Thresher,
  Stoat, Goby, BrittleStar, Shrike, Angler, Wrasse, Orca, Starling, Jacks, Honeyguide. Twelve released at
  once worked (21 vessels live with the local); no replicated-slot cap hit here, unlike the Joust card.
- `mv spawn fauna|flora NAME N [ELEMENT]` (`CellLifeSpawnerBase.SpawnFaunaBanded` / `PlantFlora` on a
  runtime clone of a catalogue config, as the spawn matrix does): the Toy Box catalogue
  (`Resources/Toybox` → `Toy_SpawnMatrix`) holds 23 fauna species (Shark, Brittlestar, Worm Colony = the
  kaiju, QuadFish, Tadpole, Clawfish, Leviathan, Pack Hunter, Stampede, the swarms…) and 27 flora
  (Borromean, Nested Gyroid, SchwarzP, Quasicrystal, Mandelbulb, Coral Bloom, Lantern, Apollonia…), each in
  its elements. `mv lifeforms` prints them. The Worm Colony spawned on request (28 segments), which the
  Wildlife Liberation arena never did.
- Abilities: `action aiN Button1Action..Button4Action` fires each hull's own bound abilities; the
  composer's `{b}` cycles the four buttons, `{j}` the twelve nearest hulls, `{dom}` the four domains
  (`domain X` = the Domain Changer toy's call: the mass effect in a new colour every bar).

The arcade archetypes of cuts 1-4 are retired from the mapping (kept in `milestones/`); four staged
scenes replace them: arboretum-fleet (build), lattice-rally (bright, all 21 hulls), kaiju-siege (the
drop: a Worm Colony with twelve hulls converging), garland-shoal (breakdown).

## Log

| Date | Cut | Score said | Person said | Changed |
|---|---|---|---|---|
| 2026-10-27 | M1 cut 1: SKY FIRE 41.0-56.0, flyby (Skim Race I2) → chase (Joust I3) → chase (Wildlife I3) | loss 3.44; dead frames 12 % (the flyby's empty opening), stuck 17 % (the Joust subject died), luminance-vs-brightness -1.0 | waiting | cameras moved closer; brightness targets follow measured luminance; `fastest` subject + the alive rule; `cue` scheduling; `camera main` default |
| 2026-10-27 | M1 cut 4 (Ultra preset: procedural sky, full cytoplasm and membrane, HDR; Lush ecosystem; Wildlife I4; 90-degree shutter): same three mounts and orchestration as cut 3 | loss 0.67; luminance-vs-brightness +1.0, dead 0 %, stuck 11 %, motion-vs-energy 0.19 (the motion criteria still read blurred frames) | waiting | the engine draws the baked panorama sky on lower presets; prefs written once and atomically |
| 2026-10-27 | M1 cut 3 (orchestrated, 1080p/60, 8x MSAA, 2x SS, 4 sub-frames): mount in the Wildlife cage (swarm herded each bar) → mount in the Joust cavern (jousters converged on the pilot, prisms burst on the bars, boosts on strong beats) → mount on the Skim Race rail (boosts on the bars, bursts on hard onsets) | loss 0.58; dead 0 %, stuck 8 %, luminance-vs-brightness +0.5, motion-vs-energy 0.23 (up from 0.19), motion-vs-onsets 0.005 (the accumulation blurs the per-frame motion measure; the criterion needs rethinking for blurred frames) | waiting | first cut under the new rules; 63 orchestration events fired across the three shots |
| 2026-10-27 | M1 cut 2: chase (Wildlife I3) → chase (Joust I3, `fastest`) → chase (Skim Race I3, `nearest`) | loss 0.70; dead 0 %, stuck 10 %, luminance-vs-brightness +0.5; motion-vs-energy 0.19 and motion-vs-onsets 0.03 still under floor | waiting | nothing yet: next are beat-driven camera moves and a camera-in-mass guard (above) |
