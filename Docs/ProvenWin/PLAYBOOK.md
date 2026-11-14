# Proven Win: one measured, cost-free improvement at a time

Garrett, 2026-11-11: "prove out exactly one change that is a categorical improvement on some well-defined metric
with no observed or predicted cost, measured in real Unity, with an A/B a human can sign off on" - then make that
repeatable. This is the repeatable part, run by the `/proven-win` project skill (`.claude/skills/proven-win/SKILL.md`),
a strict checklist written for the least capable model that will run it. The judgment calls this document describes
are made by tools in `Tools/Build/proven_win/`: `run.py` (every measurement and control, one command), `verdict.py`
(PASS / FAIL / HOLD by the rules below, with a self-test), `signoff.py` (the blind A/B page). Candidates are found
by `/proven-win-scout`. A worked example, measured end to end with the tools: `Docs/ProvenWin/candidates/example-w4-sky-stars/`.

The first win run through it is in `Docs/ProvenWin/WINS.md` (ChargeCrystal envelope cull).

## 0. What counts as a proven win

All five, or it is not one:

1. **One change.** One file or one tightly-coupled set, one idea. Not a PR of three shaders.
2. **One metric, named before measuring**, that a machine reads: GPU ms per batch of the material, vertex
   instructions executed, bytes in the build, allocations per frame, ms per death. Say its unit and instrument.
3. **Categorical:** in paired samples, B is faster in at least 99 % of pairs and the 90th percentile of B/A is below 1,
   while the A-vs-A' null control reads about 1.00 and wins about half its pairs. (A single slow pair in 200 is the
   desktop interrupting, not the change: W4 on D3D12 won 199 of 200 with p90 0.944.)
4. **No cost, observed or predicted:**
   - observed: output identical, in one of three tiers the bench reports: **bit-identical**; **display-identical**
     (zero pixels different at 8 bits); or **within one display level** (no channel of any pixel moves more than 1 of
     255, on a vanishing share of pixels: rounding, not a look change). The third tier ships only with the blind
     sign-off, because Garrett's bar is that "any human observable difference is either insignificant or an
     improvement". Anything that moves a pixel by more than one level is rejected;
     tests still pass, no new allocation, no new warning;
   - predicted: write down every way it could cost (another platform, another graphics API, a code path the
     bench does not hit, a tile GPU, divergence) and either measure it or say plainly it is unmeasured.
5. **A human signs off** on a blind A/B page of the real frames: "no difference I can see" or "B better".

## 1. Pick the candidate (spend 15 minutes, not 2 hours)

Rank by **probability of a clean proof**, not by size:

| prefer | avoid |
|---|---|
| an exact rewrite (early-out proven by an envelope / interval bound, dead code, a constant fold) | an approximation judged by PSNR |
| one file, no C# behaviour change | a scheduler, a new system, a serialized-field change |
| output can be compared bit for bit | output is stochastic or time-integrated |
| something already reasoned offline (`unity-standins/`, `autoopt/`, profiling notes) | a fresh idea with no offline evidence |
| the bench can isolate it (a material, a pure function) | needs a full scene, network, input |

**Measure the state the game draws, not the asset.** Before choosing, find every script that writes the material at
run time (property blocks, tints, `SetFloat`, overlays that replace a pass) and ask the person what the thing looks like
today. W1 was first measured on the Charge crystal as its .mat authors it; the game had replaced that look with an
overlay that holds the old crawl at zero, which made a different, three times larger exact win (WINS.md W1). Put that
state in the spec's `floats` / `colors`.

Sources of candidates, in this repo's order: draft stand-in PRs (#1877-#1880) and their branch tables
(`amoebius-genome/docs/UNITY_STANDINS.md`), the `autoopt/` opportunity list, `Docs/PERFORMANCE_OPTIMIZATION.md`,
the `diag`/`prof` captures, `perf-hunt` findings. Write down the first two runners-up and why they lost; the next
run starts there.

## 2. Isolate

- `git worktree add -b <branch> D:\ABLab\wt origin/bleeding-edge` on the GPU machine. **Never** the person's open
  checkout. Copy a warm `Library` from a worktree of the same build target (robocopy /MT:16); open with that
  `-buildTarget` so nothing reimports.
