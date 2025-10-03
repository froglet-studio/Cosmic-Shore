using System.Linq;
using CosmicShore.App.Systems;
using CosmicShore.Core;
using CosmicShore.SOAP;
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
                
            gameData.AddPlayer(networkPlayer);

            if (clientId == networkPlayer.OwnerClientId)
                gameData.InvokeClientReady();
        }
    }
}
