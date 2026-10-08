using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Tether's flight: the fleet's two-stick VECTOR model, bent by light tethers.
    ///
    /// No move step of its own. It inherits <see cref="VesselTransformer"/>'s vector model and
    /// reaches it only through the base class's default-off seams, so the danger-prism slow,
    /// knockback, the speed tunnel, external Course writes and every future fleet-wide change
    /// reach this hull unchanged — and no other hull runs a line of this file.
    ///
    /// <list type="bullet">
    /// <item><see cref="ComputeExternalAcceleration"/> — the tethers' pull (auto-tether springs,
    /// or the long tether's arc), from <see cref="TetherExecutor"/>.</item>
    /// <item><see cref="NoseConvergence"/> — 0 while the long tether holds (the rope owns the
    /// direction of travel); a finite grip while auto-tethers are taut, so their sideways pulls
    /// sway the hull before cancelling; the fleet default otherwise.</item>
    /// <item><see cref="ComputeNoseAcceleration"/> — off while hooked (speed is the rope's and the
    /// reel's), and the overspeed decay slowed for the glide after a release.</item>
    /// <item><see cref="ShapeSpeed"/> — the speed cap.</item>
    /// <item><see cref="PostVectorIntegrate"/> — puts the hull back on a rigid rope.</item>
    /// <item><see cref="AnalogTriggerDrift"/> — false: both triggers are the long tether.</item>
    /// </list>
    ///
    /// While hooked the pitch stick reels instead of pitching (stick up — the nose-up command —
    /// reels in, stick down lets out) and the nose follows the swing's tangent; ROLL stays live,
    /// because roll sets the search plane for the next hook.
    /// </summary>
    public class TetherVesselTransformer : VesselTransformer
    {
        [Header("Tether")]
        [Tooltip("The executor that owns both kinds of tether. Wired on the prefab (it lives on the " +
                 "ShipActions object, listed in the ActionExecutorRegistry).")]
        [SerializeField] TetherExecutor tether;

        TetherConfigSO Config => tether ? tether.Config : null;

        /// <summary>Frame this transformer last stepped the tethers. Only the machine that flies
        /// the hull steps them; the executor reads this to know whether to derive tension from
        /// geometry instead.</summary>
        public int LastSolveFrame { get; private set; } = -1;

        bool Hooked => tether && tether.LongHooked;

        /// <summary>Both triggers are the long tether, so this hull structurally cannot drift.
        /// Stated in code, not a prefab bool an inspector click could flip.</summary>
        protected override bool AnalogTriggerDrift => false;

        public override void ResetTransformer()
        {
            base.ResetTransformer();
            if (tether) tether.ResetLines();
        }

        protected override float NoseConvergence(float dt)
        {
            if (!tether) return base.NoseConvergence(dt);
            if (tether.LongHooked) return 0f;
            var config = Config;
            if (tether.AnyAutoTaut && config)
                return 1f - Mathf.Exp(-config.AutoNoseGrip * dt);
            return base.NoseConvergence(dt);
        }

        protected override float ComputeNoseAcceleration(float dt)
        {
            if (Hooked) return 0f;   // the rope and the reel own speed while hooked

            float accel = base.ComputeNoseAcceleration(dt);
            var config = Config;
            if (accel < 0f && config)   // shedding overspeed: slowed during the post-release glide
                accel *= TetherMath.OverspeedDecayScale(tether.SecondsSinceRelease, config.Glide, config.GlideDecayFactor);
            return accel;
        }

        protected override Vector3 ComputeExternalAcceleration(Vector3 velocity, float dt)
        {
            LastSolveFrame = Time.frameCount;
            return tether ? tether.SolveExternal(transform.position, velocity, CurrentThrottleTarget, dt) : Vector3.zero;
        }

        protected override float ShapeSpeed(float speedNow, float speedBeforeThrust, float dt)
        {
            float shaped = base.ShapeSpeed(speedNow, speedBeforeThrust, dt);
            var config = Config;
            return config ? Mathf.Min(shaped, config.MaxSpeed) : shaped;
        }

        protected override void PostVectorIntegrate(float dt)
        {
            if (Hooked) transform.position = tether.ConstrainToRope(transform.position);
        }

        protected override void RotateShip()
        {
            var config = Config;
            if (!Hooked || !config || VesselStatus == null)
            {
                base.RotateShip();
                return;
            }

            // Roll stays live — it is how the pilot sets the plane of the NEXT hook. Pitch is the
            // reel and yaw is the rope's, so neither rotates the hull here.
            Roll();

            Vector3 course = VesselStatus.Course;
            if (course.sqrMagnitude < 1e-6f) { base.RotateShip(); return; }
            Vector3 up = accumulatedRotation * Vector3.up;
            if (!SafeLookRotation.TryGet(course, up, out var target, this, logError: false)) return;

            accumulatedRotation = target;
            float follow = 1f - Mathf.Exp(-config.NoseFollowRate * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, follow);
        }

        /// <summary>
        /// The hook's redirect: momentum turned onto the swing's tangent, magnitude untouched.
        /// Owner only (this transformer only simulates there); every other peer sees it through
        /// replicated motion.
        /// </summary>
        public void RedirectOnto(Vector3 direction) => SetCourseVelocity(direction);

        /// <summary>
        /// The fling. Nose AND course snap to the exit direction and the speed is set to the exit
        /// speed in one step — otherwise grip would spend the next frames dragging momentum back
        /// toward a nose that still points along the old tangent, and eat the speed the swing paid.
        /// </summary>
        public void Fling(Vector3 direction, float exitSpeed)
        {
            if (direction.sqrMagnitude < 1e-6f) return;
            if (SafeLookRotation.TryGet(direction, transform.up, out var rotation, this, logError: false))
            {
                accumulatedRotation = rotation;
                transform.rotation = rotation;
            }
            SetCourseVelocity(direction);
            SetInitialSpeed(exitSpeed);
        }

        /// <summary>This transformer's own smoothed speed — the magnitude the vector model is
        /// integrating, before the impact-slow multiplier. What a release boost is applied to.</summary>
        public float FlightSpeed => speed;
    }
}
