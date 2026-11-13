# Proven wins

One row per change that passed `Docs/ProvenWin/PLAYBOOK.md`: one change, one machine-read metric, B better in every
paired sample, output identical, controls behaving, a human sign-off. Newest first.

## W5. Space hearts stop re-deforming while off screen (2026-11-12)

| | |
|---|---|
| Change | `SpaceCrystalAnimator.Awake` turns `updateWhenOffscreen` off on the heart's SkinnedMeshRenderer and gives it local bounds of a cube through the rest mesh's AABB corner (every spin pose is a rigid turn about the centre, so no pose leaves that sphere). The prefabs author it on, so every Space heart re-deformed its blend shapes every frame, seen or not. Found by the sweep (its P2-2). |
| Metric | CPU: `MeshSkinning.Update` and main-thread ms per frame, scene bench, editor Play mode, 200 CrystalSpace copies behind the camera plus 49 on a grid spanning past the view edges, 40 ABBA pairs of 30 frames |
| Result | `MeshSkinning.Update` 0.152 -> 0.047 ms (B lower in 99 % of frames); main thread 4.73 -> 4.47 ms (B lower in 67 %). Without the in-view grid: 0.137 -> 0.015 ms (100 %). About 0.5 us of skinning per off-screen heart, plus main-thread overhead. |
| Exactness | Screenshots with scaled time stopped: identical, including the hearts straddling the frustum edges. Negative control (bounds 1e-5): 1.6 M px differ. |
| Exposure | Garland's menu world (6-12 Space hearts), fauna-heavy cells (Wildlife Liberation, about 80). Small in the menu, more with fauna. |
| Gates | Crystal EditMode tests: the same 5 pre-existing failures as bleeding-edge. Not measured on a phone. |
| Lane | The Ecology lane was occupied (Yash, `claude/squirrel-time-upgrade-b02f6b`); Garrett said to ship the measured wins on 2026-11-12, and the change is queued for Yash's /approve. |

## W4. HyperSea sky: reject empty star cells before the per-cell maths (2026-11-12)

| | |
|---|---|
| Change | In `computeStars` (`HyperSeaSkybox.shader`), each of the 27 cells tests its existence hash against a bound computed once, `(_StarBaseProb + max(_StarGalacticBoost, 0)) * 1.0001 + 1e-6`, before the normalize, dot and pow of the exact test, which stays unchanged. `pow(1 - galDist, c) <= 1` for `c >= 0` (guarded), so only cells the exact test rejects are skipped; with the shipped values about 97 % of cells are empty. The sweep's S4. |
| Metric | as W3, measured on top of W3 |
| Owner | Shombith |

| API | W3 ms | W3 + this ms | B/A median (p10-p90) | Speed-up | Pairs B faster | Pixels different |
|---|---|---|---|---|---|---|
| Direct3D 12 | 32.92 | 30.88 | 0.9395 (0.932-0.944) | 1.06x | 199/200 | 0 (bit-identical) |
| Vulkan | 29.75 | 27.57 | 0.9273 (0.925-0.933) | 1.08x | 200/200 | 0 |
| Direct3D 11 | 32.92 | 30.41 | 0.9205 (0.915-0.927) | 1.09x | 200/200 | 0 |

Negative control (bound halved) caught on D3D12: 836,000 px, up to 65 levels. With W3 the sky is about 1.16-1.19x
faster than shipped, bit-identical.

Rejected with it: the nebula region-mask early-out (S1). On top of W3 it is 1.12x and within one level on D3D, but on
Vulkan it moves 3.3 M of 44 M pixels by up to 3 levels (270,000 px by more than one).

## W3. HyperSea sky: skip the dust off the galactic band and the core outside its caps (2026-11-12)

