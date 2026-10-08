# Branch archive: `claude/hopeful-pascal-5131v1`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-16 by Claude
- **Unmerged commits:** 2
- **Forked from:** `04a8ae3c4` (2026-06-16, Add Unity AI Package)
- **Tip:** `520705e8e`
- **Files touched (2):**
  - `Assets/_Graphics/ElementShapes.meta`
  - `ProjectSettings/ProjectVersion.txt`

### `0163cbfd8` — build: retarget project to Unity 6.3 LTS (6000.3.17f1)

_Claude, 2026-06-16 20:21:11 +0000_

```text
Switch the editor stamp from the non-LTS 6.4 to 6.3 LTS per the team's
LTS-only policy. 6.3 LTS (supported to Dec 2027) is the first LTS since
6.0 LTS; 6.4 is a non-LTS innovation release.

Opening this branch in 6.3 forward-migrates the 6.0-era SRP/graphics
packages (URP/VFX/ShaderGraph 17.0.x -> 17.3.x) to the editor's bundled
versions automatically and rewrites manifest/packages-lock on first open.
The earlier clean-recompile fixes (GameCard CS0592 removed, Friends
package 1.1.1 retained) already cover the C# errors 6.4 surfaced, which
6.3 shares.
```

```text
 ProjectSettings/ProjectVersion.txt | 4 ++--
 1 file changed, 2 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/ProjectSettings/ProjectVersion.txt b/ProjectSettings/ProjectVersion.txt
index 29501ff03..33390a1c1 100644
--- a/ProjectSettings/ProjectVersion.txt
+++ b/ProjectSettings/ProjectVersion.txt
@@ -1,2 +1,2 @@
-m_EditorVersion: 6000.4.11f1
-m_EditorVersionWithRevision: 6000.4.11f1 (b0a1d6caadd2)
+m_EditorVersion: 6000.3.17f1
+m_EditorVersionWithRevision: 6000.3.17f1 (cf0352b38e81)
```

</details>

### `520705e8e` — fix(meta): resolve leftover merge conflict markers in ElementShapes folder meta

_Claude, 2026-06-16 20:56:46 +0000_

```text
Assets/_Graphics/ElementShapes.meta carried unresolved <<<<<<<< / >>>>>>>>
conflict markers (committed back in the claude/cool-bell-m15cu3 merge),
corrupting the folder's .meta YAML so Unity can't parse it. Kept the
canonical guid a45ffe36fe4a8bb42895d544557b5c97 (this path's own guid on
origin/development); the other belonged to the since-deleted
_Scripts/Controller/XP folder. Neither guid is referenced elsewhere, so
the resolution is non-breaking. Pre-existing issue, unrelated to the 6.3
downgrade.
```

```text
 Assets/_Graphics/ElementShapes.meta | 4 ----
 1 file changed, 4 deletions(-)
```
