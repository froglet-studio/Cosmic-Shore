#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Game
{
    [CustomEditor(typeof(ShipHUDController))]
    public class ShipHUDControllerEditor : Editor
    {
        // Props
        SerializedProperty _shipTypeProp;
        SerializedProperty _profileProp;

        SerializedProperty _refs;
        // Colors
        readonly Color headerColor  = new(0.10f, 0.10f, 0.35f);
        readonly Color sectionColor = new(0.12f, 0.12f, 0.25f);

        void OnEnable()
        {
            _shipTypeProp = serializedObject.FindProperty("shipType");
            _profileProp  = serializedObject.FindProperty("profile");
            _refs  = serializedObject.FindProperty("refs");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader("Ship HUD Controller", headerColor);

            EditorGUILayout.PropertyField(_shipTypeProp, new GUIContent("Ship Type"));
            EditorGUILayout.PropertyField(_refs,  new GUIContent("Ship HUD Reference"));
            EditorGUILayout.PropertyField(_profileProp,  new GUIContent("HUD Profile (ShipHUDProfileSO)"));

            // Profile preview (read-only) to avoid diving into the asset each time
            if (_profileProp.objectReferenceValue is ShipHUDProfileSO profile)
            {
                DrawSection("Profile Preview", sectionColor, () =>
                {
                    EditorGUILayout.LabelField("Profile Ship Type:", profile.shipType.ToString());
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var subs = profile.subscriptions ?? System.Array.Empty<HudSubscriptionSO>();
                        EditorGUILayout.LabelField("Subscriptions:", subs.Length.ToString());
                        for (int i = 0; i < subs.Length; i++)
                        {
                            var sub = subs[i];
                            using (new EditorGUI.DisabledScope(true))
                            {
                                EditorGUILayout.ObjectField($"[{i}]",
                                    sub, typeof(HudSubscriptionSO), false);
                            }
                        }
                    }
                });
            }

            // Runtime actions (Play Mode)
            DrawSection("Runtime (Play Mode)", sectionColor, () =>
            {
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    var ctrl = (ShipHUDController)target;

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Initialize HUD"))
                        ctrl.InitializeShipHUD((ShipClassType)_shipTypeProp.intValue);

                    if (GUILayout.Button("Dispose HUD"))
                        ctrl.DisposeHUD();
                    GUILayout.EndHorizontal();
                }
            });

            serializedObject.ApplyModifiedProperties();
        }

        static void DrawHeader(string title, Color bg)
        {
            var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, bg);
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(rect, title, style);
            GUILayout.Space(4);
        }

        static void DrawSection(string title, Color bg, System.Action drawContent)
        {
            var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, bg);
            GUI.Label(new Rect(rect.x + 6, rect.y + 2, rect.width, rect.height),
                title, EditorStyles.boldLabel);

            GUILayout.BeginVertical(GUI.skin.box);
            drawContent?.Invoke();
            GUILayout.EndVertical();
            GUILayout.Space(6);
        }
    }
}
#endif
