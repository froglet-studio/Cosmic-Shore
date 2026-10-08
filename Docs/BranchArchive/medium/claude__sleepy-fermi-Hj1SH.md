# Branch archive: `claude/sleepy-fermi-Hj1SH`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-06-03 by Shombith03
- **Unmerged commits:** 4
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/534
- **Forked from:** `d0eebf568` (2026-06-02, docs(benchmark): refresh tool guide + architecture gist for current toolset)
- **Tip:** `d03609522`
- **Files touched (1430):**
  - `"Assets/_Graphics/Profile/\342\200\224Pngtree\342\200\224empty comic chat bubble retro_6866362.png.meta"`
  - `Assets/_Graphics/ARCADE/+.png.meta`
  - `Assets/_Graphics/ARCADE/-.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 1 Selected-1.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 1-1.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 1.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 2 Selected-1.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 2-1.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 2.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 3 Selected-1.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 3-1.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 3.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 4 Selected-1.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 4-1.png.meta`
  - `Assets/_Graphics/ARCADE/Alternate Intensity Icons/Intensity 4.png.meta`
  - `Assets/_Graphics/ARCADE/Button (8).png.meta`
  - `Assets/_Graphics/ARCADE/Button (9).png.meta`
  - `Assets/_Graphics/ARCADE/DownArrow.png.meta`
  - `Assets/_Graphics/ARCADE/EXPLORE.png.meta`
  - `Assets/_Graphics/ARCADE/EXPLORE_SELECTED.png.meta`
  - `Assets/_Graphics/ARCADE/Favorite_Star_Active.png.meta`
  - `Assets/_Graphics/ARCADE/Favorite_Star_Inactive.png.meta`
  - `Assets/_Graphics/ARCADE/Four Players Selected.png.meta`
  - `Assets/_Graphics/ARCADE/Frame 2080.png.meta`
  - `Assets/_Graphics/ARCADE/Game_Option_Border_Active.png.meta`
  - `Assets/_Graphics/ARCADE/Game_Option_Border_Inactive.png.meta`
  - `Assets/_Graphics/ARCADE/Group 2095 (2).png.meta`
  - `Assets/_Graphics/ARCADE/Group 853.png.meta`
  - `Assets/_Graphics/ARCADE/Intensity 1 Selected.png.meta`
  - `Assets/_Graphics/ARCADE/Intensity 1.png.meta`
  - `Assets/_Graphics/ARCADE/Intensity 2 Selected.png.meta`
  - `Assets/_Graphics/ARCADE/Intensity 2.png.meta`
  - `Assets/_Graphics/ARCADE/Intensity 3 Selected.png.meta`
  - `Assets/_Graphics/ARCADE/Intensity 3.png.meta`
  - `Assets/_Graphics/ARCADE/Intensity 4 Selected.png.meta`
  - `Assets/_Graphics/ARCADE/Intensity 4.png.meta`
  - `Assets/_Graphics/ARCADE/LOADING SCREEN_.png.meta`
  - `Assets/_Graphics/ARCADE/LOADOUT.png.meta`
  - `Assets/_Graphics/ARCADE/LOADOUT_SELECTED.png.meta`
  - `Assets/_Graphics/ARCADE/LockIcon.png.meta`
  - … and 1390 more

### `d68c1d118` — perf(ui): add UI sprite optimizer tool + sprite/render audit

_Claude, 2026-06-01 18:07:28 +0000_

```text
The UI is FPS-expensive primarily because sprite atlasing is fully off
(EditorSettings.spritePackerMode = 0, zero atlas assets), so ~990 standalone
UI sprite textures fragment UGUI batches into 30+ draw calls. The in-game HUD
also mixes per-frame-dynamic and static content on one canvas and flags
decorative graphics as raycast targets.

Add Tools > Cosmic Shore > UI Sprites:
- Configure Sprite Atlasing: enable Sprite Atlas V2 + author one atlas per
  UI screen (HUD, Menu, Arcade, Port, Hangar, Profile, Misc). Idempotent.
- Fix UI Sprite Import Settings: mipmaps off, alphaIsTransparency, crunch,
  right-size (no upscale), ASTC mobile overrides across _Graphics UI sprites
  (excludes FX/App Icons/References).
- Disable Raycast On Selection: strip raycastTarget from non-interactive
  decorative graphics on selected prefabs (keeps Selectable targets/buttons).

Docs/UI_SPRITE_AUDIT.md captures measurements, worst offenders, atlas groups,
how to run the tooling, and manual steps for the canvas split + fuel fill.
```

