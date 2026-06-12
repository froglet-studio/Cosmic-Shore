using System;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.App.Systems.Audio;
using Cysharp.Threading.Tasks;
using System.Threading;
using CosmicShore.Game;
using CosmicShore.Soap;
using Unity.Profiling;

namespace CosmicShore.Game.Projectiles
{
    [RequireComponent(typeof(MeshRenderer))]
    public class AOEExplosion : ElementalShipComponent
    {
        protected const float PI_OVER_TWO = Mathf.PI / 2;

        [Header("Dependencies")]
        [SerializeField] protected GameDataSO gameData;

        [Header("Explosion Settings")]
        [SerializeField] protected float ExplosionDuration = 2f;
        [SerializeField] protected float ExplosionDelay = 0.2f;
        [FormerlySerializedAs("renderer")] [SerializeField] MeshRenderer meshRenderer;

        protected Vector3 MaxScaleVector;
        protected float Inertia = 1;
        protected float speed;

        protected CancellationTokenSource explosionCts;

        public Material Material { get; protected set; }
        public Domains Domain { get; protected set; }
        public IVessel Vessel { get; protected set; }
        public bool AnonymousExplosion { get; protected set; }
        public float MaxScale { get; protected set; } = 200f;

        private ExplosionImpactor _explosionImpactor;
        private float _colliderRadius = 0.5f; // Default sphere collider radius
        private MaterialPropertyBlock _mpb;
        private SphereCollider _sphereCollider;
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
        private static readonly ProfilerMarker s_explodeFrame = new("AOE.ExplodeAsync.Frame");

        protected virtual void Awake()
        {
            if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();
            _explosionImpactor = GetComponent<ExplosionImpactor>();
            _sphereCollider = GetComponent<SphereCollider>();
            if (_sphereCollider) _colliderRadius = _sphereCollider.radius;
            _mpb = new MaterialPropertyBlock();
        }

        protected virtual void OnEnable()
        {
            if (gameData != null)
            {
                // [Visual Note] Stop CPU heavy tasks immediately on turn end
                gameData.OnMiniGameTurnEnd.OnRaised += CancelExplosion;
                // [Visual Note] Destroy objects only when resetting
                gameData.OnResetForReplay.OnRaised += PerformResetCleanup;
            }
        }

        protected virtual void OnDisable()
        {
            if (gameData != null)
            {
                gameData.OnMiniGameTurnEnd.OnRaised -= CancelExplosion;
                gameData.OnResetForReplay.OnRaised -= PerformResetCleanup;
            }
            CancelExplosion();
        }

        // Virtual: Children can override if they need to destroy spawned sub-objects (like prisms)
        protected virtual void PerformResetCleanup()
        {
            CancelExplosion();
            Destroy(gameObject);
        }

        public virtual void Initialize(InitializeStruct initStruct)
        {
            transform.SetPositionAndRotation(initStruct.SpawnPosition, initStruct.SpawnRotation);
            AnonymousExplosion = initStruct.AnnonymousExplosion;
            Vessel = initStruct.Vessel;
            Domain = initStruct.OwnDomain;
            MaxScale = initStruct.MaxScale;

            MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale);
            speed = MaxScale / ExplosionDuration;

            Material = initStruct.OverrideMaterial;

            explosionCts = new CancellationTokenSource();

            // Start invisible — ExplodeAsync enables the renderer once the animation
            // begins. Without this, the mesh is visible at prefab default scale for
            // the entire ExplosionDelay, appearing as a full-size opaque sphere.
            transform.localScale = Vector3.zero;
            if (meshRenderer) meshRenderer.enabled = false;
        }

