using CosmicShore.NetworkManagement;
using CosmicShore.Utilities.Network;
using System.Collections.Generic;
using Unity.Multiplayer.Samples.Utilities;
using UnityEngine;


namespace CosmicShore.Game
{
    /// <summary>
    /// Temporary cache for all active players in the game.
    /// Later create NetworkShipRuntimeCollectionSO to store all active players in the game.
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    [RequireComponent(typeof(NetworkShip))]
    public class NetworkShipClientCache : MonoBehaviour
    {
        private static List<NetworkShip> ms_ActiveShips = new List<NetworkShip>();
        public static List<NetworkShip> ActiveShips => ms_ActiveShips;

        private NetcodeHooks m_NetcodeHooks;
        public static NetworkShip OwnShip { get; private set; } // This is the owner of this client instance


        private void Awake()
        {
            m_NetcodeHooks = GetComponent<NetcodeHooks>();
            OwnShip = GetComponent<NetworkShip>();
        }

        private void OnEnable()
        {
            m_NetcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
            m_NetcodeHooks.OnNetworkDespawnHook += OnNetworkDespawn;
        }

        private void OnDisable()
        {
            m_NetcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
            m_NetcodeHooks.OnNetworkDespawnHook -= OnNetworkDespawn;

            ms_ActiveShips.Remove(OwnShip);
        }

        private void OnNetworkSpawn()
        {
            ms_ActiveShips.Add(OwnShip);
            // LogCaching();
        }

        private void OnNetworkDespawn()
        {
            if (m_NetcodeHooks.IsServer)
            {
                Transform movementTransform = OwnShip.transform;
                SessionPlayerData? sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(m_NetcodeHooks.OwnerClientId);
                if (sessionPlayerData.HasValue)
                {
                    SessionPlayerData playerData = sessionPlayerData.Value;
                    playerData.PlayerPosition = movementTransform.position;
                    playerData.PlayerRotation = movementTransform.rotation;
                    playerData.HasCharacterSpawned = true;
                    SessionManager<SessionPlayerData>.Instance.SetPlayerData(m_NetcodeHooks.OwnerClientId, playerData);
                }
            }
            else
            {
                ms_ActiveShips.Remove(OwnShip);
            }
        }

        public static NetworkShip GetShip(ulong ownerClientId)
        {
            foreach (NetworkShip playerView in ms_ActiveShips)
            {
                if (playerView.OwnerClientId == ownerClientId)
                {
                    return playerView;
                }
            }

            return null;
        }

        private void LogCaching()
        {
            if (m_NetcodeHooks.IsServer)
            {
                Debug.Log("Server cached network player with client id " + m_NetcodeHooks.OwnerClientId);
            }
            else
            {
                Debug.Log("Client cached network player with client id " + m_NetcodeHooks.OwnerClientId);
            }
        }
    }

}
