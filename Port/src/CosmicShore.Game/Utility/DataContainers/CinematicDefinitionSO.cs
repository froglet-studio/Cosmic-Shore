// PORT Deviation (V18, extend when CinematicDefinitionSO ports): the original
// Utility/DataContainers/CinematicDefinitionSO.cs also defines CinematicCameraType,
// CinematicCameraSetup, VictoryLapSettings, ToastAnimationSettings,
// ScoreRevealAnimationSettings, CinematicText, and the CinematicDefinitionSO asset
// class (DOTween Ease + CreateAssetMenu + Random-bound — end-game cinematic arc).
// Only the AICinematicBehaviorType enum, required by AICinematicBehavior, is ported
// here; the enum block itself is verbatim.
namespace CosmicShore.Utility
{
    public enum AICinematicBehaviorType
    {
        MoveForward,    // Simple forward flight (most common)
        Loop,           // Perform loop maneuver
        Drift,          // Drift while moving
        Spiral,         // Spiral upward
        BarrelRoll,     // Barrel roll (future)
        FlyBy,          // Victory fly-by (future)
        HoverSpin       // Hover and spin (future)
    }
}
