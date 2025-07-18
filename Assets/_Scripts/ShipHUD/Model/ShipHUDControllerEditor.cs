#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CosmicShore.Game;

[CustomEditor(typeof(ShipHUDController))]
public class ShipHUDControllerEditor : Editor
{
    // Props
    SerializedProperty shipTypeProp;
    SerializedProperty boostProp, seedProp;
    SerializedProperty overheatProp, fullAutoProp, fireGunProp, stationaryProp;

    // Colors (tweak as you like)
    Color headerColor   = new Color(0.1f, 0.1f, 0.35f);
    Color sectionColor  = new Color(0.12f, 0.12f, 0.25f);

    void OnEnable()
    {
        shipTypeProp       = serializedObject.FindProperty("shipType");
        boostProp          = serializedObject.FindProperty("_boostAction");
        seedProp           = serializedObject.FindProperty("_seedAssemblerAction");
        overheatProp       = serializedObject.FindProperty("_overheatingAction");
        fullAutoProp       = serializedObject.FindProperty("_fullAutoAction");
        fireGunProp        = serializedObject.FindProperty("_fireGunAction");
        stationaryProp     = serializedObject.FindProperty("_stationaryModeAction");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // --- Header ---
        DrawHeader("Ship HUD Controller", headerColor);

        // Ship type dropdown
        EditorGUILayout.PropertyField(shipTypeProp, new GUIContent("Ship Type"));

        // Only show the relevant action fields
        ShipTypes type = (ShipTypes)shipTypeProp.intValue;
        GUILayout.Space(6);
        DrawSection(type + " Actions", sectionColor, () =>
        {
            switch (type)
            {
                case ShipTypes.Serpent:
                    EditorGUILayout.PropertyField(boostProp, new GUIContent("Boost Action"));
                    EditorGUILayout.PropertyField(seedProp,  new GUIContent("Seed Assembler"));
                    break;

                case ShipTypes.Sparrow:
                    EditorGUILayout.PropertyField(overheatProp, new GUIContent("Overheating Action"));
                    EditorGUILayout.PropertyField(fullAutoProp, new GUIContent("Full-Auto Action"));
                    EditorGUILayout.PropertyField(fireGunProp,  new GUIContent("Fire Gun Action"));
                    EditorGUILayout.PropertyField(stationaryProp, new GUIContent("Stationary Mode"));
                    break;

                // add other variants here as needed...
                default:
                    EditorGUILayout.HelpBox("No actions configured for this Ship Type.", MessageType.Info);
                    break;
            }
        });

        serializedObject.ApplyModifiedProperties();
    }

    void DrawHeader(string title, Color bg)
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

    void DrawSection(string title, Color bg, System.Action drawContent)
    {
        // Section title bar
        var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, bg);
        GUI.Label(new Rect(rect.x + 6, rect.y + 2, rect.width, rect.height),
                  title, EditorStyles.boldLabel);

        // Section body
        GUILayout.BeginVertical(GUI.skin.box);
        drawContent?.Invoke();
        GUILayout.EndVertical();
        GUILayout.Space(6);
    }
}
#endif
