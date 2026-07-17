using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click swap of the charge crystal's visual mesh to the approved export model.
    ///
    /// The charge crystal's look lives in <c>CrystalChargeDandruff.prefab</c> - it is both the
    /// live model referenced by <c>CrystalCharge.prefab</c>'s crystalModels AND its
    /// SpentCrystalPrefab husk (and the RaceCrystalExplosion visuals), so retargeting its
    /// MeshFilter updates the charge crystal everywhere at once. The mesh sub-asset fileID of a
    /// freshly imported FBX is only knowable to Unity, hence an editor step instead of a hand
    /// edit of the prefab YAML.
    ///
    /// Usage: Tools > Cosmic Shore > Swap Charge Crystal Model. Logs old/new mesh bounds so any
    /// needed scale compensation on the prefab is an informed tweak, not a guess.
    /// </summary>
    public static class ChargeCrystalModelSwapper
    {
        const string ModelPath = "Assets/_Models/ChargeCrystalExport1_7-11-25.fbx";
        const string DandruffPrefabPath = "Assets/_Prefabs/Environment/CrystalChargeDandruff.prefab";

        [MenuItem("Tools/Cosmic Shore/Swap Charge Crystal Model")]
        static void Swap()
        {
            Mesh newMesh = null;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (sub is Mesh m) { newMesh = m; break; }
            if (!newMesh)
            {
                Debug.LogError($"[ChargeCrystalModelSwapper] No mesh found in {ModelPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(DandruffPrefabPath);
            try
            {
                var filter = root.GetComponentInChildren<MeshFilter>(true);
                if (!filter)
                {
                    Debug.LogError($"[ChargeCrystalModelSwapper] No MeshFilter in {DandruffPrefabPath}");
                    return;
                }

                var oldMesh = filter.sharedMesh;
                if (oldMesh == newMesh)
                {
                    Debug.Log("[ChargeCrystalModelSwapper] Already using the export mesh - nothing to do.");
                    return;
                }

                filter.sharedMesh = newMesh;
                PrefabUtility.SaveAsPrefabAsset(root, DandruffPrefabPath);
                Debug.Log($"[ChargeCrystalModelSwapper] {DandruffPrefabPath}: " +
                    $"'{(oldMesh ? oldMesh.name : "<none>")}' (bounds {(oldMesh ? oldMesh.bounds.size : Vector3.zero)}) -> " +
                    $"'{newMesh.name}' (bounds {newMesh.bounds.size}). If the new export's authored size differs, " +
                    "compensate on the prefab root scale.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
