---
name: to-bleeding-edge
description: Merge the latest of ANY branch into bleeding-edge through a pull request - `/to-bleeding-edge [branch]` (defaults to the current branch). Fetches both, merges bleeding-edge into the branch and resolves every conflict there, runs the bleeding-edge guard's gates and the headless compile on the merged tree, pushes, opens the PR (or refreshes the one already open) with what it carries, the verification and the hand tests, and watches it to green. Use on "/to-bleeding-edge", "/to-bleeding-edge Ys-bleeding-edge", "merge <branch> to bleeding-edge", "promote this branch to bleeding edge", "create the PR to bleeding-edge", "push our work to bleeding edge".
---

# `/to-bleeding-edge [branch]` - any branch → bleeding-edge, through a PR

**The argument is the branch to promote** (written `$BRANCH` below):
- `/to-bleeding-edge Ys-bleeding-edge` promotes `Ys-bleeding-edge`.
- With no argument, `$BRANCH` is the current branch (`git branch --show-current`).

Stop and ask if `$BRANCH` is:
- empty, which means a detached HEAD;
- `bleeding-edge` itself;
- or a protected branch such as `main` or `development`.

A branch reaches `bleeding-edge` **only through a pull request**. Never push to `bleeding-edge` directly:
- it is Garrett's branch, and it is protected;
- the `Bleeding-edge guard` workflow reviews what lands there.

All conflict resolution happens on `$BRANCH`, so the PR always merges cleanly.
Past runs: `Ys-bleeding-edge` #1078, #1081, #1086, #1098, #1104 and #1150.

## 1. Fetch and look

```bash
BRANCH="${ARG:-$(git branch --show-current)}"
git fetch origin bleeding-edge "$BRANCH"
git rev-list --left-right --count origin/bleeding-edge...origin/$BRANCH   # "<behind> <ahead>"
git log --no-merges --format='%h %an %s' origin/bleeding-edge..origin/$BRANCH
```

**The remote `$BRANCH` may be gone.** GitHub deletes the head branch when its PR merges.
- If the local branch still has commits that are not in bleeding-edge, push them; that recreates it.
- If not, there is nothing to promote.
- To restart it from bleeding-edge, keeping any unmerged local work:
  `git checkout -B "$BRANCH" origin/bleeding-edge`, then cherry-pick or merge the local work back.

**`ahead = 0`** means there is nothing to promote. Say so and stop.

**Check out `$BRANCH`** and fast-forward it to `origin/$BRANCH` (`git merge --ff-only`).
- Never rebase or force-push it: on a shared branch, other sessions push all day.

**Say whose areas the PR touches.**
- Run `python3 Tools/Build/who_works_where.py query <changed paths>` and name them in the reply (CLAUDE.md "Team").
- Name the branch's own owner too: `who_works_where` shows who drove it.
- Put `Affects: <person>'s <area>` in the PR body.
- **If `$BRANCH` is someone else's branch, confirm with the person before pushing a merge onto it.**

## 2. Merge bleeding-edge in and resolve conflicts

```bash
git merge --no-ff --no-commit origin/bleeding-edge
git diff --name-only --diff-filter=U        # the conflicted files
```

Resolve every conflict **keeping both sides' intent**: the two branches are different people's work.

- **Before choosing a side**, read both sides' commits: `git log --oneline origin/bleeding-edge -- <file>`, and the same on `$BRANCH`.
- **Generated files**: regenerate them with their tool, never hand-merge. These include `Docs/perf/status.json`, the who-works-where map, studio previews, cards, and any `author_*` output.
- **Shared numbers** (enum values, `GameModes` ids, GUIDs, Netcode hashes): if both sides claimed the same one, stop and ask the person. Renumbering serialized values breaks assets.
- **Scenes and prefabs**: never hand-merge YAML overrides you do not understand. Take one side whole, and name in the PR what the other side changed so its owner re-applies it. Or ask.
- **Docs and logs** (`Docs/TEAM_TASKS.md`, `UNITY_VERIFICATION_CHECKLIST.md`, sync logs): keep every row from both sides.
- **Same logic changed on both sides**, where picking either loses behaviour: ask the person.

Commit the merge:
- Message: `merge: bleeding-edge (<short sha>, #<PR>) into <$BRANCH> before the PR`.
- List each conflict and how it was resolved.
- End with the attribution lines.

## 3. Gates on the merged tree

```bash
bash .claude/skills/to-bleeding-edge/gates.sh
```

What it runs:
- the static checks `bleeding-edge-guard.yml` runs;
- every `author_*` generator's `--check`;
- the headless reference compile.

What counts as passing:
- **Player config**: must report `RESULT: OK`.
- **Editor config**: only files changed since bleeding-edge gate it. Its standing 3 errors in `AuthenticationServiceFacade.cs` come from a package that cannot be fetched.

Fix anything red on `$BRANCH` before the PR. Fix the root cause, and never skip a check.

## 4. Push

```bash
git push -u origin "$BRANCH"
```

**Rejected because someone pushed meanwhile:**
1. `git fetch origin "$BRANCH"`, then `git merge origin/$BRANCH`.
2. Re-run the gates if the new commits touched C#.
3. Push again.

**Network failures:** retry 4 times, waiting 2s, 4s, 8s, then 16s.

**No .NET SDK in the session** (gates.sh prints SKIP): install it once. It takes about a minute:
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o <scratchpad>/dotnet-install.sh && bash <scratchpad>/dotnet-install.sh --channel 8.0 --install-dir $HOME/.dotnet
```
The first compile then takes about 10 minutes, because it fetches the Unity references. Later ones take about 30 s.

## 5. Open or refresh the PR

Use the GitHub MCP tools (`mcp__github__*`); load them with ToolSearch.

**Look for an open PR first:** `list_pull_requests` with `head: froglet-studio:$BRANCH`, `base: bleeding-edge`, `state: open`.
- If one is open, it already carries every new push. Update its title and body; do not open a second.
- Otherwise, `create_pull_request` with head `$BRANCH` and base `bleeding-edge`.

**Title:** `merge(<$BRANCH>): <the 3-5 headline changes>`.

**Body, in this order:**
1. **Summary**: the ahead count, the bleeding-edge commit merged in first, and the conflicts resolved.
2. **What it carries**: grouped by area, with short shas.
3. **Verification**: the gates.sh result, and the compile results with their numbers. If no Unity CLI was available, say plainly that `/verify-unity` did not run.
4. **To test by hand**: one line per changed behaviour.
5. **Affects**: whose areas the PR touches.
6. The PR footer from the session's attribution reminder.

## 6. Watch it

`subscribe_pr_activity` for the PR, then handle each event:
- **CI red**: fix it on `$BRANCH` and push.
- **New pushes from other sessions**: re-run the gates if they touched C#, and refresh the PR body.
- **Codex review "usage limits" comments**: these are not code findings. Tell the person.
- **The PR merges**:
  - Confirm it landed with `git merge-base --is-ancestor origin/$BRANCH origin/bleeding-edge`, and report.
  - Mention that GitHub may have deleted `$BRANCH`. Recreate it from bleeding-edge if the person wants it back now.

**Never approve or merge the PR yourself.** The person or Garrett merges it.

## Report to the person

In plain words:
- the PR link, and whether it merges with no conflicts;
- the conflicts resolved, and how;
- what was verified and what was not;
- the hand tests still owed in Unity;
- whose areas the PR touches.
