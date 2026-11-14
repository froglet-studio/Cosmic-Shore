---
name: ship
description: End-of-branch shipping protocol - review everything on the branch, prove any editor tool's ASSET OUTPUT actually landed (§2.5, never skipped), complete the documentation so the work is ready to build from, feed what any LAB on the branch taught back into the /labmaker skill (§3.55), make an honest go/no-go call (pushing back with a concrete iteration list when the branch needs another pass), and only then open the pull request - and on a GO, merge it into bleeding-edge (§5.7; Garrett 2026-11-10: batch-test on bleeding-edge and fix what does not work). Use when a feature branch feels done ("wrap this up", "open the PR", "ship it"), before ANY pull request is created, or at the end of a long working session. Depth variants: /ship-quick (fast), /ship-deep (thorough), /ship-tools (tool output + retirement only). Pairs with /reorient (run it first when the session has run long or bleeding-edge may have moved). Starts with a 1-second triage (Tools/Build/ship_triage.py) that names the sections and traps this branch needs, runs the gates in parallel in the background, and ends by offering to hand the swim lane off (/handoff-to-<person>, /handoff-open, /handoff-for-test; §6.5).
---

# Ship Protocol — review, document, decide, PR, merge

You are closing out a feature branch. The goal is a pull request another developer can
review, build on, and trust — merged into `bleeding-edge` on a GO (§5.7), where the team
batch-tests it — or an honest "not yet" with a concrete list of what one or
two more iterations should fix. **Opening the PR is the second-to-last step, never the first; merging it is the last.**

## How to read this file (fast)

**Do not read every line of every reference on every ship** (Shombith, 2026-11-13). This file is the procedure. The
lessons past ships learned live in `references/`, word for word, and are read only when the branch needs them:

| File | Read when |
|---|---|
| `references/MERGE_TRAPS.md` | the trial merge conflicts, or bleeding-edge changed files you changed (the triage names the traps) |
| `references/REVIEW_TRAPS.md` | §2, the traps the triage names for your file kinds (`/ship-deep`: all) |
| `references/VERIFY.md` | a gate exits non-zero, or you write what `unity_refcompile` proved |
| `references/LAB_CAPTURE.md`, `COMPUTE_CAPTURE.md`, `FLEET_CODEX.md` | the triage says LAB, COMPUTE or CODEX |
| `references/REFACTOR_PASS.md` | `/ship-deep`, or you have a §3.6 row to write |

## 0.0 Triage first (one second)

```bash
python3 Tools/Build/ship_triage.py
```

It reads the diff from the merge base and a trial merge of bleeding-edge (`git merge-tree`: no work tree, no index)
and prints the depth the branch qualifies for, the conflicts and the merge traps that match them, whether C# changed,
the editor tools §2.5 must classify, the review traps for these file kinds, whether LAB / COMPUTE / CODEX apply, and
the swim lane the change is about. **Follow it: read what it names and skip what it rules out**, and say in the report
what was skipped and why (one line each). Two STOP lines override everything: more than one merge base (unshallow
first), and the depth (a change it says is not quick never ships through `/ship-quick`).

**Then start the gates in the background at once** (`bash .claude/skills/ship/gates.sh` with the shell's
background option, or `ship_gates.py --changed` for a first look while you work). They run in parallel, one per CPU (a
clean tree measured 3 min 14 s one at a time and 57 s in parallel on 4 CPUs, 2026-11-13, same verdicts), and the
reference compile runs beside them. Review while they run; re-run them on the merged tree after §1.5 if the merge
brought anything in.

## 0. Pick the depth

