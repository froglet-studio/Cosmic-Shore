#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.Tools.DensityPartitionBenchmark.Editor
{
    /// <summary>
    /// Inspector for DensityPartitionBenchmarkRunner. Two prominent action buttons:
    ///   - "Run All && Dump Report" - runs every algorithm against every scenario,
    ///     dumps to the Console, and stores the result in lastReport.
    ///   - "Copy lastReport to Clipboard" - pastes the most recent report into the
    ///     OS clipboard so the user can paste it back to the prompter.
    /// </summary>
    [CustomEditor(typeof(DensityPartitionBenchmarkRunner))]
    public class DensityPartitionBenchmarkRunnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var runner = (DensityPartitionBenchmarkRunner)target;

            EditorGUILayout.Space(8);

            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.65f, 0.85f, 0.7f);
            if (GUILayout.Button("Run All && Dump Report", GUILayout.Height(32)))
            {
                Undo.RecordObject(runner, "Run Density Benchmark");
                runner.RunAllAndDump();
                EditorUtility.SetDirty(runner);
            }
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Reset Config && Scenarios to Defaults"))
            {
                Undo.RecordObject(runner, "Reset Density Benchmark Config");
                runner.ResetToDefaults();
                EditorUtility.SetDirty(runner);
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(runner.lastReport)))
            {
                if (GUILayout.Button("Copy lastReport to Clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = runner.lastReport ?? "";
                    Debug.Log("[DensityPartitionBenchmark] Last report copied to clipboard.");
                }

                if (GUILayout.Button("Clear lastReport"))
                {
                    Undo.RecordObject(runner, "Clear Benchmark Report");
                    runner.lastReport = "";
                    EditorUtility.SetDirty(runner);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Edit-Mode safe. No Play Mode required.\n" +
                "Default scenarios cover the audit's failure modes (UniformRandom, " +
                "SingleCluster, MultiCluster, Gradient, plus a 30%-stale variant " +
                "that reproduces the ChangeTeam-after-AddBlock bug from §2.3.1).\n" +
                "Paste the report back to the prompter - that's the falsifiable " +
                "contract any algorithm redesign has to meet.",
                MessageType.Info);
        }
    }
}

#endif
