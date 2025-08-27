namespace CosmicShore.Game
{
    public abstract class ProjectileMineEffectSO : AnyPrismEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, MineImpactor mineImpactee);
    }
}