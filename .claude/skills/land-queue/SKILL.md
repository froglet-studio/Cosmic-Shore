---
name: land-queue
description: Land a person's open pull requests on froglet-studio/Cosmic-Shore into bleeding-edge - as many as can go safely, in a low-risk order, one at a time - then report what is left. Lists their open PRs (land_queue.py, read-only by default: age, branch, behind / conflicting, CI and guard state on the head, size, files, draft / scratch / negative-control wording), classes each one (READY, NEEDS-REFRESH, RED, STALE, SUPERSEDED, SCRATCH, DRAFT, PENDING; BLOCKED after a failed refresh), orders the landable ones (CI and pipeline first, then launch blockers, then scenes / shared prefabs / UGS one at a time, then features, docs last), refreshes the person's own branches by merging bleeding-edge in (never a rebase or force-push) and gating them, asks once for the whole batch, merges only onto a green trunk, proposes closing the stale, superseded and scratch ones, and appends a dated record to Docs/Reports/LAND_QUEUE.md. Use on "/land-queue", "land my PRs", "merge my open PRs", "clear my PR queue", "what's in my PR queue", "which of my PRs can merge", "get my PRs into bleeding-edge", "close my stale PRs", "how many open PRs do I have", or "/land-queue --user <login>".
---

# /land-queue - get a person's open PRs into bleeding-edge, safely, in order

A person piles up open pull requests: launch work waiting on a refresh, old experiments, negative controls that were
never meant to merge. This skill clears that queue. It lands what can land, in an order that keeps risk low, closes
what should go once the person says yes, and reports the rest with a reason for each.

It does not re-implement shipping. It calls what exists:

