#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.Tools.DensityPartitionBenchmark.Editor
{
    /// <summary>
    /// Inspector for DensityPartitionTemporalSimRunner. Runs the ecology sim twice
    /// (old fixed ±500m grid vs new cell-sized grid) and dumps the comparison.
    /// </summary>
    [CustomEditor(typeof(DensityPartitionTemporalSimRunner))]
    public class DensityPartitionTemporalSimRunnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var runner = (DensityPartitionTemporalSimRunner)target;

            EditorGUILayout.Space(8);

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.65f, 0.80f, 0.90f);
            if (GUILayout.Button("Run Temporal Sim (old grid vs new grid)", GUILayout.Height(32)))
            {
                Undo.RecordObject(runner, "Run Temporal Sim");
                runner.RunComparison();
                EditorUtility.SetDirty(runner);
            }
            GUI.backgroundColor = prev;

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(runner.lastReport)))
            {
                if (GUILayout.Button("Copy Last Report to Clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = runner.lastReport ?? "";
                    Debug.Log("[DensityPartitionTemporalSim] Last report copied to clipboard.");
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Edit-Mode safe. Runs the flora/fauna/phase loop for simDurationSeconds " +
                "twice - once with the literal shipped ±500m grid, once with the cell-sized " +
                "grid - and reports whether outer-shell mass stays bounded (fauna reach it) " +
                "or accumulates forever (the grid is blind to it).\n" +
                "This is also an ecology tuning bench: adjust the flora/fauna rate fields " +
                "to search for the parameter regime that produces the two-frequency " +
                "oscillation (fast Calm<->Restless cycle + slow Frenzy dips).",
                MessageType.Info);
        }
    }
}

#endif
