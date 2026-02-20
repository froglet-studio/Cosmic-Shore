using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
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
            gameData.SetNewPlayerActive(playerName.ToString());
            gameData.StartTurn(); 
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
                Debug.Log($"[Freestyle][Client] Removed player '{playerName}'.");
            }
        }
    }
}
