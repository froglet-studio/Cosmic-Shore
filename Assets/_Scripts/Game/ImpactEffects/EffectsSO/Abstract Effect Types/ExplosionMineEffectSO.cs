
namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class ExplosionMineEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ExplosionImpactor impactor, MineImpactor impactee);
    }
}
