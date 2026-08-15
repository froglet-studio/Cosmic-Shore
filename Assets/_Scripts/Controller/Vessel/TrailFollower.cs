
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
            vesselData = GetComponent<IVesselStatus>();
        }

        /// <summary>
        /// The rider's domain, read LIVE rather than snapshotted at <c>Start</c>. Domains
        /// re-pick at runtime (the menu's domain-changer toy, a modal re-pick, an AI reroll),
        /// and a snapshot taken at spawn would leave the rider treating its own new trail as
        /// hostile terrain - riding it at the slow speed and never growing it.
        /// </summary>
        Domains Domain => vesselData != null ? vesselData.Domain : Domains.Blue;

        public bool Attach(Prism prism)
        {
            if (!prism || prism.Trail == null)
            {
                CSDebug.LogWarning("TrailFollower.Attach: prism has no Trail - cannot ride it.");
                return false;
            }

            int index = prism.Trail.GetBlockIndex(prism);
            if (index < 0)
            {
                // The prism is not in the trail it names. Attaching anyway would ride index -1,
                // which walks off the front of the ribbon rather than failing loudly.
                CSDebug.LogWarning(
                    $"TrailFollower.Attach: prism is not a member of its own Trail (index {index}) - refusing to attach.");
                return false;
            }

            if (attachedTrail != null) attachedTrail.OnOldestRemoved -= HandleOldestRemoved;
            attachedTrail = prism.Trail;
            attachedBlockIndex = index;
            attachedTrail.OnOldestRemoved += HandleOldestRemoved;
            percentTowardNextBlock = 0; // TODO: calculate initial percentTowardNextBlock
            direction = TrailFollowerDirection.Forward; // TODO: use dot product to capture initial direction
            return true;
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

                // `as` rather than a hard cast: any vessel may carry a TrailFollower, but only
                // the Urchin's transformer owns the grow/steal payoff. A hard cast turns
                // "this vessel rides but does not convert" into an InvalidCastException on the
                // first prism boundary.
                if (vesselData.VesselTransformer is GunVesselTransformer gunTransformer)
                    gunTransformer.FinalBlockSlideEffects();
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

            if (prism.Domain == Domain)
                return FriendlyTerrainSpeed;

            // TIME level-5 "Slipstream": enemy trail stops slowing you down. Hostile mass is
            // normally ridden at HostileTerrainSpeed, which makes raiding someone else's ribbon
            // a slog exactly when you most want to be moving; with the upgrade you cross it at
            // your own speed while still converting it under you.
            //
            // IsUpgradeActive, not a raw level read - the ride writes VesselStatus.Speed, which
            // every peer's view of this vessel depends on.
            var abilities = vesselData?.ElementalAbilityHandler;
            if (abilities != null && abilities.IsUpgradeActive(Element.Time))
                return FriendlyTerrainSpeed;

            return HostileTerrainSpeed;
        }
    }
}