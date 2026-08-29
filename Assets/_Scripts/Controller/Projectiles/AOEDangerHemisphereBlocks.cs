using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.ScriptableObjects;
using Random = UnityEngine.Random;
using System.Linq;

namespace CosmicShore.Gameplay
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
                CSDebug.LogError("[AOEDangerHemisphereBlocks] Config is not assigned.");
                return;
            }

            ExplosionDuration = config.ExplosionDuration;
            ExplosionDelay    = config.ExplosionDelay;

            rayDirection = transform.forward;

            if (config.ScaleCurve == null || config.ScaleCurve.length == 0)
            {
            }
        }

        // --------------------------------------------------------------------
        // Explosion override – only spawns formation, then destroys itself
        // --------------------------------------------------------------------

        protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            if (!config)
            {
                CSDebug.LogError("[AOEDangerHemisphereBlocks] No config; aborting ExplodeAsync.");
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
                float depth = config.DepthScale?.Value ?? 1f;
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

        private void CreateBlock(Vector3 position,
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
                ownDomain       = Domain,
                Rotation        = rotation,
                SpawnPosition   = position,
                Scale           = targetScale,
                Velocity        = Vector3.zero,
                // Joust danger blocks are boost-off surfaces, same purpose as the Squirrel
                // tube - draw them from the fast-growing, collider-live-on-spawn Boost pool.
                PrismType       = PrismType.Boost,
                TargetTransform = null,
                OnGrowCompleted = null
            };

            var ret = prismEvent.RaiseEvent(data);
            if (!ret.SpawnedObject)
            {
                CSDebug.LogWarning("[AOEDangerHemisphereBlocks] PrismFactory returned null; spawn aborted.");
                return;
            }

            var prism = ret.SpawnedObject.GetComponent<Prism>();
            if (!prism)
            {
                CSDebug.LogWarning("[AOEDangerHemisphereBlocks] Spawned object missing Prism component.");
                return;
            }

            prism.ownerID = OwnerIdBase + blockId + position;
            prism.Domain = Domain;

            // Established spawner contract (Prism.Initialize applies the states
            // through the real pipeline): set the requested state FLAGS before
            // Initialize — it calls ActivateShield()/MakeDangerous() itself, which
            // route the proper per-domain theme materials via PrismStateManager.
            // The former MakeDangerousAsync deferred restyle wrote renderer.material
            // (banned clone, invisible on the instanced path, wrong material family)
            // and ran a bespoke GrowToScale racing the one growth engine
            // (Docs/PRISM_ANIMATION.md §3.8).
            if (prism.prismProperties != null)
            {
                if (config.MarkShielded) prism.prismProperties.IsShielded = true;
                if (config.MarkDangerous) prism.prismProperties.IsDangerous = true;
            }

            prism.TargetScale = targetScale;
            prism.SetGrowthRate(config.GrowthRate);

            prism.Initialize(Vessel?.VesselStatus?.PlayerName ?? "UnknownPlayer");

            prism.Trail = trail;
            trail.Add(prism);
        }
    }
}