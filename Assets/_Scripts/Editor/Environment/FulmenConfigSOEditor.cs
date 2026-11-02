using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// FulmenConfigSO's inspector (also what the Environmental Fundamentals window draws): the tuning, then a read-only
    /// "Last execution" block - prisms burnt, crystals struck, bolts - fed live by <see cref="FulmenSystem"/> in play mode.
    /// </summary>
    [CustomEditor(typeof(FulmenConfigSO))]
    public sealed class FulmenConfigSOEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var cfg = (FulmenConfigSO)target;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Last execution (play mode)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField(new GUIContent("Prisms Burnt",
                    "Prisms set alight by the most recent strike's whole chain. Counted where the burn runs: the server or offline (0 on a client)."),
                    cfg.LastPrismsBurnt);
                EditorGUILayout.IntField("Crystals Struck", cfg.LastCrystalsStruck);
                EditorGUILayout.IntField("Bolts", cfg.LastBolts);
                EditorGUILayout.TextField("Fired", cfg.LastStrikeTime < 0f
                    ? "never"
                    : $"{Mathf.Max(0f, Time.time - cfg.LastStrikeTime):0.0}s ago (strike #{cfg.Strikes})");
            }
        }
    }
}
