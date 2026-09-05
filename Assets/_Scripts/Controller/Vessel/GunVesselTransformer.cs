using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Urchin's flight model: ordinary free flight until it latches onto a prismscape,
    /// then a RIDE whose form follows the prismscape's DIMENSION
    /// (<see cref="PrismscapeDimension"/>):
    ///
    ///  * 1D trail  → GRIND the ribbon (<see cref="TrailFollower"/> is the centerline
    ///    kernel). Trail prisms are authored with Z PARALLEL to the trail. The hull rides ON
    ///    the trail and its ATTITUDE IS ENTIRELY THE PILOT'S - roll, yaw and pitch all run
    ///    exactly as in free flight, which is the shape the original shipped with
    ///    (<c>GunShipController.Slide</c>). Only the throttle is re-purposed: signed speed
    ///    along the ribbon, with forward/reverse resolved from the pilot's facing against the
    ///    trail axis (the original's dot-product scheme). Because the hull's forward IS the
    ///    rail while riding, an ordinary roll already spins the pilot around the trail - no
    ///    positional orbit is needed, and imposing one swung the hull bodily on a curve.
    ///  * 2D surface (and the boundary of a 3D volume, which is locally the same thing) →
    ///    ROLL across the aggregate surface, marble-madness style
    ///    (<see cref="BlockscapeFollower"/>). Gyroid and Schwarz-P prisms are authored with Z
    ///    ORTHOGONAL to the surface, so the ride follows a smoothed plane over those normals
    ///    with momentum - and running off a sheet's edge WRAPS around the rim onto the other
    ///    side. Attitude is the PILOT'S here too: the surface constrains POSITION, never
    ///    aim. The rider must never feel a prism's edges or the gaps between prisms.
    ///
    /// Attaching is two flags and no reparenting - <c>VesselAttachPrismEffectSO</c> sets
    /// <see cref="IVesselStatus.IsAttached"/> and <c>.AttachedPrism</c> on contact, this
    /// transformer edge-detects them and routes to the follower the prism's topology calls
    /// for. Either way the payoff per prism visited is the same one rule: a destroyed prism
    /// is RESTORED, your own is GROWN (Mass-scaled, shielded at Mass 5), an enemy's is
    /// STOLEN. That is the whole of the original "Trail Rider" ability, now dimension-blind.
    /// </summary>
    public class GunVesselTransformer : VesselTransformer
    {
        /// <summary>
        /// The 1D ride kernel - projects ALONG a trail from a block index and supports
        /// reversing. NOT <c>BlockscapeFollower</c>, which crawls FACES and is the 2D kernel;
        /// the two expose near-identical members, which is exactly how the field got silently
        /// pointed at the wrong one during the vessel-layer port.
        /// </summary>
        TrailFollower trailFollower;

        /// <summary>The 2D ride kernel - rolls across prism faces and hops to adjacent
        /// prisms, so a gyroid/Schwarz shell is one continuous floor.</summary>
        BlockscapeFollower surfaceFollower;

        enum RideMode { None = 0, Trail = 1, Surface = 2 }
        RideMode _rideMode;

        [Tooltip("Ammo gained per second while riding a prismscape. Doubled on a shielded prism.")]
        [SerializeField] float rechargeRate = .1f;

        public float ProjectileScale = 1f;
        public Vector3 BlockScale = new(4f, 4f, 1f);

        [Tooltip("How much volume a friendly prism gains as you ride over it. Scaled by MASS " +
                 "- the Urchin's Trail Rider ability.")]
        [SerializeField] ElementalFloat growthAmount = new ElementalFloat(1);

        [Tooltip("Signed throttle below this magnitude parks the rider. TrailFollower divides " +
                 "its inter-prism distance by throttle*speed, so a literal zero is an infinite " +
                 "time-to-next-block; and XDiff idles NEAR its rest value, never exactly on it.")]
        [SerializeField] float throttleDeadband = 0.1f;

        [Tooltip("The XDiff value that reads as neutral. XDiff is the dual-stick SPEED axis " +
                 "and RESTS AT 0.5 (GamepadInputStrategy: (right.x - left.x + 2) / 4), not at " +
                 "0 - the 2023 slide was authored against an older input scale and porting its " +
                 "0.2 made a hands-off vessel creep at 37% throttle. Push above this to ride " +
                 "the direction latched at attach, pull below it to back up.")]
        [SerializeField] float throttleRestPosition = 0.5f;

        [Tooltip("TRUE facing hysteresis: the forward/reverse mapping flips only when the " +
                 "pilot's aim crosses to the OTHER side of broadside by at least this much " +
                 "(dot with the ribbon axis). Around a bend the axis sweeps under a steady " +
                 "nose and the dot wanders through zero - a plain re-latch band flipped the " +
                 "ride's direction on every wander, a rapid back-and-forth jitter at the apex. " +
                 "Inside the buffer the latched direction simply holds.")]
        [SerializeField] float facingFlipThreshold = 0.35f;

        [Tooltip("How quickly the hull is drawn onto the rail after latching on (1/s, " +
                 "exponential). The ride sits ON the trail; this only eases the offset the " +
                 "hull had at the moment of contact, so attaching never pops it sideways.")]
        [SerializeField] float railSettleRate = 4f;

        [Tooltip("Extra clearance beyond the prism's surface while grinding, world units. The " +
                 "ride solves the exact distance from the trail axis to the prism's FACE, so " +
                 "this is only the hull's own half-thickness - raise it until the ship reads " +
                 "as sitting ON the trail rather than sunk into it.")]
        [SerializeField] float rideSurfaceClearance = 1.5f;

        [Tooltip("How quickly the grind speed chases the throttle (1/s, exponential) - the " +
                 "rail's WEIGHT. This is what makes letting go coast to a stop, a reversal " +
                 "swing through zero, and a friendly->hostile prism transition read as a " +
                 "deceleration instead of a 15x speed snap.")]
        [SerializeField] float trailInertiaRate = 6f;

        [Tooltip("How fast the speed CARRIED off a ride bleeds back to ordinary cruise " +
                 "(world units per second). A friendly grind runs at 150 against a ~50 u/s " +
                 "free-flight top, so this is the whole length of the launch: 12 u/s spends " +
                 "about eight seconds getting back to cruise. It only ever removes EXCESS - " +
                 "a ride slower than the throttle target hands nothing over and the vessel " +
                 "accelerates normally.")]
        [SerializeField] float detachSpeedDecayRate = 12f;

        [Tooltip("Seconds after launching off the end of a ribbon during which THAT ribbon " +
                 "cannot re-latch the vessel. A curved trail whose end doubles back would " +
                 "otherwise snatch the launch away in the same breath it was granted. Only " +
                 "the ribbon just left is refused - fly straight off one rail into another " +
                 "and the new one takes you immediately.")]
        [SerializeField] float endLaunchReattachGrace = 0.35f;

        /// <summary>The speed a ride handed to free flight, decaying toward the ordinary
        /// throttle target. 0 = nothing carried, and the vessel flies exactly as before.</summary>
        float _carriedSpeed;

        /// <summary>The ribbon a launch just left, refused by <see cref="IsReattachBlocked"/>
        /// until <see cref="_reattachBlockedUntil"/>. Held as a plain reference, never a prism -
        /// the whole ribbon is what must not re-latch, not the one terminal block.</summary>
        Trail _launchedFromTrail;
        float _reattachBlockedUntil;

        bool attached = false;
        CameraManager cameraManager;

        /// <summary>The hull's offset from the rail at the moment of contact, decayed to zero
        /// so the ride settles ON the trail without a snap.</summary>
        Vector3 _railOffset;

        /// <summary>+1 when the nose agrees with <see cref="TrailFollower.IndexOrderHeading"/>.
        /// Flips only past <see cref="facingFlipThreshold"/> the other way - hysteresis, so aiming near
        /// broadside cannot flap the throttle mapping.</summary>
        int _facingSign = 1;

        /// <summary>The grind's smoothed signed throttle - the rail's momentum.</summary>
        float _grindThrottle;

        [SerializeField] int ammoIndex = 0;

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            cameraManager = CameraManager.Instance;
            trailFollower = GetComponent<TrailFollower>();
            surfaceFollower = GetComponent<BlockscapeFollower>();

            if (!trailFollower)
                CSDebug.LogError(
                    $"{name}: GunVesselTransformer needs a TrailFollower on the same GameObject - " +
                    "without it the vessel can attach to a trail but never move along it.");

            // Detach-first, above any gate: Initialize re-runs on a LIVE component (vessel
            // swap, ownership change) and a stale subscription - or a stale launch - would pay
            // the previous pilot. A carry left standing would hand the incoming pilot a free
            // 150 u/s they never rode for.
            ClearLaunchState();

            if (surfaceFollower)
            {
                surfaceFollower.OnPrismCrossed -= ApplyPrismscapePayoff;
                surfaceFollower.OnPrismCrossed += ApplyPrismscapePayoff;
            }
        }

        void OnDisable()
        {
            if (surfaceFollower) surfaceFollower.OnPrismCrossed -= ApplyPrismscapePayoff;
        }

        /// <summary>
        /// A respawn / turn reset must not inherit the previous life's launch. `speed` is zeroed
        /// by the base, and a carry left standing would immediately floor the throttle target
        /// back up to a ride speed the new life never earned.
        /// </summary>
        public override void ResetTransformer()
        {
            ClearLaunchState();
            base.ResetTransformer();
        }

        void ClearLaunchState()
        {
            _carriedSpeed = 0f;
            _launchedFromTrail = null;
            _reattachBlockedUntil = 0f;
        }

        protected override void MoveShip()
        {
            if (VesselStatus.IsAttached && !attached)
            {
                if (IsReattachBlocked(VesselStatus.AttachedPrism) || !TryBeginRide())
                {
                    // A refused attach must release the flags, or the vessel is stuck in ride
                    // mode with nothing under it and free flight never resumes.
                    VesselStatus.IsAttached = false;
                    VesselStatus.AttachedPrism = null;
                }
                else
                {
                    // The ride owns attitude from here: start it from the hull's ACTUAL pose so
                    // any rotation input accumulated this frame doesn't fire as a turn the
                    // pilot never saw.
                    accumulatedRotation = transform.rotation;

                    if (IsLocalPilotCamera && cameraManager != null)
                    {
                        // Pull the camera in while riding: the prismscape is the thing to
                        // read, and the ride is close-quarters.
                        cameraManager.SetNormalizedCloseCameraDistance(1);
                    }
                }
            }
            else if (!VesselStatus.IsAttached && attached)
            {
                LeaveRide();
            }

            attached = VesselStatus.IsAttached;

            // A follower that let go on its own (its trail was cleared, or its ground prism
            // was destroyed) leaves VesselStatus.IsAttached true, so without this the vessel
            // would ride a null trail: frozen in place, one exception per frame. Hand it back
            // to free flight - through the SAME exit as every other detach, so this one is not
            // the one case that skips the ride teardown: it used to clear the flags and set
            // `attached` false in place, which meant the `else if` above could never fire for
            // it, so _rideMode stayed stale and the ride camera stayed pulled in for the rest
            // of the life. It would also have been the one exit that dropped the speed carry.
            if (attached && _rideMode != RideMode.None && !RideHasGround())
            {
                VesselStatus.IsAttached = false;
                VesselStatus.AttachedPrism = null;
                attached = false;
                LeaveRide();
            }

            if (attached && _rideMode != RideMode.None)
            {
                Slide();
            }
            else
            {
                // Free flight owns the bleed-off. Ticked BEFORE base.MoveShip so the frame the
                // carry expires is the frame the ordinary throttle target takes over.
                TickCarriedSpeed();
                base.MoveShip();
            }
        }

        /// <summary>Routes the attach to the follower the prism's topology calls for.</summary>
        bool TryBeginRide()
        {
            var prism = VesselStatus.AttachedPrism;
            if (!prism) return false;

            // NOT `prism.Trail != null` - `Trail` is the general lay container and the gyroid /
            // Schwarz shells are laid INTO one, so a Trail reference is membership evidence,
            // never shape evidence. The topology read resolves the layer's declared dimension
            // (Trail.Dimension) or, container-less, a spatial census.
            var dimension = PrismscapeTopology.DimensionOf(prism);

            // 0D is NOT rideable. A lone prism has no extent to travel along, and letting one
            // be attachable is how a mis-stamped ribbon (a ring whose prisms lost their
            // container, say) got ridden as a SURFACE - the marble on trail prisms, with its
            // along-z "normal" and free ribbon-hopping. Refusing here means such a prism reads
            // as ordinary mass and the vessel simply flies on; a genuine singleton is rare by
            // construction, since almost all prisms belong to a 1D or 2D lay.
            if (dimension == PrismscapeDimension.Singleton) return false;

            if (dimension == PrismscapeDimension.Trail)
            {
                if (!trailFollower || !trailFollower.Attach(prism)) return false;
                _rideMode = RideMode.Trail;
                SeedTrailRide();
                return true;
            }

            // Singleton / Surface / Volume: ride the boundary. A Surface is ridden ON, a
            // Volume on its boundary, a Singleton is one box crawled around - locally the
            // same act, so all three route to the face kernel. (A SINGLETON must never reach
            // TrailFollower even when it carries a one-block Trail: Trail.Project on a single
            // block has zero inter-block distance and never terminates.)
            if (!surfaceFollower) return false;
            surfaceFollower.Attach(prism);
            _rideMode = RideMode.Surface;
            CSDebug.LogVerbose(CSLogChannel.PrismscapeRide,
                $"[GunVesselTransformer] Riding a {dimension} prismscape.");
            return true;
        }

        /// <summary>
        /// Seed the ride from the moment of contact: the hull's offset from the rail (decayed
        /// away by <see cref="railSettleRate"/> rather than snapped), the grind throttle from
        /// the stick (so latching on while holding forward carries your speed onto the rail),
        /// and - critically - the facing sign from the direction the follower LATCHED, which
        /// it took from the vessel's COURSE.
        ///
        /// Seeding facing from the nose instead is what made "forward" a coin flip: you fly
        /// INTO a trail, so at the instant of contact the nose is usually across the ribbon,
        /// dot(forward, axis) is near zero, and its sign is noise. Push forward and you were
        /// as likely to be sent back the way you came as onward - which is exactly the
        /// reported "forward swung me around, backward worked better". Taking it from the
        /// latched direction means push-forward-at-attach always means "keep going the way I
        /// was flying", and the hysteresis band then holds that until the pilot genuinely
        /// turns to look the other way.
        /// </summary>
        void SeedTrailRide()
        {
            _facingSign = (int)trailFollower.Direction;
            _grindThrottle = ReadThrottle();
            _railOffset = transform.position - (trailFollower.CenterlinePoint + RideSurfaceOffset());
        }

        /// <summary>
        /// The offset from the ride line out to the ridden prism's SURFACE - what keeps the
        /// hull ON the trail instead of inside it. A single-ribbon wake (the Sparrow's, laid
        /// with no gap to signal a one-thumb vessel) puts its block centres exactly on the ride
        /// line, so riding the line bare buries the ship in every prism it passes through.
        ///
        /// WHICH WAY round the prism is the pilot's ROLL, and it costs no new state: the
        /// direction is the hull's own UP, flattened across the trail. Roll the ship and its up
        /// sweeps around its forward - which while riding IS the trail axis - so the ship walks
        /// around the prism's z axis, belly always toward the rail. Attitude stays entirely the
        /// pilot's (round 11) and POSITION follows from it, so nothing can fight the stick.
        ///
        /// HOW FAR is the exact box cross-section, not a constant: a prism is a scaled cube, so
        /// the distance from its z axis to its face along a direction with axis-components
        /// (u, v) is min(halfX/u, halfY/v). Riding a wide flat trail therefore sits close on
        /// its broad faces and far out at its edges, exactly as a surface should.
        ///
        /// Reads TargetScale (authored), never the live scale, so a block still blooming in -
        /// or one the payoff is GROWING under the rider - cannot drag the ship around.
        /// </summary>
        Vector3 RideSurfaceOffset()
        {
            var prism = trailFollower.AttachedPrism;
            if (!prism) return Vector3.zero;

            var t = prism.transform;
            Vector3 axis = RibbonAxis();                    // the TRAIL's direction, not the prism's

            // The hull's own up, flattened across the trail. Degenerate only when the pilot is
            // aiming straight along the rail, where any perpendicular will do.
            Vector3 radial = transform.up - axis * Vector3.Dot(transform.up, axis);
            radial = radial.sqrMagnitude > 1e-6f ? radial.normalized : t.up;

            Vector3 scale = prism.TargetScale;
            if (scale.x <= 0f || scale.y <= 0f) scale = t.localScale;
            Vector3 half = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)) * 0.5f;

            // Shielded mass is ridden as the CUBOID ITS SHELL IS NESTED IN. Both shield meshes
            // are the box's circumscribing dual - vertices at CIRCUMSCRIBING_SCALE x the box's
            // half-extents - so the factor is read from the generator rather than guessed, and
            // a super-shielded prism's spikes are inside the same envelope.
            var props = prism.prismProperties;
            if (props != null && (props.IsShielded || props.IsSuperShielded))
                half *= OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

            // The SUPPORT of the box along the radial: sum of each axis' half-extent times how
            // much that axis leans along the radial. This is the box's silhouette envelope
            // measured in the TRAIL's frame, which is what a rider going around it needs -
            // a prism at a strange angle (a drift block yawed off the ribbon) then reads as
            // effectively WIDER, exactly as its sweep along the trail makes it, and the rider
            // clears it instead of clipping through a corner.
            //
            // Support rather than a literal trail-aligned AABB, because an AABB about an axis
            // is not well defined without arbitrarily choosing which way is "up" around the
            // trail - and the answer would change with that choice. The support needs no such
            // choice, and reduces to exactly the box half-extent for a prism square to the
            // ribbon (the Sparrow case is unchanged).
            float surface = half.x * Mathf.Abs(Vector3.Dot(radial, t.right))
                          + half.y * Mathf.Abs(Vector3.Dot(radial, t.up))
                          + half.z * Mathf.Abs(Vector3.Dot(radial, t.forward));

            return radial * (surface + rideSurfaceClearance);
        }

        /// <summary>
        /// The ribbon's axis at the rider, in INDEX ORDER, preferring the CONTINUOUS spline
        /// tangent over the block-to-block central difference.
        ///
        /// <see cref="TrailFollower.IndexOrderHeading"/> is a step function - it only changes
        /// when the block index changes - so transporting the orbit frame against it kicked
        /// the grind once per block, a tick at exactly the trail's periodicity. The follower's
        /// <see cref="TrailFollower.TravelHeading"/> is the Catmull-Rom tangent and IS
        /// continuous through crossings; multiplying by the travel direction re-expresses it
        /// in index order, making it a drop-in with the same sign convention. The dot guard
        /// keeps a stale or reflected tangent from inverting the frame, and the discrete axis
        /// remains the fallback (parked before the first walk, degenerate geometry).
        /// </summary>
        Vector3 RibbonAxis()
        {
            Vector3 discrete = trailFollower.IndexOrderHeading;
            Vector3 travel = trailFollower.TravelHeading;
            if (travel.sqrMagnitude <= 1e-6f) return discrete;

            Vector3 continuous = travel.normalized * (int)trailFollower.Direction;
            return Vector3.Dot(continuous, discrete) > 0f ? continuous : discrete;
        }

        /// <summary>
        /// The 1D ride ran out of ribbon. Clear the attach flags so the next
        /// <see cref="MoveShip"/> ends the ride properly, and fence the ribbon just left so it
        /// cannot immediately re-latch what it just released.
        /// </summary>
        void LaunchOffRibbonEnd()
        {
            _launchedFromTrail = trailFollower ? trailFollower.AttachedTrail : null;
            _reattachBlockedUntil = Time.time + Mathf.Max(0f, endLaunchReattachGrace);

            VesselStatus.IsAttached = false;
            VesselStatus.AttachedPrism = null;
        }

        /// <summary>
        /// True while <paramref name="prism"/> belongs to the ribbon a launch just left and the
        /// grace has not expired. Scoped to that ONE ribbon on purpose: a blanket "no attaching
        /// for N seconds" would also refuse the next rail the pilot aimed for, which is the
        /// manoeuvre the launch exists to enable.
        /// </summary>
        bool IsReattachBlocked(Prism prism)
        {
            if (_launchedFromTrail == null || Time.time >= _reattachBlockedUntil) return false;
            return prism && prism.Trail == _launchedFromTrail;
        }

        /// <summary>
        /// Hand free flight the speed the ride was doing, to be bled off by
        /// <see cref="TickCarriedSpeed"/> rather than snapped away.
        ///
        /// Only EXCESS is carried: a ride slower than what the pilot could fly anyway
        /// (hostile terrain at 10 u/s) hands over nothing, so this can never brake a vessel and
        /// can never be a free speed floor - it is strictly momentum the pilot already had.
        /// </summary>
        void CarrySpeedIntoFreeFlight(float rideSpeed)
        {
            // A new detach REPLACES whatever the last one left bleeding off. A ride writes
            // VesselStatus.Speed authoritatively, so a pilot who launched at 150, brushed a
            // hostile ribbon and crawled at 10 really IS doing 10 when they let go - keeping the
            // old carry would hand back speed the vessel no longer had.
            _carriedSpeed = 0f;

            if (rideSpeed <= 0f || InputStatus == null) return;

            float natural = base.ComputeThrottleTarget();
            if (rideSpeed <= natural) return;

            _carriedSpeed = rideSpeed;
            // Write the smoothed cruise field directly: the whole point is that the handoff is
            // seamless, and letting `speed` lerp UP to the carried value would read as the
            // vessel accelerating after it let go rather than coasting.
            speed = rideSpeed;
        }

        /// <summary>One frame of the bleed-off, at a constant, readable
        /// <see cref="detachSpeedDecayRate"/> u/s rather than an exponential tail that never
        /// quite lands.</summary>
        void TickCarriedSpeed()
        {
            if (_carriedSpeed <= 0f || InputStatus == null) return;

            float natural = base.ComputeThrottleTarget();
            _carriedSpeed = Mathf.MoveTowards(_carriedSpeed, natural,
                                              Mathf.Max(0f, detachSpeedDecayRate) * Time.deltaTime);
            if (_carriedSpeed <= natural) _carriedSpeed = 0f;
        }

        /// <summary>
        /// The ordinary throttle target, floored by whatever speed a ride handed over. MAX, not
        /// a replacement: the pilot's throttle takes back over the instant it can beat the
        /// decaying carry, and a boost during the glide is not thrown away.
        /// </summary>
        protected override float ComputeThrottleTarget()
        {
            float natural = base.ComputeThrottleTarget();
            return _carriedSpeed > natural ? _carriedSpeed : natural;
        }

        /// <summary>The one exit from a ride: end it and give the pilot their camera back.</summary>
        void LeaveRide()
        {
            EndRide();
            if (IsLocalPilotCamera && cameraManager != null)
                cameraManager.SetNormalizedCloseCameraDistance(0);
        }

        void EndRide()
        {
            // Momentum outlives the ride. Read BEFORE Detach, which zeroes the follower's own
            // ride speed. Every exit routes through here - running off the end of a ribbon,
            // Slip, a trail that was cleared under the rider - so "an Urchin keeps its speed
            // when it lets go" is one rule with one implementation rather than a property of
            // one particular exit.
            CarrySpeedIntoFreeFlight(VesselStatus != null ? VesselStatus.Speed : 0f);

            if (trailFollower) trailFollower.Detach();
            if (surfaceFollower) surfaceFollower.Detach();
            _rideMode = RideMode.None;
            _grindThrottle = 0f;

            // Hand free flight the attitude the ride actually left the hull in - during a ride
            // the slerp application keeps accumulatedRotation and the transform close, but a
            // residual gap on detach would fire as an uncommanded turn.
            accumulatedRotation = transform.rotation;
        }

        /// <summary>
        /// The gameplay camera is a SINGLE global rig, so only the machine-local pilot may
        /// move it. Gating on !AutoPilotEnabled alone was not enough: a REMOTE player's Urchin
        /// is not on autopilot either, so every attach/detach they made yanked the local
        /// player's camera in and out.
        /// </summary>
        bool IsLocalPilotCamera =>
            !VesselStatus.AutoPilotEnabled &&
            VesselStatus.Player != null && VesselStatus.Player.IsLocalPilot;

        /// <summary>
        /// True when the ride still has ground under it. A follower can drop its own
        /// attachment (TrailFollower.Detach from HandleOldestRemoved, a cleared trail) without
        /// touching VesselStatus, which would leave Slide() dereferencing a null trail; when
        /// that happens the vessel must fall back to free flight rather than freeze.
        /// </summary>
        bool RideHasGround()
        {
            switch (_rideMode)
            {
                // BlockscapeFollower exposes no IsAttached - its AttachedPrism IS its
                // attachment state, and it goes (fake-)null when the ground prism is
                // destroyed, which is exactly the case this guard exists to catch.
                case RideMode.Trail:   return trailFollower && trailFollower.IsAttached;
                case RideMode.Surface: return surfaceFollower && surfaceFollower.AttachedPrism;
                default:               return false;
            }
        }

        void Slide()
        {
            float dt = Time.deltaTime;
            float throttle = ReadThrottle();

            // The rail has WEIGHT: the grind speed chases the stick instead of being it, so
            // letting go coasts to a stop, a reversal swings through zero rather than
            // snapping, and a friendly->hostile prism transition (150 -> 10) reads as braking
            // instead of a 15x jolt. The 2D marble already rode on this and it is what made
            // the surface feel right; the rail wants the same.
            if (_rideMode == RideMode.Trail)
                _grindThrottle = Mathf.Lerp(_grindThrottle, throttle, 1f - Mathf.Exp(-trailInertiaRate * dt));
            else
                _grindThrottle = throttle;

            throttle = _rideMode == RideMode.Trail ? _grindThrottle : throttle;
            bool moving = Mathf.Abs(throttle) > throttleDeadband;

            if (_rideMode == RideMode.Trail)
            {
                // ---- The rail grind, restored to the shape the original shipped with
                // (GunShipController.Slide, "When attached move down the direction you are
                // looking"): the hull rides ON the trail and its ATTITUDE IS ENTIRELY THE
                // PILOT'S. Everything the ride imposed on top of that - a positional orbit at
                // a radius, and a per-frame twist dragging the hull's up onto the orbit radial
                // - is gone. Those were an over-literal reading of "roll should rotate them
                // around the trail": while riding, the hull's forward IS the rail, so an
                // ordinary Roll() already spins the pilot around it. The imposed twist instead
                // fought the stick every frame and, on a curving ribbon, swung the hull
                // bodily - which is what "it swung me around" was. ----

                // Which way is "forward"? The pilot's FACING against the ribbon's stable
                // index-order axis - the original's dot-product scheme. The axis never flips
                // with travel (unlike Course), so there is no feedback loop. TRUE hysteresis:
                // the mapping FLIPS only when the aim crosses well past broadside the other
                // way; anywhere inside the buffer the latched direction holds, so a bend
                // sweeping the axis under a steady nose cannot flap the ride.
                Vector3 axis = RibbonAxis();
                float facingDot = Vector3.Dot(transform.forward, axis);
                if (_facingSign > 0 ? facingDot < -facingFlipThreshold
                                    : facingDot > facingFlipThreshold)
                    _facingSign = -_facingSign;

                // Direction only re-latches while the smoothed throttle is meaningfully off
                // zero, which is what makes a reversal SWING THROUGH ZERO: the stick flips,
                // the grind coasts down, and the direction changes as it passes through the
                // deadband - not as an instant about-face at whatever speed you were doing.
                if (moving)
                {
                    int travelSign = throttle > 0f ? _facingSign : -_facingSign;
                    trailFollower.SetDirection(travelSign > 0
                        ? TrailFollowerDirection.Forward
                        : TrailFollowerDirection.Backward);
                }

                // ALWAYS tick the follower - it owns the ride's speed and therefore the coast.
                // Cutting the call at the deadband (as this did) made releasing the stick a
                // hard stop, which no rail grind should be.
                trailFollower.Throttle = Mathf.Abs(throttle);
                trailFollower.RideTheTrail();               // advances CenterlinePoint, writes Speed + Course

                // Out of rail: LAUNCH. The rest of this frame still runs on the ride (the
                // hull finishes on the terminal block with the pilot's own attitude), and the
                // next MoveShip sees the cleared flags, ends the ride and hands free flight the
                // speed the grind was doing. Course is whatever the last successful projection
                // wrote - the ribbon's own tangent - so the vessel leaves along the rail and
                // then flies its nose, which is what makes aiming the exit a real decision.
                if (trailFollower.ReachedEnd) LaunchOffRibbonEnd();

                // Attitude: EXACTLY free flight's. Roll, yaw and pitch all run untouched, so
                // the pilot aims and rolls while the rail carries them.
                Roll();
                Yaw();
                Pitch();

                // Position: on the prism's SURFACE, not buried in its middle. The only thing
                // between the hull and that surface point is the offset it had at the instant
                // of contact, decayed away - so the ride settles onto the trail rather than
                // snapping onto it.
                _railOffset = Vector3.Lerp(_railOffset, Vector3.zero, 1f - Mathf.Exp(-railSettleRate * dt));
                transform.position = trailFollower.CenterlinePoint + RideSurfaceOffset() + _railOffset;
            }
            else
            {
                // Rolling: the pilot keeps full steering - the surface constrains position,
                // not attitude. These are the same protected rotation passes free flight runs;
                // they accumulate into accumulatedRotation, which the ride applies below
                // exactly the way RotateShip applies it in free flight.
                Roll();
                Yaw();
                Pitch();

                // NO belly alignment. Easing the hull's up onto the surface normal was a
                // per-frame torque the pilot had to hold against - it restricted pitch and
                // roll to the plane and fought the camera, so you could not look and shoot
                // where you pleased. The SURFACE CONSTRAINS POSITION, NEVER ATTITUDE - the
                // same rule the 1D grind arrived at (round 11), for the same reason. Motion
                // still follows the plane, because the crawl direction is the steered forward
                // PROJECTED onto it; aim freely and you simply travel by the component that
                // lies along the surface.

                // SIGNED throttle (pull backs up along the surface), zeroed inside the
                // deadband. The follower ticks EVERY frame regardless: a marble released
                // mid-roll glides to rest through its inertia model - a hard stop here would
                // delete the momentum that makes the ride read as rolling.
                surfaceFollower.Throttle = moving ? throttle : 0f;
                surfaceFollower.RideTheTrail();             // writes Speed (momentum magnitude)
            }

            // Apply the ride attitude the same way free flight's RotateShip applies input
            // attitude. Slide() replaces base.MoveShip entirely, so without this the
            // accumulated rotation is a backlog nothing consumes - the hull never turns during
            // the ride, and detaching slams the whole backlog at once.
            transform.rotation = Quaternion.Slerp(transform.rotation, accumulatedRotation, LERP_AMOUNT * dt);

            // Keep the fleet's smoothed cruise field tracking the ride, so DETACHING hands
            // back a speed that matches what the pilot was doing rather than snapping to a
            // stale cruise. AdvanceSpeed, never ComputeThrottleTarget: AdvanceSpeed is the one
            // path every transformer's MoveShip runs through.
            AdvanceSpeed(VesselStatus.Speed);

            SlideActions();
        }

        /// <summary>
        /// Signed throttle in [-1, 1], zero at the stick's rest. XDiff is the dual-stick
        /// speed axis and lives in [0, 1] with rest at <see cref="throttleRestPosition"/>.
        /// </summary>
        float ReadThrottle()
        {
            if (InputStatus == null) return 0f;

            float span = Mathf.Max(0.01f, Mathf.Max(throttleRestPosition, 1f - throttleRestPosition));
            return Mathf.Clamp((InputStatus.XDiff - throttleRestPosition) / span, -1f, 1f);
        }

        void SlideActions()
        {
            var rs = VesselStatus.ResourceSystem;
            var prism = VesselStatus.AttachedPrism;
            if (rs == null || !prism) return;

            // ResourceSystem.ChangeResourceAmount indexes its list WITHOUT a bounds check and
            // this runs every frame of a ride - guard here; an absent meter is an authoring
            // gap on one prefab, not a condition the shared resource layer should tolerate.
            if (rs.Resources == null || ammoIndex < 0 || ammoIndex >= rs.Resources.Count) return;

            // Riding recharges ammo, and a SHIELDED prism pays double - the reward for having
            // reinforced your own prismscape before riding it.
            float rate = prism.prismProperties.IsShielded ? rechargeRate * 2f : rechargeRate;
            rs.ChangeResourceAmount(ammoIndex, rate * Time.deltaTime);
        }

        /// <summary>
        /// The ride's payoff, per prism visited, identical in every dimension: restore it if
        /// destroyed, GROW it if it is yours (Mass-scaled; shielded at Mass 5), STEAL it if it
        /// is not. Called by <see cref="TrailFollower"/> on block crossings (via
        /// <see cref="FinalBlockSlideEffects"/>) and by <see cref="BlockscapeFollower"/> on
        /// prism hops.
        /// </summary>
        public void ApplyPrismscapePayoff(Prism prism)
        {
            if (!prism) return;

            VesselStatus.AttachedPrism = prism;

            if (prism.destroyed) prism.Restore();

            if (prism.Domain == VesselStatus.Domain)
            {
                // EvaluateLive, not .Value: element scaling is read at USE time so a MASS
                // level that moved mid-ride is reflected on the very next prism.
                prism.Grow(growthAmount.EvaluateLive(VesselStatus));

                // MASS level-5 "Reinforced Wake": prisms you grow while riding come up
                // SHIELDED. Gated on IsUpgradeActive (the replicated unlock bit), never a raw
                // local level read - this changes the prismscape, and a local read desyncs it.
                var abilities = VesselStatus.ElementalAbilityHandler;
                if (abilities != null && abilities.IsUpgradeActive(Element.Mass))
                    prism.ActivateShield();
            }
            else
            {
                prism.Steal(VesselStatus.PlayerName, VesselStatus.Domain);
            }
        }

        /// <summary>Kept as <see cref="TrailFollower"/>'s callback surface.</summary>
        public void FinalBlockSlideEffects()
        {
            if (trailFollower) ApplyPrismscapePayoff(trailFollower.AttachedPrism);
        }
    }
}
