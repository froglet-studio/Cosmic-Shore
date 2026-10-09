# Branch Janitor

Ways to clean up branches, all with the same rules (`policy.json`). The routine and test steps are in
`Docs/BranchArchive/BRANCH_HYGIENE_PLAN.md`.

- **Prisma ▸ BRANCHES**: the launcher page, using Prisma's GitHub sign-in (`Port/docs/LAUNCHER.md`).

- **Branch cleanup workflow** (recommended): GitHub ▸ Actions ▸ *Branch cleanup* ▸ Run workflow. Modes `report`,
  `dry-run`, `archive` (tags only), `delete`; paste branch names; tick `allow_large` only on purpose. Needs no personal token. Runs
  `branch_cleanup.py`. A report-only run happens on the 1st of every month (see the run's summary page).
- **Branch Janitor page** (below): a browser page for picking branches visually, using your own token.

---

A single HTML page that lists every branch in `froglet-studio/Cosmic-Shore`, sorts the inactive ones into
groups, and deletes the ones you tick. It talks to GitHub directly from your browser. There is no server and
nothing to install.

## Open it

Double-click `Tools/BranchJanitor/branch-janitor.html`, or open it from any browser with **File ▸ Open**.

## Get a token (once)

GitHub ▸ Settings ▸ Developer settings ▸ **Fine-grained personal access tokens** ▸ Generate new token:

- **Resource owner:** `froglet-studio`
- **Repository access:** Only `Cosmic-Shore`
- **Permissions:** Contents → *Read and write*; Pull requests → *Read*

Paste it into the page. It stays in that browser tab's memory and is gone when you close the tab. Never commit it.

## Use it

1. **Load branches.** The page lists every branch and measures how many commits each one has that are not in
   `bleeding-edge` or `master` ("unique commits"). With ~500 branches this takes about a minute.
2. **Tick** the branches to delete. **Tick all merged** selects every branch with 0 unique commits.
3. **Review and delete…**, type `DELETE`, then **Delete now**.

For each ticked branch the page re-reads the branch tip and skips it if it moved since you loaded. Then it saves
the tip as tag `archive/<branch>` and deletes the branch. To bring one back:

```bash
git fetch origin --tags
git checkout -b <branch> archive/<branch>
git push -u origin <branch>
```

No token with write access? **Copy as shell script** gives you the same deletes as a bash script to run from a
clone that has push rights.

## What it never deletes

These rules live in `policy.json` (used by the workflow) and the `POLICY` block at the top of the HTML. Change both
together and commit, so everyone uses the same rules.

| Rule | Default | Why |
|---|---|---|
| Trunk and pipeline branches | `master`, `main`, `bleeding-edge`, `development`, `Ys-bleeding-edge`, `build/*`, `release/*`, `archive/*` | The release pipeline (`Docs/BRANCHING_AND_RELEASE.md`) depends on them. `development` and `build/*` go quiet for weeks by design. |
| Active branches | a commit in the last **30** days | Someone may still be working on it. |
| Branches with an open pull request | always | Deleting the branch closes the PR. Close or merge it on GitHub first. |
| **Large** branches | **11 or more** unique commits | Too much work to lose by accident. Their checkboxes stay locked unless you tick **Allow ticking LARGE branches**. Read the branch's archive doc in `Docs/BranchArchive/large/` first. |
| Unmeasurable branches | GitHub could not compare the branch with either trunk | Treated as Large. |

## Before a cleanup, write the evidence

`Docs/BranchArchive/` holds a written record of what each inactive branch did: its commit messages, the files
it changed, and its code patches. Write those docs before you delete, because a deleted branch's commits are only
recoverable through its `archive/` tag. See `Docs/BranchArchive/README.md` for the 2026-10-08 cleanup.
