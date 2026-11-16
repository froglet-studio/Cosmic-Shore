using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Chum's end condition: the platform's networked round clock, with its length read from
    /// <see cref="EndConditionOverridesSO.GetChumRoundSeconds"/> (FrogletTools > Game Modes > End Game Conditions) at
    /// <see cref="StartMonitor"/> rather than from the scene's serialized <c>duration</c> - Glasshouse's shape.
    /// </summary>
    public class ChumTimeTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        public override void StartMonitor()
        {
            var overrides = EndConditionOverridesSO.Instance;
            SetDuration(overrides != null
                ? overrides.GetChumRoundSeconds()
                : EndConditionOverridesSO.DefaultChumRoundSeconds);
            base.StartMonitor();
        }
    }
}
