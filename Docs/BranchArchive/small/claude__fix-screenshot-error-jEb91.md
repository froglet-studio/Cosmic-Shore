# Branch archive: `claude/fix-screenshot-error-jEb91`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-01 by Claude
- **Unmerged commits:** 1
- **Forked from:** `671d74502` (2026-03-01, Merge pull request #308 from froglet-studio/claude/fix-camera-offset-eqVQu)
- **Tip:** `12aacf950`
- **Files touched (1):**
  - `Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs`

### `12aacf950` — fix(screenshot): replace Camera.Render() with URP-compatible screen capture

_Claude, 2026-03-01 10:14:33 +0000_

```text
Camera.Render() is not reliably supported in URP and caused errors when
taking debug screenshots in shape drawing mode. Replace with a
Canvas-hiding approach: temporarily disable all Canvas components, wait
one frame for URP to render without UI, then ReadPixels from the screen
buffer. Also adds a Screen dimension guard and null-checks on canvas
restore.
```

```text
 .../Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs     | 47 +++++++++++++++------------------
 1 file changed, 22 insertions(+), 25 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs b/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs
index 82532d6bb..6cc120c9d 100644
--- a/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs
+++ b/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs
@@ -891,8 +891,8 @@ namespace CosmicShore.Gameplay
 
         /// <summary>
         /// Captures a screenshot excluding UI layers.
-        /// Temporarily disables the UI layer on the active camera, renders to a RenderTexture,
-        /// saves the result as PNG, then restores the camera's original culling mask.
+        /// Temporarily disables all Canvas components, waits one frame for URP to render
+        /// the scene without UI, reads pixels from the screen buffer, then restores canvases.
         /// </summary>
         public void TakeDebugScreenshot()
         {
@@ -901,34 +901,31 @@ namespace CosmicShore.Gameplay
 
         IEnumerator CaptureShapeScreenshot()
         {
-            // Wait for end of frame so current rendering is done
-            yield return new WaitForEndOfFrame();
-
-            var cam = Camera.main;
-            if (!cam) yield break;
-
-            // Save original culling mask and disable UI layer
-            int originalCullingMask = cam.cullingMask;
-            int uiLayer = LayerMask.NameToLayer("UI");
-            if (uiLayer >= 0)
-                cam.cullingMask &= ~(1 << uiLayer);
+            if (Screen.width == 0 || Screen.height == 0) yield break;
 
-            // Create temporary render texture matching screen resolution
-            var rt = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
-            cam.targetTexture = rt;
-            cam.Render();
-            cam.targetTexture = null;
+            // Temporarily hide all UI canvases so the screenshot captures only the 3D scene.
+            // This avoids Camera.Render() which is not reliably supported in URP.
+            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
+            var wasEnabled = new bool[canvases.Length];
+            for (int i = 0; i < canvases.Length; i++)
+            {
+                wasEnabled[i] = canvases[i].enabled;
+                canvases[i].enabled = false;
+            }
 
-            // Restore camera
-            cam.cullingMask = originalCullingMask;
+            // Wait one frame so URP renders the scene without UI, then capture after render completes
+            yield return null;
+            yield return new WaitForEndOfFrame();
 
-            // Read pixels from render texture
-            RenderTexture.active = rt;
             var screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
             screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
             screenshot.Apply();
-            RenderTexture.active = null;
-            RenderTexture.ReleaseTemporary(rt);
+
+            // Restore canvases immediately
+            for (int i = 0; i < canvases.Length; i++)
+            {
+                if (canvases[i]) canvases[i].enabled = wasEnabled[i];
+            }
 
             // Save to disk
             string folder = Path.Combine(Application.persistentDataPath, "Screenshots");
@@ -936,7 +933,7 @@ namespace CosmicShore.Gameplay
             string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
             string filePath = Path.Combine(folder, $"Shape_{timestamp}.png");
             File.WriteAllBytes(filePath, screenshot.EncodeToPNG());
-            Object.Destroy(screenshot);
+            Destroy(screenshot);
 
             Debug.Log($"[ShapeDrawing] Screenshot saved (UI excluded): {filePath}");
         }
```

</details>
