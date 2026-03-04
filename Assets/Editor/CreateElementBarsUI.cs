using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor utility: creates the ElementalBarsView hierarchy inside the selected GameObject.
    /// Run via menu: Cosmic Shore / Create Element Bars UI.
    /// After running, wire the SilhouetteController.elementBars reference to the new object.
    /// Delete this script once you're happy with the layout.
    /// </summary>
    public static class CreateElementBarsUI
    {
        [MenuItem("Cosmic Shore/Create Element Bars UI")]
        static void Create()
        {
            // Load the pips config SO to grab sprites
            var config = AssetDatabase.LoadAssetAtPath<ElementPipsConfigSO>(
                "Assets/_SO_Assets/HUD/ElementPipsConfig.asset");

            if (!config)
            {
                Debug.LogError("Could not load ElementPipsConfig.asset at Assets/_SO_Assets/HUD/");
                return;
            }

            // Use selection as parent, or create standalone
            Transform parent = Selection.activeTransform;
            if (parent == null)
            {
                Debug.LogError("Select a GameObject in the hierarchy first (e.g. the SquirrelHUD root).");
                return;
            }

            // Layout params (matching old auto-populate defaults from the SO)
            float columnSpacing = config.columnSpacing;   // 24
            Vector2 pipSize = config.pipSize;             // 14x5
            Vector2 labelIconSize = config.labelIconSize; // 20x20
            float labelGap = config.labelGap;             // 4
            float zeroLineHeight = config.zeroLineHeight; // 1
            Color zeroLineColor = config.zeroLineColor;
            int minLevel = -5;
            int maxLevel = 15;

            Element[] elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };
            int cols = elements.Length;

            // Container
            var containerGO = new GameObject("ElementBarsContainer", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(containerGO, "Create Element Bars UI");
            var containerRT = (RectTransform)containerGO.transform;
            containerRT.SetParent(parent, false);
            containerRT.anchorMin = containerRT.anchorMax = new Vector2(0.5f, 0.5f);
            containerRT.pivot = new Vector2(0.5f, 0f);
            containerRT.anchoredPosition = Vector2.zero;
            containerRT.sizeDelta = Vector2.zero;

            // Add ElementalBarsView component
            var barsView = containerGO.AddComponent<ElementalBarsView>();

            float totalWidth = (cols - 1) * columnSpacing;
            float startX = -totalWidth * 0.5f;
            float zeroFraction = (float)(-minLevel) / (maxLevel - minLevel); // 0.25

            // We'll build the bars array via SerializedObject so private [SerializeField] is accessible
            var so = new SerializedObject(barsView);
            var barsProp = so.FindProperty("bars");
            barsProp.arraySize = cols;

            for (int c = 0; c < cols; c++)
            {
                var element = elements[c];
                float xPos = startX + c * columnSpacing;

                // Column parent
                var colGO = new GameObject($"ElementBar_{element}", typeof(RectTransform));
                var colRT = (RectTransform)colGO.transform;
                colRT.SetParent(containerRT, false);
                colRT.anchorMin = colRT.anchorMax = new Vector2(0.5f, 0f);
                colRT.pivot = new Vector2(0.5f, 0f);
                colRT.anchoredPosition = new Vector2(xPos, 0f);
                colRT.sizeDelta = new Vector2(pipSize.x, 0f);

                // Label icon
                var labelGO = new GameObject($"Label_{element}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var labelRT = (RectTransform)labelGO.transform;
                labelRT.SetParent(colRT, false);
                labelRT.anchorMin = labelRT.anchorMax = new Vector2(0.5f, 0f);
                labelRT.pivot = new Vector2(0.5f, 0f);
                labelRT.anchoredPosition = Vector2.zero;
                labelRT.sizeDelta = labelIconSize;

                var labelImg = labelGO.GetComponent<Image>();
                labelImg.sprite = config.GetLabelSprite(element);
                labelImg.color = Color.white;
                labelImg.preserveAspect = true;
                labelImg.raycastTarget = false;

                float barBaseY = labelIconSize.y + labelGap;

                // Bar background
                var bgGO = new GameObject($"BarBG_{element}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var bgRT = (RectTransform)bgGO.transform;
                bgRT.SetParent(colRT, false);
                bgRT.anchorMin = bgRT.anchorMax = new Vector2(0.5f, 0f);
                bgRT.pivot = new Vector2(0.5f, 0f);
                bgRT.anchoredPosition = new Vector2(0f, barBaseY);
                bgRT.sizeDelta = pipSize;
                var bgImg = bgGO.GetComponent<Image>();
                bgImg.color = new Color(1f, 1f, 1f, 0.08f);
                bgImg.raycastTarget = false;

                // Fill image
                var fillGO = new GameObject($"Fill_{element}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var fillRT = (RectTransform)fillGO.transform;
                fillRT.SetParent(colRT, false);
                fillRT.anchorMin = fillRT.anchorMax = new Vector2(0.5f, 0f);
                fillRT.pivot = new Vector2(0.5f, 0f);
                fillRT.anchoredPosition = new Vector2(0f, barBaseY);
                fillRT.sizeDelta = pipSize;

                var fillImg = fillGO.GetComponent<Image>();
                fillImg.sprite = config.GetPipSprite(element);
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Vertical;
                fillImg.fillOrigin = (int)Image.OriginVertical.Bottom;
                fillImg.fillAmount = 0.5f; // preview at 50%
                fillImg.color = Color.white;
                fillImg.raycastTarget = false;

                // Zero-line marker
                var zeroGO = new GameObject($"ZeroLine_{element}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var zeroRT = (RectTransform)zeroGO.transform;
                zeroRT.SetParent(colRT, false);
                zeroRT.anchorMin = zeroRT.anchorMax = new Vector2(0.5f, 0f);
                zeroRT.pivot = new Vector2(0.5f, 0.5f);
                float zeroY = barBaseY + zeroFraction * pipSize.y;
                zeroRT.anchoredPosition = new Vector2(0f, zeroY);
                zeroRT.sizeDelta = new Vector2(pipSize.x + 6f, zeroLineHeight);
                var zeroImg = zeroGO.GetComponent<Image>();
                zeroImg.color = zeroLineColor;
                zeroImg.raycastTarget = false;

                // Wire into the bars array
                var entry = barsProp.GetArrayElementAtIndex(c);
                entry.FindPropertyRelative("element").enumValueIndex = GetElementEnumIndex(element);
                entry.FindPropertyRelative("fillImage").objectReferenceValue = fillImg;
                entry.FindPropertyRelative("labelIcon").objectReferenceValue = labelImg;
                entry.FindPropertyRelative("normalLabelSprite").objectReferenceValue = labelImg.sprite;
            }

            so.ApplyModifiedProperties();

            Selection.activeGameObject = containerGO;
            Debug.Log("Element Bars UI created! Now wire SilhouetteController.elementBars → ElementBarsContainer. " +
                       "Resize the RectTransforms to taste.");
        }

        static int GetElementEnumIndex(Element element)
        {
            // Element enum values: None=0, Charge=1, Mass=2, Space=3, Time=4, ...
            // SerializedProperty enumValueIndex is the index in the enum declaration order
            var names = System.Enum.GetNames(typeof(Element));
            string target = element.ToString();
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == target) return i;
            }
            return 0;
        }
    }
}
