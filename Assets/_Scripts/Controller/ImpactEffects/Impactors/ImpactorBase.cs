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
        // Dispatch is gated on the shell's signed margin. Sphere touchers (the
        // skimmer sphere included) are measured with the sphere-vs-shell margin,
        // so "sphere reaches the shell" (margin >= 0) is the one condition for
        // both skim and pop — no per-impactor grazing band needed.
        // See Docs/CollisionLOD/DESIGN.md §2/§3.6/§7.

        /// <summary>
        /// Minimum shell signed margin (normalized shell units) at which this
        /// impactor dispatches against a shielded prism. Default 0 = containment
        /// (the toucher — sphere-aware for SphereCollider touchers — reaches the
        /// shell surface).
        /// </summary>
        protected virtual float ShieldMarginThreshold => 0f;

        /// <summary>
        /// Self-side narrowphase seam: when THIS impactor is itself a shielded
        /// prism, an incoming contact must reach its shell before dispatch.
        /// Default true (non-prism impactors have no shell of their own);
        /// PrismImpactor overrides to test its own Prism.ActiveShieldGate.
        /// </summary>
        protected virtual bool PassesOwnShieldNarrowphase(Collider other) => true;

        /// <summary>
        /// Whether THIS impactor currently has an engaged shell of its own (only a
        /// shielded prism does; base = false). Used to DROP a self-side parked
        /// contact when the shell is popped/withdrawn since parking — mirrors the
        /// impactee-side gate-null drop so a pop can't destroy the freshly
        /// unshielded prism in its own tick via a parked corner contact.
        /// </summary>
        protected virtual bool HasOwnShieldGate => false;

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

            if (pending.ImpacteePrism != null)
            {
                // Impactee-side parked contact: only dispatch while the shell
                // that parked it is still engaged. If the gate went null the
                // shell was popped/withdrawn since parking (possibly by this
                // very swing, mid-callback) — DROP the contact instead of
                // dispatching it as a plain-box hit, which would destroy the
                // freshly-unshielded prism in the same tick as the pop. A
                // genuine hit on the restored authored box produces its own
                // fresh OnTriggerEnter. See Docs/CollisionLOD/DESIGN.md §7.
                var prism = pending.ImpacteePrism.Prism;
                if (prism == null || prism.ActiveShieldGate == null)
                {
                    _pendingShieldContacts.Remove(other);
                    return;
                }

                if (!PassesShieldGate(pending.ImpacteePrism))
                    return;
            }
            else
            {
                // Self-side parked contact: THIS prism's shell parked it. If the
                // shell is gone now (popped/withdrawn since parking, possibly this
                // very swing), DROP — mirrors the impactee-side gate-null drop so
                // the pop can't destroy the freshly-unshielded prism in its tick
                // via a corner contact that never reached the shell.
                if (!HasOwnShieldGate)
                {
                    _pendingShieldContacts.Remove(other);
                    return;
                }

                if (!PassesOwnShieldNarrowphase(other))
                    return;
            }

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
        /// prism has no engaged shell, or this impactor's toucher reaches the
        /// shell within this impactor's margin threshold. Sphere touchers use
        /// the sphere-vs-shell margin (centre + radius × world gradient) —
        /// a ClosestPoint sample toward the prism centre under-measures
        /// tangential grazes and creates skim dead zones.
        /// </summary>
        bool PassesShieldGate(PrismImpactor prismImpactee)
        {
            var prism = prismImpactee.Prism;
            if (prism == null)
                return true;

            var gate = prism.ActiveShieldGate;
            if (gate == null)
                return true; // unshielded: the box IS the shape

            // Sphere touchers (skimmer sphere, other sphere triggers): gate on
            // the analytic sphere-vs-shell margin at the sphere's WORLD centre
            // and WORLD radius — "sphere reaches the shell" — rather than a
            // point sampled toward the prism centre.
            if (OwnCollider is SphereCollider sc)
            {
                Vector3 worldCentre = sc.transform.TransformPoint(sc.center);
                Vector3 ls = sc.transform.lossyScale;
                float worldRadius = sc.radius * Mathf.Max(Mathf.Abs(ls.x),
                    Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
                return gate.SignedMarginSphere(worldCentre, worldRadius) >= ShieldMarginThreshold;
            }

            // Non-sphere touchers: probe from THIS impactor's OWN collider — the
            // toucher's nearest approach to the prism centre. Measuring the
            // prism's own box (the `other` that entered our trigger) would return
            // the centre and evaluate the margin deep inside the shell (~+1), so
            // the gate would never bite.
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
