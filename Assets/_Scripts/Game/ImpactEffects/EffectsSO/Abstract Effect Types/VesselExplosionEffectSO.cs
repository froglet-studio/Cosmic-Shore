
namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class VesselExplosionEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, ExplosionImpactor impactee);
    }
}
