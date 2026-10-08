# AI System — start here

`ai-system` is the branch where every vessel AI in Cosmic Shore is reviewed, restructured, diagnosed,
tested and tuned. Others keep writing AI on `bleeding-edge`; it is pulled here, taken in, and diagnosed one
AI at a time. Performance testing runs in parallel on `perf/performance-optimization`, and the two
branches exchange work by merge.

| Read | When |
|---|---|
| [`BRANCH_WORKFLOW.md`](BRANCH_WORKFLOW.md) | Before any merge into or out of `ai-system`: the four branches, which way work flows, conflict rules, verification |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Before touching any AI: the six layers, the roster of every AI (source of truth), what is wrong today, the target architecture |
| [`DIAGNOSIS_PLAYBOOK.md`](DIAGNOSIS_PLAYBOOK.md) | Before diagnosing an AI or taking a new one in: the five tiers of evidence, the procedure, the intake checklist, the user's test script, the status board |
| [`SYNC_LOG.md`](SYNC_LOG.md) | One row per merge |
| [`../SKIM_RACE_AI.md`](../SKIM_RACE_AI.md) | The Skim Race pilot in depth (the most developed AI; its methods are the model for the rest) |

**First command of every AI session:**

```sh
python3 Tools/Build/ai_branch_sync.py
```

It fetches the three source branches and reports what is waiting, which of it is AI work, and which AI
files the roster does not know yet. It only reads; merging is a separate, deliberate step.
