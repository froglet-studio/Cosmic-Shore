using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Reefbuild's end condition: the platform's networked round clock, with its length read from
    /// <see cref="EndConditionOverridesSO.GetReefbuildRoundSeconds"/> (FrogletTools > Game Modes > End Game Conditions)
    /// at <see cref="StartMonitor"/> rather than from the scene's serialized <c>duration</c> (Tapestry's shape: the
    /// serialized value is authored to the same default, but the asset is the authority on every peer).
    /// </summary>
    public class ReefbuildTimeTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        public override void StartMonitor()
        {
            var overrides = EndConditionOverridesSO.Instance;
            SetDuration(overrides != null
                ? overrides.GetReefbuildRoundSeconds()
                : EndConditionOverridesSO.DefaultReefbuildRoundSeconds);
            base.StartMonitor();
        }
    }
}
