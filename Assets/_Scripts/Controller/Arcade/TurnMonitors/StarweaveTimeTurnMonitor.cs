using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Starweave's end condition: the platform's networked round clock, with its length read from
    /// <see cref="EndConditionOverridesSO.GetStarweaveRoundSeconds"/> (FrogletTools > Game Modes > End Game Conditions) at
    /// <see cref="StartMonitor"/> rather than from the scene's serialized <c>duration</c> - Tapestry's shape.
    /// </summary>
    public class StarweaveTimeTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        public override void StartMonitor()
        {
            var overrides = EndConditionOverridesSO.Instance;
            SetDuration(overrides != null
                ? overrides.GetStarweaveRoundSeconds()
                : EndConditionOverridesSO.DefaultStarweaveRoundSeconds);
            base.StartMonitor();
        }
    }
}
