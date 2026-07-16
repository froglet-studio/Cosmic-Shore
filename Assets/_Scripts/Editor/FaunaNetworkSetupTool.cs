using System.Collections.Generic;
using System.Text;
using CosmicShore.Gameplay;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click rollout steps for server-authoritative fauna sync
    /// (Docs/ECOSYSTEM_NETWORK_SYNC.md §5). Each step is idempotent — run it again and
    /// it only reports. Steps are separate menu items so each species can be networked
    /// and verified in-editor one at a time (the confirmed rollout order):
    ///
    ///   0 — Cell.prefab gets NetworkObject + CellNetworkSync (phase/domain replication
    ///       prerequisite; Menu_Main's Blob Cell instance inherits it).
    ///   1 — Tadpole prefabs (Boid forager — the prism-eating perf lever).
    ///   2 — Brittlestar prefab (LightFauna herbivore).
    ///   3 — Shark prefab (LightFauna predator).
    ///
    /// Fauna prefabs get: NetworkObject + a SERVER-authoritative NetworkTransform
    /// (position+rotation only, half floats, thresholds — never scale: body prisms
    /// scale in locally per peer) + FaunaNetworkSync, and are registered in
    /// DefaultNetworkPrefabs.asset. GlobalObjectIdHash is stamped by Unity's import
    /// pipeline when the prefab saves — run "Validate Fauna Network Setup" afterwards
    /// to confirm it is non-zero.
    /// </summary>
    public static class FaunaNetworkSetupTool
    {
        const string MenuRoot = "Tools/Cosmic Shore/Fauna Sync/";

        const string CellPrefabPath = "Assets/_Prefabs/Environment/Cell.prefab";
        const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";

        static readonly string[] TadpolePrefabPaths =
        {
            "Assets/_Prefabs/FloraAndFauna/MassTadPoleFauna.prefab",
            "Assets/_Prefabs/FloraAndFauna/SpaceTadPoleFauna.prefab",
            "Assets/_Prefabs/FloraAndFauna/TimeTadPoleFauna.prefab",
        };

        static readonly string[] BrittlestarPrefabPaths =
        {
            "Assets/_Models/Fauna/MassBrittlestarFauna.prefab",
        };

        static readonly string[] SharkPrefabPaths =
        {
            "Assets/_Models/Fauna/MassSharkFauna.prefab",
        };

        // NetworkTransform tuning for slow-drifting creatures. Never sync scale — body
        // prisms run their local scale-in/wither animations per peer.
        const float PositionThreshold = 0.1f;
        const float RotationThresholdDegrees = 1.5f;

        [MenuItem(MenuRoot + "0 — Wire Cell Phase Sync (Cell.prefab)")]
        public static void WireCellPhaseSync()
        {
            using (var scope = new PrefabUtility.EditPrefabContentsScope(CellPrefabPath))
            {
                var root = scope.prefabContentsRoot;
                bool changed = false;

                if (!root.TryGetComponent<NetworkObject>(out _))
                {
                    root.AddComponent<NetworkObject>();
                    changed = true;
                }

                if (!root.TryGetComponent<CellNetworkSync>(out var sync))
                {
                    sync = root.AddComponent<CellNetworkSync>();
                    changed = true;
                }

                // Wire the private [SerializeField] cell reference (Awake has a
                // GetComponent fallback, but explicit wiring keeps the inspector honest).
                var so = new SerializedObject(sync);
                var cellProp = so.FindProperty("cell");
                if (cellProp != null && cellProp.objectReferenceValue == null)
                {
                    cellProp.objectReferenceValue = root.GetComponent<Cell>();
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                // Flora replication (Docs/ECOSYSTEM_NETWORK_SYNC.md, Option B): planting
                // decisions replicate as NetworkList slots on the cell.
                if (!root.TryGetComponent<FloraNetworkSync>(out var floraSync))
                {
                    floraSync = root.AddComponent<FloraNetworkSync>();
                    changed = true;
                }

                var floraSo = new SerializedObject(floraSync);
                var floraCellProp = floraSo.FindProperty("cell");
                if (floraCellProp != null && floraCellProp.objectReferenceValue == null)
                {
                    floraCellProp.objectReferenceValue = root.GetComponent<Cell>();
                    floraSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                Debug.Log(changed
                    ? $"[FaunaNetworkSetup] Cell.prefab wired: NetworkObject + CellNetworkSync + FloraNetworkSync added. " +
                      "In-scene Cell instances (Menu_Main Blob Cell) replicate phase/domain + flora plants " +
                      "once their scenes are resaved/reimported."
                    : "[FaunaNetworkSetup] Cell.prefab already wired — nothing to do.");
            }
        }

        [MenuItem(MenuRoot + "1 — Network Tadpole Prefabs (Boid forager)")]
        public static void NetworkTadpoles() => NetworkFaunaPrefabs("Tadpole", TadpolePrefabPaths);

        [MenuItem(MenuRoot + "2 — Network Brittlestar Prefab (LightFauna herbivore)")]
        public static void NetworkBrittlestar() => NetworkFaunaPrefabs("Brittlestar", BrittlestarPrefabPaths);

        [MenuItem(MenuRoot + "3 — Network Shark Prefab (LightFauna predator)")]
        public static void NetworkShark() => NetworkFaunaPrefabs("Shark", SharkPrefabPaths);

        [MenuItem(MenuRoot + "Validate Fauna Network Setup")]
        public static void Validate()
        {
            var report = new StringBuilder("[FaunaNetworkSetup] Validation:\n");
            ValidateCell(report);

            var all = new List<string>();
            all.AddRange(TadpolePrefabPaths);
            all.AddRange(BrittlestarPrefabPaths);
            all.AddRange(SharkPrefabPaths);
            foreach (var path in all)
                ValidateFaunaPrefab(path, report);

            Debug.Log(report.ToString());
        }

        // ------------------------------------------------------------------

        static void NetworkFaunaPrefabs(string label, IReadOnlyList<string> paths)
        {
            int wired = 0, registered = 0;
            foreach (var path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogError($"[FaunaNetworkSetup] Prefab not found at '{path}' — skipped.");
                    continue;
                }

                if (AddNetworkComponents(path)) wired++;
                if (RegisterInDefaultNetworkPrefabs(path)) registered++;
            }

            Debug.Log($"[FaunaNetworkSetup] {label}: components added on {wired} prefab(s), " +
                      $"{registered} new registration(s) in DefaultNetworkPrefabs. " +
                      "Save/import stamps NetworkObject.GlobalObjectIdHash automatically — run " +
                      "'Validate Fauna Network Setup' to confirm, then verify in MPPM per " +
                      "Docs/ECOSYSTEM_NETWORK_SYNC.md §7.");
        }

        static bool AddNetworkComponents(string prefabPath)
        {
            bool changed = false;
            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                var root = scope.prefabContentsRoot;

                var fauna = root.GetComponent<Fauna>();
                if (!fauna)
                {
                    Debug.LogError($"[FaunaNetworkSetup] '{prefabPath}' has no Fauna component on its root — skipped.");
                    return false;
                }

                if (!root.TryGetComponent<NetworkObject>(out _))
                {
                    root.AddComponent<NetworkObject>();
                    changed = true;
                }

                if (!root.TryGetComponent<NetworkTransform>(out var netTransform))
                {
                    netTransform = root.AddComponent<NetworkTransform>();
                    changed = true;
                }

                // Server-authoritative by default (stock NetworkTransform, never the
                // owner-authoritative ClientNetworkTransform vessels use).
                netTransform.SyncScaleX = false;
                netTransform.SyncScaleY = false;
                netTransform.SyncScaleZ = false;
                netTransform.InLocalSpace = false;
                netTransform.Interpolate = true;
                netTransform.UseHalfFloatPrecision = true;
                netTransform.PositionThreshold = PositionThreshold;
                netTransform.RotAngleThreshold = RotationThresholdDegrees;

                if (!root.TryGetComponent<FaunaNetworkSync>(out var sync))
                {
                    sync = root.AddComponent<FaunaNetworkSync>();
                    changed = true;
                }

                var so = new SerializedObject(sync);
                var faunaProp = so.FindProperty("fauna");
                if (faunaProp != null && faunaProp.objectReferenceValue == null)
                {
                    faunaProp.objectReferenceValue = fauna;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>
        /// Appends the prefab to DefaultNetworkPrefabs.asset via serialized properties —
        /// robust against NetworkPrefabsList API drift, and exactly the shape existing
        /// entries use ({Override:None, Prefab:ref}). No-ops when already registered.
        /// </summary>
        static bool RegisterInDefaultNetworkPrefabs(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var listAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(NetworkPrefabsListPath);
            if (!prefab || !listAsset)
            {
                Debug.LogError($"[FaunaNetworkSetup] Missing '{prefabPath}' or '{NetworkPrefabsListPath}'.");
                return false;
            }

            var so = new SerializedObject(listAsset);
            var list = so.FindProperty("List");
            if (list == null || !list.isArray)
            {
                Debug.LogError("[FaunaNetworkSetup] DefaultNetworkPrefabs has no 'List' property — NGO layout changed?");
                return false;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab");
                if (entry != null && entry.objectReferenceValue == prefab)
                    return false; // already registered
            }

            int idx = list.arraySize;
            list.arraySize = idx + 1;
            var element = list.GetArrayElementAtIndex(idx);
            // arraySize++ clones the previous element — overwrite every field.
            element.FindPropertyRelative("Override").intValue = 0;
            element.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            element.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
            element.FindPropertyRelative("SourceHashToOverride").longValue = 0;
            element.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(listAsset);
            AssetDatabase.SaveAssetIfDirty(listAsset);
            return true;
        }

        static void ValidateCell(StringBuilder report)
        {
            var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
            if (!cellPrefab)
            {
                report.AppendLine($"  ✗ Cell prefab missing at {CellPrefabPath}");
                return;
            }
            bool hasNetObj = cellPrefab.GetComponent<NetworkObject>();
            bool hasSync = cellPrefab.GetComponent<CellNetworkSync>();
            bool hasFloraSync = cellPrefab.GetComponent<FloraNetworkSync>();
            report.AppendLine($"  {(hasNetObj && hasSync && hasFloraSync ? "✓" : "✗")} Cell.prefab — NetworkObject: {hasNetObj}, " +
                              $"CellNetworkSync: {hasSync}, FloraNetworkSync: {hasFloraSync}");
        }

        static void ValidateFaunaPrefab(string path, StringBuilder report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
            {
                report.AppendLine($"  ✗ missing prefab: {path}");
                return;
            }

            var netObj = prefab.GetComponent<NetworkObject>();
            bool hasTransform = prefab.GetComponent<NetworkTransform>();
            bool hasSync = prefab.GetComponent<FaunaNetworkSync>();

            bool hashOk = false;
            if (netObj)
            {
                var prop = new SerializedObject(netObj).FindProperty("GlobalObjectIdHash");
                hashOk = prop != null && prop.longValue != 0;
            }

            bool registered = false;
            var listAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(NetworkPrefabsListPath);
            if (listAsset)
            {
                var list = new SerializedObject(listAsset).FindProperty("List");
                for (int i = 0; list != null && i < list.arraySize; i++)
                {
                    var entry = list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab");
                    if (entry != null && entry.objectReferenceValue == prefab) { registered = true; break; }
                }
            }

            bool ok = netObj && hasTransform && hasSync && hashOk && registered;
            report.AppendLine($"  {(ok ? "✓" : "✗")} {System.IO.Path.GetFileName(path)} — NetworkObject: {(bool)netObj}, " +
                              $"NetworkTransform: {hasTransform}, FaunaNetworkSync: {hasSync}, " +
                              $"GlobalObjectIdHash≠0: {hashOk}, registered: {registered}");
        }
    }
}
