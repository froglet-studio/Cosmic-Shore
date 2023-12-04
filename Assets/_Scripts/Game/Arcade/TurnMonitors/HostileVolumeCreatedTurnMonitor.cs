using UnityEngine;
using TMPro;
using System.Collections;

namespace CosmicShore.Game.Arcade
{
    public class HostileVolumeCreatedTurnMonitor : TurnMonitor
    {
        [SerializeField] float Amount;
        [SerializeField] MiniGame Game;
        [HideInInspector] public TMP_Text display;
        Core.RoundStats volumeStat;

        private void Start()
        {
            StartCoroutine(WaitForRedTeam());
        }

        private IEnumerator WaitForRedTeam()
        {
            while (!StatsManager.Instance.teamStats.ContainsKey(Teams.Red))
            {
                yield return null; // Wait for the next frame
            }

            volumeStat = StatsManager.Instance.teamStats[Teams.Red];
            // Any additional logic once the red team is available
        }

        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;

            return StatsManager.Instance.teamStats[Teams.Red].volumeCreated > Amount;
        }

        public override void NewTurn(string playerName)
        {
            StatsManager.Instance.ResetStats();
        }

        private void Update()
        {
            if (display != null && StatsManager.Instance.teamStats.ContainsKey(Teams.Red))
                display.text = ((int)((Amount - StatsManager.Instance.teamStats[Teams.Red].volumeCreated) / 145.65)).ToString();
        }
    }
}
