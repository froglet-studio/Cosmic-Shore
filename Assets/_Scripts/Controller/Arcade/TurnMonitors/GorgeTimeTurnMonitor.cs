using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Gorge's end condition: the platform's networked round clock, with its length read from
    /// <see cref="EndConditionOverridesSO.GetGorgeRoundSeconds"/> (FrogletTools > Game Modes > End Game Conditions) at
    /// <see cref="StartMonitor"/> rather than from the scene's serialized <c>duration</c> - Chum's shape.
    /// </summary>
    public class GorgeTimeTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        public override void StartMonitor()
        {
            var overrides = EndConditionOverridesSO.Instance;
            SetDuration(overrides != null
                ? overrides.GetGorgeRoundSeconds()
                : EndConditionOverridesSO.DefaultGorgeRoundSeconds);
            base.StartMonitor();
        }
    }
}
