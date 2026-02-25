using System;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.App.Systems.Audio;
using Cysharp.Threading.Tasks;
using System.Threading;
using CosmicShore.Soap; // Required for GameDataSO

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
        protected float Inertia = 70;
        protected float speed;

        protected CancellationTokenSource explosionCts;

        public Material Material { get; protected set; }
        public Domains Domain { get; protected set; }
        public IVessel Vessel { get; protected set; }
        public bool AnonymousExplosion { get; protected set; }
        public float MaxScale { get; protected set; } = 200f;

        protected virtual void Awake()
        { 
            if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();
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
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(ExplosionDelay), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

                if (!this || ct.IsCancellationRequested) return;

                var cachedTransform = transform;
                if (meshRenderer) meshRenderer.material = Material;

                float time = 0f;

                while (time < ExplosionDuration)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!this || cachedTransform == null) return;

                    time += Time.deltaTime;
                    float t = time / ExplosionDuration;
                    float ease = Mathf.Sin(t * PI_OVER_TWO);

                    cachedTransform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, ease);

                    if (Material != null) Material.SetFloat("_Opacity", 1 - ease);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                if (this) Destroy(gameObject);
            }
            catch (OperationCanceledException) { }
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