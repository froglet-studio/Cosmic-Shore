
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
        /// The projected point ON the trail's centerline for the current index + lerp. The
        /// follower deliberately does NOT write the vessel's transform: the ride composes the
        /// hull's position from this point plus an orbit offset around the ribbon (the
        /// transformer owns that), so the kernel stays a pure 1D projection.
        /// </summary>
        public Vector3 CenterlinePoint { get; private set; }

        /// <summary>The live travel heading along the ribbon - what `VesselStatus.Course`
        /// is set to. Flips with <see cref="Direction"/>.</summary>
        public Vector3 TravelHeading { get; private set; }

        /// <summary>
        /// The ribbon's axis at the current block in INDEX ORDER (toward the head), central
        /// difference. Trail prisms are authored with Z parallel to the trail, and this is
        /// that axis read from the geometry - a STABLE reference that never flips with travel
        /// direction, which is what makes it safe to resolve the pilot's facing against
        /// (dot(vessel.forward, IndexOrderHeading)) every frame: unlike Course, it cannot
        /// feed back on the decision it informs.
        /// </summary>
        public Vector3 IndexOrderHeading
        {
            get
            {
                if (attachedTrail == null) return Vector3.forward;
                Vector3 h = attachedTrail.HeadingAt(attachedBlockIndex);
                return h.sqrMagnitude > 1e-6f ? h : Vector3.forward;
            }
        }

        [SerializeField] float FriendlyTerrainSpeed;
        [SerializeField] float HostileTerrainSpeed;
        [SerializeField] float DestroyedTerrainSpeed;

        [Tooltip("How quickly the ride's speed chases its target (1/s, exponential). The rail's " +
                 "weight: crossing from a friendly prism (150) onto a hostile one (10) becomes " +
                 "a braking slide rather than a 15x snap, and letting go coasts to a stop.")]
        [SerializeField] float speedTrackingRate = 5f;

        [HideInInspector]
        public float Throttle;

        public bool IsAttached { get { return attachedTrail != null; } }

        /// <summary>
        /// The ribbon this follower is riding, or null when detached. Exposed so the
        /// transformer can tell "the trail I just launched off" from any other ribbon it
        /// touches in the next moment (the re-attach grace after an end-of-ribbon launch).
        /// </summary>
        public Trail AttachedTrail => attachedTrail;

        /// <summary>
        /// Set on the frame the ride runs off the end of an OPEN ribbon, and cleared at the top
        /// of every <see cref="RideTheTrail"/>. The follower does NOT act on it - it owns the
        /// 1D kernel, never <c>VesselStatus.IsAttached</c> - so the transformer reads it
        /// immediately after ticking the ride and turns it into a LAUNCH: detach, carry the
        /// speed off the end, and let it bleed back down to cruise in free flight.
        ///
        /// Parking here instead (the previous behaviour) killed the carried speed and left the
        /// rider pinned to the terminal block, so a long fast grind ended in a dead stop at the
        /// exact moment the pilot had the most momentum to spend.
        /// </summary>
        public bool ReachedEnd { get; private set; }

        /// <summary>
        /// Null when this follower is not attached. <see cref="Detach"/> nulls the trail
        /// without touching VesselStatus, so a consumer can legitimately still be asking.
        /// </summary>
        public Prism AttachedPrism
        {
            get { return attachedTrail != null ? attachedTrail.GetBlock(attachedBlockIndex) : null; }
        }

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

            // Seed the initial direction from the vessel's own motion - the ride's first frames
            // continue the way you were flying. (Per frame thereafter the transformer resolves
            // direction from the pilot's FACING against IndexOrderHeading, the scheme the
            // original Urchin used.)
            //
            // Every geometric read here goes through RidePoint - the SPINE the ride actually
            // follows - never raw block positions. Seeding from raw positions while the ride
            // runs on the spine put the seed on the ribbon's helix and the first moving frame
            // on the spine: a lay-offset-sized snap (10u+ on a Squirrel) at the exact moment
            // of contact.
            var list = attachedTrail.TrailList;
            int last = list.Count - 1;
            Vector3 heading = attachedTrail.HeadingAt(index);
            direction = heading.sqrMagnitude > 1e-6f && Vector3.Dot(vesselData.Course, heading) < 0f
                ? TrailFollowerDirection.Backward
                : TrailFollowerDirection.Forward;

            // Seed the lerp from where the vessel ACTUALLY touched, projected onto the segment
            // ahead - percent 0 snapped every latch back to the block's start, a visible
            // backwards jump that read as jitter before the ride even began.
            percentTowardNextBlock = 0f;
            int nextIndex = attachedBlockIndex + (int)direction;
            if (nextIndex >= 0 && nextIndex <= last)
            {
                Vector3 nearPoint = attachedTrail.RidePoint(list[attachedBlockIndex]);
                Vector3 segment = attachedTrail.RidePoint(list[nextIndex]) - nearPoint;
                float segSq = segment.sqrMagnitude;
                if (segSq > 1e-6f)
                    percentTowardNextBlock = Mathf.Clamp01(
                        Vector3.Dot(transform.position - nearPoint, segment) / segSq);
            }

            // Seed the centerline read so the transformer can compose a position on the very
            // first ride frame (and while parked before the first move).
            int seedNext = Mathf.Clamp(attachedBlockIndex + (int)direction, 0, last);
            CenterlinePoint = Vector3.Lerp(
                attachedTrail.RidePoint(list[attachedBlockIndex]),
                attachedTrail.RidePoint(list[seedNext]),
                percentTowardNextBlock);
            TravelHeading = heading.sqrMagnitude > 1e-6f
                ? heading * (int)direction
                : Vector3.forward;

            ReachedEnd = false;

            // Carry the arrival speed onto the rail. Starting the grind at a dead stop and
            // ramping back up brakes the pilot for latching on, which is the opposite of what
            // a rail grind should reward; the smoothing then eases it to the ride's own pace.
            _rideSpeed = Mathf.Max(0f, vesselData.Speed);
            return true;
        }

        public void Detach()
        {
            if (attachedTrail != null) attachedTrail.OnOldestRemoved -= HandleOldestRemoved;
            attachedTrail = null;
            _rideSpeed = 0f;
            ReachedEnd = false;
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

        /// <summary>
        /// The ride's live speed - smoothed, so it is also what the ride COASTS on when the
        /// pilot lets go. The transformer keeps calling <see cref="RideTheTrail"/> while this
        /// is bleeding off rather than cutting the ride dead at the throttle deadband.
        /// </summary>
        public float RideSpeed => _rideSpeed;
        float _rideSpeed;

        public void RideTheTrail()
        {
            ReachedEnd = false;
            if (!IsAttached) return;

            // ONE speed for the frame, smoothed toward the block-under-the-rider's target.
            //
            // Terrain speed is a per-BLOCK step (friendly 150 vs hostile 10 - a 15x cliff at a
            // domain boundary), and the old walk re-read it per block WITHIN the frame and
            // published each value to vesselData.Speed in turn, so the ride's speed jumped
            // block to block and the last block of the frame won. Chasing one target
            // exponentially turns every one of those cliffs - terrain change, throttle
            // change, release - into a deceleration you can feel rather than a snap.
            //
            // A frame covers ~2.5u at full grind (150 u/s at 60fps) against blocks 4u and
            // longer, so treating speed as constant across the frame costs nothing real and
            // removes the entire per-block time-accounting walk (and with it the LookAhead
            // call, whose <2-block early-out fought the hole bridging: a ribbon whose
            // survivors are sparse would refuse to move at all).
            var block = AttachedPrism;
            float terrain = block ? GetTerrainAwareBlockSpeed(block) : FriendlyTerrainSpeed;
            float multiplier = vesselData.VesselTransformer ? vesselData.VesselTransformer.SpeedMultiplier : 1f;

            _rideSpeed = Mathf.Lerp(_rideSpeed, Throttle * terrain * multiplier,
                                    1f - Mathf.Exp(-speedTrackingRate * Time.deltaTime));
            vesselData.Speed = _rideSpeed;

            float distanceToTravel = _rideSpeed * Time.deltaTime;
            if (distanceToTravel <= 1e-5f) return;   // parked - nothing to project

            // Do the movement and save the out direction
            var projected = attachedTrail.Project(attachedBlockIndex, percentTowardNextBlock, direction, distanceToTravel,
                                                  out var newAttachedBlockIndex, out var newPercent, out TrailFollowerDirection outDirection, out Vector3 course);

            if (outDirection != direction)
            {
                // The projection bounced off the end of an open ribbon: the ride is OUT OF RAIL.
                //
                // The reflection itself is still discarded - position, index and lerp all keep
                // their pre-projection values, so there is no snap and no oscillation around the
                // terminal block (adopting the bounce is what made the head unrideable: the
                // transformer's throttle mapping flipped it straight back the next frame).
                //
                // What CHANGED is what happens next. This used to PARK - zeroing the ride speed
                // and holding the rider against the end - so the reward for a long fast grind
                // was a dead stop with nothing to spend. Now it reports the end and the
                // transformer LAUNCHES: the ribbon runs out, the vessel flies off it carrying
                // the speed it had, and free flight bleeds that back down to cruise.
                //
                // A LOOP never reaches here (Project wraps rather than reflecting), so a closed
                // ribbon is still ridden forever - the difference between the two topologies is
                // now something the pilot can feel.
                ReachedEnd = true;
                return;
            }

            // The kernel publishes the CENTERLINE point; the transformer composes the hull's
            // actual position from it (centerline + orbit offset around the ribbon). Writing
            // transform.position here would pin the hull to the centerline and make the orbit
            // impossible.
            CenterlinePoint = projected;
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
            TravelHeading = course;
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