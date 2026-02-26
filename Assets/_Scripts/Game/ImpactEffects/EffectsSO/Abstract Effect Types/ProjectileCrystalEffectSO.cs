namespace CosmicShore.Game.ImpactEffects
{
    public abstract class ProjectileCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, CrystalImpactor crystalImpactee);
    }
}