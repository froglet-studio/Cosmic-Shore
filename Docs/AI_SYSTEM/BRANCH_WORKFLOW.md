# AI System — Branch Workflow (four branches, kept in sync)

Read this before you merge anything into or out of `ai-system`. It says which branch owns which work,
which way changes flow, how often, and how to resolve the conflicts that come back every time.

Run the sync report first, every time — it lists what is waiting to come in, and which of it is AI work:

```sh
python3 Tools/Build/ai_branch_sync.py            # fetches, then reports
python3 Tools/Build/ai_branch_sync.py --no-fetch # offline / already fetched
```

---

## 1. The four branches

| Branch | Owner and purpose | What `ai-system` does with it |
|---|---|---|
| `bleeding-edge` | The team's integration branch. Everyone's PRs land here, including **new vessel AI written by other people** (Urchin rails, Scarab jukes, Grizzly bombs). | **Pull only.** Never push to it from here. New AI found here is *intake* (§4). |
| `Ys-bleeding-edge` | Ys's integration branch: everything in `bleeding-edge` plus the multiplayer / party / networking work (request discipline, package bumps). | **Pull.** When it is level with or ahead of `bleeding-edge` (the usual case), pulling it brings `bleeding-edge` too, already reconciled with the networking work. |
| `perf/performance-optimization` | Performance testing: the instruments (`prof`, `diag`, `burst`, `freeze`, `ab`), allocation gates, perf fixes (Burst, float loops). | **Two-way.** Pull its tools and fixes into `ai-system`; push AI changes to it when they need perf testing (§3). |
| `ai-system` | **This branch.** Every vessel AI: review, restructure, diagnose, test, tune. Cut 2026-10-08 from `perf/performance-optimization` (which carried all the Skim Race AI work) plus `Ys-bleeding-edge`. | Home. Goes to `bleeding-edge` by pull request when the user decides a batch is ready. |

```
 bleeding-edge ──────────────► Ys-bleeding-edge
       │  (pull, when Ys lags)        │  (pull — usual path)
       ▼                              ▼
   ┌──────────────── ai-system ◄──────────────┐
   │        ▲                                 │
   │        │ tools, perf fixes               │ AI changes that need perf testing
   │        │                                 ▼
   │   perf/performance-optimization ◄────────┘
   │
   └──► bleeding-edge (pull request, user's call)
```

**Ownership rule.** A change belongs on the branch that owns its area, then merges across:

| Area | Owning branch |
|---|---|
| `Assets/_Scripts/Controller/AI/**`, mode/ability AI code, AI configs, `Docs/SKIM_RACE_AI*.md`, `Docs/AI_SYSTEM/**`, the Skim Race simulator | `ai-system` |
| `Assets/_Scripts/Utility/PerformanceBenchmark/**`, `Docs/PERFORMANCE_OPTIMIZATION.md`, allocation / Burst gates | `perf/performance-optimization` |
| Party, presence, lobby, netcode, multiplayer packages | `Ys-bleeding-edge` |
| Everything else (modes, vessels, ecology, UI) | `bleeding-edge` (via its authors) |

A bug found on `ai-system` in someone else's area is fixed here only when the AI cannot be tested
without it, and the commit says so; otherwise it is reported to the owner.

---

## 2. Pulling into `ai-system` (the routine sync)

Do this at the start of every AI session, and whenever the sync report shows new AI work.

1. **Report.** `python3 Tools/Build/ai_branch_sync.py`. Read the "AI commits" lines for each source.
2. **Pick the source.** If `Ys-bleeding-edge` contains `bleeding-edge` (the report says
   `behind bleeding-edge: 0`), merge **only** `Ys-bleeding-edge`. If Ys lags, merge `bleeding-edge`
   first, then `Ys-bleeding-edge`. Merging both when Ys already contains bleeding-edge doubles the
   conflicts for nothing (measured 2026-10-08: 25 conflicts merging bleeding-edge, 8 merging Ys).
3. **Merge, never rebase** (others pull these branches; history is never rewritten):
   ```sh
   git checkout ai-system && git pull --ff-only origin ai-system
   git merge --no-ff origin/Ys-bleeding-edge      # or origin/bleeding-edge
   git merge --no-ff origin/perf/performance-optimization   # when the report shows perf work
   ```
4. **Resolve** with the rules in §5. Never resolve a whole file to one side after other hunks in it
   auto-merged — resolve hunk by hunk.
5. **Verify** (§6). A merge commit message carries the source commits, every conflict and how it was
   resolved, and the verification results. `Docs/AI_SYSTEM/SYNC_LOG.md` gets one row.
6. **Push** `git push -u origin ai-system`. Then do intake (§4) for any new AI the merge brought.

## 3. Exchanging with `perf/performance-optimization`

