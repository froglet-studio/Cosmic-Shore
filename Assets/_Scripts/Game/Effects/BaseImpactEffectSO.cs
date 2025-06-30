using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class BaseImpactEffectSO : ScriptableObject, IImpactEffect
    {
        public abstract void Execute(ImpactContext context);
    }
}
