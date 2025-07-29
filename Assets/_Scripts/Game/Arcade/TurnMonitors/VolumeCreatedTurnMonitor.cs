using UnityEngine;
using System.Collections;
using CosmicShore.Core;

namespace CosmicShore.Game.Arcade
{
    public class VolumeCreatedTurnMonitor : TurnMonitor
    {
        [SerializeField] float Amount;
        [SerializeField] MiniGame Game;
        [SerializeField] bool hostileVolume;
        Core.IRoundStats volumeStat;
        Teams team;
        float volumeUnitConverstion = 145.65f;

        private void Start()
        {
            StartCoroutine(WaitForTeam());
        }

        private IEnumerator WaitForTeam()
        {
            team = hostileVolume ? Teams.Ruby : Teams.Jade;

            while (!StatsManager.Instance.TeamStats.ContainsKey(team))
            {
                yield return null; // Wait for the next frame
            }

            volumeStat = StatsManager.Instance.TeamStats[team];
            // Any additional logic once the red team is available
        }

        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;

            return StatsManager.Instance.TeamStats[team].VolumeCreated > Amount;
        }

        public override void NewTurn(string playerName)
        {
            StatsManager.Instance.ResetStats();
        }

        protected override void RestrictedUpdate()
        {
            if (StatsManager.Instance.TeamStats.ContainsKey(team))
            {
                string message = ((int)((Amount - StatsManager.Instance.TeamStats[team].VolumeCreated) / volumeUnitConverstion)).ToString();
                onUpdateTurnMonitorDisplay.Raise(message);
            }
        }
    }
}
