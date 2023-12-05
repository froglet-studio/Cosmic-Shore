using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class ShipCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] int Collisions;
        [SerializeField] MiniGame Game;
        [SerializeField] Transform hostileShip;

        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;

            if (!StatsManager.Instance.playerStats.ContainsKey(Game.ActivePlayer.PlayerName))
                return false;

            return StatsManager.Instance.playerStats[Game.ActivePlayer.PlayerName].skimmerShipCollisions >= Collisions;
        }

        public override void NewTurn(string playerName)
        {
            StatsManager.Instance.ResetStats();


            // TODO: perhaps coerce stats manager to create an entry for the player here
        }
        private void Update()
        {

            if (Display != null)
                Display.text = ((int)(hostileShip.position - transform.position).magnitude).ToString();
        }
    }
}