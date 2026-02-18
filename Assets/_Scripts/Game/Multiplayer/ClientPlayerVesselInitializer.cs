using System.Threading;
using CosmicShore.Core;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;


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
            foreach (var networkPlayer in gameData.Players)
            {
                ulong playerId = networkPlayer.PlayerNetId;
                ulong vesselId = networkPlayer.VesselNetId;
                
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

            if (!gameData.TryGetPlayerByOwnerClientId(newJoinedClientId, out var player))
            {
                Debug.LogError($"No player found for client Id: {player.PlayerNetId}");
                return;
            }
                
            var ownerPlayerId = player.PlayerNetId;
            var ownerVesselId = player.VesselNetId;
            InitializePlayerAndVesselByNetIds(ownerPlayerId, ownerVesselId, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid InitializePlayerAndVesselByNetIds(
            ulong playerId,
            ulong vesselId,
            CancellationToken token)
        {
            // Wait until BOTH are available (or cancel)
            await UniTask.WaitUntil(() =>
                    gameData.TryGetPlayerByNetworkObjectId(playerId, out _) &&
                    gameData.TryGetVesselByNetworkObjectId(vesselId, out _),
                cancellationToken: token);

            // Now fetch the actual refs (they should exist)
            if (!gameData.TryGetPlayerByNetworkObjectId(playerId, out var player))
                return;

            if (!gameData.TryGetVesselByNetworkObjectId(vesselId, out var vessel))
                return;

            // Reuse your existing initialization logic (recommended)
            player.InitializeForMultiplayerMode(vessel);
            vessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, vessel);
            gameData.AddPlayer(player);
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
