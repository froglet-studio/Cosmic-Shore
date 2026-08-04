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

        /// <summary>
        /// The cone's axial reach as authored on the prefab — the baseline callers scale from when
        /// they drive reach off an element level, so the art stays the source of the cone's shape.
        /// Read it from the PREFAB; on a live instance it may already carry a per-blast override.
        /// </summary>
        public float AuthoredHeight => height;

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

            _visualComplete = false;
            if (_sphereCollider) _sphereCollider.enabled = true;

            // create CTS for explosion
            explosionCts = new CancellationTokenSource();
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                // A degenerate half-angle renders a cone that damages nothing - the
                // exact failure mode this path was rewritten to eliminate, so say so
                // rather than fail silently.
                if (tanHalfAngle <= 0f)
                    Debug.LogWarning(
                        $"[AOEConicExplosion] Degenerate cone (MaxScale={MaxScale}, height={height}) " +
                        "- the blast will render but damage nothing.", this);
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

                    // Parametric coupling: the damage volume IS the rendered cone.
                    // Container z is the current height and container x the current
                    // base width, both driving the same mesh the player sees, so
                    // editing MaxScale or height moves visuals and damage together.
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
                        sweptTo, coneHeight, tanHalfAngle,
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

                    // Keep the trigger sphere on the same parametric sphere (vessel
                    // impacts + physics fallback). Unity scales a SphereCollider by
                    // its transform's LARGEST lossy axis - max(x, z) here - so divide
                    // it back out; the historical x/(2z) form assumed z >= x and lost
                    // the coupling whenever the base outgrew the height.
                    if (_sphereCollider)
                        _sphereCollider.radius =
                            scale.x / (2f * Mathf.Max(Mathf.Max(scale.x, scale.z), 0.01f));

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
                if (_sphereCollider) _sphereCollider.enabled = false;
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
