namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class ProjectileCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, CrystalImpactor crystalImpactee);
    }
}