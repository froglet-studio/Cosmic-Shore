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

        private MeshRenderer meshRenderer;

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

            // cache whatever parent it has in the pool (ship container or pool root)
            _pooledParent = transform.parent;
        }

        // Spike opacity rides a MaterialPropertyBlock: the old .material getter cloned
        // one material instance per pooled spike (never destroyed) purely to write
        // _Opacity — the shared team material stays shared now.
        static readonly int s_opacityId = Shader.PropertyToID("_Opacity");
        MaterialPropertyBlock _spikeMpb;

        void SetSpikeOpacity(float opacity)
        {
            if (!meshRenderer) return;
            _spikeMpb ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(_spikeMpb);
            _spikeMpb.SetFloat(s_opacityId, opacity);
            meshRenderer.SetPropertyBlock(_spikeMpb);
        }

        private void Start()
        {
            if (spike)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = _themeManagerData.GetTeamSpikeMaterial(OwnDomain);
                SetSpikeOpacity(0.5f);
            }
        }

        private void OnDestroy()
        {
            // Pooled projectiles should return to the factory, not be destroyed — but
            // when one IS destroyed (scene teardown, missing-factory fallback) this
            // cancels the in-flight move loop. It replaces the per-launch linked
            // destroy-token, which cost an extra CTS + registration per shot.
            Stop();
        }

        #region Initialization
        public virtual void Initialize(ProjectileFactory factory, Domains ownDomain, IVesselStatus vesselStatus, float charge, bool detachOnLaunch = false)
        {
            _factory = factory;
            OwnDomain = ownDomain;
            VesselStatus = vesselStatus;
            Charge = charge;
            _detachOnLaunch = detachOnLaunch;
        }

        public void SetType(ProjectileType type) => Type = type;
        #endregion

        #region Impact Checks
        public bool DisallowImpactOnPrism(Domains trailBlockDomain) => !friendlyFire && trailBlockDomain == OwnDomain;
        public bool DisallowImpactOnVessel(Domains vesselDomain) => vesselDomain == OwnDomain;
        #endregion

        public void LaunchProjectile(float projectileTime)
        {
            if (!_factory)
            {
                CSDebug.LogError("No factory for this projectile found. Can't return to pool!");
            }

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
                SetSpikeOpacity(0.5f);
            }

            Stop(); // Stop any running movement before starting a new one

            // Plain CTS — destroy-cancellation is handled by OnDestroy -> Stop(), so
            // the linked-token pair (2 allocs + registration) per shot is unnecessary.
            _moveCts = new CancellationTokenSource();
            MoveProjectileAsync(projectileTime, _moveCts.Token).Forget();
        }

        public void ReturnToFactory()
        {
            Stop();

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
                            SetSpikeOpacity(1f - Mathf.Pow(percentRemaining, 4f));
                    }

                    elapsedTime += deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token);
                }

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
