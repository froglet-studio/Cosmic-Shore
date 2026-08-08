using System;
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
        }

        public void SetType(ProjectileType type) => Type = type;
        #endregion

        #region Impact Checks
        public bool DisallowImpactOnPrism(Domains trailBlockDomain) => !friendlyFire && trailBlockDomain == OwnDomain;
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
                    t.position += Velocity * (deltaTime * factor);

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

        void Stop()
        {
            if (_moveCts == null) return;

            _moveCts.Cancel();
            _moveCts.Dispose();
            _moveCts = null;
        }
    }
}
