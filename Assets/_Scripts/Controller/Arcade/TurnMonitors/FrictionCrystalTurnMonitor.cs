// FrictionCrystalTurnMonitor.cs
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Friction's crystal target scales explicitly per intensity level (10/20/30/50 per the
    /// design doc) rather than being derived from track waypoints like HexRace. Sets the
    /// inherited <c>CrystalCollisions</c> override before delegating to the network-synced base.
    /// </summary>
    public class FrictionCrystalTurnMonitor : NetworkCrystalCollisionTurnMonitor
    {
        [SerializeField]
        int[] crystalTargetByIntensity = { 10, 20, 30, 50 };

        public override void StartMonitor()
        {
            int intensity = Mathf.Clamp(gameData.SelectedIntensity.Value, 1, crystalTargetByIntensity.Length);
            CrystalCollisions = crystalTargetByIntensity[intensity - 1];

            base.StartMonitor();
        }
    }
}
