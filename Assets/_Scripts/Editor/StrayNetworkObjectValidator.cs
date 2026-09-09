using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Finds prefabs that carry a <see cref="NetworkObject"/> but are instantiated in bulk
    /// WITHOUT being network-spawned - the authoring combination that breaks multiplayer joins.
    ///
    /// <para>Netcode adopts every un-spawned NetworkObject in a loaded scene as an IN-SCENE
    /// PLACED object the moment a server starts, and indexes in-scene objects by
    /// <c>(GlobalObjectIdHash, sceneHandle)</c>. Every instance of one prefab shares that hash,
    /// so two un-spawned instances of the same prefab in one scene make
    /// <c>PopulateScenePlacedObjects</c> throw - which leaves that NetworkManager's scene manager
    /// broken and stops EVERY later guest from completing synchronization. The runtime is guarded
    /// (<c>NetworkSceneObjectGuard</c>); this reports the authoring so the guard never has to
    /// fire. See Docs/PartySystem/BUGS.md B16.</para>
    ///
    /// Read-only. FrogletTools ▸ Validation ▸ Audit Stray Network Objects.
    /// </summary>
    public static class StrayNetworkObjectValidator
    {
        [MenuItem("FrogletTools/Validation/Audit Stray Network Objects")]
        [FrogletTool(FrogletToolCategory.Validation, Importance = 5,
            Description = "A NetworkObject on a prefab that is instantiated but never spawned breaks every multiplayer join.")]
        public static void Audit()
        {
            var networked = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go && go.GetComponent<NetworkObject>()) networked.Add(go);
            }

            // Group by PREFAB, never by config: four fauna prefabs are referenced by 42 configs,
            // and a line per config reports one fact 42 times. The prefab is the thing an author
            // would actually change.
            var staged = new Dictionary<GameObject, List<string>>();   // rig present, opt-in off - fine
            var bare = new Dictionary<GameObject, List<string>>();     // NetworkObject, no rig - a real stray
            var broken = new Dictionary<GameObject, List<string>>();   // opt-in ON, rig incomplete - dead opt-in

            foreach (var guid in AssetDatabase.FindAssets("t:FaunaConfigurationSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<FaunaConfigurationSO>(path);
                if (!cfg || !cfg.FaunaPrefab) continue;

                var prefabGo = cfg.FaunaPrefab.gameObject;
                bool hasNetObj = prefabGo.GetComponentInChildren<NetworkObject>(true);
                bool hasRig = prefabGo.GetComponentInChildren<FaunaNetworkSync>(true);

                if (cfg.NetworkSynced)
                {
                    if (!hasNetObj || !hasRig) Add(broken, prefabGo, cfg.name);
                    continue;
                }

                if (!hasNetObj) continue;                       // nothing to say - no network layer at all
                Add(hasRig ? staged : bare, prefabGo, cfg.name);
            }

            // The expected, correct state: the replication rig is staged on the prefab and every
            // config leaves the opt-in off (Docs/ECOSYSTEM_NETWORK_SYNC.md). Creatures are stripped
            // at birth by NetworkSceneObjectGuard, so this costs a little work per spawn and
            // nothing else. Informational - NOT a defect to go fix.
            foreach (var pair in staged)
                Debug.Log($"[StrayNetworkObjects] '{pair.Key.name}' carries the staged replication rig " +
                          $"(NetworkObject + FaunaNetworkSync) and all {pair.Value.Count} of its config(s) leave " +
                          "NetworkSynced OFF - the documented pre-rollout state. Creatures are stripped at birth " +
                          "by NetworkSceneObjectGuard; nothing to do.", pair.Key);

            foreach (var pair in bare)
                Debug.LogWarning($"[StrayNetworkObjects] '{pair.Key.name}' carries a NetworkObject but NO " +
                                 "FaunaNetworkSync - it is not staged for replication, so the NetworkObject is " +
                                 "pure liability and should be removed from the prefab. Configs: " +
                                 string.Join(", ", pair.Value), pair.Key);

            foreach (var pair in broken)
                Debug.LogError($"[StrayNetworkObjects] '{pair.Key.name}' is opted IN (NetworkSynced) but its " +
                               "replication rig is incomplete - it needs BOTH a NetworkObject and a " +
                               "FaunaNetworkSync, or the opt-in silently does nothing. Configs: " +
                               string.Join(", ", pair.Value), pair.Key);

            Debug.Log($"[StrayNetworkObjects] {networked.Count} prefab(s) carry a root NetworkObject: " +
                      string.Join(", ", networked.Select(g => g.name).OrderBy(n => n)));
            Debug.Log($"[StrayNetworkObjects] Fauna: {staged.Count} prefab(s) staged for rollout (OK), " +
                      $"{bare.Count} unstaged stray(s), {broken.Count} broken opt-in(s).");
        }

        static void Add(Dictionary<GameObject, List<string>> map, GameObject key, string value)
        {
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<string>();
            list.Add(value);
        }
    }
}
