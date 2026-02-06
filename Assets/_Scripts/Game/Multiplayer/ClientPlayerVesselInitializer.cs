using System;
using System.Linq;
using System.Threading;
using CosmicShore.App.Systems;
using CosmicShore.Core;
using CosmicShore.Soap;
using CosmicShore.Utility.ClassExtensions;
using Cysharp.Threading.Tasks;
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
        protected GameDataSO gameData;
        
        /// <summary>
        /// // This is the new client, and we have to initialize all the other client's vessel and player clones in this client.
        /// </summary>
        [ClientRpc]
        internal void InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc(
            ClientRpcParams clientRpcParams = default)
        {
            // NPP List will contain all the players and the AIs to be initialized in this new client
            foreach (var networkPlayer in Player.NppList.Cast<Player>())
            {
                ulong playerId = networkPlayer.PlayerNetId;
                ulong vesselId = networkPlayer.NetVesselId.Value;
                
                InitializePlayerAndVesselByNetIds(playerId, vesselId, this.GetCancellationTokenOnDestroy()).Forget();
            }
            
            DelayInvokeClientReady(this.GetCancellationTokenOnDestroy()).Forget();
        }
        
        /// <summary>
        /// // A new client joined in this client, we need to initialize the new client's owned vessel and player clone only in the existing client.
        /// </summary>
        [ClientRpc]
        internal void InitializeNewClientsOwnerPlayerAndVesselInExistingClient_ClientRpc(
            ulong newJoinedClientId,
            ClientRpcParams clientRpcParams = default)
        {
            var sm = NetworkManager.Singleton.SpawnManager;
            var playerNO = sm.GetPlayerNetworkObject(newJoinedClientId);
            var ownerPlayerId = playerNO.NetworkObjectId;
            if (!playerNO.TryGetComponent(out Player player))
            {
                Debug.LogError("This should not happen!");
                return;
            }
            var ownerVesselId = player.NetVesselId.Value;
            InitializePlayerAndVesselByNetIds(ownerPlayerId, ownerVesselId, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid InitializePlayerAndVesselByNetIds(ulong playerId, ulong vesselId, System.Threading.CancellationToken token)
        {
            var sm = NetworkManager.Singleton.SpawnManager;

            await UniTask.WaitUntil(
                () => sm.SpawnedObjects.ContainsKey(playerId) && sm.SpawnedObjects.ContainsKey(vesselId),
                cancellationToken: token
            );

            if (token.IsCancellationRequested) return;

            var playerNO = sm.SpawnedObjects[playerId];
            var vesselNO = sm.SpawnedObjects[vesselId];

            var networkPlayer = playerNO.GetComponent<Player>();
            var networkShip = vesselNO.GetComponent<IVessel>();

            if (!networkPlayer || networkShip == null)
            {
                Debug.LogError("[ClientPlayerVesselInitializer] AI Player/Vessel components missing.");
                return;
            }

            // Reuse your existing initialization logic (recommended)
            networkPlayer.InitializeForMultiplayerMode(networkShip);
            networkShip.Initialize(networkPlayer);
            ShipHelper
                .SetShipProperties(themeManagerData, networkShip);
            gameData.AddPlayer(networkPlayer);
        }
        
        async UniTaskVoid DelayInvokeClientReady(CancellationToken token)
        {
            await UniTask.Delay(1000, DelayType.UnscaledDeltaTime, PlayerLoopTiming.LastPostLateUpdate ,token);
            if (token.IsCancellationRequested)
                return;
            
            gameData.InvokeClientReady();
        }
    }
}
