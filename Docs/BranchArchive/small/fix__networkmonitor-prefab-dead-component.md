# Branch archive: `fix/networkmonitor-prefab-dead-component`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-13 by jtgjones
- **Unmerged commits:** 1
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/713
- **Forked from:** `d32f26839` (2026-08-12, Merge pull request #710 from froglet-studio/claude/charge-crystal-shader-9u2ik)
- **Tip:** `6507f167a`
- **Files touched (1):**
  - `Assets/_Prefabs/CORE/NetworkMonitor.prefab`

### `6507f167a` — Remove dead NetworkMonitor MonoBehaviour from NetworkMonitor.prefab

_jtgjones, 2026-08-13 13:32:37 -0500_

```text
NetworkMonitor was refactored into a plain C# class (CosmicShore.Core,
non-MonoBehaviour), but the prefab still serialized a component slot
pointing at its script GUID. Unity logs "'CosmicShore.Core.NetworkMonitor'
is missing the class attribute 'ExtensionOfNativeClass'!" twice on every
project launch because of it.

No code calls GetComponent<NetworkMonitor>() (it is not a Component), and
nothing else references the component fileID, so this is purely serialized
dead weight.
```

```text
 Assets/_Prefabs/CORE/NetworkMonitor.prefab | 14 --------------
 1 file changed, 14 deletions(-)
```
