using System;
using System.Threading;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utilities;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CosmicShore.Game.Projectiles
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

            rayDirection = transform.forward;
            scaleCurve ??= AnimationCurve.Linear(0, 1, 1, 0.5f);
        }

        // ----------------------------------------------------------------------

        protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            try
            {
                // FIRST: Run the conic explosion visuals from parent
                base.ExplodeAsync(ct).Forget();

                // wait: primary delay + secondary delay
                float wait = Mathf.Max(0f, ExplosionDelay) + Mathf.Max(0f, SecondaryExplosionDelay);
                if (wait > 0f)
                    await UniTask.Delay((int)(wait * 1000f), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

                trails.Clear();

                // Spawn each ray over multiple frames
                for (int ray = 0; ray < numberOfRays; ray++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!this) return; // guard against base conic animation destroying gameObject

                    Trail trail = new Trail();
                    trails.Add(trail);

                    CreateRay(ray, trail);

                    // Small frame delay to distribute work
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Explosion cancelled
            }
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

                Vector3 pos = transform.position + spreadDir * radius;
                Vector3 up  = transform.up;

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
                Debug.LogError("[AOERadialBlocks] Prism spawn event channel is not assigned.");
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
                Debug.LogWarning("[AOERadialBlocks] PrismFactory returned null; spawn aborted.");
                return null;
            }

            Prism prism = ret.SpawnedObject.GetComponent<Prism>();
            if (!prism)
            {
                Debug.LogWarning("[AOERadialBlocks] Spawned object missing Prism component.");
                return null;
            }

            prism.ownerID = OwnerIdBase + blockId + position;
            prism.Domain = Domain;

            if (shielded)
                prism.prismProperties.IsShielded = true;

            // Start at zero scale
            prism.transform.localScale = Vector3.zero;

            // built-in growth (if Prism supports it)
            prism.TargetScale = targetScale;
            prism.growthRate  = growthRate;

            // fallback grower in case Prism doesn't auto grow
            GrowToScale(prism.transform, targetScale, growthRate).Forget();

            prism.Initialize(Vessel?.VesselStatus?.PlayerName ?? "UnknownPlayer");

            prism.Trail = trail;
            trail.Add(prism);

            return prism;
        }

        // ----------------------------------------------------------------------
        // Block growth without coroutine
        // ----------------------------------------------------------------------

        private static async UniTaskVoid GrowToScale(Transform tr, Vector3 target, float rate)
        {
            rate = Mathf.Max(1e-5f, rate);

            while (tr && (tr.localScale - target).sqrMagnitude > 0.0001f)
            {
                tr.localScale = Vector3.MoveTowards(tr.localScale, target, rate);
                await UniTask.Yield();
            }

            if (tr)
                tr.localScale = target;
        }
    }
}
