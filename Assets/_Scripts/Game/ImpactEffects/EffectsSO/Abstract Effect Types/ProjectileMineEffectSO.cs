
namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class ProjectileMineEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, MineImpactor mineImpactee);
    }
}
