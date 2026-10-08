# Branch archive: `claude/reorient-55psg4`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-11 by Claude
- **Unmerged commits:** 2
- **Forked from:** `fabe70748` (2026-08-12, Merge remote-tracking branch 'origin/claude/quit-game-button-ou0r4j' into blee)
- **Tip:** `b82998f23`
- **Files touched (2):**
  - `Docs/QA/QA_BACKLOG.md`
  - `Docs/UNITY_VERIFICATION_CHECKLIST.md`

### `b82998f23` — docs(qa): mark the backlog stale against a trunk 148 commits ahead

_Claude, 2026-08-11 21:18:49 +0000_

```text
The merge of bleeding-edge falsified two statements this branch shipped.

- UNITY_VERIFICATION_CHECKLIST.md's supersession banner claimed "the two
  entries below". Upstream kept recording unverified work there after our
  branch point, so it now carries seven, five of them new. The file is still
  load-bearing until /qa-backlog absorbs them; the banner now says so and
  names them.
- QA_BACKLOG.md advertised a scan up to 2e2d3aaf with no indication of how far
  behind that had fallen. It is 148 commits / PRs #674-#702 stale, missing two
  whole game modes. QA reading it as complete is the failure mode; the header
  now states the gap and lists what is absent.

The 47 items are untouched and remain valid - incomplete, not wrong. Contents
stay skill-owned; this records coverage, it does not hand-edit the list.
```

```text
 Docs/QA/QA_BACKLOG.md                | 11 +++++++++++
 Docs/UNITY_VERIFICATION_CHECKLIST.md |  8 +++++++-
 2 files changed, 18 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 6d223b11d..fc213fa04 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -3,6 +3,17 @@
 **Generated:** 2026-08-06 · **Scan covers:** merges up to `2e2d3aaf` (PRs #583–#669)
 · **Owner of this file:** the `/qa-backlog` skill — do not hand-edit.
 
+> ⚠️ **This list is behind the trunk — re-run `/qa-backlog` before handing it to QA.**
+> As of 2026-08-11 `bleeding-edge` is **148 commits / 70 feat-fix commits / PRs #674–#702**
+> past the scan above. Not covered here: the two new game modes (**Wildlife Liberation**
+> `GameModes 40`, **Dog Fight** `GameModes 41`), the Astro League sword-soccer rework, the
+> Sparrow ability redesign + turret-stance flight visuals, the Dolphin speed/boost and
+> capsule-blast retunes, the prism occlusion-dither (SHATTER) and debris-erosion work, the
+> CI guard/build pipeline, the PostHog/UGS analytics layer, the consent dialog, the display-name
+> validation path, and the Cinemachine-free menu camera rig. Five of these also still sit as 🔴
+> sections in `../UNITY_VERIFICATION_CHECKLIST.md`, which stays load-bearing until absorbed.
+> **The 47 items below remain valid** — they are incomplete, not wrong.
+
 Every item below landed on a shared branch **without ever being opened in Unity**
 by its author (or was play-tested only in part). Work top-down: P0 first.
 
diff --git a/Docs/UNITY_VERIFICATION_CHECKLIST.md b/Docs/UNITY_VERIFICATION_CHECKLIST.md
index 4921d4a88..1c97c6e64 100644
--- a/Docs/UNITY_VERIFICATION_CHECKLIST.md
+++ b/Docs/UNITY_VERIFICATION_CHECKLIST.md
@@ -3,9 +3,15 @@
 > **Superseded for new work — see `Docs/QA/`.** The untested-development backlog is now
 > generated and maintained by the `/qa-backlog` skill in `Docs/QA/QA_BACKLOG.md`, with a
 > submission/result loop (`Docs/QA/README.md`) that archives passes and turns failures
-> into dev tasks. The two entries below are kept until they are run; new unverified work
+> into dev tasks. The sections below are kept until they are run; new unverified work
 > does **not** get a section here — record it in the PR body's *Verification status*
 > section and the scan will pick it up.
+>
+> **Not yet absorbed.** Seven 🔴 sections remain below, five of them added upstream
+> *after* the backlog's last scan (`2e2d3aaf`): the Sparrow turret stance, the Dolphin
+> speed/boost retune, the Dolphin capsule crystal blast, the Sparrow stopped-stance roll,
+> and the Sparrow boost redesign. They are **not** in `QA_BACKLOG.md` yet — the next
+> `/qa-backlog` run folds them in. Until then this file is still load-bearing: read both.
 
 **Purpose.** Some changes land on shared branches (`bleeding-edge` and the
 per-feature branches) without ever being opened in the Unity Editor —
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
