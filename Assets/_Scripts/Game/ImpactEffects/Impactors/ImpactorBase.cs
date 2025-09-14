using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class ImpactorBase : MonoBehaviour, IImpactor
    {
        protected virtual bool isInitialized => true;
        
        public Transform Transform => transform;
        
        protected abstract void AcceptImpactee(IImpactor impactee);

        protected void ExecuteEffect(IImpactor impactee, ImpactEffectSO[] effects)
        {
            if (effects == null || effects.Length == 0)
                return;
            
            /*foreach (var effect in effects)
            {
                effect.Execute(this, impactee);
            }*/
        }

        protected bool DoesEffectExist(ImpactEffectSO[] effects) => effects is { Length: > 0 };
        
        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!isInitialized)
                return;
            
            if (!other.TryGetComponent(out IImpactCollider impacteeCollider))
                return;
            
            AcceptImpactee(impacteeCollider.Impactor);
        }

    }
}