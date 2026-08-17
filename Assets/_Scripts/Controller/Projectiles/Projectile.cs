using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using CosmicShore.Core;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class Projectile : MonoBehaviour
    {
        [Inject] AudioSystem audioSystem;
        public Vector3 Velocity { get; set; }
        public Vector3 InitialScale { get; private set; }

        [SerializeField] private ProjectileImpactor projectileImpactor;

        [Header("Projectile Settings")]
        [SerializeField] private bool spike = false;
        [SerializeField] private bool friendlyFire = false;

        [Tooltip("Test the SEGMENT this projectile crossed each frame for prism contact, " +
                 "instead of relying on the PhysX trigger at the point it landed on.\n\n" +
                 "A projectile is a TELEPORT, not a sweep: the mover writes " +
                 "position += Velocity·Δt and PhysX samples the trigger once per physics " +
                 "step. A Sparrow round at its base 375 u/s crosses 6.25 u per frame behind " +
                 "a 1.65-diameter hit sphere, so ~74% of its path is never tested for " +
                 "collision at all — and at high SPACE it is ~97%. That reads in play as a " +
                 "gun that cannot clear a small area no matter how much you shoot, and it is " +
                 "why oversizing the collider 'fixed' the feel: a big enough ball closes the " +
                 "per-frame gap.\n\n" +
                 "With this on, prism contact comes from the swept query ONLY (the trigger " +
                 "path is suppressed for prisms, so nothing double-dispatches) and the hit " +
                 "volume can be the size the projectile actually looks.")]
        [SerializeField] private bool sweptPrismDetection = false;

        /// <summary>True when prism contact for this projectile is owned by the swept
        /// segment query rather than the PhysX trigger — read by
        /// <c>ProjectileImpactor</c> to suppress the trigger's prism case.</summary>
        public bool UsesSweptPrismDetection => sweptPrismDetection;

        [Header("Data Containers")]
        [SerializeField] private ThemeManagerDataContainerSO _themeManagerData;

        public float Charge { get; private set; }
        public ProjectileType Type { get; private set; }
        public float ProjectileTime { get; private set; }

        public Domains OwnDomain { get; private set; }
        public IVesselStatus VesselStatus { get; private set; }

        /// Incremented on every launch. Delayed continuations (e.g. the detonator's
        /// post-explosion pool return) must capture this and bail if it has moved on,
        /// so a stale callback can't act on a pooled-and-reissued instance.
        public int FlightGeneration { get; private set; }

        /// Per-shot: destroyed on its first prism impact (the sub-level-5 SPACE default;
        /// the 'Piercing Bullets' upgrade clears it at fire time). Detonating projectiles
        /// manage their own return through the detonator and leave this false.
        public bool StopOnFirstPrismImpact { get; private set; }

        /// Per-shot: the CHARGE level-5 'Domain-Safe Skybursts' upgrade — direct-hit
        /// damage spares prisms of the shooter's own domain. Snapshot at fire time.
        public bool SpareOwnDomain { get; private set; }

        /// Per-shot: this projectile is a PART of a pooled host object (the Sparrow's
        /// turret prism carries one), not an instance of the projectile pool. Its
        /// lifetime belongs to the host, so <see cref="ReturnToFactory"/> stops the
        /// flight and returns nothing — without this the null-factory branch would
        /// Destroy the host's child on the first stopping impact.
        public bool IsCarriedByHost { get; private set; }


        /// <summary>
        /// Raised the instant this flight ends — at BOTH death points: the lifetime
        /// expiring in <see cref="MoveProjectileAsync"/>, and a stopping prism impact
        /// in <c>ProjectileImpactor</c>. The bool is true when a prism stopped it.
        ///
        /// This is "wherever the bullet would be destroyed" made addressable: the
        /// Sparrow's Turret Stance anchors its prism here instead of the shot simply
        /// vanishing. Per-FLIGHT — cleared by <see cref="Initialize"/>, so a pooled
        /// reissue never carries the previous shooter's handler.
        /// </summary>
        public event Action<Projectile, bool> FlightEnded;

        /// <summary>Raises <see cref="FlightEnded"/> exactly once per flight.</summary>
        internal void RaiseFlightEnded(bool stoppedByImpact)
        {
            if (_flightEndRaised) return;
            _flightEndRaised = true;
            FlightEnded?.Invoke(this, stoppedByImpact);
        }

        bool _flightEndRaised;

        private MeshRenderer meshRenderer;
        private Collider _rootCollider;

        // NEW: remember pooled parent so we can restore it
        private Transform _pooledParent;

        // Replaces Coroutine
        private CancellationTokenSource _moveCts;

        // Factory reference
        private ProjectileFactory _factory;

        private bool _poolParentCaptured;
        private bool _detachOnLaunch;
        private bool _detachedThisFlight;

        private void OnEnable()
        {
            if (!_poolParentCaptured)
            {
                _pooledParent = transform.parent;
                _poolParentCaptured = true;
            }

            // The detonator disables the root collider on detonation (DisableColliderNow) and
            // pool reuse must undo that, or every detonated missile is reissued as a
            // fly-through-everything dud.
            if (_rootCollider) _rootCollider.enabled = true;

            // Proximity collider-LOD: a projectile in flight is a focus, so the prism
            // colliders along its path (including distant structures it was fired at)
            // are awake by the time it arrives. Unregistered on pool return.
            PrismColliderLodManager.RegisterFocus(transform);
        }

        private void OnDisable()
        {
            PrismColliderLodManager.UnregisterFocus(transform);
        }

        private void Awake()
        {
            InitialScale = transform.localScale;
            _rootCollider = GetComponent<Collider>();

            // cache whatever parent it has in the pool (ship container or pool root)
            _pooledParent = transform.parent;
        }

        private void Start()
        {
            if (spike)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                meshRenderer.material = _themeManagerData.GetTeamSpikeMaterial(OwnDomain);
                meshRenderer.material.SetFloat("_Opacity", 0.5f);
            }
        }

        /*private void OnDestroy()
        {
            CSDebug.LogError("Projectile destroyed! Should not happen! Should return to pool!");
        }*/

        #region Initialization
        public virtual void Initialize(ProjectileFactory factory, Domains ownDomain, IVesselStatus vesselStatus, float charge, bool detachOnLaunch = false,
            bool stopOnFirstPrismImpact = false, bool spareOwnDomain = false, bool carriedByHost = false)
        {
            _factory = factory;
            OwnDomain = ownDomain;
            VesselStatus = vesselStatus;
            Charge = charge;
            _detachOnLaunch = detachOnLaunch;
            StopOnFirstPrismImpact = stopOnFirstPrismImpact;
            SpareOwnDomain = spareOwnDomain;
            IsCarriedByHost = carriedByHost;

            // Per-flight: a pooled reissue must not inherit the previous shooter's
            // end-of-flight handler, and the once-only latch must re-arm.
            FlightEnded = null;
            _flightEndRaised = false;

            // Likewise the previous shot's MASS growth — a caller that does not set it gets
            // the un-grown default rather than whoever fired this instance last.
            _flightGrowthFactor = 1f;
        }

        public void SetType(ProjectileType type) => Type = type;
        #endregion

        #region Impact Checks
        /// <summary>
        /// Two independent reasons a projectile ignores a prism:
        /// (1) authored friendly-fire off for this projectile family (domain gate);
        /// (2) the prism's PLACEMENT-IMMUNITY window (<see cref="Prism.ProjectileImmuneUntil"/>)
        ///     is still open. The Sparrow turret stamps a window on each fired prism spanning
        ///     its flight (it is parked live at the anchor from fire time) plus a short settle
        ///     after placement — a shot cannot destroy its own delivery, and a spray cannot
        ///     erase its own freshest output. One TIME rule instead of identity/owner special
        ///     cases; once the window closes the prism is ordinary friendly-fire mass.
        /// </summary>
        public bool DisallowImpactOnPrism(Prism prism) =>
            (!friendlyFire && prism.Domain == OwnDomain) || Time.time < prism.ProjectileImmuneUntil;
        public bool DisallowImpactOnVessel(Domains vesselDomain) => vesselDomain == OwnDomain;
        #endregion

        public void LaunchProjectile(float projectileTime)
        {
            // A carried projectile has no factory BY DESIGN — its lifetime belongs to the
            // pooled host it is part of — so only shout about a missing one when there
            // should have been one.
            if (!_factory && !IsCarriedByHost)
            {
                CSDebug.LogError("No factory for this projectile found. Can't return to pool!");
            }

            FlightGeneration++;
            if (audioSystem)
                audioSystem.PlayGameplaySFX(GameplaySFXCategory.ProjectileLaunch, transform.position);
            ProjectileTime = projectileTime;

            if (_detachOnLaunch && transform.parent)
            {
                transform.SetParent(null, true);
                _detachedThisFlight = true;
            }
            else
            {
                _detachedThisFlight = false;
            }

            // === DETACH when spawned if it's a spike ===
            if (spike)
            {
                // keep world position/rotation
                transform.SetParent(null, true);
            }

            if (spike)
            {
                transform.localScale = new Vector3(0.4f, 0.4f, 2f);
                meshRenderer.material.SetFloat("_Opacity", 0.5f);
            }

            Stop(); // Stop any running movement before starting a new one

            // After every scale/parent change above — the carried turret collider is sized
            // per shot, so this cannot be cached at Awake.
            if (sweptPrismDetection) CacheSweepRadius();

            // The growth baseline is whatever this shot actually launched at (the gun applies
            // projectileScale, the turret sizes its carried collider per shot), never the
            // prefab's InitialScale.
            _launchScale = transform.localScale;
            _launchSweepRadius = _sweepRadius;

            _moveCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            MoveProjectileAsync(projectileTime, _moveCts.Token).Forget();
        }

        public void ReturnToFactory()
        {
            Stop();

            // Carried by a pooled HOST (the turret prism): the host owns the lifetime
            // and its own end-of-flight handler does the reattach. Returning here would
            // fall through to the null-factory branch and Destroy the host's child.
            if (IsCarriedByHost) return;

            // Only reattach if we had detached for this flight
            if (_detachedThisFlight && _pooledParent != null && transform.parent == null)
            {
                transform.SetParent(_pooledParent, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale    = Vector3.one; // or InitialScale
            }

            if (_factory) _factory.ReturnProjectile(this);
            else
            {
                // This should not happen, make sure to handle later
                CSDebug.LogWarning("No projectile factory found to release projectile!");
                Destroy(gameObject);
            }
        }

        private async UniTaskVoid MoveProjectileAsync(float projectileTime, CancellationToken token)
        {
            float elapsedTime = 0f;
            var t = transform; // cache
            var useSpike = spike && meshRenderer;
            var mat = useSpike ? meshRenderer.material : null;

            try
            {
                while (elapsedTime < projectileTime && !token.IsCancellationRequested)
                {
                    float deltaTime = Time.deltaTime;
                    float factor = Mathf.Cos(elapsedTime * Mathf.PI / (2f * projectileTime));

                    // Grow BEFORE the step is swept, so this frame's hit volume is the size the
                    // round has actually reached rather than the one it left the muzzle at.
                    if (_flightGrowthFactor != 1f)
                        ApplyFlightGrowth(elapsedTime / projectileTime);

                    Vector3 sweepFrom = t.position;
                    t.position += Velocity * (deltaTime * factor);

                    if (sweptPrismDetection)
                    {
                        SweepPrismsAlong(sweepFrom, t.position);

                        // A stopping impact has already run the whole end-of-flight path
                        // (RaiseFlightEnded + ReturnToFactory). Returning rather than
                        // breaking is deliberate: the loop's tail would otherwise fire the
                        // end effects a second time on an instance already back in the pool.
                        if (_flightEndRaised)
                            return;
                    }

                    if (useSpike)
                    {
                        float percentRemaining = elapsedTime / projectileTime;
                        if (percentRemaining > 0.9f)
                            mat.SetFloat("_Opacity", 1f - Mathf.Pow(percentRemaining, 4f));
                    }

                    elapsedTime += deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token);
                }

                // Death point #1: the lifetime expired. Signal before the end effects,
                // so a host that leaves something behind (the turret prism's anchor)
                // acts on the position the shot actually reached rather than a pooled
                // instance that has already been reset.
                RaiseFlightEnded(stoppedByImpact: false);

                projectileImpactor.ExecuteEndEffects();
                // ReturnToFactory(); // handled by end effects (delayed)
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                CSDebug.LogError($"[Projectile] Move loop error: {ex}");
            }
        }

        #region Swept prism detection

        /// <summary>
        /// Extra radius added when GATHERING candidates, on top of the projectile's own hit
        /// radius. <see cref="PrismSpatialIndex.QuerySegment"/> tests a prism's CENTRE, so
        /// without an allowance a prism whose body crosses the path but whose centre sits
        /// beside it would be missed. Sized to comfortably exceed the bounding radius of the
        /// largest prism the guns meet (a 0.8×0.5×5 turret prism is ~2.5, stretched by MASS);
        /// every candidate is then re-tested precisely against its own bounds, so a generous
        /// value costs a few extra distance checks, never a false hit.
        /// </summary>
        const float SweepCandidateExtent = 8f;

        readonly struct SweepHit
        {
            public readonly float T;                 // parameter along this frame's segment
            public readonly PrismImpactor Impactor;
            public SweepHit(float t, PrismImpactor impactor) { T = t; Impactor = impactor; }
        }

        // Shared scratch — the sweep is main-thread and non-reentrant (dispatching an impact
        // runs effects, never another projectile's move loop).
        static readonly List<Prism> s_sweepCandidates = new(64);
        static readonly List<SweepHit> s_sweepHits = new(16);
        static readonly Comparison<SweepHit> s_nearestFirst = (x, y) => x.T.CompareTo(y.T);

        float _sweepRadius = 0.5f;

        // ---- in-flight growth (MASS) ----
        float _flightGrowthFactor = 1f;
        Vector3 _launchScale = Vector3.one;
        float _launchSweepRadius = 0.5f;

        /// <summary>
        /// How many times its launch cross-section this round swells to by the end of its
        /// flight. Set per shot from the vessel's live MASS level; 1 = no growth (every
        /// projectile that does not opt in).
        /// </summary>
        public void SetFlightGrowth(float factor) => _flightGrowthFactor = Mathf.Max(0.01f, factor);

        /// <summary>
        /// Grows the round toward its full factor across the flight, and grows the swept hit
        /// radius by the SAME factor — the size you see is the size that hits.
        ///
        /// **Cross-section only.** The tracer mesh is a unit sphere at (1.5, 1.5, 20) — a
        /// 20-long dart — so scaling it uniformly at 6× would draw a 120-unit needle across a
        /// ~72-unit range. Width is what a hit volume is made of; length is just the streak.
        ///
        /// The hit radius is scaled EXPLICITLY rather than re-derived from lossyScale, because
        /// a SphereCollider takes the largest lossy component: with the length left alone, that
        /// stays the z-stretch and the derived radius would never move. (Consequence, and a
        /// deliberate scope line: the swept PRISM radius grows, while the PhysX radius the
        /// vessel/mine path uses is unchanged for the dart. Growing bullets against vessels is
        /// a Dog Fight balance change, not a prism-clearing one.)
        /// </summary>
        void ApplyFlightGrowth(float progress01)
        {
            float g = Mathf.LerpUnclamped(1f, _flightGrowthFactor, Mathf.Clamp01(progress01));
            transform.localScale = new Vector3(_launchScale.x * g, _launchScale.y * g, _launchScale.z);
            _sweepRadius = _launchSweepRadius * g;
        }

        /// <summary>
        /// The projectile's hit radius in world units, cached per launch (the Sparrow's
        /// carried turret collider is re-scaled per shot, so it cannot be cached earlier).
        /// A SphereCollider takes the LARGEST lossy-scale component — the same rule that once
        /// turned a 0.3 radius on a ×20-stretched tracer into a 6.0 world radius.
        /// </summary>
        void CacheSweepRadius()
        {
            if (_rootCollider is SphereCollider sphere)
            {
                Vector3 s = _rootCollider.transform.lossyScale;
                _sweepRadius = sphere.radius *
                    Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
                return;
            }

            // Non-sphere colliders: the smallest half-extent is the conservative read (a long
            // dart's AABB diagonal would massively overstate its cross-section). Swept
            // detection is opt-in and every current user is a sphere.
            _sweepRadius = _rootCollider
                ? Mathf.Min(_rootCollider.bounds.extents.x,
                    Mathf.Min(_rootCollider.bounds.extents.y, _rootCollider.bounds.extents.z))
                : 0.5f;
        }

        /// <summary>
        /// Tests the segment this projectile crossed THIS FRAME for prism contact and
        /// dispatches the hits **nearest-first**, which is what makes the sub-SPACE-5
        /// "destroyed on its first prism impact" rule mean the first prism along the path
        /// rather than an arbitrary one.
        ///
        /// The projectile is moved to each contact point before its impact is dispatched, so
        /// an effect reading the shot's position — and the Turret Stance's anchor, which puts
        /// its prism "wherever the bullet would be destroyed" — sees where the shot actually
        /// met the prism, not where the frame's step happened to end.
        /// </summary>
        void SweepPrismsAlong(Vector3 from, Vector3 to)
        {
            if (!projectileImpactor) return;

            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;

            if (index.QuerySegment(from, to, _sweepRadius + SweepCandidateExtent, s_sweepCandidates) == 0)
                return;

            Vector3 ab = to - from;
            float abLenSq = ab.sqrMagnitude;

            s_sweepHits.Clear();
            for (int i = 0; i < s_sweepCandidates.Count; i++)
            {
                var prism = s_sweepCandidates[i];
                if (!prism || prism.destroyed) continue;
                if (!prism.TryGetComponent(out PrismImpactor prismImpactor)) continue;

                Vector3 centre = prism.transform.position;
                float t = abLenSq > 1e-8f
                    ? Mathf.Clamp01(Vector3.Dot(centre - from, ab) / abLenSq)
                    : 0f;

                // Bounding-SPHERE contact rather than an exact capsule-vs-OBB test: it is a
                // few instructions instead of a narrowphase, and its error is a slightly
                // generous corner hit — the right direction to err for a saturation weapon,
                // and still far tighter than the oversized collider this replaces.
                float contact = _sweepRadius + 0.5f * prism.transform.lossyScale.magnitude;
                if ((centre - (from + ab * t)).sqrMagnitude > contact * contact) continue;

                s_sweepHits.Add(new SweepHit(t, prismImpactor));
            }

            if (s_sweepHits.Count == 0) return;
            if (s_sweepHits.Count > 1) s_sweepHits.Sort(s_nearestFirst);

            for (int i = 0; i < s_sweepHits.Count; i++)
            {
                var hit = s_sweepHits[i];
                if (!hit.Impactor) continue;

                transform.position = from + ab * hit.T;
                projectileImpactor.AcceptImpacteeFromSweep(hit.Impactor);

                // A stopping impact ran the whole end-of-flight path from inside that call;
                // the shot rests here.
                if (_flightEndRaised) return;
            }

            // Pierced everything it met — finish the frame's step.
            transform.position = to;
        }

        #endregion

        void Stop()
        {
            if (_moveCts == null) return;

            _moveCts.Cancel();
            _moveCts.Dispose();
            _moveCts = null;
        }
    }
}
