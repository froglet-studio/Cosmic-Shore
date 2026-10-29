# Performance brief for an outside agent (Gemini) — `Ys-bleeding-edge`, 2026-10-29

**What this is.** Everything an agent that has never seen this repo's `CLAUDE.md` system needs before doing
performance work on `Ys-bleeding-edge`: the target, where the frame actually goes, what was done, what was
measured dead, what is open, the tests still owed, how to measure, the checklist every fix is held to, and the
house rules a change must keep. It was compiled from the repo's own records (listed in §14), not from memory.
Where this brief and a record disagree, **the record wins** — read it.

**Snapshot:** `origin/Ys-bleeding-edge` @ `d8fdda23` (48 commits ahead of `bleeding-edge` @ `30935594`).
Last automated sweep in the record: **2026-10-25 22:09 UTC** (4 days old — refresh it, §9.1).

---

## 0. Read order (do this before touching code)

1. **This brief**, end to end.
2. `Docs/PERFORMANCE_OPTIMIZATION.md` — the performance record: state (§1), history (§2), the ranked lever table
   (§3.3), **measured-dead list (§3.5 — never redo these)**, the phone plan (§3.6), and how to measure (§4).
3. `Docs/Reports/PERFORMANCE.md` — the report, with the auto-generated sweep table at the bottom.
4. `.claude/skills/perf-hunt/SKILL.md` — the resolve loop and the fix checklist (summarised in §10 here).
5. Only when the area comes up: `Docs/MEMORY_AUDIT.md` (RAM/disk), `Docs/PLATFORM_UNIFICATION.md` (device tiers,
   quality presets, phones), `Docs/PRISM_ECS_MIGRATION.md` (instanced prisms, device gate, pure-entity plan),
   `Docs/PRISM_ANIMATION.md` (LOCKED clock-material law), `Docs/SPATIAL_INDEX.md`, `Docs/SPINDLE_LOD.md`,
   `Docs/claude/SHADERS_AND_PERFORMANCE.md`, `Docs/claude/ANTI_PATTERNS.md`.
6. Frozen evidence behind older claims (code comments cite its sections, e.g. `§0.11.4`):
   `Docs/archive/PERFORMANCE_LOG_2026.md`. Do not append to it.

---

## 1. Things to know first (the ten that save the most time)

1. **The prisms are NOT the cost.** Every `Prism` draws through an instanced Entities Graphics (BRG) companion
   entity. 103,823 prisms = 4.8 ms CPU / 2.1 ms GPU in the lab (122 fps), **12.9× cheaper** than the old
   GameObject-per-prism path; the boot world's 8k prisms are 23 opaque draws. Draw calls, materials and prism
   count are measured dead as causes (§6). The frame goes to what grows *beside* prisms: spindles/snow
   (transparent renderers), fauna body-sync and behaviour ticks, AI/gunfight UniTask continuations, a ~10 ms
   fixed floor, periodic sweeps (collider-LOD), UI (uGUI clipping/TMP), and editor-side taints.
2. **Each prism is still a GameObject** (collider + ~7 MonoBehaviours, ~2–4 KB) behind its render entity. 100k
   GameObject prisms will not fit a MobileLow phone's memory → lever **L4 (pure-entity prisms)** is structural
   for the phone target, designed (Checkpoint D in `PRISM_ECS_MIGRATION.md`), not built.
3. **No phone has ever been measured.** The device gate `EntitiesGraphicsSupportProbe` (SRP + compute + the
   `SparseUploader` kernels) drops every prism to the 12.9× slower legacy MeshRenderer path on a device that fails
   it (e.g. a GLES3-only Android). That single unknown could be the whole "phone is slow" story.
