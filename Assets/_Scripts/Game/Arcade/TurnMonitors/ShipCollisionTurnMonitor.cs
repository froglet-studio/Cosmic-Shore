using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class ShipCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] int Collisions;
        
        // Use MiniGameData for player info.
        // [SerializeField] MiniGame Game;
        // [SerializeField] R_Player hostileShip;

        public override bool CheckForEndOfTurn()
        {
            /*if (!StatsManager.Instance.PlayerStats.ContainsKey(Game.ActivePlayer.PlayerName))
                return false;

            return StatsManager.Instance.PlayerStats[Game.ActivePlayer.PlayerName].SkimmerShipCollisions >= Collisions;*/

            return true;        // TEMP
        }

        protected override void StartTurn()
        {
            // StatsManager.Instance.ResetStats();
            // TODO: perhaps coerce stats manager to create an entry for the player here
        }

        protected override void RestrictedUpdate()
        {
            var message = ""; //TEMP - ((int)((hostileShip.Ship.Transform.position - Game.ActivePlayer.Ship.Transform.position).magnitude/10f)).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}