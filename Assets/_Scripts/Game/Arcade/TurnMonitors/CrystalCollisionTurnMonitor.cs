using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.Game.Arcade
{
    public class CrystalCollisionTurnMonitor : TurnMonitor
    {
        [Header("Settings")]
        [SerializeField] protected int CrystalCollisions;
        [SerializeField] protected SpawnableWaypointTrack optionalEnvironment;
        [SerializeField] protected int optionalLaps = 4;

        protected IRoundStats ownStats;

        public override void StartMonitor()
        {
            InitializeTargetCount();
            InitializeStatsListener();
            UpdateCrystalsRemainingUI(); // Initial update
            base.StartMonitor();
        }

        public override void StopMonitor()
        {
            base.StopMonitor();
            if (ownStats != null)
                ownStats.OnCrystalsCollectedChanged -= UpdateCrystals;
        }

        public override bool CheckForEndOfTurn() =>
            ownStats.CrystalsCollected >= CrystalCollisions;

        protected virtual void InitializeTargetCount()
        {
            if (optionalEnvironment)
            {
                CrystalCollisions = optionalEnvironment.waypoints[optionalEnvironment.intenstyLevel - 1].positions.Count * optionalLaps;
            }
        }

        protected virtual void InitializeStatsListener()
        {
            if (ownStats == null && !gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats))
            {
                Debug.LogError("No round stats found for local player");
                return;
            }

            ownStats.OnCrystalsCollectedChanged += UpdateCrystals;
        }

        protected virtual void UpdateCrystals(IRoundStats stats) => UpdateCrystalsRemainingUI();

        protected virtual void UpdateCrystalsRemainingUI()
        {
            string message = GetRemainingCrystalsCountToCollect();
            onUpdateTurnMonitorDisplay?.Raise(message);
        }
        
        protected virtual string GetRemainingCrystalsCountToCollect()
        {
            return ownStats == null ? "-" : Mathf.Max(0, CrystalCollisions - ownStats.CrystalsCollected).ToString();
        }
    }
}