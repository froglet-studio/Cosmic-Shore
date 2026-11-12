# Proven wins

One row per change that passed `Docs/ProvenWin/PLAYBOOK.md`: one change, one machine-read metric, B better in every
paired sample, output identical, controls behaving, a human sign-off. Newest first.

## W1. ChargeCrystal envelope cull (2026-11-12)

| | |
|---|---|
| Change | One line in `Assets/_Graphics/Materials/Graphs/ChargeCrystal.hlsl` (`ChargeCrystalArcs`): skip an edge's bolt noise when the pixel is farther than `arcWidth * (arcJitter + 3.5)` from it, where the bolt's Gaussian core is below 5e-6. Taken from draft PR #1880 (`claude/standin-noise`), the ChargeCrystal part only. |
| Metric | GPU time to draw the Charge crystal: ms per batch of 48 full-crystal draws at 2560 x 1440 (about 1.31 M crystal pixels per draw), wall clock with a GPU sync, 8 batches chained per sample, 200 interleaved A/B pairs |
| Machine | Gbox: RTX 3080, Unity 6000.3.17f1 batchmode, Android build target, `ShaderABBench` |
| Owner | Garrett (who_works_where for the noise shaders) |

| API | Shipped (A) ms | Change (B) ms | B/A median (p10-p90) | Speed-up | Pairs B faster | 8-bit pixels different | HDR pixels different (max) |
|---|---|---|---|---|---|---|---|
| Direct3D 12 | 3.822 | 2.075 | 0.5425 (0.531-0.560) | 1.84x | 200/200 | 0 | 3 (6.1e-5) |
| Vulkan | 3.670 | 1.946 | 0.5309 (0.522-0.540) | 1.88x | 200/200 | 0 | 3 (1.2e-4) |
| Direct3D 11 | 3.854 | 2.020 | 0.5234 (0.515-0.535) | 1.91x | 200/200 | 0 | 3 (6.1e-5) |

Compared: 48 moments (discharge clock 0-20 s x random rotations), 177 M pixels per API.

Controls, same runs:
- Null (A vs A' with a comment-only edit): B/A 1.0019 / 1.0002 / 0.9986, B faster in 46.5 / 49.0 / 54.5 % of
  pairs, bit-identical. The instrument's noise is about +-2 % (p10-p90) and unbiased.
- Negative (a deliberately over-tight bound, `arcJitter * 0.25`): caught on every API, 12.76 M pixels different at
  8 bits, max 7.08. A wrong cull would not have passed as identical.

Gates: EditMode tests filtered to `Crystal` in the real editor, 302 tests, run on A and on B: 297 pass and the same
5 fail on both (hull-fusion bakes stale for the Dolphin, the Rhino's skinned hull, Shrike morph bounds, two Skim Race
ring-facing tests: C# and asset tests that do not touch this shader; pre-existing on bleeding-edge 548ed93f01).
`unity_refcompile` does not cover HLSL; the real-editor compile of the shader on three APIs is the bench itself
(`hasError=False`, no error messages, both passes present).

Human sign-off: https://claude.ai/artifact/3ZwLvwTXqfWi4J3uw71EFe (blind, per-viewer random letters, flicker and
1:1 crops of the real Unity frames; votes in the artifact's `votes` collection).

Not measured: a phone GPU (tile GPUs handle the branch differently; one capture of a crystal on a low-end phone is
the next check), and the frame-level weight (the saving is per crystal pixel: large when a crystal fills the screen,
small when it is a dot).

Runner-ups from the same pick, for the next run:
1. `ProjectileChargeField` early-out (#1880, same shape: exact envelope cull before a 3-octave FBM; claimed 1.19-1.28x).
2. Vortex-ring lens 3-step curvature iteration (#1877; claimed 3.3x, max 0.32 px off: needs a displacement-error metric, not bit identity).
3. Prism rest (#1879; claimed -35 % vertex work per resting prism; C# + shader, needs a scene bench, not a material bench).
