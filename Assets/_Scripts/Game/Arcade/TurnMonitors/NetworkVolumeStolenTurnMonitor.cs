using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Networked version of VolumeStolenTurnMonitor.
    /// Server tracks all players' stolen volume and syncs to the controller.
    /// Turn ends when ANY player reaches the volume threshold (server-authoritative).
    /// </summary>
    public class NetworkVolumeStolenTurnMonitor : VolumeStolenTurnMonitor
    {
        [SerializeField] MultiplayerAcornHoardController controller;

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (!IsServer) return;

            foreach (var stat in gameData.RoundStatsList)
                stat.OnVolumeStolenChanged += ServerSideVolumeSync;
        }

        public override void StopMonitor()
        {
            if (IsServer)
            {
                foreach (var stat in gameData.RoundStatsList)
                    stat.OnVolumeStolenChanged -= ServerSideVolumeSync;
            }

            base.StopMonitor();
        }

        void ServerSideVolumeSync(IRoundStats stats)
        {
            if (!IsServer) return;
            controller?.NotifyVolumeStolen(stats.Name, stats.VolumeStolen);
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;
            return gameData.RoundStatsList.Any(s => s.VolumeStolen >= VolumeThreshold);
        }

        protected override void UpdateUI()
        {
            float target = VolumeThreshold;
            float current = ownStats?.VolumeStolen ?? 0f;
            float remaining = Mathf.Max(0, target - current);

            onUpdateTurnMonitorDisplay?.Raise(((int)remaining).ToString());
        }
    }
}