| You want | Use | What changes |
|---|---|---|
| The default, full protocol | `/ship` | Everything below. |
| A small, already-reviewed branch out the door | `/ship-quick` | Trims the §2 review and §3 doc passes. **Never** trims §2.5, and keeps §3.55's minimum (the lab's CATALOG row plus any costly lesson). |
| A big branch, a LOCKED system, or a long session | `/ship-deep` | Adds an adversarial re-read, a blast-radius sweep, a doc-drift sweep, a mechanical refactor-opportunity sweep (D8) over §3.6, and a lab audit (D9) over §3.55. |
| Only to land an editor tool's OUTPUT (no PR) | `/ship-tools` | Runs §2.5 alone, then retires the tool and pushes. |
| PROMOTE an integration branch that was already reviewed PR by PR (Yash's `Ys-bleeding-edge`) | `/ship promote [branch]` | Runs §1.5 (merge bleeding-edge in, resolve, gates, push) and §5-§5.7 (the PR, CI, the Fleet Codex, the merge) and nothing else - no review, no go/no-go. Was `/to-bleeding-edge`, folded in 2026-10-26 (Garrett: one shipping process, "to avoid confusion, drift"); that name stays as an alias. |

`/ship <mode>` works too (`/ship quick`, `/ship deep`, `/ship tools`, `/ship promote <branch>`).

**Every mode that opens a PR merges it on a GO (§5.7).** To open the PR and stop there, say so (`/ship --no-merge`,
"PR only", "don't merge"); the session then leaves the merge to a person, as before 2026-11-10.

**§2.5 is in every mode. Speed comes out of review depth, never out of the gate** — a
fast path that can silently drop a tool's output is the exact failure this protocol
exists to prevent.

## 0.05 The verify rule — what gates a C# commit (and what not to spend the branch's time on)

**The verify rule (Shombith's decision, 2026-10-29): every C# commit passes `Tools/Build/unity_refcompile` (`bash Tools/Build/unity_refcompile/run.sh`) plus whatever `/ship` already gates on (`bash .claude/skills/ship/gates.sh`). When a Unity editor is open, also run the `unity` CLI. If neither ran, say so in the commit/PR and add an entry to `Docs/UNITY_VERIFICATION_CHECKLIST.md`.** Never claim a verification that didn't run. `/verify-unity` is not required until that skill merges.

This is the same rule, in the same words, as `CLAUDE.md` § "Unity CLI verification"; if the two
ever differ, fix both. It holds in every mode (`/ship`, `/ship-quick`, `/ship-deep`, `/ship-tools`).
What it means in practice:

- **`gates.sh` runs `unity_refcompile` for you when `~/.dotnet/dotnet` exists, and prints `SKIP`
  when it does not.** A `SKIP` means refcompile did NOT run — it is not a pass. First run needs
  network and about ten minutes; how to run it and its false positives: `/asset-surgery` §4.
  **No `~/.dotnet` and the installers refused by the proxy** (dot.net, builds.dotnet.microsoft.com and GitHub releases
  are, in cloud sessions): `python3 Tools/Build/unity_refcompile/dotnet_from_nuget.py` lays one out from nuget.org
  packages in about a minute, and the SKIP becomes a real compile (2026-11-07). In such a sandbox the player config
  reports 21 errors in `FactionChatService.cs` and `PlayerReports.cs` (Vivox and Moderation cannot be fetched) and
  `0 in files changed`: identical on a clean bleeding-edge worktree, so the gate's FAIL there is the sandbox's, not the
  branch's. A/B it once and say so; anything beyond those two files is real.
- **Most sessions here have no Unity Editor**, so the `unity` CLI step usually does not apply. When
  neither refcompile nor the CLI ran, the verification line in the PR body and ship report reads
  "not compiled — <why>", followed by what a human must do at the editor, and the change gets a
  `Docs/UNITY_VERIFICATION_CHECKLIST.md` entry. An honest "unverified" beats a manufactured green.
- **Do not stand up a syntax-only compiler instead.** Installing an SDK to Roslyn-parse the diff
  buys a SYNTAX check and nothing more — every `MonoBehaviour` and `ScriptableObject` base type
  lives in the `Assembly-CSharp` monolith, so class bodies never bind and the errors that actually
  bite (a missing member, a drifted override signature, an argument mismatch, overload resolution)
  are invisible to it. `unity_refcompile` is the tool that binds them.
- **The individual `check_*.py` scripts are not extra gates beyond `gates.sh`.** Run one by hand
  only when it is the cheapest way to answer a question about your own change.
- **Do not wait on, poll, or report CI.** `unity-ci.yml` has had no Unity runner since
  2026-10-13 (its `unity` job is skipped), so a green check there is not a compile. Do not subscribe
  to a PR to watch checks. The one CI action a ship command takes is to FIRE it, once, at the end
  (§5.5) — `claude/**` branches trigger nothing on their own.
- **Before reporting any non-zero exit, read `references/VERIFY.md`**: a wrong flag exits 2 (`usage:` means you called it wrong), a pipe hides the script's exit code (read it bare or `${PIPESTATUS[0]}`), a `--check` only ever seen to pass is untested (negative-control it). It also says what a green `unity_refcompile` lets the PR claim, and that **a ship never builds, tests or patches Amoebius** (the three-weekly Amoebius update does). **§2.5 needs none of these: it runs in full in every mode.**

## 0.1 Reorient first (when in doubt)

If the session has run long, or bleeding-edge may have moved since the branch was cut,
run the `/reorient` skill first and act on its verdict before shipping.

## 1. Survey the branch

- `git log --oneline <base>..HEAD` and `git diff --stat <base>..HEAD`. **`<base>` is the
  MERGE BASE, not the base branch's tip** — `git merge-base origin/bleeding-edge HEAD`.
  Diffing against the tip of a branch that moved while you worked reports every commit
  THEY landed as deletions in YOUR diff, which reads as a catastrophic branch and is
  entirely an artefact. Read the full commit list; re-read any diff hunk you can't
  summarize from memory.
- **Before believing any staleness number or conflict count, run `git merge-base --all origin/bleeding-edge HEAD`.** More than one base: the clone is shallow, `git fetch --unshallow` and re-measure (`references/MERGE_TRAPS.md` T1). Scene or prefab conflicts: per OBJECT, never per line (`/asset-surgery`, `Tools/Build/merge_unity_scene.py`).
- **Merge the base branch in before reviewing** (§1.5), so you resolve conflicts rather than
  leaving them for a reviewer, and so §2 reviews the tree that will actually land.

## 1.5 Merge bleeding-edge in, resolve, gate, push (every mode that opens a PR; the whole of `/ship promote`)

`$BRANCH` is the branch being shipped: the current one, or the argument of `/ship promote <branch>`. Stop and
ask if it is empty (a detached HEAD), `bleeding-edge` itself, or a protected branch (`main`, `development`).
A branch reaches `bleeding-edge` **only through a pull request** - it is Garrett's branch, protected, and the
`Bleeding-edge guard` workflow reviews what lands there - and all conflict resolution happens on `$BRANCH`, so
the PR always merges cleanly.

**Fetch and look.**
```bash
git fetch origin bleeding-edge "$BRANCH"
git rev-list --left-right --count origin/bleeding-edge...origin/$BRANCH   # "<behind> <ahead>"
git log --no-merges --format='%h %an %s' origin/bleeding-edge..origin/$BRANCH
```
- The remote `$BRANCH` may be gone: GitHub deletes a head branch when its PR merges. Local commits not yet in
  bleeding-edge: push them, which recreates it. None: nothing to promote. To restart it from bleeding-edge keeping
  unmerged local work: `git checkout -B "$BRANCH" origin/bleeding-edge`, then cherry-pick or merge the work back.
- `ahead = 0` means nothing to promote: say so and stop.
- Check out `$BRANCH` and fast-forward it (`git merge --ff-only origin/$BRANCH`). Never rebase or force-push a
  shared branch: other sessions push all day.
- **If `$BRANCH` is someone else's branch, confirm with the person before pushing a merge onto it**
  (`who_works_where.py` shows who drove it).
- **Other sessions promote the same branch at the same time** (Ys-bleeding-edge, 2026-10-26: three sessions merged
  bleeding-edge in and opened PRs within the hour, twice). Before merging, list the open PRs from `$BRANCH` into
  bleeding-edge; one exists means take it over and refresh it, never open a second (`create_pull_request`
  answering "A pull request already exists" means the same). Re-fetch right before you merge and again before you
  push: if `origin/$BRANCH` already carries the same bleeding-edge merge, drop yours (`git merge --abort`, or
  `git reset --hard origin/$BRANCH` when nothing local is unpushed), gate the remote tree and carry on from it;
  stacking a second identical merge only doubles the conflicts the next promote reads.

**Merge bleeding-edge in and resolve every conflict keeping both sides' intent** - the two branches are
different people's work.
```bash
git merge --no-ff --no-commit origin/bleeding-edge
git diff --name-only --diff-filter=U        # the conflicted files
```
- Before choosing a side, read both sides' commits: `git log --oneline origin/bleeding-edge -- <file>`, and the
  same on `$BRANCH`.
- **Generated files**: regenerate them with their tool, never hand-merge (`Docs/perf/status.json`, the
  who-works-where map, studio previews, cards, any `author_*` output).
- **Shared numbers** (enum values, `GameModes` ids, GUIDs, Netcode hashes): if both sides claimed the same one,
  stop and ask the person. Renumbering serialized values breaks assets.
- **Scenes and prefabs**: per OBJECT, never per line (§1 above, `/asset-surgery`); never hand-merge YAML
  overrides you do not understand - take one side whole and name in the PR what the other side changed so its
  owner re-applies it, or ask.
- **Docs and logs** (`Docs/TEAM_TASKS.md`, `UNITY_VERIFICATION_CHECKLIST.md`, sync logs): keep every row from
  both sides.
- **Same logic changed on both sides**, where picking either loses behaviour: ask the person.
- Commit: `merge: bleeding-edge (<short sha>, #<PR>) into <$BRANCH> before the PR`, listing each conflict and
  how it was resolved, ending with the attribution lines.

**Gates on the merged tree.**
```bash
bash .claude/skills/ship/gates.sh     # run it in the background and review meanwhile; it prints the same lines in order
```
The gate LIST is `Tools/Build/ship_gates.py` (since 2026-11-06; `gates.sh` only calls it, and the Unity Source Control
window runs the same file on Windows, `Docs/SOURCE_CONTROL.md`): add a gate there, never to `gates.sh`.
It runs the static checks `bleeding-edge-guard.yml` runs, every `author_*` generator's `--check` (in parallel since
2026-11-13; `--jobs 1` for one at a time), and the headless reference compile on its own thread only when a .NET SDK is already present (it prints SKIP otherwise - §0.05 stands:
never stand a compiler up for a ship, and a SKIP is not a failure). Player config must report `RESULT: OK`;
editor config gates only files changed since bleeding-edge (its standing 3 errors in
`AuthenticationServiceFacade.cs` come from a package that cannot be fetched). Fix anything red on `$BRANCH`
before the PR - the root cause, never a skipped check. Read exit codes as §0.05 says (bare, not through a pipe).

**A clean merge can still break a reference.** One side deletes an asset while the other adds a reference to it, and git
sees no conflict (2026-10-27: Ys-bleeding-edge removed the Omega crews; bleeding-edge's new Hangar card art listed the
Warden). The `generated_assets` gate names the dangling guid. Re-run the generator that owns the referencing asset on the
merged tree; hand-delete only files that generator no longer emits, once a guid search shows nothing references them.

**Amoebius is not rebuilt here.** The three-weekly scheduled Amoebius update owns the engine build (see §0.05).

**Trunk gate: is bleeding-edge green?** (every mode that pushes or merges; /trunk-doctor §6)
```bash
python3 .claude/skills/trunk-doctor/trunk_doctor.py --gate
```
Exit 2 means `bleeding-edge` is red. **Stop.** Tell the person it is red, why (the gate names the first red PR and the
issue), and point to `/trunk-doctor`. Push or merge only if this change IS the fix (rerun with `--fix`; a
`trunk-doctor/*` branch passes by itself) or if the person says to go ahead anyway, and record that in the PR. Exit 3
means unknown: say so and carry on. Why: 2026-11-09 to 11-12, 31 PRs merged onto a red trunk while the local gates
stayed green (`Docs/Reports/TRUNK_HEALTH.md`).

**Push.** `git push -u origin "$BRANCH"`. Rejected because someone pushed meanwhile: `git fetch origin
"$BRANCH"`, `git merge origin/$BRANCH`, re-run the gates if the new commits touched C#, push again. Network
failures: retry 4 times, waiting 2 s, 4 s, 8 s, 16 s.
  **Fetch the base again right before you open the PR**: if `git merge-base origin/bleeding-edge HEAD` is not the tip, merge again (it is a second merge, not a formality). **Conflicts, or a clean merge in files your branch also changed: read the index of `references/MERGE_TRAPS.md`** (shared enum values, doc section numbers, "keep both" inside a chain, add/add `.meta` GUIDs, a parallel branch that fixed the same root cause, rotted `file:line` citations) and open only the traps that match. `ship_triage.py` runs the trial merge and names them.

## 2. Review pass (the branch, not just the last commit)

Walk every changed file against these gates:

- **Correctness**: no leftover debug scaffolding, dead code, TODO-as-substitute-for-work,
  commented-out experiments, or hand-authored YAML that never got an editor import pass.
- **Architecture**: changes follow CLAUDE.md patterns (SOAP, config-in-SOs, single-writer,
  no new singletons, threading contract, ecology invariants). A violation is a blocker.
- **Blast radius**: grep for every public API you changed or removed - every caller
  migrated? Renamed/deleted assets - every GUID reference updated?
- **Verification honesty**: list what was actually verified (in-editor play, tests) vs.
  what only compiles-by-inspection. Unverified risk goes in the PR body, not under the rug.
- **Then the traps: `python3 Tools/Build/ship_triage.py` prints the trap numbers in `references/REVIEW_TRAPS.md` that match this branch** (doc comments moved by an inserted member, a rename that means three things, a guarded call site on a null reference, a test that supplies its own answer, a retirement's residue, ...). Read those; `/ship-deep` reads them all.

## 2.5 Tool-output gate — NEVER SKIPPED, IN EVERY MODE

**An editor tool's deliverable is the DATA it writes, not the tool.** The tool lands in
the branch because you committed it; its output lands in the human's *working tree*,
where nothing forces anyone to notice it. Merge the PR and you have shipped code that
expects a scene / prefab / SO nobody pushed — broken on every other machine, with
nothing in the diff to explain it. This gate is the only thing standing between that
and a green PR, so it runs even in `/ship-quick`.

**1. Classify every tool the branch adds or changes.** Find them:

```sh
git diff --name-only <merge-base>..HEAD -- '*.cs' | xargs -r grep -l 'MenuItem("FrogletTools/'
```

**That command finds Unity editor tools and NOTHING ELSE — a WRITER here is not always C#.**
This project's `Tools/Build/*.py` generators author `.asset`/`.unity`/`.prefab` content
directly (`author_*_assets.py` is a whole family of them), so a branch adding one has exactly
the same half-landed-output exposure with no `[MenuItem]` anywhere to find. Sweep the other
languages too:

```sh
git diff --name-only <merge-base>..HEAD -- '*.py' '*.sh'
```

and classify each hit by the same evidence test, widened to that language's write calls:
`open(..., 'w')`, `Path.write_*`, `shutil.*`, `os.remove/rename/makedirs`, `subprocess` that
shells out to a writer. A tool whose only output is `print` / `sys.stderr.write` is a READER —
say so explicitly, because "the branch adds a tool and no assets" is the exact shape of the
failure this gate exists to catch, and the only thing that distinguishes it from a clean
READER branch is having run the check.

For each hit, read it and decide from the CODE, not the name:

| Kind | Evidence in the source | What the branch must contain |
|---|---|---|
| **READER** | only logs / `Debug.Log` / builds a report; no `AssetDatabase.Save*`, `PrefabUtility.*`, `EditorSceneManager.Mark*`, `ApplyModifiedProperties`, `AssetDatabase.CreateAsset`, `File.Write*` | nothing — say so explicitly in the report |
| **WRITER** | any of the above | its output, committed |

**2. Prove a WRITER's output is on the branch.** Two independent checks, both required:

```sh
git status --porcelain -- Assets ProjectSettings          # nothing tool-shaped may be dirty
git diff --stat <merge-base>..HEAD -- '*.unity' '*.prefab' '*.asset'
```

- Dirty `Assets/**` in the first command is the smoking gun. Do not commit it blind —
  read it, confirm it is that tool's output, then commit it *as its own commit*.
- A WRITER in the diff with **zero** asset changes in the second is the silent failure.
  It means one of: the human has not run it yet, they ran it and never saved, or they
  saved and never committed. You cannot tell from here — **ask** (step 3).
- Also check `Library/FrogletToolChangeLedger.json` when it exists (machine-local, and
  absent in a remote container): it names the exact paths each tool wrote.
- **Grep for `AssetDatabase.CreateAsset`, never a bare `CreateAsset`** — `[CreateAssetMenu]` is
  an attribute on hundreds of ScriptableObject classes and matches the short form, so a bare grep
  reports ordinary gameplay SOs as WRITER tools. Confirm every hit by reading the matching line;
  a hit whose line is an attribute is not a writer, and a branch whose only "writers" are
  `[CreateAssetMenu]` attributes has **no tools at all**.

**3. Prompt the human — this is a blocking question, not a note.** Use
`AskUserQuestion`, name each WRITER tool and the asset paths it targets, and ask which
is true: *ran it and it is saved* / *ran it, not sure it saved* / *have not run it yet* /
*it is read-only, no output expected*. Anything but the first or last means **NO-GO**
until it is resolved. In-editor, `FrogletTools ▸ Build ▸ Pending Tool Changes` answers
this in one click and can push the output itself.

**4. Verify what came back**, then re-run step 2 — the human saving in Unity changes the
working tree under you. Check the recovered output the way §2 checks anything else:
every new asset has its `.meta`, no orphan `.meta`, no `Missing (Mono Script)` rows, GUID
references resolve.

**5. Retire the one-offs.** A tool written to perform one migration is scaffolding, not
surface area: once its output is verified and pushed, delete the tool and its scratch
assets in their own commit (`chore(tools): retire <name> after verification`). Keep it
only if it is idempotent and re-runnable — an auditor, a validator, a wirer someone will
need again — and if you keep it, say why in the PR body. `/ship-tools` automates this
whole section.

## 3. Documentation pass ("ready to build from")

For every system the branch touched, confirm the docs a NEW developer would reach for are
current — update them if not:

- The system's `Docs/<System>/` or co-located `.md` reference (ARCHITECTURE, mechanics log).
- `CLAUDE.md` if the branch changed a pattern, invariant, or key-files table it states.
- In-editor verification steps for anything that needs a human at the editor. You cannot
  run Unity and cannot compile (§0.05), so the human is the ONLY gate: hand them the exact
  steps and knobs, and never imply a check you did not perform.
- Follow-up work goes in the relevant BACKLOG/TODOS doc, not in your head.
- **A MEASUREMENT quoted into a doc is a derived value, and nothing recomputes it.** A `file:line`
  reference at least fails visibly when you go and look; a number does not — `1.16 → 4.60`,
  `5.75% of the limb`, `~42,840 prisms at cap` all read as facts forever, and the tool that
  produced them is one retune away from disagreeing. Before shipping, RE-RUN the tool that
  measured every number the branch's docs state and diff the output against the prose, and where
  the number will keep moving, say which tool owns it ("authored by
  `Tools/Build/author_lattice_spindle_materials.py` (`--check`)") so the next reader re-derives
  instead of trusting. A measurement with no named producer is the doc equivalent of a magic
  constant.

- **A class RENAME is invisible to every gate this project has, so the docs that name it go
  stale silently.** A `git mv` keeps the file's guid, so every scene, prefab and asset
  reference survives, the compiler is happy because the call sites moved with the rename, and
  the only thing left holding the old name is prose. This branch pulled the gate-race
  machinery out of Switchback into `Racing/` and SWITCHBACK.md — the doc a reader reaches for
  FIRST — still named four types that no longer existed, plus never mentioned the extraction
  at all. Nothing failed; nothing could have. After any rename or file move, grep the OLD
  identifier across `Docs/` and every co-located `.md`, and check the moved-from system's doc
  states its new relationship to the thing it was extracted into, not just its own internals.

## 3.5 Skill-capture retrospective (harvest what the session learned)

Before the go/no-go, review the SESSION (not just the diff) for knowledge worth
keeping — the things that were painful to figure out and would be re-invented
next time:

- **Findings & techniques**: did the session discover a repeatable method (a new
  way to edit an asset class, a validation pattern, a debugging shortcut)?
- **Workarounds & traps**: did anything cost more than ~15 minutes to a
  non-obvious cause (a format quirk, an API that lies, a normalization surprise,
  a clock/coordinate-space mismatch)? Each of those is a trap entry.
- **Pushed-back punts**: did the prompter have to say "you can do this" about
  work you were deferring to them? That gap between assumed and actual
  capability is EXACTLY what a skill exists to close.

Then act on it — this step produces edits, not intentions:

- **Extend an existing skill** when the learning fits one (`ls .claude/skills/`;
  add the trap/technique to the closest skill's list).
- **Create a new skill** when the session established a coherent new capability
  with its own method and trap list (see `/asset-surgery` for the shape: doctrine
  → safety pattern → techniques → traps → limits).
- **At minimum**, name the candidates in the ship report so the prompter can
  decide — silence is the only wrong output. A session that learned nothing
  reusable says so explicitly.

## 3.55 Lab capture (any branch that made or changed a LAB — feeds `/labmaker`)

**Trigger:** the branch made or changed a lab (a browser studio under `Docs/Studios/`, a `*Lab*.html`, a research rig, a headless model with a scorecard, an in-editor lab window, or anything under `.claude/skills/labmaker/`); `ship_triage.py` says LAB. **Then read `references/LAB_CAPTURE.md` and do it all**: detect, verify each single-file browser lab, update `/labmaker` with edits (CATALOG row, LEARNINGS ids checked across every remote branch), and its two traps. `/ship-quick` keeps the CATALOG row and any costly lesson. No trigger: one report line, "no lab touched".

## 3.57 Compute capture (any branch that touched a search or a model — feeds `/compute`)

**Trigger:** the branch touched `Tools/Compute/` or `Docs/Compute/jobs/`, a `Tools/Build/*_sim.py` / `*_balance.py` / `*_budget.py` model a job imports, or added a hand-rolled sweep (`--sweep`, `itertools.product(`); `ship_triage.py` says COMPUTE. **Then read `references/COMPUTE_CAPTURE.md`.** No trigger: one report line.

## 3.6 Refactor-opportunity pass (§3.5 for DEBT — rows only, never edits)

**Rows only, never edits to this branch.** Write a row (with the command and its output) in the owning BACKLOG / TODOS / REFACTOR doc, or `spawn_task`, for debt this branch CREATED (kept-for-compatibility fields, shims), WALKED PAST (the sibling with the same latent bug) or made VISIBLE (a count you had to re-measure). Nothing seen: one report line. The full rules and examples: `references/REFACTOR_PASS.md` (read it in `/ship-deep`, or when you have a row to write).

## 4. Go / no-go (push back when warranted)

Say **NO** — and list the concrete iterations needed — when any of these hold:

- A review gate in §2 failed and the fix isn't a quick one.
- `approvals.py check` reports a **swim lane COLLISION** the person has not decided on (§5). Touching another
  collaborator's work is otherwise never a NO-GO: it goes through, with its `/approve` entries.
- **§2.5 is unresolved** — a WRITER tool is on the branch and its output is not, or the
  human has not confirmed they ran it. This one is not a judgment call: no amount of
  otherwise-good work makes a half-landed migration shippable.
- The branch mixes an unfinished experiment with finished work (split it instead).
- A change is known-broken or known-untested in a way that would block another dev
  building on it (a hand-authored asset you could not import counts — say which).
- Docs for a touched LOCKED system (ecology, party, threading, scoring) lag the code.

**If you did run a gate (see §0.05 — you are not required to), a RED result is not
automatically yours — A/B it before you attribute it, and report it either way.** A gate that passed on your branch can be red after you merge the base, and the
cause is as likely to be the base's new content as your resolution. Prove it in a detached
worktree of the base alone (`git worktree add --detach <tmp> origin/<base>`, run the same
gate there, `git worktree remove --force`): a gate that fails identically on a clean base
checkout is an upstream break you must NAME in the commit and the PR — with the exact error
and why you cannot fix it — but not one you have to fix or sit on. One session found two
this way; one needed a number only the upstream author had (a converted asset's
pre-conversion budget, which the conversion overwrites), so "fixing" it would have meant
inventing it. Never silently absorb a red gate as your own, and never let one you did not
cause block work that is otherwise ready.

Say **GO** when the work is coherent, documented, and honestly labeled. Loose ends that
don't block building on the branch become a **Follow-ups** section in the PR body — named,
scoped, and assigned a doc home — not reasons to sit on finished work.

**A GO now means it merges (§5.7)**, so the bar is "safe to batch-test on bleeding-edge", not "proven in the
editor": untested in Unity is fine when it is honestly labelled and its hand tests are on the branch's ticket. A
branch that would break bleeding-edge for everyone else (a known compile error, a half-landed tool output, a broken
scene) is still a NO.

## 5. Open the PR (GO only)

- **Who else works here** (`Docs/TEAM.md`): `git fetch origin && python3 Tools/Build/who_works_where.py query
  $(git diff --name-only origin/bleeding-edge...HEAD | cut -d/ -f1-4 | sort -u)`. For every other collaborator with
  in-flight or recent work in those paths, the PR body gets an **Affects** line ("Affects: Garrett's
  `cece/zen-fermat` Stoat modes - builds on them, changes nothing they decided").
- **Collide or go** (`Docs/TEAM.md` rules 3 and 13, Garrett 2026-11-10): `python3 Tools/Build/approvals.py check --me
  <person>`. **Exit 2 is a COLLISION** - the branch is about a swim lane someone else occupies right now - and it is a
  NO-GO until the person you work with has heard who is in it and decided (talk to the occupant, or go ahead). Nothing
  else is a NO-GO for touching another collaborator's work, re-tuning or replacing it included: tell the person whose
  toes it is on, and for each HEADS-UP person whose work the branch actually changes, commit an
  `approvals.py add --to <them> --by <person> --area ... --summary ...` entry before opening the PR (add `--pr` when
  you know it). The PR body's **Affects** section lists the entries it queued ("queued for Shombith's /approve:
  <id>").
- **Hand-offs** (`Docs/TEAM.md` rule 9, the `/team-tasks` skill): anything the branch leaves for a person (an editor
  test, a playtest, a dashboard step, content, a decision) goes on the branch's ONE ticket (`/team-tasks add`; a second
  `add` appends a checklist line), naming nobody: tickets are never assigned; taking one makes it the taker's, and naming a branch starts it. Close
  the tickets the branch finished (`team_tasks.py done <id> --note <proof>`). Then put
  `python3 Tools/Build/team_tasks.py pr-section` in the PR body as its **Hand-offs** section.
- Do not ask whether Amoebius or the Vessel Studio helped: the decision is recorded (`Docs/TOOLING_DECISIONS.md`
  §6, 2026-10-19) and Shombith asked sessions to stop. Keep Vessel Studio work out of `Port/`.
- Check for a PR template (`.github/pull_request_template.md` and variants); mirror its
  structure if present.
- **UI changes answer the UI checklist** (`Docs/UI_STYLE_GUIDE.md` §11): when the branch touches `Assets/_Prefabs/UI*`,
  `Assets/_Scenes/Menu_Main.unity`, `GameCanvas`, `Assets/_SO_Assets/UI/` or `Assets/_Scripts/UI/`, the PR body carries the
  template's `## UI checklist` with every box ticked or `N/A - <why>` (`python3 Tools/Build/check_ui_pr.py --body-file
  <body> --files <changed>` before opening; the `UI checklist` workflow runs the same check on the PR).
- PR body: what & why, per-system summary, **verification status** (what a human must
  still verify in-editor, with steps), **Tool output** (every tool the branch touched,
  reader vs writer, which commit carries its output, which tools were retired and which
  were kept and why — §2.5), **Follow-ups** list, collider/perf impact where the ecology
  gate applies.
- Base is `bleeding-edge` unless told otherwise. Do not subscribe to PR activity to watch
  CI (§0.05); subscribe only if the human asks you to follow the review - or when the harness you run in says
  a PR you opened is yours to drive to green, which overrides this line.
- **`/ship promote` PR shape** (an integration branch carrying many already-merged PRs): look for an open PR
  first (`list_pull_requests` with `head: froglet-studio:$BRANCH`, `base: bleeding-edge`, `state: open`) and
  refresh its title and body rather than opening a second. Title `merge(<$BRANCH>): <the 3-5 headline changes>`.
  Body, in order: the ahead count, the bleeding-edge commit merged in first and the conflicts resolved; what it
  carries, grouped by area with short shas; verification (the gates.sh result, the compile numbers when it ran,
  or plainly that nothing compiled); to test by hand, one line per changed behaviour; Affects; the footer. When
  the PR merges, confirm it landed with `git merge-base --is-ancestor origin/$BRANCH origin/bleeding-edge`, and
  say GitHub may have deleted `$BRANCH`. It merges like any other ship (§5.7); **never approve the PR yourself**.
- **Before pushing MORE work onto the branch of a PR you already opened, confirm the PR is still
  OPEN** (`pull_request_read` → `merged`/`state`), right before the push, not at the start of the
  task. Since §5.7 it normally is NOT: a shipped branch is merged, so follow-up work restarts the branch from
  `bleeding-edge` (below) and goes out as a new PR. A human can merge at any moment too, and a commit pushed after the merge lands on a branch
  nobody will merge again: it is stranded, and any PR-body update you make to describe it then
  claims work the merged PR does not contain. It happened on 2026-10-14: #1091 merged at 21:20 and
  the prefab-authoring commit was pushed at 21:30. The QA backlog is scanned from PR bodies, so an
  overclaiming body is a wrong test plan, not just a wrong note. If it has merged, restart the
  branch from the base (merge the base in when force-push is refused), keep the commit, and say so
  in both PRs. The follow-up went out as #1094, and #1091 carries a correction note pointing at it.

## 5.5 Fire CI once (every mode — `/ship`, `/ship-quick`, `/ship-deep`, `/ship-tools`)

A `claude/**` branch fires no CI by itself: `unity-ci.yml` skips every pull-request event whose
head is `claude/**`, and the iOS request-file workflows ignore those branches
(`Docs/BUILD_AND_DELIVERY.md` § "`claude/**` branches do not trigger CI on their own"). CI runs
for it only when it lands on `bleeding-edge` or when a ship command runs — so this step is the
branch's one pre-merge check. Do it after the LAST push of the command (the PR in §5, or the
§6/§7 push in `/ship-tools`), on a GO and on a `/ship-tools` push alike; a NO-GO pushes nothing
new and fires nothing.

```
mcp__github__actions_run_trigger
  method: run_workflow
  owner: froglet-studio   repo: cosmic-shore
  workflow_id: unity-ci.yml
  ref: <this branch>
  inputs: { mode: static }
```

- **`static`, not `compile`.** It is the ubuntu static checks only and never takes the single
  Unity runner, which is reserved for post-merge compiles and the scheduled IL2CPP builds. A
  human who wants a real compile of the branch dispatches `compile` from the Actions tab.
- **Once per command, and fire-and-forget.** Do not wait on it, poll it, or subscribe to it
  (§0.05). Put "CI dispatched: `unity-ci.yml` static on `<branch>`" in the PR body or the
  `/ship-tools` report — the run lands on the branch head's checks.
- **The branch must carry the `static` choice.** A dispatch runs the ref's own copy of
  `unity-ci.yml`, so a branch cut before the option existed is refused with a 422 until
  bleeding-edge is merged in (which §1 normally does anyway).
- **If the dispatch is refused** (no tool, a 403), say so in the report in one line with the
  manual route — Actions → Unity CI → Run workflow → `static` → the branch. It is never a
  reason to hold a GO.
- **Never push an empty commit, or close and reopen the PR, to make CI fire.** The dispatch is
  the only trigger.

## 5.6 Refresh the Fleet Codex (every mode that pushes - `/ship`, `/ship-quick`, `/ship-deep`, `/ship promote`, `/ship-tools`)

**Trigger** (on a GO, after §5.5): `ship_triage.py` says CODEX, i.e. the branch touched `ElementalAbilityMaps/`, `Spacevessels/`, `SO_Class_`, `VesselClassType.cs`, `ToyVesselRoster`, `R_VesselActions/`, `/Vessel/`, `AbilityIcons/`, `VesselHUD/`, `icon_kit/specs/`, `_SO_Assets/Games/`, `author_touch_profiles`, `fleet_codex` or `FleetCodex/`. **Then read `references/FLEET_CODEX.md` and follow it** (build on a bleeding-edge worktree with the pending branches merged, publish to the SAME url https://claude.ai/artifact/JDvgFqu1bCfgcHVfCdXaA8). No trigger: one report line, "no codex surface touched". A refused publish never holds a GO.

## 5.7 Merge into bleeding-edge (every mode that opens a PR, on a GO)

Garrett, 2026-11-10: "make /ship automatically merge. It is becoming fast to batch test in bleeding edge and fix what
doesn't work than switch to each branch for testing." So a GO ends with the PR merged, and the team tests many
branches at once on `bleeding-edge` instead of checking out each one. Skip this section only when the person said
"PR only" / `--no-merge`, on a NO-GO (nothing was opened), and in `/ship-tools` (it opens no PR).

1. **Last, after §5.5 and §5.6.** CI is fire-and-forget (§0.05): do not wait for it.
2. **Right before merging, run the trunk gate again** (`python3 .claude/skills/trunk-doctor/trunk_doctor.py --gate`,
   §1.5): on exit 2, do not merge unless this PR is the fix or the person says to merge anyway. Then **read the PR** (`pull_request_read`, method `get`): still open, `mergeable` true. If
   `bleeding-edge` moved and the PR now conflicts, run §1.5 again (merge `bleeding-edge` in, resolve, `gates.sh`,
   push) and read it again. Never merge a PR whose gates you did not run on what is being merged.
3. **Merge with a merge commit**, never squash or rebase:

   ```
   mcp__github__merge_pull_request
     owner: froglet-studio   repo: cosmic-shore
     pullNumber: <the PR>
     merge_method: merge
   ```

   The team's readers rely on GitHub's `Merge pull request #N from froglet-studio/<branch>` commits: `who_works_where.py`,
   `swimlanes.py` and `team_tasks.py` read who landed what, which lane opened and which ticket closes from them, and
   a squash merge leaves no such commit, so the ticket sweep and the landed lists miss it.
4. **If GitHub refuses** (a ruleset wants a review, a code owner's review for `.github/` or the UI paths in
   `.github/CODEOWNERS`, or a required check): turn on auto-merge instead (`mcp__github__enable_pr_auto_merge`,
   `mergeMethod: MERGE`) so it lands the moment the requirement is met, and tell the person in one line which
   review or check it waits for. **Never approve your own PR, never bypass a ruleset, never change one to get a
   merge through.** If auto-merge is refused too, say so and leave the PR open.
5. **Confirm it landed:** `git fetch origin bleeding-edge && git merge-base --is-ancestor origin/$BRANCH
   origin/bleeding-edge` (or the merge commit's sha from the tool result). `bleeding-edge-guard.yml` verifies every
   commit that lands there and attempts a repair; a red guard run after your merge is yours to fix, on a new branch.
6. **After the merge the branch is finished.** Any later work on it restarts the branch from `bleeding-edge` and goes
   out as a new PR (§5, "Before pushing MORE work"). The swim lane opens again by itself. The branch's ONE Froglet
   Tasks ticket (§5 Hand-offs) is filed, not taken, so it stays open after the merge: it is the list the team
   batch-tests on `bleeding-edge`. Whatever fails there is fixed on a new branch, shipped the same way.

Someone else's branch (`/ship promote Ys-bleeding-edge`, a teammate's branch) merges only when §1.5 already confirmed
with the person that you may push onto it; a merge is that same decision.

## 6. Report

Tell the prompter: the go/no-go call and why, the PR link (or the iteration list), whether it
merged (§5.7: the merge commit, or the review/check auto-merge waits for, or "PR only" when they asked), whether
the §5.5 CI dispatch went out, the §5.6 Fleet Codex refresh (published hash and pending list, or "no codex surface
touched"), the
**§2.5 tool-output verdict** (every tool classified, whose output landed in which commit,
what was retired), the follow-ups you recorded, the §3.5 skill-capture outcome
(skills created/extended, or the explicit "nothing reusable this session"), the §3.55
lab-capture outcome (labs touched, verifier verdicts, `/labmaker` CATALOG/LEARNINGS changes, or
"no lab touched"), and the
§3.6 refactor-opportunity outcome (the rows opened and where they live, or the explicit
"nothing found" — and never a fix made in their place)., the §0.0 triage's skips (what was not read and why), and the §6.5 hand-off (what the person chose).

## 6.5 Offer the hand-off (the work looks finished)

When the work looks finished, ask the person whether to hand its swim lane on, with the skill that does it
(Shombith, 2026-11-13). **Finished** means any of: this ship reached a GO (merged or PR opened); the person said they
are done ("that's it", "wrap up", "I'm done with this"); or the branch's last commit closes the change it named.
A NO-GO is not finished: say what the next iteration is and offer nothing.

```bash
python3 Tools/Build/handoff.py suggest --json      # the lane, ASK or MENTION, the three choices ranked, and why
```

It reads the change's lane (or `--lane <key>`), what this branch shows (editor hand tests added, merged, the lane's
owner) and **this person's own past choices under the same signals** (every handoff and every "keep it" is recorded
with them), so it learns when to ask:
- **ASK**: they usually hand off in a case like this (or there is too little history). Use `AskUserQuestion` with the
  ranked choices, best first and marked "(Recommended)", plus "Keep it (I'm still on it)". Name the lane in the
  question. For `/handoff-to-<person>`, offer the person it ranked; "Other" lets them name anyone in `Docs/TEAM.json`.
- **MENTION**: they usually keep going here. Say it in one line ("If you're done with <lane>: /handoff-open,
  /handoff-for-test or /handoff-to-<person>") and do not open a question.

Then do what they chose by its skill: `/handoff-to-<person>` (`handoff.py to <Person>`), `/handoff-open`
(`handoff.py open`) or `/handoff-for-test` (`handoff.py test --what "..."`, the tester's brief, listing the editor hand
tests this branch added). "Keep it" runs `handoff.py keep` so the next suggestion learns from it. Write a note that
says where the work stands. Commit `Docs/Handoffs/` and push it to this branch. If §5.7 already merged the branch,
commit it on the restarted branch (it lands with the next work), and tell the person the hand-off shows at once on
`/swimlanes` and, after a sync, on the Swim Lanes page (`/swimlanes` §6). Never hand off a lane without the person's yes.

