
namespace CosmicShore.Game.ImpactEffects
{
    public abstract class ProjectileMineEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, MineImpactor mineImpactee);
    }
}
