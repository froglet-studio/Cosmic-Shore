using System;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using Reflex.Attributes;
using Reflex.Core;

namespace CosmicShore.Gameplay
{
    public abstract class ImpactorBase : MonoBehaviour, IImpactor
    {
        [Inject] Container _diContainer;
        public Container DIContainer => _diContainer;

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