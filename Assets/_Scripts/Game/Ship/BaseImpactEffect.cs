using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class BaseImpactEffect : ScriptableObject, IImpactEffect
    {
        public abstract void Execute(ImpactContext context);
    }
}
