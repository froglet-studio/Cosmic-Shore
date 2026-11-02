# Ark escort chaos pass (DONE)

Branch `claude/voyage-chaos` (off `origin/claude/voyage-night` 4aebc6fa7), merged into `claude/voyage-night`. Paths
under `Assets/_Scripts/`.

## Owner feedback addressed

1. **Regrowth only in a held nucleus.**
   - New `Ark.RegrowAllowed`: a closed gate loses its beats, it doesn't save them up.
   - `ArkEscortRun.Chaos.cs` `TickChaos` (1 Hz) opens it only while the Ark is inside the nucleus of a world under the squad's control. Without a nucleus, "inside" means within 25% of the membrane radius.
   - "Under control" means a hostile world the squad **pacified** (the earned notion: share, min volume, nucleus time), or a world that started friendly and is still the squad's.
   - Merely leading an empty hostile nucleus doesn't count. In round 1 the Ark's own hull led every empty nucleus, so it healed everywhere.
   - The tier's `RegrowSeconds` still sets the rate when the gate is open.
   - Reported as `RegrowSeconds` per leg and per run, next to `PlatesRegrown`.
2. **Relentless pressure.**
   - **Feeding lure:** `Cell.SetFeedingLure` sends a tier share of a hostile world's fauna goal picks (the densest-region queries every grazer steers by) to the Ark. Its only caller is the escort, for hostile, unpacified worlds. It changes steering only. Recorded as a fenced carve-out of "no direct Ark weighting" in `Docs/ECOSYSTEM.md` §0.z.
   - **Faster waves:** tier `SpawnPeriodScale`, through new `Cell.RuntimeFaunaSpawnPeriodScale`.
   - **Armour bites for everyone:** tier `ShieldBiteMin` / `SuperShieldBiteMin` / `BiteScale` give every escort creature that eats a bite of armour. In round 1 the hull bottomed out at the Ark's 23 armoured plates, which almost no species could gnaw.
   - Every world is hostile from d1 (`HostileFrom` 1, `HostileShare` 1).
   - Populations rise from 0.35 at d1 to 1.4 at d9, inside `maxPopulation` 2 (the collider budget). Most of the extra pressure comes from behaviour (lure, bites, cadence) rather than headcount.
   - New params: `tier.<d>.lureShare`, `spawnPeriodScale`, `shieldBiteMin`, `superShieldBiteMin`, `biteScale`.
3. **Inverted Ark materials.**
   - The model's Body material is transparent with back-face culling, and the domain's ship material also culls back faces. Parts whose faces are wound inward (the mantle cavity and others) vanished from outside and showed from inside.
   - `ArkModel.MakeDoubleSided` now gives every model material a per-Ark copy with culling off (`_Cull`, `_CullMode` and `_RenderFace` set to 0). The domain swap still finds its placeholder, and the shared vessel materials are untouched.
   - Not yet checked by eye: please look at the Ark in the build.

## Final ladder (`ArkEscort.asset`, ArkSpeed unchanged)

| d | Population | RegrowSeconds | LureShare | SpawnPeriodScale | ShieldBiteMin | SuperShieldBiteMin | BiteScale |
|---|---|---|---|---|---|---|---|
| 1 | 0.35 | 0.5 | 0.05 | 1.0 | 0 | 0 | 1 |
| 2 | 0.40 | 0.6 | 0.07 | 1.0 | 0 | 0 | 1 |
| 3 | 0.45 | 0.8 | 0.09 | 0.95 | 0.02 | 0 | 1 |
| 4 | 0.52 | 1.0 | 0.12 | 0.9 | 0.04 | 0.02 | 1.0 |
| 5 | 0.60 | 1.4 | 0.18 | 0.82 | 0.07 | 0.03 | 1.1 |
| 6 | 0.72 | 2.0 | 0.26 | 0.74 | 0.10 | 0.05 | 1.25 |
| 7 | 0.85 | 3.0 | 0.35 | 0.66 | 0.13 | 0.06 | 1.4 |
| 8 | 1.10 | 4.5 | 0.55 | 0.5 | 0.20 | 0.10 | 1.8 |
| 9 | 1.40 | 7.0 | 0.80 | 0.4 | 0.30 | 0.15 | 2.4 |

## Sims

Lock-aware `run_sim.ps1`, time scale 3, 4-world voyages, random seeds. Logs are `D:\VoyageLogs\chaos_r1..r4.jsonl`.
In the hull column, each entry is a run, given as the result and the lowest hull fraction.

| round (ladder) | d | Sparrow x3 | Sparrow+Squirrel+Dolphin |
|---|---|---|---|
| r1 (first cut, leaky regrowth, no bite floors) | 4 | 2/2 won (6%, 22%) | 1/1 won (14%) |
| | 7 | 1/1 won (2%) | 0/1 |
| | 9 | 1/1 won (4%) | 1/1 won (20%), **too easy** |
| r2 (bite floors, strict regrowth; too hard) | 4 | 0/2 | 2/2 won (1%, 3%) |
| | 7 | 0/1 (fell in world 1) | 0/2 |
| | 9 | 0/1 | 0/1 |
| r3 | 4 | 2/2 won (3%, 9%) | 2/2 won (4%, 6%) |
| | 7 | 0/2 (fell in world 4 both times) | 0/2 |
| | 9 | 0/1 (88 s) | 0/1 (91 s) |
| **r4 (final)** | 4 | 1/1 won (49%) | 1/1 won (9%) |
| | 7 | 1/2 (one won at 55%, one fell in world 1) | 0/2 (fell in worlds 4 and 3) |
| | 9 | - | 0/1 (fell in world 2) |

Over rounds 3 and 4 (same shape, r4 a notch kinder), it's close to the ask:
- d4 won 8/8, often narrowly;
- d7 won 1/8, the rest falling in worlds 3-4;
- d9 won 0/4, usually falling in the first or second world.

Sample sizes are small, so treat this as a first fit and refine it with the farm. Two things to watch:
- one d7 Sparrow win at 55% is an outlier. It rolled no modifiers, but its worlds were Garland > Boneyard > Swarm > Maw, the gentlest draw without Scarab Reef, which is where most d7 runs bleed. Variety widens the spread, by design;
- d4 mixed squads finish at 4-9% hull, which is tight for "achievable".

## Not done / next

- The lure is the strongest knob, so co-evolution should search `tier.<d>.lureShare` and the bite floors rather than populations.
- No GPU device-lost was seen at these populations (d9 at 1.4 x threat, capped 2).
- Swarm and Substrate creatures still never bite armour, so the bite floors only reach Boid, LightFauna and worm eaters.
Compile clean; EditMode 66/66 (`-batchmode -force-d3d11`).
