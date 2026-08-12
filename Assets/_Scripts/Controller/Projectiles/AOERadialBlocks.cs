using System;
using System.Threading;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.ScriptableObjects;
using Random = UnityEngine.Random;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class AOERadialBlocks : AOEConicExplosion
    {
        // Scale both ray radius and block size in Z
        private ElementalFloat depthScale = new(1f);

        [SerializeField] private float growthRate = .05f;

        [Header("Events")]
        [SerializeField] private PrismEventChannelWithReturnSO _prismSpawnEvent;

        #region Block Creation
        [Header("Block Creation")]
        [SerializeField] private Vector3 baseBlockScale = new Vector3(10f, 5f, 5f);
        [SerializeField] private bool shielded = true;
        #endregion

        #region Explosion Parameters
        [Header("Explosion Parameters")]
        [SerializeField] private float SecondaryExplosionDelay = 0.3f;
        [SerializeField] private int numberOfRays = 16;
        [SerializeField] private int blocksPerRay = 5;
        [SerializeField] private float maxRadius = 50f;
        [SerializeField] private float minRadius = 10f;
        [SerializeField] private float raySpread = 15f;
        [SerializeField] private AnimationCurve scaleCurve = null;
        #endregion

        private Vector3 rayDirection;
        private readonly List<Trail> trails = new();

        private string OwnerIdBase => Vessel?.VesselStatus?.Player?.PlayerUUID ?? "UnknownOwner";

        public override void Initialize(InitializeStruct initStruct)
        {
            base.Initialize(initStruct);

            baseBlockScale.z *= depthScale.Value;
            maxRadius        *= depthScale.Value;

            rayDirection = coneContainer.transform.forward;
            scaleCurve ??= AnimationCurve.Linear(0, 1, 1, 0.5f);
        }

        // ----------------------------------------------------------------------

        protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            try
            {
                // CREATION-ONLY: the skyburst's destructive work is the spherical
                // AOEExplosion the detonator spawns alongside this object. Running the
                // parent's conic sweep here as well produced a SECOND destructive
                // explosion whose live trigger collider kept re-hitting / re-shielding
                // prisms — including the radial blocks laid below — for the full
                // ExplosionDuration (authored 50 s on the skyburst prefab: the
                // "explosion that never resolves"). This object now only deposits the
                // radial prism rays, then retires.
                DisableConicExplosion();

                // wait: primary delay + secondary delay
                float wait = Mathf.Max(0f, ExplosionDelay) + Mathf.Max(0f, SecondaryExplosionDelay);
                if (wait > 0f)
                    await UniTask.Delay((int)(wait * 1000f), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

                trails.Clear();

                // Spawn each ray over multiple frames
                for (int ray = 0; ray < numberOfRays; ray++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!this) return;

                    Trail trail = new Trail();
                    trails.Add(trail);

                    CreateRay(ray, trail);

                    // Small frame delay to distribute work
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                // All rays deposited. Block growth is owned by the prisms themselves
                // (Prism.Initialize bloom + a detached static fallback grower), so the
                // spawner can go away. Initialize reparented us under the runtime
                // coneContainer — destroy that so nothing leaks.
                if (this)
                    Destroy(coneContainer ? coneContainer : gameObject);
            }
            catch (OperationCanceledException)
            {
                // Explosion cancelled
            }
        }

        /// <summary>
        /// Kills the inherited destructive-explosion machinery: the trigger collider and
        /// impactor (so nothing gets re-hit or re-shielded) and the cone sweep visual.
        /// The base ExplodeAsync is never run, so nothing scales or animates either.
        /// </summary>
        private void DisableConicExplosion()
        {
            if (TryGetComponent<Collider>(out var triggerCollider))
                triggerCollider.enabled = false;
            if (TryGetComponent<ExplosionImpactor>(out var impactor))
                impactor.enabled = false;
            if (TryGetComponent<MeshRenderer>(out var coneVisual))
                coneVisual.enabled = false;
        }

        // ----------------------------------------------------------------------

        private void CreateRay(int rayIndex, Trail trail)
        {
            float angleStep = 360f / Mathf.Max(1, numberOfRays);

            for (int b = 0; b < blocksPerRay; b++)
            {
                float radius = Random.Range(minRadius, maxRadius);
                float tNorm  = Mathf.InverseLerp(0f, maxRadius, radius);
                float scaleMultiplier = scaleCurve.Evaluate(tNorm);

                Vector3 finalScale = baseBlockScale * scaleMultiplier;

                // Spread axis
                Vector3 axis = Vector3.Cross(rayDirection, Vector3.up);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(rayDirection, Vector3.right);
                axis.Normalize();

                float spreadDeg = raySpread;
                float randomRot = Random.Range(0f, 360f);

                Quaternion spreadRot   = Quaternion.AngleAxis(spreadDeg, axis);
                Quaternion aroundRay   = Quaternion.AngleAxis(randomRot, rayDirection);
                Vector3 spreadDir      = aroundRay * spreadRot * rayDirection;

                Vector3 pos = coneContainer.transform.position + spreadDir * radius;
                Vector3 up  = coneContainer.transform.up;

                CreateBlock(pos, spreadDir, up, $"::Radial::{rayIndex}::{b}", trail, finalScale);
            }
        }

        // ----------------------------------------------------------------------

        private Prism CreateBlock(
            Vector3 position,
            Vector3 forward,
            Vector3 up,
            string blockId,
            Trail trail,
            Vector3 targetScale)
        {
            if (!_prismSpawnEvent)
            {
                CSDebug.LogError("[AOERadialBlocks] Prism spawn event channel is not assigned.");
                return null;
            }

            SafeLookRotation.TryGet(forward, up, out var rotation, this);

            var data = new PrismEventData
            {
                ownDomain       = Domain,
                Rotation        = rotation,
                SpawnPosition   = position,
                Scale           = targetScale,
                Velocity        = Vector3.zero,
                PrismType       = PrismType.Interactive,
                TargetTransform = null,
                OnGrowCompleted = null
            };

            var ret = _prismSpawnEvent.RaiseEvent(data);
            if (!ret.SpawnedObject)
            {
                CSDebug.LogWarning("[AOERadialBlocks] PrismFactory returned null; spawn aborted.");
                return null;
            }

            Prism prism = ret.SpawnedObject.GetComponent<Prism>();
            if (!prism)
            {
                CSDebug.LogWarning("[AOERadialBlocks] Spawned object missing Prism component.");
                return null;
            }

            prism.ownerID = OwnerIdBase + blockId + position;
            prism.Domain = Domain;

            if (shielded)
                prism.prismProperties.IsShielded = true;

            // The one growth engine (Docs/PRISM_ANIMATION.md): TargetScale is the
            // initial condition; SetGrowthRate pushes the rate through to the
            // animator (a bare growthRate field write is dead on pooled prisms —
            // the field is only read in Prism.Awake). The former bespoke
            // GrowToScale fallback raced the real engine with per-frame
            // localScale writes that never reached the instanced render path.
            prism.TargetScale = targetScale;
            prism.SetGrowthRate(growthRate);

            prism.Initialize(Vessel?.VesselStatus?.PlayerName ?? "UnknownPlayer");

            prism.Trail = trail;
            trail.Add(prism);

            return prism;
        }

    }
}
