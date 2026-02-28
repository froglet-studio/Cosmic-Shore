
namespace CosmicShore.Gameplay
{
    public abstract class ExplosionMineEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ExplosionImpactor impactor, MineImpactor impactee);
    }
}
