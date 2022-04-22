using System;
using UnityEngine;

namespace Amoebius.Core.Input
{
    [RequireComponent(typeof(SpaceCraftController))]
    public class SpaceCraftControl2Axis : MonoBehaviour
    {
        // these max angles are only used on mobile, due to the way pitch and roll input are handled
        public float maxRollAngle = 80;
        public float maxPitchAngle = 80;

        private float roll = 0f;
        private float pitch = 0f;

        bool airBrakes = false;

        // reference to the spacecraft that we're controlling
        private SpaceCraftController craft;
        private Gyro gyro;


        private void Awake()
        {
            // Set up the reference to the aeroplane controller.
            craft = GetComponent<SpaceCraftController>();
            gyro = GetComponent<Gyro>();
        }


        private void FixedUpdate()
        {
            #region Keyboard Input
            if (!SystemInfo.supportsGyroscope)
            {
                // Read input for the pitch, yaw, roll and throttle of the spacecraft.
                roll = UnityEngine.Input.GetAxis("Horizontal"); 
                pitch = UnityEngine.Input.GetAxis("Vertical");
                
                UnityEngine.Input.gyro.enabled = false;
            }
            #endregion

            airBrakes = UnityEngine.Input.GetButton("Fire1");

            // auto throttle up, or down if braking.
            float throttle = airBrakes ? -1 : 1;

            #region Gyroscope Input
            if (SystemInfo.supportsGyroscope)
            {
                Quaternion gyroRotation = gyro.gyroTransform.rotation;

                Debug.Log(gyroRotation); //Testing Only

                roll = gyroRotation.eulerAngles.y;
                pitch = gyroRotation.eulerAngles.x;
             

                // Read input for the pitch, yaw, roll and throttle of the spacecraft.
                AdjustInputForMobileControls(ref roll, ref pitch, ref throttle);

                UnityEngine.Input.gyro.enabled = true;
            }
            #endregion

            // Pass the input to the spacecraft
            craft.Move(roll, pitch, 0, throttle, airBrakes);
        }


        private void AdjustInputForMobileControls(ref float roll, ref float pitch, ref float throttle)
        {
            // because mobile tilt is used for roll and pitch, we help out by
            // assuming that a centered level device means the user
            // wants to fly straight and level!

            // this means on mobile, the input represents the *desired* roll angle of the spacecraft,
            // and the roll input is calculated to achieve that.
            // whereas on non-mobile, the input directly controls the roll of the aeroplane.

            float intendedRollAngle = roll * maxRollAngle * Mathf.Deg2Rad;
            float intendedPitchAngle = pitch * maxPitchAngle * Mathf.Deg2Rad;
            roll = Mathf.Clamp((intendedRollAngle - craft.RollAngle), -1, 1);
            pitch = Mathf.Clamp((intendedPitchAngle - craft.PitchAngle), -1, 1);

            // similarly, the throttle axis input is considered to be the desired absolute value, not a relative change to current throttle.
            float intendedThrottle = throttle * 0.5f + 0.5f;
            throttle = Mathf.Clamp(intendedThrottle - craft.Throttle, -1, 1);
        }

    }
}
