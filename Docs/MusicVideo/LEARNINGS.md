# Music video generator: learnings (the memory the mapping improves from)

Read before every cut; add a row after every cut. A row says what was tried, what the scorecard and the
person said, and what changed in `Tools/Build/music_video/mapping.json` (or the tools) because of it.
`README.md` is the system; this file is why its numbers are what they are.

## Status

| Milestone | State | Where |
|---|---|---|
| M1: three ~5 s beats over ~15 s of one song | **cuts 1-2 judged by Garrett (prototype look, jitter, camera dances - see below); cut 3 judged by Garrett (sky missing, too austere, the Sparrow rounds smeared); cut 4 (Ultra preset, procedural sky) judged by Garrett too sparse; cut 5 stages every shot in the lava-lamp cell with the cell selector's cells, the 21-class fleet, fauna, flora and the kaiju, and is the current cut.** Cuts 1-2 (2026-10-27) (SKY FIRE 41.0-56.0 s: build → the drop at 46.37 → drop). Cut 1: Skim Race flyby → Joust chase → Wildlife chase. Cut 2 (closer cameras, bright bars in brighter arenas, the alive-subject rule): Wildlife chase → Joust chase → Skim Race chase. The scorecard prefers cut 2 on five of seven criteria (no dead frames, luminance now follows brightness, less stuck time); motion-vs-energy and motion-vs-onsets are still under their floors in both. | `milestones/M1-sky-fire-41-56/cut1`, `cut2` |
| M2: a minute of two songs with different vibes, then a whole song | **cut 6 (2026-10-28): SKY FIRE 30-90 s and SPACE GLIDING 20-80 s, shot from mapping v3** (section-aligned phrases, twelve cells, 10x the population, sickness-safe mounts). SKY FIRE: loss 2.49; SPACE GLIDING: loss 1.63 (the best cut so far). Both delivered to Garrett 2026-10-28; the results and what the sheets showed are in the log below. A whole song is the same command with `--start 0 --end <duration>`. | `milestones/M2-sky-fire-30-90`, `milestones/M2-space-gliding-20-80` |
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

