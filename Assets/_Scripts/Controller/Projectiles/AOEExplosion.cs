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

        [Tooltip("OFF: this blast never touches prism MASS - it neither destroys nor shields a " +
                 "single prism, and its trigger should additionally author the TrailBlocks layer " +
                 "into Exclude Layers so the Physics fallback cannot reach one either. For a " +
                 "blast whose whole payload is aimed at LIVING things (the Sparrow's missile " +
                 "warhead: debuff the pilots, kill the creatures, leave the arena to the other " +
                 "explosion in the same detonation).\n\n" +
                 "Note it is NOT the same as clearing Destructive: a non-destructive blast still " +
                 "reaches every prism it engulfs and ARMOURS it (ExecuteCommonPrismCommands' " +
                 "accept branch calls ActivateShield), which on a 95-unit sphere would " +
                 "temporarily shield half an arena.")]
        [SerializeField] protected bool affectsPrisms = true;

        /// <summary>
        /// Whether this blast's prism pass runs at all. Read by
        /// <see cref="ExplosionImpactor.BeginBatchProcessing"/> — ONE gate, so every explosion
        /// SHAPE honours it even though each owns its own ExplodeAsync.
        /// </summary>
        public bool AffectsPrisms => affectsPrisms;

        [Header("Impact")]
        [Tooltip("Gain on the blast-wave speed handed to the mass this blast destroys. 1 = debris leaves at the wavefront's own speed. NOTE: this dial only reaches the screen with Proportional Debris ON - see below.")]
        [SerializeField] protected float Inertia = 1f;

        [Tooltip("ON: the impact vector is a TRUE debris velocity and the blast supplies its own speed ceiling, so Inertia scales what the player actually sees. OFF (legacy): debris is clamped to the explosion prefab's authored max (33.33 u/s), which every AOE blast already exceeds several times over - the Dolphin cone hands over ~222 u/s - so every blast saturates to the same speed and Inertia does nothing.")]
        [SerializeField] protected bool proportionalDebris;

        [Tooltip("Debris speed as a fraction of the blast wave that threw it, before Inertia. 1/3 matches the physical read the vessel and skimmer damage paths ship at, so all three stay one tuning group. Only used when Proportional Debris is ON.")]
        [Range(0.05f, 3f)]
        [SerializeField] protected float debrisRestitution = 1f / 3f;

        // Debris speed and SHATTER RATE are one number on the proportional contract
        // (PrismExplosion.TriggerExplosion re-reads Speed off the clamped velocity when
        // an override is supplied, deliberately - otherwise raising the ceiling finishes
        // the shatter in a frame while the debris crawls). So Inertia scales BOTH, and
        // restitution x Inertia = 1 reproduces today's shatter rate exactly while
        // unclipping the throw. Tune with the single Inertia dial; do not split them.

        protected Vector3 MaxScaleVector;
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

        /// <summary>
        /// What this blast hands the mass it destroys. On the proportional contract the
        /// vector IS the debris velocity, so the blast supplies a ceiling in the same
        /// units and <see cref="Inertia"/> scales the result linearly. On the legacy
        /// contract there is no ceiling of our own, the explosion prefab's clamp applies,
        /// and - since every AOE magnitude sits several times above it - the speed reads
        /// the same no matter what <see cref="Inertia"/> says.
        /// </summary>
        public ExplosionImpulse Impulse
        {
            get
            {
                if (!proportionalDebris) return new ExplosionImpulse(speed, Inertia);

                float debrisSpeed = speed * debrisRestitution;
                return new ExplosionImpulse(debrisSpeed, Inertia, debrisSpeed * Inertia);
            }
        }

        protected ExplosionImpactor _explosionImpactor;
        private float _colliderRadius = 0.5f; // Default sphere collider radius
        private MaterialPropertyBlock _mpb;

        /// <summary>
        /// The blast's trigger volume: vessel impacts and the Physics fallback resolve through
        /// it (the prism layer is excluded while the Burst batch path owns prism damage).
        /// Typed as <see cref="Collider"/> rather than <see cref="SphereCollider"/> because the
        /// shape is the SUBCLASS's business - the spherical blast authors a sphere, the conic
        /// blast a capsule whose axis is the vessel's jaw gape.
        /// </summary>
        protected Collider _triggerCollider;
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
            _triggerCollider = GetComponent<Collider>();
            // Only the SPHERICAL blast derives its batch radius from the authored collider;
            // the conic blast computes its own geometry from MaxScale/height every frame.
            if (_triggerCollider is SphereCollider sphere) _colliderRadius = sphere.radius;
            if (s_trailBlocksMask < 0) s_trailBlocksMask = LayerMask.GetMask("TrailBlocks");
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>
        /// While batch processing is active, exclude the prism layer from the
        /// trigger collider so PhysX never generates the thousands of trigger pairs
        /// that OnTriggerEnter would discard (they're handled by ProcessBatchFrame).
        /// Only TrailBlocks is excluded - vessel pairs must stay live because
        /// explosion→vessel effects (e.g. VesselChangeSpeedByExplosionEffect) are
        /// resolved through this collider's OnTriggerEnter, not the batch path.
        /// </summary>
        protected void ApplyPrismExclusion()
        {
            if (_prismExclusionApplied || !_triggerCollider) return;
            _originalExcludeLayers = _triggerCollider.excludeLayers;
            _triggerCollider.excludeLayers = _originalExcludeLayers | s_trailBlocksMask;
            _prismExclusionApplied = true;
        }

        protected void RestorePrismExclusion()
        {
            if (!_prismExclusionApplied || !_triggerCollider) return;
            _triggerCollider.excludeLayers = _originalExcludeLayers;
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

            ApplyAffectSelfOverride(initStruct);
            ApplyDevastatingOverride(initStruct);

            MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale);
            speed = MaxScale / ExplosionDuration;

            Material = initStruct.OverrideMaterial;

            _visualComplete = false;
            if (_triggerCollider) _triggerCollider.enabled = true;

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

        /// <summary>
        /// Pushes a per-instance friendly-fire decision onto this explosion's impactor. Each
        /// explosion is instantiated fresh from its prefab, so writing the instance's flag cannot
        /// leak into the next blast. No override leaves the authored prefab value alone.
        /// </summary>
        protected void ApplyDevastatingOverride(InitializeStruct initStruct)
        {
            if (!initStruct.DevastatingOverride.HasValue) return;
            if (_explosionImpactor) _explosionImpactor.SetDevastating(initStruct.DevastatingOverride.Value);
        }

        protected void ApplyAffectSelfOverride(InitializeStruct initStruct)
        {
            if (!initStruct.AffectSelfOverride.HasValue) return;
            if (!_explosionImpactor) _explosionImpactor = GetComponent<ExplosionImpactor>();
            if (_explosionImpactor) _explosionImpactor.SetAffectSelf(initStruct.AffectSelfOverride.Value);
        }

        /// <summary>
        /// The post-injection subscription retry. PROTECTED, not private, because a subclass that
        /// overrides <see cref="Initialize"/> wholesale (the conic and cylindrical blasts both do)
        /// otherwise has no way to reach it — and <see cref="OnEnable"/> cannot cover for it: a
        /// runtime-spawned blast runs Awake+OnEnable inside `Instantiate`, BEFORE any call site
        /// gets to inject it, so `gameData` is still null there. That is why this call exists at
        /// the end of Initialize at all. An override that skips it silently ships a blast with no
        /// turn-end kill switch and no replay-reset cleanup.
        /// </summary>
        protected void SubscribeToGameEvents()
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
            return Impulse.Along(direction);
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

                if (impactor != null && impactor.IsBatchProcessing && _triggerCollider != null)
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
                            cachedTransform.position, currentRadius, cachedTransform.position, Impulse) ?? true;

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
                if (_triggerCollider) _triggerCollider.enabled = false;
                while (impactor != null && impactor.HasPendingBatchWork)
                {
                    ct.ThrowIfCancellationRequested();
                    impactor.DrainPendingBatchFrame(Impulse);
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

            /// <summary>
            /// Replaces the CONIC explosion's authored axial length (how far down-range the
            /// blast reaches) for this instance; 0 (the default) keeps the authored value.
            /// MaxScale is the cone's BASE DIAMETER, so the two together set the half-angle -
            /// which is why a caller that wants to widen the cone without lengthening it
            /// (the Dolphin's energy-driven blast) must control them independently.
            /// Ignored by the spherical base explosion.
            /// </summary>
            public float HeightOverride;

            /// <summary>
            /// The CONIC explosion's CLOSED-JAW base diameter — the width the blast has when the
            /// driving resource is empty, in the same units and already carrying the same Space
            /// size multiplier as <see cref="MaxScale"/>. It is the capsule's DIAMETER: the blast
            /// stays exactly this wide across the jaw gape at every charge, and everything
            /// <see cref="MaxScale"/> adds beyond it becomes capsule LENGTH along the gape axis
            /// (see <c>AOEConicExplosion</c>). 0 (the default) means "no separate core" — the
            /// capsule collapses to the plain circular cone, which is what the projectile
            /// overload and the spherical base explosion want.
            /// </summary>
            public float CoreScale;

            /// <summary>
            /// Per-instance friendly-fire override for the spawned <see cref="ExplosionImpactor"/>:
            /// false spares the blast's own domain, true lets it damage allies. null (the default)
            /// keeps whatever the prefab authored. Used by elemental upgrades that turn
            /// friendly fire off as a reward.
            /// </summary>
            public bool? AffectSelfOverride;

            /// <summary>Per-instance override of the prefab's authored `devastating` flag: a
            /// devastating blast destroys SHIELDED prisms outright rather than only shedding
            /// their shields. null (the default) keeps the authored value. Same shape as
            /// <see cref="AffectSelfOverride"/>.</summary>
            public bool? DevastatingOverride;
        }
    }
}
