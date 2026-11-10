# The genome nebula skybox: an evolved sky as an alternative to the HyperSea sky

Status: **prototype on branch `claude/genome-nebula-skybox`, used by no scene, no profile, no build.** Garrett asked
(2026-11-10) whether the skybox that the Amoebius genome search evolved could come into Unity as an alternative
skybox, and whether it has benefits or issues compared with the current one. This file is the answer and the
instructions for trying it.

## 1. What it is

The Amoebius genome project (a .kkrieger-style operator-graph genome searched against Amoebius gameplay footage;
shared folder `/mnt/project-files/amoebius-genome/`) produced a hand-built seed, `seeds/amoebius_like/minimal__tuned`,
that a hill climb tuned until its whole 10-node genome (118 zlib bytes) scores 1.03 on the footage statistics. That is
as close to the footage as Amoebius is to itself (1.1). Its background is four of those nodes:

| node | op | what it does |
|---|---|---|
| 0 | `tex.perlin` freq 3, 4 octaves | the nebula colour: dark green-black to lavender-blue |
| 1 | `tex.perlin` freq 6, 3 octaves | a grey warp field |
| 2 | `tex.distort` 0.43 | node 0 pushed around by node 1 |
| 3 | `tex.distort` 0.33 | node 2 pushed around by node 1 again: the marbling |

Then the out node's `sky` gene (0.35), a little fog, and the lab's grade (exposure, ACES fit, saturation, contrast).

**It is a compressed mimic of our own sky, not a new art direction.** The footage it was fitted to is Amoebius, the
engine port of this game, so the background it learned is the HyperSea sky as Amoebius draws it. The search kept the
part of HyperSea that moves the statistics most (the dark blue-violet domain-warped nebula) and dropped what is small
on screen: the stars, the Voronoi cell net, the teal nebula colour, the galactic band, the core and Andromeda.

## 2. The files

| file | what |
|---|---|
| `Assets/_Graphics/Materials/Shaders/GenomeNebula/GenomeNebula.hlsl` | the four nodes, line for line from `agenome/glsl.py`, plus a pole-free 3D variant and an optional drift |
| `Assets/_Graphics/Materials/Shaders/GenomeNebula/GenomeNebulaSkybox.shader` | pass 0 the skybox, pass 1 an equirect bake laid out like `StaticSkyPanorama.shader` |
| `Assets/_Graphics/Skyboxes/GenomeNebulaSkybox.mat` | the genome's numbers (Vectors, so Unity never sRGB-converts them) |
| `Assets/_Graphics/Materials/Shaders/GenomeNebula/GenomeNebulaCubeSky.shader` | draws a baked cubemap: one sample per pixel |
| `Assets/_Scripts/Utility/GenomeNebulaSkyBaker.cs` | puts the sky on screen by one of five methods (§6): procedural, panorama, cubemap, time-sliced cubemap, 3D noise |
| `Tools/Build/genome_nebula_methods.py` | the §6 bench: every production method rendered on a real GL pipeline and scored against one reference |
| `Tools/Build/genome_nebula_preview.py` | compiles the `.hlsl` as C++: previews, seam check, lab parity, CPU cost against HyperSea |

To try it in the editor: put `GenomeNebulaSkybox.mat` in Lighting > Environment > Skybox Material (procedural), or
add `GenomeNebulaSkyBaker` to any object, give it that material (plus a `GenomeNebulaCubeSky` material for the cubemap
methods, or `Resources/PlatformSkyboxes/StaticHyperSeaSkybox.mat` for the panorama) and pick a method. Nothing here has been opened in a Unity editor yet: **the shader has not been compiled by
Unity** (see §5).

## 3. Measured

All numbers from `python3 Tools/Build/genome_nebula_preview.py` on 2026-11-10 in a cloud container (4 CPUs, no GPU).

**Parity with the lab.** The port and the lab's own GLSL (rendered with moderngl from `agenome.glsl.compile_textures`)
over a 1024x512 panorama: mean error 0.35 of 255, 0.6% of pixels off by more than 8. The differences are thin
filaments where the double domain warp is steepest and float rounding moves them by a fraction of a pixel; the
picture is the same (`/mnt/project-files/amoebius-genome/docs/genome_nebula_parity_lab_vs_port.png`: lab, port,
difference x8). Different GPUs will differ from each other the same way.

**Cost (CPU proxy for ALU, one thread, 640x360 view, best of 3):**

| sky | ns/pixel | vs HyperSea |
|---|---|---|
| HyperSea procedural (`HyperSeaSkybox.shader` + its material) | 2297 | 1.00x |
| genome nebula, equirect (lab-exact) | 535 | 0.23x |
| genome nebula, sphere (pole-free) | 464 | 0.20x |
| either sky baked (`StaticSkyPanorama`) | one texture sample | ~0 |