| | |
|---|---|
| Change | Two exact early-outs in `Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader`: `computeDust` returns 1 where the galactic mask is exactly 0 (its occlusion is then exactly 1; about 88 % of the sky), and `computeCore` returns 0 where both cap smoothsteps are 0 (every term is 0 x finite; about 85 % of the sky). Found by the "Find more provable Unity performance wins" sweep (its S2 and S3). |
| Metric | GPU time of the sky: ms per batch of 16 full-screen sky draws at 2560 x 1440 (the material on a sphere round the eye, `_Time` driven by the bench), 200 interleaved pairs |
| Exposure | The authored skybox of 59 build scenes, Menu_Main and every match; full cost on Desktop and MobileHigh (MobileLow and Web draw a baked panorama). On the RTX 3080 one full-screen sky costs about 2.3 ms at 1440p. |
| Owner | Shombith (the sky shader) |

| API | Shipped ms | Change ms | B/A median (p10-p90) | Speed-up | Pairs B faster | Pixels different |
|---|---|---|---|---|---|---|
| Direct3D 12 | 36.61 | 33.43 | 0.9132 (0.907-0.919) | 1.095x | 200/200 | 0 (bit-identical) |
| Vulkan | 32.86 | 30.13 | 0.9158 (0.910-0.921) | 1.092x | 200/200 | 0 |
| Direct3D 11 | 35.95 | 32.99 | 0.9203 (0.913-0.925) | 1.087x | 200/200 | 0 |

About 0.2 ms per frame of sky at 1440p on this GPU, every frame, in menus and matches. Null control 1.0000 (50.5 %).
Negative control (dust bound `< 0.02`) caught on every API (about 97,000 px). Not measured: a phone GPU, where the
sky's share of the frame is larger.

How the set was cut: the sweep proposed three early-outs (nebula S1, dust S2, core S3). All three together were 1.23x
on D3D and within one display level there, but on Vulkan they moved 3.3 M pixels by up to 3 levels. Measured apart on
Vulkan: dust bit-identical, core bit-identical when inserted without reordering lines, nebula within one level on
829,000 px. So W3 ships dust + core; the nebula early-out (about 10 % more) is queued below for a human sign-off.
The star pre-test (S4) on top of S1-S3 measured bit-identical and 1.08x on D3D12; it is queued for the other APIs.

## W2. Spindle lace: search 6 Voronoi cells, not 9 (2026-11-12)

| | |
|---|---|
| Change | The lace's Voronoi node in SpindleGraph, SpindleDepthGraph, SpindleLodMidGraph, SpindleLodFarGraph and SpindleWireGraph becomes a Custom Function, `SpindleVoronoi6` (`Assets/_Graphics/Materials/Graphs/Standins/SpindleStandin.hlsl`): rows {-1, 0} x columns {-1, 0, 1}, the node's own Tchou hash and true sin/cos. The lace angle is `sin(_Time.y + _Phase)`, so a feature point's y lies in [0.77, 1] and the row above can never be nearest. From the spindle stand-in study (draft PR #1878), its exact variant. |
| Metric | GPU time to draw the spindle: ms per batch of 48 draws at 2560 x 1440 (GyroidSpindleMaterial on the Branch prefab's mesh, about 8-10 % of the frame), 200 interleaved pairs, `_Time` driven by the bench |
| Owner | Garrett (Docs/SPINDLE_LOD.md) |

| API | Shipped ms | Change ms | B/A median (p10-p90) | Speed-up | Pairs B faster | Display difference |
|---|---|---|---|---|---|---|
| Direct3D 12 | 0.689 | 0.570 | 0.8234 (0.784-0.876) | 1.21x | 200/200 | 104 px move 1 level, none more |
| Vulkan | 0.669 | 0.545 | 0.8135 (0.777-0.867) | 1.23x | 200/200 | 99 px, 1 level |
| Direct3D 11 | 0.650 | 0.524 | 0.8045 (0.769-0.852) | 1.24x | 200/200 | 104 px, 1 level |

177 M pixels compared per API; HDR max |diff| 0.00049 (float rounding of the reordered min). The final graph as
committed re-measured 1.21x (D3D12), 98 px at one level. Null control 1.00x (50.5 % of pairs). All five graphs
compile in the real editor with no errors (`ShaderABBench.CheckShaders`); `wire_spindle_lod.py --check` agrees.

Tier: **within one display level**, not bit-identical. Garrett's bar is that "any human observable difference is either
insignificant or an improvement", so the blind sign-off decides:
https://claude.ai/artifact/U4Bwcwvi127XeH8nZwjwpK

Not taken: the study's 2x2 search with polynomial sin/cos (the draft PR's own choice) measured 1.49-1.54x but moved
pixels by up to 140 levels (85-337 px by more than one), so it fails the bar. Not measured: a phone GPU.

