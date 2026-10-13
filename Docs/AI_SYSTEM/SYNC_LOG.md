# AI System — Sync Log

One row per merge into or out of the AI home branch (newest first): `ai-system` until 2026-10-09, `Ys-bleeding-edge` since. The merge commit carries the detail; this is
the index. Workflow: `BRANCH_WORKFLOW.md`.

| Date | Direction | Sources (tip) | Conflicts | New AI brought in | Verified | Merge commit |
|---|---|---|---|---|---|---|
| 2026-10-09 | ai-system → peaceful-rubin → **Ys-bleeding-edge** (fast-forward; ai-system then deleted) | peaceful-rubin `5142dcd8c` (contains ai-system `978d50841`, vessel-studio, Ys `533fdfeab`) | 0 (Ys had nothing peaceful-rubin lacked) | none new; the Squirrel Vessel Studio AI lab came with it | docs and ai_branch_sync.py --self-test | fast-forward to `5142dcd8c` + docs commit |
| 2026-10-09 | ai-system → claude/peaceful-rubin-hhw49n (by the peaceful-rubin session) | ai-system `978d50841` | see merge | — | see merge | `19130c153` |
| 2026-10-08 | Ys-bleeding-edge → ai-system (branch cut from perf `1f3c7e829`) | Ys `be50ff6ba` (contains bleeding-edge `afd66621d`) | 8: three Skim Race AI files, Trail, two blank-line hunks, two docs | Urchin rail choice in Regatta, Scarab jukes, Grizzly AI | refcompile player 0 / editor 4 known; all AI and repo gates | `991797006` |