```text
 Assets/_Scripts/Editor/UISpriteOptimizer.cs      | 361 +++++++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Editor/UISpriteOptimizer.cs.meta |  11 ++
 Docs/UI_SPRITE_AUDIT.md                          | 213 +++++++++++++++++++++++++++++++
 3 files changed, 585 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 586 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/UISpriteOptimizer.cs b/Assets/_Scripts/Editor/UISpriteOptimizer.cs
new file mode 100644
index 000000000..65423dd66
--- /dev/null
+++ b/Assets/_Scripts/Editor/UISpriteOptimizer.cs
@@ -0,0 +1,361 @@
+using System.Collections.Generic;
+using System.Linq;
+using System.Text;
+using UnityEditor;
+using UnityEditor.U2D;
+using UnityEngine;
+using UnityEngine.EventSystems;
+using UnityEngine.U2D;
+using UnityEngine.UI;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// One-stop UI sprite performance tooling for Cosmic Shore. Implements the
+    /// fixes from <c>Docs/UI_SPRITE_AUDIT.md</c>:
+    ///
+    ///   1. Configure Sprite Atlasing          — enables Sprite Atlas V2 packing
+    ///                                            (project-wide) and authors one
+    ///                                            atlas per UI screen/context so
+    ///                                            the in-game HUD and menus batch
+    ///                                            into ~1-2 draw calls instead of
+    ///                                            30+. (audit item #1)
+    ///   2. Fix UI Sprite Import Settings       — mipmaps OFF, alphaIsTransparency,
+    ///                                            crunch, sensible max size, and
+    ///                                            ASTC mobile overrides across the
+    ///                                            UI sprite folders. (audit item #4)
+    ///   3. Disable Raycast On Selection        — strips raycastTarget from
+    ///                                            non-interactive decorative
+    ///                                            Graphics on the SELECTED prefabs
+    ///                                            / scene objects. (audit item #3)
+    ///
+    /// All operations are reversible via git. The atlas/import operations require
+    /// the Unity asset pipeline, which is why they live here rather than as raw
+    /// .meta edits.
+    ///
+    /// Menu root: Tools > Cosmic Shore > UI Sprites
+    /// </summary>
+    public static class UISpriteOptimizer
+    {
+        // Root that holds all generated atlases (V2 extension).
+        const string AtlasFolder = "Assets/_Graphics/_Atlases";
+
+        // Folder substrings to NEVER touch for import settings or atlasing:
+        //  - FX/Fx Sprites: used on particle systems / VFX, may want mipmaps.
+        //  - App Icons: platform launcher icons, not runtime UI.
+        //  - References: design-comp mockups, not runtime sprites.
+        //  - Video/Skyboxes/RenderTextures: not UI sprites.
+        static readonly string[] ExcludeSubstrings =
+        {
+            "/FX/", "/Fx Sprites/", "/App Icons/", "/References/",
+            "/Video/", "/Skyboxes/", "/RenderTextures/", "/_Atlases/",
+        };
+
+        // ----------------------------------------------------------------------------
+        // Atlas group definitions. Each atlas pulls whole folders so that sprites
+        // shown together batch together. "Design Assets" is referenced post-rename;
+        // missing folders are skipped gracefully so the tool also runs pre-rename
+        // (it tries the "Design Assests" typo fallback too).
+        // ----------------------------------------------------------------------------
+        static readonly (string atlasName, string[] folders)[] AtlasGroups =
+        {
+            ("UI_HUD", new[]
+            {
+                "Assets/_Graphics/Design Assets/HUD UI",
+                "Assets/_Graphics/Design Assets/Controls Panel",
+                "Assets/_Graphics/Design Assets/End Scene",
+                "Assets/_Graphics/ElementIcons",
+                "Assets/_Graphics/ElementShapes",
+                "Assets/_Graphics/Silhouettes",
+            }),
+            ("UI_Menu", new[]
+            {
+                "Assets/_Graphics/Nav Bar",
+                "Assets/_Graphics/Buttons",
+                "Assets/_Graphics/Design Assets/Menu_Main",
+            }),
+            ("UI_Arcade", new[]
+            {
+                "Assets/_Graphics/ARCADE",
+                "Assets/_Graphics/CardImages",
+            }),
+            ("UI_Port", new[]
+            {
+                "Assets/_Graphics/Port",
+            }),
+            ("UI_Hangar", new[]
+            {
+                "Assets/_Graphics/Hangar",
+                "Assets/_Graphics/VesselButtons",
+            }),
+            ("UI_Profile", new[]
+            {
+                "Assets/_Graphics/Profile",
+                "Assets/_Graphics/Pilots",
+            }),
+            ("UI_Misc", new[]
+            {
+                "Assets/_Graphics/Store",
+                "Assets/_Graphics/Settings",
+            }),
+        };
+
+        // ============================================================================
+        // 1. ATLASING
+        // ============================================================================
+        [MenuItem("Tools/Cosmic Shore/UI Sprites/1. Configure Sprite Atlasing")]
+        public static void ConfigureSpriteAtlasing()
+        {
+            // Enable Sprite Atlas V2 packing project-wide so atlases actually pack
+            // in builds (and in play mode). This is a shared EditorSettings change.
+            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
+            {
+                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
+                Debug.Log("[UISpriteOptimizer] Set EditorSettings.spritePackerMode = SpriteAtlasV2.");
+            }
+
+            if (!AssetDatabase.IsValidFolder(AtlasFolder))
+            {
+                AssetDatabase.CreateFolder("Assets/_Graphics", "_Atlases");
+            }
+
+            var report = new StringBuilder();
+            int created = 0, updated = 0;
+
+            foreach (var (atlasName, folders) in AtlasGroups)
+            {
+                // Resolve folders, accounting for the "Design Assests" typo fallback.
+                var folderObjs = new List<Object>();
+                var includedPaths = new List<string>();
+                foreach (string f in folders)
+                {
+                    string resolved = ResolveFolder(f);
+                    if (resolved == null) continue;
+                    var obj = AssetDatabase.LoadAssetAtPath<Object>(resolved);
+                    if (obj == null) continue;
+                    folderObjs.Add(obj);
+                    includedPaths.Add(resolved);
+                }
+
+                if (folderObjs.Count == 0)
+                {
+                    report.AppendLine($"  {atlasName}: no folders found — skipped.");
+                    continue;
+                }
```

