# Genome texture programs (prototype, 2026-11-10)

Three shipped textures are reproduced by short procedural programs (a field op plus a least-squares LUT, the
`tex.*` op family of the Amoebius genome study). This prototype adds the generator and the measurements; it
**rewires nothing**. Branch: `claude/genome-texture-programs`.

- Runtime generator: `Assets/_Scripts/Utility/ProceduralTexturePrograms.cs` (`CosmicShore.Utility`): decodes a
  program's bytes, evaluates it per texel, bakes a small RGBA32 texture with mips, caches and shares it
  (`GetShared`, `SunsetGradientTexture`, `Grad00Texture`, `Circle01V1Texture`).
- EditMode test: `Assets/_Scripts/Tests/Editor/ProceduralTextureProgramsTests.cs` reads the ORIGINAL files from
  disk (PNG via `ImageConversion.LoadImage`, the TGA with a small reader; the imports are not CPU-readable) and
  compares 64x64 pixel centres against the program.
- Python twin: `python3 Tools/Build/texture_programs.py [--check] [--png DIR]` decodes the same bytes, renders and
  prints PSNR / SSIM against the shipped files. `--check` fails below each floor.

## Program format

`[template][p0][p1][p2][LUT: K knots x C channels][seed]`, all bytes. Template 6 = `tex.gradient` (angle =
2pi p0, pos = -1 + 2 p1, length = 0.25 * 16^p2; field = clamp(((u-.5)cos a + (v-.5)sin a)/length + .5 + pos/2)),
template 7 = `tex.gradient.radial` (cx = .4 + .2 p0, cy = .4 + .2 p1, length = .5 * 4^p2; field =
clamp(2|uv - c|/length)). The LUT is `tex.curve`: K uniform knots on [0,1], linear between. v counts from the image's
top row. Layouts: RGB (3), grey (1), premultiplied grey + alpha (2). The fits came from the compression study
(`unity_mining/compress/fit.py`, CMA-ES on the field parameters + a least-squares LUT).

## Reference map (guid grep over *.mat, *.prefab, *.unity, *.asset, *.shadergraph, *.vfx, *.shader)

| Texture | Import | Referenced by | How it is used |
|---|---|---|---|
| `Assets/_Graphics/Texture/SunsetGradient.png` (guid db0e70e9…, 640 KB, 1024², sRGB, mips, Repeat, not readable) | Default, max 2048, normal compression | 13 Shader Graphs (`Lifeform_World_Shader Graphs/WorldSpaceDesign1-12_Graph`, `CreatureTextureGraph`) as the default texture of a `Texture2DInputMaterialSlot` on a Sample Texture 2D node, and 13 materials (`Assets/_Models/Materials/WorldSpaceDesign1-12_Material.mat`, `CreatureMaterial.mat`) on property `_SampleTexture2D_cac4e2d9…_Texture_1_Texture2D` = 26 references | A colour ramp sampled inside our own Shader Graphs |
| `Assets/_Graphics/Materials/CrystalMaterials/Textures/Grad_00.tga` (guid c260c204…, 1.2 MB, 640² 24-bit TGA, sRGB, mips, nPOT to nearest) | Default, max 2048 | `M_Hi_Icosahedron_Hex_Explode.mat`, `M_Hi_Icosahedron_Hex_03.mat`, `M_Hi_Dodecahedron_00.mat`, all on `_DisplacementMask` of `Shapes_Shader_ST.shader` (repo-owned ASE shader, reads `.r` after a panner) | A 1D grey mask (depends on u only) |
| `Assets/Effects Library/Froglet Stuff/Textures/Circle01_v1.png` (guid d13b9cb0…, 722 KB, 2048², alpha is transparency) | Default, max 2048 | `Circle01_AB.mat`, `Circle01_AB_2.mat` on `_MainTex`, shader = built-in (fileID 211, guid 0000…f000: a Unity built-in particle shader) | A ring sprite for particles |

**Every consuming material has zero users by guid** (no prefab, scene or asset references the 13 WorldSpaceDesign /
Creature materials, the three crystal materials, or the two Circle materials; `M_Hi_Icosahedron_Hex_03` is used only by
`Assets/_Prefabs/Environment/OldCrystalTime.prefab`, which nothing references). No `*.cs` names them. Unless they are
loaded by path (Addressables or `Resources` were not searched beyond the guid grep), **none of the three textures is in
a player build today**, so the build saving below is what a future consumer would avoid, not a present saving.

