using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class MultiplayerFreestyleController : MultiplayerMiniGameControllerBase
    {
        public void OnClickReturnToMainMenu()
        {
            OnClickReturnToMainMenuAsync().Forget();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.OnClientReady.OnRaised += OnClientReady;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            gameData.OnClientReady.OnRaised -= OnClientReady;
        }
        
        void OnClientReady() => gameData.SetNonOwnerPlayersActiveInNewClient();
        
        async UniTaskVoid OnClickReturnToMainMenuAsync()
        {
            RemovePlayer_ServerRpc(gameData.LocalPlayer.Name);
            await UniTask.Delay(1000, DelayType.UnscaledDeltaTime, PlayerLoopTiming.LastPostLateUpdate ,this.GetCancellationTokenOnDestroy());
            multiplayerSetup.LeaveSession().Forget();
        }
        
        protected override void OnCountdownTimerEnded()
        {
            OnCountdownTimerEnded_ServerRpc(gameData.LocalPlayer.Name);
        }

        [ServerRpc(RequireOwnership = false)]
        void OnCountdownTimerEnded_ServerRpc(FixedString128Bytes playerName)
        {
            OnCountdownTimerEnded_ClientRpc(playerName);
        }

        [ClientRpc]
        void OnCountdownTimerEnded_ClientRpc(FixedString128Bytes playerName)
        {
            string name = playerName.ToString();
            gameData.SetNewPlayerActive(name);
            gameData.StartTurn();

            // If it's the local player who just activated, force their input live
            // so a replay-race leaves them controllable.
            if (gameData.LocalPlayer != null && gameData.LocalPlayer.Name == name)
                EnsureLocalHumanCanMove();
        }

        [ServerRpc(RequireOwnership = false)]
        void RemovePlayer_ServerRpc(string playerName)
        {
            RemovePlayer_ClientRpc(playerName);
        }
            

        /// <summary>
        /// All clients remove the same player locally to stay in sync with the server.
        /// </summary>
        [ClientRpc]
        void RemovePlayer_ClientRpc(string playerName)
        {
            if (string.IsNullOrEmpty(playerName) || gameData == null)
                return;

            bool removed = gameData.RemovePlayerData(playerName);
            if (removed)
            {
                CSDebug.Log($"[Freestyle][Client] Removed player '{playerName}'.");
            }
        }
    }
}
