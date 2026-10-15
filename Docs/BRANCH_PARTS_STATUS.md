# Status: the parts of `cece/nice-brahmagupta-ojclij` (test plan 2026-10-12)

**The one place to answer "what is the state of the A–M parts / the 2026-10-12 test plan?".** Update the
tables below in the same commit that lands, tests or drops a part, and change the "as of" line.

**As of 2026-10-14**, measured on `origin/Ys-bleeding-edge` and `origin/bleeding-edge`.

- **Source branch:** `cece/nice-brahmagupta-ojclij` (head `77a6c35b`, 2026-10-13). Thirteen independent parts, A to M.
- **The test plan:** `Docs/BRANCH_TEST_PLAN_2026-10-12.md` **on that branch** (not on this one). It has every
  part's what/benefit/risk and its numbered PASS/FAIL steps.
- **The selective-push tool:** `Tools/Build/pick_branch_parts.py`, also only on that branch. Run it from there
  (`git show origin/cece/nice-brahmagupta-ojclij:Tools/Build/pick_branch_parts.py > /tmp/pick.py`), with
  `--base origin/Ys-bleeding-edge` to build onto this branch.
- **The steps still to run in the editor** for what has landed: `Docs/UNITY_VERIFICATION_CHECKLIST.md`, section
  "From `cece/nice-brahmagupta-ojclij`: CI gates and offline audit tools; Steam …".

## 1. Where each part stands

| Part | What | State | Next action | Who |
|---|---|---|---|---|
| A | CI gates and offline audit tools | **Landed on Ys-bleeding-edge** (7 of 12 commits; 5 superseded, see §2) | Steps 3, 9 on GitHub Actions; steps 4–6 in Unity; announce the generator gate | Anyone with Actions; editor tester; Garrett |
| B | Repo hygiene: untrack FMOD's cache, PR template | Not landed | Sound designer saves, builds and commits `Cosmic Shore.fspro` first, then push B | Sound designer |
| C | Steam dual-app uploader, Playtest runbook | **Landed on Ys-bleeding-edge** (2 of 2) | Step 10 docs read-through; step 11 after Steamworks A2/A4 | Shombith, Garrett |
| D | Plans and docs only | Not landed | Last, with or after the parts it describes; then `/qa-backlog` | — |
| E | AI training fixes, deployed tournament genome | Not landed | Editor steps | Editor tester |
| F | Skim Race AI intensity-4 policy v2-i4 | Not landed | Editor steps | Editor tester |
| G | Load-time target, unattended load sweep | Not landed | Editor steps; needs E (M is in) | Editor tester |
| H | Parity harness, Unity side | Not landed | Editor steps; M is in, so H can go next | Editor tester |
| I | Prisma/Amoebius engine (Port/ only) | Not landed | After H; Port-only push | — |
| J | Multiplayer QoL (friends, recent players, ready lights, invite inbox) | Not landed | Two-account editor steps | Two UGS accounts |
| K | Leak and lifetime fixes | Not landed | Editor steps | Editor tester |
| L | Heritable genome, off by default | Not landed | Editor steps + owner sign-off on the LOCKED rule and worm fission | Editor tester; Garrett |
| M | Engine APIs the game's new code compiles against | **Landed on Ys-bleeding-edge** (2 of 2) | Nothing | — |

Nothing from this branch is on `bleeding-edge` yet: A, C and M reach it with the next Ys-bleeding-edge →
bleeding-edge merge.

## 2. What landed, and how it differs from the plan (2026-10-14, `9d9e7139`)

No Unity editor or `unity` CLI was available, so only the parts the plan marks "no editor needed" were landed.
`/verify-unity` did not run; the only C# in the push is under `Port/`, which Unity never compiles.

**Passed before the push (run in the session):**

- A: step 1 (terminal battery: 0 failures, every generator `--check` OK, 6 skimmer faults, 32 lifeform
  warnings), step 2 (the interpolation-hole negative control), step 8 (autofix list), and all 29 commands of
  the bleeding-edge guard's static job.
- C: steps 1–9, every PASS line matched.
- M: steps 1–3 (Amoebius player build 0 errors; engine tests 1,951 passed, 0 failed; both commits touch only
  `Port/`).

**Left out of A, because Ys-bleeding-edge had already fixed the same things another way:**

- `8c6952942`, `cc81cf2d5`, `a8b1dc65e` (validate_project's string-literal lexer). The base's `15d2f514`
  limits the C# checks to `Assets/` and `Packages/` and **deliberately keeps flagging** a `UnityEditor` string
  literal under `Assets/`; the lexer did the opposite. Plan step 1 therefore prints `25/25 passed`, not `34/34`.
- `e8f7a5557`, `68d502b42` (the AssetTool model-import test). The base pinned the donors' `isReadable` itself.

**Changed while landing:**

- `Tools/Build/check_ai_no_state_writes.py`: call findings now honour the reviewed allow-list by receiver
  (self-tested both ways). One new row: the Third Eye window's `_cam.transform.SetPositionAndRotation`, which
  moves its own camera rig, never a vessel. Without it the gate was red on the base.
- `Docs/BUILD_AND_DELIVERY.md`: C's `--set-live` and Playtest-app routing were merged by hand around the base's
  newer CI rows (the pick tool had kept the base's file and dropped C's fix).

## 3. Open problems not caused by these parts

- **Amoebius test `HandShaderTests.EveryHandTranslation_HasItsAsset_ItsGlsl_AndAUniformPerProperty` fails on
  Ys-bleeding-edge:** `0cb71c0b` deleted `OmniChargeEdgesShader.shader` but left its Port/ hand translation.
- **`bleeding-edge` fails 35 EditMode tests in 14 suites** (Docs/QA/DEV_TASKS.md DT-003); the plan's step-3
  list names them.
- The weekly Sunday AI-training routine still cuts from `cece/nice-brahmagupta-ojclij` until E and I land; the
  Wednesday Skim Race tune reads that branch until F and D land.

## 4. Re-measuring this report

Which part commits are on a branch (cherry-picks change the hash, so match by subject):

```sh
git fetch origin bleeding-edge Ys-bleeding-edge cece/nice-brahmagupta-ojclij
git show origin/cece/nice-brahmagupta-ojclij:Tools/Build/pick_branch_parts.py > /tmp/pick.py
python3 /tmp/pick.py list                       # the parts and their commits
git log --format=%s origin/Ys-bleeding-edge --since=2026-10-08 | grep -F "<a part commit's subject>"
```

When every part has landed (or been dropped), say so here, then delete this file together with the plan and
`pick_branch_parts.py` on the source branch.
