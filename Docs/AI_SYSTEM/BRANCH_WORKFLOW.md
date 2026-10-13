# AI System — Branch Workflow (AI work lives on `Ys-bleeding-edge`)

**2026-10-09: the `ai-system` branch is retired.** Its work (the Skim Race AI retunes, the perf
diagnostics, these docs) was merged into `claude/peaceful-rubin-hhw49n` and from there into
`Ys-bleeding-edge`, and the branch was deleted. Every AI session now works on **`Ys-bleeding-edge`**
(the user's call). The same day `claude/peaceful-rubin-hhw49n` (the Stoat, the black and white
holes, Amoebius, the Vessel Studio) was merged into `Ys-bleeding-edge` and deleted, so that work lives here too.
The rules below are the old ones with that one change; the history is in `SYNC_LOG.md`.

Run the sync report first, every time — it lists what is waiting to come in, and which of it is AI work:

```sh
python3 Tools/Build/ai_branch_sync.py            # fetches, then reports
python3 Tools/Build/ai_branch_sync.py --no-fetch # offline / already fetched
```

---

## 1. The branches

| Branch | Owner and purpose | What `Ys-bleeding-edge` does with it |
|---|---|---|
| `bleeding-edge` | The team's integration branch. Everyone's PRs land here, including **new vessel AI written by other people** (Urchin rails, Scarab jukes, Grizzly bombs). | **Pull.** New AI found here is *intake* (§4). Goes back by pull request when the user decides. |
| `Ys-bleeding-edge` | **Home.** Ys's integration branch: everything in `bleeding-edge` plus multiplayer / party / networking, and now every vessel AI (review, restructure, diagnose, test, tune). | — |
| `perf/performance-optimization` | Performance testing: the instruments (`prof`, `diag`, `burst`, `freeze`, `ab`), allocation gates, perf fixes. | **Two-way.** Pull its tools and fixes; merge Ys into perf when an AI change needs perf testing (§3). |

**Ownership rule.** A change belongs on the branch that owns its area, then merges across:

| Area | Owning branch |
|---|---|
| `Assets/_Scripts/Controller/AI/**`, mode/ability AI code, AI configs, `Docs/SKIM_RACE_AI*.md`, `Docs/AI_SYSTEM/**`, the Skim Race simulator | `Ys-bleeding-edge` |
| Party, presence, lobby, netcode, multiplayer packages | `Ys-bleeding-edge` |
| `Assets/_Scripts/Utility/PerformanceBenchmark/**`, `Docs/PERFORMANCE_OPTIMIZATION.md`, allocation / Burst gates | `perf/performance-optimization` |
| Stoat, black holes, `Port/`, `Docs/Studios/**`, `/vessel-studio` | `Ys-bleeding-edge` |
| Everything else (modes, vessels, ecology, UI) | `bleeding-edge` (via its authors) |

---

## 2. Pulling into `Ys-bleeding-edge` (the routine sync)

1. **Report.** `python3 Tools/Build/ai_branch_sync.py`. Read the "AI commits" lines for each source.
2. **Merge, never rebase** (others pull these branches; history is never rewritten):
   ```sh
   git checkout Ys-bleeding-edge && git pull --ff-only origin Ys-bleeding-edge
   git merge --no-ff origin/bleeding-edge                    # when the report shows work there
   git merge --no-ff origin/perf/performance-optimization    # when perf landed a tool or fix
   ```
3. **Resolve** with §5, hunk by hunk. **Verify** (§6). The merge message carries the sources, every
   conflict and its resolution, and the results; `SYNC_LOG.md` gets one row.
4. **Push** `git push -u origin Ys-bleeding-edge`, then intake (§4) for any new AI the merge brought.

## 3. Exchanging with `perf/performance-optimization`

- **perf → Ys**: merge perf whenever it lands a tool, a gate, or a fix the AI uses. Never cherry-pick.
- **Ys → perf**: merge Ys into perf when an AI change must be perf-tested there.
- **Perf findings about the AI** are written into the AI's own doc on `Ys-bleeding-edge`
  (`DIAGNOSIS_PLAYBOOK.md` §4).

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

- Deleting a remote branch from a cloud session may be refused by its proxy (it was on 2026-10-07);
  then delete it on GitHub's Branches page.
- A push is refused if the target moved while you merged: fetch, merge the new commits, push again.
  Never force-push a shared branch.
