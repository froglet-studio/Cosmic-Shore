using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.IO;

namespace CosmicShore.Void
{
    public class HapticEffect : IImpactEffect
    {
        public static void ApplyEffect(bool autoPilotEnabled, ImpactProperties impactProperties)
        {
            if (!autoPilotEnabled) HapticController.PlayHaptic(impactProperties.hapticType);
        }
    }
}