using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Utility.Recording
{
    /// <summary>
    /// Toggles the current cell's membrane between two prefab variants
    /// (e.g. <c>BigMembraneVariant</c> and <c>CapsuleMembrane</c>).
    /// Reads the active cell from a <see cref="CellRuntimeDataSO"/>.
    /// </summary>
    [Serializable]
    public class ToggleCellMembraneAction : VideoMacroAction
    {
        [SerializeField] CellRuntimeDataSO cellRuntime;
        [SerializeField] GameObject membraneA;
        [SerializeField] GameObject membraneB;

        [NonSerialized] bool showingB;

        public override string DisplayName => "Toggle Cell Membrane";

        public override void Execute()
        {
            if (cellRuntime == null || cellRuntime.Cell == null)
            {
                CSDebug.LogWarning("[ToggleCellMembraneAction] No active cell to toggle.");
                return;
            }
            if (membraneA == null || membraneB == null)
            {
                CSDebug.LogWarning("[ToggleCellMembraneAction] Both membrane prefabs must be assigned.");
                return;
            }

            var nextPrefab = showingB ? membraneA : membraneB;
            cellRuntime.Cell.SwapMembrane(nextPrefab);
            showingB = !showingB;
        }

#if UNITY_EDITOR
        public override void DrawEditor(UnityEngine.Object owner)
        {
            EditorGUI.BeginChangeCheck();
            cellRuntime = (CellRuntimeDataSO)EditorGUILayout.ObjectField(
                "Cell Runtime", cellRuntime, typeof(CellRuntimeDataSO), false);
            membraneA = (GameObject)EditorGUILayout.ObjectField(
                "Membrane A", membraneA, typeof(GameObject), false);
            membraneB = (GameObject)EditorGUILayout.ObjectField(
                "Membrane B", membraneB, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck() && owner != null)
                EditorUtility.SetDirty(owner);
        }
#endif
    }
}
