using UnityEngine;
using CosmicShore.Data;
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

        protected override void Pitch() // These need to not use *= because quaternions are not commutative
        {
            accumulatedRotation = Quaternion.AngleAxis(
                                -InputStatus.EasedLeftJoystickPosition.y * (speed * RotationThrottleScaler + PitchScaler) * Time.deltaTime,
                                courseTransform.right) * accumulatedRotation;
        }

        protected override void Yaw()
        {
            accumulatedRotation = Quaternion.AngleAxis(
                                InputStatus.EasedLeftJoystickPosition.x * (speed * RotationThrottleScaler + YawScaler) * Time.deltaTime,
                                courseTransform.up) * accumulatedRotation;
        }

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

        protected override void MoveShip()
        {
            float boostAmount = 1f;
            if (VesselStatus.IsBoosting) // TODO: if we run out of fuel while full speed and straight the vessel data still thinks we are boosting
                // TIME → boost speed: scaled by the vessel's live Time level via its
                // ElementalAbilityMapSO (1x for vessels without a map or Time entry).
                boostAmount = Vessel.VesselStatus.BoostMultiplier
                              * VesselStatus.ElementalAbilityHandler.Multiplier(Element.Time);

            if (VesselStatus.IsChargedBoostDischarging)
                boostAmount *= VesselStatus.ChargedBoostCharge;

            speed = Mathf.Lerp(speed, ThrottleScaler * boostAmount + MinimumSpeed, LERP_AMOUNT * Time.deltaTime);

            // Scale the output speed only - see VesselTransformer.MoveShip: multiplying
            // into the persistent smoothed `speed` field compounds per frame and
            // saturates every sub-1 modifier to a near-stop.
            float effectiveSpeed = speed * throttleMultiplier;

            if (toggleManualThrottle)
                effectiveSpeed = Mathf.Lerp(0, effectiveSpeed, InputStatus.Throttle);

            VesselStatus.Speed = effectiveSpeed;

            transform.position += (effectiveSpeed * VesselStatus.Course + velocityShift) * Time.deltaTime;
        }
    }
}
