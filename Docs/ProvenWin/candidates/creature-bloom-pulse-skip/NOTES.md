# creature-bloom-pulse-skip: where it stands (2026-11-12)

Two HOLDs in a row; per the skill this waits for a person before a third attempt.

What is solid:
- Vulkan, RTX 3080: B/A 0.7296 (1.37x), B faster in 100 % of 200 pairs, bit-identical; probe_reach and probe_guard
  both changed 27 M px; the negative control (a wrong constant, 0.01) was caught at 22 M px. The far state (800 u)
  is bit-identical and 1.33x on all three APIs (DX12 0.751, D3D11 0.753, Vulkan 0.744).

What is not:
- DX12 and D3D11 at 150 u: probe_guard changes nothing and the negative control is not caught, so on D3D the body
  is NOT in the skipped state at 150 u, while on Vulkan it is. The same spec, the same globals (logged). Something
  distance-related reaches the shader differently per API: most likely the camera position the bench writes
  (`_WorldSpaceCameraPos`, written both in the command buffer and as a plain global since this run) versus what
  URP binds. Until the bench is known to put the body where it says, the 150 u numbers on D3D say nothing.
- The open state (450 u) is not repeatable between two compiles of the SAME code: a null (A vs A plus a comment)
  moves 22.5 M px by up to 222 levels on every API, deterministically, while A drawn twice matches itself (the new
  repeatability check). B draws ~2.5 % more pixels: the bloomed body is slightly larger. So the two materials or
  shader clones see a different input while the bloom is active. Not found yet. Checked and ruled out: `_Time`,
  `_TimeParameters` and `_PrismClock` (now all driven per moment), draw order (repeatability), the include chain.

What would settle it: draw the bench through a real Camera placed at the eye (so URP binds every per-camera
value itself), or a one-off probe that writes `_WorldSpaceCameraPos` and the object origin into the colour.

## Settled the same day: the premise holds on Vulkan only

Probes (`zz` folders, deleted after reading): on both Vulkan and DX12 the shader sees the eye between 100 and 300 u
and `norm <= 0.5`, so the bench camera is right. But on DX12 `openness > 0` at that distance, while on Vulkan it is
exactly 0. The ramp's `max(eff, spread) - spread` and `saturate(sqrt(...))` leave a tiny positive openness at rest
when compiled through FXC/DXC, so on D3D the pulse is not multiplied by an exact 0 and the guard never fires: no
gain there, and nothing to skip without changing pixels. On Vulkan (the phone path) the skip is real: 1.37x and
bit-identical. Outcome: inconclusive. What would settle it: decide whether a Vulkan-only gain that is neutral and
identical on D3D (D3D11 measured 1.01x, B faster in 32.5 % of pairs) is acceptable, or make the ramp return an
exact 0 at rest on every API first (a change to IncorporealDepth's own output on D3D, so a picture review).