What staging a populated scene taught (the first cut-5 shoot was as empty as cut 4):
- **The pilot must fly INTO the life, not away from it.** The lava-lamp autopilot wanders a cell a
  thousand units across at 50 u/s; everything released at the pilot's position is behind it in two
  seconds. The scene holds together only when the pilot is aimed at the life (`aim local flora`, or at the
  kaiju's head), the fleet is aimed at the pilot (`aim all local`, re-issued every bar), and the swarm's
  goal rides the pilot (`herd follow local 40`). Flora plants at its own sites, not at the pilot, so it is
  released first and the pilot is sent to it; the settle is the flight there (8 s).
- **The menu's camera controller poses in LateUpdate, which the engine flushes after the director's
  post-tick pose**, so in Menu_Main the mount had to stand down every `*CameraController` in the scene
  (not only the camera's own parents) and re-acquire `Camera.main` when a cell swap or a spawn switches
  it. Mounts sit wider in a populated cell (back 44, up 15, side 16, lead 30).
- **Never blast at the kaiju: its body is prisms.** `blast name:WormHead` devastated the worm's own
  segments; the head died within two bars, the pilot aimed at it stopped on the spot (cut 5's first
  shoot) and an orbit on it froze (the second). Bursts go around the pilot and the fleet; a camera whose
  subject dies now hands over to the fleet's centre; the fleet fights the worm with its own abilities.
- **A species name matches exactly before it matches by substring** (`Borromean` was finding "Swarm
  Borromean" first).

### Garrett's feedback on cut 5 and what cut 6 does (2026-10-28): the phrase, the population, the camera

Garrett: *"A lot of room for improvement, and I'd like to see more that feels like fun accessible gameplay and
less erratic camera movements that are gonna make people sick. The scenes still feel like they could be 5 times
as diverse and 10 times as populated. I'm looking forward to more shots with greater diversity, longer shots
where you could build up a narrative and some payoff. Showcase a set up and payoff from a combo or loop
completion within a kit. Take these ideas and see what you can do to update the skill itself and then apply it
to 60 seconds of a song and 60 seconds of a different song with a different vibe."*

Mapping v3 answers each line:
- **Longer shots with a narrative: shots follow the song's sections** (`shots` in the mapping, `compose.py
  --beats auto`): one shot per section, 6-16 s (a short section merges into its shorter neighbour, a long one
  splits on downbeats), and a shot that ends where a drop begins runs two bars INTO the drop so its payoff
  lands on the drop. SKY FIRE 30-90 s became five shots of 9.6-13.4 s; SPACE GLIDING 20-80 s five of
  9.5-15.7 s. The 5 s beats of M1 were too short for a setup and a payoff (the scorer now has a
  `shot_seconds` criterion, floor 8 s; cut 5 scores +1.2 on it).
- **Setup and payoff: every shot is a phrase.** The payoff is the section boundary inside the shot (the drop)
  or the downbeat nearest 70 % through it; the event templates are split into `setup_bar` / `setup_beat` /
  `setup_strong` (before it), `payoff` and `payoff±N` (around it) and `release_*` (after it). Each archetype
  names its phrase: **the kit combo** (abilities 1, 2, 3 on the setup bars, ability 4 + boost + a burst of the
  pilot's own prisms on the payoff), **the loop** (`mv circle local R {loop_s}`: the pilot's AI chases a point
  circling at R, one loop per `{loop_s}` = the seconds to the payoff, so the loop closes ON the payoff and the
  wake bursts), **the siege** (the fleet converges on the kaiju bar by bar, then every hull boosts in at once),
  **the joust** (pilot and fleet aimed head-on, then the meeting), **the bloom** (a flora planted per bar, then
  twenty brittlestars released at once), **the shoal** (herded onto the wake, then scattered upward). Director
  additions for it: `circle`, `action all` (every AI hull at once: the fleet's payoff), `domain` forwarded
  from the director's cues (cut 5's `domain {dom}` cues had answered "unknown command" in the player log -
  read the player log's `[mv]` answers, not just shoot.log), and multi-word species names (`spawn flora Coral
  Bloom 3`: cut 5's "Coral Bloom" had failed to parse as a count and planted nothing).
- **5x the diversity: twelve archetypes in twelve cells** (Arboretum, Lattice, Garland, Hesperides, Crystal
  Wormhole, Yggdra, Swarm, Tandava, Geode, Orrery, Boneyard, Zephyr), each with its own flora set, fauna set,
  fleet size and domain, and a `sections` list that earns a bonus when it names the shot's section; an archetype
  AND a cell already used in the cut are penalised, so a five-shot minute visits five cells.
- **10x the population**: 60-100 fauna per scene in shoals (40 brittlestars, 40 quadfish, 30 tadpoles, 6-8
  sharks, a leviathan, stampedes, pack hunters, the kaiju), 6-12 flora of 2-4 species, 6-21 hulls (all 21 in
  the rallies). The smoke test (Arboretum, 66 fauna, 8 flora, 14 hulls) simulated at about 20 ticks a second
  at 240 Hz: a 12 s shot is 2-3 minutes of settle and sim before its render, which dominates anyway.
- **Sickness-safe cameras**: every camera is a mount whose heading is smoothed over ~1.6 s with the horizon
  leaning only a quarter of the way with the hull (the roll was what made cut 5 erratic), or an orbit at ≤ 4
  deg/s; no dollies; FOV 56-66. The mount sits at back 40 / up 13 / side 12 (at back 50 the pilot was a speck
  in the smoke test, and "fun accessible gameplay" needs the pilot's hull readable).
- **Render**: a cut over 20 s renders at 1.5x supersampling (`render_scale_long`) instead of 2x, still on
  8x MSAA; a populated 12 s shot at 1080p is about an hour, so a minute is 2-3 hours per song and the two
  songs shoot side by side (four players on four cores, ~2 GB each).
- **A minute of frames does not fit on the disk: shoot.py encodes as it records.** A 1080p PNG of a populated
  cell is 5-6 MB, a 60 s cut is 3600 of them, and the first cut-6 shoot filled the machine's volume at 1300
  frames: every player died on its next write ("Remote end closed connection without response" on the control
  port), and six shots had to be re-shot from the start. Each shot's frames now go, as they land, into one
  ffmpeg (`shot.mp4`, H.264 4:4:4 crf 8, near-lossless; ~0.4 MB a frame) and the PNG is deleted, so a shot
  holds two frames on disk; `frames.csv` still carries every frame's game time, and assemble.py / score.py
  read `shot.mp4` (the sheet's stills are extracted from it).
- **Three players at most on a 16 GB box.** A player recording a populated cell at 1080p / 1.5x / 8x MSAA
  holds 3.3-3.5 GB; four of them tripped the memory cgroup's OOM killer (the player log ends in "Killed",
  `dmesg` says `Memory cgroup out of memory`, the control port answers "Remote end closed connection").
  Two songs side by side are `--jobs 2` for one and `--jobs 1` for the other. And the rule the skill already
  states was paid for again: `pkill -f` with the runner's path in the pattern matched the calling shell
  (exit 144) and killed the runner but not its players; stop players by PID (`ps` + `[c]ontrol-port` bracket
  patterns), never by a pattern your own command line contains.
- **The different vibe picks itself**: SPACE GLIDING (108 bpm, mean energy 0.18, the quietest song in the
  project) opens on the shoal and the loop (yggdra-shoal, hesperides-loop) and only reaches a siege in its drop;
  SKY FIRE (123 bpm) runs wormhole-run → the kit combo on the drop → joust → rally → swarm siege.

### Garrett's feedback on cut 6 (2026-10-28): rejected, back to short clips

Garrett: *"this footage was nothing but erratic flights of a squirrel with a camera lazily following it. that is a
vanishingly small and not understandable subset of the gameplay we offer. where is the dolphin shooting things, the
thresher slicing things, the serpent building a wall, where are the gyroids? this is not even usable yet, and no
where close to the level of composition that we would want to be showing off our trailer. the spindle shaders look
wrong. there were frames with artifacts like z fighting. lets go back to you making short clips because you are
still way off the mark."*

What this says about cuts 5 and 6, read honestly:
- **The subject was always the Squirrel**, the menu's default local vessel, on the lava-lamp autopilot - a hull
  wandering a cell. Population and events around it did not make it gameplay: the camera followed a pilot doing
  nothing legible. The fleet's abilities fired on the AI hulls off-screen.
- **Composition means a vessel doing ITS thing, framed so the thing reads**: the Dolphin's echo cone taking
  prisms, the Thresher's ball and chain slicing a trail, the Serpent laying its seed wall, a gyroid lattice flown
  through. One action, one clear frame, a few seconds.
- **The renderer has faults that disqualify the footage before composition is judged**: the Spindle shader
  graphs (SpindleWireGraph, SpindleLodMid/Far, SpindleDepth - the flora's limbs) do not compile in the engine and
  draw as the generic family, so every flora looks wrong; and there are z-fighting frames.

The next milestone is therefore **M1 again, as short clips**: fix the spindle graphs and the depth artifacts in
the engine first, make the featured hull the local vessel (`mv vessel CLASS`, the Vessel Changer's own swap), and
shoot one 4-6 s clip per vessel ability, each composed from stills before it is recorded. Cuts 5-6 stay in
`milestones/` as the record of what populated-but-aimless footage looks like.

## Log

| Date | Cut | Score said | Person said | Changed |
|---|---|---|---|---|
| 2026-10-27 | M1 cut 1: SKY FIRE 41.0-56.0, flyby (Skim Race I2) → chase (Joust I3) → chase (Wildlife I3) | loss 3.44; dead frames 12 % (the flyby's empty opening), stuck 17 % (the Joust subject died), luminance-vs-brightness -1.0 | waiting | cameras moved closer; brightness targets follow measured luminance; `fastest` subject + the alive rule; `cue` scheduling; `camera main` default |
| 2026-10-28 | **M2 cut 6b: SPACE GLIDING 20-80 s** (mapping v3, the quietest song: 108 bpm, mean energy 0.18): Yggdra shoal (the shoal gathers on the wake, scatters upward at 26.4) → Hesperides loop (`circle` closing on the payoff at 37.0; re-shot once the half-way re-aim was removed - take 2 stayed inside the life for the whole phrase where take 1 drifted to the membrane) → Tandava joust paying off ON the drop at 44.9 → Boneyard stampede (15.7 s, the fleet boosting in on every beat) → Crystal Wormhole run. Five shots of 9.5-15.7 s in five cells, 123 events. | loss 1.63 (the best cut so far); dead 0 %, stuck 0.9 %, luminance-vs-brightness +0.5, diversity 1.0, shot_seconds 12.0; motion criteria still unreliable on blurred populated frames | waiting | the orbit archetypes (Yggdra, Orrery) read sparse when the orbit swings to the pilot's empty side: next, aim the orbit's subject at the life every bar, or orbit the flora instead of the pilot |
| 2026-10-28 | **M2 cut 6a: SKY FIRE 30-90 s** (mapping v3): wormhole run (build) → Arboretum kit combo, abilities 1-2-3 on the bars and ability 4 + boost + burst ON the drop at 46.37 s → Tandava joust (head-on, the meeting at 59.8) → Lattice rally (all 21 hulls, every hull's ability 4 at 73.2) → Swarm siege (payoff at 84.3). Five shots of 9.6-13.4 s in five cells, 135 events. | loss 2.49 (cut 5: 4.25 on the same criteria); dead 0 %, stuck 0 %, diversity 1.0, shot_seconds 12.0; motion criteria still read blurred populated frames (-0.01); luminance-vs-brightness -0.6 (the Swarm cell is the darkest and the brightest bar) | waiting | seen on the sheet: the Lattice mount passes through a white flora surface at its payoff and its last bars thin out as the pilot outruns the fleet (next: a camera-in-mass guard and a re-aim on the release bars); the kit combo's first frame opens inside a prism cluster |
| 2026-10-28 | M1 cut 5 (staged in the lava-lamp cell): Arboretum fleet (10 hulls, sharks, 24 brittlestars, Borromean + coral-bloom flora) → kaiju siege (Garland, a Worm Colony, 12 hulls converging, orbit on the head) → lattice rally (all 21 hulls, quadfish, tadpoles, quasicrystal + nested-gyroid flora, the pilot's domain shifting every bar) | loss 3.05; dead 0 %, stuck 11 %, diversity 1.0 (by scene); motion-vs-energy negative (a populated frame moves whatever the music does - the criterion needs the camera's own motion subtracted) | waiting | the kaiju shot was re-staged twice (the pilot parked on the head; blasts killed the worm); the orbit's last second drifts to empty membrane when the head dies |
| 2026-10-27 | M1 cut 4 (Ultra preset: procedural sky, full cytoplasm and membrane, HDR; Lush ecosystem; Wildlife I4; 90-degree shutter): same three mounts and orchestration as cut 3 | loss 0.67; luminance-vs-brightness +1.0, dead 0 %, stuck 11 %, motion-vs-energy 0.19 (the motion criteria still read blurred frames) | waiting | the engine draws the baked panorama sky on lower presets; prefs written once and atomically |
| 2026-10-27 | M1 cut 3 (orchestrated, 1080p/60, 8x MSAA, 2x SS, 4 sub-frames): mount in the Wildlife cage (swarm herded each bar) → mount in the Joust cavern (jousters converged on the pilot, prisms burst on the bars, boosts on strong beats) → mount on the Skim Race rail (boosts on the bars, bursts on hard onsets) | loss 0.58; dead 0 %, stuck 8 %, luminance-vs-brightness +0.5, motion-vs-energy 0.23 (up from 0.19), motion-vs-onsets 0.005 (the accumulation blurs the per-frame motion measure; the criterion needs rethinking for blurred frames) | waiting | first cut under the new rules; 63 orchestration events fired across the three shots |
| 2026-10-27 | M1 cut 2: chase (Wildlife I3) → chase (Joust I3, `fastest`) → chase (Skim Race I3, `nearest`) | loss 0.70; dead 0 %, stuck 10 %, luminance-vs-brightness +0.5; motion-vs-energy 0.19 and motion-vs-onsets 0.03 still under floor | waiting | nothing yet: next are beat-driven camera moves and a camera-in-mass guard (above) |
