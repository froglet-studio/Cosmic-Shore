using System.Collections.Generic;
using CosmicShore.Game.Analytics;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerHexRaceScoreTracker : HexRaceScoreTracker, IStatExposable
    {
        [Header("Multiplayer Specific")]
        [SerializeField] ScriptableEventString OnJoustCollisionEvent;
        [SerializeField] private MultiplayerHexRaceController controller;
        
        private int _joustsWonSession;
        public int JoustsWonSession => _joustsWonSession;

        protected override void Start()
        {
            base.Start();
            if (OnJoustCollisionEvent) OnJoustCollisionEvent.OnRaised += HandleJoustEvent;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (OnJoustCollisionEvent) OnJoustCollisionEvent.OnRaised -= HandleJoustEvent;
        }

        void HandleJoustEvent(string winner)
        {
            // Use IPlayer.Name consistently
            if (gameData.LocalPlayer?.Name == winner) _joustsWonSession++;
        }

        protected override bool DetermineIfWinner(int localCrystalsRemaining)
        {
            // This is called the moment the local player collects their last crystal.
            // If they have 0 remaining, they finished — they are the winner.
            // The server will confirm and sync authoritatively, but locally this is correct.
            return localCrystalsRemaining <= 0;
        }

        protected override void ReportToMultiplayerController(float finalScore, bool isWinner)
        {
            if (isWinner)
            {
                // Only the winner reports — server assigns loser scores internally in the ServerRpc
                if (UGSStatsManager.Instance)
                {
                    UGSStatsManager.Instance.ReportMultiplayerHexStats(
                        GameModes.MultiplayerHexRaceGame, 
                        gameData.SelectedIntensity.Value,
                        MaxCleanStreak, 
                        MaxDriftTimeRecord, 
                        _joustsWonSession, 
                        finalScore
                    );
                }

                if (controller) controller.ReportLocalPlayerFinished(finalScore);
            }
            // Losers do NOT call ReportLocalPlayerFinished — the server handles their score
            // assignment inside ReportPlayerFinished_ServerRpc when the winner reports.
        }
        
        public Dictionary<string, object> GetExposedStats()
        {
            return new Dictionary<string, object>
            {
                { "Max Clean Streak", MaxCleanStreak },
                { "Longest Drift", MaxDriftTimeRecord },
                { "Max Boost Time", MaxHighBoostTimeRecord },
                { "Jousts Won", JoustsWonSession }
            };
        }
    }
}