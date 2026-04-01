using System;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class ImpactorBase : MonoBehaviour, IImpactor
    {
        protected virtual bool isInitialized => true;
        
        public Transform Transform => transform;
        public abstract Domains OwnDomain { get; }
        
        protected abstract void AcceptImpactee(IImpactor impactee);

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