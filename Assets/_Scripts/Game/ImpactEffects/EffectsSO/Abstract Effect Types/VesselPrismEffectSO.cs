
namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class VesselPrismEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, PrismImpactor prismImpactee);
    }
}
