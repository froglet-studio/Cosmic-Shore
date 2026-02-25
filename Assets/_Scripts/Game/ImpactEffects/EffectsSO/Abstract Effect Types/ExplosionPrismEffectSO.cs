
namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class ExplosionPrismEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ExplosionImpactor impactor, PrismImpactor prismImpactee);
    }
}
