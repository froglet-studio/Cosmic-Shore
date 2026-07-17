// FrictionTimeBasedTurnMonitor.cs
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Friction's time limit scales per intensity level. Sets the inherited
    /// (now-protected) <c>duration</c> before delegating to the network-synced base.
    /// </summary>
    public class FrictionTimeBasedTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        [SerializeField]
        float[] timeLimitByIntensity = { 120f, 150f, 180f, 210f };

        public override void StartMonitor()
        {
            int intensity = Mathf.Clamp(gameData.SelectedIntensity.Value, 1, timeLimitByIntensity.Length);
            duration = timeLimitByIntensity[intensity - 1];

            base.StartMonitor();
        }
    }
}
