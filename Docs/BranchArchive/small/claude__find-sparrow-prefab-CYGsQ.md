# Branch archive: `claude/find-sparrow-prefab-CYGsQ`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-06 by Claude
- **Unmerged commits:** 3
- **Forked from:** `0d48ab5b5` (2026-02-25, Merge pull request #78 from froglet-studio/claude/add-missing-sounds-1ZgoJ)
- **Tip:** `bc7b77508`
- **Files touched (4):**
  - `Assets/_Animations/SparrowAnimatorController.controller`
  - `Assets/_Models/Ship Models/SparrowModel3.fbx`
  - `Assets/_Models/Ship Models/SparrowModel3.fbx.meta`
  - `Assets/_Prefabs/Spaceships/Sparrow.prefab`

### `35b5e160e` — Replace Sparrow prefab model with SparrowModel3

_Claude, 2026-03-06 03:31:12 +0000_

```text
Swapped SparrowModel1 references (GUID 151e3f035...) with SparrowModel3 (GUID 1b0725c5c...) across all 102 references in the Sparrow prefab. Also brought in SparrowModel3.fbx and its meta from the sparrow-missile-mechanics branch.
```

```text
 Assets/_Models/Ship Models/SparrowModel3.fbx      | Bin 0 -> 6021116 bytes
 Assets/_Models/Ship Models/SparrowModel3.fbx.meta | 107 +++++++++++++++++++++++++++
 Assets/_Prefabs/Spaceships/Sparrow.prefab         | 206 ++++++++++++++++++++++++++--------------------------
 3 files changed, 210 insertions(+), 103 deletions(-)
```

### `38b83eee7` — Re-setup animation tree for SparrowModel3

_Claude, 2026-03-06 03:44:40 +0000_

```text
- Copied all 11 animation clip definitions (Idle, SpeedUp/Down, Yaw/Pitch/Roll, Speed+maneuvers) from SparrowModel1.fbx.meta into SparrowModel3.fbx.meta, preserving the same internalIDs so blend tree fileID references remain valid
- Enabled avatarSetup on SparrowModel3.fbx.meta to match SparrowModel1's rig configuration
- Updated SparrowAnimatorController.controller to reference SparrowModel3's GUID (1b0725c5c...) instead of SparrowModel1's (151e3f035...) for all 12 animation clip motion references
```

```text
 Assets/_Animations/SparrowAnimatorController.controller |  24 ++--
 Assets/_Models/Ship Models/SparrowModel3.fbx.meta       | 323 +++++++++++++++++++++++++++++++++++++++++++++-
 2 files changed, 333 insertions(+), 14 deletions(-)
```

### `bc7b77508` — Fix animation takeName paths in SparrowModel3.fbx.meta

_Claude, 2026-03-06 03:51:06 +0000_

```text
SparrowModel3.fbx was exported from Blender with a doubled armature prefix in
the take names (e.g. Sparrow_Armature+Mesh|Sparrow_Armature+Mesh|Idle_SparrowAnim)
unlike SparrowModel1 which had a single prefix. Updated all 11 takeName values
to match the actual FBX take names so Unity can find and extract the clips.
```

```text
 Assets/_Models/Ship Models/SparrowModel3.fbx.meta | 22 +++++++++++-----------
 1 file changed, 11 insertions(+), 11 deletions(-)
```
