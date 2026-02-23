#if UNITY_EDITOR
using CosmicShore.Game.ShapeDrawing;
using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(ShapeDefinition))]
public class ShapeDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var def = (ShapeDefinition)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("─── Procedural Generation ───", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "These buttons overwrite the current waypoints with a procedural shape. " +
            "Use as a starting point, then tweak waypoints manually.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("⭐ Star")) GeneratePreset(def, ShapeDefinition.ShapePreset.Star);
        if (GUILayout.Button("⬤ Circle")) GeneratePreset(def, ShapeDefinition.ShapePreset.Circle);
        if (GUILayout.Button("☺ Smiley")) GeneratePreset(def, ShapeDefinition.ShapePreset.Smiley);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("♥ Heart")) GeneratePreset(def, ShapeDefinition.ShapePreset.Heart);
        if (GUILayout.Button("⚡ Lightning")) GeneratePreset(def, ShapeDefinition.ShapePreset.Lightning);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Current waypoint count: {def.waypoints?.Count ?? 0}",
            EditorStyles.miniLabel);
    }

    void GeneratePreset(ShapeDefinition def, ShapeDefinition.ShapePreset preset)
    {
        Undo.RecordObject(def, $"Generate {preset} waypoints");
        def.GeneratePreset(preset, 100f);
        EditorUtility.SetDirty(def);
        Debug.Log($"[ShapeDefinition] Generated {preset} with {def.waypoints.Count} waypoints.");
    }
}
#endif