using UnityEngine;


namespace CosmicShore.Game
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
            base.Vessel.VesselStatus.SingleStickControls = true;
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
            if (VesselStatus.Boosting) // TODO: if we run out of fuel while full speed and straight the vessel data still thinks we are boosting
                boostAmount = Vessel.VesselStatus.BoostMultiplier;

            if (VesselStatus.ChargedBoostDischarging)
                boostAmount *= VesselStatus.ChargedBoostCharge;

            speed = Mathf.Lerp(speed, ThrottleScaler * boostAmount + MinimumSpeed, LERP_AMOUNT * Time.deltaTime);

            speed *= throttleMultiplier;

            if (toggleManualThrottle)
                speed = Mathf.Lerp(0, speed, InputStatus.Throttle);

            VesselStatus.Speed = speed;

            transform.position += (speed * VesselStatus.Course + velocityShift) * Time.deltaTime;
        }
    }
}