- Variant files outside the project: `variants/A_*` (as shipped), `B_*` (the change), plus two controls:
  - `A'` = A with a comment-only edit: the **null control**. A and A' must read identical and their timing
    difference is the noise floor.
  - `C` = a deliberately wrong B (an over-tight bound): the **negative control**. The comparison must catch it,
    or the "identical" verdict on B is worth nothing.

## 3. Measure in real Unity on the real GPU

Use the bench (`Assets/_Scripts/Editor/Diagnostics/ProvenWin/ShaderABBench.cs` for a material; write a sibling for
other kinds and keep the same shape): one batchmode process; A and B are cloned side by side into a temporary
`Assets/_ABBenchTemp` (deleted on exit), so no project file is ever edited; async shader compilation off.

```
start "" C:\Unity\6000.3.17f1\Editor\Unity.exe -batchmode -projectPath D:\ABLab\wt -buildTarget Android ^
  -executeMethod CosmicShore.Editor.ProvenWin.ShaderABBench.Run ^
  -abSpec "D:\ABLab\spec_null.json;D:\ABLab\spec_cull.json;D:\ABLab\spec_negctrl.json" -logFile D:\ABLab\logs\dx12.log
```
Then the same with `-force-vulkan` and `-force-d3d11`. Each launch is ~6 min of editor start plus ~1.5 min per spec.

- Correctness: many timepoints and poses into an HDR target, compare bit for bit, max |diff|, 8-bit diff count.
- Cost: PAIRED samples (A then B, order alternating, 200 pairs), each half a chain of command buffers with one GPU
  sync; report the median B/A ratio and the share of pairs B won. Pairing is what makes a busy desktop usable: an
  interruption lands on both halves of a pair or neither.
