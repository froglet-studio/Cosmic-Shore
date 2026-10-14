# Converter Reel

A browser lab that turns the Elemental Converter's convert animation into a seamless, shareable video, and lets the
team choose its look before export. There is no CONVERT button and no UI in it.

- **Page (built, do not edit):** `Docs/Studios/ConverterReelLab.html`. Open it in Chrome or Edge on a computer to
  export, or use the live page.
- **Live page:** https://claude.ai/artifact/V7T3ou5hZUDJDHntohZqac (private until shared from its Share menu; team looks saved there land in its `looks` collection)
- **Sources:** `Tools/Build/converter_reel/`: `src/*.js`, `page.html`, and `build.py`, which inlines the shipped assets
  and the sources.
- **Rebuild:** `python3 Tools/Build/converter_reel/build.py`. `--check` fails when the page is stale against the assets
  or the sources.
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/ConverterReelLab.html`.
- **Headless render:** `node Tools/Build/converter_reel/render_headless.cjs out.mp4 [--format 0 --fps 60 --ss 2 --settings '{…}']`
  renders frame by frame to ffmpeg.

## The request (2026-10-14)

> make an animated video that i can share on social media with high quality, frame rate etc. that shows our new convert
> animation without the convert button. create it so it loops by pulling them out in the same order they were built
> i.e. first in, first out. build a lab that allows us to explore different color options etc. before export

## What the reel plays

One loop, about 55 s at the game's own timing (play 7):

| Phase | What happens | Whose rule |
|---|---|---|
| Idle | The four crystals idle with their own effects: Space's spin, Time's flip wave, Charge's seam walk, Mass's depth columns | The game (`OmniCombinePlayer`, round-5 effects) |
| Build | Space, Charge, Mass, Time set off one after another, morph, and socket in. This is revision 8 of the converter: Charge and Time hold their spacing until centred, and the Mass prisms come down their columns | The game (`OmniCombineCore`, a press at 1.5 s) |
| Hand-off | The omni dithers in over the landed plates (0.6 s), swells and wakes its effects (0.7 s) | The game |
| Omni awake | One cadence cycle (8.33 s): a pentagon turn, a flip wave, a triangle turn, a flip wave. The flux arcs chain and strike the raised triangle prisms | The game (`OmniCadence`, `ChargeArcs.Flux`) |
| Settle and hand back | The effects ramp back to rest at a cycle boundary, so every square and rhombus is at rest. The omni dithers back out over the landed crystals | Lab: the game's hand-off, run in reverse |
| Pull-out, first in first out | Space leaves first, then Charge, Mass and Time. Each crystal's own pose runs backwards: it un-sockets (the Mass prisms rise back up their columns), un-morphs, and flies back to where it idled. Its idle effect fades back in on arrival. Each leaves toward the camera's side, aimed the way `FaceCamera` aims an arrival | Lab |
| Idle | Back to the start; the last frame flows into the first | Lab |

**The pull-out does not exist in the game.** It is the game's pose function with each crystal's flight clock running
backwards, which makes it a proposal. If the game ever plays a split, this is the reference.

## How the lab is built (the page plays the game's code)

The converter's C# was ported to JavaScript and is **held to the shipped C# by three parity gates**. Each gate runs the
shipped files headless under .NET 8. Nothing under `Assets/` is copied or edited.

| Port | Ports | Gate | Result (2026-10-14) |
|---|---|---|---|
| `src/combineCore.js` | `OmniCombineCore` (Pose, OmniPose, Frame, FaceCamera, CrystalFrame, seeding, flip rig, socketing, MassColumnPlace), `OmniCombineMotion`, `SquareTurnFit`, `OmniCadence`, `OmniCombineConfigSO.Draw` | `parity_core/run.sh` | 14 build times × 4 crystals, plus 11 omni beats: worst 4.3e-7 R (tolerance 1e-4 R). Negative control (socket spacing +1%): 1.3e-3 R, caught |
| `src/chargeArcs.js` | `ChargeArcs.cs`: sites, the seam walker, the flux chain, the mesh writer, seam walk, flux and strikes, `System.Random` | `run_parity_charge.sh` | 2,938,658 values, 0 differences (bit for bit), including 6 large and negative `System.Random` seeds. Two negative controls fail as they must |
| `src/depthPrisms.js` | `DepthRatchet`, `CrystalDepthPrisms` (StepColumns, ColumnBody, TryGetPlates), the converter's `DepthColumns`, `DepthShape` and WriteMass | `parity_depth.cjs` | 1,200 frames × 3 cases, 0.0 (bit-exact float32). Four negative controls fail. The shader's vertex stage cannot run in C#, so it is checked against hand-derived cases (3e-7) and a GPU-vs-CPU render (0 of 15,126 pixels differ) |

The shaders are GLSL ports of `OmniCrystalFresnelShader` (the body), `OmniDepthPrismShader` (the Mass and
triangle prisms) and `CrystalChargeArcsShader` (the plasma), on the shipped material colours. The page reads the
shipped assets through `build.py`: `OmniCombineData.json`, `OmniCombineConfig.asset`, `OmniCrystalCadence.asset`,
`OmniCrystalBody.mat`, the depth and arc materials and profiles, `OmniCrystalTriangles.asset`, and the domain
colours (`studio-domains.js`).

**Stateful pieces are baked.** The depth ratchets and the camera's framing ease carry state, so the reel steps two whole
loops at 60 Hz and keeps the second. Frame 0 then continues from the last frame, and a scrub shows exactly what
playback shows. The Charge seam walk is a pure function of its start time and its clock, so it is rebuilt when the
clock goes backwards.

**Each crystal's idle effects run on their own clock.** That clock restarts only mid-flight on the way in, where the
effect has fully faded, so the loop point is invisible (the scorecard below).

## The lab

- **Look:** starting points:
  - Game: the shipped materials on a dark sky;
  - Element hues;
  - the four domain colours (Jade, Ruby, Gold, Blue);
  - four lab looks (Abyss, Solar, Ultraviolet, Daylight).

  Then every colour and dial:
  - the body pair (`_BrightColor`, `_DarkColor`, `_Spread`) and an HDR gain;
  - per-element tints (lab only: in the game, element is shape, not colour, `Docs/PALETTE.md`);
  - the Mass prisms (they follow the body by default, as both materials ship the body's pair) and their strike colour;
  - the Charge light (`_BrightColor`, `_ArcCoreColor`, bolt and glow intensity);
  - the backdrop (gradient, halo, stars fixed to the sky, vignette);
  - the grade (exposure, bloom, tonemap, saturation, contrast, grain).
- **Motion:**
  - the play (which draw from the game's ranges; each CONVERT press draws a new one);
  - omni hold cycles, idle before and after, the pause before the pull-out, video speed;
  - the camera: field of view, pitch, framing and framing ease (the game's), orbit turns per loop (whole turns, so
    the loop closes);
  - the fit mode:
    - **Tight** (the lab default) fits what the camera actually sees, in both directions of the frame, and pulls back
      ahead of time;
    - **Game** frames a sphere to the height, as `ElementalConverterView` does, which leaves a 9:16 frame mostly
      empty and clips the sides;
  - **Raised prisms**, which keeps the woken omni's raised triangle prisms in frame. The game frames the body only, and
    they then run off the frame's edges.
- **Export:**
  - sizes: 9:16 1080×1920 (Reels, TikTok, Shorts), 4:5, 1:1, 16:9, and 4K 9:16 and 16:9;
  - 24, 30 or 60 fps;
  - 4× MSAA with 1–3× supersampling;
  - three bitrates;
  - MP4 (H.264) or WebM (VP9);
  - 1–3 loops per file;
  - a caption, off by default;
  - app safe-zone overlays for the preview.

  Export renders every frame at the export size at exactly `i / fps` and encodes through WebCodecs, so the file is smooth
  whatever the device manages live. Inside claude.ai the file is saved through the `downloads` capability.
- **Saved:** team looks. A named look is saved to the page's shared store (`looks` collection) and anyone who opens
  the page can apply it. Claude reads it with `ArtifactData list looks`.
- **Check:** the loop-seam scorecard and the list of what is not modelled.

## Scorecard: does it loop?

The yardstick is the largest shape change (symmetric Hausdorff distance per crystal) from the last frame to the first,
divided by the largest shape change between two idle frames. A value near 1 means seamless. The control plays every
idle effect on one clock that restarts with the loop, which is what cutting the video anywhere else would do.

| Play | Loop | Seam / idle step | Control: naive cut / idle step |
|---|---|---|---|
| 7 | 55.27 s | **0.69** | 10.89 |
| 21 | 57.48 s | **1.14** | 12.46 |
| 64 | 54.72 s | **0.93** | 9.63 |

(`verify_lab.cjs` batch, 60 fps.) Sweeping the whole loop at 30 Hz, the largest frame-to-frame shape change is 0.061 R
(play 7, at 8.73 s: Charge in mid-flight), so neither the hand-back nor the pull-out jumps.

## Checked

- `verify_lab.cjs` PASS on desktop and phone: hooks, determinism within a page and across two page loads, and layout.
  Both screenshots were read.
- All three parity gates pass, and their negative controls fail.
- Frame strips across the whole loop:
  - the four idle;
  - Space, Charge, Mass and Time arrive and socket in;
  - the hand-off;
  - the omni awake, with flux arcs, raised prisms and the cadence;
  - the settle and hand-back;
  - Space, Charge, Mass and Time leave in that order;
  - the frame at 55.25 s and the frame at 0.01 s match.
- The in-page exporter, run in headless Chromium: 20 frames encoded through WebCodecs and muxed to MP4 (313 KB). That Chromium build has no H.264 encoder (`avc1.*` not supported) and fell back to VP9. Desktop Chrome and Edge encode H.264, and the page says so when it falls back.
- The headless render of the default look (`render_headless.cjs`, 12 resumable chunks, 3 browsers):
  - the master is 1080×1920, 60 fps, 3,316 frames, 55.27 s, H.264 High CRF 15 (8.6 Mbps, 56 MiB);
  - the share copy, a two-pass 4 Mbps encode of the master, is 26 MiB at SSIM 0.995 against the master;
  - in the file, the change from the last frame to the first is 0.52 mean levels, against 0.31 between ordinary
    neighbouring frames (the camera orbit keeps moving), and the chunk joins sit in the same range.
  Neither file is in the repo: re-render with `render_headless.cjs`, or export from the page.
  delivered `converter-reel-1080x1920-60fps.mp4`.

## Found

1. **The ported `System.Random` lost int32 wrap-around.** The Charge port passed bit-for-bit parity on its one seed, but
   per-play seeds near 2^31 overflowed the legacy seeding in doubles. `Next(n)` returned n, and a flux discharge had no
   sink. It is fixed, and the gate now covers large and negative seeds (labmaker L-STU-65).
2. **A per-vertex seam yardstick false-alarms on congruent snaps.** A Space spin or a Time flip lands on the same shape
   and snaps its vertices back to rest. Shape distance is the right measure (L-STU-64).
3. **The game's framing rule leaves a 9:16 frame mostly empty.** It frames a sphere fitted to the vertical field of view,
   and it lets the woken omni's raised prisms leave the frame. On a phone in portrait the converter screen clips the
   spread crystals at the sides. That is worth a look on the game side if the Ark screen ships in portrait.
4. **(From the depth port)** `DepthRatchet.ColumnTop`, used by the rise-while-hidden test, leaves out the column's `k`
   on the top-face term. At place 0 it sits 0.046 units below the top the shader draws (`OmniCrystalTriangles`). It is
   ported as shipped.

## Not modelled

- The menu skybox behind the converter, and the game camera's post stack (bloom, tonemapping). The backdrop and the
  grade are lab choices.
- `System.Random`'s per-press draw. The reel draws the same ranges from a seeded generator, so a Play number names one
  play.
- The depth prisms' `CrystalPoseJitter` (the converter does not use it).
- Colour is judged on the viewer's screen. Platforms re-encode, so check the exported file on a phone before posting.

## Decision needed (team)

- Which look ships on social. Save candidates under **Saved**.
- Whether the pull-out should exist in the game (a "split" playback), now that it has a reference.
