using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Palisade's end condition: the platform's networked round clock, its length read from
    /// <see cref="EndConditionOverridesSO.GetPalisadeRoundSeconds"/> at <see cref="StartMonitor"/> on every peer
    /// (Tapestry's shape), so the clock the HUD counts down and the whistle the server blows cannot disagree.
    /// </summary>
    public class PalisadeTimeTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        public override void StartMonitor()
        {
            var overrides = EndConditionOverridesSO.Instance;
            SetDuration(overrides != null
                ? overrides.GetPalisadeRoundSeconds()
                : EndConditionOverridesSO.DefaultPalisadeRoundSeconds);
            base.StartMonitor();
        }
    }
}
