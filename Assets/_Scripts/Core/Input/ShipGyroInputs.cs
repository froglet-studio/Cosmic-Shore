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
        float lookAtOffset = 200;

        [SerializeField]
        CinemachineVirtualCameraBase cam1;

        [SerializeField]
        CinemachineVirtualCameraBase cam2;

        int activePriority = 10;
        int inactivePriority = 1;

        public Transform shipTransform;

        [SerializeField]
        TextMeshProUGUI outputText;

        float touchScaler = .005f;

        //public ShipController controller;

        private Gyroscope gyro;
        private Quaternion empiricalCorrection;

 
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
            float throttle = .3f;
            lookAtTransform.position = shipTransform.position + (shipTransform.forward * lookAtOffset);
            if (SystemInfo.supportsGyroscope)
            {
                Quaternion gyroRotation = gyroTransform.rotation;
                shipTransform.rotation = Quaternion.Lerp(gyroRotation, shipTransform.rotation, .8f); //lerp is dampening the jitter from gyro sensitivity

                var yl = 0f;
                var yr = 0f;
                var xl = 0f;
                var xr = 0f;

                if (UnityEngine.Input.touches.Length == 2)
                {
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
                    throttle = (xr - xl) * touchScaler*.15f-.2f;
                }

            }

            //Move ship forward
            shipTransform.position += shipTransform.forward * throttle;
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