</details>

### `d27e7b126` — refactor(graphics): fix typo'd UI sprite folder names

_Claude, 2026-06-01 18:07:28 +0000_

```text
Rename "Design Assests" -> "Design Assets" and "Hangar/Hanger_New" ->
"Hangar/Hangar_New". Reference-safe: no C# code references these folders by
path string, and folder/asset GUIDs are preserved (the .meta files move with
their assets), so all prefab/scene references stay intact.

Kept as a separate commit because the rename touches 344 files and can cause
merge conflicts for in-flight work under the old folder name — merge when
convenient.
```

```text
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 1085.png.meta                    |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 1086.png                         | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 1086.png.meta                    |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 1108.png                         | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 1108.png.meta                    |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 1109 (1).png                     | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 1109 (1).png.meta                |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 1109.png                         | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 1109.png.meta                    |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 1110 (4).png                     | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 1110 (4).png.meta                |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 7.png                            | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Frame 7.png.meta                       |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Group 2048.png                         | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Group 2048.png.meta                    |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Group 2049.png                         | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Group 2049.png.meta                    |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Group 2071.png                         | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Group 2071.png.meta                    |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Group 2097.png                         | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Group 2097.png.meta                    |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Lock_icon.png                          | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Lock_icon.png.meta                     |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Rectangle 1283 (1).png                 | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Rectangle 1283 (1).png.meta            |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Rectangle 995.png                      | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/Rectangle 995.png.meta                 |   0
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/popup (1).png                          | Bin
 Assets/_Graphics/Hangar/{Hanger_New => Hangar_New}/popup (1).png.meta                     |   0
 772 files changed, 0 insertions(+), 0 deletions(-)
```

### `d03609522` — Add sprite atlas + add meta files

_Shombith03, 2026-06-03 05:17:27 +0530_