## W1. Charge crystal body: skip the silenced crawl (2026-11-12)

| | |
|---|---|
| Change | One early-out in `ChargeCrystalDischarge` (`Assets/_Graphics/Materials/Graphs/ChargeCrystal.hlsl`): when `_ArcIntensity` and `_NodeIntensity` are both 0, return 0 before computing the edge crawl's noise. The result was already `0 * (finite)`, so this is exact. |
| Why it matters now | The current Charge crystal's single plasma bolt is `CrystalChargeArcs`' seam walk, a separate additive overlay. It holds the body's old crawl at zero through a property block (`silenceCrawlOn` on `CrystalCharge.prefab`), and `CrystalHeartDraw` silences it on hearts. The body shader kept computing the whole crawl on every pixel and multiplying it by zero. |
| Metric | GPU time to draw the Charge crystal body: ms per batch of 48 full-crystal draws at 2560 x 1440 (about 1.31 M crystal pixels per draw), wall clock with a GPU sync, 8 batches chained per sample, 200 interleaved A/B pairs |
| Machine | Gbox: RTX 3080, Unity 6000.3.17f1 batchmode, Android build target, `ShaderABBench` with the game's state (`_ArcIntensity` = `_NodeIntensity` = 0) |
| Owner | Garrett (who_works_where for the crystal shaders) |

In the game's state (crawl silenced):

| API | Shipped (A) ms | Change (B) ms | B/A median (p10-p90) | Speed-up | Pairs B faster | Pixels different |
|---|---|---|---|---|---|---|
| Direct3D 12 | 3.695 | 1.209 | 0.3272 (0.318-0.338) | 3.06x | 200/200 | 0 (bit-identical) |
| Vulkan | 3.559 | 1.126 | 0.3158 (0.311-0.321) | 3.17x | 200/200 | 0 (bit-identical) |
| Direct3D 11 | 3.753 | 1.127 | 0.3014 (0.294-0.307) | 3.32x | 200/200 | 0 (bit-identical) |

Controls, same runs, 48 moments x 2560 x 1440 compared each (177 M pixels):
- Null (comment-only edit, crawl silenced): B/A 1.0008 / 0.9997 / 0.9965, B faster in 49.0 / 51.0 / 58.5 % of pairs.
- No cost where the crawl is on (a material that still uses it, e.g. a hull fusion copy): B/A 0.9918 / 0.9920 / 0.9948,
  bit-identical. The uniform branch costs nothing measurable.
- Negative (`if (true) return`, crawl on): caught on every API at 16.2 M differing pixels, max 7.22.

