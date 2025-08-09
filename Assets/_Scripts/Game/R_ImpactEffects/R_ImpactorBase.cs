using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class R_ImpactorBase : MonoBehaviour, R_IImpactor
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out R_IImpactor impactee))
                return;
            
            AcceptImpactee(impactee);
        }
        
        protected abstract void AcceptImpactee(R_IImpactor impactee);

        protected void ExecuteEffect(R_IImpactor impactee, R_IImpactEffect[] effects)
        {
            foreach (var effect in effects)
            {
                effect.Execute(this, impactee);
            }
        }
    }
}