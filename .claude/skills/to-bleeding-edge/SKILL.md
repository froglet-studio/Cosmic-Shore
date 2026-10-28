---
name: to-bleeding-edge
description: ALIAS of `/ship promote [branch]` - promote an integration branch (Yash's Ys-bleeding-edge, or any branch) into bleeding-edge through a PR: merge bleeding-edge in, resolve conflicts on the branch, run the gates, push, open or refresh the one PR. Kept so the habit still works; the protocol lives in /ship (SKILL.md 1.5 and 5). Use on "/to-bleeding-edge", "/to-bleeding-edge Ys-bleeding-edge", "merge <branch> to bleeding-edge", "promote this branch to bleeding edge".
---

# `/to-bleeding-edge [branch]` = `/ship promote [branch]`

Folded into `/ship` on 2026-10-26 (Garrett: one shipping process, so two skills cannot drift apart). This file
is the alias; **read `.claude/skills/ship/SKILL.md` and run its §1.5 (fetch, merge bleeding-edge in, resolve
every conflict on the branch, `bash .claude/skills/ship/gates.sh`, push) and §5-§5.5 (the PR, in the
`/ship promote` shape; fire CI once).** No review pass and no go/no-go: a promote carries work that was reviewed
PR by PR on its way into the integration branch.

The argument is the branch to promote; with none, the current branch. Past runs of this lane: `Ys-bleeding-edge`
#1078, #1081, #1086, #1098, #1104, #1150.
