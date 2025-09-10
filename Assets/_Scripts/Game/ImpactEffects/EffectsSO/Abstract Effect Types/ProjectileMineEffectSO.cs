namespace CosmicShore.Game
{
    public abstract class ProjectileMineEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, MineImpactor mineImpactee);
    }
}