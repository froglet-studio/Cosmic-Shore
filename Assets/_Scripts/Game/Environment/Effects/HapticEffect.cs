using CosmicShore._Core.Input;

namespace CosmicShore.Core
{
    public class HapticEffect : IImpactEffect
    {
        public static void ApplyEffect(Ship ship, ImpactProperties impactProperties)
        {
            if (!ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(impactProperties.hapticType);
        }
    }
}