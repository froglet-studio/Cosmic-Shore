# Branch hygiene plan

How Cosmic Shore keeps its branch list clean without losing work: what was done in the October 2026
audit, how branches are preserved, the tools, the monthly routine, what is automated, and how to test it.

Owner: the studio lead (Yash) or whoever they hand the monthly routine to.
Last updated: 2026-10-09.

---

## 1. Where things stand

| | |
|---|---|
| Branches on GitHub (2026-10-08) | **505** |
| No commit since 2026-09-08 | **363** |
| …fully merged (nothing to lose) | **114**. **108** can be deleted; 6 are kept, see below |
| …small unmerged work (1–3 commits) | **141**. Each one is written up in [`small/`](small/) |
| …medium unmerged work (4–10 commits) | **51**. Each one is written up in [`medium/`](medium/) |
| …large unmerged work (11+ commits) | **56**. Each has a write-up and plain-language summary in [`large/`](large/). **Protected.** |
| Deleted so far | **none** |
| Archive tags created so far | **none** (see §2: step 1 of the first cleanup) |

The 6 merged branches that stay: `development`, `build/android`, `build/windows` (release
pipeline, see `Docs/BRANCHING_AND_RELEASE.md`), and three with open PRs (#130, #530, #799).

Decision page for marking branches, with your ticks saved:
<https://claude.ai/artifact/XrdKPvDDM2jFhZVqZw3pyw>

### What was done (2026-10-08 / 09)

1. Every remote branch was measured: last commit date, author, and how many commits it holds that are
   in neither `bleeding-edge` nor `master` ("unique commits").
2. Every inactive branch holding unique work got an **evidence doc** in this folder: its commit
   messages, every file it touched, and its code patches (150 lines per commit for small and medium,
   80 for large). The 56 large ones also open with a plain-language summary, whether the work landed
   elsewhere, a risk rating and a keep / can-go suggestion. That gives 17 to keep and 39 that can go.
3. Cleanup tools were built (§3), all applying the same rules (§4).
4. PR [froglet-studio/Cosmic-Shore#1030](https://github.com/froglet-studio/Cosmic-Shore/pull/1030)
   carries all of it. Nothing has been deleted.

---

## 2. How branches are preserved ("archived")

GitHub has no "archive branch" switch. The standard, simplest way is a **tag**. A tag pins the
branch's last commit forever, does not show in the branch list, and costs nothing:

```
archive/<branch-name>   →   the branch's last commit
```

Every tool here saves that tag **before** it deletes a branch, and keeps the branch if the tag
cannot be saved. To bring a branch back:

```bash
git fetch origin --tags
git checkout -b <branch> archive/<branch>
git push -u origin <branch>
```

**Two layers of evidence**, on purpose:

- **Tags** keep the actual commits, so the code is restorable.
- **The docs in this folder** say what each branch was, so nobody has to restore one to find out.

**First step of the first cleanup: archive everything before deleting anything.** Tagging is
harmless, so do this once for all 363 inactive branches, LARGE and locked ones included:

```bash
python3 Tools/BranchJanitor/branch_cleanup.py --mode archive --branches-file Docs/BranchArchive/inactive-363.txt
```

Or, after PR #1030 is merged: Actions ▸ Branch cleanup ▸ `archive`, with the list pasted in.

The Claude session that did the audit could not do this itself. Its git access may only push its own
branch, so GitHub refused both branch deletes and tag pushes from it.

---

## 3. The tools

All four apply the same rules (§4). Use whichever suits you.

| Tool | Where | Sign-in | Best for |
|---|---|---|---|
| **Prisma ▸ BRANCHES** | Prisma launcher, left rail | Prisma's GitHub sign-in (git / GitHub Desktop, or SETTINGS ▸ Source token) | Looking and ticking, on Windows, where you already use Prisma |
| **Branch cleanup workflow** | GitHub ▸ Actions ▸ *Branch cleanup* (`.github/workflows/branch-cleanup.yml`) | none, uses the repo's own Actions token | Running from the browser or phone, a monthly report, or asking Claude to run it. **Needs PR #1030 merged first.** |
| `branch_cleanup.py` | `Tools/BranchJanitor/` | a personal token in `GITHUB_TOKEN` | Terminal; big lists from a file. Same code the workflow runs |
| Branch Janitor page | `Tools/BranchJanitor/branch-janitor.html` | a personal token pasted in | Any browser, no Prisma |

Modes, in every tool where they apply:

| Mode | Changes | Use |
|---|---|---|
| `report` | nothing | List inactive branches by group (MERGED / SMALL / MEDIUM / LARGE) |
| `dry-run` | nothing | "What would delete do with this list?" |
| `archive` | adds tags only | Preserve branches; never deletes |
| `delete` | adds a tag, then deletes the branch | The cleanup |

---

## 4. The rules

Stored in `Tools/BranchJanitor/policy.json`. Prisma reads that file from its workspace, and the
HTML page carries a copy, so change both together.

| Never deleted | Why |
|---|---|
| `master`, `main`, `bleeding-edge`, `development`, `Ys-bleeding-edge`, `build/*`, `release/*`, `archive/*` | Trunks and the release pipeline. `development` and `build/*` go quiet for weeks by design. |
| A branch with a commit in the last **30 days** | Someone may still be on it. |
| A branch with an **open pull request** | Deleting the branch would close the PR. Close or merge the PR first. |
| A **LARGE** branch (**11+** unique commits) unless explicitly allowed | Too much work to lose by accident. Read its doc in `large/` first. |
| A branch that cannot be compared with either trunk | Treated as LARGE. |

Every deletion: re-check the branch has not moved, save `archive/<branch>`, then delete.

---

## 5. The routine

### Monthly (about 15 minutes, first week of the month)

1. **Look at the report.** After PR #1030 is merged, the workflow runs on the 1st and posts a list
   on its run page (Actions ▸ Branch cleanup ▸ latest run ▸ Summary). Or open Prisma ▸ BRANCHES ▸ LOAD.
2. **MERGED:** delete them all. Nothing is lost, and each gets an archive tag anyway.
3. **SMALL / MEDIUM:** skim the names and last messages. Delete the ones nobody claims. If one
   looks valuable, ask its author, or have Claude write it up in this folder first.
4. **LARGE:** do not delete in the routine. Bring any you want gone to a conversation, and read the
   doc in `large/` first. Deleting one needs **Allow LARGE**.
5. **Branches locked by an open PR:** check the PR list for stale PRs. Close the dead ones on GitHub,
   and next month their branches become deletable.
6. Add a line to the log (§8).

### Weekly (optional, 2 minutes)

Prisma ▸ BRANCHES ▸ LOAD ▸ MERGED tile ▸ TICK ALL ▸ DELETE ▸ CONFIRM. Merged branches are the only
group safe to clear without reading.

### Stop the pile from growing

- Turn on **GitHub ▸ Settings ▸ General ▸ "Automatically delete head branches"**. GitHub then deletes
  a PR's branch when the PR is merged. Most of the 114 merged branches would never have existed.
- Claude Code sessions create `claude/*`, `codex/*` and `cece/*` branches. Abandoned ones are what
  SMALL is mostly made of; the monthly routine clears them.

---

## 6. What is automated, and what is not

| | Status |
|---|---|
| Monthly **report** (1st of the month, 15:17 UTC) | Automatic once PR #1030 is merged. Lists only; changes nothing. |
| Deleting branches | **Always a person's decision.** Nothing deletes on a schedule. |
| Archive tag before every delete | Automatic in every tool. |
| Merged PR branches | Automatic once "Automatically delete head branches" is on (a GitHub setting, see §5). |

A possible next step, not built: let the monthly run also archive and delete **MERGED** branches on
its own, since they hold nothing. Ask Claude to add it to `branch_cleanup.py` (a `--auto-merged`
flag on the scheduled run) when the manual routine has run cleanly a few times.

---

## 7. How to test it (start here)

### A. From Prisma (recommended)

1. Get Prisma with the BRANCHES page. Until PR #1030 is merged, that means a Prisma built from branch
   `claude/trusting-curie-bn6rrn`:
   ```bash
   git fetch origin && git checkout claude/trusting-curie-bn6rrn
   cd Port && dotnet run --project src/CosmicShore.Launcher
   ```
   (`build-launcher.bat` builds `Prisma.exe` the usual way.)
2. Make sure Prisma can sign in to GitHub. If git or GitHub Desktop is signed in, nothing to do.
   Otherwise go to SETTINGS ▸ Source ▸ GitHub token and paste a fine-grained token (resource owner
   `froglet-studio`, only `Cosmic-Shore`, Contents: Read and write, Pull requests: Read).
3. Left rail ▸ **BRANCHES** ▸ **LOAD**. Expect about a minute ("Measuring … inactive branches"),
   then six tiles. Expect roughly 108 or more in MERGED, and `master` / `development` / `build/*`
   under LOCKED.
4. **Test on two branches.** On the MERGED tile, type `fix-screenshot-errors` in the filter, tick both
   `claude/fix-screenshot-errors-…` rows, then **DELETE 2…** ▸ **CONFIRM**.
5. **Check.** Both rows say `deleted, saved as archive/…`, and CONSOLE lists them. On GitHub they are gone
   from Branches, and `archive/claude/fix-screenshot-errors-LTKde` is under Tags.
6. **Test restore** on one:
   `git fetch origin --tags && git checkout -b claude/fix-screenshot-errors-LTKde archive/claude/fix-screenshot-errors-LTKde && git push -u origin claude/fix-screenshot-errors-LTKde`.
   Refresh in Prisma and it is back.
7. **The rest:** MERGED tile ▸ TICK ALL ▸ DELETE ▸ CONFIRM.
8. **Check the locks:** the LOCKED tile shows the reason on each row, and LARGE rows have no checkbox
   until **Allow LARGE** is on.

### B. From a terminal

```bash
git checkout claude/trusting-curie-bn6rrn
export GITHUB_TOKEN=github_pat_...
python3 Tools/BranchJanitor/branch_cleanup.py --mode report
python3 Tools/BranchJanitor/branch_cleanup.py --mode archive --branches-file Docs/BranchArchive/inactive-363.txt
python3 Tools/BranchJanitor/branch_cleanup.py --mode dry-run --branches-file Docs/BranchArchive/merged-108.txt
python3 Tools/BranchJanitor/branch_cleanup.py --mode delete  --branches-file Docs/BranchArchive/merged-108.txt
```

### C. From GitHub (after PR #1030 is merged)

Actions ▸ Branch cleanup ▸ Run workflow. Pick a mode, paste branch names, and Run. The result table is
on the run's Summary page. Claude can start this for you from a chat.

---

## 8. Log

| Date | Who | What |
|---|---|---|
| 2026-10-08 | Claude (session) | Audit of 505 branches; evidence docs for 248; decision page; tools; PR #1030. No deletions. |
| 2026-10-09 | Claude (session) | Prisma BRANCHES page; `archive` mode; this plan. No deletions (session cannot push tags or delete). |

---

## Files

| File | What |
|---|---|
| `Docs/BranchArchive/README.md` | Status, index of every archived branch, step-by-step deletion |
| `Docs/BranchArchive/{small,medium,large}/` | One evidence doc per branch |
| `Docs/BranchArchive/inactive-363.txt` · `merged-108.txt` | Branch lists for `--branches-file` |
| `Tools/BranchJanitor/policy.json` | The rules |
| `Tools/BranchJanitor/branch_cleanup.py` · `.github/workflows/branch-cleanup.yml` | Script and workflow |
| `Tools/BranchJanitor/branch-janitor.html` | Browser page |
| `Port/src/Shared/BranchCleanup.cs` · `Port/src/CosmicShore.Launcher/LauncherApp.Branches.cs` | Prisma's BRANCHES page (`Port/docs/LAUNCHER.md`) |
