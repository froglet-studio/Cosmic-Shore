using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using CosmicShore.Core;

namespace CosmicShore.Game.Projectiles
{
    public class Projectile : MonoBehaviour
    {
        public Vector3 Velocity { get; set; }
        public Vector3 InitialScale { get; private set; }

        [SerializeField] private ProjectileImpactor projectileImpactor;

        [Header("Projectile Settings")]
        [SerializeField] private bool spike = false;
        [SerializeField] private float growthRate = 1.0f;
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
        }
        
        private void Awake()
        {
            InitialScale = transform.localScale;

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
            Debug.LogError("Projectile destroyed! Should not happen! Should return to pool!");
        }*/

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
                Debug.LogError("No factory for this projectile found. Can't return to pool!");
            }
            
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
                Debug.LogWarning("No projectile factory found to release projectile!");
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

                projectileImpactor.ExecuteEndEffects();
                // ReturnToFactory(); // handled by end effects (delayed)
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[Projectile] Move loop error: {ex}");
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
