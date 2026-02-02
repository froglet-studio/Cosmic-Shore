using System.Linq;
using Unity.Collections;
using Unity.Netcode;

namespace CosmicShore.Game.Arcade
{
    public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
    {
        public override bool CheckForEndOfTurn() =>
            gameData.RoundStatsList.Any(stats => stats.OmniCrystalsCollected >= CrystalCollisions);
    }
}