## Chosen replacement per texture

| Texture | Choice | Why |
|---|---|---|
| SunsetGradient | (a) baked ramp, 16x256 | Linear gradient almost along v (angle 89°, a 1.3% tilt across u), so a narrow tall ramp keeps the tilt; consumer is our Shader Graph, so a texture swap (or a material property set at load) is all it needs |
| Grad_00 | (a) baked ramp, 256x4 | Pure function of u (angle 2pi), grey; `Shapes_Shader_ST` reads `.r`, so 256x4 is exact for it. An HLSL analytic version (b) is possible since we own the shader, but the shader is ASE-generated, so the texture swap is cheaper to maintain |
| Circle01_v1 | (c) leave as is; the 256² bake is available | Consumer is a Unity built-in particle shader, so no HLSL include; the program is only 29 dB / SSIM 0.977 (the ring's soft edge is not fully captured by a 64-knot radial LUT), good enough for a blurred particle but not exact |

## Measured quality (`texture_programs.py`, reproduces the study's numbers)

| Texture | Shipped bytes | Program | fit256 PSNR (SSIM) | grid64 PSNR (the Unity test) | Baked size, upsampled to full | Baked PSNR |
|---|---|---|---|---|---|---|
| SunsetGradient | 640,486 | 53 B | 57.77 dB (0.999) | 55.52 dB (floor 54) | 16x256 | 55.46 dB |
| Grad_00 | 1,228,818 | 21 B | 41.35 dB (0.985) | 41.14 dB (floor 40) | 256x4 | 41.14 dB |
| Circle01_v1 | 722,223 | 133 B | 28.97 dB (0.977) | 27.87 dB (floor 26.5) | 256x256 | 27.61 dB |

fit256 = target Lanczos-resized to 256², the study's metric; grid64 = 64x64 nearest pixel centres at full size;
baked = program baked at the proposed size, bilinear-upsampled to the shipped size, PSNR over every pixel.

## Bytes

Source: 2.59 MB of image files would become ~207 bytes of program (the files stay until a consumer is switched).

Build (estimated from the import settings, full mip chain; not measured in a build):

| Texture | Desktop now (DXT) | Mobile now (ASTC 6x6, Unity default) | Baked instead (RGBA32 + mips, runtime) |
|---|---|---|---|
| SunsetGradient 1024² | DXT1 ≈ 683 KB | ≈ 608 KB | 16x256 ≈ 21 KB |
| Grad_00 640² → 512² (nPOT to nearest) | DXT1 ≈ 171 KB | ≈ 152 KB | 256x4 ≈ 5.5 KB |
| Circle01_v1 2048² (alpha) | DXT5 ≈ 5.3 MB | ≈ 2.4 MB | 256² ≈ 341 KB |

VRAM moves by the same amounts, since a baked texture lives only in memory (no disk copy) and an RGBA32 ramp of this
size is smaller than the compressed full-size image. Bake cost: one pass over the texels at first use (16x256 and
256x4 are under a millisecond; 256² is ~65k evaluations).

## How to switch a consumer over (not done here)

1. Keep the material; at load set its texture from the generator:
   `material.SetTexture("_SampleTexture2D_cac4e2d9e6704863afefef7a6df1dd47_Texture_1_Texture2D", ProceduralTexturePrograms.SunsetGradientTexture)`
   (on `sharedMaterial` once, or through a MaterialPropertyBlock per renderer). For Grad_00: `_DisplacementMask`.
2. Or commit a baked PNG (`texture_programs.py --png DIR`, then resize to the proposed size) with the same import
   settings (sRGB, Repeat, mips) and swap the guid in the material; that keeps everything static and needs no code.
3. Delete the old image only after `who_works_where.py query` on the consumer and a look in Unity.
4. Run `texture_programs.py --check` and the EditMode tests after any change to a program.

## What is unverified

- The EditMode tests have **not run in Unity**; the C# passed the reference compile only.
- The 26 / 3 / 2 references were not rewired, and nothing was looked at in the editor.
- Whether any consumer ships (Addressables / `Resources` / by-path loads were not traced); the guid grep says none.
- The build-size column is computed from formats and sizes, not read from a build report.
- Shader Graph sampling with a 16-wide ramp assumes the graphs sample with mesh or world UVs that span the ramp the
  same way; a graph that relies on the 1024 width (e.g. point filtering or a fixed texel offset) was not checked.
