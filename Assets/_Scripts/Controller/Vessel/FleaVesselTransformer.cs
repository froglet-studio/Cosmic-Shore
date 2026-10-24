using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Flea's flight (design: <c>R_VesselActions/FLEA.md</c>; arithmetic: <see cref="FleaMath"/>, ported from the
    /// Vessel Studio's <c>ricochet</c> kind). It crawls on its own, and speed comes from BOUNCING off mass: a prism contact
    /// reflects the hull (<see cref="FleaBounceByPrismEffectSO"/> calls <see cref="Bounce"/>) instead of resetting it, and
    /// RT pressed inside half the window round the contact is a PERFECT bounce that gains speed. LT held crouches; its
    /// release hops along the nose.
    ///
    /// <para><b>The vector model owns the momentum, FleaMath owns its rules.</b> Grip and nose thrust are both switched
    /// off (<see cref="NoseConvergence"/> and <see cref="ComputeNoseAcceleration"/> return 0), and
    /// <see cref="ComputeExternalAcceleration"/> runs one <see cref="FleaMath.Step"/> and returns the change that puts the
    /// momentum at the step's <c>course × speed</c>. So the page's velocity grip (low = ballistic) is the only thing that
    /// turns the momentum toward the nose, exactly as on the page, while the platform's modifier channels (a danger
    /// prism's slow, knockback, warp) stay live.</para>
    ///
    /// <para>A bounce writes <c>speed</c> and <c>VesselStatus.Course</c>, the two external writes the base adopts at the
    /// top of the next frame (SyncExternalWrites), so the reflection reaches the momentum without a fork of MoveShip.</para>
    /// </summary>
    public class FleaVesselTransformer : VesselTransformer
    {
        [Header("Flea")]
        [Tooltip("The Flea's numbers (the Vessel Studio's ricochet kind): Assets/_SO_Assets/VesselActions/Flea/FleaConfig.asset.")]
        [SerializeField] FleaConfigSO config;

        FleaMath.State _state;
        bool _seeded;
        float _crouch, _press;
        Object _lastPrism;
        float _lastPrismTime = float.NegativeInfinity;

        /// <summary>The live state (speed, course, chain, hop charge, the press and bounce clocks), for the HUD.</summary>
        public FleaMath.State State => _state;
        public FleaConfigSO Config => config;
        /// <summary>The numbers this frame (the config's, or the studio's defaults with no config wired).</summary>
        public FleaMath.Params Params => config ? config.Params : FleaMath.Params.Studio;
        /// <summary>LT: crouch while held (charges the hop), hop on release. Set by <see cref="FleaActionSO"/>.</summary>
        public void SetCrouch(bool held) => _crouch = held ? 1f : 0f;
        /// <summary>RT: a press opens the perfect window. Set by <see cref="FleaActionSO"/>.</summary>
        public void SetBouncePress(bool held) => _press = held ? 1f : 0f;

        public override void ResetTransformer()
        {
            _seeded = false;
            _crouch = _press = 0f;
            _lastPrism = null;
            _lastPrismTime = float.NegativeInfinity;
            base.ResetTransformer();
        }

        protected override float NoseConvergence(float dt) => 0f;

        protected override float ComputeNoseAcceleration(float dt) => 0f;

        protected override Vector3 ComputeExternalAcceleration(Vector3 velocity, float dt)
        {
            float speedNow = velocity.magnitude;
            Vector3 course = speedNow > 1e-4f ? velocity / speedNow : transform.forward;
            if (!_seeded)
            {
                _state = FleaMath.State.Start(course, speedNow);
                _seeded = true;
            }
            // The momentum is the record (a swap's SetInitialSpeed, a bounce, the AI's course write all land in it first).
            _state.speed = speedNow;
            _state.course = course;

            float boost = VesselStatus != null && VesselStatus.IsBoosting ? Mathf.Max(1f, VesselStatus.BoostMultiplier) : 1f;
            FleaMath.Step(ref _state, Params, transform.forward, _crouch, _press, Time.time, boost, dt);
            return _state.course * _state.speed - velocity;
        }

        /// <summary>
        /// A hull contact with a prism (the page's <c>onContact</c>): reflect off the prism's axis, perfect or dull by the
        /// RT press, push the hull out along the normal and turn the nose most of the way onto the new course. One contact
        /// per prism per <see cref="FleaConfigSO.SamePrismGuard"/> seconds, so a trigger that reports the same prism twice
        /// (the box and the shell tier, or a stay) bounces once.
        /// </summary>
        /// <returns>True when the contact bounced.</returns>
        public bool Bounce(Transform prism)
        {
            if (!prism || VesselStatus == null) return false;
            float now = Time.time;
            float guard = config ? config.SamePrismGuard : 0.6f;
            if (prism == _lastPrism && now - _lastPrismTime < guard) return false;
            _lastPrism = prism;
            _lastPrismTime = now;

            if (!_seeded)
            {
                _state = FleaMath.State.Start(transform.forward, speed);
                _seeded = true;
            }
            Vector3 normal = FleaMath.BounceNormal(transform.position, prism.position, prism.forward, transform.up);
            FleaMath.Bounce(ref _state, Params, normal, now);

            speed = _state.speed;
            VesselStatus.Course = _state.course;
            transform.position += normal * FleaMath.BouncePush;
            GentleSpinShip(_state.course, transform.up, 0.8f);
            return true;
        }
    }
}
