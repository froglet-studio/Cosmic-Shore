# /ship §3.57 Compute capture in full

<!-- Moved word for word out of .claude/skills/ship/SKILL.md on 2026-11-13 (Shombith: "/ship is very slow; it does not need to read every line every time"). SKILL.md keeps the procedure; this file is read when `ship_triage.py` or the step says so. -->

Read when SKILL.md's step says so (its trigger matched this branch).


The team's idle machines search whatever is queued in `Docs/Compute/jobs/` (`Docs/Compute/README.md`). A branch
feeds that queue, or breaks it, in three ways; check each:

```sh
git diff --name-only <merge-base>..HEAD | grep -E '^(Tools/Compute/|Docs/Compute/jobs/)'      # the system itself
git diff --name-only <merge-base>..HEAD | grep -E '^Tools/Build/[^/]*(_sim|_balance|_budget)\.py$'   # a model a job imports
git diff <merge-base>..HEAD -- 'Tools/Build/*.py' | grep -cE '^\+.*(--sweep|itertools\.product\()'   # a new hand-rolled search
```

- **The system or a job file changed**: `python3 Tools/Compute/compute.py self-test` and `validate` are green
  before the PR; a changed loss bumped its evaluator's `VERSION`.
- **A model a job imports changed** (`race_models.py` names them): its jobs start a new epoch by themselves (the
  inputs hash); say so in the PR, because the board's numbers reset for those jobs.
- **A new hand-rolled search**: one line in the PR ("a candidate compute job: <script>"); `/compute-scout`'s
  `sweeps` lens will list it. Converting it is a `/compute-job`, never part of this branch unless it asked.

**Report**: the three answers, or "no compute surface touched".
