using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Generic base class that caches all active networked instances of type T.
    /// </summary>
    /// <typeparam name="T">
    /// Must be a NetworkBehaviour so that we can access OwnerClientId, and will also have a NetcodeHooks component on the same GameObject.
    /// </typeparam>
    public abstract class NetworkClientCache<T> : MonoBehaviour where T : NetworkBehaviour
    {
        public static Action<T> OnNewInstanceAdded;

        // Static list of all active T instances
        private static readonly List<T> s_ActiveInstances = new();

        /// <summary>
        /// Statics live per CLOSED generic type, and [RuntimeInitializeOnLoadMethod] never runs
        /// for an open generic — so this is called per closed type from
        /// <see cref="NetworkClientCacheDomainReset"/> (add any new T there).
        /// </summary>
        public static void ResetStatics()
        {
            OnNewInstanceAdded = null;
            s_ActiveInstances.Clear();
            OwnInstance = null;
        }

        /// <summary>
        /// Read‐only access to all currently spawned instances of T.
        /// </summary>
        public static IReadOnlyList<T> ActiveInstances => s_ActiveInstances;

        /// <summary>
        /// The T component on *this* client’s owned GameObject.
        /// </summary>
        public static T OwnInstance { get; private set; }

        private NetcodeHooks _netcodeHooks;
        private T _ownComponent;

        protected virtual void Awake()
        {
            // Grab the NetcodeHooks (required) and the T component on this GameObject
            _netcodeHooks = GetComponent<NetcodeHooks>();
            _ownComponent = GetComponent<T>();

            // If this particular MonoBehaviour is running on the owned object:
            if (_ownComponent.IsOwner)
            {
                OwnInstance = _ownComponent;
            }
        }

        protected virtual void OnEnable()
        {
            // Subscribe to spawn/despawn hooks
            _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
            _netcodeHooks.OnNetworkDespawnHook += OnNetworkDespawn;
        }

        protected virtual void OnDisable()
        {
            // Unsubscribe from hooks
            _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
            _netcodeHooks.OnNetworkDespawnHook -= OnNetworkDespawn;

            // In case this object is destroyed without despawning:
            if (s_ActiveInstances.Contains(_ownComponent))
            {
                s_ActiveInstances.Remove(_ownComponent);
            }
        }

        private void OnNetworkSpawn()
        {
            // Add the *spawned* instance to the static list
            if (!_ownComponent) return;
            if (!s_ActiveInstances.Contains(_ownComponent))
            {
                s_ActiveInstances.Add(_ownComponent);
                OnNewInstanceAdded?.Invoke(_ownComponent);
            }
        }

        private void OnNetworkDespawn()
        {
            // Remove from the static list
            if (_ownComponent && s_ActiveInstances.Contains(_ownComponent))
            {
                s_ActiveInstances.Remove(_ownComponent);
            }
        }

        /// <summary>
        /// Returns the first active instance whose OwnerClientId matches the given clientId.
        /// </summary>
        public static T GetInstanceByClientId(ulong networkId)
        {
            foreach (var inst in s_ActiveInstances)
            {
                if (inst.NetworkObjectId == networkId)
                    return inst;
            }
            return null;
        }
    }

    /// <summary>
    /// Non-generic reset host for every closed <see cref="NetworkClientCache{T}"/> type —
    /// the attribute cannot live on the generic class itself.
    /// </summary>
    static class NetworkClientCacheDomainReset
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAllClosedTypes()
        {
            NetworkClientCache<Player>.ResetStatics();
            NetworkClientCache<VesselController>.ResetStatics();
        }
    }
}
