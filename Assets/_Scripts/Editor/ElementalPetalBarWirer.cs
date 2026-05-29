using UnityEditor;
using UnityEngine;
using CosmicShore.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Wires an <see cref="ElementalBarsView"/> for the single-petal flower design: for each element
    /// bar it ensures a square <c>petalRoot</c> container exists and assigns the element's crisp white
    /// petal sprite from <c>Assets/_Graphics/ElementPetals</c>. The view stacks five rotated copies of
    /// that sprite at play time.
    ///
    /// Open the prefab that carries the view (the Squirrel vessel HUD) in Prefab Mode, select its root,
    /// then run the menu item. <c>labelIcon</c> / <c>normalLabelSprite</c> are left untouched (Unity
    /// preserves them across the script change), so only the new petal fields are populated.
    /// </summary>
    public static class ElementalPetalBarWirer
    {
        const string PetalDir = "Assets/_Graphics/ElementPetals";

        // Element enum name -> petal-sprite filename prefix.
        static readonly (string elementName, string prefix)[] ElementOrder =
        {
            ("Charge", "charge"),
            ("Mass",   "mass"),
            ("Space",  "space"),
            ("Time",   "time"),
        };

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
                    ? $"Wired {wired} element flower(s).\n\nReposition the *_Flower containers under " +
                      $"'{view.name}' to taste — five rotated petals are created inside each at play time."
                    : "Nothing wired. Confirm the petal sprites exist under " + PetalDir + ".",
                "OK");
        }

        /// <summary>Populates petalRoot + petalSprite for every element bar. Returns the count wired.</summary>
        public static int Wire(ElementalBarsView view)
        {
            var so       = new SerializedObject(view);
            var barsProp = so.FindProperty("bars");

            // Fresh component? Create the four standard element bars.
            if (barsProp.arraySize == 0)
            {
                barsProp.arraySize = ElementOrder.Length;
                for (int i = 0; i < ElementOrder.Length; i++)
                {
                    var elemProp = barsProp.GetArrayElementAtIndex(i).FindPropertyRelative("element");
                    elemProp.enumValueIndex = EnumIndexForName(elemProp, ElementOrder[i].elementName);
                }
            }

            int wired = 0;
            for (int i = 0; i < barsProp.arraySize; i++)
            {
                var entry    = barsProp.GetArrayElementAtIndex(i);
                var elemProp = entry.FindPropertyRelative("element");
                string elementName = elemProp.enumNames[elemProp.enumValueIndex];
                string prefix = PrefixForElement(elementName);
                if (prefix == null) continue; // None / Omni — no flower

                var rootProp = entry.FindPropertyRelative("petalRoot");
                if (!rootProp.objectReferenceValue)
                    rootProp.objectReferenceValue = CreateFlowerContainer(view.transform, elementName, wired);

                string path = $"{PetalDir}/{prefix}_petal.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (!sprite)
                {
                    Debug.LogError($"[ElementalPetalBarWirer] Missing sprite: {path}", view);
                    continue;
                }
                entry.FindPropertyRelative("petalSprite").objectReferenceValue = sprite;
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
            rt.sizeDelta        = new Vector2(80f, 80f);   // square so rotated petals stay undistorted
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((index - 1.5f) * 96f, 0f); // default row; artist repositions
            return rt;
        }

        static string PrefixForElement(string elementName)
        {
            foreach (var (name, prefix) in ElementOrder)
                if (name == elementName) return prefix;
            return null;
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
