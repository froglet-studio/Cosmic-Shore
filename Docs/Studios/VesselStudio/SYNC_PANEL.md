# The Sync panel: keeping a shared studio artifact up to date

Two people work on the same studio from two Claude sessions and push to GitHub often. The **Sync**
button (bottom right of every page of the Vessel Studio artifact) brings the artifact up to date,
shows what changed, merges branches, and keeps a shared log of decisions.

- Live: https://claude.ai/artifact/8YakjgME9H7kNuiVyNXGzc
- Source: `Docs/Studios/VesselStudio/sync.js`. It is injected into the pages at publish time by
  `.claude/skills/vessel-studio/build_artifact.py`, and by Refresh itself. The studio pages in the
  repo do not carry it.
- Skill: `/vessel-studio` (§4 publishing, §5 the collaboration loop).

## Before the first use

1. **Share the artifact** with your colleague from its Share menu, with **edit** access. View access
   still shows the panel, but read-only.
2. **Add the GitHub connector** in claude.ai Settings → Connectors (each person, once).
3. Open the artifact and press **Sync** → **Refresh**. Allow GitHub when asked.

## What each part does

### Refresh from GitHub

- **Pull studio from branch**: the branch the artifact's pages come from. It starts on the branch
  the artifact was built from and is remembered per person.
- **Refresh** compares the commit the artifact shows (`build.json`) with the newest commit that touched
  `Docs/Studios/VesselStudio/` on that branch:
  - Up to date: the console says so.
  - Newer: it fetches `index.html`, every studio page in `studios.json`, and `studios.json`.
    It adds the panel and publishes them as a new version of the artifact. Every open copy reloads
    to it, and the refresh is noted in Decisions.
  - Switching the branch and pressing Refresh switches the studio to that branch.

### Console

Each Refresh logs:
- the repo;
- the commit the artifact shows;
- the branch head (hash, subject, time);
- the new studio commits;
- **merge suggestions**: for each watched branch (`cece/magical-carson-9bdq8z`,
  `claude/peaceful-rubin-hhw49n`, `vessel-studio`, `Ys-bleeding-edge`, `bleeding-edge`), how many commits
  it has that yours lacks.

**Copy log** copies it. The console is per view and empties on reload; Decisions keep the record.

### Merge

1. Type or pick **From** (the branch with the changes) and **Into** (the branch to merge into).
   **⇄ Swap** swaps them.
2. **Compare** shows ahead and behind, the commits that would come over, and the question:
   **Yes, merge now** · **Open a PR only** · **Not now**. **Merge** goes straight to the confirm step.
3. Nothing reaches GitHub until **Confirm merge** (or **Open PR**):
   - it reuses an open PR From → Into, or opens one listing the commits;
   - it merges with a merge commit, as you.
4. **After the merge, a popup asks whether to delete the merged branch**:
   - **Delete** opens that branch on GitHub's Branches page in a new tab. Click the bin icon next to it.
     The GitHub connector cannot delete branches, so the deletion is that one click. The request is
     noted in Decisions.
   - **Keep** leaves the branch.
   - `bleeding-edge`, `Ys-bleeding-edge`, `main` and `master` cannot be deleted from here. Merging into
     them shows a warning first.
5. **Conflicts**: GitHub refuses the merge and the PR stays open. Ask a Claude session to merge the
   branches and resolve them. Branches more than 100 commits apart are also left to a session.

### Decisions

Type what you decided and press **Record decision**. Everyone with access sees the list, newest first,
with who, when, and which branch and commit the artifact showed. Refresh, merge, delete and "not now"
entries are added automatically and tagged.

A Claude session reads the log with `ArtifactData` (`list`, collection `decisions`, the artifact URL).
Start a studio round by reading it.

### Closing

The **×**, a press anywhere outside the panel, or **Escape** closes the panel. Escape closes a popup
first.

## Data

Collection `decisions`, one document per entry:

| Field | Meaning |
|---|---|
| `text` | the decision or event |
| `kind` | `decision`, `refresh`, `merge` or `delete` |
| `by` | the author's id. Names are resolved when the list is shown, never stored |
| `at` | ISO time |
| `branch`, `sha` | what the artifact showed when it was recorded |

Rules:
- everyone who can open the artifact reads the log;
- Contributors and up write to it.

## Limits

- **No delete-branch in the connector.** Delete is the GitHub page plus one click.
- **Ahead and behind are counted over the last 100 commits per branch.** "100+" means "far apart, merge
  in a session".
- **The GitHub calls run as the person who presses the button**, with their connector. The merge is theirs
  on GitHub.
- Opened outside claude.ai (a file in a browser, Prisma's app window), the panel shows the console
  but cannot reach GitHub or the decision log. It says so.

## Rebuild by hand (a session)

```sh
python3 .claude/skills/vessel-studio/build_artifact.py --ref origin/<branch> --out <scratchpad>/hub
```

Then publish `<out>/index.html` with the other files to the same URL (`/vessel-studio` §4).
