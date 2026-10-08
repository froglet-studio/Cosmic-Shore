# Branch archive: `claude/delete-changeset-bleeding-edge-krwufx`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-06 by Claude
- **Unmerged commits:** 3
- **Forked from:** `3c607fe03` (2026-08-04, Merge branch 'bleeding-edge' of https://github.com/froglet-studio/Cosmic-Shore)
- **Tip:** `95c1769ef`
- **Files touched (3):**
  - `Cosmic-Shore.slnx`
  - `Docs/BRANCHING_AND_RELEASE.md`
  - `GIT_RULES.md`

### `e06d1fb4a` — docs(git): reconcile GIT_RULES with the real branch model, add promotion runbooks

_Claude, 2026-08-05 13:22:15 +0000_

```text
GIT_RULES.md described a branching model this repository does not have. It
referenced a `main` branch nine times, told readers to target it with their PRs
and to rebase onto it, and never mentioned bleeding-edge, development or master
at all. Anyone onboarding had two branching documents that contradicted each
other, and the more official-sounding one was the wrong one.

Section 1.1 is now the real long-lived branches, with the one-way flow rule and
a pointer to Docs/BRANCHING_AND_RELEASE.md as the authority. Every `main`
reference is updated. Branch naming now acknowledges that claude/* and codex/*
are the majority of branches here rather than pretending everything is
feature/*; they follow the same rules and the prefix is worth keeping.

Section 4 gained the exception that would have cost us a rescue today. Squash
and merge remains right for feature branches, but it is exactly wrong for a
promotion: squashing discards the shared history that makes the next promotion a
fast-forward, which is how master and development drifted 3000 commits apart in
the first place. The two cases are now tabulated, with the note that a promotion
presenting conflicts means something committed directly to the target and the
stray commit is the bug, not the conflicts.

BRANCHING_AND_RELEASE.md gains the two routine operations as runbooks: promoting
bleeding-edge into development before a cycle, and cutting a release. Both are
written around `git merge --ff-only`, and both say plainly that a refusal is a
signal to investigate rather than an obstacle to force past. The release runbook
also records why the version bump belongs on bleeding-edge rather than master,
and why a store submission needs a release/* branch instead of building from a
master that can move underneath a multi-day review.

Both fast-forward claims verified against the current branch tips.
```

```text
 Docs/BRANCHING_AND_RELEASE.md | 68 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 GIT_RULES.md                  | 70 +++++++++++++++++++++++++++++++++++++++++++++++++++++++---------------
 2 files changed, 123 insertions(+), 15 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 200 lines)</summary>

```diff
diff --git a/Docs/BRANCHING_AND_RELEASE.md b/Docs/BRANCHING_AND_RELEASE.md
index 434e690b7..81429db5a 100644
--- a/Docs/BRANCHING_AND_RELEASE.md
+++ b/Docs/BRANCHING_AND_RELEASE.md
@@ -106,6 +106,74 @@ fixed comes straight back. Fix it on `bleeding-edge` instead.
 > workflow**, leaving `source_ref` blank. Manual runs skip both the clock and
 > the cycle check, so it will run immediately.
 
+### Runbook: promote `bleeding-edge` to `development`
+
+Do this before a Wednesday cycle when you want testers on a newer batch. Skip it
+and the cycle simply rebuilds what testers already have, which is a valid choice.
+
+```bash
+git fetch origin
+git checkout development
+git merge --ff-only origin/bleeding-edge
+git push origin development
+```
+
+That is the whole operation. `--ff-only` is the safety catch, not a formality:
+it succeeds only if `development` has no commits of its own. **If it refuses,
+do not force it and do not merge manually.** Something has committed directly to
+`development`, which breaks the one-way rule, and that stray commit is the thing
+to find. See §6 R6.
+
+Verify before the cycle runs:
+
+```bash
+git diff --stat origin/development origin/bleeding-edge   # empty = in sync
+```
+
+Prefer the GitHub UI? Open a PR from `bleeding-edge` into `development` and use
+**Create a merge commit**. **Never squash a promotion** — it discards the shared
+history that makes the next one a fast-forward, which is exactly how these
+branches drifted 3000 commits apart before.
+
+### Runbook: cut a release
+
+There is no release automation yet (§6 R5), so this is deliberate and manual.
+Release from `development`, not `bleeding-edge`: `development` is the code that
+has actually been through a test build.
+
+1. **Pick the commit.** Normally `development`'s tip, and normally one that
+   testers have already been running for a cycle. If you need an older one, take
+   the `testbuild/YYYY-MM-DD` tag for the build QA signed off on.
+
+2. **Set the version.** Bump `bundleVersion` in `ProjectSettings/ProjectSettings.asset`
+   on `bleeding-edge` and let it flow down, rather than editing it on `master`,
+   which would give `master` a commit of its own and break the fast-forward rule.
+
+3. **Move `master`:**
+
+   ```bash
+   git fetch origin
+   git checkout master
+   git merge --ff-only origin/development
+   git push origin master
+   ```
+
+   Same `--ff-only` rule, same reasoning.
+
+4. **Tag it**, so the shipped build is recoverable after the branches move on:
+
+   ```bash
+   git tag -a v0.3.0 -m "Release 0.3.0"
+   git push origin v0.3.0
+   ```
+
+5. **Build from `master` in UGS**, manually. Until R1 lands, the tag is the only
+   link between what players are running and a commit, so do not skip step 4.
+
+6. **Store submission?** Do not build directly from `master` for a submission
+   that will sit in review for days while `master` may move. Cut
+   `release/<version>` from the tagged commit and build from that. See §6 R5.
+
 ---
 
 ## 4. How you find out something broke
diff --git a/GIT_RULES.md b/GIT_RULES.md
index d16db7d93..edf8dbfb7 100644
--- a/GIT_RULES.md
+++ b/GIT_RULES.md
@@ -14,16 +14,26 @@ Our goals:
 
 ## 1. Branching Model
 
-### 1.1 Main Branches
+### 1.1 Long-lived Branches
 
-- `main`
-  - Always **stable & releasable**
-  - No direct pushes. Changes come **only via Pull Requests**.
-- `release/*` (optional)
-  - For preparing releases (store submissions, milestone builds, etc.)
-  - E.g. `release/2025.12.0`
+> **Full detail, including the build schedule, lives in
+> [`Docs/BRANCHING_AND_RELEASE.md`](Docs/BRANCHING_AND_RELEASE.md).** This is the
+> short version. If the two ever disagree, that document wins.
 
-> If in doubt, target `main` with your PR.
+| Branch | What it is | Who writes to it |
+|---|---|---|
+| `bleeding-edge` | **Trunk.** Where all development lands, and the repository default branch. Internal build every Friday. | Everyone, via PR |
+| `development` | What testers get. Test build every 3 weeks. | Receives from `bleeding-edge` only |
+| `master` | What players get. Release builds only. | Receives from `development` only |
+| `build/android`, `build/windows` | Robot-owned snapshots that Unity Build Automation reads. | **The promotion workflow only** |
+
+Work flows one direction: `bleeding-edge` → `development` → `master`. Nothing
+flows back up, and `development` and `master` never take their own commits. That
+is what keeps every promotion a fast-forward.
+
+> **If in doubt, target `bleeding-edge` with your PR.**
+
+There is no `main` branch in this repository. Do not create one.
 
 ---
 
@@ -49,12 +59,18 @@ Short-lived branches for actual work:
 
 > One logical task per branch. If you feel like "this branch is doing 5 different things", split it.
 
+**Agent-created branches.** Claude Code and Codex open branches named
+`claude/<description>-<id>` and `codex/<description>`. These are the majority of
+branches in the repository. They follow the same rules as any other short-lived
+branch: one logical task, PR into `bleeding-edge`, deleted after merge. Do not
+rename them to `feature/*`; the prefix is how you tell at a glance who opened it.
+
 ---
 
 ### 1.3 Branch Checklist (Before Push)
 
 - [ ] Branch name follows `type/area-short-description`
-- [ ] Branch is based on **latest `main`**
+- [ ] Branch is based on **latest `bleeding-edge`**
 - [ ] Only **one logical feature/fix/chore** in this branch  
 - [ ] No temporary test scenes / PlayGround scenes that aren’t intended to be kept
 
@@ -169,7 +185,7 @@ Notes:
 
 ## 3. Pull Requests
 
-All changes to `main` must go through **Pull Requests**.
+All changes to `bleeding-edge` must go through **Pull Requests**.
 
 ### 3.1 PR Titles
 
@@ -248,8 +264,8 @@ If UI or gameplay changed, attach:
 
 Before assigning reviewers:
```

</details>

### `774025641` — Testing game

_Hunter VonWald, 2026-08-05 19:40:01 -0500_

```text
 Cosmic-Shore.slnx | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `95c1769ef` — revert(sln): drop accidental "Testing game" solution reorder

_Claude, 2026-08-06 02:28:53 +0000_

```text
Reverts 774025641cea718349acf698dfeea501cc72de96, an IDE-generated
reorder of Assembly-CSharp-Editor.csproj in Cosmic-Shore.slnx that was
pushed to bleeding-edge unintentionally. No gameplay or build impact;
restores the project ordering that preceded it.
```

```text
 Cosmic-Shore.slnx | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```
