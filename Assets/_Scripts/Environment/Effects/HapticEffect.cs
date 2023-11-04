using _Scripts._Core.Input;

namespace StarWriter.Core
{
    public class HapticEffect : IImpactEffect
    {
        public static void ApplyEffect(Ship ship, ImpactProperties impactProperties)
        {
            if (!ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(impactProperties.hapticType);
        }
    }
}