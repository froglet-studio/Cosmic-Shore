using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class SpawnableRings : SpawnableBase
    {
        [Header("Ring Configuration")]
        [SerializeField] Prism prism;
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
            return System.HashCode.Combine(ringCount, prismsPerRing, ringRadius,
                System.HashCode.Combine(ringSpacing, prismScale, prismAngle, isDangerous, isShielded));
        }

        public override GameObject Spawn(int intensity = 1)
        {
            intensityLevel = intensity;
            prismAngle = intensity * 0.3f;
            trails.Clear();

            GameObject container = new GameObject($"Rings_{name}");
            container.transform.SetParent(transform, false);
            container.transform.SetPositionAndRotation(transform.position, transform.rotation);

            for (int ringIndex = 0; ringIndex < ringCount; ringIndex++)
            {
                Vector3 ringCenter = transform.position + transform.forward * (ringIndex * ringSpacing + initialOffset);
                CreateRing(container, ringIndex, ringCenter);
            }

            return container;
        }

        void CreateRing(GameObject container, int ringIndex, Vector3 ringCenter)
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

                string ownerId = $"{container.name}::R{ringIndex}::P{i}";

                var block = Instantiate(prism, container.transform);
                block.ChangeTeam(domain);
                block.ownerID = ownerId;

                Quaternion rotation = Quaternion.LookRotation(lookDirection, transform.up);
                block.transform.SetPositionAndRotation(adjustedPosition, rotation);

                block.TargetScale = prismScale;
                block.Trail = trail;

                if (isDangerous) block.MakeDangerous();
                if (isShielded) block.ActivateShield();

                block.Initialize();
                trail.Add(block);
            }
        }
    }
}
