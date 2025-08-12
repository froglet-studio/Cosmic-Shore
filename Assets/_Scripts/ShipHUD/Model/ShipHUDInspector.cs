#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Game
{
    [CustomEditor(typeof(ShipHUDView), true)]
    public class ShipHUDViewInspector : Editor
    {
        private readonly Color _headerColor = new Color(0.09f, 0.24f, 0.48f);

        // Base section colors
        private readonly Color _sectionBlue = new Color(0.14f, 0.22f, 0.36f);
        private readonly Color _sectionGreen = new Color(0.14f, 0.32f, 0.21f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var view = (ShipHUDView)target;

            // Compute variant-specific header and section color
            var variantName = view.ShipHUDType.ToString();
            var variantColor = view.ShipHUDType switch
            {
                ShipClassType.Serpent => new Color(0.6f, 0.1f, 0.1f) // e.g. red
                ,
                ShipClassType.Dolphin => new Color(0.1f, 0.5f, 0.8f) // e.g. cyan
                ,
                ShipClassType.Manta => new Color(0.2f, 0.2f, 0.5f) // e.g. indigo
                ,
                ShipClassType.Rhino => new Color(0.5f, 0.5f, 0.1f) // e.g. yellow
                ,
                ShipClassType.Squirrel => new Color(0.6f, 0.4f, 0.2f) // e.g. brown
                ,
                ShipClassType.Sparrow => new Color(0.8f, 0.8f, 0.8f) // e.g. light gray
                ,
                _ => _headerColor
            };

            EditorGUILayout.Space(3);
            // Show the variant as header
            DrawHeader(variantName + " HUD View", variantColor);
            EditorGUILayout.Space(8);

            // Common section
            DrawSection("Common Variables", _sectionBlue, () =>
            {
                EditorGUILayout.PropertyField(
                     serializedObject.FindProperty("silhouetteContainer"),
                     new GUIContent("Silhouette Container")
                );
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("trailContainer"),
                    new GUIContent("Trail Container")
                );
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("psIconRoot"),
                    new GUIContent("PS Icon Root")
                );
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("xboxIconRoot"),
                    new GUIContent("XBOX Icon Root")
                );
                
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("hudType"),
                    new GUIContent("HUD Effect Type")
                );
            });

            // Enum section
            DrawSection("HUD Type", _sectionGreen, () =>
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("hudType"),
                    new GUIContent("HUD Type")
                );
            });

            DrawSection("Resource Display Information", _sectionGreen, () =>
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("resourceDisplays"),
                    new GUIContent("Resource Displays")
                );
            });

            // Variant section with dynamic color and label
            DrawSection(variantName + " Buttons", variantColor, () =>
            {
                switch (view.ShipHUDType)
                {
                    case ShipClassType.Serpent:
                        EditorGUILayout.PropertyField(
                            serializedObject.FindProperty("serpentBoostButton"),
                            new GUIContent("Boost Button")
                        );
                        EditorGUILayout.PropertyField(
                            serializedObject.FindProperty("serpentWallDisplayButton"),
                            new GUIContent("Wall Display Button")
                        );
            
                        break;
                    case ShipClassType.Dolphin:
                        EditorGUILayout.PropertyField(
                            serializedObject.FindProperty("dolphinBoostFeedback"),
                            new GUIContent("Boost Feedback")
                        );
                        break;
                    case ShipClassType.Manta:
                        EditorGUILayout.PropertyField(
                            serializedObject.FindProperty("mantaBoostButton"),
                            new GUIContent("Boost Button")
                        );

                        break;
                    case ShipClassType.Rhino:
                        EditorGUILayout.PropertyField(
                            serializedObject.FindProperty("rhinoBoostFeedback"),
                            new GUIContent("Boost Feedback")
                        );
                        break;
                    case ShipClassType.Squirrel:
                        EditorGUILayout.PropertyField(
                            serializedObject.FindProperty("squirrelBoostDisplay"),
                            new GUIContent("Boost Display")
                        );
                        break;
                    case ShipClassType.Sparrow:
                        EditorGUILayout.PropertyField(
                            serializedObject.FindProperty("sparrowFullAutoAction"),
                            new GUIContent("Full Auto Action Button")
                        );
                        EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("sparrowOverheatingBoostAction"),
                        new GUIContent("Overheating Boost Button")
                        );
                        EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("sparrowSkyBurstMissileAction"),
                        new GUIContent("Sky Burst Missile Button")
                        );
                        EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("sparrowExhaustBarrage"),
                        new GUIContent("Exhause Barrage Button")
                        );
                        break;
                    case ShipClassType.Any:
                    case ShipClassType.Random:
                    case ShipClassType.Urchin:
                    case ShipClassType.Grizzly:
                    case ShipClassType.Termite:
                    case ShipClassType.Falcon:
                    case ShipClassType.Shrike:
                        break;
                    default:
                        EditorGUILayout.HelpBox(
                            "Select a valid HUD Type to assign variant buttons.",
                            MessageType.Info
                        );
                        break;
                }
            });

            serializedObject.ApplyModifiedProperties();
        }

        // Draw header with custom color
        void DrawHeader(string label, Color bgColor)
        {
            Rect r = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, bgColor);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
                padding = new RectOffset(10, 0, 0, 0)
            };
            r.x += 10;
            r.width -= 10;
            GUI.Label(r, label, headerStyle);
        }

        // Generic section drawer
        void DrawSection(string label, Color color, System.Action drawContent)
        {
            GUILayout.Space(2);
            Rect r = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, color);
            var sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = Color.white },
                padding = new RectOffset(10, 0, 0, 0)
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
