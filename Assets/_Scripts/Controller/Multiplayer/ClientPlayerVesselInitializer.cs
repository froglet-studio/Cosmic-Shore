using System.Collections.Generic;
using System.Threading;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class ClientPlayerVesselInitializer : NetworkBehaviour
    {
        [SerializeField]
        ThemeManagerDataContainerSO themeManagerData;

        [Inject]
        protected GameDataSO gameData;

        /// <summary>
        /// Timeout (in ms) for waiting on player/vessel NetworkObjects to become
        /// available on the client before giving up.
        /// </summary>
        const int InitTimeoutMs = 10_000;

        /// <summary>
        /// This is the new client, and we have to initialize all the other client's vessel and player clones in this client.
        /// </summary>
        [ClientRpc]
        internal void InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc(
            ClientRpcParams clientRpcParams = default)
        {
            InitializeAllThenSignalReady(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// Awaits every player/vessel initialization, then signals ClientReady.
        /// Replaces the previous fire-and-forget pattern that used a hardcoded
        /// 1-second delay, which could race against slow vessel spawns.
        /// </summary>
        async UniTaskVoid InitializeAllThenSignalReady(CancellationToken token)
        {
            // Collect all initialization tasks so we can await them together.
            var tasks = new List<UniTask>();
            foreach (var networkPlayer in gameData.Players)
            {
                ulong playerId = networkPlayer.PlayerNetId;
                ulong vesselId = networkPlayer.VesselNetId;
                tasks.Add(InitializePlayerAndVesselByNetIds(playerId, vesselId, token));
            }

            await UniTask.WhenAll(tasks);

            if (token.IsCancellationRequested)
                return;

            gameData.InvokeClientReady();
        }

        /// <summary>
        /// A new client joined in this client, we need to initialize the new client's owned vessel and player clone only in the existing client.
        /// </summary>
        [ClientRpc]
        internal void InitializeNewClientsOwnerPlayerAndVesselInExistingClient_ClientRpc(
            ulong newJoinedClientId,
            ClientRpcParams clientRpcParams = default)
        {

            if (!gameData.TryGetPlayerByOwnerClientId(newJoinedClientId, out var player))
            {
                CSDebug.LogError($"No player found for owner client Id: {newJoinedClientId}");
                return;
            }

            var ownerPlayerId = player.PlayerNetId;
            var ownerVesselId = player.VesselNetId;
            InitializePlayerAndVesselByNetIds(ownerPlayerId, ownerVesselId, this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTask InitializePlayerAndVesselByNetIds(
            ulong playerId,
            ulong vesselId,
            CancellationToken token)
        {
            // Create a linked CTS with a timeout so we don't hang forever
            // if a NetworkObject fails to replicate.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(InitTimeoutMs);
            var linkedToken = timeoutCts.Token;

            try
            {
                // Wait until BOTH are available (or timeout/cancel)
                await UniTask.WaitUntil(() =>
                        gameData.TryGetPlayerByNetworkObjectId(playerId, out _) &&
                        gameData.TryGetVesselByNetworkObjectId(vesselId, out _),
                    cancellationToken: linkedToken);
            }
            catch (System.OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // Timeout — not a destroy cancellation
                CSDebug.LogError($"[ClientVesselInit] Timed out waiting for player {playerId} / vessel {vesselId} to replicate.");
                return;
            }

            // Now fetch the actual refs (they should exist)
            if (!gameData.TryGetPlayerByNetworkObjectId(playerId, out var player))
                return;

            if (!gameData.TryGetVesselByNetworkObjectId(vesselId, out var vessel))
                return;

            player.InitializeForMultiplayerMode(vessel);
            vessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, vessel);
            gameData.AddPlayer(player);

            // AddPlayer teleports the vessel to its spawn position.
            // Re-snap the camera so it starts at the correct location
            // instead of the pre-teleport position.
            if (player.IsLocalUser && CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }
    }
}