- Run the three specs: null (A/A'), the change (A/B), negative control (A/C). Read all three.

Traps this cost a day to find (all handled in the bench; keep them handled in any sibling):
- **Batchmode draws magenta.** Nothing has rendered, so URP has not created its pipeline, `Shader.globalRenderPipeline`
  is empty and every `RenderPipeline=UniversalPipeline` SubShader is rejected for the fallback error shader. Render
  one camera first, and refuse a frame that is mostly magenta.
- **An idle GPU times its clock ramp.** Syncing after every small batch let the RTX 3080 drop to 210 MHz between
  batches (p90 15x the median). Warm up with sustained load and chain batches between syncs.
- **AsyncGPUReadback leaks in batchmode.** No frame ever ends, so each request's staging buffer is never freed; after
  a few thousand the D3D12 device fails (`8007000e`) and the editor crashes. Sync with a 1-pixel `ReadPixels`.
- **Swapping the file between rounds is too slow and too noisy.** Rounds minutes apart catch different desktop load
  (one round's median went 5x). Compile both variants at once and interleave.
- **Vulkan can disagree with D3D, and parts can disagree with their sum.** The sky's three early-outs were within one
  display level on D3D12 and D3D11 (about 75 px) but moved 3.3 M pixels by up to 3 levels on Vulkan, while each alone
  was bit-identical or within one level there (2026-11-12). Unity's Vulkan path keeps HLSL `half` as relaxed precision,
  which the driver may run at fp16, so moving code changes results. Measure every API, measure the exact combination
  you will ship, and when a combination fails, split it and ship the parts that pass.
- **Prove the guarded state happens in the bench before believing a "no gain".** For an early-out, run two probes
  first: B returns a constant unconditionally (it must change the picture and the time: the code is reached and
  costs something), and B returns that constant only inside the guard (it must change the picture: the guarded state
  occurs). The creature-body bloom early-out measured 1.00x, and the second probe showed its guard never fired under
  the bench's state, so the 1.00x said nothing about the game (WINS.md, queued).
- **Launch detached** (`start` / `Start-Process` without waiting): a dropped remote session kills a child Unity mid
  swap and leaves the project file changed.
- Then the project's own gates on the branch with only B applied: `bash Tools/Build/unity_refcompile/run.sh`,
  `bash Tools/Build/verify_unity.sh --project D:\ABLab\wt --filter <the area's tests>`.

## 3.5 On a phone

The desktop GPU is not the target that matters most. `ProvenWinDeviceBenchBuilder` builds one development APK from
the same spec files (A and B shaders and their game-state materials compiled in, nothing else of the game), and
`ProvenWinDeviceBench` runs every pair on the phone, writing `provenwin_report.json`:

```
Unity.exe -batchmode -projectPath D:\ABLab\wt -buildTarget Android ^
  -executeMethod CosmicShore.Editor.ProvenWin.ProvenWinDeviceBenchBuilder.Build ^
  -abSpec "D:\ABLab\spec_dev_w1_null.json;..." -apk D:\ABLab\phone\provenwin_bench.apk -logFile D:\ABLab\logs\device_build.log
adb install -r D:\ABLab\phone\provenwin_bench.apk
adb shell monkey -p <package> 1
adb logcat -s Unity | findstr ProvenWin
adb pull /sdcard/Android/data/<package>/files/provenwin_report.json
```

Keep the phone on power, screen on, and run it twice; a tile GPU throttles. Add a phone row to the win in WINS.md.
The build (IL2CPP, ARM64 only) took 38.5 min on Gbox with a warm Library. Its final `ld.lld` link of
`libil2cpp.so` sat near 0 % CPU for several minutes and looks hung; it is not (the first attempt was killed there by
mistake). The Android build rewrites a dozen project files (Gradle templates, FMOD plugin metas, URP and project
settings): revert them in the bench worktree afterwards (`git checkout -- Assets/Plugins ProjectSettings ...`).

## 3.6 In a scene, for "this component stops running"

`ProvenWinSceneBenchBuilder.RunInEditor` (run the editor WITHOUT -batchmode so the Game view renders) instantiates
one real prefab in a temporary scene, enters Play, and `ProvenWinSceneBench` alternates the component enabled (A)
and disabled (B) in ABBA blocks of frames, reading the engine's own counters (PlayerUpdateCanvases, Canvas.BuildBatch,
UGUI.Rendering.UpdateBatches, Main Thread) and comparing a screenshot of each state. The editor quits when the json
is written. It stands the project's SceneBootstrapper aside through its in-process guard, never its EditorPrefs key
(shared with every editor on the machine). `ProvenWinSceneBenchBuilder.Build` makes a Windows player instead, but
only where the Library is already built for Windows: a platform switch reimports everything.

For a cost with no obvious owner (a canvas that re-batches every frame), run the same bench with
`-diagnoseFrames 300` first: it lists, by how many frames they did it, the RectTransforms that moved, the elements in
uGUI's layout and graphic rebuild queues, and the enabled Animators under each root canvas. Those are the A/B
candidates, most frequent first.

## 4. The human A/B

Publish one blind page per win: the real frames A and B under random letters, a flicker toggle, the diff image,
and one question - "Do you see a difference? none / B better / B worse, and where". Votes go to the artifact's
store. Reveal and numbers after the vote. A "B worse" vote stops the win until it is understood.

## 5. Record and hand over

- `Docs/ProvenWin/WINS.md`: one row per win - change, metric, A, B, ratio, rounds won, controls, what is
  unmeasured, sign-off link and result.
- The change ships as its own PR (one change) with the numbers in the body and the owner queued
  (`approvals.py add`). The bench and spec files go with it so anyone can re-run.
- Next: the runner-up from step 1.

## 6. What the bench does not prove

Desktop GPU only (a tile GPU's early-Z and bandwidth behave differently); one graphics API per run (re-run with
`-force-vulkan` / `-force-d3d11` to widen); the material in isolation, not its share of a real frame. A per-pixel
saving is real; its frame-level weight is (pixels the material covers) x (saving per pixel), which is small for a
small object. Say so in the row.

## 7. The loop that keeps itself going

- One queue: `QUEUE.json` (view `QUEUE.md`), changed only by `Tools/Build/proven_win/queue.py`. The scout `add`s,
  `/proven-win` `take`s, every candidate ends with `done` (won, rejected or inconclusive) and a required lesson.
- Refill: `/proven-win` step 1 runs the scout whenever fewer than 3 are queued.
- Learning: every lesson goes into `LENSES.md`, which the scout reads first; three lessons saying the same thing are
  folded into their lens.
- Staleness: each entry records its files' hashes; `queue.py stale` sets aside any entry whose code moved, and `take`
  refuses one.
- Schedule: the desktop app's scheduled task `proven-win-weekly` (Sundays 02:00 on Gbox) runs scout + one
  `/proven-win` in `D:\ABLab\wt`, opens a PR for an identical PASS, publishes a sign-off page for a one-level PASS,
  and records failures. It never merges: the person does.

