# Branch archive: `claude/restore-block-bandit-J6Pl1`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-02 by Garrett Milliron
- **Unmerged commits:** 2
- **Forked from:** `f719b6f0f` (2026-03-01, Merge pull request #317 from froglet-studio/claude/analog-drift-triggers-DnnmE)
- **Tip:** `a2b8fdeb7`
- **Files touched (5):**
  - `Assets/_Prefabs/Environment/MembraneBase.prefab`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameBlockBandit.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameBlockBandit.unity.meta`
  - `Assets/_Scripts/Models/Enums/InputDeviceType.cs.meta`
  - `ProjectSettings/EditorBuildSettings.asset`

### `c847e0b41` — Restore Block Bandit mini game scene

_Claude, 2026-03-01 20:58:36 +0000_

```text
Recover MinigameBlockBandit.unity from git history (deleted in dedbeff)
and place it in the Singleplayer Scenes directory to match the current
scene organization. Add scene to EditorBuildSettings so it's included
in builds. The SO config (ArcadeGameBlockBandit.asset) and game list
registration were already intact.
```

```text
 Assets/_Scenes/Singleplayer Scenes/MinigameBlockBandit.unity      | 1973 +++++++++++++++++++++++++++++++++++
 Assets/_Scenes/Singleplayer Scenes/MinigameBlockBandit.unity.meta |    7 +
 ProjectSettings/EditorBuildSettings.asset                         |    3 +
 3 files changed, 1983 insertions(+)
```

### `a2b8fdeb7` — pushing scene changes like changine to stealing and the extending helix

_Garrett Milliron, 2026-03-02 13:30:55 -0500_

```text
 Assets/_Prefabs/Environment/MembraneBase.prefab              |  2 +-
 Assets/_Scenes/Singleplayer Scenes/MinigameBlockBandit.unity | 56 +++++++++++++++---------------------------
 Assets/_Scripts/Models/Enums/InputDeviceType.cs.meta         |  2 ++
 3 files changed, 23 insertions(+), 37 deletions(-)
```
