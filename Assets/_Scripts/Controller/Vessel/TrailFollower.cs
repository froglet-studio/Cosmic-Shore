
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

        /// <summary>
        /// The travel direction LATCHED at attach - the way the vessel was flying when it
        /// touched the ribbon. The pilot's signed throttle is interpreted RELATIVE to this
        /// (<see cref="SetRideSign"/>: push = keep going that way, pull = back up), which is
        /// what makes reversal stable: a latched decision the stick flips, never a per-frame
        /// recomputation that can flap (the AI break-off lesson, again).
        /// </summary>
        TrailFollowerDirection attachDirection;

        [SerializeField] float FriendlyTerrainSpeed;
        [SerializeField] float HostileTerrainSpeed;
        [SerializeField] float DestroyedTerrainSpeed;

        [HideInInspector]
        public float Throttle;

        public bool IsAttached { get { return attachedTrail != null; } }
        public Prism AttachedPrism { get { return attachedTrail.GetBlock(attachedBlockIndex); } }

        IVesselStatus vesselData;

        void Awake()
        {
            // Awake, not Start: Attach can arrive from the transformer's first MoveShip on a
            // freshly-swapped vessel, before this component's Start has run.
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

            // LATCH the travel direction from the vessel's own motion: ride the way you were
            // flying. Central difference so a latch on the terminal block still has a heading.
            var list = attachedTrail.TrailList;
            int last = list.Count - 1;
            Vector3 heading = list[Mathf.Min(index + 1, last)].transform.position
                            - list[Mathf.Max(index - 1, 0)].transform.position;
            direction = heading.sqrMagnitude > 1e-6f && Vector3.Dot(vesselData.Course, heading) < 0f
                ? TrailFollowerDirection.Backward
                : TrailFollowerDirection.Forward;
            attachDirection = direction;

            // Seed the lerp from where the vessel ACTUALLY touched, projected onto the segment
            // ahead - percent 0 snapped every latch back to the block's start, a visible
            // backwards jump that read as jitter before the ride even began.
            percentTowardNextBlock = 0f;
            int nextIndex = attachedBlockIndex + (int)direction;
            if (nextIndex >= 0 && nextIndex <= last)
            {
                Vector3 segment = list[nextIndex].transform.position - list[attachedBlockIndex].transform.position;
                float segSq = segment.sqrMagnitude;
                if (segSq > 1e-6f)
                    percentTowardNextBlock = Mathf.Clamp01(
                        Vector3.Dot(transform.position - list[attachedBlockIndex].transform.position, segment) / segSq);
            }
            return true;
        }

        /// <summary>
        /// The pilot's signed throttle, mapped onto the LATCHED attach direction: +1 rides the
        /// way the vessel was flying when it latched, -1 backs up. Idempotent per frame
        /// (<see cref="SetDirection"/> early-outs when unchanged), so the transformer can state
        /// the desired sign every frame without ever flapping the ride.
        /// </summary>
        public void SetRideSign(int sign)
        {
            SetDirection(sign >= 0
                ? attachDirection
                : (TrailFollowerDirection)(-(int)attachDirection));
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
            var projected = attachedTrail.Project(attachedBlockIndex, percentTowardNextBlock, direction, distanceToTravel,
                                                  out var newAttachedBlockIndex, out var newPercent, out TrailFollowerDirection outDirection, out Vector3 course);

            if (outDirection != direction)
            {
                // The projection bounced off the end of an open ribbon. PARK instead of adopting
                // the reflection: discard this frame's move entirely (position, index and lerp
                // all keep their pre-projection values, so there is no snap - the rider stops
                // within one frame-step of the end) and hold direction, so the ride continues
                // the instant the trail grows a new head block or the pilot pulls the throttle
                // the other way. Adopting the bounce is what made the head UNRIDEABLE: the
                // transformer's throttle mapping would flip it straight back next frame, and the
                // two flips oscillated the rider around the terminal block.
                vesselData.Speed = 0f;
                return;
            }

            transform.position = projected;
            percentTowardNextBlock = newPercent;

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
        }

        public void SetDirection(TrailFollowerDirection direction)
        {
            if (this.direction == direction) return;

            this.direction = direction;
            percentTowardNextBlock = 1 - percentTowardNextBlock;

            if (this.direction == TrailFollowerDirection.Forward) attachedBlockIndex--;
            else attachedBlockIndex++;

            // The flip re-expresses the SAME point from the other direction's frame
            // (lerp(b[i], b[i+1], p) == lerp(b[i+1], b[i], 1-p)), which shifts the index by
            // one - off the end of the list when the rider is parked ON a terminal block.
            // The equivalent in-range expression of that point is the terminal block at
            // lerp 0. Clamp, or AttachedPrism indexes out of the trail on the next read.
            if (attachedTrail != null)
            {
                int last = attachedTrail.TrailList.Count - 1;
                if (attachedBlockIndex < 0) { attachedBlockIndex = 0; percentTowardNextBlock = 0f; }
                else if (attachedBlockIndex > last) { attachedBlockIndex = last; percentTowardNextBlock = 0f; }
            }
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