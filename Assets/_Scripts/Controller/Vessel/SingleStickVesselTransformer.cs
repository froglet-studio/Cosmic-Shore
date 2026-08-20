using UnityEngine;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    public class SingleStickVesselTransformer : VesselTransformer
    {
        Quaternion additionalRotation = Quaternion.identity;
        GameObject courseObject;
        Transform courseTransform;

        public override void Initialize(IVessel vessel)
        {
            courseObject = new GameObject("CourseObject");
            courseTransform = courseObject.transform;
            
            base.Initialize(vessel);
            Vessel.VesselStatus.IsSingleStickControls = true;
        }

        // TurnScalar triples pitch/yaw while IsTranslationRestricted (see VesselTransformer.
        // restrictedTurnMultiplier). It has to be applied HERE as well as on the base methods:
        // the Sparrow and the Serpent — the two vessels with a stationary stance — both run this
        // transformer, so these overrides are the only pitch/yaw either of them ever executes.
        protected override void Pitch() // These need to not use *= because quaternions are not commutative
        {
            accumulatedRotation = Quaternion.AngleAxis(
                                -InputStatus.EasedLeftJoystickPosition.y * (speed * RotationThrottleScaler + PitchScaler) * TurnScalar * Time.deltaTime,
                                courseTransform.right) * accumulatedRotation;
        }

        protected override void Yaw()
        {
            accumulatedRotation = Quaternion.AngleAxis(
                                InputStatus.EasedLeftJoystickPosition.x * (speed * RotationThrottleScaler + YawScaler) * TurnScalar * Time.deltaTime,
                                courseTransform.up) * accumulatedRotation;
        }

        // Deliberately NOT scaled by TurnScalar: this is the bank INTO the turn, not a turn rate,
        // and it shares the yaw axis of the stick. Tripling it would roll the vessel three times
        // as far off level for the same stick push. Expect the stopped turn to read as flatter
        // than a flying one — that is the trade, and RollScaler is the knob if it wants a nudge.
        protected override void Roll()
        {
            accumulatedRotation = Quaternion.AngleAxis(
                                -InputStatus.EasedLeftJoystickPosition.x * (speed * RotationThrottleScaler + RollScaler) * Time.deltaTime, //use roll scaler to adjust the banking into turns
                                transform.forward) * accumulatedRotation;
        }

        protected override void RotateShip()
        {
            Roll();
            Yaw();
            Pitch();

            transform.rotation = Quaternion.Slerp(transform.rotation, accumulatedRotation, LERP_AMOUNT * Time.deltaTime);
            courseTransform = transform;
            VesselStatus.Course = courseTransform.forward;
        }

        // Single-stick has no analog throttle: full throttle is implicit, so the target
        // ignores XDiff / the throttle-scaler multiplier.
        protected override float ComputeThrottleTarget()
            => ThrottleScaler * CurrentBoostAmount() + MinimumSpeed;

        protected override void MoveShip()
        {
            AdvanceSpeed(ComputeThrottleTarget());

            // Scale the output speed only - see VesselTransformer.MoveShip: multiplying
            // into the persistent smoothed `speed` field compounds per frame and
            // saturates every sub-1 modifier to a near-stop.
            float effectiveSpeed = speed * throttleMultiplier;

            // See VesselTransformer.MoveShip: a held drift silences the manual-throttle channel
            // too, since its value at capture time is already inside the held speed.
            if (toggleManualThrottle && !IsDriftSpeedHeld)
                effectiveSpeed = Mathf.Lerp(0, effectiveSpeed, InputStatus.Throttle);

            VesselStatus.Speed = effectiveSpeed;

            transform.position += (effectiveSpeed * VesselStatus.Course + velocityShift) * Time.deltaTime;
        }
    }
}
