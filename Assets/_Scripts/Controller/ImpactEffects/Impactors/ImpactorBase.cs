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
        /// Uniform shrink applied to THIS impactor's hull collider(s) in the
        /// shielded narrowphase ONLY — never to the physics broadphase trigger.
        /// A vessel's authored hull box is a loose bounding box, larger than the
        /// visible mesh, so an exact box-vs-shell overlap fires the instant the
        /// box EDGE reaches the shell while the visible ship still has a gap
        /// ("the box outline pops it"). Scaling the box half-edges about their
        /// centre by this factor lets the effective hull hug the visible
        /// silhouette so contact reads as ship-touches-shell, not
        /// box-touches-shell. 1 = the authored collider (default — the skimmer
        /// sphere MUST stay exact or tangential skims dead-zone). VesselImpactor
        /// exposes it as a live-tunable inspector knob.
        /// See Docs/CollisionLOD/DESIGN.md §7.4.
        /// </summary>
        protected virtual float HullNarrowphaseScale => 1f;

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

        // This impactor's own trigger collider SET — the "touchers" whose overlap with a
        // shielded prism the narrowphase measures. On a toucher's OnTrigger callback
        // `other` is the prism's OWN (enlarged) box, so measuring `other` returns the
        // prism centre (deep inside the shell) and defeats the gate; probe from these
        // colliders instead. The impactor's collider often lives on CHILD GameObjects
        // (e.g. VesselImpactor sits on the vessel ROOT with the kinematic Rigidbody,
        // while the hull BoxColliders are on children — the Squirrel has two), so a
        // same-GO-only lookup returns null and defeats the whole shape test. Prefer
        // collider(s) on this GO; else fall back to the compound hull in children.
        // Lazily resolved once.
        Collider[] _ownColliders;
        bool _ownCollidersLookedUp;
        Collider[] OwnColliders
        {
            get
            {
                if (!_ownCollidersLookedUp)
                {
                    _ownCollidersLookedUp = true;
                    var own = GetComponents<Collider>();
                    _ownColliders = (own != null && own.Length > 0)
                        ? own
                        : GetComponentsInChildren<Collider>(true);
                }
                return _ownColliders;
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

            // THIS impactor's collider(s) are the touchers; dispatch if ANY of them
            // reaches the impactee prism's shell (logical OR — a compound hull is one
            // toucher made of several boxes). HullNarrowphaseScale shrinks the loose
            // hull box down to the visible silhouette (vessels only; 1 elsewhere).
            return AnyColliderReachesShell(OwnColliders, gate, prism.transform.position,
                ShieldMarginThreshold, transform.position, HullNarrowphaseScale);
        }

        /// <summary>
        /// OR over an impactor's collider SET: dispatch if ANY collider reaches the shell.
        /// A compound hull can be several colliders (the Squirrel's two boxes); the
        /// impactor touches the shell if any piece does. Falls back to the single
        /// pivot-point test ONLY when the set holds no live collider at all.
        /// </summary>
        protected static bool AnyColliderReachesShell(Collider[] colliders, IShieldContainmentGate gate,
            Vector3 prismCentre, float threshold, Vector3 fallbackPoint, float hullScale = 1f)
        {
            bool sawCollider = false;
            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    var c = colliders[i];
                    if (c == null)
                        continue;
                    sawCollider = true;
                    if (ColliderReachesShell(c, gate, prismCentre, threshold, fallbackPoint, hullScale))
                        return true;
                }
            }

            // No collider exists at all — measure from the impactor's pivot.
            return sawCollider ? false : gate.SignedMargin(fallbackPoint) >= threshold;
        }

        /// <summary>
        /// Shape-aware "does collider <paramref name="c"/> reach the shell
        /// <paramref name="gate"/> within <paramref name="threshold"/>" test — the
        /// analytic equivalent of a convex octahedron/stella mesh collider.
        /// A SphereCollider uses the exact sphere-vs-shell margin. A BoxCollider uses
        /// the exact analytic OBB-vs-shell overlap (Separating-Axis Test) — no
        /// false-reject at the thin tips, unlike the old two-point support sample that
        /// let hulls clip straight through. Any other convex/primitive collider is
        /// approximated by its world-AABB OBB (conservative over-cover). The
        /// <paramref name="threshold"/> maps to the shell inflate in normalized units
        /// (inflate = −threshold): threshold 0 ⇒ exact containment/pop, a negative
        /// grazing threshold ⇒ the shell is grown by that magnitude.
        /// </summary>
        protected static bool ColliderReachesShell(Collider c, IShieldContainmentGate gate,
            Vector3 prismCentre, float threshold, Vector3 fallbackPoint, float hullScale = 1f)
        {
            if (c == null)
                return gate.SignedMargin(fallbackPoint) >= threshold;

            // hullScale shrinks the effective toucher about its OWN centre so a loose
            // bounding collider reads as the visible silhouette, not the box outline.
            if (hullScale <= 0f) hullScale = 1f;

            if (c is SphereCollider sc)
            {
                Vector3 wc = sc.transform.TransformPoint(sc.center);
                Vector3 ls = sc.transform.lossyScale;
                float r = sc.radius * Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
                return gate.SignedMarginSphere(wc, r * hullScale) >= threshold;
            }

            // Shell inflate (normalized units): 0 = exact containment/pop; a negative
            // grazing threshold grows the shell by its magnitude.
            float inflate = -threshold;

            if (c is BoxCollider bc)
            {
                Transform t = bc.transform;
                Vector3 worldCenter = t.TransformPoint(bc.center);
                float h = 0.5f * hullScale;
                Vector3 axX = t.TransformVector(new Vector3(bc.size.x * h, 0f, 0f));
                Vector3 axY = t.TransformVector(new Vector3(0f, bc.size.y * h, 0f));
                Vector3 axZ = t.TransformVector(new Vector3(0f, 0f, bc.size.z * h));
                return gate.OverlapsWorldBox(worldCenter, axX, axY, axZ, inflate);
            }

            // Any other convex/primitive (or non-convex mesh) collider: approximate as
            // its world-AABB OBB. Conservative — over-covers the true hull, the safe
            // direction (never a skim dead zone). ClosestPoint isn't usable for a
            // general shape, and the AABB SAT still catches the thin tips.
            Bounds b = c.bounds;
            Vector3 ext = b.extents * hullScale;
            return gate.OverlapsWorldBox(b.center,
                new Vector3(ext.x, 0f, 0f), new Vector3(0f, ext.y, 0f), new Vector3(0f, 0f, ext.z),
                inflate);
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