4. **The editor lies unless set up.** Code Optimization must be **Release** (Debug ≈ 5× slower C#), Burst on
   (off ≈ 10–20× on jobs), Deep Profile off (≈ 10×), Game view focused, no visible Scene view, **the "Third Eye"
   window closed** (a second full render; its old pilot scan caused 85–130 ms freezes every 0.5 s), `fps uncap`.
   The capture tooling detects and refuses most taints.
5. **A capture is not a number until it is valid.** `perf_capture_report.py` and `perf_hunt.py` refuse Debug,
   Deep Profile, frame-capped, Burst-off, managed-job, or second-camera captures. Never build a fix on one.
6. **Two measurement engines.** *Unity* (the real numbers; captures arrive automatically from collaborators'
   editors on `bugledger/<user>` branches). *Amoebius* — the team's own .NET re-implementation of the engine under
   `Port/`, which runs the game's C# headless: its **allocations per marker are trustworthy and repeat to ~1 %**;
   its milliseconds are .NET without Burst, so they only *order* things. A timing win still needs a Unity number.
7. **Amoebius has engine shims that allocate where Unity doesn't** (`Mesh.Set*(List)` copies each call). They are
   listed in `Docs/perf/amoebius_known.json` and are not game regressions — check there before "fixing" one.
8. **`FindObjectsByType` / `FindObjectsOfType` in a periodic path is a proven killer** (1.8 ms → 20–61 ms per call
   as a session ages). The fix pattern is a static live registry kept in `OnEnable`/`OnDisable`
   (`VesselStatus.Live`).
9. **Spikes come from "everything at once".** The house pattern is: slice a pass across frames under a per-frame
   *time* budget, queue instead of one coroutine per item (`Prism.CreationQueue` replaced 57k coroutines), stagger
   periodic heavy work across actors (`SkimRaceReplanGate`), never log per frame.
10. **Several systems are LOCKED** (ecology, prism animation, party/presence, scoring, elemental abilities,
    threading). A perf change there must keep every invariant — read the doc first (§11).

---

## 2. The target and how it is judged

| | |
|---|---|
| **Desktop target** (confirmed 2026-09-23) | 60 fps in a **Development build** on the reference PC: 16.7 ms frame, plan ≤ 14 ms average, **no frame over 50 ms** in steady play. The editor is for finding and ranking, never for judging (it adds 4–19 ms and runs Mono). |
| **Phone target** (Yash, 2026-10-14) | **100,000 prisms at 60+ fps on a phone** — "at least 2× today". Defined as: on the same phone, same scenario, Release, `diag` 15 s: **(A)** halve CPU busy ms in S2 and S4; **(B)** double the largest prism count that holds 60 fps p95 in S4, on each device tier. |
| **Device tiers** | `Desktop`, `MobileHigh`, `MobileLow` (`DeviceTierClassifier` → one `PlatformProfileSO` per tier). Quality presets Medium/Low/Very Low cut GPU work through `GraphicsCuts` (merges tier + preset; `Resources/QualityPresetCuts.asset`). |

**The scenario set** (same every time, so numbers compare across weeks):

| # | Scenario | Wait before measuring | Last known state (editor, merged tree, 09-24→26) |
|---|---|---|---|
| S1 | Menu_Main, **Garland** (default boot cell) | 4 min | 14.3 ms CPU busy |
| S2 | Menu_Main, **Lattice** via Cell Selector | 8 min | 21.4 ms, p99 41 — heaviest growing world |
| S3 | **Rampage intensity 1** | 3 min | **never measured** (a game bug grew no flora/fauna at i1) |
| S4 | **Scurry intensity 4** (Atlantis, ~69k prisms) | 3 min | 10.6 ms — most prisms, fastest scene; 15.9 ms `LOD.Sweep` spike every 0.25 s |
| S5 | **Wildlife Liberation** (~1.2k creatures) | 3 min | **46.0 ms, p99 163** — the only scenario missing the target; all main-thread script |
| S6 | Menu_Main, **Arboretum** via Cell Selector | 8 min | 12.5 ms — hard-capped world, cleanest for A/B |

Auto-capture times (`PerfAutoCapture`, from `perf_hunt.py SCENARIOS`): S2 480 s, Skim Race 90 s, S5 120 s,
anything else 90 s.

---

## 3. Branches and ownership

- **`Ys-bleeding-edge`** — Yash's working branch; **performance work lands here first**, then flows to
  `bleeding-edge` (the team's shared branch, where the daily GitHub workflow runs). `Docs/perf/status.json`
  `branchFlow` records this.
- Performance area owner: **Yash**. Other owners you will hit while doing perf work (ask before changing their
  areas; record `Affects: <person>'s <area>` in the commit):

| Area | Owner / who to ask |
|---|---|
| Ecology (flora, fauna, cells, spindles, spindle LOD, prism motion/collider ladder, guns/projectiles) | **Garrett** |
| Menu / style-B UI kit (`Assets/_Scripts/UI/Kit`, Menu_Main), tooling, Amoebius engine (`Port/`), Claude skills/hooks, UGS/backend | **Shombith** |
| FTUE | Caleb with Shombith |
| Performance, AI learning, Switchback, Fulmen / prism burn, Soliton tunnels (all recent on `Ys-bleeding-edge`) | **Yash** |

Run `python3 Tools/Build/who_works_where.py query <paths>` before editing; it prints in-flight branches and owners.

---

## 4. What is done (since day 1, condensed)

| When | Done | Result |
|---|---|---|
| Jul | De-spike pass: growth sliced under budgets, cached waits, GC behind the splash; collider-LOD classification in Burst; async pool refills; cell volume sum → Burst job | Menu ~6 → ~50 fps; script tick 15.2 → 1.7 ms. Found Burst was OFF in the editor all along |
| Aug | **Clock-material law**: all CPU prism animation deleted, animation is GPU-clocked; deaths batched as pure entities; shields to a GPU morph; FMOD looping-one-shot leak fixed | A 2,408-death frame had cost 1,863 ms; gone. `RuntimeManager.Update` 693 ms → normal |
| Sep | Instanced path measured 12.9× vs legacy; Frame Debugger proved ~90 % of draws are not prisms; tools: `renderers` census, `freeze`/`ab` same-state A/B, `prof` (Profiler → JSON), `diag` per-system markers; the six-scenario baseline | Only S5 misses the target |
| 10-05→08 | Skim Race AI pilot: no-alloc math, float inner loops, staggered re-plans (`SkimRaceReplanGate`); **Burst fix**: `System.MathF` in two jobs had silently disabled Burst for every game job in the editor | Skim Race I2 28.3 → 18.0 ms, GC 78 → 27 KB/f; re-plan p99 15 → 9.6 ms |
| 10-09 | Amoebius records `ProfilerMarker` time/calls/**bytes** → per-system allocation measurement with no Unity | |
| 10-15 | **The perf-hunt system**: auto-capture in Play, auto-send after Play, `perf_hunt.py` sweep/gates/baselines, daily workflow, `/perf-hunt` loop. Lava-lamp regression (100+ → 19–30 fps) found by Amoebius A/B and fixed (charge arcs, guide scene searches, spotlight text) | |
| 10-16 | First real shared Unity capture: retired PipCamera still rendering on the Grizzly (fixed), per-frame `List` in `BufoAnimation`/`DolphinAnimation`, LINQ in `TurnMonitorController`; `GunTransformer.Update` `GetComponentsInChildren` per frame; `WaitCache` shared `WaitForSeconds` | Update-alloc findings 6 → 0 |
| 10-17 | AOE explosion pool (L13) — real allocator was `CrystalMorphMeshBuilder.TryBuild` (cached now: 5.53 → 3.41 KB/f); charge arcs half-rate 24–64 px; **snow drawn instanced** (L6, −4.2k GameObjects); over-prewarmed pools cut (implosion 160 → 24, explosion 1,500 → 0); **`VesselStatus.Live` registry** replaced `FindObjectsByType` (black-hole tick max 36 → 6 ms) | |
| 10-20→25 | **Spindle LOD** (L1, Garrett's area): thickness-based three tiers, far limbs instanced; Drain budgeted by time; batch rebuild made incremental | Unity A/B still owed |
| 10-24 | **Bloomrush 3.5 fps**: one coroutine per waiting prism (57k/frame) → one FIFO `Prism.CreationQueue`; Cell swap release by 12 ms time slice; Menu_Main slowdown round 1 (`QuestTrackView` parallax) | Unity after-number owed |
| 10-25/26 | Charge arcs: closures/arrays removed (0.83 ms, 2.4 KB/f); markers added to `KiteNavigator`, `BlackHoleGravityField`, `StoatPathfinder`; `StoatDipoleMath.PredictPath` loop check blocked (−15–21 %); Squirrel prism pool refill raised (`PoolMiss` 7.5 → 0 KB/f in Amoebius); **`KitRectMask2D`** — masks sleep while hidden (round 2); Third Eye scan no longer walks the scene | Unity after-numbers owed |
| 10-29 | **`PrismRecycler`**: destroyed vessel-wake prisms and natural-death skeletons return to their pools after the death effect (idle lava lamp no longer grows without bound); Menu round 3: sleeping masks park their TMP text; `DailyRewardCard` formats once a second | Unity after-numbers owed |

---

## 5. Where the frame goes today (the known costs, ranked by evidence)

From the lever table (`PERFORMANCE_OPTIMIZATION.md` §3.3) plus the 10-16→10-29 rows:

| Lever | Cost | Status / next step |
|---|---|---|
| **L8** creature body-prism re-sync (S5) | `LightFauna.Update` 13.1 ms avg self, 374 calls | **The pick** — batch: `TransformAccessArray` in Burst, one index pass, one bulk matrix write. **Go/no-go:** `Fauna.BodySync` ≥ 8 ms in a `prof S5` (Amoebius Skim Race reads 0.06 ms, so measure S5 first). Expected −5 ms (4–7). Ecology LOCKED → mover-contract same-frame visibility must hold |
| **L9** S5 gunfight continuations | UniTask `PreLateUpdate` 20.2 ms avg, **123 ms spike = the p99** | Markers exist (`FullAuto.Fire`, `Projectile.*`); the lever is batching every round's flight step into one job instead of ~140 UniTask steps (Garrett's guns). Needs the marker capture |
| **L10** behaviour-tick neighbour scan | 12.7 ms avg, ~0.85 ms/tick | Must keep the ONE diet predicate `Fauna.IsPreyForMe` — no Burst copy of the rule |
| **L1** spindles | +5.3 ms ±0.4 in grown S2 (~0.105 ms per 1,000 renderers); spindle count premise may be stale (2.9k vs ~40k) | LOD built; **Unity S2 A/B `lodEnabled` on/off owed** (T-20261020-fb02), style pick owed (T-20261020-09fe) |
| **L2** growth instantiation | `GrowCoroutine` 15.8 ms spike frame, 5.0 ms `Instantiate` | Recycle flora prisms/spindles; spindles are raw `Instantiate` (`LifeForm.AddSpindle`, `AssembledFlora`, `PhyllotacticFlora`, `BranchingFlora`). Ecology change |
| **L11** collider-LOD sweep hitch (S4) | `LOD.Sweep` 15.9 ms every 0.25 s at 69k | Slice `PrismColliderLodManager.RunSweep` transitions; **never delay a restore near a focus** |
| **L5** cheap known-wrong code | small each | Hijack AI re-plans all on one frame; `AstroLeagueBall` `List.Contains`; `PrismSpatialIndex`'s four `Schedule(_highWaterMark).Complete()` scans with no "anything shielded?" early-out; `ConnectingPanelController.Update` string rebuild; `Boid.cs` allocating `OverlapSphere` (re-grep: line numbers drift) |
| **L3** gameplay GC | 20–53 KB/f (pre-merge 154.5 not reproduced) | `PoolRefill.Squirrel Prism` ~9 KB/f lead (Amoebius) |
| **L4** pure-entity prisms | memory ceiling for 100k on a phone | Designed (Checkpoint D), not built; post-launch unless a scenario demands it |
| **L7** memory | music `DecompressOnLoad` 182 MB resident, texture streaming off | `MEMORY_AUDIT.md` §4: audio import (no code), video audit, texture streaming on |
| **L14** Bloomrush → menu teardown frame | 1.3–1.5 s single frame (Amoebius); can drop the Relay connection | Measure in Unity first (Load Time Insights, `loadKind: "menuReturn"`), then choose: fewer objects (Garrett's call) or destroy waiting prisms in slices behind the return splash |
| Menu UI (`ClipperRegistry.Cull`, `SendWillRenderCanvases`, `UGUI.Rendering.UpdateBatches`) | Cull 3.1–7.7 ms → 0.08–0.56 after round 2; UpdateBatches 1.5–2.2 ms vs 0.6–0.9 baseline | Round 3 (TMP parking) landed, **unmeasured in Unity** |
| `BlackHole.Field` (Stoat dipole gravity) | 1–2.5 ms avg, 5 ms worst in menu; `BoxContacts` p95 2.5 ms | Markers added; suspect `BoxContacts` (bodies × neighbours, up to `BoxMaxMovers` 256 on main thread) — Garrett's prism-motion ladder (T-20261017-9990) |
| `KiteNavigator.Update` (Wrasse kite) | 1.67 ms self | Markers added (`.Board/.Motes/.Shadow*/.Steer/.Fly/.Effects/.Draw`); next capture splits it |
| Breakwater projectiles | ~8 µs per round per frame | Batch the flight step (shared projectile loop, Garrett) — a local reorder was measured dead |

---

## 6. Measured DEAD — do not redo (the record's §3.5)

| Hypothesis | Why dead |
|---|---|
| Prism draw calls are the cost | 0.042 µs CPU per draw; all draw submission ~0.2 ms |
| Re-key `PrismRenderService.GetPrototype` by mesh + material | Would move ~10 % of draws; prisms are 23 draws |
| Chunk spatial locality | 320× locality change moved draws 0.05 % |
| Convert `SnowMaterial` / `SpindleMaterial` to opaque | Snow's surface is baked in its graph; spindles fade by alpha — opaque makes withering pop (continuity law) |
| GC comes from Entities Graphics / prisms / HUD / coroutines | Legacy path allocates more; empty scene allocates more; arithmetic 100× short |
| The stress cloud inflated boot-world numbers | Reproduced without it |
| Reorder `Projectile.SweepPrismsAlong` (lookup after contact test) | Neutral on an interleaved A/B (0.178 → 0.177 ms); cost is per round, not per lookup |
| Size-on-screen (bounding-sphere) spindle LOD | Hid 0 of 2,898 limbs — spindles go sub-pixel by THICKNESS, not size (the current LOD fixes this) |
| Closures in `VesselImpactor` effect dispatch were the allocator | Removed, measured neutral (5.58 → 5.58 KB/f); the allocator was the crystal morph mesh build |
| `WaitCache` as a lava-lamp win | Neutral there (good practice; scales with creature count in S5) |

Also refused on principle: any lever built on a desktop number when the target is a phone, before phone step 0.

---

## 7. Open now on `Ys-bleeding-edge`

### 7.1 The regression board (sweep 2026-10-25; 35 rows; refresh before acting)

Top items, with what the record already says about each — several are **before** numbers for fixes that have
since landed, so a fresh capture may clear them:

| # | Regression | Reading |
|---|---|---|
| 1 | MinigameRegatta (belac, 10-16 15:13) GC 47 → 89 KB/f (+90 %) | Only 2,480 prisms in that run vs 26,945 at baseline — likely a different game state; unattributed |
| 2–4 | MinigameGrizzlyCharge (mrglo, 10-25 15:19) CPU busy 6.5 → 8.8 ms, p95 19.2 → 23.3, **`LateBehaviourUpdate` 0.40 → 2.44 ms (+514 %)** | **Unattributed and new.** Open the capture's `prof` block; candidate late-update drivers are the black-hole/Stoat drivers, `PrismLit.Flush`, `PrismCradle.Flush` and other `Driver.LateUpdate` children (each now has its own marker) |
| 5 | Menu_Main Garland (yskha, 10-26 02:27) `UGUI.Rendering.UpdateBatches` 0.82 → 4.65 ms | Before the 10-26 mask fix and 10-29 TMP-park fix; **after-capture owed** |
| 6 | Menu_Main Garland (belac, 10-22) `BehaviourUpdate` +32 % | Same menu window (style-B menu, pre-fixes) |
| — | Menu_Main S2-4min / Regatta (yskha, 10-25/26) `BlackHole.Tick/Field` +80 %…+80,000 % | The Stoat dipole field (see §5); T-20261017-9990 |
| — | Menu_Main Garland (owner, 10-20/22/23) CPU busy +119…+195 % | The `ClipperRegistry.Cull` menu slowdown, fixed in rounds 1–3 |
| — | Bloomrush (owner, 10-20) 130 → 221 ms | Before the 10-24 `Prism.CreationQueue` fix; re-capture owed (T-20261024-0212) |

The Amoebius Skim Race I2 3-seat run is **within baseline**; top game allocators `PoolRefill.Squirrel Prism`
8.9 KB/f, `VesselImpactor.AcceptImpactee` 3.4, `PoolActivate.Squirrel Prism` 0.7.

### 7.2 Static gates are RED on the current tree (checked 2026-10-29 on `d8fdda23`)

`python3 Tools/Build/perf_hunt.py gates` → **2 failures** (the 10-25 sweep was all OK; these came in with the 48
newer commits):

| Gate | Finding | Fix |
|---|---|---|
| mathf params alloc | `Assets/_Scripts/Controller/Environment/SolitonTunnel/SolitonNetwork.cs:99` — `Mathf.Max(a, b, c)` allocates a `params` array | `MathfNoAlloc.Max(...)` or nest two 2-arg calls |
| console logging | `Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackBlastAI.cs:99` and `:363` — raw `Debug.Log` | `CSDebug.LogVerbose(CSLogChannel.<channel>, ...)` (warnings/errors stay loud via `CSDebug`) |
| per-frame alloc scan (passes, count 41) | `Assets/_Scripts/UI/Kit/KitFocusKeeper.cs:27` — `GetComponentsInChildren` inside `LateUpdate` | Reuse one list (`GetComponentsInChildren(list)`) or cache; Shombith's UI kit |

All three are Yash's / Shombith's recent code — small, local, safe first commits.

### 7.3 New code on `Ys-bleeding-edge` that no capture has measured yet

The 48 commits ahead of `bleeding-edge` add per-frame systems after the last sweep. None has a Unity number:

| System | Files | Instrumentation |
|---|---|---|
| **Fulmen** (lightning bolts on omni-crystal collect, networked) | `Controller/Environment/Fulmen/*`, `FulmenByCrystalEffectSO`, `NetworkVesselImpactor.Fulmen.cs` | Has markers (`FulmenSystem` 3, `FulmenNetSync` 3, `FulmenView` 1); pooled ribbon mesh, per-frame budgets in `FulmenConfigSO` |
| **Prism burn** (5 s laser burn, spread, instanced paper-burn proxies, near/mid/far LOD) | `Controller/Managers/PrismBurnManager.cs` (has an `Update`), `PrismBurnView.cs`, `PrismBurnSpread.cs`, `PrismPaperBurn` shader | **No `ProfilerMarker`** — add `PrismBurn.*` markers before judging it |
| **Switchback Blast Run** (3D block fields, clock-driven movers, Dolphin AI blast planner, per-frame emergency sweep) | `Controller/Arcade/Switchback/*` | **No markers** in `SwitchbackBlastRunRuntime` (has an `Update`), `SwitchbackBlastAI`, `SwitchbackPrismAvoidance` |
| **Soliton tunnels / network** (Meerkat) | `Controller/Environment/SolitonTunnel/*`, `MeerkatSoliton*` | Markers in `SolitonNetwork`, `SolitonTunnelView`, `SolitonTunnelLensPass`, `MeerkatSolitonExecutor` |
| **PrismRecycler** (10-29) | `Controller/Vessel/PrismRecycler.cs`, `Trail.RemoveMembers` | Marker `PrismRecycler.Drain`; ≤ 64 entries a frame |

---

## 8. TODOs (ordered; each is one commit, each states the number it should move)

1. **Refresh the board:** `git fetch origin && python3 Tools/Build/perf_hunt.py sweep --write` (no `--tasks`, §12).
2. **Turn the gates green** (§7.2): `SolitonNetwork.cs:99`, `SwitchbackBlastAI.cs:99/:363`, and the
   `KitFocusKeeper.LateUpdate` allocation. Re-run `perf_hunt.py gates`.
3. **Instrument the unmeasured new systems** (§7.3): `ProfilerMarker`s named `System.Part` in `PrismBurnManager`
   / `PrismBurnView` / `PrismBurnSpread`, `SwitchbackBlastRunRuntime`, `SwitchbackBlastAI`,
   `SwitchbackPrismAvoidance`; add the new names to `diag`'s default marker list (`MarkerBudget`) so captures
   time them; then measure them in Amoebius (allocations) before anyone tunes them.
4. **Attribute the Grizzly Charge `LateBehaviourUpdate` +514 %** from the mrglo 10-25 capture's `prof` block.
   Fix only once a marker names it.
5. **Static-read the per-frame code added in the 48 commits** with the §10 checklist (allocations, `Find*`,
   `GetComponent` in loops, LINQ, closures, per-frame logs, `yield return new` in loops, Burst-hostile code in
   jobs). The 10-06 fast-forward audit is the template (record §2, row 10-06).
6. **L8** only after its go/no-go (`Fauna.BodySync` ≥ 8 ms in `prof S5`). Ecology LOCKED.
7. **L11** collider-LOD sweep slicing (S4) — never delay a restore near a focus.
8. **L5** small items (spatial-index early-out, Hijack re-plan stagger via the `SkimRaceReplanGate` pattern,
   `AstroLeagueBall` `HashSet`, `ConnectingPanelController` string rebuild, `Boid` `OverlapSphereNonAlloc`).
9. **L9 / L10** once the S5 marker capture names the split; both are Garrett's areas — propose, don't push.
10. **Phone step 0** (§9) gates everything aimed at the 100k-on-phone goal; L4 is the structural step after it.

---

## 9. Future tests (owed measurements)

### 9.1 Unity editor (Release, Third Eye closed, Game view focused) — each is "play past the auto-capture, stop Play"

| Test | Judged on | Ticket |
|---|---|---|
| Menu_Main Garland, 4 min | `ClipperRegistry.Cull` (~0.3 ms expected), `Canvas.SendWillRenderCanvases` self, `UGUI.Rendering.UpdateBatches` (back toward 0.6–0.9 ms), `KitRectMask2D.Clip` calls = masks awake | — |
| Menu_Main with the Third Eye **open** | no 0.5 s spike train; `EditorLoop` max a few ms | — |
| Menu_Main with a Stoat making holes | which `BlackHole.GravityField.*` stage costs (`Jobs` / `BoxContacts` / `IndexWrite` / `Contacts`) | T-20261017-9990 |
| Bloomrush past 90 s | `Prism.CreateBlockCoroutine` gone, `Prism.CreationQueue.Drain` small, CPU busy vs 130–221 ms | T-20261024-0212 |
| Bloomrush → Menu_Main return (Load Time Insights, `menuReturn`) | the longest frame; > ~5 s → slice the destroy (L14 b), else fewer objects (a) | — |
| S2 Lattice 8 min, `SpindleLodProfile.lodEnabled` ON vs OFF | CPU/GPU ms, draws, renderer census | T-20261020-fb02 |
| S5 Wildlife Liberation `prof` | `Fauna.BodySync` average (L8 go/no-go), `FullAuto.Fire`, `Projectile.*`, `LightFauna.Tick.PrismScan` | — |
| Skim Race I2 Release `burst` + `diag` + `prof` | Burst fix in effect (no managed job rows); `PoolRefill.Squirrel Prism` lead | — |
| Thresher Time / Regatta | `PoolMiss.Squirrel Prism` → ~0 after the refill-rate raise | — |
| Regatta with a Stoat | `StoatPathfinder.Predict` after the loop-check change | — |
| Wrasse Run | `KiteNavigator.*` split | — |
| An idle AI lava lamp for ~1 hour | GameObject count, `PrismSpatialIndex` high-water mark and memory flat (PrismRecycler) | — |
| S3 Rampage intensity 1 | still owed since 09-26; first check it grows flora/fauna (`cells` command) | — |
| Fulmen / prism burn / Switchback Blast Run / Soliton | first baseline captures once instrumented | — |

### 9.2 Builds and devices (none ever done)

1. **Phone step 0:** Development builds — Android **Vulkan**, Android **GLES3 forced**, iOS — on one MobileLow
   (Adreno 610 / Mali-G52 class) and one MobileHigh device. F7 → `diag phone 15` in S2 and S4, `prisms 50000` then
   `diag`, `renderers`, and the HUD's prism-path line. Commit the JSONs under `Docs/perf/phone/`; the `prismPath`
   field answers instanced vs legacy per device. If any device is `legacy`, the GLES3 decision comes first.
2. Quality presets on an older phone: `diag` GPU ms at High / Medium / Low in the same scene (T-20261021-fa97);
   which preset a MobileLow phone starts on is an open decision (T-20261021-b133).
3. Memory in a **build** (not the editor) at 50k and 100k prisms — sizes L4.
4. Development-build `diag` of S1–S6 on the reference PC, 3 runs each, median — the first numbers that can be
   *judged* against the target.

---

## 10. How to measure (tools, commands, rules)

### 10.1 Scripts that run without Unity

```bash
git fetch origin
python3 Tools/Build/perf_hunt.py gates                          # static allocation/Burst/logging gates
python3 Tools/Build/perf_hunt.py sweep --write                  # gates + every shared capture vs baseline → Docs/Reports/PERFORMANCE.md, Docs/perf/status.json
python3 Tools/Build/perf_hunt.py status --refresh               # the short board
python3 Tools/Build/perf_capture_report.py <capture.json>       # judge one capture (validity first)
python3 Tools/Build/perf_capture_report.py before.json after.json   # before/after
python3 Tools/Build/scan_perframe_allocations.py                # per-frame allocation scan
python3 Tools/Build/check_mathf_params_alloc.py
python3 Tools/Build/check_console_logging.py
python3 Tools/Build/check_conditional_compilation.py            # before any #if UNITY_EDITOR / DEVELOPMENT_BUILD
bash Tools/Build/unity_refcompile/run.sh --config player-dev    # real compile against Unity references (.NET 8+; ~10 min first run)
```

Amoebius (needs .NET 8+): `dotnet build Port/src/CosmicShore.Player -c Release`, then
`python3 Port/tools/amoebius_markers_run.py --mode <Mode>` (per-marker ms, calls, **KB/frame**),
`--compare A B` to diff two runs, or `python3 Tools/Build/perf_hunt.py amoebius --no-build` against
`Docs/perf/amoebius_baselines.json`. Check `Docs/perf/amoebius_known.json` before blaming the game.

Shared Unity captures live on `origin/bugledger/<user>` branches under `BugLedger/shared/reports/<user>/perf/`;
`perf_hunt.py collect` reads them all.

### 10.2 Regression thresholds (`THRESHOLDS` in `perf_hunt.py`)

CPU busy +15 % and ≥ 0.5 ms · p95 +15 % · GC +25 % and ≥ 2 KB/frame · a diag marker +30 % and ≥ 0.2 ms ·
Amoebius allocation +10 % and ≥ 0.05 KB/frame (Amoebius ms only watched, +25 %). Editor noise is ±5–10 %; a
smaller claimed win needs repeats.

### 10.3 In-game console (F7 → console; Editor and Development builds)

`capture <label> 15` (burst + diag + prof + renderers + cells + console + cameras → one JSON) · `diag <label> <s>
[m=Marker.A,Marker.B]` · `prof <label> [frames] [root=…] [sort=self|gc|…]` (Editor) · `renderers` /
`renderers hide *<text>` / `renderers show` · `freeze on|off` (hold ecology production) ·
`ab "<A>" "<B>" 20 6` (counterbalanced same-state A/B with ± error) · `burst` · `prismpath on|off|auto` ·
`prisms <n>` · `fps uncap` · `timescale 0.25`.

### 10.4 Rules that produced wrong numbers when broken

- **Same state:** an A/B is valid only if the arms differ in the thing under test. Growing worlds: `freeze on`
  then `ab`. Hide a *family* with `*Spindle` (lattice spindles wear `Gyroid/Assembly/QuasicrystalSpindleMaterial`)
  and check the hidden count first.
- **A live HUD row is one frame** — use `diag` averages. Quote p50 for speed, p95/p99 for smoothness, CPU busy for
  work; never FPS alone.
- **A measurement describes one tree** — stamp the commit SHA; re-take after a merge.
- **Attribute before you fix**: Profiler/`prof` names the caller; Deep Profile finds WHERE, never HOW MUCH.
- **A load is not a frame** — use Load Time Insights; measure the split before promising a load speed-up; a covered
  screen can hide a *wait* (an animation) rather than work.
- **Frame rate is a gameplay input** (contacts on a 0.04 s fixed step, trails laid once per frame): measure
  frame-rate-sensitive systems at several frame rates.
- **A measurement toggle is not a shipped fix** (`renderers hide` is a probe; a shipped spindle cut must FADE).
- **The Editor runs Mono in double precision at Debug by default**; a Release .NET number does not predict it.

---

## 11. The checklist every performance change is held to

- **Hot path named:** every per-frame cost has a `ProfilerMarker` named `System.Part`, used as
  `using (marker.Auto())`. If the thing you fix has none, add it in the same commit (that is how "after" is measured).
  A `ProfilerRecorder` built in code records nothing unless `StartImmediately` is passed.
- **No per-frame allocation:** no `new`, LINQ, closures, string building, boxing, `params` (3-arg `Mathf.Min/Max`
  → `MathfNoAlloc`), `GetComponent*` or `FindObjects*` in Update, `yield return new WaitFor*` in a loop (use
  `WaitCache`), `foreach` over interface-typed collections.
- **Pool, don't instantiate:** prisms, projectiles, beams, effects come from pools (`PoolRefill`/`PoolActivate`/
  `PoolMiss` markers). `Instantiate` on a per-frame path is a finding.
- **Burst stays Burst:** no `System.MathF` (except `Min/Max/Abs/PI`), managed types, strings or `try` in a job —
  one `MathF.Sqrt` once disabled Burst for every game job. Use `Unity.Mathematics.math`.
- **Render:** `sharedMaterial` + `MaterialPropertyBlock`, never `renderer.material`; instanced path for anything
  numerous. On a renderer other systems also write, do get-modify-set and match their per-index vs renderer-wide
  block convention (`ANTI_PATTERNS.md`).
- **GPU cuts for weaker hardware go through `GraphicsCuts`** (add the field to `PlatformProfileSO` and
  `QualityPresetCutsSO`), never a new per-feature switch; never cut a *functional* visual to zero (cytoplasm shards
  floor 0.1 — they point at the crystal).
- **Physics:** collider budget ≤ ~1,500 active per cell (`PrismColliderLodManager`); NonAlloc query forms; growth
  occupancy via `PrismSpatialIndex.TryReserve`, never `Physics.CheckBox` (fresh prisms have no collider for 0.6 s).
- **Right phase, right rate:** slice sweeps under a per-frame **time** budget; stagger periodic heavy steps across
  actors; per-event over per-frame; nothing logged per frame or per contact, ever.
- **Memory is a phone number:** data in arrays/entities for anything numerous, not a GameObject per thing.
- **Prove it:** before/after in the same state (Unity `freeze on` + `ab`, or two Amoebius runs for allocations);
  write the dated row into `PERFORMANCE_OPTIMIZATION.md` §2 (what, before → after, protocol, commit); move the
  lever row in §3.3 to done or to §3.5 if it measured dead; `perf_hunt.py compare <after.json> --adopt` /
  `amoebius --adopt` so the baseline becomes the new normal. **Never adopt a worse baseline to go green.**

---

## 12. House rules a change must keep (from `CLAUDE.md` and its topic docs)

**Process**
- Work on `Ys-bleeding-edge`-based branches; merge, never rebase/force-push a shared branch.
- `git fetch origin` + `who_works_where.py query <paths>` first. **Never replace, retire, rename or re-tune another
  collaborator's work** without the person you work for confirming; name the owner. Commit trailer:
  `Affects: <person>'s <area>` when you touch someone else's area.
- Conventional commits: `type(scope): summary` (e.g. `perf(menu): …`).
- **Team tickets (GitHub issues labelled `team-task`) are filed only with a human's approval** — do not run
  `perf_hunt.py sweep --tasks` or `team_tasks.py add` on your own, and never assign a ticket to anyone.
- No new packages or dependencies without flagging first. No folder/namespace restructuring unasked.
- No `TODO` comments in place of work; no `Debug.Log` as a fix.

**Verification** — any C# change must be compiled before commit: `/verify-unity` (Unity CLI against an open
editor) where available; otherwise `unity_refcompile` (`player`, `player-dev`, `editor` configs) and say plainly in
the commit/PR that the editor check did not run, and add it to `Docs/UNITY_VERIFICATION_CHECKLIST.md`. Never claim
a verification that did not run; never claim a speed-up without a measurement.

**Code**
- Logging: no raw `Debug.Log` in runtime code; bring-up traces go on `CSDebug.LogVerbose(CSLogChannel.X, …)` (off by
  default), guarded with `CSDebug.IsVerbose` when the message is expensive; no rich text/emoji in logs; warnings
  and errors stay loud.
- `#if UNITY_EDITOR` / `DEVELOPMENT_BUILD`: read `Docs/CONDITIONAL_COMPILATION.md` and run
  `check_conditional_compilation.py` (the Editor always defines `UNITY_EDITOR`, so release-only breakage is invisible).
- Config in ScriptableObjects (budgets, rates, cooldowns), not hard-coded. `[SerializeField] private` +
  `[Header]`/`[Tooltip]`. `TryGetComponent`. Enum members always have explicit numeric values; never take "the next
  free id" without checking `bleeding-edge` and in-flight branches.
- No null-guards on SOAP `ScriptableEvent` fields (fail loud).
- Player-facing text is always an authored serialized field (no C# string literal reaching a `TMP_Text`); the UI font
  has ASCII glyphs only.
- FMOD: every sound is an exposed `EventReference`; never a hard-coded path or temp event.
- **Never hand-edit scene/prefab YAML to "apply" overrides**; shared prefabs (`GameCanvas.prefab`) are single sources
  of truth — a scene override always beats the prefab, and a nulled-reference override silently disables a feature.
- Editor tools live under `FrogletTools/`; no new editor windows/dashboards for performance — extend
  `Docs/perf/status.json` (the badge and Project Record read it).

**LOCKED systems a perf change must not break**
- **Ecology** (`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`, `Docs/ECOSYSTEM.md`): mass conservation, no trail caps
  outside the two recorded exceptions, continuity of existence (things fade, never pop), the collider budget.
- **Prism animation — the clock-material law** (`Docs/PRISM_ANIMATION.md`): no multi-frame CPU animation of prisms
  (no per-frame writes of scale/colour/shader params/positions to play a transition); animation is GPU-clocked.
- **Prism motion** (`Docs/PRISM_MOTION.md`): a moving prism that hits another destroys both; shielded collisions bounce.
- **PvP is petals only**, scoring, party/presence, threading/main-thread affinity, elemental abilities: read the
  doc before touching.
- **World facts are shown in the world in 3D**, never as 2D overlays; nothing in the world looks flat.

---

## 13. Quick map of the relevant code

| What | Where |
|---|---|
| Prism render (instanced companion entities) | `PrismRenderService`, `Prism.EnsureRenderEntity`, `Assets/Resources/PrismRenderConfig.asset`, `EntitiesGraphicsSupportProbe` |
| Prism spatial index (Burst, packed arrays) | `PrismSpatialIndex` (`Docs/SPATIAL_INDEX.md`) |
| Collider LOD | `PrismColliderLodManager` (marker `LOD.Sweep`) |
| Prism creation queue / recycling | `Prism.CreationQueue`, `PrismRecycler`, `Trail.RemoveMembers`, `EnvironmentPrismPool`, `GenericPoolManager`, `Assets/_Prefabs/Environment/PrismManagers.prefab` |
| Creatures | `LightFauna`, `Fauna.NotifyBodyPrismsMoved`, `QuadFishSwimDriver`, `Boid` |
| Spindles / snow | `Spindle.cs`, `SpindleLodSystem` + `Resources/SpindleLodProfile`, `SnowChanger` |
| Gunfight | `FullAutoActionExecutor`, `Projectile.MoveProjectileAsync` |
| Menu UI clipping | `KitRectMask2D` (`Assets/_Scripts/UI/Kit/`), `ScreenSwitcher`, `QuestTrackView` |
| Black holes / Stoat | `BlackHoleRegistry`, `BlackHoleGravityField`, `StoatPathfinderExecutor`, `StoatDipoleMath` |
| Device tiers / quality cuts | `DeviceTierClassifier`, `PlatformProfileSO`, `GraphicsCuts`, `QualityPresetCutsSO` |
| Measurement | `DiagnosticsHUD`, `MarkerBudget`/`MarkerBudgetRecorder`, `PerfAutoCapture`, `BugHuntPerfBadge`, `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md` |
| Sweep / gates | `Tools/Build/perf_hunt.py`, `.github/workflows/perf-daily.yml`, `Docs/perf/*.json` |

---

## 14. Sources this brief was built from

`Docs/PERFORMANCE_OPTIMIZATION.md`, `Docs/Reports/PERFORMANCE.md`, `.claude/skills/perf-hunt/SKILL.md`,
`Docs/MEMORY_AUDIT.md`, `Docs/PLATFORM_UNIFICATION.md`, `Docs/claude/SHADERS_AND_PERFORMANCE.md`,
`Docs/claude/PLUGINS_AND_CONNECTORS.md`, `Docs/claude/ANTI_PATTERNS.md`, `CLAUDE.md`, `Docs/perf/status.json`,
`Docs/perf/amoebius_known.json`, `python3 Tools/Build/team_report.py area performance` (open tickets),
`who_works_where.py query`, and a fresh `perf_hunt.py gates` run on `origin/Ys-bleeding-edge` @ `d8fdda23`.
