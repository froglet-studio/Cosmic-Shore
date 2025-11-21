using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace CosmicShore.Game.Projectiles
{
    [RequireComponent(typeof(MeshRenderer))]
    public class AOEExplosion : ElementalShipComponent
    {
        protected const float PI_OVER_TWO = Mathf.PI / 2;

        [Header("Explosion Settings")]
        [SerializeField] protected float ExplosionDuration = 2f;
        [SerializeField] protected float ExplosionDelay = 0.2f;
        [SerializeField] MeshRenderer renderer;

        protected Vector3 MaxScaleVector;
        protected float Inertia = 70;
        protected float speed;

        protected CancellationTokenSource explosionCts;

        public Material Material { get; protected set; }
        public Domains Domain { get; protected set; }
        public IVessel Vessel { get; protected set; }
        public bool AnonymousExplosion { get; protected set; }
        public float MaxScale { get; protected set; } = 200f;

        private void Awake()
        { 
            if (!renderer)
                renderer = GetComponent<MeshRenderer>();
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
            ExplodeAsync(explosionCts.Token).Forget();
        }

        public void CancelExplosionAndDestroy()
        {
            CancelExplosion();
            // Destroy handled by derived classes too
            Destroy(gameObject);
        }

        void CancelExplosion()
        {
            if (explosionCts is not { IsCancellationRequested: false }) 
                return;
            
            explosionCts.Cancel();
            explosionCts.Dispose();
        }

        public Vector3 CalculateImpactVector(Vector3 impacteePosition)
        {
            // Direction from explosion to impactee
            Vector3 direction = (impacteePosition - transform.position).normalized;

            // Scale by explosion speed and inertia
            return direction * speed * Inertia;
        }
        
        protected virtual async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(ExplosionDelay), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
                
                if (renderer)
                    renderer.material = Material;

                float time = 0f;

                while (time < ExplosionDuration)
                {
                    ct.ThrowIfCancellationRequested();

                    time += Time.deltaTime;
                    float t = time / ExplosionDuration;
                    float ease = Mathf.Sin(t * PI_OVER_TWO);

                    transform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, ease);

                    if (Material != null)
                        Material.SetFloat("_Opacity", 1 - ease);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                Destroy(gameObject);
            }
            catch (OperationCanceledException) { }
        }
        
        private void OnValidate()
        {
            renderer = GetComponent<MeshRenderer>();
            if (!renderer)
            {
                Debug.LogError("No mesh renderer found!");
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
