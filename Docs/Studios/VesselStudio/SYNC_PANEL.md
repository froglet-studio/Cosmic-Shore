# The Sync panel: keeping a shared studio artifact up to date

Two people work on the same studio from two Claude sessions and push to GitHub often. The **Sync**
button (bottom right of every page of the Vessel Studio artifact) brings the artifact up to date,
shows what changed, merges branches (then offers to delete the merged one), and keeps a shared log of
decisions.

- Live: https://claude.ai/artifact/8YakjgME9H7kNuiVyNXGzc
- Source: `Docs/Studios/VesselStudio/sync.js`. It is injected into the pages at publish time by
  `.claude/skills/vessel-studio/build_artifact.py`, and by Refresh. The studio pages in the repo do not
  carry it.
- Skill: `/vessel-studio` (§4 publishing, §5 the jobs and how a session handles them).

## How it works

The page never touches GitHub. Each button writes a **job** into the artifact's shared database and
sends a message to **your Claude Code session**, through the built-in Claude Code Remote connector
that every claude.ai account has. That session already has the repo:
- it does the git work with `.claude/skills/vessel-studio/sync_job.py`;
- it writes progress into the job, which the panel's console prints as it arrives;
- after a refresh or merge, it rebuilds and republishes the artifact.

No GitHub connector and no organization approval are needed.

## Before the first use

1. **Share the artifact** with your colleague from its Share menu, with **edit** access.
2. **Claude session that does the git work** (first section of the panel):
   - The field starts with the session that published the artifact, which only its owner can message.
   - Anyone else presses **Start session**. That starts a Claude Code session on their own account,
     on the branch the sync tools live on, with standing instructions to handle sync jobs.
   - Or paste the id of a session you already have open (from its URL, `session_...`).
   - The panel remembers your choice for you alone.
3. Allow the panel to use your Claude sessions the first time it asks.

A session has to be running to pick a job up. If it has ended, Refresh says it could not reach it;
press **Start session** and try again.

## What each part does

### Refresh from GitHub

- **Pull studio from branch** sets which branch the artifact's pages come from.
- **Refresh** sends a `refresh` job. The session compares the commit the artifact shows with the newest
  commit on that branch that touched `Docs/Studios/VesselStudio/`:
  - Up to date: the console says so.
  - Newer: the session rebuilds the hub and studio pages from that branch, adds the panel, and
    republishes. Every open copy reloads.
- The console also lists, for each watched branch, how many commits it has that yours lacks, as
  **merge suggestions**. These are exact counts.

### Console

- It shows each job as it is sent, picked up, its progress lines, and done or failed.
- **Copy log** copies it.
- It empties on reload. The decision log and the job records keep the history.

### Merge

1. **From** is the branch with the changes, **Into** is the branch to merge into. **⇄ Swap** swaps them.
2. **Compare** asks the session for ahead/behind and the commits, then shows **Yes, merge now** or
   **Not now**.
3. **Merge** compares, then goes straight to the confirm step.
4. **Confirm merge** sends a `merge` job:
   - The session merges in a scratch copy and pushes **Into**.
   - If the branches conflict, nothing is pushed. The console lists the files; ask a Claude session to
     merge them by hand.
5. **After a successful merge, a popup asks whether to delete the merged branch.**
   - **Delete** sends a `delete` job, and the session deletes the branch on GitHub.
   - **Keep** leaves it.
   - Both are recorded in Decisions.
6. Never touched from the panel:
   - `bleeding-edge`, `Ys-bleeding-edge`, `main` and `master` (no merging into them, no deleting them);
   - the session's own branch and the tools branch (`claude/peaceful-rubin-hhw49n`) are never deleted.

### Decisions

Type what you decided and press **Record decision**. Everyone with access sees the list, newest first,
with who, when, and which branch and commit the artifact showed. Refresh, merge, delete and "not now"
entries are added automatically and tagged.

### Closing

The **×**, a press anywhere outside the panel, or **Escape** closes the panel or popup.

## Data

| Collection | One document per | Fields |
|---|---|---|
| `decisions` | decision or event | `text`, `kind` (decision, refresh, merge, delete), `by` (user id; names are resolved when shown, never stored), `at`, `branch`, `sha` |
| `jobs` | button press | `kind`, `args`, `status` (queued, running, done, failed), `by`, `at`, `session`, `artifact`, `log`, `result` |
| `data/users/<id>/sync` | person (private) | `session`: the Claude session that person's jobs go to |

Everyone who can open the artifact reads `decisions` and `jobs`; Contributors and up write them.

## Limits

- A running Claude session is needed for Refresh, Compare, Merge and Delete. A job takes as long as
  the session needs: seconds for compare, a minute or so for refresh or merge.
- A page can only message sessions of the person using it, so each person uses their own session.
- Opened outside claude.ai (a file in a browser, Prisma's app window), the panel shows the console only.
