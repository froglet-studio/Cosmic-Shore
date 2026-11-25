using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Longshot's end condition: the platform's networked round clock, with its length read from
    /// <see cref="EndConditionOverridesSO.GetLongshotRoundSeconds"/> (FrogletTools > Game Modes > End Game Conditions) at
    /// <see cref="StartMonitor"/> rather than from the scene's serialized <c>duration</c> - Tapestry's shape.
    /// </summary>
    public class LongshotTimeTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        public override void StartMonitor()
        {
            var overrides = EndConditionOverridesSO.Instance;
            SetDuration(overrides != null
                ? overrides.GetLongshotRoundSeconds()
                : EndConditionOverridesSO.DefaultLongshotRoundSeconds);
            base.StartMonitor();
        }
    }
}
