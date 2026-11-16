using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Windrow's end condition (the Rhino's Mass game, WINDROW.md): the platform's networked round clock, with its length read from
    /// <see cref="EndConditionOverridesSO.GetWindrowRoundSeconds"/> (FrogletTools > Game Modes >
    /// End Game Conditions) at <see cref="StartMonitor"/> rather than from the scene's serialized
    /// <c>duration</c>. The serialized value is still authored (to the same default) so a scene
    /// read without the asset shows the right number - but the asset is the authority, on every
    /// peer, so the clock the HUD counts down and the whistle the server blows cannot disagree.
    /// </summary>
    public class WindrowTimeTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        public override void StartMonitor()
        {
            var overrides = EndConditionOverridesSO.Instance;
            SetDuration(overrides != null
                ? overrides.GetWindrowRoundSeconds()
                : EndConditionOverridesSO.DefaultWindrowRoundSeconds);
            base.StartMonitor();
        }
    }
}
