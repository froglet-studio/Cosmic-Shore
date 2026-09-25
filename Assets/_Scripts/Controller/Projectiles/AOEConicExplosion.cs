using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class AOEConicExplosion : AOEExplosion
    {
        [SerializeField] private float height = 800f;
        [SerializeField] protected GameObject coneContainer;

        [Header("Gape")]
        [Tooltip("Container-local axis the blast's capsule extends along - the axis the emitting " +
                 "vessel's JAWS open across. The container is spawned with the ship's rotation, so " +
                 "(0,1,0) is ship UP, which is how the Dolphin's jaws gape. Perpendicular to this " +
                 "the blast stays exactly Core Scale wide at every charge.")]
        [SerializeField] private Vector3 gapeAxis = Vector3.up;

        /// <summary>
        /// The cone's axial reach as authored on the prefab — the baseline callers scale from when
        /// they drive reach off an element level, so the art stays the source of the cone's shape.
        /// Read it from the PREFAB; on a live instance it may already carry a per-blast override.
        /// </summary>
        public float AuthoredHeight => height;

        /// <summary>
        /// The container-local axis the capsule extends along, as authored on the prefab. Exposed
        /// so a PREVIEW of this blast (the Dolphin's Echo Sight) can describe the same volume the
        /// detonation will actually sweep instead of assuming ship-up.
        /// </summary>
        public Vector3 AuthoredGapeAxis => gapeAxis;

        /// <summary>
        /// The CLOSED-JAW base diameter (see <see cref="InitializeStruct.CoreScale"/>), clamped
        /// into (0, MaxScale]. Defaults to MaxScale when the caller supplies none, which collapses
        /// the capsule to a point and reproduces the plain circular cone exactly.
        /// </summary>
        public float CoreScale { get; private set; }

        /// <summary>World-space unit vector the capsule extends along — the emitting vessel's jaw
        /// gape axis, re-orthogonalised against the cone axis at Initialize.</summary>
        private Vector3 _gapeAxisWorld = Vector3.up;

        /// <summary>The trigger collider as a capsule, when the prefab authors one. Null on a
        /// prefab that still carries a sphere (or none), which simply keeps the old envelope.</summary>
        private CapsuleCollider _triggerCapsule;

        /// <summary>Which of the collider transform's LOCAL axes (0=x, 1=y, 2=z) the world gape
        /// axis runs along — Unity's <see cref="CapsuleCollider.direction"/>.</summary>
        private int _capsuleDirection = 2;

        public override void Initialize(InitializeStruct initStruct)
        {
            AnonymousExplosion = initStruct.AnnonymousExplosion;
            Vessel = initStruct.Vessel;

            if (Vessel == null)
            {
                CSDebug.LogError("Vessel is not initialized in AOEConicExplosion!");
                return;
            }

            Domain = initStruct.OwnDomain;
            if (Domain == Domains.Blue)
                Domain = Vessel.VesselStatus.Domain;

            MaxScale = initStruct.MaxScale;

            // MaxScale is the cone's BASE DIAMETER and height its axial reach, and the two together
            // fix the half-angle (see tanHalfAngle below). A caller that wants to widen the cone
            // without lengthening it - the Dolphin, whose skim energy opens the blast out while
            // Space sets how far down-range it carries - drives them independently through here.
            if (initStruct.HeightOverride > 0f)
                height = initStruct.HeightOverride;

            // The CLOSED-JAW width. Everything MaxScale adds beyond it is capsule LENGTH, not
            // extra radius (see the capsule geometry note on ExplodeAsync). Clamped into
            // (0, MaxScale]: a caller that supplies none, or one whose min already equals its
            // max, gets CoreScale == MaxScale, which is a capsule of zero length - the plain
            // circular cone, bit for bit.
            CoreScale = initStruct.CoreScale > 0f
                ? Mathf.Min(initStruct.CoreScale, MaxScale)
                : MaxScale;

            MaxScaleVector = new Vector3(MaxScale, MaxScale, height);

            ApplyAffectSelfOverride(initStruct);

            speed = height / (ExplosionDuration * 4);

            // Clone material so opacity change doesn't affect shared asset
            Material = new Material(Vessel.VesselStatus.AOEConicExplosionMaterial);
            if (!Material)
                Material = new Material(Vessel.VesselStatus.AOEExplosionMaterial);

            // Always create a fresh container – the serialised field may
            // reference the prefab's own root (self-parenting is a no-op).
            coneContainer = new GameObject("AOEContainer");
            coneContainer.transform.SetPositionAndRotation(initStruct.SpawnPosition, initStruct.SpawnRotation);

            // Parent our object to the container.
            // Euler(-90,0,0) rotates the cone mesh so its apex (mesh +Y)
            // points along -Z in container space. The Z+0.5 offset then
            // places the apex at the container origin (the spawn point),
            // with the cone opening forward along +Z.
            transform.SetParent(coneContainer.transform, false);
            transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            transform.localPosition = new Vector3(0f, 0f, 0.5f);

            ResolveGapeAxis();

            _visualComplete = false;
            if (_triggerCollider) _triggerCollider.enabled = true;

            // create CTS for explosion
            explosionCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Fixes the world-space gape axis for this blast and, when the prefab authors a capsule
        /// trigger, which of the collider transform's local axes that is.
        ///
        /// The container carries the SHIP's rotation, so the authored container-local
        /// <see cref="gapeAxis"/> is a vessel-local direction (up, for the Dolphin's jaws). It is
        /// re-orthogonalised against the cone axis because the capsule's cross-section has to be a
        /// clean stadium: a gape axis with a component along the cone axis would tilt the capsule
        /// out of the base plane and the axial slab test would no longer tile the swept volume.
        ///
        /// The capsule's <see cref="CapsuleCollider.direction"/> is DERIVED, not authored: the mesh
        /// child sits at a fixed 90 degree roll inside the container, so the collider's local axes
        /// are a permutation of the container's. Picking the best-matching one by dot product keeps
        /// the trigger and the Burst query pointing the same way even if that roll ever changes.
        /// </summary>
        private void ResolveGapeAxis()
        {
            var containerTransform = coneContainer.transform;
            Vector3 axis = containerTransform.forward;

            Vector3 world = containerTransform.TransformDirection(gapeAxis);
            world -= axis * Vector3.Dot(world, axis);            // strip the on-axis component
            _gapeAxisWorld = world.sqrMagnitude > 1e-8f
                ? world.normalized
                : containerTransform.up;

            _triggerCapsule = _triggerCollider as CapsuleCollider;
            if (!_triggerCapsule) return;

            // Which local axis of the COLLIDER's transform is the gape axis?
            float bestDot = -1f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 localAxis = i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
                float d = Mathf.Abs(Vector3.Dot(transform.TransformDirection(localAxis).normalized,
                                                _gapeAxisWorld));
                if (d <= bestDot) continue;
                bestDot = d;
                _capsuleDirection = i;
            }

            _triggerCapsule.direction = _capsuleDirection;
        }

        /// <summary>
        /// Drives the capsule trigger to this frame's blast cross-section: RADIUS pinned to the
        /// closed-jaw core, total LENGTH spanning the cone's full base diameter along the gape
        /// axis, so its two tips land exactly on the base circle the visual draws.
        ///
        /// Both are authored in the collider's LOCAL units, so each divides out the lossy scale
        /// Unity will re-apply — and Unity scales a capsule anisotropically: the height by the
        /// scale along <see cref="CapsuleCollider.direction"/>, the radius by the LARGEST of the
        /// other two. The container's non-uniform (base, base, reach) scale makes those different
        /// numbers, which is why they cannot share one divisor.
        /// </summary>
        private void UpdateCapsuleTrigger(float coreRadius, float baseRadius)
        {
            if (!_triggerCapsule) return;

            Vector3 lossy = transform.lossyScale;
            float alongScale = Mathf.Max(Mathf.Abs(lossy[_capsuleDirection]), 1e-4f);
            float radialScale = Mathf.Max(
                Mathf.Abs(lossy[(_capsuleDirection + 1) % 3]),
                Mathf.Abs(lossy[(_capsuleDirection + 2) % 3]));
            radialScale = Mathf.Max(radialScale, 1e-4f);

            _triggerCapsule.radius = coreRadius / radialScale;
            // Unity clamps height up to 2*radius on its own, so at closed jaws
            // (coreRadius == baseRadius) this IS a sphere of the core radius.
            _triggerCapsule.height = (2f * baseRadius) / alongScale;
        }

        protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            var impactor = _explosionImpactor;
            bool colliderExcludedForBatch = false;
            try
            {
                // Prism damage rides the Burst batch path (PrismSpatialIndex), same as
                // the spherical base explosion; the trigger sphere stays live for
                // explosion→vessel effects and as the physics fallback.
                impactor?.BeginBatchProcessing();
                if (impactor != null && impactor.IsBatchProcessing)
                {
                    ApplyPrismExclusion();
                    colliderExcludedForBatch = true;
                }
                else
                {
                    RestorePrismExclusion();
                }

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(ExplosionDelay),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    ct);

                if (meshRenderer) meshRenderer.material = Material;

                float elapsed = 0f;

                var containerTransform = coneContainer.transform;
                float maxScaleMag = MaxScaleVector.magnitude; // invariant for this explosion - hoist out of the per-frame loop

                // The cone is self-similar as it grows, so its half-angle is fixed:
                // base radius (MaxScale/2) over height. One value for the whole blast.
                float tanHalfAngle = height > 0f ? (MaxScale * 0.5f) / height : 0f;

                // ---- The blast cross-section is a CAPSULE, not a disc ----
                //
                // At axial depth s the swept volume's cross-section is a stadium: a disc of the
                // CLOSED-JAW radius (tanCoreHalfAngle * s) dragged along the gape axis. The core
                // radius never grows - what charge buys is LENGTH:
                //
                //     coreRadius(s) = tanCoreHalfAngle * s          (fixed by CoreScale)
                //     halfLength(s) = (tanHalfAngle - tanCore) * s  (what charge adds)
                //     tip extent    = coreRadius + halfLength = tanHalfAngle * s
                //
                // so the capsule's tips always land exactly on the cone's base circle, and the
                // whole stadium is inscribed in the cone the visual draws - the damage volume is
                // still bounded by what the player sees, it just no longer fills it off-axis.
                //
                // At empty charge MaxScale == CoreScale, halfLength is 0 and this degenerates to
                // the original disc - the same sweep, the same radius, bit for bit. Charging then
                // opens the blast the way the hull's jaws open: wide across the gape, unchanged
                // across the beam.
                float tanCoreHalfAngle = height > 0f ? (CoreScale * 0.5f) / height : 0f;
                float tanGapePerUnit = Mathf.Max(tanHalfAngle - tanCoreHalfAngle, 0f);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                // A degenerate cross-section renders a cone that damages nothing - the
                // exact failure mode this path was rewritten to eliminate, so say so
                // rather than fail silently.
                if (tanCoreHalfAngle <= 0f)
                    Debug.LogWarning(
                        $"[AOEConicExplosion] Degenerate cone (CoreScale={CoreScale}, " +
                        $"MaxScale={MaxScale}, height={height}) - the blast will render but " +
                        "damage nothing.", this);

                // The Burst sweep is now a capsule; a prefab still carrying a sphere leaves the
                // vessel-impact trigger and the physics fallback on the OLD circular envelope,
                // which is exactly the silent divergence this pairing exists to prevent.
                if (tanGapePerUnit > 0f && !_triggerCapsule)
                    Debug.LogWarning(
                        "[AOEConicExplosion] Blast opens a gape but its trigger is not a " +
                        "CapsuleCollider - vessel impacts will use the authored collider's " +
                        "shape instead of the swept capsule. Swap the trigger to a capsule.",
                        this);
#endif

                // Axial distance already swept. Each frame damages the slab between
                // this and the new cone height, so successive slabs tile the swept
                // cone EXACTLY - no coverage gaps at any frame rate, and never past
                // the visible tip.
                float sweptTo = 0f;

                while (elapsed < ExplosionDuration)
                {
                    ct.ThrowIfCancellationRequested();

                    elapsed += Time.deltaTime;
                    // Clamp before easing: elapsed overshoots ExplosionDuration on the
                    // final iteration, and sin() past 90 degrees DECREASES - so an
                    // unclamped t both shrinks the cone on its last frame and leaves a
                    // tip shell that no slab ever reaches.
                    float t = Mathf.Min(elapsed / ExplosionDuration, 1f);
                    float lerp = Mathf.Sin(t * PI_OVER_TWO);

                    // Scale cone
                    containerTransform.localScale =
                        Vector3.Lerp(Vector3.zero, MaxScaleVector, lerp);

                    // Parametric coupling: the damage volume is bounded by the rendered
                    // cone. Container z is the current height and container x the
                    // current base width, both driving the same mesh the player sees,
                    // so editing MaxScale or height moves visuals and damage together.
                    // Within that envelope the swept solid is the CAPSULE sweep set up
                    // above - inscribed in the cone, touching its base circle at the
                    // gape axis.
                    //
                    // This used to be a single ball per frame riding the leading base
                    // plane. Those balls are tangent to the cone - their envelope
                    // half-angle asin(k) beats the cone's atan(k) by only 0.37% at the
                    // Dolphin's min charge (k = 1/12) - so discrete frames left a
                    // scalloped shell along the mantle and a never-sampled plug at the
                    // muzzle, while the ball simultaneously over-reached a hemisphere
                    // past the visible tip. The slab is exact on both counts.
                    Vector3 scale = containerTransform.localScale;
                    float coneHeight = scale.z;

                    // Blast origin = the cone APEX (container origin, the spawn
                    // point): impact vectors radiate from it at the blast-wave speed,
                    // so every struck prism flies outward with the expanding blast.
                    bool shouldContinue = impactor?.ProcessBatchConeFrame(
                        containerTransform.position, containerTransform.forward,
                        _gapeAxisWorld,
                        sweptTo, coneHeight, tanCoreHalfAngle, tanGapePerUnit,
                        Impulse) ?? true;

                    sweptTo = Mathf.Max(sweptTo, coneHeight);

                    if (!shouldContinue)
                    {
                        // Super-shielded enemy prism physically blocks the explosion.
                        impactor?.EndBatchProcessing();
                        if (colliderExcludedForBatch) RestorePrismExclusion();
                        DestroyContainer();
                        if (this) Destroy(gameObject);
                        return;
                    }

                    // Keep the trigger capsule on the same parametric cross-section the
                    // Burst query just used (vessel impacts + physics fallback), taken at
                    // the leading base plane the collider rides: core radius across the
                    // beam, the cone's full base diameter along the gape.
                    UpdateCapsuleTrigger(tanCoreHalfAngle * coneHeight,
                                         tanHalfAngle * coneHeight);

                    // Opacity fade
                    float opacity =
                        Mathf.Clamp(
                            (MaxScaleVector - containerTransform.localScale).magnitude
                             / maxScaleMag,
                            0f,
                            1f);

                    Material.SetFloat("_Opacity", opacity);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                // A cone dense enough to exceed the per-frame budget still owes work to
                // everything it enclosed. Keep draining that backlog past the end of the
                // visual - a prism's fate is decided by whether the blast CONTAINED it,
                // never by how long the VFX happened to run.
                //
                // The blast is over as far as the world is concerned, so retire it
                // first: hide the mesh AND disable the trigger. The trigger still holds
                // vessel pairs live (only TrailBlocks is excluded), so leaving it
                // enabled would park an invisible, full-size vessel hitbox here for the
                // whole drain.
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
                if (colliderExcludedForBatch) RestorePrismExclusion();

                // Clean up when the animation finishes
                DestroyContainer();
                if (this) Destroy(gameObject);
            }
            catch (OperationCanceledException)
            {
                impactor?.EndBatchProcessing();
                // Prism exclusion deliberately NOT restored here - same reasoning as
                // the base class: a cancelled explosion stays frozen at its current
                // scale, and re-including TrailBlocks would make PhysX refilter and
                // fire OnTriggerEnter for every overlapping prism after the turn
                // ended. It is restored lazily on the next run or discarded with the
                // GameObject on reset.
                //
                // Exception: once the visual is done we are only draining bookkeeping,
                // and the frozen-cone reasoning no longer applies - the mesh is hidden
                // and the trigger is off. Leaving it would strand the container, so
                // tear it down as the normal completion path would.
                if (_visualComplete)
                {
                    DestroyContainer();
                    if (this) Destroy(gameObject);
                }
            }
            catch (Exception e)
            {
                // Safety net (mirrors the base class): any unexpected exception - e.g.
                // the container destroyed externally mid-animation - must still clean
                // up batch processing, or _useBatchProcessing stays stuck true.
                Debug.LogException(e);
                impactor?.EndBatchProcessing();
                if (colliderExcludedForBatch) RestorePrismExclusion();
                DestroyContainer();
                if (this) Destroy(gameObject);
            }
        }

        /// <summary>
        /// Vessel impacts (and the physics fallback) get the same blast-wave
        /// dynamics as the batch prism path: direction radiates from the cone
        /// APEX (the container origin), not from this transform, which sits at
        /// the cone's midpoint. Per-hit managed normalize is fine here - vessel
        /// hits are rare.
        /// </summary>
        public override Vector3 CalculateImpactVector(Vector3 impacteePosition)
        {
            Vector3 origin = coneContainer ? coneContainer.transform.position : transform.position;
            return Impulse.Along((impacteePosition - origin).normalized);
        }

        protected override void PerformResetCleanup()
        {
            DestroyContainer();
            base.PerformResetCleanup();
        }

        private void DestroyContainer()
        {
            if (coneContainer != null)
            {
                Destroy(coneContainer);
                coneContainer = null;
            }
        }
    }
}