- **perf → ai-system**: merge perf whenever it lands a tool, a gate, or a fix the AI uses (a new
  console command, an allocation gate, a Burst fix). Do not cherry-pick: a cherry-pick makes the same
  change twice and both copies conflict at the next merge.
- **ai-system → perf**: merge `ai-system` into perf when an AI change must be perf-tested there (a
  planner rewrite, a Burst port, a new AI that runs per frame). This also carries everything
  `ai-system` pulled from Ys/bleeding-edge, which perf wants anyway for realistic tests.
- **Perf findings about the AI** (a `prof` capture showing an AI marker) are written into the AI's own
  doc on `ai-system` (§4 of `DIAGNOSIS_PLAYBOOK.md`), so the AI branch keeps the record.
- Both directions are plain merges; criss-cross merges are fine in git and keep both branches
  pullable by anyone.

## 4. When someone adds a new vessel AI on bleeding-edge

The sync report flags it (AI paths or an "AI"/"autopilot" commit message). After merging:

1. Add a row to the roster in `Docs/AI_SYSTEM/ARCHITECTURE.md` §2 with its layer, files, doc, tests.
2. Run the intake checklist in `Docs/AI_SYSTEM/DIAGNOSIS_PLAYBOOK.md` §3 (input-only gate, markers,
   offline test of its decision math, a manual test script for the user).
3. Record the result in the playbook's status board (§5). Restructuring it to the target architecture
   (`ARCHITECTURE.md` §4) is a separate, later commit — intake never changes behaviour.

The first intake batch (2026-10-08) is the Urchin rail choice in Regatta, the Scarab jukes in Scarab
Scramble and the Grizzly AI bombs.

## 5. Conflict rules (each one paid for by a real merge)

| Conflict | Rule |
|---|---|
| Skim Race AI files, Ys vs perf (Ys carries an older merge of the AI branch) | Take the side that matches the LATEST design (2026-10-08: Ys's `Pilot` on the objective, `Bind(…, objective, handicap)`, `AIDifficultyRules.IsOfferedFor`), and keep perf's mechanisms that side lacks (`SkimRaceReplanGate`). Then grep for members neither side uses any more and delete them. |
| `Docs/UNITY_VERIFICATION_CHECKLIST.md` | Keep **every** entry from both sides, newest first; when both sides edited one entry, take the newer status marker (🟡 over 🔴). |
| A doc table row describing code | Take the side that describes the code as merged. |
| A doc section one side rewrote, the other appended to | Keep the rewrite and append the other side's new paragraphs. |
| `Docs/claude/*.md` rule lists | Union: both sides' rules. |
| Blank-line / comment-only hunks | Either side; prefer the one with the explanatory comment. |
| A file one side deleted and the other edited | Read why it was deleted (`git log --diff-filter=D -1 -- <path>`); a retirement wins unless the edit is a live fix. |
| Generated assets (`Assets/Resources/SkimRaceAIConfig*.asset`, cell configs) | Never hand-merge: regenerate with the generator (`author_skimrace_ai_config.py`, `author_<mode>_assets.py`) and run its `--check`. |

## 6. Verification after every merge

```sh
bash Tools/Build/unity_refcompile/run.sh --quiet-buckets            # player: expect 0 errors in project code
bash Tools/Build/unity_refcompile/run.sh --config editor --quiet-buckets
python3 Tools/Build/check_ai_no_state_writes.py --check
python3 Tools/Build/check_mathf_params_alloc.py
python3 Tools/Build/author_skimrace_ai_config.py --check
python3 Tools/Build/skimrace_track_fingerprint.py --check
python3 Tools/Build/check_using_directives.py --check
python3 Tools/Build/check_enum_member_references.py --check
python3 Tools/Build/check_switch_label_collisions.py --check
python3 Tools/Build/check_console_logging.py
python3 Tools/Build/check_conditional_compilation.py
```

The editor config has carried 4 known errors in untouched files since 2026-10 (`CameraSettingsSOEditor.cs`,
`ResourceDisplay.cs`, `UniversalStatsProviderEditor.cs` ×2); anything else is new. If the merge touched
a file the Skim Race simulator compiles (`Tools/Build/skimrace_sim_harness/run.sh` lists them), re-run a
benchmark and compare with the previous one on the same seeds. `/verify-unity` is the rule for C#
commits when the Unity CLI is available; when it is not, say so in the commit and file the editor steps
in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 7. Things git or the session proxy will not do

- Pushes to `bleeding-edge` / `Ys-bleeding-edge` from an AI session: never. They go by pull request.
- Deleting a remote branch from a cloud session is refused by its proxy (2026-10-07); delete it on
  GitHub's Branches page or from a local machine.
- A push is refused if the target moved while you merged: fetch, merge the new commits, push again.
  Never force-push a shared branch.
