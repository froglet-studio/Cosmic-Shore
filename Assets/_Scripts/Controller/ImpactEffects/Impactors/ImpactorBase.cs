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

        // --- Shielded-prism narrowphase (Collision LOD analytic tier) --------
        // A shielded prism's broadphase box is resized to over-cover its visible
        // shell, so entering the box is no longer proof of touching the shell.
        // Dispatch is gated on the shell's signed margin; each impactor picks its
        // threshold: 0 = must reach the surface (pop/damage), negative = a grazing
        // proximity band (skim). See Docs/CollisionLOD/DESIGN.md §2/§3.6.

        /// <summary>
        /// Minimum shell signed margin (normalized shell units) at which this
        /// impactor dispatches against a shielded prism. 0 = containment
        /// (reached the surface); SkimmerImpactor overrides with a negative
        /// grazing band.
        /// </summary>
        protected virtual float ShieldMarginThreshold => 0f;

        /// <summary>
        /// Self-side narrowphase seam: when THIS impactor is itself a shielded
        /// prism, an incoming contact must reach its shell before dispatch.
        /// Default true (non-prism impactors have no shell of their own);
        /// PrismImpactor overrides to test its own Prism.ActiveShieldGate.
        /// </summary>
        protected virtual bool PassesOwnShieldNarrowphase(Collider other) => true;

        struct PendingShieldContact
        {
            public ImpactCollider ImpacteeCollider;
            public PrismImpactor ImpacteePrism; // null = self-side (re-test own gate)
        }

        // Contacts that entered the enlarged broadphase box OUTSIDE the shell
        // (margin below threshold): parked and re-tested on OnTriggerStay until
        // they cross the threshold (dispatch) or OnTriggerExit (drop). Without
        // this, a swipe that enters an AABB corner then sweeps into the shell
        // would never dispatch — OnTriggerEnter only fires once. Lazily
        // allocated: most impactors never meet a shielded prism.
        Dictionary<Collider, PendingShieldContact> _pendingShieldContacts;

        // This impactor's own trigger collider — the "toucher" whose nearest approach
        // to a shielded prism the narrowphase measures. On the toucher's OnTrigger
        // callback `other` is the prism's OWN (enlarged) box, so measuring `other`
        // returns the prism centre (deep inside the shell) and defeats the gate; probe
        // from this collider instead. Lazily resolved once.
        Collider _ownCollider;
        bool _ownColliderLookedUp;
        Collider OwnCollider
        {
            get
            {
                if (!_ownColliderLookedUp)
                {
                    _ownColliderLookedUp = true;
                    TryGetComponent(out _ownCollider);
                }
                return _ownCollider;
            }
        }

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

            // Self-side narrowphase: if THIS impactor is a shielded prism, the
            // incoming contact must reach its analytic shell first.
            if (!PassesOwnShieldNarrowphase(other))
            {
                ParkPendingContact(other, impacteeCollider, null);
                return;
            }

            // Impactee-side narrowphase: entering a shielded prism's enlarged
            // broadphase box only dispatches once the contact reaches its shell
            // (margin >= this impactor's threshold).
            if (impacteeCollider.Impactor is PrismImpactor prismImpactee
                && !PassesShieldGate(prismImpactee))
            {
                ParkPendingContact(other, impacteeCollider, prismImpactee);
                return;
            }

            DispatchAccept(impacteeCollider);
        }

        /// <summary>
        /// Re-tests parked contacts against the shell each physics tick and
        /// dispatches once the margin crosses this impactor's threshold.
        /// Overrides MUST call base first.
        /// </summary>
        protected virtual void OnTriggerStay(Collider other)
        {
            if (_pendingShieldContacts == null || _pendingShieldContacts.Count == 0)
                return;

            if (!_pendingShieldContacts.TryGetValue(other, out var pending))
                return;

            if (!isInitialized)
                return;

            if (pending.ImpacteeCollider == null)
            {
                // Impactee destroyed while parked — nothing left to dispatch.
                _pendingShieldContacts.Remove(other);
                return;
            }

            bool passes = pending.ImpacteePrism != null
                ? PassesShieldGate(pending.ImpacteePrism)
                : PassesOwnShieldNarrowphase(other);

            if (!passes)
                return;

            _pendingShieldContacts.Remove(other);
            DispatchAccept(pending.ImpacteeCollider);
        }

        /// <summary>
        /// Drops any parked shell contact for the departing collider.
        /// Overrides MUST call base first.
        /// </summary>
        protected virtual void OnTriggerExit(Collider other)
        {
            _pendingShieldContacts?.Remove(other);
        }

        /// <summary>
        /// True when the contact may dispatch against the impactee prism: the
        /// prism has no engaged shell, or the contact's closest point to the
        /// prism center is within this impactor's margin threshold of the shell.
        /// </summary>
        bool PassesShieldGate(PrismImpactor prismImpactee)
        {
            var prism = prismImpactee.Prism;
            if (prism == null)
                return true;

            var gate = prism.ActiveShieldGate;
            if (gate == null)
                return true; // unshielded (or shell dropped while parked): the box IS the shape

            // Probe from THIS impactor's OWN collider — the toucher's nearest approach
            // to the prism centre. Measuring the prism's own box (the `other` that
            // entered our trigger) would return the centre and evaluate the margin
            // deep inside the shell (~+1), so the gate would never bite.
            var probe = OwnCollider != null
                ? OwnCollider.ClosestPoint(prism.transform.position)
                : transform.position;
            return gate.SignedMargin(probe) >= ShieldMarginThreshold;
        }

        void ParkPendingContact(Collider other, ImpactCollider impacteeCollider, PrismImpactor impacteePrism)
        {
            _pendingShieldContacts ??= new Dictionary<Collider, PendingShieldContact>();
            _pendingShieldContacts[other] = new PendingShieldContact
            {
                ImpacteeCollider = impacteeCollider,
                ImpacteePrism = impacteePrism
            };
        }

        void DispatchAccept(ImpactCollider impacteeCollider)
        {
            if (!_acceptMarkerInit)
            {
                _acceptMarkerInit = true;
                _acceptMarker = new ProfilerMarker(GetType().Name + ".AcceptImpactee");
            }
            using (_acceptMarker.Auto())
                AcceptImpactee(impacteeCollider.Impactor);
        }
    }
}
