using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class CrystalCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] int CrystalCollisions;
        [SerializeField] MiniGame Game;
        [SerializeField] bool hostileCollection;

        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;

            if (!StatsManager.Instance.PlayerStats.ContainsKey(Game.ActivePlayer.PlayerName))
                return false;

            return StatsManager.Instance.PlayerStats[Game.ActivePlayer.PlayerName].OmniCrystalsCollected >= CrystalCollisions;
        }

        public override void NewTurn(string playerName)
        {
            StatsManager.Instance.ResetStats();

            // TODO: perhaps coerce stats manager to create an entry for the player here
        }
    }
}