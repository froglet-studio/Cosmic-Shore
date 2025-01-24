using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.IO;

namespace CosmicShore.Void
{
    public class HapticEffect : IImpactEffect
    {
        public static void ApplyEffect(IShip ship, ImpactProperties impactProperties)
        {
            if (!ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(impactProperties.hapticType);
        }
    }
}