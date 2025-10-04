using System.Linq;
using CosmicShore.App.Systems;
using CosmicShore.Core;
using CosmicShore.SOAP;
using CosmicShore.Utility.ClassExtensions;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;


namespace CosmicShore.Game
{
    public class ClientPlayerVesselInitializer : NetworkBehaviour
    {
        [SerializeField] 
        ThemeManagerDataContainerSO themeManagerData;
        
        [SerializeField]
        protected MiniGameDataSO gameData;
        
        [ClientRpc]
        internal void InitializeAllPlayersAndVessels_ClientRpc(ClientRpcParams clientRpcParams = default)
        {
            InitializeAllPlayersAndVessels();
        }
        
        [ClientRpc]
        internal void InitializePlayerAndVessel_ClientRpc(ulong clientId, ClientRpcParams clientRpcParams = default)
        {
            InitializePlayerAndVessel(clientId);
        }
        
        [ClientRpc]
        internal void InvokeClientReady_ClientRpc(ClientRpcParams clientRpcParams = default) => gameData.InvokeClientReady();

        void InitializeAllPlayersAndVessels()
        {
            foreach (var networkPlayer in Player.NppList.Cast<Player>())
            {
                InitializePlayerAndVessel(networkPlayer.OwnerClientId);
            }
        }
        
        void InitializePlayerAndVessel(ulong clientId)
        {
            var networkPlayer = NetworkPlayerClientCache.GetInstanceByClientId(clientId);
            var networkShip = NetworkVesselClientCache.GetInstanceByClientId(clientId);
            networkPlayer.InitializeForMultiplayerMode(networkShip);
            networkShip.Initialize(networkPlayer, false);
            PlayerVesselInitializeHelper.SetShipProperties(themeManagerData, networkShip);
            
            
            var roundStats = networkPlayer.gameObject.GetOrAdd<NetworkRoundStats>();
            if (!roundStats)
            {
                Debug.LogError(
                    "No network round stats found in network player!. Add a NetworkRoundStats component to the prefab!");
                return;
            }
            gameData.AddPlayerInMultiplayer(networkPlayer, roundStats);

            if (IsServer)
            {
                roundStats.Name = networkPlayer.Name;
                roundStats.Domain = networkPlayer.Domain;
            }

            if (clientId.IsLocalClient())
                gameData.InvokeClientReady();
        }
    }
}