Gates: EditMode tests filtered to `Crystal` in the real editor, 302 tests, on A and on B: 297 pass and the same 5 fail
on both (hull-fusion bakes stale for the Dolphin, the Rhino's skinned hull, Shrike morph bounds, two Skim Race
ring-facing tests: C# and asset tests that do not touch this shader; pre-existing on bleeding-edge 548ed93f01).
`unity_refcompile` does not cover HLSL; the bench compiled the shader in the real editor on three APIs.

Human sign-off: https://claude.ai/artifact/3ZwLvwTXqfWi4J3uw71EFe (blind, per-viewer random numbering, flicker and
1:1 crops of the real Unity frames of the body pass; votes in the artifact's `votes` collection).

Not measured: a phone GPU; the frame-level weight (the saving is per crystal-body pixel); the seam-walk overlay itself
(CPU-built mesh, a separate draw this change does not touch).

How it was found: the first pick was the envelope cull from draft PR #1880, measured on the material as authored
(crawl on). Garrett pointed out the game draws a newer crystal; the bench then got `floats`/`colors` overrides so it
measures the state the game draws, and the silenced crawl showed up as pure waste. Lesson in PLAYBOOK §1: measure the
shipped state, read the scripts that write the material at run time, not the .mat.

Runner-ups, for the next run:
1. The envelope cull (#1880): rejected on top of W1 (see Rejected): it slows the silenced crystal on D3D11.
2. `ProjectileChargeField` early-out (#1880, same shape; claimed 1.19-1.28x). Check its run-time state first.
3. Prism rest (#1879; claimed -35 % vertex work per resting prism; C# + shader, needs a scene bench, not a material bench).

## Queue and rejections

The queue moved to `QUEUE.json` (view: `QUEUE.md`), changed only by `Tools/Build/proven_win/queue.py`; every outcome
also teaches the scout through `LENSES.md`. The Rejected table below stays as the record with the numbers.

Rejected:

| Date | Candidate | Why | Evidence |
|---|---|---|---|
| 2026-11-12 | Sky nebula skip at the call site (sweep N1b, the "Vulkan-safe" rewrite) | Vulkan still moves 2.86 M px by up to 3 levels (270,000 by more than one), 1.13x. N1a (inserted in place, no reorder) had already failed as `W3S1`. So the branch itself changes Vulkan's arithmetic, not the line order. Dead on Vulkan. | `rejected_nebula/` |
| 2026-11-12 | SeaLines wave-line early-out (sweep U1) | Bit-identical but no gain on desktop: Sea_Window 1.000x (52 % of pairs), Sea_Panel 1.000x (47 %); the negative control caught 216,000 px. 32 full-screen draws take 3.5 ms, so the pass is fill- or bandwidth-bound here, not ALU-bound. May matter on a phone. | `rejected_sea/` |
| 2026-11-12 | Scene-transition doors Animator sleep (sweep UI-1) | No gain anywhere it runs. In-scene bench, editor Play mode, the doors opened as SceneLoader does, then 40 ABBA pairs of 30 frames, Animator enabled vs disabled. GameCanvas.prefab: PlayerUpdateCanvases 0.063 vs 0.066 ms, Canvas.BuildBatch 0.0061 vs 0.0063 ms (screens identical). Menu_Main: PlayerUpdateCanvases 1.854 vs 1.857 ms, B lower in 49 % of frames. The settled Animator does not cost a measurable rebuild; the menu canvas rebuilds every frame for other reasons. | `rejected_doors/` |
| 2026-11-12 | ChargeCrystal envelope cull (#1880) on top of W1 | With the crawl ON (hull-fusion copies keep it) 1.86-1.89x and identical at 8 bits on all APIs. But in the game's main state (crawl silenced, where W1's skip returns before the cull's code runs) D3D11 got 1.6 % SLOWER, B slower in 95.5 % of pairs (D3D12 and Vulkan neutral): code generation, not the branch. An observed cost on every live crystal to help only fused copies. | `rejected_cull_on_w1/` |
| 2026-11-12 | Sky nebula region-mask early-out (sweep S1) | On Vulkan, on top of W3, 3.3 M of 44 M px move by up to 3 levels (270,000 by more than one); D3D is within one level. Vulkan is the phone path. A rewrite that keeps Vulkan's fp16 arithmetic identical might pass. | `W4_stars/report_*_W3S1.json` |
| 2026-11-12 | Spindle 2x2 Voronoi with polynomial trig (draft PR #1878's choice) | 1.5x but moves pixels by up to 140 levels | `W2_spindle/report_*_pr4.json` |
| 2026-11-12 | `ProjectileChargeField` early-out (#1880; skip the wander FBM off the arc's band) | Not identical and not categorical: 4 px differ at 8 bits (max 0.00098, 48 moments x 3.7 M px), and B was faster in only 85 % of pairs (median 1.17x, p90 B/A 1.01). The null control read 1.00x with 47.5 %, so the instrument was fine; the change is near-exact and sometimes slower (a divergent branch on a partly covered sphere). The Sparrow's charge shell is also a few pixels on screen in play. | `ShaderABBench` DX12, `spec_pcf_*.json`; negative control caught at 140,527 px |

