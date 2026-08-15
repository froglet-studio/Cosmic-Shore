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
    ///  * 1D trail  → SLIDE along the ribbon (<see cref="TrailFollower"/>). Steering is the
    ///    trail's geometry; the pilot owns only a signed throttle - push to keep riding the
    ///    direction latched at attach, pull to back up - while the hull eases into line with
    ///    the ribbon (trail prisms are authored with Z parallel to the trail).
    ///  * 2D surface (and the boundary of a 3D volume, which is locally the same thing) →
    ///    ROLL across the aggregate surface (<see cref="BlockscapeFollower"/>). Gyroid and
    ///    Schwarz-P flora are the canonical case: their prisms are authored with Z orthogonal
    ///    to the surface, so the ride follows a smoothed plane over those normals and the
    ///    pilot keeps full steering while the belly eases onto the surface. The rider must
    ///    never feel a prism's edges or the gaps between prisms.
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

        [Tooltip("How quickly the hull turns to lie along the ribbon while sliding a trail " +
                 "(1/s, exponential). Trail prisms are authored with Z parallel to the trail; " +
                 "this eases the vessel's forward onto the travel heading.")]
        [SerializeField] float trailAlignRate = 4f;

        [Tooltip("How quickly the hull's belly eases onto the surface normal while rolling a " +
                 "2D prismscape (1/s, exponential). A minimal-twist correction on top of the " +
                 "pilot's steering, never a replacement for it.")]
        [SerializeField] float surfaceAlignRate = 3f;

        bool attached = false;
        CameraManager cameraManager;

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
                // Signed throttle maps onto the direction LATCHED at attach (the way the vessel
                // was flying when it touched the ribbon): push keeps going, pull backs up.
                // SetRideSign is idempotent, so stating it every frame can never flap the ride
                // - unlike deriving direction from dot(forward, Course), which oscillated the
                // moment the ribbon curved 90 degrees away from the (un-rotated) hull and
                // teleported the rider a block per flip.
                if (moving) trailFollower.SetRideSign(throttle > 0f ? 1 : -1);
                trailFollower.Throttle = Mathf.Abs(throttle);

                if (moving) trailFollower.RideTheTrail();   // writes Speed + Course
                else VesselStatus.Speed = 0f;

                // Lie along the ribbon: trail prisms are authored with Z parallel to the
                // trail, and Course is the live travel heading - ease the hull's forward onto
                // it (minimal twist, current up preserved).
                var course = VesselStatus.Course;
                if (course.sqrMagnitude > 1e-4f &&
                    SafeLookRotation.TryGet(course, transform.up, out var alongRibbon, this, logError: false))
                {
                    accumulatedRotation = Quaternion.Slerp(
                        accumulatedRotation, alongRibbon, 1f - Mathf.Exp(-trailAlignRate * dt));
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

                surfaceFollower.Throttle = throttle;        // SIGNED: pull backs up along the surface
                if (moving) surfaceFollower.RideTheTrail(); // writes Speed
                else VesselStatus.Speed = 0f;
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
