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
            MaxScaleVector = new Vector3(MaxScale, MaxScale, height);

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

                if (TryGetComponent<MeshRenderer>(out var meshRenderer))
                    meshRenderer.material = Material;

                float elapsed = 0f;

                var sphereCol = GetComponent<SphereCollider>();
                var containerTransform = coneContainer.transform;
                float maxScaleMag = MaxScaleVector.magnitude; // invariant for this explosion - hoist out of the per-frame loop

                while (elapsed < ExplosionDuration)
                {
                    ct.ThrowIfCancellationRequested();

                    elapsed += Time.deltaTime;
                    float t = elapsed / ExplosionDuration;
                    float lerp = Mathf.Sin(t * PI_OVER_TWO);

                    // Scale cone
                    containerTransform.localScale =
                        Vector3.Lerp(Vector3.zero, MaxScaleVector, lerp);

                    // Parametric coupling: the damage sphere IS the rendered cone's
                    // leading cross-section - centered on the growing cone's base plane
                    // (container z = current height), radius = half the current base
                    // width (container x). Growth and translation both derive from the
                    // same container scale that shapes the mesh, so a sphere sweeping
                    // this path covers exactly the conic volume the player sees, and
                    // editing MaxScale or height moves visuals and damage together.
                    Vector3 scale = containerTransform.localScale;
                    float sphereWorldRadius = scale.x * 0.5f;
                    Vector3 sphereWorldCenter =
                        containerTransform.position + containerTransform.forward * scale.z;

                    bool shouldContinue = impactor?.ProcessBatchFrame(
                        sphereWorldCenter, sphereWorldRadius, speed, Inertia) ?? true;

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
                    if (sphereCol)
                        sphereCol.radius =
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
