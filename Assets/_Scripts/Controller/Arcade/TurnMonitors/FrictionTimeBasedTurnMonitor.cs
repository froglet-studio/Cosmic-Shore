// FrictionTimeBasedTurnMonitor.cs
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Friction's time limit scales per intensity level (120/150/180/210s). The table is the
    /// mode's AUTO-CALC - the answer when FrogletTools > Game Modes > End Game Conditions
    /// leaves Friction's run seconds at 0 - and a number authored there wins outright, for
    /// every intensity, the same way Drumfire's clock is authored. Resolved through
    /// <see cref="TimeBasedTurnMonitor.SetDuration"/> before delegating to the network-synced
    /// base, which replicates the countdown to every client through its ClientRpc.
    ///
    /// In the Friction scene this monitor publishes on the CLOCK channel
    /// (Resources/Channels/TurnClockChannel), not the objective channel the crystal monitor
    /// owns - so the goal stack draws the crystal count as the primary row and this clock as
    /// the secondary row under it, rather than the two monitors fighting over one string.
    /// </summary>
    public class FrictionTimeBasedTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        [Tooltip("Run length in seconds per intensity (index 0 = intensity 1). Read only while " +
                 "the End Game Conditions tool has Friction's run seconds at 0 (auto).")]
        [SerializeField]
        float[] timeLimitByIntensity = { 120f, 150f, 180f, 210f };

        public override void StartMonitor()
        {
            int intensity = Mathf.Clamp(gameData.SelectedIntensity.Value, 1, timeLimitByIntensity.Length);
            int autoSeconds = Mathf.RoundToInt(timeLimitByIntensity[intensity - 1]);

            var overrides = EndConditionOverridesSO.Instance;
            int seconds = overrides != null ? overrides.GetFrictionSeconds(autoSeconds) : autoSeconds;
            SetDuration(seconds);

            base.StartMonitor();
        }
    }
}