By count: the genome sky is 10 octaves of 2D value noise (4 hashes each) and no Voronoi; HyperSea is about 20 octaves
of 3D value noise (8 hashes each) plus two 3x3x3 Voronoi searches. The ratio agrees with the timing. This is not a
frame time: a device run (`DiagnosticsHUD`, Docs/PLATFORM_UNIFICATION.md) is the real measure.

**Seams and poles (2048x1024 panorama):** mean colour step across the u=0/1 seam 1.03 (equirect) and 1.29 (sphere),
against 1.90 / 1.71 between any other two columns: no seam in either. Spread along the row next to the pole: 24.6
equirect (the pinch is visible as a star of streaks when looking straight up, as in the lab's own frames), 4.2 sphere.

**Size:** the material is 1.5 KB (758 bytes gzipped), the shader and include 11.6 KB of text. The genome's whole
sky is about 60 bytes of the 118-byte genome. The current baked sky is `StaticHyperSeaSky.png`, 4096x2048, 6.7 MB in
the repo, shipped in `Resources` on every platform. A runtime bake at 2048x1024 RGBA8 is 8 MB of GPU memory (2 MB at
1024x512), made in one draw at load, with nothing shipped.

## 4. Benefits and issues against the current sky

| | HyperSea (current) | genome nebula |
|---|---|---|
| Per-pixel cost, procedural | heavy (the biggest sky cost on a budget phone, PLATFORM_UNIFICATION.md §1) | ~1/4-1/5 of HyperSea |
| Low tier | 4096x2048 baked PNG, static | runtime bake, static, no texture shipped |
| Build / download size | 6.7 MB PNG in Resources | ~1.5 KB material |
| Animation | drift and star twinkle | static in the lab; this port adds an optional drift of the warp field (`_Drift`, clip: `docs/genome_nebula_drift.mp4`); the baked path is static like HyperSea's bake |
| Content | nebula, stars, galactic band and core (a sense of direction), Andromeda, the cell net that ties the sky to the membrane, the atmosphere bridge | the nebula only. No stars, no landmark, no cell net |
| Brightness | dark; prisms and trails read on top of it | brighter and busier (lavender marbling over much of the sky); it competes with the neon prisms for attention. `_SkyIntensity` turns it down |
| Seams / poles | none (3D noise over the direction) | none in longitude; equirect pinches at the poles, sphere mode does not |
| Tuning | 40 hand-named properties an artist knows | ~12 genome numbers; good values come from the search, not by hand |
| Variety | one sky | any other genome's sky is another ~60 bytes: a sky per biome, cell or intensity is nearly free |
| Platforms | CGPROGRAM, target 3.0, URP | same: CGPROGRAM, target 3.0, URP. Needs 32-bit floats for the hashes (`float`, never `half`) |
| Risk | shipped and tested | not compiled in Unity yet; colour match depends on the grade (below) |

**Fair to HyperSea:** most of the SIZE win is the runtime bake, not the genome. `GenomeNebulaSkyBaker` would bake
HyperSea just as well (one heavy draw at load instead of a 6.7 MB PNG), which would remove the PNG on MobileLow and Web
whichever sky is chosen. The genome's own wins are the 4-5x cheaper procedural sky (so a MobileHigh-like tier could
keep an animated sky) and how cheaply new skies can be made.

**The grade.** The lab tonemaps (ACES fit, saturation, contrast) after the sky. With `_Grade.w = 1` the shader does
that itself and returns linear light, which matches the lab in a Linear-space project whose camera does no
tonemapping. If the scene's URP volume tonemaps, set `_Grade.w = 0` or the curve is applied twice.

## 5. What is not known yet

- Unity has not compiled the shader. The include is proven by compiling it as C++ (the same subset), and the C# by the
  reference compile (`unity_refcompile`: 0 errors in the new file; the run's 21 other errors are in files whose
  packages the cloud proxy could not fetch, the same on bleeding-edge). Open it in an editor before anything else.
- Device cost. The CPU ratio is a proxy.
- Whether the team wants the look: it is a crop of the HyperSea look, brighter. That is an art call (Garrett, and
  whoever owns the world look).

## 6. Producing the nebula on a phone, and the poles

Garrett (2026-11-10): the lab's sky warps at the poles, a common spherical artefact; could the nebula alone be made
another way that improves the skybox on mobile? The pole warp is the lab's mapping, not the nebula: it lays 2D noise
over longitude and latitude, so every column meets at the poles. Any method that evaluates the nebula from the 3D view
direction has no pole and no seam. That is SPHERE mode, and every bake below is made from it.