```text
 .../{PLACEHOLDERS}/Icons/GyroIconSelected-PLACEHOLDER.png.meta        | 43 +++++++++++++++----------
 Assets/_Graphics/{PLACEHOLDERS}/Icons/HuntIcon-PLACEHOLDER.png.meta   | 43 +++++++++++++++----------
 .../{PLACEHOLDERS}/Icons/HuntIconSelected-PLACEHOLDER.png.meta        | 43 +++++++++++++++----------
 Assets/_Graphics/{PLACEHOLDERS}/Icons/Mass-Icon-PLACEHOLDER.png.meta  | 45 +++++++++++++++-----------
 .../{PLACEHOLDERS}/Icons/Mass-Icon-Selected-PLACEHOLDER.png.meta      | 45 +++++++++++++++-----------
 Assets/_Graphics/{PLACEHOLDERS}/Icons/PlusIcon-PLACEHOLDER.png.meta   | 45 +++++++++++++++-----------
 Assets/_Graphics/{PLACEHOLDERS}/Icons/ShockIcon-PLACEHOLDER.png.meta  | 43 +++++++++++++++----------
 .../{PLACEHOLDERS}/Icons/ShockIconSelected-PLACEHOLDER.png.meta       | 43 +++++++++++++++----------
 Assets/_Graphics/{PLACEHOLDERS}/Icons/SmashIcon-PLACEHOLDER.png.meta  | 43 +++++++++++++++----------
 .../{PLACEHOLDERS}/Icons/SmashIconSelected-PLACEHOLDER.png.meta       | 43 +++++++++++++++----------
 Assets/_Graphics/{PLACEHOLDERS}/Icons/Space-Icon-PLACEHOLDER.png.meta | 45 +++++++++++++++-----------
 .../{PLACEHOLDERS}/Icons/Space-Icon-Selected-PLACEHOLDER.png.meta     | 45 +++++++++++++++-----------
 Assets/_Graphics/{PLACEHOLDERS}/Icons/StealIcon-PLACEHOLDER.png.meta  | 43 +++++++++++++++----------
 .../{PLACEHOLDERS}/Icons/StealIconSelected-PLACEHOLDER.png.meta       | 43 +++++++++++++++----------
 Assets/_Graphics/{PLACEHOLDERS}/Icons/Time-Icon-PLACEHOLDER.png.meta  | 43 +++++++++++++++----------
 .../{PLACEHOLDERS}/Icons/Time-Icon-Selected-PLACEHOLDER.png.meta      | 43 +++++++++++++++----------
 Assets/_Graphics/{PLACEHOLDERS}/Icons/ZapIcon-PLACEHOLDER.png.meta    | 43 +++++++++++++++----------
 .../{PLACEHOLDERS}/Icons/ZapIconSelected-PLACEHOLDER.png.meta         | 43 +++++++++++++++----------
 Assets/_Graphics/{PLACEHOLDERS}/NotchedBorderPlaceholder.png.meta     | 45 +++++++++++++++-----------
 Assets/_Graphics/{PLACEHOLDERS}/Rectangle 820.png.meta                | 23 ++++++++++---
 Assets/_Graphics/{PLACEHOLDERS}/Rectangle 840.png.meta                | 23 ++++++++++---
 Assets/_Graphics/{PLACEHOLDERS}/Serpent_Square_PLACEHOLDER.png.meta   | 45 +++++++++++++++-----------
 .../{PLACEHOLDERS}/Serpent_Square_Silhouette_PLACEHOLDER.png.meta     | 45 +++++++++++++++-----------
 Assets/_Graphics/{PLACEHOLDERS}/Sparrow_Square_PLACEHOLDER.png.meta   | 45 +++++++++++++++-----------
 .../{PLACEHOLDERS}/Sparrow_Square_Silhouette_PLACEHOLDER.png.meta     | 45 +++++++++++++++-----------
 Assets/_Graphics/{PLACEHOLDERS}/Squirrel_Square_PLACEHOLDER.png.meta  | 45 +++++++++++++++-----------
 .../{PLACEHOLDERS}/Squirrel_Square_Silhouette_PLACEHOLDER.png.meta    | 45 +++++++++++++++-----------
 Assets/_Graphics/{PLACEHOLDERS}/aRealSquirrel.png.meta                | 52 +++++++++++++++++++++---------
 Assets/_Graphics/{PLACEHOLDERS}/aSquirrel.png.meta                    | 52 +++++++++++++++++++++---------
 993 files changed, 25740 insertions(+), 15283 deletions(-)
```

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
