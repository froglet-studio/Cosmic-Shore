using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class ShipCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] int Collisions;
        
        // Use MiniGameData for player info.
        // [SerializeField] MiniGame Game;
        // [SerializeField] Player hostileShip;

        public override bool CheckForEndOfTurn()
        {
            /*if (!StatsManager.Instance.PlayerStats.ContainsKey(Game.LocalPlayer.PlayerName))
                return false;

            return StatsManager.Instance.PlayerStats[Game.LocalPlayer.PlayerName].SkimmerShipCollisions >= Collisions;*/

            return true;        // TEMP
        }

        public override void StartMonitor()
        {
            // StatsManager.Instance.ResetStats();
            // TODO: perhaps coerce stats manager to create an entry for the player here
            UpdateUI();
        }

        protected override void RestrictedUpdate()
        {
            UpdateUI();
        }

        void UpdateUI()
        {
            var message = ""; //TEMP - ((int)((hostileShip.Vessel.Transform.position - Game.LocalPlayer.Vessel.Transform.position).magnitude/10f)).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}