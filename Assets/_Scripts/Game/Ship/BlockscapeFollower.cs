using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore.Core
{
    public class BlockscapeFollower : MonoBehaviour
    {
        public TrailBlock AttachedTrailBlock { get; private set; }
        [SerializeField] private float FriendlyTerrainSpeed;
        [SerializeField] private float HostileTerrainSpeed;
        [SerializeField] private float DestroyedTerrainSpeed;
        [HideInInspector] public float Throttle;
        private Vector3 currentSurfaceNormal = Vector3.up;

        private IShip ship;
        private Teams team;
        private ShipStatus shipData;
        float SurfaceOffset = 1f;

        private void Start()
        {
            ship = GetComponent<IShip>();
            team = ship.ShipStatus.Team;
            shipData = GetComponent<ShipStatus>();
        }

        public void Attach(TrailBlock trailBlock)
        {
            AttachedTrailBlock = trailBlock;
            currentSurfaceNormal = DetermineCollidingSurface(transform, AttachedTrailBlock.transform);
        }

        public void Detach()
        {
            AttachedTrailBlock = null;
        }

        void UpdateSurfaceNormalIfNeeded(ref Vector3 projectedMovement)
        {
            Vector3 localPosition = AttachedTrailBlock.transform.InverseTransformPoint(transform.position + projectedMovement);
            Vector3 extents = AttachedTrailBlock.transform.localScale / 2;

            if (Mathf.Abs(localPosition.x) > extents.x || Mathf.Abs(localPosition.y) > extents.y || Mathf.Abs(localPosition.z) > extents.z)
            {
                currentSurfaceNormal = DetermineNewSurfaceNormalAfterCrossingEdge(localPosition, extents, currentSurfaceNormal);
                // Adjust the position to the edge of the new surface
                AdjustPositionForNewSurface(ref localPosition, extents, currentSurfaceNormal);
                // Convert back to world space after adjustment
                transform.position = AttachedTrailBlock.transform.TransformPoint(localPosition);
                // Re-project the movement onto the new surface normal
                projectedMovement = Vector3.ProjectOnPlane(transform.forward * Throttle * GetTerrainAwareBlockSpeed(AttachedTrailBlock) * Time.deltaTime, currentSurfaceNormal);
            }
        }

        void AdjustPositionForNewSurface(ref Vector3 localPosition, Vector3 extents, Vector3 currentNormal)
        {
            // Normalize the excess to keep the entity within the cube's bounds
            if (Mathf.Abs(currentNormal.x) > 0.5f) // Transitioned to a side on the X-axis
            {
                localPosition.x = Mathf.Sign(currentNormal.x) * extents.x;
            }
            else if (Mathf.Abs(currentNormal.y) > 0.5f) // Transitioned to a side on the Y-axis
            {
                localPosition.y = Mathf.Sign(currentNormal.y) * extents.y;
            }
            else if (Mathf.Abs(currentNormal.z) > 0.5f) // Transitioned to a side on the Z-axis
            {
                localPosition.z = Mathf.Sign(currentNormal.z) * extents.z;
            }

            // Ensure the entity is slightly above the surface to avoid clipping
            localPosition += currentNormal * SurfaceOffset;
        }



        Vector3 DetermineNewSurfaceNormalAfterCrossingEdge(Vector3 localPosition, Vector3 extents, Vector3 currentNormal)
        {
            // First, determine the axis of crossing based on which component of localPosition exceeds extents the most.
            float xExcess = Mathf.Abs(localPosition.x) - extents.x;
            float yExcess = Mathf.Abs(localPosition.y) - extents.y;
            float zExcess = Mathf.Abs(localPosition.z) - extents.z;

            // Determine the direction of crossing based on the maximum excess.
            if (xExcess > yExcess && xExcess > zExcess)
            {
                // Crossed on the X-axis.
                return localPosition.x > 0 ? AttachedTrailBlock.transform.right : -AttachedTrailBlock.transform.right;
            }
            else if (yExcess > zExcess)
            {
                // Crossed on the Y-axis.
                return localPosition.y > 0 ? AttachedTrailBlock.transform.up : -AttachedTrailBlock.transform.up;
            }
            else
            {
                // Crossed on the Z-axis.
                return localPosition.z > 0 ? AttachedTrailBlock.transform.forward : -AttachedTrailBlock.transform.forward;
            }
        }


        public void RideTheTrail()
        {
            if (AttachedTrailBlock == null) return;

            float speed = Throttle * GetTerrainAwareBlockSpeed(AttachedTrailBlock);
            shipData.Speed = speed;

            Vector3 movementDirection = transform.forward * speed * Time.deltaTime;
            Vector3 projectedMovement = Vector3.ProjectOnPlane(movementDirection, currentSurfaceNormal);

            // Update surface normal if needed (edge crossing detected)
            UpdateSurfaceNormalIfNeeded(ref projectedMovement);

            transform.position += projectedMovement;
        }


        private Vector3 DetermineCollidingSurface(Transform urchin, Transform block)
        {
            Vector3 localPos = block.InverseTransformPoint(urchin.position);
            Vector3 extents = block.localScale / 2;
            float distX = Mathf.Abs(extents.x - Mathf.Abs(localPos.x));
            float distY = Mathf.Abs(extents.y - Mathf.Abs(localPos.y));
            float distZ = Mathf.Abs(extents.z - Mathf.Abs(localPos.z));

            if (distX < distY && distX < distZ) return localPos.x > 0 ? block.right : -block.right;
            else if (distY < distZ) return localPos.y > 0 ? block.up : -block.up;
            else return localPos.z > 0 ? block.forward : -block.forward;
        }

        private float GetTerrainAwareBlockSpeed(TrailBlock trailBlock)
        {
            if (trailBlock.destroyed) return DestroyedTerrainSpeed;
            return trailBlock.Team == team ? FriendlyTerrainSpeed : HostileTerrainSpeed;
        }
    }
}
