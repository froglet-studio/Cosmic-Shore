using CosmicShore.Core;
using CosmicShore.Game.IO;

namespace CosmicShore.Void
{
    public class HapticEffect : IImpactEffect
    {
        public static void ApplyEffect(Ship ship, ImpactProperties impactProperties)
        {
            if (!ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(impactProperties.hapticType);
        }
    }
}