        public void Detonate()
        {
            CancelExplosion();
            explosionCts = new CancellationTokenSource();
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.Explosion);
            ExplodeAsync(explosionCts.Token).Forget();
        }

        public void CancelExplosionAndDestroy()
        {
            PerformResetCleanup();
        }

        public void CancelExplosion()
        {
            if (explosionCts == null) return;
            if (!explosionCts.IsCancellationRequested) explosionCts.Cancel();
            explosionCts.Dispose();
            explosionCts = null;
        }

        // ... [CalculateImpactVector and ExplodeAsync remain unchanged] ...
        
        public Vector3 CalculateImpactVector(Vector3 impacteePosition)
        {
            Vector3 direction = (impacteePosition - transform.position).normalized;
            return direction * speed * Inertia;
        }
        
        protected virtual async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            // Cache impactor ref — _explosionImpactor may be null after Destroy
            var impactor = _explosionImpactor;
            // Track collider state outside try/catch so catch blocks can restore it
            bool colliderDisabledForBatch = false;
            try
            {
                // Start batch AOE processing — skips Physics OnTriggerEnter for prisms
                impactor?.BeginBatchProcessing();

                // When batch processing is active, disable the SphereCollider entirely.
                // PhysX won't compute any trigger pairs — the Burst job handles spatial queries.
                if (impactor != null && impactor.IsBatchProcessing && _sphereCollider != null)
                {
                    _sphereCollider.enabled = false;
                    colliderDisabledForBatch = true;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(ExplosionDelay), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

                if (!this || ct.IsCancellationRequested)
                {
                    impactor?.EndBatchProcessing();
                    return;
                }

                var cachedTransform = transform;
                if (meshRenderer)
                {
                    meshRenderer.material = Material;
                    meshRenderer.enabled = true;
                }

                float time = 0f;

                while (time < ExplosionDuration)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!this || cachedTransform == null)
                    {
                        impactor?.EndBatchProcessing();
                        if (colliderDisabledForBatch && _sphereCollider) _sphereCollider.enabled = true;
                        return;
                    }

                    using (s_explodeFrame.Auto())
                    {
                        time += Time.deltaTime;
                        float t = time / ExplosionDuration;
                        float ease = Mathf.Sin(t * PI_OVER_TWO);

                        cachedTransform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, ease);

                        // Batch AOE damage via Burst job over cache-packed prism data
                        // Effective radius = collider radius (local) * localScale
                        float currentRadius = _colliderRadius * MaxScale * ease;
                        bool shouldContinue = impactor?.ProcessBatchFrame(
                            cachedTransform.position, currentRadius, speed, Inertia) ?? true;

                        if (!shouldContinue)
                        {
                            // Super-shielded enemy hit — mirrors original Destroy(gameObject) in
                            // ExecuteCommonPrismCommands. Stop explosion immediately.
                            impactor?.EndBatchProcessing();
                            if (colliderDisabledForBatch && _sphereCollider) _sphereCollider.enabled = true;
                            if (this) Destroy(gameObject);
                            return;
                        }

                        // Use MaterialPropertyBlock for per-instance opacity so multiple
                        // concurrent explosions sharing the same Material don't fight over
                        // the shared material's _Opacity value (which caused flickering).
                        if (meshRenderer)
                        {
                            meshRenderer.GetPropertyBlock(_mpb);
                            _mpb.SetFloat(OpacityID, 1 - ease);
                            meshRenderer.SetPropertyBlock(_mpb);
                        }
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                impactor?.EndBatchProcessing();
                if (colliderDisabledForBatch && _sphereCollider) _sphereCollider.enabled = true;
                if (this) Destroy(gameObject);
            }
            catch (OperationCanceledException)
            {
                impactor?.EndBatchProcessing();
                if (colliderDisabledForBatch && _sphereCollider) _sphereCollider.enabled = true;
            }
            catch (System.Exception e)
            {
                // Safety net: any unexpected exception (e.g. NativeList overflow) must still
                // clean up batch processing and destroy the explosion — otherwise it stays
                // stuck at max scale with _useBatchProcessing permanently true.
                Debug.LogException(e);
                impactor?.EndBatchProcessing();
                if (colliderDisabledForBatch && _sphereCollider) _sphereCollider.enabled = true;
                if (this) Destroy(gameObject);
            }
        }

        public struct InitializeStruct
        {
            public Domains OwnDomain;
            public bool AnnonymousExplosion;
            public IVessel Vessel;
            public Material OverrideMaterial;
            public float MaxScale;
            public Vector3 SpawnPosition;
            public Quaternion SpawnRotation;
        }
    }
}