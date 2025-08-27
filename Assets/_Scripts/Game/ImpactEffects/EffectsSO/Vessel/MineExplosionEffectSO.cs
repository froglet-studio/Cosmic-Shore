namespace CosmicShore.Game
{
    public abstract class MineExplosionEffectSO : AnyPrismEffectSO
    {
        public abstract void Execute(MineImpactor impactor, ExplosionImpactor explosionImpactee);
    }
}