# Branch archive: `claude/vehicle-selection-back-button-o2ukgr`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-07-24 by Claude
- **Unmerged commits:** 1
- **Forked from:** `bd6a8b405` (2026-07-23, last mix push)
- **Tip:** `68e043282`
- **Files touched (1):**
  - `Assets/_Prefabs/ArcadeGameConfigureModal.prefab`

### `68e043282` — fix(ui): make back button work on vessel/team selection screen

_Claude, 2026-07-24 16:38:40 +0000_

```text
The CloseButton on the arcade config modal's GameDetailView (vessel/team
selection, Screen 2) was wired to OnBackFromGameSelectView() — a deprecated
no-op stub — so clicking it did nothing. Players could only back out with the
gamepad B button, which the modal base handles directly via ModalWindowOut().
The Screen 1 (intensity/player count) CloseButton was correctly wired to a
real close method, hence its back button worked.

Rewire the Screen 2 CloseButton onClick to OnCloseModal(), the method
ArcadeGameConfigureModal documents as the canonical close ("Wire ALL
close/back buttons to this method instead of ModalWindowOut() directly"). It
closes the modal, resets stale game/config state, re-arms the commit guard,
and notifies party clients to close too — the correct behavior for this
post-commit screen.
```

```text
 Assets/_Prefabs/ArcadeGameConfigureModal.prefab | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```
