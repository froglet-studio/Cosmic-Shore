namespace CosmicShore.Game
{
    public abstract class SkimmerShipEffectsSO : AnyPrismEffectSO
    {
        public abstract void Execute(SkimmerImpactor impactor, ShipImpactor shipImpactee);
    }
}