using UnityEditor;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.ScriptableObjects;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click setup for the shared elemental-bar widget on a vessel HUD. For the selected
    /// <see cref="ElementalBarsView"/> it: assigns the shared <see cref="ElementalBarsConfigSO"/>
    /// (the default at <c>Resources/ElementalBarsConfig</c> unless one is already set), ensures the
    /// four element bars exist, creates a square <c>petalRoot</c> container per element, and BAKES
    /// the five rotated petals into each container - so the whole widget is authored prefab content
    /// and nothing is spawned at runtime (the view reuses authored petals via
    /// <see cref="ElementalBarsView.ConfigurePetal"/> and warns when it has to create one).
    ///
    /// Petal sprites come from the config, so the per-bar sprite override is left empty.
    /// <c>labelIcon</c> / <c>normalLabelSprite</c> are left untouched.
    ///
    /// Two entry points:
    ///   • <b>Wire Elemental Petal Bars</b> - the selected view (open the HUD prefab in Prefab Mode).
    ///   • <b>Bake Elemental Petal Bars Into All Vessel HUDs</b> - walks every prefab carrying an
    ///     ElementalBarsView and bakes it in place. Run after adding a vessel or changing petal sprites.
    /// </summary>
    public static class ElementalPetalBarWirer
    {
        const string ConfigPath = "Assets/Resources/ElementalBarsConfig.asset";

        // Standard element bars created when the view has none yet.
        static readonly string[] DefaultElements = { "Charge", "Mass", "Space", "Time" };

        [MenuItem("FrogletTools/Vessels/Wire Elemental Petal Bars")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 3,
            Description = "Author the 5-petal element flowers onto a vessel HUD.")]
        static void WireSelected()
        {
            var go = Selection.activeGameObject;
            var view = go ? go.GetComponentInChildren<ElementalBarsView>(true) : null;
            if (!view)
            {
                EditorUtility.DisplayDialog("Wire Elemental Petal Bars",
                    "Select a GameObject with an ElementalBarsView in its hierarchy.\n\n" +
                    "Tip: open the vessel HUD prefab in Prefab Mode and select its root.", "OK");
                return;
            }

            int wired = Wire(view);
            int baked = wired > 0 ? BakePetals(view) : 0;
            EditorUtility.DisplayDialog("Wire Elemental Petal Bars",
                wired > 0
                    ? $"Set up {wired} element flower(s) on '{view.name}' and baked {baked} petal(s) " +
                      "into the prefab.\n\nReposition the *_Flower containers to taste - the petals " +
                      "are authored children now, tinted from the shared ElementalBarsConfig."
                    : "Nothing wired. Confirm ElementalBarsConfig exists at " + ConfigPath + ".",
                "OK");
        }

        [MenuItem("FrogletTools/Vessels/Bake Elemental Petal Bars Into All Vessel HUDs")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 3,
            Description = "Bulk-apply the petal flower wiring across the fleet.")]
        static void BakeAllPrefabs()
        {
            int prefabsBaked = 0, petalsBaked = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab || !prefab.GetComponentInChildren<ElementalBarsView>(true)) continue;

                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int bakedHere = 0;
                    foreach (var view in contents.GetComponentsInChildren<ElementalBarsView>(true))
                        if (Wire(view) > 0)
                            bakedHere += BakePetals(view);

                    if (bakedHere > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        prefabsBaked++;
                        petalsBaked += bakedHere;
                        Debug.Log($"[ElementalPetalBarWirer] Baked {bakedHere} petal(s) into {path}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            EditorUtility.DisplayDialog("Bake Elemental Petal Bars",
                prefabsBaked > 0
                    ? $"Baked {petalsBaked} petal(s) across {prefabsBaked} prefab(s). Already-authored " +
                      "petals were left in place."
                    : "Every prefab with an ElementalBarsView already has authored petals - nothing to do.",
                "OK");
        }

        /// <summary>
        /// Authors the five rotated petals inside each bar's petalRoot (reusing any already-authored
        /// "Petal{n}" children) with the config's sprite + neutral tick colour, exactly matching what
        /// <see cref="ElementalBarsView.ConfigurePetal"/> produces at runtime. Returns petals created.
        /// </summary>
        public static int BakePetals(ElementalBarsView view)
        {
            var so = new SerializedObject(view);
            var config = so.FindProperty("config").objectReferenceValue as ElementalBarsConfigSO;
            if (!config) return 0;

            int created = 0;
            var barsProp = so.FindProperty("bars");
            for (int i = 0; i < barsProp.arraySize; i++)
            {
                var entry = barsProp.GetArrayElementAtIndex(i);
                var root = entry.FindPropertyRelative("petalRoot").objectReferenceValue as RectTransform;
                if (!root) continue;

                var elemProp = entry.FindPropertyRelative("element");
                var element = (Element)elemProp.intValue;
                var sprite = entry.FindPropertyRelative("petalSpriteOverride").objectReferenceValue as Sprite;
                if (!sprite) sprite = config.GetPetalSprite(element);
                if (!sprite)
                {
                    Debug.LogWarning($"[ElementalPetalBarWirer] No petal sprite for '{element}' in the " +
                                     "config - skipping that flower.", view);
                    continue;
                }

                for (int p = 0; p < ElementalBarsConfigSO.PetalCount; p++)
                {
                    var existing = root.Find($"Petal{p}");
                    var img = existing ? existing.GetComponent<UnityEngine.UI.Image>() : null;
                    if (!img)
                    {
                        var go = new GameObject($"Petal{p}", typeof(RectTransform),
                            typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                        Undo.RegisterCreatedObjectUndo(go, "Bake Element Petal");
                        go.layer = root.gameObject.layer;
                        go.transform.SetParent(root, false);
                        img = go.GetComponent<UnityEngine.UI.Image>();
                        created++;
                    }
                    ElementalBarsView.ConfigurePetal(img, sprite, p);
                    img.color = config.ColorForTick(0);   // authored rest state: neutral grey
                    EditorUtility.SetDirty(img);
                }
            }

            EditorUtility.SetDirty(view);
            return created;
        }

        /// <summary>Assigns the config and creates a petalRoot per element bar. Returns the count wired.</summary>
        public static int Wire(ElementalBarsView view)
        {
            var so = new SerializedObject(view);

            // Assign the shared config if one isn't already set.
            var configProp = so.FindProperty("config");
            if (!configProp.objectReferenceValue)
            {
                var cfg = AssetDatabase.LoadAssetAtPath<ElementalBarsConfigSO>(ConfigPath);
                if (!cfg)
                {
                    Debug.LogError($"[ElementalPetalBarWirer] No ElementalBarsConfigSO at {ConfigPath}. " +
                                   "Create one via Assets > Create > ScriptableObjects > UI > Elemental Bars Config.", view);
                    return 0;
                }
                configProp.objectReferenceValue = cfg;
            }

            var barsProp = so.FindProperty("bars");
            if (barsProp.arraySize == 0)
            {
                barsProp.arraySize = DefaultElements.Length;
                for (int i = 0; i < DefaultElements.Length; i++)
                {
                    var elemProp = barsProp.GetArrayElementAtIndex(i).FindPropertyRelative("element");
                    elemProp.enumValueIndex = EnumIndexForName(elemProp, DefaultElements[i]);
                }
            }

            int wired = 0;
            for (int i = 0; i < barsProp.arraySize; i++)
            {
                var entry = barsProp.GetArrayElementAtIndex(i);
                var rootProp = entry.FindPropertyRelative("petalRoot");
                if (!rootProp.objectReferenceValue)
                {
                    var elemProp = entry.FindPropertyRelative("element");
                    string elementName = elemProp.enumNames[elemProp.enumValueIndex];
                    rootProp.objectReferenceValue = CreateFlowerContainer(view.transform, elementName, wired);
                }
                wired++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);
            return wired;
        }

        static RectTransform CreateFlowerContainer(Transform parent, string elementName, int index)
        {
            var go = new GameObject($"{elementName}_Flower", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create Element Flower");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta        = new Vector2(88f, 88f);   // square so rotated petals stay undistorted
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((index - 1.5f) * 104f, 0f); // default row; artist repositions
            return rt;
        }

        static int EnumIndexForName(SerializedProperty enumProp, string name)
        {
            var names = enumProp.enumNames;
            for (int i = 0; i < names.Length; i++)
                if (names[i] == name) return i;
            return 0;
        }
    }
}
