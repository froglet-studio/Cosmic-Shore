#if false // UNITY_EDITOR
using CosmicShore.Game.UI;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MiniGameHUDView), true)]
public class MiniGameHUDViewInspector : Editor
{
    // Default header and section colors
    Color headerColor = new Color(0.09f, 0.24f, 0.48f);
    Color sectionBlue = new Color(0.14f, 0.22f, 0.36f);
    Color sectionGreen = new Color(0.14f, 0.32f, 0.21f);

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var view = (MiniGameHUDView)target;

        // Variant?specific header color
        string variantName = view.MiniGameHUDType.ToString();
        var variantColor = view.MiniGameHUDType switch
        {
            MiniGameType.Freestyle => new Color(0.2f, 0.6f, 0.2f),// greenish
            MiniGameType.CellularDuel => new Color(0.6f, 0.2f, 0.6f),// purple
            _ => headerColor,
        };
        EditorGUILayout.Space(3);
        DrawHeader($"{variantName} HUD View", variantColor);
        EditorGUILayout.Space(8);

        // Common Elements
        DrawSection("Common Elements", sectionBlue, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("miniGameType"), new GUIContent("HUD Type"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("scoreDisplay"), new GUIContent("Score Display"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("leftNumberDisplay"), new GUIContent("Left Number Display"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rightNumberDisplay"), new GUIContent("Right Number Display"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("roundTimeDisplay"), new GUIContent("Round Time Display"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("countdownDisplay"), new GUIContent("Countdown Image"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("readyButton"), new GUIContent("Ready Button"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("countdownTimer"), new GUIContent("Countdown Timer"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pip"), new GUIContent("Pip Container"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("silhouette"), new GUIContent("Silhouette Container"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("trailDisplay"), new GUIContent("Trail Display"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("buttonPanel"), new GUIContent("Button Panel"));
        });

        // Bottom Buttons
        DrawSection("Bottom Buttons", sectionGreen, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("button1"), new GUIContent("Button 1"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("button2"), new GUIContent("Button 2"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("button3"), new GUIContent("Button 3"));
        });

        // Button Events
        DrawSection("Button Events", variantColor, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnButton1Pressed"), new GUIContent("On Button 1 Pressed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnButton1Released"), new GUIContent("On Button 1 Released"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnButton2Pressed"), new GUIContent("On Button 2 Pressed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnButton2Released"), new GUIContent("On Button 2 Released"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnButton3Pressed"), new GUIContent("On Button 3 Pressed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnButton3Released"), new GUIContent("On Button 3 Released"));
        });

        serializedObject.ApplyModifiedProperties();
    }

    // Draw a colored header bar
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
        r.x += 10; r.width -= 10;
        GUI.Label(r, label, headerStyle);
    }

    // Draw a collapsible section with a colored title bar
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
#endif
