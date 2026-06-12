
namespace CosmicShore.Gameplay
{
    public abstract class VesselExplosionEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, ExplosionImpactor impactee);
    }
}
