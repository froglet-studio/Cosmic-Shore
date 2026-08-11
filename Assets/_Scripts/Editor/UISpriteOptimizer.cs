using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-stop UI sprite performance tooling for Cosmic Shore. Implements the
    /// fixes from <c>Docs/UI_SPRITE_AUDIT.md</c>:
    ///
    ///   1. Configure Sprite Atlasing          — enables Sprite Atlas V2 packing
    ///                                            (project-wide) and authors one
    ///                                            atlas per UI screen/context so
    ///                                            the in-game HUD and menus batch
    ///                                            into ~1-2 draw calls instead of
    ///                                            30+. (audit item #1)
    ///   2. Fix UI Sprite Import Settings       — mipmaps OFF, alphaIsTransparency,
    ///                                            crunch, sensible max size, and
    ///                                            ASTC mobile overrides across the
    ///                                            UI sprite folders. (audit item #4)
    ///   3. Disable Raycast On Selection        — strips raycastTarget from
    ///                                            non-interactive decorative
    ///                                            Graphics on the SELECTED prefabs
    ///                                            / scene objects. (audit item #3)
    ///
    /// All operations are reversible via git. The atlas/import operations require
    /// the Unity asset pipeline, which is why they live here rather than as raw
    /// .meta edits.
    ///
    /// Menu root: Tools > Cosmic Shore > UI Sprites
    /// </summary>
    public static class UISpriteOptimizer
    {
        // Root that holds all generated atlases (V2 extension).
        const string AtlasFolder = "Assets/_Graphics/_Atlases";

        // Folder substrings to NEVER touch for import settings or atlasing:
        //  - FX/Fx Sprites: used on particle systems / VFX, may want mipmaps.
        //  - App Icons: platform launcher icons, not runtime UI.
        //  - References: design-comp mockups, not runtime sprites.
        //  - Video/Skyboxes/RenderTextures: not UI sprites.
        static readonly string[] ExcludeSubstrings =
        {
            "/FX/", "/Fx Sprites/", "/App Icons/", "/References/",
            "/Video/", "/Skyboxes/", "/RenderTextures/", "/_Atlases/",
        };

        // ----------------------------------------------------------------------------
        // Atlas group definitions. Each atlas pulls whole folders so that sprites
        // shown together batch together. "Design Assets" is referenced post-rename;
        // missing folders are skipped gracefully so the tool also runs pre-rename
        // (it tries the "Design Assests" typo fallback too).
        // ----------------------------------------------------------------------------
        static readonly (string atlasName, string[] folders)[] AtlasGroups =
        {
            ("UI_HUD", new[]
            {
                "Assets/_Graphics/Design Assets/HUD UI",
                "Assets/_Graphics/Design Assets/Controls Panel",
                "Assets/_Graphics/Design Assets/End Scene",
                "Assets/_Graphics/ElementIcons",
                "Assets/_Graphics/ElementShapes",
                "Assets/_Graphics/Silhouettes",
            }),
            ("UI_Menu", new[]
            {
                "Assets/_Graphics/Nav Bar",
                "Assets/_Graphics/Buttons",
                "Assets/_Graphics/Design Assets/Menu_Main",
            }),
            ("UI_Arcade", new[]
            {
                "Assets/_Graphics/ARCADE",
                "Assets/_Graphics/CardImages",
            }),
            ("UI_Port", new[]
            {
                "Assets/_Graphics/Port",
            }),
            ("UI_Hangar", new[]
            {
                "Assets/_Graphics/Hangar",
                "Assets/_Graphics/VesselButtons",
            }),
            ("UI_Profile", new[]
            {
                "Assets/_Graphics/Profile",
                "Assets/_Graphics/Pilots",
            }),
            ("UI_Misc", new[]
            {
                "Assets/_Graphics/Store",
                "Assets/_Graphics/Settings",
            }),
        };

        // ============================================================================
        // 1. ATLASING
        // ============================================================================
        [MenuItem("Tools/Cosmic Shore/UI Sprites/1. Configure Sprite Atlasing")]
        public static void ConfigureSpriteAtlasing()
        {
            // Enable Sprite Atlas V2 packing project-wide so atlases actually pack
            // in builds (and in play mode). This is a shared EditorSettings change.
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
            {
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
                Debug.Log("[UISpriteOptimizer] Set EditorSettings.spritePackerMode = SpriteAtlasV2.");
            }

            if (!AssetDatabase.IsValidFolder(AtlasFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Graphics", "_Atlases");
            }

            var report = new StringBuilder();
            int created = 0, updated = 0;

            foreach (var (atlasName, folders) in AtlasGroups)
            {
                // Resolve folders, accounting for the "Design Assests" typo fallback.
                var folderObjs = new List<Object>();
                var includedPaths = new List<string>();
                foreach (string f in folders)
                {
                    string resolved = ResolveFolder(f);
                    if (resolved == null) continue;
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(resolved);
                    if (obj == null) continue;
                    folderObjs.Add(obj);
                    includedPaths.Add(resolved);
                }

                if (folderObjs.Count == 0)
                {
                    report.AppendLine($"  {atlasName}: no folders found — skipped.");
                    continue;
                }

                string atlasPath = $"{AtlasFolder}/{atlasName}.spriteatlasv2";
                bool exists = System.IO.File.Exists(atlasPath);

                // Always start from a fresh asset so the packed-object set is fully
                // replaced (Add() appends, so reusing the loaded asset would duplicate
                // folder refs on every re-run). Saving over an existing path preserves
                // the .meta — and therefore the importer settings configured below.
                var asset = new SpriteAtlasAsset();
                asset.SetIncludeInBuild(true);
                asset.SetIsVariant(false);
                asset.Add(folderObjs.ToArray());
                SpriteAtlasAsset.Save(asset, atlasPath);
                AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);

                // Configure packing + texture + platform settings via the V2 importer.
                if (AssetImporter.GetAtPath(atlasPath) is SpriteAtlasImporter importer)
                {
                    importer.packingSettings = new SpriteAtlasPackingSettings
                    {
                        enableRotation = false,    // rotation breaks UI sliced/9-grid sprites
                        enableTightPacking = false, // rect packing is safe for UI
                        padding = 4,
                    };
                    importer.textureSettings = new SpriteAtlasTextureSettings
                    {
                        generateMipMaps = false,   // UI never samples mips
                        sRGB = true,
                        filterMode = FilterMode.Bilinear,
                    };
                    importer.SetPlatformSettings(new TextureImporterPlatformSettings
                    {
                        name = "Android", overridden = true, maxTextureSize = 2048,
                        format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50,
                    });
                    importer.SetPlatformSettings(new TextureImporterPlatformSettings
                    {
                        name = "iPhone", overridden = true, maxTextureSize = 2048,
                        format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50,
                    });
                    importer.SaveAndReimport();
                }

                if (exists) updated++; else created++;
                report.AppendLine($"  {atlasName}: {(exists ? "updated" : "created")} ({folderObjs.Count} folder(s))\n    - {string.Join("\n    - ", includedPaths)}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UISpriteOptimizer] Atlasing configured. Created {created}, updated {updated} atlas(es) in {AtlasFolder}.\n{report}");
        }

        // ============================================================================
        // 2. IMPORT SETTINGS
        // ============================================================================
        [MenuItem("Tools/Cosmic Shore/UI Sprites/2. Fix UI Sprite Import Settings")]
        public static void FixUISpriteImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Graphics" });
            int fixedCount = 0, skipped = 0;
            var changedFolders = new SortedDictionary<string, int>();

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (ExcludeSubstrings.Any(path.Contains)) { skipped++; continue; }
                    if (AssetImporter.GetAtPath(path) is not TextureImporter ti) { skipped++; continue; }
                    // Only touch real UI sprites — leave Default/NormalMap textures alone.
                    if (ti.textureType != TextureImporterType.Sprite) { skipped++; continue; }

                    EditorUtility.DisplayProgressBar("Fixing UI sprite imports",
                        System.IO.Path.GetFileName(path), (float)i / guids.Length);

                    bool dirty = false;

                    if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
                    if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
                    if (ti.isReadable) { ti.isReadable = false; dirty = true; }

                    // Right-size: never upscale; cap at next-pow2 of the source, max 2048.
                    ti.GetSourceTextureWidthAndHeight(out int w, out int h);
                    int cap = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(w, h)), 32, 2048);

                    var def = ti.GetDefaultPlatformTextureSettings();
                    int newMax = Mathf.Min(def.maxTextureSize, cap);
                    if (def.maxTextureSize != newMax) { def.maxTextureSize = newMax; dirty = true; }
                    if (!def.crunchedCompression) { def.crunchedCompression = true; def.compressionQuality = 50; dirty = true; }
                    ti.SetPlatformTextureSettings(def);

                    // Explicit ASTC overrides for mobile (the demo target).
                    foreach (string plat in new[] { "Android", "iPhone" })
                    {
                        var ps = ti.GetPlatformTextureSettings(plat);
                        if (!ps.overridden || ps.format != TextureImporterFormat.ASTC_6x6)
                        {
                            ps.overridden = true;
                            ps.maxTextureSize = Mathf.Min(2048, cap);
                            ps.format = TextureImporterFormat.ASTC_6x6;
                            ps.compressionQuality = 50;
                            ti.SetPlatformTextureSettings(ps);
                            dirty = true;
                        }
                    }

                    if (dirty)
                    {
                        ti.SaveAndReimport();
                        fixedCount++;
                        string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                        changedFolders[folder] = changedFolders.GetValueOrDefault(folder) + 1;
                    }
                    else skipped++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            var report = new StringBuilder();
            foreach (var kv in changedFolders) report.AppendLine($"  {kv.Value,4}  {kv.Key}");
            Debug.Log($"[UISpriteOptimizer] Fixed import settings on {fixedCount} UI sprite(s), skipped {skipped}.\nChanged per folder:\n{report}");
        }

        // ============================================================================
        // 3. RAYCAST CLEANUP (selection-scoped — human in the loop)
        // ============================================================================
        [MenuItem("Tools/Cosmic Shore/UI Sprites/3. Disable Raycast On Selection (decorative)")]
        public static void DisableRaycastOnSelection()
        {
            Object[] selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("UI Sprite Optimizer",
                    "Select one or more UI prefabs (in the Project window) or GameObjects " +
                    "(in the Hierarchy) first.\n\nThe tool only disables raycastTarget on " +
                    "Graphics that are clearly non-interactive (no Selectable / EventTrigger / " +
                    "not a Selectable target graphic), so buttons keep working.", "OK");
                return;
            }

            int totalDisabled = 0;
            var report = new StringBuilder();

            foreach (Object obj in selected)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                bool isPrefabAsset = !string.IsNullOrEmpty(path) && path.EndsWith(".prefab");

                if (isPrefabAsset)
                {
                    using var scope = new PrefabUtility.EditPrefabContentsScope(path);
                    int n = StripDecorativeRaycasts(scope.prefabContentsRoot);
                    if (n > 0) report.AppendLine($"  {n,3}  {path}");
                    totalDisabled += n;
                }
                else if (obj is GameObject go)
                {
                    int n = StripDecorativeRaycasts(go);
                    if (n > 0)
                    {
                        EditorUtility.SetDirty(go);
                        report.AppendLine($"  {n,3}  {go.name} (scene/instance)");
                    }
                    totalDisabled += n;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UISpriteOptimizer] Disabled raycastTarget on {totalDisabled} decorative Graphic(s).\n{report}");
        }

        /// <summary>
        /// Sets raycastTarget=false on every Graphic under <paramref name="root"/>
        /// that is not interactive and is not the target graphic of a Selectable.
        /// Returns the number changed.
        /// </summary>
        static int StripDecorativeRaycasts(GameObject root)
        {
            var selectables = root.GetComponentsInChildren<Selectable>(true);
            var protectedGraphics = new HashSet<Graphic>();
            foreach (var s in selectables)
                if (s.targetGraphic != null) protectedGraphics.Add(s.targetGraphic);

            int changed = 0;
            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (!g.raycastTarget) continue;
                if (protectedGraphics.Contains(g)) continue;
                // Interactive if the GameObject hosts any input-handling component.
                if (g.GetComponent<Selectable>() != null) continue;
                if (g.GetComponent<EventTrigger>() != null) continue;
                if (g.GetComponents<MonoBehaviour>().Any(m => m is IEventSystemHandler)) continue;

                g.raycastTarget = false;
                changed++;
            }
            return changed;
        }

        // ============================================================================
        // Helpers
        // ============================================================================
        /// <summary>Resolves a folder path, falling back to the "Design Assests" typo.</summary>
        static string ResolveFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return path;
            string typo = path.Replace("Design Assets", "Design Assests");
            return AssetDatabase.IsValidFolder(typo) ? typo : null;
        }
    }
}
