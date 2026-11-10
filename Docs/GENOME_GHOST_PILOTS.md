# Ghost pilots: a footage-tuned ambient flock (prototype)

Branch `claude/genome-ghost-flock`, 2026-11-10. Not wired into any scene or prefab.

## What it is

`GhostPilotFlock` draws a few dozen "ghost pilots" for a background, such as the menu cell: faceted dart bodies
that wander inside a soft sphere, each laying a trail of faceted prisms that grow in, taper, and shrink to nothing.
It is render-only. It is **not fauna**: no colliders, no NetworkObject, no heart, no mass, no `PrismSpatialIndex`
entries. Nothing eats it and it eats nothing, so the ecology rules are untouched. It owns no GameObjects.

- It blooms in on enable: everything scales up from zero, staggered per pilot.
- `FadeOut()` shrinks everything to nothing and then switches the component off. Disabling the component
  directly skips the fade, so callers use `FadeOut()`.
- Bodies and trails are lit, faceted meshes with one normal per face: the lab's dart and pyramid, built in code
  (`GhostPilotMeshes`). They draw with a clone of `Resources/OrcaCrewLit.mat` (URP/Lit, instancing on) with
  emission switched on. Colours go in through one property block per team.

Files:
- `Assets/_Scripts/Game/Environment/GhostPilots/`
  - `GhostPilotFlock.cs`: the component and its Burst job.
  - `GhostPilotMotion.cs`: the pure step.
  - `GhostPilotMeshes.cs`: the two meshes.
  - `GhostPilotFlockProfileSO.cs`: the profile asset.
- `Assets/_SO_Assets/Environment/GhostPilots/`
  - `GhostPilots_ChaseNoHud.asset`: tight and busy, 16 pilots in a 146 u bound.
  - `GhostPilots_OrbitOverview.asset`: wide and slow, 24 pilots in a 566 u bound.
- `Assets/_Scripts/Tests/Editor/GhostPilotFlockTests.cs`: the tests.

## Where the numbers came from

A procedural genome lab (`amoebius-genome`) tuned agent-motion genomes until its rendered clips matched real
gameplay footage. The motion that matched was **not flocking**. At the tuned densities a pilot averages about 0.1
neighbours inside the boids radius, so cohesion, alignment and separation do almost nothing. What remains is pilots
wandering under a turn cap: neighbour-free and O(n).

The two presets are the lab's `chase_nohud__tuned.json` (footage score 5.20 to 1.66) and
`orbit_overview__tuned.json` (5.26 to 3.49). Their `ag.*` nodes are copied value for value; each asset's
`sourceGenome` field names the nodes.

| Parameter | Chase | Orbit | Lab op |
|---|---|---|---|
| pilots | 16 | 24 | `ag.spawn n` (rounded, as the lab does) |
| cruise speed (u/s) | 29.68 | 30.58 | `ag.spawn speed` |
| turn cap (rad/s) | 1.341 | 2.530 | `ag.boids turn` (its other params are dropped) |
| wander (Hz, amp) | 0.300, 6.10 | 0.032, 3.46 | `ag.wander` |
| bound (radius, stiffness) | 146.1, 0.494 | 566.2, 0.494 | `ag.bound` |
| trail (interval s, life s) | 0.058, 9.77 | 0.080, 20.21 | `ag.trail` |

The step is a line-for-line port of `sim.py` `World._step_movers`: wander as a sum of six sines, the optional
`ag.attract` spring and orbit (off in both presets), the soft bound, the turn cap, speed relaxed toward cruise at
2/s, at the lab's fixed 60 Hz. There are two deliberate differences:
1. The boids forces are left out. At about 0.1 neighbours they are near zero.
2. The turn cap is exact. The lab's normalised lerp overshoots the cap by a few percent.

The RNG is Unity.Mathematics', not numpy's, so the presets give a statistically matching flock, not the lab's exact
one.

Measured from the C# step, run under .NET with Unity.Mathematics from source, 3 seeds, 60 s each:

| Preset | Peak radius | Median speed | Peak speed | Peak turn rate |
|---|---|---|---|---|
| Chase | 1.58-1.60 R | 29.5 (cruise 29.7) | ~70 u/s | 1.342 (cap 1.341) |
| Orbit | 1.13-1.14 R | ~49.5 (cruise 30.6) | ~69 u/s | 2.530 (cap 2.530) |

The orbit speed is the lab's own behaviour: its slow wander pushes along the heading for many seconds, and the
2/s relax only partly pulls the speed back. With the bound removed, pilots reach 6.5 R (the negative control).

## Costs

- **Step**: 0.28 us per pilot-step under .NET 8 JIT, single thread, no Burst. Measured with the harness above.
  The brief's figure for a compiled port is ~0.08 us per agent at 4096 agents; Burst has not been measured here.
- **Trail matrices**: about 0.13 ms per tick for the orbit preset (6,096 prisms), managed, one thread.
  - Each prism's world frame is fixed when it is laid, so a tick only rescales it.
  - The first draft rebuilt each one with TRS and a matrix multiply. That cost 2.5 ms per tick.
- **Draws**: 2 x teams per frame (bodies and trails per team), chunked at 1023 instances. The matrices are rebuilt
  at `matrixUpdateHz` (default 30). The cached arrays are submitted every frame, the same pattern as
  `CapsuleMembrane`.
- **Memory**: about 120 bytes per trail slot. The orbit preset holds about 0.75 MB of native arrays; the
  component disposes them in `OnDisable`.
- **WebGL Toybox**: there are no threads and no Burst there, so the job runs inline with `Run()`. The same check
  as `SwarmFauna` is used: `Application.platform == RuntimePlatform.WebGLPlayer`. Expect roughly 2x the managed
  numbers above.

## How to try it

1. Add an empty GameObject where the flock should be centred. For the menu cell, put it at the cell's centre.
   Menu_Main's camera orbits at about 686 u.
2. Add `GhostPilotFlock` and assign a preset. Orbit Overview fits a wide backdrop.
3. Set `visualScale`. The lab's sizes read at chase distance, about 1 u. From 686 u, try 4-8. This scales the
   look only, never the motion.
4. Enter Play. The flock warms up for 8 s (motion only, nothing drawn), then blooms in.
5. To remove it, call `FadeOut()`. Do not disable the component directly.

Menu_Main is governed by the design board (`Docs/MENU_REDESIGN_B.md`, `/frogma`), so wiring the flock into it is a
board decision, not part of this branch. Every Menu_Main screen except HOME pauses time. `useUnscaledTime`
(default on) keeps the pilots moving under those screens; turn it off if they should freeze with the world.

## What is verified, and what is not

Verified:
- `Tools/Build/unity_refcompile`, configs `player`, `webgl` and `editor`: no errors in these files. The 21 errors
  every config reports are in files this branch does not touch.
- The step's numbers above, run in C# outside Unity.

Not verified:
- **The EditMode tests have not been run in Unity.** They compile under the editor config.
- **Nothing has been seen in Unity**:
  - whether `OrcaCrewLit` with emission and per-team property blocks reads well;
  - whether the bodies are visible from 686 u;
  - whether the trail pyramids read as prisms.
- **Burst has not been measured.** The 0.08 us figure is the earlier study's.
- **The look has not been checked against the footage in Unity.** The parameters match the footage in the lab's
  renderer only.
- The lab's neon edge emission (`edge`) and its colour HDR handling are approximated by URP/Lit emission.
- The scattered static crystals (`ag.scatter`) in the same genomes are not ported.
