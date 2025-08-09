using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Any impact effect that is defined in the game should inherit from this class.
    /// </summary>
    public abstract class ImpactEffectSO : ScriptableObject, R_IImpactEffect
    {
        public abstract void Execute(R_IImpactor impactor, R_IImpactor impactee);
    }
}
