using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class JoustCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] int collisionsNeeded;
        public int CollisionsNeeded => collisionsNeeded;

        public void SetCollisionsNeeded(int value) => collisionsNeeded = value;

        IRoundStats ownStats;

        public override void StartMonitor()
        {
            InitializeOwnStats();

            if (ownStats != null)
            {
                ownStats.OnJoustCollisionChanged += OnJoustCollisionChanged;
                UpdateUI();
            }

            base.StartMonitor();
        }

        public override void StopMonitor()
        {
            base.StopMonitor();

            if (ownStats != null)
                ownStats.OnJoustCollisionChanged -= OnJoustCollisionChanged;
        }
        
        public override bool CheckForEndOfTurn()
        {
            InitializeOwnStats();
            return ownStats != null && ownStats.JoustCollisions >= collisionsNeeded;
        }

        void OnJoustCollisionChanged(IRoundStats stats) => UpdateUI();

        void UpdateUI()
        {
            InitializeOwnStats();
            if (ownStats == null) return;

            int remaining = Mathf.Max(0, collisionsNeeded - ownStats.JoustCollisions);
            onUpdateTurnMonitorDisplay?.Raise(remaining.ToString());
        }

        void InitializeOwnStats()
        {
            if (ownStats != null) return;

            if (!gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats))
                CSDebug.LogWarning("[JoustCollisionTurnMonitor] No round stats found for local player");
        }
    }
}