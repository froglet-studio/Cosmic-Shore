namespace StarWriter.Core
{
    public class HapticEffect : IImpactEffect
    {
        public static void ApplyEffect(Ship ship, ImpactProperties impactProperties)
        {
            HapticController.PlayHaptic(impactProperties.hapticType);
        }
    }
}