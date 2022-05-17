using UnityEngine;

namespace StarWriter.Core.Input
{
    public class InputController : MonoBehaviour
    {
        /*
        public delegate void OnPitch();
        public static event OnPitch OnPitch;

        public delegate void OnRoll();
        public static event OnRoll OnRollEvent;

        public delegate void OnYaw();
        public static event OnYaw OnYawEvent;

        public delegate void OnGyro();
        public static event OnGyro OnGyroEvent;
        */

        public delegate void OnThrottle();
        public static event OnThrottle OnThrottleEvent;

        #region Camera 

        CameraManager cameraManager;
       
        #endregion

        #region Ship
        [SerializeField] 
        Transform shipTransform;

        [SerializeField]
        Transform Fusilage;

        [SerializeField]
        Transform LeftWing;

        [SerializeField]
        Transform RightWing;
        #endregion

        #region UI
        [SerializeField]
        RectTransform UITransform;
        #endregion

        public float speed;

        private readonly float defaultThrottle = 5f;
        private readonly float rotationThrottleScaler = 3;
        private readonly float throttleScaler = 50;

        private readonly float OnThrottleEventThreshold = 1;

        private readonly float lerpAmount = .2f;
        private readonly float smallLerpAmount = .1f;

        private readonly float rotationScaler = 130f;
        
        private readonly float animationScaler = 25f;
        private readonly float yawAnimationScaler = 80f;

        private Gyroscope gyro;
        private Quaternion empiricalCorrection;
        private Quaternion displacementQ;


        public bool gyroEnabled = true;
        private bool isCameraDisabled = false;


        private void Awake()
        {
            if (SystemInfo.supportsGyroscope)
            {
                gyro = UnityEngine.Input.gyro;
                empiricalCorrection = Quaternion.Inverse(new Quaternion(0, .65f, .75f, 0)); // TODO: move to derivedCoorection
            }
        }

        private void OnEnable()
        {
            IntensitySystem.gameOver += OnGameOver;
            GameManager.onPlayGame += SetFarCameraActive;
        }

        private void OnDisable()
        {
            IntensitySystem.gameOver -= OnGameOver;
            GameManager.onPlayGame -= SetFarCameraActive;
        }

        void Start()
        {
            cameraManager = CameraManager.Instance;
            isCameraDisabled = false;

            if (SystemInfo.supportsGyroscope)
            {
                empiricalCorrection = GyroToUnity(empiricalCorrection);
                gyro.enabled = true;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                displacementQ = new Quaternion(0, 0, 0, -1);
            }
        }

        void Update()
        {
            // TODO: remove this check once movement is based on time.deltaTime
            if (PauseSystem.GetIsPaused())
            {
                return;
            }

            if (SystemInfo.supportsGyroscope && gyroEnabled==true)
            {
                // Updates GameObjects rotation from input device's gyroscope
                shipTransform.rotation = Quaternion.Lerp(
                                            shipTransform.rotation, 
                                            displacementQ * GyroToUnity(gyro.attitude) * empiricalCorrection, 
                                            lerpAmount);
            }

            //change the camera if you flip you phone
            if (UnityEngine.Input.acceleration.y > 0)
            {
                UITransform.rotation = Quaternion.Euler(0, 0, 180);
                if (!isCameraDisabled) { cameraManager.SetFarCameraActive(); }
                
                gameObject.GetComponent<TrailSpawner>().waitTime = .3f;
                
            }
            else
            {
                UITransform.rotation = Quaternion.identity;
                if (!isCameraDisabled)
                {
                    cameraManager.SetCloseCameraActive();
                }
               
                gameObject.GetComponent<TrailSpawner>().waitTime = 1.5f;
            }

            if (UnityEngine.Input.touches.Length == 2)
            {
                Vector2 leftTouch, rightTouch;

                if (UnityEngine.Input.touches[0].position.x <= UnityEngine.Input.touches[1].position.x)
                {
                    leftTouch = UnityEngine.Input.touches[0].position;
                    rightTouch = UnityEngine.Input.touches[1].position;
                }
                else
                {
                    leftTouch = UnityEngine.Input.touches[1].position;
                    rightTouch = UnityEngine.Input.touches[0].position;
                }
                //reparameterize
                float xSum = ((rightTouch.x + leftTouch.x) / (Screen.currentResolution.width) - 1);
                float ySum = ((rightTouch.y + leftTouch.y) / (Screen.currentResolution.height) - 1);
                float xDiff = (rightTouch.x - leftTouch.x) / (Screen.currentResolution.width);
                float yDiff = (rightTouch.y - leftTouch.y) / (Screen.currentResolution.width);

                Pitch(ySum);
                Roll(yDiff);
                Yaw(xSum);
                Throttle(xDiff);
                PerformShipAnimations(xSum, ySum, xDiff, yDiff);
            }
            else
            {
                speed = Mathf.Lerp(speed, defaultThrottle, smallLerpAmount);
                LeftWing.localRotation = Quaternion.Lerp(LeftWing.localRotation, Quaternion.identity, smallLerpAmount);
                RightWing.localRotation = Quaternion.Lerp(RightWing.localRotation, Quaternion.identity, smallLerpAmount);
                Fusilage.localRotation = Quaternion.Lerp(Fusilage.localRotation, Quaternion.identity, smallLerpAmount);
            }

            // Move ship forward
            shipTransform.position += speed * Time.deltaTime * shipTransform.forward;
            
        }

