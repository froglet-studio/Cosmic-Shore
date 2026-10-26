# AI System — Branch Workflow (Yash's work lives on `Ys-bleeding-edge`)

**Only sessions Yash drives use `Ys-bleeding-edge` (Shombith, 2026-10-26).** It is Yash's integration branch. Every
other session (Garrett's, Caleb's, Shombith's, anyone's), in every area including AI, multiplayer, `Port/` and the
studios, branches from `bleeding-edge`, opens its PR into `bleeding-edge`, and never pushes to, merges into or opens a
PR into `Ys-bleeding-edge`. Those sessions still READ it (`who_works_where.py`, and before claiming an enum value, a
`GameModes` id or a GUID) so they do not redo Yash's work. A session is Yash's when Yash drives it: it runs on his
account (an email `Docs/TEAM.json` lists for him), it continues a branch he started off `Ys-bleeding-edge`, or he asks
for `Ys-bleeding-edge` in the chat. The scheduled sweeps whose own docs push to it (the AI learning sweep,
`AUTO_LEARNING.md`) work for him too, and promoting it (`/ship promote Ys-bleeding-edge`) is his call. When in doubt,
a session is not his, and it uses `bleeding-edge`. Everything below is the workflow of Yash's sessions.

**2026-10-09: the `ai-system` branch is retired.** Its work (the Skim Race AI retunes, the perf
diagnostics, these docs) was merged into `claude/peaceful-rubin-hhw49n` and from there into
`Ys-bleeding-edge`, and the branch was deleted. Every AI session Yash drives now works on **`Ys-bleeding-edge`**
(his call). The same day `claude/peaceful-rubin-hhw49n` (the Stoat, the black and white
holes, Amoebius, the Vessel Studio) was merged into `Ys-bleeding-edge` and deleted, so that work lives here too.
The rules below are the old ones with that one change; the history is in `SYNC_LOG.md`.

Yash's sessions run the sync report first, every time — it lists what is waiting to come in, and which of it is AI work:

```sh
python3 Tools/Build/ai_branch_sync.py            # fetches, then reports
python3 Tools/Build/ai_branch_sync.py --no-fetch # offline / already fetched
```

---

## 1. The branches

| Branch | Owner and purpose | What `Ys-bleeding-edge` does with it |
|---|---|---|
| `bleeding-edge` | The team's integration branch. Everyone's PRs land here, including **new vessel AI written by other people** (Urchin rails, Scarab jukes, Grizzly bombs). | **Pull only.** Merge the latest `bleeding-edge` INTO `Ys-bleeding-edge` (a merge commit) before and during work. New AI found here is *intake* (§4). **Nothing goes from `Ys-bleeding-edge` to `bleeding-edge` until the user has tested it** (Yash, 2026-10-15); then by pull request, when the user decides. |
| `Ys-bleeding-edge` | **Yash's home.** Ys's integration branch: everything in `bleeding-edge` plus Yash's multiplayer / party / networking work, and his vessel AI work (review, restructure, diagnose, test, tune). Only Yash's sessions build here. | — |
| `perf/performance-optimization` | Performance testing: the instruments (`prof`, `diag`, `burst`, `freeze`, `ab`), allocation gates, perf fixes. | **Two-way.** Pull its tools and fixes; merge Ys into perf when an AI change needs perf testing (§3). |

**Ownership rule (Yash's sessions only).** In a session Yash drives, a change belongs on the branch that owns its area,
then merges across. Every other session puts every area, these included, on `bleeding-edge`; this table does not
apply to it:

| Area | Owning branch (Yash's sessions) |
|---|---|
| `Assets/_Scripts/Controller/AI/**`, mode/ability AI code, AI configs, `Docs/SKIM_RACE_AI*.md`, `Docs/AI_SYSTEM/**`, the Skim Race simulator | `Ys-bleeding-edge` |
| Party, presence, lobby, netcode, multiplayer packages | `Ys-bleeding-edge` |
| `Assets/_Scripts/Utility/PerformanceBenchmark/**`, `Docs/PERFORMANCE_OPTIMIZATION.md`, allocation / Burst gates | `perf/performance-optimization` |
| Stoat, black holes, `Port/`, `Docs/Studios/**`, `/vessel-studio` | `Ys-bleeding-edge` |
| Everything else (modes, vessels, ecology, UI) | `Ys-bleeding-edge` too (below); other people's work in these areas reaches it through `bleeding-edge` |

**Yash's sessions build on `Ys-bleeding-edge` first, whatever the area (Yash, 2026-10-25).** "I'm changing features and
adding them to Ys-bleeding-edge, testing them first and then deciding whether to push it or not in bleeding edge." So a
session Yash drives, on any feature (a menu flow, a mode, a vessel, not only AI or multiplayer):

- branches from `Ys-bleeding-edge` (or merges it into the working branch before the PR) and opens its PR **into
  `Ys-bleeding-edge`**;
- **never pushes to `bleeding-edge` and never opens a PR into it**, `/to-bleeding-edge` included, unless Yash asks for
  that promotion in the chat;
- leaves the promotion to Yash, who tests on `Ys-bleeding-edge` and then decides.

(The first change this would have caught: #1529, the return-to-launch-panel flow, went straight to `bleeding-edge`; its
doc follow-up #1537 was retargeted to `Ys-bleeding-edge`.)

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
