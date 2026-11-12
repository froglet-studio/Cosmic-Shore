# Proven wins

One row per change that passed `Docs/ProvenWin/PLAYBOOK.md`: one change, one machine-read metric, B better in every
paired sample, output identical, controls behaving, a human sign-off. Newest first.

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
candidate moves here with its evidence when it is picked, proven or rejected. Rejected so far: none (the envelope cull
was superseded, not rejected: it is runner-up 1).
