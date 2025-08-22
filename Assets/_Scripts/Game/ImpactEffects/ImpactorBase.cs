using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class ImpactorBase : MonoBehaviour, IImpactor
    {
        public Transform Transform => transform;
        
        protected abstract void AcceptImpactee(IImpactor impactee);

        protected void ExecuteEffect(IImpactor impactee, IImpactEffect[] effects)
        {
            if (effects == null || effects.Length == 0)
                return;
            
            foreach (var effect in effects)
            {
                effect.Execute(this, impactee);
            }
        }
        
        void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out IImpactCollider impacteeCollider))
                return;
            
            AcceptImpactee(impacteeCollider.Impactor);
        }

    }
}