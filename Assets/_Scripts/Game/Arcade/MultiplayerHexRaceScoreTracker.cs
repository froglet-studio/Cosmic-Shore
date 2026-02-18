using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using Obvious.Soap;
using UnityEngine;
using System.Linq;

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
            if (gameData.LocalPlayer?.Vessel.VesselStatus.PlayerName == winner) _joustsWonSession++;
        }

        protected override bool DetermineIfWinner(int localCrystalsRemaining)
        {
            var localPlayerName = gameData.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;
            var allPlayers = gameData.RoundStatsList;
            
            if (allPlayers == null || allPlayers.Count == 0)
                return false;

            var minCrystalsRemaining = allPlayers
                .Select(p => p.Score >= 10000f ? (int)(p.Score - 10000f) : 0)
                .Min();
            
            bool isWinner = localCrystalsRemaining == minCrystalsRemaining;
            if (minCrystalsRemaining == 0)
            {
                var finishedPlayers = allPlayers.Where(p => p.Score < 10000f).OrderBy(p => p.Score).ToList();
                isWinner = finishedPlayers.Count > 0 && finishedPlayers[0].Name == localPlayerName;
            }

            return isWinner;
        }

        protected override void ReportToMultiplayerController(float finalScore, bool isWinner)
        {
            if (UGSStatsManager.Instance && isWinner)
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