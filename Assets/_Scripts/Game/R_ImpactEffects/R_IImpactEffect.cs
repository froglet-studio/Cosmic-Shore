using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// This interface must be implemented by all impact effects.
    /// This interface is used to define the contract for 
    /// impact effects that can be applied to ships, prisms, skimmers, projectiles, explosions, fake crystals, elemental cystals, omni crystals
    /// </summary>
    public interface R_IImpactEffect
    {
        void Execute(R_IImpactor impactor, R_IImpactor impactee);
    }
}
