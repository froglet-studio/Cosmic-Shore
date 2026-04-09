using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Ends the turn when the local player has stolen enough prism volume.
    /// Used in Acorn Hoard mode where the Squirrel steals prisms from neutral/enemy trails.
    /// </summary>
    public class VolumeStolenTurnMonitor : TurnMonitor
    {
        [SerializeField] float volumeThreshold = 5000f;
        protected IRoundStats ownStats;

        public float VolumeThreshold => volumeThreshold;

        public override void StartMonitor()
        {
            InitializeOwnStats();

            if (ownStats != null)
                ownStats.OnVolumeStolenChanged += OnVolumeStolenChanged;

            UpdateUI();
            base.StartMonitor();
        }

        public override void StopMonitor()
        {
            base.StopMonitor();

            if (ownStats != null)
                ownStats.OnVolumeStolenChanged -= OnVolumeStolenChanged;
        }

        public override bool CheckForEndOfTurn()
        {
            InitializeOwnStats();
            return ownStats != null && ownStats.VolumeStolen >= volumeThreshold;
        }

        void OnVolumeStolenChanged(IRoundStats stats) => UpdateUI();

        protected virtual void UpdateUI()
        {
            InitializeOwnStats();
            if (ownStats == null) return;

            float remaining = Mathf.Max(0, volumeThreshold - ownStats.VolumeStolen);
            string message = ((int)remaining).ToString();
            onUpdateTurnMonitorDisplay?.Raise(message);
        }

        protected virtual void InitializeOwnStats()
        {
            if (ownStats != null) return;

            if (!gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats))
                Debug.LogWarning("[VolumeStolenTurnMonitor] No round stats found for local player");
        }

        protected override void ResetState()
        {
            ownStats = null;
        }
    }
}
