#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Game
{
    [CustomEditor(typeof(ShipHUDView), true)]
    public class ShipHUDViewInspector : Editor
    {
        private readonly Color _headerColor  = new(0.09f, 0.24f, 0.48f);
        private readonly Color _sectionBlue  = new(0.14f, 0.22f, 0.36f);
        private readonly Color _sectionGreen = new(0.14f, 0.32f, 0.21f);

        // Cached props
        SerializedProperty hudTypeProp;
        SerializedProperty effectsBehaviourProp;
        SerializedProperty silhouetteProp, trailProp;
        SerializedProperty psRootProp, xboxRootProp;
        SerializedProperty buttonIconMappingsProp;

        // Variant props
        SerializedProperty serpentBoostBtnProp, serpentWallBtnProp;
        SerializedProperty dolphinBoostBtnProp;
        SerializedProperty mantaBoostBtnProp, mantaBoost2BtnProp;
        SerializedProperty rhinoBoostImgProp;
        SerializedProperty squirrelBoostImgProp;
        SerializedProperty sparrowFullAutoBtnProp, sparrowOverheatBtnProp, sparrowSkyBurstBtnProp, sparrowExhaustBtnProp;

        void OnEnable()
        {
            hudTypeProp             = serializedObject.FindProperty("hudType");
            effectsBehaviourProp    = serializedObject.FindProperty("effectsBehaviour");
            silhouetteProp          = serializedObject.FindProperty("silhouetteContainer");
            trailProp               = serializedObject.FindProperty("trailContainer");
            psRootProp              = serializedObject.FindProperty("psIconRoot");
            xboxRootProp            = serializedObject.FindProperty("xboxIconRoot");
            buttonIconMappingsProp  = serializedObject.FindProperty("buttonIconMappings");

            serpentBoostBtnProp     = serializedObject.FindProperty("serpentBoostButton");
            serpentWallBtnProp      = serializedObject.FindProperty("serpentWallDisplayButton");

            dolphinBoostBtnProp     = serializedObject.FindProperty("dolphinBoostFeedback");

            mantaBoostBtnProp       = serializedObject.FindProperty("mantaBoostButton");
            mantaBoost2BtnProp      = serializedObject.FindProperty("mantaBoost2Button");

            rhinoBoostImgProp       = serializedObject.FindProperty("rhinoBoostFeedback");
            squirrelBoostImgProp    = serializedObject.FindProperty("squirrelBoostDisplay");

            sparrowFullAutoBtnProp  = serializedObject.FindProperty("sparrowFullAutoAction");
            sparrowOverheatBtnProp  = serializedObject.FindProperty("sparrowOverheatingBoostAction");
            sparrowSkyBurstBtnProp  = serializedObject.FindProperty("sparrowSkyBurstMissileAction");
            sparrowExhaustBtnProp   = serializedObject.FindProperty("sparrowExhaustBarrage");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var view = (ShipHUDView)target;

            var variantName  = view.ShipHUDType.ToString();
            var variantColor = view.ShipHUDType switch
            {
                ShipClassType.Serpent  => new Color(0.6f, 0.1f, 0.1f),
                ShipClassType.Dolphin  => new Color(0.1f, 0.5f, 0.8f),
                ShipClassType.Manta    => new Color(0.2f, 0.2f, 0.5f),
                ShipClassType.Rhino    => new Color(0.5f, 0.5f, 0.1f),
                ShipClassType.Squirrel => new Color(0.6f, 0.4f, 0.2f),
                ShipClassType.Sparrow  => new Color(0.8f, 0.8f, 0.8f),
                _ => _headerColor
            };

            EditorGUILayout.Space(3);
            DrawHeader(variantName + " HUD View", variantColor);
            EditorGUILayout.Space(8);

            // Common
            DrawSection("Common", _sectionBlue, () =>
            {
                EditorGUILayout.PropertyField(hudTypeProp, new GUIContent("HUD Type"));
                EditorGUILayout.PropertyField(effectsBehaviourProp, new GUIContent("Effects (IHUDEffects)"));
                EditorGUILayout.PropertyField(silhouetteProp, new GUIContent("Silhouette Container"));
                EditorGUILayout.PropertyField(trailProp, new GUIContent("Trail Container"));
            });

            // Controller Icons (common toggle for all ships)
            DrawSection("Controller Icons", _sectionGreen, () =>
            {
                EditorGUILayout.PropertyField(psRootProp,   new GUIContent("PS Icon Root"));
                EditorGUILayout.PropertyField(xboxRootProp, new GUIContent("XBOX Icon Root"));
                EditorGUILayout.PropertyField(buttonIconMappingsProp, new GUIContent("Button → Icon Mappings"), true);

                if (GUILayout.Button("Refresh Controller Icons (Play Mode)"))
                {
                    if (Application.isPlaying)
                        view.UpdateControllerIcons();
                }
            });

            // Variant fields
            DrawSection(variantName + " Variant", variantColor, () =>
            {
                switch (view.ShipHUDType)
                {
                    case ShipClassType.Serpent:
                        EditorGUILayout.PropertyField(serpentBoostBtnProp, new GUIContent("Boost Button"));
                        EditorGUILayout.PropertyField(serpentWallBtnProp,  new GUIContent("Wall Display Button"));
                        break;

                    case ShipClassType.Dolphin:
                        EditorGUILayout.PropertyField(dolphinBoostBtnProp, new GUIContent("Boost Feedback (Button)"));
                        break;

                    case ShipClassType.Manta:
                        EditorGUILayout.PropertyField(mantaBoostBtnProp,  new GUIContent("Boost Button"));
                        EditorGUILayout.PropertyField(mantaBoost2BtnProp, new GUIContent("Boost 2 Button"));
                        break;

                    case ShipClassType.Rhino:
                        EditorGUILayout.PropertyField(rhinoBoostImgProp, new GUIContent("Boost Feedback (Image)"));
                        break;

                    case ShipClassType.Squirrel:
                        EditorGUILayout.PropertyField(squirrelBoostImgProp, new GUIContent("Boost Display (Image)"));
                        break;

                    case ShipClassType.Sparrow:
                        EditorGUILayout.PropertyField(sparrowFullAutoBtnProp, new GUIContent("Full Auto Button"));
                        EditorGUILayout.PropertyField(sparrowOverheatBtnProp, new GUIContent("Overheating Boost Button"));
                        EditorGUILayout.PropertyField(sparrowSkyBurstBtnProp, new GUIContent("Sky Burst Missile Button"));
                        EditorGUILayout.PropertyField(sparrowExhaustBtnProp, new GUIContent("Exhaust Barrage Button"));
                        break;

                    // Other ship types currently have no special fields
                    default:
                        EditorGUILayout.HelpBox("No variant-specific UI for this ship.", MessageType.Info);
                        break;
                }
            });

            serializedObject.ApplyModifiedProperties();
        }

        // --- UI helpers ---

        void DrawHeader(string label, Color bgColor)
        {
            Rect r = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, bgColor);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                normal   = { textColor = Color.white },
                padding  = new RectOffset(10, 0, 0, 0)
            };
            r.x += 10; r.width -= 10;
            GUI.Label(r, label, headerStyle);
        }

        void DrawSection(string label, Color color, System.Action drawContent)
        {
            GUILayout.Space(2);
            Rect r = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, color);
            var sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal   = { textColor = Color.white },
                padding  = new RectOffset(10, 0, 0, 0)
            };
            GUI.Label(new Rect(r.x + 10, r.y + 2, r.width - 10, r.height - 2), label, sectionStyle);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            drawContent?.Invoke();
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }
    }
}
#endif
