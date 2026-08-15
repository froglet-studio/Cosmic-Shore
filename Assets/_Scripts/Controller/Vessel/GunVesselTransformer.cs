using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Urchin's flight model: ordinary free flight until it latches onto a trail, then a
    /// RIDE along that trail's prisms until it lets go.
    ///
    /// Attaching is two flags and no reparenting — <c>VesselAttachPrismEffectSO</c> sets
    /// <see cref="IVesselStatus.IsAttached"/> and <c>.AttachedPrism</c> on contact, this
    /// transformer edge-detects them, and while attached <see cref="Slide"/> replaces
    /// <c>base.MoveShip()</c> entirely. Steering is then the trail's geometry rather than the
    /// pilot's stick: the only input consumed is throttle magnitude, plus a look-backwards
    /// gesture that reverses direction along the ribbon.
    ///
    /// Crossing from one prism to the next fires <see cref="FinalBlockSlideEffects"/>, which is
    /// where the ride pays: your own trail GROWS under you, an enemy's is STOLEN as you pass
    /// over it, and a destroyed prism is restored. That is the whole of the original "Trail
    /// Rider" ability — "steal enemy trails or grow and shield your own, zipping through trails
    /// is how to get around fast and gain ammo too."
    /// </summary>
    public class GunVesselTransformer : VesselTransformer
    {
        /// <summary>
        /// The ride kernel. This is <see cref="TrailFollower"/> — which projects ALONG a trail
        /// from a block index and supports reversing — and NOT <c>BlockscapeFollower</c>, which
        /// crawls over prism FACES and is a separate, unfinished experiment.
        ///
        /// The field was pointed at the blockscape variant at some point during the vessel-layer
        /// port, which broke the ride silently and in a way that looks fine: both types expose
        /// <c>Attach</c>/<c>Detach</c>/<c>RideTheTrail</c>/<c>Throttle</c>/<c>AttachedPrism</c>,
        /// so it compiled — but only <see cref="TrailFollower"/> calls
        /// <see cref="FinalBlockSlideEffects"/> back, so the grow/steal payoff never ran.
        /// </summary>
        TrailFollower trailFollower;

        [Tooltip("Ammo gained per second while riding a trail. Doubled on a shielded prism.")]
        [SerializeField] float rechargeRate = .1f;

        public float ProjectileScale = 1f;
        public Vector3 BlockScale = new(4f, 4f, 1f);

        [Tooltip("How much volume a friendly prism gains as you ride over it. Scaled by MASS " +
                 "- the Urchin's Trail Rider ability.")]
        [SerializeField] ElementalFloat growthAmount = new ElementalFloat(1);

        [Tooltip("Throttle below this magnitude is treated as stationary. TrailFollower " +
                 "divides its inter-prism distance by throttle*speed, so a literal zero is an " +
                 "infinite time-to-next-block: the loop never runs and the rider parks.")]
        [SerializeField] float throttleDeadband = 0.05f;

        [Tooltip("Look back past this dot product (forward vs course) while on the throttle " +
                 "to reverse along the trail.")]
        [SerializeField] float reverseLookThreshold = -0.6f;

        [Tooltip("Stick centre. Throttle is remapped from this to 1 so a resting stick reads " +
                 "as zero rather than as a permanent crawl.")]
        [SerializeField] float throttleZeroPosition = 0.2f;

        bool moveForward = true;
        bool attached = false;
        CameraManager cameraManager;

        [SerializeField] int ammoIndex = 0;

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            cameraManager = CameraManager.Instance;
            trailFollower = GetComponent<TrailFollower>();

            if (!trailFollower)
                CSDebug.LogError(
                    $"{name}: GunVesselTransformer needs a TrailFollower on the same GameObject - " +
                    "without it the vessel can attach to a trail but never move along it.");
        }

        protected override void MoveShip()
        {
            switch (VesselStatus.IsAttached)
            {
                case true when !attached:
                {
                    // A refused attach (the prism has no trail, or is not a member of the one
                    // it names) must release the flag, or the vessel is stuck in ride mode with
                    // no trail under it and free flight never resumes.
                    if (!trailFollower || !VesselStatus.AttachedPrism ||
                        !trailFollower.Attach(VesselStatus.AttachedPrism))
                    {
                        VesselStatus.IsAttached = false;
                        VesselStatus.AttachedPrism = null;
                        break;
                    }

                    // Pull the camera in while riding: the trail is the thing to read, and the
                    // ride is close-quarters. (Gated on being the pilot, not on autopilot -
                    // the previous form had this inverted and pulled the camera in only for
                    // AI, which nobody is watching.)
                    if (!VesselStatus.AutoPilotEnabled && cameraManager != null)
                        cameraManager.SetNormalizedCloseCameraDistance(1);

                    break;
                }
                case false when attached:
                {
                    if (trailFollower) trailFollower.Detach();

                    if (!VesselStatus.AutoPilotEnabled && cameraManager != null)
                        cameraManager.SetNormalizedCloseCameraDistance(0);

                    break;
                }
            }

            attached = VesselStatus.IsAttached;

            if (attached && trailFollower)
                Slide();
            else
                base.MoveShip();
        }

        /// <summary>
        /// One frame of riding. Reads throttle, resolves direction along the ribbon, hands both
        /// to the follower, and then feeds the resulting speed back into the fleet's smoothed
        /// cruise field.
        /// </summary>
        void Slide()
        {
            float throttle = ReadThrottle();

            // Look back over your shoulder while on the throttle to reverse along the trail.
            if (Vector3.Dot(transform.forward, VesselStatus.Course) < reverseLookThreshold &&
                Mathf.Abs(throttle) > throttleDeadband)
                moveForward = !moveForward;

            trailFollower.SetDirection(moveForward
                ? TrailFollowerDirection.Forward
                : TrailFollowerDirection.Backward);

            trailFollower.Throttle = Mathf.Abs(throttle);

            if (trailFollower.Throttle > throttleDeadband)
                trailFollower.RideTheTrail();   // writes VesselStatus.Speed and .Course
            else
                VesselStatus.Speed = 0f;

            // Keep the fleet's smoothed cruise field tracking the ride, so DETACHING hands back
            // a speed that matches what the pilot was doing rather than snapping to whatever
            // they were carrying when they latched on - possibly minutes earlier.
            //
            // AdvanceSpeed, never ComputeThrottleTarget: AdvanceSpeed is the one path every
            // transformer's MoveShip runs through, so this stays correct for any subclass that
            // overrides the target.
            AdvanceSpeed(VesselStatus.Speed);

            SlideActions();
        }

        /// <summary>
        /// Throttle for the ride, remapped so the stick's rest position reads as zero.
        ///
        /// This was hardcoded to <c>0</c> during the port ("TODO - Vessel components should not
        /// be accessing InputStatus directly"), which silently made the whole ability inert:
        /// the vessel would attach, and then sit motionless on the trail forever.
        /// </summary>
        float ReadThrottle()
        {
            if (InputStatus == null) return 0f;

            float raw = InputStatus.XDiff;
            float span = 1f - throttleZeroPosition;
            if (span <= Mathf.Epsilon) return Mathf.Clamp(raw, -1f, 1f);

            return Mathf.Clamp((raw - throttleZeroPosition) / span, -1f, 1f);
        }

        void SlideActions()
        {
            var rs = VesselStatus.ResourceSystem;
            if (rs == null || !trailFollower.AttachedPrism) return;

            // ResourceSystem.ChangeResourceAmount indexes its list WITHOUT a bounds check, and
            // this runs every frame of a ride - so a vessel whose Resources list is short (or,
            // as the Urchin's prefab shipped for years, empty) throws an
            // ArgumentOutOfRangeException on every single frame the pilot is attached. Guard
            // here rather than in ResourceSystem: the meter being absent is an authoring gap
            // on one prefab, not a condition the shared resource layer should start tolerating.
            if (rs.Resources == null || ammoIndex < 0 || ammoIndex >= rs.Resources.Count) return;

            // Riding recharges ammo, and a SHIELDED prism pays double - the reward for having
            // reinforced your own trail before riding it.
            float rate = trailFollower.AttachedPrism.prismProperties.IsShielded
                ? rechargeRate * 2f
                : rechargeRate;

            rs.ChangeResourceAmount(ammoIndex, rate * Time.deltaTime);
        }

        /// <summary>
        /// Called by <see cref="TrailFollower"/> the moment the rider crosses onto a new prism.
        /// This is where the ride pays for itself.
        /// </summary>
        public void FinalBlockSlideEffects()
        {
            if (!trailFollower || !trailFollower.AttachedPrism) return;

            VesselStatus.AttachedPrism = trailFollower.AttachedPrism;

            if (VesselStatus.AttachedPrism.destroyed)
                VesselStatus.AttachedPrism.Restore();

            if (VesselStatus.AttachedPrism.Domain == VesselStatus.Domain)
            {
                // EvaluateLive, not .Value: element scaling is read at USE time so a MASS level
                // that moved mid-ride (a crystal, a comeback buff) is reflected on the very next
                // prism. `.Value` is the serialized default and only updates through a binding
                // this component never registers.
                VesselStatus.AttachedPrism.Grow(growthAmount.EvaluateLive(VesselStatus));

                // MASS level-5 "Reinforced Wake": the prisms you grow while riding come up
                // SHIELDED, so a lap of your own trail is also a lap of fortification - and,
                // because riding a shielded prism pays double ammo, the reward compounds on the
                // next lap.
                //
                // Gated on IsUpgradeActive (the replicated unlock bit), never on a raw local
                // level read: this changes the prismscape, and a local read desyncs it.
                var abilities = VesselStatus.ElementalAbilityHandler;
                if (abilities != null && abilities.IsUpgradeActive(Element.Mass))
                    VesselStatus.AttachedPrism.ActivateShield();
            }
            else
            {
                VesselStatus.AttachedPrism.Steal(VesselStatus.PlayerName, VesselStatus.Domain);
            }
        }
    }
}
