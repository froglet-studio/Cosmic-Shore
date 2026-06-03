using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class BrittleStarVesselTelemetry : VesselTelemetry
    {
        [Header("Stat Events — BrittleStar")]
        [SerializeField] private VesselStatEventSO volleysFiredStat;
        [SerializeField] private VesselStatEventSO wallsSeededStat;

        public int VolleysFired { get; private set; }
        public int WallsSeeded  { get; private set; }

        protected override void RegisterStatsExtended()
        {
            RegisterStat(volleysFiredStat);
            RegisterStat(wallsSeededStat);
        }

        protected override void OnTurnStartedExtended()
        {
            FullAutoActionExecutor.OnVolleyFired += HandleVolleyFired;
        }

        protected override void OnTurnEndedExtended()
        {
            FullAutoActionExecutor.OnVolleyFired -= HandleVolleyFired;
        }

        protected override void ResetExtended()
        {
            VolleysFired = 0;
            WallsSeeded = 0;

            volleysFiredStat?.Reset();
            wallsSeededStat?.Reset();
        }

        private void HandleVolleyFired(string playerName)
        {
            if (!IsTracking || Vessel?.PlayerName != playerName) return;
            VolleysFired++;
            volleysFiredStat?.Raise(VolleysFired);
        }
    }
}
