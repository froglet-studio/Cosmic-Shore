using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Utilities;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CosmicShore.Game.Projectiles
{
    public sealed class AOEDangerHemisphereBlocks : AOEExplosion
    {
        [Header("Config")]
        [SerializeField] private DangerHemisphereConfigSO config;

        private Vector3 rayDirection;
        private readonly List<Trail> trails = new();

        private string OwnerIdBase =>
            Vessel?.VesselStatus?.Player?.PlayerUUID ?? "UnknownOwner";

        // --------------------------------------------------------------------
        // Initialization
        // --------------------------------------------------------------------

        public override void Initialize(InitializeStruct initStruct)
        {
            base.Initialize(initStruct);

            if (!config)
            {
                Debug.LogError("[AOEDangerHemisphereBlocks] Config is not assigned.");
                return;
            }

            // Push timing from config into the base AOEExplosion fields
            ExplosionDuration = config.ExplosionDuration;
            ExplosionDelay    = config.ExplosionDelay;

            // Interpret MaxScale as some overall bound if you want
            if (config.MaxRadius <= 0f && MaxScale > 0f)
            {
                // If config maxRadius is not set, derive from base MaxScale
                // (optional – can be removed if you always set MaxRadius in config).
            }

            rayDirection = transform.forward;

            // Ensure curve is never null at runtime
            if (config.ScaleCurve == null || config.ScaleCurve.length == 0)
            {
                // This won't modify the asset; just guard usage later.
                // We'll just use a linear fallback when we read it.
            }
        }

        // --------------------------------------------------------------------
        // Explosion override – only spawns formation, then destroys itself
        // --------------------------------------------------------------------

        protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            if (!config)
            {
                Debug.LogError("[AOEDangerHemisphereBlocks] No config; aborting ExplodeAsync.");
                return;
            }

            try
            {
                if (ExplosionDelay > 0f)
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(ExplosionDelay),
                        DelayType.DeltaTime,
                        PlayerLoopTiming.Update,
                        ct);

                trails.Clear();

                for (int ray = 0; ray < config.NumberOfRays; ray++)
                {
                    ct.ThrowIfCancellationRequested();

                    var trail = new Trail();
                    trails.Add(trail);

                    CreateRay(ray, trail, ct);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            finally
            {
                if (!ct.IsCancellationRequested && this != null)
                    Destroy(gameObject);
            }
        }

        // --------------------------------------------------------------------
        // Ray & block creation (hemisphere-limited)
        // --------------------------------------------------------------------

        private void CreateRay(int rayIndex, Trail trail, CancellationToken ct)
        {
            if (!config) return;

            float angleStep = 360f / Mathf.Max(1, config.NumberOfRays);
            float baseAngle = rayIndex * angleStep;

            Vector3 axis = Vector3.Cross(rayDirection, Vector3.up);
            if (axis.sqrMagnitude < 1e-6f)
                axis = Vector3.Cross(rayDirection, Vector3.right);
            axis.Normalize();

            for (int b = 0; b < config.BlocksPerRay; b++)
            {
                if (ct.IsCancellationRequested)
                    return;

                float minR = config.MinRadius;
                float maxR = config.MaxRadius > 0f ? config.MaxRadius : config.MinRadius + 1f;

                float radius = Random.Range(minR, maxR);
                float tNorm  = Mathf.InverseLerp(0f, maxR, radius);

                var curve = config.ScaleCurve;
                float scaleMul = (curve != null && curve.length > 0)
                    ? curve.Evaluate(tNorm)
                    : Mathf.Lerp(1f, 0.5f, tNorm); // fallback

                // Depth scale
                float depth = config.DepthScale != null ? config.DepthScale.Value : 1f;
                Vector3 baseScale = config.BaseBlockScale;
                baseScale.z *= depth;

                Vector3 targetScale = baseScale * scaleMul;

                Quaternion aroundForward = Quaternion.AngleAxis(baseAngle, rayDirection);
                float randomTilt = Random.Range(-config.RaySpread, config.RaySpread);
                Quaternion tiltRot = Quaternion.AngleAxis(randomTilt, axis);

                Vector3 dir = aroundForward * tiltRot * rayDirection;

                // Ensure we stay in the forward hemisphere
                if (Vector3.Dot(dir, rayDirection) < 0f)
                    dir = -dir;

                Vector3 position = transform.position + dir * radius;
                Vector3 up       = transform.up;

                CreateBlock(
                    position,
                    dir,
                    up,
                    $"::DangerHemisphere::{rayIndex}::{b}",
                    trail,
                    targetScale
                );
            }
        }

        // --------------------------------------------------------------------
        // Prism spawning & configuration
        // --------------------------------------------------------------------

        private Prism CreateBlock(
            Vector3 position,
            Vector3 forward,
            Vector3 up,
            string blockId,
            Trail trail,
            Vector3 targetScale)
        {
            var prismEvent = config.PrismSpawnEvent;
            SafeLookRotation.TryGet(forward, up, out var rotation, this);

            var data = new PrismEventData
            {
                // Domain is passed to the factory if it needs it,
                // but we do NOT assign prism.Domain on the instance.
                ownDomain       = Domain,
                Rotation        = rotation,
                SpawnPosition   = position,
                Scale           = targetScale,
                Velocity        = Vector3.zero,
                PrismType       = PrismType.Interactive,
                TargetTransform = null,
                OnGrowCompleted = null
            };

            var ret = prismEvent.RaiseEvent(data);
            if (!ret.SpawnedObject)
            {
                Debug.LogWarning("[AOEDangerHemisphereBlocks] PrismFactory returned null; spawn aborted.");
                return null;
            }

            var prism = ret.SpawnedObject.GetComponent<Prism>();
            if (!prism)
            {
                Debug.LogWarning("[AOEDangerHemisphereBlocks] Spawned object missing Prism component.");
                return null;
            }

            prism.ownerID = OwnerIdBase + blockId + position;
            prism.TargetScale = targetScale;
            prism.growthRate  = config.GrowthRate;
            prism.Initialize(Vessel?.VesselStatus?.PlayerName ?? "UnknownPlayer");

            MakeDangerousAsync(
                prism,
                targetScale,
                config.GrowthRate,
                config.DangerMaterial,
                config.MarkShielded,
                config.MarkDangerous
            ).Forget();

            prism.Trail = trail;
            trail.Add(prism);

            return prism;
        }

        private static async UniTaskVoid MakeDangerousAsync(
            Prism prism,
            Vector3 targetScale,
            float growthRate,
            Material dangerMat,
            bool markShielded,
            bool markDangerous)
        {
            if (!prism) return;

            // Wait one frame so any PrismTeamManager / StateManager / Init logic finishes
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            if (!prism) return;

            // Flags on PrismProperties
            if (prism.prismProperties != null)
            {
                if (markShielded)
                    prism.prismProperties.IsShielded = true;

                if (markDangerous)
                    prism.prismProperties.IsDangerous = true;
            }

            // Apply danger material to all renderers
            if (dangerMat != null)
            {
                var renderers = prism.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers)
                {
                    if (!r) continue;
                    r.material = dangerMat;
                }
            }

            var tr = prism.transform;
            if (!tr) return;

            tr.localScale = Vector3.zero;
            await GrowToScale(tr, targetScale, growthRate);
        }

        private static async UniTask GrowToScale(Transform tr, Vector3 target, float rate)
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