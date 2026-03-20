using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class VolumeDestructionTurnMonitor : TurnMonitor
    {
        [SerializeField] float volumeThreshold = 500f;
        public float VolumeThreshold => volumeThreshold;

        public void SetVolumeThreshold(float value) => volumeThreshold = value;

        IRoundStats ownStats;

        public override void StartMonitor()
        {
            InitializeOwnStats();

            if (ownStats != null)
            {
                ownStats.OnHostileVolumeDestroyedChanged += OnVolumeChanged;
                UpdateUI();
            }

            base.StartMonitor();
        }

        public override void StopMonitor()
        {
            base.StopMonitor();

            if (ownStats != null)
                ownStats.OnHostileVolumeDestroyedChanged -= OnVolumeChanged;
        }

        public override bool CheckForEndOfTurn()
        {
            InitializeOwnStats();
            return ownStats != null && ownStats.HostileVolumeDestroyed >= volumeThreshold;
        }

        void OnVolumeChanged(IRoundStats stats) => UpdateUI();

        void UpdateUI()
        {
            InitializeOwnStats();
            if (ownStats == null) return;

            int remaining = Mathf.Max(0, Mathf.CeilToInt(volumeThreshold - ownStats.HostileVolumeDestroyed));
            onUpdateTurnMonitorDisplay?.Raise(remaining.ToString());
        }

        public string GetRemainingVolumeToDestroy()
        {
            InitializeOwnStats();
            if (ownStats == null) return Mathf.CeilToInt(volumeThreshold).ToString();
            int remaining = Mathf.Max(0, Mathf.CeilToInt(volumeThreshold - ownStats.HostileVolumeDestroyed));
            return remaining.ToString();
        }

        void InitializeOwnStats()
        {
            if (ownStats != null) return;

            if (!gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats))
                CSDebug.LogWarning("[VolumeDestructionTurnMonitor] No round stats found for local player");
        }
    }
}
