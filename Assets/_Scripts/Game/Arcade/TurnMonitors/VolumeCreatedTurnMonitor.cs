using UnityEngine;
using System.Collections;

namespace CosmicShore.Game.Arcade
{
    public class VolumeCreatedTurnMonitor
        : TurnMonitor
    {
        [SerializeField] float Amount;
        [SerializeField] MiniGame Game;
        [SerializeField] bool hostileVolume;
        Core.RoundStats volumeStat;
        Teams team;

        private void Start()
        {
            StartCoroutine(WaitForTeam());
        }

        private IEnumerator WaitForTeam()
        {
            team = hostileVolume ? Teams.Red : Teams.Green;

            while (!StatsManager.Instance.teamStats.ContainsKey(team))
            {
                yield return null; // Wait for the next frame
            }

            volumeStat = StatsManager.Instance.teamStats[team];
            // Any additional logic once the red team is available
        }

        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;

            return StatsManager.Instance.teamStats[team].volumeCreated > Amount;
        }

        public override void NewTurn(string playerName)
        {
            StatsManager.Instance.ResetStats();
        }

        private void Update()
        {
            if (Display != null && StatsManager.Instance.teamStats.ContainsKey(team))
                Display.text = ((int)((Amount - StatsManager.Instance.teamStats[team].volumeCreated) / 145.65)).ToString();
        }
    }
}
