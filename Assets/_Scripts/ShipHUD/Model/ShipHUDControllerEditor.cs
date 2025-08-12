#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CosmicShore.Game;

[CustomEditor(typeof(ShipHUDController))]
public class ShipHUDControllerEditor : Editor
{
    // Props
    private SerializedProperty _shipTypeProp;
    private SerializedProperty _chargeBoostProp;
    private SerializedProperty _boostProp, _seedProp;
    private SerializedProperty _overheatProp, _fullAutoProp, _fireGunProp, _stationaryProp;

    // Colors (tweak as you like)
    private readonly Color _headerColor   = new Color(0.1f, 0.1f, 0.35f);
    private readonly Color _sectionColor  = new Color(0.12f, 0.12f, 0.25f);

    private void OnEnable()
    {
        _shipTypeProp       = serializedObject.FindProperty("shipType");
        _chargeBoostProp     = serializedObject.FindProperty("chargeBoostAction");
        _boostProp          = serializedObject.FindProperty("boostAction");
        _seedProp           = serializedObject.FindProperty("seedAssemblerAction");
        _overheatProp       = serializedObject.FindProperty("overheatingAction");
        _fullAutoProp       = serializedObject.FindProperty("fullAutoAction");
        _fireGunProp        = serializedObject.FindProperty("fireGunAction");
        _stationaryProp     = serializedObject.FindProperty("stationaryModeAction");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // --- Header ---
        DrawHeader("Ship HUD Controller", _headerColor);

        // Ship type dropdown
        EditorGUILayout.PropertyField(_shipTypeProp, new GUIContent("Ship Type"));

        // Only show the relevant action fields
        ShipClassType type = (ShipClassType)_shipTypeProp.intValue;
        GUILayout.Space(6);
        DrawSection(type + " Actions", _sectionColor, () =>
        {
            switch (type)
            {
                case ShipClassType.Serpent:
                    EditorGUILayout.PropertyField(_boostProp, new GUIContent("Boost Action"));
                    EditorGUILayout.PropertyField(_seedProp,  new GUIContent("Seed Assembler"));
                    break;

                case ShipClassType.Sparrow:
                    EditorGUILayout.PropertyField(_overheatProp, new GUIContent("Overheating Action"));
                    EditorGUILayout.PropertyField(_fullAutoProp, new GUIContent("Full-Auto Action"));
                    EditorGUILayout.PropertyField(_fireGunProp,  new GUIContent("Fire Gun Action"));
                    EditorGUILayout.PropertyField(_stationaryProp, new GUIContent("Stationary Mode"));
                    break;
                
                case ShipClassType.Dolphin:
                    EditorGUILayout.PropertyField(_chargeBoostProp, new GUIContent("Charge Boost Action"));
                    break;
                case ShipClassType.Any:
                case ShipClassType.Random:
                case ShipClassType.Manta:
                case ShipClassType.Rhino:
                case ShipClassType.Urchin:
                case ShipClassType.Grizzly:
                case ShipClassType.Squirrel:
                case ShipClassType.Termite:
                case ShipClassType.Falcon:
                case ShipClassType.Shrike:
                    break;
                default:
                    EditorGUILayout.HelpBox("No actions configured for this Ship Type.", MessageType.Info);
                    break;
            }
        });

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawHeader(string title, Color bg)
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

    private static void DrawSection(string title, Color bg, System.Action drawContent)
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
