using UnityEngine;
using TMPro;

namespace StarWriter.Core.Input
{
    public class ShipGyroInputs : MonoBehaviour
    {
        [SerializeField]

        private Transform gyroTransform;
        public Transform shipTransform;

        [SerializeField]
        TextMeshProUGUI outputText;
        //[SerializeField]
        LineRenderer outputVector;

        public ShipController controller;

        //Quaternion displacementQ = Quaternion.identity;

        // these max angles are only used on mobile, due to the way pitch and roll input are handled
        public float maxRollAngle = 80;
        public float maxPitchAngle = 80;

        private float roll = 0f;
        private float pitch = 0f;

        bool airBrakes = false;
        Quaternion displacementQ;


        // Start is called before the first frame update
        void Start()
        {

            if (SystemInfo.supportsGyroscope)
            {

                UnityEngine.Input.gyro.enabled = true;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                outputText = gameObject.GetComponent<TextMeshProUGUI>();
                outputVector = gameObject.GetComponent<LineRenderer>();
                displacementQ = new Quaternion(0,0,0, -1);
                //displacementQ = Quaternion.AngleAxis(UnityEngine.Input.gyro.attitude.eulerAngles.y, Vector3.up);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (SystemInfo.supportsGyroscope)
            {
                //updates GameObjects rotation from input devices gyroscope
                
                gyroTransform.rotation = GyroToUnity(UnityEngine.Input.gyro.attitude) * GyroToUnity(Quaternion.Inverse(displacementQ));

                var gravity = UnityEngine.Input.gyro.gravity;
                var north = UnityEngine.Input.compass.magneticHeading;


                //display the gravity vector
                var points = new Vector3[2];
                points[0] = new Vector3(0, 0, 0);
                points[1] = gravity;
                outputVector.SetPositions(points);
               
            }
        }

        private void FixedUpdate()
        {

            airBrakes = UnityEngine.Input.GetButton("Fire1");

            // auto throttle up, or down if braking.
            float throttle = 1;// airBrakes ? -1 : 1;


            if (SystemInfo.supportsGyroscope)
            {
                Quaternion gyroRotation = gyroTransform.rotation;
                shipTransform.rotation = gyroRotation;

                roll = gyroRotation.eulerAngles.z;
                pitch = gyroRotation.eulerAngles.x;
                //roll = UnityEngine.Input.gyro.rotationRate.y;
                //pitch = UnityEngine.Input.gyro.rotationRate.x;



                // Read input for the pitch, yaw, roll and throttle of the spacecraft.
                AdjustInputForMobileControls(ref roll, ref pitch, ref throttle);

                UnityEngine.Input.gyro.enabled = true;
            }


            // Pass the input to the spacecraft
            controller.Move(0, 0, 0, throttle, airBrakes);
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
            roll = Mathf.Clamp((intendedRollAngle - controller.RollAngle), -1, 1);
            pitch = Mathf.Clamp((intendedPitchAngle - controller.PitchAngle), -1, 1);

            // similarly, the throttle axis input is considered to be the desired absolute value, not a relative change to current throttle.
            float intendedThrottle = throttle * 0.5f + 0.5f;
            throttle = Mathf.Clamp(intendedThrottle - controller.Throttle, -1, 1);
            Debug.Log("We have mobile inputs working!");
        }


        //Coverts Android and Mobile Device Quaterion into Unity Quaterion  TODO: Test
        private Quaternion GyroToUnity(Quaternion q)
        {
            return new Quaternion(q.x, -q.z, q.y, q.w);
        }

        public void SetGyroHome()
        {
            UnityEngine.Input.compass.enabled = true;
            displacementQ = UnityEngine.Input.gyro.attitude;
            outputText.text = displacementQ.x.ToString() + " , "
                            + displacementQ.y.ToString() + " , "
                            + displacementQ.z.ToString() + " , "
                            + displacementQ.w.ToString();
        }
    }
}
