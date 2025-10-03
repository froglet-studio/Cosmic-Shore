using CosmicShore.App.Systems;
using CosmicShore.Core;
using CosmicShore.SOAP;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;


namespace CosmicShore.Game
{
    public class ClientPlayerSpawner : NetworkBehaviour
    {
        [SerializeField] 
        ThemeManagerDataContainerSO themeManagerData;
        
        [SerializeField]
        protected MiniGameDataSO gameData;
        
        [ClientRpc]
        internal void InitializeAndSetupPlayer_ClientRpc(ClientRpcParams clientRpcParams = default)
        {
            InitializeAndSetupPlayer();
        }

        protected virtual void InitializeAndSetupPlayer()
        {
            foreach (Player networkPlayer in Player.NppList)
            {
                var networkShip = NetworkVesselClientCache.GetInstanceByClientId(networkPlayer.OwnerClientId);
                if (!networkShip) continue;

                networkPlayer.InitializeForMultiplayerMode(networkShip);
                networkShip.Initialize(networkPlayer, false);
                PlayerVesselInitializeHelper.SetShipProperties(themeManagerData, networkShip);
                
                gameData.AddPlayer(networkPlayer);
            }
        }
    }
}
