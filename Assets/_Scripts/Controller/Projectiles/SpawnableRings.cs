using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class SpawnableRings : SpawnableBase
    {
        [Header("Ring Configuration")]
        [Tooltip("Prism spawn channel → PrismFactory → the FastGrowPrism pool. Ring prisms are " +
                 "pooled (no Instantiate in the impact frame) and pool-parented in world space, " +
                 "so the spawner can be destroyed freely without touching them.")]
        [SerializeField] PrismEventChannelWithReturnSO _prismSpawnEvent;
        [SerializeField] int ringCount = 3;
        [SerializeField] int prismsPerRing = 8;
        [SerializeField] float ringRadius = 20f;
        [SerializeField] float ringSpacing = 15f;
        float initialOffset = 8;

        [Header("Prism Configuration")]
        [SerializeField] Vector3 prismScale = new Vector3(4, 4, 9);
        float prismAngle = 0f;

        [Header("Prism Properties")]
        [SerializeField] bool isDangerous = false;
        [SerializeField] bool isShielded = false;

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(seed, ringCount, prismsPerRing, ringRadius,
                System.HashCode.Combine(ringSpacing, prismScale, prismAngle, isDangerous, isShielded));
        }

        // Monotonic per-Spawn salt so ring prisms from different detonations never
        // share ownerIDs (the old container-name IDs collided across pickups).
        static int s_spawnSerial;

        public override GameObject Spawn(int intensity = 1)
        {
            if (_prismSpawnEvent == null)
            {
                CSDebug.LogError("[SpawnableRings] Prism spawn event channel is not assigned.");
                return gameObject;
            }

            intensityLevel = intensity;
            prismAngle = intensity * 0.3f;
            trails.Clear();

            int spawnSerial = ++s_spawnSerial;
            for (int ringIndex = 0; ringIndex < ringCount; ringIndex++)
            {
                Vector3 ringCenter = transform.position + transform.forward * (ringIndex * ringSpacing + initialOffset);
                CreateRing(ringIndex, ringCenter, spawnSerial);
            }

            return gameObject;
        }

        void CreateRing(int ringIndex, Vector3 ringCenter, int spawnSerial)
        {
            Trail trail = new Trail();
            trails.Add(trail);

            float lookOffsetZ = Mathf.Tan(prismAngle * Mathf.Deg2Rad) * ringRadius;
            Vector3 lookTarget = ringCenter + transform.forward * lookOffsetZ;
            float halfLength = prismScale.z / 2f;

            for (int i = 0; i < prismsPerRing; i++)
            {
                float angle = (i / (float)prismsPerRing) * Mathf.PI * 2 + Mathf.PI * 0.5f;
                Vector3 position = ringCenter
                    + transform.right * (Mathf.Cos(angle) * ringRadius)
                    + transform.up * (Mathf.Sin(angle) * ringRadius);

                // Tip closest to ring center (using base direction at prismAngle=0)
                Vector3 baseLookDir = (ringCenter - position).normalized;
                Vector3 tipPosition = position + baseLookDir * halfLength;

                // Actual look direction from the fixed tip toward the angled target
                Vector3 lookDirection = (lookTarget - tipPosition).normalized;

                // Offset center back from the tip so the tip stays pinned
                Vector3 adjustedPosition = tipPosition - lookDirection * halfLength;

                Quaternion rotation = Quaternion.LookRotation(lookDirection, transform.up);

                var ret = _prismSpawnEvent.RaiseEvent(new PrismEventData
                {
                    ownDomain     = domain,
                    Rotation      = rotation,
                    SpawnPosition = adjustedPosition,
                    Scale         = prismScale,
                    Velocity      = Vector3.zero,
                    PrismType     = PrismType.FastGrow,
                });
                if (!ret.SpawnedObject) continue; // pool drained a dead entry — skip this slot

                var block = ret.SpawnedObject.GetComponent<Prism>();
                if (!block) continue;

                block.ChangeTeam(domain);
                block.ownerID = $"Rings::{spawnSerial}::R{ringIndex}::P{i}";
                block.TargetScale = prismScale;
                block.Trail = trail;

                // Pooled reuse: prismProperties flags persist across pool lives and
                // Initialize re-engages from them — write BOTH ways so a prior life's
                // shield/danger state can never leak into this one, and the shield
                // engages exactly once (inside Initialize) instead of the old
                // pre-Initialize call that double-engaged.
                block.prismProperties.IsShielded = isShielded;
                block.prismProperties.IsDangerous = isDangerous;

                block.Initialize();
                trail.Add(block);
            }
        }
    }
}
