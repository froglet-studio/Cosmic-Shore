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
        CinemachineVirtualCameraBase cam1;

        [SerializeField]
        CinemachineVirtualCameraBase cam2;

        int activePriority = 10;
        int inactivePriority = 1;

        public Transform shipTransform;

        [SerializeField]
        Transform Fusilage;

        [SerializeField]
        Transform LeftWing;

        [SerializeField]
        Transform RightWing;

        [SerializeField]
        TextMeshProUGUI outputText;

        float touchScaler = .005f;

        //public ShipController controller;

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


                //pitch
                displacementQ = Quaternion.AngleAxis((((yl + yr) / 2) - (Screen.currentResolution.height / 2)) * -touchScaler
                                , shipTransform.right) * displacementQ;
                //roll
                displacementQ = Quaternion.AngleAxis((yr - yl) * touchScaler
                                , shipTransform.forward) * displacementQ;

                //yaw
                displacementQ = Quaternion.AngleAxis((((xl + xr) / 2) - (Screen.currentResolution.width / 2)) * touchScaler
                                , shipTransform.up) * displacementQ;

                //throttle
                throttle = Mathf.Lerp(throttle, (xr - xl) * touchScaler*.2f-.15f,.2f);

      
                ///ship animations
                LeftWing.localRotation = Quaternion.Lerp(LeftWing.localRotation, Quaternion.Euler(
                                                            (((yl + yr) - (Screen.currentResolution.height)) + (yr - yl)) * .025f,
                                                            0,
                                                            -(throttle - defaultThrottle) * 50
                                                                + ((xl + xr) - (Screen.currentResolution.width)) * .025f), lerpAmount);

                RightWing.localRotation = Quaternion.Lerp(RightWing.localRotation, Quaternion.Euler(
                                                            ((yl + yr) - (Screen.currentResolution.height) - (yr - yl))  * .025f,
                                                            0,
                                                            (throttle - defaultThrottle) * 50
                                                                + (((xl + xr)) - (Screen.currentResolution.width)) * .025f), lerpAmount);

                Fusilage.localRotation = Quaternion.Lerp(Fusilage.localRotation, Quaternion.Euler(
                                                            ((yl + yr) - (Screen.currentResolution.height))*.018f,//fusilage scales by half so it lags the wings
                                                            0,
                                                            (((xl + xr)) - (Screen.currentResolution.width)) * .018f),lerpAmount);


                
                
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
