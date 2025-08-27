namespace CosmicShore.Game
{
    public abstract class ProjectileShipEffectSO : AnyPrismEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, ShipImpactor shipImpactee);
    }
}