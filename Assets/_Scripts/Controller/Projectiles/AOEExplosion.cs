using System;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Core;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Profiling;

namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(MeshRenderer))]
    public class AOEExplosion : ElementalShipComponent
    {
        protected const float PI_OVER_TWO = Mathf.PI / 2;

        [Header("Dependencies")]
        [Inject] protected GameDataSO gameData;

        [Header("Explosion Settings")]
        [SerializeField] protected float ExplosionDuration = 2f;
        [SerializeField] protected float ExplosionDelay = 0.2f;
        [FormerlySerializedAs("renderer")] [SerializeField] protected MeshRenderer meshRenderer;

        protected Vector3 MaxScaleVector;
        protected float Inertia = 1;
        protected float speed;

        protected CancellationTokenSource explosionCts;

        public Material Material { get; protected set; }
        public Domains Domain { get; protected set; }
        public IVessel Vessel { get; protected set; }
        public bool AnonymousExplosion { get; protected set; }
        public float MaxScale { get; protected set; } = 200f;

        /// <summary>The visual wavefront's full-expansion time (authored on the prefab,
        /// or the per-instance <see cref="InitializeStruct.DurationOverride"/>).</summary>
        public float Duration => ExplosionDuration;

        protected ExplosionImpactor _explosionImpactor;
        private float _colliderRadius = 0.5f; // Default sphere collider radius
        private MaterialPropertyBlock _mpb;
        protected SphereCollider _sphereCollider;
        private LayerMask _originalExcludeLayers;
        private bool _prismExclusionApplied;

        /// <summary>
        /// Set once the growth animation finishes and only the deferred-damage drain
        /// remains. A cancellation after that point should tear the explosion down
        /// rather than leave the frozen-at-scale husk the mid-animation case wants.
        /// </summary>
        protected bool _visualComplete;
        private static int s_trailBlocksMask = -1;
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
        private static readonly ProfilerMarker s_explodeFrame = new("AOE.ExplodeAsync.Frame");

        protected virtual void Awake()
        {
            if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();
            _explosionImpactor = GetComponent<ExplosionImpactor>();
            _sphereCollider = GetComponent<SphereCollider>();
            if (_sphereCollider) _colliderRadius = _sphereCollider.radius;
            if (s_trailBlocksMask < 0) s_trailBlocksMask = LayerMask.GetMask("TrailBlocks");
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>
        /// While batch processing is active, exclude the prism layer from the
        /// SphereCollider so PhysX never generates the thousands of trigger pairs
        /// that OnTriggerEnter would discard (they're handled by ProcessBatchFrame).
        /// Only TrailBlocks is excluded - vessel pairs must stay live because
        /// explosion→vessel effects (e.g. VesselChangeSpeedByExplosionEffect) are
        /// resolved through this collider's OnTriggerEnter, not the batch path.
        /// </summary>
        protected void ApplyPrismExclusion()
        {
            if (_prismExclusionApplied || !_sphereCollider) return;
            _originalExcludeLayers = _sphereCollider.excludeLayers;
            _sphereCollider.excludeLayers = _originalExcludeLayers | s_trailBlocksMask;
            _prismExclusionApplied = true;
        }

        protected void RestorePrismExclusion()
        {
            if (!_prismExclusionApplied || !_sphereCollider) return;
            _sphereCollider.excludeLayers = _originalExcludeLayers;
            _prismExclusionApplied = false;
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

            // Optional per-instance duration (0 = keep the authored value). Callers
            // that want a fixed physical EXPANSION SPEED rather than a fixed sweep
            // time pass radius/speed here — everything downstream (wavefront ease,
            // batch damage radius, impact speed) already derives from this one field.
            if (initStruct.DurationOverride > 0f)
                ExplosionDuration = initStruct.DurationOverride;

            MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale);
            speed = MaxScale / ExplosionDuration;

            Material = initStruct.OverrideMaterial;

            _visualComplete = false;
            if (_sphereCollider) _sphereCollider.enabled = true;

            explosionCts = new CancellationTokenSource();

            // Start invisible - ExplodeAsync enables the renderer once the animation
            // begins. Without this, the mesh is visible at prefab default scale for
            // the entire ExplosionDelay, appearing as a full-size opaque sphere.
            transform.localScale = Vector3.zero;
            if (meshRenderer) meshRenderer.enabled = false;

            // Subscribe to game events - OnEnable fires before DI injection
            // for runtime-spawned objects, so retry here after injection.
            SubscribeToGameEvents();
        }

        private void SubscribeToGameEvents()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnEnd.OnRaised -= CancelExplosion;
            gameData.OnMiniGameTurnEnd.OnRaised += CancelExplosion;
            gameData.OnResetForReplay.OnRaised -= PerformResetCleanup;
            gameData.OnResetForReplay.OnRaised += PerformResetCleanup;
        }

        public void Detonate()
        {
            CancelExplosion();
            explosionCts = new CancellationTokenSource();
            AudioSystem.Instance?.PlayGameplaySFX(GameplaySFXCategory.Explosion, transform.position);
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

        public virtual Vector3 CalculateImpactVector(Vector3 impacteePosition)
        {
            Vector3 direction = (impacteePosition - transform.position).normalized;
            return direction * speed * Inertia;
        }

        protected virtual async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            // Cache impactor ref - _explosionImpactor may be null after Destroy
            var impactor = _explosionImpactor;
            // Track collider state outside try/catch so catch blocks can restore it
            bool colliderDisabledForBatch = false;
            try
            {
                // Start batch AOE processing - skips Physics OnTriggerEnter for prisms
                impactor?.BeginBatchProcessing();

                if (impactor != null && impactor.IsBatchProcessing && _sphereCollider != null)
                {
                    // Batch processing is active: stop PhysX from generating
                    // prism trigger pairs - the Burst job handles spatial queries.
                    ApplyPrismExclusion();
                    colliderDisabledForBatch = true;
                }
                else
                {
                    // Falling back to Physics triggers (index unavailable or
                    // ForceLegacyPhysics). A previous cancelled run may have left
                    // the prism exclusion applied - prisms must be visible again.
                    RestorePrismExclusion();
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
                        if (colliderDisabledForBatch) RestorePrismExclusion();
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
                        // Spherical explosion: wavefront and blast origin share the
                        // stationary center - impacts radiate from the spawn point.
                        bool shouldContinue = impactor?.ProcessBatchFrame(
                            cachedTransform.position, currentRadius, cachedTransform.position, speed, Inertia) ?? true;

                        if (!shouldContinue)
                        {
                            // Super-shielded enemy hit - mirrors original Destroy(gameObject) in
                            // ExecuteCommonPrismCommands. Stop explosion immediately.
                            impactor?.EndBatchProcessing();
                            if (colliderDisabledForBatch) RestorePrismExclusion();
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

                // Work the blast owes but could not afford within its per-frame budget
                // still has to land - a prism's fate is decided by whether the blast
                // CONTAINED it, never by how long the VFX happened to run.
                //
                // Retire the blast from the world first: hide the mesh AND disable the
                // trigger. The trigger keeps vessel pairs live (only TrailBlocks is
                // excluded), so leaving it on would park an invisible, full-size vessel
                // hitbox here for the whole drain.
                _visualComplete = true;
                if (meshRenderer) meshRenderer.enabled = false;
                if (_sphereCollider) _sphereCollider.enabled = false;
                while (impactor != null && impactor.HasPendingBatchWork)
                {
                    ct.ThrowIfCancellationRequested();
                    impactor.DrainPendingBatchFrame(speed, Inertia);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                impactor?.EndBatchProcessing();
                if (colliderDisabledForBatch) RestorePrismExclusion();
                if (this) Destroy(gameObject);
            }
            catch (OperationCanceledException)
            {
                impactor?.EndBatchProcessing();
                // Deliberately NOT restoring the prism exclusion here: a cancelled
                // explosion (e.g. OnMiniGameTurnEnd) stays alive frozen at its
                // current scale. Re-including TrailBlocks would make PhysX refilter
                // and fire OnTriggerEnter for every overlapping prism - a damage
                // burst through the Physics fallback path after the turn ended.
                // The exclusion is restored lazily at the start of the next
                // ExplodeAsync run, or discarded with the GameObject on reset.
                //
                // Exception: once the visual is done we are only draining bookkeeping -
                // the mesh is hidden and the trigger is off, so there is no frozen cone
                // to preserve and leaving it would strand the GameObject.
                if (_visualComplete && this) Destroy(gameObject);
            }
            catch (System.Exception e)
            {
                // Safety net: any unexpected exception (e.g. NativeList overflow) must still
                // clean up batch processing and destroy the explosion - otherwise it stays
                // stuck at max scale with _useBatchProcessing permanently true.
                Debug.LogException(e);
                impactor?.EndBatchProcessing();
                if (colliderDisabledForBatch) RestorePrismExclusion();
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
            /// <summary>Replaces the prefab's authored ExplosionDuration for this
            /// instance; 0 (the default) keeps the authored value.</summary>
            public float DurationOverride;
        }
    }
}
