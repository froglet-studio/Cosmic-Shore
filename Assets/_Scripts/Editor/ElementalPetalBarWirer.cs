using UnityEditor;
using UnityEngine;
using CosmicShore.UI;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click setup for the shared elemental-bar widget on a vessel HUD. For the selected
    /// <see cref="ElementalBarsView"/> it: assigns the shared <see cref="ElementalBarsConfigSO"/>
    /// (the default at <c>Resources/ElementalBarsConfig</c> unless one is already set), ensures the
    /// four element bars exist, and creates a square <c>petalRoot</c> container per element.
    ///
    /// Petal sprites come from the config, so the per-bar sprite override is left empty. At play time
    /// the view fills each container with five rotated, tinted petals. <c>labelIcon</c> /
    /// <c>normalLabelSprite</c> are left untouched.
    ///
    /// Usage: open the vessel HUD prefab in Prefab Mode, select the GameObject carrying the view (or any
    /// parent), then run the menu item. Reposition the generated *_Flower containers to taste.
    /// </summary>
    public static class ElementalPetalBarWirer
    {
        const string ConfigPath = "Assets/Resources/ElementalBarsConfig.asset";

        // Standard element bars created when the view has none yet.
        static readonly string[] DefaultElements = { "Charge", "Mass", "Space", "Time" };

        [MenuItem("Tools/Cosmic Shore/Wire Elemental Petal Bars")]
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
            EditorUtility.DisplayDialog("Wire Elemental Petal Bars",
                wired > 0
                    ? $"Set up {wired} element flower(s) on '{view.name}'.\n\n" +
                      "Reposition the *_Flower containers to taste - five rotated petals are created " +
                      "inside each at play time, tinted from the shared ElementalBarsConfig."
                    : "Nothing wired. Confirm ElementalBarsConfig exists at " + ConfigPath + ".",
                "OK");
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
