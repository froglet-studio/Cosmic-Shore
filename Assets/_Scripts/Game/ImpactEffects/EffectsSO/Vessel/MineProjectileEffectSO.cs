namespace CosmicShore.Game
{
    public abstract class MineProjectileEffectSO : ImpactEffectSO
    {
        public abstract void Execute(MineImpactor mineImpactor, ProjectileImpactor projectileImpact);
    }
}