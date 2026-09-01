using System.Collections.Generic;
using System.Linq;
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

            Debug.Log($"[StrayNetworkObjects] {networked.Count} prefab(s) carry a root NetworkObject.");

            // The fauna case: a species prefab carries a NetworkObject as its replication opt-in,
            // but the CONFIG leaves that opt-in off - so every creature the cell spawns is a
            // stray. This is the exact combination that shipped in the menu's lava lamp.
            int flagged = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:FaunaConfigurationSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<FaunaConfigurationSO>(path);
                if (!cfg || !cfg.FaunaPrefab) continue;

                var prefabGo = cfg.FaunaPrefab.gameObject;
                if (!prefabGo.GetComponentInChildren<NetworkObject>(true)) continue;
                if (cfg.NetworkSynced) continue;

                flagged++;
                Debug.LogWarning(
                    $"[StrayNetworkObjects] '{cfg.name}' spawns '{prefabGo.name}', which carries a " +
                    "NetworkObject, but NetworkSynced is OFF - every creature it spawns is an un-spawned " +
                    "NetworkObject. NetworkSceneObjectGuard strips these at birth, so this is not fatal; " +
                    "it is dead weight on the prefab. Either turn NetworkSynced on, or remove the " +
                    "NetworkObject from the prefab if the species is never meant to replicate.\n" +
                    $"    config: {path}", cfg);
            }

            if (flagged == 0)
                Debug.Log("[StrayNetworkObjects] No fauna config spawns a NetworkObject-carrying prefab with replication off.");
            else
                Debug.LogWarning($"[StrayNetworkObjects] {flagged} fauna config(s) flagged - see the warnings above.");

            // Anything else that is instantiated a lot and carries a NetworkObject is worth a look
            // by eye; list them so the reviewer can check each one is genuinely spawned.
            var names = string.Join(", ", networked.Select(g => g.name).OrderBy(n => n));
            Debug.Log($"[StrayNetworkObjects] Prefabs with a NetworkObject: {names}");
        }
    }
}
