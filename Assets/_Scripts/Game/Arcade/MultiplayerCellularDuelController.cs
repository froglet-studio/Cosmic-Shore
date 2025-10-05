using System;
using CosmicShore.App.Systems;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerCellularDuelController : MiniGameControllerBase
    {
        private int readyClientCount;
        
        protected override void Start()
        {
        }
        
        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;
            
            miniGameData.OnMiniGameTurnEnd += EndTurn;
            InitializeAfterDelay().Forget();
        }
        
        private async UniTaskVoid InitializeAfterDelay()
        {
            try
            {
                // Delay for 2 seconds (scaled time by default)
                await UniTask.Delay(1000, DelayType.UnscaledDeltaTime);
                Initialize();
            }
            catch (OperationCanceledException)
            {
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;
            
            miniGameData.OnMiniGameTurnEnd += EndTurn;
        }

        protected override void OnReadyClicked_()
        {
            DisableReadyButton();
            Debug.Log($"{NetworkManager.Singleton.LocalClientId} is ready!");
            OnReadyClicked_ServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void OnReadyClicked_ServerRpc()
        {
            readyClientCount++;
            if (!readyClientCount.Equals(miniGameData.SelectedPlayerCount))
                return;
            OnReadyClicked_ClientRpc();
        }

        [ClientRpc]
        private void OnReadyClicked_ClientRpc()
        {
            StartCountdownTimer();
        }

        protected override void OnCountdownTimerEnded()
        {
            miniGameData.SetPlayersActiveForMultiplayer(active: true);

            if (!IsServer)
                return;
            
            roundsPlayed = 0;
            turnsTakenThisRound = 0;
            miniGameData.StartNewGame();
        }

        protected override void EndGame()
        {
            readyClientCount = 0;
            miniGameData.InvokeMiniGameEnd();
            EndGame_ClientRpc();
        }

        [ClientRpc]
        void EndGame_ClientRpc()
        {
            miniGameData.SetPlayersActive(false);
        }
    }
}