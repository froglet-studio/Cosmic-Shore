namespace CosmicShore.Game
{
    /// <summary>
    /// This interface must be implemented by all impact effects.
    /// This interface is used to define the contract for 
    /// impact effects that can be applied to ships, prisms, skimmers, projectiles, explosions, fake crystals, elemental cystals, omni crystals
    /// </summary>
    public interface IImpactEffect
    {
        void Execute(IImpactor impactor, IImpactor impactee);
    }
}