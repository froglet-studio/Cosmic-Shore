using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Ice Shelf's end condition: the platform's networked round clock, with its length read from
    /// <see cref="EndConditionOverridesSO.GetIceShelfRoundSeconds"/> (FrogletTools > Game Modes > End Game Conditions) at
    /// <see cref="StartMonitor"/> rather than from the scene's serialized <c>duration</c> - Glasshouse's and Tapestry's
    /// shape. The serialized value is still authored (to the same default) so a scene read without the asset shows the
    /// right number, but the asset is the authority on every peer.
    /// </summary>
    public class IceShelfTimeTurnMonitor : NetworkTimeBasedTurnMonitor
    {
        public override void StartMonitor()
        {
            var overrides = EndConditionOverridesSO.Instance;
            SetDuration(overrides != null
                ? overrides.GetIceShelfRoundSeconds()
                : EndConditionOverridesSO.DefaultIceShelfRoundSeconds);
            base.StartMonitor();
        }
    }
}
