using UnityEngine;
using TMPro;
using Cinemachine;

namespace StarWriter.Core.Input
{
    public class ShipGyroInputs : MonoBehaviour
    {
        public delegate void OnPitch();
        public static event OnPitch onPitch;

        public delegate void OnRoll();
        public static event OnRoll onRollEvent;

        public delegate void OnYaw();
        public static event OnYaw onYawEvent;

        public delegate void OnThrottle();
        public static event OnThrottle onThrottleEvent;

        public delegate void OnGyro();
        public static event OnGyro onGyroEvent;

        #region Camera 
        [SerializeField]
        CinemachineVirtualCameraBase cam1;

        [SerializeField]
        CinemachineVirtualCameraBase cam2;

        int activePriority = 10;
        int inactivePriority = 1;
        #endregion
        #region Ship
        [SerializeField]
        private Transform gyroTransform;

        public Transform shipTransform;

        [SerializeField]
        Transform Fusilage;

        [SerializeField]
        Transform LeftWing;

        [SerializeField]
        Transform RightWing;
        #endregion

        float touchScaler = .005f;

        private Gyroscope gyro;
        private Quaternion empiricalCorrection;

        private float throttle;
        float defaultThrottle = .3f;
        float lerpAmount = .2f;
        
 
        Quaternion displacementQ;

        private void Awake()
        {
            if (SystemInfo.supportsGyroscope)
            {
                gyro = UnityEngine.Input.gyro;
                empiricalCorrection = Quaternion.Inverse(new Quaternion(0, .65f, .75f, 0)); //TODO: move to derivedCoorection
            }
        }

        // Start is called before the first frame update
        void Start()
        {
            float throttle = defaultThrottle;
            if (SystemInfo.supportsGyroscope)
            {
                empiricalCorrection = GyroToUnity(empiricalCorrection);
                gyro.enabled = true;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                displacementQ = new Quaternion(0,0,0, -1);
            }
        }

        // Update is called once per frame
        void Update()
        {
            
            if (SystemInfo.supportsGyroscope)
            {
                //updates GameObjects rotation from input devices gyroscope

                gyroTransform.rotation = displacementQ * GyroToUnity(gyro.attitude) * empiricalCorrection;
            }

            if (SystemInfo.supportsGyroscope)
            {
                Quaternion gyroRotation = gyroTransform.rotation;
                shipTransform.rotation = Quaternion.Lerp(shipTransform.rotation, gyroRotation, lerpAmount);

                
            }
            
            

            if (UnityEngine.Input.touches.Length == 2)
            {
                if (UnityEngine.Input.touches[0].tapCount == 2 && UnityEngine.Input.touches[1].tapCount ==2) { ChangeCamera(); } 
                var yl = 0f;
                var yr = 0f;
                var xl = 0f;
                var xr = 0f;
                if (UnityEngine.Input.touches[0].position.x <= UnityEngine.Input.touches[1].position.x)
                {
                    yl = UnityEngine.Input.touches[0].position.y;
                    xl = UnityEngine.Input.touches[0].position.x;
                    yr = UnityEngine.Input.touches[1].position.y;
                    xr = UnityEngine.Input.touches[1].position.x;
                }
                else
                {
                    yl = UnityEngine.Input.touches[1].position.y;
                    xl = UnityEngine.Input.touches[1].position.x;
                    yr = UnityEngine.Input.touches[0].position.y;
                    xr = UnityEngine.Input.touches[0].position.x;
                }
                

                Pitch(yl, yr);
                Roll(yl, yr);
                Yaw(xl, xr);
                Throttle(xl, xr);


                ///delete once model is zeroed
                //LeftWing.localPosition += Vector3.down;
                //RightWing.localPosition += Vector3.down;
                //Fusilage.localPosition += Vector3.down*4f;

                PerformShipAnimations(yl, yr, xl, xr);

                ///delete once zeroed
                //LeftWing.localPosition +=  Vector3.up;
                //RightWing.localPosition += Vector3.up;
                //Fusilage.localPosition += Vector3.up;
            }
            else
            {
                throttle = Mathf.Lerp(throttle, defaultThrottle, .1f);
                LeftWing.localRotation = Quaternion.Lerp(LeftWing.localRotation, Quaternion.Euler(0, 0, 0), .1f);
                RightWing.localRotation = Quaternion.Lerp(RightWing.localRotation, Quaternion.Euler(0, 0, 0), .1f);
                Fusilage.localRotation = Quaternion.Lerp(Fusilage.localRotation,Quaternion.Euler(0, 0, 0),.1f);
            }

            //Move ship forward
            shipTransform.position += shipTransform.forward * throttle;

            

            
            //Quaternion.AngleAxis()
            
        }

        private void PerformShipAnimations(float yl, float yr, float xl, float xr)
        {
            ///ship animations
            LeftWing.localRotation = Quaternion.Lerp(LeftWing.localRotation, Quaternion.Euler(
                                                        (((yl + yr) - (Screen.currentResolution.height)) + (yr - yl)) * .02f,
                                                        0,
                                                        -(throttle - defaultThrottle) * 50
                                                            + ((xl + xr) - (Screen.currentResolution.width)) * .025f), lerpAmount);

            RightWing.localRotation = Quaternion.Lerp(RightWing.localRotation, Quaternion.Euler(
                                                        ((yl + yr) - (Screen.currentResolution.height) - (yr - yl)) * .02f,
                                                        0,
                                                        (throttle - defaultThrottle) * 50
                                                            + (((xl + xr)) - (Screen.currentResolution.width)) * .025f), lerpAmount);

            Fusilage.localRotation = Quaternion.Lerp(Fusilage.localRotation, Quaternion.Euler(
                                                        ((yl + yr) - (Screen.currentResolution.height)) * .02f,
                                                        0,
                                                        (((xl + xr)) - (Screen.currentResolution.width)) * .01f), lerpAmount);
        }

        private void Throttle(float xl, float xr)
        {
            //throttle
            throttle = Mathf.Lerp(throttle, (xr - xl) * touchScaler * .17f - .2f, .2f);
        }

        private void Yaw(float xl, float xr)
        {
            //yaw
            displacementQ = Quaternion.AngleAxis((((xl + xr) / 2) - (Screen.currentResolution.width / 2)) * touchScaler
                            , shipTransform.up) * displacementQ;
        }

        private void Roll(float yl, float yr)
        {
            //roll
            displacementQ = Quaternion.AngleAxis((yr - yl) * touchScaler
                            , shipTransform.forward) * displacementQ;
        }

        private void Pitch(float yl, float yr)
        {
            //pitch
            displacementQ = Quaternion.AngleAxis((((yl + yr) / 2) - (Screen.currentResolution.height / 2)) * -touchScaler
                            , shipTransform.right) * displacementQ;
        }


        //Coverts Android and Mobile Device Quaterion into Unity Quaterion  TODO: Test
        private Quaternion GyroToUnity(Quaternion q)
        {
            return new Quaternion(q.x, -q.z, q.y, q.w);
        }

        public void ChangeCamera()
        {

            if (cam2.Priority == activePriority)
            {
                cam1.Priority = activePriority;
                cam2.Priority = inactivePriority;
            }
            else
            {
                cam2.Priority = activePriority;
                cam1.Priority = inactivePriority;
            }
        }   
        
}
}
