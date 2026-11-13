# Land queue

One entry per `/land-queue` run (`.claude/skills/land-queue/`), newest last: what landed in `bleeding-edge`, what is blocked and why, what was closed, and the open count before and after. Written by `land_queue.py record`; append, never rewrite.

## 2026-11-12 16:39 UTC - Shombith

- Open PRs by Shombith03: 42 before, 42 after.
- Merged into `bleeding-edge`: none.
- Closed (branch kept): none.
- First run, read-only, while building the skill (claude/dreamy-goldberg-enztvh): 10 NEEDS-REFRESH, 2 RED (#1695 Unity compile, #1751 Environments exist), 28 STALE, 2 SUPERSEDED (#130, #530). Nothing merged or closed: the trunk was red (Bleeding-edge guard: author_corona/flatline/shatterplane --check fail on a missing numpy in CI). Dry-run refreshes: #1748 merges clean and passes the fast gates; #1738 is BLOCKED on Docs/UI_STYLE_GUIDE.md and Docs/UNITY_VERIFICATION_CHECKLIST.md.
