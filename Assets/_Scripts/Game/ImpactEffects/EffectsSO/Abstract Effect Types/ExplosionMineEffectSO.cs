namespace CosmicShore.Game
{
    public abstract class ExplosionMineEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ExplosionImpactor impactor, MineImpactor impactee);
    }
}