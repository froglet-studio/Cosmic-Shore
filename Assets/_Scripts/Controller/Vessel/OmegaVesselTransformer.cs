using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Omega crews' flight: the hull's two-stick transformer, whose cruise and turn rates are SWITCHED LIVE by
    /// <see cref="OmegaCrew"/> as the hull changes form: the member the person is seated in apart (its page cruise, turn
    /// and roll), the crew's omega flight combined (<see cref="OmegaMath.OmegaFlight"/>). A member's RT move that is a
    /// boost multiplies the cruise through <see cref="SetExtraBoost"/>, beside the platform's own boost channel, so a
    /// danger prism's slow, knockback and the elemental multipliers stay live. Design: <c>Crew/OMEGA_CREW.md</c>.
    /// </summary>
    public class OmegaVesselTransformer : VesselTransformer
    {
        OmegaMath.Flight _form;
        bool _hasForm, _authoredCached;
        float _authoredPitch, _authoredYaw, _authoredRoll, _authoredThrottle;
        float _extraBoost = 1f;

        /// <summary>The hull's live speed (u/s), for the crew's simulated members and the HUD.</summary>
        public float Speed => speed;
        public bool HasForm => _hasForm;
        public OmegaMath.Flight Form => _form;
        public float ExtraBoost => _extraBoost;

        /// <summary>Fly as this form from now on (a member apart, the omega combined).</summary>
        public void SetForm(in OmegaMath.Flight form)
        {
            CacheAuthored();
            _form = form;
            _hasForm = true;
            Apply();
        }

        /// <summary>Back to the prefab's own numbers (the crew unbound).</summary>
        public void ClearForm()
        {
            _hasForm = false;
            _extraBoost = 1f;
            if (!_authoredCached) return;
            ThrottleScaler = _authoredThrottle;
            PitchScaler = _authoredPitch;
            YawScaler = _authoredYaw;
            RollScaler = _authoredRoll;
        }

        /// <summary>A member's boost burst: the cruise is multiplied by this (1 = none) until the crew lowers it.</summary>
        public void SetExtraBoost(float boost) => _extraBoost = Mathf.Max(1f, boost);

        /// <summary>A seat swap or a split moves the hull: pose, course and speed in one write (the base adopts the
        /// external course and speed writes at the top of its next frame).</summary>
        public void Teleport(Vector3 position, Quaternion rotation, Vector3 course, float newSpeed)
        {
            transform.SetPositionAndRotation(position, rotation);
            speed = Mathf.Max(0f, newSpeed);
            if (VesselStatus != null && course.sqrMagnitude > 1e-6f) VesselStatus.Course = course.normalized;
        }

        public override void ResetTransformer()
        {
            base.ResetTransformer();
            _extraBoost = 1f;
            Apply();
        }

        protected override float ComputeThrottleTarget()
        {
            float target = base.ComputeThrottleTarget();
            return (target - MinimumSpeed) * _extraBoost + MinimumSpeed;
        }

        void CacheAuthored()
        {
            if (_authoredCached) return;
            _authoredCached = true;
            _authoredThrottle = DefaultThrottleScaler;
            _authoredPitch = PitchScaler;
            _authoredYaw = YawScaler;
            _authoredRoll = RollScaler;
        }

        void Apply()
        {
            if (!_hasForm) return;
            ThrottleScaler = _form.Cruise;
            PitchScaler = _form.Turn;
            YawScaler = _form.Turn;
            RollScaler = _form.Roll;
        }
    }
}
