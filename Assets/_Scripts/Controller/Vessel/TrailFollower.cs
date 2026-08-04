
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
using System.Linq;
namespace CosmicShore.Gameplay
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
        public Prism AttachedPrism { get { return attachedTrail.GetBlock(attachedBlockIndex); } }

        IVesselStatus vesselData;

        void Start()
        {
            // TODO: find a better way of setting team that doesn't assume a vessel
            vesselData = GetComponent<IVesselStatus>();
            domain = vesselData.Domain;
        }

        public void Attach(Prism prism)
        {
            CSDebug.Log($"Attaching: trail:{prism.Trail}");
            if (attachedTrail != null) attachedTrail.OnOldestRemoved -= HandleOldestRemoved;
            attachedTrail = prism.Trail;
            attachedBlockIndex = attachedTrail.GetBlockIndex(prism);
            attachedTrail.OnOldestRemoved += HandleOldestRemoved;
            percentTowardNextBlock = 0; // TODO: calculate initial percentTowardNextBlock
            direction = TrailFollowerDirection.Forward; // TODO: use dot product to capture initial direction
        }

        public void Detach()
        {
            if (attachedTrail != null) attachedTrail.OnOldestRemoved -= HandleOldestRemoved;
            attachedTrail = null;
        }

        /// <summary>
        /// The trail dropped its oldest prism, so every survivor - including the one being ridden -
        /// shifted one slot toward the head. Follow the MASS, not the slot: decrement the cached
        /// index so the rider stays on the prism it was actually on. Reaching -1 means the ridden
        /// prism is the one that just left, so let go.
        ///
        /// Only the Wanderway's rolling tether removes from the front today
        /// (<see cref="Trail.RemoveOldest"/>); without this the rider would race forward along the
        /// ribbon at the recycle rate.
        /// </summary>
        void HandleOldestRemoved()
        {
            if (--attachedBlockIndex < 0) Detach();
        }

        void OnDestroy()
        {
            if (attachedTrail != null) attachedTrail.OnOldestRemoved -= HandleOldestRemoved;
        }

        public void RideTheTrail()
        {
            if (!IsAttached) return;

            var upcomingBlocks = attachedTrail.LookAhead(attachedBlockIndex, percentTowardNextBlock, direction, Throttle * FriendlyTerrainSpeed * Time.deltaTime);
            if (upcomingBlocks == null || upcomingBlocks.Count < 2) {
                CSDebug.LogWarning("Could not move TrailFollower, not enough upcoming blocks");
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

        float GetTerrainAwareBlockSpeed(Prism prism) 
        {
            if (prism.destroyed)
                return DestroyedTerrainSpeed;

            if (prism.Domain == domain)
                return FriendlyTerrainSpeed;

            return HostileTerrainSpeed;
        }
    }
}