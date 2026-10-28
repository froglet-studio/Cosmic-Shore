# Stoat: the black-hole / white-hole look, parked (2026-10-27)

**Why this file exists.** Round 23 (Garrett: *"remove both the white hole and black hole. lean completely into the
wormhole"*) and round 24 (Yash: *"we are not going with black hole and white holes, we will use black hole white holes
later maybe, save this in project folders"*) took every black-hole and white-hole visual off the Stoat's Gate mode.
A tunnel is now only space-time bent along the grid. This file keeps what was built for the holes, so it can come back
without being rebuilt.

**Where it is still live.** The Stoat's Mass and Time modes still use black and white holes (their pull, their look
and their Scene Config cards). Only Gate mode dropped them. Nothing in this file is deleted code: it is the Gate-mode
variant that was removed. Since 2026-10-27 the wormhole is the **Meerkat**'s, not the Stoat's (`Docs/Studios/MEERKAT.md`,
`VesselStudio/meerkat.html`): bring a hole look back there, not to the Stoat.

## What was parked

| Piece | What it did | Where to take it from |
|---|---|---|
| `gateHoleCore` (0–0.5, × the mouth; shipped 0.12) | A concentrated hole in each mouth of a tunnel: a black shadow with its photon ring at the entry, a white-hot core at the exit. 0 = a pure dent | `Docs/Studios/StoatFlightStudio.html` at commit `94a4021c52` (round 22): its SHIPPED row, the `WM` slider, the help and `GAME_NAMES` rows (`StoatModeAction.gateHoleCore`) |
| The lens shader's mouth core | In the lens pass's graded-lens branch: `hc = wM[i].x` (the core's angular radius) drew `shadow` plus the photon-ring glow at the entry, and `core` / `coreGlow` at the exit | the same commit, the `lensMat` fragment shader (`float hc = wM[i].z < 0.5 ? wM[i].x : 0.0;` and the four lines after it), and `setLensUniforms`'s gate branch (`core = gateHoleCore * rM`, `wM.x = ang(core)`) |
| The hole spheres on a tunnel | `updateHoleMeshes` drew the black and white sphere meshes at a tunnel's mouths (hidden since round 22) | `Docs/Studios/StoatFlightStudio.html`, `updateHoleMeshes` (it still draws them for Mass and Time) |
| Birth and death effects | A tunnel's white hole arriving as a converging light shell (`spawnWave('in')`), and its annihilation as a gravitational wave and an outgoing shell (`Pair.startDying`) | round 22's `gateCommit` / `gateHold`; round 23 replaced them with `gateFade` (a silent fade) |
| The hole markers | The HUD's "black hole · 22 u" / "white hole · 321 u" labels for a tunnel's mouths | `wellMarkers`; round 23 filters tunnels out (`!w.pr.gate`) |
| Round 18's game gate | The game's own Gate mode is still the straight black-hole / white-hole warp gate (`StoatGateMath`, `StoatModeAction` gate* fields, `StoatSlingExecutor`) | `Assets/_Scripts/Controller/Vessel/R_VesselActions/` (unchanged; §1f of `STOAT_DIPOLE.md`) |

## How to bring it back

1. `git show 94a4021c52:Docs/Studios/StoatFlightStudio.html > /tmp/r22.html` and copy the pieces above into the
   current page (the tunnel code around them has moved on: `whSync`, `setLensUniforms`'s gate branch, the shader).
2. Give it its own slider in **Wormhole · look** again, default 0, so the pure-warp tunnel stays the default.
3. Re-run the studio gates (`check_studio.cjs`, `parity_gate.py`, `studio_to_unity.py --check`) and republish.

The design notes behind the holes: `Assets/_Scripts/Controller/Vessel/R_VesselActions/STOAT_DIPOLE.md` §1f (the game's
gate), §1g–§1j (the studio's wormhole rounds 19–22).
