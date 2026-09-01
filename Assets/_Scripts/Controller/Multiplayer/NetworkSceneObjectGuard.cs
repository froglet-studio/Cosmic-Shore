using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Removes STRAY NetworkObjects - prefab instances that were created with plain
    /// <c>Instantiate</c> and never network-spawned - before a NetworkManager can adopt them.
    ///
    /// <para><b>Why this exists.</b> When a server starts, Netcode sweeps the loaded scenes and
    /// adopts every NetworkObject it finds that is not already spawned, treating each one as an
    /// IN-SCENE PLACED object. Netcode then indexes in-scene objects by
    /// <c>(GlobalObjectIdHash, sceneHandle)</c>, and that index must be unique - a second object
    /// with the same pair makes <c>NetworkSceneManager.PopulateScenePlacedObjects</c> THROW.
    /// Every instance of one prefab carries the SAME hash, so N un-spawned instances of the same
    /// prefab in one scene is a guaranteed exception the moment a host starts or a client
    /// synchronizes.</para>
    ///
    /// <para><b>What that cost us.</b> The lava lamp's fauna prefabs carry a NetworkObject as
    /// their per-species replication opt-in (<c>FaunaConfigurationSO.NetworkSynced</c>), and the
    /// menu's species have that opt-in OFF - so every tadpole and quadfish swimming in Menu_Main
    /// was an un-spawned NetworkObject. A COLD-BOOTED host never noticed: it starts in the
    /// Authentication scene, before any fauna exist. A host that RESTARTS in place (a party
    /// leave, a failed-join bounce) loads Menu_Main locally first, so by the time it starts there
    /// are already a dozen identical-hash strays swimming in the scene - the sweep adopted them,
    /// the index collided, the exception left that NetworkManager's scene manager broken, and no
    /// guest could ever complete synchronization again. The guest's log showed only the far end
    /// of it: deferred spawn messages for objects it never received, then the 30s connect
    /// timeout and a bounce. "Restart the game" worked because it restored the cold-boot order.
    /// See Docs/PartySystem/BUGS.md B16.</para>
    ///
    /// <para><b>The rule.</b> A prefab instance is not an in-scene object, so it must never be
    /// adopted as one. Where the creating code knows it will never spawn the thing (the fauna
    /// seam), it strips the network layer at birth via <see cref="NeutralizeStray"/>. Everything
    /// else is caught by <see cref="Sweep"/>, which runs at the network transition boundaries -
    /// the moments just before Netcode would do its own adopting sweep.</para>
    ///
    /// <para>Reflection is used to read <c>GlobalObjectIdHash</c> because Netcode declares it
    /// <c>internal</c>. It is read at transition boundaries only (never per frame), resolved
    /// once, and the sweep degrades to a no-op (with one warning) if a future Netcode renames
    /// it - the birth-time strip is the primary defence either way.</para>
    /// </summary>
    public static class NetworkSceneObjectGuard
    {
        static FieldInfo _hashField;
        static bool _hashFieldResolved;
        static bool _warnedNoHashField;

        /// <summary>Hashes already reported, so a recurring stray logs once rather than per sweep.</summary>
        static readonly HashSet<uint> _reported = new();

        /// <summary>
        /// Strips the network layer from a GameObject that will never be network-spawned, so
        /// Netcode's start-up sweep cannot adopt it as an in-scene object. Safe to call on
        /// anything: it no-ops when there is no NetworkObject and when the object IS spawned.
        ///
        /// <para>Uses <c>DestroyImmediate</c> deliberately: callers invoke this at a point where
        /// the object must be invisible to Netcode NOW (before a host start in the same call
        /// stack), and ordinary <c>Destroy</c> defers to the end of the frame.</para>
        /// </summary>
        public static bool NeutralizeStray(GameObject go, string reason)
        {
            if (!go) return false;
            if (!go.TryGetComponent(out NetworkObject netObj)) return false;
            return NeutralizeStray(netObj, reason);
        }

        /// <inheritdoc cref="NeutralizeStray(GameObject,string)"/>
        public static bool NeutralizeStray(NetworkObject netObj, string reason)
        {
            if (!netObj) return false;
            if (netObj.IsSpawned) return false;   // Netcode already owns it - never touch a live object.

            var go = netObj.gameObject;

            // NetworkBehaviours first: a NetworkBehaviour whose NetworkObject vanished logs an
            // error on its next enable/disable, and they are inert on an object that will never
            // be spawned anyway.
            var behaviours = go.GetComponentsInChildren<NetworkBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                // Leave anything belonging to a NESTED NetworkObject alone - it is not ours to strip.
                if (!behaviour || behaviour.NetworkObject != netObj) continue;
                UnityEngine.Object.DestroyImmediate(behaviour);
            }

            UnityEngine.Object.DestroyImmediate(netObj);

            if (TryGetHash(netObj, out uint hash) && !_reported.Add(hash))
                return true;   // already reported this prefab once

            CSDebug.LogWarning(
                $"[NetworkSceneObjectGuard] Stripped the NetworkObject from '{go.name}' ({reason}). " +
                "It is a prefab instance that is never network-spawned, and Netcode would have adopted " +
                "it as an in-scene object at the next host start - a second instance of the same prefab " +
                "then collides in the scene-object index and breaks synchronization for every joining " +
                "player. If this object is MEANT to replicate, spawn it (NetworkObject.Spawn); if it is " +
                "not, remove the NetworkObject from its prefab. See Docs/PartySystem/BUGS.md B16.");
            return true;
        }

        /// <summary>
        /// Neutralizes every stray that would collide in Netcode's in-scene object index: any
        /// group of two or more un-spawned NetworkObjects sharing a
        /// <c>(GlobalObjectIdHash, scene)</c> pair. The FIRST of each group is kept, because a
        /// genuine in-scene placed object is legitimately un-spawned before the host starts and
        /// must survive; only the surplus - which can only be prefab instances - is stripped.
        ///
        /// <para>Call this immediately before anything that starts a NetworkManager as server or
        /// client. Returns how many objects were stripped.</para>
        /// </summary>
        public static int Sweep(string reason)
        {
            NetworkObject[] all;
            try
            {
                all = UnityEngine.Object.FindObjectsByType<NetworkObject>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[NetworkSceneObjectGuard] Sweep could not enumerate NetworkObjects ({e.GetType().Name}): {e.Message}");
                return 0;
            }

            if (all == null || all.Length == 0) return 0;

            var groups = new Dictionary<(uint hash, int scene), List<NetworkObject>>();
            foreach (var netObj in all)
            {
                if (!netObj || netObj.IsSpawned) continue;
                if (!TryGetHash(netObj, out uint hash)) return 0;   // reflection unavailable - see field doc

                var key = (hash, netObj.gameObject.scene.handle);
                if (!groups.TryGetValue(key, out var list))
                    groups[key] = list = new List<NetworkObject>();
                list.Add(netObj);
            }

            int stripped = 0;
            foreach (var pair in groups)
            {
                var list = pair.Value;
                if (list.Count < 2) continue;

                for (int i = 1; i < list.Count; i++)
                    if (NeutralizeStray(list[i], $"{reason} - duplicate scene-object index entry ({list.Count} instances)"))
                        stripped++;
            }

            if (stripped > 0)
                CSDebug.LogWarning($"[NetworkSceneObjectGuard] {reason}: stripped {stripped} stray NetworkObject(s) that would have collided in the scene-object index.");

            return stripped;
        }

        static bool TryGetHash(NetworkObject netObj, out uint hash)
        {
            hash = 0;
            if (!netObj) return false;

            if (!_hashFieldResolved)
            {
                _hashFieldResolved = true;
                _hashField = typeof(NetworkObject).GetField("GlobalObjectIdHash",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (_hashField == null)
            {
                if (!_warnedNoHashField)
                {
                    _warnedNoHashField = true;
                    CSDebug.LogWarning(
                        "[NetworkSceneObjectGuard] NetworkObject.GlobalObjectIdHash could not be resolved - " +
                        "the duplicate sweep is inactive on this Netcode version. Birth-time stripping " +
                        "(NeutralizeStray) still applies. See Docs/PartySystem/BUGS.md B16.");
                }
                return false;
            }

            try
            {
                hash = (uint)_hashField.GetValue(netObj);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
