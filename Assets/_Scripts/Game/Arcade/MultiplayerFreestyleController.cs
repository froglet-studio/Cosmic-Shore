using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerFreestyleController : MultipalyerMiniGameControllerBase
    {
        public void OnClickReturnToMainMenu()
        {
            OnClickReturnToMainMenuAsync().Forget();
        }
        
        async UniTaskVoid OnClickReturnToMainMenuAsync()
        {
            RemovePlayer_ServerRpc(miniGameData.ActivePlayer.Name);
            await UniTask.Delay(1000, DelayType.UnscaledDeltaTime, PlayerLoopTiming.LastPostLateUpdate ,this.GetCancellationTokenOnDestroy());
            MultiplayerSetup.Instance.LeaveSession().Forget();
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
            if (string.IsNullOrEmpty(playerName) || miniGameData == null)
                return;

            bool removed = miniGameData.RemovePlayerData(playerName);
            if (removed)
            {
                Debug.Log($"[Freestyle][Client] Removed player '{playerName}'.");
            }
        }
    }
}
