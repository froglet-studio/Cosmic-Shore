# AI System — Branch Workflow (AI work lives on `ai`)

**2026-10-28: `ai` is the one branch for all AI work** (Shombith, head of tooling). It replaced `Ys-bleeding-edge`, which
was the AI home from 2026-10-09 (when the `ai-system` branch was merged into it and retired) and also served as Yash's
integration branch for any area. **No personal integration branch any more**: AI work builds on `ai` and reaches
`bleeding-edge` by PR; everything else goes on its own short-lived branch with a PR straight into `bleeding-edge`.
`Ys-bleeding-edge` is left in place; Yash brings its unmerged commits to `bleeding-edge` himself. The history is in
`SYNC_LOG.md`.

Run the sync report first, every time — it lists what is waiting to come in, and which of it is AI work:

```sh
python3 Tools/Build/ai_branch_sync.py            # fetches, then reports
python3 Tools/Build/ai_branch_sync.py --no-fetch # offline / already fetched
```

---

## 1. The branches

| Branch | Owner and purpose | What `ai` does with it |
|---|---|---|
| `bleeding-edge` | The team's integration branch. Everyone's PRs land here, including **new vessel AI written by other people** (Urchin rails, Scarab jukes, Grizzly bombs). | **Pull, then PR back.** Merge the latest `bleeding-edge` INTO `ai` (a merge commit) before and during work. New AI found here is *intake* (§4). AI work goes from `ai` to `bleeding-edge` by pull request. |
| `ai` | **Home.** Every vessel AI (review, restructure, diagnose, test, tune) and the AI learning sweep's output. | — |
| `perf/performance-optimization` | Performance testing: the instruments (`prof`, `diag`, `burst`, `freeze`, `ab`), allocation gates, perf fixes. | **Two-way.** Pull its tools and fixes; merge `ai` into perf when an AI change needs perf testing (§3). |

**Ownership rule.** A change belongs on the branch that owns its area:

| Area | Where it goes |
|---|---|
| `Assets/_Scripts/Controller/AI/**`, mode/ability AI code, AI configs, `Docs/SKIM_RACE_AI*.md`, `Docs/AI_SYSTEM/**`, the Skim Race simulator, the AI learning sweep's results | `ai` (a short-lived branch off `ai` with a PR into it is fine too), then a PR into `bleeding-edge` |
| `Assets/_Scripts/Utility/PerformanceBenchmark/**`, `Docs/PERFORMANCE_OPTIMIZATION.md`, allocation / Burst gates | `perf/performance-optimization` |
| Everything else (multiplayer, modes, vessels, ecology, UI, studios, `Port/`) | its own short-lived branch off `bleeding-edge`, with a PR straight into `bleeding-edge` |

**No personal integration branch** (Shombith, 2026-10-28). Before that, Yash's sessions built every feature on
`Ys-bleeding-edge` first and promoted it later (2026-10-25); that rule is retired.

---

## 2. Pulling into `ai` (the routine sync)

1. **Report.** `python3 Tools/Build/ai_branch_sync.py`. Read the "AI commits" lines for each source.
2. **Merge, never rebase** (others pull these branches; history is never rewritten):
   ```sh
   git checkout ai && git pull --ff-only origin ai
   git merge --no-ff origin/bleeding-edge                    # when the report shows work there
   git merge --no-ff origin/perf/performance-optimization    # when perf landed a tool or fix
   ```
3. **Resolve** with §5, hunk by hunk. **Verify** (§6). The merge message carries the sources, every
   conflict and its resolution, and the results; `SYNC_LOG.md` gets one row.
4. **Push** `git push -u origin ai`, then intake (§4) for any new AI the merge brought.

## 3. Exchanging with `perf/performance-optimization`

- **perf → ai**: merge perf whenever it lands a tool, a gate, or a fix the AI uses. Never cherry-pick.
- **ai → perf**: merge `ai` into perf when an AI change must be perf-tested there.
- **Perf findings about the AI** are written into the AI's own doc on `ai` (`DIAGNOSIS_PLAYBOOK.md` §4).

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
| Generated assets (`Assets/Resources/SkimRaceAIConfig*.asset`, `Assets/Resources/AIBoostPolicies/*_Regatta.asset`, cell configs) | Never hand-merge: regenerate with the generator (`author_skimrace_ai_config.py`, `learn_drives_from_flights.py --write`, `author_<mode>_assets.py`) and run its `--check`. |

## 6. Verification after every merge

```sh
bash Tools/Build/unity_refcompile/run.sh --quiet-buckets            # player: expect 0 errors in project code
bash Tools/Build/unity_refcompile/run.sh --config editor --quiet-buckets
python3 Tools/Build/check_ai_no_state_writes.py --check
python3 Tools/Build/check_mathf_params_alloc.py
python3 Tools/Build/author_skimrace_ai_config.py --check
python3 Tools/Build/learn_drives_from_flights.py --check
python3 Tools/Build/author_regatta_assets.py --check
python3 Tools/Build/skimrace_track_fingerprint.py --check
python3 Tools/Build/check_using_directives.py --check
python3 Tools/Build/check_enum_member_references.py --check
python3 Tools/Build/check_switch_label_collisions.py --check
python3 Tools/Build/check_console_logging.py
python3 Tools/Build/check_conditional_compilation.py
```

The editor config carries 3 known errors (since 2026-10-15), all in `AuthenticationServiceFacade` on
`com.unity.multiplayer.playmode`, a package the container cannot fetch; anything else is new. (Before that it was 4, in
`CameraSettingsSOEditor.cs`, `ResourceDisplay.cs` and `UniversalStatsProviderEditor.cs` ×2.) If the merge touched
a file the Skim Race simulator compiles (`Tools/Build/skimrace_sim_harness/run.sh` lists them), re-run a
benchmark and compare with the previous one on the same seeds. `/verify-unity` is the rule for C#
commits when the Unity CLI is available; when it is not, say so in the commit and file the editor steps
in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 7. Things git or the session proxy will not do

- Deleting a remote branch from a cloud session may be refused by its proxy (it was on 2026-10-07);
  then delete it on GitHub's Branches page.
- A push is refused if the target moved while you merged: fetch, merge the new commits, push again.
  Never force-push a shared branch.
