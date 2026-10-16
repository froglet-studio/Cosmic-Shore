---
name: ys-to-bleeding-edge
description: Merge the latest Ys-bleeding-edge into bleeding-edge through a pull request - fetch both, merge bleeding-edge into Ys-bleeding-edge and resolve every conflict, run the bleeding-edge guard's gates and the headless compile on the merged tree, push, then open the PR (or refresh the one already open) with a summary of what it carries, the verification and the hand tests, and watch it to green. Use on "/ys-to-bleeding-edge", "merge Ys-bleeding-edge to bleeding-edge", "promote Ys to bleeding edge", "create the PR to bleeding-edge", "push our work to bleeding edge", "sync Ys and bleeding edge".
---

# Ys-bleeding-edge → bleeding-edge

Yash's working branch `Ys-bleeding-edge` reaches `bleeding-edge` **only through a pull request**
(#1078, #1081, #1086, #1098, #1104). Never push to `bleeding-edge` directly: it is Garrett's
branch, it is protected, and the `Bleeding-edge guard` workflow reviews what lands there.
All conflict resolution happens on `Ys-bleeding-edge`, so the PR always merges cleanly.

## 1. Fetch and look

```bash
git fetch origin bleeding-edge Ys-bleeding-edge
git rev-list --left-right --count origin/bleeding-edge...origin/Ys-bleeding-edge   # "<behind> <ahead>"
git log --no-merges --format='%h %an %s' origin/bleeding-edge..origin/Ys-bleeding-edge
```

- **The remote `Ys-bleeding-edge` may be gone**: GitHub deletes the head branch when a PR merges.
  Recreate it from bleeding-edge, keeping any local commits not yet in it:
  `git checkout -B Ys-bleeding-edge origin/bleeding-edge`, then cherry-pick or merge the local work, then push.
- `ahead = 0` → nothing to promote; say so and stop.
- Check out `Ys-bleeding-edge` and fast-forward it to `origin/Ys-bleeding-edge` (`git merge --ff-only`).
  Never rebase or force-push it: other sessions push to it all day.
- Run `python3 Tools/Build/who_works_where.py query <changed paths>` and name, in the reply, whose
  areas the PR touches (CLAUDE.md "Team"). Put `Affects: <person>'s <area>` in the PR body.

## 2. Merge bleeding-edge in and resolve conflicts

```bash
git merge --no-ff --no-commit origin/bleeding-edge
git diff --name-only --diff-filter=U        # the conflicted files
```

Resolve every conflict **keeping both sides' intent** - the two branches are different people's work:

- Read both sides' commits (`git log --oneline origin/bleeding-edge -- <file>` and the same on Ys) before choosing.
- **Generated files** (`Docs/perf/status.json`, the who-works-where map, studio previews, cards, any
  `author_*` output): regenerate with their tool, never hand-merge.
- **Shared numbers** (enum values, `GameModes` ids, GUIDs, Netcode hashes): if both sides claimed the same
  one, stop and ask the person - renumbering serialized values breaks assets.
- **Scenes / prefabs**: never hand-merge YAML overrides you do not understand; take one side whole and
  name what the other side changed in the PR so its owner re-applies it, or ask.
- **Docs and logs** (`Docs/TEAM_TASKS.md`, `UNITY_VERIFICATION_CHECKLIST.md`, sync logs): keep every row from both sides.
- Only when both sides changed the same logic and picking either loses behaviour: ask the person.

Commit the merge as `merge: bleeding-edge (<short sha>, #<PR>) into Ys-bleeding-edge before the PR`,
listing each conflict and how it was resolved, plus the attribution lines.

## 3. Gates on the merged tree

```bash
bash .claude/skills/ys-to-bleeding-edge/gates.sh
```

It runs the static checks `bleeding-edge-guard.yml` runs, every `author_*` generator's `--check`, and the
headless reference compile (player config must be `RESULT: OK`; the editor config gates only files changed
since bleeding-edge - its standing 3 errors in `AuthenticationServiceFacade.cs` come from an unfetchable
package). Anything red is fixed on `Ys-bleeding-edge` before the PR - root cause, never by skipping a check.

## 4. Push

```bash
git push -u origin Ys-bleeding-edge
```

Rejected because someone pushed meanwhile: `git fetch origin Ys-bleeding-edge`, `git merge origin/Ys-bleeding-edge`,
re-run the gates if the new commits touched C#, push again. Network failures: retry 4 times (2s, 4s, 8s, 16s).

No .NET SDK in the session (gates.sh prints SKIP): install it once, about 1 minute -
`curl -sSL https://dot.net/v1/dotnet-install.sh -o <scratchpad>/dotnet-install.sh && bash <scratchpad>/dotnet-install.sh --channel 8.0 --install-dir $HOME/.dotnet`.
The first compile then takes about 10 minutes (it fetches Unity references), later ones about 30 s.

## 5. Open or refresh the PR

Use the GitHub MCP tools (`mcp__github__*`; load them with ToolSearch).

- `list_pull_requests` with `head: froglet-studio:Ys-bleeding-edge`, `base: bleeding-edge`, `state: open`.
  **One open PR from this branch already carries every new push** - update its title and body, do not open a second.
- Otherwise `create_pull_request` (head `Ys-bleeding-edge`, base `bleeding-edge`).
- Title: `merge(Ys-bleeding-edge): <the 3-5 headline changes>`.
- Body, in this order:
  - **Summary**: the ahead count, the bleeding-edge commit merged in first, and the conflicts resolved.
  - **What it carries**: grouped by area, with short shas.
  - **Verification**: the gates.sh result, and the compile results with their numbers. State plainly
    that `/verify-unity` did not run if no Unity CLI was available.
  - **To test by hand**: one line per changed behaviour.
  - **Affects**: whose areas the PR touches.
  - The PR footer from the session's attribution reminder.

## 6. Watch it

- Call `subscribe_pr_activity` for the PR.
- **CI red**: fix it on `Ys-bleeding-edge` and push.
- **New pushes from other sessions**: re-run the gates if they touched C#, and refresh the PR body.
- **Codex review "usage limits" comments**: not code findings. Tell the person and move on.
- **The merge**: when the PR merges, check `git merge-base --is-ancestor origin/Ys-bleeding-edge origin/bleeding-edge`
  and report. Mention that GitHub may have deleted `Ys-bleeding-edge`, and recreate it from bleeding-edge
  if the person wants it back now.
- Never approve or merge the PR yourself. The person or Garrett merges it.

## Report to the person

Report in plain words:

- **Status**: the PR link, and whether it merges with no conflicts.
- **Conflicts**: which ones were resolved, and how.
- **Verification**: what was checked and what was not.
- **Hand tests**: the ones owed in Unity.
- **Who to tell**: whose areas the PR touches.
