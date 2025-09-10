namespace CosmicShore.Game
{
    public abstract class VesselExplosionEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, ExplosionImpactor impactee);
    }
}