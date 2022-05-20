using UnityEngine;

namespace StarWriter.Core.Input
{
    public class InputController : MonoBehaviour
    {
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
        RectTransform IntensityRectTransform;

        [SerializeField]
        RectTransform FinalRectTransform;
        #endregion

        public float speed;

        private readonly float defaultThrottle = 5f;
        private readonly float rotationThrottleScaler = 3;
        private readonly float throttleScaler = 50;
        private readonly float rotationScaler = 130f;

        private readonly float OnThrottleEventThreshold = 1;

        private readonly float lerpAmount = .2f;
        private readonly float smallLerpAmount = .1f;

        private readonly float animationScaler = 25f;
        private readonly float yawAnimationScaler = 80f;

        private Gyroscope gyro;
        private Quaternion empiricalCorrection;
        private Quaternion displacementQ;


        
        private bool isCameraDisabled = false;

        private bool isPitchEnabled = true;
        private bool isYawEnabled = true;
        private bool isRollEnabled = true;
        private bool isThrottleEnabledl = true;
        private bool isGyroEnabled = true;

        public bool IsPitchEnabled { get => isPitchEnabled; set => isPitchEnabled = value; }
        public bool IsYawEnabled { get => isYawEnabled; set => isYawEnabled = value; }
        public bool IsRollEnabled { get => isRollEnabled; set => isRollEnabled = value; }
        public bool IsThrottleEnabledl { get => isThrottleEnabledl; set => isThrottleEnabledl = value; }
        public bool IsGyroEnabled { get => isGyroEnabled;  } //GameManager controls the gyro status

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
            GameManager.onToggleGyro += OnToggleGyro;
        }

        private void OnDisable()
        {
            IntensitySystem.gameOver -= OnGameOver;
            GameManager.onToggleGyro -= OnToggleGyro;
        }

        void Start()
        {
            cameraManager = CameraManager.Instance;
            //isCameraDisabled = false;

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
            if (PauseSystem.GetIsPaused())
            {
                // TODO: remove this check and verify pause still works
                return;
            }

            //change the camera if you flip your phone
            CameraFlip();

            //convert two finger touch into values for displacement, speed, and ship animations
            RecieveTouchInput();

            RotateShip();

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

        private void RotateShip()
        {

            if (SystemInfo.supportsGyroscope && isGyroEnabled == true)
            {
                // Updates GameObjects rotation from input device's gyroscope
                shipTransform.rotation = Quaternion.Lerp(
                                            shipTransform.rotation,
                                            displacementQ * GyroToUnity(gyro.attitude) * empiricalCorrection,
                                            lerpAmount);
            }
            else
            {
                shipTransform.rotation = Quaternion.Lerp(
                                            shipTransform.rotation,
                                            displacementQ,
                                            lerpAmount);
            }
        }

        private void CameraFlip()
        {
            if (UnityEngine.Input.acceleration.y > 0)
            {
                if (!isCameraDisabled)
                {
                    cameraManager.SetFarCameraActive();
                }

                IntensityRectTransform.rotation = Quaternion.Euler(0, 0, 180);
                FinalRectTransform.rotation = Quaternion.Euler(0, 0, 180);
                gameObject.GetComponent<TrailSpawner>().waitTime = .3f;
            }
            else
            {
                if (!isCameraDisabled)
                {
                    cameraManager.SetCloseCameraActive();
                }

                IntensityRectTransform.rotation = Quaternion.identity;
                FinalRectTransform.rotation = Quaternion.identity;
                gameObject.GetComponent<TrailSpawner>().waitTime = 1.5f;
            }
        }

        private void RecieveTouchInput()
        {
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

                // reparameterize
                float xSum = ((rightTouch.x + leftTouch.x) / (Screen.currentResolution.width) - 1);
                float ySum = ((rightTouch.y + leftTouch.y) / (Screen.currentResolution.height) - 1);
                float xDiff = (rightTouch.x - leftTouch.x) / (Screen.currentResolution.width);
                float yDiff = (rightTouch.y - leftTouch.y) / (Screen.currentResolution.width);

                if (isPitchEnabled) { Pitch(ySum); }
                if (isRollEnabled) { Roll(yDiff); }
                if(isYawEnabled) { Yaw(xSum); }
                if (isThrottleEnabledl) { Throttle(xDiff); }
                              
                PerformShipAnimations(xSum, ySum, xDiff, yDiff);

            }
            else
            {
                speed = Mathf.Lerp(speed, defaultThrottle, smallLerpAmount);
                LeftWing.localRotation = Quaternion.Lerp(LeftWing.localRotation, Quaternion.identity, smallLerpAmount);
                RightWing.localRotation = Quaternion.Lerp(RightWing.localRotation, Quaternion.identity, smallLerpAmount);
                Fusilage.localRotation = Quaternion.Lerp(Fusilage.localRotation, Quaternion.identity, smallLerpAmount);
            }
        }

        private void Throttle(float Xdiff)
        {
            speed = Mathf.Lerp(speed, Xdiff * throttleScaler + defaultThrottle, lerpAmount);
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

        private void OnGameOver()
        {
            isCameraDisabled = true; //Disables Cameras in Input Controller Update TODO: switch "disabled" to "enabled"
        }

        /// <summary>
        /// Gets gyros updated current status from GameManager.onToggleGyro Event
        /// </summary>
        /// <param name="status"></param>bool
        private void OnToggleGyro(bool status)
        {
            isGyroEnabled = status;
        }
    }    
}

