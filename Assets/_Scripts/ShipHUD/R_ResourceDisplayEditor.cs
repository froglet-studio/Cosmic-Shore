#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Game
{
    [CustomEditor(typeof(R_ResourceDisplay))]
    public class R_ResourceDisplayEditor : Editor
    {
        // Core
        SerializedProperty indexProp;
        SerializedProperty modeProp;
        SerializedProperty verboseProp;

        // Sprite Fill
        SerializedProperty fillImageProp;
        SerializedProperty segmentsProp;
        SerializedProperty changeColorOnFullProp;
        SerializedProperty normalColorProp;
        SerializedProperty fullColorProp;

        // Sprite Swap / Sequence (sprite-based)
        SerializedProperty spriteTargetProp;
        SerializedProperty spritesProp;

        // Sequence (object-based)
        SerializedProperty stepObjectsProp;

        void OnEnable()
        {
            // Core
            indexProp            = serializedObject.FindProperty("index");
            modeProp             = serializedObject.FindProperty("mode");
            verboseProp          = serializedObject.FindProperty("verbose");

            // Fill
            fillImageProp        = serializedObject.FindProperty("fillImage");
            segmentsProp         = serializedObject.FindProperty("segments");
            changeColorOnFullProp= serializedObject.FindProperty("changeColorOnFull");
            normalColorProp      = serializedObject.FindProperty("normalColor");
            fullColorProp        = serializedObject.FindProperty("fullColor");

            // Swap / Sequence (sprite)
            spriteTargetProp     = serializedObject.FindProperty("spriteTarget");
            spritesProp          = serializedObject.FindProperty("sprites");

            // Sequence (objects)
            stepObjectsProp      = serializedObject.FindProperty("stepObjects");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Header
            DrawHeader("R_ResourceDisplay");

            EditorGUILayout.PropertyField(indexProp,  new GUIContent("Index"));
            EditorGUILayout.PropertyField(modeProp,   new GUIContent("Mode"));

            var mode = (R_ResourceDisplay.DisplayMode)modeProp.enumValueIndex;

            EditorGUILayout.Space(6);

            switch (mode)
            {
                case R_ResourceDisplay.DisplayMode.SpriteFill:
                    DrawSection("Sprite Fill", new Color(0.14f, 0.22f, 0.36f), () =>
                    {
                        EditorGUILayout.PropertyField(fillImageProp, new GUIContent("Fill Image"));
                        EditorGUILayout.PropertyField(segmentsProp,  new GUIContent("Segments"), true);
                        EditorGUILayout.PropertyField(changeColorOnFullProp, new GUIContent("Change Color On Full"));
                        if (changeColorOnFullProp.boolValue)
                        {
                            EditorGUILayout.PropertyField(normalColorProp, new GUIContent("Normal Color"));
                            EditorGUILayout.PropertyField(fullColorProp,   new GUIContent("Full Color"));
                        }

                        // Validation
                        if (fillImageProp.objectReferenceValue == null && segmentsProp.arraySize == 0)
                        {
                            EditorGUILayout.HelpBox(
                                "Provide a Fill Image and/or Segments to visualize the Fill mode.",
                                MessageType.Warning);
                        }
                    });
                    break;

                case R_ResourceDisplay.DisplayMode.SpriteSwap:
                    DrawSection("Sprite Swap", new Color(0.16f, 0.30f, 0.22f), () =>
                    {
                        EditorGUILayout.PropertyField(spriteTargetProp, new GUIContent("Sprite Target"));
                        EditorGUILayout.PropertyField(spritesProp,      new GUIContent("Sprites"), true);

                        // Validation
                        if (spriteTargetProp.objectReferenceValue == null || spritesProp.arraySize == 0)
                        {
                            EditorGUILayout.HelpBox(
                                "Assign a Sprite Target and at least one Sprite for Sprite Swap.",
                                MessageType.Warning);
                        }
                    });
                    break;

                case R_ResourceDisplay.DisplayMode.SpriteSequence:
                    DrawSection("Sprite Sequence", new Color(0.26f, 0.22f, 0.14f), () =>
                    {
                        EditorGUILayout.PropertyField(stepObjectsProp,  new GUIContent("Animated Sprite Objects"), true);
                        EditorGUILayout.Space(4);
                        // EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(spriteTargetProp, new GUIContent("Sprite Target"));
                        EditorGUILayout.PropertyField(spritesProp,      new GUIContent("Sprites"), true);
                        // EditorGUI.indentLevel--;

                        // Validation
                        bool hasSteps    = stepObjectsProp.arraySize > 0;
                        bool hasSprites  = spritesProp.arraySize > 0 && spriteTargetProp.objectReferenceValue != null;

                        if (!hasSteps && !hasSprites)
                        {
                            EditorGUILayout.HelpBox(
                                "Provide Step Objects (preferred) OR a Sprite Target + Sprites as a fallback for Sequence.",
                                MessageType.Warning);
                        }
                    });
                    break;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(verboseProp, new GUIContent("Verbose Logging"));

            serializedObject.ApplyModifiedProperties();
        }

        // ------------ UI helpers ------------
        static void DrawHeader(string title)
        {
            var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.10f, 0.10f, 0.35f));
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