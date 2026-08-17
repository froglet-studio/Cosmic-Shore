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
    ///    kernel). Trail prisms are authored with Z PARALLEL to the trail, and every control
    ///    maps onto that axis: throttle is signed speed along it (forward/reverse resolved
    ///    from the pilot's facing against the ribbon - the original Urchin's dot-product
    ///    scheme), ROLL carries the hull around it (an orbit at the attach radius), and
    ///    pitch/yaw stay free so the pilot can AIM while riding.
    ///  * 2D surface (and the boundary of a 3D volume, which is locally the same thing) →
    ///    ROLL across the aggregate surface, marble-madness style
    ///    (<see cref="BlockscapeFollower"/>). Gyroid and Schwarz-P prisms are authored with Z
    ///    ORTHOGONAL to the surface, so the ride follows a smoothed plane over those normals
    ///    with momentum, the belly eased onto the surface - and running off a sheet's edge
    ///    WRAPS around the rim onto the other side. The rider must never feel a prism's edges
    ///    or the gaps between prisms.
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

        [Tooltip("How fast roll input carries the hull AROUND the ribbon while riding a trail " +
                 "(degrees/second at full stick). The 1D ride's roll axis IS the trail.")]
        [SerializeField] float orbitDegreesPerSecond = 180f;

        [Tooltip("Minimum orbit radius around the trail centerline (world units). The attach " +
                 "distance is kept when larger, so you grind at the height you latched on.")]
        [SerializeField] float minOrbitRadius = 2.5f;

        [Tooltip("Ceiling on the grind radius. The seed keeps the distance you latched on at, " +
                 "which must still be bounded: a far attach - or a fork onto a ribbon several " +
                 "block-widths away - would otherwise swing the hull around the rail at that " +
                 "whole distance, which reads as being flung sideways rather than riding.")]
        [SerializeField] float maxOrbitRadius = 8f;

        [Tooltip("How quickly the hull's up twists to point radially OUT from the ribbon " +
                 "(1/s, exponential). Twist only - pitch/yaw stay the pilot's, for aiming.")]
        [SerializeField] float trailUpAlignRate = 4f;

        [Tooltip("Facing hysteresis: |dot(forward, ribbon axis)| must exceed this before the " +
                 "ride's forward/backward mapping re-latches. Prevents micro-flapping while " +
                 "the pilot aims near broadside.")]
        [SerializeField] float facingDeadband = 0.15f;

        [Header("Junctions")]
        [Tooltip("How far around the ridden block the ride looks for ANOTHER trail's prisms " +
                 "when it crosses a block (a junction probe), in WORLD UNITS. Deliberately not " +
                 "scaled by the ridden prism's size: a vessel whose block scale is dynamic " +
                 "(the Squirrel widens its blocks as it skims, up to ~40u) would otherwise " +
                 "sweep a radius of a hundred-plus units and fork onto anything in the arena.")]
        [SerializeField] float junctionSearchRadius = 12f;

        [Tooltip("How much better aligned (|dot| with the pilot's forward) a crossing trail " +
                 "must be before the ride forks onto it. Hysteresis - zero would flip-flop " +
                 "the rider at every junction it lingers near.")]
        [SerializeField] float junctionSwitchMargin = 0.1f;

        [Tooltip("A candidate this parallel to the ridden ribbon (|dot| of the two headings) " +
                 "is the SAME ROAD, not a fork - most importantly the vessel's own SECOND " +
                 "ribbon, which runs alongside the first for its whole length. A junction is " +
                 "a DIVERGENCE.")]
        [SerializeField, Range(0.5f, 0.99f)] float junctionParallelThreshold = 0.9f;

        [Tooltip("The grind radius eases toward minOrbitRadius at this rate (world units/s) - " +
                 "reels the rider in onto the rail, and matters most after a wide junction " +
                 "fork, where the new centerline starts several units away.")]
        [SerializeField] float orbitRadiusSettleRate = 1.5f;

        [Tooltip("How quickly the hull's belly eases onto the surface normal while rolling a " +
                 "2D prismscape (1/s, exponential). A minimal-twist correction on top of the " +
                 "pilot's steering, never a replacement for it.")]
        [SerializeField] float surfaceAlignRate = 3f;

        bool attached = false;
        CameraManager cameraManager;

        // ---- 1D orbit state: the hull grinds AROUND the ribbon at a radius. ----
        /// <summary>Unit radial from the centerline to the hull, kept perpendicular to the
        /// ribbon axis by parallel transport each frame (so curves don't kink the grind).</summary>
        Vector3 _orbitRadial = Vector3.up;
        float _orbitRadius;
        /// <summary>+1 when the nose agrees with <see cref="TrailFollower.IndexOrderHeading"/>.
        /// Re-latched only outside <see cref="facingDeadband"/> - hysteresis, so aiming near
        /// broadside cannot flap the throttle mapping.</summary>
        int _facingSign = 1;

        /// <summary>Set on each block crossing; the junction probe runs once per crossing,
        /// AFTER RideTheTrail returns (forking mid-walk would corrupt the walk's state).</summary>
        bool _junctionCheckQueued;

        // Main-thread only, like every QuerySphere consumer.
        static readonly System.Collections.Generic.List<Prism> s_junctionCandidates = new(32);

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
            // swap, ownership change) and a stale subscription would pay the previous pilot.
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

        protected override void MoveShip()
        {
            if (VesselStatus.IsAttached && !attached)
            {
                if (!TryBeginRide())
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

                    if (!VesselStatus.AutoPilotEnabled && cameraManager != null)
                    {
                        // Pull the camera in while riding: the prismscape is the thing to
                        // read, and the ride is close-quarters.
                        cameraManager.SetNormalizedCloseCameraDistance(1);
                    }
                }
            }
            else if (!VesselStatus.IsAttached && attached)
            {
                EndRide();
                if (!VesselStatus.AutoPilotEnabled && cameraManager != null)
                    cameraManager.SetNormalizedCloseCameraDistance(0);
            }

            attached = VesselStatus.IsAttached;

            if (attached && _rideMode != RideMode.None)
                Slide();
            else
                base.MoveShip();
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
            CSDebug.Log($"[GunVesselTransformer] Riding a {dimension} prismscape.");
            return true;
        }

        /// <summary>
        /// Seed the grind from where the hull actually stands: radius = distance from the
        /// centerline (floored at minOrbitRadius), radial = that offset made perpendicular to
        /// the ribbon axis, facing = the nose's current agreement with the index-order axis.
        /// Runs at ride begin AND at every junction fork - both are "a new rail from here".
        /// </summary>
        void SeedTrailRide()
        {
            Vector3 axis = trailFollower.IndexOrderHeading;
            Vector3 offset = transform.position - trailFollower.CenterlinePoint;
            Vector3 radial = offset - axis * Vector3.Dot(offset, axis);
            _orbitRadius = Mathf.Clamp(radial.magnitude, minOrbitRadius, maxOrbitRadius);
            _orbitRadial = radial.sqrMagnitude > 1e-4f ? radial.normalized : PerpendicularTo(axis);
            _facingSign = Vector3.Dot(transform.forward, axis) >= 0f ? 1 : -1;
        }

        /// <summary>
        /// The junction rule: when the ride crosses a block, look for ANOTHER trail passing
        /// nearby; if one runs MORE ALONG the pilot's facing than the current ribbon does
        /// (larger |dot|, beyond a hysteresis margin), fork onto it. This is what makes a
        /// wake that meets another trail into a real junction - ride to the meeting point
        /// aiming down the branch you want, and the ride takes it.
        /// </summary>
        void TryForkAtJunction()
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;

            var current = trailFollower.AttachedPrism;
            if (!current) return;
            var currentTrail = current.Trail;

            Vector3 axis = trailFollower.IndexOrderHeading;
            float currentAlign = Mathf.Abs(Vector3.Dot(transform.forward, axis));

            index.QuerySphere(trailFollower.CenterlinePoint, junctionSearchRadius, s_junctionCandidates);

            Prism best = null;
            float bestAlign = currentAlign + junctionSwitchMargin;
            for (int i = 0; i < s_junctionCandidates.Count; i++)
            {
                var candidate = s_junctionCandidates[i];
                if (!candidate || candidate.Trail == null || candidate.Trail == currentTrail) continue;
                if (candidate.Trail.Dimension != PrismscapeDimension.Trail) continue; // fork onto RIBBONS only

                Vector3 branch = candidate.Trail.HeadingAt(candidate.Trail.GetBlockIndex(candidate));
                if (branch.sqrMagnitude < 1e-6f) continue;

                // A junction is a DIVERGENCE. A ribbon running parallel to the one being
                // ridden is the same road - above all the vessel's OWN second ribbon, which
                // runs alongside the first for its entire length and is therefore a candidate
                // at every single block crossing. Without this test the ride ping-pongs
                // between a vessel's two wake ribbons (the Squirrel lays its pair 19u apart,
                // so each hop was a 19-unit sideways teleport onto a new centerline).
                if (Mathf.Abs(Vector3.Dot(branch, axis)) > junctionParallelThreshold) continue;

                float align = Mathf.Abs(Vector3.Dot(transform.forward, branch));
                if (align > bestAlign)
                {
                    bestAlign = align;
                    best = candidate;
                }
            }

            if (!best || !trailFollower.Attach(best)) return;
            SeedTrailRide();
        }

        void EndRide()
        {
            if (trailFollower) trailFollower.Detach();
            if (surfaceFollower) surfaceFollower.Detach();
            _rideMode = RideMode.None;

            // Hand free flight the attitude the ride actually left the hull in - during a ride
            // the slerp application keeps accumulatedRotation and the transform close, but a
            // residual gap on detach would fire as an uncommanded turn.
            accumulatedRotation = transform.rotation;
        }

        void Slide()
        {
            float throttle = ReadThrottle();
            bool moving = Mathf.Abs(throttle) > throttleDeadband;
            float dt = Time.deltaTime;

            if (_rideMode == RideMode.Trail)
            {
                // ---- The rail grind. Throttle = signed speed along the ribbon, ROLL = orbit
                // around it, pitch/yaw = free aim. Each control maps onto the 1D prismscape's
                // own geometry (trail prisms are authored with Z parallel to the trail). ----

                // Which way is "forward"? The pilot's FACING against the ribbon's stable
                // index-order axis - the original Urchin's scheme. The axis never flips with
                // travel (unlike Course), so there is no feedback loop; the hysteresis band
                // keeps an aim near broadside from flapping the mapping.
                Vector3 axis = trailFollower.IndexOrderHeading;
                float facingDot = Vector3.Dot(transform.forward, axis);
                if (Mathf.Abs(facingDot) > facingDeadband)
                    _facingSign = facingDot >= 0f ? 1 : -1;

                if (moving)
                {
                    int travelSign = throttle > 0f ? _facingSign : -_facingSign;
                    trailFollower.SetDirection(travelSign > 0
                        ? TrailFollowerDirection.Forward
                        : TrailFollowerDirection.Backward);
                    trailFollower.Throttle = Mathf.Abs(throttle);
                    trailFollower.RideTheTrail();           // advances CenterlinePoint, writes Speed + Course
                }
                else
                {
                    VesselStatus.Speed = 0f;
                }

                // Junction probe, once per block crossing, AFTER the walk (forking mid-walk
                // would corrupt the walk's own state).
                if (_junctionCheckQueued)
                {
                    _junctionCheckQueued = false;
                    TryForkAtJunction();
                    axis = trailFollower.IndexOrderHeading; // a fork changes the rail under us
                }

                // Reel the grind in toward the rail - imperceptible on a normal ride, and
                // what brings the rider home after a wide junction fork.
                _orbitRadius = Mathf.MoveTowards(_orbitRadius, minOrbitRadius, orbitRadiusSettleRate * dt);

                // Orbit: keep the radial perpendicular to the (curving) axis by parallel
                // transport, then let roll input carry it around the ribbon. The extra
                // _facingSign keeps the pilot's roll handedness: rolling right moves YOUR
                // right whichever way you face along the trail.
                _orbitRadial -= axis * Vector3.Dot(_orbitRadial, axis);
                _orbitRadial = _orbitRadial.sqrMagnitude > 1e-6f ? _orbitRadial.normalized : PerpendicularTo(axis);
                float rollInput = InputStatus != null ? InputStatus.YDiff : 0f;
                if (Mathf.Abs(rollInput) > 0.01f)
                    _orbitRadial = Quaternion.AngleAxis(
                        rollInput * orbitDegreesPerSecond * _facingSign * dt, axis) * _orbitRadial;

                transform.position = trailFollower.CenterlinePoint + _orbitRadial * _orbitRadius;

                // Attitude: the pilot AIMS - pitch and yaw run exactly as in free flight. The
                // roll input is consumed by the orbit above, so no Roll() pass; instead the
                // hull's up is twist-aligned radially OUT from the ribbon (forward preserved),
                // which is what sells "grinding around the rail".
                Pitch();
                Yaw();
                Vector3 steeredForward = accumulatedRotation * Vector3.forward;
                Vector3 targetUp = _orbitRadial - steeredForward * Vector3.Dot(_orbitRadial, steeredForward);
                if (targetUp.sqrMagnitude > 1e-6f)
                {
                    targetUp.Normalize();
                    Vector3 steeredUp = accumulatedRotation * Vector3.up;
                    Quaternion twist = Quaternion.FromToRotation(steeredUp, targetUp);
                    accumulatedRotation = Quaternion.Slerp(
                        Quaternion.identity, twist, 1f - Mathf.Exp(-trailUpAlignRate * dt)) * accumulatedRotation;
                }
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

                // Ease the belly onto the ridden surface: the minimal rotation taking the
                // steered attitude's up onto the smoothed normal, blended in gently ON TOP of
                // the pilot's steering so it reads as the surface holding the vessel, never as
                // the stick fighting back.
                var steeredUp = accumulatedRotation * Vector3.up;
                var belly = Quaternion.FromToRotation(steeredUp, surfaceFollower.SurfaceNormal);
                accumulatedRotation = Quaternion.Slerp(
                    Quaternion.identity, belly, 1f - Mathf.Exp(-surfaceAlignRate * dt)) * accumulatedRotation;

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

        /// <summary>Any unit vector perpendicular to <paramref name="axis"/> - the orbit
        /// radial's degenerate-case fallback.</summary>
        static Vector3 PerpendicularTo(Vector3 axis)
        {
            Vector3 p = Vector3.Cross(axis, Vector3.up);
            if (p.sqrMagnitude < 1e-4f) p = Vector3.Cross(axis, Vector3.right);
            return p.sqrMagnitude > 1e-6f ? p.normalized : Vector3.up;
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
            if (!trailFollower) return;
            ApplyPrismscapePayoff(trailFollower.AttachedPrism);
            _junctionCheckQueued = true;
        }
    }
}
