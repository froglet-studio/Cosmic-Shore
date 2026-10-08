# Branch archive: `claude/loving-clarke-9xl6he`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-17 by Claude
- **Unmerged commits:** 1
- **Forked from:** `a042a3c5a` (2026-06-18, Merge branch 'bleeding-edge' into claude/sweet-sagan-pgenlj)
- **Tip:** `5431b9028`
- **Files touched (2):**
  - `Assets/_Scripts/UI/MenuMiniGameHUD.cs`
  - `Assets/_Scripts/UI/MiniGameHUD.cs`

### `5431b9028` — fix(ui): only hoist the local player's vessel HUD into the game canvas

_Claude, 2026-06-17 22:05:27 +0000_

```text
onShipHUDInitialized is a global, untargeted SOAP event raised by every
vessel that spawns on a machine (via ShipHUD.Start). MiniGameHUD and
MenuMiniGameHUD both reparented whatever HUD they were handed into the
shared screen-space "Game UI" canvas with no local-player filter.

Solo has exactly one vessel, so this lined up. When a second player joins
a party in Menu_Main, the host spawns the remote vessel — whose ShipHUD
raises the same event — and the host reparents/cross-wires the remote
HUD on top of its own in the shared canvas, leaving the host's energy /
elemental bars dead (the multiplayer-only "energy bar not responsive"
regression). The remote HUD also leaked into the canvas on client leave.

Guard both reparenting handlers so each only processes the HUD belonging
to gameData.LocalPlayer.Vessel; a positively-identified remote/AI vessel
HUD is skipped (it stays under its vessel, off-canvas, as intended). The
fallback returns true when the local pair isn't resolved yet or the HUD
is already unparented, so a legitimate local HUD is never dropped.
```

```text
 Assets/_Scripts/UI/MenuMiniGameHUD.cs | 26 ++++++++++++++++++++++++++
 Assets/_Scripts/UI/MiniGameHUD.cs     | 23 +++++++++++++++++++++++
 2 files changed, 49 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/MenuMiniGameHUD.cs b/Assets/_Scripts/UI/MenuMiniGameHUD.cs
index 8cb5e4071..ebae780ba 100644
--- a/Assets/_Scripts/UI/MenuMiniGameHUD.cs
+++ b/Assets/_Scripts/UI/MenuMiniGameHUD.cs
@@ -163,6 +163,15 @@ namespace CosmicShore.UI
         {
             if (!data.ShipHUD) return;
 
+            // onShipHUDInitialized is a global, untargeted SOAP event: EVERY vessel that
+            // spawns on this machine raises it — including a remote/AI party member's vessel
+            // that the host spawns when someone joins. This screen-space "Game UI" canvas may
+            // only ever host the LOCAL player's vessel HUD. Without this guard the host would
+            // reparent (and Hide/cross-wire) a joining client's HUD on top of its own, which
+            // is what leaves the local energy/elemental bars dead after a second player joins
+            // — the multiplayer-only regression that never reproduces solo (one vessel only).
+            if (!IsLocalPlayerHUD(data.ShipHUD)) return;
+
             Hide();
 
             foreach (Transform child in data.ShipHUD.GetComponentsInChildren<Transform>(false))
@@ -181,6 +190,23 @@ namespace CosmicShore.UI
             }
         }
 
+        /// <summary>
+        /// True when the given ship HUD belongs to the local player's vessel (the only HUD
+        /// that should be hoisted into this screen-space canvas). Falls back to true when the
+        /// local pair hasn't resolved yet or the HUD is already unparented, so a legitimate
+        /// local HUD is never dropped — only a positively-identified remote vessel is skipped.
+        /// </summary>
+        bool IsLocalPlayerHUD(MiniGameHUD shipHud)
+        {
+            var localVessel = gameData?.LocalPlayer?.Vessel;
+            if (localVessel == null) return true;
+
+            var vessel = shipHud.GetComponentInParent<IVessel>(true);
+            if (vessel == null) return true;
+
+            return ReferenceEquals(vessel, localVessel);
+        }
+
         void InstantiatePauseMenu()
         {
             if (!pauseMenuPrefab) return;
diff --git a/Assets/_Scripts/UI/MiniGameHUD.cs b/Assets/_Scripts/UI/MiniGameHUD.cs
index be035b12e..729d0a892 100644
--- a/Assets/_Scripts/UI/MiniGameHUD.cs
+++ b/Assets/_Scripts/UI/MiniGameHUD.cs
@@ -567,6 +567,12 @@ namespace CosmicShore.UI
         {
             if (!data.ShipHUD) return;
 
+            // onShipHUDInitialized is a global, untargeted SOAP event raised by EVERY vessel
+            // (including remote/AI vessels in a multiplayer session). This HUD canvas may only
+            // hoist the LOCAL player's vessel HUD — reparenting a remote vessel's HUD on top of
+            // it cross-wires the shared canvas and kills the local HUD's live bars.
+            if (!IsLocalPlayerHUD(data.ShipHUD)) return;
+
             Hide();
 
             foreach (Transform child in data.ShipHUD.GetComponentsInChildren<Transform>(false))
@@ -579,6 +585,23 @@ namespace CosmicShore.UI
             data.ShipHUD.gameObject.SetActive(true);
         }
 
+        /// <summary>
+        /// True when the given ship HUD belongs to the local player's vessel — the only HUD that
+        /// should be hoisted into this canvas. Falls back to true when the local pair hasn't
+        /// resolved yet or the HUD is already unparented, so a legitimate local HUD is never
+        /// dropped; only a positively-identified remote/AI vessel HUD is skipped.
+        /// </summary>
+        private bool IsLocalPlayerHUD(MiniGameHUD shipHud)
+        {
+            var localVessel = gameData?.LocalPlayer?.Vessel;
+            if (localVessel == null) return true;
+
+            var vessel = shipHud.GetComponentInParent<IVessel>(true);
+            if (vessel == null) return true;
+
+            return ReferenceEquals(vessel, localVessel);
+        }
+
         protected virtual void UpdateScoreUI()
         {
             if (localRoundStats == null) return;
```

</details>
