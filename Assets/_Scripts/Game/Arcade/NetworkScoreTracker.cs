using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class NetworkScoreTracker : ScoreTracker
    {
        protected override void OnEnable()
        {
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;
            
            miniGameData.OnMiniGameInitialized += InitializeScoringMode;
            miniGameData.OnMiniGameEnd += CalculateWinnerOnServer;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;
            
            miniGameData.OnMiniGameInitialized -= InitializeScoringMode;
            miniGameData.OnMiniGameEnd -= CalculateWinnerOnServer;
        }

        protected override void OnDisable()
        {
        }
        
        private void CalculateWinnerOnServer()
        {
            CalculateScores();
            SendRoundStats_ClientRpc();
        }
        
        [ClientRpc]
        private void SendRoundStats_ClientRpc()
        {
            SortAndInvokeResults();
        }
    }
}