| Step | Comes from |
|---|---|
| Refreshing a branch (merge bleeding-edge in, the conflict rules, never rebase or force-push) | `/ship` §1.5 |
| The tool-output gate (an editor tool's assets must be on the branch) | `/ship` §2.5, run on any PR that adds a tool |
| The merge (merge commit, auto-merge when a review is required, never approve your own PR) | `/ship` §5.7 |
| Collisions with someone else's swim lane | `/swimlanes`, `Tools/Build/approvals.py check` (`/approve` §7) |
| Hand-off tickets a landed branch leaves | `/team-tasks`: the branch's ticket closes by itself when it merges |

`/ship` closes out ONE branch you are working on. `/to-bleeding-edge` (= `/ship promote`) promotes one integration
branch. `/land-queue` works through the PRs a person already opened.

**Who owns it:** tooling, so Shombith (CLAUDE.md § Team). A change to how it classes or orders PRs goes in his
`/approve` queue.

## 0. The rules (every step enforces its part)

1. **Only the invoker's PRs get actions.** A PR by someone else is listed but never refreshed, merged, closed or
   commented on, unless its author asked the invoker to land it. Get the link or quote and pass it
   (`list --asked <n>`, `refresh <n> --author-asked "<link>"`).
2. **Never skip, disable or quarantine a test or gate.** A red gate is fixed on its branch, or the PR stays open.
3. **Never merge onto a red trunk.** `land_queue.py trunk` and `preflight` read the Bleeding-edge guard's newest run
   and every check on bleeding-edge's head. Red means stop and suggest **`/trunk-doctor`**. That skill is not built
   yet (2026-11-12). Until it is, fix the trunk on a new branch, the way `/ship` §5.7 step 5 says.
4. **Never touch repo settings, rulesets, environments or secrets.** If GitHub refuses a merge because of a ruleset,
   use auto-merge or leave the PR open (`/ship` §5.7 step 4).
5. **Stop and ask before every outward action**: each merge batch, each close, each comment. The one exception is
   pushing a refresh to the invoker's own branch, which is part of the approved batch (§5).
6. **Never delete a branch.** The person decides that.

## 1. List

```bash
git fetch origin bleeding-edge
```

```bash
python3 .claude/skills/land-queue/land_queue.py list [--user <login>] [--save "$SCRATCH/records.json"]
```

By default it lists the open PRs of the GitHub user the session runs as (`gh api user`); `--user <login>` overrides
that, and `--as <login>` sets who is landing. It takes about 50 s for 40 PRs. For each PR it reads, all through
`gh api repos/froglet-studio/Cosmic-Shore/...`:

- **age** and **last commit**;
- the **head branch**;
- **behind / conflicts** against bleeding-edge (`compare`, `mergeable`);
- **checks on the head**: every check run and commit status, with re-runs collapsed so the newest run counts;
- **size** and **files**;
- **flags**: draft, scratch or negative-control wording in the title, branch, labels or description.

Nothing is written: the output is two markdown tables (what each PR is, then the plan). Other forms:

- `--json` for the whole record;
- `--pr 1749,1755` for a subset;
- `--save F` and then `--from-json F` to re-plan saved records without the network.

`gh api` here pages by `&page=N`, never by GitHub's `Link` header. That header points at `repositories/<id>/...`,
which the session proxy refuses, and the first run lost every PR with more than 100 files to it.

Say how many PRs there are, the count per class, and show both tables.

## 2. Classify

`classify()` applies these in order, and the first that fits wins. Because SCRATCH, STALE and RED are all decided
before READY, a red PR can never read READY. `--self-test` holds that with five red negative controls.

| Class | When | Action |
|---|---|---|
| **SCRATCH** | title, branch (`scratch/`, `ci-scratch/`, `*negative*`), labels or description say negative control / do not merge / scratch. "Do not merge **without** a review" is a request, not a scratch marker: it goes in the reason. | propose close |
| **SUPERSEDED** | nothing ahead of bleeding-edge; or every file it changes already reads on bleeding-edge exactly as the PR has it (blob ids from `git ls-tree`); or its thread says "superseded by #N", or another open PR says "supersedes / replaces #N" | propose close |
| **DRAFT** | GitHub's draft flag, `[WIP]` / `Draft:` title, a draft label, "this PR is a draft" | leave open |
| **STALE** | no commit for 14+ days; or a PR merged after its last commit changed half or more of its files (hot shared files like the verification checklist do not count) **and it adds nothing bleeding-edge lacks**. A PR stacked on one that landed still adds its own files, so it is not stale: the overlap goes in its reason. | propose close |
| **RED** | a failing check on its head (failure, timed out, startup failure, action required) | leave open: fix it on its branch |
| **PENDING** | a check on its head still running, or GitHub has not computed `mergeable` yet | re-run the list later |
| **NEEDS-REFRESH** | behind bleeding-edge, or conflicting with it | refresh (§4) |
| **READY** | none of the above: green, or no checks at all (a `claude/**` branch fires no CI by itself, `/ship` §5.5; the land gates are then its check) | gate, merge |
| **BLOCKED** | set by §4: conflicts that no side clearly wins, or a gate that is red on the merged tree but green on bleeding-edge | leave open, with the files |

Read the reasons before you act on them. A class is a measurement, not a verdict: a STALE launch PR may still be
wanted, and a SUPERSEDED one may have a doc nobody carried across. The person decides at §5 and §6.

## 3. Order

Landable PRs (READY, NEEDS-REFRESH, the invoker's own) go in tiers, oldest first within a tier:

0. **CI / pipeline**: a `ci(` / `build(` title, or at least 30 % of its files under `.github/`, `Tools/CI/`, the gates
   (`ship_gates.py`, `gates.sh`, `unity_refcompile/`, `push_checks.py`).
1. **Launch blockers**: a PR number in `Docs/LAUNCH/READINESS.md` §10-§12, or "launch blocker" / "(blocker N)" in its
   title or the top of its description.
2. **Scenes, shared prefabs and UGS** (`.unity`, `.prefab`, Cloud Code / Cloud Save / UGS paths), **one at a time**:
   each refreshes after the merge before it and is gated again.
3. **Features**: everything else.
4. **Docs only**: every file `.md` / `.txt` / under `Docs/`.

Two PRs that touch the same scene or prefab are never back to back. The plan pulls a PR from the same tier between
them, and if there is none, the second is marked to refresh after the first lands.

## 4. Refresh (NEEDS-REFRESH, the invoker's own branches only)

```bash
python3 .claude/skills/land-queue/land_queue.py refresh <n> \
  --trailer "Co-Authored-By: <the session's line>" --trailer "Claude-Session: <the session's line>"
```

Without `--push` it is a dry run. It works in a scratch worktree, so your checkout is never touched:

1. It refuses another person's PR (unless `--author-asked`), a fork, and protected branches (`bleeding-edge`,
   `development`, `master`, `main`, `build/*`).
2. It fetches the branch and bleeding-edge. On a shallow clone it deepens until there is exactly one merge base,
   because a shallow clone invents bases (`/ship` §1).
3. It runs `git merge --no-ff --no-commit origin/bleeding-edge`. It never rebases and never force-pushes.
4. **Conflicts.** It resolves a conflict on its own only when one side clearly wins, meaning the two sides differ
   by whitespace alone. Anything else is **BLOCKED**: it lists the files and aborts the merge. You may then resolve
   by hand on the branch, but only where `/ship` §1.5 makes one side the clear winner:
   - a generated file, rebuilt with its generator;
   - a pure-append log row from both sides (the checklist, `TEAM_TASKS`), keeping both.

   Scenes, prefabs, shared numbers (enums, `GameModes` ids, GUIDs) and logic changed on both sides stay BLOCKED. Tell
   the person which files and why.
5. It commits `merge: bleeding-edge (<sha>) into <branch> before landing #<n>` with your trailers.
6. **The fast gates, on the merged tree:**
   - `validate_project.py`;
   - `check_conditional_compilation.py`;
   - `check_console_logging.py`;
   - each generator `--check` the diff touches: the generator itself, a 3+-segment path it names, its mode's name in
     an `Assets/` path, or every generator when `arcade_mode_lib.py` changed;
   - `unity_refcompile`, when C# changed and a .NET SDK already exists. Otherwise it prints SKIP, and a SKIP is not a
     pass: write "not compiled" in the report (`/ship` §0.05).

   `land_queue.py gates --pr <n>` prints that list without running anything.
7. **A failing gate is run again on bleeding-edge** in a second worktree:
   - red there too: it is the trunk's, printed `INHERITED`, and the red trunk blocks the merge anyway;
   - green there: it is the PR's, and the PR is BLOCKED.
8. With `--push` it pushes the merge (`git push origin HEAD:refs/heads/<branch>`, retried 2/4/8/16 s; a rejection
   means the branch moved, so run it again). The last line is `GATED <sha>`: keep that sha for §5.

Exit codes: 0 gated (and pushed with `--push`); 1 red gate; 2 refused; 3 GitHub, git or the push failed; 4 BLOCKED
on conflicts.

A READY PR gets the same command. It is already current, so `refresh` only gates its head and prints `GATED <sha>`.

**A PR that adds an editor tool or a `Tools/Build` writer** also passes `/ship` §2.5 (its output is on the branch)
before it lands.

## 5. Land

1. **Show the plan** from §1: PR, class, action, reason. Add what §4 found (BLOCKED files, INHERITED gates).
2. **Lane collisions.** For each PR in the batch:

   ```bash
   python3 Tools/Build/approvals.py check --me <person> --files $(python3 .claude/skills/land-queue/land_queue.py files <n>)
   ```

   Exit 2 (COLLISION) takes that PR out of the batch until the person has talked to the occupant (`/approve` §7).
3. **Ask once for the whole batch** with `AskUserQuestion`: land these N in this order, yes / no / pick. Include
   the trunk state, and the merge method: **a merge commit**. Measured by `land_queue.py trunk` on 2026-11-12: 180
   merge commits against 2 squashes on bleeding-edge in 60 days. `/ship` §5.7 explains why: the team's readers
   (`who_works_where.py`, `swimlanes.py`, `team_tasks.py`) need `Merge pull request #N` commits. Never squash or
   rebase into bleeding-edge.
4. **Then, one PR at a time, in order:**
   - `python3 .claude/skills/land-queue/land_queue.py preflight <n> --gated-sha <GATED sha>`. It checks that the PR
     is open, not a draft and mergeable, that its head is the commit the gates ran on, that nothing on it is red, and
     that **the trunk is green**. If it is not OK, stop that PR. Stop the whole batch when the trunk is red, and
     suggest `/trunk-doctor`.
   - Merge with `mcp__github__merge_pull_request` (`owner: froglet-studio`, `repo: Cosmic-Shore`, `merge_method:
     merge`). If GitHub refuses because a review or check is required, follow `/ship` §5.7 step 4: auto-merge,
     never your own approval.
   - Confirm it landed:

     ```bash
     git fetch origin bleeding-edge && git merge-base --is-ancestor origin/<branch> origin/bleeding-edge
     ```
   - Before the next PR: if it is tier 2, or shares a scene or prefab with the one just merged, run §4 again
     (`refresh <next> --push`). Any PR now behind gets its `preflight` re-run against the new trunk, and a
     `mergeable: false` sends it back to §4.
5. When the batch is done, fire CI once per pushed branch only if the person asks. `/ship` §5.5 owns that dispatch;
   the land queue waits on nothing.

## 6. Close

For STALE, SUPERSEDED and SCRATCH PRs (the invoker's own), the plan carries a one-line comment in `--json`
(`close_comment`):

> Closing from /land-queue: stale, no commit for 64 days. The branch `claude/x` is kept; delete it yourself if
> nothing on it is needed.

1. **Ask first.** Show the list with each reason and ask which to close (`AskUserQuestion`, multi-select, up to
   four per question). Unticked PRs stay open.
2. **Close each one the person ticked**:
   - post the comment with `mcp__github__add_issue_comment`, ending with the attribution footer
     (`---` / `_Generated by [Claude Code](https://claude.ai/code)_`);
   - then close it with `mcp__github__update_pull_request` (`state: closed`).
3. **Never delete the branch.** Say in the report which branches the person may want to delete by hand.

## 7. Report

Tell the person:

- what merged, with each merge commit;
- what is blocked and why: the files, the gate, or the red trunk;
- what was closed;
- what is left open, with the class of each;
- the open count before and after.

Then append the dated record and commit it on the session's branch (never on `bleeding-edge`):

```bash
python3 .claude/skills/land-queue/land_queue.py record --user <login> --person <name> --before <n> --after <m> \
  --merged 1748,1749 --closed 89,91 --blocked "#1738: conflicts in Menu_Main.unity" --note "trunk red: numpy in CI"
```

`Docs/Reports/LAND_QUEUE.md` keeps every run, newest last, so progress shows over time.

## 8. Changing it

- Class rules, tiers and patterns live at the top of `land_queue.py`: `HOT_FILES`, `CI_PATHS`, `UGS_PATHS`,
  `SCRATCH_*`, `DRAFT_*`, `STALE_DAYS`, `OVERLAP_SHARE`, `CI_SHARE`.
- Each class has a fixture in `--self-test`. A new rule gets a new fixture, plus a negative control that would fail
  without it.
- Run `python3 .claude/skills/land-queue/land_queue.py --self-test` and one real `list` after any change.
  Mutating the RED rule out of `classify()` makes the self-test fail 7 checks: try it again whenever the order
  changes.
