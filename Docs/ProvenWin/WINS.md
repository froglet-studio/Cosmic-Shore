# Proven wins

One row per change that passed `Docs/ProvenWin/PLAYBOOK.md`: one change, one machine-read metric, B better in every
paired sample, output identical, controls behaving, a human sign-off. Newest first.

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
1. The envelope cull (#1880, ChargeCrystal part; `B_ChargeCrystal.hlsl` here): measured 1.84x / 1.88x / 1.91x with the
   crawl on and 1.85x silenced (D3D12), identical at 8 bits. With W1 in, it only helps materials that keep the crawl on
   (hull fusion copies), so price how many of those draw before taking it.
2. `ProjectileChargeField` early-out (#1880, same shape; claimed 1.19-1.28x). Check its run-time state first.
3. Prism rest (#1879; claimed -35 % vertex work per resting prism; C# + shader, needs a scene bench, not a material bench).

## Queue and rejections

The queue is W1's runner-ups above plus whatever the "Find more provable Unity performance wins" thread ranks; a
candidate moves here with its evidence when it is picked, proven or rejected. The envelope cull was superseded, not
rejected: it is runner-up 1.

Rejected:

| Date | Candidate | Why | Evidence |
|---|---|---|---|
| 2026-11-12 | `ProjectileChargeField` early-out (#1880; skip the wander FBM off the arc's band) | Not identical and not categorical: 4 px differ at 8 bits (max 0.00098, 48 moments x 3.7 M px), and B was faster in only 85 % of pairs (median 1.17x, p90 B/A 1.01). The null control read 1.00x with 47.5 %, so the instrument was fine; the change is near-exact and sometimes slower (a divergent branch on a partly covered sphere). The Sparrow's charge shell is also a few pixels on screen in play. | `ShaderABBench` DX12, `spec_pcf_*.json`; negative control caught at 140,527 px |

