
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore.Core
{
    // TODO: move to enum folder
    public enum TrailFollowerDirection
    {
        Forward = 1,
        Backward = -1,
    }

    [RequireComponent(typeof(IVesselStatus))]
    public class TrailFollower : MonoBehaviour
    {
        int attachedBlockIndex;
        Trail attachedTrail;
        Domains domain;
        float percentTowardNextBlock;
        TrailFollowerDirection direction;
        public TrailFollowerDirection Direction { get { return direction; } }


        [SerializeField] float FriendlyTerrainSpeed;
        [SerializeField] float HostileTerrainSpeed;
        [SerializeField] float DestroyedTerrainSpeed;

        [HideInInspector]
        public float Throttle;

        public bool IsAttached { get { return attachedTrail != null; } }
        public TrailBlock AttachedTrailBlock { get { return attachedTrail.GetBlock(attachedBlockIndex); } }

        IVesselStatus vesselData;

        void Start()
        {
            // TODO: find a better way of setting team that doesn't assume a vessel
            vesselData = GetComponent<IVesselStatus>();
            domain = vesselData.Domain;
        }

        public void Attach(TrailBlock trailBlock)
        {
            Debug.Log($"Attaching: trail:{trailBlock.Trail}");
            attachedTrail = trailBlock.Trail;
            attachedBlockIndex = attachedTrail.GetBlockIndex(trailBlock);
            percentTowardNextBlock = 0; // TODO: calculate initial percentTowardNextBlock
            direction = TrailFollowerDirection.Forward; // TODO: use dot product to capture initial direction
        }

        public void Detach()
        {
            attachedTrail = null;
        }

        public void RideTheTrail()
        {
            if (!IsAttached) return;

            var upcomingBlocks = attachedTrail.LookAhead(attachedBlockIndex, percentTowardNextBlock, direction, Throttle * FriendlyTerrainSpeed * Time.deltaTime);
            if (upcomingBlocks == null || upcomingBlocks.Count < 2) {
                Debug.LogWarning("Could not move TrailFollower, not enough upcoming blocks");
                return;
            }

            // TODO: percentTowardNextBlock is always positive?

            var distanceToTravel = 0f;  // <-- This is what we're calculating
            var timeRemaining = Time.deltaTime; 

            var blockIndex = 0;
            var currentBlock = upcomingBlocks[blockIndex];
            var nextBlock = upcomingBlocks[blockIndex+1];

            var distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position) * (1-percentTowardNextBlock);
            var speedToNextBlock = Throttle * GetTerrainAwareBlockSpeed(currentBlock);
            
            speedToNextBlock *= vesselData.VesselTransformer.SpeedMultiplier;
            vesselData.Speed = speedToNextBlock;

            var timeToNextBlock = distanceToNextBlock / speedToNextBlock;

            while (timeRemaining > timeToNextBlock)
            {
                distanceToTravel += distanceToNextBlock;
                timeRemaining -= timeToNextBlock;
                
                currentBlock = upcomingBlocks[++blockIndex];
                nextBlock = upcomingBlocks[blockIndex + 1];
                
                distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position);
                speedToNextBlock = Throttle * GetTerrainAwareBlockSpeed(currentBlock);
                speedToNextBlock *= vesselData.VesselTransformer.SpeedMultiplier;
                vesselData.Speed = speedToNextBlock;

                timeToNextBlock = distanceToNextBlock / speedToNextBlock;
            }

            // Accumulate the remain
            distanceToTravel += speedToNextBlock * timeRemaining;

            // Do the movement and save the out direction
            transform.position = attachedTrail.Project(attachedBlockIndex, percentTowardNextBlock, direction, distanceToTravel, 
                                                      out var newAttachedBlockIndex, out percentTowardNextBlock, out TrailFollowerDirection outDirection, out Vector3 course);
            if (newAttachedBlockIndex != attachedBlockIndex) 
            {
                attachedBlockIndex = newAttachedBlockIndex;
                ((GunVesselTransformer) vesselData.VesselTransformer).FinalBlockSlideEffects(); 
            }
            vesselData.Course = course;
            
            if (outDirection != direction)
            {
                // Ping ponged
                // TODO: Probably need to do other stuff here
                direction = outDirection;
            }
        }

        public void SetDirection(TrailFollowerDirection direction)
        {
            if (this.direction == direction) return;

            this.direction = direction;
            percentTowardNextBlock = 1 - percentTowardNextBlock;

            if (this.direction == TrailFollowerDirection.Forward) attachedBlockIndex--;
            else attachedBlockIndex++;
        }

        float GetTerrainAwareBlockSpeed(TrailBlock trailBlock) 
        {
            if (trailBlock.destroyed)
                return DestroyedTerrainSpeed;

            if (trailBlock.Domain == domain)
                return FriendlyTerrainSpeed;

            return HostileTerrainSpeed;
        }
    }
}