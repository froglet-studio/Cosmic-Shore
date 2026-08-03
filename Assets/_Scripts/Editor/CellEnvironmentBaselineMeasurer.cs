using System.Text;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Measures the TRUE in-engine baseline (prism count + summed volume) of every
    /// cell-environment prefab by running its deterministic generation in edit mode and
    /// summing the produced spawn points. The per-cell PhaseThresholds in the Cell Config
    /// assets are authored as "baseline + the Blob ladder deltas"; this tool is the ground
    /// truth those baselines must match (a simulated baseline that drifts from the C# noise
    /// leaves a cell idling in the wrong phase at rest - see Docs/ECOSYSTEM.md §17).
    /// </summary>
    public static class CellEnvironmentBaselineMeasurer
    {
        [MenuItem("FrogletTools/Ecology/Measure Cell Environment Baselines")]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 4,
            Description = "Measure per-cell prism baselines that phase thresholds ride on.")]
        public static void Measure()
        {
            var report = new StringBuilder("Cell environment baselines (count / summed volume):\n");
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Prefabs/Spawnables" });
            int measured = 0;
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || !prefab.TryGetComponent<CellEnvironmentSpawnableBase>(out var env))
                    continue;

                // GetTrailData caches on the asset; invalidate so edits are always reflected.
                env.InvalidateCache();
                var trails = env.GetTrailData();
                int count = 0;
                double volume = 0;
                foreach (var td in trails)
                    foreach (var pt in td.Points)
                    {
                        count++;
                        volume += (double)pt.Scale.x * pt.Scale.y * pt.Scale.z;
                    }
                env.InvalidateCache(); // don't leave a full lay list cached on the asset

                report.AppendLine($"  {env.GetType().Name,-22} {count,8:N0} prisms   {volume,14:N0} volume   ({path})");
                measured++;
            }

            report.AppendLine(measured > 0
                ? "Author PhaseThresholds as baseline + Blob deltas (+700/+500/+3600/+3000 count, +11200/+8000/+57600/+48000 volume)."
                : "No CellEnvironmentSpawnableBase prefabs found under Assets/_Prefabs/Spawnables.");
            Debug.Log(report.ToString());
        }
    }
}
