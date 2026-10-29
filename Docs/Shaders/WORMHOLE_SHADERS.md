# Wormhole shaders: every look, step by step, so any of them can be rebuilt

Yash, 2026-10-29: *"docs every procedure to all shaders, so that we can recreate when needed ... then we make one shader,
the good PC running a nice subtle wormhole and its networked holes."*

This page records every shader that has drawn a Meerkat wormhole tunnel, or a channel of the omnicrystal's wormhole
network. For each one it gives:
- what it looks like
- how it draws, pass by pass and in maths
- every number and its shipped value
- the C# that drives it
- the commit it came from
- how to bring it back

It ends with the other wormhole and lens shaders in the project, which share the same plumbing. The general recipe for
the "watery glass" family is in `WATERY_GLASS.md`.

**What ships today (`feat/wormhole-pc-unified`, PR #1624, from 2026-10-29):** ONE look, nothing to switch. It is
§1's contour-field lens (the water pipe, with the numbers Yash chose in the Tube Lens Lab artifact) with §11's outer
layer always on, and the networked-hole upgrade on every omnicrystal channel. Its layer numbers are GARRETT'S preset,
baked in as `MeerkatWormholeHQ.Shipped`. A formed tunnel draws no tube geometry; only the reticle (your aiming
wireframe, §2's additive spindles) is drawn as a mesh.

Yash, 2026-10-29: *"just keep one high quality look, the one there is now, then document other shaders, so we can
recreate later if needed, delete the shaders."* **Every other wormhole/tunnel shader was deleted** with its material,
its C# view and its lab button. §2-§7 still describe them in full, and **§12 says exactly how to bring each one back
from git**. §13 is how to make the inside of a tunnel feel more underwater.

Paths below are relative to the repository root. The two folders used most often are shortened:
- `Graphs/` means `Assets/_Graphics/Materials/Graphs/`.
- `RVA/` means `Assets/_Scripts/Controller/Vessel/R_VesselActions/`.

---

## 0. The map

| # | Look | Shader | Driver (C#) | Material (`Assets/Resources/`) | Status |
|---|---|---|---|---|---|
| 1 + 11 | **THE look**: the contour-field lens, its outer layer, the networked holes | `Graphs/MeerkatWormholeLens.shader` (no keywords) | `RVA/MeerkatWormholeLensPass.cs`, `RVA/MeerkatWormholeHQ.cs` (`Shipped`) | `MeerkatWormholeLens.mat` | **Ships**, always on |
| 2 (reticle) | The reticle: your aiming spindles | `Graphs/MeerkatWormholeAdditive.shader` | `RVA/MeerkatWormholeView.cs` (`DrawReticle`, `BridgeFrame`, `Spindle`) | `MeerkatWormholeAdditive.mat` | **Ships** |
| 2 | YASH'S LOOK: the spindle bridge on formed tunnels, and its glass skin | `MeerkatWormholeAdditive.shader`, `MeerkatWormholeGlass.shader` | `MeerkatWormholeView.DrawBridge` | `MeerkatWormholeGlass.mat` | **Removed** (§12) |
| 3 | The warp tube, the mouth dents, the collapse ripple | `MeerkatWormholeWarp.shader` + the lens's wells/ripple code | `MeerkatWormholeLensPass` warp pass, `MeerkatWormholeView.AddWarpTube` | `MeerkatWormholeWarp.mat` | **Removed** (§12) |
| 4 | The calm inner wall (the "blue tube") | `MeerkatTubeWall.shader` | `MeerkatTubeWallView.cs` | `MeerkatTubeWall.mat` | **Removed** (§12) |
| 5a | The cheap mobile water pipe | `MeerkatWaterTube.shader` | `MeerkatWaterTubeView.cs` | `MeerkatWaterTube.mat` | **Removed** (§12) |
| 5b | The watery-glass "aquarium" tube (PR #1620) | `MeerkatWaterTube.shader` at cdc1837a6 | `MeerkatWaterTubeView.cs` at cdc1837a6 | `MeerkatWaterTube.mat`, `MeerkatWaterTubeNetwork.mat` | Reverted earlier (c2d3d6be1); §5b |
| 6 | The tree (the outer layer, rounds 13-15) and the roots' bark | `MeerkatTree.shader` | `MeerkatTreeView.cs`, `MeerkatTreeMesh.cs` | `MeerkatTree.mat` | **Removed** (§12) |
| 7 | The roots from inside | `MeerkatRootInside.shader` | `MeerkatTreeView.cs` | `MeerkatRootInside.mat` | **Removed** (§12) |
| 8 | The Butterfly fold and crystal wormhole mouths (a different wormhole) | `Graphs/Wormhole.shader` | `Assets/_Scripts/Controller/Environment/Wormhole/WormholeView.cs`, `WormholeMouth.cs` | `Assets/_Graphics/Materials/Wormhole.mat`, `WormholeSeamless.mat` | Ships, untouched |
| 9 | Sibling lenses: black hole, vortex ring | `Graphs/BlackHoleLens.shader` and `.hlsl`, `Graphs/VortexRingLens.shader` and `.hlsl` | `BlackHoleLensPass.cs`, `VortexRingLensPass.cs` | `BlackHoleLens.mat`, `VortexRingLens.mat` | Ship, untouched |

**There are no switches any more.** The `formedLook` field, the YASH'S LOOK / GARRETT'S LOOK Inspector buttons
(`MeerkatWormholeConfigSOEditor.cs`), the `MeerkatTubeLook` test switch, its `_MW_HQ` keyword and the lab's look buttons
were removed with the looks they chose between. Before removal the switches were:

| `MeerkatTubeLook.Mode` (removed) | Lens (§1) | Calm wall (§4) | Water tube (§5a) |
|---|---|---|---|
| Original (0) | yes | yes (if `tubeInner` > 0) | no |
| WaterOnly (1) | no | no | yes |
| LensOnly (2) | yes | no | no |
| WallOnly (3) | no | yes | no |
| None (4) | no | no | no |
| WaterAndLens (5) | yes | no | yes |
| HighQuality (6, the branch default) | yes, with `_MW_HQ` (§11) | no | no |

The bridge (§2) drew whenever `formedLook` was Bridge. Sections §1.4-§7 below describe the code **as it was before
the removal** (commit 435ce058c); where they name a field, switch or pass that is gone, §12 says how to bring it back.

---

## 1. The water pipe: the contour-field lens (GARRETT'S LOOK, ships)

### 1.1 What it looks like

A formed tunnel has no surface. Where its wall would be, the world behind it is bent through a thin invisible shell.
- From outside, the tunnel reads as a clear glass pipe. Its outline is traced by the world sliding sideways, and faint
  ripples (the ribs) travel toward the way out.
- From inside, the ride looks out through gently bent water.
- Nothing is tinted and nothing is added. Every pixel shows a pixel of the real scene, taken from another place.

Yash, round 11: *"a pleasant water ride experience through a water pipe. Transparent pipe ... I should not feel too much
of warping ... performant"*. Garrett, from the Tube Lens Lab: *"lets the lensing alone sell the effect."*

### 1.2 History

| Commit | What |
|---|---|
| d2380c883 | Garrett's **Tube Lens Lab** (`Docs/Studios/StoatTubeLensLab.html`, https://claude.ai/artifact/PEScFn528pACfFq2KVNJRF). The GLSL `tube`, `prof`, `ribF`, `fieldDir`, `trace` is the source the shader ports. |
| a959070e2, 587a7eb39 | Garrett's before/after: the contour field ported into `MeerkatWormholeLens.shader`, behind `formedLook` ContourField |
| 746797834 | Round 8: Yash's tuned look ships |
| **53b34e076** | **Round 11, the clear water pipe, the numbers Yash chose** (rendered against round 10 in the lab first) |
| 6a78dfa7a | Round 22: the tube bends round its middle (one oval arc); the shader measures it from 25 centreline points joined by circle arcs, within 0.07 u |
| b61e575a2, 466eb497f | Rounds 23-26: the network's channels join the same bank as 3D polylines (`_MWTubeShape.y` 1) |
| 767f849e2 | Yash in Unity: `fieldGlass` 1 → **0.139**, `tubeInner` 0.1 → 0 (calm wall off), `bridgeGlass` 0.15 → 0 |
| 7dc78c4bc | Yash in Unity: throat 7.1, mouth 35.6 |
| c2d3d6be1 | 2026-10-29: the watery-glass replacement (#1620) reverted. This look is the default again. |
| f7780c44f | The code defaults of the 16 `field*` numbers set to what the asset ships (they were Garrett's pick) |

### 1.3 The numbers (Yash's choice, as shipped)

From `MeerkatWormholeConfig.asset`, which now equals the code defaults. The `fieldGlass` value is the artifact's
round-11 number lowered by Yash in Unity. The last three columns compare it with Garrett's lab pick, Yash's round-8 tune
and round 10.

| Field | Shipped | Meaning | Garrett's pick | Yash round 8 | Round 10 |
|---|---|---|---|---|---|
| `formedLook` | ContourField (1) | A formed tunnel is only bent space | ContourField | Bridge | — |
| `fieldStrength` | **6** | The bend of a head-on wall crossing, in degrees (×Deg2Rad in C#) | 18 | 18 | 18 |
| `fieldPitch` | **0** | 0° = the field runs along the tube; 90° = round its rings | 90 | 90 | 90 |
| `fieldDrag` | **0.5** | Pulls rays along the field's lines | 1 | 1 | 1 |
| `fieldTwist` | **0** | Turns rays round the lines | 0 | 0 | 0 |
| `fieldGlass` | **0.139** (artifact: 1) | Refraction through the shell's density gradient | 0.4 | 1.579 | 1.579 |
| `fieldGlassIndex` | **0.05** | Δn of that refraction | 0.04 | 0.078 | 0.078 |
| `fieldGrazing` | **0.9** | 1 = a ray skimming the wall bends as little as it crosses it (no swirl) | 0.6 | 0.883 | 0.883 |
| `fieldFlow` | **Even (0)** | Even / InThenOut (cos πu) / Throat (sin πu) along the way | InThenOut | InThenOut | InThenOut |
| `fieldInner` | **0.1** | Shell thickness inside the wall, × r | 0.1 | 0.1 | 0.1 |
| `fieldOuter` | **0.15** | Shell thickness outside the wall, × r | 0.12 | 0.12 | 0.12 |
| `fieldProfile` | **Smoother (1)** | The shell's grade: Smooth (smoothstep), Smoother (smootherstep), Linear, Bell | Smooth | Smoother | Smoother |
| `fieldRibs` | **0.25** | How much the field gathers on the rib grid | 0.9 | 0.155 | 0.155 |
| `fieldRibSharpness` | **3** | The grid lines' power | 8 | 11.12 | 11.12 |
| `fieldRibTravel` | **1.2** | Rings per second toward the way out | 1.5 | 1.95 | 1.95 |
| `fieldRibRings` | **16** | Rings along the live window | 22 | 9 | 9 |
| `fieldRibLines` | **12** | Lines round the tube | 12 | 12 | 12 |
| `throatDiameter` | 7.1 | The tube's waist | | | |
| `mouthDiameter` | 35.6 | Its ends | | | |

Sources:
- `RVA/MEERKAT.md` §2.1 ("Numbers, round 11").
- `Docs/Studios/MEERKAT.md` (round 11, round 8).
- Commits 53b34e076 and 767f849e2.

The Round 10 column is round 8's numbers with Garrett's strength. Cells left blank were not recorded.

### 1.4 The procedure (how it draws)

**Plumbing (`MeerkatWormholeLensPass.cs`, URP RenderGraph):**
1. On `RenderPipelineManager.beginCameraRendering` it enqueues itself on every base game camera (and the Scene view)
   while `MeerkatTubeLook.LensOn` and something is live: a tunnel, a ripple or a network. Event:
   `AfterRenderingTransparents`, with `requiresIntermediateTexture`. A scene with nothing live pays nothing.
2. While live, it holds the camera depth texture on through `BlackHoleLens.CameraSupport`.
3. **Pass "Meerkat tube warp".** It clears a half-resolution RGBA16F target, `_MeerkatWarpTex`, and draws
   `MeerkatWormholeView.WarpMesh` into it (§3). The mesh is empty while `warpStrength` is 0.
4. **Pass "Meerkat scene copy".** A Blitter copy of the camera colour into `_MeerkatSceneColor`, single sample, bilinear,
   clamped.
5. **Pass "Meerkat wormhole lens".** One `DrawProcedural` full-screen triangle with `MeerkatWormholeLens.shader`
   (`ZTest Always`, `ZWrite Off`, `Blend Off`), writing to the camera colour. The uniforms come from a
   MaterialPropertyBlock:
   - `_MWWellC[8]`, `_MWWellP[8]`: the mouth dents (§3)
   - `_MWRipple`: the collapse ripple (§3)
   - `_MWView`: aspect, warp on, wells, tubes
   - the tube bank, built by `ContourTubesFor` from the **4 tubes nearest the camera**, tunnels and channels alike:
     - `_MWTubeW2L[4]`: world to the tube's own frame. Way in at the origin, first heading +z, bend toward +x.
     - `_MWTubeShape[4]`: L, kind (0 a tunnel, 1 a network channel), θ, throat radius.
     - `_MWTubeWin[4]`: live window s0, s1, fade, mouth radius.
     - `_MWTubeBound[4]`: a bounding sphere (centre, radius).
     - `_MWTubePath[4×25]`: the centreline. For a tunnel, each point is (across, ahead, s, turned); for a channel,
       (x, y, z, radius).
     - `_MWTubeNorm[4×25]`: a channel's carried normal.
   - `_MWField0..3`: the field numbers. They come from the **nearest** tube's config.
     - `F0` = (strength°→rad, pitch°→rad, drag, twist)
     - `F1` = (glass, glassIndex, grazing, flow)
     - `F2` = (inner, outer, profile, ribs)
     - `F3` = (sharpness, `Time.time × ribTravel`, max(2, rings), max(1, lines))
   - `_MWCamPos` (xyz eye, w tan(fov/2)), `_MWCamR`, `_MWCamU`, `_MWCamF`: the camera basis, so the shader builds each
     pixel's world ray itself.

**The fragment (`MeerkatLensFrag`), per pixel.** Here `p` is the screen point in screen heights from the centre, y up.
1. `sceneZ` = the linear eye depth.
2. `disp` = 0.
3. Add the mouth dents and the warp texture (both 0 when switched off, §3).
4. **`disp += MWContour(p, sceneZ)`, the contour field:**
   1. Build the ray: `rd = normalize(F + R·p.x·2tan + U·p.y·2tan)`.
   2. Intersect every tube's bounding sphere. Start at the nearest entry `tStart`. If the ray hits none, return 0.
   3. **March, at most 256 steps.** At each step, for each of the up to 4 tubes:
      1. Query the wall with `MWTube` (or `MWChannel` for a network channel), in the tube's own frame:
         - Find the nearest point on the 25-point centreline. Past either end, s continues along the end's heading.
         - Clamp s to the live window, and reconstruct the centre on the **circle arc** between the two points, plus its
           heading T, its normal N and its curvature `invR`.
         - Radius: a hyperboloid over the live window, `r = sqrt(b² + (M² − b²)·w²)` with `w = 2(s − s0)/span − 1`. Both
           b (throat) and M (mouth) shrink while the window is short. A channel uses its stored per-point radius
           instead.
         - Signed distance `d = ρ − r`, measured to the lip past a live mouth.
         - Also `u` (the share of the way along the window), the angle `φ` round the tube, `drds`, and
           `scale = 1 − r·cos·invR` (the inside of a bend is shorter).
      2. **Shell thickness:** `th = (d > 0 ? outer : inner) · r`. If `|d| < th`, the ray is inside the shell:
         1. **Step:** `across = max(|dir·n|, 0.12)`, `step = clamp(th/(7·across), 0.02, 1.2·th)`. With ribs on, the step
            is also capped at `span/(rings−1)/(2·sharpness+4)`, so a rib is never skipped.
         2. **Weight:** `w = prof(1 − |d|/th) · rib · flow(u) · fade`.
            - `prof`: smoothstep, smootherstep, linear, or a bell `(e^{−9(1−t)²} − e^{−9})/(1 − e^{−9})`.
            - `rib = lerp(1, max(lineP(u·(rings−1) − phase), lineP(φ/2π·lines)), ribs)`, where
              `lineP(x) = (0.5 + 0.5·cos 2πx)^sharpness`.
            - `flow` = 1, cos πu or sin πu.
         3. **Field direction on the wall:**
            - `ts = normalize(T·scale + drds·n)`, the long grid line tilted by the flare and the bend.
            - `ph = normalize(ts × n)`, the ring direction.
            - `F = cos(pitch)·ts + sin(pitch)·ph`.
         4. **Coupling:** `k = strength / (0.5·(inner + outer)·r) · lerp(1, across, grazing)`.
         5. **Turn:** `dd += (drag·(F − (F·dir)dir) + twist·(dir × F)) · k · w`.
         6. **Glass:** with `glass·Δn > 0`, take the gradient of the weight field `MWN` by a 4-tap tetrahedral finite
            difference (step `max(0.15·th, 0.02)`). Add `glass·Δn·lerp(1, across, grazing)·(g − (g·dir)dir)`: refraction
            through the shell's density.
      3. Outside the shell, the step is `max(0.85·(|d| − th), hmin)`: a sphere-trace toward the shell.
   4. Inside any shell, `dir = normalize(dir + dd·step)`. Remember the first shell entry depth.
   5. Stop when the ray is outside every bounding sphere and heading away from each.
   6. If no shell was met, or the opaque scene is in front of the first shell (`sceneZ < entry − 0.5`), return 0.
      **A vessel in front of the tube is never bent.**
   7. Project the final `dir` back to the screen. `landed − p` is the offset, clamped to a length of 2.
5. Add the ripple (§3).
6. If `disp ≈ 0`, **discard**: the pixel keeps the camera colour, so unbent pixels cost nothing more.
7. Sample `_MeerkatSceneColor` at `p + disp`. A ray bent off-screen is mirrored back
   (`suv = 1 − |1 − |suv||`).

**Cost.** Up to 256 steps × 4 tubes × a 24-segment centreline search per covered pixel, at full resolution, plus 4 more
field taps per in-shell step for glass. While you ride inside a tunnel, every pixel is covered. On a good PC this is fine;
on mobile it is not, which is why §5 was tried.

### 1.5 How to recreate it

1. Check out any commit at or after 53b34e076. The shader and the pass are unchanged in meaning since round 26
   (466eb497f).
2. In the config: `formedLook` ContourField, the §1.3 numbers, `tubeInner` 0, `trunkVisibility` 0, `rootBarkVisibility`
   0, warp/dent/ripple 0. `MeerkatTubeLook.Mode` Original (the default) or LensOnly.
3. Make sure `Assets/Resources/MeerkatWormholeLens.mat` (shader guid `33370c7c15ca4b54b2d25421e8cd65f9`) exists.
   `Resolve()` warns once if it is missing. (Before 2026-10-29 the pass also needed `MeerkatWormholeWarp.mat`, §12.)
4. To port it elsewhere, copy the lab's GLSL from `Docs/Studios/StoatTubeLensLab.html`. The HLSL is a line-for-line
   port; the traps are listed in `WATERY_GLASS.md` §3.

---

## 2. YASH'S LOOK: the spindle bridge and its glass (removed 2026-10-29, the reticle stays; §12)

### 2.1 What it looks like

A formed tunnel draws as a lattice of glowing six-sided spindles:
- They run along `bridgeRings` rings × `bridgeSpindlesPerRing` long lines, plus `bridgeRibs` ribs, in the pilot's domain
  colour, whitened toward `bridgeWhite`.
- A shimmer runs from the way in to the way out.
- Spindles fade within 4-30 u of the camera.
- The whole lattice condenses out of the centre as the tunnel forms.
- An optional glass skin is lit at its silhouette (Fresnel only).

Since round 9, **your reticle is always this wireframe** (`DrawReticle` → `BridgeFrame`), in both looks.

### 2.2 The numbers (shipped asset)

| Field | Value | Meaning |
|---|---|---|
| `bridgeVisibility` | 0.163 | Brightness (0 hides it) |
| `bridgeGlass` | 0 (was 0.15; 0.6 code default) | The glass skin's brightness |
| `bridgeRings` | 36 | Rings along |
| `bridgeSpindlesPerRing` | 21 | Spindles round each ring |
| `bridgeRibs` | 5 | Long ribs |
| `bridgeSpindleThickness` | 0.25 | Spindle width (u) |
| `bridgeShimmer` | 0.5 | Shimmer running in → out |
| `bridgeWhite` | 0.45 | Whitening of the domain colour |
| `reticleWhite`, `reticleIdleAlpha`, `reticleNearLight` | 0.45, 0.053, 0.25 | The reticle's version |

### 2.3 The procedure

1. `MeerkatWormholeView.DrawOncePerFrame()` is called from every Meerkat's LateUpdate and runs once a frame. For each
   live tunnel with `formedLook` Bridge (and each network channel, round 26), it calls `DrawBridge` → `BridgeFrame`.
2. `BridgeFrame` walks the tube (`Tube.Conic` for a tunnel, `Tube.Channel` for a channel). For each ring × spindle it
   appends a **six-sided bipyramid** (`Spindle`: 2 tips plus 6 side vertices, facing the camera) to one shared triangle
   list.
   - Vertex colour = domain colour × brightness × `MeerkatWormholeMath.BridgeGlow(u, shimmer, t)` × the near fade.
   - The brightness is pre-multiplied.
3. The glass skin, if `bridgeGlass` > 0, is appended to a second mesh: positions, normals, colour, with rgb the in → out
   shade and a = brightness × glass.
4. Both meshes are uploaded once a frame and drawn with `Graphics.RenderMesh` at identity:
   - **`MeerkatWormholeAdditive.shader`**: `Blend One One`, `ZWrite Off`, `ZTest LEqual`, `Cull Off`. The fragment returns
     the vertex colour.
   - **`MeerkatWormholeGlass.shader`**: the same states. The fragment returns `rgb·a·(0.05 + 0.55·rim³)`, with
     `rim = 1 − |n·v|`.
5. Commits:
   - round 3: the bridge
   - b8d6ef57a: the first Unity port of the vessel
   - f8248cb80: round 9, the reticle as the wireframe

### 2.4 How to recreate it

Press **YASH'S LOOK** on the config. The lens pass then skips formed tunnels (`ContourFieldOn` is false), and the
spindles draw.

---

## 3. The warp tube, the mouth dents, the collapse ripple (removed 2026-10-29; §12)

All three are drawn by the same lens pass and shader as §1. They ship at 0, so the pass never draws them.

- **The warp tube** (`warpStrength`, 0; round 4 set it to 0):
  - `MeerkatWormholeView.AddWarpTube` builds every live tube's wall as a mesh, `WarpMesh`:
    - positions and normals
    - uv.x = `cos(π·u)·fade` (bend in at the way in, out at the way out)
    - uv.y = `0.05·warpStrength`
  - `MeerkatWormholeWarp.shader` draws it additively into the half-resolution RG16F target:
    `n_view.xy · sin(π·rim) · uv.x · uv.y`.
  - `sin(π·rim)` is zero at the centre line **and** at the silhouette, so there is no seam.
  - The lens adds the target's value to `disp`.
- **The mouth dents** (`dentStrength`, 0):
  - `ScreenWells` puts each mouth on screen: c = (screen x, y, depth, 1), p = (3 in / 4 out, angular radius,
    strength × fade × nearness, foreground margin).
  - The fragment adds `±dir · strength · rc · u · e^{−u²/2}` with `u = θ/rc`: a Gaussian-derivative dent as wide as the
    mouth.
  - It skips a pixel whose scene depth is in front of the mouth.
  - Nearness = `smooth(0.6·rM, DentNear·rM, distance)`.
- **The collapse ripple** (`rippleStrength`, 0; `rippleEdge` 0.154; `rippleSeconds` 1.2):
  - A ring at `r = |p|/half-diagonal` moving from `rippleEdge` outward.
  - `disp += p̂ · A · smoothstep(edge, edge + 0.25, r) · e^{−x²} · sin(πx)`, with `x = (r − front)/0.07`.
  - It only touches the screen's edges (Yash: no distortion in the player's eyesight).

To recreate any of them, set its strength above 0.

---

## 4. The calm inner wall (the "blue tube", removed 2026-10-29; §12)

Added in round 26 (b61e575a2). The Vessel Studio's round-21 bore, drawn on its own once the tree shipped off.

- **Look:** a smooth additive sheen on every tube's wall, with no lines. It runs from the maker's **domain colour** at the
  way in (default cyan `(0.3, 0.85, 1)`) to white at the way out, flowing slowly. This is what made tunnels read blue
  (Yash, 2026-10-28). He switched it off in 767f849e2 (`tubeInner` 0).
- **Procedure:**
  1. `MeerkatTubeWallView.Draw` builds `MeerkatTreeMesh.BuildTubeWall` per tube (uv0.x = aS, the share along).
  2. It draws each with `Graphics.RenderMesh` and its own MaterialPropertyBlock (`_CIn`, `_COut`, `_Alpha` = fade,
     `_Inner` = `tubeInner`, `_F0`/`_F1` = the window, `_T`).
  3. The shader uses `Blend One One`, `ZWrite Off`, `Cull Off`, and computes:
     - discard outside `[_F0, _F1]`
     - `w = smoothstep(0, _Edge, min(s − F0, F1 − s))`
     - `flow = 0.75 + 0.25·sin(23s − 2.4t)`
     - `fr = (1 − |n·v|)^_Power`
     - `a = _Inner·(0.55 + 0.45·fr^1.4)`
     - `rgb = lerp(CIn, COut, s)·a·Alpha·w·flow`
- **Defaults:** `_Inner` 0.1, `_Power` 3, `_Edge` 0.06.
- **To recreate:** set `tubeInner` > 0 (0.1 was the original) with `MeerkatTubeLook` Original or WallOnly.

---

## 5. The water-tube experiments (mesh skins, no ray-march)

### 5a. The cheap mobile water pipe (d42f0d97d, removed 2026-10-29; §12)

- **Ask:** Yash, 2026-10-28: "a transparent watery tube, mobile friendly, an illusion of a wormhole rather than real
  lensing".
- **Look:** a near-white, mostly clear alpha skin with:
  - a Fresnel rim
  - slow flow bands
  - a two-sine caustic shimmer
  - an optional twist
  - a fade near the camera and at the window's fronts
- **Bends nothing:** no screen copy, no grab pass.
- **Cost:** one draw per tube, about 30 ALU per pixel.
- **Procedure:** `MeerkatWaterTubeView.Draw` draws the calm wall's mesh (uv0.x = share along, uv1.y = share round) per
  tube with an MPB (`_Domain`, `_Alpha`, `_F0`/`_F1`, `_L`, `_T`). The shader is alpha blend, `ZWrite Off`,
  `Cull [_Cull]`:
  - `nv = |n·v|`
  - `fr = (1 − nv)^RimPower`
  - `ang = 2π·round + along·Twist·0.0628 + 0.35t`
  - `band = 0.5 + 0.5·sin(2π(along/FlowScale − t·FlowSpeed))`
  - `caustic = sat(sin(7ang + along·k − 1.3t)·sin(5ang − 0.73·along·k + 0.9t))`
  - `col = lerp(Tint, lerp(Tint, Domain, DomainMix), fr) + 0.6·caustic·Caustic + 0.3·band·Flow`
  - `a = sat(Clarity + Rim·fr + 0.5·band·Flow + 0.35·caustic·Caustic) · window · nearFade · Alpha`
- **Numbers (material defaults):**

  | Property | Value |
  |---|---|
  | `_Tint` | (0.92, 0.97, 1) |
  | `_DomainMix` | 0.15 |
  | `_Clarity` | 0.04 |
  | `_Rim` | 0.45 at power 3 |
  | `_Flow` | 0.12 at speed 0.6, spacing 24 |
  | `_Caustic` | 0.25, size 9 |
  | `_Twist` | 0.15 turns per 100 u |
  | `_NearFade` | (2, 14) |
  | `_Edge` | 0.06 |

- The lab can tune these live ("Tune the water pipe").

### 5b. The watery-glass "aquarium" tube and its network variant (PR #1620, reverted)

- **Ask:** Yash, 2026-10-29: "watery-glassy tunnel, like looking from an underwater aquarium ... tunnels won't have
  domains ... the networked tunnel a little upgraded look".
- **Commits:** cdc1837a6, merged as aec04d0eb, **reverted in c2d3d6be1**.
- **Why it was reverted:** Yash: "this doesn't have an outer layer, no light bending / warping mimic, doesn't feel like a
  wormhole at all". A mesh skin that samples `_CameraOpaqueTexture` can only push the picture sideways at the tube's own
  pixels. It cannot bend rays round the tube, and it has no shell to read as a layer.
- **Procedure:**
  1. It was made the default look (`MeerkatTubeLook` default WaterOnly; the lab started there).
  2. **The aquarium pane:** sample URP's `_CameraOpaqueTexture` at `screenUV + n_view.xy·Refract·rim^1.5/(1 + dist·RefractFalloff)`,
     plus a per-vertex two-sine wobble. `MeerkatWaterTubeView` holds the opaque texture on for game cameras while a tube
     draws, and sets the global `_MeerkatTubeSceneOn`.
  3. Tint the result faintly toward `_Tint`. The domain colour was removed.
  4. Add the Fresnel rim, flow bands and caustic, as §5a. Premultiplied alpha.
  5. `MEERKAT_TUBE_NETWORK` keyword (on `MeerkatWaterTubeNetwork.mat`, used for network channels), adding:
     - an iridescent thin-film rim: a cosine palette on the Fresnel term, `_Iri` 0.7, `_IriScale` 1.6
     - energy streaks racing along the tube in `_StreakLanes` 6 lanes, spacing 36 u, speed 0.9
     - HDR `_Glow` 1.6 for bloom
- **Numbers:**

  | Property | Value |
  |---|---|
  | `_Tint` | (0.84, 0.95, 1) |
  | `_Transmit` | 0.9 |
  | `_Refract` | 0.04 |
  | `_RefractFalloff` | 0.004 |
  | `_Wobble` | 0.006, size 16, speed 0.8 |
  | `_Clarity` | 0.03 |
  | `_Rim` | 0.4 at power 3, `_RimColor` (0.9, 0.98, 1) |
  | `_Flow` | 0.1 |
  | `_Caustic` | 0.22 |
  | `_Twist` | 0.15 |
  | `_NearFade` | (2, 14) |

- **To recreate:** `git show cdc1837a6:Assets/_Graphics/Materials/Graphs/MeerkatWaterTube.shader`. Bring back with it:
  - `Assets/Resources/MeerkatWaterTubeNetwork.mat` and its `.meta`
  - `RVA/MeerkatWaterTubeView.cs`
  - `RVA/MeerkatTubeLook.cs`
  - `Tools/Shaders/verify_meerkat_water_tube.py`

  `git revert c2d3d6be1` re-applies all of it at once.

---

## 6. The tree: the tunnel's outer layer (rounds 13-15, off since round 23, removed 2026-10-29; §12)

- **Look:** a dead tree's trunk wrapped round every tunnel, with gnarled ridges, bark fissures, tree rings on the cut ends
  and branches. It is all watery glass: clear face-on and bright at the silhouette, with the bore (the tube you ride) a
  smooth sheen. The roots of an omnicrystal network wore the same bark. This was the tunnel's **outer layer**.
- **Commits:**
  - 77a203dd8: round 13, the trunk and branches (artifact)
  - c1a6274ed: round 14, the tree as the tunnel's outer layer, formed in the wall's glass and warp
  - d2d51211e, 8fab8f7cc: cloned to Unity
  - e2beadfee, b61e575a2: round 23, no tree and no bark (`trunkVisibility` 0)
- **Procedure:**
  1. `MeerkatTreeMesh` builds the trunk, branches and bore in world space. The vertex data is packed into UVs:
     uv0 = (aS, aBi, aCap, aR), uv1 = (aB, aPhi), uv2 = aBase.
  2. `MeerkatTreeView.Draw` draws it with `MeerkatTree.shader` (additive; one MPB per draw). Each branch's growth is in
     the global `float _Bg[16]`.
  3. The fragment computes:
     - `a = Glass + Rim·(1 − |n·v|)^Power`
     - bark fissures, `Grain` × `GrainN` lines round the wood, fading in over 15-90 u from the camera
     - rings on the caps (`Rings`)
     - the bore (aR = 2) as `Inner` sheen only
     - flow `0.75 + 0.25·sin(23s − 2.4t)`
     - the live window `[F0, F1]` condensing at `Edge`
     - colour from `CIn` (domain) to `COut` (white)
- **Numbers:**

  | Field | Value |
  |---|---|
  | `trunkVisibility` | 0 (was 1) |
  | `trunkTop` / `trunkBase` | 1.5 / 1.9 × r |
  | `trunkBark` | 0.22 |
  | `trunkRidges` | 16 |
  | `trunkGnarl` | 0.14 |
  | `trunkGlass` | 0.012 |
  | `trunkRim` | 0.28 |
  | `trunkFresnel` | 3 |
  | `trunkRings` | 7 |
  | `trunkGrain` | 0.22 × 22 lines |
  | `branchCount` | 6 |
  | `branchLength` | 2.4 |
  | `branchThickness` | 0.28 |
  | `branchUp` | 28° |
  | `branchZone` | 0.85 |
  | `branchSymmetry` | 0.45 |
  | `branchForks` | 3 |
  | `branchGrowSeconds` | 0.5 |
  | `rootBarkVisibility` | 0 |
  | `rootGlass` | 0.0232 |
  | `rootRim` | 1.573 |
  | `rootGrain` | 1.172 |

- **To recreate:** set `trunkVisibility` (for tunnels) and `rootBarkVisibility` (for the network) above 0. A wrapped tube
  skips the bridge and the lens (`TunnelWrapped` / `ChannelWrapped`).

## 7. The roots from inside (`MeerkatRootInside.shader`, removed 2026-10-29; §12)

- **Look:** a soft, smooth inner tube in one slow warm tone. It is slightly dense (`_Op`, from `rootOpacity`, which ships
  at 0), denser at its silhouette, and lit by the rider's lamp (`rootLamp` 0.352, falloff `_Fall`). It fades to **clear**
  (never to black) by `rootFog` 240 u, and its ends stay open.
- **When it draws:** only while the local pilot is inside a bark-wrapped network.
- **Procedure:** alpha blend; `_Lamp`, `_HullP`, `_Fog`, `_Lo0`/`_Lo1` (the openings over `_EndW`). Same mesh layout as
  §6. Commit d2d51211e.

---

## 8. The other wormhole: the Butterfly fold and crystal wormhole mouths (`Wormhole.shader`)

This is a different object: two spheres that are one place. The sphere shows what lies beyond the partner mouth.

- **EXACT VIEW:** `WormholeView` renders the gameplay camera's pose moved through the pair into `_WormholeExactTex`,
  sampled at the sphere's own screen position.
- **PANORAMA:** the partner's six-face capture (`_WormholePanorama`, a 2D array), parallax-corrected to a proxy sphere
  (`_ProxyRadius` 600).
- The two are crossfaded by `_WormholeExactBlend`.
- **Rim:** a Fresnel rim in the domain hue (`_RimPower` 6, `_RimIntensity` 0.35, `_DomainRimBoost` 2, `_RimDarken` 0.25),
  plus a transit flare (2.5) and a sealed shell for rivals.
- **Seamless variant:** `WormholeSeamless.mat` (`_SoftEdge` > 0) is alpha-blended and dissolves into the world with no
  rim. It is used by the crystal wormhole.
- **Commits:** d36cb8850, c2a35c2ff, bb45bb424, 16b41b045.
- **Docs:** `Docs/WORMHOLES.md`, `Docs/CRYSTAL_WORMHOLE.md`.
- **Port:** `Port/src/CosmicShore.Content/Shaders/Hand/Wormhole.glsl`, a hand translation without the panorama.

## 9. Sibling lenses (same plumbing as §1)

`BlackHoleLens.shader` and `.hlsl` (black and white holes), and `VortexRingLens.shader` and `.hlsl` (a toroidal doughnut
of bent light), are documented in `Docs/BLACK_HOLE.md` §5. Each follows the same pattern:
- a script-injected `ScriptableRenderPass` after the transparents
- a copy of the camera colour
- one full-screen triangle that displaces the lookup

Each has a CPU verifier: `Tools/Shaders/verify_black_hole_lens.py` and `verify_vortex_ring_lens.py`. The first also
holds the shared glslang URP mock.

---

## 10. Checking a shader before you commit

1. **HLSL compile:** run `glslangValidator -D -S frag -e <entry>` (and `-S vert`) against the small URP mock in
   `Tools/Shaders/verify_black_hole_lens.py`. On the box it lives at `/home/box/tools/bin/glslangValidator`.
2. **C# compile:** `Tools/Build/unity_refcompile/run.sh` should print `RESULT: OK - no errors in project code`. Four
   Cinemachine "unverified missing-type" lines are environmental.
3. **In Unity:** open `Assets/_Scenes/Game_TestDesign/LavalampWormholeLab.unity` and press Play. Its IMGUI panel rides a
   tunnel or the network, measures the look for 4 s with the camera held (CPU ms, plus GPU ms when Frame Timing Stats is
   on), and puts the layer numbers on live sliders ("Tune the look").

---

## 11. The PC look: one shader, YASH'S and GARRETT'S presets, the networked holes (`_MW_HQ`)

Yash, 2026-10-29: *"Make the shader good for a good quality PC. Keep the final one shader ... a nice subtle wormhole and
its networked holes."*

**One shader.** `MeerkatWormholeLens.shader` gets a local keyword, `_MW_HQ`. Without it, the pass is byte for byte the
§1 lens: `MeerkatTubeLook` Original is the A/B fallback.

With it, **the same march runs on the same numbers** (§1.3, the artifact's water pipe, glass 0.139), and three things are
added on top.

1. **The outer layer.** While the bent ray is inside a shell, it accumulates optical depth: the shell profile × (0.6 + 0.4
   × rib) × fade / thickness × step. This is graded like the field, and the ribs show only faintly. Then, with
   `layer = 1 − e^{−depth}`:
   - The world behind is tinted toward `Tint` by `layer·Absorb`. This is the water's faint absorption.
   - The layer glows `Tint·Outer·layer`.
   - A Fresnel rim, `Tint·Rim·(1 − e^{−Σ grazeᴾ})` with `graze = 1 − |dir·n|`, brightens the silhouette where the ray
     skims the layer.
   - A glint of the scene's sun (`RenderSettings.sun`, otherwise a soft key from above) adds Blinn `|n·h|^48` off the
     shell.
   - Only shell that the ray crosses in front of the opaque scene counts, so a vessel in front of the tube stays clean.
2. **Real light bending, dispersed.** Red is read at `p + disp·(1 + k)`, green at `p + disp` and blue at
   `p + disp·(1 − k)`, so the bent world splits faintly into colour, as through glass.
3. **The networked holes** (a channel of an omnicrystal's network, `_MWTubeShape.y` 1). The layer's share that came from a
   channel (`net`) blends in:
   - a thin-film iridescence round the channel: a cosine palette on `φ/2π·IriScale + u + 0.05t`, weighted by `Iridescence`
   - pulses of light running along it: `(0.5 + 0.5cos 2π(u·count − t·speed))^12`, weighted by `Pulse`
   - glow × `NetGlow`
   - dispersion `NetDispersion` instead of `Dispersion`

**Presets** (`MeerkatWormholeHQ.YashPreset` / `GarrettPreset`). The pass takes the preset of the **nearest** tube's look.

| Value | YASH'S LOOK (`formedLook` Bridge) | GARRETT'S LOOK (ContourField) |
|---|---|---|
| Outer (layer glow) | 0.10 | 0.02 |
| Rim / rim power | 0.22 / 3 | 0.05 / 4 |
| Absorb | 0.35 | 0.12 |
| Tint | (0.82, 0.94, 1) | (0.86, 0.95, 1) |
| Glint | 0.5 | 0.25 |
| Dispersion / network | 0.035 / 0.08 | 0.02 / 0.06 |
| Network iridescence / pulses | 0.65 / 0.18 | 0.5 / 0.12 |
| Iri scale, pulses along, pulse speed | 1.5, 3, 0.6 | 1.5, 3, 0.6 |
| Network glow (× Outer) | 1.6 | 2 |
| Bridge spindles | drawn (`bridgeVisibility` 0.163) | not drawn |

In the PC look the contour field runs in **both** looks (`MeerkatTubeLook.FieldOn`). In Original, it runs only in
Garrett's, as before.

**Uniforms (MaterialPropertyBlock):**
- `_MWHQ0` = (Outer, Rim, RimPower, Absorb)
- `_MWHQ1` = (Tint.rgb, Glint)
- `_MWHQ2` = (Dispersion, NetDispersion, NetIridescence, NetPulse)
- `_MWHQ3` = (NetIriScale, NetPulseCount, NetPulseSpeed, NetGlow)
- `_MWLightDir` = (toward the light, strength)

**Selecting it:**
- `MeerkatTubeLook.Mode` defaults to HighQuality on this branch. The Lavalamp lab's "PC high quality (lens + layer)"
  button selects it; "Original (lens + wall)" is the fallback.
- The lab's "Formed look" row (Config's / YASH'S / GARRETT'S) sets `MeerkatTubeLook.FormedOverride` without touching the
  config asset. The Inspector buttons still set the asset.
- "Tune the PC look (live)" puts every number above on sliders. This is `MeerkatWormholeHQ.Tuning`, starting from the
  preset on screen.
- The networked look needs no switch: ride or watch an omnicrystal network (the lab's RideNetwork).

**Cost over §1.** The march and its taps are unchanged. Inside a shell, each step adds one `pow` and one normalize. The
pixel does two more scene reads (dispersion), and a pixel the layer covers no longer discards.

**Since 2026-10-29 this IS the shipped look**, with no keyword: the `_MW_HQ` guards were removed and the layer always
runs. GARRETT'S column is baked as `MeerkatWormholeHQ.Shipped` (`MeerkatWormholeHQ.Current` returns the lab's live
tuning while "Tune the look" is on, otherwise `Shipped`). The field runs on every formed tunnel and channel
(`MeerkatWormholeConfigSO.ContourFieldBends`). YASH'S column and the keyword version (with the §1 fallback) are kept
in git: §12.

---

## 12. Removed in <REMOVAL_COMMIT> (2026-10-29): what was deleted and how to bring each one back

Yash, 2026-10-29: *"just keep one high quality look, the one there is now, then document other shaders, so we can
recreate later if needed, delete the shaders."*

**Kept:** THE look (§1 + §11: `MeerkatWormholeLens.shader`, now with no keyword, plus `MeerkatWormholeHQ.Shipped` =
GARRETT'S preset), the reticle (`MeerkatWormholeAdditive.shader`/`.mat`), `Wormhole.shader` (§8), the sibling lenses
(§9), and `MeerkatWormholeMath` (pure maths, tested, including `RippleAmplitude`, `RippleFront` and `DentNear`).

**Why GARRETT'S preset was baked:** the shipped config had `formedLook` ContourField (GARRETT'S LOOK), and the branch
default was `MeerkatTubeLook` HighQuality. So what was on screen was the HQ lens with GARRETT'S layer numbers and no
bridge. That is exactly what ships now.

### 12.1 The restore commit

**435ce058c** (the first commit of PR #1624, branch `feat/wormhole-pc-unified`) holds every removed file **byte for
byte as it was deleted**, and the pre-removal versions of every file this removal edited. Restore from it with
`git show` (one file) or `git checkout 435ce058c -- <path>` (several at once). The same commit stays reachable as
`refs/pull/1624/head` on GitHub after the branch is gone.

The non-HQ versions (before the PC look: no `MeerkatWormholeHQ`, no `_MW_HQ`, `MeerkatTubeLook` default Original) are on
`Ys-bleeding-edge` at **ae47984c3**. The six shaders, six materials, `MeerkatTreeView.cs`, `MeerkatTreeMesh.cs`,
`MeerkatTubeWallView.cs` and `MeerkatWaterTubeView.cs` are identical there.

**Always restore the `.meta` with its file.** It carries the GUID below, and the `.mat` files find their shader by that
GUID.

### 12.2 Deleted files

Shaders are in `Assets/_Graphics/Materials/Graphs/`, materials in `Assets/Resources/` (loaded by name with
`Resources.Load`), and scripts in `RVA/` unless a path is given.

| File | GUID (`.meta`) | Lines | Blob | Section | What it was |
|---|---|---|---|---|---|
| `MeerkatWormholeGlass.shader` | 7029c5f2afe0411e9f8f6f70aae88ebc | 69 | db025545 | §2 | YASH'S LOOK: the bridge's Fresnel glass skin |
| `MeerkatWormholeWarp.shader` | c3a6ae601dc34c14bf01fdbab3ff4f04 | 68 | 8fda930c | §3 | The warp tube into `_MeerkatWarpTex` |
| `MeerkatTubeWall.shader` | d44739cf8d4a42dc85d9c93fc4a58b97 | 90 | 446bb932 | §4 | The calm inner wall |
| `MeerkatWaterTube.shader` | 9afb482b928e45ebbafd69cb4b24934c | 130 | 039dc536 | §5a | The cheap mobile water pipe |
| `MeerkatTree.shader` | 412bcd318c0a4b368b031aeffd1818f0 | 115 | 0037dd6c | §6 | The tree / the roots' bark |
| `MeerkatRootInside.shader` | 2afcdf6c151a4a05aee2487cda0357d1 | 110 | 184796b6 | §7 | The roots from inside |
| `MeerkatWormholeGlass.mat` | d6f7018a4e4343199dd9d646cdc4eeea | 30 | 7d9f7929 | §2 | |
| `MeerkatWormholeWarp.mat` | a74684ffcb40435f90b20c6622f57fe6 | 30 | 797185bf | §3 | |
| `MeerkatTubeWall.mat` | 6aa4ba7b9fef44f1bc2facb5282eb7be | 30 | 1e0f01ef | §4 | |
| `MeerkatWaterTube.mat` | 14f7aac5c58642ba83f43718955d9fff | 30 | 080c8ac9 | §5a | |
| `MeerkatTree.mat` | f631b1e04b5640b8aff255e16bae1f7c | 30 | 54f6e373 | §6 | |
| `MeerkatRootInside.mat` | dbd903e084f743b98bde1b64f6eff0e2 | 30 | 78577ecb | §7 | |
| `MeerkatTubeWallView.cs` | 5bc175e132b84907b70b7cf53afc2e7f | 172 | 42d12104 | §4 | Draws the calm wall; `Release(net)` frees `net.Walls` |
| `MeerkatWaterTubeView.cs` | f88bb59c166e4f2493701da0eec336af | 210 | 42dde5b3 | §5a | Draws the water pipe; `Tuning` (lab sliders) |
| `MeerkatTreeView.cs` | 7310ed076cb644b2a49fbfc082d9b583 | 303 | 2a11a2e3 | §6, §7 | Draws the tree, the bark, the root inside; `PrepareSkins`, `Release` |
| `MeerkatTreeMesh.cs` | b66fc30aeef847d2b8647f17275373f1 | 311 | a98b8211 | §4, §6 | Builds the trunk, branches, bore, `BuildTubeWall`; `TreeSettings` |
| `MeerkatTubeLook.cs` | 5bcb6ea9c7974347b879c010fa8c565f | 73 | e8b16732 | §0, §11 | The look switch (`Mode`, `FormedOverride`, `FieldOn`, `LensOn`, `HighQuality`, `OldWallOn`, `WaterOn`) |
| `Assets/_Scripts/Editor/Studios/MeerkatWormholeConfigSOEditor.cs` | 2a9f1883fc8c4393ab24d06107519b98 | 68 | 454a2286 | §0 | The YASH'S LOOK / GARRETT'S LOOK Inspector buttons, `LookName` |

**GUID check before deleting:** each shader GUID was referenced only by its own (also deleted) `.mat`. No scene, prefab,
asset or ProjectSettings file referenced any of the material or script GUIDs, and none named them. **No other object in
the game lost a shader.**

### 12.3 Code changed by the removal (take the 435ce058c version to undo)

| File | What was removed |
|---|---|
| `Graphs/MeerkatWormholeLens.shader` | `#pragma multi_compile_local _ _MW_HQ` and every `#if defined(_MW_HQ)` guard (the layer is now unconditional); `MEERKAT_LENS_MAX_WELLS`, `_MWWellC[8]`, `_MWWellP[8]` and the mouth-dent loop (§3); `_MWRipple` and the ripple term (§3); `TEXTURE2D(_MeerkatWarpTex)` and its sample (§3). `_MWView` is now (aspect, unused, unused, tubes); it was (aspect, warp on, wells, tubes). The fragment discards when no tube is bound, then returns `MWHighQuality(p, MWContour(p, sceneZ), aspect)`. |
| `RVA/MeerkatWormholeLensPass.cs` | The "Meerkat tube warp" render pass and `WarpData`; `WarpMaterialPath`, `MaxWells` and the warp/well/ripple shader IDs; `ScreenWells`, `RippleFor`; `HighQualityKeyword`, `s_lensHQ` (the runtime lens clone with the keyword on), `s_warp`, `s_nearLook`. The `MeerkatTubeLook.LensOn` / `FieldOn` checks became `cfg.ContourFieldBends`; the `ChannelWrapped` skip went. `AnythingLive` no longer counts ripples. |
| `RVA/MeerkatWormholeView.cs` | `GlassMaterialPath`; the glass and warp meshes and `WarpMesh`; `TunnelWrapped`, `ChannelWrapped`; `DrawBridge`, `Fill`, `AddWarpTube`, `Grid`; the per-tunnel and per-channel loops in `DrawOncePerFrame` that drew the bridge and the warp; the calls `MeerkatTreeView.Draw()`, `MeerkatTubeWallView.Draw()`, `MeerkatWaterTubeView.Draw()`. `BridgeFrame` lost its glass arguments; `Upload` lost normals and uv. Only the reticle draws now. |
| `RVA/MeerkatWormholeHQ.cs` | `YashPreset`, `GarrettPreset`, `For(MeerkatFormedLook)`. Replaced by `Shipped` (= GARRETT'S) and `Current`. |
| `RVA/Data Containers/MeerkatWormholeConfigSO.cs` | `enum MeerkatFormedLook { Bridge, ContourField }`, the `formedLook` field (it was not in the artifact mapping), `FormedLook`, `ContourFieldOn`, `Tree()`. The other fields (`warpStrength`, `bridgeGlass`, `trunk*`, `root*`, `tubeInner`, ...) **were kept**, because `Tools/Build/studio_to_unity/meerkat.json` maps them to the artifact and the drift check needs them. Several of them now drive nothing (the tree group's header says so). |
| `RVA/MeerkatRootNetwork.cs` | `internal Mesh[] Skins`, `internal Mesh[] Walls`; `MeerkatTreeView.PrepareSkins(net, cfg)` in `Grow`; `MeerkatTreeView.Release` / `MeerkatTubeWallView.Release` in `Remove`. |
| `RVA/Executors/MeerkatWormholeExecutor.cs` | `MeerkatTreeView.PrepareSkins(net, config)` in `StepRootNetworks`. |
| `Assets/_Scenes/Game_TestDesign/LavalampWormholeLab.cs` / `.unity` | `startMode`; the seven look buttons (`s_modes`, `s_modeNames`); "Bench every look" (now "Measure", one look); the Formed look row (Config's / YASH'S / GARRETT'S); "Tune the water pipe". "Tune the look" stays and starts from `Shipped`. |
| `Assets/_Scripts/Tests/Editor/VesselStudioWindowTests.cs` | `MeerkatLook_NamesYashsAndGarrettsLooks` |

### 12.4 The numbers at removal

**The config asset** (`Assets/_SO_Assets/VesselActions/Meerkat/MeerkatWormholeConfig.asset`, unchanged by the removal
except `formedLook`):

| Group | Values |
|---|---|
| Look | `formedLook` ContourField (1) |
| Field (still used) | strength 6, pitch 0, drag 0.5, twist 0, glass 0.139, glassIndex 0.05, grazing 0.9, flow Even, inner 0.1, outer 0.15, profile Smoother, ribs 0.25, ribSharpness 3, ribTravel 1.2, ribRings 16, ribLines 12 (§1.3) |
| Warp / dent / ripple (§3) | warpStrength 0, dentStrength 0, rippleStrength 0, rippleEdge 0.154, rippleSeconds 1.2 |
| Bridge (§2) | bridgeVisibility 0.163, bridgeGlass 0, bridgeRings 36, bridgeSpindlesPerRing 21, bridgeRibs 5, bridgeSpindleThickness 0.25, bridgeShimmer 0.5, bridgeWhite 0.45 |
| Reticle (still used) | reticleIdleAlpha 0.053, reticleWhite 0.45, reticleNearLight 0.25 |
| Trunk (§6) | trunkVisibility 0, trunkTop 1.5, trunkBase 1.9, trunkBark 0.22, trunkRidges 16, trunkGnarl 0.14, trunkGlass 0.012, trunkRim 0.28, trunkFresnel 3, trunkRings 7, trunkGrain 0.22, trunkGrainLines 22; branches as §6 |
| Roots (§6, §7) | rootBarkVisibility 0, rootTip 0.309, rootGnarl 0.779, rootGlass 0.0232, rootRim 1.573, rootGrain 1.172, rootOpacity 0, rootLamp 0.352, rootFog 240 |
| Calm wall (§4) | tubeInner 0 (0.1 before 767f849e2) |

**The material defaults** are in each `.mat` at 435ce058c and are listed in §4 (calm wall), §5a (water pipe) and §6
(tree).

**The two PC presets** (`MeerkatWormholeHQ`, `_MWHQ0-3` order):

| Look | Outer | Rim | RimPower | Absorb | Tint | Glint | Disp | NetDisp | NetIri | NetPulse | IriScale | PulseCount | PulseSpeed | NetGlow |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| YASH'S (removed) | 0.10 | 0.22 | 3 | 0.35 | (0.82, 0.94, 1) | 0.5 | 0.035 | 0.08 | 0.65 | 0.18 | 1.5 | 3 | 0.6 | 1.6 |
| GARRETT'S = `Shipped` | 0.02 | 0.05 | 4 | 0.12 | (0.86, 0.95, 1) | 0.25 | 0.02 | 0.06 | 0.5 | 0.12 | 1.5 | 3 | 0.6 | 2 |

### 12.5 How to bring a look back, step by step

**Everything at once (the branch exactly as it was before the removal):**
1. `git checkout 435ce058c -- <every path in 12.2, with its .meta> <every path in 12.3>`
2. Check `git diff --staged` for later edits in the 12.3 files that you want to keep, and re-apply them.
3. Run the checks in §10.

**One look (keep THE look and add one back).** First, for every file the look needs, run:
`git show 435ce058c:<path> > <path>` and `git show 435ce058c:<path>.meta > <path>.meta`. Then:

- **YASH'S LOOK, the bridge (§2):**
  - Files: `MeerkatWormholeGlass.shader`, `MeerkatWormholeGlass.mat` (+ `.meta`).
  - In `MeerkatWormholeView`: restore `GlassMaterialPath`, the glass mesh lists, `DrawBridge`, `Fill`, the glass
    arguments of `BridgeFrame`, and the tunnel/channel loop in `DrawOncePerFrame` that calls `DrawBridge`. Copy them
    from the file at 435ce058c.
  - Add a switch back (the `formedLook` field and enum in the config, or a bool) so formed tunnels draw the bridge.
  - For YASH'S layer, use the 12.4 numbers as `MeerkatWormholeHQ.Shipped`, or bring back `YashPreset` and `For()`.
- **The warp tube, dents, ripple (§3):**
  - Files: `MeerkatWormholeWarp.shader`, `MeerkatWormholeWarp.mat`.
  - Lens shader: restore `MEERKAT_LENS_MAX_WELLS`, `_MWWellC/P`, `_MWRipple`, `_MeerkatWarpTex` and their three
    `disp +=` terms, plus `_MWView.y/z`.
  - Lens pass: restore `WarpMaterialPath`, `s_warp`, `WarpData`, the "Meerkat tube warp" pass, the well/ripple IDs,
    `ScreenWells`, `RippleFor`, and ripples in `AnythingLive`.
  - View: restore the `WarpMesh` lists, `AddWarpTube`, `Grid`, and the calls to them in `DrawOncePerFrame`.
  - Then set `warpStrength` / `dentStrength` / `rippleStrength` above 0.
- **The calm inner wall (§4):**
  - Files: `MeerkatTubeWall.shader`/`.mat`, `MeerkatTubeWallView.cs`, `MeerkatTreeMesh.cs` (for `BuildTubeWall`).
  - In `MeerkatRootNetwork`: `internal Mesh[] Walls;` and `MeerkatTubeWallView.Release(this);` in `Remove`.
  - Call `MeerkatTubeWallView.Draw();` from `MeerkatWormholeView.DrawOncePerFrame`.
  - The view reads `MeerkatTubeLook.OldWallOn`: remove that check, or bring back `MeerkatTubeLook.cs`.
  - Set `tubeInner` 0.1.
- **The cheap water pipe (§5a):**
  - Files: `MeerkatWaterTube.shader`/`.mat`, `MeerkatWaterTubeView.cs`, `MeerkatTreeMesh.cs`.
  - Call `MeerkatWaterTubeView.Draw();` from `DrawOncePerFrame`.
  - It is gated on `MeerkatTubeLook.WaterOn`: bring back `MeerkatTubeLook.cs` or remove the gate.
  - The lab's "Tune the water pipe" panel is in `LavalampWormholeLab.cs` at 435ce058c.
- **The aquarium tube (§5b):** as §5b, from cdc1837a6.
- **The tree, bark and roots inside (§6, §7):**
  - Files: `MeerkatTree.shader`/`.mat`, `MeerkatRootInside.shader`/`.mat`, `MeerkatTreeView.cs`, `MeerkatTreeMesh.cs`.
  - Config: `Tree()`.
  - `MeerkatRootNetwork`: `internal Mesh[] Skins;`, `MeerkatTreeView.PrepareSkins(net, cfg);` in `Grow`, and
    `MeerkatTreeView.Release(this);` in `Remove`.
  - Executor `StepRootNetworks`: `MeerkatTreeView.PrepareSkins(net, config);` when `Step` returns true.
  - View: `MeerkatTreeView.Draw();` in `DrawOncePerFrame`, plus `TunnelWrapped` / `ChannelWrapped`. A wrapped tube
    skips the lens: put those checks back in the lens pass's `ContourTubesFor`.
  - Set `trunkVisibility` / `rootBarkVisibility` above 0.
- **The A/B switch and the `_MW_HQ` keyword (§11):**
  - Files: `MeerkatTubeLook.cs`, and the 435ce058c lens shader, lens pass, `MeerkatWormholeHQ.cs` and lab.
  - Optionally `MeerkatWormholeConfigSOEditor.cs`, which needs `formedLook`.

Check each one with §10: glslang on the lens, `Tools/Build/unity_refcompile/run.sh` (and `--config editor`), then the
Lavalamp lab in Unity.

---

## 13. More underwater from inside a tunnel (notes, not implemented)

Yash, 2026-10-29: *"Currently I see clear outer world, but it should look like I'm viewing it from water."*

**Why the inside looks clear now:**
- From inside, your view crosses the shell only at grazing angles. `fieldGrazing` 0.9 scales the bend down to about
  0.2-0.35 there.
- With `fieldPitch` 0, the field runs along the tube, nearly parallel to your view, so `fieldDrag` turns almost nothing.
  `fieldTwist` is 0.
- The glass term is `fieldGlass` × `fieldGlassIndex` = 0.139 × 0.05 ≈ 0.007. The artifact's glass was 1.
- The middle of the screen looks out of the far mouth and never crosses the shell, so it is never bent.
- Only the ribs move.
- GARRETT'S layer is a whisper (Absorb 0.12, Outer 0.02).
- Nothing models the water you are *in*. The lens only bends rays that cross the wall.

**Turn up with the existing knobs** (the config asset, live in Play mode; the HQ numbers via the lab's "Tune the look",
then copy them into `MeerkatWormholeHQ.Shipped`):

| Knob | Now | Try | Range | Effect inside |
|---|---|---|---|---|
| `fieldGlass` | 0.139 | 0.5-1 | 0-2 | Refraction through the shell: the main "glass/water" bend |
| `fieldGlassIndex` | 0.05 | 0.12-0.25 | 0-0.25 | Strength of that refraction (Δn) |
| `fieldGrazing` | 0.9 | 0.2-0.4 | 0-1 | Lower = grazing rays (all you see from inside) bend more |
| `fieldPitch` | 0° | 30-60° | 0-90° | Tilts the field round the tube, so drag bends your view |
| `fieldTwist` | 0 | 0.2-0.5 | 0-2 | Swirl: the world wobbles round the tube |
| `fieldStrength` | 6° | 8-12° | 0-40° | Overall bend |
| `fieldDrag` | 0.5 | 0.8-1.2 | 0-2 | Pulls rays along the field (needs pitch > 0 to show inside) |
| `fieldRibs` | 0.25 | 0.5-0.8 | 0-1 | Moving ripples in the bend |
| `fieldRibTravel` | 1.2 | 2-3 | -4-4 | Ripple speed (the moving "wobble") |
| `fieldRibSharpness` | 3 | 1.5-2 | 1-24 | Lower = softer, wider, more liquid ripples |
| `fieldInner` | 0.1 | 0.2-0.35 | 0-0.6 | Thicker shell inside the wall, so more of the inside view crosses it |
| HQ `Absorb` | 0.12 | 0.3-0.5 | 0-1 | Water tint over the bent world |
| HQ `Outer` | 0.02 | 0.05-0.1 | 0-0.5 | The layer's own glow |
| HQ `Dispersion` | 0.02 | 0.03-0.05 | 0-0.2 | Colour fringing, as through water and glass |

Too much `fieldStrength`, pitch or twist makes the edges swim. Change two or three knobs at a time and ride the lab's
tunnel.

**For a true "inside the water" feel (code, not done):** add an inside-medium term to the lens, applied while the
camera is inside a tube (`MWTube`/`MWChannel` at the eye gives `d < 0`, which the pass can also compute on the CPU and
pass in `_MWView.y` as 0-1 with a fade at the mouths). The term needs:
1. **A full-screen wobble.** Add
   `disp += amp·(sin(f·p.y + s·t), sin(f·p.x·1.3 − 0.8·s·t))` (or a cheap 2-octave noise) to every pixel, so the centre
   of the view wobbles too. Suggested values: amp 0.003-0.01 screen heights, frequency f 3-8, speed s 0.5-1.5.
2. **Water tint and fog.** Lerp the result toward the tint by `1 − e^{−sceneZ/fogDistance}`. Optionally add faint
   caustics, as §5a's two-sine term.
3. **Plumbing.** It must run when no shell is crossed. Skip the `disp ≈ 0` discard and the "no tube hit" early-out
   while inside, and keep the pass alive while the camera is inside a tube.
4. **New config fields:** `insideWobble` (amp, frequency, speed), `insideTint`, `insideFog`, mapped in
   `Tools/Build/studio_to_unity/meerkat.json` if the artifact gains them.

Cost: the full screen is already covered while you ride inside, so the extra cost is a few ALU and no extra taps.
