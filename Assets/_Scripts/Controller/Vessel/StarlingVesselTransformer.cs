using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Starlings' "flight" (design: <c>R_VesselActions/STARLING.md</c>). The Starlings is a swarm, so there is nothing to fly: the
    /// hull is the VIEW, a carrier that sits on the swarm's centre and that the pilot turns with the left stick (the lab's
    /// <c>turnView</c>: yaw about the view's own up, pitch about its own right, stopping the moment the stick is let go).
    /// Its position eases onto <see cref="FollowTarget"/>, which <see cref="StarlingSwarmExecutor"/> writes every frame.
    ///
    /// <para>Its reported speed is always 0: the carrier must lay no trail (<see cref="VesselPrismController"/> lays only
    /// above 3 u/s) and must not drive the speed-tunnel FOV. A single-stick vessel, so the right stick is never read and
    /// the four buttons are the card picks.</para>
    /// </summary>
    public class StarlingVesselTransformer : SingleStickVesselTransformer
    {
        [Header("Starling view")]
        [Tooltip("Turn rate of the viewing plane at full stick (deg/s). StarlingConfigSO.viewTurnRate overrides it when the " +
                 "executor is wired; this is the fallback.")]
        [SerializeField, Min(1f)] float viewTurnRate = 90f;
        [Tooltip("How fast the view's centre eases onto the swarm (1/s). StarlingConfigSO.followRate overrides it.")]
        [SerializeField, Min(0.01f)] float followRate = 3f;

        /// <summary>Where the view's centre should be (the swarm's sampled centroid). Written by the executor only.</summary>
        public Vector3? FollowTarget { get; set; }

        /// <summary>Set by the executor from its config so the lab owns both numbers.</summary>
        public void Configure(float turnRate, float rate)
        {
            viewTurnRate = Mathf.Max(1f, turnRate);
            followRate = Mathf.Max(0.01f, rate);
        }

        protected override void RotateShip()
        {
            Vector2 stick = InputStatus != null ? InputStatus.EasedLeftJoystickPosition : Vector2.zero;
            float step = viewTurnRate * Time.deltaTime;
            Quaternion rotation = transform.rotation;
            if (Mathf.Abs(stick.x) > 1e-3f) rotation = Quaternion.AngleAxis(stick.x * step, rotation * Vector3.up) * rotation;
            if (Mathf.Abs(stick.y) > 1e-3f) rotation = Quaternion.AngleAxis(-stick.y * step, rotation * Vector3.right) * rotation;
            transform.rotation = rotation;
            accumulatedRotation = rotation;
            if (VesselStatus != null) VesselStatus.Course = transform.forward;
        }

        protected override float ComputeThrottleTarget() => 0f;

        protected override void MoveShip() => Follow();

        protected override void MoveRestricted() => Follow();

        void Follow()
        {
            if (VesselStatus != null) VesselStatus.Speed = 0f;
            if (FollowTarget is not { } target) return;
            float k = 1f - Mathf.Exp(-followRate * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target, k);
        }

        public override void ResetTransformer()
        {
            FollowTarget = null;
            base.ResetTransformer();
        }
    }
}
