namespace CosmicShore.Game
{
    public abstract class ShipExplosionEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ShipImpactor impactor, ExplosionImpactor impactee);
    }
}