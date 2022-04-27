using UnityEngine;
using TMPro;
using Cinemachine;

namespace StarWriter.Core.Input
{
    public class ShipGyroInputs : MonoBehaviour
    {
        [SerializeField]

        private Transform gyroTransform;

        [SerializeField]

        private Transform lookAtTransform;

        [SerializeField]
        float lookAtOffset = 50;

        [SerializeField]
        CinemachineVirtualCameraBase cam1;

        [SerializeField]
        CinemachineVirtualCameraBase cam2;

        public Transform shipTransform;

        [SerializeField]
        TextMeshProUGUI outputText;
        //[SerializeField]
        LineRenderer outputVector;

        float touchScaler = .1f;

        public int controlScheme = 0;

        public ShipController controller;

        //Quaternion displacementQ = Quaternion.identity;

        // these max angles are only used on mobile, due to the way pitch and roll input are handled
        public float maxRollAngle = 80;
        public float maxPitchAngle = 80;

        private float roll = 0f;
        private float pitch = 0f;
        private Gyroscope gyro;
        private Compass compass;
        private Quaternion empericalCorrection;

        bool airBrakes = false;
        Quaternion displacementQ;

        private void Awake()
        {
            if (SystemInfo.supportsGyroscope)
            {
                gyro = UnityEngine.Input.gyro;
                compass = UnityEngine.Input.compass;
                empericalCorrection = Quaternion.Inverse(new Quaternion(0, .65f, .75f, 0)); //TODO: move to derivedCoorection
            }
        }

        // Start is called before the first frame update
        void Start()
        {
            if (SystemInfo.supportsGyroscope)
            {
                empericalCorrection = GyroToUnity(empericalCorrection);
                gyro.enabled = true;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                outputVector = gameObject.GetComponent<LineRenderer>();
                displacementQ = new Quaternion(0,0,0, -1);
                //displacementQ = Quaternion.AngleAxis(gyro.attitude.eulerAngles.y, Vector3.up);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (SystemInfo.supportsGyroscope)
            {
                //updates GameObjects rotation from input devices gyroscope

                gyroTransform.rotation = displacementQ * GyroToUnity(gyro.attitude)
                                       * empericalCorrection;
                                     

                var gravity = gyro.gravity;
                var north = compass.magneticHeading;

                //display the gravity vector
                var points = new Vector3[2];
                points[0] = new Vector3(0, 0, 0);
                points[1] = gravity;
                outputVector.SetPositions(points);
            }
        }

        private void FixedUpdate()
        {
            //airBrakes = UnityEngine.Input.GetButton("Fire1");

            // auto throttle up, or down if braking.
            float throttle = 1;// airBrakes ? -1 : 1;
            lookAtTransform.position = shipTransform.position + (shipTransform.forward * lookAtOffset);
            if (SystemInfo.supportsGyroscope)
            {
                Quaternion gyroRotation = gyroTransform.rotation;
                shipTransform.rotation = gyroRotation;
                

                for (int i = 0; i < UnityEngine.Input.touches.Length; i++) 
                {
                    switch (controlScheme)
                    {
                        case 0:
                            if (UnityEngine.Input.touches[i].position.x < Screen.currentResolution.width / 2)
                            {
                                //pitch
                                displacementQ = Quaternion.AngleAxis(UnityEngine.Input.touches[i].deltaPosition.y * touchScaler
                                                                    , shipTransform.right)
                                                * displacementQ;
                                //roll
                                displacementQ = Quaternion.AngleAxis(UnityEngine.Input.touches[i].deltaPosition.x * -touchScaler
                                                                    , shipTransform.forward)
                                                * displacementQ;
                            }
                            if (UnityEngine.Input.touches[i].position.x > Screen.currentResolution.width / 2)
                            {
                                //airBrakes = UnityEngine.Input.GetButton("Fire1");
                                if (UnityEngine.Input.touches[i].phase == TouchPhase.Began)
                                {
                                    if (cam2.m_Priority <= cam1.m_Priority) { cam2.m_Priority++;}
                                    else { cam2.m_Priority-=2; }
                                }
                                
                            }
                            break;
                        case 1:
                            if (UnityEngine.Input.touches[i].position.x < Screen.currentResolution.width / 2)
                            {
                                //pitch
                                displacementQ = Quaternion.AngleAxis(UnityEngine.Input.touches[i].deltaPosition.y * touchScaler
                                                                    , shipTransform.right)
                                                * displacementQ;
                                //roll
                                displacementQ = Quaternion.AngleAxis(UnityEngine.Input.touches[i].deltaPosition.x * -touchScaler
                                                                    , shipTransform.forward)
                                                * displacementQ;
                            }
                            if (UnityEngine.Input.touches[i].position.x > Screen.currentResolution.width / 2)
                            {
                                //pitch
                                displacementQ = Quaternion.AngleAxis(UnityEngine.Input.touches[i].deltaPosition.y * touchScaler
                                                                    , shipTransform.right)
                                                * displacementQ;
                                //yaw
                                displacementQ = Quaternion.AngleAxis(UnityEngine.Input.touches[i].deltaPosition.x * touchScaler
                                                                    , shipTransform.up)
                                                * displacementQ;
                            }
                            break;
                        //case 3:
                        //    var yl = 0f;
                        //    var yr = 0f;

                        //    if (UnityEngine.Input.touches[i].position.x < Screen.currentResolution.width / 2)
                        //    {
                        //        yl = UnityEngine.Input.touches[i].position.y;

                        //    }
                        //    if (UnityEngine.Input.touches[i].position.x > Screen.currentResolution.width / 2)
                        //    {
                        //        yr = UnityEngine.Input.touches[i].position.y;
                        //    }
                        //    //pitch
                        //    displacementQ = Quaternion.AngleAxis(((yl+yr)/2- Screen.currentResolution.height/2)
                        //                                            , shipTransform.right)
                        //    //roll
                        //                        * displacementQ;
                        //    displacementQ = Quaternion.AngleAxis((yl - yr)*100
                        //                                            , shipTransform.forward)
                        //                        * displacementQ;
                        //    break;
                    }
                    

                }
                //roll = gyroRotation.eulerAngles.z;
                //pitch = gyroRotation.eulerAngles.x;
                //roll = gyro.rotationRate.y;
                //pitch = gyro.rotationRate.x;

                // Read input for the pitch, yaw, roll and throttle of the spacecraft.
                AdjustInputForMobileControls(ref roll, ref pitch, ref throttle);

                gyro.enabled = true;
            }

            // Pass the input to the spacecraft
            controller.Move(0, 0, 0, throttle, airBrakes);  //currently passing static values for simple forward movement
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

        public void ChangeControls()
        {
            controlScheme = (controlScheme + 1) % 2;
            //compass.enabled = true;
            //displacementQ = Quaternion.AngleAxis(5, shipTransform.up)*displacementQ;
            ////displacementQ = new Quaternion(0,.65f,.75f,0);

            //outputText.text = displacementQ.x.ToString() + " , "
            //                + displacementQ.y.ToString() + " , "
            //                + displacementQ.z.ToString() + " , "
            //                + displacementQ.w.ToString();

        }
    }
}
