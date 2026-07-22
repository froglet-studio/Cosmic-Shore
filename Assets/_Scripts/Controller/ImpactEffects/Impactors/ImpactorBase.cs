using System;
using System.Collections.Generic;
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

        // Own collider, lazily cached for the shield-narrowphase test point (one
        // typed lookup per component lifetime — same budget rule as the marker).
        Collider _ownCollider;
        bool _ownColliderInit;

        // Colliders whose Enter was rejected by the shield narrowphase. The proxy
        // AABB over-covers the true shield surface and PhysX fires Enter only once
        // per overlap episode, so rejected pairs are re-tested every OnTriggerStay
        // until they cross the analytic surface or the overlap ends — otherwise a
        // corner/notch approach that later reaches the real shield would be lost
        // and shielded prisms would be intangible to most trajectories. Lazily
        // allocated; empty for every impactor not currently grazing a shield AABB.
        List<Collider> _pendingShieldContacts;

        /// <summary>
        /// Impactors that are volumetric rather than surface-contact (AOE
        /// explosions) opt out — for them the AABB over-cover is the correct
        /// containment read, and gating would break the super-shield
        /// blocks-the-explosion contract on the legacy physics path.
        /// </summary>
        protected virtual bool UsesShieldNarrowphase => true;

        Collider OwnCollider
        {
            get
            {
                if (!_ownColliderInit)
                {
                    _ownColliderInit = true;
                    _ownCollider = GetComponent<Collider>();
                }
                return _ownCollider;
            }
        }

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

            if (UsesShieldNarrowphase && !ShieldNarrowphasePasses(other, impacteeCollider.Impactor))
            {
                // Not (yet) touching the true shield surface — keep the pair and
                // re-test in OnTriggerStay as the overlap deepens.
                _pendingShieldContacts ??= new List<Collider>(4);
                if (!_pendingShieldContacts.Contains(other))
                    _pendingShieldContacts.Add(other);
                return;
            }

            DispatchImpact(impacteeCollider);
        }

        protected virtual void OnTriggerStay(Collider other)
        {
            // Hot path for every persisting overlap — bail on the common case first.
            if (_pendingShieldContacts == null || _pendingShieldContacts.Count == 0)
                return;
            if (!_pendingShieldContacts.Contains(other))
                return;

            if (!other.TryGetComponent(out ImpactCollider impacteeCollider))
            {
                _pendingShieldContacts.Remove(other);
                return;
            }

            if (!ShieldNarrowphasePasses(other, impacteeCollider.Impactor))
                return; // still in the over-cover region — keep waiting

            _pendingShieldContacts.Remove(other);
            DispatchImpact(impacteeCollider);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            _pendingShieldContacts?.Remove(other);
        }

        void DispatchImpact(ImpactCollider impacteeCollider)
        {
            if (!_acceptMarkerInit)
            {
                _acceptMarkerInit = true;
                _acceptMarker = new ProfilerMarker(GetType().Name + ".AcceptImpactee");
            }
            using (_acceptMarker.Auto())
                AcceptImpactee(impacteeCollider.Impactor);
        }

        /// <summary>
        /// Shield narrowphase: an engaged prism shield's PhysX trigger is its box
        /// AABB proxy, so a contact can start in the AABB's corner/notch regions
        /// outside the true octahedron/stellation surface. Both directions use the
        /// SAME conceptual test point — the point on the TOUCHER's collider nearest
        /// the shield's center — so the two halves of one physical impact agree:
        /// the impactee direction tests our own collider against their shield, and
        /// the self direction (we are the shielded prism) tests the toucher against
        /// ours via <see cref="PassesOwnShieldNarrowphase"/>.
        /// </summary>
        bool ShieldNarrowphasePasses(Collider other, IImpactor impactee)
        {
            if (impactee is PrismImpactor prismImpactee)
            {
                var prism = prismImpactee.Prism;
                var gate = prism != null ? prism.ActiveShieldGate : null;
                if (gate != null)
                {
                    var own = OwnCollider;
                    // other IS the shield's AABB proxy — its bounds center is the
                    // shield center. Fall back to our pivot for collider-less GOs.
                    Vector3 testPoint = own != null
                        ? own.ClosestPoint(other.bounds.center)
                        : transform.position;
                    if (!gate.ContainsWorldPoint(testPoint))
                        return false;
                }
            }
            return PassesOwnShieldNarrowphase(other);
        }

        /// <summary>
        /// Self-side shield narrowphase — overridden by <see cref="PrismImpactor"/>
        /// to test the toucher against this prism's engaged shield surface. Default:
        /// pass (only prisms carry shields).
        /// </summary>
        protected virtual bool PassesOwnShieldNarrowphase(Collider other) => true;
    }
}