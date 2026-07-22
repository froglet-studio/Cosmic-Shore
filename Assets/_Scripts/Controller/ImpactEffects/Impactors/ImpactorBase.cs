using System;
using Unity.Netcode;
using Unity.Profiling;
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

        // Per-concrete-type profiler marker so an impact storm shows up in captures as
        // e.g. 'SkimmerImpactor.AcceptImpactee' with real timings instead of vanishing
        // into Physics.SendEvents self-time. Lazily created (one string per component
        // lifetime) — no Awake added here, since most subclasses declare their own.
        ProfilerMarker _acceptMarker;
        bool _acceptMarkerInit;

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!isInitialized)
                return;

            // Use the concrete ImpactCollider (the sole IImpactCollider implementer)
            // rather than TryGetComponent<IImpactCollider>. An interface-typed
            // GetComponent forces Unity to iterate and type-check every component on
            // the GameObject; this runs per prism-enter across dense trails and was
            // ~26% self-time inside Physics.SendEvents. The concrete type uses Unity's
            // native typed-lookup fast path.
            if (!other.TryGetComponent(out ImpactCollider impacteeCollider))
                return;

            // Shield narrowphase: an engaged prism shield's PhysX trigger is its box
            // AABB proxy, so a contact can land in the AABB's corner/notch regions
            // outside the true octahedron/stellation surface. Reject those with the
            // shield's analytic containment test (a few linear forms — cheaper than
            // the convex MeshCollider narrowphase this replaced). Checked in both
            // directions: the impactee's shield (their proxy fired our trigger) and
            // our own (we are a shielded prism receiving a toucher).
            if (impacteeCollider.Impactor is PrismImpactor prismImpactee)
            {
                var gate = prismImpactee.Prism != null ? prismImpactee.Prism.ActiveShieldGate : null;
                if (gate != null && !gate.ContainsWorldPoint(other.ClosestPoint(transform.position)))
                    return;
            }
            if (!PassesOwnShieldNarrowphase(other))
                return;

            if (!_acceptMarkerInit)
            {
                _acceptMarkerInit = true;
                _acceptMarker = new ProfilerMarker(GetType().Name + ".AcceptImpactee");
            }
            using (_acceptMarker.Auto())
                AcceptImpactee(impacteeCollider.Impactor);
        }

        /// <summary>
        /// Self-side shield narrowphase — overridden by <see cref="PrismImpactor"/>
        /// to test the toucher against this prism's engaged shield surface. Default:
        /// pass (only prisms carry shields).
        /// </summary>
        protected virtual bool PassesOwnShieldNarrowphase(Collider other) => true;
    }
}