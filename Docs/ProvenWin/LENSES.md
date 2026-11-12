# Proven Win scout lenses

How `/proven-win-scout` finds candidates, and what every outcome taught it. The lenses are the search; the lessons at
the bottom are appended by `queue.py done` (one per finished candidate, required) and are read before every scout run.
When three or more lessons say the same thing, fold them into the lens they belong to (edit the lens text, keep the
lessons).

## Lenses (search in this order)

### multiplied-by-zero
A term computed and then multiplied by a value that is exactly 0 over a large region: a `smoothstep` or `saturate`
that clamps to 0, an intensity, a mask. Skip the term where the multiplier is exactly 0.
- Found: W1 (3x), W3, W4. Rejected: the sky nebula (Vulkan), ProjectileChargeField (not exact), SeaLines (no gain).
- Check: the skip must be INSERTED with nothing moved; a Vulkan-heavy shader (`half` everywhere) may still change
  pixels, so expect Vulkan to decide.
- Check: is the pass ALU-bound? A cheap full-screen pass (SeaLines, 0.11 ms per full screen on an RTX 3080) is
  bandwidth-bound on desktop and shows no gain; propose it only with a phone in hand.

### silenced-but-computed
A feature a script switches off at run time (`SetFloat(<id>, 0)`, a property block) whose shader still computes it.
Grep `SetFloat(\w+, 0` and property blocks that zero an intensity, then read the shader.
- Found: W1 (the Charge crystal's crawl, held at 0 by the seam-walk overlay).

### narrowable-search
A search over neighbouring cells, taps or iterations where a bound on the inputs proves some can never win.
- Found: W2 (6 Voronoi cells instead of 9). Rejected: the same search with approximate trig (140 levels off).
- Check: keep the original arithmetic (same hash, same sin/cos); approximations fail the identity bar.

### unseen-work
Work for things no camera can see: `updateWhenOffscreen`, instanced draws with whole-batch bounds, cameras rendering
under an opaque cover, particle systems whose renderer is off, animation of hidden UI.
- Found: W5 (Space hearts, `updateWhenOffscreen`).
- Check: first prove the engine does NOT already skip it (a probe: does the count change frame to frame?).
- Check: a scene that moves on by itself (Authentication -> Menu_Main) cannot be scene-benched on its own.

### every-frame-writes
A value rewritten every frame with the same value, dirtying a canvas or renderer.
- Rejected: the doors Animator; Menu_Main's 42 writers frozen together changed nothing (BuildBatch ran on ~0.1 of
  frames). Unity 6 does not re-batch on these writes the way older notes claim: measure with the scene bench's
  freeze-all before proposing a single writer.

## Lessons (appended by `queue.py done`; newest last)

- 2026-11-12 w1-charge-crystal-silenced-skip (won, lens: silenced-but-computed): read the C# that writes a material at run time before measuring it; the asset's state was the old look and hid a 3x win.
- 2026-11-12 charge-crystal-envelope-cull (rejected, lens: multiplied-by-zero): measure a candidate in EVERY state the game draws it in; a skip that helps one state can cost 1-2 % in another through code generation alone.
- 2026-11-12 spindle-voronoi4-poly (rejected, lens: narrowable-search): approximate replacements (polynomial trig) fail the identity bar; propose only exact rewrites.
- 2026-11-12 sky-nebula-skip (rejected, lens: multiplied-by-zero): in a shader written with half, a branch anywhere can change Vulkan's arithmetic; run Vulkan first and expect it to decide.
- 2026-11-12 sealines-early-out (rejected, lens: multiplied-by-zero): a cheap full-screen UI pass is not ALU-bound on desktop; an ALU saving there needs a phone to show.
- 2026-11-12 doors-animator-sleep (rejected, lens: every-frame-writes): a settled Animator does not cost a measurable canvas rebuild in Unity 6; do not propose single every-frame writers without a freeze-all measurement first.
- 2026-11-12 menu-canvas-dirtiers (inconclusive, lens: every-frame-writes): Menu_Main's 1.8 ms UpdateBatches is not dirt-driven; the next step is a development-player measurement, not another writer.
- 2026-11-12 w5-space-hearts (won, lens: unseen-work): engine flags that keep off-screen work alive (updateWhenOffscreen) are cheap, exact wins once the bounds provably hold every pose.
- 2026-11-12 creature-bloom-pulse-skip (inconclusive in the material bench, lens: multiplied-by-zero): give every early-out a probe pair; without it a 1.00x result said nothing because the guarded state never occurred.
- 2026-11-12 auth-cameras-under-cover (inconclusive, lens: unseen-work): before proposing a scene-bench candidate, check whether the scene navigates away on its own (Authentication, Bootstrap); bench those in their real boot path or as a prefab in an empty scene
