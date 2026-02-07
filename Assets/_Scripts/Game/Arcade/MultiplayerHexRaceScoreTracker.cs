using CosmicShore.Game.Analytics;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerHexRaceScoreTracker : HexRaceScoreTracker
    {
        [Header("Multiplayer Specific")]
        [SerializeField] ScriptableEventString OnJoustCollisionEvent; 
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

        protected override void ReportToMultiplayerController(float finalScore)
        {
            if (UGSStatsManager.Instance)
            {
                UGSStatsManager.Instance.ReportMultiplayerHexStats(
                    MaxCleanStreak, MaxDriftTimeRecord, MaxHighBoostTimeRecord, 
                    _joustsWonSession, finalScore
                );
            }

            var controller = FindObjectOfType<MultiplayerHexRaceController>();
            if (!controller) return;

            controller.ReportLocalPlayerFinished(finalScore);

        }
    }
}