        private void PerformShipAnimations(float Xsum, float Ysum, float Xdiff, float Ydiff)
        {
            // Ship animations TODO: figure out how to leverage a single definition for pitch, etc. that captures the gyro in the animations.
            LeftWing.localRotation = Quaternion.Lerp(
                                        LeftWing.localRotation, 
                                        Quaternion.Euler(
                                            (Ydiff - Ysum) * animationScaler, //tilt based on pitch and roll
                                            0,
                                            -(Xdiff + Xsum) * yawAnimationScaler), //sweep back based on throttle and yaw
                                        lerpAmount);

            RightWing.localRotation = Quaternion.Lerp(
                                        RightWing.localRotation, 
                                        Quaternion.Euler(
                                            -(Ysum + Ydiff) * animationScaler, 
                                            0,
                                            (Xdiff - Xsum) * yawAnimationScaler), 
                                        lerpAmount);

            Fusilage.localRotation = Quaternion.Lerp(
                                        Fusilage.localRotation, 
                                        Quaternion.Euler(
                                            -Ysum * animationScaler,
                                            Ydiff* animationScaler,
                                            -Xsum * animationScaler),
                                        lerpAmount);
        }

        private void Throttle(float Xdiff)
        {
            speed = Mathf.Lerp(speed, Xdiff * throttleScaler + defaultThrottle, lerpAmount);

            if (speed > OnThrottleEventThreshold)
                OnThrottleEvent?.Invoke();
        }
        
        private void Yaw(float Xsum)  // These need to not use *= ... remember quaternions are not commutative
        {
            displacementQ = Quaternion.AngleAxis(
                                Xsum * (speed * rotationThrottleScaler + rotationScaler) *
                                    (Screen.currentResolution.width/Screen.currentResolution.height) * Time.deltaTime, 
                                shipTransform.up) * displacementQ;
        }

        private void Roll(float Ydiff)
        {
            displacementQ = Quaternion.AngleAxis(
                                Ydiff * (speed * rotationThrottleScaler + rotationScaler) * Time.deltaTime,
                                shipTransform.forward) * displacementQ;
        }

        private void Pitch(float Ysum)
        {
            displacementQ = Quaternion.AngleAxis(
                                Ysum * -(speed * rotationThrottleScaler + rotationScaler) * Time.deltaTime, 
                                shipTransform.right) * displacementQ;
        }

        //Converts Android Quaterions into Unity Quaterions
        private Quaternion GyroToUnity(Quaternion q)
        {
            return new Quaternion(q.x, -q.z, q.y, q.w);
        }

        private void SetFarCameraActive()
        {
            cameraManager.SetFarCameraActive();
        }
        private void OnGameOver()
        {
            isCameraDisabled = true; //Disables Cameras in Input Controller Update 
        }
    }    
}

