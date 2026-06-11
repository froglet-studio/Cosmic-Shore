using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class DogFightCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] int hitsNeeded;
        public int HitsNeeded => hitsNeeded;

        IRoundStats ownStats;

        public override void StartMonitor()
        {
            InitializeOwnStats();

            if (ownStats != null)
            {
                ownStats.OnDogFightHitChanged += OnHitChanged;
                UpdateUI();
            }

            base.StartMonitor();
        }

        public override void StopMonitor()
        {
            base.StopMonitor();

            if (ownStats != null)
                ownStats.OnDogFightHitChanged -= OnHitChanged;
        }

        public override bool CheckForEndOfTurn()
        {
            InitializeOwnStats();
            return ownStats != null && ownStats.DogFightHits >= hitsNeeded;
        }

        void OnHitChanged(IRoundStats stats) => UpdateUI();

        void UpdateUI()
        {
            InitializeOwnStats();
            if (ownStats == null) return;

            int remaining = Mathf.Max(0, hitsNeeded - ownStats.DogFightHits);
            onUpdateTurnMonitorDisplay?.Raise(remaining.ToString());
        }

        void InitializeOwnStats()
        {
            if (ownStats != null) return;

            gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats);
        }
    }
}
