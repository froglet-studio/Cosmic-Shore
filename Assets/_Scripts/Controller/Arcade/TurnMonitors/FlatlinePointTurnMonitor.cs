using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Flatline's end condition: the shared combat-point monitor, with the target read PER INTENSITY from
    /// <see cref="EndConditionOverridesSO.GetFlatlinePointTarget(int)"/> (FrogletTools > Game Modes > End Game
    /// Conditions) - never a per-scene field. Resolved on the server and replicated by the base, so a client receives
    /// the number rather than deriving it from an intensity it may not have yet (Cleave's rule).
    /// </summary>
    public class FlatlinePointTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "FlatlinePointMonitor";

        protected override int ResolvePointTarget()
        {
            int intensity = gameData != null && gameData.SelectedIntensity != null
                ? gameData.SelectedIntensity.Value : 1;
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetFlatlinePointTarget(intensity)
                : EndConditionOverridesSO.DefaultFlatlinePointTarget;
        }
    }
}
