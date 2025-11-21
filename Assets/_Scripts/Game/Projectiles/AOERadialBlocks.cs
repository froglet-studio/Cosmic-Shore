using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utilities;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOERadialBlocks : AOEConicExplosion
    {
        // Scale both ray radius and block size, in the z direction (kept behavior)
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

        private string OwnerIdBase => Vessel?.VesselStatus?.Player?.PlayerUUID ?? "UnknownOwner";

        public override void Initialize(InitializeStruct initStruct)
        {
            base.Initialize(initStruct);

            // Match the old behavior: apply depth scaling up front
            baseBlockScale.z *= depthScale.Value;
            maxRadius        *= depthScale.Value;

            // Direction and curve defaults
            rayDirection = transform.forward;
            scaleCurve ??= AnimationCurve.Linear(0, 1, 1, 0.5f);

            // (Optional) if you had elemental bindings previously
            // BindElementalFloats(Ship); // uncomment if needed in your project
        }

        /// <summary>
        /// IMPORTANT: restore coroutine path so Detonate() from base calls into this.
        /// </summary>
        protected override IEnumerator ExplodeCoroutine()
        {
            // run the base cone (visual) first
            StartCoroutine(base.ExplodeCoroutine());

            // wait: primary + secondary delay
            float wait = Mathf.Max(0f, ExplosionDelay) + Mathf.Max(0f, SecondaryExplosionDelay);
            if (wait > 0f) yield return new WaitForSeconds(wait);

            // Build trails & rays
            trails.Clear();

            for (int ray = 0; ray < numberOfRays; ray++)
            {
                var trail = new Trail();
                trails.Add(trail);
                CreateRay(ray, trail);

                // small yield to distribute load like the UniTask version did
                yield return null;
            }
        }

        private void CreateRay(int rayIndex, Trail trail)
        {
            float angleStep = 360f / Mathf.Max(1, numberOfRays);
            float rayAngle  = rayIndex * angleStep; // kept for potential future use

            for (int b = 0; b < blocksPerRay; b++)
            {
                float radius = Random.Range(minRadius, maxRadius);
                float tNorm  = Mathf.InverseLerp(0f, maxRadius, radius);
                float scaleMult = (scaleCurve != null) ? scaleCurve.Evaluate(tNorm) : 1f;
                Vector3 blockScale = baseBlockScale * scaleMult;

                // spread axis: perpendicular to rayDirection (fallback if parallel to up)
                Vector3 axis = Vector3.Cross(rayDirection, Vector3.up);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(rayDirection, Vector3.right);
                axis.Normalize();

                float spreadDeg            = raySpread;
                float rotationAroundRayDeg = Random.Range(0f, 360f);

                Quaternion spreadRotation = Quaternion.AngleAxis(spreadDeg, axis);
                Quaternion rotationAround = Quaternion.AngleAxis(rotationAroundRayDeg, rayDirection);
                Vector3 spreadDirection   = rotationAround * spreadRotation * rayDirection;

                Vector3 position = transform.position + spreadDirection * radius;
                Vector3 up       = transform.up;

                CreateBlock(position, spreadDirection, up, $"::Radial::{rayIndex}::{b}", trail, blockScale);
            }
        }

        /// <summary>
        /// Spawns an Interactive Prism via PrismFactory and forces growth like old TrailBlock behavior.
        /// </summary>
        private Prism CreateBlock(Vector3 position, Vector3 forward, Vector3 up, string blockId, Trail trail, Vector3 targetScale)
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

            var prism = ret.SpawnedObject.GetComponent<Prism>();
            if (!prism)
            {
                Debug.LogWarning("[AOERadialBlocks] Spawned object has no Prism component.");
                return null;
            }

            // Owner + gameplay flags
            prism.ownerID = OwnerIdBase + blockId + position;
            if (shielded) prism.prismProperties.IsShielded = true;
            prism.Domain = Domain;
            // Make sure it starts at zero and grows (like TrailBlock used to)
            var tr = prism.transform;
            tr.localScale = Vector3.zero; // force from-zero growth

            // If your Prism already grows when TargetScale & growthRate are set, keep these:
            prism.TargetScale = targetScale;
            prism.growthRate  = growthRate;

            // If not, run a local grower to guarantee the effect:
            StartCoroutine(GrowToScale(tr, targetScale, growthRate));

            // Initialize & trail bookkeeping
            prism.Initialize(Vessel?.VesselStatus?.PlayerName ?? "UnknownPlayer");
            prism.Trail = trail;
            trail.Add(prism);

            return prism;
        }

        /// <summary>
        /// Local fallback growth (frame-based, non-alloc). Matches old "grow after exploding".
        /// </summary>
        private static IEnumerator GrowToScale(Transform tr, Vector3 target, float ratePerFrame)
        {
            // safeguard
            ratePerFrame = Mathf.Max(1e-5f, ratePerFrame);

            while (tr && (tr.localScale - target).sqrMagnitude > 0.0001f)
            {
                // Move scale toward target at 'growthRate' per frame (like the old TrailBlock)
                tr.localScale = Vector3.MoveTowards(tr.localScale, target, ratePerFrame);
                yield return null;
            }

            if (tr) tr.localScale = target;
        }
    }
}
