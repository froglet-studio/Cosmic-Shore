using CosmicShore.Core;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class CrystalCollisionTurnMonitor : TurnMonitor
    {
        [SerializeField] int CrystalCollisions;
        [SerializeField] bool hostileCollection;

        public override bool CheckForEndOfTurn()
        {
            if (!gameData.TryGetActivePlayerStats(out IPlayer _, out IRoundStats roundStats))
                return false;

            return roundStats.OmniCrystalsCollected >= CrystalCollisions;
        }

        /*public override void StartMonitor()
        {
            // TODO: perhaps coerce stats manager to create an entry for the player here
        }*/

        protected override void RestrictedUpdate() { }
    }
}