using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game.Projectiles
{
    public class AOERadialBlocks : AOEConicExplosion
    {
        // Scale both ray radius and block size, in the z direction (kept from your original)
        private ElementalFloat depthScale = new(1f);

        [SerializeField] private float growthRate = .05f;

        [Header("Events")]
        [SerializeField] private PrismEventChannelWithReturnSO _prismSpawnEvent; // PrismFactory event channel

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
        [SerializeField] private float raySpread = 15f;             // spread angle in degrees
        [SerializeField] private AnimationCurve scaleCurve = null;  // defaults to linear if null
        #endregion

        private Vector3 rayDirection;
        private readonly List<Trail> trails = new();
        private CancellationTokenSource _cts;

        private string OwnerIdBase => Vessel?.VesselStatus?.Player?.PlayerUUID ?? "UnknownOwner";

        public override void Initialize(InitializeStruct initStruct)
        {
            base.Initialize(initStruct);
            rayDirection = SpawnRotation * Vector3.forward;
            scaleCurve ??= AnimationCurve.Linear(0, 1, 1, 0.5f);
        }

        private void OnDisable()
        {
            CancelExplosion();
        }

        /// <summary>
        /// Start the radial block burst (UniTask-based, no coroutines).
        /// </summary>
        public void BeginExplosion()
        {
            CancelExplosion();
            _cts = new CancellationTokenSource();
            ExplodeAsync(_cts.Token).Forget();
        }

        /// <summary>
        /// Cancel current sequence.
        /// </summary>
        public void CancelExplosion()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        // ---------------- Internals ----------------

        private async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            try
            {
                // Wait for primary + secondary delay before creating rays
                float totalDelay = Mathf.Max(0f, ExplosionDelay) + Mathf.Max(0f, SecondaryExplosionDelay);
                if (totalDelay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(totalDelay), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

                // Create trails and rays
                for (int ray = 0; ray < numberOfRays; ray++)
                {
                    ct.ThrowIfCancellationRequested();

                    trails.Add(new Trail());
                    CreateRay(ray, trails[ray], ct);

                    // Yield a frame between rays to smooth spikes
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on cancel/disable
            }
        }

        private void CreateRay(int rayIndex, Trail trail, CancellationToken ct)
        {
            float angleStep = 360f / Mathf.Max(1, numberOfRays);
            float rayAngle = rayIndex * angleStep; // kept for future use if needed

            for (int b = 0; b < blocksPerRay; b++)
            {
                if (ct.IsCancellationRequested) return;

                // Choose random radius within band, scale & size follow curve
                float radius = UnityEngine.Random.Range(minRadius, maxRadius);
                float tNorm = Mathf.InverseLerp(0f, maxRadius, radius);
                float scaleMultiplier = (scaleCurve != null) ? scaleCurve.Evaluate(tNorm) : 1f;
                Vector3 blockScale = baseBlockScale * scaleMultiplier;

                // Spread a bit around the main ray direction
                float spreadDeg = raySpread;
                float rotationAroundRayDeg = UnityEngine.Random.Range(0f, 360f);

                // spread axis: perpendicular to rayDirection (fallback if rayDirection//up)
                Vector3 axis = Vector3.Cross(rayDirection, Vector3.up);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(rayDirection, Vector3.right);
                axis.Normalize();

                Quaternion spreadRotation = Quaternion.AngleAxis(spreadDeg, axis);
                Quaternion rotationAround = Quaternion.AngleAxis(rotationAroundRayDeg, rayDirection);
                Vector3 spreadDirection = rotationAround * spreadRotation * rayDirection;

                Vector3 position = transform.position + spreadDirection * radius;

                // Up vector: align with transform’s up to keep a stable roll
                Vector3 up = transform.up;

                CreateBlock(position, spreadDirection, up, $"::Radial::{rayIndex}::{b}", trail, blockScale);
            }
        }

        /// <summary>
        /// Requests an Interactive Prism via PrismFactory (event channel) and configures it.
        /// </summary>
        private Prism CreateBlock(Vector3 position, Vector3 forward, Vector3 up, string blockId, Trail trail, Vector3 scale)
        {
            if (_prismSpawnEvent == null)
            {
                Debug.LogError("[AOERadialBlocks] Prism spawn event channel is not assigned.");
                return null;
            }

            var data = new PrismEventData
            {
                ownDomain       = Domain,
                Rotation        = Quaternion.LookRotation(forward, up),
                SpawnPosition   = position,
                Scale           = scale,                  // per-block scale
                Velocity        = Vector3.zero,
                PrismType       = PrismType.Interactive,  // interactive prism
                TargetTransform = null,
                OnGrowCompleted = null
            };

            var ret = _prismSpawnEvent.RaiseEvent(data);
            if (!ret.SpawnedObject)
            {
                Debug.LogWarning("[AOERadialBlocks] PrismFactory returned null. Spawn aborted.");
                return null;
            }

            var block = ret.SpawnedObject.GetComponent<Prism>();
            if (!block)
            {
                Debug.LogWarning("[AOERadialBlocks] Spawned object has no Prism component.");
                return null;
            }

            // Ownership tag & unique id
            block.ownerID = OwnerIdBase + blockId + position;

            // Growth & shield
            block.TargetScale = scale;
            block.growthRate  = growthRate;
            if (shielded) block.prismProperties.IsShielded = true;
            
            // Initialize gameplay side
            block.Initialize(Vessel?.VesselStatus?.PlayerName ?? "UnknownPlayer");

            // Register with trail
            block.Trail = trail;
            trail.Add(block);

            return block;
        }
    }
}
