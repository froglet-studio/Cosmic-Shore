# Launch Console (the artifact)

**https://claude.ai/artifact/XwF8FQrHhUTvNv1gE8kws2** (private; share it from the page's Share menu). Its source is
`index.html` here; publish it with the Artifact tool to that URL. The FrogletTools editor window reads the same
`launch-status` branch directly (`Docs/LAUNCH/STATUS_CONTRACT.md`).

What it holds, in its own store (ArtifactData):

| Store path | What | Who writes |
|---|---|---|
| `status/board` | the pipelines' merged `status.json` from the `launch-status` branch | a Claude session: `console_sync.py board` (admin only) |
| `status/quality` | QA backlog, EditMode, verification counts, with source and date | a Claude session (admin only) |
| `checklist/*` | the launch checklist, seeded from `READINESS.md` §10 | the team on the page (status, taken by, due, evidence) |
| `runbook/*` | the T-7 … T+7 timeline | the team on the page (step status) |
| `changelog/*` | each release's generated notes | a Claude session, from the release workflow's draft releases |

Refresh the pipeline board from a session (the page cannot reach GitHub itself; no claude.ai GitHub connector exists):

```bash
python3 Tools/Launch/console_sync.py board --out /tmp/console-sync
```

then send `/tmp/console-sync/writes.json` as one ArtifactData `batch` to the console's URL. `seed` ran once on
2026-11-05; it refuses to run again from the same folder because it would overwrite the team's edits.

Capabilities: `db` (rules: shared data read by every viewer, written by editors; `checklist` and `runbook` written by
any viewer), `user` (names for "taken by"), `sample` ("Ask about the launch", answered only from the page's data).
The page's two repository links (the runbook and the readiness audit) point at the
`claude/exciting-pascal-t8ijbo` branch on purpose: the published page has to work before that branch merges, and GitHub
deletes the branch when it does. **The first republish after the merge switches them to `bleeding-edge`** (search
`index.html` for `exciting-pascal`). `console_sync.py` already links the checklist rows to `bleeding-edge`; the rows
seeded on 2026-11-05 still carry the branch link and the store-form owners as seeded, until the team edits them on
the page. Action buttons are plain links to the workflow or dashboard; production steps wait for a reviewer there. The page
holds no tokens. Metrics show "not connected" until an analytics source exists.
