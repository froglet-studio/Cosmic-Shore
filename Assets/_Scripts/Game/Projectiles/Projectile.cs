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

        // Replaces Coroutine
        private CancellationTokenSource _moveCts;

        // Factory reference
        private ProjectileFactory _factory;

        private void Awake()
        {
            InitialScale = transform.localScale;
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

        private void OnDestroy()
        {
            Debug.LogError("Projectile destroyed! Should not happen! Should return to pool!");
        }

        #region Initialization
        public virtual void Initialize(ProjectileFactory factory, Domains ownDomain, IVesselStatus vesselStatus, float charge)
        {
            _factory = factory;
            OwnDomain = ownDomain;
            VesselStatus = vesselStatus;
            Charge = charge;
        }

        public void SetType(ProjectileType type) => Type = type;
        #endregion

        #region Impact Checks
        public bool DisallowImpactOnPrism(Domains trailBlockDomain) => !friendlyFire && trailBlockDomain == OwnDomain;
        public bool DisallowImpactOnVessel(Domains vesselDomain) => vesselDomain == OwnDomain;
        #endregion
        
        
        public void LaunchProjectile(float projectileTime)
        {
            ProjectileTime = projectileTime;

            if (spike)
            {
                transform.localScale = new Vector3(0.4f, 0.4f, 2f);
                /*if (meshRenderer == null)
                    meshRenderer = GetComponent<MeshRenderer>();*/
                meshRenderer.material.SetFloat("_Opacity", 0.5f);
            }

            Stop(); // Stop any running movement before starting a new one

            _moveCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            MoveProjectileAsync(projectileTime, _moveCts.Token).Forget();
        }

        private async UniTaskVoid MoveProjectileAsync(float projectileTime, CancellationToken token)
        {
            float elapsedTime = 0f;
            var t = transform; // cache for speed
            var useSpike = spike && meshRenderer;
            var mat = useSpike ? meshRenderer.material : null;

            try
            {
                while (elapsedTime < projectileTime && !token.IsCancellationRequested)
                {
                    float deltaTime = Time.deltaTime;

                    // smoother trajectory tapering using cosine falloff
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
                // ReturnToFactory();
            }
            catch (OperationCanceledException)
            {
                // expected when stopped manually
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Projectile] Move loop error: {ex}");
            }
        }
        
        public void ReturnToFactory()
        {
            Stop();
            if (_factory)
                _factory.ReturnProjectile(this);
            else
            {
                Debug.LogError("No projectile factory found to release projectile!, this shouldn't happen!");
            }
        }

        void Stop()
        {
            if (_moveCts == null) 
                return;
            
            _moveCts.Cancel();
            _moveCts.Dispose();
            _moveCts = null;
        }
    }
}