**The bench** (`Tools/Build/genome_nebula_methods.py`, 2026-11-10): each method draws the same nebula in real GLSL on a
real GL pipeline (moderngl/llvmpipe: real bilinear, cubemap and 3D-texture filtering), four 1280x720 views (horizon,
below, both poles), scored against SPHERE supersampled 2x2. Error is the mean and 99th-percentile absolute difference
in 8-bit sRGB, averaged over the views. Bakes are stored as linear 8-bit in the bench (Unity's sRGB targets band less),
so bake errors here are pessimistic. Per-pixel cost is counted, not timed: llvmpipe timings do not rank a phone's
texture unit against its ALU, so they are left out.

| method | mean / p99 err | at the poles | per pixel each frame | memory | made at load | animates |
|---|---|---|---|---|---|---|
| procedural, lab equirect | (other pattern) | **pinch** | 40 2D hashes + atan/asin, fp32 | 0 | - | yes |
| procedural, sphere (reference) | 0.06 / 1 | clean | 80 3D hashes, fp32 (~0.2x HyperSea) | 0 | - | yes |
| bake equirect 2048 | 1.16 / 8.2 | clean (texels squeezed) | 1 fetch | 8 MB | 0.8M px | no |
| bake equirect 4096 (today's HyperSea layout) | 1.02 / 6.0 | clean | 1 fetch | 32 MB raw | 8.4M px | no |
| **bake cubemap 256** | 1.34 / 13.5 | clean | 1 fetch | 1.5 MB | 0.4M px | no |
| **bake cubemap 512** | **0.81 / 5.5** | clean | 1 fetch | 6 MB | 1.6M px | no |
| bake cubemap 1024 | 0.71 / 4.8 | clean | 1 fetch | 24 MB | 6.3M px | no |
| cubemap 512, one face re-baked per frame | as 512 | clean | 1 fetch + 262k procedural px per frame (about a tenth of a 1080x2400 screen) | 6 MB | 1.6M px | yes, refreshed every 6 frames |
| 3D noise RG8 64^3, live warp | 4.35 / 36.8 | clean | 3 fetches + ~40 ALU | 0.5 MB | 0.26M texels | yes |
| **3D noise RGHalf 96^3, live warp** | **1.68 / 16.2** | clean | 3 fetches + ~40 ALU, fp16-safe | 3.4 MB | 0.9M texels | **yes, every frame** |
| 3D noise RGHalf 128^3, live warp | 1.08 / 10.5 | clean | 3 fetches + ~40 ALU, fp16-safe | 8 MB | 2.1M texels | yes |
| vertex-shaded dome, 40k / 164k vertices | 3.99 / 32.8, 1.67 / 19.0 | clean | 0 per pixel; the procedural sky per vertex | 0 | - | yes |
| procedural at 1/2 res, upscaled | 1.42 / 12.2 | clean | a quarter of the procedural pixels + 1 fetch | 0.9 MB | - | yes |
| procedural at 1/4 res, upscaled | 2.08 / 19.0 | clean | a sixteenth + 1 fetch | 0.2 MB | - | yes |

What the pictures show (`/mnt/project-files/amoebius-genome/docs/nebula_methods_sheet.png`, `nebula_pole_crops.png`):
the lab equirect's pinch is gone in every other method; the cubemaps are indistinguishable from the reference; RG8
3D noise TERRACES (the double warp magnifies 8-bit steps into contour lines), which half floats remove; the dome and
1/4-res versions go soft and stair-step along the sharp warp filaments.

**Precision, measured:** the value-noise hash in fp16 differs from fp32 by 0.48 on average (of 0..1), and 89% of hash
values are off by more than 0.1. So the procedural sky needs `highp` on every phone GPU; any method that bakes the
noise (cubemap, 3D noise) does not, which is the safer shape for Mali and Adreno parts that run mediump fast.

**Recommendation per tier:**
- **MobileLow and Web: bake a cubemap at load** (`Method.Cubemap`, 512, or 256 where memory is tight). One fetch per
  pixel, no seam or pole, better than a 4096x2048 panorama at a fifth of its memory, no texture in the build. This is
  also the right shape for HyperSea's own low-tier bake (its stars need more resolution than the nebula; measure).
- **MobileHigh: 3D noise at RGHalf 96^3** (`Method.Noise3D`) for a drift that moves every frame at three fetches a
  pixel, or the time-sliced cubemap where one fetch matters more than smooth motion.
- **Desktop: the procedural sphere.**
- Not worth it: the dome (needs ~164k vertices for the quality of a 2048 panorama) and low-res upscaling (softens the
  filaments that make the look).

Not yet verified in Unity: `Camera.RenderToCubemap` under URP (the cubemap methods use it so Unity owns the face
orientation; if URP refuses it, render the faces with a direction pass instead), and `Graphics.Blit` into 3D slices on
every target API.

## 7. Next steps, cheapest first

1. Open the material in an editor, look at it in a mode scene beside HyperSea.
2. Bake HyperSea at load with `GenomeNebulaSkyBaker` and drop the 6.7 MB PNG (a size win independent of the genome).
3. Point the genome search at the sky ALONE with HyperSea's own render as the target and shader cost in the loss:
   an evolved cheap stand-in for HyperSea with its stars and cell net (needs the proposed `tex.stars` op from the seed
   thread's `OP_PROPOSALS.md`; the existing `tex.cell` Voronoi op, mode 2, can draw the cell net).
4. If a genome sky is kept, add a row to `PlatformProfileSO.skyboxReplacements` for the tier that should get it.
