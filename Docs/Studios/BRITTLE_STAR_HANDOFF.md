# Brittle Star: handoff between the two sessions (2026-10-14)

> **Status: DONE except publishing (2026-10-14).** Garrett asked for the value of `cece/brave-galileo-d95fpj` to be kept and
> retired that branch and its thread. It is merged into `cece/gallant-lamport-sc3khe`: §1's two text follow-ups are made
> (the Falcon (9) row is the legacy placeholder; the two `BRITTLE_STAR.md` files cross-link); §3 is done (the page's default
> Flight numbers set is the Unity build, `SHIPPED` holds its numbers, `Tools/Build/studio_to_unity/brittlestar.json` maps 34
> of them, `--check` 0 drift); §5's checks pass (and `brittlestar_score.cjs` gained the `require` lines it was missing). §4,
> publishing into the one artifact, is still open: it goes through `Ys-bleeding-edge` and needs the owner's approval.
> Round 2's scorecard and findings: `BRITTLE_STAR.md` §5.2. The rest of this file is the handoff as written.

Two sessions built the Brittle Star on the same day, from the same Notion class document, without seeing each other:

| Branch | Session | What it holds |
|---|---|---|
| `cece/brave-galileo-d95fpj` | `session_01Lrmpko3ipkWr2zfNmLg2in` ("find everything on the brittlestar/falcon … a polished version in the selector") | **The research and the web studio.** `Docs/Studios/BRITTLE_STAR.md` (everything found: the Notion design, bleeding-edge's dead `Falcon.prefab`, the five `Reintroduce-Falcon` / `brittlestar` branches, the fauna brittle star, scorecard findings), the **Vessel Studio page** `Docs/Studios/VesselStudio/brittlestar.html` in the hub and `studios.json` (the "selector"), concept art `Docs/Studios/BrittleStar/*.jpg`, the scorecard runner `Docs/Studios/BrittleStar/brittlestar_score.cjs`. **No C#.** |
| `cece/gallant-lamport-sc3khe` | `session_01RxzcdLn1BkEkrUdJTiaZ6N` ("Polished vessel from Class Brittle-Star") | **The Unity vessel.** `VesselClassType.BrittleStar = 16`, `BrittleStarExecutor` / `Rules` / `HullForm` / `HullBuilder` / `VesselTransformer` / `PrismController`, HUD pair, ability map, `Tools/Build/author_brittlestar_assets.py`, the harness, tests, and its own record `Assets/_Scripts/Controller/Vessel/R_VesselActions/BRITTLE_STAR.md` — **with Garrett's in-session decisions** (×6 Speed, the five arms, corner trails, ring 2.5×, level camera). |

**Garrett's decisions on `gallant-lamport` win.** He made them there, in conversation, after this studio's numbers were chosen.
The studio must be brought into line with them (§3), because the artifact is the one source of truth and Unity is its
clone (CLAUDE.md) — a studio that disagrees with the shipped build decides nothing.

## 1. Merging

`git merge cece/brave-galileo-d95fpj` into `cece/gallant-lamport-sc3khe` was **test-merged clean** (2026-10-14, both
tips as of this file): `Docs/claude/ABOUT_PROJECT.md` and `Docs/claude/DOCUMENTATION_INDEX.md` auto-merge. Two textual
follow-ups after the merge, because both sides are right in isolation:

- `ABOUT_PROJECT.md` then carries both this branch's edited **Falcon (9)** row ("to be called the Brittle Star …") and
  gallant-lamport's new **Brittle Star (16)** row. Make the Falcon row say it is the legacy Manta-copy placeholder
  superseded by `BrittleStar = 16`, and point at `Docs/Studios/BRITTLE_STAR.md` for its history.
- Two records named `BRITTLE_STAR.md` live in two folders. Keep both: `R_VesselActions/BRITTLE_STAR.md` is the Unity
  build's contract; `Docs/Studios/BRITTLE_STAR.md` is the research, the design transcription, the studio and its
  findings. Cross-link them (one line each).

## 2. What this branch found that the Unity build should know

All in `Docs/Studios/BRITTLE_STAR.md` §3–§4. The load-bearing points:

- **Never reuse 12** (the BrittleStar branches' id; Scarab owns it). gallant-lamport's **16** is right.
- `Falcon.prefab` (`VesselClassType.Falcon = 9`) on bleeding-edge is a **non-spawnable Manta copy**: no `VesselController`, no bindings,
  null gun factory and prism channel, no skimmer, `vesselType: 0`, in `DefaultNetworkPrefabs` but not the prefab
  container; `_TEMP/FalconClassSO` + four training SOs + a captain SO point at it. Decide whether 9 is retired, renamed, or
  left as a placeholder (it is still in `SO_Classlist_All` and in `OneThumbVesselCoverageTests`' list).
- The never-merged BrittleStar branches' gun ring is worth reading before tuning the arms: `GunRingTransformer`
  (ring spins 20°/s; every gun `LookAt`s a focus 70 + 300·|R|² u ahead — the same "stick moves the convergence" idea the
  arms now use), with an `ea615857b` fix (iterate direct children only, or the barrel meshes double-rotate).
- The BrittleStar branches' shared-SO state (`BrittleStarModeSwitchingFireSO` keeps `_selector` on the asset) is the AUDIT
  §2 #4 bug class — gallant-lamport's executor-owned mode state avoids it; keep it that way.
- AUDIT/BACKLOG rows about a `maxSyncPrewarm=8` cap were wrong (no such field anywhere); corrected on this branch.

## 3. Bring the studio into line with the Unity build (the next job)

The studio page (`brittlestar.html`) models the design document on the BrittleStar branch's numbers. Where
gallant-lamport decided otherwise, change the PAGE (its `SHIP_SETS`, `KIT`, `STYLES`, `stepPilot`, `layTrail`,
`volley`, the input map and the copy), then re-run §5. Every row:

| Topic | Studio today (`brittlestar.html`) | Unity build (`R_VesselActions/BRITTLE_STAR.md`) — **adopt** |
|---|---|---|
| Modes | Exactly one always on (starts in Speed); none only without a ring | Exclusive, plus **Cruise (none)**: pressing the active mode's button again returns to Cruise |
| Mode buttons | X Speed · Y Creation · B Destruction; keys 1/2/3 | **A Speed · B Creation · X Destruction**; keys Space / R / Q |
| Cruise speed | 35 u/s (branch `DefaultThrottleScaler` 25 + 10) | **45 u/s** |
| Speed mode | ×4 (`boostMultiplier`), Time ×1→×1.5 on the bonus → 110–155 u/s | **×6 at rest → ×8 at Time 10** (floored ×4) on the throttle TARGET → 270 u/s; turn rates follow speed (`RotationThrottleScaler` 0.1) |
| Trail in Speed | wavelength × the boost (the mode's TARGET) | trail spacing follows the hull's **actual** speed (so a Speed→Creation switch no longer lays 3× for a second) |
| Base trail | (2,2,5) every 7 u, 4 u behind | **(2.5, 2.5, 4.5) every 6 u, 2 u behind** |
| Creation | one block per **edge** of the pentagon (a hollow pentagonal frame), at cruise speed | **five lines, one per ring CORNER**, 2 u behind each corner, every 6 u; block 2.5×2.5×4.5 × Mass ×1→×1.8; cockpit trail paused. **Creation is the slowest mode: ×0.5 cruise** (Overdrive: ×6 × ×0.5 = ×3) |
| Destruction | the five turrets fire **while fire is held**, ×2.5 rounds | **fires on its own — the five ARMS**: 90 rounds/s each, 1350 u/s, unspread; RT adds nothing |
| Aim stick | "Spread" (design) or "Convergence" (branch) dropdown | **in = the arms converge 120 u ahead; out = each splays 25°** (`BrittleStarRules.ArmDirection`); **in Destruction the stick's DIRECTION also turns the ring and the arms** (Garrett, later 2026-10-14); outside Destruction: picks the turret (ring turns ≤36° at 540°/s) and opens a spread to 16° |
| Fire outside Destruction | 30 rounds/s, 1500→4000 u/s with Space, 0.3 s | **RT: 12 rounds/s, 700 u/s**, 0.3 s × **range ×1→×1.6 with Space** |
| Ring | radius 7 | **radius 6** (ring is NOT a hit box: hull box 4.6 × 4.6 × 2.4) |
| Ring Detonation | +420 u/s, 1.4 s fuse, ball 55 u, wave to 150 u ±10, Charge ×1→×1.6 | **(Garrett, later 2026-10-14) one FLAT DISK in the ring's plane: as thick as the ring (half-thickness 0.22 × Charge ×1→×4), as wide as the cell's NUCLEUS (`NucleusVisualWorldRadius`, fallback 200), swept out over 0.7 s, one hit per hull (MissileBlast). No AOE ball, no separate plane wave.** +240 u/s, 1.1 s fuse; super-shielded mass refuses |
| Rebuild | instant from a charge | **0.5 s bloom** from a charge |
| Overdrive | 6 s | 6 s (same) |
| Vessel hits | **petals only** (fixed on this branch to match the LOCKED rule) | petals only (same) |
| Camera | Sparrow's (0, 10, −50) | **straight behind, no height offset** (the ring frames the aim) |
| Element map | Charge rounds+blast, Mass Creation blocks, Space range, Time Speed bonus | **Charge = Ring Detonation, Mass = Creation, Space = Destruction (+Fire), Time = Speed**; same dials; **all L5 open** (proposals in its §5, for Garrett) |
| AI | four persona brains + Balanced; planner over a heading fan | one brain (sits in Creation, Destruction for walls ≥ 8, ring at clusters ≥ 18, Speed by `BrittleStarSpeedModeAIPolicySO`); **personas are its follow-up F3 — the studio's `STYLES[].brain` is a ready reference** |

Add a **"Unity build (gallant-lamport)"** entry to `SHIP_SETS` and make it the default, rather than deleting the branch
and Falcon sets: Vessel Config ▸ Flight numbers is where a tester compares them. Then write
`Tools/Build/studio_to_unity/brittlestar.json` (the `/artifact-to-unity` map from page names to `BrittleStarConfigSO`
fields) so `studio_to_unity.py --check` guards the two from drifting again.

The hub card's spec row "Flight · one stick · 35 u/s, 110 in Speed" must change with it (`index.html` bay AND
`studios.json` spec, then `bake_previews.cjs` and `parity_gate.py`); the hub's Ask `SPEC['Brittle Star']` too.

## 4. Publishing into the one artifact (not done yet)

- The artifact (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa) is built from **`origin/Ys-bleeding-edge`**, which has
  newer studio files than either branch (`studio-game.js`, `studio-new.js`, `.claude/skills/vessel-studio/game_catalog.py`,
  `sync_job.py`, `index.html` +68 lines, `studios.json`). Publishing from a Brittle Star branch would roll those back.
  Merge the studio into `Ys-bleeding-edge` first (expect conflicts in `index.html`, `studios.json`,
  `VesselStudio/README.md`, `SKILL.md` — keep both sides' cards and rows), then
  `build_artifact.py --ref origin/Ys-bleeding-edge --out <scratch>/vs` and publish per `/vessel-studio` §4.
- The `Artifact read` of that URL returned "a public artifact from outside your organization — retry so the user is
  asked": the person has to approve that read (and needs writer access) before any publish.
- After publishing: record the first decision in the artifact's `decisions` (what the studio is for), per `/vessel-studio` §2.6.

## 5. How to check the studio

```sh
export NODE_PATH=/opt/node-tools/node_modules
curl -sSfL -o /tmp/three.min.js https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js
node .claude/skills/vessel-studio/check_studio.cjs Docs/Studios/VesselStudio/brittlestar.html --hook __brittleStarStudio --three /tmp/three.min.js --out /tmp/bs
node Docs/Studios/BrittleStar/brittlestar_score.cjs Docs/Studios/VesselStudio/brittlestar.html /tmp/three.min.js   # ~20 s
node .claude/skills/vessel-studio/bake_previews.cjs && python3 .claude/skills/vessel-studio/parity_gate.py
python3 .claude/skills/vessel-studio/domain_colors.py --check
```

All passed on `cece/brave-galileo-d95fpj` at handoff. The test hook `window.__brittleStarStudio` exposes `state`, `race`,
`set(k, v)` (`you`, `game`, `course`, …), `camera(c)`, `free`, `startRace`, `stopRace`, `newRace`, `runHeadless`,
`runScorecard`, `runStyles`, `makeWorld`, `stepWorld`, `aiInput`, `KIT`, `WORLD`, `SHIP`, `ide`.

## 6. Open questions for Garrett (from the studio's scorecard, `BRITTLE_STAR.md` §5.1)

1. **Ring Detonation doesn't pay against Overdrive.** A crystal taken while charged gives all three modes for 6 s, so
   the best pilot never spends the charge on a ring. Shorter Overdrive, a ring that refunds on a big burst, or keep it?
2. **Destruction clears Derby walls faster than the hull reaches them** (at the branch's numbers; re-measure at the Unity
   build's five arms, 450 rounds/s, which will be stronger still): the intensity ladder only bites at I4.
3. ~~Speed momentum carries into Creation~~ — **fixed on gallant-lamport** (spacing follows actual speed); the studio still shows it until §3 is applied.
4. The four **level-5** slots (gallant-lamport §5 has proposals A/B per element).
5. **Touch** cannot reach the three modes (gallant-lamport F1): the studio's phone layout has SPEED / CREATE / DESTROY / RING
   buttons on the right — a ready answer to try on a phone.
