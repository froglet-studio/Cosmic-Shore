using CosmicShore.SOAP;
using Unity.Netcode;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Serialization;

namespace CosmicShore.Game.UI
{
    public class NetworkVolumeUIController : NetworkBehaviour
    {
        [FormerlySerializedAs("miniGameData")] [SerializeField] GameDataSO gameData;
        [SerializeField] VolumeUI volumeUI;

        private bool _active;
        private bool _initialized;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                gameData.OnMiniGameTurnStarted.OnRaised += MiniGameTurnStartServer;
                gameData.OnMiniGameTurnEnd.OnRaised += GameTurnEndServer;
            }

            if (IsClient)
            {
                // Late joiners check if the game is already running
                TrySyncLateJoinStateAsync().Forget();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                gameData.OnMiniGameTurnStarted.OnRaised -= MiniGameTurnStartServer;
                gameData.OnMiniGameTurnEnd.OnRaised -= GameTurnEndServer;
            }
        }

        private async UniTaskVoid TrySyncLateJoinStateAsync()
        {
            // Wait a small moment to ensure MiniGameData is initialized
            await UniTask.Delay(500, DelayType.UnscaledDeltaTime);

            if (gameData.IsTurnRunning)
            {
                // Request the current state from server
                RequestSyncFromServer_ServerRpc();
            }
        }

        #region --- SERVER HANDLERS ---

        private void MiniGameTurnStartServer()
        {
            _active = true;
            SendActiveState_ClientRpc(true, gameData.GetTeamVolumes());
        }

        private void GameTurnEndServer()
        {
            _active = false;
            SendActiveState_ClientRpc(false, Vector4.zero);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSyncFromServer_ServerRpc(ServerRpcParams rpcParams = default)
        {
            var senderId = rpcParams.Receive.SenderClientId;
            SendActiveState_SingleClientRpc(senderId, _active, gameData.GetTeamVolumes());
        }

        #endregion

        #region --- CLIENT RPCS ---

        [ClientRpc]
        private void SendActiveState_ClientRpc(bool state, Vector4 teamVolumes)
        {
            _active = state;
            if (_active)
            {
                volumeUI.UpdateVolumes(teamVolumes);
                MonitorVolumesAsync().Forget();
            }
        }

        [ClientRpc]
        private void SendActiveState_SingleClientRpc(ulong targetClientId, bool state, Vector4 teamVolumes, ClientRpcParams clientRpcParams = default)
        {
            if (NetworkManager.Singleton.LocalClientId != targetClientId) return;
            _active = state;
            if (_active)
            {
                volumeUI.UpdateVolumes(teamVolumes);
                MonitorVolumesAsync().Forget();
            }
        }

        #endregion

        private async UniTaskVoid MonitorVolumesAsync()
        {
            if (_initialized) return;
            _initialized = true;

            while (_active && this != null)
            {
                if (gameData != null && volumeUI != null)
                {
                    var teamVolumes = gameData.GetTeamVolumes();
                    volumeUI.UpdateVolumes(teamVolumes);
                }

                await UniTask.Delay(250, DelayType.UnscaledDeltaTime);
            }

            _initialized = false;
        }
    }
}
