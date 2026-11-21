using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tapestry's end condition: the platform's networked round clock, with its length read from
    /// <see cref="EndConditionOverridesSO.GetTapestryRoundSeconds"/> (FrogletTools > Game Modes >
    /// End Game Conditions) at <see cref="StartMonitor"/> rather than from the scene's serialized
    /// <c>duration</c>. The serialized value is still authored (to the same default) so a scene
    /// read without the asset shows the right number - but the asset is the authority, on every
    /// peer, so the clock the HUD counts down and the whistle the server blows cannot disagree.
    /// </summary>
    public class TapestryTimeTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        public override void StartMonitor()
        {
            var overrides = EndConditionOverridesSO.Instance;
            SetDuration(overrides != null
                ? overrides.GetTapestryRoundSeconds()
                : EndConditionOverridesSO.DefaultTapestryRoundSeconds);
            base.StartMonitor();
        }
    }
}
