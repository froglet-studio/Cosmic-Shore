# Watery glass: the Meerkat's shader family, as a recipe

Yash, 2026-10-28: *"The watery thing, you can keep this shaders we are creating on the flow. You can dock them for future
uses so that it become easy for us to create shaders like this."*

The Meerkat's wormhole, its tree and its roots grew a small family of shaders that all read as **water and glass**:
see-through face-on, bright where the surface turns away from you, slowly flowing, and bending what is behind them. This
page lists each one, says what makes it read as water, and gives the steps to make a new one. Every shader here was
designed in the Vessel Studio artifact first (`Docs/Studios/VesselStudio/meerkat.html`, GLSL in three.js) and ported line
for line to URP HLSL.

## 1. The family

| Shader | What it draws | The look, in one line | Blend |
|---|---|---|---|
| `MeerkatWormholeLens.shader` (+ `MeerkatWormholeLensPass.cs`) | **The water pipe** (Garrett's Tube Lens Lab, Yash's round-11 numbers): every tunnel as bent space only | Each pixel's ray is marched through a thin shell on the tube's wall and turned by a field along the tube's contours; the pixel shows where the turned ray lands. The tube is measured along its centreline: 25 points uploaded per tube, joined by circle arcs from each point's heading to the next (straight chords cut inside a curve: 1.4 u out against 0.07 u) | Full-screen pass, after transparents |
| `MeerkatWormholeWarp.shader` | The tube's wall as a screen-space offset, for the same lens pass | `normal.xy * sin(pi * rim)`: zero at the centre line AND at the silhouette, so there is no seam | Additive, into a half-float target |
| `MeerkatWormholeGlass.shader` | The bridge's glass (with `formedLook` Bridge) | Fresnel only: `0.05 + 0.55 * rim^3` | Additive |
| `MeerkatTree.shader` | **The tree** round every tunnel, and the **roots'** bark | Fresnel glass + bark fissures (fading within 15-90 u of the camera) + tree rings on the cut faces + a slow flow; the bore (the tube you ride) a smooth sheen with no lines; shows only the tunnel's live window; branches grow out of their roots in the vertex shader | Additive |
| `MeerkatRootInside.shader` | The roots **from inside** | A soft, smooth inner tube: one slow warm tone, a little dense, denser at its silhouette, lit by the rider's lamp, fading to CLEAR with distance (never to black); its ends stay open | Alpha |
| `MeerkatWaterTube.shader` (+ `MeerkatWaterTubeView.cs`) | **THE wormhole tube, the default look** (2026-10-29): every formed tunnel (`Resources/MeerkatWaterTube.mat`) and every omni-crystal network channel (`Resources/MeerkatWaterTubeNetwork.mat`, keyword `MEERKAT_TUBE_NETWORK`) | An aquarium pane: URP's opaque copy (`_CameraOpaqueTexture`, held on by the view only while a tube draws) read at a screen position pushed along the wall's view-space normal (stronger at the outline) plus a per-vertex two-sine ripple, under a faint aqua tint; Fresnel rim `abs(n.v)`, flow bands, caustic shimmer; fades near the camera and outside the live window. No domain colour (tunnels are universal). The network variant adds an iridescent cosine-palette rim, energy streaks in lanes and an HDR glow. 1 texture fetch + ~45 ALU a pixel (network ~60). The old lens + wall are A/B buttons in `LavalampWormholeLab.unity`; `Tools/Shaders/verify_meerkat_water_tube.py` compiles both variants | Premultiplied (`One OneMinusSrcAlpha`), ZWrite Off, `Cull [_Cull]` (default Off) |

`rim` is always `1 - abs(dot(normal, viewDir))`: 0 where you look straight at the surface, 1 at its outline. `abs` makes
it the same from inside and outside, so one material serves both.

## 2. What makes it read as water

Yash's rule for a ride (2026-10-28): **rough and edgy outside, very soft inside; calming, inviting; never a black fill.**
Detail (fissures, ribs, warp) fades as the camera comes close (`smoothstep(15, 90, distance)`), and the surface you ride
inside is one smooth sheen.

1. **See-through face-on, bright at the outline (Fresnel).** `a = glass + rim_strength * pow(rim, power)`. Keep `glass`
   tiny (0.01-0.05) and let the rim carry the shape. A higher power gives a thinner, sharper outline.
