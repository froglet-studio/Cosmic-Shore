using UnityEngine;
using UnityEngine.UI;

namespace StarWriter.Core.Input
{
    public class GyroShipController : MonoBehaviour
    {
        //[System.Serializable]
        public Transform gyroTransform;
        public Transform shipTransform;

        public SpaceCraftController craft;

        Quaternion displacementQ = Quaternion.identity;

        // these max angles are only used on mobile, due to the way pitch and roll input are handled
        public float maxRollAngle = 80;
        public float maxPitchAngle = 80;

        private float roll = 0f; 
        private float pitch = 0f;

        bool airBrakes = false;



        // Start is called before the first frame update
        void Start()
        {

            if (SystemInfo.supportsGyroscope)
            {

                UnityEngine.Input.gyro.enabled = true;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (SystemInfo.supportsGyroscope)
            {
                //updates GameObjects rotation from input devices gyroscope
                gyroTransform.rotation = GyroToUnity(UnityEngine.Input.gyro.attitude * Quaternion.Inverse(displacementQ));

            }
        }

        private void FixedUpdate()
        {

            airBrakes = UnityEngine.Input.GetButton("Fire1");

            // auto throttle up, or down if braking.
            float throttle = airBrakes ? -1 : 1;

            
            if (SystemInfo.supportsGyroscope)
            {
                Quaternion gyroRotation = gyroTransform.rotation;

                roll = gyroRotation.eulerAngles.y;
                pitch = gyroRotation.eulerAngles.x;


                // Read input for the pitch, yaw, roll and throttle of the spacecraft.
                AdjustInputForMobileControls(ref roll, ref pitch, ref throttle);

                UnityEngine.Input.gyro.enabled = true;
            }
           

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
            Debug.Log("We have mobile inputs working!");
        }


        //Coverts Android and Mobile Device Quaterion into Unity Quaterion  TODO: Test
        private Quaternion GyroToUnity(Quaternion q)
        {
            return new Quaternion(q.x, q.y, -q.z, -q.w);
        }

        public void SetGyroHome()
        {
            displacementQ = UnityEngine.Input.gyro.attitude;
        }
    }
}
