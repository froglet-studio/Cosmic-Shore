using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hedgerow's end condition: the platform's networked round clock, with its length read from
    /// <see cref="EndConditionOverridesSO.GetHedgerowRoundSeconds"/> (FrogletTools > Game Modes > End Game Conditions) at
    /// <see cref="StartMonitor"/> rather than from the scene's serialized <c>duration</c> - Tapestry's shape.
    /// </summary>
    public class HedgerowTimeTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        public override void StartMonitor()
        {
            var overrides = EndConditionOverridesSO.Instance;
            SetDuration(overrides != null
                ? overrides.GetHedgerowRoundSeconds()
                : EndConditionOverridesSO.DefaultHedgerowRoundSeconds);
            base.StartMonitor();
        }
    }
}
