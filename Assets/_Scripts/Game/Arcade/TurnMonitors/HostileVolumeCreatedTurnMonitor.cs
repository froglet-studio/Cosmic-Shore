using UnityEngine;
using TMPro;

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
            volumeStat = StatsManager.Instance.teamStats[Teams.Red];
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
            if (display != null)
                display.text = ((int)Amount - (int)StatsManager.Instance.teamStats[Teams.Red].volumeCreated).ToString();
        }
    }
}