2. **Additive, no depth write.** `Blend One One`, `ZWrite Off`, `Cull Off`. The world behind always shows through, the
   surface only adds light, and draw order never matters. Colour is pre-multiplied by brightness in the shader.
3. **Flow.** Multiply by `0.75 + 0.25 * sin((along + other) * 23 - t * 2.4)`: a soft band of light running from the way in
   to the way out. Water runs; glass is still.
4. **Bent space, gently.** A real refraction is the lens pass (§1). For "a pleasant water ride ... I should not feel too
   much of warping" (Yash, round 11), keep the bend small and even along the tube: strength about 6, flow Even, grazing
   0.9 (a ray skimming the wall from inside bends little, so the ride does not swirl), soft ribs.
5. **A live window.** A tube that forms and collapses shows only `[f0, f1]` of its length (a share of the way along),
   condensing at the window's fronts with `smoothstep(0, edge, min(s - f0, f1 - s))`. Use -1 / 2 for a finished end, so a
   formed end is a hard cut and not a fading front.

## 3. Making a new one (the steps)

1. **Design it in the artifact first.** Write it as a three.js `ShaderMaterial` on the vessel's studio page, with every
   number a studio key. Play it there and get it decided.
2. **Pick the base.** Start from `MeerkatTree.shader` for a surface (glass, grain, rings, flow, window), from
   `MeerkatRootInside.shader` for a solid, lit wall, or from the lens pass for bent space.
3. **Port the GLSL to HLSL line for line.** The traps that cost time:
   - `line` is a **reserved word in HLSL** (a geometry-shader primitive type). The page's `float line = ...` failed to
     compile until it was renamed.
   - `fract` → `frac`, `mix` → `lerp`, `vec3` → `float3`, `mod` → `fmod` (they differ for negative numbers).
   - `cameraPosition` → `_WorldSpaceCameraPos`; three.js `modelMatrix` → `TransformObjectToWorld`.
   - `gl_FrontFacing`: prefer a face-independent form, such as `abs(dot(n, v))`, or flip the normal toward the camera.
     A tube's triangles face outward, so its inside is the BACK face.
4. **Pack the vertex data into UVs.** Three.js lets a mesh carry any named attribute. In Unity, pack them into `uv0`-`uv3`
   as `Vector4`s, and say which is which in the shader's header comment. `MeerkatTreeMesh` packs aS, aBi, aCap, aR into
   uv0; aB and aPhi into uv1; aBase into uv2.
5. **Per-object numbers go in a `MaterialPropertyBlock`.** Use one block per draw call within a frame: a queued
   `Graphics.RenderMesh` can read its block after the call returns (`MeerkatTreeView.Next()`). A per-object array, such
   as each branch's growth, is a global `float _Bg[16]` outside the `UnityPerMaterial` buffer, set with `SetFloatArray`.
6. **Ship the material in `Resources/`** and load it by name. Report a missing one with a single warning that names
   the file (`MeerkatTreeView.Resolve`).
7. **Compile it before you commit.** `glslangValidator -D` (the HLSL front end) against a small URP mock compiles both
   stages in seconds. `Tools/Shaders/verify_black_hole_lens.py` holds the shared mock and the `dxc` path for the real URP
   headers. Make sure both the vertex and the fragment entry compile.
8. **Map every number** through `Tools/Build/studio_to_unity/<vessel>.json`, so `studio_to_unity.py --check` holds the game
   to the artifact.

## 4. Where they are used

- The Meerkat: `Assets/_Scripts/Controller/Vessel/R_VesselActions/MEERKAT.md` §2 (the water pipe) and §7 (the tree and
  the roots).
- The A/B lab for the original lens + wall against the cheap water pipe, with a frame-time readout:
  `Assets/_Scenes/Game_TestDesign/LavalampWormholeLab.unity` (Play; IMGUI buttons).
- The lab where the water pipe was tuned: `Docs/Studios/StoatTubeLensLab.html`
  (https://claude.ai/artifact/PEScFn528pACfFq2KVNJRF).
- The studio page that holds the tree and the roots' GLSL: `Docs/Studios/VesselStudio/meerkat.html` (`treeMaterials`,
  `rootInMat`, `soilMat`; the soil is not ported yet).
